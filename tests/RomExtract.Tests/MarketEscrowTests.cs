using Microsoft.Data.Sqlite;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The market's escrow, which is the one thing the whole feature turns on.
/// <para>
/// A market is trading without both people present, and that means a creature has to sit
/// somewhere that is not its owner's hands for hours or days. Where it sits is the fourth
/// value in the column that already says whether a row is in the party, the box or the
/// daycare — so it inherits the storage, the moves table, the PP, the genes and the
/// migration from machinery that already works.
/// </para>
/// <para>
/// What it does <em>not</em> inherit is the wholesale rewrite. Every save deletes an
/// account's creatures and reinserts them from the snapshot, which is what makes saving
/// safe — there is nowhere for a half-written party to exist — and which would destroy
/// anything on the market, because a listed creature is deliberately in none of the lists
/// a snapshot contains.
/// </para>
/// <para>
/// So there are two bounds, on the delete and on the select, and forgetting either is
/// invisible until somebody has lost something. These are their tests.
/// </para>
/// </summary>
public class MarketEscrowTests
{
    private static SavedMon Mon(int species) =>
        new(species, 20, null, 20, StatusCondition.None, Nature.Hardy, [1, 2])
        {
            Sex = Gender.Female,
            Ivs = [31, 30, 29, 28, 27, 26],
            AbilitySlot = 1,
            Pp = [30, 25],
        };

    /// <summary>
    /// Puts a creature straight into escrow, the way the market will once there is one.
    /// <para>
    /// By hand rather than through a market that is not written yet, because the claim
    /// being tested is about the store and not about the market: a row in the fourth state
    /// survives everything the store does to its owner. Writing the market first and
    /// testing through it would test both at once and tell you neither.
    /// </para>
    /// </summary>
    private static async Task<long> EscrowAsync(string path, long accountId, SavedMon mon)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();

        await using SqliteCommand insert = connection.CreateCommand();

        insert.CommandText =
            $"""
            INSERT INTO party_members
                (account_id, slot, species, level, nickname, current_hp, status, nature,
                 experience, held_item, in_box, sex, ability_slot)
            VALUES ($account, 900, $species, $level, NULL, $hp, 0, 0, 0, 0, {SqlitePlayerStore.OnTheMarket}, $sex, $ability)
            RETURNING id;
            """;

        insert.Parameters.AddWithValue("$account", accountId);
        insert.Parameters.AddWithValue("$species", mon.Species);
        insert.Parameters.AddWithValue("$level", mon.Level);
        insert.Parameters.AddWithValue("$hp", mon.CurrentHp);
        insert.Parameters.AddWithValue("$sex", (int)mon.Sex);
        insert.Parameters.AddWithValue("$ability", mon.AbilitySlot);

