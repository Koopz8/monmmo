using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Three more widths, and the first one whose consequence is a Pokémon.
/// <para>
/// 199 read <c>0xB3</c> and <c>0xB4</c>. Behind them were <c>0xB5</c>, <c>0x92</c> and
/// <c>0x91</c> — the pair the GAME CORNER is built from, the one that asks about money and the
/// one that takes it. Nine sites each, carrying the same nine values: 50, 200, 300, 350, 500,
/// 500, 1000, 10000 and 50.
/// </para>
/// <para>
/// <b>0xB5's width nought is refuted by a pointer</b>, which is the test that settled 0xD0.
/// At nought the block ends immediately — but seventeen bytes on sits a loadpointer with a real
/// text address, a callstd, a compare, an if and a goto ending properly, and NOTHING IN THE
/// FILE POINTS AT IT. You do not fall into a block that has its own pointer, and you do not
/// reach one that has none except by falling in.
/// </para>
/// <para>
/// <b>And the false column.</b> Read two, three or four wide, all nine sites of <c>0x92</c>
/// resume on <c>0x00</c>. That is not nine agreements — it is one, landing in the middle of the
/// same run of zero bytes in the same argument. A nop slide is what the widest agreement in the
/// table looks like from inside, and it is the least evidence rather than the most.
/// </para>
/// </summary>
public class TheMoneyCommandsAndTheThingBehindThemTests
{
    /// <summary>Three sites, all handing over the variable 0x4002.</summary>
    private const byte TakesAVariableToo = 0xB5;

    /// <summary>Nine sites. The one that asks.</summary>
    private const byte AsksAboutMoney = 0x92;

    /// <summary>Nine sites. The one that takes it.</summary>
    private const byte TakesTheMoney = 0x91;

    /// <summary>
    /// The wall, and the same one milestone 199's fixtures lean on. A zero-filled image is a
    /// nop slide, and these two commands are five bytes of mostly zeroes — exactly the shape
    /// that slides. Without a widthless byte in the arguments a short read walks to the
    /// assertion and the test passes at the wrong width.
    /// </summary>
    private const byte NoWidth = 0xE6;

    private const byte SetFlag = 0x29;

    private const byte End = 0x02;

    private const int BehindTheVariable = 0x0064;

    private const int BehindTheAsking = 0x0065;

    private const int BehindTheTaking = 0x0066;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static byte[] Image()
    {
        var image = new byte[0x2000];

        // 0xB5 hands over 0x4002 at all three of its sites, the same shape as 0xB3. The low
        // half of the variable is kept; the high half is the wall.
        Put(image, 0x100, TakesAVariableToo, 0x02, NoWidth);
        Put(image, 0x103, SetFlag, BehindTheVariable & 0xFF, BehindTheVariable >> 8);
        Put(image, 0x106, End);

        // A four-byte little-endian price and a byte. Fifty, as three of the nine sites carry.
        // The wall goes in the LAST argument byte, because that is the one a short read has to
        // cross: at four the read lands on it and stops instead of sliding over a zero.
        Put(image, 0x200, AsksAboutMoney, 0x32, 0x00, 0x00, 0x00, NoWidth);
        Put(image, 0x206, SetFlag, BehindTheAsking & 0xFF, BehindTheAsking >> 8);
        Put(image, 0x209, End);

        Put(image, 0x300, TakesTheMoney, 0x32, 0x00, 0x00, 0x00, NoWidth);
        Put(image, 0x306, SetFlag, BehindTheTaking & 0xFF, BehindTheTaking >> 8);
        Put(image, 0x309, End);

        return image;
    }

    private static bool Reaches(uint from, int flag)
    {
        (IReadOnlyCollection<int> on, IReadOnlyCollection<int> _) = WhatItIsWaitingFor.Touches(
            new Rom(Image()), [new SetsAFlag("1.1", "on arrival (0x4001 == 0)", from)]);

        return on.Contains(flag);
    }

    /// <summary>The one 0xB3's shape vouches for, and whose nought is refuted by a pointer.</summary>
    [Fact]
    public void TheOtherCommandThatHandsOverAVariableIsThreeBytes()
    {
        Assert.Equal(2, ScriptCommands.ArgumentLength(TakesAVariableToo));
        Assert.True(Reaches(0x08000100, BehindTheVariable));
    }

    /// <summary>Six bytes: a four-byte price and a byte, on nine sites.</summary>
    [Fact]
    public void TheCommandThatAsksAboutMoneyIsSixBytes()
    {
        Assert.Equal(5, ScriptCommands.ArgumentLength(AsksAboutMoney));
        Assert.True(Reaches(0x08000200, BehindTheAsking));
    }

    /// <summary>
    /// And its twin, whose three clearest sites are consecutive one-line subroutines — pay two
    /// hundred and return, pay three hundred and return, pay three hundred and fifty and
    /// return. Twenty-one bytes, three commands, three returns and three prices.
    /// </summary>
    [Fact]
    public void TheCommandThatTakesTheMoneyIsSixBytes()
    {
        Assert.Equal(5, ScriptCommands.ArgumentLength(TakesTheMoney));
        Assert.True(Reaches(0x08000300, BehindTheTaking));
    }

    /// <summary>
    /// The fixture auditing itself, as milestone 199's does. These two commands are five bytes
    /// of mostly zeroes, so they are the most slide-prone shape in the table — if the wall ever
    /// stops being a wall, all three tests above pass for the wrong reason and say nothing.
    /// </summary>
    [Fact]
    public void TheWallTheseFixturesLeanOnIsStillAWall()
    {
        Assert.Null(ScriptCommands.ArgumentLength(NoWidth));
    }
}
