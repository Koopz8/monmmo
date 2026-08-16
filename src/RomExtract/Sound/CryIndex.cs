namespace PokeMmo.RomExtract.Sound;

/// <summary>
/// What was worked out about which entry of the cry table belongs to which creature.
/// </summary>
/// <param name="GapFrom">Where the run of slots that are not creatures begins.</param>
/// <param name="GapLength">How many of them there are.</param>
/// <param name="GapName">What they are all called, which is what made the run findable.</param>
/// <param name="Mapped">How many species came out with an entry.</param>
/// <param name="Unreachable">
/// How many species have a number the table is too short to reach. Nought is the answer that
/// says the arithmetic and the file agree.
/// </param>
public sealed record CryIndexResult(
    int GapFrom,
    int GapLength,
    string GapName,
    int Mapped,
    int Unreachable)
{
    /// <summary>True when no run of placeholder slots was found at all.</summary>
    public bool NoGap => GapLength == 0;
}

/// <summary>
/// Which entry of the cry table belongs to which creature.
/// <para>
/// <b>Why this is not simply the species number.</b> A real cartridge names 412 species and
/// carries 388 cries. Those cannot line up, and the twenty-four are not missing from the end
/// — they are a block in the middle of the numbering that carries no creature at all, slots
/// that share one placeholder name. The cry table skips them. So every species after that
/// block sits at an entry that many places earlier, and reading the table by species number
/// would give the wrong noise to more than a hundred creatures and be right about all the
/// early ones, which is the worst way for something to be wrong.
/// </para>
/// <para>
/// The gap is found by what it looks like rather than by number: the longest run of
/// consecutive species sharing a name. Nothing here knows that a cartridge has twenty-five
/// unused slots, or where they are.
/// </para>
/// <para>
/// <b>Modelled, and this is the part still to be confirmed by ear.</b> That the entries run
/// in species order, that the first entry is the first creature rather than the empty slot
/// numbered nought, and that the table skips the gap rather than filling it with silence.
/// Every one of those is consistent with the counts on a real file — 386 creatures outside
/// the gap against 388 entries — and counts are not the same as hearing it.
/// </para>
/// </summary>
public static class CryIndex
{
    /// <summary>
    /// The fewest slots in a row that make a run of placeholders rather than a coincidence.
    /// <b>Modelled.</b>
    /// </summary>
    public const int ShortestGap = 4;

    /// <summary>
    /// Works out the entry each species uses, and says what it worked out from.
    /// </summary>
    /// <param name="names">Every species name, by species number, with nought unused.</param>
    /// <param name="entries">How many entries the cry table has.</param>
    public static (Dictionary<int, int> BySpecies, CryIndexResult Found) Derive(
        IReadOnlyList<string> names, int entries, Action<string>? log = null)
    {
        (int From, int Length) gap = LongestRepeatedRun(names);

        var bySpecies = new Dictionary<int, int>();

        var unreachable = 0;

        // Species nought is not a creature on any cartridge in this family — it is the slot
        // the game uses to mean "nothing" — so it has no cry and is not counted as one that
        // could not be reached.
        for (int species = 1; species < names.Count; species++)
        {
            if (gap.Length > 0 && species >= gap.From && species < gap.From + gap.Length) continue;

            int at = species - 1;

            if (gap.Length > 0 && species >= gap.From + gap.Length) at -= gap.Length;

            if (at >= entries)
            {
                unreachable++;

                continue;
            }

            bySpecies[species] = at;
        }

        var result = new CryIndexResult(
            gap.From,
            gap.Length,
            gap.Length > 0 ? names[gap.From] : string.Empty,
            bySpecies.Count,
            unreachable);

        log?.Invoke(
            gap.Length > 0
                ? $"    {gap.Length} species from {gap.From} share the name \"{names[gap.From]}\" and are skipped"
                : "    no run of placeholder species, so entry n is species n+1 throughout");

        log?.Invoke(
            $"    {bySpecies.Count} species map onto {entries} entries"
            + (unreachable > 0 ? $", and {unreachable} have no entry to map onto" : ", with none left over"));

        return (bySpecies, result);
    }

    /// <summary>
    /// The longest run of consecutive species sharing one name.
    /// <para>
    /// Species nought is skipped: it is not a creature, and on a cartridge whose empty slot
    /// carries the same placeholder as the block it would join the run and shift it by one.
    /// </para>
    /// </summary>
    private static (int From, int Length) LongestRepeatedRun(IReadOnlyList<string> names)
    {
        (int from, int length) best = (0, 0);

        for (int at = 1; at < names.Count;)
        {
            int run = 1;

            while (at + run < names.Count && names[at + run] == names[at]) run++;

            if (run >= ShortestGap && run > best.length) best = (at, run);

            at += run;
        }

        return best;
    }
}
