using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

public class NpcMovementTests
{
    private const string Town = "3.0";

    private static GameWorld World(params MapObject[] people)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = people };

        return new GameWorld(new WorldData([map]), Town, TestRules.All);
    }

    private static ServerPlayer Watching(GameWorld world, int x = 7, int y = 7)
    {
        (ServerPlayer player, _) = world.Join(
            1, "Mason", new SavedCharacter(Town, x, y, Direction.Down, []));

        return player;
    }

    /// <summary>Ticks until something happens, or gives up.</summary>
    private static List<ObjectMoved> TickUntilMoved(GameWorld world, int ticks = 200)
    {
        var moved = new List<ObjectMoved>();
        double now = 0;

        for (int i = 0; i < ticks && moved.Count == 0; i++)
        {
            now += 0.2;
            moved.AddRange(world.Tick(now).Select(o => o.Message).OfType<ObjectMoved>());
        }

        return moved;
    }

    private static MapObject Wanderer(int x, int y, int rangeX = 2, int rangeY = 2) =>
        new(1, 5, x, y, Direction.Down, 2, false, rangeX, rangeY);

    [Fact]
    public void AWandererMoves()
    {
        GameWorld world = World(Wanderer(3, 3));
        Watching(world);

        Assert.NotEmpty(TickUntilMoved(world));
    }

    [Fact]
    public void SomebodyStandingStillNeverMoves()
    {
        // Movement type zero is most of them: shopkeepers, the person by the sign.
        GameWorld world = World(new MapObject(1, 5, 3, 3, Direction.Down, 0, false));
        Watching(world);

        Assert.Empty(TickUntilMoved(world, ticks: 100));
    }

    [Fact]
    public void SomebodyWhoOnlyLooksAroundStaysPut()
    {
        GameWorld world = World(new MapObject(1, 5, 3, 3, Direction.Down, 1, false, 2, 2));
        Watching(world);

        List<ObjectMoved> moved = TickUntilMoved(world);

        Assert.NotEmpty(moved);
        Assert.All(moved, m => Assert.Equal((3, 3), (m.X, m.Y)));
    }

    [Fact]
    public void NobodyLeavesTheirBeat()
    {
        // The range is a box around where they started, per axis. Without it everybody
        // wanders off across the map and blocks doorways nobody expected.
        GameWorld world = World(Wanderer(4, 4, rangeX: 1, rangeY: 1));
        Watching(world);

        double now = 0;

        for (int i = 0; i < 400; i++)
        {
            now += 0.2;

            foreach (ObjectMoved moved in world.Tick(now).Select(o => o.Message).OfType<ObjectMoved>())
            {
                Assert.InRange(moved.X, 3, 5);
                Assert.InRange(moved.Y, 3, 5);
            }
        }
    }

    [Fact]
    public void APacerKeepsToItsAxis()
    {
        // Movement types three and four go up and down. Letting one pick sideways would
        // have it leave its post the moment its range in x happened to be non-zero.
        GameWorld world = World(new MapObject(1, 5, 4, 4, Direction.Up, 3, false, 3, 3));
        Watching(world);

        double now = 0;

        for (int i = 0; i < 400; i++)
        {
            now += 0.2;

            foreach (ObjectMoved moved in world.Tick(now).Select(o => o.Message).OfType<ObjectMoved>())
                Assert.Equal(4, moved.X);
        }
    }

    [Fact]
    public void NobodyWalksThroughAnybodyElse()
    {
        GameWorld world = World(Wanderer(3, 3), Wanderer(4, 3));
        Watching(world);

        var seen = new Dictionary<int, GridPosition>
        {
            [1] = new(3, 3),
        };

        double now = 0;

        for (int i = 0; i < 400; i++)
        {
            now += 0.2;

            foreach (ObjectMoved moved in world.Tick(now).Select(o => o.Message).OfType<ObjectMoved>())
            {
                seen[moved.LocalId] = new GridPosition(moved.X, moved.Y);

                Assert.Equal(seen.Count, seen.Values.Distinct().Count());
            }
        }
    }

    [Fact]
    public void NobodyStepsOntoAPlayer()
    {
        GameWorld world = World(Wanderer(3, 3, rangeX: 4, rangeY: 4));
        ServerPlayer player = Watching(world, 4, 3);

        double now = 0;

        for (int i = 0; i < 400; i++)
        {
            now += 0.2;

            foreach (ObjectMoved moved in world.Tick(now).Select(o => o.Message).OfType<ObjectMoved>())
                Assert.NotEqual(player.Square, new GridPosition(moved.X, moved.Y));
        }
    }

    [Fact]
    public void AMapNobodyIsOnIsNotSimulated()
    {
        // Sixteen hundred of these across four hundred maps. Stepping the ones nobody
        // can see would be the largest thing this server does, for nothing.
        GameWorld world = World(Wanderer(3, 3));

        double now = 0;

        for (int i = 0; i < 100; i++)
        {
            now += 0.2;
            Assert.Empty(world.Tick(now));
        }
    }

    [Fact]
    public void ArrivingSomewhereTellsYouWhoIsThere()
    {
        GameWorld world = World(Wanderer(3, 3), Wanderer(5, 5));

        (ServerPlayer player, List<Outgoing> send) = world.Join(
            1, "Mason", new SavedCharacter(Town, 0, 0, Direction.Down, []));

        ObjectsPlaced placed = send
            .Where(o => o.OnlyTo == player.Id)
            .Select(o => o.Message)
            .OfType<ObjectsPlaced>()
            .Single();

        Assert.Equal(2, placed.Objects.Count);
    }

    [Fact]
    public void APersonWhoHasMovedIsStillSolid()
    {
        // Collision has to follow them. Reading it from the map file would leave a
        // hole where somebody used to be and a person you can walk through.
        GameWorld world = World(Wanderer(3, 3, rangeX: 3, rangeY: 0));
        ServerPlayer player = Watching(world, 0, 0);

        double now = 0;
        GridPosition? wanderer = null;

        for (int i = 0; i < 400 && wanderer is null; i++)
        {
            now += 0.2;

            foreach (ObjectMoved moved in world.Tick(now).Select(o => o.Message).OfType<ObjectMoved>())
            {
                if (moved.X != 3) wanderer = new GridPosition(moved.X, moved.Y);
            }
        }

        Assert.NotNull(wanderer);

        // Stand next to where they now are and try to walk into them.
        player.Square = new GridPosition(wanderer.Value.X, wanderer.Value.Y + 1);
        player.LastStepAt = double.NegativeInfinity;

        world.Move(player.Id, Direction.Up, now + 10);

        Assert.NotEqual(wanderer.Value, player.Square);
    }
}

