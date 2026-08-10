using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.World;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Maps;
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
            ShowMessageWindow(
                "No cartridge configured.",
                $"Put the path to your own .gba file in {ClientSettings.FileName}",
                Path.Combine(directory, ClientSettings.FileName),
                "",
                "or run:  monmmo --rom <path to your .gba> --map \"pallet town\"",
                "",
                "The file is read locally and never leaves this machine.");

            return 1;
        }

        GameData data;
        LoadedMap map;

        try
        {
            // Opened once: locating the tables scans the whole cartridge several
            // times, which is fine at startup and not fine mid-encounter.
            data = GameData.Load(settings.RomPath);
            map = WorldLoader.Load(data.Rom, settings.MapName, settings.MapAddress);
        }
        catch (Exception ex)
        {
            ShowMessageWindow("Could not load a map.", "", ex.Message);
            return 1;
        }

        using var network = new NetworkClient();

        if (!string.IsNullOrWhiteSpace(settings.Server))
        {
            try
            {
                (string host, int port) = ParseServer(settings.Server);
                network.ConnectAsync(host, port, settings.PlayerName).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                ShowMessageWindow("Could not reach the server.", "", ex.Message);
                return 1;
            }
        }

        Run(data, map, network, settings);
        return 0;
    }

    private static (string Host, int Port) ParseServer(string value)
    {
        string[] parts = value.Split(':');

        return parts.Length == 2 && int.TryParse(parts[1], out int port)
            ? (parts[0], port)
            : (value, 7777);
    }

    private static void Run(GameData data, LoadedMap map, NetworkClient network, ClientSettings settings)
    {
        Raylib.InitWindow(WindowWidth, WindowHeight, $"MonMMO — {map.Name}");
        Raylib.SetTargetFPS(60);

        // Reuse the extractor's own PNG writer rather than marshalling raw pixels:
        // it is already covered by tests, and it keeps this file free of unsafe code.
        Image image = Raylib.LoadImageFromMemory(".png", map.ToPng());
        Texture2D texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        Raylib.SetTextureFilter(texture, TextureFilter.Point);

        var player = new WalkingCharacter();
        player.Place(map.Collision, map.Collision.FirstWalkable());

        var camera = new Camera2D
        {
            Offset = new System.Numerics.Vector2(WindowWidth / 2f, WindowHeight / 2f),
            Zoom = ViewZoom,
        };

        var others = new Dictionary<int, RemoteCharacter>();
        var party = new Party();
        BattleScreen? battle = null;
        int balls = settings.Balls;

        while (!Raylib.WindowShouldClose())
        {
            float delta = Raylib.GetFrameTime();

            WildEncounterStarted? encounter = ApplyServerMessages(network, others, player, map);

            // An encounter suspends the overworld entirely: the server has already
            // decided it, and walking on while a battle is pending would put the two
            // sides out of step.
            if (encounter is not null && battle is null)
                battle = StartBattle(data, settings, encounter, party);

            if (battle is not null)
            {
                battle.Update();
                Raylib.BeginDrawing();
                battle.Draw();
                Raylib.EndDrawing();

                if (battle.IsDismissed)
                {
                    balls = battle.Balls;

                    // A caught creature joins the party. Nothing persists it yet, so
                    // it lasts only as long as the client runs.
                    if (battle.Caught is { } caught) party.TryAdd(caught);

                    battle.Unload();
                    battle = null;
                }

                continue;
            }

            bool wasStepping = player.IsStepping;
            player.Update(delta, ReadDirection());

            // Tell the server the moment a step begins, not when it finishes — it is
            // already predicted locally, so waiting would add a round trip of lag to
            // every square.
            if (!wasStepping && player.IsStepping) network.SendMove(player.Facing);

            foreach (RemoteCharacter other in others.Values) other.Update(delta);

            (float playerX, float playerY) = player.PixelPosition;
            camera.Target = ClampView(playerX, playerY, map);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            Raylib.BeginMode2D(camera);
            Raylib.DrawTexture(texture, 0, 0, Color.White);

            foreach (RemoteCharacter other in others.Values)
            {
                (float x, float y) = other.PixelPosition;
                DrawPlayer(x, y, other.Facing, new Color(120, 200, 255, 255));
                DrawNameTag(other.Name, x, y);
            }

            DrawPlayer(playerX, playerY, player.Facing);
            Raylib.EndMode2D();

            DrawStatus(map, player, network, others.Count);
            Raylib.EndDrawing();
        }

        Raylib.UnloadTexture(texture);
        Raylib.CloseWindow();
    }

    /// <summary>
    /// Folds anything the server has said into local state. Our own movement is
    /// predicted, so a position for us is only applied when it actually disagrees —
    /// otherwise every step would stutter as the confirmation arrived.
    /// </summary>
    /// <summary>
    /// Folds server messages into local state, returning an encounter if one arrived.
    /// </summary>
    private static WildEncounterStarted? ApplyServerMessages(
        NetworkClient network,
        Dictionary<int, RemoteCharacter> others,
        WalkingCharacter player,
        LoadedMap map)
    {
        WildEncounterStarted? encounter = null;

        foreach (NetMessage message in network.Drain())
        {
            switch (message)
            {
                case Welcome welcome:
                    player.Place(map.Collision, new GridPosition(welcome.X, welcome.Y));
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

                case PlayerMoved mine when !player.IsStepping:
                    var confirmed = new GridPosition(mine.X, mine.Y);
                    if (confirmed != player.Square) player.Place(map.Collision, confirmed);
                    break;

                case MoveRejected rejected:
                    player.Place(map.Collision, new GridPosition(rejected.X, rejected.Y));
                    break;

                case PlayerLeft left:
                    others.Remove(left.PlayerId);
                    break;

                case WildEncounterStarted started:
                    encounter = started;
                    break;
            }
        }

        return encounter;
    }

    /// <summary>
    /// Builds the battle the server has already rolled. The seed came with the
    /// encounter, so every roll here matches what the server would compute.
    /// </summary>
    private static BattleScreen? StartBattle(
        GameData data, ClientSettings settings, WildEncounterStarted encounter, Party party)
    {
        Battler? wild = PartyBuilder.BuildWild(data, encounter.Species, encounter.Level);

        // Lead with whatever has been caught, falling back to the placeholder starter
        // while the party is empty.
        Battler? lead = party.Lead
            ?? PartyBuilder.BuildStarter(data, settings.StarterSpecies, settings.StarterLevel);

        if (wild is null || lead is null) return null;

        return new BattleScreen(new Battle(lead, wild, encounter.Seed), data, settings.Balls);
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

    private static void DrawStatus(LoadedMap map, WalkingCharacter player, NetworkClient network, int others)
    {
        string connection = network.Failure is { } failure
            ? $"   offline: {failure}"
            : network.IsConnected ? $"   online, {others} others" : "";

        string line = $"{map.Name}  ({map.Bank}.{map.Number})   " +
                      $"{player.Square.X},{player.Square.Y}   " +
                      $"{map.Collision.Width}x{map.Collision.Height}{connection}";

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
