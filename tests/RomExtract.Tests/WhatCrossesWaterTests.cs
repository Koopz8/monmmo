using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The move that crosses water, and the walk that used to be told about it.
/// <para>
/// <c>--surf</c> was a lever standing in for a fact: the walker has always been able to swim
/// and nothing ever asked whether the party could, so 1245 squares across 35 maps were a wall
/// or an open sea depending on a command-line flag. The cartridge decides it the other way
/// round — the one block in the image that offers to cross water opens by asking who knows the
/// move and stops if the answer is nobody.
/// </para>
/// <para>
/// Two halves, and both are here: finding that block by its shape, and the walk asking the
/// same question of its own party.
/// </para>
/// </summary>
public class WhatCrossesWaterTests
{
    private const byte AsksWhoKnows = ObstacleMoves.FindMove;   // 0x7C
    private const byte LoadPointer = 0x0F;
    private const byte CallStandard = 0x09;
    private const byte DoFieldEffect = 0x9C;
    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte Release = 0x6C;
    private const byte End = 0x02;

    private const byte TheYesOrNo = 0x05;
    private const byte AnOrdinaryLine = 0x04;

    private const int TheMove = 57;
    private const int TheEffect = 9;

    private const uint ThePrompt = 0x08000400;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static void Pointer(byte[] image, int at, uint address)
    {
        for (int i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>
    /// The shape the cartridge uses: ask who knows it, bail if nobody does, put a yes-or-no
    /// on the screen, and do the field effect.
    /// </summary>
    private static byte[] Image(byte standard = TheYesOrNo, int move = TheMove)
    {
        var image = new byte[0x1000];

        Put(image, 0x200, AsksWhoKnows, (byte)(move & 0xFF), (byte)(move >> 8));
        Put(image, 0x203, Compare, 0x0D, 0x80, 0x06, 0x00);
        Put(image, 0x208, GotoIf, 0x01);
        Pointer(image, 0x20A, 0x08000300);
        Put(image, 0x20E, LoadPointer, 0x00);
        Pointer(image, 0x210, ThePrompt);
        Put(image, 0x214, CallStandard, standard);
        Put(image, 0x216, DoFieldEffect, TheEffect, 0x00);
        Put(image, 0x219, Release, End);

        Put(image, 0x300, Release, End);

        return image;
    }

    private static MoveSite Only(byte[] image) =>
        Assert.Single(EverywhereInTheImage.AsksWhoKnows(new Rom(image), 355)
            .Where(s => s.ReadsAsAScript));

    [Fact]
    public void ABlockThatAsksAndThenOffersIsFound()
    {
        MoveSite site = Only(Image());

        Assert.Equal(TheMove, site.Move);
        Assert.True(site.Offers);
        Assert.Equal(TheEffect, site.FieldEffect);
        Assert.Equal(ThePrompt, site.Question);
    }

    /// <summary>
    /// AND THE DISCRIMINATION. A block that asks who knows a move and says something without
    /// asking is not offering to do anything — which matters, because a raw sweep for this
    /// three-byte pattern finds 600 sites on the real cartridge and 787 on the same file
    /// backwards. The offer is the whole difference between a scene and a coincidence.
    /// </summary>
    [Fact]
    public void ABlockThatOnlyTalksIsNotOfferingAnything()
    {
        MoveSite site = Only(Image(standard: AnOrdinaryLine));

        Assert.Equal(TheMove, site.Move);
        Assert.False(site.Offers);
        Assert.Equal(0u, site.Question);
    }

    /// <summary>A move id past the end of this cartridge's own table is not a move id.</summary>
    [Fact]
    public void AMoveIdPastTheTableIsNotOne()
    {
        Assert.Empty(EverywhereInTheImage.AsksWhoKnows(new Rom(Image(move: 900)), 355));
    }

    /// <summary>
    /// <b>And the same sweep takes a RANGE, which is what its own floor needs (284).</b> A sweep
    /// filtered on <c>1..355</c> has no unused id to be asked for — every id in it is a move — so
    /// the nudge 272 gave the flag and variable sweeps had to move the whole window instead. Here
    /// the window is moved onto the id the fixture uses and off it again.
    /// </summary>
    [Fact]
    public void TheSweepTakesARangeSoItsOwnFloorCanBeAWindow()
    {
        var rom = new Rom(Image(move: 900));

        Assert.Empty(EverywhereInTheImage.AsksWhoKnows(rom, 1, 355));
        Assert.Single(EverywhereInTheImage.AsksWhoKnows(rom, 356, 1000));
        Assert.Empty(EverywhereInTheImage.AsksWhoKnows(rom, 901, 1000));

        // And the bound overload is the range starting at one — the same answer, so the two
        // cannot drift apart.
        Assert.Equal(
            EverywhereInTheImage.AsksWhoKnows(new Rom(Image()), 355).Count,
            EverywhereInTheImage.AsksWhoKnows(new Rom(Image()), 1, 355).Count);
    }

    /// <summary>
    /// And a site found through a moved window is the SAME site, offer and all — otherwise the
    /// floor would be measuring a weaker version of the thing it is a floor for.
    /// </summary>
    [Fact]
    public void AWindowedSiteIsReadTheSameWayAsARealOne()
    {
        MoveSite through = Assert.Single(
            EverywhereInTheImage.AsksWhoKnows(new Rom(Image(move: 900)), 356, 1000)
                .Where(s => s.ReadsAsAScript));

        Assert.Equal(900, through.Move);
        Assert.True(through.Offers);
        Assert.Equal(TheEffect, through.FieldEffect);
    }

    /// <summary>
    /// And the floor under all of it: the same sweep on the image backwards. This one is a
    /// claim about the instrument rather than about the cartridge — a control that cannot come
    /// back with a number is not a control.
    /// </summary>
    [Fact]
    public void TheReversedImageIsSweptTheSameWay()
    {
        (int sites, _, int jumped, _) = EverywhereInTheImage.MoveNoiseFloor(new Rom(Image()), 355);

        Assert.True(sites >= 0);
        Assert.Equal(0, jumped);
    }

    // AND THE WALK, WHICH IS THE OTHER HALF OF THE PATH.

    /// <summary>
    /// A shore: one row of dry land with somebody standing on it, and open water beyond.
    /// <para>
    /// The land is where the run has to be able to stand — a map that is all sea has nobody to
    /// talk to and reports the same nothing whether or not it can swim, which is a fixture
    /// that cannot fail rather than a hard one.
    /// </para>
    /// </summary>
    private static MapData Sea()
    {
        var behaviours = new byte[16];

        for (var i = 4; i < behaviours.Length; i++) behaviours[i] = MetatileBehaviour.Water;

        return new MapData("1.0", "1.0", 4, 4, new byte[16]) { Behaviours = behaviours };
    }

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    private static MapObject Person(int localId, uint script) =>
        new(localId, 1, localId, 1, Direction.Down, 0, false) { ScriptAddress = script };

    private static Attempt Walk(bool teachIt, bool surfAnyway = false) =>
        Autoplayer.Play(
            new WorldData([Sea() with { Objects = [Person(1, 0x1000)] }]),
            "1.0",
            TestRules.All,
            (_, _, _) => teachIt ? Nothing with { Teaches = [TestRules.SurfMove] } : Nothing,
            surfing: surfAnyway);

    /// <summary>
    /// A party that learns it swims, and the run says which pass it learned on. "It can swim"
    /// and "it could swim in time for that to matter" are different claims.
    /// </summary>
    [Fact]
    public void APartyThatLearnsTheMoveCrossesWater()
    {
        Attempt played = Walk(teachIt: true);

        Assert.Equal(TestRules.SurfMove, played.SurfMove);
        Assert.True(played.LearnedToCrossOnPass > 0, "it never learned it");
        Assert.False(played.SwamAnyway);
    }

    /// <summary>And one that does not, does not — with the sea reported as a wall.</summary>
    [Fact]
    public void APartyThatDoesNotKnowItFindsTheSeaAWall()
    {
        Attempt played = Walk(teachIt: false);

        Assert.Equal(0, played.LearnedToCrossOnPass);
        Assert.False(played.SwamAnyway);
    }

    /// <summary>
    /// And the lever is still there, and still says it is a lever. It is what is left when the
    /// answer to the cartridge's own question is no.
    /// </summary>
    [Fact]
    public void TheLeverSwimsAnywayAndSaysSo()
    {
        Attempt played = Walk(teachIt: false, surfAnyway: true);

        Assert.True(played.SwamAnyway);
        Assert.Equal(0, played.LearnedToCrossOnPass);
    }
}
