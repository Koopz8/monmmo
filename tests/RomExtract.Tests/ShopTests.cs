using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Finding out what somebody sells.
/// <para>
/// Nothing marks a shopkeeper. There is no flag on the object and no field anywhere —
/// the only thing that distinguishes one is a <c>pokemart</c> command inside their
/// script, which is why this had to wait for a script reader too.
/// </para>
/// </summary>
public class ShopExtractionTests
{
    private static readonly SyntheticRom Fixture = new();

    [Fact]
    public void AShopkeepersStockIsReadOffTheirScript()
    {
        List<int> stock = ScriptReader.FindMart(
            Fixture.ToRom(), SyntheticRom.ScriptAddressFor(2, SyntheticRom.ShopObjectSlot));

        Assert.Equal(SyntheticRom.StockFor(2), stock);
    }

    [Fact]
    public void TheListEndsAtItsTerminatorAndNotAtACount()
    {
        // There is no count. A reader that guessed a length would sell whatever bytes
        // came after the shop, at whatever price those ids happened to name.
        List<int> stock = ScriptReader.FindMart(
            Fixture.ToRom(), SyntheticRom.ScriptAddressFor(5, SyntheticRom.ShopObjectSlot));

        Assert.Equal(3, stock.Count);
        Assert.DoesNotContain(0, stock);
    }

    [Fact]
    public void SomebodyWhoOnlyTalksSellsNothing()
    {
        Assert.Empty(ScriptReader.FindMart(Fixture.ToRom(), SyntheticRom.ScriptAddressFor(2, 0)));
    }

    [Fact]
    public void AShopkeeperStillSaysWhatTheySay()
    {
        // The mart command comes first and the dialogue after it. A reader that stopped
        // at the shop would lose the line, and one that never got past it would lose
        // everything in every script that opens a shop.
        List<string> pages = ScriptReader.ReadDialogue(
            Fixture.ToRom(), SyntheticRom.ScriptAddressFor(2, SyntheticRom.ShopObjectSlot));

        Assert.Equal(SyntheticRom.DialogueFor(2, 3), pages);
    }

    [Fact]
    public void StockSurvivesExtractionOntoTheObject()
    {
        Rom rom = Fixture.ToRom();

        MapBankTable banks = MapBankLocator.Locate(rom)!;

        (int _, int _, MapHeaderRecord header) = banks.AllMaps.First(m => m.Bank == 0 && m.Map == 2);

        List<MapObject> objects = MapLinkExtractor.ReadObjects(
            rom, header, SyntheticRom.MapWidth, SyntheticRom.MapHeight);

        MapObject keeper = objects.Single(o => o.IsShopkeeper);

        Assert.Equal(SyntheticRom.StockFor(2), keeper.Stock);
    }

    [Fact]
    public void StockSurvivesTheWorldFile()
    {
        // Item ids are numbers. The list itself lived at a cartridge address, and that
        // address stays where it was — same rule the script addresses follow.
        var before = new MapData("3.0", "PALLET TOWN", 4, 4, new byte[16])
        {
            Objects = [new MapObject(1, 5, 1, 1, Direction.Down, 0, false, 0, 0, 0x08123456, 0, 0, [4, 13, 20])],
        };

        using var buffer = new MemoryStream();
        new WorldData([before]).Save(buffer);
        buffer.Position = 0;

        MapData after = WorldData.Load(buffer).Maps.Single();

        Assert.Equal([4, 13, 20], after.Objects.Single().Stock);
        Assert.True(after.Objects.Single().IsShopkeeper);
        Assert.False(after.Objects.Single().HasScript);
    }
}

/// <summary>
/// Buying and selling, which the server decides entirely.
/// </summary>
public class ShopTests
{
    private const string Town = "3.0";