        return (long)(await insert.ExecuteScalarAsync())!;
    }

    private static async Task<int> EscrowedCountAsync(string path, long accountId)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();

        await using SqliteCommand count = connection.CreateCommand();

        count.CommandText =
            $"SELECT COUNT(*) FROM party_members WHERE account_id = $id AND in_box = {SqlitePlayerStore.OnTheMarket};";

        count.Parameters.AddWithValue("$id", accountId);

        return Convert.ToInt32(await count.ExecuteScalarAsync());
    }

    /// <summary>
    /// The test the whole feature turns on: a creature in escrow is still there after its
    /// seller has saved, and saving is the commonest thing a server does.
    /// </summary>
    [Fact]
    public async Task SavingItsSellerDoesNotDestroyIt()
    {
        string path = TempDatabase.Path();

        try
        {
            long accountId;

            using (var store = new SqlitePlayerStore(path))
            {
                var made = Assert.IsType<AuthOutcome.Success>(await store.RegisterAsync(
                    "Mason", "a-good-password",
                    new SavedCharacter("1.0", 3, 4, Direction.Down, [Mon(1)])));

                accountId = made.Account.Id;
            }

            await EscrowAsync(path, accountId, Mon(150));

            Assert.Equal(1, await EscrowedCountAsync(path, accountId));

            // Now the ordinary thing: the seller plays, and their character is written
            // down. This is the save that used to take the market with it.
            using (var store = new SqlitePlayerStore(path))
            {
                await store.SaveAsync(
                    accountId,
                    new SavedCharacter("1.0", 5, 6, Direction.Up, [Mon(1), Mon(4)])
                    {
                        Box = [Mon(7)],
                        Daycare = [Mon(16), Mon(19)],
                    });
            }

            Assert.Equal(1, await EscrowedCountAsync(path, accountId));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And the other half: it does not come back as though it were still theirs. A listed
    /// creature showing up in its seller's box is a creature in two places at once, and the
    /// first thing anybody would do with it is sell it twice.
    /// </summary>
    [Fact]
    public async Task AndItIsInNoneOfItsSellersLists()
    {
        string path = TempDatabase.Path();

        try
        {
            long accountId;

            using (var store = new SqlitePlayerStore(path))
            {
                var made = Assert.IsType<AuthOutcome.Success>(await store.RegisterAsync(
                    "Mason", "a-good-password",
                    new SavedCharacter("1.0", 3, 4, Direction.Down, [Mon(1)])
                    {
                        Box = [Mon(7)],
                        Daycare = [Mon(16), Mon(19)],
                    }));

                accountId = made.Account.Id;
            }

            await EscrowAsync(path, accountId, Mon(150));

            using var reading = new SqlitePlayerStore(path);

            var login = Assert.IsType<AuthOutcome.Success>(
                await reading.LoginAsync("Mason", "a-good-password"));

            Assert.Equal([1], login.Character.Party.Select(m => m.Species));
            Assert.Equal([7], login.Character.Box.Select(m => m.Species));
            Assert.Equal([16, 19], login.Character.Daycare.Select(m => m.Species));

            // And nowhere else either — the species that is in escrow appears in no list.
            Assert.DoesNotContain(
                150,
                login.Character.Party
                    .Concat(login.Character.Box)
                    .Concat(login.Character.Daycare)
                    .Select(m => m.Species));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And it survives a restart, which is the claim a market makes to anybody who lists
    /// something and closes the game.
    /// </summary>
    [Fact]
    public async Task AndItSurvivesARestart()
    {
        string path = TempDatabase.Path();

        try
        {
            long accountId;

            using (var store = new SqlitePlayerStore(path))
            {
                var made = Assert.IsType<AuthOutcome.Success>(await store.RegisterAsync(
                    "Mason", "a-good-password",
                    new SavedCharacter("1.0", 3, 4, Direction.Down, [Mon(1)])));

                accountId = made.Account.Id;
            }

            await EscrowAsync(path, accountId, Mon(150));

            using (var reopened = new SqlitePlayerStore(path))
            {
                Assert.IsType<AuthOutcome.Success>(await reopened.LoginAsync("Mason", "a-good-password"));
            }

            Assert.Equal(1, await EscrowedCountAsync(path, accountId));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// The four values are four different things, and the line between "mine" and "not
    /// mine" falls where this says it does. Written down as a test because two of the
    /// numbers are in SQL strings, where the compiler is no help at all.
    /// </summary>
    [Fact]
    public void TheFourValuesAreWhatTheySay()
    {
        Assert.Equal(0, SqlitePlayerStore.InTheParty);
        Assert.Equal(1, SqlitePlayerStore.InTheBox);
        Assert.Equal(2, SqlitePlayerStore.AtTheDaycare);
        Assert.Equal(3, SqlitePlayerStore.OnTheMarket);

        Assert.Equal(SqlitePlayerStore.AtTheDaycare, SqlitePlayerStore.LastOwnList);
        Assert.True(SqlitePlayerStore.OnTheMarket > SqlitePlayerStore.LastOwnList);
    }
}
