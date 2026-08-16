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

    /// <summary>And what it spent is subtracted, so a purse cannot buy the same thing forever.</summary>
    [Fact]
    public void WhatItSpentComesOffThePurse()
    {
        int price = TestRules.All.ItemAt(TestRules.PotionItem)!.Price;

        Attempt played = Run(Shop(TestRules.PotionItem), price + 5, TestRules.PotionItem);

        Assert.Equal(5, played.MoneyLeft);
    }
}
