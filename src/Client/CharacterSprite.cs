using PokeMmo.Core.World;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Graphics;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// A walking figure, drawn from the player's own cartridge.
/// <para>
/// The frames are uploaded once and kept. Mirroring for the right-facing frames is
/// done by drawing with a negative source width rather than by building flipped
/// copies — the hardware does it for free, and the cartridge stores no right-facing
/// frames precisely because it is free there too.
/// </para>
/// </summary>
public sealed class CharacterSprite : IDisposable
{
    /// <summary>The first player character's graphics id, which is index zero.</summary>
    public const int DefaultGraphicsId = 0;

    private readonly List<Texture2D> _frames = [];

    private CharacterSprite(List<Texture2D> frames, int width, int height)
    {
        _frames = frames;
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public int FrameCount => _frames.Count;

    /// <summary>
    /// Loads a sprite, or returns null when this cartridge has nothing usable at that
    /// id — in which case the caller should draw whatever it drew before.
    /// </summary>
    public static CharacterSprite? Load(Rom rom, int graphicsId)
    {
        if (OverworldSprites.LocateGraphicsTable(rom) is not { } table) return null;
        if (OverworldSprites.LocatePaletteTable(rom) is not { } paletteTable) return null;

        List<ObjectGraphicsInfo?> records = OverworldSprites.ReadGraphics(rom, table, graphicsId + 1);

        if (graphicsId >= records.Count || records[graphicsId] is not { } info) return null;
        if (OverworldSprites.PaletteForTag(rom, paletteTable, info.PaletteTag) is not { } palette) return null;

        List<IndexedImage> images = OverworldSprites.ReadFrames(
            rom, info, OverworldSprites.FrameListBoundaries(rom, records));

        if (!OverworldAnimation.CanWalk(images.Count)) return null;

        var frames = new List<Texture2D>();

        foreach (IndexedImage image in images.Take(OverworldAnimation.WalkingFrameCount))
        {
            Image raw = Raylib.LoadImageFromMemory(
                ".png", PngWriter.ToArray(image.Width, image.Height, image.ToRgba(palette)));

            Texture2D texture = Raylib.LoadTextureFromImage(raw);
            Raylib.UnloadImage(raw);
            Raylib.SetTextureFilter(texture, TextureFilter.Point);

            frames.Add(texture);
        }

        return new CharacterSprite(frames, info.Width, info.Height);
    }

    /// <summary>
    /// Draws a character. The sprite is taller than a square, so it is lifted to stand
    /// on the square rather than sit inside it — which is how a character can walk
    /// behind the bottom of a building.
    /// </summary>
    public void Draw(float x, float y, Direction facing, bool walking, int stride)
    {
        if (_frames.Count == 0) return;

        (int index, bool mirror) = OverworldAnimation.FrameFor(facing, walking, stride);
        if (index >= _frames.Count) index = 0;

        Texture2D frame = _frames[index];

        var source = new Rectangle(0, 0, mirror ? -Width : Width, Height);

        var destination = new Rectangle(
            x + (WalkingCharacter.SquarePixels - Width) / 2f,
            y + WalkingCharacter.SquarePixels - Height,
            Width,
            Height);

        Raylib.DrawTexturePro(frame, source, destination, System.Numerics.Vector2.Zero, 0f, Color.White);
    }

    public void Dispose()
    {
        foreach (Texture2D frame in _frames) Raylib.UnloadTexture(frame);
        _frames.Clear();
    }
}
