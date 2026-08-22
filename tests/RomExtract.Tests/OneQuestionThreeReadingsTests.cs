using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// One question, three readings of it (298).
/// <para>
/// "The run of commands before a call" was asked in three places in this repository, each with its
/// own <c>Window = 4</c> and its own barriers. 295 and 296 replaced the distance in
/// <see cref="SpecialCalls"/> with two rules read off the script; the other two were never touched
/// — so <c>--routines</c> printed <b>37</b> routines handed a value in one section and named
/// <b>44</b> in the column below it, in one output, and nothing compared them.
/// </para>
/// <para>
/// They disagree at <b>39</b> and <b>13</b> of the same 936 places. Both are the shared reading
/// now, and the readings they replaced are kept so the size of the correction is printed rather
/// than asserted.
/// </para>
/// </summary>
public sealed class OneQuestionThreeReadingsTests
{
    private static ScriptCommand SetVar(int at, int variable, int value) =>
        new(at, 0x16,
            [(byte)(variable & 0xFF), (byte)(variable >> 8), (byte)(value & 0xFF), (byte)(value >> 8)]);

    private static ScriptCommand Special(int at, int routine) =>
        new(at, 0x25, [(byte)(routine & 0xFF), (byte)(routine >> 8)]);

    private static ScriptCommand CopyVar(int at, int into, int from) =>
        new(at, 0x19,
            [(byte)(into & 0xFF), (byte)(into >> 8), (byte)(from & 0xFF), (byte)(from >> 8)]);

    /// <summary>
    /// <b>THE THING, and it is the cartridge's own shape.</b> <c>2.1</c> at <c>0x1C510D</c> is
    /// <c>setvar 0x8004, 1 ; setvar 0x8005, 2 ; copyvar 0x8006, 0x4003 ; special 0x0194</c>. The
    /// two readings this repository had both stopped short of the values: one counted a distance
    /// of four with no barrier and the other stopped dead at the <c>copyvar</c>.
    /// <para>
    /// The fixture puts the <c>copyvar</c> where the cartridge does — between the values and the
    /// call — because that is the position both old readings mishandle and neither would have been
    /// caught by a run of plain <c>setvar</c>s.
    /// </para>
    /// </summary>
    [Fact]
    public void TheThreeReadingsOfTheSameRunAreNotTheSameReading()
    {
        List<ScriptCommand> run =
        [
            SetVar(0x100, 0x8004, 1),
            SetVar(0x105, 0x8005, 2),
            CopyVar(0x10A, 0x8006, 0x4003),
            Special(0x10F, 0x194),
        ];

        Assert.Equal(
            [(0x8004, 1), (0x8005, 2)],
            SpecialCalls.ArgumentsBefore(run, 3));

        // What each replaced reading said about the very same four commands.
        Assert.Equal(2, SpecialContracts.TheCrudeReading(run, 3));

        Assert.Equal(WhatIsWaitedFor.NoSelector, WhatIsWaitedFor.TheCrudeReading(run, 3));

        // NOTE the contract reading is NOT asserted here: on this run it walks past the copyvar
        // and counts both values, so it agrees with the rules and this fixture cannot see it.
        // It is asserted on the run below, where the two disagree — a fixture built on the shape
        // where two readings agree cannot tell them apart (fixture-lie 5, and 297 sprang it too).

        // And what they say now, which is one answer rather than three.
        Assert.Equal(1, WhatIsWaitedFor.SelectorBefore(run, 3));
    }

    /// <summary>
    /// And the two replaced readings are wrong in OPPOSITE directions, which is why comparing them
    /// to each other would have caught this and comparing either to nothing did not: the contract
    /// count credits more than the rules do at 39 places and fewer at 4.
    /// </summary>
    [Fact]
    public void TheTwoReplacedReadingsMissInOppositeDirections()
    {
        // A value four commands back with an ordinary command in between: the crude contract
        // reading walks past it and counts it, and so do the rules.
        List<ScriptCommand> near =
        [
            SetVar(0x100, 0x8004, 1),
            Special(0x105, 0x194),
        ];

        Assert.Equal(1, SpecialContracts.TheCrudeReading(near, 1));
        Assert.Single(SpecialCalls.ArgumentsBefore(near, 1));

        // A slot something else READ in between: the crude reading counts it and the rules do not.
        List<ScriptCommand> spent =
        [
            SetVar(0x100, 0x8004, 1),
            CopyVar(0x105, 0x4001, 0x8004),
            Special(0x10A, 0x194),
        ];

        Assert.Equal(1, SpecialContracts.TheCrudeReading(spent, 2));
        Assert.Empty(SpecialCalls.ArgumentsBefore(spent, 2));

        // And THIS is where the contract reading is pinned to the shared one. It lives inside a
        // sweep that needs a whole cartridge, so without an assertion here the only guard on it
        // would be one no fixture can reach — this repository's most repeated structural fault
        // (219, 221, 222, 223).
        Assert.Equal(0, SpecialContracts.Arguments(spent, 2));
        Assert.NotEqual(SpecialContracts.TheCrudeReading(spent, 2), SpecialContracts.Arguments(spent, 2));
    }

