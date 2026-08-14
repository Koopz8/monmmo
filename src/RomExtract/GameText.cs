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

        // Read off the cartridge rather than remembered, which is what printing the
        // byte was for. Each one is fixed by the sentence it turned up in:
        //   POK{1B}MON              — every line on every route
        //   Go with GRIMER first{B0}and{B0}   — an ellipsis, not a word
        //   ELI{F0} Twin power is fantastic.  — the twins on Route 8 name themselves
        [0x1B] = 'é',
        [0xB0] = '…',
        [0xF0] = ':',
    };

    /// <summary>A line break inside a text box.</summary>
    public const byte NewLine = 0xFE;

    /// <summary>Wait for a button, then clear the box and carry on.</summary>
    public const byte Paragraph = 0xFB;

    /// <summary>Wait for a button, then scroll up a line and carry on.</summary>
    public const byte Scroll = 0xFA;

    /// <summary>
    /// The introducer for a formatting command, and what it introduces is an argument
    /// list whose length depends on the command.
    /// <para>
    /// It was called Prompt here and skipped as a single byte, which is how the sailor
    /// on the VERMILION dock came to say "{06}{04}Sorry!" — the two bytes after it are
    /// its arguments and were being read as letters. Fifteen of the 2230 pages the map
    /// scripts name carry one, which is few enough to have gone unnoticed for fifty
    /// milestones and quite enough to be on the critical path: two of the fifteen are
    /// the gangway to the S.S. ANNE.
    /// </para>
    /// <para>
    /// The lengths are read off the sites rather than remembered. Every one of them
    /// leaves clean text at exactly one width:
    /// </para>
    /// <code>
    ///   FC 06 02 | {FD}{01} flashed the S.S. TICKET!     one argument
    ///   FC 06 04 | Great! Welcome to the S.S. ANNE!      one argument
    ///   FC 08 60 | ...                                   one argument
    ///   FC 0B 56 01 | FF                                 two, and the page ends
    ///   FC 17 | FC 0B 01 01 | FC 08 60 | FC 18 | FB      none, and none
    /// </code>
    /// <para>
    /// The last of those is the one that settles it: four of these run back to back, so
    /// any width that is wrong for one of them derails the next, and only this reading
    /// arrives at the "Okay, then." that follows.
    /// </para>
    /// </summary>
    public const byte Format = 0xFC;

    /// <summary>
    /// How many bytes each formatting command takes after its own number.
    /// <para>
    /// What they mean is not needed and not claimed: they change colour, timing and
    /// sound, none of which this client does mid-line. What is needed is their length,
    /// because the alternative to knowing it is printing it.
    /// </para>
    /// <para>
    /// A command not in this table takes one, which is the commonest length here and the
    /// one that fails most gently — a page with an unknown code in it loses a byte
    /// rather than a sentence.
    /// </para>
    /// </summary>
    public static int FormatArguments(byte command) => command switch
    {
        0x0B => 2,
        0x17 or 0x18 => 0,
        _ => 1,
    };

    /// <summary>
    /// Decodes a run of dialogue, which is not the same job as decoding a name.
    /// <para>
    /// A name is a fixed-width record of letters. Dialogue is a stream with control
    /// bytes in it — line breaks, page breaks, waits — and running it through the name
    /// decoder turns every one of those into a question mark. The control bytes are
    /// what make a text box read as a text box, so they are kept and turned into the
    /// breaks a renderer needs.
    /// </para>
    /// <para>
    /// <paramref name="maxBytes"/> bounds it: dialogue has no length, only a
    /// terminator, and a pointer that is not really text would otherwise read to the
    /// end of the cartridge.
    /// </para>
    /// </summary>
    public static List<string> DecodeDialogue(ReadOnlySpan<byte> bytes, int maxBytes = 1024)
    {
        var pages = new List<string>();
        var page = new StringBuilder();

        for (int i = 0; i < bytes.Length && i < maxBytes; i++)
        {
            byte b = bytes[i];

            if (b == Terminator) break;

            switch (b)
            {
                case NewLine:
                    page.Append('\n');
                    break;

                // A scroll and a page break differ in how the box animates, which is
                // not something this can express — both end the page.
                case Paragraph:
                case Scroll:
                    // An empty page is not a page. They appear where a line begins with
                    // nothing but formatting — the trainer at the door of the S.S. ANNE
                    // starts with four commands and a page break — and handing one to a
                    // text box gives the player an empty box to press through.
                    if (page.ToString().TrimEnd() is { Length: > 0 } said) pages.Add(said);

                    page.Clear();
                    break;

                // The formatting command and its arguments, stepped over together.
                case Format:
                    i += 1 + (i + 1 < bytes.Length ? FormatArguments(bytes[i + 1]) : 0);
                    break;

                default:
                    // A byte with no letter behind it is written as its own number
                    // rather than as a question mark. A question mark is a lie — it
                    // reads as punctuation somebody typed, so the one character this
                    // project cannot decode is also the one it can never notice. The
                    // é in POKéMON hid in plain sight in every line on Route 1.
                    if (DecodeByte(b) == '?' && b != 0xAC) page.Append($"{{{b:X2}}}");
                    else page.Append(DecodeByte(b));

                    break;
            }
        }

        if (page.Length > 0) pages.Add(page.ToString().TrimEnd());

        return pages;
    }

    /// <summary>
    /// True when a run of bytes reads as dialogue rather than as arbitrary data.
    /// <para>
    /// Used to decide whether a pointer leads to text at all. Anything can be decoded;
    /// the question is whether the result is words.
    /// </para>
    /// <para>
    /// Spaces are not evidence. A zero byte decodes as a space, so a run of empty
    /// memory decodes as nothing but spaces — which counted as perfectly readable text
    /// under the obvious version of this check, and made every pointer into padding
    /// look like somebody with a great deal to say.
    /// </para>
    /// </summary>
    public static bool LooksLikeDialogue(ReadOnlySpan<byte> bytes, int maxBytes = 512)
    {
        int words = 0;
        int nonsense = 0;

        for (int i = 0; i < bytes.Length && i < maxBytes; i++)
        {
            byte b = bytes[i];
            if (b == Terminator) break;

            if (b is NewLine or Paragraph or Scroll) continue;

            if (b == Format)
            {
                // Stepped over here too, and for a sharper reason than tidiness: this is
                // what decides whether a pointer leads to text at all, and counting a
                // command's arguments as letters is counting nonsense against a page
                // that is perfectly good.
                i += 1 + (i + 1 < bytes.Length ? FormatArguments(bytes[i + 1]) : 0);
                continue;
            }

            char decoded = DecodeByte(b);

            if (decoded == ' ') continue;

            if (decoded == '?' && b != 0xAC) nonsense++;
            else words++;
        }

        return words >= 4 && words >= nonsense * 3;
    }

    /// <summary>
    /// The same text with the characters a plain ASCII font cannot draw swapped for
    /// ones it can.
    /// <para>
    /// The cartridge's apostrophe is a curly one, and it turns up in roughly every
    /// other sentence anybody says. A font with no glyph for it draws nothing at all,
    /// so "I'm" comes out as "Im" and the text looks subtly broken everywhere without
    /// ever looking broken enough to investigate.
    /// </para>
    /// </summary>
    public static string ToAscii(string text)
    {
        var sb = new StringBuilder(text.Length);

        foreach (char c in text)
        {
            switch (c)
            {
                case '“':
                case '”': sb.Append('"'); break;
                case '‘':
                case '’': sb.Append('\''); break;
                case '♂': sb.Append("(M)"); break;
                case '♀': sb.Append("(F)"); break;
                case 'é': sb.Append('e'); break;
                case '…': sb.Append("..."); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }

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

    /// <summary>
    /// Encodes text into the cartridge character set as a fixed-width record.
    /// <para>
    /// The name is followed by a single terminator, and any remaining space is
    /// <em>zero</em> fill. That matters: these tables are fixed-width arrays
    /// initialised from string literals, so the compiler zero-fills the tail rather
    /// than repeating the terminator. Assuming otherwise makes byte-exact searches
    /// for a known record silently fail to match.
    /// </para>
    /// </summary>
    public static byte[] Encode(string text, int fieldWidth)
    {
        var buffer = new byte[fieldWidth]; // zero-filled by default

        int i = 0;
        foreach (char c in text)
        {
            if (i >= fieldWidth - 1) break;
            buffer[i++] = EncodeChar(c);
        }

        buffer[i] = Terminator;
        return buffer;
    }

    /// <summary>
    /// Encodes text as a search key: the characters plus the terminator, and nothing
    /// after it.
    /// <para>
    /// Used to locate a known record inside a table without depending on how the tail
    /// of the field happens to be padded, which varies between tables and revisions.
    /// </para>
    /// </summary>
    public static byte[] EncodeAnchor(string text)
    {
        var buffer = new byte[text.Length + 1];

        for (int i = 0; i < text.Length; i++)
            buffer[i] = EncodeChar(text[i]);

        buffer[^1] = Terminator;
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
