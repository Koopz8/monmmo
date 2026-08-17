namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// A run of sites close enough together to be one place rather than several.
/// </summary>
/// <param name="From">The first site in the run.</param>
/// <param name="To">The last site in the run.</param>
/// <param name="Sites">How many sites are in it.</param>
/// <param name="Entropy">
/// Shannon entropy of the bytes it spans. This cartridge's script regions run about 5.7 bits
/// per byte and its tables run under 4, so this is the cheapest thing that tells them apart.
/// </param>
public sealed record Clump(int From, int To, int Sites, double Entropy)
{
    /// <summary>Under this, the bytes are table-like rather than script-like.</summary>
    public const double TableLike = 4.5;

    /// <summary>True when the span reads as data rather than as code.</summary>
    public bool LooksLikeATable => Entropy < TableLike;
}

/// <summary>
/// How much of a count is one place rather than many.
/// <para>
/// <b>The error bar this project prints is a whole-image average, and the image is not
/// uniform.</b> "A three-byte pattern turns up by accident about 1.0 time(s) in an image this
/// size" assumes independent bytes. A run of table data with a repeating record in it produces
/// seven hits in eight hundred bytes, and no amount of the global figure predicts that.
/// </para>
/// <para>
/// <c>0x0089</c> is the case. It turns up nine times against a floor of one, which reads as
/// signal — and seven of the nine are inside 791 bytes of a low-entropy table with names in it,
/// where the same record repeats and the pattern is a field inside it rather than a command.
/// Nine sites spread over 16 MiB and nine sites inside one kilobyte are the same number and
/// completely different findings, and they have been printing identically.
/// </para>
/// <para>
/// Here rather than in whoever prints it, for the reason this project has moved a rule out of
/// <c>Program.cs</c> six times before: a rule about the world in a file no test can reach is a
/// rule nothing can fail.
/// </para>
/// </summary>
public static class HowClustered
{
    /// <summary>
    /// Closer than this to the site before it and the two are one place.
    /// <para>
    /// A kilobyte. Chance puts two three-byte hits this close in a 16 MiB file about once in
    /// sixteen thousand pairs, so the threshold is not a fine judgement — anything in the same
    /// order of magnitude gives the same answer, and the number is MODELLED rather than read.
    /// </para>
    /// </summary>
    public const int SamePlace = 1024;

    /// <summary>
    /// The runs of sites that sit within <see cref="SamePlace"/> of each other, longest first.
    /// <para>
    /// Empty when the sites are spread out, which is the answer that matters: it says the count
    /// above is that many separate facts about the file.
    /// </para>
    /// </summary>
    /// <param name="rom">The image, for the entropy of each run.</param>
    /// <param name="offsets">Where the sites are. Order does not matter.</param>
    /// <param name="pattern">How many bytes a site is, so a run's span includes the last one.</param>
    public static IReadOnlyList<Clump> In(Rom rom, IEnumerable<int> offsets, int pattern = 3)
    {
        List<int> at = [.. offsets.Distinct().OrderBy(o => o)];

        var clumps = new List<Clump>();
        var start = 0;

        for (var i = 1; i <= at.Count; i++)
        {
            if (i < at.Count && at[i] - at[i - 1] <= SamePlace) continue;

            if (i - start > 1)
            {
                clumps.Add(new Clump(
                    at[start],
                    at[i - 1],
                    i - start,
                    EntropyOf(rom, at[start], at[i - 1] + pattern)));
            }

            start = i;
        }

        return [.. clumps.OrderByDescending(c => c.Sites)];
    }

    /// <summary>How many of the sites are in a clump at all.</summary>
    public static int Clumped(Rom rom, IEnumerable<int> offsets, int pattern = 3) =>
        In(rom, offsets, pattern).Sum(c => c.Sites);

    /// <summary>Shannon entropy of a slice, in bits per byte.</summary>
    public static double EntropyOf(Rom rom, int from, int to)
    {
        var counts = new int[256];
        var n = 0;

        for (int i = Math.Max(0, from); i < Math.Min(rom.Length, to); i++)
        {
            counts[rom.ReadU8(i)]++;
            n++;
        }

        if (n == 0) return 0;

        var h = 0.0;

        foreach (int c in counts)
        {
            if (c == 0) continue;

            double p = (double)c / n;

            h -= p * Math.Log2(p);
        }

        return h;
    }
}
