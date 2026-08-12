using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Maps;
using PokeMmo.RomExtract.Scripts;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The client: reads the player's own cartridge, draws one map, and lets you walk
/// around it.
/// <para>
/// There is no engine and no editor — a window, a texture and an input poll. Loading,
/// rendering and movement all live in tested libraries; this file is the part that
/// cannot be tested without a screen, so it is kept as small as possible.
/// </para>
/// </summary>
public static class Program
{
    private const int WindowWidth = 960;
    private const int WindowHeight = 640;
    private const float ViewZoom = 3f;

    public static int Main(string[] args)
    {
        // The working directory, not the build output, so the file sits alongside the
        // repository where .gitignore already covers it.
        string directory = Directory.GetCurrentDirectory();
        ClientSettings.WriteTemplate(directory);
        ClientSettings settings = ClientSettings.Load(directory, args);

        if (!settings.IsUsable)
        {
            // Two different problems that used to share one message. "No cartridge
            // configured" is misleading when a path was given and simply is not there
            // — the answer to one is to set a path, and to the other is to check the
            // one already set.
            ShowMessageWindow(string.IsNullOrWhiteSpace(settings.RomPath)
                ?
                [
                    "No cartridge configured.",
                    $"Put the path to your own .gba file in {ClientSettings.FileName}",
                    Path.Combine(directory, ClientSettings.FileName),
                    "",
                    "or run:  monmmo --rom <path to your .gba>",
                    "",
                    "The file is read locally and never leaves this machine.",
                ]
                :
                [
                    "That cartridge is not there.",
                    "",
                    settings.RomPath,
                    "",
                    "Check the path, or set it once in:",
                    Path.Combine(directory, ClientSettings.FileName),
                ]);

            return 1;
        }

        GameData data;
        MapLibrary library;
        LoadedMap map;

        try
        {
            // Opened once: locating the tables scans the whole cartridge several
            // times, which is fine at startup and not fine when a player opens a door.
            data = GameData.Load(settings.RomPath);
            library = MapLibrary.Open(data.Rom);

            map = (string.IsNullOrWhiteSpace(settings.MapAddress)
                      ? library.TryLoadByName(settings.MapName)
                      : library.TryLoad(settings.MapAddress!))
                  ?? throw new InvalidDataException(
                      $"No map matching '{settings.MapAddress ?? settings.MapName}'.");
        }
        catch (Exception ex)
        {
            ShowMessageWindow("Could not load a map.", "", ex.Message);
            return 1;
        }

        using var network = new NetworkClient();
        bool online = !string.IsNullOrWhiteSpace(settings.Server);

        if (online)
        {
            try
            {
                (string host, int port) = ParseServer(settings.Server);
                network.ConnectAsync(host, port).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                ShowMessageWindow("Could not reach the server.", "", ex.Message);
                return 1;
            }
        }

        Raylib.InitWindow(WindowWidth, WindowHeight, $"MonMMO — {map.Name}");
        Raylib.SetTargetFPS(60);

        if (online)
        {
            var login = new LoginScreen(settings.Username);

            if (!login.Run(network))
            {
                Raylib.CloseWindow();
                return 0;
            }

            ClientSettings.RememberUsername(directory, login.Username);
        }

        Run(data, library, map, network, settings);

        Raylib.CloseWindow();
        return 0;
    }

    private static (string Host, int Port) ParseServer(string value)
    {
        string[] parts = value.Split(':');

        return parts.Length == 2 && int.TryParse(parts[1], out int port)
            ? (parts[0], port)
            : (value, 7777);
    }

