using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// How far in front of a call a value can be written and still be read as its argument (294).
/// <para>
/// <c>SpecialCalls.Before</c> has stopped four commands in front of a call since it was written,
/// and nothing had asked whether four is enough. <b>It never plateaus:</b> 44 routines are handed
/// a value at a window of four, 52 at eight, 62 at twenty-four, and it is still climbing there.
/// So 292's "44 of 178" is a property of the setting, not of the cartridge.
/// </para>
/// <para>
/// <b>What survives is 293's reading.</b> <c>0x194</c> is the only routine whose answer is
/// compared differently depending on the value it was handed at EVERY window from 1 to 24. Two
/// others appear and vanish with the setting — <c>0x0A3</c> at two and three, <c>0x0A4</c> at
/// twelve and above — and those are artefacts of a knob.
/// </para>
/// </summary>
public sealed class HowFarBackAnArgumentCountsTests
{
    private const byte SetVar = 0x16;
    private const byte End = 0x02;
    private const int Slot = 0x8004;
    private const int Routine = 0x194;

    private static byte[] Word(int value) => [(byte)value, (byte)(value >> 8)];

    /// <summary>A <c>setvar</c> putting a value in an argument slot.</summary>
    private static byte[] Puts(int value) => [SetVar, .. Word(Slot), .. Word(value)];

    /// <summary>A <c>special</c>, which is a code and a routine number.</summary>
    private static byte[] Calls() => [SpecialCalls.Special, .. Word(Routine)];

    /// <summary>
    /// An image whose script at 0x200 puts a value in the slot, runs <paramref name="between"/>
    /// filler <c>setvar</c>s into a slot nothing reads as an argument, and then calls.
    /// </summary>
    private static Rom Image(int between)
    {
        var image = new byte[0x1000];

        List<byte> script = [.. Puts(9)];

        // Filler that is adjacent and is not an argument: a setvar into the SAVE's own numbers,
        // which `Before` skips over rather than stopping at.
        for (var i = 0; i < between; i++) script.AddRange([SetVar, .. Word(0x4001), .. Word(i)]);

        script.AddRange(Calls());
        script.Add(End);

        script.CopyTo(image, 0x200);

        return new Rom(image);
    }

    private static IReadOnlyList<(int Variable, int Value)> Arguments(Rom rom, int window) =>
        Assert.Single(
            SpecialCalls.In(rom, "1.0", "person", Rom.BaseAddress + 0x200, window)
                .Where(c => c.Routine == Routine))
            .Arguments;

    /// <summary>
    /// <b>THE THING.</b> The same script, the same call, the same value — and whether the value is
    /// its argument depends on a constant in this repository rather than on anything in the
    /// cartridge.
    /// </summary>
    [Fact]
    public void AValueFiveCommandsBackIsFoundOnlyByAWiderWindow()
    {
        Rom rom = Image(between: 5);

        Assert.Empty(Arguments(rom, SpecialCalls.Window));
        Assert.Equal([(Slot, 9)], Arguments(rom, 8));
    }

    /// <summary>
    /// And one inside the window is found at the default — so the fixture above is about the
    /// DISTANCE and not about the filler being unreadable.
    /// </summary>
    [Fact]
    public void AValueInsideTheWindowIsFoundAtTheDefault()
    {
        Assert.Equal([(Slot, 9)], Arguments(Image(between: 2), SpecialCalls.Window));
    }

    /// <summary>
    /// A window of nought finds nothing at all, which is the floor of this whole sweep: every
    /// argument this project has ever read is read because somebody chose a number here.
    /// </summary>
    [Fact]
    public void AWindowOfNoughtFindsNoArguments()
    {
        Assert.Empty(Arguments(Image(between: 0), 0));
    }

