using PokeMmo.Core.Cosmetics;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The clothes counter, which is the last piece of a feature that had been three quarters
/// built for fifteen milestones.
/// <para>
/// Every cosmetic already had a price, art drawn on the figure facing the way it faces, a
/// mirror to look in, and a server that decided what an account owned and refused what it
/// did not. What was missing was money — the only thing that makes any of it a choice.
/// </para>
/// <para>
/// It is a second counter in the Poké Marts rather than a shop of its own, because every
/// Mart already has a shopkeeper the world knows about. No new map object, no script this
/// cartridge does not have, and it works in all twenty of them at once.
/// </para>
/// </summary>
public class ClothesCounterTests
{
    private const string Town = "1.0";

    /// <summary>A shopkeeper, which is what a clothes counter is bolted to.</summary>
    private static (GameWorld World, ServerPlayer Player) AtTheCounter(int money = 100_000)
    {
        MapObject keeper = new(1, 5, 3, 3, Direction.Down, 0, false, Sells: [TestRules.PotionItem])
        {
            Talks = true,
        };

        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = [keeper] };

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4) with
        {
            Facing = Direction.Up,
        });

        player.Money = money;

        return (world, player);
    }

    private static ShopOpened Opened(GameWorld world, ServerPlayer player)
    {
        player.Facing = Direction.Up;

        return Assert.Single(
            world.StartTalking(player.Id, 1).Select(o => o.Message).OfType<ShopOpened>());
    }

    /// <summary>Something the wardrobe has that nobody starts with.</summary>
    private static Cosmetic ForSale =>
        Wardrobe.All.First(c => !Wardrobe.FreeToEverybody.Contains(c.Id) && c.Price > 0);

    [Fact]
    public void TalkingToAShopkeeperShowsTheClothesToo()
    {
        (GameWorld world, ServerPlayer player) = AtTheCounter();

        ShopOpened opened = Opened(world, player);

        Assert.NotEmpty(opened.Stock);
        Assert.NotEmpty(opened.Clothes);

        // Priced by the wardrobe rather than by anything on this counter.
        ShopEntry entry = opened.Clothes.First(c => c.ItemId == ForSale.Id);

        Assert.Equal(Wardrobe.PriceOf(ForSale.Id), entry.Price);
    }

    /// <summary>
    /// And it stocks what you have not got. A counter offering something already owned is
    /// a counter whose every third row is a refusal waiting to happen.
    /// </summary>
    [Fact]
    public void AndItDoesNotOfferWhatYouAlreadyOwn()
    {
        (GameWorld world, ServerPlayer player) = AtTheCounter();

        Assert.Contains(ForSale.Id, Opened(world, player).Clothes.Select(c => c.ItemId));

        world.BuyCosmetic(player.Id, ForSale.Id);
        world.StopTalking(player.Id);

        Assert.DoesNotContain(ForSale.Id, Opened(world, player).Clothes.Select(c => c.ItemId));

        // And the free ones are never on it, because nobody buys what everybody has.
        Assert.DoesNotContain(
            Wardrobe.FreeToEverybody[0], Opened(world, player).Clothes.Select(c => c.ItemId));
    }

    [Fact]
    public void BuyingOneTakesTheMoneyAndHandsItOver()
    {
        (GameWorld world, ServerPlayer player) = AtTheCounter();

        Opened(world, player);

        int before = player.Money;

        List<Outgoing> done = world.BuyCosmetic(player.Id, ForSale.Id);

        Assert.Contains(ForSale.Id, player.Owns);
        Assert.Equal(before - ForSale.Price, player.Money);

        // Two answers: the money, and what is now owned. The second is the one nothing on
        // the wire could say until this milestone.
        ShopUpdated paid = Assert.Single(done.Select(o => o.Message).OfType<ShopUpdated>());
        CosmeticsOwned owns = Assert.Single(done.Select(o => o.Message).OfType<CosmeticsOwned>());

        Assert.Equal(player.Money, paid.Money);
        Assert.Contains(ForSale.Id, owns.Owned);
    }

    [Fact]
    public void NobodyBuysWhatTheyCannotAfford()
    {
        (GameWorld world, ServerPlayer player) = AtTheCounter(money: 0);

        Opened(world, player);

        List<Outgoing> refused = world.BuyCosmetic(player.Id, ForSale.Id);

        Assert.DoesNotContain(ForSale.Id, player.Owns);
        Assert.Equal(0, player.Money);

        // What the player is told, rather than what the log says. The refusal is the
        // product; the log line is a note to whoever is watching.
        Assert.Contains(
            "cannot afford",
            Assert.Single(refused.Select(o => o.Message).OfType<ShopUpdated>()).Message);

        Assert.Contains("refused", world.LastBought ?? "");
    }

    [Fact]
    public void AndNobodyBuysTheSameThingTwice()
    {
        (GameWorld world, ServerPlayer player) = AtTheCounter();

        Opened(world, player);

        world.BuyCosmetic(player.Id, ForSale.Id);

        int after = player.Money;

        world.BuyCosmetic(player.Id, ForSale.Id);

        Assert.Equal(after, player.Money);
        Assert.Contains("already owns", world.LastBought ?? "");
    }

    /// <summary>And nothing the wardrobe has never heard of is sold at any price.</summary>
    [Fact]
    public void AndThereIsNoSuchThingAsCosmeticNineThousand()
    {
        (GameWorld world, ServerPlayer player) = AtTheCounter();

        Opened(world, player);

        int before = player.Money;

        world.BuyCosmetic(player.Id, 9_000);

        Assert.Equal(before, player.Money);
        Assert.Contains("no cosmetic", world.LastBought ?? "");
    }

    /// <summary>
    /// And not in the middle of a field. The same gate the items have, which a player who
    /// walked away mid-shop would otherwise be shopping through from wherever they now
    /// stand.
    /// </summary>
    [Fact]
    public void AndNotInTheMiddleOfAField()
    {
        MapData bare = new(Town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([bare]), Town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));

        player.Money = 100_000;

        world.BuyCosmetic(player.Id, ForSale.Id);

        Assert.DoesNotContain(ForSale.Id, player.Owns);
        Assert.Contains("no counter", world.LastBought ?? "");
    }

    /// <summary>
    /// The gap this milestone found rather than made: an operator's grant changed what an
    /// account owned and told nobody. It only ever reached a client in the Welcome, so a
    /// granted hat appeared at the next login and not before — invisible for as long as it
    /// existed, because until there was a counter selling them the wardrobe was only ever
    /// opened after a login.
    /// </summary>
    [Fact]
    public void AndGrantingOneSaysSoNow()
    {
        (GameWorld world, ServerPlayer player) = AtTheCounter();

        world.Operators.Add(player.Name);

        List<Outgoing> granted = world.RunConsole(player.Id, $"/grant {ForSale.Id}");

        CosmeticsOwned owns = Assert.Single(granted.Select(o => o.Message).OfType<CosmeticsOwned>());

        Assert.Contains(ForSale.Id, owns.Owned);

        // And granting it again says nothing new, because nothing changed.
        Assert.Empty(
            world.RunConsole(player.Id, $"/grant {ForSale.Id}").Select(o => o.Message).OfType<CosmeticsOwned>());
    }

    /// <summary>
    /// The highest item id on a real FireRed, measured off the cartridge rather than
    /// remembered.
    /// </summary>
    private const int HighestRealItemId = 374;

    /// <summary>
    /// A cosmetic id and an item id are two numberings that happen to both be integers,
    /// and on a real cartridge they <em>collide</em>: seven of the twenty-two cosmetics
    /// share an id with a real item, and 103 is both RED HAIR and item 103.
    /// <para>
    /// That is the whole reason this counter must never look one up in the other's table.
    /// If the two sets were disjoint a mix-up would give a missing name, which anybody
    /// would notice. Because they overlap it gives a <em>wrong</em> name, which is the kind
    /// of bug that survives a review and ships.
    /// </para>
    /// <para>
    /// Counted rather than assumed — the first draft of this test asserted the overlap
    /// against the hand-built test rules, which have only a handful of items and no
    /// collision at all, and it failed. The number above came from the real image.
    /// </para>
    /// </summary>
    [Fact]
    public void TheTwoNumberingsCollideWhichIsWhyTheyAreKeptApart()
    {
        Assert.Contains(Wardrobe.All, c => c.Id <= HighestRealItemId);

        (GameWorld world, ServerPlayer player) = AtTheCounter();

        ShopOpened opened = Opened(world, player);

        // Everything on the clothes half is a cosmetic, priced by the wardrobe. Neither is
        // true of anything on the item half, and nothing here consults both.
        Assert.All(opened.Clothes, c => Assert.NotNull(Wardrobe.At(c.ItemId)));
        Assert.All(opened.Clothes, c => Assert.Equal(Wardrobe.PriceOf(c.ItemId), c.Price));

        Assert.All(opened.Stock, s => Assert.Null(Wardrobe.At(s.ItemId)));
    }
}
