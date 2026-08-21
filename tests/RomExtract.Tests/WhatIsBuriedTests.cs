using PokeMmo.Core.World;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Maps;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The four bytes a buried item keeps where every other sign keeps a script pointer.
/// <para>
/// <b>183 of this cartridge's 702 signs are the buried kind</b> and nothing had read their word.
/// 239 established that following it as a pointer is wrong; this reads it. The third byte is 183
/// distinct values in 0..190 — an index — which means the flag that remembers a picked-up item
/// is a base plus that index, and the base is in compiled code. That is exactly the blind spot
/// 246 printed and could not close.
/// </para>
/// </summary>
public sealed class WhatIsBuriedTests
{
    private const uint Events = Rom.BaseAddress + 0x100;

    private const int SignTable = 0x200;

    /// <summary>
    /// An image with an events record whose fourth list is a sign table of the given records.
    /// </summary>
    /// <remarks>
    /// Built rather than described: four count bytes, then four pointers, and the signs are the
    /// fourth of each. Getting that wrong here would make every test below pass for the wrong
    /// reason, so the shape is the cartridge's own and <see cref="EventLayout"/> reads it.
    /// </remarks>
    private static Rom Image(params byte[][] records)
    {
        var data = new byte[0x1000];

        data[0x100 + EventLayout.Signs] = (byte)records.Length;

        uint table = Rom.BaseAddress + SignTable;

        BitConverter.GetBytes(table).CopyTo(data, 0x100 + EventLayout.PointersOffset + EventLayout.Signs * 4);

        for (var i = 0; i < records.Length; i++)
            records[i].CopyTo(data, SignTable + i * WhatIsBuried.SignSizeBytes);

        return new Rom(data);
    }

    /// <summary>One twelve-byte sign record.</summary>
    private static byte[] Sign(int x, int y, int kind, uint word) =>
    [
        (byte)x, (byte)(x >> 8),
        (byte)y, (byte)(y >> 8),
        0,
        (byte)kind,
        0, 0,
        (byte)word, (byte)(word >> 8), (byte)(word >> 16), (byte)(word >> 24),
    ];

    /// <summary>An item, an index and a count, packed the way the cartridge packs them.</summary>
    private static uint Word(int item, int index, int count) =>
        (uint)(item | (index << 16) | (count << 24));

    // ------------------------------------------------------------------- reading the record

    /// <summary>THE THING: the word splits into an item, an index and a count.</summary>
    [Fact]
    public void TheWordSplitsIntoAnItemAnIndexAndACount()
    {
        Buried one = Assert.Single(
            WhatIsBuried.On(Image(Sign(3, 22, MapSign.HiddenItem, Word(13, 0, 1))), "1.0", Events));

        Assert.Equal((13, 0, 1), (one.Item, one.Third, one.Fourth));
        Assert.Equal((3, 22), (one.X, one.Y));
    }

    /// <summary>
    /// AND THE HALF THAT MAKES THE INDEX CLAIM MEAN ANYTHING: only the buried kind is read.
    /// </summary>
    /// <remarks>
    /// Every other sign keeps a script pointer in those four bytes. Reading one as an item and an
    /// index would put five hundred and nineteen invented indices into a list whose entire claim
    /// is that its indices are distinct — and they would not be, so the claim would collapse for
    /// a reason that has nothing to do with the cartridge.
    /// </remarks>
    [Fact]
    public void OnlyTheBuriedKindIsRead()
    {
        IEnumerable<Buried> found = WhatIsBuried.On(
            Image(
                Sign(3, 22, MapSign.HiddenItem, Word(13, 0, 1)),
                Sign(4, 22, 0, Rom.BaseAddress + 0x300)),
            "1.0",
            Events);

        Assert.Equal([13], [.. found.Select(b => b.Item)]);
    }

    // ------------------------------------------------------------------- what an index is

    /// <summary>A field holding each of 0..N-1 once is an index.</summary>
    [Fact]
    public void EachValueOnceFromNoughtIsAnIndex()
    {
        Assert.True(WhatIsBuried.IsADenseIndex([2, 0, 3, 1]));
    }

    /// <summary>
    /// And repeated values are not one — without this, a field holding the same number a hundred
    /// times reads as an index and the whole argument for a computed flag disappears.
    /// </summary>
    /// <remarks>
    /// <b>The first version of this fixture was <c>[0, 1, 1, 2]</c> and the break came back
    /// green.</b> Four values from nought with a largest of two fails the "largest is one less
    /// than the count" rule as well, so removing the distinctness check changed nothing — the
    /// fixture was caught by the other half and could not tell the two apart. Trap 20 and
    /// fixture-lie 12 at once, and the count was predicted at one before the break ran, which is
    /// the only reason the green was a finding rather than a shrug.
    /// </remarks>
    [Fact]
    public void RepeatedValuesAreNotAnIndex()
    {
        // Four values, nought to three, so every rule but distinctness is satisfied — and one
        // of them is repeated while another is missing.
        Assert.False(WhatIsBuried.IsADenseIndex([0, 1, 1, 3]));
        Assert.False(WhatIsBuried.IsADenseIndex([1, 2, 3]));
    }

    /// <summary>
    /// And the answer is never a bare yes or no: the density is printed, because this cartridge's
    /// own answer is 183 distinct values in 0..190 with eight gaps, which is an index and is not
    /// a dense one.
    /// </summary>
    [Fact]
    public void TheGapCountIsWithinTheRangeTheValuesOccupy()
    {
        WhatIsBuried.HowDense how = WhatIsBuried.Density([4, 6, 7]);

        Assert.Equal((3, 3, 4, 7, 1), (how.Values, how.Distinct, how.Low, how.High, how.Missing));
    }

