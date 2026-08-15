using PokeMmo.Core.Battle;
using PokeMmo.Core.Cosmetics;
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

    /// <summary>How long the black over a new map takes to clear.</summary>
    private const float FadeSeconds = 0.22f;

    /// <summary>
    /// The working variable a script means when it says "this one".
    /// <para>
    /// Scripts write it themselves 45 times in this cartridge, so it is an ordinary
    /// variable and not a fact about the engine — but the ball on the professor's table
    /// reads it without writing it, to remove itself the moment it is taken. A variable
    /// read and never written inside the script language has to be filled from outside
    /// it, and outside a person's own script the only thing to hand is the person.
    /// </para>
    /// </summary>
    private const int TalkingTo = 0x800F;

    /// <summary>
    /// The party slot a script is about to have named, which it writes just before it
    /// calls the cartridge's keyboard.
    /// </summary>
    private const int NamingSlot = 0x8004;

    /// <summary>
    /// Where a script leaves and looks for an answer: a question's yes or no, and how a
    /// fight came out. One variable for both, which is the cartridge's own arrangement.
    /// </summary>
    private const int Result = 0x800D;

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

        // The lettering, which needs a window before it can become a texture. Every
        // screen draws through this from here on.
        Skin.Font = PixelFont.Build();

        // Escape does not quit. It is raylib's default and it is wrong for a game: the
        // naming screen offers Escape to leave a name alone, and the first time that was
        // played the whole client shut down to the desktop instead. The window's own
        // close button still works, which is where people look for it anyway.
        Raylib.SetExitKey(KeyboardKey.Null);

        // Who the cartridge's dialogue means when it leaves a gap for the player. The
        // signed-in name, because that is the only name this game ever asked anybody for.
        string signedInAs = settings.PlayerName;

        if (online)
        {
            var login = new LoginScreen(settings.Username);

            if (!login.Run(network))
            {
                Raylib.CloseWindow();
                return 0;
            }

            ClientSettings.RememberUsername(directory, login.Username);
            signedInAs = login.Username;
        }

        Run(data, library, map, network, settings, signedInAs);

        Raylib.CloseWindow();
        return 0;
    }

    /// <summary>
    /// Whether the fight about to start is with the boy who follows you around.
    /// <para>
    /// A box rather than a flag because the two places that know are static and the place
    /// that needs to know is a message handler thirty lines away. What it carries is one
    /// bit and it is set by the only thing that can tell: the script that picked the
    /// fight is the same script that says his name.
    /// </para>
    /// </summary>
    /// <summary>
    /// Where a script stopped for a fight with nobody in it, kept until the fight is over.
    /// <para>
    /// A mutable note rather than a return value because a script can start a fight from
    /// six different places — talking, arriving, answering a question — and every one of
    /// them already hands back what the player should be looking at. What is wanted here
    /// is what they should be looking at <em>next</em>, and only one thing knows when
    /// that is: the battle screen closing.
    /// </para>
    /// </summary>
    private sealed class ScriptedFight
    {
        /// <summary>The address the run stopped at, or nothing when no script is waiting.</summary>
        public uint? ResumesAt { get; set; }

        public uint? Take()
        {
            uint? was = ResumesAt;
            ResumesAt = null;

            return was;
        }
    }

    private sealed class RivalFight
    {
        public bool Next { get; set; }

        public bool Take()
        {
            bool was = Next;
            Next = false;

            return was;
        }
    }

    /// <summary>How long the mark stays up. About as long as the games leave it.</summary>
    private const float ExclaimSeconds = 0.9f;

    /// <summary>Which id a person is filed under, so the right head gets the mark.</summary>
    private static int? KeyOf(Dictionary<int, WalkingPerson> people, WalkingPerson person)
    {
        foreach ((int id, WalkingPerson candidate) in people)
        {
            if (ReferenceEquals(candidate, person)) return id;
        }

        return null;
    }

    /// <summary>
    /// The shadow under somebody in the air over a ledge.
    /// <para>
    /// Drawn where the feet would be if they were on the ground, which is the whole
    /// trick: without it a raised sprite reads as a taller sprite, and the hop looks
    /// like the character growing rather than jumping.
    /// </para>
    /// </summary>
    private static void DrawShadow(float x, float y)
    {
        Raylib.DrawEllipse((int)x + 8, (int)y + 15, 6, 3, new Color(0, 0, 0, 70));
    }

    /// <summary>A speech bubble with an exclamation mark in it, above somebody's head.</summary>
    private static void DrawExclamation(float x, float y)
    {
        var bubble = new Rectangle(x + 3, y - 15, 10, 13);

        Raylib.DrawRectangleRec(bubble, Color.White);
        Raylib.DrawRectangleLinesEx(bubble, 1, new Color(40, 40, 48, 255));

        // The tail, and then the mark: a stroke and a dot, which is all an exclamation
        // mark is at this size.
        Raylib.DrawRectangle((int)x + 6, (int)y - 3, 2, 2, Color.White);
        Raylib.DrawRectangle((int)x + 7, (int)y - 13, 2, 6, new Color(40, 40, 48, 255));
        Raylib.DrawRectangle((int)x + 7, (int)y - 6, 2, 2, new Color(40, 40, 48, 255));
    }

    private static (string Host, int Port) ParseServer(string value)
    {
        string[] parts = value.Split(':');

        return parts.Length == 2 && int.TryParse(parts[1], out int port)
            ? (parts[0], port)
            : (value, 7777);
    }

    private static void Run(
        GameData data, MapLibrary library, LoadedMap first, NetworkClient network, ClientSettings settings,
        string signedInAs)
    {
        using var view = new MapView(library, first);

        // What the number a finished fight leaves behind means, read off this cartridge
        // rather than remembered. Nothing breaks without it: a script that stopped for a
        // fight simply does not pick up again, which is what happened until now.
        BattleOutcomes? outcomes = BattleOutcomeLocator.Locate(data.Rom, Note);

        // Who heals, which is now also where the box is. Located here rather than asked
        // of the server, because a rule kept on both sides of the split is kept twice or
        // it is not kept at all — and this side has the same cartridge to read it from.
        uint? healer = HealerLocator.Locate(
            library.All().Select(m => ($"{m.Bank}.{m.Number}", (IReadOnlyList<MapObject>)m.Objects)),
            data.Rom);

        if (healer is null) Note("no healer script, so the box has nowhere to be offered");

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
        player.Place(view.Collision, view.Collision.FirstWalkable(), HopsOn(view));

        var camera = new Camera2D
        {
            Offset = new System.Numerics.Vector2(WindowWidth / 2f, WindowHeight / 2f),
            Zoom = ViewZoom,
        };

        var others = new Dictionary<int, RemoteCharacter>();

        // What this player is wearing, as the server last said it.
        Appearance looks = Appearance.Bare;
        IReadOnlyList<int> owned = [];
        WardrobeScreen? dressing = null;
        TradeScreen? trading = null;

        // Somebody has asked, and has not been answered. Kept so the offer can be
        // accepted with a key rather than by typing a player id.
        int askedBy = 0;
        int challengedBy = 0;
        BattleScreen? battle = null;
        DialogueBox? talking = null;

        // The script that picked a fight, so it can be run again once the fight is over.
        // Running it again is the whole trick: a trainerbattle whose trainer has already
        // been beaten does nothing at all and the script carries straight on into what
        // they say afterwards — which for the rival is losing gracefully and leaving.
        uint? afterTheFight = null;

        // And the same for a fight with nobody in it. A wild fight a script started stops
        // that script where it stands; what comes after is behind the question "how did
        // it go", which is a question only the server can answer.
        uint? afterTheWildFight = null;
        int? cameOut = null;

        // Which of the fights ahead is with him, set by whichever script picks one.
        var rival = new RivalFight();
        var waiting = new ScriptedFight();

        // A scene, when a script turns out to be one. Kept beside the text box rather
        // than inside it: a box is one thing being said and a scene is an order of
        // things happening, and most scripts are the first kind.
        Cutscene? scene = null;

        // The screen this project had to build itself, because the one the cartridge
        // would have shown is a keyboard drawn in code.
        NamingScreen? naming = null;

        // A line to type commands into. It sends text and shows text; the server decides
        // everything, and refuses everybody it was not told to allow.
        var console = new ConsoleBox();

        ShopScreen? shop = null;
        FerryScreen? boat = null;
        IReadOnlyList<BagEntry> bag = [];
        int money = 0;

        // The cartridge's bookkeeping, as the server has it. Kept here because running
        // a script needs it and only this machine can run one — the server stores these
        // and has no idea what any of them mean.
        // Which set of words this character reads. Client-side because it decides which
        // arm of a fork gets read, and the server has never seen either arm.
        // Who to put where the cartridge's dialogue leaves a gap. 0xFD marks a gap and
        // the byte after it says what goes there — the player at 109 sites, the rival at
        // 33, a species at 19. Filled here because the name of a species is on a
        // cartridge and this is the only half of the project that has one.
        var script = new ScriptState
        {
            IsGirl = settings.Girl,
            PlayerName = signedInAs,

            // The cartridge's own first suggestion rather than a word this project made
            // up. Which of the forty-two the games give the rival is not settled by the
            // list, but it is one of them — and "RIVAL" was none of them.
            RivalName = NameSuggestions.FirstName(data.SuggestedNames) ?? "RIVAL",
            NameOfSpecies = species => data.SpeciesAt(species)?.Name ?? "",
            NameOfItem = items.Of,
        };

        // The party, out of a fight. Held because the bag has to say who a potion would
        // go on, and until now nothing outside a battle had any reason to know.
        IReadOnlyList<SavedMon> party = [];

        // What is not in the party, and how much room there is for more of it. Both come
        // from the server — the size is the cartridge's number and a client that assumed
        // thirty would be remembering rather than reading.
        IReadOnlyList<SavedMon> box = [];
        int boxSize = 0;
        BagScreen? carrying = null;
        BoxScreen? storing = null;
        PartyScreen? looking = null;

        // Who has spotted the player and is walking over, and how long the mark above
        // their head has left. The server refuses movement for the duration; this is the
        // client's half of that rule, and without it the client predicts a step, has it
        // refused, and snaps back — which reads as a broken game rather than as being
        // caught. Fifth time this project has needed both halves of one rule.
        int? watching = null;
        float exclaimFor = 0f;

        // Which square the player is standing on, as far as the trigger check is
        // concerned. Held rather than compared against the last frame's position so that
        // arriving somewhere fires once, and standing there does not fire at all.
        GridPosition standingOn = player.Square;

        // Whether the map has just changed under us, so its own arrival script gets a
        // chance to run. Held rather than acted on where the message lands, because the
        // scene it may start has to be built from the map the player is now on and the
        // view has only just switched to it.
        bool arrived = false;

        // How much black is still over the screen after arriving somewhere. The original
        // fades through black at every door; cutting straight to the far side is the
        // single harshest thing about moving between maps here. Only the fade in — the
        // fade out would have to start before the server has agreed the player is going
        // anywhere, and a fade that plays for a refused door is worse than none.
        float fadingIn = 0f;

        // The longest frame in the last couple of seconds, and how long is left of that
        // window. Kept rather than averaged: a stutter is one frame that took ten times
        // as long as the rest, and every average this could be replaced with is designed
        // to hide it.
        float worstFrame = 0f;
        float worstUntil = 0f;

        // How long before this side may ask to step again. The client's half of the
        // server's rate limit, and until now it did not exist because it did not have to:
        // a step cannot begin before the last one's animation ends, and that animation is
        // longer than the limit. Arriving somewhere is what breaks it — the character is
        // placed outright, so a step half performed ends immediately and the next one can
        // be asked for at once. The server refuses it, says where they really are, and
        // the player is snapped back onto the doormat.
        float holdInput = 0f;

        // Where the server last said we are, when that disagreed with where we think we
        // are. Held rather than applied on the spot: a correction almost always arrives
        // mid-step, and snapping a character sideways through a stride looks worse than
        // the half-square of error it fixes.
        // Tagged with the map it was about, which is the whole of the fix for walking out
        // of a town and arriving at the far end of the route. A correction is held until
        // the character is between steps, and somebody walking in a straight line is
        // never between steps — so the confirmation of the last step in Pallet Town was
        // still pending when the edge was crossed, and landed on Route 1 as an instruction
        // to stand on (12, 0). Which is where it had meant, on a map 40 squares away.
        (string MapId, GridPosition Square)? correction = null;

        // Time until the client may next ask the server about an edge it cannot
        // predict. Without it, holding a direction into an edge would send a request
        // every frame and be rate-limited into uselessness.
        float edgeCooldown = 0f;

        while (!Raylib.WindowShouldClose())
        {
            float delta = Raylib.GetFrameTime();

            worstUntil -= delta;

            if (worstUntil <= 0f)
            {
                worstUntil = 2f;
                worstFrame = delta;
            }
            else if (delta > worstFrame)
            {
                worstFrame = delta;
            }

            ApplyServerMessages(
                network, others, player, view, data, trainers, items, script, carrying, storing, looking,
                ref talking, ref battle, ref shop, ref boat, ref bag, ref party, ref box, ref boxSize, ref money,
                ref correction, ref looks, ref owned, ref trading, ref askedBy, ref challengedBy, ref watching, ref exclaimFor, ref scene, ref arrived, ref fadingIn, ref holdInput,
                ref afterTheFight, ref cameOut, outcomes, rival, console);

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

                battle.Animate(delta);
                battle.Update();

                // The answer to "forget which?", once there is one. Sent from the loop
                // rather than from the screen, for the same reason every other request
                // is: the screen draws and reads keys, and the socket is not its business.
                if (battle.TakeAnswer() is { } answer) network.SendLearnMove(answer.MoveId, answer.Forget);

                if (battle.TakePendingAction() is { } action) network.SendBattleAction(action);

                Raylib.BeginDrawing();
                battle.Draw();
                Raylib.EndDrawing();

                if (battle.IsDismissed)
                {
                    // The money is not taken off the screen here. It arrives twice —
                    // BattleFinished carries the prize, and a loss is followed by
                    // BlackedOut carrying half of what is left — and reading it back off
                    // the screen at the end replayed the first over the second. Losing
                    // cost nothing until the next thing that mentioned money.
                    battle.Unload();
                    battle = null;

                    // Now, rather than when the server said the fight was over: the
                    // words that come after a fight belong on the map, and a scene
                    // played behind the battle screen is a scene nobody sees.
                    if (afterTheFight is { } again)
                    {
                        afterTheFight = null;

                        ScriptRun rest = ScriptRunner.Run(
                            data.Rom, again, script.WithParty(party.Select(m => m.Moves)));

                        Note($"after the fight, 0x{again:X8} ran again: " +
                             $"{rest.Pages.Count} pages, {rest.Beats.Count} beats");

                        (talking, scene) = Present(data, view, network, script, rest, waiting);
                    }

                    // And the other kind of afterwards: a fight with nobody in it, which
                    // a script started and then stood still for. It picks up where it
                    // stopped, with how the fight went written where the script looks —
                    // and the sleeper on ROUTE 12 finally says what it has always said.
                    else if (waiting.Take() is { } carryOn && cameOut is { } howItWent)
                    {
                        script.Write(Result, howItWent);

                        ScriptRun rest = ScriptRunner.Run(
                            data.Rom, carryOn, script.WithParty(party.Select(m => m.Moves)));

                        Note($"after the wild fight, 0x{carryOn:X8} carried on with {Result:X} = " +
                             $"{howItWent}: {rest.Pages.Count} pages, {rest.Beats.Count} beats");

                        (talking, scene) = Present(data, view, network, script, rest, waiting);
                    }

                    cameOut = null;
                }

                continue;
            }

            // Applied between steps, which is the only moment it can be applied without
            // tearing the animation. Dropping it instead — which is what happened before
            // — leaves the client a square ahead of the server for good.
            if (!player.IsStepping && correction is { } pending)
            {
                if (pending.MapId != view.MapId)
                {
                    // Not this map's business. Dropped rather than applied: where the
                    // server said somebody was standing somewhere else is not a fact
                    // about here, and the arrival has already placed them.
                    correction = null;
                }
                else if (pending.Square != player.Square)
                {
                    // A square out is ordinary — it is one optimistic step the server did
                    // not agree with, which is what this exists to undo. More than that is
                    // not a correction, it is a disagreement, and it is worth saying so.
                    //
                    // The S.S. ANNE is why. The client had the player three squares north
                    // of where the server had them, so the captain was not in front of
                    // them, so he could not be talked to, so HM01 could not be got — and
                    // from the outside all of that looked like a missing person. It cost
                    // two milestones of looking at flags.
                    int apart =
                        Math.Abs(pending.Square.X - player.Square.X) +
                        Math.Abs(pending.Square.Y - player.Square.Y);

                    if (apart > 1)
                        Note($"put back {apart} squares, from {player.Square} to {pending.Square} — that is a disagreement, not a step");

                    player.Place(view.Collision, pending.Square, HopsOn(view));
                }

                correction = null;
            }

            // The bag, which is a whole screen for the same reason the counter is. Only
            // openable with nothing else going on — mid-conversation it would be a way
            // to walk off while somebody is held still.
            if (carrying is null && talking is null && !console.IsOpen && Raylib.IsKeyPressed(KeyboardKey.B))
                carrying = new BagScreen(bag, party, items, data);

            // The box, on its own key now rather than on a square. It used to open by
            // facing behaviour 0x6A, which is the stairs — and there is no byte and no
            // script anywhere in this cartridge for a storage machine, so there is no
            // square to face. What is left is the room: somewhere that heals is somewhere
            // the box is offered, which the server decides from the same field.
            //
            // Not on the action key, because the action key belongs to whoever is in
            // front of you and a healing centre is full of people.
            if (carrying is null && storing is null && looking is null && talking is null && !console.IsOpen
                && Raylib.IsKeyPressed(KeyboardKey.C) && InARoomThatHeals(data, view, healer))
            {
                storing = new BoxScreen(party, box, boxSize, data, items);
            }

            // The party, on the key the box used to take. Nothing else is going on, the
            // console is shut, and this is the one screen that needs no machine.
            // The wardrobe, which needs no machine and nobody to talk to — getting dressed
            // is the one thing in this game that is entirely your own business.
            if (carrying is null && storing is null && looking is null && dressing is null
                && talking is null && !console.IsOpen && Raylib.IsKeyPressed(KeyboardKey.O))
            {
                dressing = new WardrobeScreen(owned, looks);
            }

            if (trading is not null)
            {
                trading.Apply(party);
                trading.Update();

                if (trading.TakePending() is { } move) network.SendMessage(move);

                trading.Draw();

                if (trading.IsClosed) trading = null;

                Raylib.EndDrawing();
                continue;
            }

            // Saying yes to somebody who asked, which is the same message they sent.
            // One key for both invitations, because a player agreeing to something should
            // not have to remember which kind of something it was.
            //
            // Answered from inside the box that asked, which is the whole of what was
            // wrong the first time this was played: the invitation opened a text box, a
            // text box swallows every key, and the one key it was telling the player to
            // press was the one it had just taken away.
            if ((askedBy != 0 || challengedBy != 0) && !console.IsOpen
                && Raylib.IsKeyPressed(KeyboardKey.Y))
            {
                if (challengedBy != 0) network.SendMessage(new DuelRequest(challengedBy));
                else network.SendMessage(new TradeRequest(askedBy));

                askedBy = 0;
                challengedBy = 0;
                talking = null;
            }

            if (dressing is not null)
            {
                // Refreshed every frame from the two things the server owns, rather than
                // pushed at it from the message handler. One place to be right.
                dressing.Apply(owned);
                dressing.Apply(looks);

                dressing.Update();

                if (dressing.TakePending() is WearRequest asked) network.SendWear(asked.CosmeticId, asked.Slot);

                dressing.Draw();

                if (dressing.IsClosed) dressing = null;

                Raylib.EndDrawing();
                continue;
            }

            if (carrying is null && storing is null && looking is null && talking is null && !console.IsOpen
                && Raylib.IsKeyPressed(KeyboardKey.P))
            {
                looking = new PartyScreen(party, data, items);
            }

            if (looking is not null)
            {
                looking.Update();

                if (looking.TakePending() is SwapPartyRequest swap) network.SendSwapParty(swap.A, swap.B);

                Raylib.BeginDrawing();
                looking.Draw();
                Raylib.EndDrawing();

                if (looking.IsClosed) looking = null;

                continue;
            }

            if (storing is not null)
            {
                storing.Update();

                switch (storing.TakePending())
                {
                    case DepositRequest put: network.SendDeposit(put.Slot); break;
                    case WithdrawRequest out_: network.SendWithdraw(out_.Slot); break;
                }

                Raylib.BeginDrawing();
                storing.Draw();
                Raylib.EndDrawing();

                if (storing.IsClosed) storing = null;

                continue;
            }

            if (carrying is not null)
            {
                carrying.Update();

                switch (carrying.TakePending())
                {
                    case UseItemRequest use: network.SendUseItem(use.ItemId, use.Slot); break;
                    case GiveItemRequest give: network.SendGiveItem(give.ItemId, give.Slot); break;
                    case TakeItemRequest take: network.SendTakeItem(take.Slot); break;
                    case LearnMoveRequest answer: network.SendLearnMove(answer.MoveId, answer.Forget); break;
                }

                Raylib.BeginDrawing();
                carrying.Draw();
                Raylib.EndDrawing();

                if (carrying.IsClosed) carrying = null;

                continue;
            }

            // The boat, on the same terms as the shop and just above it: a sailor who was
            // asked where he goes has been asked instead of talked to.
            if (boat is not null)
            {
                talking = null;

                boat.Update();

                if (boat.TakePending() is SailRequest sailing) network.SendSail(sailing.Number);

                Raylib.BeginDrawing();
                boat.Draw();
                Raylib.EndDrawing();

                if (boat.IsClosed)
                {
                    boat = null;
                    network.SendTalkFinished();
                }

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

            // Ahead of the naming screen even, because it is the same kind of thing and
            // opening one on top of the other would leave two fields taking the same
            // letters.
            console.Update(delta);

            if (console.TakePending() is { } typed) network.SendConsole(typed);

            if (!console.IsOpen && naming is null && talking is null && battle is null &&
                Raylib.IsKeyPressed(KeyboardKey.Slash))
            {
                console.Open();
            }

            // Ahead of everything else, because a name is being typed and W, A, S and D
            // are letters before they are directions.
            if (console.IsOpen)
            {
                // Nothing else reads the keyboard while a command is being typed.
            }
            else if (naming is not null)
            {
                naming.Update();

                if (naming is { IsFinished: true } named)
                {
                    naming = null;

                    // Only when they changed it. A monster with no nickname and one
                    // nicknamed after its own species look identical on every screen,
                    // but only one of them is still called whatever its species is
                    // called — which matters the first time somebody plays in another
                    // language.
                    if (!named.Unchanged) network.SendNameMon(named.Slot, named.Name);

                    Note(named.Unchanged
                        ? $"slot {named.Slot} keeps the name {named.Species}"
                        : $"slot {named.Slot} is called {named.Name}");

                    // And then whatever the script was going to do next, which for the
                    // ball on the professor's table is the rival taking his own.
                    if (named.Rest is { } rest)
                        (talking, scene) = Present(data, view, network, script, rest, waiting);

                    if (talking is null && scene is null) network.SendTalkFinished();
                }
            }

            // A conversation stops the world the same way a battle does, except the map
            // stays on screen behind it. Reading movement here would have the player
            // walking away from somebody mid-sentence.
            else if (scene is not null)
            {
                scene.Update(delta);

                if (scene.IsFinished)
                {
                    // Where it left everybody, for the server to accept or refuse. Sent
                    // before letting go, because the hold is what makes it acceptable.
                    foreach ((int localId, GridPosition left, Direction facing) in scene.Moved)
                        network.SendScenePlaced(localId, left, facing);

                    // And only now what the scripts wrote. A scene's bookkeeping is about
                    // the world after it, and the professor's says he is indoors.
                    foreach (ScriptRun ran in scene.Aftermath)
                    {
                        Remember(ran, script, network);

                        // After the walking, with the rest of the scene's bookkeeping.
                        // The rival leaves the lab by walking out of it and then not
                        // being there, and taking him off the map before he has walked
                        // is how a scene ends up with nobody in it.
                        TakeAway(ran, view, script, network);

                        // And what it handed over, which for a scene is here rather than
                        // where a conversation does it. Taking a fossil in MT. MOON is a
                        // scene — the man who wanted the other one walks over to take it
                        // — so the whole exchange went through this branch, and the
                        // fossil went nowhere: "Obtained the DOME FOSSIL!" on screen and
                        // nothing in the bag.
                        Handed(ran, script, network);
                    }

                    scene = null;
                    network.SendTalkFinished();
                }
            }
            else if (talking is not null)
            {
                talking.Update();

                if (talking is { IsFinished: true } answered)
                {
                    // A question is not the end of a script, it is the middle of one. The
                    // run stopped where it was asked because nothing in a save can answer
                    // it; now somebody has, so the rest of it runs with the answer in
                    // place — which is how a starter gets taken rather than declined.
                    (talking, scene, naming) = Answered(data, view, network, script, party, answered, waiting);

                    if (talking is null && scene is null && naming is null) network.SendTalkFinished();
                }
            }
            else if (DialogueBox.Pressed() && !player.IsStepping)
            {
                talking = Talk(data, view, player, network, script, party, rival, waiting);
            }

            exclaimFor = Math.Max(0f, exclaimFor - delta);

            // Nothing is read while somebody is on their way over. Refusing to predict
            // is the point: the server refuses the step either way, and a client that
            // predicts one anyway spends the whole walk snapping backwards.
            // A scene reads exactly like a conversation here: it stops the world and it
            // does not move anybody. What plays out is other people's, and when it is
            // over the player is standing where they were and free to walk to it.
            holdInput = Math.Max(0f, holdInput - delta);

            // A name being typed stops the world for the same reason a conversation
            // does, and with more urgency: the arrow keys move a caret through a field
            // and would otherwise also walk the player out of the room they are being
            // asked the question in. The first run of this had the rival's challenge
            // fire while the box was still open.
            Direction? input =
                scene is null && talking is null && naming is null && !console.IsOpen &&
                watching is null && holdInput <= 0f
                    ? ReadDirection()
                    : null;

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
            else if (player.ToReport is { } report && scene is null)
            {
                // Told the moment a step begins, not when it finishes — it is already
                // predicted locally, so waiting would add a round trip of lag to every
                // square. A turn on the spot comes through here too, and used not to
                // come through anywhere.
                network.SendMove(report);
            }

            // Standing somewhere is the other way a script starts. Checked on arrival
            // rather than on setting off, because a trigger that fires as the foot
            // leaves the previous square runs a cutscene about a place the player is
            // not yet standing in.
            if (arrived && scene is null)
            {
                arrived = false;
                standingOn = player.Square;

                DialogueBox? before = talking;

                (talking, scene) = OnArrival(data, view, network, script, party, talking, waiting);

                // And whatever is waiting on the square, if the map itself had nothing.
                // This used to be unreachable: arriving through a door landed you on the
                // door, and the square in front of it was reached by walking, which is
                // the branch below. Now that a doorway is stepped out of, the first
                // square somebody stands on can be a trigger — and standing somewhere is
                // standing somewhere however you came to be there.
                if (scene is null && ReferenceEquals(talking, before))
                    (talking, scene) = Arrive(data, view, player, network, script, party, talking, rival, waiting);
            }
            else if (!player.IsStepping && player.Square != standingOn && scene is null)
            {
                standingOn = player.Square;

                (talking, scene) = Arrive(data, view, player, network, script, party, talking, rival, waiting);
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

                // The mark over the head of whoever just noticed. Drawn with rectangles
                // rather than read off the cartridge, and it is placeholder exactly like
                // the rest of the chrome — but a walk that starts with no warning at all
                // reads as the game deciding something on its own.
                if (exclaimFor > 0f && watching == KeyOf(view.People, standing)) DrawExclamation(ox, oy);
            }

            foreach (RemoteCharacter other in others.Values)
            {
                (float x, float y) = other.PixelPosition;

                if (other.Arc > 0f) DrawShadow(x, y);

                if (sprite is not null) sprite.Draw(x, y - other.Arc, other.Facing, other.IsMoving, other.Id);
                else DrawPlayer(x, y - other.Arc, other.Facing, new Color(120, 200, 255, 255));

                DrawWorn(other.Looks, x, y - other.Arc);

                // Stacked when somebody is standing on somebody. Players walk through each
                // other by design, so sharing a square is ordinary rather than a glitch —
                // and two name tags drawn in the same place are one unreadable smudge.
                DrawNameTag(other.Name, x, y - other.Arc - Stacked(others, other) * 10);
            }

            // Off the ground while hopping, and the shadow stays where the feet would
            // be — without it a raised sprite reads as a taller sprite rather than as
            // one in the air.
            if (player.Arc > 0f) DrawShadow(playerX, playerY);

            DrawWorn(looks, playerX, playerY - player.Arc);

            if (sprite is not null)
                sprite.Draw(playerX, playerY - player.Arc, player.Facing, player.IsStepping, player.StepsTaken);
            else
                DrawPlayer(playerX, playerY - player.Arc, player.Facing);
            Raylib.EndMode2D();

            if (fadingIn > 0f)
            {
                fadingIn = Math.Max(0f, fadingIn - delta);

                Raylib.DrawRectangle(
                    0, 0, WindowWidth, WindowHeight,
                    new Color((byte)0, (byte)0, (byte)0, (byte)(255 * (fadingIn / FadeSeconds))));
            }

            DrawStatus(
                view.Map, player, network, others.Count, money, bag.Count, camera.Target, sprite, worstFrame,
                scene?.Progress);
            // A scene's line goes in the same box a conversation's does. There is only
            // one box on screen and only one thing being said at a time.
            (scene?.Saying ?? talking)?.Draw(WindowWidth, WindowHeight);

            // Over the box rather than instead of it: the question that led here is
            // still the last thing said, and a screen that clears it reads as a
            // different scene.
            naming?.Draw(WindowWidth, WindowHeight);

            // Over everything, because it is the one thing on screen that is not the game.
            console.Draw(WindowWidth, WindowHeight);
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
    /// <summary>
    /// What the last press of the button came to.
    /// <para>
    /// There is a server line for every conversation — "talked to 3, held still" — and
    /// nothing at all for what this side did with it. Pressing the button at somebody
    /// and getting silence has four completely different causes and they are
    /// indistinguishable from the outside: nobody in front, nobody with a script, a
    /// script that ran and said nothing, and a box that opened and was not drawn.
    /// </para>
    /// </summary>
    private static readonly List<string> Talks = ["nothing yet"];

    /// <summary>
    /// Says something on the client's own terminal, as well as on the status bar.
    /// <para>
    /// The server has printed a line for everything it decides since the day it had
    /// anything to decide, and this side has printed nothing since it was written. A
    /// screenshot shows the last thing that happened; a terminal shows the order things
    /// happened in, which is the difference between "the box did not open" and "the box
    /// did not open and here is what ran instead".
    /// </para>
    /// </summary>
    private static void Note(string what)
    {
        Console.WriteLine($"  {what}");

        // The last three rather than the last one. A status bar that shows only the most
        // recent thing is a status bar that has already forgotten the interesting one by
        // the time anybody looks at it — press the button at two people and the first
        // answer is gone.
        Talks.Add(what);

        while (Talks.Count > 3) Talks.RemoveAt(0);
    }

    private static DialogueBox? Talk(
        GameData data, MapView view, WalkingCharacter player, NetworkClient network, ScriptState script,
        IReadOnlyList<SavedMon> party, RivalFight rival, ScriptedFight waiting)
    {
        // Where the server says people are, which after a few seconds of wandering is
        // nowhere near where the cartridge put them.
        Dictionary<int, GridPosition> live = view.People.ToDictionary(p => p.Key, p => p.Value.Square);

        // Only the people the server has said are here. Six hundred objects in this game
        // are behind a flag, and the drawing already leaves those out — but the list the
        // cartridge holds does not, so until now the button could be pressed at somebody
        // who is not on the map, and they would answer.
        //
        // The cartridge's list is still the fallback, for the moment between arriving
        // somewhere and being told who is on it.
        IReadOnlyList<MapObject> here = view.People.Count > 0
            ? [.. view.Map.Objects.Where(o => view.People.ContainsKey(o.LocalId))]
            : view.Map.Objects;

        // The map's own walkability, not the grid the client predicts against — that one
        // has people in it, and a person is not a counter.
        if (Interaction.InFrontOf(
                player.Square, player.Facing, here, live,
                square => !view.Map.Collision.IsWalkable(square)) is not { } person)
        {
            // Nobody there, but there may still be something written there. A sign is
            // not a person: it has no local id, occupies no square anybody could stand
            // on, and there is nothing for the server to arbitrate — nobody stands still
            // to be read, nothing changes hands, and the words are on an image the
            // server has never seen. So this one never leaves the machine.
            Note($"nobody in front of {player.Square} facing {player.Facing}");

            // Water ahead and somebody in the party who can cross it. Asked rather than
            // done: this side knows the party and can see the square, but a client that
            // put itself out to sea would be a client that could put itself anywhere.
            //
            // Before the sign, because a sign is something written on a wall and the sea
            // is not a wall anybody writes on.
            GridPosition ahead = player.Square.Step(player.Facing);

            if (view.Map.IsWater(ahead))
            {
                if (data.MoveNamed("SURF") is not { } surf)
                {
                    Note("water ahead, but this cartridge has no move called SURF");
                }
                else if (ScriptState.SlotKnowing(party.Select(m => m.Moves), surf.Id) == ScriptState.NoSlot)
                {
                    Note($"water at {ahead}, and nobody in the party knows move {surf.Id}");
                }
                else
                {
                    network.SendSurf();
                    return null;
                }
            }
            else
            {
                Note($"{ahead} is not water");
            }

            return Read(data, view, player, network, script, party);
        }

        // Except in the rooms that are held quiet, where nobody is spoken to at all.
        // Nothing is sent, because there is nothing for the far end to arbitrate.
        if (view.Map.PeopleAreSilent)
        {
            Note($"object {person.LocalId} is in a room whose people are held quiet");
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
        // With the party attached, because two hundred objects in this game open by
        // asking who in it knows a particular move. Run without one, every cut tree in
        // the world reads as though the lead could fell it.
        // Which object this conversation is about. The ball on the professor's table
        // takes itself off the map with `0x53 0x800F` — the command that removes an
        // object, reading a variable its own script never writes. Nothing inside the
        // script language could have put a number there, and the only number a person's
        // own script could mean is the person. Seeded here because this is the one place
        // that knows it.
        script.Write(TalkingTo, person.LocalId);

        ScriptRun run = person.HasScript
            ? ScriptRunner.Run(data.Rom, person.ScriptAddress, script.WithParty(party.Select(m => m.Moves)))
            : new ScriptRun();

        // Applied on both sides rather than waiting to be told. The server is where
        // these live, but the next line this person reads is decided here and it would
        // be decided from yesterday's flags for as long as the round trip takes.
        // Whether the fight this just picked, if it picked one, is with him.
        if (run.TrainerId is not null && run.NamesRival) rival.Next = true;

        foreach (int flag in run.FlagsSet) script.Set(flag);
        foreach (int flag in run.FlagsCleared) script.Clear(flag);
        foreach ((int id, int value) in run.VariablesWritten) script.Write(id, value);

        if (run.FlagsSet.Count + run.FlagsCleared.Count + run.VariablesWritten.Count > 0)
            network.SendScriptRan(run);

        // And off the map, if what was just talked to was a ball on the ground.
        PickedUp(run, person, view, script, network);

        // And into the bag, if they handed something over while talking. This was the
        // one door into the bag that a conversation did not have: a ball on the ground
        // came through the branch above, a fossil came through a scene's aftermath, and
        // a gym leader's TM came off the fight — so the man at the top of NUGGET BRIDGE
        // said "tester received a NUGGET from the mystery TRAINER!" to a bag with a
        // POKe BALL in it and nothing else, and nothing anywhere reported a problem.
        Handed(run, script, network);
        Fought(run, script, network, waiting);

        // A counter that heals asks, and until now this project answered for the player.
        // The yes and the no are inside a standard routine — code, never followed here —
        // so the words stay hers and only the box is ours.
        //
        // Asked of the living population rather than of the cartridge's own record. The
        // record's Heals is filled in by the export, from a scan of every map at once,
        // and the client never runs that scan — so reading it here got false for every
        // nurse in the game, and she asked her question with a "Z to close" under it.
        bool heals = view.People.TryGetValue(person.LocalId, out WalkingPerson? standing) && standing.Heals;

        DialogueBox? box = person.HasScript
            ? new DialogueBox(run.Pages, run.Question, asks: heals && party.Count > 0)
            : null;

        Note(
            !person.HasScript
                ? $"person {person.LocalId} at {person.Square} has no script"
                : $"person {person.LocalId} script 0x{person.ScriptAddress:X8}: {run.Pages.Count} pages" +
                  (run.StoppedAt is { } stopper ? $", stopped at 0x{stopper:X2}" : "") +
                  $", box {(new DialogueBox(run.Pages).IsEmpty ? "empty" : "opens")}");

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
    /// Runs whatever this square runs, if it runs anything.
    /// <para>
    /// The other way a script starts, and the one most of a Pokémon game's story is made
    /// of: nothing is talked to, it happens because you stood somewhere. The professor
    /// stopping you at the edge of town, the rival waiting on a route.
    /// </para>
    /// <para>
    /// The condition is checked here as well as on the server, and both need it. This
    /// side needs it to know whether to open a box at all; that side needs it because a
    /// client is a thing a player can rewrite, and "I stepped on the rival's square
    /// again" would otherwise be a fight that can be had forever.
    /// </para>
    /// </summary>
    private static (DialogueBox? Talking, Cutscene? Scene) Arrive(
        GameData data, MapView view, WalkingCharacter player, NetworkClient network, ScriptState script,
        IReadOnlyList<SavedMon> party, DialogueBox? talking, RivalFight rival, ScriptedFight waiting)
    {
        if (view.Map.Triggers.FirstOrDefault(t =>
                t.Square == player.Square && t.HasScript && t.Armed(script.Read(t.Variable))) is not { } trigger)
        {
            return (talking, null);
        }

        // Which fight, if any, this square comes to for this save. Run on a copy and
        // thrown away: the real run happens in Play a moment later, and what is wanted
        // here is only the trainer's number.
        //
        // It has to be looked for rather than read off the trigger, because the rival at
        // the lab door is three trainers and the script picks between them on which
        // starter was taken. The server holds the set and checks this against it.
        ScriptRun peek = ScriptRunner
            .Run(data.Rom, trigger.ScriptAddress, script.Copy().WithParty(party.Select(m => m.Moves)));

        int? fight = peek.TrainerId;

        if (fight is not null && peek.NamesRival) rival.Next = true;

        // Sent whether or not there is anything to read, for the same reason talking is:
        // what happens next is not this side's decision. Nineteen of these squares start
        // a fight, and gating the message on finding dialogue would mean a rival who
        // says nothing could never challenge anybody.
        //
        // And sent before Play, not after. Play tells the server what the script wrote,
        // and the last thing a story script writes is the variable that disarms its own
        // square — so a trigger message arriving afterwards is a trigger the server has
        // already spent.
        network.SendTriggerFired(player.Square.X, player.Square.Y, fight);

        return Play(data, view, network, script, party, talking, [trigger.ScriptAddress], waiting);
    }

    /// <summary>
    /// Runs whatever the map itself runs on arrival, if anything is armed.
    /// <para>
    /// The fifth list, and the third way a script starts. Nothing was stepped on and
    /// nobody was spoken to: you came through a door, and the map had something waiting
    /// for the state you came through it in. It is what carries the story between the
    /// scenes attached to squares — the professor's lab has three of those waiting on a
    /// variable that nothing in the world's people, signs or triggers ever sets.
    /// </para>
    /// <para>
    /// Nothing is sent to say this happened, unlike a trigger. The server reads the same
    /// conditions out of its own world file and the same variables out of its own copy of
    /// the save, so it already knows — and a message would only be a chance to disagree.
    /// </para>
    /// </summary>
    private static (DialogueBox? Talking, Cutscene? Scene) OnArrival(
        GameData data, MapView view, NetworkClient network, ScriptState script,
        IReadOnlyList<SavedMon> party, DialogueBox? talking, ScriptedFight waiting)
    {
        List<uint> armed = MapEntryScript.ArmedIn(view.Map.OnEntry, script.Read);

        if (armed.Count == 0) return (talking, null);

        Note($"arriving on {view.MapId} runs {armed.Count}: " +
             string.Join(", ", armed.Select(a => $"0x{a:X8}")));

        return Play(data, view, network, script, party, talking, armed, waiting);
    }

    /// <summary>
    /// Runs a script and turns it into whatever the player should be looking at.
    /// <para>
    /// Shared by the two ways a script starts without being spoken to, because what
    /// happens after the address is decided is identical: run it, tell the server what it
    /// wrote down, and hand back either a box or a scene.
    /// </para>
    /// </summary>
    private static (DialogueBox? Talking, Cutscene? Scene) Play(
        GameData data, MapView view, NetworkClient network, ScriptState script,
        IReadOnlyList<SavedMon> party, DialogueBox? talking, IReadOnlyList<uint> addresses,
        ScriptedFight waiting)
    {
        // All of them, in the order the cartridge wrote them, rather than the first.
        // A doorway can have more than one thing armed at once — the professor's lab has
        // two on the same value of the same variable — and taking the first meant taking
        // the one whose read stops at its first command and does nothing. The scene that
        // carries the story was second in the list.
        var beats = new List<SceneBeat>();
        var pages = new List<string>();
        var later = new List<ScriptRun>();

        foreach (uint address in addresses)
        {
            ScriptRun run = ScriptRunner.Run(data.Rom, address, script.WithParty(party.Select(m => m.Moves)));

            // A scene's writes wait for the scene. See Cutscene.Aftermath: the last thing
            // the professor's script does is set the flag that means he has gone inside,
            // and applied before he walks it takes him off the map for the whole of his
            // own scene.
            if (run.IsScene)
            {
                later.Add(run);
                beats.AddRange(run.Beats);

                continue;
            }

            Remember(run, script, network);
            TakeAway(run, view, script, network);
            pages.AddRange(run.Pages);
        }

        Note($"ran {addresses.Count}: {beats.Count} beats, {pages.Count} pages");

        if (beats.Count > 0)
        {
            var playing = new Cutscene(beats, view, later);

            // Held before a foot is moved, and not by talking to them. Talking checks
            // that somebody is within reach — rightly, since a conversation across a town
            // is not one — and a scene's cast is across the town by definition. The
            // professor starts his walk from outside his own lab.
            if (playing.Cast.ToList() is { Count: > 0 } cast) network.SendSceneCast(cast);

            return (talking, playing);
        }

        var box = new DialogueBox(pages);

        return (box.IsEmpty ? talking : box, null);
    }

    /// <summary>
    /// Carries a script on past the question it stopped at, with the answer written down.
    /// <para>
    /// 0x800D is where the games put it and where the script looks for it: every question
    /// in this game is a <c>callstd 5</c> followed at once by a compare on that variable.
    /// Running past it instead of stopping meant reading whatever happened to be there,
    /// which on a fresh save is nought, and nought is no — so every offer in the game was
    /// being declined before anybody saw it.
    /// </para>
    /// </summary>
    private static (DialogueBox? Talking, Cutscene? Scene, NamingScreen? Naming) Answered(
        GameData data, MapView view, NetworkClient network, ScriptState script,
        IReadOnlyList<SavedMon> party, DialogueBox asked, ScriptedFight waiting)
    {
        // A question with no script behind it, which is the counter in a POKeMON
        // CENTER. Nothing to carry on from; there is only the answer to send.
        if (asked.Resume is null)
        {
            if (asked.Asks && asked.Answer) network.SendHeal();

            return (null, null, null);
        }

        if (asked.Resume is not { } from) return (null, null, null);

        script.Write(0x800D, asked.Answer ? 1 : 0);

        ScriptRun run = ScriptRunner.Run(data.Rom, from, script.WithParty(party.Select(m => m.Moves)));

        Note(
            $"answered {(asked.Answer ? "yes" : "no")}, carried on from 0x{from:X8}: " +
            $"{run.Pages.Count} pages, {run.Beats.Count} beats");

        // A run that called into code it could not read, having just written the slot
        // that code was going to work on, is the naming screen — and it is the only
        // thing in the opening that is either. Three scripts in the whole cartridge end
        // by returning from code, so this is not a net that catches much.
        //
        // The screen has to be ours: 0x081A74EB is a keyboard drawn by the game itself,
        // and no amount of adopting command widths will ever decode a keyboard.
        if (run.CodeCalled.Count > 0 && script.Read(NamingSlot) is var slot && slot < party.Count)
        {
            string species = data.SpeciesAt(party[slot].Species)?.Name ?? "it";

            Note($"the script asked for a name for slot {slot} ({species})");

            // The rest of the run is kept rather than played. The naming screen sits in
            // the middle of it — the call the cartridge makes to its keyboard is followed
            // by the goto that leads to the rival taking his own — so what comes after
            // the name is what came after the call.
            return (null, null, new NamingScreen(slot, species, data.SuggestedNames) { Rest = run });
        }

        (DialogueBox? box, Cutscene? scene) = Present(data, view, network, script, run, waiting);

        return (box, scene, null);
    }

    /// <summary>
    /// Turns a finished run into whatever the player should be looking at.
    /// <para>
    /// Shared because there are now three ways to arrive here — answering a question,
    /// naming something, and the run that started it all — and the difference between a
    /// scene and a box is not a thing any of them should decide separately.
    /// </para>
    /// </summary>
    private static (DialogueBox? Talking, Cutscene? Scene) Present(
        GameData data, MapView view, NetworkClient network, ScriptState script, ScriptRun run,
        ScriptedFight waiting)
    {
        // What comes after an answer is not always more talking. Saying yes to the ball
        // on the professor's table runs on into the rival taking his and walking over,
        // which is a scene — and a scene handed to a text box is a text box with nothing
        // in it, which is exactly what "the box went away and nothing happened" was.
        if (run.IsScene)
        {
            var playing = new Cutscene(run.Beats, view, [run]);

            if (playing.Cast.ToList() is { Count: > 0 } cast) network.SendSceneCast(cast);

            return (null, playing);
        }

        Remember(run, script, network);
        TakeAway(run, view, script, network);

        Handed(run, script, network);
        Fought(run, script, network, waiting);

        var box = new DialogueBox(run.Pages, run.Question);

        return (box.IsEmpty && !box.IsQuestion ? null : box, null);
    }

    /// <summary>
    /// Applies what a script wrote down, here and on the server.
    /// <para>
    /// On both sides rather than waiting to be told. The server is where these live, but
    /// the next line somebody reads is decided here and it would be decided from
    /// yesterday's flags for as long as the round trip takes.
    /// </para>
    /// </summary>
    private static void Remember(ScriptRun run, ScriptState script, NetworkClient network)
    {
        foreach (int flag in run.FlagsSet) script.Set(flag);
        foreach (int flag in run.FlagsCleared) script.Clear(flag);
        foreach ((int id, int value) in run.VariablesWritten) script.Write(id, value);

        if (run.FlagsSet.Count + run.FlagsCleared.Count + run.VariablesWritten.Count > 0)
            network.SendScriptRan(run);
    }

    /// <summary>
    /// Names whatever a run handed over, for the server to check.
    /// <para>
    /// Which item, if any, depends on the branch taken — "Do you want the DOME FOSSIL?"
    /// — and a branch is a fact about a save. So the client says which and the server
    /// checks it against the set that person's script could ever produce, which is the
    /// arrangement a trigger's trainer id already uses.
    /// </para>
    /// <para>
    /// 0x800F holds who the conversation is about. It is seeded when one starts, which is
    /// what makes this possible without threading a local id through every scene.
    /// </para>
    /// </summary>
    private static void Handed(ScriptRun run, ScriptState script, NetworkClient network)
    {
        if (run.GivesItem is not { } given) return;

        int who = script.Read(TalkingTo);

        if (who is <= 0 or >= 64) return;

        network.SendScriptGave(who, given);
    }

    /// <summary>
    /// Tells the server about a fight a script started.
    /// <para>
    /// Same shape as handing something over, and for the same reason: the client runs
    /// the script and the server runs the battle. What travels is a species and a level,
    /// which the server checks against the list the world file carries for that person
    /// before it fields anything.
    /// </para>
    /// </summary>
    private static void Fought(
        ScriptRun run, ScriptState script, NetworkClient network, ScriptedFight waiting)
    {
        if (run.WildBattle is not { } fight) return;

        int who = script.Read(TalkingTo);

        if (who is <= 0 or >= 64) return;

        // Where to pick the script up once the fight is over. Kept even though the server
        // may yet refuse the fight: a note nobody reads costs nothing, and the next thing
        // that starts a fight overwrites it.
        waiting.ResumesAt = run.ResumesAfterTheFight;

        network.SendScriptFought(who, fight.Species, fight.Level);
    }

    /// <summary>
    /// Takes the people a script removed off the map, on both sides.
    /// <para>
    /// Through the flag the object already carries rather than through a message of its
    /// own, because that flag is the one thing about it both halves already agree on:
    /// the server reads it out of the world file to decide who a player can see, and it
    /// is saved, so somebody who takes a ball and signs out finds it still gone.
    /// </para>
    /// <para>
    /// An object with no flag is left alone. Six hundred and five in this cartridge have
    /// one and the rest do not, and inventing a number for those would be writing to a
    /// flag space this project does not own.
    /// </para>
    /// </summary>
    private static void TakeAway(ScriptRun run, MapView view, ScriptState script, NetworkClient network)
    {
        TakeAway(run.Hides, view, script, network);
    }

    /// <summary>
    /// What a trainer says on the way into a fight, off their own script.
    /// <para>
    /// Which script that is has to be looked up rather than remembered, exactly as the
    /// afterwards is: the world knows which person or which square on this map fields
    /// this trainer, and a note kept across the moment a fight starts is a note that can
    /// be wrong. The rival at the lab door is three trainers behind one square, so the
    /// lookup is by what the script <em>can</em> field, not by a number stored on it.
    /// </para>
    /// <para>
    /// Nothing here is load-bearing. A trainer whose script cannot be found, or whose
    /// fight is the one variant with no intro text, opens the fight the way every fight
    /// in this project has opened until now.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string>? Challenge(
        GameData data, MapView view, ScriptState script, int? trainerId)
    {
        if (trainerId is not { } id) return null;

        uint? theirs =
            view.Map.Triggers.FirstOrDefault(t => t.HasScript && t.Fields(id))?.ScriptAddress
            ?? view.Map.Objects.FirstOrDefault(o => o.HasScript && o.TrainerId == id)?.ScriptAddress;

        if (theirs is not { } start) return null;
        if (ScriptReader.BeforeTheFight(data.Rom, start, id) is not { } said) return null;

        List<string> pages = ScriptRunner.Speech(data.Rom, said, script);

        return pages.Count > 0 ? pages : null;
    }

    /// <summary>
    /// A ball on the ground, once it has been picked up.
    /// <para>
    /// Nothing in its script says so. All four commands of one are two arguments and a
    /// call into a standard routine — no <c>setflag</c> anywhere, which is why TM09 in
    /// MT. MOON went into the bag and stayed lying on the floor. The vanishing is the
    /// game's own code, and the only thing this side can read is the flag in the object's
    /// record: 170 of the cartridge's 170 balls carry one, which is a rule rather than a
    /// coincidence.
    /// </para>
    /// <para>
    /// Told apart from a person by the cartridge's own difference: a thing that hands
    /// something over and says nothing at all is a ball, and the fifteen people who hand
    /// something over <em>while</em> talking stay exactly where they are. The president of
    /// SILPH does not disappear on handing over a MASTER BALL.
    /// </para>
    /// </summary>
    private static void PickedUp(
        ScriptRun run, MapObject person, MapView view, ScriptState script, NetworkClient network)
    {
        if (run.GivesItem is null || run.Pages.Count > 0) return;

        TakeAway([person.LocalId], view, script, network);
    }

    private static void TakeAway(
        IReadOnlyList<int> hides, MapView view, ScriptState script, NetworkClient network)
    {
        if (hides.Count == 0) return;

        var gone = new List<int>();

        foreach (int localId in hides)
        {
            if (view.Map.Objects.FirstOrDefault(o => o.LocalId == localId) is not { HiddenBy: > 0 } person)
            {
                Note($"script took object {localId} off the map, but it carries no flag to remember that by");
                continue;
            }

            if (!script.Set(person.HiddenBy)) continue;

            gone.Add(person.HiddenBy);
            view.Remove(localId);
        }

        if (gone.Count > 0) network.SendFlagsSet(gone);
    }

    /// <summary>
    /// How this client reads a ledge, handed to whatever is walking.
    /// <para>
    /// The client's half of a rule the server keeps too, off the same behaviour bytes:
    /// a client that predicted a hop the server refuses would jump two squares and be
    /// pulled back, and one that refused a hop the server allows would stand at the
    /// edge pressing a direction that quietly works for everybody else.
    /// </para>
    /// </summary>
    private static Func<GridPosition, Direction, GridPosition?> HopsOn(MapView view) =>
        (square, facing) =>
        {
            if (view.Map.Behaviours.Length == 0) return null;
            if (!view.Collision.Contains(square)) return null;

            int at = square.Y * view.Collision.Width + square.X;

            if (at >= view.Map.Behaviours.Length) return null;
            if (MetatileBehaviour.Hop(view.Map.Behaviours[at]) != facing) return null;

            GridPosition landing = square.Step(facing);

            return view.Collision.IsWalkable(landing) ? landing : null;
        };

    /// <summary>
    /// True when this room heals, which is where the box is offered.
    /// <para>
    /// The client's half of a rule the server also keeps, off the same field of the same
    /// world file. It used to be a square — behaviour 0x6A, in the corner of every healing
    /// centre — and 0x6A is the stairs. There is no byte for a storage machine on this
    /// cartridge and no script for one either, so there is no square to face, and the room
    /// is what is left.
    /// </para>
    /// <para>
    /// Neither side is told by the other, and the server is the one that decides — this
    /// only stops the interface offering something that would be refused.
    /// </para>
    /// </summary>
    /// <summary>
    /// What somebody is wearing, drawn as coloured marks over their head.
    /// <para>
    /// Placeholder, and deliberately so. There is no art for a hat yet and there will not be
    /// until somebody draws one; what there is is the whole of the machinery that gets a hat
    /// from an account, through a server that decides whether it is owned, onto a wire, and
    /// into every other client on the map. Drawing that as a row of squares proves the part
    /// that is hard and admits the part that is not done.
    /// </para>
    /// <para>
    /// The same choice the walking figures make one screen away: <em>a rectangle is the
    /// fallback rather than a failure</em>.
    /// </para>
    /// </summary>
    private static void DrawWorn(Appearance looks, float x, float y)
    {
        if (looks.Worn.Count == 0) return;

        int at = 0;

        foreach ((CosmeticSlot slot, int id) in looks.InDrawingOrder())
        {
            Raylib.DrawRectangle((int)x + 2 + at * 5, (int)y - 20, 4, 4, MarkFor(slot));
            at++;
        }
    }

    /// <summary>A colour per slot, so a row of marks can be read at a glance.</summary>
    private static Color MarkFor(CosmeticSlot slot) => slot switch
    {
        CosmeticSlot.Hair => new Color(120, 72, 40, 255),
        CosmeticSlot.Eyes => new Color(90, 170, 220, 255),
        CosmeticSlot.Hat => new Color(220, 60, 60, 255),
        CosmeticSlot.Glasses => new Color(230, 230, 230, 255),
        CosmeticSlot.Scarf => new Color(230, 140, 60, 255),
        CosmeticSlot.Shirt => new Color(90, 200, 120, 255),
        CosmeticSlot.Pants => new Color(70, 90, 200, 255),
        CosmeticSlot.Skirt => new Color(200, 90, 190, 255),
        CosmeticSlot.Dress => new Color(230, 120, 200, 255),
        CosmeticSlot.Shoes => new Color(110, 90, 70, 255),
        CosmeticSlot.Cape => new Color(150, 60, 200, 255),
        _ => new Color(200, 200, 120, 255),
    };

    private static bool InARoomThatHeals(GameData data, MapView view, uint? healer) =>
        healer is { } script && view.Map.Objects.Any(o => HealerLocator.Heals(data.Rom, o, script));

    /// <summary>
    /// Reads whatever is written on the square in front, if anything is.
    /// <para>
    /// Signs are the fourth list in a map's events record and this project has never
    /// opened it, so every notice board, bookshelf and television in the world has been
    /// a solid block of scenery with nothing behind it. There are seven hundred of them.
    /// </para>
    /// <para>
    /// The buried items are in the same list and are skipped here. They are found by
    /// searching a square rather than by reading it, which is a different interaction
    /// and not one this game has yet.
    /// </para>
    /// </summary>
    private static DialogueBox? Read(
        GameData data, MapView view, WalkingCharacter player, NetworkClient network, ScriptState script,
        IReadOnlyList<SavedMon> party)
    {
        GridPosition front = player.Square.Step(player.Facing);

        if (view.Map.Signs.FirstOrDefault(s => s.Square == front && s.HasScript) is not { } sign) return null;

        ScriptRun run = ScriptRunner.Run(
            data.Rom, sign.ScriptAddress, script.WithParty(party.Select(m => m.Moves)));

        foreach (int flag in run.FlagsSet) script.Set(flag);
        foreach (int flag in run.FlagsCleared) script.Clear(flag);
        foreach ((int id, int value) in run.VariablesWritten) script.Write(id, value);

        // Told even though nobody is being held. What a sign changes is the same save
        // the people share, and a flag set by reading a notice board that never reached
        // the server is a flag that comes back unset on the next login.
        if (run.FlagsSet.Count + run.FlagsCleared.Count + run.VariablesWritten.Count > 0)
            network.SendScriptRan(run);

        var box = new DialogueBox(run.Pages);

        return box.IsEmpty ? null : box;
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
        BoxScreen? storing,
        PartyScreen? looking,
        ref DialogueBox? said,
        ref BattleScreen? battle,
        ref ShopScreen? shop,
        ref FerryScreen? boat,
        ref IReadOnlyList<BagEntry> bag,
        ref IReadOnlyList<SavedMon> party,
        ref IReadOnlyList<SavedMon> box,
        ref int boxSize,
        ref int money,
        ref (string MapId, GridPosition Square)? correction,
        ref Appearance looks,
        ref IReadOnlyList<int> owned,
        ref TradeScreen? trading,
        ref int askedBy,
        ref int challengedBy,
        ref int? watching,
        ref float exclaimFor,
        ref Cutscene? scene,
        ref bool arrived,
        ref float fadingIn,
        ref float holdInput,
        ref uint? afterTheFight,
        ref int? cameOut,
        BattleOutcomes? outcomes,
        RivalFight rival,
        ConsoleBox console)
    {
        foreach (NetMessage message in network.Drain())
        {
            switch (message)
            {
                case ConsoleReply reply:
                    console.Said(reply.Text);
                    break;

                case Welcome welcome:
                    // The title was set from whatever client.json asked for; the server
                    // decides where you actually are, and it is usually somewhere else.
                    if (view.SwitchTo(welcome.MapId)) Raylib.SetWindowTitle($"MonMMO — {view.Map.Name}");

                    player.Place(view.Collision, new GridPosition(welcome.X, welcome.Y), HopsOn(view));
                    bag = welcome.Bag;
                    party = welcome.Party;
                    box = welcome.Box;
                    boxSize = welcome.BoxSize;
                    if (battle is not null) battle.Party = party;
                    money = welcome.Money;

                    watching = null;

                    // Cleared first. This arrives again whenever the console changes any
                    // of it, and a flag that has been turned off has to actually go —
                    // adding to what was already here would make "clear" mean nothing.
                    script.Forget();

                    owned = welcome.Cosmetics;
                    looks = welcome.Looks;

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

                case TrainerSpotted noticed:
                    // Not a text box. A box has to be dismissed, and dismissing it means
                    // pressing a button through the walk it exists to announce.
                    watching = noticed.LocalId;
                    exclaimFor = ExclaimSeconds;

                    break;

                case ApproachEnded:
                    watching = null;

                    break;

                case SurfingChanged afloat:
                    // The grid changes before the player is placed, because placing them
                    // on a square this side still thinks is a wall would put them back
                    // on the shore a frame later.
                    view.Surfing = afloat.Surfing;

                    player.Place(view.Collision, new GridPosition(afloat.X, afloat.Y), HopsOn(view));

                    break;

                case PartyHealed healed:
                    party = healed.Party;
                    if (battle is not null) battle.Party = party;

                    // The counter says it out loud, in a box dismissed like any other.
                    // There is nothing to choose, so there is nothing to open.
                    said = new DialogueBox([
                        healed.Needed
                            ? "We've restored your POKeMON to full health."
                            : "Your POKeMON are all healthy already.",
                    ]);

                    break;

                case ItemFound found:
                    bag = found.Bag;

                    string line = found.Count > 1
                        ? $"Found {found.Count} {items.Of(found.ItemId)}!"
                        : $"Found one {items.Of(found.ItemId)}!";

                    // Added to whatever is already open rather than put in place of it.
                    // A ball on the ground has nothing open and gets a box of its own;
                    // somebody who hands this over mid-sentence is still mid-sentence.
                    if (said is { IsFinished: false }) said.Add(line);
                    else said = new DialogueBox([line]);

                    break;

                case WentInside inside:
                    // Same removal a felled tree gets, and for the same reason it is the
                    // server's to say: a client that takes people off its own map is a
                    // client that can clear a doorway by claiming a scene did it.
                    view.Remove(inside.LocalId);

                    break;

                case ObstacleShifted shifted:
                    // The tree comes down here rather than when the button was pressed.
                    // The client knows perfectly well who in the party knows CUT — it ran
                    // the script — but a client that removes its own obstacles is a
                    // client that can walk through walls by lying about its party.
                    view.Remove(shifted.LocalId);

                    break;

                case MoveOffered offered:
                    // Only the bag can be holding this. A machine is used from the bag
                    // and nowhere else, and a question with no screen to appear on would
                    // be an offer the server holds for ever.
                    carrying?.Apply(offered);

                    break;

                case BagUpdated updated:
                    bag = updated.Bag;
                    party = updated.Party;
                    if (battle is not null) battle.Party = party;
                    carrying?.Apply(updated);
                    storing?.Apply(party);
                    looking?.Apply(party);

                    break;

                case PartyOrdered ordered:
                    party = ordered.Party;
                    if (battle is not null) battle.Party = party;
                    looking?.Apply(party, ordered.Message);

                    break;

                case BoxUpdated stored:
                    party = stored.Party;
                    box = stored.Box;
                    boxSize = stored.BoxSize;
                    if (battle is not null) battle.Party = party;
                    storing?.Apply(stored);

                    break;

                case TrainerBeaten beaten:
                    // Winning is decided on the server. Until this arrives, running that
                    // trainer's script gives their opening line — because what the script
                    // asks is whether the fight has already happened.
                    script.MarkBeaten(beaten.TrainerId);

                    // And which script that was, so it can be run again once the screen
                    // closes. Looked up rather than remembered: the world already knows
                    // which square and which person on this map field this trainer, and
                    // a note kept across a fight is a note that can be wrong.
                    //
                    // This arrives only on a win, which is the only case that has an
                    // afterwards — a script does not carry on past a fight you lost.
                    uint? theirs =
                        view.Map.Triggers.FirstOrDefault(t => t.HasScript && t.Fields(beaten.TrainerId))
                            ?.ScriptAddress
                        ?? view.Map.Objects.FirstOrDefault(o => o.HasScript && o.TrainerId == beaten.TrainerId)
                            ?.ScriptAddress;

                    // The fight's own afterwards, when it has one, and their script from
                    // the top when it does not. Most trainers carry straight on from the
                    // command that started the fight, and running them again with the
                    // fight marked as done lands on exactly that line. A gym leader does
                    // not: the badge, the TM and five flags are behind a pointer the
                    // command carries, and BROCK spent a whole session saying "There are
                    // all kinds of TRAINERS in this huge world of ours" and handing over
                    // nothing because this ran his script from the top instead.
                    afterTheFight = theirs is { } start
                        ? ScriptReader.AfterTheFight(data.Rom, start, beaten.TrainerId) ?? start
                        : null;

                    // And who the afterwards is about, because a fight is a conversation
                    // with an interruption in it. MISTY's TM was refused by the server as
                    // coming from object 8 — the number left in 0x800F by whatever was
                    // talked to before the fight screen opened.
                    if (view.Map.Objects.FirstOrDefault(o => o.HasScript && o.TrainerId == beaten.TrainerId)
                        is { } fought)
                    {
                        script.Write(TalkingTo, fought.LocalId);
                    }

                    break;

                case PlayerAppeared appeared when appeared.PlayerId != network.PlayerId:
                    others[appeared.PlayerId] = new RemoteCharacter(
                        appeared.PlayerId, appeared.Name,
                        new GridPosition(appeared.X, appeared.Y), appeared.Facing)
                    {
                        Looks = appeared.Looks,
                    };
                    break;

                case AppearanceChanged dressed when dressed.PlayerId != network.PlayerId:
                    if (others.TryGetValue(dressed.PlayerId, out RemoteCharacter? wearer))
                        wearer.Looks = dressed.Looks;
                    break;

                case DuelAsked challenge:
                    challengedBy = challenge.FromPlayerId;
                    said = new DialogueBox([$"{challenge.FromName} wants a fight! Press Y."]);
                    break;

                case TradeAsked asked:
                    askedBy = asked.FromPlayerId;
                    Note($"{asked.FromName} wants to trade — Y to agree");
                    break;

                case TradeUpdated table:
                    askedBy = 0;

                    if (trading is null) trading = new TradeScreen(party, table, data);
                    else trading.Apply(table);
                    break;

                case TradeEnded over:
                    party = over.Party;
                    trading?.Apply(party);
                    trading?.Finish(over.Reason);
                    looking?.Apply(party);
                    storing?.Apply(party);
                    break;

                case AppearanceChanged mine when mine.PlayerId == network.PlayerId:
                    // Ours arrives the same way as anybody else's, and it has to: what was
                    // asked for and what was granted are not the same thing once a dress
                    // has taken a shirt and a pair of trousers off with it.
                    looks = mine.Looks;
                    break;

                case PlayerMoved moved when moved.PlayerId != network.PlayerId:
                    if (others.TryGetValue(moved.PlayerId, out RemoteCharacter? other))
                        other.MoveTo(new GridPosition(moved.X, moved.Y), moved.Facing);
                    break;

                // Somebody else went over a ledge. Two squares in one movement, and told
                // apart from a step so it can be drawn as the jump it was rather than as
                // a figure sliding twice as fast as anyone can walk.
                case PlayerHopped hopped when hopped.PlayerId != network.PlayerId:
                    if (others.TryGetValue(hopped.PlayerId, out RemoteCharacter? jumper))
                        jumper.HopTo(new GridPosition(hopped.X, hopped.Y), hopped.Facing);
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

                    // And so was everything a scene was about. A scene can now end by
                    // walking the player through a door, and the beats left over on the
                    // far side of it are about people who are not here — the same reason
                    // the others are cleared, one line up.
                    scene = null;
                    arrived = true;
                    fadingIn = FadeSeconds;
                    // A whole step, not the bare minimum. Arriving somewhere is a step —
                    // one was taken to get here — so the next one waits as long as any
                    // other would. Holding for exactly the server's limit put every
                    // arrival on the boundary of it, and half the time the boundary went
                    // the other way: "too fast: 0.20s since the last step, and the limit
                    // is 0.20s".
                    holdInput = WalkingCharacter.StepSeconds;

                    if (view.SwitchTo(changed.MapId))
                    {
                        player.Place(view.Collision, new GridPosition(changed.X, changed.Y), HopsOn(view));
                        Raylib.SetWindowTitle($"MonMMO — {view.Map.Name}");
                    }

                    break;

                // Our own hop, confirmed. Same as a step: where the server agrees this
                // costs nothing, and where it does not it is the only thing that puts us
                // back on the right side of the ledge.
                case PlayerHopped mine when mine.PlayerId == network.PlayerId:
                    correction = (view.MapId, new GridPosition(mine.X, mine.Y));
                    break;

                case PlayerMoved mine when mine.PlayerId == network.PlayerId:
                    // The server's answer about us. Where it agrees this costs nothing;
                    // where it does not, this is the only thing that puts us back.
                    correction = (view.MapId, new GridPosition(mine.X, mine.Y));
                    break;

                case MoveRejected rejected:
                    correction = (view.MapId, new GridPosition(rejected.X, rejected.Y));
                    break;

                case PlayerLeft left:
                    others.Remove(left.PlayerId);
                    break;

                case BattleStarted started:
                    // What they say on the way in, found the same way the afterwards is:
                    // by asking the map which script fields this trainer, and then asking
                    // that script's own fight command. Done here rather than in whatever
                    // started the fight, because the three ways a fight can start — a
                    // line of sight, a conversation, a square stepped on — lose the words
                    // in three different places, and this is the one place all three
                    // arrive at.
                    battle = new BattleScreen(
                        started, data, trainers, items,
                        calledInstead: rival.Take() ? script.RivalName : null,
                        challenge: Challenge(data, view, script, started.TrainerId))
                    {
                        // Who else could come out. Kept up to date below, because a party
                        // that goes stale mid-fight is a list offering somebody who
                        // fainted two turns ago.
                        Party = party,
                        Active = started.Slot,
                    };

                    // The walk is over the moment the fight begins, which is the ending
                    // almost every walk has.
                    watching = null;

                    break;

                case BattlerSentOut sent when sent.Side == Side.Player:
                    battle?.Apply(sent);

                    // And which slot, so the list offers the other five rather than the
                    // one already standing there.
                    if (battle is not null) battle.Active = sent.Slot;

                    break;

                case BattlerSentOut sent:
                    battle?.Apply(sent);
                    break;

                case ShopOpened opened:
                    shop = new ShopScreen(opened, items);
                    break;

                case FerryOpened asked:
                    boat = new FerryScreen(asked);
                    break;

                case ShopUpdated updated:
                    shop?.Apply(updated);
                    money = updated.Money;
                    bag = updated.Bag;
                    break;

                case BattleUpdate update:
                    battle?.Apply(update);

                    // A fight is where health changes, and the list of who could come out
                    // instead is the one thing that reads it every turn.
                    if (update.Party is { } standing)
                    {
                        party = standing;
                        if (battle is not null) battle.Party = standing;
                    }

                    break;

                case BattleFinished finished:
                    battle?.Apply(finished);
                    money = finished.Money;

                    // How it went, in the cartridge's numbering rather than in ours. A
                    // loss is left out on purpose: the script does not carry on past a
                    // fight that ended with the player face down in a POKeMON CENTER,
                    // and neither does this.
                    cameOut = outcomes is null || finished.Winner == Side.Opponent
                        ? null
                        : finished.Caught ? outcomes.Caught
                        : finished.Winner == Side.Player ? outcomes.Won
                        : outcomes.Ran;

                    // A fight is where health actually changes. Without this the bag
                    // would open on a party that was last accurate at login and offer
                    // a potion to somebody who is already full.
                    party = finished.Party;
                    box = finished.Box;
                    looking?.Apply(party);
                    storing?.Apply(party);

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

        // On a whole pixel, for the same reason a character is. The camera is what every
        // tile in the world is drawn relative to, so half a pixel here is half a pixel on
        // four hundred tiles at once — and at three times scale with point filtering, the
        // rounding lands differently for each of them. It reads as the whole map
        // shimmering rather than as the camera being slightly off.
        return new System.Numerics.Vector2(MathF.Round(x), MathF.Round(y));
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

    /// <summary>
    /// How many other people are already drawn on this square, so a name tag can be lifted
    /// clear of theirs. Counted by id order rather than by draw order, so the pile does not
    /// reshuffle itself every frame.
    /// </summary>
    private static int Stacked(Dictionary<int, RemoteCharacter> others, RemoteCharacter who) =>
        others.Values.Count(o => o.Id < who.Id && o.Square == who.Square);

    private static void DrawNameTag(string name, float x, float y)
    {
        int width = Raylib.MeasureText(name, 8);
        int left = (int)x + WalkingCharacter.SquarePixels / 2 - width / 2;

        Raylib.DrawText(name, left + 1, (int)y - 8, 8, Color.Black);
        Raylib.DrawText(name, left, (int)y - 9, 8, Color.White);
    }

    private static void DrawStatus(
        LoadedMap map, WalkingCharacter player, NetworkClient network, int others, int money, int carrying,
        System.Numerics.Vector2 camera, CharacterSprite? sprite, float worstFrame, string? scene)
    {
        // A plaque for the place and a strip for the party, and everything else behind
        // a key. What was here before was six lines of diagnostics drawn over the world
        // at all times — indispensable while the world was being built and, once it was,
        // the first thing anybody looking at the screen sees.
        DrawPlace(map, player, money, carrying, network, others);

        if (!Raylib.IsKeyDown(KeyboardKey.F1)) return;

        DrawDiagnostics(map, player, camera, sprite, worstFrame, scene);
    }

    /// <summary>Where you are and what you are carrying, as a plaque in the corner.</summary>
    private static void DrawPlace(
        LoadedMap map, WalkingCharacter player, int money, int carrying, NetworkClient network, int others)
    {
        PixelFont font = Skin.Font;

        string place = GameText.ToAscii(map.Name);
        string under = $"${money}   {carrying} items";

        int width = Math.Max(font.Measure(place, 3), font.Measure(under, 2)) + 36;

        var box = new Rectangle(16, 14, width, 62);

        Skin.DrawPanel(box);

        font.DrawShadowed(place, 32, 26, 3, Skin.Ink, new Color(0, 0, 0, 120));
        font.Draw(under, 32, 52, 2, Skin.InkDim);

        // Connection, as a dot rather than a sentence. Green is fine, amber is trying,
        // red is not — and the only time it needs words is when it is broken.
        Color light = network.Failure is not null
            ? Skin.HpPoor
            : network.IsConnected ? Skin.HpGood : Skin.HpFair;

        Raylib.DrawCircle((int)(box.X + box.Width - 16), (int)box.Y + 18, 5, light);

        if (network.IsConnected && others > 0)
            font.DrawRight($"{others} here", box.X + box.Width - 28, box.Y + 14, 2, Skin.InkFaint);

        if (network.Failure is { } failure)
        {
            var strip = new Rectangle(16, box.Y + box.Height + 8, font.Measure(failure, 2) + 32, 28);

            Skin.DrawPanel(strip, fill: Skin.HpPoor);
            font.Draw(GameText.ToAscii(failure), 32, strip.Y + 10, 2, Skin.InkOnLight);
        }
    }

    /// <summary>
    /// The numbers that were on screen at all times until now, kept because they earned
    /// their keep and moved behind F1 because they had stopped earning it every frame.
    /// <para>
    /// "My character turned invisible" has three completely different causes and no way
    /// to tell them apart by looking: the figure is somewhere the camera is not, the
    /// camera is somewhere the map is not, or the cartridge's own sprite never loaded and
    /// what is drawn is the white placeholder — invisible on the professor's white floor
    /// and perfectly visible on grass, which is exactly the shape of that report.
    /// </para>
    /// </summary>
    private static void DrawDiagnostics(
        LoadedMap map, WalkingCharacter player, System.Numerics.Vector2 camera,
        CharacterSprite? sprite, float worstFrame, string? scene)
    {
        PixelFont font = Skin.Font;

        (float px, float py) = player.PixelPosition;

        var lines = new List<string>
        {
            $"{map.Name} ({map.Bank}.{map.Number}) at {player.Square.X},{player.Square.Y}",
            $"you {px:F0},{py:F0}   map {map.PixelWidth}x{map.PixelHeight}   camera {camera.X:F0},{camera.Y:F0}",

            // Frame time as well as position, because "jittery" is a word and this is a
            // number. A stutter is one long frame, so the longest recent one is the
            // reading that matters — an average hides the thing being complained about.
            $"{Raylib.GetFPS()} fps, worst {worstFrame * 1000f:F0} ms   " +
            (sprite is null ? "NO SPRITE" : "sprite ok"),
        };

        if (scene is not null) lines.Add(scene);

        lines.AddRange(Talks);

        int width = lines.Max(l => font.Measure(l, 2)) + 32;
        var box = new Rectangle(16, 90, width, lines.Count * 20 + 22);

        Skin.DrawPanel(box, fill: new Color(16, 18, 28, 225));

        for (int i = 0; i < lines.Count; i++)
            font.Draw(lines[i], 32, box.Y + 12 + i * 20, 2, i < 3 ? Skin.Ink : Skin.InkDim);
    }

    /// <summary>Opens a window that just explains what went wrong, rather than exiting silently.</summary>
    private static void ShowMessageWindow(params string[] lines)
    {
        foreach (string line in lines) Console.WriteLine(line);

        Raylib.InitWindow(WindowWidth, WindowHeight, "MonMMO");
        Raylib.SetTargetFPS(30);

        Skin.Font = PixelFont.Build();

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
