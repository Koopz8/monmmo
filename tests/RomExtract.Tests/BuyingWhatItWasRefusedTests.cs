using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The last link, and the only one that was ever actually about money.
/// <para>
/// The chain took five measurements to lay out and every one of them moved the wall: the
/// guard on <c>10.5</c> wants a drink, the only shelf in FireRed selling one is on
/// <c>3.13</c>, <c>3.13</c> is across the water, and the boat asks for nothing. With the
/// ferry ridden the shopping list finally reads "1 of them on ground it reached" — the
/// playthrough is standing in the shop, and it has never been able to buy anything.
/// </para>
/// <para>
/// <b>What it buys is deliberately not a policy.</b> It buys what it has been refused, and
/// nothing else. A shopping policy is a second thing to keep correct, and what is being
/// measured is whether the story can be finished rather than whether a shopper is sensible.
/// </para>
/// <para>
/// The money is <b>modelled</b> and handed in from outside. The prices are <b>read</b>.
/// </para>
/// </summary>
public class BuyingWhatItWasRefusedTests
{
    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// A guard who wants something, and a counter selling it two squares away.
    /// </summary>
    private static WorldData Shop(int sells) =>
        new(
        [
            Room("1.0") with
            {
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                    new MapObject(2, 1, 2, 1, Direction.Down, 0, false, Sells: [sells]),
                ],
            },
        ]);

    private static Attempt Run(WorldData world, int money, int wants) =>
        Autoplayer.Play(
            world,
            "1.0",
            TestRules.All,
            (_, _, bag) => Nothing with { Asked = [(wants, 1, bag.Has(wants))] },
            null,
            false,
            money);

    /// <summary>
    /// The whole thing in one test: refused at a door, sold on a shelf, and enough to pay.
    /// </summary>
    [Fact]
    public void SomethingItWasRefusedAndCanAffordIsBought()
    {
        Attempt played = Run(Shop(TestRules.PotionItem), 9999, TestRules.PotionItem);

        Bought buy = Assert.Single(played.Bought);

        Assert.Equal(TestRules.PotionItem, buy.ItemId);
        Assert.Equal("1.0", buy.MapId);
        Assert.Contains(played.Carried, e => e.ItemId == TestRules.PotionItem);

        // And the shopping list empties, which is the measurement rather than the purchase.
        Assert.DoesNotContain(played.Refused, w => w.ItemId == TestRules.PotionItem);
    }

    /// <summary>
    /// With nothing to spend it buys nothing, which is where this instrument has been for its
    /// whole life and is still the default.
    /// </summary>
    [Fact]
    public void WithNothingToSpendItBuysNothing()
    {
        Attempt played = Run(Shop(TestRules.PotionItem), 0, TestRules.PotionItem);

        Assert.Empty(played.Bought);
        Assert.Contains(played.Refused, w => w.ItemId == TestRules.PotionItem);
    }

    /// <summary>
    /// And it does not buy a shop out. Every other thing on that shelf was never asked for by
    /// anybody, and a run that swept the shelves would answer "can the story be finished" with
    /// "yes, if you buy everything", which is not an answer.
    /// </summary>
    [Fact]
    public void ItBuysOnlyWhatSomebodyAskedItFor()
    {
        var world = new WorldData(
        [
            Room("1.0") with
            {
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                    new MapObject(2, 1, 2, 1, Direction.Down, 0, false,
                        Sells: [TestRules.PotionItem, TestRules.AntidoteItem, TestRules.BallItem]),
                ],
            },
        ]);

        Attempt played = Run(world, 9999, TestRules.PotionItem);

        Assert.Equal(TestRules.PotionItem, Assert.Single(played.Bought).ItemId);
    }

    /// <summary>
    /// A shelf it cannot stand in front of is not a shop. The whole thread this came out of
    /// was a shelf on the far side of the sea, and a run that could buy from it without going
    /// there would have reported Saffron open five measurements ago and been wrong.
    /// </summary>
    [Fact]
    public void AShelfItCannotStandInFrontOfIsNotAShop()
    {
        var world = new WorldData(
        [
            Room("1.0") with
            {
                Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],
            },

            // Joined to nothing at all.
            Room("9.9") with
            {
                Objects = [new MapObject(1, 1, 2, 1, Direction.Down, 0, false, Sells: [TestRules.PotionItem])],
            },
        ]);

        Attempt played = Run(world, 9999, TestRules.PotionItem);

        Assert.Empty(played.Bought);
    }

    /// <summary>
    /// And a counter walled off on a map it <em>is</em> standing on is not a shop either.
    /// <para>
    /// The decoy for the rule above, and it was needed: the map in that test is never reached
    /// at all, so the reach filter catches it before the standing check is ever consulted.
    /// Nothing could fail the rule that actually matters. A shop across a wall on a map you
    /// are standing on is the case the cartridge is full of — every counter in this game is
    /// behind something — and it is the one the rule is for.
    /// </para>
    /// </summary>
    [Fact]
    public void AndACounterWalledOffOnAMapItIsStandingOnIsNotOneEither()
    {
        // A solid column down x = 2, so the right-hand side of the room cannot be walked to.
        var collision = new byte[16];

        for (var y = 0; y < 4; y++) collision[y * 4 + 2] = 1;

        var world = new WorldData(
        [
            new MapData("1.0", "1.0", 4, 4, collision)
            {
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },

                    // On the far side of the wall, on a map the walk is standing on.
                    new MapObject(2, 1, 3, 1, Direction.Down, 0, false, Sells: [TestRules.PotionItem]),
                ],
            },
        ]);

        Attempt played = Run(world, 9999, TestRules.PotionItem);

        Assert.Contains("1.0", played.Reached);
        Assert.Empty(played.Bought);
    }

    /// <summary>
    /// And a counter a flag has taken off the map is not one at all.
    /// <para>
    /// The same rule everything else on a map already gets, and it needed its own decoy: with
    /// no hidden shopkeeper in the fixture, nothing could fail it. Several counters in this
    /// game come and go with the story — the shop that only opens once somebody has been
    /// dealt with — and buying from one before it exists would put an item in the bag that
    /// the save cannot account for.
    /// </para>
    /// </summary>
    [Fact]
    public void ACounterAFlagHasTakenOffTheMapIsNotOne()
    {
        const int shut = 0x0900;

        var world = new WorldData(
        [
            Room("1.0") with
            {
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                    new MapObject(2, 1, 2, 1, Direction.Down, 0, false, Sells: [TestRules.PotionItem])
                        with { HiddenBy = shut },
                ],
            },
        ])
        {
            // A fresh save is not an empty save, and this is one of the flags it starts with.
            FlagsAtStart = [shut],
        };

        Assert.Empty(Run(world, 9999, TestRules.PotionItem).Bought);
    }

    /// <summary>
    /// And it cannot spend what it has not got. The price is read off the cartridge's own item
    /// record; the purse is the one modelled number in the whole arrangement.
    /// </summary>
    [Fact]
    public void ItCannotSpendWhatItHasNotGot()
    {
        int price = TestRules.All.ItemAt(TestRules.PotionItem)!.Price;

        Assert.True(price > 1, "the fixture's price has to be worth being short of");

        Attempt played = Run(Shop(TestRules.PotionItem), price - 1, TestRules.PotionItem);

        Assert.Empty(played.Bought);

        // And having exactly enough is enough.
        Assert.Single(Run(Shop(TestRules.PotionItem), price, TestRules.PotionItem).Bought);
    }

    /// <summary>
    /// A key item on a shelf is a listing rather than a purchase, and so is anything priced at
    /// nothing. Both are the cartridge's own answer — <c>CanBeBought</c> reads the record.
    /// </summary>
    [Fact]
    public void SomethingTheCartridgeDoesNotSellIsNotBought()
    {
        Attempt played = Run(Shop(TestRules.BicycleItem), 9999, TestRules.BicycleItem);

        Assert.Empty(played.Bought);
    }

    // ---- the cap that was named for one thing and counted across another ----------------

    /// <summary>
    /// A full pocket does not shut the whole bag, and this is the fault that made the first
    /// real run of the shopping buy nothing at all.
    /// <para>
    /// <c>PocketCapacity</c> is named for a pocket and was counted across every item carried.
    /// The playthrough was holding exactly sixty different things — the cap — so every
    /// purchase and every ball on the floor was refused from then on, silently, and the
    /// output read as a shop that sold nothing rather than a bag with no room. A limit
    /// described as one thing and applied as another.
    /// </para>
    /// </summary>
    [Fact]
    public void AFullPocketDoesNotShutTheWholeBag()
    {
        var bag = new Bag();

        // A pocket filled to its cap, all of them sharing one pocket.
        for (var i = 0; i < Bag.PocketCapacity; i++) bag.Add(9000 + i);

        Assert.Equal(Bag.PocketCapacity, bag.DistinctItems);

        // Counted across the whole bag, nothing else fits — which is what used to happen.
        Assert.Equal(0, bag.Add(TestRules.PotionItem));

        // Counted by pocket, something from a different one still does. The predicate says
        // which of the things already carried share a pocket with the one going in, and none
        // of the sixty fillers share this one's.
        Assert.Equal(
            1,
            bag.Add(TestRules.PotionItem, 1, Bag.MaxStack, other => other < 9000));
    }

    /// <summary>
    /// And a pocket that <em>is</em> full still refuses, which is the decoy: a capacity that
    /// stopped applying at all would be no better than one that applied everywhere.
    /// </summary>
    [Fact]
    public void AndAPocketThatIsFullStillRefuses()
    {
        var bag = new Bag();

        for (var i = 0; i < Bag.PocketCapacity; i++) bag.Add(9000 + i);

        // The same pocket as everything already in it.
        Assert.Equal(0, bag.Add(8999, 1, Bag.MaxStack, other => other >= 9000 || other == 8999));
    }

    /// <summary>
    /// And something already carried still stacks when its pocket is full, because a stack is
    /// not a new slot. Otherwise a full pocket would stop a player topping up potions.
    /// </summary>
    [Fact]
    public void SomethingAlreadyCarriedStillStacksInAFullPocket()
    {
        var bag = new Bag();

        for (var i = 0; i < Bag.PocketCapacity; i++) bag.Add(9000 + i);

        Assert.Equal(1, bag.Add(9000, 1, Bag.MaxStack, other => other >= 9000));
        Assert.Equal(2, bag.CountOf(9000));
    }

    /// <summary>
    /// And the playthrough gets the same answer, which needed its own decoy.
    /// <para>
    /// The bag tests above prove the predicate works; nothing proved the playthrough asks the
    /// right question with it. A version that said "everything shares my pocket" passed every
    /// one of them and reproduced the original fault exactly — sixty things carried, and the
    /// shop it is standing in front of sells it nothing.
    /// </para>
    /// <para>
    /// So: sixty things on the floor whose ids the rules have never heard of, and then a
    /// POTION on a shelf. Nothing off that floor shares a pocket with a potion.
    /// </para>
    /// </summary>
    [Fact]
    public void AFullPocketOfOneKindDoesNotStopItBuyingFromAnother()
    {
        const int filler = 9000;
        const uint floor = 0x2000;

        List<MapObject> objects =
        [
            new MapObject(1, 1, 0, 0, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
            new MapObject(2, 1, 1, 0, Direction.Down, 0, false, Sells: [TestRules.PotionItem]),
        ];

        for (var i = 0; i < Bag.PocketCapacity; i++)
        {
            objects.Add(
                new MapObject(10 + i, 1, 2 + i % 12, 1 + i / 12, Direction.Down, 0, false)
                    {
                        ScriptAddress = floor + (uint)i,
                    }
                    with { GivesItemId = filler + i, GivesCount = 1, HiddenBy = 0x400 + i });
        }

        var world = new WorldData(
            [new MapData("1.0", "1.0", 16, 16, new byte[256]) { Objects = objects }]);

        Attempt played = Autoplayer.Play(
            world,
            "1.0",
            TestRules.All,
            (address, _, bag) => address >= floor
                ? Nothing with { Gets = (filler + (int)(address - floor), 1) }
                : Nothing with { Asked = [(TestRules.PotionItem, 1, bag.Has(TestRules.PotionItem))] },
            null,
            false,
            9999);

        Assert.Equal(Bag.PocketCapacity + 1, played.Carried.Count);
        Assert.Equal(TestRules.PotionItem, Assert.Single(played.Bought).ItemId);
    }

    // ---- and why it did not, when it did not --------------------------------------------

    /// <summary>
    /// Not being able to afford one says so, by name and by number. Four things stop a
    /// purchase and they are not alike; the run that found this hit the one nobody would
    /// have guessed and it read as this one.
    /// </summary>
    [Fact]
    public void NotBeingAbleToAffordOneSaysSo()
    {
        int price = TestRules.All.ItemAt(TestRules.PotionItem)!.Price;

        Attempt played = Run(Shop(TestRules.PotionItem), price - 1, TestRules.PotionItem);

        NotBought missed = Assert.Single(played.CouldNotBuy);

        Assert.Equal(TestRules.PotionItem, missed.ItemId);
        Assert.Contains("afford", missed.Why);
    }

    /// <summary>And something the cartridge does not sell says that instead.</summary>
    [Fact]
    public void SomethingTheCartridgeWillNotSellSaysThatInstead()
    {
        Attempt played = Run(Shop(TestRules.BicycleItem), 9999, TestRules.BicycleItem);

        Assert.Contains("does not sell", Assert.Single(played.CouldNotBuy).Why);
    }

    /// <summary>
    /// And a purchase that went through leaves nothing on the list. A reason recorded on the
    /// pass it failed and never cleared would report every bought item as unbought.
    /// </summary>
    [Fact]
    public void SomethingItDidBuyIsNotAlsoReportedAsMissed()
    {
        Assert.Empty(Run(Shop(TestRules.PotionItem), 9999, TestRules.PotionItem).CouldNotBuy);
    }

    /// <summary>And what it spent is subtracted, so a purse cannot buy the same thing forever.</summary>
    [Fact]
    public void WhatItSpentComesOffThePurse()
    {
        int price = TestRules.All.ItemAt(TestRules.PotionItem)!.Price;

        Attempt played = Run(Shop(TestRules.PotionItem), price + 5, TestRules.PotionItem);

        Assert.Equal(5, played.MoneyLeft);
    }
}
