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
