namespace PokeMmo.RomExtract;

/// <summary>
/// The LZ77 variant implemented by the GBA BIOS (compression type 0x10), which is
/// what every compressed graphic on the cartridge is stored in.
/// <para>
/// Stream layout: one header byte 0x10, then a 24-bit little-endian decompressed
/// size. The body is a sequence of groups; each group is one flag byte followed by
/// up to eight units, walked from the most significant flag bit down. A clear bit
/// means "copy one literal byte", a set bit means "copy <c>length</c> bytes from
/// <c>distance</c> bytes earlier in the output".
/// </para>
/// </summary>
public static class Lz77
{
    public const byte CompressionTypeMarker = 0x10;

    /// <summary>Reads the declared decompressed size without decompressing.</summary>
    public static int PeekDecompressedSize(ReadOnlySpan<byte> src)
    {
        if (src.Length < 4) throw new InvalidDataException("LZ77 stream is shorter than its header.");
        if (src[0] != CompressionTypeMarker)
            throw new InvalidDataException($"Expected LZ77 marker 0x10, found 0x{src[0]:X2}.");

        return src[1] | (src[2] << 8) | (src[3] << 16);
    }

    /// <summary>
    /// Decompresses a BIOS LZ77 stream.
    /// </summary>
    /// <param name="src">Bytes starting at the 0x10 header.</param>
    /// <param name="consumed">Number of input bytes the stream occupied.</param>
    public static byte[] Decompress(ReadOnlySpan<byte> src, out int consumed)
    {
        int size = PeekDecompressedSize(src);
        var dst = new byte[size];

        int inPos = 4;
        int outPos = 0;

        while (outPos < size)
        {
            if (inPos >= src.Length)
                throw new InvalidDataException("LZ77 stream ended before producing the declared output size.");

            byte flags = src[inPos++];

            for (int bit = 7; bit >= 0 && outPos < size; bit--)
            {
                bool isBackReference = ((flags >> bit) & 1) != 0;

                if (!isBackReference)
                {
                    if (inPos >= src.Length)
                        throw new InvalidDataException("LZ77 stream ended mid-literal.");

                    dst[outPos++] = src[inPos++];
                    continue;
                }

                if (inPos + 1 >= src.Length)
                    throw new InvalidDataException("LZ77 stream ended mid-reference.");

                byte b0 = src[inPos++];
                byte b1 = src[inPos++];

                int length = (b0 >> 4) + 3;
                int distance = (((b0 & 0x0F) << 8) | b1) + 1;

                if (distance > outPos)
                    throw new InvalidDataException(
                        $"LZ77 back-reference reaches {distance} bytes back but only {outPos} bytes have been produced.");

                // Deliberately byte-at-a-time: overlapping runs (distance < length)
                // are legal and are how the format encodes repeats.
                for (int i = 0; i < length && outPos < size; i++)
                {
                    dst[outPos] = dst[outPos - distance];
                    outPos++;
                }
            }
        }

        consumed = inPos;
        return dst;
    }

    public static byte[] Decompress(ReadOnlySpan<byte> src) => Decompress(src, out _);

    /// <summary>
    /// Produces a valid — but deliberately naive — LZ77 stream. This exists so tests
    /// can build synthetic cartridges to decompress; the extractor never needs to compress.
    /// </summary>
    public static byte[] Compress(ReadOnlySpan<byte> src)
    {
        var output = new List<byte>(src.Length + src.Length / 8 + 4)
        {
            CompressionTypeMarker,
            (byte)(src.Length & 0xFF),
            (byte)((src.Length >> 8) & 0xFF),
            (byte)((src.Length >> 16) & 0xFF),
        };

        int pos = 0;
        while (pos < src.Length)
        {
            int flagIndex = output.Count;
            output.Add(0);
            byte flags = 0;

            for (int bit = 7; bit >= 0 && pos < src.Length; bit--)
            {
                int bestLength = 0;
                int bestDistance = 0;

                int windowStart = Math.Max(0, pos - 0x1000);
                int maxLength = Math.Min(18, src.Length - pos);

                for (int candidate = windowStart; candidate < pos && bestLength < maxLength; candidate++)
                {
                    int length = 0;
                    while (length < maxLength && src[candidate + length] == src[pos + length])
                        length++;

                    if (length > bestLength)
                    {
                        bestLength = length;
                        bestDistance = pos - candidate;
                    }
                }

                if (bestLength >= 3)
                {
                    flags |= (byte)(1 << bit);
                    int encodedLength = bestLength - 3;
                    int encodedDistance = bestDistance - 1;
                    output.Add((byte)((encodedLength << 4) | ((encodedDistance >> 8) & 0x0F)));
                    output.Add((byte)(encodedDistance & 0xFF));
                    pos += bestLength;
                }
                else
                {
                    output.Add(src[pos++]);
                }
            }

            output[flagIndex] = flags;
        }

        return output.ToArray();
    }
}
