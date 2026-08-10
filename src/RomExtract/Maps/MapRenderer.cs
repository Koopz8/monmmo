using PokeMmo.RomExtract.Graphics;

namespace PokeMmo.RomExtract.Maps;

/// <summary>A rendered map: tightly packed RGBA8888 rows.</summary>
public sealed record RenderedMap(int Width, int Height, byte[] Rgba)
{
    public byte[] ToPng() => PngWriter.ToArray(Width, Height, Rgba);
}

/// <summary>
/// Composes a map into a picture.
/// <para>
/// Each map square is a metatile: a 2x2 grid of 8x8 tiles drawn twice, once for the
/// bottom layer and again for the top layer with colour 0 treated as transparent.
/// Tile, metatile and palette indices address the primary and secondary tilesets as
/// one shared space, split at the boundaries in <see cref="TilesetSplit"/>.
/// </para>
/// </summary>
public sealed class MapRenderer
{
    public const int TilePixels = 8;
    public const int BlockPixels = 16;
    private const int TilesPerLayer = 4;

    private readonly Rom _rom;
    private readonly TilesetSplit _split;

    private readonly TilesetRecord _primary;
    private readonly TilesetRecord? _secondary;

    private readonly byte[] _primaryTiles;
    private readonly byte[] _secondaryTiles;
    private readonly GbaPalette[] _palettes;

    private MapRenderer(Rom rom, TilesetSplit split, TilesetRecord primary, TilesetRecord? secondary)
    {
        _rom = rom;
        _split = split;
        _primary = primary;
        _secondary = secondary;

        _primaryTiles = primary.ReadTiles(rom);
        _secondaryTiles = secondary?.ReadTiles(rom) ?? [];
        _palettes = BuildPalettes(rom, primary, secondary, split);
    }

    public static MapRenderer Create(Rom rom, MapLayoutRecord layout, TilesetSplit? split = null)
    {
        TilesetSplit chosen = split ?? TilesetSplit.FireRed;

        TilesetRecord primary = TilesetRecord.TryParse(rom, layout.PrimaryTilesetPointer)
            ?? throw new InvalidDataException(
                $"The primary tileset at 0x{layout.PrimaryTilesetPointer:X8} is not a valid tileset record.");

        TilesetRecord? secondary = layout.SecondaryTilesetPointer == 0
            ? null
            : TilesetRecord.TryParse(rom, layout.SecondaryTilesetPointer);

        return new MapRenderer(rom, chosen, primary, secondary);
    }

    /// <summary>
    /// The palette slots below the split come from the primary tileset and the rest
    /// from the secondary, which is what lets two tilesets share one 16-slot space.
    /// </summary>
    private static GbaPalette[] BuildPalettes(
        Rom rom, TilesetRecord primary, TilesetRecord? secondary, TilesetSplit split)
    {
        GbaPalette[] palettes = primary.ReadPalettes(rom);
        if (secondary is null) return palettes;

        GbaPalette[] secondaryPalettes = secondary.ReadPalettes(rom);

        for (int i = split.Palettes; i < TilesetRecord.PaletteCount; i++)
            palettes[i] = secondaryPalettes[i];

        return palettes;
    }

    public RenderedMap Render(MapLayoutRecord layout)
    {
        ushort[] blocks = layout.ReadBlocks(_rom);

        int width = layout.Width * BlockPixels;
        int height = layout.Height * BlockPixels;
        var rgba = new byte[width * height * 4];

        for (int by = 0; by < layout.Height; by++)
        {
            for (int bx = 0; bx < layout.Width; bx++)
            {
                var block = new MapBlock(blocks[by * layout.Width + bx]);
                DrawMetatile(rgba, width, bx * BlockPixels, by * BlockPixels, block.MetatileId);
            }
        }

        return new RenderedMap(width, height, rgba);
    }

    private void DrawMetatile(byte[] rgba, int stride, int originX, int originY, int metatileId)
    {
        (TilesetRecord? tileset, int localId) = metatileId < _split.Metatiles
            ? (_primary, metatileId)
            : (_secondary, metatileId - _split.Metatiles);

        if (tileset is null) return;

        MetatileEntry[] entries = tileset.ReadMetatile(_rom, localId);

        for (int layer = 0; layer < 2; layer++)
        {
            for (int i = 0; i < TilesPerLayer; i++)
            {
                MetatileEntry entry = entries[layer * TilesPerLayer + i];

                DrawTile(
                    rgba, stride,
                    originX + (i % 2) * TilePixels,
                    originY + (i / 2) * TilePixels,
                    entry,
                    // The bottom layer paints every pixel; the top layer lets colour 0
                    // through so the terrain beneath shows.
                    skipColorZero: layer == 1);
            }
        }
    }

    private void DrawTile(byte[] rgba, int stride, int originX, int originY, MetatileEntry entry, bool skipColorZero)
    {
        (byte[] source, int localTile) = entry.TileId < _split.Tiles
            ? (_primaryTiles, entry.TileId)
            : (_secondaryTiles, entry.TileId - _split.Tiles);

        int tileOffset = localTile * TileDecoder.BytesPerTile;
        if (tileOffset < 0 || tileOffset + TileDecoder.BytesPerTile > source.Length) return;

        GbaPalette palette = _palettes[entry.PaletteIndex];

        for (int y = 0; y < TilePixels; y++)
        {
            for (int x = 0; x < TilePixels; x++)
            {
                byte packed = source[tileOffset + (y * TilePixels + x) / 2];
                int colorIndex = (x & 1) == 0 ? packed & 0x0F : (packed >> 4) & 0x0F;

                if (skipColorZero && colorIndex == 0) continue;

                int destX = originX + (entry.FlipHorizontal ? TilePixels - 1 - x : x);
                int destY = originY + (entry.FlipVertical ? TilePixels - 1 - y : y);

                int at = (destY * stride + destX) * 4;
                if (at < 0 || at + 4 > rgba.Length) continue;

                Rgba32 color = palette[colorIndex];
                rgba[at + 0] = color.R;
                rgba[at + 1] = color.G;
                rgba[at + 2] = color.B;
                rgba[at + 3] = 255;
            }
        }
    }
}
