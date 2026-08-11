using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a player is carrying.
/// <para>
/// Item ids and counts, and nothing else — the pocket something lives in is a property
/// of the item and stays in the rules file, because two places recording it is two
/// places that can disagree.
/// </para>
/// </summary>
public class BagTests
{
    [Fact]
    public void AddingSaysHowManyWentIn()
    {
        // The number taken rather than a yes or no, because a shop has to charge for
        // what it actually handed over. A bag with room for two cannot sell five and
        // cannot silently swallow three.
        var bag = new Bag();

        Assert.Equal(5, bag.Add(4, 5));
        Assert.Equal(5, bag.CountOf(4));

        Assert.Equal(Bag.MaxStack - 5, bag.Add(4, 200));
        Assert.Equal(Bag.MaxStack, bag.CountOf(4));

        Assert.Equal(0, bag.Add(4, 1));
    }

    [Fact]
    public void RemovingSaysHowManyWereThere()
    {
        var bag = new Bag();
        bag.Add(4, 3);

        Assert.Equal(3, bag.Remove(4, 10));
        Assert.Equal(0, bag.CountOf(4));
        Assert.Equal(0, bag.Remove(4));
    }

    [Fact]
    public void SomethingRunOutOfIsNoLongerCarried()
    {
        // An empty stack left in place is a bag that fills up with nothing.
        var bag = new Bag();

        bag.Add(4, 1);
        bag.Remove(4, 1);

        Assert.Equal(0, bag.DistinctItems);
        Assert.Empty(bag.Entries);
    }

    [Fact]
    public void ThingsStayInTheOrderTheyWerePickedUp()
    {
        // Not id order. A bag that reshuffles itself when you pick something up is one
        // whose third slot is never the same thing twice.
        var bag = new Bag();

        bag.Add(9);
        bag.Add(2);
        bag.Add(5);
        bag.Add(2, 4);

        Assert.Equal([9, 2, 5], bag.Entries.Select(e => e.ItemId));
    }

    [Fact]
    public void APocketFillsUpRatherThanGrowingForever()
    {
        // A cap has to exist or a player who walks over every item in the world ends up
        // with a save nobody can load.
        var bag = new Bag();

        for (int id = 1; id <= Bag.PocketCapacity; id++) Assert.Equal(1, bag.Add(id));

        Assert.Equal(0, bag.Add(9999));

        // And a full bag still takes more of something already in it.
        Assert.Equal(1, bag.Add(1));
    }

    [Fact]
    public void ABagBuiltFromASaveKeepsWhatWasSaved()
    {
        var bag = new Bag([new BagEntry(4, 12), new BagEntry(2, 3)]);

        Assert.Equal(12, bag.CountOf(4));
        Assert.Equal(3, bag.CountOf(2));
    }

    [Fact]
    public void NonsenseInASaveIsDroppedRatherThanCarried()
    {
        // A save is a file, and a file can be edited. None of these are things a bag
        // can hold, and none of them should stop an account from loading.
        var bag = new Bag([new BagEntry(0, 5), new BagEntry(-3, 2), new BagEntry(4, 0), new BagEntry(4, -1)]);

        Assert.Equal(0, bag.DistinctItems);
    }

    [Fact]
    public void APocketIsWhateverTheRulesSayItIs()
    {
        var bag = new Bag([new BagEntry(TestRules.BallItem, 5), new BagEntry(TestRules.PotionItem, 2)]);

        Assert.Equal(
            [TestRules.BallItem],
            bag.InPocket(TestRules.All, Pocket.Balls).Select(e => e.ItemId));

        Assert.Equal(
            [TestRules.PotionItem],
            bag.InPocket(TestRules.All, Pocket.Items).Select(e => e.ItemId));
    }
}

/// <summary>
/// Money, and throwing what is actually in the bag.
/// </summary>
public class MoneyAndBallTests
{
    private const string Route = "3.19";

    private static GameWorld GrassyWorld()
    {
        var behaviours = new byte[16];
        Array.Fill(behaviours, MetatileBehaviour.TallGrass);

        MapData map = new(Route, "ROUTE 1", 4, 4, new byte[16])
        {
            Behaviours = behaviours,
            Encounters = new MapEncounters(Route, Land: new EncounterTable(
                EncounterKind.Land,
                100,
                Enumerable.Range(0, 12).Select(_ => new WildSlot(16, 3, 3)).ToList())),
        };

        return new GameWorld(new WorldData([map]), Route, TestRules.All);
    }

