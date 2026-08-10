using PokeMmo.Core.World;
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

        LoadedMap map;

        try
        {
            map = WorldLoader.Load(settings.RomPath, settings.MapName, settings.MapAddress);
        }
        catch (Exception ex)
        {
            ShowMessageWindow("Could not load a map.", "", ex.Message);
            return 1;
        }

        Run(map);
        return 0;
    }

    private static void Run(LoadedMap map)
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

        while (!Raylib.WindowShouldClose())
        {
            player.Update(Raylib.GetFrameTime(), ReadDirection());

            (float playerX, float playerY) = player.PixelPosition;
            camera.Target = ClampView(playerX, playerY, map);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            Raylib.BeginMode2D(camera);
            Raylib.DrawTexture(texture, 0, 0, Color.White);
            DrawPlayer(playerX, playerY, player.Facing);
            Raylib.EndMode2D();

            DrawStatus(map, player);
            Raylib.EndDrawing();
        }

        Raylib.UnloadTexture(texture);
        Raylib.CloseWindow();
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
    private static void DrawPlayer(float x, float y, Direction facing)
    {
        const int size = WalkingCharacter.SquarePixels;

        Raylib.DrawRectangle((int)x + 2, (int)y + 1, size - 4, size - 2, new Color(248, 248, 248, 255));
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

    private static void DrawStatus(LoadedMap map, WalkingCharacter player)
    {
        string line = $"{map.Name}  ({map.Bank}.{map.Number})   " +
                      $"{player.Square.X},{player.Square.Y}   " +
                      $"{map.Collision.Width}x{map.Collision.Height}";

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
