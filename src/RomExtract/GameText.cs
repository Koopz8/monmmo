using System.Text;

namespace PokeMmo.RomExtract;

/// <summary>
/// Decoder for the proprietary single-byte character set the Generation III
/// games use for text. Only the subset needed for species and move names is
/// mapped; anything unmapped decodes to '?' rather than throwing, so a wrong
/// offset produces visible garbage instead of an exception.
/// </summary>
public static class GameText
{
    /// <summary>End-of-string marker.</summary>
    public const byte Terminator = 0xFF;

    /// <summary>Width of one species-name record on the cartridge, including the terminator.</summary>
    public const int SpeciesNameLength = 11;

    /// <summary>Width of one move-name record on the cartridge, including the terminator.</summary>
    public const int MoveNameLength = 13;

    private const byte UppercaseA = 0xBB;
    private const byte LowercaseA = 0xD5;
    private const byte DigitZero = 0xA1;

    private static readonly Dictionary<byte, char> Punctuation = new()
    {
        [0x00] = ' ',
        [0xAB] = '!',
        [0xAC] = '?',
        [0xAD] = '.',
        [0xAE] = '-',
        [0xB1] = '“',
        [0xB2] = '”',
        [0xB3] = '‘',
        [0xB4] = '’',
        [0xB5] = '♂', // male sign
        [0xB6] = '♀', // female sign
        [0xB8] = ',',
        [0xBA] = '/',
    };

    /// <summary>Decodes bytes up to the terminator or the end of the span.</summary>
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length);

        foreach (byte b in bytes)
        {
            if (b == Terminator) break;
            sb.Append(DecodeByte(b));
        }

        return sb.ToString().TrimEnd();
    }

    private static char DecodeByte(byte b)
    {
        if (b >= UppercaseA && b < UppercaseA + 26) return (char)('A' + (b - UppercaseA));
        if (b >= LowercaseA && b < LowercaseA + 26) return (char)('a' + (b - LowercaseA));
        if (b >= DigitZero && b < DigitZero + 10) return (char)('0' + (b - DigitZero));
        return Punctuation.TryGetValue(b, out char c) ? c : '?';
    }

    /// <summary>Encodes ASCII text into the cartridge character set, terminated and padded to <paramref name="fieldWidth"/>.</summary>
    public static byte[] Encode(string text, int fieldWidth)
    {
        var buffer = new byte[fieldWidth];
        buffer.AsSpan().Fill(Terminator);

        int i = 0;
        foreach (char c in text)
        {
            if (i >= fieldWidth - 1) break;
            buffer[i++] = EncodeChar(c);
        }

        return buffer;
    }

    private static byte EncodeChar(char c)
    {
        if (c is >= 'A' and <= 'Z') return (byte)(UppercaseA + (c - 'A'));
        if (c is >= 'a' and <= 'z') return (byte)(LowercaseA + (c - 'a'));
        if (c is >= '0' and <= '9') return (byte)(DigitZero + (c - '0'));

        foreach ((byte raw, char mapped) in Punctuation)
        {
            if (mapped == c) return raw;
        }

        return 0x00;
    }

    /// <summary>
    /// True when a decoded name looks like a real name rather than the noise a
    /// wrong offset produces. Used to validate a located table before trusting it.
    /// </summary>
    public static bool LooksLikeName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (s.Contains('?')) return false;

        foreach (char c in s)
        {
            bool ok = char.IsLetterOrDigit(c) || c is ' ' or '.' or '-' or '’' or '♂' or '♀';
            if (!ok) return false;
        }

        return true;
    }
}