    private static void Run(
        GameData data, MapLibrary library, LoadedMap first, NetworkClient network, ClientSettings settings)
    {
        using var view = new MapView(library, first);

        // The cartridge's own walking figures, if they can be read. A rectangle is the
        // fallback rather than a failure: a client that will not start because it could
        // not find a sprite table is worse than one that draws a box.
        using var sprites = new CharacterSprites(data.Rom);

        // Located while the window is opening rather than in the moment somebody steps
        // into a trainer's line of sight — locating walks the whole image.
        var trainers = new TrainerNames(data.Rom, data.Species.Count);
        var items = new ItemNames(data.Rom);

        CharacterSprite? sprite = sprites.For(CharacterSprite.DefaultGraphicsId);

        var player = new WalkingCharacter();
        player.Place(view.Collision, view.Collision.FirstWalkable());

        var camera = new Camera2D
        {
            Offset = new System.Numerics.Vector2(WindowWidth / 2f, WindowHeight / 2f),
            Zoom = ViewZoom,
        };

        var others = new Dictionary<int, RemoteCharacter>();
        BattleScreen? battle = null;
        DialogueBox? talking = null;
        ShopScreen? shop = null;
        IReadOnlyList<BagEntry> bag = [];
        int money = 0;

        // The cartridge's bookkeeping, as the server has it. Kept here because running
        // a script needs it and only this machine can run one — the server stores these
        // and has no idea what any of them mean.
        var script = new ScriptState();

        // The party, out of a fight. Held because the bag has to say who a potion would
        // go on, and until now nothing outside a battle had any reason to know.
        IReadOnlyList<SavedMon> party = [];
        BagScreen? carrying = null;

        // Where the server last said we are, when that disagreed with where we think we
        // are. Held rather than applied on the spot: a correction almost always arrives
        // mid-step, and snapping a character sideways through a stride looks worse than
        // the half-square of error it fixes.
        GridPosition? correction = null;

        // Time until the client may next ask the server about an edge it cannot
        // predict. Without it, holding a direction into an edge would send a request
        // every frame and be rate-limited into uselessness.
        float edgeCooldown = 0f;

        while (!Raylib.WindowShouldClose())
        {
            float delta = Raylib.GetFrameTime();

            ApplyServerMessages(
                network, others, player, view, data, trainers, items, script, carrying,
                ref talking, ref battle, ref shop, ref bag, ref party, ref money, ref correction);

            // A battle suspends the overworld entirely: the server is running it, and
            // walking on meanwhile would put the two sides out of step.
            if (battle is not null)
            {
                // Something interrupted the conversation. Let go of whoever was being
                // held before the overworld disappears, or they stand to attention
                // until the server notices the player is elsewhere.
                if (talking is not null)
                {
                    talking = null;
                    network.SendTalkFinished();
                }

                battle.Update();

                if (battle.TakePendingAction() is { } action) network.SendBattleAction(action);

                Raylib.BeginDrawing();
                battle.Draw();
                Raylib.EndDrawing();

                if (battle.IsDismissed)
                {
                    money = battle.Money;
                    battle.Unload();
                    battle = null;
                }

                continue;
            }

            // Applied between steps, which is the only moment it can be applied without
            // tearing the animation. Dropping it instead — which is what happened before
            // — leaves the client a square ahead of the server for good.
            if (!player.IsStepping && correction is { } square)
            {
                if (square != player.Square) player.Place(view.Collision, square);
                correction = null;
            }

            // The bag, which is a whole screen for the same reason the counter is. Only
            // openable with nothing else going on — mid-conversation it would be a way
            // to walk off while somebody is held still.
            if (carrying is null && talking is null && Raylib.IsKeyPressed(KeyboardKey.B))
                carrying = new BagScreen(bag, party, items, data);

            if (carrying is not null)
            {
                carrying.Update();

                if (carrying.TakePending() is UseItemRequest use) network.SendUseItem(use.ItemId, use.Slot);

                Raylib.BeginDrawing();
                carrying.Draw();
                Raylib.EndDrawing();

                if (carrying.IsClosed) carrying = null;

                continue;
            }

            // A shop takes the whole screen, like a battle. Anything the text box was
            // waiting for is dropped: the counter is what the button press was for.
            if (shop is not null)
            {
                talking = null;

                shop.Update();

                // Taken once. Asking twice clears it on the first call and the second
                // question is always answered "nothing".
                switch (shop.TakePending())
                {
                    case BuyRequest buy: network.SendBuy(buy.ItemId, buy.Count); break;
                    case SellRequest sell: network.SendSell(sell.ItemId, sell.Count); break;
                }

                Raylib.BeginDrawing();
                shop.Draw();
                Raylib.EndDrawing();

                if (shop.IsClosed)
                {
                    money = shop.Money;
                    shop = null;
                    network.SendTalkFinished();
                }

                continue;
            }

            // A conversation stops the world the same way a battle does, except the map
            // stays on screen behind it. Reading movement here would have the player
            // walking away from somebody mid-sentence.
            if (talking is not null)
            {
                talking.Update();

                if (talking.IsFinished)
                {
                    talking = null;
                    network.SendTalkFinished();
                }
            }
            else if (DialogueBox.Pressed() && !player.IsStepping)
            {
                talking = Talk(data, view, player, network, script);
            }

            Direction? input = talking is null ? ReadDirection() : null;
            player.Update(delta, input);

            edgeCooldown = Math.Max(0f, edgeCooldown - delta);

            if (!player.IsStepping &&
                input is { } wanted &&
                edgeCooldown <= 0f &&
                view.Collision.LeavesGrid(player.Square, wanted))
            {
                // The one step the client cannot predict: it has no idea what is on the
                // next map, or whether there is one. Ask, stand still, and let the
                // answer arrive as a map change or as nothing at all.
                network.SendMove(wanted);
                edgeCooldown = WalkingCharacter.StepSeconds;
            }
            else if (player.ToReport is { } report)
            {
                // Told the moment a step begins, not when it finishes — it is already
                // predicted locally, so waiting would add a round trip of lag to every
                // square. A turn on the spot comes through here too, and used not to
                // come through anywhere.
                network.SendMove(report);
            }

            foreach (RemoteCharacter other in others.Values) other.Update(delta);

            view.Update(delta);

            (float playerX, float playerY) = player.PixelPosition;
            camera.Target = ClampView(playerX, playerY, view.Map);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            Raylib.BeginMode2D(camera);
            Raylib.DrawTexture(view.Texture, 0, 0, Color.White);

            // Drawn before the players, so anyone standing in front of somebody is in
            // front of them rather than behind.
            foreach (WalkingPerson standing in view.People.Values)
            {
                (float ox, float oy) = standing.PixelPosition;

                if (sprites.For(standing.GraphicsId) is { } theirs)
                    theirs.Draw(ox, oy, standing.Facing, standing.IsWalking, standing.Stride);
                else
                    DrawPlayer(ox, oy, standing.Facing, new Color(200, 180, 140, 255));
            }

            foreach (RemoteCharacter other in others.Values)
            {
                (float x, float y) = other.PixelPosition;

                if (sprite is not null) sprite.Draw(x, y, other.Facing, other.IsMoving, other.Id);
                else DrawPlayer(x, y, other.Facing, new Color(120, 200, 255, 255));

                DrawNameTag(other.Name, x, y);
            }

            if (sprite is not null)
                sprite.Draw(playerX, playerY, player.Facing, player.IsStepping, player.StepsTaken);
            else
                DrawPlayer(playerX, playerY, player.Facing);
            Raylib.EndMode2D();

            DrawStatus(view.Map, player, network, others.Count, money, bag.Count);
            talking?.Draw(WindowWidth, WindowHeight);
            Raylib.EndDrawing();
        }

    }

