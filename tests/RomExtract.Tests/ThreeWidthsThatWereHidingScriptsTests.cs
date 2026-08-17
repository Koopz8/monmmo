using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Three argument widths, and the rule that settled the biggest of them.
/// <para>
/// <b>A missing width does not fail.</b> The read stops, the block comes back short, and every
/// instrument downstream reports a world with less in it — cleanly, with no error anywhere.
/// <c>0x9E</c> stopped three blocks, and eleven bytes past one of them is a <c>call</c> to the
/// script that clears the flag keeping nineteen people off eleven maps.
/// </para>
/// <para>
/// And <c>0xD0</c>, which stopped fifty-one — more than the next three commands together — is
/// the one every continuation test got wrong. At three bytes it swallows an <c>end</c> and
/// reads on into a textbox that parses beautifully and belongs to the neighbouring script. What
/// caught it is the only thing in this file that says where a script stops: <b>something else
/// points at that textbox</b>, and you do not fall into a block that has its own pointer.
/// </para>
/// </summary>
public class ThreeWidthsThatWereHidingScriptsTests
{
    private const byte Paired = 0x9C;
    private const byte Partner = 0x9E;
    private const byte Fifty = 0xD0;
    private const byte Seventeen = 0x78;
    private const byte SetVar = 0x16;
    private const byte Call = 0x04;
    private const byte SetFlag = 0x29;
    private const byte ClearFlag = 0x2A;
    private const byte Goto = 0x05;
    private const byte Return = 0x03;
    private const byte End = 0x02;

    /// <summary>The flag on the far side of the command that had no width.</summary>
    private const int PastTheStop = 0x009D;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static void Pointer(byte[] image, int at, uint address)
    {
        for (int i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>
    /// The shape on the cartridge: a pair of commands carrying the same word, and then a call
    /// to the block that does the work.
    /// </summary>
    private static byte[] Image()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, Paired, 0x3E, 0x00);
        Put(image, 0x103, Partner, 0x3E, 0x00);
        Put(image, 0x106, SetVar, 0x01, 0x40, 0x01, 0x00);
        Put(image, 0x10B, Call);
        Pointer(image, 0x10C, 0x08000300);
        Put(image, 0x110, End);

        Put(image, 0x300, ClearFlag, PastTheStop & 0xFF, PastTheStop >> 8, Return);

        // And the 0xD0 shape: a word, an end, and a textbox after it that something else in
        // the image points at — so the end is a real end and this command is two bytes wide.
        Put(image, 0x400, Fifty, 0xA4, 0x08);
        Put(image, 0x403, End);
        Put(image, 0x404, SetFlag, 0x55, 0x00, End);

        // The pointer that makes 0x404 a script in its own right.
        Put(image, 0x500, Goto);
        Pointer(image, 0x501, 0x08000404);

        return image;
    }

    private static Rom Rom() => new(Image());

    /// <summary>
    /// The finding, at the only size that matters: what was behind the command that had no
    /// width is read now.
    /// </summary>
    [Fact]
    public void TheFlagBehindTheCommandWithNoWidthIsReachedNow()
    {
        (IReadOnlyCollection<int> _, IReadOnlyCollection<int> off) = WhatItIsWaitingFor.Touches(
            Rom(), [new SetsAFlag("1.1", "on arrival (0x4001 == 0)", 0x08000100)]);

        Assert.Contains(PastTheStop, off);
    }

    /// <summary>
    /// And the widths themselves, stated once so that changing one has to be deliberate. Each
    /// was read off a column of sites on the cartridge; the reasoning is in the table.
    /// </summary>
    [Theory]
    [InlineData(Fifty, 2)]
    [InlineData(Seventeen, 4)]
    [InlineData(Partner, 2)]
    public void TheWidthsReadOffTheCartridge(byte code, int width) =>
        Assert.Equal(width, ScriptCommands.ArgumentLength(code));

    /// <summary>
    /// <b>The rule that settled 0xD0.</b> A width whose next command lands where something else
    /// points has eaten a block boundary — and every other test prefers exactly that width,
    /// because it skips the <c>end</c> and reads on into something that parses.
    /// </summary>
    [Fact]
    public void AWidthThatReadsOnIntoAPointedAtBlockIsCaught()
    {
        Rom rom = Rom();

        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        // Three bytes carries the read past the end at 0x403 and into 0x404, which the goto at
        // 0x500 points at.
        Assert.Equal(1.0, EverywhereInTheImage.ReadsOnIntoSomebodyElses(index, [0x400], 3));

        // Two bytes lands on the end, which nothing points at.
        Assert.Equal(0.0, EverywhereInTheImage.ReadsOnIntoSomebodyElses(index, [0x400], 2));
    }

    /// <summary>
    /// And it says nothing when there is nothing to say. A rule that returns a confident number
    /// with no sites behind it is a rule that rules out whichever width was asked about first.
    /// </summary>
    [Fact]
    public void WithNoSitesItRulesNothingOut()
    {
        Rom rom = Rom();

        Assert.Equal(0.0, EverywhereInTheImage.ReadsOnIntoSomebodyElses(
            EverywhereInTheImage.PointerIndex(rom), [], 3));
    }
}