    // ---------------------------------------------------------------- where a base could live

    /// <summary>THE THING: a run of numbers nothing names is where a computed range could be.</summary>
    [Fact]
    public void ARunNothingNamesIsAWindow()
    {
        IReadOnlyList<WhatIsBuried.Window> gaps = WhatIsBuried.Gaps([0, 5, 20], 20, 4);

        // 1-4 between the nought and the five, and 6-19 between the five and the twenty. Every
        // run of at least four, not the widest one — the caller needs all of them to count.
        Assert.Equal([(1, 4, 4), (6, 14, 19)], [.. gaps.Select(g => (g.From, g.Length, g.To))]);
    }

    /// <summary>
    /// And a gap too narrow for every index is not one — the width is the point of the question.
    /// </summary>
    [Fact]
    public void AGapTooNarrowForEveryIndexIsNotReported()
    {
        Assert.Empty(WhatIsBuried.Gaps([0, 5, 20], 20, 15));
    }

    /// <summary>
    /// And the run that reaches the ceiling counts — the loop ends without meeting a named
    /// number, and a version that only closed a run when it hit one would lose the largest gap
    /// on this cartridge, which is the one that runs to the top.
    /// </summary>
    [Fact]
    public void AGapThatRunsToTheCeilingIsReported()
    {
        IReadOnlyList<WhatIsBuried.Window> gaps = WhatIsBuried.Gaps([0, 1], 10, 4);

        WhatIsBuried.Window one = Assert.Single(gaps);

        Assert.Equal((2, 9, 10), (one.From, one.Length, one.To));
    }

    /// <summary>
    /// And more than one window is a real answer: this instrument is allowed to come back unable
    /// to say, and on this cartridge it does — three gaps, and the load test narrows 14883
    /// candidate bases to 889 against a reversed 84, which picks nothing.
    /// </summary>
    [Fact]
    public void SeveralWindowsAreReportedRatherThanOneChosen()
    {
        Assert.Equal(3, WhatIsBuried.Gaps([0, 5, 10, 30], 30, 4).Count);
    }

    // ------------------------------------------------------------ what is only underground

    private static Buried Thing(string mapId, int item) => new(mapId, 0, 0, 0, (uint)item);

    /// <summary>THE THING: a buried kind no script names has no other source.</summary>
    [Fact]
    public void AnItemNoScriptNamesIsOnlyBuried()
    {
        Assert.Equal([13], [.. WhatIsBuried.OnlyBuried([Thing("1.0", 13)], [])]);
    }

    /// <summary>
    /// And one a script DOES name is not — without this, every buried kind reads as unique to
    /// the ground and the whole list is a restatement of what is buried.
    /// </summary>
    [Fact]
    public void AnItemAScriptNamesIsNotOnlyBuried()
    {
        Assert.Empty(WhatIsBuried.OnlyBuried([Thing("1.0", 13)], [13]));
    }

    /// <summary>
    /// AND ITEM NOUGHT IS NOT AN ITEM: the twelve records that name none of them would otherwise
    /// put the item table's placeholder at the top of a list of things found only underground.
    /// </summary>
    [Fact]
    public void ItemNoughtIsNotAnItem()
    {
        Assert.Empty(WhatIsBuried.OnlyBuried([Thing("10.14", 0)], []));
    }

    /// <summary>
    /// And a kind buried in four places is one kind — the list is of items, and counting places
    /// here would make a common berry look like four separate findings.
    /// </summary>
    [Fact]
    public void AKindBuriedSeveralTimesIsListedOnce()
    {
        Assert.Equal(
            [13],
            [.. WhatIsBuried.OnlyBuried([Thing("1.0", 13), Thing("1.2", 13)], [])]);
    }

    // ------------------------------------------------------------- and what the run misses

    /// <summary>
    /// A buried thing is keyed by its map, because it has no script and there is nothing finer
    /// for a walk to have run.
    /// </summary>
    [Fact]
    public void WhatTheRunNeverStoodOnIsWhatIsNotOnAReachedMap()
    {
        IReadOnlyList<Buried> missed = WhatIsBuried.NeverStoodOn(
            [Thing("1.0", 13), Thing("1.62", 36)], ["1.0"]);

        Assert.Equal(["1.62"], [.. missed.Select(b => b.MapId)]);
    }

    /// <summary>
    /// A PLACEHOLDER IS NOT A NAME. 248's evidence that the first halfword is an item id was
    /// "183 of 183 resolve to a name in the item table's 308 entries", and twelve of the 183
    /// resolve to entry NOUGHT — which this cartridge's table calls <c>????????</c>. Counting a
    /// blank as a hit is how a test that could have failed stops being able to; the honest count
    /// is 171 of 171 and it is a stronger result.
    /// </summary>
    [Fact]
    public void EntryNoughtIsNotAName()
    {
        var names = new Dictionary<int, string> { [0] = "????????", [7] = "POTION" };

        Assert.True(new Buried("1.0", 0, 0, 0, 7).NamesAnItem(names));
        Assert.False(new Buried("1.0", 0, 0, 0, 0).NamesAnItem(names));
    }

    /// <summary>
    /// AND AN ID THE TABLE DOES NOT HOLD IS NOT ONE EITHER, which is the answer that has to be
    /// possible or the test is a machine for saying yes.
    /// </summary>
    [Fact]
    public void AnIdTheTableDoesNotHoldDoesNotResolve()
    {
        Assert.False(
            new Buried("1.0", 0, 0, 0, 999).NamesAnItem(new Dictionary<int, string> { [7] = "POTION" }));
    }
}
