using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Ledges, and going over them.
/// <para>
/// The claim being tested is not "hopping works" but the shape of the rule, which has
/// three parts pointing in different directions. A ledge is a wall — every one of the
/// 1034 in this world is solid in the block data, and that is what makes it a ledge
/// rather than a step down. It is a wall in three directions and a door in the fourth.
/// And what is on the far side of it is two squares away, not one, because nobody ever
/// stands on a ledge.
/// </para>
/// </summary>
public class LedgeTests
{
    private const string Route = "3.90";

    /// <summary>
    /// A map with a ledge across the middle of it, hopped southward.
    /// <para>
    /// The ledge row is solid in the collision data, exactly as the cartridge's are. A
    /// fixture whose ledge was walkable would pass this whole file with the feature
    /// deleted — the character would simply stroll across it.
    /// </para>
    /// </summary>
    private static MapData Terrace()
    {
        const int width = 6;
        const int height = 6;

        var behaviours = new byte[width * height];
        var collision = new byte[width * height];

        for (int x = 0; x < width; x++)
        {
            behaviours[3 * width + x] = MetatileBehaviour.HopSouth;
            collision[3 * width + x] = 1;
        }

        return new MapData(Route, "THE TERRACE", width, height, collision) { Behaviours = behaviours };
    }

    private static GameWorld World() => new(new WorldData([Terrace()]), Route, TestRules.All, 1);

    private static ServerPlayer Standing(GameWorld world, GridPosition square, Direction facing)
    {
        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter() with { Party = [] });

        player.Square = square;
        player.Facing = facing;
        player.LastStepAt = double.NegativeInfinity;

        return player;
    }

    [Fact]
    public void ALedgeIsAWallInTheBlockDataAndStaysOne()
    {
        // The whole reason a hop has to exist at all. Nothing about this is opened up:
        // the square is not walkable before and is not walkable after.
        Assert.False(Terrace().ToGrid().IsWalkable(new GridPosition(2, 3)));
    }

    [Fact]
    public void GoingTheRightWayLandsTwoSquaresOn()
    {
        MapData map = Terrace();

        Assert.Equal(new GridPosition(2, 4), map.HopOnto(new GridPosition(2, 3), Direction.Down));
    }

    [Fact]
    public void GoingAnyOtherWayIsRefused()
    {
        // Three walls and a door. Coming at the same square from below is the one that
        // matters — that is the direction a player will try, having just hopped down.
        MapData map = Terrace();

        Assert.Null(map.HopOnto(new GridPosition(2, 3), Direction.Up));
        Assert.Null(map.HopOnto(new GridPosition(2, 3), Direction.Left));
        Assert.Null(map.HopOnto(new GridPosition(2, 3), Direction.Right));
    }

    [Fact]
    public void ASquareThatIsNotALedgeIsNotAHop()
    {
        Assert.Null(Terrace().HopOnto(new GridPosition(2, 1), Direction.Down));
    }

    [Fact]
    public void ALedgeWithNowhereToLandIsJustAWall()
    {
        // The bottom row of a map, or a ledge with a wall under it. Refused rather than
        // dropping somebody off the edge of the world.
        MapData map = Terrace();

        var collision = map.Collision.ToArray();
        collision[4 * map.Width + 2] = 1;

        Assert.Null((map with { Collision = collision }).HopOnto(new GridPosition(2, 3), Direction.Down));
    }

    [Fact]
    public void TheServerTakesTheHop()
    {
        GameWorld world = World();
        ServerPlayer player = Standing(world, new GridPosition(2, 2), Direction.Down);

        List<NetMessage> said = world.Move(player.Id, Direction.Down, 1).Select(o => o.Message).ToList();

        Assert.Equal(new GridPosition(2, 4), player.Square);

        PlayerHopped hop = said.OfType<PlayerHopped>().Single();

        Assert.Equal(2, hop.X);
        Assert.Equal(4, hop.Y);

        // And it is not sent as an ordinary step, because everyone watching has to draw
        // two squares of movement rather than one.
        Assert.Empty(said.OfType<PlayerMoved>());
    }

    [Fact]
    public void TheServerRefusesItFromBelow()
    {
        GameWorld world = World();
        ServerPlayer player = Standing(world, new GridPosition(2, 4), Direction.Up);

        world.Move(player.Id, Direction.Up, 1);

        Assert.Equal(new GridPosition(2, 4), player.Square);
    }

    [Fact]
    public void AHopCostsTwoStepsOfTheClock()
    {
        // Otherwise a row of ledges is a faster road than the path beside it, and the
        // rate limit that keeps everybody honest is the thing paying for it.
        GameWorld world = World();
        ServerPlayer player = Standing(world, new GridPosition(2, 2), Direction.Down);

        world.Move(player.Id, Direction.Down, 1);

        Assert.True(player.LastStepAt > 1);
    }

    [Fact]
    public void NobodyLandsOnSomebodyStandingThere()
    {
        // A hop has no half-way, so a landing square with somebody on it is a refusal
        // rather than a shorter jump. Tested with one of the map's own people, because
        // players have never blocked each other anywhere in this game and a rule about
        // them here would be a rule about nothing.
        var person = new MapObject(1, 5, 2, 4, Direction.Up, 0, IsTrainer: false);

        MapData map = Terrace() with { Objects = [person] };

        var world = new GameWorld(new WorldData([map]), Route, TestRules.All, 1);

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter() with { Party = [] });

        player.Square = new GridPosition(2, 2);
        player.Facing = Direction.Down;
        player.LastStepAt = double.NegativeInfinity;

        List<NetMessage> said = world.Move(player.Id, Direction.Down, 1).Select(o => o.Message).ToList();

        Assert.Equal(new GridPosition(2, 2), player.Square);
        Assert.Single(said.OfType<MoveRejected>());
    }

    [Fact]
    public void TheClientPredictsTheSameHop()
    {
        // The counterpart, and it has to exist: a client that could not predict this
        // would stand at the ledge pressing a direction that works for everybody else,
        // and one that predicted it wrongly would jump and be pulled back.
        MapData map = Terrace();

        var walking = new WalkingCharacter();

        walking.Place(
            map.ToGrid(),
            new GridPosition(2, 2),
            (square, facing) => map.HopOnto(square, facing));

        walking.Update(0f, Direction.Down);

        Assert.True(walking.IsHopping);
        Assert.Equal(new GridPosition(2, 4), walking.Square);
        Assert.Equal(Direction.Down, walking.ToReport);
    }

    [Fact]
    public void TheHopTakesTwoStepsWorthOfTime()
    {
        MapData map = Terrace();

        var walking = new WalkingCharacter();

        walking.Place(map.ToGrid(), new GridPosition(2, 2), (square, facing) => map.HopOnto(square, facing));

        walking.Update(0f, Direction.Down);
        walking.Update(WalkingCharacter.StepSeconds, null);

        // Half way, and off the ground: a step's worth of time has passed and a hop is
        // two of them.
        Assert.True(walking.IsHopping);
        Assert.True(walking.Arc > 0f);

        walking.Update(WalkingCharacter.StepSeconds, null);

        Assert.False(walking.IsHopping);
        Assert.Equal(0f, walking.Arc);
    }

    [Fact]
    public void AClientThatKnowsNothingOfLedgesJustWalksAsBefore()
    {
        // The feature is opt-in per placement, so everything that places a character
        // without one — a scene's cast, a map with no behaviour data — is unchanged.
        MapData map = Terrace();

        var walking = new WalkingCharacter();

        walking.Place(map.ToGrid(), new GridPosition(2, 2));
        walking.Update(0f, Direction.Down);

        Assert.False(walking.IsHopping);
        Assert.Equal(new GridPosition(2, 2), walking.Square);
    }
}

