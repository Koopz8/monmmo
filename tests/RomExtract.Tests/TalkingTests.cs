using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Deciding who a player is talking to.
/// <para>
/// This is shared code rather than client code on purpose. The client decides whether
/// pressing the button opens a box; the server decides whether that person is close
/// enough to hold still. Two answers to the same question is how you get a
/// conversation with somebody who is not there.
/// </para>
/// </summary>
public class FacingTests
{
    private static MapObject Somebody(int localId, int x, int y) =>
        new(localId, 5, x, y, Direction.Down, 0, false);

    [Fact]
    public void TheSquareInFrontIsTheOneThatCounts()
    {
        MapObject[] people = [Somebody(1, 3, 2), Somebody(2, 5, 5)];

        Assert.Equal(1, Interaction.InFrontOf(new GridPosition(3, 3), Direction.Up, people)?.LocalId);
    }

    [Fact]
    public void FacingAwayFromSomebodyIsNotTalkingToThem()
    {
        MapObject[] people = [Somebody(1, 3, 2)];

        Assert.Null(Interaction.InFrontOf(new GridPosition(3, 3), Direction.Down, people));
    }

    [Fact]
    public void StandingOnSomebodyIsNotTalkingToThemEither()
    {
        // Not reachable in a world with collision, but the rule is "the square in
        // front", and a version written as "within one square" would answer differently.
        MapObject[] people = [Somebody(1, 3, 3)];

        Assert.Null(Interaction.InFrontOf(new GridPosition(3, 3), Direction.Up, people));
    }

    [Fact]
    public void WhereTheyAreNowBeatsWhereTheCartridgePutThem()
    {
        // The whole reason this takes a second argument. After a few seconds of
        // wandering, the placement in the image is a fact about a file rather than
        // about the world, and reading from it has players talking to empty squares.
        MapObject[] people = [Somebody(1, 3, 2)];

        var live = new Dictionary<int, GridPosition> { [1] = new(4, 3) };

        Assert.Null(Interaction.InFrontOf(new GridPosition(3, 3), Direction.Up, people, live));
        Assert.Equal(1, Interaction.InFrontOf(new GridPosition(3, 3), Direction.Right, people, live)?.LocalId);
    }

    [Fact]
    public void SomebodyNotYetHeardAboutIsWhereTheCartridgeSaid()
    {
        // A player can press the button in the moment between arriving on a map and the
        // server saying who is on it. Falling back to the placement is right, because
        // that is exactly where everybody is until the first tick moves them.
        MapObject[] people = [Somebody(1, 3, 2)];

        Assert.Equal(
            1,
            Interaction.InFrontOf(new GridPosition(3, 3), Direction.Up, people, new Dictionary<int, GridPosition>())
                ?.LocalId);
    }

    [Fact]
    public void EverybodyTurnsToFaceYou()
    {
        foreach (Direction facing in Enum.GetValues<Direction>())
        {
            Direction turned = Interaction.Opposite(facing);

            Assert.NotEqual(facing, turned);
            Assert.Equal(facing, Interaction.Opposite(turned));
        }
    }
}

/// <summary>
/// What the server does about a conversation, which is only ever one thing: hold the
/// person still. It has no cartridge and so has no idea what anybody says.
/// </summary>
public class TalkingTests
{
    private const string Town = "3.0";

