using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Reading an address: the bytes and what they read as, off the same command.
/// <para>
/// This project's stated method is to stop inferring and print the bytes, and until 232 there was
/// no command that printed them — 190, 199, 228 and 232 all hand-dumped an address and hand-copied
/// a width table into a scratch script to read it. A hexdump and a disassembly maintained
/// separately are two readings that can disagree; here they are one.
/// </para>
/// </summary>
public sealed class PrintingTheBytesTests
{
    /// <summary>
    /// The image is filled with a byte this project has NO width for, so any read that drifts by
    /// one stops immediately and loudly.
    /// </summary>
    /// <remarks>
    /// A zero-filled image is a nop slide — every <c>0x00</c> is a valid no-op, so a read at the
    /// wrong width walks sixty bytes to the target and the test passes for the wrong reason. That
    /// is fixture-lie 1, and it is the reason for the 0xFF.
    /// </remarks>
    private const byte NoWidth = 0xFF;

    private static Rom Image(params (int At, byte[] Bytes)[] pieces)
    {
        var data = new byte[0x1000];

        Array.Fill(data, NoWidth);

        foreach ((int at, byte[] bytes) in pieces) bytes.CopyTo(data, at);

        return new Rom(data);
    }

    private static byte[] Goto(uint to) => [0x05, .. BitConverter.GetBytes(to)];

    private static byte[] GotoIf(byte condition, uint to) =>
        [0x06, condition, .. BitConverter.GetBytes(to)];

    // ------------------------------------------------------- the bytes and the decode

    /// <summary>
    /// THE DISCRIMINATION: the printed bytes are the opcode AND its arguments, so the hexdump
    /// column and the name column cannot come apart. A reading that printed only the arguments
    /// would look right in every line whose opcode a reader already knows.
    /// </summary>
    [Fact]
    public void TheBytesAndTheDecodeComeOffTheSameCommand()
    {
        // setflag 0x0025 ; end
        ABlockRead.Block block = ABlockRead.One(
            Image((0x100, [0x29, 0x25, 0x00, 0x02])), 0x08000100);

        Assert.Equal(2, block.Lines.Count);

        Assert.Equal([0x29, 0x25, 0x00], block.Lines[0].Bytes);
        Assert.Equal("setflag", block.Lines[0].Name);
        Assert.Equal(0x100, block.Lines[0].Offset);

        Assert.Equal([0x02], block.Lines[1].Bytes);
        Assert.Equal("end", block.Lines[1].Name);
    }

    // ----------------------------------------------------------------- where it stopped

    /// <summary>
    /// A read that runs out of table says which byte stopped it and where — and one that ends
    /// properly says nothing, which is the half that stops "always report a stop" passing.
    /// </summary>
    [Fact]
    public void AStopIsReportedWithItsByteAndItsOffsetAndAProperEndIsNot()
    {
        // setflag 0x0025 ; 0xFF
        ABlockRead.Block stopped = ABlockRead.One(
            Image((0x100, [0x29, 0x25, 0x00, NoWidth])), 0x08000100);

        Assert.True(stopped.Stopped);
        Assert.Equal(NoWidth, stopped.StoppedOn);
        Assert.Equal(0x103, stopped.StoppedAt);
        Assert.Single(stopped.Lines);

        // setflag 0x0025 ; end — the ordinary case
        ABlockRead.Block ended = ABlockRead.One(
            Image((0x100, [0x29, 0x25, 0x00, 0x02])), 0x08000100);

        Assert.False(ended.Stopped);
        Assert.Null(ended.StoppedOn);
        Assert.Null(ended.StoppedAt);
    }

    // -------------------------------------------------------------- what it hands over to

