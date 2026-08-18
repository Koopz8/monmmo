using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Two halves of one milestone, and they meet on the same eleven records.
/// <para>
/// <b>A counter walk that reaches everything cannot say no.</b> 255 credited three conditions to
/// "a counter can reach it" and 257 reported those three as the whole of what the square list
/// gained from the correction. Every variable the test has ever been given — <c>0x4001</c>,
/// <c>0x4002</c>, <c>0x4003</c> — reaches <b>100 of the 100 values in 0..99</b>, so it answered
/// yes before it was asked. Saturation is an exact predicate, not a threshold.
/// </para>
/// <para>
/// <b>And the three were the same idiom as the eight.</b> All eleven of this cartridge's square
/// conditions that nothing can produce want <c>99</c> — a value that appears in no
/// <c>setvar</c> and no <c>compare</c> anywhere in the image — and all eleven of their scripts,
/// at eleven distinct addresses, open <c>compare &lt;own variable&gt;, 100</c>. Nought of the
/// other 217 do.
/// </para>
/// </summary>
public sealed class NinetyNineAndAWalkThatReachesEverythingTests
{
    private const int Ceiling = 99;

    // ------------------------------------------------------------------- the walk's denominator

    /// <summary>
    /// A counter set to nought and stepped by one reaches every value in range — and the count
    /// says so, rather than the caller having to notice.
    /// </summary>
    [Fact]
    public void AStepOfOneReachesEveryValueInRange()
    {
        var scratch = new WhatItCanHold(0x4001, [0], [1], false);

        Assert.Equal(100, scratch.HowManyItReaches(Ceiling));
        Assert.True(scratch.ACounterReachesEverything(Ceiling));
    }

    /// <summary>
    /// AND THE OTHER ANSWER HAS TO BE POSSIBLE. A step of ten from nought reaches ten of the
    /// hundred, so the test can distinguish a counter that says something from one that does not
    /// — without which "saturated" is a label with no opposite.
    /// </summary>
    [Fact]
    public void AStepThatCannotCoverTheRangeDoesNotSaturate()
    {
        var counter = new WhatItCanHold(0x4002, [0], [10], false);

        Assert.Equal(10, counter.HowManyItReaches(Ceiling));
        Assert.False(counter.ACounterReachesEverything(Ceiling));
    }

    /// <summary>
    /// A variable nothing steps reaches exactly what something sets it to, and never saturates —
    /// the count is about the WALK, so a variable with no walk must not read as covering the
    /// range by having a large set.
    /// </summary>
    [Fact]
    public void AVariableNothingStepsReachesOnlyWhatSomethingSetsIt()
    {
        var plain = new WhatItCanHold(0x406F, [0, 3, 6], [], false);

        Assert.Equal(3, plain.HowManyItReaches(Ceiling));
        Assert.False(plain.ACounterReachesEverything(Ceiling));
    }

    // ------------------------------------------------- the fifth answer, and what it is not

    /// <summary>
    /// <b>THE CORRECTION.</b> A saturated counter is its own answer and not
    /// <see cref="HowItIsReached.Counted"/> — and a real counter still is, so the fixture carries
    /// one of each and names them.
    /// </summary>
    [Fact]
    public void ASaturatedCounterIsItsOwnAnswerAndARealCounterIsStill()
    {
        var canHold = new Dictionary<int, WhatItCanHold>
        {
            // Reaches everything in 0..99, so its yes carries nothing.
            [0x4001] = new(0x4001, [0], [1], false),

            // Reaches 0, 10, 20 … 90 and nothing else, so its yes is a reading.
            [0x4002] = new(0x4002, [0], [10], false),
        };

        Assert.Equal(
            HowItIsReached.CounterReachesEverything,
            WhatAVariableCanHold.HowReached(canHold, 0x4001, 99, Ceiling));
        Assert.Equal(
            HowItIsReached.Counted,
            WhatAVariableCanHold.HowReached(canHold, 0x4002, 90, Ceiling));
    }

    /// <summary>
    /// AND IT IS NOT EVIDENCE ANYTHING WRITES THE VALUE. This is the whole point: 255 and 257
    /// both quoted three conditions this had credited, and a verdict that still credits them
    /// would leave the correction in the output and out of the answer.
    /// </summary>
    [Fact]
    public void ASaturatedCounterIsNotEvidenceAnythingWritesTheValue()
    {
        var canHold = new Dictionary<int, WhatItCanHold>
        {
            [0x4001] = new(0x4001, [0], [1], false),
            [0x4002] = new(0x4002, [0], [10], false),
        };

        Assert.Equal(
            WhetherItCanFire.NothingCan,
            WhatAVariableCanHold.CanItFire(canHold, 0x4001, 99, 0, Ceiling));

        // And a counter that is a reading still counts, or the correction has thrown the
        // mechanism away along with the artefact.
        Assert.Equal(
            WhetherItCanFire.SomethingWritesIt,
            WhatAVariableCanHold.CanItFire(canHold, 0x4002, 90, 0, Ceiling));
    }

