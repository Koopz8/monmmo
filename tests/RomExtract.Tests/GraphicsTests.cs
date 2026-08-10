using System.IO.Compression;
using PokeMmo.RomExtract.Graphics;

namespace PokeMmo.RomExtract.Tests;

public class TileDecoderTests
{
    [Fact]
    public void LowNibbleIsTheLeftPixel()
    {
        // One tile, first byte 0x21: left pixel = 1, right pixel = 2.
        var tile = new byte[TileDecoder.BytesPerTile];
        tile[0] = 0x21;

        IndexedImage image = TileDecoder.Decode4Bpp(tile, 8, 8);

        Assert.Equal(1, image[0, 0]);
        Assert.Equal(2, image[1, 0]);
    }

    [Theory]
    [InlineData(TileOrder.RowMajor)]
    [InlineData(TileOrder.ColumnMajor)]
    public void RoundTripsThroughEncodeAndDecode(TileOrder order)
    {
        var original = new IndexedImage(64, 64);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                original[x, y] = (byte)((x + y * 3) & 0x0F);

        byte[] encoded = TileDecoder.Encode4Bpp(original, order);
        IndexedImage decoded = TileDecoder.Decode4Bpp(encoded, 64, 64, order);

        Assert.Equal(original.Pixels, decoded.Pixels);
    }

    [Fact]
    public void TileOrderActuallyChangesTheLayout()
    {
        var image = new IndexedImage(16, 16);
        image[8, 0] = 5; // second tile across, in row-major terms

        byte[] encoded = TileDecoder.Encode4Bpp(image, TileOrder.RowMajor);
        IndexedImage misread = TileDecoder.Decode4Bpp(encoded, 16, 16, TileOrder.ColumnMajor);

        Assert.NotEqual(image.Pixels, misread.Pixels);
    }

    [Fact]
    public void ASixtyFourSquareSpriteIsExactlyTwoKilobytes()
    {
        var image = new IndexedImage(64, 64);
        Assert.Equal(0x800, TileDecoder.Encode4Bpp(image).Length);
    }

    [Fact]
    public void RejectsDimensionsThatAreNotTileAligned()
    {
        Assert.Throws<ArgumentException>(() => TileDecoder.Decode4Bpp(new byte[1000], 60, 64));
    }

    [Fact]
    public void RejectsDataThatIsTooShort()
    {
        Assert.Throws<ArgumentException>(() => TileDecoder.Decode4Bpp(new byte[10], 64, 64));
    }
}

public class GbaPaletteTests
{
    [Fact]
    public void FullFiveBitChannelsExpandToTwoFiftyFive()
    {
        // 0x7FFF is white: all three 5-bit channels saturated.
        var raw = new byte[GbaPalette.SizeBytes];
        raw[2] = 0xFF;
        raw[3] = 0x7F;

        GbaPalette palette = GbaPalette.FromBytes(raw);

        Assert.Equal(new Rgba32(255, 255, 255, 255), palette[1]);
    }

    [Fact]
    public void ChannelsAreUnpackedInRedGreenBlueBitOrder()
    {
        // Pure red is 0x001F, pure green 0x03E0, pure blue 0x7C00.
        var raw = new byte[GbaPalette.SizeBytes];
        raw[2] = 0x1F; raw[3] = 0x00;
        raw[4] = 0xE0; raw[5] = 0x03;
        raw[6] = 0x00; raw[7] = 0x7C;

        GbaPalette palette = GbaPalette.FromBytes(raw);

        Assert.Equal(new Rgba32(255, 0, 0, 255), palette[1]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), palette[2]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), palette[3]);
    }

    [Fact]
    public void FirstEntryIsTransparentForSprites()
    {
        GbaPalette palette = GbaPalette.FromBytes(new byte[GbaPalette.SizeBytes]);
        Assert.Equal(0, palette[0].A);
        Assert.Equal(255, palette[1].A);
    }

    [Fact]
    public void FirstEntryIsOpaqueWhenNotTreatedAsAKey()
    {
        GbaPalette palette = GbaPalette.FromBytes(new byte[GbaPalette.SizeBytes], treatFirstAsTransparent: false);
        Assert.Equal(255, palette[0].A);
    }

    [Fact]
    public void RoundTripsThroughTheCartridgeEncoding()
    {
        var colors = new Rgba32[16];
        for (int i = 0; i < 16; i++)
        {
            byte v = (byte)((i << 3) | (i >> 2));
            colors[i] = new Rgba32(v, v, v, (byte)(i == 0 ? 0 : 255));
        }

        GbaPalette restored = GbaPalette.FromBytes(GbaPalette.ToBytes(colors));

        for (int i = 0; i < 16; i++) Assert.Equal(colors[i], restored[i]);
    }

    [Fact]
    public void RejectsAShortPalette()
    {
        Assert.Throws<ArgumentException>(() => GbaPalette.FromBytes(new byte[8]));
    }
}