/// <summary>
/// Which way each ledge byte is hopped.
/// <para>
/// The values themselves are a measurement against a cartridge and cannot be checked
/// here. What can be checked is the shape of the answer, which is the part that was
/// wrong for four milestones: the names came from another game's table, and three of
/// the four were wrong about this one.
/// </para>
/// </summary>
public class LedgeDirectionTests
{
    [Fact]
    public void EveryLedgeByteIsHoppedSomewhereAndNothingElseIs()
    {
        Assert.Equal(Direction.Down, MetatileBehaviour.Hop(MetatileBehaviour.HopSouth));
        Assert.Equal(Direction.Right, MetatileBehaviour.Hop(MetatileBehaviour.HopEast));
        Assert.Equal(Direction.Left, MetatileBehaviour.Hop(MetatileBehaviour.HopWest));

        Assert.Null(MetatileBehaviour.Hop(MetatileBehaviour.Normal));
        Assert.Null(MetatileBehaviour.Hop(MetatileBehaviour.TallGrass));
        Assert.Null(MetatileBehaviour.Hop(MetatileBehaviour.Water));
    }

    [Fact]
    public void TheOneWithNoSquaresInTheWorldIsNotHoppedAtAll()
    {
        // 0x3A is in the run and on nought squares of this cartridge. Giving it a
        // direction would be inventing a rule for terrain that does not exist.
        Assert.Null(MetatileBehaviour.Hop(MetatileBehaviour.HopUnused));
    }

    [Fact]
    public void ALedgeIsStillRecognisedAsOne()
    {
        Assert.True(MetatileBehaviour.IsLedge(MetatileBehaviour.HopUnused));
        Assert.False(MetatileBehaviour.IsLedge(MetatileBehaviour.Normal));
    }
}
