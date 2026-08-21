using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The jumped-into test against the region-preserving floor (270).
/// <para>
/// "A site something jumps into" has meant, since 175, that a jump-owned pointer lands within 192
/// bytes BEFORE the site. On this cartridge that is 7.5% of the unopened flag sites against the
/// reversal's 1.3% — and against the same pointers aimed 256 to 4096 bytes off it is 5.3% to
/// 7.1%. The window test measures whether a site sits in a region full of script, not whether a
/// script names its block. The fixtures here are the discrimination the control made.
/// </para>
/// </summary>
public sealed class JumpedIntoUnderANudgeTests
{
    private const byte SetFlag = 0x29;
    private const byte Goto = 0x05;
    private const byte End = 0x02;
    private const byte Nop = 0x00;

    private const int Block = 0x200;

    /// <summary>
    /// A block at 0x200 — <c>setflag 0x0014 ; setflag 0x0015 ; end</c> — and, just after its end,
    /// a stray <c>setflag 0x0016</c> that is not a command of it. One <c>goto 0x08000200</c> at
    /// 0x100 names the block. A four-byte aligned literal at 0x300 names it too, and four loose
    /// bytes at 0x305 name it a third time.
    /// </summary>
    private static Rom Image(bool withTheJump = true, bool withTheLiteral = false, bool withLooseBytes = false)
    {
        // 0xFF everywhere that is not placed on purpose: 0x00 is a no-op and a run of them is a
        // slide (269's sixth break), so a fixture about where a block ENDS cannot be zero-filled.
        var image = new byte[0x1000];

        Array.Fill(image, (byte)0xFF);

        int at = Block;

        image[at++] = SetFlag; image[at++] = 0x14; image[at++] = 0x00;
        image[at++] = SetFlag; image[at++] = 0x15; image[at++] = 0x00;
        image[at++] = End;

        // THE STRAY: within the window of the jump, not on the jump's block.
        image[at++] = SetFlag; image[at++] = 0x16; image[at++] = 0x00;
        image[at++] = End;

        if (withTheJump)
        {
            image[0x100] = Goto;
            Put(image, 0x101, 0x08000000 + Block);
            image[0x105] = End;
        }

        if (withTheLiteral) Put(image, 0x300, 0x08000000 + Block);

        if (withLooseBytes) Put(image, 0x305, 0x08000000 + Block);

        return new Rom(image);
    }

    private static void Put(byte[] image, int at, uint value)
    {
        image[at] = (byte)value;
        image[at + 1] = (byte)(value >> 8);
        image[at + 2] = (byte)(value >> 16);
        image[at + 3] = (byte)(value >> 24);
    }

    private const uint FirstSite = 0x08000000 + Block;
    private const uint SecondSite = 0x08000000 + Block + 3;
    private const uint TheStray = 0x08000000 + Block + 7;

    [Fact]
    public void WithinTheWindowCountsAJumpAimedAtTheBlock()
    {
        Rom rom = Image();
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        Assert.True(JumpedIntoUnderANudge.IsJumpedInto(rom, index, FirstSite, 0));
        Assert.True(JumpedIntoUnderANudge.IsJumpedInto(rom, index, SecondSite, 0));

        // And it counts the stray too — which is the whole finding. The stray is seven bytes after
        // a block a jump names and is no part of it, and the window cannot tell.
        Assert.True(JumpedIntoUnderANudge.IsJumpedInto(rom, index, TheStray, 0));

        Assert.Equal(3, JumpedIntoUnderANudge.Count(rom, index, [FirstSite, SecondSite, TheStray], 0));
    }

    [Fact]
    public void OnAJumpsBlockNeedsTheSiteToBeACommandOfTheBlockTheJumpNames()
    {
        Rom rom = Image();
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        Assert.True(JumpedIntoUnderANudge.IsOnAJumpsBlock(rom, index, FirstSite, 0));
        Assert.True(JumpedIntoUnderANudge.IsOnAJumpsBlock(rom, index, SecondSite, 0));

        // THE DISCRIMINATION: the stray is within the window and is not on the block.
        Assert.False(JumpedIntoUnderANudge.IsOnAJumpsBlock(rom, index, TheStray, 0));

        Assert.Equal(2, JumpedIntoUnderANudge.CountOnABlock(rom, index, [FirstSite, SecondSite, TheStray], 0));
    }

