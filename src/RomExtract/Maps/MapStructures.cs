namespace PokeMmo.RomExtract.Maps;

/// <summary>
/// Counts that split a map's two tilesets. Tile, metatile and palette slots are a
/// single shared address space: indices below the boundary come from the primary
/// tileset, the rest from the secondary.
/// <para>
/// These are per-game constants and the values below are FireRed's. They are
/// parameters rather than literals because getting one wrong produces a
/// recognisably wrong picture rather than an error — mismatched colours or
/// duplicated terrain — and being able to flip one from the command line turns a
/// code change into a one-word experiment.
/// </para>
/// </summary>
public sealed record TilesetSplit(int Tiles, int Metatiles, int Palettes)
{
    public static readonly TilesetSplit FireRed = new(Tiles: 640, Metatiles: 640, Palettes: 7);

    public static readonly TilesetSplit Emerald = new(Tiles: 512, Metatiles: 512, Palettes: 6);
}

/// <summary>
/// One map's dimensions and the data it is built from.
/// <para>
/// On the cartridge this is a 28-byte record: two 32-bit dimensions followed by four
/// pointers (border, block data, primary tileset, secondary tileset) and the border
/// dimensions.
/// </para>
/// </summary>
public sealed record MapLayoutRecord(
    int Offset,
    int Width,
    int Height,
    uint BorderPointer,
    uint BlocksPointer,
    uint PrimaryTilesetPointer,
    uint SecondaryTilesetPointer,
    byte BorderWidth,
    byte BorderHeight)
{
    public const int SizeBytes = 28;

    /// <summary>
    /// Largest dimension treated as plausible. Real maps top out well below this;
    /// the bound exists so that random data fails the check.
    /// </summary>
    public const int MaxDimension = 1024;

    public uint Address => Rom.BaseAddress + (uint)Offset;

    public int BlockCount => Width * Height;

    public override string ToString() =>
        $"0x{Address:X8}  {Width,3} x {Height,3} blocks ({Width * 16} x {Height * 16} px)";

    /// <summary>
    /// Reads a layout record, or returns null when the bytes at <paramref name="offset"/>
    /// do not satisfy the record's invariants.
    /// </summary>
    public static MapLayoutRecord? TryParse(Rom rom, int offset)
    {
        if (offset < 0 || offset + SizeBytes > rom.Length) return null;

        int width = unchecked((int)rom.ReadU32(offset));
        int height = unchecked((int)rom.ReadU32(offset + 4));

        if (width is < 1 or > MaxDimension || height is < 1 or > MaxDimension) return null;

        uint border = rom.ReadU32(offset + 8);
        uint blocks = rom.ReadU32(offset + 12);
        uint primary = rom.ReadU32(offset + 16);
        uint secondary = rom.ReadU32(offset + 20);

        // The secondary tileset is genuinely optional; the other three are not.
        if (!rom.IsRomAddress(border) || !rom.IsRomAddress(blocks) || !rom.IsRomAddress(primary))
            return null;

        if (secondary != 0 && !rom.IsRomAddress(secondary)) return null;

        // The block data must fit inside the cartridge, which rules out most
        // coincidental matches that got this far.
        long blockBytes = (long)width * height * 2;
        if (rom.ToOffsetOrNull(blocks) is not { } blocksOffset || blocksOffset + blockBytes > rom.Length)
            return null;

        return new MapLayoutRecord(
            offset, width, height, border, blocks, primary, secondary,
            rom.ReadU8(offset + 24), rom.ReadU8(offset + 25));
    }

    /// <summary>Reads the block data: one 16-bit entry per map square, row-major.</summary>
    public ushort[] ReadBlocks(Rom rom)
    {
        int offset = rom.ToOffset(BlocksPointer);
        var blocks = new ushort[BlockCount];

        for (int i = 0; i < blocks.Length; i++)
            blocks[i] = rom.ReadU16(offset + i * 2);

        return blocks;
    }

    /// <summary>
    /// Builds the walkability grid for this map from the collision bits already
    /// carried by each block.
    /// </summary>
    public Core.World.CollisionGrid ReadCollision(Rom rom)
    {
        ushort[] blocks = ReadBlocks(rom);
        var collision = new byte[blocks.Length];

        for (int i = 0; i < blocks.Length; i++)
            collision[i] = (byte)new MapBlock(blocks[i]).Collision;

        return new Core.World.CollisionGrid(Width, Height, collision);
    }
}

