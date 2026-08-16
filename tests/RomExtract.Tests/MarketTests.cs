using PokeMmo.Core.Battle;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The market: listing something, changing your mind, and what everybody else can see.
/// <para>
/// The round trip with no money in it, which is the half that can be proved without a
/// second player — and the half where a creature can go missing. Buying is the other half
/// and has its own arithmetic.
/// </para>
/// <para>
/// What every one of these is really testing is that a creature is in exactly one place at
/// every instant. In your box, or on the market, and never both and never neither.
/// </para>
/// </summary>
public class MarketTests
{
    private static SavedMon Mon(int species, int level = 20) =>
        new(species, level, null, 20, StatusCondition.None, Nature.Hardy, [1, 2])
        {
            Sex = Gender.Female,
            Ivs = [31, 30, 29, 28, 27, 26],
            AbilitySlot = 1,
            Pp = [30, 25],
        };

    private static SavedCharacter Character(params SavedMon[] box) =>
        new("1.0", 3, 4, Direction.Down, [Mon(1)]) { Box = [.. box], Money = 5_000 };

    private static async Task<(SqlitePlayerStore Store, long Id)> AccountAsync(
        string path, string name, SavedCharacter character)
    {
        var store = new SqlitePlayerStore(path);

        var made = Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync(name, "a-good-password", character));

