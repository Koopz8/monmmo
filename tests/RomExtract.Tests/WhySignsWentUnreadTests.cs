using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The signs a run never read, sorted by whether anything could have read them.
/// <para>
/// <b>241 printed "215 of the 519 sign scripts ran" and nothing about the rest.</b> That number
/// reads the same whether the run is a few rooms short or three hundred signs are written on
/// walls nothing can walk up to, and those are opposite findings. It is the join
/// <c>WhyTheGatesAreShut</c> makes for flags, one list over.
/// </para>
/// <para>
/// <b>And 215 was wrong</b>, which this milestone found by needing a key with one entry per sign.
/// A sign is a SQUARE; the address is a script and 519 of them sit at 360 addresses, so keying
/// the read set on (map, address) counted two signs on one map sharing a block as one. The real
/// numbers are 317 / 396 / 465. 224 is the milestone about exactly this and it is now the
/// milestone about it twice.
/// </para>
/// </summary>
public sealed class WhySignsWentUnreadTests
{
    private const uint TheSign = 0x3000;
    private const uint TheOtherSign = 0x3100;

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>A one-wide column, walkable except where said otherwise.</summary>
    private static MapData Column(string id, params byte[] collision) =>
        new(id, id, 1, collision.Length, collision);

    private static IReadOnlyList<UnreadSign> Of(
        WorldData world, IEnumerable<string> reached, params RanASign[] read) =>
        WhySignsWentUnread.Of(world, read, [.. reached]);

    // -------------------------------------------------------------- the file's own answer

    /// <summary>
    /// THE THING: a sign with no walkable square among the five it is read from is not a reach
    /// problem. Walking further cannot fix it and nothing ever will.
    /// </summary>
    [Fact]
    public void ASignNothingCanStandBesideIsNotAReachProblem()
    {
        // Three squares: the near one open, then two solid. The sign is on the far one and
        // neither it nor its only neighbour can be stood on.
        var world = new WorldData(
        [
            Column("1.0", 0, 1, 1) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
        ]);

        UnreadSign one = Assert.Single(Of(world, ["1.0"]));

        Assert.Equal(UnreadBecause.NothingCouldStandBesideIt, one.Why);
    }

    /// <summary>
    /// And a sign's OWN square is solid in this cartridge — that is what a sign is — so the rule
    /// has to be the five squares and not the one. With only its own square asked, every sign in
    /// the game reads as one nothing could stand beside.
    /// </summary>
    [Fact]
    public void ASignOnASolidSquareWithFloorNextToItCanBeStoodBeside()
    {
        // The sign is on the middle square, which is solid, and the near square is open.
        var world = new WorldData(
        [
            Column("1.0", 0, 1, 1) with { Signs = [new MapSign(0, 1, Kind: 0, TheSign)] },
        ]);

        UnreadSign one = Assert.Single(Of(world, ["1.0"]));

        Assert.Equal(UnreadBecause.ItNeverGotToThatWall, one.Why);
    }

    /// <summary>
    /// And it is a fact about the FILE, so it does not move when the run does — 211's rule, and
    /// the check that caught a bucket named for the wrong cause.
    /// </summary>
    [Fact]
    public void AndThatAnswerDoesNotMoveWhenTheRunDoes()
    {
        var world = new WorldData(
        [
            Column("1.0", 0, 1, 1) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
        ]);

        Assert.Equal(
            UnreadBecause.NothingCouldStandBesideIt,
            Assert.Single(Of(world, ["1.0"])).Why);

        Assert.Equal(
            UnreadBecause.NothingCouldStandBesideIt,
            Assert.Single(Of(world, [])).Why);
    }

    /// <summary>
    /// THE DISCRIMINATION: water is opened when this is asked, because a sign a surfing run
    /// reads is not a sign nothing can stand beside — and filing it as one would put a bucket
    /// about the cartridge under a lever.
    /// </summary>
    [Fact]
    public void ASignBesideNothingButWaterIsStillOneSomethingCanReach()
    {
        var world = new WorldData(
        [
            Column("1.0", 0, 1, 1) with
            {
                // The middle square is water rather than wall: solid on foot, open surfing.
                Behaviours = [0, (byte)MetatileBehaviour.PondWater, 0],
                Signs = [new MapSign(0, 2, Kind: 0, TheSign)],
            },
        ]);

        UnreadSign one = Assert.Single(Of(world, ["1.0"]));

        Assert.Equal(UnreadBecause.ItNeverGotToThatWall, one.Why);
    }

    // ------------------------------------------------------------------ and the run's answers

    /// <summary>A sign on a map the run never reached is a reach problem and says so.</summary>
    [Fact]
    public void ASignOnAMapItNeverReachedIsAReachProblem()
    {
        var world = new WorldData(
        [
            Column("1.0", 0, 0, 0) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
        ]);

        Assert.Equal(UnreadBecause.OnAMapItNeverReached, Assert.Single(Of(world, [])).Why);
    }