    /// <summary>
    /// The four pointer forms hand control over. A <c>compare</c>'s operand is a number that can
    /// look like an address, and the byte after a conditional is the fall-through rather than a
    /// hand-over: counting either would make a block claim to reach places it does not.
    /// </summary>
    [Fact]
    public void OnlyThePointerFormsHandControlOver()
    {
        Assert.Equal(0x08000200u, ABlockRead.HandsOverTo(Command(0x100, Goto(0x08000200))));
        Assert.Equal(0x08000200u, ABlockRead.HandsOverTo(Command(0x100, [0x04, .. BitConverter.GetBytes(0x08000200u)])));
        Assert.Equal(0x08000200u, ABlockRead.HandsOverTo(Command(0x100, GotoIf(1, 0x08000200))));
        Assert.Equal(0x08000200u, ABlockRead.HandsOverTo(Command(0x100, [0x07, 1, .. BitConverter.GetBytes(0x08000200u)])));

        // compare 0x800D, 0 — an operand, not an address
        Assert.Null(ABlockRead.HandsOverTo(Command(0x100, [0x21, 0x0D, 0x80, 0x00, 0x00])));
        Assert.Null(ABlockRead.HandsOverTo(Command(0x100, [0x02])));
        Assert.Null(ABlockRead.HandsOverTo(Command(0x100, [0x29, 0x25, 0x00])));
    }

    /// <summary>
    /// And the fall-through of a conditional is not one of them, read off a whole block: the
    /// shape `0x00AB`'s branch has on this cartridge, where the two arms are two bytes apart.
    /// </summary>
    [Fact]
    public void TheFallThroughOfAConditionalIsNotAHandOver()
    {
        // if EQUAL goto 0x08000200 ; faceplayer ; end
        ABlockRead.Block block = ABlockRead.One(
            Image((0x100, [.. GotoIf(1, 0x08000200), 0x6B, 0x02])), 0x08000100);

        Assert.Equal([0x08000200u], block.Reaches);
        Assert.Equal(3, block.Lines.Count);
    }

    // ------------------------------------------------------------------ each block once

    /// <summary>
    /// THE DISCRIMINATION for the walk: a block TWO DIFFERENT BLOCKS reach is read once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first version of this fixture put both arms of one branch on one target and came back
    /// GREEN when the seen-set was removed — because a block already refuses to list the same
    /// target twice, so the duplicate never reached the walk at all. That is fixture-lie 10:
    /// <b>ask where in the fixture the thing you are asserting about actually is</b>.
    /// </para>
    /// <para>
    /// Two arms, two different blocks, and both of them ending on ONE third block. That is the
    /// diamond this cartridge is full of and the only shape the seen-set decides.
    /// </para>
    /// </remarks>
    [Fact]
    public void ABlockTwoDifferentBlocksReachIsReadOnce()
    {
        ABlockRead.Block[] blocks =
        [
            .. ABlockRead.From(
                Image(
                    (0x100, [.. GotoIf(1, 0x08000200), .. Goto(0x08000300)]),
                    (0x200, Goto(0x08000400)),
                    (0x300, Goto(0x08000400)),
                    (0x400, [0x6B, 0x02])),
                0x08000100),
        ];

        Assert.Equal(
            [0x08000100u, 0x08000200u, 0x08000300u, 0x08000400u],
            blocks.Select(b => b.Address));
    }

    /// <summary>
    /// And a block that hands back to one already read does not go round for ever — the other
    /// half, and the reason the walk needs the set rather than a depth limit.
    /// </summary>
    [Fact]
    public void AndABlockThatHandsBackDoesNotGoRoundForEver()
    {
        ABlockRead.Block[] blocks =
        [
            .. ABlockRead.From(
                Image(
                    (0x100, Goto(0x08000200)),
                    (0x200, Goto(0x08000100))),
                0x08000100),
        ];

        Assert.Equal([0x08000100u, 0x08000200u], blocks.Select(b => b.Address));
    }

    /// <summary>
    /// And two arms landing on DIFFERENT addresses are two blocks — the other half, without
    /// which "read the entry and nothing else" would pass the test above.
    /// </summary>
    [Fact]
    public void ButTwoArmsReachingTwoPlacesAreTwoBlocks()
    {
        ABlockRead.Block[] blocks =
        [
            .. ABlockRead.From(
                Image(
                    (0x100, [.. GotoIf(1, 0x08000200), .. Goto(0x08000300)]),
                    (0x200, [0x6B, 0x02]),
                    (0x300, [0x02])),
                0x08000100),
        ];

        Assert.Equal(3, blocks.Length);
        Assert.Equal([0x08000100u, 0x08000200u, 0x08000300u], blocks.Select(b => b.Address));
    }

    private static ScriptCommand Command(int offset, byte[] bytes) =>
        new(offset, bytes[0], bytes[1..]);
}
