using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A loop that goes round in a circle, told apart from one that is still opening things.
/// <para>
/// The settle test compares a pass with the one before it, which only ever finds a fixed point.
/// That was enough for as long as everything the run did was one-way. Running the signs broke it:
/// `9.6` is a fifteen-door puzzle whose shared block sets and clears <c>0x0001</c> depending on
/// the answer, so a walk that stands in front of all fifteen every pass flips one flag on and off
/// forever — 234, 233, 234, 233 — and every <c>--say-yes</c> row ran to the twenty-four-pass
/// backstop.
/// </para>
/// </summary>
public sealed class WhereItHasBeenTests
{
    private static long Signature(IEnumerable<int> flags, int party = 1) =>
        WhereItHasBeen.Signature(flags, [15], party, carried: 3, gone: 0, moved: 0);

    /// <summary>
    /// THE DISCRIMINATION: a run that clears one flag and sets another has the same COUNT and is
    /// not the same state. A signature built out of counts would call that a cycle and stop a run
    /// that still had somewhere to go, which is the expensive direction to be wrong in.
    /// </summary>
    [Fact]
    public void TheSameNumberOfFlagsIsNotTheSameState()
    {
        Assert.NotEqual(Signature([1, 2, 3]), Signature([1, 2, 4]));
    }

    /// <summary>And a set has no order, so the same flags in a different one are the same state.</summary>
    [Fact]
    public void OrderIsNotPartOfTheState()
    {
        Assert.Equal(Signature([1, 2, 3]), Signature([3, 1, 2]));
    }

    /// <summary>
    /// Every field the settle test looks at is in the signature — otherwise a pass that changed
    /// only that field reads as a repeat and the run stops with somewhere left to go.
    /// </summary>
    [Fact]
    public void EveryFieldTheSettleTestLooksAtIsInIt()
    {
        long baseline = WhereItHasBeen.Signature([1], [2], party: 3, carried: 4, gone: 5, moved: 6);

        Assert.NotEqual(baseline, WhereItHasBeen.Signature([9], [2], 3, 4, 5, 6));
        Assert.NotEqual(baseline, WhereItHasBeen.Signature([1], [9], 3, 4, 5, 6));
        Assert.NotEqual(baseline, WhereItHasBeen.Signature([1], [2], 9, 4, 5, 6));
        Assert.NotEqual(baseline, WhereItHasBeen.Signature([1], [2], 3, 9, 5, 6));
        Assert.NotEqual(baseline, WhereItHasBeen.Signature([1], [2], 3, 4, 9, 6));
        Assert.NotEqual(baseline, WhereItHasBeen.Signature([1], [2], 3, 4, 5, 9));
    }

    /// <summary>
    /// A state is new the first time and not the second — and a THIRD state in between does not
    /// make the first one new again, which is the whole difference between this and comparing
    /// with the previous pass.
    /// </summary>
    [Fact]
    public void AStateIsNewOnceAndOnlyOnce()
    {
        var been = new WhereItHasBeen();

        long on = Signature([1, 2]);
        long off = Signature([1]);

        Assert.False(been.SeenBefore(on));
        Assert.False(been.SeenBefore(off));

        // The two-cycle: back to a state it has been in, with a different one in between.
        Assert.True(been.SeenBefore(on));

        Assert.Equal(2, been.Count);
    }

    /// <summary>
    /// And the flags go in by their CONTENTS: two flags one apart must not fold to nearly the
    /// same thing, or a set differing by one reads as the same state.
    /// </summary>
    [Fact]
    public void FlagsOneApartAreDifferentStates()
    {
        Assert.NotEqual(Signature([0x0001]), Signature([0x0002]));
        Assert.NotEqual(Signature([0x0001, 0x0004]), Signature([0x0002, 0x0003]));
    }
}
