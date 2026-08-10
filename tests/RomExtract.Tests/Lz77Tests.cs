using PokeMmo.RomExtract;

namespace PokeMmo.RomExtract.Tests;

public class Lz77Tests
{
    [Fact]
    public void RoundTripsDataWithRepeatingStructure()
    {
        // Compressible on purpose: the format exists for exactly this shape of data.
        var original = new byte[4096];
        for (int i = 0; i < original.Length; i++)
            original[i] = (byte)((i / 16) % 7);

        byte[] compressed = Lz77.Compress(original);
        byte[] restored = Lz77.Decompress(compressed);

        Assert.Equal(original, restored);
        Assert.True(compressed.Length < original.Length, "repetitive input should actually shrink");
    }

    [Fact]
    public void RoundTripsIncompressibleData()
    {
        var random = new Random(1234);
        var original = new byte[1024];
        random.NextBytes(original);

        Assert.Equal(original, Lz77.Decompress(Lz77.Compress(original)));
    }

    [Fact]
    public void RoundTripsEmptyInput()
    {
        Assert.Empty(Lz77.Decompress(Lz77.Compress([])));
    }

    [Fact]
    public void ReportsDeclaredSizeWithoutDecompressing()
    {
        byte[] compressed = Lz77.Compress(new byte[0x800]);
        Assert.Equal(0x800, Lz77.PeekDecompressedSize(compressed));
    }

    [Fact]
    public void HandlesOverlappingBackReferences()
    {
        // A run of one byte repeated: literal 'A', then a reference reaching back
        // 1 byte for 10 bytes. The copy must proceed byte-at-a-time for this to work.
        byte[] stream =
        [
            0x10, 11, 0, 0,          // header: 11 bytes out
            0b0100_0000,             // unit 0 literal, unit 1 reference
            (byte)'A',
            0x70, 0x00,              // length (7 + 3) = 10, distance (0 + 1) = 1
        ];

        byte[] result = Lz77.Decompress(stream);

        Assert.Equal(11, result.Length);
        Assert.All(result, b => Assert.Equal((byte)'A', b));
    }

    [Fact]
    public void ReportsHowManyInputBytesWereConsumed()
    {
        byte[] compressed = Lz77.Compress(new byte[256]);
        var padded = new byte[compressed.Length + 500];
        compressed.CopyTo(padded, 0);

        Lz77.Decompress(padded, out int consumed);

        Assert.Equal(compressed.Length, consumed);
    }

    [Fact]
    public void RejectsAStreamThatIsNotLz77()
    {
        byte[] notCompressed = [0x11, 0x00, 0x08, 0x00, 0x00];
        var ex = Assert.Throws<InvalidDataException>(() => Lz77.Decompress(notCompressed));
        Assert.Contains("0x10", ex.Message);
    }

    [Fact]
    public void RejectsATruncatedStream()
    {
        byte[] truncated = [0x10, 0x00, 0x10, 0x00, 0b0000_0000, 1, 2];
        Assert.Throws<InvalidDataException>(() => Lz77.Decompress(truncated));
    }

    [Fact]
    public void RejectsABackReferenceThatReachesBeforeTheStart()
    {
        byte[] bad =
        [
            0x10, 8, 0, 0,
            0b1000_0000,
            0x00, 0x10,   // distance 17, but nothing has been produced yet
        ];

        Assert.Throws<InvalidDataException>(() => Lz77.Decompress(bad));
    }
}
