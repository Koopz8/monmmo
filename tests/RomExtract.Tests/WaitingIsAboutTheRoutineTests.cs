using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Whether waiting for a routine is a property of the ROUTINE or of the call site.
/// <para>
/// 232 measured that <c>0x27</c> follows a <c>special</c> at 68 of its 98 byte positions against a
/// 2.35% floor, which says only that it belongs after a routine call. A routine asked in seven
/// places with a wait after all seven says something about that routine; the same seven waits
/// scattered over seven routines say something about the scripts — and <b>the two read identically
/// as a count</b>.
/// </para>
/// <para>
/// Measured: 68 of 936 call places wait, at 36 routines. Twenty-two of the thirty-six are asked in
/// ONE place, where the question cannot be asked. Of the other fourteen, <b>thirteen are waited for
/// at every place that asks them</b>, and sixty-eight of the eighty-two multi-place routines are
/// waited for at none. Under a null of per-site sprinkling at the overall 7.3%, the expected number
/// of multi-place routines waited at every one is <b>0.21</b>.
/// </para>
/// </summary>
public sealed class WaitingIsAboutTheRoutineTests
{
    /// <summary>
    /// THE DISCRIMINATION: one address read twice is one PLACE. A block hanging off two triggers is
    /// decoded twice, and counting the second as another agreeing site would make one routine look
    /// like two.
    /// </summary>
    [Fact]
    public void OneAddressReadTwiceIsOnePlace()
    {
        IReadOnlyList<WhatIsWaitedFor.Routine> found = WhatIsWaitedFor.From(
        [
            (0x09F, 0x1000, true),
            (0x09F, 0x1000, true),
            (0x09F, 0x2000, true),
            (0x020, 0x3000, false),
        ]);

        WhatIsWaitedFor.Routine waited = found.Single(r => r.Number == 0x09F);

        Assert.Equal(2, waited.Places);
        Assert.Equal(2, waited.Waited);
        Assert.True(waited.AtEveryPlace);

        WhatIsWaitedFor.Routine never = found.Single(r => r.Number == 0x020);

        Assert.Equal(1, never.Places);
        Assert.Equal(0, never.Waited);
        Assert.False(never.AtEveryPlace);
    }

    /// <summary>
    /// Every place, some places and no places are three answers and not two — a reading with only
    /// "does it wait" would put the middle one in whichever bucket it was written to prefer.
    /// </summary>
    [Fact]
    public void EverySomeAndNoneAreThreeAnswers()
    {
        IReadOnlyList<WhatIsWaitedFor.Routine> found = WhatIsWaitedFor.From(
        [
            (0x09F, 0x1000, true), (0x09F, 0x2000, true),
            (0x194, 0x3000, true), (0x194, 0x4000, false),
            (0x039, 0x5000, false), (0x039, 0x6000, false),
        ]);

        WhatIsWaitedFor.Routine every = found.Single(r => r.Number == 0x09F);
        WhatIsWaitedFor.Routine some = found.Single(r => r.Number == 0x194);
        WhatIsWaitedFor.Routine none = found.Single(r => r.Number == 0x039);

        Assert.True(every.AtEveryPlace);
        Assert.False(every.AtSomeOnly);

        Assert.False(some.AtEveryPlace);
        Assert.True(some.AtSomeOnly);

        Assert.False(none.AtEveryPlace);
        Assert.False(none.AtSomeOnly);
    }

    /// <summary>
    /// A routine asked in ONE place is waited at every place the moment it is waited at all, which
    /// is not a fact about the routine — twenty-two of this cartridge's thirty-six are like that.
    /// </summary>
    [Fact]
    public void ARoutineAskedOnceCannotSayAnythingAboutAllOrNothing()
    {
        IReadOnlyList<WhatIsWaitedFor.Routine> found = WhatIsWaitedFor.From(
            [(0x0BD, 0x1000, true), (0x09F, 0x2000, true), (0x09F, 0x3000, true)]);

        WhatIsWaitedFor.Routine once = found.Single(r => r.Number == 0x0BD);

        Assert.True(once.AtEveryPlace);
        Assert.False(once.AsksMoreThanOnce);

        Assert.True(found.Single(r => r.Number == 0x09F).AsksMoreThanOnce);
    }

    /// <summary>
    /// THE FLOOR, and the rule it turns on: routines asked once are left OUT of the expectation.
    /// <para>
    /// Leaving them in puts the population's own rate straight back into the answer — a
    /// single-place routine is all-waited with probability p rather than p to the n — and it
    /// moves the number in the direction that flatters the finding, which is the direction to
    /// guard hardest.
    /// </para>
    /// </summary>
    [Fact]
    public void TheExpectationLeavesOutTheRoutinesAskedOnce()
    {
        WhatIsWaitedFor.Routine[] routines =
        [
            new(0x0BD, Places: 1, Waited: 1),
            new(0x09F, Places: 3, Waited: 3),
        ];

        // Only the three-place one counts: 0.5 * 0.5 * 0.5.
        Assert.Equal(0.125, WhatIsWaitedFor.ExpectedAtEveryPlace(routines, 0.5), 6);

        // With nothing asked more than once there is nothing to expect.
        Assert.Equal(0, WhatIsWaitedFor.ExpectedAtEveryPlace([new(0x0BD, 1, 1)], 0.5), 6);
    }

    /// <summary>
    /// And the rate is waited PLACES over all PLACES — over the whole population, including the
    /// routines nothing ever waits for, or the null is built out of the sites that agree with it.
    /// </summary>
    [Fact]
    public void TheRateIsOverEveryPlaceAndNotOnlyTheWaitingOnes()
    {
        WhatIsWaitedFor.Routine[] routines =
        [
            new(0x09F, Places: 2, Waited: 2),
            new(0x039, Places: 8, Waited: 0),
        ];

        Assert.Equal(0.2, WhatIsWaitedFor.Chance(routines), 6);
        Assert.Equal(0, WhatIsWaitedFor.Chance([]), 6);
    }
}
