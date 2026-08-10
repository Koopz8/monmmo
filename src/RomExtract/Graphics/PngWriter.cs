using System.Buffers.Binary;
using System.IO.Compression;

namespace PokeMmo.RomExtract.Graphics;

/// <summary>
/// A minimal PNG encoder for 8-bit RGBA images.
/// <para>
/// Written by hand rather than pulled from NuGet on purpose: the extractor is the
/// one component every player runs against their own cartridge, so keeping its
/// dependency surface at zero keeps the client trivially auditable.
/// </para>
/// </summary>
public static class PngWriter
{
    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static void Write(string path, int width, int height, ReadOnlySpan<byte> rgba)
    {
        using var fs = File.Create(path);
        Write(fs, width, height, rgba);
    }

    public static byte[] ToArray(int width, int height, ReadOnlySpan<byte> rgba)
    {
        using var ms = new MemoryStream();
        Write(ms, width, height, rgba);
        return ms.ToArray();
    }

    public static void Write(Stream output, int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (rgba.Length != width * height * 4)
            throw new ArgumentException(
                $"Expected {width * height * 4} bytes of RGBA, got {rgba.Length}.", nameof(rgba));

        output.Write(Signature);

        // IHDR: width, height, 8-bit depth, colour type 6 (truecolour + alpha),
        // deflate, adaptive filtering, no interlace.
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(output, "IHDR", ihdr);

        // Each scanline is prefixed with its filter type; 0 means "no filtering".
        var raw = new byte[height * (width * 4 + 1)];
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * (width * 4 + 1);
            raw[rowStart] = 0;
            rgba.Slice(y * width * 4, width * 4).CopyTo(raw.AsSpan(rowStart + 1));
        }

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw, 0, raw.Length);
        }

        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);

        Span<byte> typeBytes = stackalloc byte[4];
        for (int i = 0; i < 4; i++) typeBytes[i] = (byte)type[i];
        output.Write(typeBytes);
        output.Write(data);

        uint crc = Crc32.Compute(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }
}

/// <summary>The CRC-32 variant PNG chunks are checksummed with.</summary>
internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];

        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }

        return table;
    }

    public static uint Compute(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte x in a) c = Table[(c ^ x) & 0xFF] ^ (c >> 8);
        foreach (byte x in b) c = Table[(c ^ x) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
