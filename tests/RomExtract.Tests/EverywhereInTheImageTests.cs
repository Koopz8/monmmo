using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Reading the file rather than the world.
/// <para>
/// Every other scan in this project starts at a map and follows the jumps. Asked "is there
/// anything here the maps do not point at?" it comes back identical to a scan that looked
/// everywhere and found nothing — which is the mistake this thread made three times in one
/// session, each time with byte-identical output.
/// </para>
/// <para>
/// So the fixture is built with the shape that hid: <b>two scenes that move the same pair of
/// flags, one of them reachable from a map and one of them reachable from nothing at all.</b>
/// A map-first reading finds the first and is silent about the second, and silence is what
/// this whole file exists to make impossible.
/// </para>
/// </summary>
public class EverywhereInTheImageTests
{
    private const byte SetVar = 0x16;
    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte Goto = 0x05;
    private const byte SetFlag = 0x29;
    private const byte ClearFlag = 0x2A;
    private const byte LoadPointer = 0x0F;
    private const byte Release = 0x6C;
    private const byte End = 0x02;

    private const int Holds = 0x003E;    // holds people in place
    private const int Keeps = 0x003F;    // keeps people off the map

    /// <summary>Where the trigger a map points at begins.</summary>
    private const int Trigger = 0x100;

    /// <summary>Where the block both scenes share begins — the one the map reaches.</summary>
    private const int Shared = 0x200;

    /// <summary>The setflag inside the shared block.</summary>
    private const int InTheOpen = 0x20B;

    /// <summary>The scene nothing on any map points at.</summary>
    private const int Orphan = 0x800;

    /// <summary>Four aligned bytes holding the orphan's address, with no command in front.</summary>
    private const int Literal = 0x900;

    /// <summary>A second scene nothing opens — and this one a script really does jump into.</summary>
    private const int Deep = 0xB00;

    /// <summary>The goto that jumps into it, itself in a block nothing points at.</summary>
    private const int TheWayIn = 0xC00;

    /// <summary>A flag moved only inside the block a map reaches — not news, and not on the list.</summary>
    private const int OnlyInTheOpen = 0x0055;

