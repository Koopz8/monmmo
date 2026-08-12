using PokeMmo.Core.World;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Maps;
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

    private CharacterSprite(List<Texture2D> frames, int width, int height, bool walks)
    {
        _frames = frames;
        Width = width;
        Height = height;
        Walks = walks;
    }

    /// <summary>
    /// True when this one has enough frames to be animated as somebody walking.
    /// <para>
    /// Plenty of things standing on a map are not people. A ball on the ground has one
    /// frame; so does a cuttable tree and a boulder. Refusing to load those because they
    /// cannot walk is what left a Poké Ball in Viridian Forest drawn as the same tan
    /// rectangle this client falls back to when it has nothing at all — a placeholder
    /// standing in for a sprite that was on the cartridge the whole time.
    /// </para>
    /// </summary>
    public bool Walks { get; }

    public int Width { get; }

    public int Height { get; }

    public int FrameCount => _frames.Count;

    /// <summary>
    /// Loads a sprite from an already-located table, or returns null when this
    /// cartridge has nothing usable at that id.
    /// </summary>
    public static CharacterSprite? Load(Rom rom, OverworldSpriteTables tables, int graphicsId)
    {
        if (tables.Records.ElementAtOrDefault(graphicsId) is not { } info) return null;
        if (OverworldSprites.PaletteForTag(rom, tables.PaletteTable, info.PaletteTag) is not { } palette) return null;

        List<IndexedImage> images = OverworldSprites.ReadFrames(rom, info, tables.Boundaries);

        // One frame is enough to draw. Only walking needs nine, and most of what stands
        // on a map does not walk.
        if (images.Count == 0) return null;

        bool walks = OverworldAnimation.CanWalk(images.Count);

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

        return new CharacterSprite(frames, info.Width, info.Height, walks);
    }

    /// <summary>
    /// Draws a character. The sprite is taller than a square, so it is lifted to stand
    /// on the square rather than sit inside it — which is how a character can walk
    /// behind the bottom of a building.
    /// </summary>
    public void Draw(float x, float y, Direction facing, bool walking, int stride)
    {
        if (_frames.Count == 0) return;

        // Something that does not walk has one picture and no facing. Asking the
        // animation which frame a ball uses when looking left is a question with no
        // answer, and the old one was to draw a rectangle instead.
        (int index, bool mirror) = Walks
            ? OverworldAnimation.FrameFor(facing, walking, stride)
            : (0, false);

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

/// <summary>The located sprite tables, so nothing has to scan the cartridge twice.</summary>
public sealed record OverworldSpriteTables(
    int PaletteTable,
    IReadOnlyList<ObjectGraphicsInfo?> Records,
    IReadOnlyDictionary<int, int> Boundaries)
{
    /// <summary>Every graphics id a cartridge could name. The real table holds 151.</summary>
    private const int MaxGraphicsIds = 256;

    public static OverworldSpriteTables? Locate(Rom rom)
    {
        if (OverworldSprites.LocateGraphicsTable(rom) is not { } table) return null;
        if (OverworldSprites.LocatePaletteTable(rom) is not { } palettes) return null;

        List<ObjectGraphicsInfo?> records = OverworldSprites.ReadGraphics(rom, table, MaxGraphicsIds);

        return new OverworldSpriteTables(
            palettes, records, OverworldSprites.FrameListBoundaries(rom, records));
    }
}

/// <summary>
/// Sprites by graphics id, loaded once each.
/// <para>
/// A map can hold a dozen people and several maps share the same faces, so loading is
/// worth doing once. Ids with no usable sprite are remembered as nothing, or every
/// frame would retry one.
/// </para>
/// <para>
/// The tables are located once here rather than per sprite. Locating means walking
/// sixteen megabytes, and doing that once per person on a map would stall visibly
/// every time somebody opened a door.
/// </para>
/// </summary>
public sealed class CharacterSprites : IDisposable
{
    private readonly Rom _rom;
    private readonly OverworldSpriteTables? _tables;
    private readonly Dictionary<int, CharacterSprite?> _byGraphicsId = [];

    public CharacterSprites(Rom rom)
    {
        _rom = rom;
        _tables = OverworldSpriteTables.Locate(rom);
    }

    public bool IsUsable => _tables is not null;

    public CharacterSprite? For(int graphicsId)
    {
        if (_tables is null) return null;
        if (_byGraphicsId.TryGetValue(graphicsId, out CharacterSprite? cached)) return cached;

        CharacterSprite? sprite = CharacterSprite.Load(_rom, _tables, graphicsId);

        _byGraphicsId[graphicsId] = sprite;
        return sprite;
    }

    public void Dispose()
    {
        foreach (CharacterSprite? sprite in _byGraphicsId.Values) sprite?.Dispose();
        _byGraphicsId.Clear();
    }
}
