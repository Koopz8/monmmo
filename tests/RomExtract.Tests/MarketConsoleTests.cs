using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The market as somebody actually uses it, which is where the disk and the server's own
/// memory have to end up agreeing.
/// <para>
/// Everything below the console was proved against the store alone. What these add is the
/// half that store tests cannot reach: a player is a row <em>and</em> a live object, and a
/// purchase that moved one without the other is a purchase that is undone by the next save
/// or duplicated by the next login.
/// </para>
/// </summary>
public class MarketConsoleTests
{
    private const string Town = "1.0";

    private static SavedMon Mon(int species, int level = 20) =>
        new(species, level, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
        {
            Sex = Gender.Female,
            Ivs = [31, 30, 29, 28, 27, 26],
        };

    private static GameWorld World() =>
        new(new WorldData([new MapData(Town, "PALLET TOWN", 8, 8, new byte[64])]), Town, TestRules.All);

    /// <summary>
    /// An account in the store and a player in the world, which is what everybody on a
    /// running server is. Both, because the market is the one thing that changes them
    /// together.
    /// </summary>
    private static async Task<(ServerPlayer Player, long AccountId)> ArriveAsync(
        SqlitePlayerStore store, GameWorld world, string name, params SavedMon[] box)
    {
        SavedCharacter fresh = new(Town, 3, 4, Direction.Down, [Mon(1)])
        {
            Box = [.. box],
            Money = 5_000,
        };

        var made = Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync(name, "a-good-password", fresh));

        (ServerPlayer player, _) = world.Join(made.Account.Id, name, fresh);

        world.Operators.Add(name);

        return (player, made.Account.Id);
    }

    private static string Said(IEnumerable<Outgoing> from) =>
        string.Join("\n", from.Select(o => o.Message).OfType<ConsoleReply>().Select(r => r.Text));

    private static Task<List<Outgoing>> RunAsync(
        Market market, GameWorld world, ServerPlayer player, long accountId, string text) =>
        market.RunAsync(world, player.Id, accountId, ConsoleLine.Of(text));

    [Fact]
    public async Task SellingTakesItOutOfTheBoxInMemoryAsWellAsOnDisk()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason", Mon(150));

            string reply = Said(await RunAsync(market, world, mason, masonId, "/sell 0 2500"));

            Assert.Contains("species 150", reply);

            // The live object, which is what every later save is written from.
            Assert.Empty(mason.Box);

            // And the board.
            Listing shown = Assert.Single(await store.BrowseAsync());

            Assert.Equal(150, shown.Species);
            Assert.Equal(2_500, shown.Price);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And a save taken straight afterwards — the thing a server does within a second of
    /// anything happening — does not put it back.
    /// </summary>
    [Fact]
    public async Task AndTheNextSaveDoesNotUndoIt()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason", Mon(150));

            await RunAsync(market, world, mason, masonId, "/sell 0 2500");

            // Exactly what the server does next, from the live object.
            await store.SaveAsync(masonId, world.Snapshot(mason.Id)!);

            Assert.Single(await store.BrowseAsync());

            var login = Assert.IsType<AuthOutcome.Success>(
                await store.LoginAsync("Mason", "a-good-password"));

            Assert.Empty(login.Character.Box);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task BuyingMovesTheCreatureAndTheMoneyInMemory()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason", Mon(150));
            (ServerPlayer koop, long koopId) = await ArriveAsync(store, world, "Koop");

            await RunAsync(market, world, mason, masonId, "/sell 0 2500");

            long listingId = (await store.BrowseAsync()).Single().Id;

            string reply = Said(await RunAsync(market, world, koop, koopId, $"/buy {listingId}"));

            Assert.Contains("bought", reply);

            Assert.Equal([150], koop.Box.Select(m => m.Species));
            Assert.Equal(2_500, koop.Money);

            // And the seller has not been quietly paid — their money is waiting for them.
            Assert.Equal(5_000, mason.Money);
            Assert.Contains("SOLD", Said(await RunAsync(market, world, mason, masonId, "/mine")));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task CollectingPaysOnceAndOnlyOnce()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason", Mon(150));
            (ServerPlayer koop, long koopId) = await ArriveAsync(store, world, "Koop");

            await RunAsync(market, world, mason, masonId, "/sell 0 2500");

            long listingId = (await store.BrowseAsync()).Single().Id;

            await RunAsync(market, world, koop, koopId, $"/buy {listingId}");

            // Less the market's cut, which is why this is not 2500.
            int paid = 2_500 - (2_500 * IMarketStore.Cut / 100);

            Assert.Contains(
                $"collected {paid}", Said(await RunAsync(market, world, mason, masonId, "/collect")));

            Assert.Equal(5_000 + paid, mason.Money);

            Assert.Contains("nothing has sold", Said(await RunAsync(market, world, mason, masonId, "/collect")));
            Assert.Equal(5_000 + paid, mason.Money);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// A refused purchase changes nothing at all, which is the case worth checking because
    /// it is the one where memory and disk could drift apart without anybody noticing.
    /// </summary>
    [Fact]
    public async Task ARefusedPurchaseLeavesEverythingWhereItWas()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason", Mon(150));
            (ServerPlayer koop, long koopId) = await ArriveAsync(store, world, "Koop");

            await RunAsync(market, world, mason, masonId, "/sell 0 9000");

            long listingId = (await store.BrowseAsync()).Single().Id;

            string reply = Said(await RunAsync(market, world, koop, koopId, $"/buy {listingId}"));

            Assert.Contains("cannot afford", reply);
            Assert.Empty(koop.Box);
            Assert.Equal(5_000, koop.Money);
            Assert.Single(await store.BrowseAsync());
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And nobody sells a slot they have not got.</summary>
    [Fact]
    public async Task NobodySellsAnEmptySlot()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");

            Assert.Contains("nobody in box slot", Said(await RunAsync(market, world, mason, masonId, "/sell 0 100")));
            Assert.Empty(await store.BrowseAsync());
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And a price has to be a price.</summary>
    [Fact]
    public async Task AndAPriceHasToBeANumberAboveNought()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason", Mon(150));

            Assert.Contains("above nought", Said(await RunAsync(market, world, mason, masonId, "/sell 0 0")));
            Assert.Contains("/sell", Said(await RunAsync(market, world, mason, masonId, "/sell")));

            Assert.Single(mason.Box);
            Assert.Empty(await store.BrowseAsync());
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>The verbs this takes, and the ones it leaves to the world.</summary>
    [Fact]
    public void ItTakesItsOwnVerbsAndNobodyElses()
    {
        Assert.True(Market.Handles("market"));
        Assert.True(Market.Handles("sell"));
        Assert.True(Market.Handles("buy"));
        Assert.True(Market.Handles("mine"));
        Assert.True(Market.Handles("collect"));

        Assert.False(Market.Handles("give"));
        Assert.False(Market.Handles("trade"));
        Assert.False(Market.Handles("daycare"));
        Assert.False(Market.Handles(""));
    }
}
