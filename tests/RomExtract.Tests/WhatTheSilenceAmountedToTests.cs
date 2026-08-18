using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// "396 places call 33 routines it could not answer — every one took the zero arm" is four
/// different findings added together, and for most of them there is no arm.
/// <para>
/// A routine whose answer nobody ever branches on has no arm at all. A routine where nought
/// takes none of the branches costs nothing a wrong answer would not have cost. A routine where
/// nought takes every branch is the run deciding it — and that one is the only one that is a
/// ceiling.
/// </para>
/// <para>
/// <b>The question is what nought DOES, not what it is compared against.</b> The first version
/// of this classified on the compared values alone and the instrument caught it inside a run:
/// the "nought is never the value tested" bucket reported thirty-nine of its six hundred and
/// ninety branches taken by nought. <c>compare 0x800D, 1 ; if LESS</c> is taken by nought and
/// does not test nought. The condition is half the question.
/// </para>
/// </summary>
public sealed class WhatTheSilenceAmountedToTests
{
    private const int NobodyBranches = 0x100;
    private const int NoughtTakesNone = 0x200;
    private const int NoughtTakesAll = 0x300;
    private const int NoughtTakesSome = 0x400;
    private const int NotInTheFileAtAll = 0x500;

    /// <summary>
    /// Compared against something that is not nought, and taken by nought at every site.
    /// <para>
    /// The discrimination this whole file turns on: <c>compare 0x800D, 1 ; if LESS</c>. A rule
    /// that reads the values alone calls this a refusal, and it is the opposite.
    /// </para>
    /// </summary>
    private const int TestedAgainstOneAndTakenByNought = 0x600;

    private static SpecialCalls.Profile Profile(
        int routine, IReadOnlyList<int> tested, int branches, int takenByNought) =>
        new(routine, 1, 1, tested.Count > 0, [], tested, branches, takenByNought);

    private static IReadOnlyList<SpecialCalls.Profile> Profiles() =>
    [
        Profile(NobodyBranches, [], 0, 0),
        Profile(NoughtTakesNone, [2], 4, 0),
        Profile(NoughtTakesAll, [0], 3, 3),
        Profile(NoughtTakesSome, [0, 1], 5, 2),
        Profile(TestedAgainstOneAndTakenByNought, [1], 6, 6),
    ];

    private static SpecialCalls.WhatZeroDid Of(int routine, int asked = 1) =>
        Assert.Single(
            SpecialCalls.ZeroAt(Profiles(), new Dictionary<int, int> { [routine] = asked }));

    /// <summary>
    /// Each of the four, on its own routine — the smallest fixture that can tell them apart.
    /// </summary>
    [Fact]
    public void EachRoutineGetsWhatItsSilenceActuallyDid()
    {
        Assert.Equal(SpecialCalls.ZeroWas.NeverTested, Of(NobodyBranches).Was);
        Assert.Equal(SpecialCalls.ZeroWas.ARefusal, Of(NoughtTakesNone).Was);
        Assert.Equal(SpecialCalls.ZeroWas.AnAssertion, Of(NoughtTakesAll).Was);
        Assert.Equal(SpecialCalls.ZeroWas.Both, Of(NoughtTakesSome).Was);
    }

    /// <summary>
    /// AND THE ONE THAT KILLED THE FIRST VERSION: compared against 1, taken by nought every
    /// time.
    /// <para>
    /// On the cartridge this is <c>0x084</c> — tested against 1 and 2, and nought takes nineteen
    /// of its twenty-one branches. Reading the values alone calls that a refusal.
    /// </para>
    /// </summary>
    [Fact]
    public void ARoutineComparedAgainstOneCanStillBeTakenByNoughtEveryTime()
    {
        SpecialCalls.WhatZeroDid what = Of(TestedAgainstOneAndTakenByNought);

        Assert.Equal(SpecialCalls.ZeroWas.AnAssertion, what.Was);
        Assert.DoesNotContain(0, what.Tested);
        Assert.Equal(what.Branches, what.TakenByZero);
    }

    /// <summary>
    /// A routine the run asked that the map scan never saw is not an assertion by default.
    /// <para>
    /// It has no profile, so nothing is known about what its answer does — and "unknown" has to
    /// read as its own answer rather than being folded into whichever bucket is nearest.
    /// </para>
    /// </summary>
    [Fact]
    public void ARoutineWithNoProfileIsNotClassifiedAsAnythingElse()
    {
        SpecialCalls.WhatZeroDid what = Of(NotInTheFileAtAll);

        Assert.Equal(SpecialCalls.ZeroWas.NeverTested, what.Was);
        Assert.Empty(what.Tested);
        Assert.Equal(0, what.Branches);
    }

    /// <summary>
    /// The branching count comes from the FILE and the asked count from the RUN, and they are
    /// different numbers.
    /// <para>
    /// A routine asked eighty-eight times whose answer is branched on at two sites is a routine
    /// whose silence can matter twice. Counting the eighty-eight as places where the silence
    /// took a branch is the same mistake as counting sites where a bucket wants places.
    /// </para>
    /// </summary>
    [Fact]
    public void TheAskedCountIsTheRunsAndTheBranchCountIsTheFiles()
    {
        SpecialCalls.WhatZeroDid what = Of(NoughtTakesAll, asked: 88);

        Assert.Equal(88, what.Asked);
        Assert.Equal(3, what.Branches);
        Assert.Equal(3, what.TakenByZero);
    }

    /// <summary>
    /// THE ORDER IS BY WHAT NOUGHT DECIDES, NOT BY HOW OFTEN THE RUN ASKED.
    /// <para>
    /// On this cartridge the two are nearly opposite. <c>0x194</c> is asked fifty-four times by
    /// the widest run and nought takes ONE of its eighteen branches; <c>0x083</c> and
    /// <c>0x084</c> are asked once and twice, and between them nought takes thirty-nine of the
    /// mixed bucket's forty-four. Ranking by asks puts the least important first, which is
    /// trap 3 — a count is not a ranking.
    /// </para>
    /// <para>
    /// The fixture makes the two orders disagree on purpose: the routine asked most is the one
    /// whose silence decides least.
    /// </para>
    /// </summary>
    [Fact]
    public void TheOrderFollowsWhatNoughtDecidesAndNotWhatWasAsked()
    {
        IReadOnlyList<SpecialCalls.WhatZeroDid> found = SpecialCalls.ZeroAt(
            Profiles(),
            new Dictionary<int, int>
            {
                [NoughtTakesNone] = 900,
                [NoughtTakesSome] = 3,
                [TestedAgainstOneAndTakenByNought] = 1,
            });

        Assert.Equal(
            new[] { TestedAgainstOneAndTakenByNought, NoughtTakesSome, NoughtTakesNone },
            found.Select(z => z.Routine));

        // And the asks are still carried, so the two numbers can be read against each other.
        Assert.Equal(new[] { 1, 3, 900 }, found.Select(z => z.Asked));
    }

    /// <summary>
    /// AND IT COMES BACK EMPTY when the run answered everything it was asked — the answer that
    /// would mean this ceiling is not one.
    /// </summary>
    [Fact]
    public void ARunThatCouldAnswerEverythingHasNoSilenceToAccountFor()
    {
        Assert.Empty(SpecialCalls.ZeroAt(Profiles(), new Dictionary<int, int>()));
    }
}
