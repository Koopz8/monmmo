using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The flag that hides a ball on the ground, said by the server.
/// <para>
/// One fact is written down twice. A ball lying on the floor is hidden by a flag, and
/// that same flag is what the cartridge sets when somebody picks the ball up — hidden and
/// taken are the same bit. This server kept the taken half itself, in a list of what each
/// player has already picked up, and left the hidden half entirely to the client: the
/// client picks the ball up, sets the flag on its own map and reports it.
/// </para>
/// <para>
/// That is fine until the report goes missing, and then the two halves disagree for good.
/// The item is in the bag, the ball is still standing on the floor, and walking up to it
/// again is refused because the server remembers handing it over — a ball nobody can pick
/// up and nothing can clear. So the server says it too, off the pickup it already knows
/// about, and says it whether or not this was the pickup that did it.
/// </para>
/// <para>
/// Found the long way round: a played run through the ROCKET HIDEOUT looked like it had
/// lost a cleared flag across a sign-out, and the save said otherwise — LIFT KEY in the
/// bag, the Rocket beaten, the ball's flag set. Nothing had been lost. The flag came back
/// because picking the ball up is supposed to bring it back, and the only thing wrong was
/// which side of the wire had said so.
/// </para>
/// </summary>
public class GhostBallTests
{
    private const string Town = "3.0";

    /// <summary>The flag hiding it, which is also the flag remembering it was taken.</summary>
    private const int Hides = 0x0036;

    private static MapObject Ball(int localId, int hiddenBy = Hides) =>
        new(localId, 5, 3, 3, Direction.Down, 0, false)
        {
            GivesItemId = TestRules.PotionItem,
            GivesCount = 1,
            HiddenBy = hiddenBy,
        };

    /// <summary>The same thing with somebody behind it, who does not disappear.</summary>
    private static MapObject Giver(int localId) => Ball(localId) with { Talks = true };

    private static GameWorld World(params MapObject[] people)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = people };

        return new GameWorld(new WorldData([map]), Town, TestRules.All);
    }

    private static (GameWorld World, ServerPlayer Player) Standing(params MapObject[] people)
    {
        GameWorld world = World(people);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));

        player.Facing = Direction.Up;

        return (world, player);
    }

    [Fact]
    public void PickingUpABallSetsTheFlagThatHidesIt()
    {
        (GameWorld world, ServerPlayer player) = Standing(Ball(1));

        Assert.False(player.Script.Has(Hides));

        world.StartTalking(player.Id, 1);

        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
        Assert.True(player.Script.Has(Hides));
    }

    /// <summary>
    /// And the ball goes off the map on the server's own say-so. The client takes it off
    /// its own map the moment it hands over, so this is the second of two — but it is the
    /// one that survives a client that never got round to saying anything.
    /// </summary>
    [Fact]
    public void AndTheBallGoesOffTheMap()
    {
        (GameWorld world, ServerPlayer player) = Standing(Ball(1));

        List<Outgoing> said = world.StartTalking(player.Id, 1);

        Assert.Contains(said, o => o.Message is WentInside { LocalId: 1 });
    }

    /// <summary>
    /// And it is still gone after signing out and back in, which is the whole point of
    /// writing it down as a flag rather than only as a list of what has been taken.
    /// </summary>
    [Fact]
    public void ItIsStillGoneOnTheWayBackIn()
    {
        (GameWorld world, ServerPlayer player) = Standing(Ball(1));

        world.StartTalking(player.Id, 1);

        SavedCharacter saved = world.Snapshot(player.Id)!;

        Assert.Contains(Hides, saved.Flags);

        GameWorld again = World(Ball(1));

        (_, List<Outgoing> send) = again.Join(1, "Mason", saved);

        Assert.DoesNotContain(
            send.Select(o => o.Message).OfType<ObjectsPlaced>().SelectMany(p => p.Objects),
            o => o.LocalId == 1);
    }

    /// <summary>
    /// A save that already lost the flag is put right by walking up to the ball again.
    /// The item is not handed over twice — the list of what has been taken says so — but
    /// the flag is set all the same, which is what clears a ghost.
    /// </summary>
    [Fact]
    public void AGhostIsPutRightByWalkingUpToIt()
    {
        (GameWorld world, ServerPlayer player) = Standing(Ball(1));

        // What a lost report leaves behind: taken, and still standing there.
        player.ItemsTaken.Add($"{Town}:1");

        List<Outgoing> said = world.StartTalking(player.Id, 1);

        Assert.Equal(0, player.Bag.CountOf(TestRules.PotionItem));
        Assert.True(player.Script.Has(Hides));
        Assert.Contains(said, o => o.Message is WentInside { LocalId: 1 });
    }

    /// <summary>
    /// And somebody who hands something over <em>while</em> talking stays exactly where
    /// they are. The president of SILPH does not disappear on handing over a MASTER BALL,
    /// and this is the same difference the client draws: a thing that gives and says
    /// nothing is a ball, and everybody else is a person.
    /// </summary>
    [Fact]
    public void SomebodyWhoTalksWhileHandingItOverStaysPut()
    {
        (GameWorld world, ServerPlayer player) = Standing(Giver(1));

        world.StartTalking(player.Id, 1);

        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
        Assert.False(player.Script.Has(Hides));
    }

    /// <summary>
    /// A ball carrying no flag at all is left alone rather than having one invented for
    /// it. Flag zero is not a flag, and setting it would hide whoever else on that map is
    /// waiting on it.
    /// </summary>
    [Fact]
    public void ABallWithNoFlagHasNoneInvented()
    {
        (GameWorld world, ServerPlayer player) = Standing(Ball(1, hiddenBy: 0));

        List<Outgoing> said = world.StartTalking(player.Id, 1);

        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
        Assert.False(player.Script.Has(0));
        Assert.DoesNotContain(said, o => o.Message is WentInside);
    }
}