    private static (GameWorld World, ServerPlayer Player, BattleStarted Start) InBattle()
    {
        GameWorld world = GrassyWorld();

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        double now = 0;

        for (int step = 0; step < 200; step++)
        {
            player.Square = new GridPosition(step % 4, 1);
            player.LastStepAt = double.NegativeInfinity;
            now += 1;

            foreach (Outgoing outgoing in world.Move(player.Id, Direction.Down, now))
            {
                if (outgoing.Message is BattleStarted started) return (world, player, started);
            }
        }

        throw new InvalidOperationException("Never met anything.");
    }

    [Fact]
    public void ANewCharacterIsHandedBallsRatherThanACount()
    {
        GameWorld world = GrassyWorld();

        SavedCharacter fresh = world.FreshCharacter();

        BagEntry balls = Assert.Single(fresh.Items);

        Assert.Equal(TestRules.BallItem, balls.ItemId);
        Assert.Equal(SavedCharacter.StartingBalls, balls.Count);
        Assert.Equal(SavedCharacter.StartingMoney, fresh.Money);
    }

    [Fact]
    public void ABattleIsToldWhatIsInTheBallPocketAndNothingElse()
    {
        GameWorld world = GrassyWorld();

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());
        player.Bag.Add(TestRules.PotionItem, 4);

        (_, _, BattleStarted start) = InBattleWith(world, player);

        Assert.DoesNotContain(start.Balls, b => b.ItemId == TestRules.PotionItem);
        Assert.Contains(start.Balls, b => b.ItemId == TestRules.BallItem);
    }

    private static (GameWorld, ServerPlayer, BattleStarted) InBattleWith(GameWorld world, ServerPlayer player)
    {
        double now = 0;

        for (int step = 0; step < 200; step++)
        {
            player.Square = new GridPosition(step % 4, 1);
            player.LastStepAt = double.NegativeInfinity;
            now += 1;

            foreach (Outgoing outgoing in world.Move(player.Id, Direction.Down, now))
            {
                if (outgoing.Message is BattleStarted started) return (world, player, started);
            }
        }

        throw new InvalidOperationException("Never met anything.");
    }

    [Fact]
    public void ThrowingSomethingThatIsNotABallIsNotAThrow()
    {
        // The client sends an item id. Nothing stops it sending the id of a potion, and
        // the answer to that is a wasted turn rather than a catch.
        (GameWorld world, ServerPlayer player, _) = InBattle();

        player.Bag.Add(TestRules.PotionItem, 3);

        List<Outgoing> send = world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(TestRules.PotionItem));

        BattleUpdate update = send.Select(o => o.Message).OfType<BattleUpdate>().Single();

        Assert.DoesNotContain(update.Events, e => e is BattleEvent.BallThrown);
        Assert.Equal(3, player.Bag.CountOf(TestRules.PotionItem));
    }

    [Fact]
    public void ThrowingSomethingNotInTheBagIsNotAThrowEither()
    {
        (GameWorld world, ServerPlayer player, _) = InBattle();

        List<Outgoing> send = world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(TestRules.UltraBallItem));

        BattleUpdate update = send.Select(o => o.Message).OfType<BattleUpdate>().Single();

        Assert.DoesNotContain(update.Events, e => e is BattleEvent.BallThrown);
        Assert.Equal(0, player.Bag.CountOf(TestRules.UltraBallItem));
    }

    /// <summary>A world whose grass holds something an ordinary ball will not catch.</summary>
    private static GameWorld StubbornWorld()
    {
        var behaviours = new byte[16];
        Array.Fill(behaviours, MetatileBehaviour.TallGrass);

        MapData map = new(Route, "ROUTE 1", 4, 4, new byte[16])
        {
            Behaviours = behaviours,
            Encounters = new MapEncounters(Route, Land: new EncounterTable(
                EncounterKind.Land,
                100,
                Enumerable.Range(0, 12).Select(_ => new WildSlot(TestRules.HardToCatch, 3, 3)).ToList())),
        };

        return new GameWorld(new WorldData([map]), Route, TestRules.All);
    }

    [Fact]
    public void WhichBallItIsIsTheServersAnswerAndNotTheClients()
    {
        // A request naming a kind would let a client spend the cheap one and throw the
        // good one. It names an item; the kind comes out of the rules.
        //
        // Asserting only that the right item left the bag was not enough — that passed
        // whether or not the server read the kind at all. What makes this bite is a
        // target an ordinary ball cannot catch, so the two answers look different.
        GameWorld world = StubbornWorld();

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());
        player.Bag.Add(TestRules.MasterBallItem, 1);

        (_, _, _) = InBattleWith(world, player);

        List<Outgoing> send = world.TakeBattleTurn(
            player.Id,
            new BattleAction.ThrowBall(TestRules.MasterBallItem) { Kind = BallKind.Poke });

        BattleEvent.BallThrown thrown = send
            .Select(o => o.Message)
            .OfType<BattleUpdate>()
            .SelectMany(u => u.Events)
            .OfType<BattleEvent.BallThrown>()
            .Single();

        Assert.True(thrown.Caught);
    }

    [Fact]
    public void AnOrdinaryBallWouldNotHaveCaughtIt()
    {
        // The control. Without it the test above proves only that something was caught,
        // not that the ball is what caught it.
        GameWorld world = StubbornWorld();

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        (_, _, _) = InBattleWith(world, player);

        List<Outgoing> send = world.TakeBattleTurn(
            player.Id,
            new BattleAction.ThrowBall(TestRules.BallItem) { Kind = BallKind.Master });

        BattleEvent.BallThrown thrown = send
            .Select(o => o.Message)
            .OfType<BattleUpdate>()
            .SelectMany(u => u.Events)
            .OfType<BattleEvent.BallThrown>()
            .Single();

        Assert.False(thrown.Caught);
    }

    [Fact]
    public void OnlyTheBallThrownLeavesTheBag()
    {
        (GameWorld world, ServerPlayer player, _) = InBattle();

        player.Bag.Add(TestRules.UltraBallItem, 2);

        int ordinary = player.Bag.CountOf(TestRules.BallItem);

        world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(TestRules.UltraBallItem));

        Assert.Equal(1, player.Bag.CountOf(TestRules.UltraBallItem));
        Assert.Equal(ordinary, player.Bag.CountOf(TestRules.BallItem));
    }
}

