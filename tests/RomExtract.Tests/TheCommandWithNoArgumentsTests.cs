using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A command with no arguments at all, and the dependency that proves it anyway.
/// <para>
/// <c>0x43</c> has five sites and every one of them is followed by a comparison of
/// <c>0x800D</c> — this game's standard result variable — and then by something that reads
/// <c>0x800D</c> again or branches on it. That is <c>0xB3</c>'s shape with the argument
/// removed: an argument column can be a coincidence, an argument that reappears as the next
/// command's operand cannot, and here the command carries nothing and the dependency is
/// <em>still</em> visible in what comes after it.
/// </para>
/// <para>
/// <b>And the false column for the third milestone running.</b> Read one wide, all five sites
/// resume on <c>0x0D</c>; read two wide, all five resume on <c>0x80</c>. Ten agreements, and
/// they are one: those are the two halves of <c>0x800D</c> being read as though they were
/// opcodes. Four wide is a nop slide. The widest-looking agreement has now been the wrong
/// answer three times in a row.
/// </para>
/// </summary>
public class TheCommandWithNoArgumentsTests
{
    /// <summary>Five sites, all block starts, none of them carrying anything.</summary>
    private const byte NoArgumentsAtAll = 0x43;

    private const byte SetFlag = 0x29;

    private const byte End = 0x02;

    /// <summary>
    /// The flag behind it — and its own low byte is the wall.
    /// <para>
    /// <c>0x00E6</c> rather than any other number on purpose: read one wide, the command
    /// swallows the <c>setflag</c> opcode and the very next byte is <c>0xE6</c>, which this
    /// project has no width for, so the read stops dead instead of sliding through zeroes to
    /// the assertion. A fixture for a nought-argument command is otherwise the most slide-prone
    /// thing there is, because being wrong by one costs it only a single byte.
    /// </para>
    /// </summary>
    private const int BehindIt = 0x00E6;

    private static byte[] Image()
    {
        var image = new byte[0x2000];

        byte[] block = [NoArgumentsAtAll, SetFlag, BehindIt & 0xFF, BehindIt >> 8, End];

        block.CopyTo(image, 0x100);

        return image;
    }

    /// <summary>The finding: it carries nothing, and what follows is the next command.</summary>
    [Fact]
    public void TheCommandThatCarriesNothingIsOneByte()
    {
        Assert.Equal(0, ScriptCommands.ArgumentLength(NoArgumentsAtAll));

        (IReadOnlyCollection<int> on, IReadOnlyCollection<int> _) = WhatItIsWaitingFor.Touches(
            new Rom(Image()), [new SetsAFlag("1.1", "on arrival (0x4001 == 0)", 0x08000100)]);

        Assert.Contains(BehindIt, on);
    }

    /// <summary>
    /// The wall this fixture's flag number is doubling as. Said out loud, because it is not
    /// obvious from the number that <c>0x00E6</c> was chosen rather than picked.
    /// </summary>
    [Fact]
    public void TheLowByteOfThatFlagIsStillAByteWithNoWidth()
    {
        Assert.Null(ScriptCommands.ArgumentLength(BehindIt & 0xFF));
    }
}
