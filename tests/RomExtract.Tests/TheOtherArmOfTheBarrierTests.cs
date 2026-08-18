using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// <c>SpecialContracts</c> is the other arm of the reading <c>SpecialCalls</c> was given a
/// barrier for at 214, and it had none at all until 220.
/// <para>
/// The two contradicted each other out loud on the cartridge. <c>--routines</c> gave
/// <c>0x01C</c> nineteen branches, compared against 1 nineteen times; <c>--special 0x1C</c>
/// said it was never branched on. <b>The same nineteen sites</b> — and between the
/// <c>special</c> and the <c>compare</c> sits <c>call 0x081A6675</c>, which the newer reading
/// stops at and the older one walked straight through.
/// </para>
/// <para>
/// Worse than a disagreement, it double-counted. At <c>1.93</c> the bytes are
/// <c>special 0x0156 ; special 0x0188 ; compare 0x800D, 0 ; if EQUAL</c>, and one compare was
/// credited to both routines. 215 read that site by hand and wrote up the <c>0x0188</c> half;
/// the <c>0x0156</c> half was in the table all along.
/// </para>
/// <para>
/// <b>The compares past the barrier are kept, not dropped.</b> "Nothing branches on this
/// routine" and "the branch is past something that may have answered instead" are different
/// facts, and 219 showed the second can resolve in the routine's favour: the thing in the way
/// at those nineteen sites is <c>copyvar 0x8012, 0x8013 ; return</c>, which cannot have
/// answered anything.
/// </para>
/// </summary>
public sealed class TheOtherArmOfTheBarrierTests
{
    private const byte Filler = 0x77;
    private const byte End = 0x02;
    private const byte Special = 0x25;
    private const byte Call = 0x04;
    private const byte SetVar = 0x16;
    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte WaitState = 0x27;
    private const byte Return = 0x03;

    private const int Answer = 0x800D;

    private static byte[] Blank()
    {
        var image = new byte[0x20000];

        Array.Fill(image, Filler);

        return image;
    }

    private static void Put(byte[] image, int at, params int[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++) image[at + i] = (byte)bytes[i];
    }

    private static void Address(byte[] image, int at, int offset)
    {
        uint address = Rom.BaseAddress + (uint)offset;

        for (var i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>What the routine at the head of the block is credited with, and what it is not.</summary>
    private static (IReadOnlyList<int> Direct, IReadOnlyList<int> Beyond) Read(byte[] image)
    {
        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        return SpecialContracts.WhatIsComparedAfter(commands, 0, Answer);
    }

    /// <summary>
    /// The ordinary case, asserted first — without it every test below passes on a reading that
    /// credits nobody with anything.
    /// </summary>
    [Fact]
    public void ACompareRightAfterASpecialIsThatSpecialsAnswer()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x1C, 0x00);
        Put(image, 0x1003, Compare, 0x0D, 0x80, 0x01, 0x00);
        Put(image, 0x1008, GotoIf, 0x01);
        Address(image, 0x100A, 0x1000);
        Put(image, 0x100E, End);

        (IReadOnlyList<int> direct, IReadOnlyList<int> beyond) = Read(image);

        Assert.Equal(1, Assert.Single(direct));
        Assert.Empty(beyond);
    }

