namespace PokeMmo.RomExtract.Graphics;

/// <summary>
/// How a linear run of 8x8 tiles maps onto a rectangular image.
/// </summary>
public enum TileOrder
{
    /// <summary>Tile n fills position (n % tilesWide, n / tilesWide) — left to right, then down.</summary>
    RowMajor,

    /// <summary>Tile n fills position (n / tilesHigh, n % tilesHigh) — top to bottom, then across.</summary>
    ColumnMajor,
}

/// <summary>
/// An 8-bit palette-indexed bitmap. Kept indexed rather than pre-flattened to RGBA
/// so the same pixels can be re-coloured with the normal or shiny palette.
/// </summary>
public sealed class IndexedImage
{
    public IndexedImage(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[width * height];
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public byte this[int x, int y]
    {
        get => Pixels[y * Width + x];
        set => Pixels[y * Width + x] = value;
    }

    /// <summary>Applies a palette, producing tightly packed RGBA8888 rows.</summary>
    public byte[] ToRgba(GbaPalette palette)
    {
        var rgba = new byte[Width * Height * 4];

        for (int i = 0; i < Pixels.Length; i++)
        {
            Rgba32 c = palette[Pixels[i] & 0x0F];
            rgba[i * 4 + 0] = c.R;
            rgba[i * 4 + 1] = c.G;
            rgba[i * 4 + 2] = c.B;
            rgba[i * 4 + 3] = c.A;
        }

        return rgba;
    }
}

/// <summary>
/// Decodes 4-bits-per-pixel GBA tile data.
/// <para>
/// Each 8x8 tile occupies 32 bytes, one byte per pixel pair, and within a byte the
/// <em>low</em> nibble is the left-hand pixel. That nibble order is the single most
/// common thing to get backwards; a mirrored-looking sprite means it was flipped.
/// </para>
/// </summary>
public static class TileDecoder
{
    public const int TileWidth = 8;
    public const int TileHeight = 8;
    public const int BytesPerTile = TileWidth * TileHeight / 2;

    /// <summary>
    /// Decodes tile data into an image <paramref name="width"/> x <paramref name="height"/> pixels.
    /// </summary>
    /// <param name="order">
    /// Tile arrangement. Mon sprites are square and both orderings produce a
    /// same-sized image, so a wrong choice yields a scrambled — not a crashing —
    /// result. Flip this if a decoded sprite comes out shuffled in 8-pixel blocks.
    /// </param>
    public static IndexedImage Decode4Bpp(
        ReadOnlySpan<byte> src,
        int width,
        int height,
        TileOrder order = TileOrder.RowMajor)
    {
        if (width % TileWidth != 0 || height % TileHeight != 0)
            throw new ArgumentException($"Image dimensions must be multiples of {TileWidth}.");

        int tilesWide = width / TileWidth;
        int tilesHigh = height / TileHeight;
        int tileCount = tilesWide * tilesHigh;

        int required = tileCount * BytesPerTile;
        if (src.Length < required)
            throw new ArgumentException(
                $"Need {required} bytes for a {width}x{height} 4bpp image, got {src.Length}.", nameof(src));

        var image = new IndexedImage(width, height);

        for (int tile = 0; tile < tileCount; tile++)
        {
            (int tileX, int tileY) = order == TileOrder.RowMajor
                ? (tile % tilesWide, tile / tilesWide)
                : (tile / tilesHigh, tile % tilesHigh);

            int tileBase = tile * BytesPerTile;

            for (int py = 0; py < TileHeight; py++)
            {
                for (int px = 0; px < TileWidth; px += 2)
                {
                    byte packed = src[tileBase + (py * TileWidth + px) / 2];

                    image[tileX * TileWidth + px, tileY * TileHeight + py] = (byte)(packed & 0x0F);
                    image[tileX * TileWidth + px + 1, tileY * TileHeight + py] = (byte)((packed >> 4) & 0x0F);
                }
            }
        }

        return image;
    }

    /// <summary>Re-encodes an indexed image to 4bpp tile data. Used by tests to build synthetic data.</summary>
    public static byte[] Encode4Bpp(IndexedImage image, TileOrder order = TileOrder.RowMajor)
    {
        int tilesWide = image.Width / TileWidth;
        int tilesHigh = image.Height / TileHeight;
        int tileCount = tilesWide * tilesHigh;

        var dst = new byte[tileCount * BytesPerTile];

        for (int tile = 0; tile < tileCount; tile++)
        {
            (int tileX, int tileY) = order == TileOrder.RowMajor
                ? (tile % tilesWide, tile / tilesWide)
                : (tile / tilesHigh, tile % tilesHigh);

            int tileBase = tile * BytesPerTile;

            for (int py = 0; py < TileHeight; py++)
            {
                for (int px = 0; px < TileWidth; px += 2)
                {
                    byte lo = (byte)(image[tileX * TileWidth + px, tileY * TileHeight + py] & 0x0F);
                    byte hi = (byte)(image[tileX * TileWidth + px + 1, tileY * TileHeight + py] & 0x0F);
                    dst[tileBase + (py * TileWidth + px) / 2] = (byte)(lo | (hi << 4));
                }
            }
        }

        return dst;
    }
}
