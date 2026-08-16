namespace PokeMmo.RomExtract.Sound;

/// <summary>
/// The table of noises, one a species, and what was found where.
/// <para>
/// The counts are the point. A locator that returns an offset and nothing else cannot be
/// checked against anybody's cartridge, and this one is going to be run against a file this
/// machine has never seen.
/// </para>
/// </summary>
/// <param name="Offset">Where the run begins.</param>
/// <param name="Type">
/// The one type byte every entry shares, reported rather than assumed. This project does not
/// know what number a cry entry carries and does not need to — what it needs is that they all
/// carry the <em>same</em> one, which is what a table made by one macro looks like.
/// </param>
/// <param name="Samples">Where each entry's recording is, in the table's own order.</param>
/// <param name="Runs">How many constant-type runs were long enough to be considered at all.</param>
public sealed record CryTableResult(
    int Offset,
    byte Type,
    IReadOnlyList<int> Samples,
    int Runs)
{
    public int Count => Samples.Count;

    /// <summary>Where one species' cry is, or nothing when this table does not reach it.</summary>
    public int? SampleFor(int species) =>
        species >= 0 && species < Samples.Count ? Samples[species] : null;
}

/// <summary>
/// Finding the cries by what the table looks like, without being told what a cry is.
/// <para>
/// A cry is a recording like any other and <see cref="SampleLocator"/> already finds them all.
/// What is missing is which one belongs to which creature, and that is a table — the same
/// twelve bytes an instrument uses, one entry a species, every one of them written by the same
/// macro in the original source.
/// </para>
/// <para>
/// <b>Why this is not the voicegroup walk.</b> <see cref="SoundLocator"/> rejects an entry
/// whose first byte is outside the driver's kind enumeration, and the cry entries on this
/// cartridge are not in it — they were invisible to it, which is why the sound tree found
/// several hundred recordings and could not say which creature made any of them.
/// </para>
/// <para>
/// <b>What is read and what is modelled.</b> Read: that a run of twelve-byte entries all
/// carrying one type byte and all pointing at confirmed recordings is there, where it is, and
/// how long. Modelled: that the longest such run is the cry table, and that entry <i>n</i> is
/// species <i>n</i>. Both of those are decisions, both are checkable against a real file by
/// running the dump, and neither is hidden — the type byte and the count come back in the
/// result so they can be compared against the number of species the same cartridge names.
/// </para>
/// </summary>
public static class CryTableLocator
{
    /// <summary>
    /// The fewest entries a run needs before it is worth considering. <b>Modelled.</b>
    /// <para>
    /// Far above what happens by accident and far below any cartridge's species count, so it
    /// separates the two without being close to either.
    /// </para>
    /// </summary>
    public const int ShortestTable = 32;

    /// <summary>
    /// The whole search. Nothing is hardcoded; what came back is the return value.
    /// </summary>
    public static CryTableResult? Locate(
        Rom rom, IReadOnlyList<SampleRecord> samples, Action<string>? log = null)
    {
        var sampleAt = samples.Select(s => s.Offset).ToHashSet();

        (int Offset, byte Type, List<int> Samples) best = (-1, 0, []);

        var runs = 0;

        for (int offset = 0; offset + InstrumentRecord.SizeBytes <= rom.Length;)
        {
            if (PointedSample(rom, offset, sampleAt) is not { } first)
            {
                offset += 4;

                continue;
            }

            byte type = rom.ReadU8(offset);

            List<int> run = [first];

            int at = offset + InstrumentRecord.SizeBytes;

            // Every entry in one table was written by one macro, so the type byte does not
            // change part way through. That is the condition doing the work here: it is what
            // stops a cry table running on into the voicegroup that happens to follow it.
            while (at + InstrumentRecord.SizeBytes <= rom.Length
                   && rom.ReadU8(at) == type
                   && PointedSample(rom, at, sampleAt) is { } next)
            {
                run.Add(next);

                at += InstrumentRecord.SizeBytes;
            }

            if (run.Count >= ShortestTable)
            {
                runs++;

                // The longest wins, which is the same rule the song table uses and for the
                // same reason: a cartridge has one of these and a great many things that
                // look like its first entry.
                if (run.Count > best.Samples.Count) best = (offset, type, run);
            }

            offset = Math.Max(offset + 4, at);
        }

        if (best.Offset < 0)
        {
            log?.Invoke($"  no cry table — no run of {ShortestTable} or more entries sharing a type byte");

            return null;
        }

        log?.Invoke(
            $"  cry table at 0x{best.Offset:X6} with {best.Samples.Count} entries, "
            + $"type byte 0x{best.Type:X2}");

        if (runs > 1) log?.Invoke($"    {runs - 1} other run(s) were long enough to be considered");

        return new CryTableResult(best.Offset, best.Type, best.Samples, runs);
    }

    /// <summary>
    /// Where this entry's recording is, if these twelve bytes name one this build confirmed.
    /// <para>
    /// The load-bearing condition, the same one the voicegroup walk leans on: it is what turns
    /// twelve bytes that could be anything into twelve bytes that name something real.
    /// </para>
    /// </summary>
    private static int? PointedSample(Rom rom, int offset, HashSet<int> sampleAt)
    {
        if (offset + InstrumentRecord.SizeBytes > rom.Length) return null;

        if (rom.ToOffsetOrNull(rom.ReadU32(offset + 4)) is not { } at) return null;

        return sampleAt.Contains(at) ? at : null;
    }
}