        return (store, made.Account.Id);
    }

    [Fact]
    public async Task ListingOneTakesItOutOfTheBoxAndPutsItOnTheBoard()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(Mon(7), offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                await store.ListAsync(sellerId, mason with { Box = [Mon(7)] }, offered, 2_500);

                // Gone from the box, and from every other list they have.
                var login = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Mason", "a-good-password"));

                Assert.Equal([7], login.Character.Box.Select(m => m.Species));
                Assert.DoesNotContain(150, login.Character.Party.Select(m => m.Species));

                // And on the board, with everything a buyer would search by.
                Listing shown = Assert.Single(await store.BrowseAsync());

                Assert.Equal("Mason", shown.Seller);
                Assert.Equal(150, shown.Species);
                Assert.Equal(20, shown.Level);
                Assert.Equal(2_500, shown.Price);
                Assert.Equal(Gender.Female, shown.Sex);
                Assert.Equal([31, 30, 29, 28, 27, 26], shown.Ivs);
                Assert.False(shown.Sold);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And it survives a restart, which is the whole claim a market makes: leave it and
    /// close the game.
    /// </summary>
    [Fact]
    public async Task AndItIsStillThereAfterARestart()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store) await store.ListAsync(sellerId, mason with { Box = [] }, offered, 2_500);

            using var reopened = new SqlitePlayerStore(path);

            Assert.Equal(150, Assert.Single(await reopened.BrowseAsync()).Species);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// Taking it back puts it in the box, with everything it went in with. A creature that
    /// came back having forgotten its moves would be a creature that had been through a
    /// copy rather than a shelf.
    /// </summary>
    [Fact]
    public async Task TakingItBackReturnsTheSameCreature()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150, level: 44);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                SavedCharacter without = mason with { Box = [] };

                long listingId = await store.ListAsync(sellerId, without, offered, 2_500);

                Parcel? back = await store.CancelAsync(sellerId, listingId, without);

                Assert.NotNull(back);
                Assert.Equal(150, back!.Creature!.Species);
                Assert.Equal(44, back.Creature.Level);
                Assert.Equal([1, 2], back.Creature.Moves);
                Assert.Equal([30, 25], back.Creature.Pp);
                Assert.Equal([31, 30, 29, 28, 27, 26], back.Creature.Ivs);
                Assert.Equal(Gender.Female, back.Creature.Sex);
                Assert.Equal(1, back.Creature.AbilitySlot);

                // In the box, off the board, and only once.
                var login = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Mason", "a-good-password"));

                Assert.Equal([150], login.Character.Box.Select(m => m.Species));
                Assert.Empty(await store.BrowseAsync());
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// Nobody cancels anybody else's listing. The obvious rule, and the one a market with
    /// an id in the message has to actually enforce rather than assume.
    /// </summary>
    [Fact]
    public async Task NobodyTakesBackSomebodyElsesListing()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                var other = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Koop", "a-good-password", Character()));

                long listingId = await store.ListAsync(sellerId, mason with { Box = [] }, offered, 2_500);

                Assert.Null(await store.CancelAsync(other.Account.Id, listingId, other.Character));

                // Still on the board, and the thief's box is still empty.
                Assert.Single(await store.BrowseAsync());

                var theirs = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Koop", "a-good-password"));

                Assert.Empty(theirs.Character.Box);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And cancelling something that was never there is refused rather than thrown at.</summary>
    [Fact]
    public async Task AndAListingThatNeverExistedIsSimplyNotThere()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter mason = Character();

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store) Assert.Null(await store.CancelAsync(sellerId, 4_040, mason));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// A seller sees their own listings; the board shows everybody's. Two questions, two
    /// answers, and the first is the one with money waiting on it.
    /// </summary>
    [Fact]
    public async Task TheBoardShowsEverybodyAndMineShowsMe()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon hers = Mon(150);
            SavedMon his = Mon(151);

            SavedCharacter mason = Character(hers);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                var other = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Koop", "a-good-password", Character(his)));

                await store.ListAsync(sellerId, mason with { Box = [] }, hers, 2_500);
                await store.ListAsync(other.Account.Id, other.Character with { Box = [] }, his, 900);

                Assert.Equal(2, (await store.BrowseAsync()).Count);
                Assert.Equal(150, Assert.Single(await store.MineAsync(sellerId)).Species);
                Assert.Equal(151, Assert.Single(await store.MineAsync(other.Account.Id)).Species);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And the seller's ordinary saves do not disturb any of it — which is the escrow
    /// bound doing its work, asked here through the market rather than through SQL.
    /// </summary>
    [Fact]
    public async Task AndTheSellerCanCarryOnPlaying()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                SavedCharacter without = mason with { Box = [] };

                long listingId = await store.ListAsync(sellerId, without, offered, 2_500);

                // Three ordinary saves, the sort a server makes every few seconds.
                for (int save = 0; save < 3; save++)
                {
                    await store.SaveAsync(
                        sellerId, without with { X = 10 + save, Box = [Mon(7)], Money = 4_000 - save });
                }

                Assert.Single(await store.BrowseAsync());

                // And it still comes back whole.
                Parcel? back = await store.CancelAsync(
                    sellerId, listingId, without with { X = 12, Box = [Mon(7)], Money = 3_998 });

                Assert.Equal(150, back?.Creature?.Species);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    // ---- and the half with money in it -------------------------------------------------

    /// <summary>
    /// Buying moves both things at once: the creature into the buyer's box, the price out
    /// of their pocket. Either half on its own is somebody robbed.
    /// </summary>
    [Fact]
    public async Task BuyingMovesTheCreatureAndTheMoney()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150, level: 44);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                var koop = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Koop", "a-good-password", Character()));

                long listingId = await store.ListAsync(sellerId, mason with { Box = [] }, offered, 2_500);

                var bought = await store.BuyAsync(koop.Account.Id, listingId, koop.Character);

                Assert.NotNull(bought);
                Assert.Equal(150, bought!.Value.Bought.Creature!.Species);
                Assert.Equal(44, bought.Value.Bought.Creature.Level);
                Assert.Equal(2_500, bought.Value.Price);

                var theirs = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Koop", "a-good-password"));

                Assert.Equal([150], theirs.Character.Box.Select(m => m.Species));
                Assert.Equal(5_000 - 2_500, theirs.Character.Money);

                // Off the board, and the creature exists exactly once.
                Assert.Empty(await store.BrowseAsync());

                var sellers = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Mason", "a-good-password"));

                Assert.DoesNotContain(150, sellers.Character.Box.Select(m => m.Species));
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// The second buyer loses, and is told so. This is the guard on the update doing its
    /// work: a check beforehand reads the past, and by the time it has answered somebody
    /// else has committed.
    /// <para>
    /// Asked one after the other rather than at the same instant, on purpose. What is being
    /// proved is that the rule holds once a listing has been sold — which is the state a
    /// racing buyer arrives into — and a test that raced two threads would be proving the
    /// scheduler instead, differently on every machine.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TheSecondBuyerIsToldRatherThanCharged()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                var first = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Koop", "a-good-password", Character()));

                var second = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Ash", "a-good-password", Character()));

                long listingId = await store.ListAsync(sellerId, mason with { Box = [] }, offered, 2_500);

                Assert.NotNull(await store.BuyAsync(first.Account.Id, listingId, first.Character));
                Assert.Null(await store.BuyAsync(second.Account.Id, listingId, second.Character));

                // The loser paid nothing and got nothing.
                var theirs = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Ash", "a-good-password"));

                Assert.Empty(theirs.Character.Box);
                Assert.Equal(5_000, theirs.Character.Money);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>Nobody buys what they cannot pay for, and nothing moves when they try.</summary>
    [Fact]
    public async Task NobodyBuysWhatTheyCannotAfford()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                var koop = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Koop", "a-good-password", Character() with { Money = 100 }));

                long listingId = await store.ListAsync(sellerId, mason with { Box = [] }, offered, 2_500);

                Assert.Null(await store.BuyAsync(koop.Account.Id, listingId, koop.Character with { Money = 100 }));
                Assert.Single(await store.BrowseAsync());
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And nobody buys their own, which would be a way to move a price about.</summary>
    [Fact]
    public async Task NobodyBuysTheirOwn()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                SavedCharacter without = mason with { Box = [] };

                long listingId = await store.ListAsync(sellerId, without, offered, 2_500);

                Assert.Null(await store.BuyAsync(sellerId, listingId, without));
                Assert.Single(await store.BrowseAsync());
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// The seller's money waits for them rather than being paid into a row their next save
    /// would write over. Until it is collected the listing is the ledger, and it says sold.
    /// </summary>
    [Fact]
    public async Task TheSellersMoneyWaitsUntilTheyCollectIt()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                var koop = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Koop", "a-good-password", Character()));

                SavedCharacter without = mason with { Box = [] };

                long listingId = await store.ListAsync(sellerId, without, offered, 2_500);

                await store.BuyAsync(koop.Account.Id, listingId, koop.Character);

                // Sold, and still theirs to see.
                Listing sold = Assert.Single(await store.MineAsync(sellerId));

                Assert.True(sold.Sold);
                Assert.Equal(2_500, sold.Price);

                // Nothing has been paid into the row while they were not looking.
                var before = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Mason", "a-good-password"));

                Assert.Equal(5_000, before.Character.Money);

                // What a seller actually gets is the price less the market's cut, which is
                // taken here rather than at the sale — taking it at the sale would leave
                // the listing no longer saying what it sold for, and what a thing sold for
                // is the only price history this market has.
                int paid = 2_500 - (2_500 * IMarketStore.Cut / 100);

                Assert.Equal(paid, await store.CollectAsync(sellerId, without, ceiling: 999_999));

                var after = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Mason", "a-good-password"));

                Assert.Equal(5_000 + paid, after.Character.Money);

                // And the listing still says the full price it went for.
                Assert.Equal(2_500, sold.Price);

                // And collecting twice pays nothing, because the ledger is gone.
                Assert.Equal(0, await store.CollectAsync(sellerId, after.Character, ceiling: 999_999));
                Assert.Empty(await store.MineAsync(sellerId));
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And what will not fit under the ceiling is not paid. The ceiling belongs to the
    /// caller because how much money a character may hold is a rule about the game rather
    /// than about the disk.
    /// </summary>
    [Fact]
    public async Task AndOnlyWhatFitsIsPaid()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                var koop = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Koop", "a-good-password", Character()));

                SavedCharacter without = mason with { Box = [] };

                long listingId = await store.ListAsync(sellerId, without, offered, 2_500);

                await store.BuyAsync(koop.Account.Id, listingId, koop.Character);

                // Room for four hundred of the two and a half thousand owed.
                Assert.Equal(400, await store.CollectAsync(sellerId, without, ceiling: 5_400));
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And a sold listing can no longer be taken back off the board.</summary>
    [Fact]
    public async Task AndSomethingSoldCannotBeTakenBack()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                var koop = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Koop", "a-good-password", Character()));

                SavedCharacter without = mason with { Box = [] };

                long listingId = await store.ListAsync(sellerId, without, offered, 2_500);

                await store.BuyAsync(koop.Account.Id, listingId, koop.Character);

                Assert.Null(await store.CancelAsync(sellerId, listingId, without));
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And two buyers genuinely at once. Whoever wins, exactly one of them wins, and the
    /// creature exists in exactly one box afterwards.
    /// <para>
    /// Written after the sequential test above turned out not to prove what it claimed:
    /// removing the guard on the update left every test passing, because the read at the
    /// top of the purchase already refuses a listing that has been sold. That read is what
    /// catches a buyer who arrives late; it is not what catches two who arrive together.
    /// </para>
    /// <para>
    /// This one does not assert which buyer wins or how the loser fails — a refusal and a
    /// database that says "busy" are both losing, and insisting on one of them would be
    /// asserting the scheduler. What it asserts is the thing that must never happen: two
    /// people paying for one creature.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TwoBuyersAtOnceMeansOneCreature()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(offered);

            (SqlitePlayerStore store, long sellerId) = await AccountAsync(path, "Mason", mason);

            using (store)
            {
                var first = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Koop", "a-good-password", Character()));

                var second = Assert.IsType<AuthOutcome.Success>(
                    await store.RegisterAsync("Ash", "a-good-password", Character()));

                long listingId = await store.ListAsync(sellerId, mason with { Box = [] }, offered, 2_500);

                async Task<bool> TryBuy(long who, SavedCharacter theirs)
                {
                    try
                    {
                        return await store.BuyAsync(who, listingId, theirs) is not null;
                    }
                    catch (Exception)
                    {
                        // Losing by being told the database is busy is still losing.
                        return false;
                    }
                }

                bool[] won = await Task.WhenAll(
                    TryBuy(first.Account.Id, first.Character),
                    TryBuy(second.Account.Id, second.Character));

                Assert.Equal(1, won.Count(w => w));

                var koop = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Koop", "a-good-password"));

                var ash = Assert.IsType<AuthOutcome.Success>(
                    await store.LoginAsync("Ash", "a-good-password"));

                // One box has it, the other does not, and the board is empty.
                Assert.Equal(
                    1,
                    koop.Character.Box.Count(m => m.Species == 150) +
                    ash.Character.Box.Count(m => m.Species == 150));

                // And exactly one of them paid.
                Assert.Equal(7_500, koop.Character.Money + ash.Character.Money);

                Assert.Empty(await store.BrowseAsync());
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    // ---- searching -------------------------------------------------------------------------

    /// <summary>
    /// A market you can only read newest-first is unusable past a hundred listings, which
    /// one afternoon of play would pass. This is the difference between a board and a
    /// market.
    /// </summary>
    [Fact]
    public async Task SearchingFindsTheOnesAskedFor()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            var made = Assert.IsType<AuthOutcome.Success>(await store.RegisterAsync(
                "Mason", "a-good-password", Character()));

            long sellerId = made.Account.Id;

            SavedMon cheap = Mon(150) with { Ivs = [10, 10, 10, 10, 10, 10] };
            SavedMon dear = Mon(150) with { Ivs = [31, 31, 31, 31, 31, 31] };
            SavedMon other = Mon(151) with { Ivs = [31, 31, 31, 31, 31, 31] };

            SavedCharacter bare = Character();

            await store.ListAsync(sellerId, bare, cheap, 500);
            await store.ListAsync(sellerId, bare, dear, 9_000);
            await store.ListAsync(sellerId, bare, other, 100);

            // By species.
            IReadOnlyList<Listing> ofOne = await store.SearchAsync(new MarketSearch { Species = 150 });

            Assert.Equal(2, ofOne.Count);
            Assert.All(ofOne, l => Assert.Equal(150, l.Species));

            // Cheapest first, which is the question anybody actually has.
            Assert.Equal(500, ofOne[0].Price);

            // By price.
            Assert.Equal(2, (await store.SearchAsync(new MarketSearch { Most = 1_000 })).Count);

            // And by what they were born with, added across the six.
            IReadOnlyList<Listing> good = await store.SearchAsync(new MarketSearch { Born = 180 });

            Assert.Equal(2, good.Count);
            Assert.All(good, l => Assert.True(l.Total >= 180));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And the three narrow each other rather than competing.</summary>
    [Fact]
    public async Task AndTheThreeNarrowEachOther()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            var made = Assert.IsType<AuthOutcome.Success>(await store.RegisterAsync(
                "Mason", "a-good-password", Character()));

            SavedCharacter bare = Character();

            await store.ListAsync(made.Account.Id, bare, Mon(150) with { Ivs = [31, 31, 31, 31, 31, 31] }, 500);
            await store.ListAsync(made.Account.Id, bare, Mon(150) with { Ivs = [1, 1, 1, 1, 1, 1] }, 400);
            await store.ListAsync(made.Account.Id, bare, Mon(151) with { Ivs = [31, 31, 31, 31, 31, 31] }, 400);

            IReadOnlyList<Listing> found = await store.SearchAsync(
                new MarketSearch { Species = 150, Most = 600, Born = 180 });

            Assert.Equal(150, Assert.Single(found).Species);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And a search that asks for nothing is not a search.</summary>
    [Fact]
    public void AndASearchForNothingKnowsItIsNothing()
    {
        Assert.True(new MarketSearch().IsEverything);
        Assert.False(new MarketSearch { Species = 1 }.IsEverything);
        Assert.False(new MarketSearch { Most = 1 }.IsEverything);
        Assert.False(new MarketSearch { Born = 1 }.IsEverything);
    }

    /// <summary>And nothing already sold is ever on it.</summary>
    [Fact]
    public async Task AndNothingSoldIsEverFound()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            SavedMon offered = Mon(150);
            SavedCharacter mason = Character(offered);

            var made = Assert.IsType<AuthOutcome.Success>(await store.RegisterAsync(
                "Mason", "a-good-password", mason));

            var koop = Assert.IsType<AuthOutcome.Success>(await store.RegisterAsync(
                "Koop", "a-good-password", Character()));

            long listingId = await store.ListAsync(made.Account.Id, mason with { Box = [] }, offered, 2_500);

            Assert.Single(await store.SearchAsync(new MarketSearch { Species = 150 }));

            await store.BuyAsync(koop.Account.Id, listingId, koop.Character);

            Assert.Empty(await store.SearchAsync(new MarketSearch { Species = 150 }));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// A seller can list more than one thing, which they could not before the search tests
    /// were written.
    /// <para>
    /// Every escrowed row took the same slot number, and the table's uniqueness rule is
    /// (account, slot) — so the second listing from one account threw. No test noticed
    /// because none of them had ever listed twice from one seller, which is a thing every
    /// real seller does immediately.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ASellerCanListMoreThanOneThing()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            var made = Assert.IsType<AuthOutcome.Success>(await store.RegisterAsync(
                "Mason", "a-good-password", Character()));

            SavedCharacter bare = Character();

            for (int which = 0; which < 5; which++)
                await store.ListAsync(made.Account.Id, bare, Mon(150 + which), 100 + which);

            Assert.Equal(5, (await store.BrowseAsync()).Count);
            Assert.Equal(5, (await store.MineAsync(made.Account.Id)).Count);

            // And every one of them comes back whole.
            foreach (Listing listing in await store.MineAsync(made.Account.Id))
                Assert.NotNull(await store.CancelAsync(made.Account.Id, listing.Id, bare));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }
}
