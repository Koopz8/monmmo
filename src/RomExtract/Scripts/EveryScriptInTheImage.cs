namespace PokeMmo.RomExtract.Scripts;

/// <summary>What a whole-image hunt for script blocks found.</summary>
/// <param name="Pointed">Distinct addresses something in the file points at.</param>
/// <param name="Entries">Those of them that decode to a proper end — the way-in list.</param>
/// <param name="Blocks">Every block reachable from those entries, deduplicated.</param>
public sealed record TheImagesScripts(
    int Pointed, IReadOnlyCollection<uint> Entries, IReadOnlyCollection<uint> Blocks)
{
    /// <summary>
    /// For each entry, the longest run of consecutive aligned ROM-address words that one of its
    /// pointer sites sits in.
    /// </summary>
    /// <remarks>
    /// <b>The one thing that tells a script's name from a text's.</b> Compiled code names a
    /// script by putting its address in a literal pool, where it sits among instructions and
    /// numbers — a run of one. A table of a hundred and fifty text pointers is a run of a hundred
    /// and fifty, and every one of its entries is four bytes that look exactly like a script
    /// pointer. The reversed-image floor cannot see the difference, because a table reversed is
    /// still a table.
    /// </remarks>
    public IReadOnlyDictionary<uint, int> InARunOf { get; init; } =
        new Dictionary<uint, int>();
}

/// <summary>
/// Every script block in the file, rather than every script block a map leads to.
/// <para>
/// <b>The map scan opens 0.6% of this cartridge.</b> Every sweep this project runs over "the
/// scripts" runs over the ones the four hundred and twenty-five maps point at, and 252's operand
/// audit — which found two write operands in neither of this repository's tables — is one of
/// them. Its own note says the obvious next question is the whole image, and it has been owed
/// since.
/// </para>
/// <para>
/// <b>The population is what makes it a question and not a fishing trip.</b> "Every offset whose
/// bytes read as a script" is sixteen million tries and mostly luck; what this uses instead is the
/// cartridge's own index — every four bytes anywhere in the file holding a ROM address, which is
/// how compiled code names a script and how a script names another one. An address something
/// points at, which decodes to a proper end, is a way in. Everything reachable from it is a block.
/// </para>
/// <para>
/// <b>And it has a floor</b>, because "reads as a script" is a weak filter on sixteen megabytes:
/// <see cref="Floor"/> runs the identical hunt on the image reversed, which keeps every byte and
/// every byte frequency and destroys every command boundary.
/// </para>
/// </summary>
public static class EveryScriptInTheImage
{
    /// <summary>Every way in, and everything reachable from one.</summary>
    /// <param name="maxScripts">
    /// The reach limit <see cref="ScriptReader.Reachable"/> takes, handed through rather than
    /// defaulted here — a whole-image walk with a different limit from the map scan's would
    /// produce two populations that differ for a reason nobody wrote down.
    /// </param>
    /// <param name="aligned">
    /// Take only addresses named by four bytes ON a four-byte boundary.
    /// <para>
    /// <b>The difference between a population and a haystack.</b> Any four bytes at any offset
    /// with <c>0x08</c> on top look like a pointer, and in sixteen megabytes tens of thousands do
    /// by accident. A pointer the game's own code holds is a literal-pool entry or a table entry
    /// and is aligned; a pointer a script holds is the argument of a <c>call</c> or a
    /// <c>goto</c>, which is not aligned — and those blocks arrive anyway, through the reach of
    /// whatever block holds the call. So alignment costs no real script and takes most of the
    /// luck out.
    /// </para>
    /// <para>
    /// Kept as a parameter rather than as the only behaviour because the loose answer is half the
    /// evidence: what the floor does when it is turned on is the argument for it.
    /// </para>
    /// </param>
    /// <param name="leastCommands">
    /// How long a block has to be to count as a way in.
    /// <para>
    /// The other half of the luck. Three bytes that decode and hit an <c>end</c> is a short block
    /// and a common accident; a real script is several commands. Swept rather than chosen — the
    /// caller prints the floor AND a known operand's score at each setting, so the threshold is
    /// picked by the control rather than by the answer it produces.
    /// </para>
    /// </param>
    public static TheImagesScripts In(
        Rom rom, int maxScripts = 96, bool aligned = true, int leastCommands = 1)
    {
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index =
            EverywhereInTheImage.PointerIndex(rom);

        var entries = new List<uint>();

        foreach ((uint at, IReadOnlyList<int> from) in index)
        {
            if (aligned && !from.Any(o => o % 4 == 0)) continue;
            if (!ScriptReader.ReadsAsAScript(rom, at)) continue;
            if (leastCommands > 1 && ScriptReader.Read(rom, at).Take(leastCommands).Count()
                < leastCommands)
            {
                continue;
            }

            entries.Add(at);
        }

        entries.Sort();

        var blocks = new HashSet<uint>();

        foreach (uint entry in entries)
        {
            foreach (uint block in ScriptReader.Reachable(rom, entry, maxScripts)) blocks.Add(block);
        }

        return new TheImagesScripts(
            aligned ? index.Count(p => p.Value.Any(o => o % 4 == 0)) : index.Count,
            entries,
            blocks)
        {
            InARunOf = Runs(rom, entries, index),
        };
    }

