using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Milestone 214 added <c>call</c> to the answer scan's barrier list and lost 42 of 1097
/// attributions. This is the other half: what a call actually leaves behind, read one level in.
/// <para>
/// <b>The rule is the LAST thing on the straight line that puts something in the answer
/// variable, of any kind.</b> The first version looked only for routines and credited
/// <c>0x153</c> at fifty-seven places where the block ends <c>setvar 0x800D, 1 ; return</c> —
/// the same fault the barrier was added for, one level down.
/// </para>
/// <para>
/// And a literal on the straight line is not a constant: <c>0x081BBB1E</c> ends
/// <c>setvar 0x800D, 1</c> and its LESS arm ends <c>setvar 0x800D, 0</c>, so the block returns
/// one or nought depending on a routine. Calling that "no ceiling" is a bucket named for a cause
/// with the cause false — trap 5, for the fourth time.
/// </para>
/// </summary>
public sealed class WhatACallLeavesBehindTests
{
    private const byte Filler = 0x77;
    private const byte End = 0x02;
    private const byte Return = 0x03;
    private const byte Special = 0x25;
    private const byte SpecialVar = 0x26;
    private const byte SetVar = 0x16;
    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte Goto = 0x05;
    private const byte Call = 0x04;
    private const byte Lock = 0x6A;

    private const int Answer = 0x800D;
    private const int SomeOtherSlot = 0x8004;

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

    private static int Lo(int v) => v & 0xFF;

    private static int Hi(int v) => v >> 8;

    private static (SpecialCalls.LeftBehind Left, int Who) Left(byte[] image, int at = 0x1000) =>
        SpecialCalls.WhatACallLeaves(new Rom(image), Rom.BaseAddress + (uint)at);

