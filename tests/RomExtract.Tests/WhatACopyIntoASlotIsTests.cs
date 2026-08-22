using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a copy into an argument slot is worth (297) — 296's own stated caveat, measured.
/// <para>
/// 296 records a <c>setvar</c> and nothing else, so a value copied into a slot is invisible as an
/// argument. It wrote that down and put no number on it. The number is <b>26 places and 12
/// routines</b>, and the floor refuses all three kinds: the same walk run FORWARD, where nothing
/// can be an argument to the call it follows, finds copies into slots MORE often than the walk run
/// back does — 0.50, 1.33 and 0.29 against the plain <c>setvar</c>'s 2.46.
/// </para>
/// </summary>
public sealed class WhatACopyIntoASlotIsTests
{
    private const byte SetVar = 0x16;
    private const byte CopyVar = 0x19;
    private const byte End = 0x02;
    private const byte Goto = 0x05;
    private const int Slot = 0x8004;
    private const int Routine = 0x194;

    private static byte[] Word(int value) => [(byte)value, (byte)(value >> 8)];

    private static byte[] Puts(int value) => [SetVar, .. Word(Slot), .. Word(value)];

    private static byte[] Copies(int into, int from) => [CopyVar, .. Word(into), .. Word(from)];

    private static byte[] Calls(int routine = Routine) => [SpecialCalls.Special, .. Word(routine)];

    private static byte[] Pointer(int to) =>
    [
        (byte)to, (byte)(to >> 8), (byte)((Rom.BaseAddress + (uint)to) >> 16),
        (byte)((Rom.BaseAddress + (uint)to) >> 24),
    ];

    private static Rom Image(params byte[][] script)
    {
        var image = new byte[0x1000];

        script.SelectMany(b => b).ToList().CopyTo(image, 0x200);

        return new Rom(image);
    }

    private static List<AWriteIntoASlot> Writes(Rom rom) =>
        WhatACopyIntoASlotIs.In(rom, "1.0", Rom.BaseAddress + 0x200);

    // ------------------------------------------------------------------ what kind a write is

    /// <summary>
    /// <b>THE BANDS, and the fixture uses one of each.</b> A variable id in this cartridge is
    /// <c>0x4000</c> upwards or <c>0x8000</c> upwards, so a copy's second word BELOW <c>0x4000</c>
    /// is not a variable at all and can only be a number. Every kind is named, so "four kinds"
    /// cannot be satisfied by whatever the code happens to have (251, trap 35).
    /// </summary>
    [Theory]
    [InlineData(0x0000, WhereTheValueCameFrom.ALiteral)]
    [InlineData(0x3FFF, WhereTheValueCameFrom.ALiteral)]
    [InlineData(0x4000, WhereTheValueCameFrom.TheSave)]
    [InlineData(0x7FFF, WhereTheValueCameFrom.TheSave)]
    [InlineData(0x8000, WhereTheValueCameFrom.AnotherSlot)]
    [InlineData(0x800D, WhereTheValueCameFrom.AnotherSlot)]
    public void ACopyIsNamedByTheBandItsSourceFallsIn(int source, WhereTheValueCameFrom expected)
    {
        AWriteIntoASlot write = Assert.Single(Writes(Image(Copies(Slot, source), Calls(), [End])));

        Assert.Equal(expected, write.From);
    }

    /// <summary>
    /// And a <c>setvar</c> is its own kind whatever its value looks like — it is the row whose
    /// answer is known, so putting it in any of the copy buckets would move the calibration.
    /// </summary>
    [Fact]
    public void ASetVarIsItsOwnKindEvenWhenItsValueLooksLikeAVariable()
    {
        AWriteIntoASlot write = Assert.Single(Writes(Image(Puts(0x4001), Calls(), [End])));

        Assert.Equal(WhereTheValueCameFrom.ASetVar, write.From);
    }

    /// <summary>
    /// The answer variable is marked, because the second table takes it out of BOTH columns: a
    /// script moving a routine's reply about is not handing anything over, and 33 of the 45
    /// behind-a-call copies out of another slot are that.
    /// </summary>
    [Fact]
    public void ACopyOfTheAnswerVariableIsMarked()
    {
        Assert.True(Assert.Single(Writes(Image(Copies(Slot, 0x800D), Calls(), [End]))).TheAnswer);
        Assert.False(Assert.Single(Writes(Image(Copies(Slot, 0x800C), Calls(), [End]))).TheAnswer);
    }

    // ------------------------------------------------------------------- the walk, both ways

    /// <summary>
    /// <b>THE THING.</b> The same write is found on both sides of a call and says which side it is
    /// on. That is the whole floor: nothing behind a call can be an argument to it, so a kind that
    /// IS an argument has to be commoner in front than behind.
    /// </summary>
    [Fact]
    public void AWriteIsFoundOnEitherSideAndSaysWhichSide()
    {
        List<AWriteIntoASlot> writes = Writes(Image(Copies(Slot, 0x4001), Calls(), Copies(0x8005, 0x4002), [End]));

        Assert.Equal(2, writes.Count);
        Assert.Equal([true, false], writes.Select(w => w.Before));
        Assert.Equal([Slot, 0x8005], writes.Select(w => w.Slot));
    }

    /// <summary>
    /// <b>And the forward walk stops at the NEXT call.</b> Without it the floor swallows the
    /// following call's arguments and the behind-a-call column is inflated by exactly the thing
    /// the in-front column is counting — which would make the ratio smaller and the verdict look
    /// better founded than it is.
    /// </summary>
    [Fact]
    public void TheForwardWalkStopsAtTheNextCall()
    {
        Rom rom = Image(Calls(), Calls(0x100), Copies(Slot, 0x4001), [End]);

        // Two calls, and the copy belongs to neither: it is behind the second and the first
        // cannot see past it.
        Assert.Equal([false], Writes(rom).Select(w => w.Before));
        Assert.Equal([0x100], Writes(rom).Select(w => w.Routine));
    }