    /// <summary>
    /// Runs the script of whoever the player is facing, if anybody is there and they
    /// have one.
    /// <para>
    /// Read from this machine's own cartridge, because this is the only machine that
    /// has one. The server knows where everybody is standing and nothing whatsoever
    /// about what they say — it is told a conversation started so that it can hold the
    /// person still, and that is the whole of its involvement.
    /// </para>
    /// </summary>
    private static DialogueBox? Talk(
        GameData data, MapView view, WalkingCharacter player, NetworkClient network, ScriptState script)
    {
        // Where the server says people are, which after a few seconds of wandering is
        // nowhere near where the cartridge put them.
        Dictionary<int, GridPosition> live = view.People.ToDictionary(p => p.Key, p => p.Value.Square);

        // The map's own walkability, not the grid the client predicts against — that one
        // has people in it, and a person is not a counter.
        if (Interaction.InFrontOf(
                player.Square, player.Facing, view.Map.Objects, live,
                square => !view.Map.Collision.IsWalkable(square)) is not { } person)
        {
            return null;
        }

        // Sent whether or not there is anything to read, because what happens next is
        // not this side's decision. Somebody who wants a fight starts one here, and
        // gating the message on finding dialogue would mean a trainer with nothing to
        // say could never be challenged by walking up to them.
        network.SendTalk(person.LocalId);

        // Run rather than read. The reader follows both arms of every conditional
        // because it has to — choosing needs a save's flags — which is why a trainer
        // used to greet you, gloat about losing and thank you for the rematch in one
        // breath. Given the flags, this walks the one path that actually happens.
        ScriptRun run = person.HasScript
            ? ScriptRunner.Run(data.Rom, person.ScriptAddress, script)
            : new ScriptRun();

        // Applied on both sides rather than waiting to be told. The server is where
        // these live, but the next line this person reads is decided here and it would
        // be decided from yesterday's flags for as long as the round trip takes.
        foreach (int flag in run.FlagsSet) script.Set(flag);
        foreach (int flag in run.FlagsCleared) script.Clear(flag);
        foreach ((int id, int value) in run.VariablesWritten) script.Write(id, value);

        if (run.FlagsSet.Count + run.FlagsCleared.Count + run.VariablesWritten.Count > 0)
            network.SendScriptRan(run);

        DialogueBox? box = person.HasScript ? new DialogueBox(run.Pages) : null;

        // Plenty of scripts say nothing at all — they set a flag, or hand something
        // over. An empty box would still have to be dismissed, so there isn't one.
        if (box is null || box.IsEmpty)
        {
            network.SendTalkFinished();
            return null;
        }

        return box;
    }