public class PngWriterTests
{
    [Fact]
    public void ProducesAStructurallyValidPngWithTheExpectedPixels()
    {
        const int width = 4, height = 3;
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i * 7);

        byte[] png = PngWriter.ToArray(width, height, rgba);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);

        List<(string Type, byte[] Data)> chunks = PngProbe.ReadChunks(png);
        Assert.Equal(["IHDR", "IDAT", "IEND"], chunks.Select(c => c.Type).ToArray());

        byte[] ihdr = chunks[0].Data;
        Assert.Equal(width, (ihdr[0] << 24) | (ihdr[1] << 16) | (ihdr[2] << 8) | ihdr[3]);
        Assert.Equal(height, (ihdr[4] << 24) | (ihdr[5] << 16) | (ihdr[6] << 8) | ihdr[7]);
        Assert.Equal(8, ihdr[8]);
        Assert.Equal(6, ihdr[9]);

        // Inflate the image data and confirm every scanline round-trips.
        byte[] raw = PngProbe.Inflate(chunks[1].Data);
        Assert.Equal(height * (width * 4 + 1), raw.Length);

        for (int y = 0; y < height; y++)
        {
            int rowStart = y * (width * 4 + 1);
            Assert.Equal(0, raw[rowStart]); // filter type "none"
            Assert.Equal(
                rgba[(y * width * 4)..((y + 1) * width * 4)],
                raw[(rowStart + 1)..(rowStart + 1 + width * 4)]);
        }
    }

    [Fact]
    public void EveryChunkCrcIsCorrect()
    {
        byte[] png = PngWriter.ToArray(8, 8, new byte[8 * 8 * 4]);
        PngProbe.ReadChunks(png, verifyCrc: true);
    }

    [Fact]
    public void RejectsAPixelBufferOfTheWrongLength()
    {
        Assert.Throws<ArgumentException>(() => PngWriter.ToArray(4, 4, new byte[10]));
    }
}

/// <summary>Minimal PNG chunk reader used only to verify what the writer produced.</summary>
internal static class PngProbe
{
    public static List<(string Type, byte[] Data)> ReadChunks(byte[] png, bool verifyCrc = false)
    {
        var chunks = new List<(string, byte[])>();
        int pos = 8;

        while (pos < png.Length)
        {
            int length = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
            string type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            byte[] data = png[(pos + 8)..(pos + 8 + length)];

            if (verifyCrc)
            {
                uint stored = (uint)((png[pos + 8 + length] << 24) | (png[pos + 9 + length] << 16) |
                                     (png[pos + 10 + length] << 8) | png[pos + 11 + length]);
                uint actual = Crc(png.AsSpan(pos + 4, 4 + length));
                Assert.Equal(stored, actual);
            }

            chunks.Add((type, data));
            pos += 12 + length;
        }

        return chunks;
    }

    public static byte[] Inflate(byte[] zlib)
    {
        using var input = new MemoryStream(zlib);
        using var decompressor = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        return output.ToArray();
    }

    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint c = 0xFFFFFFFFu;

        foreach (byte b in bytes)
        {
            c ^= b;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }

        return c ^ 0xFFFFFFFFu;
    }
}
