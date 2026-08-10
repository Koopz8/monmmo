namespace PokeMmo.RomExtract.Graphics;

public readonly record struct Rgba32(byte R, byte G, byte B, byte A);

/// <summary>
/// A 16-entry GBA palette. On the cartridge each colour is a little-endian 16-bit
/// value packed as 0BBBBBGG GGGRRRRR — five bits per channel, blue in the high bits.
/// </summary>
public sealed class GbaPalette
{
    public const int ColorCount = 16;
    public const int SizeBytes = ColorCount * 2;

    private readonly Rgba32[] _colors;

    private GbaPalette(Rgba32[] colors) => _colors = colors;

    public Rgba32 this[int index] => _colors[index];

    public int Count => _colors.Length;

    /// <summary>
    /// Decodes a raw 32-byte palette.
    /// </summary>
    /// <param name="treatFirstAsTransparent">
    /// Sprite palettes use colour 0 as the transparency key rather than as a drawn
    /// colour, so it is emitted with alpha 0.
    /// </param>
    public static GbaPalette FromBytes(ReadOnlySpan<byte> src, bool treatFirstAsTransparent = true)
    {
        if (src.Length < SizeBytes)
            throw new ArgumentException($"A palette needs {SizeBytes} bytes, got {src.Length}.", nameof(src));

        var colors = new Rgba32[ColorCount];

        for (int i = 0; i < ColorCount; i++)
        {
            ushort packed = (ushort)(src[i * 2] | (src[i * 2 + 1] << 8));

            int r5 = packed & 0x1F;
            int g5 = (packed >> 5) & 0x1F;
            int b5 = (packed >> 10) & 0x1F;

            // Scale 5 bits to 8 by replicating the high bits, so 31 maps to 255 exactly.
            byte r = (byte)((r5 << 3) | (r5 >> 2));
            byte g = (byte)((g5 << 3) | (g5 >> 2));
            byte b = (byte)((b5 << 3) | (b5 >> 2));
            byte a = (byte)(treatFirstAsTransparent && i == 0 ? 0 : 255);

            colors[i] = new Rgba32(r, g, b, a);
        }

        return new GbaPalette(colors);
    }

    /// <summary>Re-encodes to the cartridge format. Used by tests to build synthetic data.</summary>
    public static byte[] ToBytes(IReadOnlyList<Rgba32> colors)
    {
        var dst = new byte[SizeBytes];

        for (int i = 0; i < ColorCount && i < colors.Count; i++)
        {
            Rgba32 c = colors[i];
            int packed = (c.R >> 3) | ((c.G >> 3) << 5) | ((c.B >> 3) << 10);
            dst[i * 2] = (byte)(packed & 0xFF);
            dst[i * 2 + 1] = (byte)((packed >> 8) & 0xFF);
        }

        return dst;
    }
}
