namespace PokeMmo.RomExtract.Scripts;

/// <summary>How often each command code turns up in a set of blocks.</summary>
/// <param name="Counts">Code to how many times it was read.</param>
/// <param name="Commands">How many commands in all, so every count has a denominator.</param>
/// <param name="Blocks">How many blocks were read.</param>
public sealed record HowOftenEachCommand(
    IReadOnlyDictionary<byte, int> Counts, int Commands, int Blocks)
{
    public double ShareOf(byte code) =>
        Commands == 0 ? 0 : (double)Counts.GetValueOrDefault(code) / Commands;

    /// <summary>Commands per block, which is the other thing a junk population gets wrong.</summary>
    public double Length => Blocks == 0 ? 0 : (double)Commands / Blocks;
}

/// <summary>
/// What a set of blocks is made of, so "these look like scripts" can be a number.
/// <para>
/// <b>267 counted 6621 blocks no map leads to and could not say what they are.</b> Its own
/// calibration row says they are not behaving like the map scan's scripts, and there are two very
/// different reasons that could be: they are scripts using variables only compiled code writes, or
/// they are not scripts at all.
/// </para>
/// <para>
/// <b>The reversed-image floor cannot answer that</b>, and this is the one place in this project
/// where that control is known to be blind: reversing destroys command boundaries but preserves
/// structure, so a table of text pointers reversed is still a table of pointers, and every entry
/// in it is four bytes indistinguishable from a script's address. The floor measures the accident
/// rate of RANDOM bytes and the file's accidents come from ITS OWN data.
/// </para>
/// <para>
/// What can answer it is the mix. Real script blocks in this cartridge are full of the same two
/// dozen commands in roughly the same proportions; a run of bytes that decodes by luck is full of
/// whatever the bytes happened to be. So: read the blocks, tally the codes, and put the
/// distributions beside each other with a distance on them.
/// </para>
/// </summary>
public static class WhatABlockIsMadeOf
{
    /// <summary>Every command in every block given, tallied by code.</summary>
    public static HowOftenEachCommand In(Rom rom, IEnumerable<uint> blocks)
    {
        var counts = new Dictionary<byte, int>();
        var commands = 0;
        var read = 0;

        foreach (uint block in blocks)
        {
            read++;

            foreach (ScriptCommand command in ScriptReader.Read(rom, block))
            {
                counts[command.Code] = counts.GetValueOrDefault(command.Code) + 1;
                commands++;
            }
        }

        return new HowOftenEachCommand(counts, commands, read);
    }

    /// <summary>
    /// How far apart two command mixes are: nought identical, one with nothing in common.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Half the summed absolute difference of the shares — the distance between two distributions
    /// rather than between two counts, so a population ten times the size of another is not
    /// automatically far from it. That distinction is the whole reason this is a function and not
    /// two columns to be eyeballed.
    /// </para>
    /// <para>
    /// Over EVERY code either one names, not the ones they share. A population that uses six
    /// commands the other never uses is far from it, and an intersection-only distance would call
    /// them close.
    /// </para>
    /// </remarks>
    public static double Distance(HowOftenEachCommand one, HowOftenEachCommand other)
    {
        HashSet<byte> codes = [.. one.Counts.Keys, .. other.Counts.Keys];

        return codes.Sum(c => Math.Abs(one.ShareOf(c) - other.ShareOf(c))) / 2;
    }

    /// <summary>
    /// The largest share of <paramref name="mixed"/> that could be drawn from
    /// <paramref name="real"/> rather than from <paramref name="junk"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A bound rather than an estimate, and it is exact arithmetic.</b> If a population is a
    /// mixture — a share <c>f</c> of the real thing and the rest junk — then its distance from the
    /// real thing is exactly <c>(1 - f)</c> times the junk's distance from it, because total
    /// variation is linear in a mixture. So <c>f = 1 - d(mixed, real) / d(junk, real)</c>, and
    /// there is no fitting and no threshold anywhere in it.
    /// </para>
    /// <para>
    /// <b>It is only as good as the junk model</b>, which is why the caller prints the distance
    /// between the two junk populations as well: if the thing standing in for junk is not what
    /// this population's junk looks like, the bound is about the stand-in.
    /// </para>
    /// <para>
    /// Nought when the mixture is no closer to the real thing than the junk is, negative
    /// clamped away — "less real than junk" is not a share of anything.
    /// </para>
    /// </remarks>
    /// <summary>
    /// How far a sample of <paramref name="howMany"/> blocks drawn from one population sits from
    /// that whole population — the sampling band for a count that small.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A distance measured on few blocks is inflated by sampling noise alone</b>, and a
    /// population of thirty-odd blocks compared against one of hundreds cannot be read without
    /// knowing by how much. This splits the big population into consecutive groups of
    /// <paramref name="howMany"/> and gives each group's distance from the whole.
    /// </para>
    /// <para>
    /// <b>Consecutive rather than drawn at random</b>, for the reason 269 gave about rotation
    /// offsets: a control that cannot be reproduced from the file alone is a control nobody can
    /// check, and this project has no source of randomness it is willing to put in a measurement.
    /// Consecutive groups are, if anything, the conservative choice — blocks near each other in
    /// the file are more alike, so a group of neighbours is FARTHER from the whole than a
    /// scattered sample would be, and the band this returns is wider than the true one.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<double> SamplingBand(
        Rom rom, IReadOnlyList<uint> population, int howMany)
    {
        // Nought would make the step below no step at all. A sample LARGER than the population
        // needs no check: the loop's own condition never admits a group, which a break aimed at
        // the removed check proved by coming back green (219).
        if (howMany <= 0) return [];

        HowOftenEachCommand whole = In(rom, population);

        var found = new List<double>();

        for (int at = 0; at + howMany <= population.Count; at += howMany)
        {
            found.Add(Distance(In(rom, population.Skip(at).Take(howMany)), whole));
        }

        return [.. found.Order()];
    }

    public static double HowMuchCouldBeReal(
        HowOftenEachCommand mixed, HowOftenEachCommand real, HowOftenEachCommand junk)
    {
        double apart = Distance(junk, real);

        return apart == 0 ? 0 : Math.Max(0, 1 - (Distance(mixed, real) / apart));
    }
}
