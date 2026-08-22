using PokeMmo.Core.Scripts;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;
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

    // ------------------------------------------- and the same thing end to end, on bytes

    private const uint Start = Rom.BaseAddress + 0x100;

    private const int Unanswerable = 0x999;

    private static byte[] Word(int value) => [(byte)value, (byte)(value >> 8)];

    private static byte[] At(uint address) =>
        [(byte)address, (byte)(address >> 8), (byte)(address >> 16), (byte)(address >> 24)];

    /// <summary>Where the conditional jumps: one <c>end</c>, well clear of the script.</summary>
    private const uint Elsewhere = Start + 0x40;

    private static Rom Image(params byte[] script)
    {
        var image = new byte[0x400];
        script.CopyTo(image, (int)(Start - PokeMmo.RomExtract.Rom.BaseAddress));

        // The arm the branch may take. It has to go SOMEWHERE that decodes and is not this
        // script — a conditional pointed back at its own start is a loop, and a loop makes one
        // call into as many records as the step cap allows.
        image[(int)(Elsewhere - PokeMmo.RomExtract.Rom.BaseAddress)] = 0x02;

        return new PokeMmo.RomExtract.Rom(image);
    }

    /// <summary>
    /// The whole shape on bytes: something puts a number in the slot, a routine this cannot
    /// answer is stepped over, and the compare after it reads the number instead of an answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sequencing — that a write between the call and the compare spends the slot, that a
    /// conditional is what decides, that the run ends by flushing what nobody read — lives inside
    /// <see cref="ScriptRunner"/>, which needs an image. Without one of these it is guarded by
    /// nothing at all, and the cartridge has no example of two of the three shapes.
    /// </para>
    /// </remarks>
    [Fact]
    public void ARoutineItCannotAnswerLeavesTheSlotAloneAndTheCompareReadsIt()
    {
        ScriptRun run = ScriptRunner.Run(
            rom: Image(
            [
                0x16, .. Word(0x800D), .. Word(5),                 // setvar 0x800D, 5
                0x26, .. Word(0x800D), .. Word(Unanswerable),      // specialvar — nothing answers it
                0x21, .. Word(0x800D), .. Word(5),                 // compare 0x800D, 5
                0x06, 1, .. At(Elsewhere),                             // goto if EQUAL
                0x02,
            ]), address: Start);

        WhatTheRoutineLeft left = Assert.Single(run.LeftInTheSlot);

        Assert.Equal(Unanswerable, left.Routine);
        Assert.Equal(0x800D, left.Slot);
        Assert.True(left.Read, "the compare is the very next command");
        Assert.Equal(5, left.Held);
        Assert.True(left.Differs, "5 against 5 is Equal where nought against 5 is Less");
        Assert.True(left.TookADifferentArm, "and the conditional tests EQUAL");
    }

    /// <summary>
    /// A write between the call and the compare SPENDS the slot, so what the compare reads is
    /// that write and not a leftover.
    /// </summary>
    /// <remarks>
    /// The cartridge does this and the distinction is the whole reading: without it every
    /// ordinary <c>setvar ; compare</c> after any unanswered call anywhere would be counted.
    /// </remarks>
    [Fact]
    public void AWriteBetweenThemSpendsTheSlot()
    {
        ScriptRun run = ScriptRunner.Run(
            rom: Image(
            [
                0x16, .. Word(0x800D), .. Word(5),
                0x26, .. Word(0x800D), .. Word(Unanswerable),
                0x16, .. Word(0x800D), .. Word(9),                 // setvar 0x800D, 9 — the answer now
                0x21, .. Word(0x800D), .. Word(9),
                0x06, 1, .. At(Elsewhere),
                0x02,
            ]), address: Start);

        WhatTheRoutineLeft left = Assert.Single(run.LeftInTheSlot);

        Assert.False(left.Read, "the setvar spent the slot before the compare reached it");
        Assert.False(left.TookADifferentArm);
    }

    /// <summary>
    /// And a call nothing ever reads is still recorded, in the bucket where it costs nothing.
    /// </summary>
    /// <remarks>
    /// That bucket is 545 of the widest run's 1072 places. A reading that dropped it would report
    /// a share over a denominator missing its own harmless half, which is 8 in one line.
    /// </remarks>
    [Fact]
    public void AndACallNobodyReadsIsStillCounted()
    {
        ScriptRun run = ScriptRunner.Run(
            rom: Image([0x26, .. Word(0x800D), .. Word(Unanswerable), 0x02]), address: Start);

        WhatTheRoutineLeft left = Assert.Single(run.LeftInTheSlot);

        Assert.False(left.Read);
        Assert.Equal(Unanswerable, left.Routine);
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

    // -------------------------------------------------- which pass of a place is kept

    /// <summary>
    /// A place runs on every pass with a different state behind it, and the WORST pass is the one
    /// that counts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same call can answer nought on one pass and read a leftover on the next, because what
    /// is in the slot depends on where the walk has been. Keeping the last pass would report
    /// whichever pass happened to be last, which is a fact about the walk order and not about the
    /// cartridge — and the whole point of this reading is that the walk order should not decide
    /// anything.
    /// </para>
    /// <para>
    /// Named in order rather than counted, so a ranking that happened to sort right for two of
    /// the four cannot pass (35).
    /// </para>
    /// </remarks>
    [Fact]
    public void TheWorstPassOfAPlaceIsTheOneKept()
    {
        WhatTheRoutineLeft NobodyRead() => new(1, 0x800D, 0, 214, false, 0, false);
        WhatTheRoutineLeft AnsweredNought() => new(1, 0x800D, 0, 0, true, 2, false) { Branched = true };
        WhatTheRoutineLeft Harmless() => new(1, 0x800D, 0, 129, true, 129, false) { Branched = true };

        // The 0x0187 shape: the comparison differs and the branch does not. It is a level of its
        // own, and a ranking that folds it into the one below or the one above cannot tell a
        // place that only ever read a harmless leftover from one that took a different arm.
        WhatTheRoutineLeft Differs() => new(1, 0x800D, 0, 129, true, 2, true) { Branched = true };

        WhatTheRoutineLeft TookAnother() =>
            new(1, 0x800D, 0, 1, true, 0, true) { Branched = true, TookADifferentArm = true };

        Assert.True(Autoplayer.Worse(TookAnother(), Differs()));
        Assert.True(Autoplayer.Worse(Differs(), Harmless()));
        Assert.True(Autoplayer.Worse(Harmless(), AnsweredNought()));
        Assert.True(Autoplayer.Worse(AnsweredNought(), NobodyRead()));

        // And it is a strict order in one direction only — otherwise every pass replaces the
        // one before it and the merge keeps the last after all.
        Assert.False(Autoplayer.Worse(NobodyRead(), TookAnother()));
        Assert.False(Autoplayer.Worse(Differs(), TookAnother()));
        Assert.False(Autoplayer.Worse(Harmless(), Harmless()));
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