    /// <summary>A routine called for its answer is what the block leaves.</summary>
    [Fact]
    public void ARoutineAskedOnTheStraightLineIsWhatTheBlockLeaves()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x5D, 0x00, Return);

        Assert.Equal((SpecialCalls.LeftBehind.ARoutine, 0x5D), Left(image));
    }

    /// <summary>
    /// THE LAST ONE, not the first — a block that asks two things leaves the second one's
    /// answer.
    /// </summary>
    [Fact]
    public void TheLastThingToAnswerIsTheOneThatCounts()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x11, 0x00, Special, 0x22, 0x00, Return);

        Assert.Equal((SpecialCalls.LeftBehind.ARoutine, 0x22), Left(image));
    }

    /// <summary>
    /// A <c>specialvar</c> into some other slot leaves the answer variable alone.
    /// <para>
    /// Without this check every routine called for a result in any slot counts as answering the
    /// compare, which is the mis-attribution this whole family of rules exists to stop.
    /// </para>
    /// </summary>
    [Fact]
    public void ARoutineAnsweringIntoSomeOtherSlotLeavesTheAnswerAlone()
    {
        byte[] image = Blank();

        Put(image, 0x1000, SpecialVar, Lo(SomeOtherSlot), Hi(SomeOtherSlot), 0x99, 0x00, Return);

        Assert.Equal((SpecialCalls.LeftBehind.Nothing, 0), Left(image));
    }

    /// <summary>
    /// A block that says the answer out loud and asks nothing anywhere is a constant — the only
    /// case where there is no ceiling at all.
    /// </summary>
    [Fact]
    public void ABlockThatSaysTheAnswerAndAsksNothingIsAConstant()
    {
        byte[] image = Blank();

        Put(image, 0x1000, SetVar, Lo(Answer), Hi(Answer), 7, 0, Return);

        Assert.Equal((SpecialCalls.LeftBehind.ANumber, 7), Left(image));
    }

    /// <summary>
    /// AND THE ONE THAT MATTERS: a literal on the straight line with a routine down an arm is
    /// not a constant.
    /// <para>
    /// The fixture is the cartridge's shape: ask, compare, branch away, and end the straight
    /// line with a literal — while the arm the branch takes ends with a different one.
    /// </para>
    /// </summary>
    [Fact]
    public void ALiteralOnTheStraightLineWithARoutineDownAnArmIsNotAConstant()
    {
        byte[] image = Blank();

        Put(image, 0x1000, SpecialVar, Lo(Answer), Hi(Answer), 0x84, 0x00);
        Put(image, 0x1005, Compare, Lo(Answer), Hi(Answer), 2, 0);
        Put(image, 0x100A, GotoIf, 0x00);
        Address(image, 0x100C, 0x1100);
        Put(image, 0x1010, SetVar, Lo(Answer), Hi(Answer), 1, 0, Return);

        // The arm, ending in the other answer.
        Put(image, 0x1100, SetVar, Lo(Answer), Hi(Answer), 0, 0, Return);

        Assert.Equal((SpecialCalls.LeftBehind.ANumberOnTheStraightLine, 1), Left(image));
    }

    /// <summary>
    /// A BLOCK THAT ENDS BY JUMPING SOMEWHERE ELSE DID NOT LEAVE THE VARIABLE ALONE — the
    /// reading stopped.
    /// <para>
    /// Those are different facts and printing them both as "nothing" is the same conflation
    /// this family of rules has been caught by three times. Nine of the cartridge's 336 are
    /// this, and they were sitting in the forty-nine that "leave the answer alone".
    /// </para>
    /// </summary>
    [Fact]
    public void ABlockThatEndsByJumpingAwayIsNotABlockThatLeftItAlone()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Goto);
        Address(image, 0x1001, 0x1200);
        Put(image, 0x1200, SetVar, Lo(Answer), Hi(Answer), 4, 0, Return);

        Assert.Equal((SpecialCalls.LeftBehind.WentSomewhereElse, 0), Left(image));
    }

    /// <summary>
    /// EVERYTHING A BLOCK CAN RETURN, and what the choice turns on.
    /// <para>
    /// 217 could say fifty-seven places call a block that is not a constant and could not say
    /// what it returns instead. The cartridge's is <c>0x081BB79C</c>: nought or one, turning on
    /// <c>0x083</c> and then <c>0x153</c>.
    /// </para>
    /// </summary>
    [Fact]
    public void WhatABlockCanReturnIncludesItsArmsAndWhatChoosesBetweenThem()
    {
        byte[] image = Blank();

        Put(image, 0x1000, SpecialVar, Lo(Answer), Hi(Answer), 0x84, 0x00);
        Put(image, 0x1005, Compare, Lo(Answer), Hi(Answer), 2, 0);
        Put(image, 0x100A, GotoIf, 0x00);
        Address(image, 0x100C, 0x1100);
        Put(image, 0x1010, SetVar, Lo(Answer), Hi(Answer), 1, 0, Return);

        Put(image, 0x1100, SetVar, Lo(Answer), Hi(Answer), 0, 0, Return);

        SpecialCalls.WhatItCanReturn can =
            SpecialCalls.Returns(new Rom(image), Rom.BaseAddress + 0x1000);

        Assert.Equal(new[] { 0, 1 }, can.Answers.Select(a => a.Who).Order());
        Assert.Equal(new[] { 0x84 }, can.Deciders);
    }

    /// <summary>
    /// A block with no branch returns one thing and nothing chooses — the ordinary case, without
    /// which "it returns two things" passes on an instrument that always says two.
    /// </summary>
    [Fact]
    public void ABlockWithNoBranchReturnsOneThingAndNothingChooses()
    {
        byte[] image = Blank();

        Put(image, 0x1000, SetVar, Lo(Answer), Hi(Answer), 7, 0, Return);

        SpecialCalls.WhatItCanReturn can =
            SpecialCalls.Returns(new Rom(image), Rom.BaseAddress + 0x1000);

        Assert.Equal(7, Assert.Single(can.Answers).Who);
        Assert.Empty(can.Deciders);
    }

    /// <summary>
    /// The decider is the routine asked LAST before the branch, not the first one in the block.
    /// <para>
    /// A block that asks two things and branches on the second is the shape the cartridge uses,
    /// and crediting the first is the same off-by-one the barrier list exists to stop.
    /// </para>
    /// </summary>
    [Fact]
    public void TheDeciderIsWhateverWasAskedLastBeforeTheBranch()
    {
        byte[] image = Blank();

        Put(image, 0x1000, SpecialVar, Lo(Answer), Hi(Answer), 0x11, 0x00);
        Put(image, 0x1005, SpecialVar, Lo(Answer), Hi(Answer), 0x22, 0x00);
        Put(image, 0x100A, Compare, Lo(Answer), Hi(Answer), 2, 0);
        Put(image, 0x100F, GotoIf, 0x00);
        Address(image, 0x1011, 0x1100);
        Put(image, 0x1015, SetVar, Lo(Answer), Hi(Answer), 1, 0, Return);

        Put(image, 0x1100, SetVar, Lo(Answer), Hi(Answer), 0, 0, Return);

        Assert.Equal(
            new[] { 0x22 },
            SpecialCalls.Returns(new Rom(image), Rom.BaseAddress + 0x1000).Deciders);
    }

    /// <summary>
    /// WALKING BACK, which is only allowed when the call provably leaves the variable alone.
    /// <para>
    /// A call that touches nothing means the compare after it reads something older, so the
    /// older answer is the right attribution rather than a guess. 214's barrier stops the scan
    /// guessing; this is the case where it does not have to guess.
    /// </para>
    /// </summary>
    private static (SpecialCalls.LeftBehind Left, int Who) Before(byte[] image, int callAt)
    {
        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        int at = commands.FindIndex(c => c.Offset == callAt);

        Assert.True(at >= 0, $"the fixture has to decode the call at 0x{callAt:X4}");

        return SpecialCalls.WhatAnsweredBefore(commands, at);
    }

    /// <summary>The routine asked before the call is what the compare after it reads.</summary>
    [Fact]
    public void TheRoutineAskedBeforeTheCallIsWhatTheCompareReads()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x1C, 0x00);
        Put(image, 0x1003, Call);
        Address(image, 0x1004, 0x1200);
        Put(image, 0x1008, Compare, Lo(Answer), Hi(Answer), 0, 0, Return);
        Put(image, 0x1200, Return);

        Assert.Equal((SpecialCalls.LeftBehind.ARoutine, 0x1C), Before(image, 0x1003));
    }

    /// <summary>
    /// And a command that cannot have answered is walked over rather than stopping the walk.
    /// <para>
    /// Without this the walk stops at the first thing it meets and the instrument reports
    /// "nothing answered" for every call with a <c>lock</c> in front of it — which is the same
    /// answer as being careful, and a completely different rule.
    /// </para>
    /// </summary>
    [Fact]
    public void SomethingThatCannotHaveAnsweredIsWalkedOver()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x1C, 0x00);
        Put(image, 0x1003, Lock);
        Put(image, 0x1004, Call);
        Address(image, 0x1005, 0x1200);
        Put(image, 0x1009, Compare, Lo(Answer), Hi(Answer), 0, 0, Return);
        Put(image, 0x1200, Return);

        Assert.Equal((SpecialCalls.LeftBehind.ARoutine, 0x1C), Before(image, 0x1004));
    }

    /// <summary>
    /// A ROUTINE ANSWERING INTO SOME OTHER SLOT STOPS THE WALK RATHER THAN BEING CREDITED.
    /// <para>
    /// It did not answer this compare — its answer went elsewhere — but it RAN, and nothing
    /// here can say it left the answer variable alone. The forward scan treats a
    /// <c>specialvar</c> as a barrier for exactly that reason, and walking back has to be at
    /// least as careful. So this is "the reading stopped", not "nothing answered".
    /// </para>
    /// </summary>
    [Fact]
    public void ARoutineAnsweringElsewhereStopsTheWalkWithoutBeingCredited()
    {
        byte[] image = Blank();

        Put(image, 0x1000, SpecialVar, Lo(SomeOtherSlot), Hi(SomeOtherSlot), 0x1C, 0x00);
        Put(image, 0x1005, Call);
        Address(image, 0x1006, 0x1200);
        Put(image, 0x100A, Compare, Lo(Answer), Hi(Answer), 0, 0, Return);
        Put(image, 0x1200, Return);

        Assert.Equal((SpecialCalls.LeftBehind.WentSomewhereElse, 0), Before(image, 0x1005));
    }

    /// <summary>
    /// AND A SECOND CALL STOPS THE WALK RATHER THAN BEING FOLLOWED — one level, in this
    /// direction too.
    /// </summary>
    [Fact]
    public void AnotherCallStopsTheWalkRatherThanBeingFollowed()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Call);
        Address(image, 0x1001, 0x1300);
        Put(image, 0x1005, Call);
        Address(image, 0x1006, 0x1200);
        Put(image, 0x100A, Compare, Lo(Answer), Hi(Answer), 0, 0, Return);
        Put(image, 0x1200, Return);
        Put(image, 0x1300, Special, 0x1C, 0x00, Return);

        Assert.Equal((SpecialCalls.LeftBehind.WentSomewhereElse, 0), Before(image, 0x1005));
    }

    /// <summary>
    /// AND THE WALK BACK ONLY HAPPENS WHEN THE CALL LEFT THE VARIABLE ALONE.
    /// <para>
    /// That condition is the whole licence for walking back at all: it is a reading when the
    /// call provably touched nothing and a guess otherwise. Where the call answered, the older
    /// answer is not the one being read and claiming it would be crediting a routine with
    /// another's reply — which is the fault this entire family of rules exists to stop.
    /// </para>
    /// </summary>
    [Fact]
    public void TheWalkBackOnlyHappensWhenTheCallLeftTheVariableAlone()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x1C, 0x00);
        Put(image, 0x1003, Call);
        Address(image, 0x1004, 0x1200);
        Put(image, 0x1008, Compare, Lo(Answer), Hi(Answer), 0, 0, Return);
        Put(image, 0x1200, Return);

        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        int at = commands.FindIndex(c => c.Offset == 0x1003);

        Assert.Equal(
            (SpecialCalls.LeftBehind.ARoutine, 0x1C),
            SpecialCalls.OlderAnswer(SpecialCalls.LeftBehind.Nothing, commands, at));

        Assert.Equal(
            (SpecialCalls.LeftBehind.Nothing, 0),
            SpecialCalls.OlderAnswer(SpecialCalls.LeftBehind.ARoutine, commands, at));

        Assert.Equal(
            (SpecialCalls.LeftBehind.Nothing, 0),
            SpecialCalls.OlderAnswer(SpecialCalls.LeftBehind.WentSomewhereElse, commands, at));
    }

    /// <summary>
    /// A block nothing can be read from leaves nothing, which is what stops an unreadable
    /// address being reported as an answer.
    /// </summary>
    [Fact]
    public void AnUnreadableBlockLeavesNothing()
    {
        byte[] image = Blank();

        Put(image, 0x1000, End);

        Assert.Equal((SpecialCalls.LeftBehind.Nothing, 0), Left(image));
        Assert.Equal(
            (SpecialCalls.LeftBehind.Nothing, 0),
            SpecialCalls.WhatACallLeaves(new Rom(image), 0));
    }
}
