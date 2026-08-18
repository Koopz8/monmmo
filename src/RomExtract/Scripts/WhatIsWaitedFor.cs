namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// Which routines a script waits for after asking, counted in BYTE POSITIONS.
/// </summary>
/// <remarks>
/// <para>
/// 232 found that <c>0x27</c> — a command with no arguments — sits immediately after a
/// <c>special</c> at 68 of its 98 byte positions, against a chance floor of 2.35%. That says it
/// belongs after a routine call and nothing more.
/// </para>
/// <para>
/// The question worth asking next is not how often it happens but <b>whether it is a property of
/// the ROUTINE or of the call site</b>. A routine asked in seven places with a wait after all
/// seven is saying something about that routine; the same seven waits scattered over seven
/// different routines are saying something about the scripts. The two read identically as a
/// count.
/// </para>
/// </remarks>
public static class WhatIsWaitedFor
{
    /// <param name="Number">The routine.</param>
    /// <param name="Places">How many BYTE POSITIONS ask it.</param>
    /// <param name="Waited">How many of those are followed straight away by a wait.</param>
    public sealed record Routine(int Number, int Places, int Waited)
    {
        /// <summary>Every place that asks it waits for it.</summary>
        public bool AtEveryPlace => Places > 0 && Waited == Places;

        /// <summary>Some do and some do not — the answer that would say it is about the site.</summary>
        public bool AtSomeOnly => Waited > 0 && Waited < Places;

        /// <summary>
        /// Whether this routine can say anything about all-or-nothing at all.
        /// <para>
        /// A routine asked in ONE place is waited for at every place whenever it is waited for at
        /// all, which is not a fact about the routine. Twenty-two of this cartridge's thirty-six
        /// are like that, and counting them in would make the finding out of nothing.
        /// </para>
        /// </summary>
        public bool AsksMoreThanOnce => Places > 1;
    }

    /// <summary>
    /// Every routine that is asked anywhere, with how many places ask it and how many wait.
    /// </summary>
    /// <remarks>
    /// Places, not calls: a block hanging off nineteen maps asks whatever it asks nineteen times
    /// at one address, and counting those separately would make one routine look like nineteen
    /// agreeing sites. This is the same rule as 231's, asked of a different column.
    /// </remarks>
    public static IReadOnlyList<Routine> From(IEnumerable<(int Routine, int At, bool Waited)> calls)
    {
        var places = new Dictionary<int, HashSet<int>>();
        var waited = new Dictionary<int, HashSet<int>>();

        foreach ((int routine, int at, bool waits) in calls)
        {
            if (!places.TryGetValue(routine, out HashSet<int>? seen)) places[routine] = seen = [];

            seen.Add(at);

            if (!waits) continue;

            if (!waited.TryGetValue(routine, out HashSet<int>? waits1)) waited[routine] = waits1 = [];

            waits1.Add(at);
        }

        return
        [
            .. places.Keys.Order().Select(r =>
                new Routine(r, places[r].Count, waited.GetValueOrDefault(r)?.Count ?? 0)),
        ];
    }

    /// <summary>The variable this cartridge's scripts write an argument into before a call.</summary>
    public const int FirstArgument = 0x8004;

    /// <summary>
    /// What the block put in <see cref="FirstArgument"/> immediately before the call at
    /// <paramref name="index"/>, or nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only the unbroken run of setvars touching the call.</b> A <c>setvar 0x8004</c> four
    /// commands earlier with a message in between is a variable that happens to be nearby, and
    /// crediting it would give a routine a selector it was never handed. Same rule
    /// <see cref="SpecialContracts"/> uses to count arguments, asked for the value rather than the
    /// count.
    /// </para>
    /// <para>
    /// A call with no such run gets <see cref="NoSelector"/> and is its own bucket — not folded in
    /// with the ones that pass nought, which is a different thing said a different way.
    /// </para>
    /// </remarks>
    public static int SelectorBefore(IReadOnlyList<ScriptCommand> block, int index)
    {
        for (int i = index - 1; i >= 0 && i >= index - Window; i--)
        {
            if (block[i].Code != SetVar || block[i].Arguments.Length < 4) break;

            if (block[i].Word() == FirstArgument) return block[i].Word(2);
        }

        return NoSelector;
    }

    /// <summary>A call handed no argument in the run before it.</summary>
    public const int NoSelector = -1;