    /// <summary>
    /// Folds anything the server has said into local state. Our own movement is
    /// predicted, so a position for us is only applied when it actually disagrees —
    /// otherwise every step would stutter as the confirmation arrived.
    /// </summary>
    /// <summary>
    /// Folds server messages into local state, returning an encounter if one arrived.
    /// </summary>
    private static void ApplyServerMessages(
        NetworkClient network,
        Dictionary<int, RemoteCharacter> others,
        WalkingCharacter player,
        MapView view,
        GameData data,
        TrainerNames trainers,
        ItemNames items,
        ScriptState script,
        BagScreen? carrying,
        ref DialogueBox? said,
        ref BattleScreen? battle,
        ref ShopScreen? shop,
        ref IReadOnlyList<BagEntry> bag,
        ref IReadOnlyList<SavedMon> party,
        ref int money,
        ref GridPosition? correction)
    {
        foreach (NetMessage message in network.Drain())
        {
            switch (message)
            {
                case Welcome welcome:
                    // The title was set from whatever client.json asked for; the server
                    // decides where you actually are, and it is usually somewhere else.
                    if (view.SwitchTo(welcome.MapId)) Raylib.SetWindowTitle($"MonMMO — {view.Map.Name}");

                    player.Place(view.Collision, new GridPosition(welcome.X, welcome.Y));
                    bag = welcome.Bag;
                    party = welcome.Party;
                    money = welcome.Money;

                    foreach (int flag in welcome.Flags) script.Set(flag);
                    foreach (SavedVariable variable in welcome.Variables) script.Write(variable.Id, variable.Value);
                    foreach (int beaten in welcome.Beaten) script.MarkBeaten(beaten);

                    break;

                case FlagsChanged changed:
                    foreach (int flag in changed.Flags) script.Set(flag);

                    break;

                case BlackedOut fainted:
                    // The map change arrives with this and does the moving. What is left
                    // is what the player has to be told, because a party that healed and
                    // a purse that halved are both things they did not ask for.
                    party = fainted.Party;
                    money = fainted.Money;

                    said = new DialogueBox([
                        "You blacked out!",
                        "You scurried back, feeling weaker for it.",
                    ]);

                    break;

                case TrainerSpotted:
                    // Nothing to draw yet, and deliberately nothing rather than a text
                    // box: a box has to be dismissed, and dismissing it would mean
                    // pressing a button through the walk it exists to announce. The
                    // exclamation mark over their head is a sprite, and sprites are the
                    // milestone this one stopped short of.
                    //
                    // What the player does see is the walk, which arrives as ordinary
                    // ObjectMoved messages, and the fight at the end of it.
                    break;

                case PartyHealed healed:
                    party = healed.Party;

                    // The counter says it out loud, in a box dismissed like any other.
                    // There is nothing to choose, so there is nothing to open.
                    said = new DialogueBox([
                        healed.Needed
                            ? "We've restored your POKeMON to full health."
                            : "Your POKeMON are all healthy already.",
                    ]);

                    break;

                case BagUpdated updated:
                    bag = updated.Bag;
                    party = updated.Party;
                    carrying?.Apply(updated);

                    break;

                case TrainerBeaten beaten:
                    // Winning is decided on the server. Until this arrives, running that
                    // trainer's script gives their opening line — because what the script
                    // asks is whether the fight has already happened.
                    script.MarkBeaten(beaten.TrainerId);

                    break;

                case PlayerAppeared appeared when appeared.PlayerId != network.PlayerId:
                    others[appeared.PlayerId] = new RemoteCharacter(
                        appeared.PlayerId, appeared.Name,
                        new GridPosition(appeared.X, appeared.Y), appeared.Facing);
                    break;

                case PlayerMoved moved when moved.PlayerId != network.PlayerId:
                    if (others.TryGetValue(moved.PlayerId, out RemoteCharacter? other))
                        other.MoveTo(new GridPosition(moved.X, moved.Y), moved.Facing);
                    break;

                case ObjectsPlaced placed:
                    view.Place(placed.Objects);
                    break;

                case ObjectMoved moved:
                    view.Moved(moved);
                    break;

                case MapChanged changed:
                    // Everyone who was visible was visible on the old map.
                    others.Clear();

                    if (view.SwitchTo(changed.MapId))
                    {
                        player.Place(view.Collision, new GridPosition(changed.X, changed.Y));
                        Raylib.SetWindowTitle($"MonMMO — {view.Map.Name}");
                    }

                    break;

                case PlayerMoved mine when mine.PlayerId == network.PlayerId:
                    // The server's answer about us. Where it agrees this costs nothing;
                    // where it does not, this is the only thing that puts us back.
                    correction = new GridPosition(mine.X, mine.Y);
                    break;

                case MoveRejected rejected:
                    correction = new GridPosition(rejected.X, rejected.Y);
                    break;

                case PlayerLeft left:
                    others.Remove(left.PlayerId);
                    break;

                case BattleStarted started:
                    battle = new BattleScreen(started, data, trainers, items);
                    break;

                case BattlerSentOut sent:
                    battle?.Apply(sent);
                    break;

                case ShopOpened opened:
                    shop = new ShopScreen(opened, items);
                    break;

                case ShopUpdated updated:
                    shop?.Apply(updated);
                    money = updated.Money;
                    bag = updated.Bag;
                    break;

                case BattleUpdate update:
                    battle?.Apply(update);
                    break;

                case BattleFinished finished:
                    battle?.Apply(finished);
                    money = finished.Money;

                    // A fight is where health actually changes. Without this the bag
                    // would open on a party that was last accurate at login and offer
                    // a potion to somebody who is already full.
                    party = finished.Party;

                    break;

                case Rejected rejected when battle is not null:
                    // The two sides disagree about whether a battle is running. The
                    // server wins, and the screen has to let go rather than wait for a
                    // reply that is never coming.
                    battle.Abandon(rejected.Reason);
                    break;
            }
        }
    }