    /// <summary>
    /// AND A VALUE SOMETHING SETS OUTRIGHT IS UNTOUCHED BY ANY OF THIS. Without this the
    /// correction could quietly demote a plain <c>setvar</c> on a variable that also happens to
    /// be stepped, which is most of the interesting ones.
    /// </summary>
    [Fact]
    public void AValueSomethingSetsIsStillWrittenEvenOnASaturatingCounter()
    {
        var canHold = new Dictionary<int, WhatItCanHold>
        {
            [0x4001] = new(0x4001, [0, 7], [1], false),
        };

        Assert.Equal(
            HowItIsReached.Written,
            WhatAVariableCanHold.HowReached(canHold, 0x4001, 7, Ceiling));
    }

    // -------------------------------------------------------------- the script's own guard

    /// <summary>
    /// THE ELEVEN: a script that opens by comparing the very variable its record's condition
    /// names. On this cartridge every one of them compares against 100 while the record says 99.
    /// </summary>
    [Fact]
    public void AScriptThatComparesItsOwnVariableIsFound()
    {
        Assert.Equal(
            100,
            TheScriptsOwnGuard.Guard(
                [LockAll(), Compare(0x4064, 100), SetVar(0x4064, 100)], 0x4064));
    }

    /// <summary>
    /// AND A SCRIPT THAT COMPARES SOMETHING ELSE IS NOT ONE. 292 of the arrival list's 295
    /// opening compares name a different variable, so without this the reading would report
    /// almost every condition as self-guarded.
    /// </summary>
    [Fact]
    public void AScriptThatComparesADifferentVariableIsNotAGuard()
    {
        Assert.Null(
            TheScriptsOwnGuard.Guard([Compare(0x8007, 0), SetVar(0x4064, 100)], 0x4064));

        // …and the control sees it, which is what makes the eleven a number with a floor.
        Assert.Equal(0x8007, TheScriptsOwnGuard.FirstCompareNames([Compare(0x8007, 0)]));
    }

    /// <summary>
    /// THE FIRST ONE, because a guard is at the top. A compare further down is the script
    /// branching on its own progress, which is a different thing and would make this answer yes
    /// for reasons that have nothing to do with the record.
    /// </summary>
    [Fact]
    public void TheGuardIsTheFirstCompareAndNotALaterOne()
    {
        Assert.Equal(
            100,
            TheScriptsOwnGuard.Guard(
                [Compare(0x4064, 100), SetVar(0x4064, 100), Compare(0x4064, 3)], 0x4064));
    }

    /// <summary>
    /// WRITING YOUR OWN VARIABLE IS ORDINARY AND IS A DIFFERENT QUESTION — 142 of the 228 square
    /// scripts do it and eleven guard. Answering both with one function is how "the script
    /// handles its own condition" would have read as true of two thirds of the game.
    /// </summary>
    [Fact]
    public void WritingTheVariableAndGuardingOnItAreDifferentQuestions()
    {
        ScriptCommand[] disarms = [SetVar(0x4062, 1), LockAll()];

        Assert.Equal(1, TheScriptsOwnGuard.Writes(disarms, 0x4062));
        Assert.Null(TheScriptsOwnGuard.Guard(disarms, 0x4062));
    }

    /// <summary>And a script that does neither says so rather than throwing.</summary>
    [Fact]
    public void AScriptThatDoesNeitherAnswersNothingTwice()
    {
        Assert.Null(TheScriptsOwnGuard.Guard([LockAll()], 0x4064));
        Assert.Null(TheScriptsOwnGuard.Writes([LockAll()], 0x4064));
        Assert.Null(TheScriptsOwnGuard.FirstCompareNames([LockAll()]));
    }

    private static ScriptCommand Compare(int variable, int value) =>
        new(0, 0x21, [(byte)variable, (byte)(variable >> 8), (byte)value, (byte)(value >> 8)]);

    private static ScriptCommand SetVar(int variable, int value) =>
        new(0, 0x16, [(byte)variable, (byte)(variable >> 8), (byte)value, (byte)(value >> 8)]);

    private static ScriptCommand LockAll() => new(0, 0x69, []);
}
