using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Items on the market, which is a different thing from creatures on it and not a smaller
/// one.
/// <para>
/// A creature is a row. Escrowing it means moving that row somewhere nobody owns, and every
/// test about it is really asking whether the row is in exactly one place. A number of
/// POTIONs is not a row — it is a count in somebody's bag — so escrowing it means writing
/// one number down and another up, and every test here is really asking whether those two
/// numbers still add to what they added to before.
/// </para>
/// <para>
/// That is the failure worth hunting: not a creature in two boxes, but five items that
/// became ten, or none.
/// </para>
/// </summary>
public class MarketItemTests
{
    private const string Town = "1.0";

    private const int Potion = TestRules.PotionItem;

    private const int Antidote = TestRules.AntidoteItem;

    private const int Bicycle = TestRules.BicycleItem;

    private static SavedMon Mon(int species, int level = 20) =>
        new(species, level, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
        {
            Sex = Gender.Female,
            Ivs = [31, 30, 29, 28, 27, 26],
        };

    private static SavedCharacter Character(params BagEntry[] carrying) =>
        new(Town, 3, 4, Direction.Down, [Mon(1)]) { Money = 5_000, Items = [.. carrying] };

    private static GameWorld World() =>
        new(new WorldData([new MapData(Town, "PALLET TOWN", 8, 8, new byte[64])]), Town, TestRules.All);

    private static async Task<(SqlitePlayerStore Store, long Id)> AccountAsync(
        string path, string name, SavedCharacter character)
    {
        var store = new SqlitePlayerStore(path);

        var made = Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync(name, "a-good-password", character));

        return (store, made.Account.Id);
    }

    private static async Task<(ServerPlayer Player, long AccountId)> ArriveAsync(
        SqlitePlayerStore store, GameWorld world, string name, SavedCharacter character)
    {
        var made = Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync(name, "a-good-password", character));

        (ServerPlayer player, _) = world.Join(made.Account.Id, name, character);

        world.Operators.Add(name);

