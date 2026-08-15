using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Players walk through each other, and everything else still blocks.
/// <para>
/// A decision rather than a discovery. A game where standing still is a wall is a game where
/// one person can shut a door for everybody, and a doorway is exactly where somebody would
/// choose to stand — so two players may share a square and neither is refused.
/// </para>
/// <para>
/// It was found by accident, walking one client through another the first time two of them
/// were ever run at once, and at that point it was not a decision at all: it was what the
/// code happened to do while a second rule counted a player's square as occupied. These
/// tests are what turns that into something on purpose, and what stops a later reading of
/// "somebody is in the way" putting collision back.
/// </para>
/// </summary>
public class WalkingThroughTests
{
    private const string Town = "1.0";

    private static (GameWorld World, ServerPlayer One, ServerPlayer Two) Both(params MapObject[] people)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = people };

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer one, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));
        (ServerPlayer two, _) = world.Join(2, "Koop", SavedCharacter.Fresh(Town, 3, 4));

        // Put them a square apart, facing along it, whatever the spawn rule did with them.
        one.Square = new GridPosition(3, 4);
        two.Square = new GridPosition(3, 3);
        one.Facing = Direction.Up;

        return (world, one, two);
    }

    /// <summary>The decision, stated as a test.</summary>
    [Fact]
    public void APlayerWalksThroughAnotherPlayer()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Both();

        world.Move(one.Id, Direction.Up, 1.0);

        Assert.Equal(two.Square, one.Square);
    }

    /// <summary>And is not refused, which is the other half of the same sentence.</summary>
    [Fact]
    public void AndIsNotRefused()
    {
        (GameWorld world, ServerPlayer one, _) = Both();

        List<Outgoing> said = world.Move(one.Id, Direction.Up, 1.0);

        Assert.Empty(said.Select(o => o.Message).OfType<MoveRejected>());
        Assert.Contains(said.Select(o => o.Message).OfType<PlayerMoved>(), m => m.PlayerId == one.Id);
    }

    /// <summary>
    /// The map's own people still block, which is the cartridge's rule and not up for
    /// decision. Without this test "players do not block" is one careless edit away from
    /// "nothing blocks".
    /// </summary>
    [Fact]
    public void ThePeopleOnTheMapStillBlock()
    {
        MapObject standing = new(1, 5, 3, 3, Direction.Down, 0, false) { Talks = true };

        (GameWorld world, ServerPlayer one, _) = Both(standing);

        world.Move(one.Id, Direction.Up, 1.0);

        Assert.Equal(new GridPosition(3, 4), one.Square);
    }

    /// <summary>
    /// And a blocked step is still answered, which is milestone 89's invariant and the
    /// thing that kept a captain out of reach for two milestones when it was not true.
    /// </summary>
    [Fact]
    public void ABlockedStepIsStillAnswered()
    {
        MapObject standing = new(1, 5, 3, 3, Direction.Down, 0, false) { Talks = true };

        (GameWorld world, ServerPlayer one, _) = Both(standing);

        List<Outgoing> said = world.Move(one.Id, Direction.Up, 1.0);

        Assert.NotEmpty(said.Where(o => o.OnlyTo is null || o.OnlyTo == one.Id));
    }

    /// <summary>
    /// Two players may share a square, and both of them may then walk off it. A rule that
    /// let somebody in and not out would be worse than one that kept them out.
    /// </summary>
    [Fact]
    public void BothCanLeaveASquareTheyAreSharing()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Both();

        world.Move(one.Id, Direction.Up, 1.0);

        Assert.Equal(one.Square, two.Square);

        world.Move(one.Id, Direction.Up, 2.0);
        world.Move(two.Id, Direction.Down, 2.0);

        Assert.NotEqual(one.Square, two.Square);
    }

    /// <summary>
    /// Everybody arrives exactly where they left off, even onto somebody.
    /// <para>
    /// This test was written the other way round — "an arriving player is not put on top of
    /// one" — on the strength of a rule in the code that counts a player's square as
    /// occupied, and it failed at once: four characters saved on one square all arrive on
    /// it. The rule it was reasoning from only runs when the <em>saved</em> square is
    /// unusable, and it never was.
    /// </para>
    /// <para>
    /// Which is the better behaviour anyway, and now says so on purpose. Once players walk
    /// through each other there is nothing to protect, and the alternative — moving somebody
    /// off the square they signed out on because a stranger is standing there — is a login
    /// that puts you somewhere you have never been.
    /// </para>
    /// </summary>
    [Fact]
    public void EverybodyArrivesWhereTheyLeftOffEvenOntoSomebody()
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        var squares = new List<GridPosition>();

        for (int i = 1; i <= 4; i++)
        {
            (ServerPlayer joined, _) = world.Join(i, $"Player{i}", SavedCharacter.Fresh(Town, 3, 4));
            squares.Add(joined.Square);
        }

        Assert.All(squares, square => Assert.Equal(new GridPosition(3, 4), square));
    }
}