/// <summary>
/// How somebody on a map is drawn between the squares the server names.
/// <para>
/// The server sends a square a second or so apart. Without this they teleport, which
/// is what "clunky" looks like from the outside.
/// </para>
/// </summary>
public class WalkingPersonTests
{
    private static WalkingPerson Somebody(int x = 2, int y = 2) =>
        new(5, new GridPosition(x, y), Direction.Down);

    [Fact]
    public void AStepSlidesRatherThanTeleporting()
    {
        WalkingPerson person = Somebody();

        person.GoTo(new GridPosition(3, 2), Direction.Right);

        (float startX, _) = person.PixelPosition;
        Assert.True(person.IsWalking);

        person.Update(WalkingCharacter.StepSeconds / 2f);

        (float midX, _) = person.PixelPosition;

        // Somewhere between the two squares, not at either end.
        Assert.True(midX > startX);
        Assert.True(midX < 3 * WalkingCharacter.SquarePixels);
    }

    [Fact]
    public void AStepFinishesOnTheSquare()
    {
        WalkingPerson person = Somebody();

        person.GoTo(new GridPosition(3, 2), Direction.Right);
        person.Update(WalkingCharacter.StepSeconds);

        Assert.False(person.IsWalking);
        Assert.Equal(3 * WalkingCharacter.SquarePixels, person.PixelPosition.X);
    }

    [Fact]
    public void ATurnOnTheSpotMovesNothing()
    {
        // Looking around and stepping arrive as the same message. A shopkeeper glancing
        // about should not slide anywhere, and should not appear to be mid-stride.
        WalkingPerson person = Somebody();

        (float x, float y) = person.PixelPosition;

        person.GoTo(new GridPosition(2, 2), Direction.Left);

        Assert.Equal(Direction.Left, person.Facing);
        Assert.False(person.IsWalking);
        Assert.Equal((x, y), person.PixelPosition);
    }

    [Fact]
    public void OnlyStepsChangeFeet()
    {
        WalkingPerson person = Somebody();

        person.GoTo(new GridPosition(2, 2), Direction.Left);
        person.GoTo(new GridPosition(2, 2), Direction.Up);

        Assert.Equal(0, person.Stride);

        person.GoTo(new GridPosition(2, 1), Direction.Up);
        person.Update(WalkingCharacter.StepSeconds);
        person.GoTo(new GridPosition(2, 0), Direction.Up);

        // Two steps, two different feet — a walker that never alternates limps.
        Assert.Equal(2, person.Stride);
    }

    [Fact]
    public void AnUpdateArrivingMidStepDoesNotSnapBackwards()
    {
        // The server can send the next square before the last slide has finished. The
        // walk has to carry on from where they are drawn, not from where they were.
        WalkingPerson person = Somebody();

        person.GoTo(new GridPosition(3, 2), Direction.Right);
        person.Update(WalkingCharacter.StepSeconds / 2f);

        (float midX, _) = person.PixelPosition;

        person.GoTo(new GridPosition(4, 2), Direction.Right);

        Assert.Equal(midX, person.PixelPosition.X);
    }
}