    /// <summary>
    /// And the forward walk stops at a gap, exactly as the backward one does. The two rules are
    /// mirrored rather than written twice, so a fixture on one side is not a fixture on the other
    /// — the walk runs in either direction from ONE loop (53).
    /// </summary>
    [Fact]
    public void TheForwardWalkStopsAtAGap()
    {
        var image = new byte[0x1000];

        List<byte> first = [.. Calls(), Goto, .. Pointer(0x280)];

        first.CopyTo(image, 0x200);

        List<byte> second = [.. Copies(Slot, 0x4001), End];

        second.CopyTo(image, 0x280);

        var rom = new Rom(image);

        // The read reaches the second block, so the copy really is in the command list.
        Assert.Contains(ScriptReader.ReadAll(rom, Rom.BaseAddress + 0x200), c => c.Code == CopyVar);

        Assert.Empty(Writes(rom));
    }

    /// <summary>
    /// <b>And a slot something nearer the call already spent is not counted either</b> — 296's own
    /// rule, asked of the other commands that write one. Without it the reading would count a
    /// value that had already been taken and report a routine as handed something it never sees.
    /// </summary>
    [Fact]
    public void ASlotSomethingNearerAlreadyTookIsNotCounted()
    {
        // copyvar 0x8004, 0x4001 ; copyvar 0x4002, 0x8004 ; special — the second command reads
        // the slot, so the first one's value is not this call's.
        Assert.Empty(Writes(Image(Copies(Slot, 0x4001), Copies(0x4002, Slot), Calls(), [End])));
    }

    // --------------------------------------------------------------------------- the counting

    private static AWriteIntoASlot At(int at, int routine, int source, bool before) =>
        new(at, "1.0", routine, CopyVar, Slot, source, before);

    /// <summary>
    /// <b>PLACES, NOT RECORDS.</b> A block hanging off nineteen maps is nineteen records at one
    /// byte position, and this repository has now printed the first and read it as the second
    /// three times (224, 241, 291). The busiest row in this reading is one address seen sixty-eight
    /// times.
    /// </summary>
    [Fact]
    public void TheCountsAreByBytePositionAndNotByRecord()
    {
        List<AWriteIntoASlot> same = [At(0x100, Routine, 0x4001, true), At(0x100, Routine, 0x4001, true)];

        HowOftenBesideACall row = Assert.Single(
            WhatACopyIntoASlotIs.Read(same, new HashSet<int>()),
            r => r.From == WhereTheValueCameFrom.TheSave);

        Assert.Equal(1, row.Before);
    }

    /// <summary>
    /// And the NEW column is what adopting a kind would actually cost — a routine 296 already
    /// reads as handed a value is not new. Trap 9: a count of how wrong something COULD be is not
    /// a count of how wrong it is.
    /// <para>
    /// The fixture has two routines and only the SECOND is already known, so a version that reads
    /// the first row and stops passes nothing (119).
    /// </para>
    /// </summary>
    [Fact]
    public void TheNewColumnLeavesOutRoutinesAlreadyHandedAValue()
    {
        List<AWriteIntoASlot> writes =
            [At(0x100, 0x0001, 0x4001, true), At(0x200, 0x0002, 0x4001, true)];

        HowOftenBesideACall row = Assert.Single(
            WhatACopyIntoASlotIs.Read(writes, new HashSet<int> { 0x0002 }),
            r => r.From == WhereTheValueCameFrom.TheSave);

        Assert.Equal(2, row.Routines);
        Assert.Equal(1, row.New);
    }

    /// <summary>
    /// The ratio is in front over behind, and a kind that never appears behind a call is
    /// unbounded rather than nought — which is a different fact and has to read as one.
    /// </summary>
    [Fact]
    public void TheRatioIsInFrontOverBehind()
    {
        List<AWriteIntoASlot> writes =
        [
            At(0x100, 0x0001, 0x4001, true),
            At(0x200, 0x0001, 0x4001, true),
            At(0x300, 0x0001, 0x4001, false),
        ];

        HowOftenBesideACall row = Assert.Single(
            WhatACopyIntoASlotIs.Read(writes, new HashSet<int>()),
            r => r.From == WhereTheValueCameFrom.TheSave);

        Assert.Equal(2.0, row.Ratio);
        Assert.True(
            double.IsPositiveInfinity(
                Assert.Single(
                    WhatACopyIntoASlotIs.Read(
                        writes.Where(w => w.Before).ToList(), new HashSet<int>()),
                    r => r.From == WhereTheValueCameFrom.TheSave).Ratio));
    }

    /// <summary>
    /// And taking the answer variable out takes it out of BOTH columns. Out of one only, the
    /// ratio moves for a reason that is about the arithmetic rather than about the cartridge.
    /// </summary>
    [Fact]
    public void TakingTheAnswerOutTakesItOutOfBothColumns()
    {
        List<AWriteIntoASlot> writes =
        [
            At(0x100, 0x0001, WhatACopyIntoASlotIs.TheAnswer, true),
            At(0x200, 0x0001, WhatACopyIntoASlotIs.TheAnswer, false),
            At(0x300, 0x0001, 0x8001, true),
        ];

        HowOftenBesideACall row = Assert.Single(
            WhatACopyIntoASlotIs.Read(writes, new HashSet<int>(), countTheAnswer: false),
            r => r.From == WhereTheValueCameFrom.AnotherSlot);

        Assert.Equal(1, row.Before);
        Assert.Equal(0, row.After);
    }
}
