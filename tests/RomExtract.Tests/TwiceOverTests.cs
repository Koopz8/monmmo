using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// One thing handed over, reported twice.
/// <para>
/// A conversation with a ball on the ground reaches this server along two roads. The
/// client says "I am talking to object 4", and the server hands the item over off the
/// world file — the half it can make on its own, needing nothing from anybody. Then the
/// client runs that ball's script, sees a give command in it, and reports that too; the
/// claim is checked against the same world file and handed over again.
/// </para>
/// <para>
/// Both roads were right. Neither knew about the other, and they kept separate lines in
/// the ledger of what a player has already picked up — <c>map:local</c> for the first and
/// <c>map:local:gift</c> for the second — so every one of the hundred and eighty-six
/// objects in this game that hands something over handed it over twice. A hundred and
/// seventy silent balls on the ground and sixteen people who talk while giving; the
/// overlap is not most of them, it is all of them.
/// </para>
/// <para>
/// Found by reading a save that had one LIFT KEY in the bag and both keys written down,
/// which only looked consistent because the lift had eaten the other one.
/// </para>
/// </summary>
public class TwiceOverTests
{
    private const string Town = "3.0";

    private static MapObject Ball(int localId) =>
        new(localId, 5, 3, 3, Direction.Down, 0, false)
        {
            GivesItemId = TestRules.PotionItem,
            GivesCount = 1,
            HiddenBy = 0x36,
            CanGive = [TestRules.PotionItem],
        };

    private static (GameWorld World, ServerPlayer Player) Standing(params MapObject[] people)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = people };

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));

        player.Facing = Direction.Up;

        return (world, player);
    }

    /// <summary>
    /// The whole of it. Walking up to a ball and reporting its script gives one item.
    /// </summary>
    [Fact]
    public void ABallOnTheGroundIsWorthOneItem()
    {
        (GameWorld world, ServerPlayer player) = Standing(Ball(1));

        world.StartTalking(player.Id, 1);
        world.ScriptGave(player.Id, 1, TestRules.PotionItem);

        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
        Assert.Equal("an item that has already been handed over", world.LastGift);
    }

    /// <summary>
    /// And in the other order, because nothing guarantees which report arrives first —
    /// they are two messages on one socket and the server handles each as it comes.
    /// </summary>
    [Fact]
    public void AndInTheOtherOrder()
    {
        (GameWorld world, ServerPlayer player) = Standing(Ball(1));

        world.ScriptGave(player.Id, 1, TestRules.PotionItem);
        world.StartTalking(player.Id, 1);

        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
        Assert.Contains("already been picked up", world.LastTalkOutcome ?? "");
    }

    /// <summary>
    /// Somebody who hands over two different things can still hand over both. The old key
    /// for a script's gift named the person and not the item, so the second was refused
    /// for good — which is two people in this game, the man in CERULEAN and the one on
    /// ROUTE 12, each holding something nobody could ever be given.
    /// </summary>
    [Fact]
    public void TwoDifferentThingsFromOnePersonAreBothHandedOver()
    {
        MapObject person = new(1, 5, 3, 3, Direction.Down, 0, false)
        {
            Talks = true,
            CanGive = [TestRules.PotionItem, TestRules.FullPotionItem],
        };

        (GameWorld world, ServerPlayer player) = Standing(person);

        world.ScriptGave(player.Id, 1, TestRules.PotionItem);
        world.ScriptGave(player.Id, 1, TestRules.FullPotionItem);

        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
        Assert.Equal(1, player.Bag.CountOf(TestRules.FullPotionItem));
    }

    /// <summary>And the same thing twice is still refused.</summary>
    [Fact]
    public void ButNotTheSameThingTwice()
    {
        MapObject person = new(1, 5, 3, 3, Direction.Down, 0, false)
        {
            Talks = true,
            CanGive = [TestRules.PotionItem],
        };

        (GameWorld world, ServerPlayer player) = Standing(person);

        world.ScriptGave(player.Id, 1, TestRules.PotionItem);
        world.ScriptGave(player.Id, 1, TestRules.PotionItem);

        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
    }

    /// <summary>
    /// A save written before this is left where it was. The line the talk writes is the
    /// line it always wrote, so a ball already picked up stays picked up rather than
    /// coming back worth one more.
    /// </summary>
    [Fact]
    public void AnOlderSaveIsNotWorthAnotherOne()
    {
        (GameWorld world, ServerPlayer player) = Standing(Ball(1));

        player.ItemsTaken.Add($"{Town}:1");

        world.StartTalking(player.Id, 1);
        world.ScriptGave(player.Id, 1, TestRules.PotionItem);

        Assert.Equal(0, player.Bag.CountOf(TestRules.PotionItem));
    }
}
