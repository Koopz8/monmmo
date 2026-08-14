using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

public class MapLinkExtractionTests
{
    private static readonly SyntheticRom Fixture = new();

    private static MapHeaderRecord HeaderFor(int index)
    {
        Rom rom = Fixture.ToRom();

        MapBankTable banks = MapBankLocator.Locate(rom)
            ?? throw new InvalidOperationException("No bank table in the fixture.");

        (int bank, int map) = (index / SyntheticRom.MapsPerBank, index % SyntheticRom.MapsPerBank);

        return banks.AllMaps.Single(m => m.Bank == bank && m.Map == map).Header;
    }

    [Fact]
    public void ReadsWarpsExactly()
    {
        Rom rom = Fixture.ToRom();

        foreach (int index in new[] { 0, 1, 12, SyntheticRom.MapCount - 1 })
        {
            List<Warp> warps = MapLinkExtractor.ReadWarps(
                rom, HeaderFor(index), SyntheticRom.MapWidth, SyntheticRom.MapHeight);

            Assert.Equal(SyntheticRom.WarpsFor(index), warps);
        }
    }

    [Fact]
    public void ReadsTheWarpTableAndNotTheObjectTable()
    {
        // The events record holds four counts and four pointers. Taking the first of
        // each — the object events — would give three warps from the wrong table, and
        // they would look entirely plausible.
        Rom rom = Fixture.ToRom();

        List<Warp> warps = MapLinkExtractor.ReadWarps(
            rom, HeaderFor(0), SyntheticRom.MapWidth, SyntheticRom.MapHeight);

        Assert.Equal(2, warps.Count);
    }

    [Fact]
    public void AMapWithNoEventsHasNoWarps()
    {
        Rom rom = Fixture.ToRom();

        List<Warp> warps = MapLinkExtractor.ReadWarps(
            rom, HeaderFor(SyntheticRom.MapWithoutEvents), SyntheticRom.MapWidth, SyntheticRom.MapHeight);

        Assert.Empty(warps);
    }

    [Fact]
    public void AWarpOutsideTheMapIsDropped()
    {
        Rom rom = Fixture.ToRom();

        List<Warp> warps = MapLinkExtractor.ReadWarps(
            rom, HeaderFor(SyntheticRom.MapWithAStrayWarp), SyntheticRom.MapWidth, SyntheticRom.MapHeight);

        Assert.Equal(SyntheticRom.WarpsFor(SyntheticRom.MapWithAStrayWarp), warps);
        Assert.All(warps, w => Assert.InRange(w.X, 0, SyntheticRom.MapWidth - 1));
    }

    [Fact]
    public void ReadsConnectionsExactly()
    {
        Rom rom = Fixture.ToRom();

        foreach (int index in new[] { 0, 5, SyntheticRom.MapCount - 1 })
        {
            List<MapConnection> connections = MapLinkExtractor.ReadConnections(rom, HeaderFor(index));

            Assert.Equal(SyntheticRom.ConnectionsFor(index), connections);
        }
    }

    [Fact]
    public void ADiveConnectionIsNotAWalkableEdge()
    {
        // Directions five and six join a surface map to an underwater one. Reading
        // them as a side would give a map an edge that leads somewhere unreachable.
        Rom rom = Fixture.ToRom();

        List<MapConnection> connections = MapLinkExtractor.ReadConnections(rom, HeaderFor(0));

        Assert.Equal(2, connections.Count);
        Assert.All(connections, c => Assert.True(Enum.IsDefined(c.Side)));
    }

    [Fact]
    public void NegativeConnectionOffsetsSurvive()
    {
        // The offset slides a neighbour along the shared edge and is signed. Reading
        // it unsigned would turn a small negative into four billion, and the map would
        // join at a column that does not exist.
        Rom rom = Fixture.ToRom();

        List<MapConnection> connections = MapLinkExtractor.ReadConnections(rom, HeaderFor(0));

        Assert.Contains(connections, c => c.Offset < 0);
        Assert.Equal(-2, connections.Single(c => c.Side == ConnectionSide.Down).Offset);
    }
}