/// <summary>
/// Using something out of the bag in a battle.
/// <para>
/// The amount was already extracted and nobody had looked: a Potion's restore is in the
/// same field as a held item's parameter, 20 on a Potion and 200 on a Hyper Potion. This
/// project had a paragraph planned about reading a second table with a variable-length
/// format before checking what it already had.
/// </para>
/// </summary>
public class UsingItemsTests
{
    private const string Route = "3.19";

    private static GameWorld World()
    {
        var behaviours = new byte[16];
        Array.Fill(behaviours, MetatileBehaviour.TallGrass);

        MapData map = new(Route, "ROUTE 1", 4, 4, new byte[16])
        {
            Behaviours = behaviours,
            Encounters = new MapEncounters(Route, Land: new EncounterTable(
                EncounterKind.Land, 100,
                Enumerable.Range(0, 12).Select(_ => new WildSlot(16, 3, 3)).ToList())),
        };

        return new GameWorld(new WorldData([map]), Route, TestRules.All);
    }

    /// <summary>In a battle, with a lead that has already taken a beating.</summary>
    private static (GameWorld World, ServerPlayer Player) Hurt()
    {
        GameWorld world = World();

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter() with
        {
            Party = [new SavedMon(3, 30, null, 1, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])],
        });

        double now = 0;

        for (int step = 0; step < 200; step++)
        {
            player.Square = new GridPosition(step % 4, 1);
            player.LastStepAt = double.NegativeInfinity;
            now += 1;

            if (world.Move(player.Id, Direction.Down, now).Any(o => o.Message is BattleStarted))
                return (world, player);
        }

        throw new InvalidOperationException("Never met anything.");
    }

    private static BattleUpdate Use(GameWorld world, ServerPlayer player, int itemId) =>
        world.TakeBattleTurn(player.Id, new BattleAction.UseItem(itemId))
            .Select(o => o.Message)
            .OfType<BattleUpdate>()
            .Single();

    [Fact]
    public void APotionPutsHealthBackAndIsSpent()
    {
        (GameWorld world, ServerPlayer player) = Hurt();

        player.Bag.Add(TestRules.PotionItem, 2);

        BattleUpdate update = Use(world, player, TestRules.PotionItem);

        BattleEvent.HealthRestored healed = update.Events.OfType<BattleEvent.HealthRestored>().Single();

        Assert.Equal(20, healed.Amount);
        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
    }

    [Fact]
    public void HowMuchItRestoresIsTheServersNumber()
    {
        // A request that carried the amount would let a client drink a Potion for two
        // hundred. It names an item; the amount comes out of the rules.
        (GameWorld world, ServerPlayer player) = Hurt();

        player.Bag.Add(TestRules.PotionItem, 1);

        BattleUpdate update = world
            .TakeBattleTurn(player.Id, new BattleAction.UseItem(TestRules.PotionItem) { Restores = 999 })
            .Select(o => o.Message)
            .OfType<BattleUpdate>()
            .Single();

        Assert.Equal(20, update.Events.OfType<BattleEvent.HealthRestored>().Single().Amount);
    }

    [Fact]
    public void TheFullRestoreKindFillsWhateverTheMaximumIs()
    {
        // 255 is not an amount, it is the cartridge's way of saying "all of it".
        (GameWorld world, ServerPlayer player) = Hurt();

        player.Bag.Add(TestRules.FullPotionItem, 1);

        BattleUpdate update = Use(world, player, TestRules.FullPotionItem);

        Assert.True(update.Events.OfType<BattleEvent.HealthRestored>().Single().Amount > 20);
        Assert.Equal(update.YourHp, player.Party[0].CurrentHp);
    }

    [Fact]
    public void UsingSomethingThatRestoresNothingIsNotAUse()
    {
        (GameWorld world, ServerPlayer player) = Hurt();

        int before = player.Bag.CountOf(TestRules.BallItem);

        BattleUpdate update = Use(world, player, TestRules.BallItem);

        // Not restoring anything and not being spent either. A ball is thrown, not drunk.
        Assert.Empty(update.Events.OfType<BattleEvent.HealthRestored>());
        Assert.Equal(before, player.Bag.CountOf(TestRules.BallItem));
    }

    [Fact]
    public void UsingSomethingYouDoNotHaveIsNotAUseEither()
    {
        (GameWorld world, ServerPlayer player) = Hurt();

        BattleUpdate update = Use(world, player, TestRules.PotionItem);

        Assert.Empty(update.Events.OfType<BattleEvent.HealthRestored>());
    }

    [Fact]
    public void ABattleIsToldWhatMedicineIsCarried()
    {
        (GameWorld world, ServerPlayer player) = Hurt();

        player.Bag.Add(TestRules.PotionItem, 1);

        BattleUpdate update = Use(world, player, TestRules.PotionItem);

        Assert.DoesNotContain(update.Medicine, m => m.ItemId == TestRules.BallItem);
    }
}

