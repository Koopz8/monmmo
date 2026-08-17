using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Two more widths, and one of them is the trap milestone 200 wrote down, met head on.
/// <para>
/// <c>0x95</c> has seven sites and every one of them reads <c>95 00 00 00</c>. Read nought, one
/// or two wide, ALL SEVEN resume on <c>0x00</c> — the widest agreement anywhere in the width
/// table, and worth nothing: every site is landing in the middle of the same run of zero bytes
/// inside the same argument. Read three wide they resume on seven different bytes. <b>The
/// disagreement is the evidence</b>, because opcodes vary between sites and arguments have
/// columns.
/// </para>
/// <para>
/// <c>0xC2</c> is the opposite kind of proof — the longest chain in the table. Read two wide,
/// <c>0x0816CDB6</c> runs eight commands each parsing into the next, two of them carrying real
/// addresses and one comparing against <c>0x800D</c>. Read one wide it resumes on a <c>goto</c>
/// whose pointer is <c>0x4001007D</c>, which is not an address in a 16 MiB file.
/// </para>
/// </summary>
public class TheFalseColumnAndTheLongChainTests
{
    /// <summary>Seven sites, and three wrong widths that agree with each other perfectly.</summary>
    private const byte SevenFalseAgreements = 0x95;

    /// <summary>Three sites, and an eight-command chain behind it.</summary>
    private const byte TheLongChain = 0xC2;

    /// <summary>
    /// The wall six fixtures now lean on. <c>0x95</c> is three zero bytes — the most
    /// slide-prone shape there is — so without a widthless byte among them a short read walks
    /// straight to the assertion and the test passes at exactly the wrong width.
    /// </summary>
    private const byte NoWidth = 0xE6;

    private const byte SetFlag = 0x29;

    private const byte End = 0x02;

    private const int BehindTheFalseColumn = 0x0067;

    private const int BehindTheChain = 0x0068;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static byte[] Image()
    {
        var image = new byte[0x2000];

        // Three arguments, the last of them the wall. On the cartridge all three are zero,
        // which is precisely why the wrong widths agree there and why one of them has to be
        // something a short read cannot step over here.
        Put(image, 0x100, SevenFalseAgreements, 0x00, 0x00, NoWidth);
        Put(image, 0x104, SetFlag, BehindTheFalseColumn & 0xFF, BehindTheFalseColumn >> 8);
        Put(image, 0x107, End);

        Put(image, 0x200, TheLongChain, 0x00, NoWidth);
        Put(image, 0x203, SetFlag, BehindTheChain & 0xFF, BehindTheChain >> 8);
        Put(image, 0x206, End);

        return image;
    }

    private static bool Reaches(uint from, int flag)
    {
        (IReadOnlyCollection<int> on, IReadOnlyCollection<int> _) = WhatItIsWaitingFor.Touches(
            new Rom(Image()), [new SetsAFlag("1.1", "on arrival (0x4001 == 0)", from)]);

        return on.Contains(flag);
    }

    /// <summary>
    /// Four bytes, chosen against three widths that agreed with themselves seven times over.
    /// </summary>
    [Fact]
    public void TheOneWithSevenFalseAgreementsIsFourBytes()
    {
        Assert.Equal(3, ScriptCommands.ArgumentLength(SevenFalseAgreements));
        Assert.True(Reaches(0x08000100, BehindTheFalseColumn));
    }

    /// <summary>Three bytes, with eight commands reading on behind it.</summary>
    [Fact]
    public void TheOneWithTheLongChainIsThreeBytes()
    {
        Assert.Equal(2, ScriptCommands.ArgumentLength(TheLongChain));
        Assert.True(Reaches(0x08000200, BehindTheChain));
    }

    /// <summary>
    /// The wall, still a wall. Six fixtures across three milestones lean on this now, and if it
    /// ever acquires a width they all quietly become nop slides and start passing for the wrong
    /// reason.
    /// </summary>
    [Fact]
    public void TheWallIsStillAWall()
    {
        Assert.Null(ScriptCommands.ArgumentLength(NoWidth));
    }
}