        return (player, made.Account.Id);
    }

    private static string Said(IEnumerable<Outgoing> from) =>
        string.Join("\n", from.Select(o => o.Message).OfType<ConsoleReply>().Select(r => r.Text));

    private static Task<List<Outgoing>> RunAsync(
        Market market, GameWorld world, ServerPlayer player, long accountId, string text) =>
        market.RunAsync(world, player.Id, accountId, ConsoleLine.Of(text));

    /// <summary>How many of one item somebody is carrying, straight off the disk.</summary>
    private static async Task<int> CarriedAsync(SqlitePlayerStore store, string name)
    {
        var login = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync(name, "a-good-password"));

        return login.Character.Items.FirstOrDefault(e => e.ItemId == Potion)?.Count ?? 0;
    }

    // ---- the store -----------------------------------------------------------------

    [Fact]
    public async Task ListingAPileTakesThemOutOfTheBagAndPutsThemOnTheBoard()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter mason = Character(new BagEntry(Potion, 12));

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                await store.ListItemsAsync(
                    sellerId, mason with { Items = [new BagEntry(Potion, 7)] }, Potion, 5, 900);

                // The bag is short by exactly what went up, and by nothing else.
                Assert.Equal(7, await CarriedAsync(store, "Mason"));

                Listing shown = Assert.Single(await store.BrowseAsync());

                Assert.Equal("Mason", shown.Seller);
                Assert.True(shown.IsItem);
                Assert.Equal(Potion, shown.Item);
                Assert.Equal(5, shown.Count);
                Assert.Equal(900, shown.Price);

                // And it is not pretending to be a creature.
                Assert.Equal(0, shown.Species);
                Assert.False(shown.Sold);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// The count on the row is the only copy, so a restart is the whole question: five
    /// items that came back as ten would mean somewhere else was holding them too.
    /// </summary>
    [Fact]
    public async Task AndTheCountIsStillTheSameAfterARestart()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter mason = Character(new BagEntry(Potion, 12));

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                await store.ListItemsAsync(
                    sellerId, mason with { Items = [new BagEntry(Potion, 7)] }, Potion, 5, 900);
            }

            using var reopened = new SqlitePlayerStore(path);

            Listing shown = Assert.Single(await reopened.BrowseAsync());

            Assert.Equal(5, shown.Count);
            Assert.Equal(7, await CarriedAsync(reopened, "Mason"));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task BuyingAPileMovesTheItemsAndTheMoney()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter mason = Character(new BagEntry(Potion, 12));

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                long listingId = await store.ListItemsAsync(
                    sellerId, mason with { Items = [new BagEntry(Potion, 7)] }, Potion, 5, 900);

                var koop = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Koop", "a-good-password", Character(new BagEntry(Antidote, 1))));

                var bought = await store.BuyAsync(koop.Account.Id, listingId, koop.Character);

                Assert.NotNull(bought);
                Assert.True(bought!.Value.Bought.IsItem);
                Assert.Equal(Potion, bought.Value.Bought.Item);
                Assert.Equal(5, bought.Value.Bought.Count);
                Assert.Equal(900, bought.Value.Price);

                // The buyer, on disk: five more, and nine hundred less.
                var theirs = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Koop", "a-good-password"));

                Assert.Equal(5, theirs.Character.Items.Single(e => e.ItemId == Potion).Count);
                Assert.Equal(1, theirs.Character.Items.Single(e => e.ItemId == Antidote).Count);
                Assert.Equal(4_100, theirs.Character.Money);

                // The seller, who has not been paid yet and still has only what they kept.
                Assert.Equal(7, await CarriedAsync(store, "Mason"));

                // Off the board, and remembered as sold.
                Assert.Empty(await store.BrowseAsync());

                Listing sold = Assert.Single(await store.MineAsync(sellerId));

                Assert.True(sold.Sold);
                Assert.Equal(Potion, sold.Item);
                Assert.Equal(5, sold.Count);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// A bag with no room refuses the whole lot rather than taking what fits.
    /// <para>
    /// The buyer has ninety-eight of them and a stack holds ninety-nine, so exactly one of
    /// the five would go in. Taking one and charging for five is the polite kind of
    /// robbery, and it is the kind a bag that returns "how many fitted" invites.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ABuyerWithNoRoomForTheLotGetsNoneOfItAndPaysNothing()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter mason = Character(new BagEntry(Potion, 12));

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                long listingId = await store.ListItemsAsync(
                    sellerId, mason with { Items = [new BagEntry(Potion, 7)] }, Potion, 5, 900);

                var koop = Assert.IsType<AuthOutcome.Success>(await store.RegisterAsync(
                    "Koop", "a-good-password", Character(new BagEntry(Potion, Bag.MaxStack - 1))));

                Assert.Null(await store.BuyAsync(koop.Account.Id, listingId, koop.Character));

                var theirs = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Koop", "a-good-password"));

                Assert.Equal(Bag.MaxStack - 1, theirs.Character.Items.Single(e => e.ItemId == Potion).Count);
                Assert.Equal(5_000, theirs.Character.Money);

                // And it is still for sale, for somebody who has room.
                Assert.Equal(listingId, Assert.Single(await store.BrowseAsync()).Id);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task TakingAPileBackPutsThemInTheBag()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter mason = Character(new BagEntry(Potion, 12));

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                SavedCharacter without = mason with { Items = [new BagEntry(Potion, 7)] };

                long listingId = await store.ListItemsAsync(sellerId, without, Potion, 5, 900);

                Parcel? back = await store.CancelAsync(sellerId, listingId, without);

                Assert.NotNull(back);
                Assert.True(back!.IsItem);
                Assert.Equal(Potion, back.Item);
                Assert.Equal(5, back.Count);

                // Twelve again, which is the number this whole file is about.
                Assert.Equal(12, await CarriedAsync(store, "Mason"));
                Assert.Empty(await store.BrowseAsync());
                Assert.Empty(await store.MineAsync(sellerId));
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And a seller with no room keeps the listing rather than losing the difference —
    /// being told to make space is recoverable, and nine of ten handed back is not.
    /// </summary>
    [Fact]
    public async Task ASellerWithNoRoomKeepsTheListing()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter mason = Character(new BagEntry(Potion, 12));

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                long listingId = await store.ListItemsAsync(
                    sellerId, mason with { Items = [new BagEntry(Potion, 7)] }, Potion, 5, 900);

                // They filled the stack back up while it was away.
                SavedCharacter full = mason with { Items = [new BagEntry(Potion, Bag.MaxStack)] };

                Assert.Null(await store.CancelAsync(sellerId, listingId, full));

                Assert.Equal(listingId, Assert.Single(await store.BrowseAsync()).Id);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task NobodyBuysTheirOwnPile()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter mason = Character(new BagEntry(Potion, 12));

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                SavedCharacter without = mason with { Items = [new BagEntry(Potion, 7)] };

                long listingId = await store.ListItemsAsync(sellerId, without, Potion, 5, 900);

                Assert.Null(await store.BuyAsync(sellerId, listingId, without));

                Assert.Equal(7, await CarriedAsync(store, "Mason"));
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// A search for one item finds that item, and neither the other one nor any creature.
    /// </summary>
    [Fact]
    public async Task SearchingForOneItemFindsOnlyThatItem()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter mason = Character(new BagEntry(Potion, 12), new BagEntry(Antidote, 3));

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                await store.ListItemsAsync(sellerId, mason, Potion, 5, 900);
                await store.ListItemsAsync(sellerId, mason, Antidote, 2, 400);
                await store.ListAsync(sellerId, mason, Mon(150), 2_500);

                Listing found = Assert.Single(await store.SearchAsync(new MarketSearch { Item = Potion }));

                Assert.Equal(Potion, found.Item);
                Assert.Equal(5, found.Count);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And a search about creatures does not return piles of items, even when the numbers
    /// on those piles would have satisfied it.
    /// <para>
    /// This is the one that catches the shortcut. A pile has species nought and six genes
    /// of nought, so a species search skips it for free and a search for "born 100 or
    /// better" does too — right up until somebody searches for born nought, at which point
    /// every POTION in the market is a creature good enough to breed from. The store says
    /// <c>item_id = 0</c> out loud rather than relying on that, and this is what says so.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ASearchAboutCreaturesNeverReturnsAPileOfItems()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter mason = Character(new BagEntry(Potion, 12));

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                await store.ListItemsAsync(sellerId, mason, Potion, 5, 900);

                Assert.Empty(await store.SearchAsync(new MarketSearch { Born = 0 }));
                Assert.Empty(await store.SearchAsync(new MarketSearch { Species = 0 }));

                // And the pile is still there to be found by somebody asking about items.
                Assert.Single(await store.SearchAsync(new MarketSearch { Item = Potion }));
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// Selling two different items is two listings, which is the shape of the bug that
    /// escrowed creatures had: one slot number for all of them, so a seller could list
    /// once and never twice.
    /// </summary>
    [Fact]
    public async Task ASellerCanListMoreThanOnePile()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter mason = Character(new BagEntry(Potion, 12), new BagEntry(Antidote, 3));

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                await store.ListItemsAsync(sellerId, mason, Potion, 5, 900);
                await store.ListItemsAsync(sellerId, mason, Potion, 2, 400);
                await store.ListItemsAsync(sellerId, mason, Antidote, 1, 100);

                Assert.Equal(3, (await store.MineAsync(sellerId)).Count);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    // ---- the console ---------------------------------------------------------------

    [Fact]
    public async Task SellingItemsThroughTheConsoleEmptiesTheLiveBagToo()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) =
                await ArriveAsync(store, world, "Mason", Character(new BagEntry(Potion, 12)));

            string reply = Said(await RunAsync(market, world, mason, masonId, $"/sell item {Potion} 5 900"));

            Assert.Contains($"5 of item {Potion}", reply);

            // The live object, which every later save is written from.
            Assert.Equal(7, mason.Bag.CountOf(Potion));

            Listing shown = Assert.Single(await store.BrowseAsync());

            Assert.Equal(Potion, shown.Item);
            Assert.Equal(5, shown.Count);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// The bag screen is told, because the console is where somebody typed and the bag is
    /// where the result of it has to appear.
    /// </summary>
    [Fact]
    public async Task AndTheBagScreenIsToldAboutIt()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) =
                await ArriveAsync(store, world, "Mason", Character(new BagEntry(Potion, 12)));

            List<Outgoing> sent = await RunAsync(market, world, mason, masonId, $"/sell item {Potion} 5 900");

            BagUpdated told = Assert.Single(sent.Select(o => o.Message).OfType<BagUpdated>());

            Assert.Equal(7, told.Bag.Single(e => e.ItemId == Potion).Count);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task SellingMoreThanYouAreCarryingIsRefusedAndChangesNothing()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) =
                await ArriveAsync(store, world, "Mason", Character(new BagEntry(Potion, 3)));

            string reply = Said(await RunAsync(market, world, mason, masonId, $"/sell item {Potion} 5 900"));

            Assert.Contains("you have 3", reply);
            Assert.Equal(3, mason.Bag.CountOf(Potion));
            Assert.Empty(await store.BrowseAsync());
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// A key item cannot be sold, and that is a rule about the cartridge rather than about
    /// the market. There is one BICYCLE, and a market that let somebody sell theirs would
    /// be a market that could strand them behind a gate their own game had opened.
    /// </summary>
    [Fact]
    public async Task AKeyItemIsNotSomethingAnybodyMaySell()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) =
                await ArriveAsync(store, world, "Mason", Character(new BagEntry(Bicycle, 1)));

            string reply = Said(await RunAsync(market, world, mason, masonId, $"/sell item {Bicycle} 1 900"));

            Assert.Contains("not something anybody may sell", reply);
            Assert.Equal(1, mason.Bag.CountOf(Bicycle));
            Assert.Empty(await store.BrowseAsync());
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task BuyingAPileThroughTheConsoleFillsTheLiveBagAndEmptiesTheLivePurse()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) =
                await ArriveAsync(store, world, "Mason", Character(new BagEntry(Potion, 12)));

            (ServerPlayer koop, long koopId) =
                await ArriveAsync(store, world, "Koop", Character(new BagEntry(Antidote, 1)));

            await RunAsync(market, world, mason, masonId, $"/sell item {Potion} 5 900");

            long listingId = Assert.Single(await store.BrowseAsync()).Id;

            string reply = Said(await RunAsync(market, world, koop, koopId, $"/buy {listingId}"));

            Assert.Contains($"bought 5 of item {Potion}", reply);

            Assert.Equal(5, koop.Bag.CountOf(Potion));
            Assert.Equal(4_100, koop.Money);

            // And the seller's live copy is untouched by somebody else's purchase.
            Assert.Equal(7, mason.Bag.CountOf(Potion));
            Assert.Equal(5_000, mason.Money);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// Cancelling reached the store from the first day the market existed and no console
    /// verb reached the cancelling, so nobody could take anything back off the board
    /// without a database client. Both kinds go through the one verb.
    /// </summary>
    [Fact]
    public async Task CancellingPutsACreatureBackInTheLiveBox()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            SavedCharacter mason = Character() with { Box = [Mon(150)] };

            (ServerPlayer player, long masonId) = await ArriveAsync(store, world, "Mason", mason);

            await RunAsync(market, world, player, masonId, "/sell 0 2500");

            long listingId = Assert.Single(await store.BrowseAsync()).Id;

            Assert.Empty(player.Box);

            string reply = Said(await RunAsync(market, world, player, masonId, $"/cancel {listingId}"));

            Assert.Contains("took back species 150", reply);
            Assert.Equal(150, Assert.Single(player.Box).Species);
            Assert.Empty(await store.BrowseAsync());
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task AndAPileBackInTheLiveBag()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) =
                await ArriveAsync(store, world, "Mason", Character(new BagEntry(Potion, 12)));

            await RunAsync(market, world, mason, masonId, $"/sell item {Potion} 5 900");

            long listingId = Assert.Single(await store.BrowseAsync()).Id;

            string reply = Said(await RunAsync(market, world, mason, masonId, $"/cancel {listingId}"));

            Assert.Contains($"took back 5 of item {Potion}", reply);
            Assert.Equal(12, mason.Bag.CountOf(Potion));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And somebody else's listing is not theirs to take back, which is the same question
    /// the store answers and worth asking through the verb people actually type.
    /// </summary>
    [Fact]
    public async Task NobodyCancelsSomebodyElsesListing()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) =
                await ArriveAsync(store, world, "Mason", Character(new BagEntry(Potion, 12)));

            (ServerPlayer koop, long koopId) =
                await ArriveAsync(store, world, "Koop", Character(new BagEntry(Antidote, 1)));

            await RunAsync(market, world, mason, masonId, $"/sell item {Potion} 5 900");

            long listingId = Assert.Single(await store.BrowseAsync()).Id;

            string reply = Said(await RunAsync(market, world, koop, koopId, $"/cancel {listingId}"));

            Assert.Contains("no:", reply);
            Assert.Equal(0, koop.Bag.CountOf(Potion));
            Assert.Single(await store.BrowseAsync());
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// The board prints a pile as a pile. Not as a creature with dashes where its genes
    /// would be, which is what one shared format would have made it.
    /// </summary>
    [Fact]
    public async Task ThePileReadsAsAPileOnTheBoard()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) =
                await ArriveAsync(store, world, "Mason", Character(new BagEntry(Potion, 12)));

            await RunAsync(market, world, mason, masonId, $"/sell item {Potion} 5 900");

            string board = Said(await RunAsync(market, world, mason, masonId, "/market"));

            Assert.Contains($"5 of item {Potion}", board);
            Assert.DoesNotContain("species", board);
            Assert.DoesNotContain("Lv", board);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }
}
