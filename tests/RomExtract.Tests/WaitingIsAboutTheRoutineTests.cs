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

    // ------------------------------------------------- and what was actually asked

    private static ScriptCommand SetVar(int at, int variable, int value) =>
        new(at, 0x16,
            [(byte)(variable & 0xFF), (byte)(variable >> 8), (byte)(value & 0xFF), (byte)(value >> 8)]);

    private static ScriptCommand Special(int at, int routine) =>
        new(at, 0x25, [(byte)(routine & 0xFF), (byte)(routine >> 8)]);

    private static ScriptCommand Message(int at) => new(at, 0x67, [0, 0, 0, 0]);

    /// <summary>
    /// <b>NOTHING BETWEEN A VALUE AND A CALL CLEARS THE SLOT (298).</b> This asserted the opposite
    /// until 298 — <em>only the unbroken run of setvars touching the call</em>, on the stated
    /// grounds that a <c>setvar 0x8004</c> with something in between is a variable that happens to
    /// be nearby. It is not: nothing here empties a variable, and 295 and 296 replaced that rule
    /// with two read off the script — the value belongs to the first call after it, and a slot
    /// something else READ is spent.
    /// <para>
    /// The cartridge settles it. <c>2.1</c> at <c>0x1C510D</c> is
    /// <c>setvar 0x8004, 1 ; setvar 0x8005, 2 ; copyvar 0x8006, 0x4003 ; special 0x0194</c> — the
    /// old rule stopped dead at the <c>copyvar</c> and reported a call handed nothing, three
    /// commands after it was handed a one. Thirteen places in the game are that shape.
    /// </para>
    /// </summary>
    [Fact]
    public void SomethingInTheWayThatDoesNotTouchTheSlotDoesNotClearIt()
    {
        ScriptCommand[] touching =
        [
            SetVar(0x100, 0x8004, 9), SetVar(0x105, 0x8005, 1), Special(0x10A, 0x194),
        ];

        Assert.Equal(9, WhatIsWaitedFor.SelectorBefore(touching, 2));

        ScriptCommand[] across =
        [
            SetVar(0x100, 0x8004, 9), Message(0x105), SetVar(0x10A, 0x8005, 1), Special(0x10F, 0x194),
        ];

        Assert.Equal(9, WhatIsWaitedFor.SelectorBefore(across, 3));

        // And the reading it replaced, kept so the size of the correction stays measurable:
        // it stops at the message and reports the call as handed nothing.
        Assert.Equal(9, WhatIsWaitedFor.TheCrudeReading(touching, 2));
        Assert.Equal(WhatIsWaitedFor.NoSelector, WhatIsWaitedFor.TheCrudeReading(across, 3));
    }

    /// <summary>
    /// <b>And a slot something else READ in between IS spent</b> — the rule that replaced the one
    /// above, reachable through this caller as well as through <c>SpecialCalls</c>. Without it the
    /// looser walk would credit values the old rule was right to refuse, which is the direction
    /// the change had to be checked in.
    /// </summary>
    [Fact]
    public void ASlotSomethingElseReadInBetweenIsStillSpent()
    {
        // setvar 0x8004, 9 ; copyvar 0x4001, 0x8004 ; special — the copy takes the value.
        ScriptCommand[] spent =
        [
            SetVar(0x100, 0x8004, 9),
            new(0x105, 0x19, [0x01, 0x40, 0x04, 0x80]),
            Special(0x10A, 0x194),
        ];

        Assert.Equal(WhatIsWaitedFor.NoSelector, WhatIsWaitedFor.SelectorBefore(spent, 2));
    }

    /// <summary>
    /// The nearest one wins when the run sets it twice, and a call with nothing in front of it is
    /// handed nothing — which is a bucket of its own and not the same as being handed nought.
    /// </summary>
    [Fact]
    public void TheNearestOneWinsAndNothingIsNotNought()
    {
        ScriptCommand[] twice =
        [
            SetVar(0x100, 0x8004, 9), SetVar(0x105, 0x8004, 2), Special(0x10A, 0x194),
        ];

        Assert.Equal(2, WhatIsWaitedFor.SelectorBefore(twice, 2));

        Assert.Equal(WhatIsWaitedFor.NoSelector, WhatIsWaitedFor.SelectorBefore([Special(0x100, 0x194)], 0));
        Assert.NotEqual(0, WhatIsWaitedFor.NoSelector);
    }

    /// <summary>
    /// THE DISCRIMINATION for the buckets: one routine asked two ways is TWO askings, and a
    /// reading that bucketed by routine alone gets exactly what 235 got — a routine that looks
    /// waited-for at some places and not others.
    /// </summary>
    [Fact]
    public void OneRoutineAskedTwoWaysIsTwoAskings()
    {
        IReadOnlyList<WhatIsWaitedFor.Asking> askings = WhatIsWaitedFor.ByAsking(
        [
            (0x194, 2, 0x1000, true),
            (0x194, 19, 0x2000, false),
            (0x194, 19, 0x3000, false),
        ]);

        Assert.Equal(2, askings.Count);

        WhatIsWaitedFor.Asking waited = askings.Single(a => a.Selector == 2);
        WhatIsWaitedFor.Asking not = askings.Single(a => a.Selector == 19);

        Assert.Equal(1, waited.Places);
        Assert.Equal(1, waited.Waited);
        Assert.False(waited.Mixed);

        Assert.Equal(2, not.Places);
        Assert.Equal(0, not.Waited);
        Assert.False(not.Mixed);

        // And by routine alone the same calls read as the exception 235 reported.
        WhatIsWaitedFor.Routine asOne = WhatIsWaitedFor.From(
            [(0x194, 0x1000, true), (0x194, 0x2000, false), (0x194, 0x3000, false)]).Single();

        Assert.True(asOne.AtSomeOnly);
    }

    /// <summary>An asking waited at some of its places and not others is the thing being counted.</summary>
    [Fact]
    public void MixedIsSomeButNotAll()
    {
        WhatIsWaitedFor.Asking mixed = WhatIsWaitedFor.ByAsking(
            [(0x194, 2, 0x1000, true), (0x194, 2, 0x2000, false)]).Single();

        Assert.True(mixed.Mixed);
        Assert.True(mixed.AskedMoreThanOnce);
    }

    /// <summary>
    /// The null is the chance of the MIXED outcome — one minus all and minus none — because
    /// "all of them or none of them" is two outcomes of two to the n and the count of all-waited
    /// groups is dominated by the ones that wait for nothing.
    /// </summary>
    /// <remarks>
    /// What this test cannot do: an asking with ONE place contributes
    /// <c>1 - p - (1 - p) = 0</c> whatever p is, so excluding them changes no answer and no
    /// fixture can catch a reading that keeps them. The filter is a statement of intent. Written
    /// down rather than left to be found.
    /// </remarks>
    [Fact]
    public void TheNullIsTheChanceOfTheMixedOutcome()
    {
        WhatIsWaitedFor.Asking[] two = [new(0x194, 2, Places: 2, Waited: 0)];

        // 1 - 0.25 - 0.25
        Assert.Equal(0.5, WhatIsWaitedFor.ExpectedMixed(two, 0.5), 6);

        WhatIsWaitedFor.Asking[] three = [new(0x194, 2, Places: 3, Waited: 0)];

        // 1 - 0.125 - 0.125
        Assert.Equal(0.75, WhatIsWaitedFor.ExpectedMixed(three, 0.5), 6);

        Assert.Equal(0, WhatIsWaitedFor.ExpectedMixed([new(0x194, 2, 1, 1)], 0.5), 6);
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
