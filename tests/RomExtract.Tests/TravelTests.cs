using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Moving between maps: through doors, and off edges.
/// <para>
/// Built by hand rather than from a cartridge, because what is being tested is the
/// server's arithmetic and its broadcasting, not extraction. Two small maps make the
/// off-by-one cases obvious in a way a real 24x40 route would not.
/// </para>
/// </summary>
public class TravelTests
{
    private const string Town = "3.0";
    private const string Route = "3.1";
    private const string Cave = "3.2";

    /// <summary>A map with nothing solid on it, so only the rules under test can block a step.</summary>
    private static MapData Open(string id, string name, int width, int height) =>
        new(id, name, width, height, new byte[width * height]);

    /// <summary>
    /// A town with a door, joined below to a route which is joined below to nothing.
    /// The door leads into a cave, whose own door leads back.
    /// </summary>
    private static GameWorld World(int connectionOffset = 0)
    {
        MapData town = Open(Town, "PALLET TOWN", 6, 4) with
        {
            Warps = [new Warp(2, 1, 0, Cave)],
            Connections = [new MapConnection(ConnectionSide.Down, connectionOffset, Route)],
        };

        MapData route = Open(Route, "ROUTE 1", 6, 5) with
        {
            Connections = [new MapConnection(ConnectionSide.Up, connectionOffset, Town)],
        };

        MapData cave = Open(Cave, "MT MOON", 3, 3) with
        {
            Warps = [new Warp(1, 2, 0, Town)],
        };

        return new GameWorld(new WorldData([town, route, cave]), Town);
    }

    private static ServerPlayer JoinAt(GameWorld world, string name, string mapId, int x, int y, long account = 1)
    {
        (ServerPlayer player, _) = world.Join(
            account, name, new SavedCharacter(mapId, x, y, Direction.Down, 10, []));

        return player;
    }

    /// <summary>Steps without tripping the rate limit, which counts in server seconds.</summary>
    private static List<Outgoing> Step(GameWorld world, ServerPlayer player, Direction direction, double at) =>
        world.Move(player.Id, direction, at);

    [Fact]
    public void SteppingOnADoorMovesYouToTheOtherMap()
    {
        GameWorld world = World();
        ServerPlayer player = JoinAt(world, "Mason", Town, 2, 0);

        List<Outgoing> send = Step(world, player, Direction.Down, 10);

        MapChanged changed = send.Select(o => o.Message).OfType<MapChanged>().Single();

        Assert.Equal(Cave, changed.MapId);
        Assert.Equal(Cave, player.MapId);

        // The far end of the door, not a spawn: warp zero on the cave is at (1, 2).
        Assert.Equal(new GridPosition(1, 2), player.Square);
    }

    [Fact]
    public void ArrivingThroughADoorDoesNotBounceYouStraightBack()
    {
        // You land standing on the warp at the other end. Firing it on arrival would
        // send you back, and then forward, forever.
        GameWorld world = World();
        ServerPlayer player = JoinAt(world, "Mason", Town, 2, 0);

        Step(world, player, Direction.Down, 10);

        Assert.Equal(Cave, player.MapId);
        Assert.Equal(new GridPosition(1, 2), player.Square);
    }

    [Fact]
    public void SteppingOffAndBackOntoADoorUsesItAgain()
    {
        GameWorld world = World();
        ServerPlayer player = JoinAt(world, "Mason", Town, 2, 0);

        Step(world, player, Direction.Down, 10);      // into the cave, standing on its warp
        Step(world, player, Direction.Up, 20);        // off the warp
        Assert.Equal(Cave, player.MapId);

        Step(world, player, Direction.Down, 30);      // back onto it

        Assert.Equal(Town, player.MapId);
        Assert.Equal(new GridPosition(2, 1), player.Square);
    }

    [Fact]
    public void WalkingOffTheBottomEdgeArrivesOnTheMapBelow()
    {
        GameWorld world = World();
        ServerPlayer player = JoinAt(world, "Mason", Town, 4, 3);

        List<Outgoing> send = Step(world, player, Direction.Down, 10);

        MapChanged changed = send.Select(o => o.Message).OfType<MapChanged>().Single();

        Assert.Equal(Route, changed.MapId);
        Assert.Equal(new GridPosition(4, 0), player.Square);
    }