    private const byte SetVar = 0x16;

    private const int Window = 4;

    /// <param name="Number">The routine.</param>
    /// <param name="Selector">What was in <see cref="FirstArgument"/>, or <see cref="NoSelector"/>.</param>
    /// <param name="Places">How many byte positions ask it that way.</param>
    /// <param name="Waited">How many of those wait.</param>
    public sealed record Asking(int Number, int Selector, int Places, int Waited)
    {
        /// <summary>Waited at some places and not others — the answer 236 found nought of.</summary>
        public bool Mixed => Waited > 0 && Waited < Places;

        /// <summary>Asked more than once, so all-or-nothing can say something about it.</summary>
        public bool AskedMoreThanOnce => Places > 1;
    }

    /// <summary>
    /// The same question asked of the ROUTINE AND ITS ARGUMENT rather than of the routine.
    /// </summary>
    /// <remarks>
    /// 235 found one routine — <c>0x194</c> — waited for at 1 of its 34 places and called it the
    /// exception. It is not one operation: 31 of those 34 places set <c>0x8004</c> first, to
    /// eighteen different values. Bucketed by what is actually being asked, the exception is gone.
    /// </remarks>
    public static IReadOnlyList<Asking> ByAsking(
        IEnumerable<(int Routine, int Selector, int At, bool Waited)> calls)
    {
        var places = new Dictionary<(int R, int S), HashSet<int>>();
        var waited = new Dictionary<(int R, int S), HashSet<int>>();

        foreach ((int routine, int selector, int at, bool waits) in calls)
        {
            (int R, int S) key = (routine, selector);

            if (!places.TryGetValue(key, out HashSet<int>? seen)) places[key] = seen = [];

            seen.Add(at);

            if (!waits) continue;

            if (!waited.TryGetValue(key, out HashSet<int>? did)) waited[key] = did = [];

            did.Add(at);
        }

        return
        [
            .. places.Keys
                .OrderBy(k => k.R)
                .ThenBy(k => k.S)
                .Select(k => new Asking(
                    k.R, k.S, places[k].Count, waited.GetValueOrDefault(k)?.Count ?? 0)),
        ];
    }

    /// <summary>
    /// How many askings put in more than one place would be waited for at SOME and not all, if
    /// each place were decided on its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the null for the thing actually observed. "All of them or none of them" is two
    /// outcomes out of <c>2^n</c>, so the interesting number is how often chance would produce the
    /// third — <c>1 - p^n - (1-p)^n</c> — summed over the askings that can show it.
    /// </para>
    /// <para>
    /// Asking it this way round matters: the count of all-waited groups is dominated by the ones
    /// that wait for nothing, and a null built on those is a null about nothing.
    /// </para>
    /// </remarks>
    public static double ExpectedMixed(IEnumerable<Asking> askings, double chance) =>
        askings
            .Where(a => a.AskedMoreThanOnce)
            .Sum(a => 1
                - Math.Pow(chance, a.Places)
                - Math.Pow(1 - chance, a.Places));

    /// <summary>How often a call place is followed by a wait, across everything.</summary>
    public static double Chance(IEnumerable<Routine> routines)
    {
        Routine[] all = [.. routines];

        int places = all.Sum(r => r.Places);

        return places == 0 ? 0 : (double)all.Sum(r => r.Waited) / places;
    }

    /// <summary>
    /// How many routines asked in more than one place would have a wait at EVERY one of them, if
    /// each place were decided on its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The null this milestone is against: waiting is sprinkled per call site at whatever rate it
    /// happens overall, and a routine with all its places waited is a coincidence. Then a routine
    /// asked in <c>n</c> places is all-waited with probability <c>p^n</c>, and this is the sum of
    /// that over the routines that can say anything.
    /// </para>
    /// <para>
    /// <b>Routines asked in one place are left out</b>, and that is the whole rule: they are
    /// all-waited whenever they are waited at all, so including them would put the population's
    /// own rate straight back into the expectation and make the answer meaningless in the
    /// direction that flatters it.
    /// </para>
    /// </remarks>
    public static double ExpectedAtEveryPlace(IEnumerable<Routine> routines, double chance) =>
        routines.Where(r => r.AsksMoreThanOnce).Sum(r => Math.Pow(chance, r.Places));
}