public class ExportedWorldLinkTests
{
    private static readonly WorldData Exported = WorldExporter.Export(new SyntheticRom().ToRom());

    [Fact]
    public void EveryMapCarriesItsWarpsAndConnections()
    {
        MapData map = Exported.Find(SyntheticRom.MapIdAt(1))!;

        Assert.Equal(SyntheticRom.WarpsFor(1), map.Warps);
        Assert.Equal(SyntheticRom.ConnectionsFor(1), map.Connections);
    }

    [Fact]
    public void LinksSurviveASaveAndLoad()
    {
        using var buffer = new MemoryStream();
        Exported.Save(buffer);

        buffer.Position = 0;
        WorldData reloaded = WorldData.Load(buffer);

        MapData before = Exported.Find(SyntheticRom.MapIdAt(9))!;
        MapData after = reloaded.Find(SyntheticRom.MapIdAt(9))!;

        Assert.Equal(before.Warps, after.Warps);
        Assert.Equal(before.Connections, after.Connections);
    }

    [Fact]
    public void EveryLinkLeadsSomewhereReal()
    {
        // The check the export report makes on a real cartridge, asserted here: a
        // whole-file misread would leave every link pointing at a map that is not
        // there, and the totals alone would look fine.
        var known = Exported.Maps.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (MapData map in Exported.Maps)
        {
            Assert.All(map.Warps, w => Assert.Contains(w.TargetMapId, known));
            Assert.All(map.Connections, c => Assert.Contains(c.MapId, known));
        }
    }

    [Fact]
    public void AWarpIsFoundBySquare()
    {
        MapData map = Exported.Find(SyntheticRom.MapIdAt(1))!;

        Warp expected = SyntheticRom.WarpsFor(1)[0];

        Assert.Equal(expected, map.WarpAt(new GridPosition(expected.X, expected.Y)));
        Assert.Null(map.WarpAt(new GridPosition(9, 7)));
    }
}

/// <summary>
/// Walking the world to find out how much of it a character can actually reach.
/// <para>
/// "The story stops somewhere" is not a fact anybody can act on. This turns it into a
/// number and a list of squares, off the world file alone.
/// </para>
/// </summary>
public class WorldWalkerTests
{
    private const string Town = "3.0";
    private const string Inside = "3.1";

    /// <summary>
    /// Two maps and a door, with one square of corridor that something can sit in.
    /// <para>
    /// Column 2 is solid except at row 2, so the only way east is through (2, 2) — which
    /// is where the tree goes. A test whose obstacle can be walked around proves nothing.
    /// </para>
    /// </summary>
    private static WorldData World(params MapObject[] people)
    {
        var collision = new byte[25];

        for (int y = 0; y < 5; y++) collision[y * 5 + 2] = 1;

        collision[2 * 5 + 2] = 0;

        MapData town = new(Town, "PALLET TOWN", 5, 5, collision)
        {
            Objects = people,
            Warps = [new Warp(4, 2, 0, Inside)],
        };

        MapData inside = new(Inside, "A HOUSE", 4, 4, new byte[16])
        {
            Warps = [new Warp(1, 1, 0, Town)],
        };

        return new WorldData([town, inside]);
    }

    [Fact]
    public void WhatIsWalkableIsWalkedAndWhatIsBehindADoorIsFollowed()
    {
        Reach reach = WorldWalker.Walk(World(), Town);

        Assert.Equal(2, reach.Maps.Count);
        Assert.Empty(reach.Blocked);
    }

    [Fact]
    public void ATreeInTheOnlyGapClosesTheWorldAndSaysSo()
    {
        // The frontier is the point of the whole instrument: not "you cannot get there"
        // but "here is the square, and here is the move it wants".
        MapObject tree = new(1, 5, 2, 2, Direction.Down, 0, false) { ShiftedBy = 15 };

        Reach reach = WorldWalker.Walk(World(tree), Town);

        Assert.Equal([Town], reach.Maps);

        Frontier stopped = Assert.Single(reach.Blocked);

        Assert.Equal(new GridPosition(2, 2), stopped.Square);
        Assert.Equal(15, stopped.ShiftedBy);
    }

