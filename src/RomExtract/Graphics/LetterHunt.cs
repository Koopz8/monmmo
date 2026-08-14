namespace PokeMmo.RomExtract.Graphics;

/// <summary>One way of looking, and what it turned up.</summary>
public sealed record LetterSearch(string Method, int Looked, IReadOnlyList<LetterHit> Hits);

/// <summary>Somewhere that scored well enough to be worth a person's eye.</summary>
public sealed record LetterHit(uint Address, int Depth, int Height, int Offset, int Score, int OutOf)
{
    public double Share => OutOf == 0 ? 0 : (double)Score / OutOf;
}

/// <summary>
/// Looking for the cartridge's own lettering, and writing down where it is not.
/// <para>
/// Every menu in this client draws with a font somebody typed in by hand, on rectangles
/// this project picked the colours of, next to a map and a walking figure that are the
/// real thing. It is the one part of the client that does not come off the player's own
/// image, and it is the part they notice.
/// </para>
/// <para>
/// Two milestones have gone looking and neither found it. That is worth a tool rather
/// than a note, because the expensive part of a search like this is not running it —
/// it is discovering, again, that the four obvious ideas do not work. What follows is
/// those four, each one mechanical, each one reporting what it looked at as well as
/// what it found.
/// </para>
/// <para>
/// <b>By character code.</b> A sheet indexed by code has a glyph at every code the
/// encoding prints and nothing at the ones it does not. Seventy-six of two hundred and
/// fifty-six codes print something on this cartridge, so the blank-or-not pattern is
/// two hundred and fifty-six yes-or-nos — far too many to match by accident. Nowhere on
/// the image matches it at one, two or four bits deep.
/// </para>
/// <para>
/// <b>By the shape of an alphabet.</b> Eleven capitals read the same backwards — A H I
/// M O T U V W X Y — and fifteen do not, in an order nothing else has. Scoring every
/// run of twenty-six glyphs against that pattern, over three bit depths and three glyph
/// heights, produces nothing above twenty-three of twenty-six, and the things that do
/// score twenty-three are vertical stripes.
/// </para>
/// <para>
/// <b>The same, unpacked.</b> Seventeen hundred and seventy-nine compressed blocks on
/// this image, decompressed and put through the same test. Two score twenty-four; both
/// are noise when drawn.
/// </para>
/// <para>
/// What that leaves is a layout none of these readers assume. The Japanese block at
/// 0x08232800 <em>is</em> plain eight-by-eight and reads perfectly, which is how we know
/// the readers work — it holds a clean Latin lowercase alphabet among the kana, and it
/// is not indexed by code either. So the lettering exists, in this image, in a form that
/// none of four mechanical tests recognises. That is a smaller haystack than yesterday.
/// </para>
/// </summary>
public static class LetterHunt
{
    /// <summary>
    /// Which capitals are the same read backwards, in order from A.
    /// <para>
    /// Judged inside the ink rather than inside the glyph box: these are variable-width
    /// letters sitting in a fixed box, and the empty columns to the right of a narrow
    /// one would call everything even.
    /// </para>
    /// </summary>
    private const string Mirrored = "10000001100010100000111111";

    /// <summary>How wide a glyph is assumed to be. Every font this project has seen is.</summary>
    private const int Wide = 8;

    /// <summary>How lopsided a glyph may be and still count as even.</summary>
    private const double Lopsided = 0.12;

    /// <summary>Below this much ink there is no shape to judge.</summary>
    private const int Faintest = 6;

    private static readonly int[] Depths = [1, 2, 4];

    private static readonly int[] Heights = [8, 12, 16];