/// <summary>One entry of a map's block data.</summary>
public readonly record struct MapBlock(ushort Raw)
{
    /// <summary>Which metatile is drawn here.</summary>
    public int MetatileId => Raw & 0x03FF;

    /// <summary>Movement permission: 0 is walkable, non-zero blocks or is water/surf.</summary>
    public int Collision => (Raw >> 10) & 0x3;

    /// <summary>Height layer, used for bridges and overpasses.</summary>
    public int Elevation => (Raw >> 12) & 0xF;
}

/// <summary>
/// A tileset: 8x8 tile graphics, sixteen palettes, and the metatiles that assemble
/// tiles into 16x16 map squares.
/// <para>
/// On the cartridge this is a 24-byte record. Only the first three pointers are read
/// here; the trailing two fields differ in order between games and are not needed to
/// draw anything.
/// </para>
/// </summary>
public sealed record TilesetRecord(
    int Offset,
    bool IsCompressed,
    bool IsSecondary,
    uint TilesPointer,
    uint PalettesPointer,
    uint MetatilesPointer)
{
    public const int SizeBytes = 24;

    /// <summary>Every tileset carries sixteen 16-colour palettes.</summary>
    public const int PaletteCount = 16;

    public uint Address => Rom.BaseAddress + (uint)Offset;

    public static TilesetRecord? TryParse(Rom rom, uint pointer)
    {
        if (rom.ToOffsetOrNull(pointer) is not { } offset) return null;
        if (offset + SizeBytes > rom.Length) return null;

        byte compressed = rom.ReadU8(offset);
        byte secondary = rom.ReadU8(offset + 1);

        // Both are booleans in the original source, so anything else means this is
        // not a tileset record.
        if (compressed > 1 || secondary > 1) return null;

        uint tiles = rom.ReadU32(offset + 4);
        uint palettes = rom.ReadU32(offset + 8);
        uint metatiles = rom.ReadU32(offset + 12);

        if (!rom.IsRomAddress(tiles) || !rom.IsRomAddress(palettes) || !rom.IsRomAddress(metatiles))
            return null;

        return new TilesetRecord(offset, compressed == 1, secondary == 1, tiles, palettes, metatiles);
    }

    /// <summary>
    /// Reads the tile graphics, decompressing when the record says they are compressed.
    /// </summary>
    public byte[] ReadTiles(Rom rom)
    {
        int offset = rom.ToOffset(TilesPointer);

        if (!IsCompressed)
            return rom.Slice(offset, Math.Min(rom.Length - offset, MaxTileBytes)).ToArray();

        return Lz77.Decompress(rom.Slice(offset, rom.Length - offset));
    }

    /// <summary>Upper bound on uncompressed tile data, used only to bound a raw read.</summary>
    private const int MaxTileBytes = 0x8000;

    /// <summary>Reads all sixteen palettes. Tileset palettes are stored uncompressed.</summary>
    public Graphics.GbaPalette[] ReadPalettes(Rom rom)
    {
        int offset = rom.ToOffset(PalettesPointer);
        var palettes = new Graphics.GbaPalette[PaletteCount];

        for (int i = 0; i < PaletteCount; i++)
        {
            int at = offset + i * Graphics.GbaPalette.SizeBytes;

            palettes[i] = at + Graphics.GbaPalette.SizeBytes <= rom.Length
                // Map tiles are opaque; colour 0 is a real colour here, not a key.
                ? Graphics.GbaPalette.FromBytes(
                    rom.Slice(at, Graphics.GbaPalette.SizeBytes), treatFirstAsTransparent: false)
                : Graphics.GbaPalette.FromBytes(new byte[Graphics.GbaPalette.SizeBytes], false);
        }

        return palettes;
    }

    /// <summary>
    /// Reads one metatile: eight tile references, four for the bottom layer and four
    /// for the top, arranged as a 2x2 grid of 8x8 tiles.
    /// </summary>
    public MetatileEntry[] ReadMetatile(Rom rom, int localIndex)
    {
        int offset = rom.ToOffset(MetatilesPointer) + localIndex * MetatileEntry.CountPerMetatile * 2;
        var entries = new MetatileEntry[MetatileEntry.CountPerMetatile];

        for (int i = 0; i < entries.Length; i++)
        {
            int at = offset + i * 2;
            entries[i] = new MetatileEntry(at + 2 <= rom.Length ? rom.ReadU16(at) : (ushort)0);
        }

        return entries;
    }
}

/// <summary>One tile reference inside a metatile.</summary>
public readonly record struct MetatileEntry(ushort Raw)
{
    /// <summary>Four tiles per layer, two layers.</summary>
    public const int CountPerMetatile = 8;

    public int TileId => Raw & 0x03FF;

    public bool FlipHorizontal => (Raw & 0x0400) != 0;

    public bool FlipVertical => (Raw & 0x0800) != 0;

    public int PaletteIndex => (Raw >> 12) & 0xF;
}
