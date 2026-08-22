using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Following a <c>call</c> one level in the ARGUMENT direction (300).
/// <para>
/// 214 made a plain <c>call</c> a barrier in the answer scan because the block it jumps into can
/// answer. The argument scan has no such barrier and the same argument applies — a called block can
/// write an argument slot exactly as it can write the answer variable — and 298 printed the count
/// of places where that could have happened without following one.
/// </para>
/// <para>
/// <b>The finding is the negative.</b> Of the thirteen, NOUGHT is a value the block overwrote: five
/// call something of their own and are unread, three are conditional jumps the call is reached past
/// with the value intact, five are calls whose blocks write no slot at all, and there is not one
/// <c>goto</c> among them.
/// </para>
/// </summary>
public sealed class WhatACallWritesTests
{
    private const byte Filler = 0x77;
    private const byte End = 0x02;
    private const byte Return = 0x03;
    private const byte SetVar = 0x16;
    private const byte CopyVar = 0x19;

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

    /// <summary>
    /// <b>THE THING.</b> A block that puts a number in an argument slot has written it, and a call
    /// to that block between a value and the call it is credited to means the credit is wrong.
    /// <para>
    /// Two slots, and only ONE of them is the one asked about — a version answering "did it write
    /// anything" rather than "did it write THIS" passes a fixture with one.
    /// </para>
    /// </summary>
    [Fact]
    public void ABlockThatWritesASlotIsFound()
    {
        byte[] image = Blank();

        Put(image, 0x1000, SetVar, 0x04, 0x80, 0x09, 0x00);
        Put(image, 0x1005, SetVar, 0x06, 0x80, 0x01, 0x00);
        Put(image, 0x100A, Return);

        (IReadOnlySet<int> slots, bool calls) =
            SpecialCalls.WhichSlotsACallWrites(new Rom(image), Rom.BaseAddress + 0x1000);

        Assert.Equal([0x8004, 0x8006], slots.Order());
        Assert.False(calls);
    }

    /// <summary>
    /// And a COPY into a slot is a write too — the destination, not the source. 251 found
    /// <c>copyvar</c>'s destination missing from both of this repository's write tables and 252
    /// found two more operands in neither; this is the fourth list of the same shape and it takes
    /// the measured one rather than a fresh guess.
    /// </summary>
    [Fact]
    public void ACopyIntoASlotIsAWriteAndTheSourceIsNot()
    {
        byte[] image = Blank();

        // copyvar 0x8004, 0x8009 ; return — the DESTINATION is written, the source is read.
        Put(image, 0x1000, CopyVar, 0x04, 0x80, 0x09, 0x80);
        Put(image, 0x1005, Return);

        (IReadOnlySet<int> slots, _) =
            SpecialCalls.WhichSlotsACallWrites(new Rom(image), Rom.BaseAddress + 0x1000);

        Assert.Equal([0x8004], slots);
    }

    /// <summary>
    /// <b>And a block that calls something of its own says so rather than being chased.</b> That is
    /// the does-not-know column, and on the cartridge it is 5 of the 13 — the whole of what is
    /// left of 298's error bar once the rest resolve.
    /// </summary>
    [Fact]
    public void ABlockThatCallsSomethingSaysSo()
    {
        byte[] image = Blank();

        // special 0x1C ; return — nothing written, but this reading cannot see what it does.
        Put(image, 0x1000, SpecialCalls.Special, 0x1C, 0x00);
        Put(image, 0x1003, Return);

        (IReadOnlySet<int> slots, bool calls) =
            SpecialCalls.WhichSlotsACallWrites(new Rom(image), Rom.BaseAddress + 0x1000);

        Assert.Empty(slots);
        Assert.True(calls);

        // And an ordinary block does not claim it does.
        byte[] plain = Blank();

        Put(plain, 0x1000, SetVar, 0x01, 0x40, 0x01, 0x00);
        Put(plain, 0x1005, End);

        Assert.False(SpecialCalls.WhichSlotsACallWrites(new Rom(plain), Rom.BaseAddress + 0x1000)
            .CallsSomething);
    }

    /// <summary>
    /// A write to the SAVE's own numbers is not a write to an argument slot. Without this every
    /// block in the game writes something and the reading answers yes before it is asked (50).
    /// </summary>
    [Fact]
    public void AWriteToTheSavesOwnNumbersIsNotASlot()
    {
        byte[] image = Blank();

        Put(image, 0x1000, SetVar, 0x01, 0x40, 0x09, 0x00);
        Put(image, 0x1005, Return);

        Assert.Empty(
            SpecialCalls.WhichSlotsACallWrites(new Rom(image), Rom.BaseAddress + 0x1000).Slots);
    }

    // ------------------------------------------------ the barrier that was not in the run

    /// <summary>
    /// <b>A DECOY, because the cartridge holds no counterexample (300).</b>
    /// <c>WhoTheCompareBelongsTo.WhatStoodInTheWay</c> had no contiguity check at all, and 299 took
    /// the distance off it — so the walk could run off the end of one script and name a thing in
    /// the way belonging to whatever block the reader concatenated next.
    /// <para>
    /// Measured, it costs NOUGHT: all 140 sorted sites name something in their own run. So no
    /// break can be aimed at the rule from the cartridge, and a rule the cartridge never exercises
    /// is a rule that needs a fixture carrying it (57).
    /// </para>
    /// </summary>
    [Fact]
    public void AThingInTheWayHasToBeInTheSameRun()
    {
        byte[] image = Blank();

        // special 0x1C ; goto 0x1200 — and at 0x1200, in a block of its own, another special.
        Put(image, 0x1000, SpecialCalls.Special, 0x1C, 0x00);
        Put(image, 0x1003, 0x05);
        Address(image, 0x1004, 0x1200);
        Put(image, 0x1200, SpecialCalls.Special, 0x1D, 0x00);
        Put(image, 0x1203, End);

        var rom = new Rom(image);

        List<ScriptCommand> commands = ScriptReader.ReadAll(rom, Rom.BaseAddress + 0x1000);

        // The reader reaches the second block, so the second special really is in the list.
        Assert.Equal(2, commands.Count(c => c.Code == SpecialCalls.Special));

        Assert.Null(WhoTheCompareBelongsTo.WhatStoodInTheWay(rom, commands, 0));

        // And the same two commands CONTIGUOUS are found, so the fixture is about the gap rather
        // than about the second special being unreachable.
        byte[] touching = Blank();

        Put(touching, 0x1000, SpecialCalls.Special, 0x1C, 0x00);
        Put(touching, 0x1003, SpecialCalls.Special, 0x1D, 0x00);
        Put(touching, 0x1006, End);

        var second = new Rom(touching);

        Assert.NotNull(
            WhoTheCompareBelongsTo.WhatStoodInTheWay(
                second, ScriptReader.ReadAll(second, Rom.BaseAddress + 0x1000), 0));
    }
}
