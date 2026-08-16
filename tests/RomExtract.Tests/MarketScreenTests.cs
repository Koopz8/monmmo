using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The market asked for by a screen instead of typed at.
/// <para>
/// The thing worth proving here is not that the screen works — it is that there is only one
/// market. Every act a screen asks for goes through the same console line the console would
/// have built, so escrow is implemented once. A second implementation of escrow is how a
/// creature ends up in two places, and two front ends is exactly the situation that invites
/// one.
/// </para>
/// </summary>
public class MarketScreenTests
{
    private const string Town = "1.0";

    private const int Potion = TestRules.PotionItem;

    private static SavedMon Mon(int species, int level = 20) =>
        new(species, level, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
        {
            Sex = Gender.Female,
            Ivs = [31, 30, 29, 28, 27, 26],
        };

    private static GameWorld World() =>
        new(new WorldData([new MapData(Town, "PALLET TOWN", 8, 8, new byte[64])]), Town, TestRules.All);

    private static async Task<(ServerPlayer Player, long AccountId)> ArriveAsync(
        SqlitePlayerStore store,
        GameWorld world,
        string name,
        IReadOnlyList<SavedMon>? box = null,
        IReadOnlyList<BagEntry>? carrying = null)
    {
        SavedCharacter fresh = new(Town, 3, 4, Direction.Down, [Mon(1)])
        {
            Box = box ?? [],
            Items = carrying ?? [],
            Money = 5_000,
        };

        var made = Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync(name, "a-good-password", fresh));

        (ServerPlayer player, _) = world.Join(made.Account.Id, name, fresh);

        return (player, made.Account.Id);
    }

    private static async Task<MarketOpened> AskAsync(
        Market market, GameWorld world, ServerPlayer player, long accountId, MarketRequest asking)
    {
        List<Outgoing> sent = await market.ScreenAsync(world, player.Id, accountId, asking);

        return Assert.Single(sent.Select(o => o.Message).OfType<MarketOpened>());
    }

    /// <summary>
    /// Every kind of ask a screen can make is a console line the market answers to.
    /// <para>
    /// The one guardrail this pair of front ends actually needs. A new kind added to the
    /// enum with no arm in the translation falls through to nothing, which is the same
    /// value "just look" has — so the button would do nothing, say nothing, and report
    /// success. That is the failure this project treats as its worst class: not an error,
    /// an absence.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryKindOfAskIsALineTheMarketAnswersTo()
    {
        var stranded = new List<string>();

        foreach (MarketAsk asking in Enum.GetValues<MarketAsk>())
        {
            ConsoleLine? line = Market.LineFor(new MarketRequest(asking));

            if (asking == MarketAsk.Look)
            {
                if (line is not null) stranded.Add($"{asking} should ask for nothing and asks for {line.Verb}");
                continue;
            }

            if (line is null) stranded.Add($"{asking} makes no line at all");
            else if (!Market.Handles(line.Verb)) stranded.Add($"{asking} makes /{line.Verb}, which is not the market's");
        }

        Assert.Empty(stranded);
    }