    /// <summary>A flag moved only where nothing looks, and jumped into.</summary>
    private const int OnlyOutOfSight = 0x0056;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static void Pointer(byte[] image, int at, uint address)
    {
        for (int i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    private static byte[] Image()
    {
        var image = new byte[0x4000];

        // A trigger a map points at. It puts a number in a variable and jumps to the block
        // it shares with something else — which is the shape that took ten measurements.
        Put(image, Trigger, SetVar, 0x01, 0x40, 0x00, 0x00);
        Put(image, Trigger + 5, Goto);
        Pointer(image, Trigger + 6, 0x08000000 + Shared);

        // The shared block: a comparison on the number the trigger just wrote, and the flags
        // on the arm that comparison rules out.
        Put(image, Shared, Compare, 0x01, 0x40, 0x00, 0x00);
        Put(image, Shared + 5, GotoIf, 0x01);
        Pointer(image, Shared + 7, 0x08000300);
        Put(image, InTheOpen, SetFlag, Holds & 0xFF, Holds >> 8);
        Put(image, InTheOpen + 3, ClearFlag, Keeps & 0xFF, Keeps >> 8);
        Put(image, InTheOpen + 6, SetFlag, OnlyInTheOpen & 0xFF, OnlyInTheOpen >> 8);
        Put(image, InTheOpen + 9, End);

        Put(image, 0x300, Release, End);

        // And the same pair of flags again, in a piece of script no map leads to.
        Put(image, Orphan, SetFlag, Holds & 0xFF, Holds >> 8);
        Put(image, Orphan + 3, ClearFlag, Keeps & 0xFF, Keeps >> 8);
        Put(image, Orphan + 6, End);

        // Four aligned bytes holding its address, with nothing in front of them. This is what
        // a table entry or a literal in compiled code looks like, and it is the only thing
        // that names the orphan.
        Pointer(image, Literal, 0x08000000 + (uint)Orphan);

        // A decoy that names it and is not a way in: loadpointer's argument is text.
        Put(image, 0x910, LoadPointer, 0x00);
        Pointer(image, 0x912, 0x08000000 + (uint)Orphan);

        // And a decoy that is neither: four bytes holding the address, off any boundary, with
        // no command in front. Sixteen megabytes contain sixty thousand of these by accident.
        Pointer(image, 0x921, 0x08000000 + (uint)Orphan);

        // A second scene nothing opens, and a goto into it from a block nothing points at
        // either. Being jumped into on purpose is what separates a job from a coincidence.
        Put(image, Deep, SetFlag, OnlyOutOfSight & 0xFF, OnlyOutOfSight >> 8, End);
        Put(image, TheWayIn, Goto);
        Pointer(image, TheWayIn + 1, 0x08000000 + (uint)Deep);

        // A hit on the flag pattern that is not a setflag at all — three bytes in the middle
        // of something, with bytes after them that are not commands.
        Put(image, 0xA03, SetFlag, Holds & 0xFF, Holds >> 8, 0xFF, 0xFF, 0xFF);

        return image;
    }

    private static Rom Rom() => new(Image());

    /// <summary>The one script a map points at, which is the whole of the map-first reading.</summary>
    private static SetsAFlag[] TheWorld() => [new SetsAFlag("1.1", "trigger (0,0)", 0x08000000 + Trigger)];

    /// <summary>
    /// The finding, stated as plainly as it can be: the file moves this flag somewhere the
    /// scripts the maps reach do not go.
    /// </summary>
    [Fact]
    public void ItFindsASiteNoMapLeadsTo()
    {
        Rom rom = Rom();

        IReadOnlyList<FlagSite> sites = EverywhereInTheImage.Moves(rom, Holds, EverywhereInTheImage.Opened(rom, TheWorld()));

        Assert.Contains(sites, s => s.Offset == Orphan && s.ReadsAsAScript && !s.Opened);
    }

    /// <summary>
    /// And it is not simply blind to the one the maps do reach — which would find the orphan
    /// for the wrong reason and report every site in the cartridge as a discovery.
    /// </summary>
    [Fact]
    public void TheSiteAMapDoesLeadToIsMarkedAsOpened()
    {
        Rom rom = Rom();

        IReadOnlyList<FlagSite> sites = EverywhereInTheImage.Moves(rom, Holds, EverywhereInTheImage.Opened(rom, TheWorld()));

        Assert.Contains(sites, s => s.Offset == InTheOpen && s.Opened);
    }

    /// <summary>
    /// <b>Following the jumps, not reading a straight line.</b> The flag in the open is behind
    /// a <c>goto</c>; a reading that stopped at the handoff would call it unopened, which turns
    /// every ordinary script in the game into a discovery and buries the one real one.
    /// </summary>
    [Fact]
    public void WhatOneScriptOpensIncludesTheBlocksItJumpsTo()
    {
        Rom rom = Rom();

        int[] covered = EverywhereInTheImage.Opened(rom, TheWorld());

        Assert.NotEqual(EverywhereInTheImage.Nobody, covered[Shared]);
        Assert.NotEqual(EverywhereInTheImage.Nobody, covered[InTheOpen]);
    }

    /// <summary>
    /// And a command's arguments are opened as well as its opcode. A byte scan hits addresses
    /// inside arguments constantly, and "was this byte read" is the question — marking only the
    /// opcodes answers a different one that looks the same.
    /// </summary>
    [Fact]
    public void ACommandsArgumentsAreOpenedTooNotJustItsOpcode()
    {
        int[] covered = EverywhereInTheImage.Opened(Rom(), TheWorld());

        Assert.NotEqual(EverywhereInTheImage.Nobody, covered[Trigger + 1]);
        Assert.NotEqual(EverywhereInTheImage.Nobody, covered[InTheOpen + 1]);
    }

    /// <summary>Nothing points at the orphan, so nothing opens it.</summary>
    [Fact]
    public void WhatNoMapLeadsToIsNotOpened()
    {
        int[] covered = EverywhereInTheImage.Opened(Rom(), TheWorld());

        Assert.Equal(EverywhereInTheImage.Nobody, covered[Orphan]);
        Assert.Equal(EverywhereInTheImage.Nobody, covered[Literal]);
    }

    /// <summary>
    /// A hit whose following bytes are not commands is reported and marked as not reading like
    /// script. Both halves matter: swallowing it hides how noisy the instrument is, and
    /// trusting it invents a scene out of three bytes.
    /// </summary>
    [Fact]
    public void AHitInTheMiddleOfSomethingElseIsMarkedAsNotScript()
    {
        IReadOnlyList<FlagSite> sites = EverywhereInTheImage.Moves(Rom(), Holds);

        FlagSite noise = Assert.Single(sites, s => s.Offset == 0xA03);

        Assert.False(noise.ReadsAsAScript);
    }

    /// <summary>
    /// <b>Both ways a flag moves.</b> Three flags in the middle of this game are opened by a
    /// <c>clearflag</c> and nothing else, and a scan that looked only for <c>setflag</c> would
    /// report the flag keeping seven people off SAFFRON as moved by nothing anywhere.
    /// </summary>
    [Fact]
    public void AFlagOnlyEverClearedIsStillFound()
    {
        IReadOnlyList<FlagSite> sites = EverywhereInTheImage.Moves(Rom(), Keeps);

        Assert.Equal(2, sites.Count);
        Assert.All(sites, s => Assert.False(s.Sets));
    }

    /// <summary>
    /// The question the pair was built for: one piece of script that moves both.
    /// </summary>
    [Fact]
    public void TwoFlagsMovedCloseTogetherComeBackAsOneScene()
    {
        Rom rom = Rom();

        IReadOnlyList<(FlagSite First, FlagSite Second)> pairs = EverywhereInTheImage.Together(
            EverywhereInTheImage.Moves(rom, Holds), EverywhereInTheImage.Moves(rom, Keeps));

        Assert.Equal(2, pairs.Count);
        Assert.Contains(pairs, p => p.First.Offset == Orphan && p.Second.Offset == Orphan + 3);
        Assert.Contains(pairs, p => p.First.Offset == InTheOpen && p.Second.Offset == InTheOpen + 3);
    }

    /// <summary>
    /// And two sites far apart are two scenes. Without a distance this pairs everything with
    /// everything and reports a cartridge full of scenes.
    /// </summary>
    [Fact]
    public void TwoFlagsMovedFarApartAreNotOneScene()
    {
        Rom rom = Rom();

        IReadOnlyList<(FlagSite First, FlagSite Second)> pairs = EverywhereInTheImage.Together(
            EverywhereInTheImage.Moves(rom, Holds), EverywhereInTheImage.Moves(rom, Keeps));

        Assert.DoesNotContain(pairs, p => p.First.Offset == Orphan && p.Second.Offset == InTheOpen + 3);
    }

    /// <summary>A jump into a block is found, and named as the command that carries it.</summary>
    [Fact]
    public void AGotoIntoABlockIsFoundAsAWayIn()
    {
        Rom rom = Rom();

        IReadOnlyList<NamesIt> names = EverywhereInTheImage.WhoNames(
            rom, EverywhereInTheImage.PointerIndex(rom), 0x08000000 + Shared);

        NamesIt jump = Assert.Single(names, n => n.Offset == Trigger + 6);

        Assert.True(jump.AJump);
    }

    /// <summary>
    /// <b>The conditional pair put a condition byte between the opcode and the pointer.</b>
    /// Reading only the byte in front of a pointer finds <c>call</c> and <c>goto</c> and misses
    /// every conditional jump in the cartridge — which is most of them, and the miss reads as
    /// "nothing jumps here".
    /// </summary>
    [Fact]
    public void AConditionalJumpIsAWayInToo()
    {
        Rom rom = Rom();

        IReadOnlyList<NamesIt> names = EverywhereInTheImage.WhoNames(
            rom, EverywhereInTheImage.PointerIndex(rom), 0x08000300);

        NamesIt jump = Assert.Single(names, n => n.Offset == Shared + 7);

        Assert.True(jump.AJump);
    }

    /// <summary>
    /// A block is jumped into at its first command, and what is being hunted is some way
    /// inside it. Asking for the exact address answers "nothing names this" correctly and
    /// uselessly.
    /// </summary>
    [Fact]
    public void TheSlackFindsWhoNamesTheBlockAnAddressIsInside()
    {
        Rom rom = Rom();
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        Assert.Empty(EverywhereInTheImage.WhoNames(rom, index, 0x08000000 + InTheOpen));

        Assert.Contains(
            EverywhereInTheImage.WhoNames(rom, index, 0x08000000 + InTheOpen, 192),
            n => n.Offset == Trigger + 6);
    }

    /// <summary>
    /// <b>The positive finding.</b> Four aligned bytes holding an address, with no command in
    /// front of them, are a table entry or a literal in compiled code — which is the code
    /// boundary with an offset on it, and the answer rather than the absence of one.
    /// </summary>
    [Fact]
    public void AnAlignedPointerWithNoCommandInFrontIsALiteral()
    {
        Rom rom = Rom();

        IReadOnlyList<NamesIt> names = EverywhereInTheImage.WhoNames(
            rom, EverywhereInTheImage.PointerIndex(rom), 0x08000000 + Orphan);

        NamesIt literal = Assert.Single(names, n => n.Offset == Literal);

        Assert.True(literal.ALiteral);
        Assert.False(literal.AJump);
    }

    /// <summary>
    /// And a <c>loadpointer</c> naming the same address is not a way in. Its argument is text,
    /// and counting it as a jump turns every message that happens to sit near a script into a
    /// route somebody could walk.
    /// </summary>
    [Fact]
    public void ALoadPointerNamingItIsNotAWayIn()
    {
        Rom rom = Rom();

        IReadOnlyList<NamesIt> names = EverywhereInTheImage.WhoNames(
            rom, EverywhereInTheImage.PointerIndex(rom), 0x08000000 + Orphan);

        NamesIt text = Assert.Single(names, n => n.Offset == 0x912);

        Assert.False(text.AJump);
        Assert.False(text.ALiteral);
    }

    /// <summary>
    /// And four bytes off any boundary with nothing in front of them are neither. There are
    /// sixty thousand of these in a real image and calling them literals would report the code
    /// boundary everywhere.
    /// </summary>
    [Fact]
    public void FourLooseBytesAreNeitherAJumpNorALiteral()
    {
        Rom rom = Rom();

        IReadOnlyList<NamesIt> names = EverywhereInTheImage.WhoNames(
            rom, EverywhereInTheImage.PointerIndex(rom), 0x08000000 + Orphan);

        NamesIt loose = Assert.Single(names, n => n.Offset == 0x921);

        Assert.False(loose.AJump);
        Assert.False(loose.ALiteral);
    }

    /// <summary>
    /// The error bar, and it has to move with both things it depends on. A number that does
    /// not is a decoration.
    /// </summary>
    [Fact]
    public void TheNoiseFigureRisesWithTheImageAndFallsWithThePattern()
    {
        var small = new Rom(new byte[0x1000]);
        var large = new Rom(new byte[0x2000]);

        Assert.Equal(EverywhereInTheImage.ByChance(small, 3) * 2, EverywhereInTheImage.ByChance(large, 3), 9);
        Assert.Equal(EverywhereInTheImage.ByChance(large, 3) / 256, EverywhereInTheImage.ByChance(large, 4), 9);
    }

    /// <summary>
    /// A flag nothing in the file moves comes back empty rather than coming back wrong. The
    /// instrument being able to say "there is nothing here" is the only reason to build one.
    /// </summary>
    [Fact]
    public void AFlagNothingMovesComesBackEmpty()
    {
        Assert.Empty(EverywhereInTheImage.Moves(Rom(), 0x0BAD));
    }

    /// <summary>
    /// The sweep, which is the same question asked of every flag at once — and the two answers
    /// it has to keep apart. A flag moved in script no map opens is an entry point to find; a
    /// flag moved nowhere in the file is compiled code, and the difference is what to do next.
    /// </summary>
    [Fact]
    public void TheSweepSeparatesMovedWhereNothingLooksFromNotMovedAtAll()
    {
        Rom rom = Rom();

        IReadOnlyDictionary<int, IReadOnlyList<FlagSite>> moved =
            EverywhereInTheImage.EveryFlagMoved(rom, EverywhereInTheImage.Opened(rom, TheWorld()));

        Assert.Contains(moved[Holds], s => s.Offset == Orphan && !s.Opened);
        Assert.Contains(moved[Holds], s => s.Offset == InTheOpen && s.Opened);
        Assert.False(moved.ContainsKey(0x0BAD));
    }

    /// <summary>
    /// <b>And the sweep drops what the targeted read reports.</b> A hundred and thirty thousand
    /// raw hits land in a real image by accident, nearly all of them in the middle of somebody
    /// else's argument — a sweep that kept them would report every flag in the game as moved
    /// somewhere, which is the same as reporting nothing.
    /// </summary>
    [Fact]
    public void TheSweepDropsAHitThatDoesNotReadAsScript()
    {
        Rom rom = Rom();

        Assert.DoesNotContain(EverywhereInTheImage.EveryFlagMoved(rom)[Holds], s => s.Offset == 0xA03);
        Assert.Contains(EverywhereInTheImage.Moves(rom, Holds), s => s.Offset == 0xA03 && !s.ReadsAsAScript);
    }

    /// <summary>
    /// <b>The control has to be a control.</b> Reversing keeps every byte and destroys every
    /// command boundary, so a scene that is really there does not survive it — and a figure
    /// that came back equal to the real count would be a decoration sitting where the error bar
    /// is supposed to be.
    /// </summary>
    [Fact]
    public void ReversingTheImageDestroysTheScenesInIt()
    {
        Rom rom = Rom();

        int real = EverywhereInTheImage.EveryFlagMoved(rom).Values.Sum(s => s.Count);

        Assert.Equal(6, real);
        Assert.True(EverywhereInTheImage.NoiseFloor(rom).Sites < real);
    }

    /// <summary>
    /// And it has to find something when there is nothing there, or it is not measuring the
    /// filter at all. Bytes with no scripts in them still produce sites, which is the whole
    /// reason the figure is printed.
    /// </summary>
    [Fact]
    public void TheNoiseFloorIsNotZeroOnBytesThatAreOnlyNoise()
    {
        var bytes = new byte[1 << 20];

        new Random(20250817).NextBytes(bytes);

        Assert.True(EverywhereInTheImage.NoiseFloor(new Rom(bytes)).Sites > 0);
    }

    /// <summary>
    /// <b>The rule that decides which flags are news, kept where a fixture can hold it.</b>
    /// Three times this project has written a classification like this into the reporting
    /// layer, which has no tests, and three times it was wrong somewhere nothing could reach.
    /// </summary>
    [Fact]
    public void PastTheBoundaryPromotesTheOneAScriptJumpsInto()
    {
        Rom rom = Rom();

        IReadOnlyList<EverywhereInTheImage.OutsideTheWorld> outside = EverywhereInTheImage.PastTheBoundary(
            rom,
            EverywhereInTheImage.PointerIndex(rom),
            [Holds, Keeps, OnlyInTheOpen, OnlyOutOfSight, 0x0BAD],
            EverywhereInTheImage.EveryFlagMoved(rom, EverywhereInTheImage.Opened(rom, TheWorld())));

        EverywhereInTheImage.OutsideTheWorld jumped = Assert.Single(outside, f => f.Flag == OnlyOutOfSight);

        Assert.Contains(jumped.JumpedInto, s => s.Offset == Deep);

        // And the orphan, which nothing jumps into: still news, still not promoted.
        EverywhereInTheImage.OutsideTheWorld held = Assert.Single(outside, f => f.Flag == Holds);

        Assert.Contains(held.Unopened, s => s.Offset == Orphan);
        Assert.Empty(held.JumpedInto);
    }

    /// <summary>
    /// A flag whose every site the map scan already opened is not news. Without that, this list
    /// is every flag in the game and the two the file really is hiding are somewhere in it.
    /// </summary>
    [Fact]
    public void AFlagTheMapScanAlreadyOpensIsNotOnTheList()
    {
        Rom rom = Rom();

        IReadOnlyList<EverywhereInTheImage.OutsideTheWorld> outside = EverywhereInTheImage.PastTheBoundary(
            rom,
            EverywhereInTheImage.PointerIndex(rom),
            [Holds, Keeps, OnlyInTheOpen, OnlyOutOfSight, 0x0BAD],
            EverywhereInTheImage.EveryFlagMoved(rom, EverywhereInTheImage.Opened(rom, TheWorld())));

        Assert.DoesNotContain(outside, f => f.Flag == OnlyInTheOpen);
        Assert.DoesNotContain(outside, f => f.Flag == 0x0BAD);
    }

    /// <summary>
    /// <b>Which script opened a byte, not merely that one did.</b> A climb that reaches an
    /// opened byte and says "a map leads here" has answered a question nobody asked; the next
    /// question is always <em>which</em>, and it costs an index to answer.
    /// </summary>
    [Fact]
    public void AnOpenedByteNamesTheScriptThatOpenedIt()
    {
        SetsAFlag[] world = TheWorld();

        int[] covered = EverywhereInTheImage.Opened(Rom(), world);

        Assert.Equal("1.1 trigger (0,0)", world[covered[InTheOpen]].ToString());
    }

    /// <summary>
    /// And the control covers the promoted filter too. A noise figure on the raw count with
    /// none on the filtered one leaves the filtered one looking rigorous by association — which
    /// is the number anybody would actually act on.
    /// </summary>
    [Fact]
    public void TheControlMeasuresTheJumpedIntoFilterAsWellAsTheRawOne()
    {
        var bytes = new byte[1 << 21];

        new Random(20260817).NextBytes(bytes);

        (int sites, int jumpedInto) = EverywhereInTheImage.NoiseFloor(new Rom(bytes));

        Assert.True(sites > 0);
        Assert.True(jumpedInto < sites);
    }

    /// <summary>And it finds a flag that is only ever cleared, for the same reason as above.</summary>
    [Fact]
    public void TheSweepFindsAFlagOnlyEverCleared()
    {
        IReadOnlyDictionary<int, IReadOnlyList<FlagSite>> moved =
            EverywhereInTheImage.EveryFlagMoved(Rom());

        Assert.Equal(2, moved[Keeps].Count);
        Assert.All(moved[Keeps], s => Assert.False(s.Sets));
    }
}