    /// <summary>And one on a map it walked, at a wall it never got to, is the third answer.</summary>
    [Fact]
    public void ASignOnAReachedMapItNeverWalkedUpToIsTheThirdAnswer()
    {
        var world = new WorldData(
        [
            Column("1.0", 0, 0, 0) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
        ]);

        Assert.Equal(UnreadBecause.ItNeverGotToThatWall, Assert.Single(Of(world, ["1.0"])).Why);
    }

    /// <summary>
    /// THE ORDER: a sign that is both unreachable and on an unreached map is the FILE's answer.
    /// Calling it a reach problem claims that walking further would fix something walking
    /// cannot fix.
    /// </summary>
    [Fact]
    public void TheFilesAnswerComesBeforeTheRunsAnswer()
    {
        var world = new WorldData(
        [
            Column("1.0", 0, 1, 1) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
        ]);

        Assert.Equal(UnreadBecause.NothingCouldStandBesideIt, Assert.Single(Of(world, [])).Why);
    }

    /// <summary>And a sign it DID read is not in the list at all.</summary>
    [Fact]
    public void ASignItReadIsNotUnread()
    {
        var world = new WorldData(
        [
            Column("1.0", 0, 0, 0) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
        ]);

        Assert.Empty(
            Of(world, ["1.0"], new RanASign("1.0", new GridPosition(0, 2), TheSign, 1)));
    }

    /// <summary>
    /// And a sign read on ANOTHER map is not this one — the key is (map, square) on both sides
    /// or a shared block read in one town marks it read in every town.
    /// </summary>
    [Fact]
    public void ASignReadInAnotherTownIsNotThisOne()
    {
        var world = new WorldData(
        [
            Column("1.0", 0, 0, 0) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
            Column("2.0", 0, 0, 0) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
        ]);

        UnreadSign one = Assert.Single(
            Of(world, ["1.0", "2.0"], new RanASign("1.0", new GridPosition(0, 2), TheSign, 1)));

        Assert.Equal("2.0", one.MapId);
    }

    // ------------------------------------------------------- and the count that was wrong