    /// <summary>
    /// Whether the glyph at this offset reads the same backwards, or nothing when there
    /// is not enough ink in it to say.
    /// </summary>
    public static bool? ReadsTheSameBackwards(ReadOnlySpan<byte> data, int at, int depth, int height)
    {
        Span<bool> on = stackalloc bool[16 * Wide];

        int left = Wide, right = -1, ink = 0;

        for (int y = 0; y < height; y++)
        {
            int row = at + y * depth;

            if (row + depth > data.Length) return null;

            for (int x = 0; x < Wide; x++)
            {
                bool lit = depth switch
                {
                    1 => ((data[row] >> (Wide - 1 - x)) & 1) != 0,
                    2 => ((data[row + x / 4] >> (x % 4 * 2)) & 3) != 0,
                    _ => (x % 2 == 0 ? data[row + x / 2] & 0xF : data[row + x / 2] >> 4) != 0,
                };

                on[y * Wide + x] = lit;

                if (!lit) continue;

                ink++;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }

        if (ink < Faintest || right <= left) return null;

        int wrong = 0, total = 0;

        for (int y = 0; y < height; y++)
            for (int x = left; x <= right; x++)
            {
                total++;

                if (on[y * Wide + x] != on[y * Wide + left + right - x]) wrong++;
            }

        return wrong <= total * Lopsided;
    }

    /// <summary>
    /// Every run of twenty-six glyphs whose evenness matches an alphabet's, at or above
    /// the given score.
    /// </summary>
    public static List<LetterHit> LooksLikeAnAlphabet(
        ReadOnlySpan<byte> data, uint address, int least = 24)
    {
        var hits = new List<LetterHit>();

        foreach (int depth in Depths)
        {
            foreach (int height in Heights)
            {
                int bytes = depth * height;
                int glyphs = data.Length / bytes;

                if (glyphs < Mirrored.Length) continue;

                var even = new bool?[glyphs];

                for (int g = 0; g < glyphs; g++) even[g] = ReadsTheSameBackwards(data, g * bytes, depth, height);

                for (int g = 0; g + Mirrored.Length <= glyphs; g++)
                {
                    int score = 0;
                    var usable = true;

                    for (int i = 0; i < Mirrored.Length && usable; i++)
                    {
                        if (even[g + i] is not { } same) usable = false;
                        else if (same == (Mirrored[i] == '1')) score++;
                    }

                    if (usable && score >= least)
                        hits.Add(new LetterHit(address, depth, height, g, score, Mirrored.Length));
                }
            }
        }

        return hits;
    }

    /// <summary>
    /// Every place where blank-or-not matches print-or-not across all two hundred and
    /// fifty-six codes.
    /// <para>
    /// The strongest of the four when it works, and it does not work here. A sheet laid
    /// out by character code would light up at once; a sheet packed shoulder to shoulder,
    /// with a table somewhere turning a code into a position, never will.
    /// </para>
    /// </summary>
    public static List<LetterHit> IndexedByCharacterCode(Rom rom, int least = 240)
    {
        var prints = new bool[256];

        for (int code = 1; code < 256; code++)
        {
            string one = GameText.Decode([(byte)code]);

            prints[code] = one.Length == 1 && one is not ("?" or " ");
        }

        var hits = new List<LetterHit>();

        foreach (int depth in Depths)
        {
            int bytes = depth * Wide;
            int span = 256 * bytes;

            for (int at = 0; at + span <= rom.Length; at += 4)
            {
                // Code nought is a space and has to be empty. That one line throws most
                // of sixteen megabytes away before anything else is counted.
                if (!IsBlank(rom, at, bytes)) continue;

                int agreed = 0;

                for (int code = 1; code < 256; code++)
                    if (IsBlank(rom, at + code * bytes, bytes) != prints[code]) agreed++;

                if (agreed >= least)
                    hits.Add(new LetterHit(Rom.BaseAddress + (uint)at, depth, Wide, 0, agreed, 255));
            }
        }

        return hits;
    }

    /// <summary>How many codes this cartridge's encoding prints something for.</summary>
    public static int PrintableCodes()
    {
        var printed = 0;

        for (int code = 1; code < 256; code++)
        {
            string one = GameText.Decode([(byte)code]);

            if (one.Length == 1 && one is not ("?" or " ")) printed++;
        }

        return printed;
    }

    private static bool IsBlank(Rom rom, int at, int bytes)
    {
        for (int i = 0; i < bytes; i++)
            if (rom.ReadU8(at + i) != 0) return false;

        return true;
    }
}