    /// <summary>
    /// <b>THE FORWARD HALF HAS NO DISTANCE EITHER (298).</b> It stopped four commands past a call
    /// and nothing had asked whether four was enough — every sentence this project has written
    /// about what an answer is COMPARED AGAINST was bounded by it. Swept, it plateaus at three,
    /// so the distance was deciding nothing and is gone; what bounds the walk is the barrier list
    /// and contiguity, both read off the script.
    /// <para>
    /// The compare here is <b>six</b> commands past the call with nothing that answers in between,
    /// which is outside the old window and inside no window at all now. Nothing in the suite
    /// reached that axis before this: every other fixture puts its compare next to the call.
    /// </para>
    /// </summary>
    [Fact]
    public void ACompareFarPastACallIsStillItsCompare()
    {
        var image = new byte[0x1000];

        List<byte> script =
        [
            0x25, 0x94, 0x01,                                       // special 0x194
            0x67, 0, 0, 0, 0,                                       // five ordinary commands
            0x67, 0, 0, 0, 0,
            0x67, 0, 0, 0, 0,
            0x67, 0, 0, 0, 0,
            0x67, 0, 0, 0, 0,
            0x21, 0x0D, 0x80, 0x07, 0x00,                           // compare 0x800D, 7
            0x02,
        ];

        script.CopyTo(image, 0x200);

        var rom = new Rom(image);

        SpecialCall call = Assert.Single(
            SpecialCalls.In(rom, "1.0", "person", Rom.BaseAddress + 0x200));

        Assert.Equal([(7, (byte)0xFF)], call.Compared);

        static IReadOnlyList<(int Value, byte Condition)> Compared(Rom rom, int forward) =>
            Assert.Single(
                SpecialCalls.In(
                    rom, "1.0", "person", Rom.BaseAddress + 0x200, SpecialCalls.NoLimit, forward))
                .Compared;

        // And at the setting 291-297 were measured at, it is not found at all — which is what
        // makes the sweep a reading rather than a restatement.
        Assert.Empty(Compared(rom, SpecialCalls.Window));

        // AND THE BOUNDARY IS EXACT. The sweep in 298's table has the forward window as its axis,
        // so an off-by-one there shifts every row — and an off-by-one is invisible at the default,
        // where the window is four thousand million and the comparison can never bind. Nothing in
        // the suite reached it: a break moving `<=` to `<` came back green (219, third time).
        Assert.Empty(Compared(rom, 5));
        Assert.Equal([(7, (byte)0xFF)], Compared(rom, 6));
    }

    /// <summary>
    /// <b>And the crude readings keep their own window</b>, so the sweep that shows the backward
    /// distance never plateaued can still be run against them. Without the parameter the old
    /// reading is a single number and 294's question cannot be asked of it at all.
    /// </summary>
    [Fact]
    public void TheReplacedReadingStillTakesItsWindow()
    {
        List<ScriptCommand> run =
        [
            SetVar(0x100, 0x8004, 1),
            SetVar(0x105, 0x8005, 2),
            SetVar(0x10A, 0x8006, 3),
            SetVar(0x10F, 0x8007, 4),
            Special(0x114, 0x194),
        ];

        Assert.Equal(1, SpecialContracts.TheCrudeReading(run, 4, window: 1));
        Assert.Equal(3, SpecialContracts.TheCrudeReading(run, 4, window: 3));
        Assert.Equal(4, SpecialContracts.TheCrudeReading(run, 4, window: 96));
    }
}