    /// <summary>
    /// <b>And the window cannot reach across a gap.</b> The loop stops at the first command that
    /// is not contiguous with the next, so widening it only ever finds values inside one packed
    /// run — which is why the sweep climbs slowly rather than swallowing the whole script.
    /// </summary>
    [Fact]
    public void AWiderWindowStillStopsAtAGap()
    {
        // The value and a GOTO in one block, the call in another. Both blocks are in the read, so
        // the setvar is two commands in front of the call and well inside any window — and the
        // goto's last byte is not the call's first, so nothing joins them.
        //
        // The first version of this put the value in a block the read never reached at all, which
        // tests that an unread command is not found and says nothing about adjacency. It passed
        // while a version that skipped gaps instead of stopping at them also passed (119).
        var image = new byte[0x1000];

        List<byte> first = [.. Puts(9), Goto, .. Pointer(0x280)];

        first.CopyTo(image, 0x200);

        List<byte> second = [.. Calls(), End];

        second.CopyTo(image, 0x280);

        var rom = new Rom(image);

        // The read reaches both blocks, so the setvar really is in the command list.
        Assert.Contains(
            ScriptReader.ReadAll(rom, Rom.BaseAddress + 0x200),
            c => c.Code == SetVar);

        Assert.Empty(Arguments(rom, 24));
    }

    // ------------------------------------------------- the rule that replaced the knob (295)

    /// <summary>
    /// <b>A VALUE BELONGS TO THE FIRST CALL AFTER IT.</b> Without this the second call collects
    /// the first one's argument as well — which is the FAN CLUB on <c>14.9</c>, where a script
    /// sets the slot and asks <c>0x0A3</c> eight times over. It is a rule read off the script
    /// rather than a distance chosen in this repository, and under it the whole sweep converges
    /// at a window of twelve and stops moving.
    /// </summary>
    [Fact]
    public void AValueBelongsToTheFirstCallAfterIt()
    {
        var image = new byte[0x1000];

        List<byte> script = [.. Puts(9), .. Calls(), .. Calls(), End];

        script.CopyTo(image, 0x200);

        var rom = new Rom(image);

        IReadOnlyList<SpecialCall> calls =
            [.. SpecialCalls.In(rom, "1.0", "person", Rom.BaseAddress + 0x200)];

        Assert.Equal(2, calls.Count);
        Assert.Equal([(Slot, 9)], calls[0].Arguments);
        Assert.Empty(calls[1].Arguments);
    }

    /// <summary>
    /// <b>And a <c>specialvar</c> is a call too.</b> The barrier is both forms — a routine asked
    /// for its answer takes the argument in front of it exactly as one asked for its effect does.
    /// The first version of this fixture used only <c>special</c>, so a barrier that named one of
    /// the two passed it (119, fifth time in this session).
    /// </summary>
    [Fact]
    public void ASpecialVarIsACallToo()
    {
        var image = new byte[0x1000];

        // setvar 0x8004, 9 ; specialvar 0x800D <- 0x194 ; special 0x194 ; end
        List<byte> script =
        [
            .. Puts(9),
            SpecialCalls.SpecialVar, .. Word(0x800D), .. Word(Routine),
            .. Calls(),
            End,
        ];

        script.CopyTo(image, 0x200);

        IReadOnlyList<SpecialCall> calls =
        [
            .. SpecialCalls.In(new Rom(image), "1.0", "person", Rom.BaseAddress + 0x200),
        ];

        Assert.Equal(2, calls.Count);
        Assert.Equal([(Slot, 9)], calls[0].Arguments);
        Assert.Empty(calls[1].Arguments);
    }

    /// <summary>
    /// And the backward search is not bounded by a distance any more: the same value nine commands
    /// back is found at the default, because the default is <c>NoLimit</c> and the two READ rules
    /// do the bounding.
    /// </summary>
    [Fact]
    public void TheDefaultHasNoDistanceLimitAtAll()
    {
        Rom rom = Image(between: 9);

        Assert.Empty(Arguments(rom, SpecialCalls.Window));
        Assert.Equal([(Slot, 9)], Arguments(rom, SpecialCalls.NoLimit));

        // And NoLimit is what a caller gets without asking.
        Assert.Equal(
            [(Slot, 9)],
            Assert.Single(
                SpecialCalls.In(rom, "1.0", "person", Rom.BaseAddress + 0x200)
                    .Where(c => c.Routine == Routine))
                .Arguments);
    }

    private const byte Goto = 0x05;

    private static byte[] Pointer(int to) =>
    [
        (byte)to, (byte)(to >> 8), (byte)((Rom.BaseAddress + (uint)to) >> 16),
        (byte)((Rom.BaseAddress + (uint)to) >> 24),
    ];
}
