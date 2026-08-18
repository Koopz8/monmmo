using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// 220 moved 145 sites into "the compare is past something that <b>may</b> have answered
/// instead". May have is a statement about the reading, not about the cartridge, and this is
/// what turns it into one of three answers.
/// <para>
/// 219 did one by hand: between <c>special 0x001C</c> and its compare sits
/// <c>call 0x081A6675</c>, and that block is <c>copyvar 0x8012, 0x8013 ; return</c>. Four bytes
/// that cannot have touched the answer variable, so the compare is <c>0x001C</c>'s after all.
/// </para>
/// <para>
/// <b>The third answer is the one that matters most.</b> A <c>callstd</c> is in the barrier list
/// because a standard routine answers, and this project has never read one — so a site behind
/// one is <em>not said</em>, not somebody else's. Filling that hole with a verdict would be
/// inventing a fact, which is the fault this project has caught in itself four milestones
/// running.
/// </para>
/// </summary>
public sealed class WhatStoodInTheWayTests
{
    private const byte Filler = 0x77;
    private const byte End = 0x02;
    private const byte Special = 0x25;
    private const byte Call = 0x04;
    private const byte CallStandard = 0x09;
    private const byte Goto = 0x05;
    private const byte SetVar = 0x16;
    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte CopyVar = 0x19;
    private const byte WaitState = 0x27;
    private const byte Return = 0x03;

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