    /// <summary>
    /// How long a run of consecutive aligned ROM-address words each entry is named from.
    /// </summary>
    /// <remarks>
    /// One pass to mark every aligned word that holds a ROM address, one pass to measure the runs
    /// they fall in, and then each entry takes the longest run any of its own sites sits in. The
    /// longest rather than the shortest: an address named once by a table and once by a literal
    /// pool is named by a table, and it is the table that has to be explained away.
    /// </remarks>
    public static IReadOnlyDictionary<uint, int> Runs(
        Rom rom,
        IEnumerable<uint> entries,
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index)
    {
        int words = rom.Length / 4;
        var holds = new bool[words];

        for (var i = 0; i < words; i++)
        {
            holds[i] = rom.ReadU8((i * 4) + 3) == 0x08 && rom.IsRomAddress(rom.ReadU32(i * 4));
        }

        var run = new int[words];

        for (var i = 0; i < words;)
        {
            if (!holds[i]) { i++; continue; }

            int start = i;

            while (i < words && holds[i]) i++;

            for (int j = start; j < i; j++) run[j] = i - start;
        }

        var longest = new Dictionary<uint, int>();

        foreach (uint entry in entries)
        {
            var most = 0;

            foreach (int at in index.GetValueOrDefault(entry) ?? [])
            {
                if (at % 4 != 0) continue;

                int word = at / 4;

                if (word < run.Length && run[word] > most) most = run[word];
            }

            longest[entry] = most;
        }

        return longest;
    }

    /// <summary>
    /// The same entries asked of an address a little further on — the region-preserving floor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The control the reversal cannot be.</b> What "an aligned pointer whose target decodes to
    /// a proper end" claims is that the pointer NAMES that block. The null for that is not a file
    /// with different statistics; it is these same pointers, in this same file, aimed a few bytes
    /// off. If a target that is four bytes wrong decodes just as often, then decoding says nothing
    /// about naming.
    /// </para>
    /// <para>
    /// Small offsets on purpose. A rotation of four megabytes moves a pointer out of the region
    /// scripts live in and measures whether THAT region decodes; a nudge keeps it exactly where it
    /// was and asks the only question in dispute.
    /// </para>
    /// </remarks>
    public static int NudgedFloor(Rom rom, IEnumerable<uint> pointed, int by) =>
        Nudged(rom, pointed, by).Count;

    /// <summary>
    /// The nudge as a POPULATION rather than a count: the same pointers, aimed
    /// <paramref name="by"/> bytes off, and the ones that still decode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A count cannot be asked what it is made of</b> (275). The nudge has been this project's
    /// floor for anything that follows a pointer since 269 and it has only ever produced a
    /// number; the mixture bound needs a junk model it can tally, and the reversed image — the
    /// only one it has ever had — is the one control 268 showed to be blind here, and supplies
    /// 456 blocks where this supplies thousands.
    /// </para>
    /// <para>
    /// Same loop as <c>NudgedFloor</c>, which now counts what this returns. Two questions about
    /// one nudge, one place that performs it (258).
    /// </para>
    /// </remarks>
    public static IReadOnlyList<uint> Nudged(Rom rom, IEnumerable<uint> pointed, int by) =>
        [
            .. pointed.Select(at => (uint)(at + by))
                .Where(at => ScriptReader.ReadsAsAScript(rom, at)),
        ];

    /// <summary>Every aligned address something points at, decoding or not — the nudge's list.</summary>
    public static IReadOnlyList<uint> Aligned(Rom rom)
    {
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index =
            EverywhereInTheImage.PointerIndex(rom);

        return [.. index.Where(p => p.Value.Any(o => o % 4 == 0)).Select(p => p.Key).Order()];
    }

    /// <summary>The identical hunt on the image backwards.</summary>
    /// <remarks>
    /// Not a formality. A pointer is four bytes with <c>0x08</c> on top and a decode that reaches
    /// an end is a few more bytes of luck, and this file has sixteen million places to be lucky
    /// in. Whatever the hunt finds in the reversal is what it would find in a file with these
    /// statistics and no scripts at all.
    /// </remarks>
    public static TheImagesScripts Floor(
        Rom rom, int maxScripts = 96, bool aligned = true, int leastCommands = 1)
    {
        byte[] backwards = rom.Span.ToArray();

        Array.Reverse(backwards);

        return In(new Rom(backwards), maxScripts, aligned, leastCommands);
    }
}