    private static Direction? ReadDirection()
    {
        if (Raylib.IsKeyDown(KeyboardKey.Up) || Raylib.IsKeyDown(KeyboardKey.W)) return Direction.Up;
        if (Raylib.IsKeyDown(KeyboardKey.Down) || Raylib.IsKeyDown(KeyboardKey.S)) return Direction.Down;
        if (Raylib.IsKeyDown(KeyboardKey.Left) || Raylib.IsKeyDown(KeyboardKey.A)) return Direction.Left;
        if (Raylib.IsKeyDown(KeyboardKey.Right) || Raylib.IsKeyDown(KeyboardKey.D)) return Direction.Right;
        return null;
    }

    /// <summary>
    /// Keeps the view inside the map, so a small map does not leave the player staring
    /// at empty space. Maps narrower than the window stay centred instead.
    /// </summary>
    private static System.Numerics.Vector2 ClampView(float playerX, float playerY, LoadedMap map)
    {
        float halfWidth = WindowWidth / 2f / ViewZoom;
        float halfHeight = WindowHeight / 2f / ViewZoom;

        float centreX = playerX + WalkingCharacter.SquarePixels / 2f;
        float centreY = playerY + WalkingCharacter.SquarePixels / 2f;

        float x = map.PixelWidth <= halfWidth * 2
            ? map.PixelWidth / 2f
            : Math.Clamp(centreX, halfWidth, map.PixelWidth - halfWidth);

        float y = map.PixelHeight <= halfHeight * 2
            ? map.PixelHeight / 2f
            : Math.Clamp(centreY, halfHeight, map.PixelHeight - halfHeight);

        return new System.Numerics.Vector2(x, y);
    }