    [Fact]
    public void AConnectionOffsetSlidesTheArrival()
    {
        // The offset exists because a route is rarely the same width as the town above
        // it. Getting its sign wrong puts every arrival in the same wrong column,
        // consistently enough to look deliberate.
        GameWorld world = World(connectionOffset: 2);
        ServerPlayer player = JoinAt(world, "Mason", Town, 4, 3);

        Step(world, player, Direction.Down, 10);

        Assert.Equal(Route, player.MapId);
        Assert.Equal(new GridPosition(2, 0), player.Square);
    }

    [Fact]
    public void WalkingOffAnEdgeWithNoNeighbourIsJustAWall()
    {
        GameWorld world = World();
        ServerPlayer player = JoinAt(world, "Mason", Town, 0, 2);

        List<Outgoing> send = Step(world, player, Direction.Left, 10);

        Assert.Empty(send.Select(o => o.Message).OfType<MapChanged>());
        Assert.Equal(new GridPosition(0, 2), player.Square);
        Assert.Equal(Town, player.MapId);
    }

    [Fact]
    public void TheReturnEdgeLandsOnTheBottomRow()
    {
        GameWorld world = World();
        ServerPlayer player = JoinAt(world, "Mason", Route, 3, 0);

        Step(world, player, Direction.Up, 10);

        Assert.Equal(Town, player.MapId);
        Assert.Equal(new GridPosition(3, 3), player.Square);
    }

    [Fact]
    public void PlayersOnOtherMapsAreNotToldAboutYourSteps()
    {
        GameWorld world = World();

        ServerPlayer here = JoinAt(world, "Mason", Town, 0, 0, account: 1);
        ServerPlayer elsewhere = JoinAt(world, "Someone", Route, 0, 0, account: 2);

        List<Outgoing> send = Step(world, here, Direction.Right, 10);

        // Scoped to the mover's map, so the fan-out above can skip everyone else.
        Assert.All(send.Where(o => o.Message is PlayerMoved), o => Assert.Equal(Town, o.OnMap));
        Assert.NotEqual(here.MapId, elsewhere.MapId);
    }

    [Fact]
    public void LeavingAMapLooksLikeADisconnectToTheMapYouLeft()
    {
        // Deliberate: a client watching other players needs no new case for travel,
        // because leaving and arriving are the messages it already handles.
        GameWorld world = World();

        ServerPlayer traveller = JoinAt(world, "Mason", Town, 2, 0, account: 1);
        JoinAt(world, "Watcher", Town, 5, 3, account: 2);

        List<Outgoing> send = Step(world, traveller, Direction.Down, 10);

        Outgoing left = send.Single(o => o.Message is PlayerLeft);

        Assert.Equal(Town, left.OnMap);
        Assert.Equal(traveller.Id, left.Except);
    }

    [Fact]
    public void ArrivingIsAnnouncedToTheMapYouArriveOn()
    {
        GameWorld world = World();

        ServerPlayer resident = JoinAt(world, "Watcher", Cave, 0, 0, account: 2);
        ServerPlayer traveller = JoinAt(world, "Mason", Town, 2, 0, account: 1);

        List<Outgoing> send = Step(world, traveller, Direction.Down, 10);

        Outgoing appeared = send.Single(o => o.Message is PlayerAppeared p && p.PlayerId == traveller.Id);

        Assert.Equal(Cave, appeared.OnMap);

        // And the traveller is told who was already there.
        Assert.Contains(send, o =>
            o.Message is PlayerAppeared p && p.PlayerId == resident.Id && o.OnlyTo == traveller.Id);
    }

    [Fact]
    public void JoiningOnlyShowsYouPlayersOnYourOwnMap()
    {
        GameWorld world = World();

        JoinAt(world, "Nearby", Town, 1, 1, account: 1);
        JoinAt(world, "FarAway", Route, 1, 1, account: 2);

        (ServerPlayer player, List<Outgoing> send) = world.Join(
            3, "Mason", new SavedCharacter(Town, 3, 3, Direction.Down, 10, []));

        List<PlayerAppeared> shown = send
            .Where(o => o.OnlyTo == player.Id)
            .Select(o => o.Message)
            .OfType<PlayerAppeared>()
            .ToList();

        Assert.Single(shown);
        Assert.Equal("Nearby", shown[0].Name);
    }

    [Fact]
    public void ASaveOnAMapThisWorldNoLongerHasFallsBackToTheStart()
    {
        GameWorld world = World();

        ServerPlayer player = JoinAt(world, "Mason", "99.99", 1, 1);

        Assert.Equal(Town, player.MapId);
        Assert.True(world.GridOf(Town).IsWalkable(player.Square));
    }