    private static GameWorld World(params int[] stock)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64])
        {
            Objects = [new MapObject(1, 5, 4, 3, Direction.Up, 0, false, 0, 0, 0, 0, 0, stock)],
        };

        return new GameWorld(new WorldData([map]), Town, TestRules.All);
    }

    /// <summary>A player standing in front of the counter, with the shop already open.</summary>
    private static (GameWorld World, ServerPlayer Player, ShopOpened Shop) AtTheCounter(params int[] stock)
    {
        GameWorld world = World(stock);

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        player.Square = new GridPosition(4, 2);
        player.Facing = Direction.Down;

        ShopOpened opened = world.StartTalking(player.Id, 1)
            .Select(o => o.Message)
            .OfType<ShopOpened>()
            .Single();

        return (world, player, opened);
    }

    private static ShopUpdated Buy(GameWorld world, ServerPlayer player, int itemId, int count) =>
        world.Buy(player.Id, itemId, count).Select(o => o.Message).OfType<ShopUpdated>().Single();

    private static ShopUpdated Sell(GameWorld world, ServerPlayer player, int itemId, int count) =>
        world.Sell(player.Id, itemId, count).Select(o => o.Message).OfType<ShopUpdated>().Single();

    [Fact]
    public void TalkingToAShopkeeperOpensTheirShop()
    {
        (_, _, ShopOpened shop) = AtTheCounter(TestRules.BallItem, TestRules.PotionItem);

        Assert.Equal([TestRules.BallItem, TestRules.PotionItem], shop.Stock.Select(s => s.ItemId));
    }

    [Fact]
    public void ThePricesComeFromTheRulesAndNotFromTheShop()
    {
        // A cartridge shop is a list of ids and nothing else. What each one costs is a
        // property of the item, which is why a ball is the same price in every town.
        (_, _, ShopOpened shop) = AtTheCounter(TestRules.BallItem);

        Assert.Equal(TestRules.All.ItemAt(TestRules.BallItem)!.Price, shop.Stock.Single().Price);
    }

    [Fact]
    public void BuyingTakesTheMoneyAndHandsOverTheGoods()
    {
        (GameWorld world, ServerPlayer player, _) = AtTheCounter(TestRules.BallItem);

        int price = TestRules.All.ItemAt(TestRules.BallItem)!.Price;
        int before = player.Money;
        int held = player.Bag.CountOf(TestRules.BallItem);

        ShopUpdated update = Buy(world, player, TestRules.BallItem, 3);

        Assert.Equal(before - price * 3, update.Money);
        Assert.Equal(held + 3, player.Bag.CountOf(TestRules.BallItem));
    }

    [Fact]
    public void AskingForMoreThanYouCanAffordBuysWhatYouCan()
    {
        // Which is what a shop does. Refusing the whole order because it is one over is
        // a rule nobody expects and a menu nobody can navigate.
        (GameWorld world, ServerPlayer player, _) = AtTheCounter(TestRules.BallItem);

        int price = TestRules.All.ItemAt(TestRules.BallItem)!.Price;
        player.Money = price * 2;

        Buy(world, player, TestRules.BallItem, 99);

        Assert.Equal(0, player.Money);
    }

    [Fact]
    public void BuyingWithNoMoneyBuysNothing()
    {
        (GameWorld world, ServerPlayer player, _) = AtTheCounter(TestRules.BallItem);

        player.Money = 0;

        int held = player.Bag.CountOf(TestRules.BallItem);

        Assert.Contains("money", Buy(world, player, TestRules.BallItem, 1).Message);
        Assert.Equal(held, player.Bag.CountOf(TestRules.BallItem));
    }

    [Fact]
    public void NothingIsChargedForWhatTheBagWouldNotTake()
    {
        // Adding first and charging after is deliberate. The bag is the thing that might
        // refuse, and being charged for items that never went in is the one failure here
        // that actually costs a player something.
        (GameWorld world, ServerPlayer player, _) = AtTheCounter(TestRules.BallItem);

        player.Bag.Add(TestRules.BallItem, Bag.MaxStack);

        int before = player.Money;

        Assert.Contains("carry", Buy(world, player, TestRules.BallItem, 5).Message);
        Assert.Equal(before, player.Money);
    }

    [Fact]
    public void OnlyPartOfAnOrderThatFitsIsPaidFor()
    {
        (GameWorld world, ServerPlayer player, _) = AtTheCounter(TestRules.BallItem);

        int price = TestRules.All.ItemAt(TestRules.BallItem)!.Price;

        player.Bag.Add(TestRules.BallItem, Bag.MaxStack - 2);

        int held = player.Bag.CountOf(TestRules.BallItem);
        int before = player.Money;

        Buy(world, player, TestRules.BallItem, 10);

        Assert.Equal(Bag.MaxStack, player.Bag.CountOf(TestRules.BallItem));
        Assert.Equal(before - price * (Bag.MaxStack - held), player.Money);
    }

    [Fact]
    public void AShopWillNotSellYouWhatItDoesNotStock()
    {
        // The list is the server's. A client naming any id it likes gets told no.
        (GameWorld world, ServerPlayer player, _) = AtTheCounter(TestRules.BallItem);

        int before = player.Money;

        Assert.Contains("don't sell", Buy(world, player, TestRules.PotionItem, 1).Message);
        Assert.Equal(0, player.Bag.CountOf(TestRules.PotionItem));
        Assert.Equal(before, player.Money);
    }

    [Fact]
    public void BuyingWithNoShopOpenBuysNothing()
    {
        GameWorld world = World(TestRules.BallItem);

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        int before = player.Money;

        world.Buy(player.Id, TestRules.BallItem, 1);

        Assert.Equal(before, player.Money);
    }

    [Fact]
    public void WalkingAwayShutsTheShop()
    {
        // Held rather than looked up per purchase, so a player who leaves the counter
        // cannot keep buying from wherever they now stand.
        (GameWorld world, ServerPlayer player, _) = AtTheCounter(TestRules.BallItem);

        world.StopTalking(player.Id);

        int before = player.Money;

        world.Buy(player.Id, TestRules.BallItem, 1);

        Assert.Equal(before, player.Money);
    }

    [Fact]
    public void SellingPaysHalfAndTakesTheItem()
    {
        (GameWorld world, ServerPlayer player, _) = AtTheCounter(TestRules.BallItem);

        player.Bag.Add(TestRules.PotionItem, 4);

        int half = TestRules.All.ItemAt(TestRules.PotionItem)!.SellPrice;
        int before = player.Money;

        ShopUpdated update = Sell(world, player, TestRules.PotionItem, 3);

        Assert.Equal(before + half * 3, update.Money);
        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
    }

    [Fact]
    public void SellingSomethingYouDoNotHavePaysNothing()
    {
        (GameWorld world, ServerPlayer player, _) = AtTheCounter(TestRules.BallItem);

        int before = player.Money;

        Assert.Contains("don't have", Sell(world, player, TestRules.PotionItem, 5).Message);
        Assert.Equal(before, player.Money);
    }

    [Fact]
    public void YouCanSellSomethingAShopDoesNotStock()
    {
        // A shop that would only buy back its own stock is not how any of them work,
        // and it is a rule that would leave a player unable to get rid of anything.
        (GameWorld world, ServerPlayer player, _) = AtTheCounter(TestRules.BallItem);

        player.Bag.Add(TestRules.PotionItem, 1);

        int before = player.Money;

        Sell(world, player, TestRules.PotionItem, 1);

        Assert.True(player.Money > before);
    }

    [Fact]
    public void MoneyStopsAtTheCeilingRatherThanWrappingRound()
    {
        (GameWorld world, ServerPlayer player, _) = AtTheCounter(TestRules.BallItem);

        player.Money = GameWorld.MaxMoney;
        player.Bag.Add(TestRules.PotionItem, 10);

        Sell(world, player, TestRules.PotionItem, 10);

        Assert.Equal(GameWorld.MaxMoney, player.Money);
    }
}
