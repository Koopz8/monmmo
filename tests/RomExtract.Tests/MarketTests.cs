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

                SavedMon? back = await store.CancelAsync(sellerId, listingId, without);

                Assert.NotNull(back);
                Assert.Equal(150, back!.Species);
                Assert.Equal(44, back.Level);
                Assert.Equal([1, 2], back.Moves);
                Assert.Equal([30, 25], back.Pp);
                Assert.Equal([31, 30, 29, 28, 27, 26], back.Ivs);
                Assert.Equal(Gender.Female, back.Sex);
                Assert.Equal(1, back.AbilitySlot);

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
                SavedMon? back = await store.CancelAsync(
                    sellerId, listingId, without with { X = 12, Box = [Mon(7)], Money = 3_998 });

                Assert.Equal(150, back?.Species);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }
}