    [Fact]
    public void TheSameTreeWithTheMoveInHandIsNotAWall()
    {
        MapObject tree = new(1, 5, 2, 2, Direction.Down, 0, false) { ShiftedBy = 15 };

        Reach reach = WorldWalker.Walk(World(tree), Town, [15]);

        Assert.Equal(2, reach.Maps.Count);
        Assert.Empty(reach.Blocked);
    }

    [Fact]
    public void SomebodyStandingInTheGapIsAWallUntilAskedToBeWalkedThrough()
    {
        // A person in a doorway is a wall to a walker and a wall a script opens in the
        // game. The difference between these two answers is the share of the world gated
        // on scripts rather than on geometry, which is the number worth planning from.
        MapObject guard = new(1, 5, 2, 2, Direction.Down, 0, false);

        Assert.Equal([Town], WorldWalker.Walk(World(guard), Town).Maps);
        Assert.Equal(2, WorldWalker.Walk(World(guard), Town, null, throughPeople: true).Maps.Count);
    }

    [Fact]
    public void ADoorToAMapThisWorldDoesNotHaveIsReported()
    {
        MapData town = new(Town, "PALLET TOWN", 4, 4, new byte[16])
        {
            Warps = [new Warp(1, 1, 0, "9.9")],
        };

        Assert.Equal(["9.9"], WorldWalker.Walk(new WorldData([town]), Town).Beyond);
    }
}

/// <summary>
/// Who is a wall and who is a wait.
/// <para>
/// The walker measures what a player can eventually reach, and "eventually" is the whole
/// difference: somebody with a beat to walk steps off a square by themselves, and a ball
/// on the floor of a cave is gone the moment you pick it up. Counting either as a wall
/// is how this project spent five milestones believing the world was 36 maps large when
/// it is 173 — what stood between the player and CERULEAN was two fossils lying side by
/// side in a corridor in MT. MOON.
/// </para>
/// <para>
/// The server keeps the opposite rule and is right to: at any instant everybody is
/// solid. These two questions only look contradictory until you notice they are asked
/// about different times.
/// </para>
/// </summary>
public class InTheWayTests
{
    private static MapObject Person(int movementType, int rangeX = 0, int rangeY = 0) =>
        new(1, 5, 3, 3, Direction.Down, movementType, IsTrainer: false, RangeX: rangeX, RangeY: rangeY);

    [Fact]
    public void SomebodyWithABeatStepsAside()
    {
        Assert.True(Person(2, rangeX: 1).CanStepAside);
        Assert.True(Person(3, rangeY: 2).CanStepAside);
    }

    [Fact]
    public void SomebodyWhoWandersNowhereIsAWall()
    {
        // The trap this rule exists for. The movement type says "walks about" and the
        // range says "no further than here", and the second one wins.
        Assert.False(Person(2).CanStepAside);
    }

    [Fact]
    public void SomebodyStandingStillIsAWall()
    {
        Assert.False(Person(1, rangeX: 3, rangeY: 3).CanStepAside);
        Assert.False(Person(8).CanStepAside);
    }

    [Fact]
    public void AThingOnTheFloorCanBeTakenAway()
    {
        // Both halves are needed. It hands something over, and it can be hidden — which
        // is what happens to it once you have.
        var ball = Person(8) with { GivesItemId = 4, HiddenBy = 0x0123 };

        Assert.True(ball.CanBeTakenAway);
    }

    [Fact]
    public void SoCanOneWhoseScriptDoesTheHandingOver()
    {
        // The fossils. Their own record has no item in it at all; what they give is
        // inside the script, and the export records it as what they are allowed to give.
        var fossil = Person(8) with { CanGive = [358], HiddenBy = 0x002F };

        Assert.True(fossil.CanBeTakenAway);
    }

