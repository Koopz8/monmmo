using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

public class MapObjectExtractionTests
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

    private static List<MapObject> Read(int index) =>
        MapLinkExtractor.ReadObjects(
            Fixture.ToRom(), HeaderFor(index), SyntheticRom.MapWidth, SyntheticRom.MapHeight);

    [Fact]
    public void ReadsObjectsExactly()
    {
        foreach (int index in new[] { 0, 1, 12, SyntheticRom.MapCount - 1 })
            Assert.Equal(SyntheticRom.ObjectsFor(index), Read(index));
    }

    [Fact]
    public void ReadsTheObjectTableAndNotTheWarpTable()
    {
        // Objects and warps are different pairs of the same four counts and pointers.
        // Taking the wrong pair gives a plausible number of plausible-looking people
        // standing exactly where the doors are.
        List<MapObject> objects = Read(0);
        List<Warp> warps = MapLinkExtractor.ReadWarps(
            Fixture.ToRom(), HeaderFor(0), SyntheticRom.MapWidth, SyntheticRom.MapHeight);

        Assert.NotEmpty(objects);
        Assert.NotEmpty(warps);
        Assert.DoesNotContain(objects, o => warps.Any(w => w.X == o.X && w.Y == o.Y));
    }

    [Fact]
    public void AMapWithNoEventsHasNoObjects() =>
        Assert.Empty(Read(SyntheticRom.MapWithoutEvents));

    [Fact]
    public void AnObjectOutsideTheMapIsDropped()
    {
        List<MapObject> objects = Read(4);

        Assert.Equal(SyntheticRom.ObjectsFor(4), objects);
        Assert.All(objects, o => Assert.InRange(o.X, 0, SyntheticRom.MapWidth - 1));
    }

    [Fact]
    public void MovementTypeDecidesWhichWayTheyLook()
    {
        // Wandering in a direction and standing still facing it are different numbers
        // with the same starting look.
        Assert.Equal(Direction.Up, MapObject.FacingFor(7));
        Assert.Equal(Direction.Up, MapObject.FacingFor(3));
        Assert.Equal(Direction.Left, MapObject.FacingFor(9));
        Assert.Equal(Direction.Right, MapObject.FacingFor(10));

        // Anything unrecognised faces the camera, which is what the games default to.
        Assert.Equal(Direction.Down, MapObject.FacingFor(0));
        Assert.Equal(Direction.Down, MapObject.FacingFor(200));
    }

    [Fact]
    public void TrainersAreMarked()
    {
        Assert.Contains(Read(2), o => o.IsTrainer);
        Assert.Contains(Read(2), o => !o.IsTrainer);
    }

    [Fact]
    public void ObjectsSurviveTheWorldFile()
    {
        WorldData exported = WorldExporter.Export(Fixture.ToRom());

        using var buffer = new MemoryStream();
        exported.Save(buffer);

        buffer.Position = 0;
        WorldData reloaded = WorldData.Load(buffer);

        MapData before = exported.Find(SyntheticRom.MapIdAt(6))!;
        MapData after = reloaded.Find(SyntheticRom.MapIdAt(6))!;

        Assert.NotEmpty(before.Objects);
        Assert.Equal(before.Objects, after.Objects);
    }
}

public class ObjectsBlockMovementTests
{
    private const string Town = "3.0";

    private static GameWorld World(params MapObject[] objects)
    {
        MapData map = new(Town, "PALLET TOWN", 4, 4, new byte[16]) { Objects = objects };

        return new GameWorld(new WorldData([map]), Town, TestRules.All);
    }

    private static ServerPlayer JoinAt(GameWorld world, int x, int y)
    {
        (ServerPlayer player, _) = world.Join(
            1, "Mason", new SavedCharacter(Town, x, y, Direction.Down, 10, []));

        return player;
    }

    [Fact]
    public void YouCannotWalkThroughSomebody()
    {
        // As solid as a wall, and the server's answer rather than the client's — the
        // client draws them from its own cartridge and could decline to.
        GameWorld world = World(new MapObject(1, 5, 1, 0, Direction.Down, 0, false));

        ServerPlayer player = JoinAt(world, 1, 1);

        world.Move(player.Id, Direction.Up, 10);

        Assert.Equal(new GridPosition(1, 1), player.Square);
    }

    [Fact]
    public void AnEmptySquareIsStillWalkable()
    {
        GameWorld world = World(new MapObject(1, 5, 3, 3, Direction.Down, 0, false));

        ServerPlayer player = JoinAt(world, 1, 1);

        world.Move(player.Id, Direction.Up, 10);

        Assert.Equal(new GridPosition(1, 0), player.Square);
    }

    [Fact]
    public void WalkingIntoSomebodyStillTurnsYou()
    {
        // Pressing into a wall turns a character without moving them, and a person is
        // a wall for this purpose.
        GameWorld world = World(new MapObject(1, 5, 1, 0, Direction.Down, 0, false));

        ServerPlayer player = JoinAt(world, 1, 1);

        world.Move(player.Id, Direction.Up, 10);

        Assert.Equal(Direction.Up, player.Facing);
    }
}

/// <summary>
/// The grid the client predicts against.
/// <para>
/// People are as solid as walls but they are not part of a map's collision data — they
/// are placed on top of it. A client predicting against bare map collision walks
/// through somebody, gets corrected, and disagrees with the server from then on.
/// </para>
/// </summary>
public class CollisionWithPeopleTests
{
    private static CollisionGrid Open(int width = 4, int height = 4) =>
        new(width, height, new byte[width * height]);

    [Fact]
    public void ASquareWithSomebodyOnItIsNotWalkable()
    {
        CollisionGrid grid = Open().With([new GridPosition(1, 2)]);

        Assert.False(grid.IsWalkable(new GridPosition(1, 2)));
        Assert.True(grid.IsWalkable(new GridPosition(1, 1)));
    }

    [Fact]
    public void TheOriginalGridIsLeftAlone()
    {
        // Shared with whatever else holds it — the map is loaded once and the people
        // change with the map, not with the grid.
        CollisionGrid original = Open();
        CollisionGrid blocked = original.With([new GridPosition(0, 0)]);

        Assert.True(original.IsWalkable(new GridPosition(0, 0)));
        Assert.False(blocked.IsWalkable(new GridPosition(0, 0)));
    }

    [Fact]
    public void SomebodyOutsideTheMapIsIgnoredRatherThanThrowing()
    {
        // Real cartridges place objects beyond a map's edge; extraction drops those,
        // but nothing here should depend on that having happened.
        CollisionGrid grid = Open().With([new GridPosition(99, 99), new GridPosition(-1, 0)]);

        Assert.True(grid.IsWalkable(new GridPosition(0, 0)));
    }

    [Fact]
    public void WhatTheClientPredictsMatchesWhatTheServerAllows()
    {
        // The two have to agree square for square. Where they do not, a player walks
        // somewhere the server will not let them be.
        var people = new[]
        {
            new MapObject(1, 5, 1, 1, Direction.Down, 0, false),
            new MapObject(2, 6, 3, 0, Direction.Up, 7, false),
        };

        MapData map = new("3.0", "PALLET TOWN", 4, 4, new byte[16]) { Objects = people };

        CollisionGrid predicted = map.ToGrid().With(people.Select(o => o.Square));

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var square = new GridPosition(x, y);

                bool serverAllows = map.ToGrid().IsWalkable(square) && map.ObjectAt(square) is null;

                Assert.Equal(serverAllows, predicted.IsWalkable(square));
            }
        }
    }
}