    /// <summary>
    /// AND THE ONE THAT WAS WRONG FOR SIX MILESTONES: the cartridge's own shape at
    /// <c>0x1BB567</c> — a call between the special and the compare.
    /// </summary>
    [Fact]
    public void ACallInBetweenMeansTheCompareIsNotCreditedHere()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x1C, 0x00);
        Put(image, 0x1003, WaitState);
        Put(image, 0x1004, Call);
        Address(image, 0x1005, 0x1200);
        Put(image, 0x1009, Compare, 0x0D, 0x80, 0x01, 0x00);
        Put(image, 0x100E, GotoIf, 0x01);
        Address(image, 0x1010, 0x1000);
        Put(image, 0x1014, End);
        Put(image, 0x1200, Return);

        (IReadOnlyList<int> direct, IReadOnlyList<int> beyond) = Read(image);

        Assert.Empty(direct);
        Assert.Equal(1, Assert.Single(beyond));
    }

    /// <summary>
    /// AND IT IS NOT DROPPED. The value past the barrier is still reported, separately, because
    /// it is where to look next rather than nothing at all.
    /// <para>
    /// The break for this is the obvious one — return the direct list and throw the far one
    /// away — and it is the difference between an instrument that says "unknown" and one that
    /// says "no".
    /// </para>
    /// </summary>
    [Fact]
    public void WhatIsPastTheBarrierIsReportedRatherThanThrownAway()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x1C, 0x00);
        Put(image, 0x1003, Call);
        Address(image, 0x1004, 0x1200);
        Put(image, 0x1008, Compare, 0x0D, 0x80, 0x04, 0x00);
        Put(image, 0x100D, GotoIf, 0x01);
        Address(image, 0x100F, 0x1000);
        Put(image, 0x1013, End);
        Put(image, 0x1200, Return);

        Assert.Equal(new[] { 4 }, Read(image).Beyond);
    }

    /// <summary>
    /// A SECOND SPECIAL IS A BARRIER TOO, and this is the double-count: at <c>1.93</c> one
    /// compare was credited to <c>0x0156</c> and to <c>0x0188</c> both.
    /// </summary>
    [Fact]
    public void AnotherSpecialInBetweenIsTheSameBarrier()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x56, 0x01);
        Put(image, 0x1003, Special, 0x88, 0x01);
        Put(image, 0x1006, Compare, 0x0D, 0x80, 0x00, 0x00);
        Put(image, 0x100B, GotoIf, 0x01);
        Address(image, 0x100D, 0x1000);
        Put(image, 0x1011, End);

        (IReadOnlyList<int> direct, IReadOnlyList<int> beyond) = Read(image);

        Assert.Empty(direct);
        Assert.Equal(0, Assert.Single(beyond));

        // And the second special IS credited with it — the compare belongs to somebody.
        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        Assert.Equal(new[] { 0 }, SpecialContracts.WhatIsComparedAfter(commands, 1, Answer).Direct);
    }

    /// <summary>
    /// AND THE BARRIER IS THE SAME LIST AS THE OTHER ARM'S, not a copy of it.
    /// <para>
    /// A copied list is how the two arms came apart in the first place: <c>SpecialCalls</c>
    /// learned <c>call</c> at 214 and this one did not, because there was nothing to learn
    /// through. Every code the one reading stops at, the other stops at.
    /// </para>
    /// <para>
    /// <b>Each barrier is written at its own width and the fixture checks where it landed.</b>
    /// The first version padded every code to five bytes, which for the one-byte
    /// <c>callstd</c> and the no-argument <c>0xA0</c> pushed the compare out of the four-command
    /// window entirely — so those two rows passed with the barrier removed. They were testing
    /// the window, not the list.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(Special, 2)]
    [InlineData(Call, 4)]
    [InlineData(0x09, 1)]   // callstd
    [InlineData(0xA0, 0)]
    public void EveryCodeTheOtherArmStopsAtStopsThisOneToo(byte barrier, int width)
    {
        Assert.True(SpecialCalls.AnswersItself(barrier));

        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x1C, 0x00);
        Put(image, 0x1003, barrier);

        for (var i = 0; i < width; i++) image[0x1004 + i] = 0x00;

        int compare = 0x1004 + width;

        Put(image, compare, Compare, 0x0D, 0x80, 0x01, 0x00);
        Put(image, compare + 5, GotoIf, 0x01);
        Address(image, compare + 7, 0x1000);
        Put(image, compare + 11, End);

        // WHERE THE THING BEING ASSERTED ABOUT ACTUALLY IS. Without this the row passes for
        // any reason at all, including the compare being somewhere nothing looks.
        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        Assert.Equal(barrier, commands[1].Code);
        Assert.Equal(Compare, commands[2].Code);

        Assert.Empty(Read(image).Direct);
        Assert.Equal(new[] { 1 }, Read(image).Beyond);
    }

    /// <summary>
    /// A command that answers NOTHING is not a barrier — otherwise the barrier swallows the
    /// ordinary case and the instrument reports nought branches everywhere.
    /// </summary>
    [Fact]
    public void SomethingThatCannotAnswerDoesNotStopTheReading()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x1C, 0x00);
        Put(image, 0x1003, WaitState);
        Put(image, 0x1004, Compare, 0x0D, 0x80, 0x01, 0x00);
        Put(image, 0x1009, GotoIf, 0x01);
        Address(image, 0x100B, 0x1000);
        Put(image, 0x100F, End);

        (IReadOnlyList<int> direct, IReadOnlyList<int> beyond) = Read(image);

        Assert.Equal(1, Assert.Single(direct));
        Assert.Empty(beyond);
    }

    /// <summary>
    /// AND A SETVAR TO THE ANSWER VARIABLE STILL ENDS THE READING ALTOGETHER, rather than
    /// putting what follows past the barrier.
    /// <para>
    /// Those are different: a barrier means somebody else may have answered and the compare is
    /// worth chasing; a <c>setvar</c> means the script said the number out loud itself and
    /// there is nothing to chase. The rule was here before 220 and the new one must not
    /// swallow it.
    /// </para>
    /// </summary>
    [Fact]
    public void ASetVarToTheAnswerEndsTheReadingRatherThanCrossingIt()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x1C, 0x00);
        Put(image, 0x1003, SetVar, 0x0D, 0x80, 0x01, 0x00);
        Put(image, 0x1008, Compare, 0x0D, 0x80, 0x01, 0x00);
        Put(image, 0x100D, GotoIf, 0x01);
        Address(image, 0x100F, 0x1000);
        Put(image, 0x1013, End);

        (IReadOnlyList<int> direct, IReadOnlyList<int> beyond) = Read(image);

        Assert.Empty(direct);
        Assert.Empty(beyond);
    }

    /// <summary>
    /// A compare nothing branches on is still not a branch, on either side of the barrier.
    /// </summary>
    [Fact]
    public void ACompareWithNoBranchAfterItCountsOnNeitherSide()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x1C, 0x00);
        Put(image, 0x1003, Call);
        Address(image, 0x1004, 0x1200);
        Put(image, 0x1008, Compare, 0x0D, 0x80, 0x01, 0x00);
        Put(image, 0x100D, End);
        Put(image, 0x1200, Return);

        (IReadOnlyList<int> direct, IReadOnlyList<int> beyond) = Read(image);

        Assert.Empty(direct);
        Assert.Empty(beyond);
    }
}
