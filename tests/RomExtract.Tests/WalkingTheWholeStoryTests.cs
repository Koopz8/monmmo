using PokeMmo.Core.World;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Maps;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Walking the world from where the game starts, and saying where it stops.
/// <para>
/// The point of this file is one design decision, so it is written down here as well as in
/// the class it tests: <b>the route is derived, not followed.</b>
/// </para>
/// <para>
/// A walkthrough for this game is easy to find and would have been easy to encode — eight
/// gyms in a known order, a known list of doors. A report built that way can only ever
/// confirm that the walkthrough is right. It cannot find that a door is missing from the
/// world file, because it never asks the world file where the doors are, and a missing door
/// is the entire question. So the walk asks the exported world what leads out of each map,
/// over and over, and whatever it arrives at is the answer.
/// </para>
/// <para>
/// Which means these tests assert the <em>shape</em> of the answer rather than a route: that
/// everything joined on is found, that everything not joined on is reported rather than
/// quietly missing, and that a door naming somewhere absent is named out loud.
/// </para>
/// </summary>
public class WalkingTheWholeStoryTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static WorldData Exported() => WorldExporter.Export(Synthetic.ToRom());

    /// <summary>A small world, joined the way this test wants rather than the fixture's way.</summary>
    private static MapData Map(string id, params Warp[] doors) =>
        new(id, id, 4, 4, new byte[16]) { Warps = doors };

    /// <summary>The walk starts where it is told and counts that as nought steps.</summary>
    [Fact]
    public void ItStartsWhereItIsTold()
    {
        var world = new WorldData([Map("1.0"), Map("1.1")]);

        StoryReach walked = StoryWalk.From(world, "1.0");

        Reached start = walked.Got.Single();

        Assert.Equal("1.0", start.MapId);
        Assert.Equal(HowReached.TheBeginning, start.How);
        Assert.Equal(0, start.Steps);
    }

    /// <summary>
    /// And a map nothing leads to is reported rather than quietly absent. This is the whole
    /// value of the report: the interesting half of the answer is the half it could not get
    /// to.
    /// </summary>
    [Fact]
    public void AndSomewhereNothingLeadsToIsReported()
    {
        var world = new WorldData([Map("1.0"), Map("1.1")]);

        StoryReach walked = StoryWalk.From(world, "1.0");

        Assert.Equal(["1.1"], walked.DidNot);
        Assert.Equal(2, walked.Total);
    }

    /// <summary>It follows a door, and says which door and how far.</summary>
    [Fact]
    public void ItFollowsADoor()
    {
        var world = new WorldData([Map("1.0", new Warp(1, 1, 0, "1.1")), Map("1.1")]);

        StoryReach walked = StoryWalk.From(world, "1.0");

        Reached arrived = walked.Got.Single(r => r.MapId == "1.1");

        Assert.Equal(HowReached.ThroughADoor, arrived.How);
        Assert.Equal("1.0", arrived.From);
        Assert.Equal(1, arrived.Steps);

        Assert.Empty(walked.DidNot);
    }

    /// <summary>And keeps going, so a chain of doors is walked to its end.</summary>
    [Fact]
    public void AndKeepsGoing()
    {
        var world = new WorldData(
        [
            Map("1.0", new Warp(1, 1, 0, "1.1")),
            Map("1.1", new Warp(1, 1, 0, "1.2")),
            Map("1.2", new Warp(1, 1, 0, "1.3")),
            Map("1.3"),
        ]);

        StoryReach walked = StoryWalk.From(world, "1.0");

        Assert.Empty(walked.DidNot);
        Assert.Equal(3, walked.Furthest);
    }

    /// <summary>And a door back does not make it walk for ever.</summary>
    [Fact]
    public void AndADoorBackDoesNotLoop()
    {
        var world = new WorldData(
        [
            Map("1.0", new Warp(1, 1, 0, "1.1")),
            Map("1.1", new Warp(1, 1, 0, "1.0")),
        ]);

        Assert.Empty(StoryWalk.From(world, "1.0").DidNot);
    }

    /// <summary>
    /// A door naming somewhere this world file does not contain is named out loud.
    /// <para>
    /// The single most useful thing this walk reports. It is either a door the cartridge
    /// never uses or a map the exporter dropped, and those two want opposite responses — so
    /// it is counted and named rather than resolved.
    /// </para>
    /// </summary>
    [Fact]
    public void ADoorToNowhereIsNamedOutLoud()
    {
        var world = new WorldData([Map("1.0", new Warp(1, 1, 0, "9.9"))]);

        StoryReach walked = StoryWalk.From(world, "1.0");

        DoorToNowhere lost = walked.Nowhere.Single();

        Assert.Equal("1.0", lost.OnMap);
        Assert.Equal("9.9", lost.Names);
        Assert.Equal(HowReached.ThroughADoor, lost.Kind);
    }

    /// <summary>And two doors to the same missing place are two findings, not one.</summary>
    [Fact]
    public void AndTwoDoorsToTheSameMissingPlaceAreTwoFindings()
    {
        var world = new WorldData(
        [
            Map("1.0", new Warp(1, 1, 0, "9.9"), new Warp(2, 2, 0, "1.1")),
            Map("1.1", new Warp(1, 1, 0, "9.9")),
        ]);

        Assert.Equal(2, StoryWalk.From(world, "1.0").Nowhere.Count);
    }

    /// <summary>
    /// The doors a script makes count, and they are the reason this is worth doing: a world
    /// that knew only about squares left most of this game with nothing leading in.
    /// </summary>
    [Fact]
    public void TheDoorsAScriptMakesCount()
    {
        var world = new WorldData(
        [
            new MapData("1.0", "1.0", 4, 4, new byte[16])
            {
                Doors = [new ScriptedDoor("lift", "1.1", 0, 1, 1)],
            },
            Map("1.1"),
        ]);

        StoryReach walked = StoryWalk.From(world, "1.0");

        Assert.Equal(HowReached.ThroughAScriptedDoor, walked.Got.Single(r => r.MapId == "1.1").How);
    }

    /// <summary>And walking over the edge of a map counts too.</summary>
    [Fact]
    public void AndWalkingOverTheEdgeCounts()
    {
        var world = new WorldData(
        [
            new MapData("1.0", "1.0", 4, 4, new byte[16])
            {
                Connections = [new MapConnection(ConnectionSide.Up, 0, "1.1")],
            },
            Map("1.1"),
        ]);

        Assert.Equal(HowReached.OverTheEdge, StoryWalk.From(world, "1.0").Got.Single(r => r.MapId == "1.1").How);
    }

    /// <summary>
    /// The boat joins every dock to every other, and then the walk carries on from wherever
    /// it put us — a dock is a map like any other and has doors of its own.
    /// </summary>
    [Fact]
    public void TheBoatJoinsTheDocksAndTheWalkCarriesOn()
    {
        var world = new WorldData(
        [
            new MapData("1.0", "1.0", 4, 4, new byte[16]) { Ferry = new FerryDock(1, 0, 1, 1) },
            new MapData("2.0", "2.0", 4, 4, new byte[16])
            {
                Ferry = new FerryDock(2, 0, 1, 1),
                Warps = [new Warp(1, 1, 0, "2.1")],
            },
            Map("2.1"),
        ]);

        // Without the boat, nothing past the first dock.
        Assert.Equal(2, StoryWalk.From(world, "1.0").DidNot.Count);

        StoryReach sailed = StoryWalk.WithTheBoat(world, "1.0");

        Assert.Empty(sailed.DidNot);
        Assert.Equal(HowReached.ByBoat, sailed.Got.Single(r => r.MapId == "2.0").How);

        // And the map behind the far dock, which is the "carries on" half.
        Assert.Equal(HowReached.ThroughADoor, sailed.Got.Single(r => r.MapId == "2.1").How);
    }

    /// <summary>And a boat nobody can reach is no help, which it says by changing nothing.</summary>
    [Fact]
    public void AndABoatNobodyCanReachIsNoHelp()
    {
        var world = new WorldData(
        [
            Map("1.0"),
            new MapData("2.0", "2.0", 4, 4, new byte[16]) { Ferry = new FerryDock(2, 0, 1, 1) },
        ]);

        Assert.Equal(["2.0"], StoryWalk.WithTheBoat(world, "1.0").DidNot);
    }

    /// <summary>
    /// A starting map this world file does not contain is a finding rather than a crash.
    /// </summary>
    [Fact]
    public void AStartingMapThatIsNotHereIsAFinding()
    {
        StoryReach walked = StoryWalk.From(new WorldData([Map("1.0")]), "9.9");

        Assert.Empty(walked.Got);
        Assert.Equal(["1.0"], walked.DidNot);
    }

    /// <summary>
    /// And on the whole exported world it reaches a great deal more than it misses.
    /// <para>
    /// Deliberately not an exact figure. The number that matters comes from a real cartridge
    /// and this is a fixture; what this asserts is that the walk works at scale and finds
    /// the joined-up part of a world it did not have hand-written for it.
    /// </para>
    /// </summary>
    [Fact]
    public void AndItWalksTheWholeExportedWorld()
    {
        WorldData world = Exported();

        Assert.NotEmpty(world.Maps);

        StoryReach walked = StoryWalk.WithTheBoat(world, world.Maps.First().Id);

        Assert.Equal(world.Maps.Count, walked.Total);
        Assert.True(walked.Got.Count > 1, "the walk got nowhere from the first map");
    }
}
