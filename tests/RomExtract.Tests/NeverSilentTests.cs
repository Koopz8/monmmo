using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A refused step is always answered.
/// <para>
/// The client predicts every step from its own collision grid and reports it, and takes the
/// server's word back as a correction. That arrangement is sound for exactly as long as the
/// server always has a word — and one path did not. A step blocked by something, from a
/// player who was already facing that way, returned nothing at all:
/// </para>
/// <code>
/// return before == direction ? [] : [PlayerMoved …];
/// </code>
/// <para>
/// The reasoning was that the client had predicted the same refusal and was already standing
/// still. That is true exactly as often as the two sides agree about the square, and the test
/// is half walkability — which they always agree on, being the same map file — and half who
/// is standing there, which they do not: people arrive, leave, are hidden by flags one side
/// has and the other has not, and are placed by messages that can be missed.
/// </para>
/// <para>
/// Every time they disagreed, the client stepped, the server refused in silence, and the
/// client was one square wrong for ever. Three of those is a captain on the S.S. ANNE who
/// cannot be spoken to, and before that a GIOVANNI who read as a missing person for a whole
/// milestone.
/// </para>
/// </summary>
public class NeverSilentTests
{
    private const string Room = "1.0";

    /// <summary>A room with somebody standing one square north of the player.</summary>
    private static (GameWorld World, ServerPlayer Player) Standing(bool withSomebody)
    {
        MapObject other = new(1, 5, 3, 3, Direction.Down, 0, false) { Talks = true };

        MapData map = new(Room, "PALLET TOWN", 8, 8, new byte[64])
        {
            Objects = withSomebody ? [other] : [],
        };

        var world = new GameWorld(new WorldData([map]), Room, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Room, 3, 4) with
        {
            Facing = Direction.Up,
        });

        return (world, player);
    }

    private static List<NetMessage> ToTheMover(List<Outgoing> from, int playerId) =>
        [.. from.Where(o => o.OnlyTo is null || o.OnlyTo == playerId).Select(o => o.Message)];

    /// <summary>
    /// The whole of it. Walking into somebody while already facing them says something —
    /// which used to be the one case that said nothing.
    /// </summary>
    [Fact]
    public void AStepBlockedBysomebodyIsAnsweredEvenWhenTheFacingDoesNotChange()
    {
        (GameWorld world, ServerPlayer player) = Standing(withSomebody: true);

        List<Outgoing> said = world.Move(player.Id, Direction.Up, 1.0);

        Assert.NotEmpty(ToTheMover(said, player.Id));
    }

    /// <summary>And what it says is where the player actually is.</summary>
    [Fact]
    public void AndItSaysWhereTheyReallyAre()
    {
        (GameWorld world, ServerPlayer player) = Standing(withSomebody: true);

        List<Outgoing> said = world.Move(player.Id, Direction.Up, 1.0);

        MoveRejected put = Assert.Single(ToTheMover(said, player.Id).OfType<MoveRejected>());

        Assert.Equal(player.Square.X, put.X);
        Assert.Equal(player.Square.Y, put.Y);
        Assert.Equal(Direction.Up, put.Facing);
    }

    /// <summary>
    /// A blocked step that also turns still announces the turn to the whole map, because
    /// everybody else has to see somebody face the wall they walked into.
    /// </summary>
    [Fact]
    public void ABlockedStepThatTurnsIsStillAnnouncedToEveryone()
    {
        (GameWorld world, ServerPlayer player) = Standing(withSomebody: true);

        player.Facing = Direction.Down;

        List<Outgoing> said = world.Move(player.Id, Direction.Up, 1.0);

        PlayerMoved turned = Assert.Single(said.Select(o => o.Message).OfType<PlayerMoved>());

        Assert.Equal(Direction.Up, turned.Facing);
        Assert.Equal(player.Square.Y, turned.Y);
    }

    /// <summary>And a step nothing blocks is still a step.</summary>
    [Fact]
    public void AnOrdinaryStepIsUnchanged()
    {
        (GameWorld world, ServerPlayer player) = Standing(withSomebody: false);

        List<Outgoing> said = world.Move(player.Id, Direction.Up, 1.0);

        PlayerMoved moved = Assert.Single(said.Select(o => o.Message).OfType<PlayerMoved>());

        Assert.Equal(3, moved.Y);
        Assert.Empty(said.Select(o => o.Message).OfType<MoveRejected>());
    }

    /// <summary>
    /// The invariant behind all of it, stated once: whatever a player asks, the server
    /// answers them. This is the test that would have caught the original hole without
    /// anybody having to think of the case.
    /// </summary>
    [Theory]
    [InlineData(Direction.Up)]
    [InlineData(Direction.Down)]
    [InlineData(Direction.Left)]
    [InlineData(Direction.Right)]
    public void EveryDirectionIsAnswered(Direction direction)
    {
        (GameWorld world, ServerPlayer player) = Standing(withSomebody: true);

        player.Facing = direction;

        Assert.NotEmpty(ToTheMover(world.Move(player.Id, direction, 1.0), player.Id));
    }
}