    /// <summary>The block: a routine, something in the way at 0x1003, then a compare and a branch.</summary>
    private static byte[] WithInTheWay(params int[] between)
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x1C, 0x00);
        Put(image, 0x1003, between);

        int compare = 0x1003 + between.Length;

        Put(image, compare, Compare, 0x0D, 0x80, 0x01, 0x00);
        Put(image, compare + 5, GotoIf, 0x01);
        Address(image, compare + 7, 0x1000);
        Put(image, compare + 11, End);

        return image;
    }

    private static (WhoTheCompareBelongsTo.InTheWay Was, uint Called)? Stood(byte[] image)
    {
        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        // The fixture is only worth anything if the compare is where it is supposed to be.
        Assert.Contains(commands, c => c.Code == Compare);

        return WhoTheCompareBelongsTo.WhatStoodInTheWay(new Rom(image), commands, 0);
    }

    /// <summary>
    /// THE ONE 219 READ BY HAND: a call to a block that moves one argument slot into another
    /// and returns. It cannot have answered, so the compare is the routine's after all.
    /// </summary>
    [Fact]
    public void ACallToABlockThatTouchesNothingGivesTheCompareBack()
    {
        byte[] image = WithInTheWay(WaitState, Call, 0, 0, 0, 0);

        Address(image, 0x1005, 0x1200);

        // copyvar 0x8012, 0x8013 ; return — the cartridge's own four bytes.
        Put(image, 0x1200, CopyVar, 0x12, 0x80, 0x13, 0x80);
        Put(image, 0x1205, Return);

        (WhoTheCompareBelongsTo.InTheWay was, uint called) = Assert.NotNull(Stood(image));

        Assert.Equal(WhoTheCompareBelongsTo.InTheWay.ACallThatTouchesNothing, was);
        Assert.Equal(Rom.BaseAddress + 0x1200, called);
        Assert.Equal(WhoTheCompareBelongsTo.Whose.StillThisRoutines, WhoTheCompareBelongsTo.Belongs(was));
    }

    /// <summary>
    /// AND THE SAME SHAPE WITH A BLOCK THAT DOES ANSWER is the opposite verdict — which is what
    /// makes the one above a reading rather than a shrug.
    /// </summary>
    [Fact]
    public void ACallToABlockThatAnswersTakesTheCompareAway()
    {
        byte[] image = WithInTheWay(Call, 0, 0, 0, 0);

        Address(image, 0x1004, 0x1200);

        Put(image, 0x1200, Special, 0x5D, 0x00);
        Put(image, 0x1203, Return);

        (WhoTheCompareBelongsTo.InTheWay was, uint _) = Assert.NotNull(Stood(image));

        Assert.Equal(WhoTheCompareBelongsTo.InTheWay.ACallThatAnswers, was);
        Assert.Equal(WhoTheCompareBelongsTo.Whose.SomebodyElses, WhoTheCompareBelongsTo.Belongs(was));
    }

    /// <summary>
    /// A block that ends by jumping away answered nothing and did not fail to — the reading
    /// stopped, which is 218's distinction and its own verdict here.
    /// </summary>
    [Fact]
    public void ACallToABlockThatJumpsAwayIsNotSaidEitherWay()
    {
        byte[] image = WithInTheWay(Call, 0, 0, 0, 0);

        Address(image, 0x1004, 0x1200);

        Put(image, 0x1200, Goto);
        Address(image, 0x1201, 0x1400);
        Put(image, 0x1400, Return);

        (WhoTheCompareBelongsTo.InTheWay was, uint _) = Assert.NotNull(Stood(image));

        Assert.Equal(WhoTheCompareBelongsTo.InTheWay.ACallThatJumpsAway, was);
        Assert.Equal(WhoTheCompareBelongsTo.Whose.NotSaid, WhoTheCompareBelongsTo.Belongs(was));
    }

    /// <summary>
    /// ANOTHER ROUTINE IN THE WAY IS THE ONE CERTAIN NO — and it is the double-count 220 found
    /// at <c>1.93</c>, where one compare was credited to <c>0x0156</c> and <c>0x0188</c> both.
    /// </summary>
    [Fact]
    public void AnotherRoutineInTheWayTakesTheCompare()
    {
        (WhoTheCompareBelongsTo.InTheWay was, uint _) =
            Assert.NotNull(Stood(WithInTheWay(Special, 0x88, 0x01)));

        Assert.Equal(WhoTheCompareBelongsTo.InTheWay.AnotherRoutine, was);
        Assert.Equal(WhoTheCompareBelongsTo.Whose.SomebodyElses, WhoTheCompareBelongsTo.Belongs(was));
    }

    /// <summary>
    /// AND A STANDARD ROUTINE IS NOT A NO. This project has never read one, so the honest answer
    /// is that it does not know — and a rule that swept it in with the others would report a
    /// certainty it has not got.
    /// </summary>
    [Fact]
    public void AStandardRoutineIsNotSaidRatherThanSomebodyElses()
    {
        (WhoTheCompareBelongsTo.InTheWay was, uint _) =
            Assert.NotNull(Stood(WithInTheWay(CallStandard, 0x06)));

        Assert.Equal(WhoTheCompareBelongsTo.InTheWay.AStandardRoutine, was);
        Assert.Equal(WhoTheCompareBelongsTo.Whose.NotSaid, WhoTheCompareBelongsTo.Belongs(was));

        // And the discrimination this whole file turns on: the three verdicts are three, and a
        // rule that collapsed the unknown one into either of the others would pass every other
        // test here.
        Assert.NotEqual(
            WhoTheCompareBelongsTo.Belongs(WhoTheCompareBelongsTo.InTheWay.AStandardRoutine),
            WhoTheCompareBelongsTo.Belongs(WhoTheCompareBelongsTo.InTheWay.AnotherRoutine));

        Assert.NotEqual(
            WhoTheCompareBelongsTo.Belongs(WhoTheCompareBelongsTo.InTheWay.AStandardRoutine),
            WhoTheCompareBelongsTo.Belongs(WhoTheCompareBelongsTo.InTheWay.ACallThatTouchesNothing));
    }

    /// <summary>
    /// A command that answers on its own account — <c>0xA0</c>, the one the barrier list was
    /// written for in the first place.
    /// </summary>
    [Fact]
    public void ACommandThatAnswersOnItsOwnAccountTakesTheCompareToo()
    {
        (WhoTheCompareBelongsTo.InTheWay was, uint _) = Assert.NotNull(Stood(WithInTheWay(0xA0)));

        Assert.Equal(WhoTheCompareBelongsTo.InTheWay.ACommandThatAnswers, was);
        Assert.Equal(WhoTheCompareBelongsTo.Whose.SomebodyElses, WhoTheCompareBelongsTo.Belongs(was));
    }

    /// <summary>
    /// AND IT COMES BACK EMPTY when nothing is in the way at all — the answer that has to be
    /// possible, or every verdict above is an artefact of always finding something.
    /// </summary>
    [Fact]
    public void NothingInTheWayIsNoVerdictAtAll()
    {
        Assert.Null(Stood(WithInTheWay(WaitState)));
        Assert.Null(Stood(WithInTheWay(SetVar, 0x12, 0x80, 0x01, 0x00)));
    }

    /// <summary>
    /// AND WHICH SITES THIS READING IS EVEN ABOUT: the ones whose whole claim on the routine is
    /// past a barrier, not merely the ones that have something past one.
    /// <para>
    /// <b>Those are 78 and 145 on the cartridge</b>, and 220's own headline printed the second
    /// with the first's wording. A site with a clean compare as well already has an owner and
    /// the barrier only adds values to it.
    /// </para>
    /// <para>
    /// One rule, asked by both readings. A second copy that drifts is what 220 was about.
    /// </para>
    /// </summary>
    [Fact]
    public void OnlyASiteWithNothingCleanIsThisReadingsToAnswer()
    {
        Assert.True(SpecialContracts.NothingCleanHere([], [1]));

        // Has an owner already — the barrier is not the whole story here.
        Assert.False(SpecialContracts.NothingCleanHere([0], [1]));

        // And nothing past a barrier at all is not this reading's business either way.
        Assert.False(SpecialContracts.NothingCleanHere([0], []));
        Assert.False(SpecialContracts.NothingCleanHere([], []));
    }

    /// <summary>
    /// The FIRST thing in the way decides, not the last. Two barriers and the verdict is the
    /// nearer one's — anything after it is already about somebody else.
    /// </summary>
    [Fact]
    public void TheFirstThingInTheWayIsTheOneThatDecides()
    {
        byte[] image = WithInTheWay(Special, 0x88, 0x01, Call, 0, 0, 0, 0);

        Address(image, 0x1007, 0x1200);

        Put(image, 0x1200, CopyVar, 0x12, 0x80, 0x13, 0x80);
        Put(image, 0x1205, Return);

        (WhoTheCompareBelongsTo.InTheWay was, uint _) = Assert.NotNull(Stood(image));

        // The call touches nothing, and it does not matter: the special already answered.
        Assert.Equal(WhoTheCompareBelongsTo.InTheWay.AnotherRoutine, was);
    }
}