    [Fact]
    public void SomebodyWhoHandsSomethingOverAndStaysIsStillAWall()
    {
        // The difference is the hiding flag. Fifteen people in this game hand something
        // over in the middle of a conversation and are still standing there afterwards.
        var giver = Person(8) with { GivesItemId = 4 };

        Assert.False(giver.CanBeTakenAway);
    }
}

/// <summary>
/// The same rules, seen through the walker.
/// <para>
/// A corridor one square wide with something in it, which is the shape the whole
/// question is about: half the world was behind exactly this on MT. MOON's second
/// floor.
/// </para>
/// </summary>
public class WalkingPastPeopleTests
{
    private const string Cave = "1.90";

    /// <summary>
    /// Two rooms joined by a corridor one square wide, with whoever is given standing
    /// in the middle of it.
    /// </summary>
    private static WorldData Corridor(MapObject? standing)
    {
        const int width = 5;
        const int height = 7;

        var collision = new byte[width * height];

        // Walls down both sides, so the only way between the two ends is the middle
        // column — and the only square of it that matters is (2, 3).
        for (int y = 2; y <= 4; y++)
        {
            collision[y * width + 0] = 1;
            collision[y * width + 1] = 1;
            collision[y * width + 3] = 1;
            collision[y * width + 4] = 1;
        }

        MapData map = new(Cave, "THE CORRIDOR", width, height, collision);

        return new WorldData([standing is null ? map : map with { Objects = [standing] }]);
    }

    private static int Reached(MapObject? standing) =>
        WorldWalker.Walk(Corridor(standing), Cave).Stood.Count;

    private static MapObject At(int movementType, int rangeX = 0) =>
        new(1, 5, 2, 3, Direction.Down, movementType, IsTrainer: false, RangeX: rangeX);

    [Fact]
    public void AnEmptyCorridorIsWalkedEndToEnd()
    {
        // Both rooms and the corridor between them: everything the map has.
        Assert.Equal(23, Reached(null));
    }

    [Fact]
    public void SomebodyRootedToTheSpotClosesIt()
    {
        // Six squares instead of eleven: one end of the map, and nothing beyond.
        Assert.True(Reached(At(movementType: 8)) < Reached(null));
    }

    [Fact]
    public void SomebodyWithABeatDoesNot()
    {
        // The whole map again. Walking through them is not optimism: they are going to
        // move, and a player waits a second and goes past.
        Assert.Equal(Reached(null), Reached(At(movementType: 2, rangeX: 1)));
    }

    [Fact]
    public void NorDoesAThingOnTheFloor()
    {
        Assert.Equal(
            Reached(null),
            Reached(At(movementType: 8) with { GivesItemId = 4, HiddenBy = 0x0123 }));
    }

    [Fact]
    public void TheOneWhoClosesItIsSaidOutLoud()
    {
        // The point of reporting them: this list is the story's own list of gates,
        // arrived at without reading a single script.
        Reach reach = WorldWalker.Walk(Corridor(At(movementType: 8)), Cave);

        Standing who = reach.People.Single();

        Assert.Equal(Cave, who.MapId);
        Assert.Equal(new GridPosition(2, 3), who.Square);
        Assert.Equal(1, who.LocalId);
    }

    [Fact]
    public void BeingInTheWayIsNotTheSameAsBeingAGate()
    {
        // Somebody standing in the open is in the way of one square and gates nothing,
        // and they are still on the list — the list is "who the walk could not walk
        // through", not "who is costing you the world". Which of them is a gate is a
        // second question, and it has a second answer: walk again as if they were not
        // there and see what opens.
        MapData map = new(Cave, "THE ROOM", 5, 5, new byte[25])
        {
            Objects = [new MapObject(1, 5, 2, 2, Direction.Down, 8, IsTrainer: false)],
        };

        var world = new WorldData([map]);

        Assert.Single(WorldWalker.Walk(world, Cave).People);

        // And the second question says so: nothing opens.
        Assert.Equal(
            WorldWalker.Walk(world, Cave).Maps.Count,
            WorldWalker.Walk(world, Cave, asIfGone: [(Cave, 1)]).Maps.Count);
    }
}