    /// <summary>
    /// A placeholder character. The cartridge's own overworld sprites are a later job;
    /// this milestone is about movement.
    /// </summary>
    private static void DrawPlayer(float x, float y, Direction facing, Color? body = null)
    {
        const int size = WalkingCharacter.SquarePixels;

        Raylib.DrawRectangle((int)x + 2, (int)y + 1, size - 4, size - 2, body ?? new Color(248, 248, 248, 255));
        Raylib.DrawRectangleLines((int)x + 2, (int)y + 1, size - 4, size - 2, new Color(32, 32, 32, 255));

        (int dx, int dy) = facing switch
        {
            Direction.Up => (0, -4),
            Direction.Down => (0, 4),
            Direction.Left => (-4, 0),
            _ => (4, 0),
        };

        Raylib.DrawCircle((int)x + size / 2 + dx, (int)y + size / 2 + dy, 2f, new Color(216, 72, 72, 255));
    }

    private static void DrawNameTag(string name, float x, float y)
    {
        int width = Raylib.MeasureText(name, 8);
        int left = (int)x + WalkingCharacter.SquarePixels / 2 - width / 2;

        Raylib.DrawText(name, left + 1, (int)y - 8, 8, Color.Black);
        Raylib.DrawText(name, left, (int)y - 9, 8, Color.White);
    }

    private static void DrawStatus(
        LoadedMap map, WalkingCharacter player, NetworkClient network, int others, int money, int carrying)
    {
        string connection = network.Failure is { } failure
            ? $"   offline: {failure}"
            : network.IsConnected ? $"   online, {others} others" : "";

        string line = $"{map.Name}  ({map.Bank}.{map.Number})   " +
                      $"{player.Square.X},{player.Square.Y}   " +
                      $"{money}   {carrying} items{connection}";

        Raylib.DrawText(line, 13, 13, 20, Color.Black);
        Raylib.DrawText(line, 12, 12, 20, Color.White);
    }

    /// <summary>Opens a window that just explains what went wrong, rather than exiting silently.</summary>
    private static void ShowMessageWindow(params string[] lines)
    {
        foreach (string line in lines) Console.WriteLine(line);

        Raylib.InitWindow(WindowWidth, WindowHeight, "MonMMO");
        Raylib.SetTargetFPS(30);

        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(24, 24, 32, 255));

            for (int i = 0; i < lines.Length; i++)
                Raylib.DrawText(lines[i], 40, 60 + i * 30, 20, Color.White);

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
