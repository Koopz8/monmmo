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
/// <summary>How a population is cut into groups (277).</summary>
public enum Cut
{
    /// <summary>Runs of neighbours — the right null for a population that is itself a run.</summary>
    Consecutive,

    /// <summary>Every n-th item — the right null for a population that is a scatter.</summary>
    Interleaved,
}

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
    /// The population cut into consecutive disjoint groups of <paramref name="howMany"/> blocks,
    /// each tallied — the one place a group is ever made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Consecutive rather than drawn at random</b>, for the reason 269 gave about rotation
    /// offsets: a control that cannot be reproduced from the file alone is a control nobody can
    /// check, and this project has no source of randomness it is willing to put in a measurement.
    /// Consecutive groups are, if anything, the conservative choice — blocks near each other in
    /// the file are more alike, so a group of neighbours is FARTHER from the whole than a
    /// scattered sample would be, and any band taken off them is wider than the true one.
    /// </para>
    /// <para>
    /// <b>That argument is about the FILE and it is only true if the caller hands the population
    /// over in file order</b> (275). A list that came out of a <c>HashSet</c> is in whatever order
    /// the set happened to enumerate, and neighbours in it are not neighbours in the cartridge —
    /// the groups are then a scatter and the band is NARROWER than the documentation claims, which
    /// is the unsafe direction. <c>InFileOrder</c> is how a caller says so, and
    /// <c>--the-ruler</c> prints the band both ways so the size of that choice is visible.
    /// </para>
    /// <para>
    /// <b>Four questions are asked of these groups and they share <c>Cuts</c></b> — a band
    /// (<c>SamplingBand</c>), the bound (<c>BoundPerGroup</c>), a group against the REST of its
    /// own population (<c>AgainstTheRest</c>) and a group against another population entirely
    /// (<c>AgainstAnother</c>). 258's fault was a second copy of a walk written for a second
    /// question, which left the suite guarding the other copy.
    /// </para>
    /// </remarks>
    public static IEnumerable<HowOftenEachCommand> Groups(
        Rom rom, IReadOnlyList<uint> population, int howMany, Cut how = Cut.Consecutive) =>
        Cuts(population.Count, howMany, how).Select(g => In(rom, g.Select(i => population[i])));

    /// <summary>Where each consecutive disjoint group starts — the one place a group is cut.</summary>
    public static IEnumerable<int> Cuts(IReadOnlyList<uint> population, int howMany) =>
        Cuts(population.Count, howMany);

    /// <summary>
    /// The same cut, of a count rather than of a list — because a BAND gets cut too (277).
    /// </summary>
    /// <remarks>
    /// A rate is a number like any other and 276 quoted two of them with nothing under either. The
    /// band on a rate is the rate computed over consecutive blocks of groups, which is this loop
    /// applied to a list of distances instead of a list of blocks. One loop, five questions.
    /// </remarks>
    public static IEnumerable<int> Cuts(int inAll, int howMany) =>
        Cuts(inAll, howMany, Cut.Consecutive).Select(g => g[0]);

    /// <summary>
    /// Which items fall in each group, cut CONSECUTIVELY or INTERLEAVED — the one place a group is
    /// ever decided.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Consecutive is only the conservative choice if the thing being read is contiguous</b>
    /// (277). 273 argued for consecutive groups because neighbours in the cartridge are alike, so a
    /// group of them is farther from the whole and any band off them is wider than the truth. True
    /// — and a wider null is only conservative when the population being READ is itself a run of
    /// neighbours. The 38 unnamed boundary sites are scattered over sixteen megabytes, from
    /// <c>0x028514</c> to <c>0xEA7A8F</c>; against a null made of runs, the null carries the
    /// file's regional structure and the reading carries none of it, and everything comes back
    /// unreadable.
    /// </para>
    /// <para>
    /// Interleaved is every n-th item, so each group is a scatter across the whole population. It
    /// is exactly as reproducible from the file as consecutive is — no randomness — and it is the
    /// matching null for a scattered population. <b>Both are printed wherever this is used, and
    /// the difference between them is how much of a population's spread is regional structure
    /// rather than sampling noise.</b>
    /// </para>
    /// </remarks>
    public static IEnumerable<IReadOnlyList<int>> Cuts(int inAll, int howMany, Cut how)
    {
        // Nought would make the loop below no loop at all. A sample LARGER than the population
        // needs no check: the group count comes out nought, which a break aimed at the removed
        // check proved by coming back green (219).
        if (howMany <= 0) yield break;

        int groups = inAll / howMany;

        for (var g = 0; g < groups; g++)
        {
            yield return how == Cut.Consecutive
                ? [.. Enumerable.Range(g * howMany, howMany)]
                : [.. Enumerable.Range(0, howMany).Select(i => g + (i * groups))];
        }
    }

    /// <summary>
    /// How many of <paramref name="slices"/> equal slices of <paramref name="from"/>..
    /// <paramref name="to"/> hold at least one of <paramref name="members"/> — how spread over the
    /// file a population is.
    /// </summary>
    /// <remarks>
    /// <b>The measurement the cut is chosen by</b> (278). 277 gave this project two ways to cut a
    /// null and left which one to use as a judgement at each call site, which is a knob that
    /// changes conclusions and fails no test. A run of neighbours lands in one slice; a scatter
    /// lands in many; and the population being READ lands wherever it lands. The span is passed in
    /// rather than taken from the reference, because a reference that occupies a tenth of the file
    /// would otherwise rate everything outside it as one slice — which is how the first version of
    /// this read 38 sites spanning fourteen megabytes as touching THREE.
    /// </remarks>
    public static int Touches(IEnumerable<uint> members, uint from, uint to, int slices)
    {
        if (slices <= 0 || to <= from) return 0;

        return members
            .Select(
                at => at < from
                    ? 0
                    : (int)Math.Min(slices - 1, (long)(at - from) * slices / (to - from)))
            .Distinct()
            .Count();
    }

    /// <summary>
    /// Which cut matches a population being read, decided by the two KNOWN rows rather than by
    /// hand — and able to answer NEITHER.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A consecutive group of the reference and an interleaved group of the same size are what the
    /// two answers look like, measured on this cartridge rather than argued about (262: an
    /// instrument whose known rows are in its own output controls itself on every run). The
    /// population being read is scored the same way over the same slices.
    /// </para>
    /// <para>
    /// <b>And it can answer that the question is not askable</b>, which is what it does for the
    /// 38. The cut is about how a sample of the REFERENCE should be shaped, so it can only be
    /// measured off members of the read population that lie inside the reference's own span. Three
    /// of the 38 do. With the other thirty-five outside it there is nothing to measure the shape
    /// against, and the cut is MODELLED — 277 chose it by reasoning, and reasoning is what it
    /// stays. <c>Inside</c> is how a caller finds that out.
    /// </para>
    /// <para>
    /// A note on what the file-spread does NOT show. The null is "if these were real script", and
    /// this cartridge keeps all the script it is known to hold in 2.5% of the file — so a sample
    /// of real script is region-confined by necessity, and the read population being spread over
    /// ninety per cent of the file is a property of the ALTERNATIVE, not a defect in the null.
    /// </para>
    /// </remarks>
    public static (Cut Which, int Read, int Reference, int Inside, IReadOnlyList<int> InRuns,
        IReadOnlyList<int> Scattered) WhichCut(
        IReadOnlyList<uint> reference, IReadOnlyList<uint> read)
    {
        if (reference.Count == 0 || read.Count == 0)
        {
            return (Cut.Consecutive, 0, 0, 0, [], []);
        }

        int slices = read.Count;

        uint from = Math.Min(reference.Min(), read.Min());
        uint to = Math.Max(reference.Max(), read.Max());

        int Of(IEnumerable<int> group) =>
            Touches(group.Select(i => reference[i]), from, to, slices);

        IReadOnlyList<int> runs =
            [.. Cuts(reference.Count, read.Count, Cut.Consecutive).Select(Of).Order()];

        IReadOnlyList<int> scattered =
            [.. Cuts(reference.Count, read.Count, Cut.Interleaved).Select(Of).Order()];

        int here = Touches(read, from, to, slices);
        int whole = Touches(reference, from, to, slices);

        uint own = reference.Min();
        uint upTo = reference.Max();

        int inside = read.Count(at => at >= own && at <= upTo);

        if (runs.Count == 0 || scattered.Count == 0)
        {
            return (Cut.Consecutive, here, whole, inside, runs, scattered);
        }

        // Measured on the members that are INSIDE the reference's span, because those are the only
        // ones whose shape can be compared with a group cut from it.
        int fits = Touches(read.Where(at => at >= own && at <= upTo), from, to, slices);

        double toRuns = Math.Abs(fits - runs.Average());
        double toScatter = Math.Abs(fits - scattered.Average());

        return (
            toScatter <= toRuns ? Cut.Interleaved : Cut.Consecutive,
            here,
            whole,
            inside,
            runs,
            scattered);
    }

    /// <summary>A population in the order the cartridge holds it, so a group is neighbours.</summary>
    public static IReadOnlyList<uint> InFileOrder(IEnumerable<uint> population) =>
        [.. population.Order()];

    /// <summary>
    /// How far a sample of <paramref name="howMany"/> blocks drawn from one population sits from
    /// that whole population — the sampling band for a count that small.
    /// </summary>
    /// <remarks>
    /// <b>A distance measured on few blocks is inflated by sampling noise alone</b>, and a
    /// population of thirty-odd blocks compared against one of hundreds cannot be read without
    /// knowing by how much. This gives each consecutive group's distance from the whole.
    /// </remarks>
    public static IReadOnlyList<double> SamplingBand(
        Rom rom, IReadOnlyList<uint> population, int howMany, Cut how = Cut.Consecutive)
    {
        HowOftenEachCommand whole = In(rom, population);

        return [.. Groups(rom, population, howMany, how).Select(g => Distance(g, whole)).Order()];
    }

    /// <summary>
    /// Each consecutive group's distance from the REST of its own population — one end of a scale,
    /// measured at the sample size and with nothing compared against itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>SamplingBand</c> scores a group against a whole that CONTAINS it</b> (276), which
    /// pulls every distance down by the share of the whole the group is: 38 blocks of 435 makes
    /// the band about a tenth too tight. That was harmless while the band was only being asked
    /// whether some other population fell inside it, and it is not harmless when the band is the
    /// end of a scale something is read against.
    /// </para>
    /// <para>
    /// The rest is everything on both sides of the cut, so it is the same blocks the whole holds
    /// minus the group and nothing else.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<double> AgainstTheRest(
        Rom rom, IReadOnlyList<uint> population, int howMany, Cut how = Cut.Consecutive) =>
        [
            .. Cuts(population.Count, howMany, how)
                .Select(
                    group => Distance(
                        In(rom, group.Select(i => population[i])),
                        In(
                            rom,
                            Enumerable.Range(0, population.Count)
                                .Where(i => !group.Contains(i))
                                .Select(i => population[i]))))
                .Order(),
        ];

    /// <summary>
    /// Each consecutive group's distance from a DIFFERENT population — the other end of the same
    /// scale, and the shape a population being read is measured in.
    /// </summary>
    /// <remarks>
    /// The thing being read is scored against one whole population, so the end it is read against
    /// has to be scored the same way. <c>AgainstTheRest</c> cannot do it: the two populations are
    /// different bodies of blocks and there is no rest to take.
    /// </remarks>
    public static IReadOnlyList<double> AgainstAnother(
        Rom rom,
        IReadOnlyList<uint> population,
        int howMany,
        HowOftenEachCommand other,
        Cut how = Cut.Consecutive) =>
        [.. Groups(rom, population, howMany, how).Select(g => Distance(g, other)).Order()];

    /// <summary>
    /// The bound asked of every consecutive group of <paramref name="howMany"/> blocks, against a
    /// reference and a junk model that hold none of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is what makes the bound readable</b> (275). <c>HowMuchCouldBeReal</c> on a whole
    /// population is one number with nothing to compare it against, and 273 and 274 both found a
    /// distance whose whole answer was its sample size. Asked group by group it has a spread; run
    /// on a population whose answer is KNOWN — a held-out half of the real thing, which must come
    /// back 1, and a held-out slice of the junk, which must come back 0 — that spread is a ruler
    /// with both ends marked, and the size the ruler can be marked at is a measurement rather
    /// than an argument.
    /// </para>
    /// <para>
    /// The reference must contain none of the blocks being scored, or a group is being compared
    /// against a whole it is part of and is closer to it for that reason alone.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<double> BoundPerGroup(
        Rom rom,
        IReadOnlyList<uint> population,
        int howMany,
        HowOftenEachCommand real,
        HowOftenEachCommand junk,
        Cut how = Cut.Consecutive) =>
        [
            .. Groups(rom, population, howMany, how)
                .Select(g => HowMuchCouldBeReal(g, real, junk))
                .Order(),
        ];

    /// <summary>
    /// The widest sampling band this population can support with at least
    /// <paramref name="leastGroups"/> disjoint groups, and the group size it was taken at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A band of one group is not a band</b>, and asking for a sample as big as the population
    /// gives exactly that. Sampling noise falls as the sample grows, so a band measured at a
    /// SMALLER size than the one in question is an over-estimate of the noise at that size — which
    /// is the conservative direction for anything the band is then handed to.
    /// </para>
    /// </remarks>
    public static (IReadOnlyList<double> Band, int At) WidestBand(
        Rom rom, IReadOnlyList<uint> population, int wanted, int leastGroups = 4)
    {
        int at = Math.Min(wanted, population.Count / leastGroups);

        return at <= 0 ? ([], 0) : (SamplingBand(rom, population, at), at);
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
    /// <para>
    /// <b>AND IT READS NOUGHT ON A POPULATION THAT IS HALF REAL SCRIPT</b> (275). Putting real
    /// script at distance nought from the reference is the fault; <c>BetweenTheEnds</c> is the
    /// version with both ends measured. This is kept because 268 and 274 are quoted off it and a
    /// number nobody can reproduce is worse than a number that is wrong.
    /// </para>
    /// </remarks>
    public static double HowMuchCouldBeReal(
        HowOftenEachCommand mixed, HowOftenEachCommand real, HowOftenEachCommand junk)
    {
        double apart = Distance(junk, real);

        return apart == 0 ? 0 : Math.Max(0, 1 - (Distance(mixed, real) / apart));
    }

    /// <summary>
    /// The same share read between two ends that were both MEASURED, rather than between nought
    /// and the junk's distance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>HowMuchCouldBeReal</c> puts real script at distance nought from the reference and it
    /// is not there</b> (275). The reference is a sample of real script and so is anything being
    /// scored against it, so two halves of the maps' own scripts sit a real distance apart — and
    /// every reading that divides by <c>d(junk, real)</c> alone is measuring off a scale whose top
    /// mark is somewhere it never checked. A held-out half says where that mark is.
    /// </para>
    /// <para>
    /// Nought when the two ends are the wrong way round or on top of each other: a scale with no
    /// length cannot be read, and returning something from one would be inventing it.
    /// </para>
    /// </remarks>
    public static double BetweenTheEnds(double distance, double realEnd, double junkEnd) =>
        junkEnd <= realEnd ? 0 : Math.Clamp((junkEnd - distance) / (junkEnd - realEnd), 0, 1);

    /// <summary>
    /// How often a group of this size lands AT LEAST as far away as the thing being read — the
    /// count-independent version of "inside the band".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A band's top is a MAXIMUM, and a maximum is a property of how many times you looked</b>
    /// (276). Eleven groups of the maps' own flag sites top out at 0.451; a hundred and two groups
    /// of the maps' own scripts reach 0.826, and their top climbs 0.222, 0.236, 0.278, 0.345,
    /// 0.826 as the count goes 4, 11, 25, 50, 102. So "outside the band" is a verdict against a
    /// threshold that moves with the sample count, and 273's reading of the 38 was one.
    /// </para>
    /// <para>
    /// A rate has a denominator — trap 8's whole point — and looking more does not inflate it. It
    /// is the number to quote and the number to compare between two populations.
    /// </para>
    /// </remarks>
    public static double AtLeastAsFar(IReadOnlyList<double> band, double distance) =>
        band.Count == 0 ? double.NaN : (double)band.Count(d => d >= distance) / band.Count;

    /// <summary>
    /// The rate, computed over consecutive blocks of <paramref name="howManyGroups"/> groups — the
    /// band on a rate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>276 quoted 5.9% against 33.0% and put nothing under either</b> (277). A difference of two
    /// rates is a number, and this project's own rule about numbers is that one with no denominator
    /// cannot come back empty (8) and one with no band cannot come back the same (273). The band
    /// here is the rate re-computed on consecutive blocks of the population's own groups: if two
    /// blocks of the SAME kind differ by more than the two populations do, the difference between
    /// the populations is nothing.
    /// </para>
    /// <para>
    /// Consecutive for 273's reason — reproducible from the file, and neighbours are alike, so the
    /// spread is wider than a scattered sample's and the comparison is conservative.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<double> RateBand(
        IReadOnlyList<double> band,
        double distance,
        int howManyGroups,
        Cut how = Cut.Consecutive) =>
        [
            .. Cuts(band.Count, howManyGroups, how)
                .Select(g => AtLeastAsFar([.. g.Select(i => band[i])], distance))
                .Order(),
        ];

    /// <summary>
    /// The same share read between two ends that are BANDS rather than points — every corner of
    /// them, least and most.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An end measured at the sample size is a band and not a number</b> (276). 275 took its
    /// ends off whole populations of hundreds and read a thirty-eight-block row against them,
    /// which is trap 8's shape one level up: the ends were measured on more evidence than the
    /// thing being read. Measured at the same size they have a spread, and the honest reading is
    /// the range that spread admits.
    /// </para>
    /// <para>
    /// <b>A <c>Least</c> of nought is the reading saying it cannot exclude anything</b> — it is
    /// what a corner where the two bands cross produces, and a crossing is exactly a scale with
    /// no length.
    /// </para>
    /// </remarks>
    public static (double Least, double Most) BetweenTheEnds(
        double distance, IReadOnlyList<double> realEnd, IReadOnlyList<double> junkEnd)
    {
        if (realEnd.Count == 0 || junkEnd.Count == 0) return (double.NaN, double.NaN);

        double[] corners =
        [
            BetweenTheEnds(distance, realEnd.Min(), junkEnd.Min()),
            BetweenTheEnds(distance, realEnd.Min(), junkEnd.Max()),
            BetweenTheEnds(distance, realEnd.Max(), junkEnd.Min()),
            BetweenTheEnds(distance, realEnd.Max(), junkEnd.Max()),
        ];

        return (corners.Min(), corners.Max());
    }
}