    private static GameWorld World(params MapObject[] people)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = people };

        return new GameWorld(new WorldData([map]), Town, TestRules.All);
    }

    private static ServerPlayer Join(GameWorld world, int id, int x, int y, Direction facing)
    {
        (ServerPlayer player, _) = world.Join(
            id, $"Player{id}", new SavedCharacter(Town, x, y, facing, 10, []));

        return player;
    }

    private static MapObject Wanderer(int localId, int x, int y) =>
        new(localId, 5, x, y, Direction.Down, 2, false, 3, 3);

    /// <summary>Runs the clock forward and collects everything that moved.</summary>
    private static List<ObjectMoved> Run(GameWorld world, double seconds, double from = 0)
    {
        var moved = new List<ObjectMoved>();

        for (double now = from + 0.2; now <= from + seconds; now += 0.2)
            moved.AddRange(world.Tick(now).Select(o => o.Message).OfType<ObjectMoved>());

        return moved;
    }

    [Fact]
    public void SomebodyBeingTalkedToStandsStill()
    {
        // Without this they wander off every second or so, which over a conversation of
        // three pages means finishing a sentence from the other side of the street.
        GameWorld world = World(Wanderer(1, 3, 3));
        ServerPlayer player = Join(world, 1, 3, 4, Direction.Up);

        world.StartTalking(player.Id, 1);

        Assert.Empty(Run(world, 60).Where(m => (m.X, m.Y) != (3, 3)));
    }

    [Fact]
    public void TheyCarryOnOnceYouAreDone()
    {
        GameWorld world = World(Wanderer(1, 3, 3));
        ServerPlayer player = Join(world, 1, 3, 4, Direction.Up);

        world.StartTalking(player.Id, 1);
        world.StopTalking(player.Id);

        Assert.NotEmpty(Run(world, 60));
    }

    [Fact]
    public void TheyTurnToLookAtYou()
    {
        GameWorld world = World(new MapObject(1, 5, 3, 3, Direction.Down, 0, false));
        ServerPlayer player = Join(world, 1, 3, 2, Direction.Down);

        List<Outgoing> send = world.StartTalking(player.Id, 1);

        ObjectMoved turned = send.Select(o => o.Message).OfType<ObjectMoved>().Single();

        Assert.Equal(Direction.Up, turned.Facing);
        Assert.Equal((3, 3), (turned.X, turned.Y));
    }

    [Fact]
    public void TalkingToSomebodyAcrossTheMapDoesNothing()
    {
        // Not anti-cheat — there is nothing to cheat at yet. It is what stops the hold
        // from being a way to freeze anybody on the map from anywhere on it.
        GameWorld world = World(Wanderer(1, 6, 6));
        ServerPlayer player = Join(world, 1, 0, 0, Direction.Down);

        Assert.Empty(world.StartTalking(player.Id, 1));
        Assert.Null(world.TalkingTo(player.Id));
        Assert.NotEmpty(Run(world, 60));
    }

    [Fact]
    public void FacingTheWrongWayIsNotTalking()
    {
        GameWorld world = World(Wanderer(1, 3, 3));
        ServerPlayer player = Join(world, 1, 3, 4, Direction.Down);

        world.StartTalking(player.Id, 1);

        Assert.Null(world.TalkingTo(player.Id));
    }

    [Fact]
    public void HangingUpByDisconnectingReleasesThem()
    {
        // The client says when a box closes. It does not, and cannot, say anything when
        // the window is closed instead — so the release has to be able to happen without
        // being asked for, or somebody stands to attention forever.
        GameWorld world = World(Wanderer(1, 3, 3));
        ServerPlayer talker = Join(world, 1, 3, 4, Direction.Up);

        world.StartTalking(talker.Id, 1);

        // Somebody else has to stay, or the map stops being simulated at all and this
        // would pass whether or not the hold was released.
        Join(world, 2, 0, 0, Direction.Down);
        world.Leave(talker.Id);

        Assert.NotEmpty(Run(world, 60));
    }

    [Fact]
    public void OnlyTheOneTalkingCanEndIt()
    {
        GameWorld world = World(Wanderer(1, 3, 3));
        ServerPlayer talker = Join(world, 1, 3, 4, Direction.Up);
        ServerPlayer other = Join(world, 2, 3, 2, Direction.Down);

        world.StartTalking(talker.Id, 1);
        world.StopTalking(other.Id);

        Assert.Equal(1, world.TalkingTo(talker.Id));
        Assert.Empty(Run(world, 60).Where(m => (m.X, m.Y) != (3, 3)));
    }

    [Fact]
    public void NobodyCollectsFrozenPeopleBehindThem()
    {
        // A client that loses a "finished" message and starts a second conversation
        // would otherwise leave the first person held for the rest of the session.
        GameWorld world = World(
            new MapObject(1, 5, 3, 3, Direction.Down, 2, false, 3, 3),
            new MapObject(2, 5, 3, 5, Direction.Down, 2, false, 3, 3));

        ServerPlayer player = Join(world, 1, 3, 4, Direction.Up);

        world.StartTalking(player.Id, 1);

        player.Facing = Direction.Down;
        world.StartTalking(player.Id, 2);

        Assert.Equal(2, world.TalkingTo(player.Id));
    }
}