/// <summary>
/// Drinking something out of a fight, which until now was the half of a potion that
/// did not exist: they could be bought, carried and sold, and the only healing in the
/// game was losing.
/// </summary>
public class UsingItemsOutOfBattleTests
{
    private const string Town = "3.0";

    private static GameWorld World() =>
        new(new WorldData([new MapData(Town, "PALLET TOWN", 8, 8, new byte[64])]), Town, TestRules.All);

    private static (GameWorld World, ServerPlayer Player) Hurt(int potions = 1)
    {
        GameWorld world = World();

        (ServerPlayer player, _) = world.Join(
            1,
            "Koop",
            SavedCharacter.Fresh(Town, 1, 1) with
            {
                // One health out of whatever thirty levels of this comes to, so there is
                // plenty of room for twenty to go back on.
                Party = [new SavedMon(3, 30, null, 1, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])],
                Items = potions > 0 ? [new BagEntry(TestRules.PotionItem, potions)] : [],
            });

        return (world, player);
    }

    private static BagUpdated Use(GameWorld world, ServerPlayer player, int itemId, int slot) =>
        world.UseItem(player.Id, itemId, slot).Select(o => o.Message).OfType<BagUpdated>().Single();

    [Fact]
    public void APotionPutsHealthBackOnAndIsSpent()
    {
        (GameWorld world, ServerPlayer player) = Hurt();

        int before = player.Party[0].CurrentHp;

        BagUpdated update = Use(world, player, TestRules.PotionItem, 0);

        Assert.True(update.Party[0].CurrentHp > before);
        Assert.Empty(update.Bag);
    }

    [Fact]
    public void SomebodyAlreadyWellIsNotChargedForIt()
    {
        // Out of a fight there is no turn being used up, so a potion that would do
        // nothing costs nothing. In a battle the same press costs the turn, which is
        // the difference between the two and the reason this is not shared code.
        (GameWorld world, ServerPlayer player) = Hurt();

        player.Bag.Add(TestRules.FullPotionItem, 1);

        Use(world, player, TestRules.FullPotionItem, 0);

        int held = player.Bag.CountOf(TestRules.PotionItem);

        BagUpdated again = Use(world, player, TestRules.PotionItem, 0);

        Assert.Equal(held, player.Bag.CountOf(TestRules.PotionItem));
        Assert.Contains("effect", again.Message);
    }

    [Fact]
    public void SomethingNotCarriedDoesNothing()
    {
        // The request is an id and a slot. Everything else — that it is a thing, that
        // it restores anything, that it is actually in the bag — is checked here,
        // because a client sends whatever it likes.
        (GameWorld world, ServerPlayer player) = Hurt(potions: 0);

        Assert.Empty(world.UseItem(player.Id, TestRules.PotionItem, 0));
    }

    [Fact]
    public void ASlotNobodyIsStandingInDoesNothing()
    {
        (GameWorld world, ServerPlayer player) = Hurt();

        Assert.Empty(world.UseItem(player.Id, TestRules.PotionItem, 4));
        Assert.Empty(world.UseItem(player.Id, TestRules.PotionItem, -1));
    }
}
