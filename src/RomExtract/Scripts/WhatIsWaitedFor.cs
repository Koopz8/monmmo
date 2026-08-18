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