    [Fact]
    public void ASnapshotRemembersWhichMapYouAreOn()
    {
        GameWorld world = World();
        ServerPlayer player = JoinAt(world, "Mason", Town, 2, 0);

        Step(world, player, Direction.Down, 10);

        SavedCharacter? snapshot = world.Snapshot(player.Id);

        Assert.NotNull(snapshot);
        Assert.Equal(Cave, snapshot.MapId);
        Assert.Equal(1, snapshot.X);
        Assert.Equal(2, snapshot.Y);
    }

    [Fact]
    public void AnEdgeThatRefusesSaysWhy()
    {
        // Three different situations feel identical to a player — they walk into what
        // seems like a wall — and only the server can tell them apart.
        GameWorld world = World();
        ServerPlayer player = JoinAt(world, "Mason", Town, 0, 2);

        Step(world, player, Direction.Left, 10);
        Assert.Contains("no connection", world.LastEdgeRefusal);

        Step(world, player, Direction.Right, 20);
        Assert.Null(world.LastEdgeRefusal);
    }

    [Fact]
    public void ASolidArrivalSquareIsNamedRatherThanJustBlocked()
    {
        var collision = new byte[4 * 2];
        collision[0] = 1;   // (0, 0) — the square you would land on — is solid

        MapData town = Open(Town, "PALLET TOWN", 4, 2) with
        {
            Connections = [new MapConnection(ConnectionSide.Down, 0, Route)],
        };

        MapData route = new(Route, "ROUTE 1", 4, 2, collision);

        var world = new GameWorld(new WorldData([town, route]), Town);
        ServerPlayer player = JoinAt(world, "Mason", Town, 0, 1);

        world.Move(player.Id, Direction.Down, 10);

        Assert.Equal(Town, player.MapId);
        Assert.Contains("is solid", world.LastEdgeRefusal);
        Assert.Contains(Route, world.LastEdgeRefusal);
    }

    [Fact]
    public void ADoorOnTheFarSideOfAnEdgeStillWorks()
    {
        // Arriving across an edge is ordinary walking, so a warp on the square you
        // land on has to fire. Checking warps only on same-map steps would leave a
        // door that works from one direction and not the other.
        MapData town = Open(Town, "PALLET TOWN", 4, 3) with
        {
            Connections = [new MapConnection(ConnectionSide.Down, 0, Route)],
        };

        MapData route = Open(Route, "ROUTE 1", 4, 3) with
        {
            Warps = [new Warp(1, 0, 0, Cave)],
            Connections = [new MapConnection(ConnectionSide.Up, 0, Town)],
        };

        MapData cave = Open(Cave, "MT MOON", 3, 3) with
        {
            Warps = [new Warp(2, 2, 0, Route)],
        };

        var world = new GameWorld(new WorldData([town, route, cave]), Town);
        ServerPlayer player = JoinAt(world, "Mason", Town, 1, 2);

        world.Move(player.Id, Direction.Down, 10);

        Assert.Equal(Cave, player.MapId);
        Assert.Equal(new GridPosition(2, 2), player.Square);
    }

    [Fact]
    public void AWarpFiresAtMostOncePerStep()
    {
        // Two doors facing each other on the squares they lead to. Chaining warps
        // within one step would bounce a player between them until the stack gave out.
        MapData first = Open(Town, "PALLET TOWN", 3, 3) with
        {
            Warps = [new Warp(1, 1, 0, Route)],
        };

        MapData second = Open(Route, "ROUTE 1", 3, 3) with
        {
            Warps = [new Warp(1, 1, 0, Town)],
        };

        var world = new GameWorld(new WorldData([first, second]), Town);
        ServerPlayer player = JoinAt(world, "Mason", Town, 1, 0);

        world.Move(player.Id, Direction.Down, 10);

        Assert.Equal(Route, player.MapId);
        Assert.Equal(new GridPosition(1, 1), player.Square);
    }

    [Fact]
    public void ADoorLeadingNowhereLeavesYouWhereYouAre()
    {
        // Real cartridges have warps whose destination is decided at run time, and
        // their target map is not a map at all.
        MapData town = Open(Town, "PALLET TOWN", 4, 4) with
        {
            Warps = [new Warp(1, 1, 0, "77.77")],
        };

        var world = new GameWorld(new WorldData([town]), Town);
        ServerPlayer player = JoinAt(world, "Mason", Town, 1, 0);

        world.Move(player.Id, Direction.Down, 10);

        Assert.Equal(Town, player.MapId);
        Assert.Equal(new GridPosition(1, 1), player.Square);
    }
}