    /// <summary>
    /// TWO SIGNS ON ONE MAP SHARING A BLOCK ARE TWO SIGNS. Keyed on (map, address) they were
    /// one, which is how 241 reported 215 where the answer is 317.
    /// </summary>
    [Fact]
    public void TwoSignsOnOneMapSharingABlockAreTwoSigns()
    {
        var world = new WorldData(
        [
            new MapData("1.0", "1.0", 4, 4, new byte[16])
            {
                Signs =
                [
                    new MapSign(1, 1, Kind: 0, TheSign),
                    new MapSign(3, 3, Kind: 0, TheSign),
                ],
            },
        ]);

        Attempt played = Autoplayer.Play(
            world, "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.Equal(2, played.SignsRead.Count);
        Assert.Single(played.SignsRead.Select(s => s.Address).Distinct());
        Assert.Empty(WhySignsWentUnread.Of(world, played.SignsRead, [.. played.Reached]));
    }

    // ------------------------------------------------------- the side, in the SORTING (283)

    /// <summary>A five-by-five room, walkable except where said.</summary>
    private static MapData Room(params (int X, int Y)[] solid)
    {
        var collision = new byte[25];

        foreach ((int x, int y) in solid) collision[(y * 5) + x] = 1;

        return new MapData("1.0", "room", 5, 5, collision);
    }

    /// <summary>
    /// <b>THE THING (283).</b> A sign whose kind names the SOUTH, with the south square walled and
    /// the other three open, is a sign nothing can ever read — and this class sorted it as a reach
    /// problem until now, because it asked 242's five-square question about a sign 280 reads from
    /// one. One rule in two places disagreeing is the fault; the walk had the new one.
    /// </summary>
    [Fact]
    public void ASignWhoseNamedSideIsAWallIsNotAReachProblem()
    {
        var world = new WorldData(
        [
            Room((2, 2), (2, 3)) with { Signs = [new MapSign(2, 2, MapSign.FromTheSouth, TheSign)] },
        ]);

        Assert.Equal(
            UnreadBecause.NothingCouldStandBesideIt,
            Assert.Single(WhySignsWentUnread.Of(world, [], ["1.0"])).Why);
    }

    /// <summary>
    /// And the control, in the same process: with the side ignored the very same sign is a reach
    /// problem, because three of its four neighbours are open. This is the whole difference the
    /// parameter buys, and on the cartridge it is worth nothing — 279 read the side FROM the named
    /// square being walkable on every record of those kinds, so no sign in the file can fail it.
    /// The difference is real, and only a fixture can show it.
    /// </summary>
    [Fact]
    public void AndWithTheSideIgnoredTheSameSignIsAReachProblem()
    {
        var world = new WorldData(
        [
            Room((2, 2), (2, 3)) with { Signs = [new MapSign(2, 2, MapSign.FromTheSouth, TheSign)] },
        ]);

        Assert.Equal(
            UnreadBecause.ItNeverGotToThatWall,
            Assert.Single(WhySignsWentUnread.Of(world, [], ["1.0"], obeySignSides: false)).Why);
    }

    /// <summary>
    /// And a sign of the same kind with its named side OPEN is a reach problem, so the rule is
    /// about the square and not about the kind. Without this, "every sign of kind one is
    /// unreadable" passes the test above.
    /// </summary>
    [Fact]
    public void ASignWhoseNamedSideIsOpenIsStillAReachProblem()
    {
        var world = new WorldData(
        [
            Room((2, 2)) with { Signs = [new MapSign(2, 2, MapSign.FromTheSouth, TheSign)] },
        ]);

        Assert.Equal(
            UnreadBecause.ItNeverGotToThatWall,
            Assert.Single(WhySignsWentUnread.Of(world, [], ["1.0"])).Why);
    }

    // ------------------------------------------------------------ at NO setting (283)

    /// <summary>
    /// <b>A sign read at ONE setting is read.</b> Six lever settings walk six distances, so one
    /// run's unread list is a fact about that lever — and "the sign scripts that run at no
    /// setting" has been in this project's prompt since 241 with no instrument that could answer
    /// it.
    /// </summary>
    [Fact]
    public void ASignOneRunReadIsNotUnreadAtNoSetting()
    {
        var world = new WorldData(
        [
            Column("1.0", 0, 0, 0) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
        ]);

        RanASign it = new("1.0", new GridPosition(0, 2), TheSign, 1);

        Assert.Empty(WhySignsWentUnread.AtNoSetting(
            world, [([], ["1.0"]), ([it], ["1.0"])]));

        // And the run that missed it, asked alone, still says it missed it.
        Assert.Single(WhySignsWentUnread.Of(world, [], ["1.0"]));
    }

    /// <summary>
    /// And REACHED is unioned too: a map one setting reaches is not a map nothing reaches, so a
    /// sign on it is the third answer rather than the second.
    /// </summary>
    [Fact]
    public void AMapOneRunReachedIsReached()
    {
        var world = new WorldData(
        [
            Column("1.0", 0, 0, 0) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
        ]);

        Assert.Equal(
            UnreadBecause.ItNeverGotToThatWall,
            Assert.Single(WhySignsWentUnread.AtNoSetting(world, [([], []), ([], ["1.0"])])).Why);

        Assert.Equal(
            UnreadBecause.OnAMapItNeverReached,
            Assert.Single(WhySignsWentUnread.AtNoSetting(world, [([], []), ([], [])])).Why);
    }

    /// <summary>
    /// No runs at all is every scripted sign unread, sorted by the file first — the union of
    /// nothing, which is the shape that would otherwise quietly report a clean sweep.
    /// </summary>
    [Fact]
    public void NoRunsAtAllIsEverySignUnread()
    {
        var world = new WorldData(
        [
            Column("1.0", 0, 1, 1) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
            Column("2.0", 0, 0, 0) with { Signs = [new MapSign(0, 2, Kind: 0, TheOtherSign)] },
        ]);

        IReadOnlyList<UnreadSign> none = WhySignsWentUnread.AtNoSetting(world, []);

        Assert.Equal(2, none.Count);
        Assert.Equal(
            [UnreadBecause.NothingCouldStandBesideIt, UnreadBecause.OnAMapItNeverReached],
            none.Select(u => u.Why));
    }

    /// <summary>
    /// And every scripted sign is accounted for exactly once — read or unread, never both and
    /// never neither. Without this a classifier that quietly drops a case reads as a clean
    /// answer.
    /// </summary>
    [Fact]
    public void EveryScriptedSignIsEitherReadOrUnread()
    {
        var world = new WorldData(
        [
            new MapData("1.0", "1.0", 4, 4, new byte[16])
            {
                Signs =
                [
                    new MapSign(1, 1, Kind: 0, TheSign),
                    new MapSign(3, 3, Kind: 0, TheOtherSign),
                    new MapSign(2, 2, MapSign.HiddenItem, ScriptAddress: 0),
                ],
            },
            Column("2.0", 0, 0, 0) with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
        ]);

        Attempt played = Autoplayer.Play(world, "1.0", TestRules.All, (_, _, _) => Nothing);

        int scripted = world.Maps.Sum(m => m.Signs.Count(s => s.HasScript));

        Assert.Equal(3, scripted);
        Assert.Equal(
            scripted,
            played.SignsRead.Count
            + WhySignsWentUnread.Of(world, played.SignsRead, [.. played.Reached]).Count);
    }
}
