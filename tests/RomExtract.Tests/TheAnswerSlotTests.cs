using PokeMmo.Core.Scripts;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a routine this run cannot answer leaves behind, and why there was anything there (308).
/// <para>
/// An unanswerable <c>special</c> writes NOTHING into the slot it would have answered into, so
/// the comparison after it reads whatever is still in there. <i>The run answers nought</i> has
/// been quoted since 214 and is a sentence about a slot nothing had written.
/// </para>
/// <para>
/// Two rules, and both were somewhere a fixture could not reach until this milestone: what makes
/// a leftover matter, and why one is there at all.
/// </para>
/// </summary>
public sealed class TheAnswerSlotTests
{
    // ------------------------------------------- what makes a leftover actually matter

    private const byte Less = 0;

    private const byte Equal = 1;

    private const byte Greater = 2;

    /// <summary>
    /// <b>The case the whole correction turns on.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>special 0x0187</c> heads all three obstacle scripts, its answer is compared against 2
    /// at every one of its sites, and every conditional there tests EQUAL. A slot holding 129
    /// gives Greater where nought gives Less — the comparison plainly DIFFERS — and neither is
    /// equal, so the branch is the same both times and the leftover costs nothing.
    /// </para>
    /// <para>
    /// Reported off the comparison alone it is 506 places; off the branch it is 85. A fixture
    /// that only carried cases where the two agree could not tell the two readings apart at all,
    /// which is 297's costume and 190's before it.
    /// </para>
    /// </remarks>
    [Fact]
    public void AComparisonCanDifferWhileTheBranchDoesNot()
    {
        (bool differs, bool tookAnother) = WhatTheRoutineLeft.Reading(129, 2, Equal);

        Assert.True(differs, "129 against 2 is Greater and nought against 2 is Less");
        Assert.False(tookAnother, "neither Greater nor Less is Equal, so the EQUAL arm is not taken either way");
    }

    /// <summary>And the other side of it, so the test is a discrimination and not an assertion.</summary>
    [Fact]
    public void AndACaseWhereTheBranchDoesGoTheOtherWay()
    {
        (bool differs, bool tookAnother) = WhatTheRoutineLeft.Reading(1, 0, Greater);

        Assert.True(differs);
        Assert.True(tookAnother, "1 against 0 is Greater and nought against 0 is Equal");
    }

    /// <summary>
    /// A comparison nobody branches on cannot differ, however far apart the two results are.
    /// </summary>
    /// <remarks>
    /// The cartridge has none of these — 0 of 545 read places at the widest setting — so this is
    /// a decoy, and it is written down as one rather than left to be discovered (57).
    /// </remarks>
    [Fact]
    public void AndNoConditionalMeansNothingCouldHaveGoneEitherWay()
    {
        (bool differs, bool tookAnother) = WhatTheRoutineLeft.Reading(214, 0, null);

        Assert.True(differs);
        Assert.False(tookAnother);
    }

    /// <summary>A slot that held nought is the case the old sentence was true of.</summary>
    [Fact]
    public void AndNoughtInTheSlotIsNoDifferenceAtAll()
    {
        foreach (byte condition in new[] { Less, Equal, Greater })
        {
            (bool differs, bool tookAnother) = WhatTheRoutineLeft.Reading(0, 5, condition);

            Assert.False(differs);
            Assert.False(tookAnother);
        }
    }

    /// <summary>The buckets do not overlap, and every place lands in exactly one of them.</summary>
    /// <remarks>
    /// Four buckets is the whole reading — a leftover nobody read, a slot that held nought, a
    /// leftover that changed nothing, and one that did. A place in two of them at once would make
    /// every column in the output add up to more than its own total, quietly.
    /// </remarks>
    [Theory]
    [InlineData(0, false, false)]
    [InlineData(214, false, false)]
    [InlineData(0, true, false)]
    [InlineData(129, true, false)]
    [InlineData(1, true, true)]
    public void EveryPlaceLandsInExactlyOneBucket(int held, bool read, bool differs)
    {
        var call = new WhatTheRoutineLeft(0x187, 0x800D, 0x08000000, held, read, 2, differs);

        int buckets =
            (call.Read ? 0 : 1)
            + (call.Read && call.AnsweredNought ? 1 : 0)
            + (call.ReadAndHarmless ? 1 : 0)
            + (call.ReadAndDiffers ? 1 : 0);

        Assert.Equal(1, buckets);
    }

    // ----------------------------------------- why there was anything in the slot at all

    /// <summary>
    /// <b>The cut is two-sided, and it was one-sided for as long as it existed.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>FirstRemembered</c>'s own paragraph is about the twelve pads below it in the
    /// <c>0x400x</c> band. Written as <c>variable &gt;= 0x4010</c> it also keeps everything from
    /// <c>0x8000</c> up — and that band is sixteen numbers written at 3428 places, 214 places per
    /// number against the remembered band's 11. The scratchiest thing in the game was on the
    /// remembered side of a cut written to exclude scratch.
    /// </para>
    /// <para>
    /// Named at every boundary rather than counted, because a rule with two edges is satisfied by
    /// whatever the code happens to do at one of them (35).
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(0x0000, false)]
    [InlineData(0x400F, false)]
    [InlineData(0x4010, true)]
    [InlineData(0x4055, true)]
    [InlineData(0x7FFF, true)]
    [InlineData(0x8000, false)]
    [InlineData(0x8004, false)]
    [InlineData(0x800D, false)]
    [InlineData(0x800F, false)]
    public void OnlyTheStorysOwnBandSurvivesAScript(int variable, bool remembered) =>
        Assert.Equal(remembered, HowAScriptRuns.IsRemembered(variable));

    /// <summary>
    /// And the control puts the argument slots back, which is what every number this project
    /// printed before 308 was measured under.
    /// </summary>
    /// <remarks>
    /// A control the reader cannot re-run is not a control (241). Asserted on both bands, because
    /// a version that turned the whole rule off would satisfy the interesting half of it.
    /// </remarks>
    [Fact]
    public void AndTheControlPutsTheArgumentSlotsBackAndNothingElse()
    {
        Assert.True(HowAScriptRuns.IsRemembered(0x800D, rememberSlots: true));
        Assert.True(HowAScriptRuns.IsRemembered(0x4055, rememberSlots: true));

        // And it does not reach below the other edge, which is not what it is about.
        Assert.False(HowAScriptRuns.IsRemembered(0x400F, rememberSlots: true));
    }

    /// <summary>
    /// The two edges are different numbers and the argument slots are above the pads.
    /// <para>
    /// The fault was that one constant did the work of two, so a test that only ever asked about
    /// one of them could not have noticed.
    /// </para>
    /// </summary>
    [Fact]
    public void TheTwoEdgesAreTwoNumbers() =>
        Assert.True(
            HowAScriptRuns.FirstRemembered < HowAScriptRuns.FirstArgumentSlot,
            "the argument slots have to be above the pads or the rule keeps nothing at all");
}
