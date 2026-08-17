using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Three widths that nothing could have found without standing where the player stands.
/// <para>
/// 198 taught the walk to talk across a shop counter. Behind those counters is the GAME
/// CORNER, and behind the GAME CORNER is a run of commands no read had ever reached:
/// <c>0xB3</c> stopped seven places, <c>0xC1</c> two, and adopting <c>0xB3</c> exposed
/// <c>0xB4</c> behind it. A missing width does not fail — the read stops, the block comes back
/// short, and every instrument downstream reports a world with less in it, cleanly.
/// </para>
/// <para>
/// The evidence for each is written in the width table beside the bytes, because a fixture
/// cannot hold a column of seven sites. What a fixture can do is fail when the number changes,
/// and that is all these ask.
/// </para>
/// <para>
/// <b>The anti-slide device is <see cref="NoWidth"/>.</b> A zero-filled image is a nop slide:
/// every <c>0x00</c> is a valid no-op, so a read that drifted one byte short walks through the
/// padding and arrives at the assertion anyway, and the test passes at the wrong width for the
/// right-looking reason. Every command below therefore carries a byte with no width of its own
/// in its arguments, so a read that stops short stops <em>dead</em> instead of sliding.
/// </para>
/// </summary>
public class ThreeMoreWidthsBehindACounterTests
{
    /// <summary>Seven sites, each handing a variable to the command that reads it next.</summary>
    private const byte TakesAVariable = 0xB3;

    /// <summary>Two sites, both `... | 6C 02` — an argument, then release and end.</summary>
    private const byte TwoSitesOnly = 0xC1;

    /// <summary>Five sites carrying 10, 20, 20, 500 and 50.</summary>
    private const byte ACountOfSomething = 0xB4;

    /// <summary>
    /// A byte this project has no width for, used on purpose. It is on the owed list in its own
    /// right; here it is the wall that stops a short read from sliding to the assertion.
    /// </summary>
    private const byte NoWidth = 0xE6;

    private const byte SetFlag = 0x29;

    private const byte End = 0x02;

    private const int BehindTheVariable = 0x0061;

    private const int BehindTheTwoSites = 0x0062;

    private const int BehindTheCount = 0x0063;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    /// <summary>
    /// Three blocks, each the cartridge's own shape with a flag on the far side of it.
    /// <para>
    /// The flag is the assertion and the only one: reaching it means the reader stepped over
    /// exactly the right number of bytes, because the <see cref="NoWidth"/> in the arguments
    /// stops anything shorter and the <c>setflag</c>'s own operands are not commands.
    /// </para>
    /// </summary>
    private static byte[] Image()
    {
        var image = new byte[0x2000];

        // 0xB3 carries a variable on the cartridge — 0x4001 at five sites and 0x800D at two,
        // and at every one of the seven the NEXT command reads that same variable back. The
        // low half is kept here; the high half is the wall.
        Put(image, 0x100, TakesAVariable, 0x01, NoWidth);
        Put(image, 0x103, SetFlag, BehindTheVariable & 0xFF, BehindTheVariable >> 8);
        Put(image, 0x106, End);

        // 0xC1's shape is 0x94's, four lines above it in the table: an argument, then release
        // and end. Two sites, which is below this project's usual bar and is said out loud
        // there rather than hidden here.
        Put(image, 0x200, TwoSitesOnly, 0x00, NoWidth);
        Put(image, 0x203, SetFlag, BehindTheTwoSites & 0xFF, BehindTheTwoSites >> 8);
        Put(image, 0x206, End);

        // 0xB4 is a count — 10, 20, 20, 500, 50 across its five sites, which is a column of
        // round numbers and not a column of opcodes.
        Put(image, 0x300, ACountOfSomething, 0x0A, NoWidth);
        Put(image, 0x303, SetFlag, BehindTheCount & 0xFF, BehindTheCount >> 8);
        Put(image, 0x306, End);

        return image;
    }

    private static Rom Rom() => new(Image());

    private static bool Reaches(uint from, int flag)
    {
        (IReadOnlyCollection<int> on, IReadOnlyCollection<int> _) = WhatItIsWaitingFor.Touches(
            Rom(), [new SetsAFlag("1.1", "on arrival (0x4001 == 0)", from)]);

        return on.Contains(flag);
    }

    /// <summary>
    /// The one with a column of seven, and the only one of the three whose argument is checkable
    /// against the command after it: every site hands over a variable and the next command
    /// reads that variable back.
    /// </summary>
    [Fact]
    public void TheCommandThatHandsOverAVariableIsThreeBytes()
    {
        Assert.Equal(2, ScriptCommands.ArgumentLength(TakesAVariable));
        Assert.True(Reaches(0x08000100, BehindTheVariable));
    }

    /// <summary>
    /// Two sites, and the same shape 0x94 was settled on. Widths of nought and one are refuted
    /// on the cartridge rather than merely unpreferred — both leave the first site resuming on
    /// a <c>goto</c> whose pointer is not an address in a 16 MiB file.
    /// </summary>
    [Fact]
    public void TheCommandWithOnlyTwoSitesIsThreeBytes()
    {
        Assert.Equal(2, ScriptCommands.ArgumentLength(TwoSitesOnly));
        Assert.True(Reaches(0x08000200, BehindTheTwoSites));
    }

    /// <summary>
    /// The one that appeared only because the one before it was adopted — and the next one
    /// behind IT, 0xB5, is on the owed list rather than guessed at here.
    /// </summary>
    [Fact]
    public void TheCountIsThreeBytes()
    {
        Assert.Equal(2, ScriptCommands.ArgumentLength(ACountOfSomething));
        Assert.True(Reaches(0x08000300, BehindTheCount));
    }

    /// <summary>
    /// And the fixture's own honesty check: the wall really is a wall.
    /// <para>
    /// If <see cref="NoWidth"/> ever acquires a width, every test above quietly becomes a nop
    /// slide and starts passing for the wrong reason. This says so out loud instead — a test
    /// named for a discrimination it does not make is worse than no test.
    /// </para>
    /// </summary>
    [Fact]
    public void TheByteHoldingTheSlideOpenStillHasNoWidthOfItsOwn()
    {
        Assert.Null(ScriptCommands.ArgumentLength(NoWidth));
    }
}
