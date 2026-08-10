using PokeMmo.Core.Data;
using PokeMmo.RomExtract.Graphics;

namespace PokeMmo.RomExtract;

/// <summary>A decoded sprite: still palette-indexed, plus the palette to apply.</summary>
public sealed record ExtractedSprite(int SpeciesIndex, IndexedImage Image, GbaPalette Palette)
{
    public byte[] ToPng() => PngWriter.ToArray(Image.Width, Image.Height, Image.ToRgba(Palette));
}

/// <summary>
/// Top-level extraction entry point. Owns the loaded cartridge and the located
/// tables, and turns them into engine-native data.
/// </summary>
public sealed class RomExtractor
{
    public const int MonSpriteWidth = 64;
    public const int MonSpriteHeight = 64;

    private readonly Rom _rom;

    private RomExtractor(Rom rom, RomIdentity identity, RomTables tables)
    {
        _rom = rom;
        Identity = identity;
        Tables = tables;
    }

    public RomIdentity Identity { get; }
    public RomTables Tables { get; }

    public static RomExtractor Open(string path, Action<string>? log = null) =>
        Open(Rom.Load(path), log);

    public static RomExtractor Open(Rom rom, Action<string>? log = null)
    {
        RomIdentity identity = RomIdentity.Identify(rom);
        RomTables tables = TableLocator.Locate(rom, log);
        return new RomExtractor(rom, identity, tables);
    }

    /// <summary>
    /// Reads the base-stat table, merging in decoded names when the name table was found.
    /// </summary>
    public List<SpeciesData> ExtractSpecies(int? count = null)
    {
        TableLocation stats = Tables.BaseStats
            ?? throw new InvalidOperationException("The base-stat table was not located in this ROM.");

        int total = count ?? stats.EntryCount;
        var result = new List<SpeciesData>(total);

        for (int i = 0; i < total; i++)
        {
            int offset = stats.Offset + i * SpeciesData.SizeBytes;
            if (offset + SpeciesData.SizeBytes > _rom.Length) break;

            SpeciesData species = SpeciesData.Parse(_rom.Slice(offset, SpeciesData.SizeBytes), i);
            species.Name = ReadSpeciesName(i);
            result.Add(species);
        }

        return result;
    }

    private string ReadSpeciesName(int index)
    {
        TableLocation? names = Tables.SpeciesNames;
        if (names is null) return string.Empty;

        int offset = names.Offset + index * GameText.SpeciesNameLength;
        if (offset + GameText.SpeciesNameLength > _rom.Length) return string.Empty;

        return GameText.Decode(_rom.Slice(offset, GameText.SpeciesNameLength));
    }

    /// <summary>
    /// Decodes one mon sprite. Both the graphic and its palette are stored compressed
    /// and are pointed at from parallel tables indexed by species.
    /// </summary>
    /// <param name="shiny">Use the alternate palette table.</param>
    /// <param name="back">Use the back-sprite table.</param>
    /// <param name="tileOrder">Flip this if the result comes out scrambled in 8-pixel blocks.</param>
    public ExtractedSprite ExtractSprite(
        int speciesIndex,
        bool shiny = false,
        bool back = false,
        TileOrder tileOrder = TileOrder.RowMajor)
    {
        TableLocation picTable = (back ? Tables.BackPics : Tables.FrontPics)
            ?? throw new InvalidOperationException(
                $"The {(back ? "back" : "front")}-sprite table was not located in this ROM.");

        TableLocation paletteTable = (shiny ? Tables.ShinyPalettes : Tables.NormalPalettes)
            ?? throw new InvalidOperationException(
                $"The {(shiny ? "shiny" : "normal")} palette table was not located in this ROM.");

        if (speciesIndex < 0 || speciesIndex >= picTable.EntryCount)
            throw new ArgumentOutOfRangeException(
                nameof(speciesIndex),
                $"Species {speciesIndex} is outside the sprite table (0..{picTable.EntryCount - 1}).");

        uint picPointer = _rom.ReadU32(picTable.Offset + speciesIndex * picTable.EntrySize);
        byte[] tiles = DecompressAt(picPointer, "sprite graphic");

        uint palettePointer = _rom.ReadU32(paletteTable.Offset + speciesIndex * paletteTable.EntrySize);
        byte[] paletteBytes = DecompressAt(palettePointer, "sprite palette");

        IndexedImage image = TileDecoder.Decode4Bpp(tiles, MonSpriteWidth, MonSpriteHeight, tileOrder);
        GbaPalette palette = GbaPalette.FromBytes(paletteBytes);

        return new ExtractedSprite(speciesIndex, image, palette);
    }

    private byte[] DecompressAt(uint pointer, string what)
    {
        int offset = _rom.ToOffset(pointer);
        int available = _rom.Length - offset;

        try
        {
            return Lz77.Decompress(_rom.Slice(offset, available));
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException(
                $"Failed to decompress {what} at 0x{pointer:X8}: {ex.Message}", ex);
        }
    }
}
