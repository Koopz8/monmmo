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

    /// <summary>Twenty sites of one shape, and the width the column test could not choose.</summary>
    private const byte Placed = 0x3F;

    /// <summary>The one that was WRONG rather than missing, and drifted instead of stopping.</summary>
    private const byte WasWrong = 0x1F;

    private const byte FourByteBlock = 0xA7;
    private const byte BeforeATextBox = 0xC0;
    private const byte Counted = 0x70;

    /// <summary>The second one found wrong rather than missing — and it invented a flag.</summary>
    private const byte WasWrongToo = 0x6F;

    private const byte CopyVar = 0x19;
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

        // The one whose twenty sites are all the same idiom: a byte, a counter, 0xFF — how this
        // cartridge writes "the player" — and two little-endian words. At seven the next
        // command is real; at six it is the high byte of the second word, read as a nop.
        Put(image, 0x600, Placed, 0x01, 0x2A, 0xFF, 0x18, 0x00, 0x19, 0x00);
        Put(image, 0x608, SetFlag, 0x57, 0x00, End);

        // The wrong width's shape: a counter, the command, and a goto to somewhere that does
        // something. At two the goto is the next command; at five it is swallowed whole, and
        // the read carries on into the middle of the block it points at.
        Put(image, 0x700, Counted, 0x00, 0x00);
        Put(image, 0x703, WasWrong, 0x00, 0x00);
        Put(image, 0x706, Goto);
        Pointer(image, 0x707, 0x08000740);
        Put(image, 0x70B, End);

        // And a byte the reader cannot step over, right after the block. Without it the zeroes
        // between here and the target are a NOP SLIDE: a read that drifted past the goto walks
        // through sixty bytes of 0x00 and arrives at the setflag anyway, so the test passes at
        // the wrong width for the wrong reason. A zero-filled fixture is not empty space.
        Put(image, 0x70C, 0xFE);

        Put(image, 0x740, SetFlag, 0x58, 0x00, End);

        // The second wrong width, with a flag hidden inside its own arguments. Read at one, the
        // arguments decode as a `setflag` that is not there; read at four they are arguments.
        Put(image, 0x780, SetVar, 0x04, 0x80, 0x00, 0x00);
        Put(image, 0x785, WasWrongToo, 0x00, SetFlag, 0x5A, 0x00);
        Put(image, 0x78A, CopyVar, 0x00, 0x80, 0x0D, 0x80);
        Put(image, 0x78F, End);

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
    [InlineData(Placed, 7)]
    [InlineData(WasWrong, 2)]
    [InlineData(FourByteBlock, 2)]
    [InlineData(BeforeATextBox, 2)]
    [InlineData(WasWrongToo, 4)]
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

    /// <summary>
    /// And the block reads through it rather than stopping dead — which is what having any
    /// width at all buys.
    /// <para>
    /// <b>This does not tell six from seven and is not claimed to.</b> At six the read lands on
    /// the high byte of the second coordinate, calls it a nop, and carries on to the same place;
    /// the two widths only separate across the twenty sites on the cartridge, where one lands on
    /// <c>compare</c> every time and the other on a nop every time. The width itself is held by
    /// the table above. Said out loud because a test named for a discrimination it does not make
    /// is worse than no test.
    /// </para>
    /// </summary>
    [Fact]
    public void TheBlockBehindItReadsThroughRatherThanStoppingDead()
    {
        (IReadOnlyCollection<int> on, IReadOnlyCollection<int> _) = WhatItIsWaitingFor.Touches(
            Rom(), [new SetsAFlag("1.1", "trigger (2,2)", 0x08000600)]);

        Assert.Contains(0x0057, on);
    }

    /// <summary>
    /// <b>How much a population of sites is one idiom repeated.</b> The scorer throws out any
    /// width that resumes on the same byte at nearly every site, which is right when a width has
    /// landed inside an argument and exactly backwards when the sites are duplicates — there,
    /// the correct width resumes on a column too.
    /// <para>
    /// Printed rather than wired into the verdict. Suppressing a rule until it agrees with an
    /// answer already obtained by reading the bytes is decoration, not evidence.
    /// </para>
    /// </summary>
    [Fact]
    public void SitesThatShareTheirRunUpAreOneIdiom()
    {
        Rom rom = Rom();

        // Three copies of the same run-up, planted where nothing else reads.
        var image = new byte[0x2000];

        for (var i = 0; i < 3; i++)
        {
            Put(image, 0x100 + (i * 0x20), 0x16, 0x06, 0x80, 0x03, 0x00, Placed);
        }

        Assert.Equal(1.0, WhatIsBehindAStop.AreOneIdiom(new Rom(image), [0x105, 0x125, 0x145]));

        // And sites with genuinely different run-ups do not read as one. The first attempt at
        // this used four offsets in the zero-filled part of the fixture, which share the run-up
        // "00 00 00 00 00" and score a perfect one — correctly, and uselessly. Padding looks
        // exactly like an idiom to this measure, which is a real weakness of it and the reason
        // it is printed rather than acted on.
        Assert.True(WhatIsBehindAStop.AreOneIdiom(rom, [0x103, 0x403, 0x608, 0x110]) < 0.9);
    }

    /// <summary>
    /// And one site is not a population. A figure of "all of them" from a single site would rule
    /// the column test out everywhere it matters least.
    /// </summary>
    [Fact]
    public void OneSiteIsNotAnIdiom() =>
        Assert.Equal(0, WhatIsBehindAStop.AreOneIdiom(Rom(), [0x100]));

    /// <summary>
    /// <b>A wrong width does not stop anything.</b> It eats the commands after it and reads
    /// whatever it lands on, so the block comes back full of instructions that are not there —
    /// and the read never follows the <c>goto</c> it swallowed.
    /// <para>
    /// This one was found by a phantom stop twenty-four bytes downstream, at a byte sitting
    /// inside a <c>gotoif</c>'s pointer. The width itself never failed at all.
    /// </para>
    /// </summary>
    [Fact]
    public void AWrongWidthSwallowsTheGotoAndEverythingBehindIt()
    {
        (IReadOnlyCollection<int> on, IReadOnlyCollection<int> _) = WhatItIsWaitingFor.Touches(
            Rom(), [new SetsAFlag("1.1", "on load (kind 3)", 0x08000700)]);

        Assert.Contains(0x0058, on);
    }

    /// <summary>
    /// <b>A wrong width does not only hide things — it invents them.</b> Read one byte short,
    /// this command's own arguments decode as a <c>setflag</c>, and the run comes back holding
    /// a flag no script on the cartridge ever set.
    /// <para>
    /// That is what fixing it did on the real image: flags touched went 259 to 258, and the
    /// playthrough's own count 286 to 284. Every flag figure this project has published was
    /// inflated by a misalignment, which is the opposite of the failure it has spent a session
    /// chasing and reads exactly the same from outside.
    /// </para>
    /// </summary>
    [Fact]
    public void AWrongWidthInventsAFlagOutOfItsOwnArguments()
    {
        (IReadOnlyCollection<int> on, IReadOnlyCollection<int> _) = WhatItIsWaitingFor.Touches(
            Rom(), [new SetsAFlag("1.1", "person 1", 0x08000780)]);

        Assert.DoesNotContain(0x005A, on);
    }
}
