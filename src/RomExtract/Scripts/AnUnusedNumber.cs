namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// The nudge for a three-byte sweep (272).
/// <para>
/// <see cref="EverywhereInTheImage.Moves"/> and <see cref="EverywhereInTheImage.AsksWhoKnows"/>
/// scan for a PATTERN — a command byte and a halfword — rather than following a pointer, so
/// 269's "aim it a few bytes off" does not translate, and 269 said what does: ask the same sweep
/// for a number the cartridge does not use. A flag id nothing names, nothing gates and a new
/// game does not set has every site the sweep finds for it by accident, with the same three
/// bytes' worth of luck as the real one.
/// </para>
/// <para>
/// <b>The same high byte, on purpose.</b> Byte frequencies are not uniform in this file — nought
/// is everywhere and <c>0x08</c> is in every pointer — so the accident rate of <c>29 LL HH</c>
/// depends on <c>HH</c> as much as on <c>LL</c>, and a floor drawn from the other end of the
/// number line would be a floor for a different pattern. The nearest unused ids above and below
/// keep the high byte and vary the low one.
/// </para>
/// </summary>
public static class AnUnusedNumber
{
    /// <summary>What the sweep finds for one id: how many sites, how many of them read on.</summary>
    public sealed record Found(int Number, int Sites, int ReadsAsScript);

    /// <summary>The floor for one number: the same sweep over its nearest unused neighbours.</summary>
    /// <param name="Over">How many neighbours were asked — the denominator.</param>
    public sealed record Floor(int Number, IReadOnlyList<Found> Neighbours)
    {
        public int Over => Neighbours.Count;

        public int MedianSites => Median(Neighbours.Select(n => n.Sites));
        public int MaxSites => Neighbours.Count == 0 ? 0 : Neighbours.Max(n => n.Sites);

        /// <summary>Which neighbour the most belongs to — an outlier is a name or it is nothing.</summary>
        public int MaxSitesAt => Neighbours.Count == 0 ? 0 : Neighbours.MaxBy(n => n.Sites)!.Number;
        public int MedianReads => Median(Neighbours.Select(n => n.ReadsAsScript));
        public int MaxReads => Neighbours.Count == 0 ? 0 : Neighbours.Max(n => n.ReadsAsScript);

        private static int Median(IEnumerable<int> of)
        {
            int[] sorted = [.. of.Order()];

            return sorted.Length == 0 ? 0 : sorted[sorted.Length / 2];
        }
    }

    /// <summary>
    /// The nearest ids to <paramref name="number"/> with the same high byte that
    /// <paramref name="used"/> says nothing uses — half below, half above, as far as the byte
    /// allows.
    /// </summary>
    public static IReadOnlyList<int> Neighbours(int number, Func<int, bool> used, int howMany = 16)
    {
        int high = number & ~0xFF;

        var found = new List<int>();

        for (var step = 1; step < 0x100 && found.Count < howMany; step++)
        {
            int below = number - step;
            int above = number + step;

            if (below >= high && !used(below)) found.Add(below);
            if (found.Count < howMany && above <= high + 0xFF && !used(above)) found.Add(above);
        }

        return [.. found.Order()];
    }

    /// <summary>
    /// The flag sweep's floor for one flag: <see cref="EverywhereInTheImage.Moves"/> asked of its
    /// unused neighbours.
    /// </summary>
    public static Floor ForAFlag(Rom rom, int flag, Func<int, bool> used, int howMany = 16) =>
        new(flag,
        [
            .. Neighbours(flag, used, howMany).Select(n =>
            {
                IReadOnlyList<FlagSite> sites = EverywhereInTheImage.Moves(rom, n);

                return new Found(n, sites.Count, sites.Count(s => s.ReadsAsAScript));
            }),
        ]);

    // ---------------------------------------------------------------- a bound's nudge (284)

    /// <summary>One window of ids, and what a sweep found in it.</summary>
    /// <param name="First">The lowest id in the window.</param>
    /// <param name="Last">The highest.</param>
    public sealed record Window(int First, int Last)
    {
        public int Ids => Last - First + 1;

        /// <summary>The high bytes this window spans, which is what makes floors comparable.</summary>
        public IReadOnlyList<int> HighBytes =>
            [.. Enumerable.Range(First >> 8, (Last >> 8) - (First >> 8) + 1)];

        public override string ToString() => $"0x{First:X4}..0x{Last:X4}";
    }

    /// <summary>
    /// Windows of the same width as <c>first..last</c>, laid end to end ABOVE it — the nudge for
    /// a sweep that takes a bound rather than an id (284).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A sweep filtered on <c>1..355</c> cannot be asked for an unused id, because every id in
    /// the range is used; the thing that can be moved is the range. Above rather than around,
    /// because below <c>1</c> there is one id and it is <c>0</c>.
    /// </para>
    /// <para>
    /// <b>These windows do NOT share the real one's high bytes</b> and so they are a floor for a
    /// different pattern — read them for the spread and read
    /// <see cref="SameHighByteAbove"/> for the answer. Printing both is the whole point: the
    /// distance between them is the size of the high-byte effect, measured rather than assumed.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Window> WindowsAbove(
        int first, int last, int howMany, int ceiling = 0xFFFF)
    {
        int width = last - first + 1;

        var windows = new List<Window>();

        for (int at = last + 1; at + width - 1 <= ceiling && windows.Count < howMany; at += width)
            windows.Add(new Window(at, at + width - 1));

        return windows;
    }

    /// <summary>
    /// The ids above <paramref name="last"/> that share its high byte — the one floor for a bound
    /// that is matched on the byte the accident rate actually depends on (284).
    /// </summary>
    /// <remarks>
    /// Nought when <paramref name="last"/> is the top of its high byte, which is the honest answer
    /// and not an error: a cartridge whose last move id ends a page affords no matched floor at
    /// all, and saying so beats quoting an unmatched one.
    /// </remarks>
    public static Window? SameHighByteAbove(int last)
    {
        int top = last | 0xFF;

        return last >= top ? null : new Window(last + 1, top);
    }

    /// <summary>
    /// The variable sweep's floor for one variable: <see cref="EverywhereInTheImage.Writes"/> asked
    /// of its unused neighbours — all four writing commands, the same as the reading.
    /// </summary>
    public static Floor ForAVariable(Rom rom, int variable, Func<int, bool> used, int howMany = 16) =>
        new(variable,
        [
            .. Neighbours(variable, used, howMany).Select(n =>
            {
                IReadOnlyList<VariableSite> sites = EverywhereInTheImage.Writes(rom, n);

                return new Found(n, sites.Count, sites.Count(s => s.ReadsAsAScript));
            }),
        ]);
}