    [Fact]
    public async Task LookingGivesTheBoardYourOwnListingsAndWhatYouHaveToSellWithThem()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) =
                await ArriveAsync(store, world, "Mason", [Mon(150)], [new BagEntry(Potion, 12)]);

            (ServerPlayer koop, long koopId) = await ArriveAsync(store, world, "Koop", [Mon(9)]);

            await market.ScreenAsync(
                world, koop.Id, koopId, new MarketRequest(MarketAsk.SellOne) { Slot = 0, Price = 2_500 });

            MarketOpened seen = await AskAsync(
                market, world, mason, masonId, new MarketRequest(MarketAsk.Look));

            // Somebody else's listing is on the board and not in "mine".
            Assert.Equal(9, Assert.Single(seen.Board).Species);
            Assert.Empty(seen.Mine);

            // And what this player has, which is the half a screen is for.
            Assert.Equal(150, Assert.Single(seen.Box).Species);
            Assert.Equal(12, Assert.Single(seen.Bag).Count);
            Assert.Equal(5_000, seen.Money);
            Assert.Equal(0, seen.Owed);
            Assert.Equal(IMarketStore.Cut, seen.Cut);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task BuyingThroughTheScreenIsTheSameActAsTypingIt()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason", [Mon(150)]);
            (ServerPlayer koop, long koopId) = await ArriveAsync(store, world, "Koop");

            await market.ScreenAsync(
                world, mason.Id, masonId, new MarketRequest(MarketAsk.SellOne) { Slot = 0, Price = 2_500 });

            long listingId = Assert.Single(await store.BrowseAsync()).Id;

            MarketOpened seen = await AskAsync(
                market, world, koop, koopId, new MarketRequest(MarketAsk.Buy) { Listing = listingId });

            // The live objects, which every later save is written from.
            Assert.Equal(150, Assert.Single(koop.Box).Species);
            Assert.Equal(2_500, koop.Money);
            Assert.Empty(mason.Box);

            // And the picture that came back agrees with them.
            Assert.Equal(150, Assert.Single(seen.Box).Species);
            Assert.Equal(2_500, seen.Money);
            Assert.Empty(seen.Board);
            Assert.Contains("bought species 150", seen.Message);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// A refusal arrives on the picture rather than as a line of its own, so a screen shows
    /// it beside the thing that was refused instead of somewhere else entirely.
    /// </summary>
    [Fact]
    public async Task ARefusalComesBackOnThePictureAndNotAsAConsoleLine()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason", [Mon(150)]);
            (ServerPlayer koop, long koopId) = await ArriveAsync(store, world, "Koop");

            await market.ScreenAsync(
                world, mason.Id, masonId, new MarketRequest(MarketAsk.SellOne) { Slot = 0, Price = 9_000 });

            long listingId = Assert.Single(await store.BrowseAsync()).Id;

            List<Outgoing> sent = await market.ScreenAsync(
                world, koop.Id, koopId, new MarketRequest(MarketAsk.Buy) { Listing = listingId });

            Assert.Empty(sent.Select(o => o.Message).OfType<ConsoleReply>());

            MarketOpened seen = Assert.Single(sent.Select(o => o.Message).OfType<MarketOpened>());

            Assert.Contains("no:", seen.Message);
            Assert.Equal(5_000, seen.Money);
            Assert.Single(seen.Board);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// Selling a pile still tells the bag screen, because the bag screen is somebody else's
    /// and has to stay in step whether or not the market is open.
    /// </summary>
    [Fact]
    public async Task SellingAPileThroughTheScreenStillTellsTheBag()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) =
                await ArriveAsync(store, world, "Mason", null, [new BagEntry(Potion, 12)]);

            List<Outgoing> sent = await market.ScreenAsync(
                world,
                mason.Id,
                masonId,
                new MarketRequest(MarketAsk.SellSome) { Item = Potion, Count = 5, Price = 900 });

            BagUpdated told = Assert.Single(sent.Select(o => o.Message).OfType<BagUpdated>());

            Assert.Equal(7, told.Bag.Single(e => e.ItemId == Potion).Count);

            MarketOpened seen = Assert.Single(sent.Select(o => o.Message).OfType<MarketOpened>());

            Listing up = Assert.Single(seen.Mine);

            Assert.Equal(Potion, up.Item);
            Assert.Equal(5, up.Count);
            Assert.Equal(7, Assert.Single(seen.Bag).Count);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// What the screen says is waiting has already had the cut taken off it.
    /// <para>
    /// A screen that promised the gross and paid the net would be the market taking its
    /// share by surprise, which is the one thing this market decided not to do when it put
    /// the cut on the seller rather than the buyer.
    /// </para>
    /// </summary>
    [Fact]
    public async Task WhatIsWaitingIsWhatWouldActuallyBePaid()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason", [Mon(150)]);
            (ServerPlayer koop, long koopId) = await ArriveAsync(store, world, "Koop");

            await market.ScreenAsync(
                world, mason.Id, masonId, new MarketRequest(MarketAsk.SellOne) { Slot = 0, Price = 2_000 });

            long listingId = Assert.Single(await store.BrowseAsync()).Id;

            await market.ScreenAsync(
                world, koop.Id, koopId, new MarketRequest(MarketAsk.Buy) { Listing = listingId });

            MarketOpened before = await AskAsync(
                market, world, mason, masonId, new MarketRequest(MarketAsk.Look));

            int expected = 2_000 - (2_000 * IMarketStore.Cut / 100);

            Assert.Equal(expected, before.Owed);

            // And collecting pays exactly that, which is what makes the promise a promise.
            MarketOpened after = await AskAsync(
                market, world, mason, masonId, new MarketRequest(MarketAsk.Collect));

            Assert.Equal(5_000 + expected, after.Money);
            Assert.Equal(5_000 + expected, mason.Money);
            Assert.Equal(0, after.Owed);
            Assert.Empty(after.Mine);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task CancellingThroughTheScreenPutsItBackAndTakesItOffTheBoard()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason", [Mon(150)]);

            await market.ScreenAsync(
                world, mason.Id, masonId, new MarketRequest(MarketAsk.SellOne) { Slot = 0, Price = 2_500 });

            long listingId = Assert.Single(await store.BrowseAsync()).Id;

            MarketOpened seen = await AskAsync(
                market, world, mason, masonId, new MarketRequest(MarketAsk.Cancel) { Listing = listingId });

            Assert.Equal(150, Assert.Single(seen.Box).Species);
            Assert.Equal(150, Assert.Single(mason.Box).Species);
            Assert.Empty(seen.Board);
            Assert.Empty(seen.Mine);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// Looking changes nothing, which is worth a test of its own because the screen sends
    /// it every time it opens and would otherwise be a way to do something by accident.
    /// </summary>
    [Fact]
    public async Task LookingDoesNothingAtAll()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var market = new Market(store);

            (ServerPlayer mason, long masonId) =
                await ArriveAsync(store, world, "Mason", [Mon(150)], [new BagEntry(Potion, 12)]);

            List<Outgoing> sent = await market.ScreenAsync(
                world, mason.Id, masonId, new MarketRequest(MarketAsk.Look));

            Assert.Single(sent);

            Assert.Equal(150, Assert.Single(mason.Box).Species);
            Assert.Equal(12, mason.Bag.CountOf(Potion));
            Assert.Equal(5_000, mason.Money);
            Assert.Equal("", Assert.Single(sent.Select(o => o.Message).OfType<MarketOpened>()).Message);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }
}