    /// <summary>
    /// The nudge aims the same pointer a few bytes further on. Four bytes on, the jump lands on the
    /// second setflag's argument — which the reader resynchronises off (269) — so the first site
    /// is no longer on the block and the second still is. Past the end of the block, nothing is.
    /// </summary>
    [Fact]
    public void TheNudgeAimsTheSamePointerOff()
    {
        Rom rom = Image();
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        // Nudged by three the jump lands exactly on the second setflag.
        Assert.False(JumpedIntoUnderANudge.IsOnAJumpsBlock(rom, index, FirstSite, 3));
        Assert.True(JumpedIntoUnderANudge.IsOnAJumpsBlock(rom, index, SecondSite, 3));

        // Nudged clean past the block: the window still holds the pointer (it is 0x100 bytes
        // behind the site, inside 192 only when aimed forward enough), the block does not.
        Assert.Equal(0, JumpedIntoUnderANudge.CountOnABlock(rom, index, [FirstSite, SecondSite, TheStray], 0x20));

        // And nudged by more than the window, the window is empty too — there is no pointer
        // aimed 0x1000 bytes before this block.
        Assert.Equal(0, JumpedIntoUnderANudge.Count(rom, index, [FirstSite, SecondSite, TheStray], 0x800));
    }

    [Fact]
    public void WithoutTheJumpNothingIsJumpedIntoByEitherTest()
    {
        Rom rom = Image(withTheJump: false);
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        Assert.Equal(0, JumpedIntoUnderANudge.Count(rom, index, [FirstSite, SecondSite, TheStray], 0));
        Assert.Equal(0, JumpedIntoUnderANudge.CountOnABlock(rom, index, [FirstSite, SecondSite, TheStray], 0));
    }

    /// <summary>
    /// An aligned word no command owns is the far side of the code boundary with an address on it,
    /// and it names a block. Four loose bytes never do: they are the accident the control measures.
    /// </summary>
    [Fact]
    public void ALiteralNamesABlockOnlyWhenAskedAndLooseBytesNever()
    {
        Rom literal = Image(withTheJump: false, withTheLiteral: true);
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(literal);

        Assert.False(JumpedIntoUnderANudge.IsOnAJumpsBlock(literal, index, FirstSite, 0));
        Assert.True(JumpedIntoUnderANudge.IsOnAJumpsBlock(literal, index, FirstSite, 0, orALiteral: true));
        Assert.False(JumpedIntoUnderANudge.IsOnAJumpsBlock(literal, index, TheStray, 0, orALiteral: true));

        Rom loose = Image(withTheJump: false, withLooseBytes: true);
        index = EverywhereInTheImage.PointerIndex(loose);

        Assert.False(JumpedIntoUnderANudge.IsOnAJumpsBlock(loose, index, FirstSite, 0, orALiteral: true));
    }

    [Fact]
    public void OnABlockNamesWhatNamedEachSite()
    {
        Rom rom = Image();
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        IReadOnlyList<(uint Site, NamesIt By)> found =
            JumpedIntoUnderANudge.OnABlock(rom, index, [FirstSite, SecondSite, TheStray]);

        Assert.Equal([FirstSite, SecondSite], found.Select(f => f.Site));
        Assert.All(found, f => Assert.Equal(0x101, f.By.Offset));
        Assert.All(found, f => Assert.True(f.By.AJump));
    }

    [Fact]
    public void GroupsWithOneCountsGroupsNotSites()
    {
        Rom rom = Image();
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        // Two flags on the block, one group each; the stray is a third group and is in the
        // window but not on the block.
        (int Key, uint Site)[] sites = [(0x14, FirstSite), (0x15, SecondSite), (0x16, TheStray), (0x14, TheStray)];

        Assert.Equal(3, JumpedIntoUnderANudge.GroupsWithOne(rom, index, sites, 0));
        Assert.Equal(2, JumpedIntoUnderANudge.GroupsWithOne(rom, index, sites, 0, onTheBlock: true));
    }

    /// <summary>
    /// A nudge shorter than the window slides the window onto itself, and the row it produces
    /// cannot have come back different. The boundary is the window itself.
    /// </summary>
    [Fact]
    public void ANudgeInsideTheWindowIsSaidToBe()
    {
        Assert.True(JumpedIntoUnderANudge.InsideTheWindow(4));
        Assert.True(JumpedIntoUnderANudge.InsideTheWindow(JumpedIntoUnderANudge.Slack));
        Assert.False(JumpedIntoUnderANudge.InsideTheWindow(JumpedIntoUnderANudge.Slack + 1));
        Assert.False(JumpedIntoUnderANudge.InsideTheWindow(4096));

        // The ladder is 269's, so the two are read against each other, and it reaches past the
        // window — or every row would be inside it and the control would have no floor at all.
        Assert.Contains(JumpedIntoUnderANudge.Nudges, by => !JumpedIntoUnderANudge.InsideTheWindow(by));
    }
}
