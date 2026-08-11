using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;

namespace PokeMmo.RomExtract.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void AcceptsTheRightPasswordAndRefusesEveryOther()
    {
        string hash = PasswordHasher.Hash("correct horse battery");

        Assert.True(PasswordHasher.Verify("correct horse battery", hash));
        Assert.False(PasswordHasher.Verify("correct horse batter", hash));
        Assert.False(PasswordHasher.Verify("Correct horse battery", hash));
        Assert.False(PasswordHasher.Verify("", hash));
    }

    [Fact]
    public void TheSamePasswordHashesDifferentlyEveryTime()
    {
        // Distinct salts. Without them, identical passwords are visible as identical
        // rows, and one cracked hash is every account that shared it.
        string first = PasswordHasher.Hash("a-good-password");
        string second = PasswordHasher.Hash("a-good-password");

        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify("a-good-password", first));
        Assert.True(PasswordHasher.Verify("a-good-password", second));
    }

    [Fact]
    public void StoresItsOwnCostParameters()
    {
        string hash = PasswordHasher.Hash("a-good-password");

        Assert.StartsWith("$argon2id$v=19$", hash);
        Assert.Contains($"m={PasswordHasher.MemoryKib},t={PasswordHasher.Iterations}", hash);
        Assert.False(PasswordHasher.NeedsRehash(hash));
    }

    [Fact]
    public void AWeakerOldHashIsFlaggedForUpgrade()
    {
        // The parameters live in the string precisely so they can be raised later. An
        // old hash still has to verify, and still has to be recognised as old.
        string weak = "$argon2id$v=19$m=1024,t=1,p=1$c2FsdHlzYWx0eXNhbHQ=$aGFzaGhhc2hoYXNoaGFzaA==";

        Assert.True(PasswordHasher.NeedsRehash(weak));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("$argon2id$v=19$m=x,t=3,p=1$c2FsdA==$aGFzaA==")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$not base64$aGFzaA==")]
    public void AMalformedHashRefusesRatherThanThrows(string stored)
    {
        // A corrupted row should cost one account its login, not take the server down.
        Assert.False(PasswordHasher.Verify("a-good-password", stored));
    }
}

public class UsernameRulesTests
{
    [Theory]
    [InlineData("Mason")]
    [InlineData("a_b_c")]
    [InlineData("Player99")]
    public void AcceptsOrdinaryNames(string name) => Assert.Null(UsernameRules.Problem(name));

    [Theory]
    [InlineData("ab")]
    [InlineData("seventeen_chars_x")]
    [InlineData("has space")]
    [InlineData("emoji\U0001F600")]
    [InlineData("semi;colon")]
    public void RefusesTheRest(string name) => Assert.NotNull(UsernameRules.Problem(name));

    [Fact]
    public void FoldsCaseSoTwoPeopleCannotShareAName()
    {
        Assert.Equal(UsernameRules.Fold("Mason"), UsernameRules.Fold("MASON"));
    }
}

/// <summary>
/// The SQLite store, against a real database.
/// <para>
/// In-memory SQLite rather than a temp file for speed, and a file for the one test
/// that has to prove data outlives the process holding it — which is, after all, the
/// entire point of this milestone.
/// </para>
/// </summary>
public class SqlitePlayerStoreTests
{
    private static SavedCharacter Character(params SavedMon[] party) =>
        new("3.19", 10, 4, Direction.Left, party) { Money = 4321 };

    private static SavedMon Mon(int species = 16, int level = 3) =>
        new(species, level, null, 11, StatusCondition.None, Nature.Hardy, [33, 45]);

    [Fact]
    public async Task RegisteringThenLoggingInReturnsTheSameCharacter()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        var registered = Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync("Mason", "a-good-password", Character()));

        var login = Assert.IsType<AuthOutcome.Success>(
            await store.LoginAsync("Mason", "a-good-password"));

        Assert.Equal(registered.Account.Id, login.Account.Id);
        Assert.Equal("3.19", login.Character.MapId);
        Assert.Equal(10, login.Character.X);
        Assert.Equal(Direction.Left, login.Character.Facing);
        Assert.Equal(4321, login.Character.Money);
    }

    [Fact]
    public async Task APartyComesBackWholeWithItsMovesInOrder()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        var caught = new SavedMon(25, 12, "Sparky", 19, StatusCondition.Paralysis, Nature.Timid, [84, 45, 98]);
        var second = new SavedMon(1, 5, null, 3, StatusCondition.None, Nature.Adamant, [33]);

        await store.RegisterAsync("Mason", "a-good-password", Character(caught, second));

        var login = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

        Assert.Equal(2, login.Character.Party.Count);
        Assert.Equal(caught, login.Character.Party[0]);
        Assert.Equal(second, login.Character.Party[1]);

        // Move order is slot order, not insertion luck: a party member's first move is
        // the one the menu puts first.
        Assert.Equal(new[] { 84, 45, 98 }, login.Character.Party[0].Moves);
    }

    [Fact]
    public async Task ABagOutlivesTheConnection()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        await store.RegisterAsync("Mason", "a-good-password", Character());

        var account = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

        await store.SaveAsync(
            account.Account.Id,
            Character() with { Items = [new BagEntry(4, 12), new BagEntry(2, 1)], Money = 8800 });

        var login = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

        Assert.Equal(8800, login.Character.Money);
        Assert.Equal([new BagEntry(2, 1), new BagEntry(4, 12)], login.Character.Items.OrderBy(i => i.ItemId));
    }

    [Fact]
    public async Task ThrowingSomethingAwayIsNotUndoneByTheNextSave()
    {
        // The bag is rewritten wholesale rather than inserted into, unlike the beaten
        // trainers. A bag genuinely does shrink, and an insert-only one is a bag nothing
        // can ever leave.
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        await store.RegisterAsync("Mason", "a-good-password", Character());

        var account = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

        await store.SaveAsync(account.Account.Id, Character() with { Items = [new BagEntry(4, 12)] });
        await store.SaveAsync(account.Account.Id, Character() with { Items = [] });

        var login = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

        Assert.Empty(login.Character.Items);
    }

    [Fact]
    public async Task WhoYouHaveBeatenOutlivesTheConnection()
    {
        // A trainer who forgets they lost challenges you again the moment you walk back
        // past them, which is worse than having no trainers at all.
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        await store.RegisterAsync("Mason", "a-good-password", Character());

        var account = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

        await store.SaveAsync(account.Account.Id, Character() with { DefeatedTrainers = [3, 214] });

        var login = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

        Assert.Equal([3, 214], login.Character.DefeatedTrainers.Order());
    }

    [Fact]
    public async Task BeingBeatenIsNeverTakenBack()
    {
        // Written with an insert rather than a rewrite, so a save that happens to be
        // built from a stale set cannot un-beat anybody.
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        await store.RegisterAsync("Mason", "a-good-password", Character());

        var account = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

        await store.SaveAsync(account.Account.Id, Character() with { DefeatedTrainers = [3, 214] });
        await store.SaveAsync(account.Account.Id, Character() with { DefeatedTrainers = [3] });

        var login = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

        Assert.Equal([3, 214], login.Character.DefeatedTrainers.Order());
    }

    [Fact]
    public async Task SavingReplacesThePartyRatherThanAddingToIt()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        var registered = Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync("Mason", "a-good-password", Character(Mon(), Mon(19))));

        await store.SaveAsync(registered.Account.Id, Character(Mon(133)));

        var login = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

        Assert.Single(login.Character.Party);
        Assert.Equal(133, login.Character.Party[0].Species);
    }

    [Fact]
    public async Task TheWrongPasswordIsRefused()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        await store.RegisterAsync("Mason", "a-good-password", Character());

        var failed = Assert.IsType<AuthOutcome.Failed>(await store.LoginAsync("Mason", "another-password"));

        // The same wording as an unknown account, so this cannot be used to find out
        // which names exist.
        var unknown = Assert.IsType<AuthOutcome.Failed>(await store.LoginAsync("Nobody", "a-good-password"));

        Assert.Equal(unknown.Reason, failed.Reason);
    }

    [Fact]
    public async Task ANameCannotBeTakenTwice()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        await store.RegisterAsync("Mason", "a-good-password", Character());

        Assert.IsType<AuthOutcome.Failed>(await store.RegisterAsync("Mason", "another-password", Character()));

        // Nor in a different case, which is what impersonation would look like.
        Assert.IsType<AuthOutcome.Failed>(await store.RegisterAsync("MASON", "another-password", Character()));
    }

    [Theory]
    [InlineData("ab", "a-good-password")]
    [InlineData("has space", "a-good-password")]
    [InlineData("Mason", "short")]
    public async Task RefusesNamesAndPasswordsThatBreakTheRules(string username, string password)
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        Assert.IsType<AuthOutcome.Failed>(await store.RegisterAsync(username, password, Character()));
    }

    [Fact]
    public async Task ClosingTheStoreReleasesTheFile()
    {
        // Only Windows can fail this: disposing a connection returns it to a pool
        // rather than closing it, and Windows refuses to delete a file something still
        // holds open while Linux allows it. Asserted here so the gap is checked rather
        // than discovered by whoever happens to run the suite on Windows next.
        string path = TempDatabase.Path();

        try
        {
            using (var store = new SqlitePlayerStore(path))
            {
                await store.RegisterAsync("Mason", "a-good-password", Character());
            }

            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            Assert.True(exclusive.Length > 0);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task DataOutlivesTheProcessThatWroteIt()
    {
        // The whole milestone in one test: write with one store, read with another
        // that shares nothing but the file.
        string path = TempDatabase.Path();

        try
        {
            var caught = new SavedMon(16, 3, null, 8, StatusCondition.None, Nature.Bold, [33, 45]);

            using (var writing = new SqlitePlayerStore(path))
            {
                await writing.RegisterAsync("Mason", "a-good-password", Character(caught));
            }

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                Assert.Equal(caught, Assert.Single(login.Character.Party));
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }
}

/// <summary>
/// Cleaning up a database file a test just used.
/// <para>
/// Tolerant on purpose. Deleting a scratch file is not what any of these tests are
/// about, and a stray file in the temp directory must not turn a green run red — as
/// it did on Windows, where an unreleased handle makes the delete fail outright and
/// Linux quietly allows it.
/// </para>
/// </summary>
internal static class TempDatabase
{
    public static string Path() =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"monmmo-{Guid.NewGuid():N}.db");

    public static void Delete(string path)
    {
        string directory = System.IO.Path.GetDirectoryName(path)!;
        string prefix = System.IO.Path.GetFileName(path);

        // The -wal and -shm companions belong to the same database.
        foreach (string leftover in Directory.GetFiles(directory, prefix + "*"))
        {
            try
            {
                File.Delete(leftover);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}

public class SavedCharacterInTheWorldTests
{
    private static GameWorld World()
    {
        var collision = new byte[12];
        collision[2] = 1;   // a wall at (2, 0)

        return new GameWorld(new WorldData([new MapData("3.0", "PALLET TOWN", 4, 3, collision)]), "3.0");
    }

    private static SavedMon Mon(int species = 16) =>
        new(species, 3, null, 8, StatusCondition.None, Nature.Hardy, [33]);

    [Fact]
    public void APlayerResumesWhereTheirSaveLeftThem()
    {
        GameWorld world = World();

        var saved = new SavedCharacter("3.0", 3, 2, Direction.Left, [Mon()])
        {
            Items = [new BagEntry(TestRules.BallItem, 7)],
            Money = 1234,
        };

        (ServerPlayer player, _) = world.Join(1, "Mason", saved);

        Assert.Equal(new GridPosition(3, 2), player.Square);
        Assert.Equal(Direction.Left, player.Facing);
        Assert.Equal(7, player.Bag.CountOf(TestRules.BallItem));
        Assert.Equal(1234, player.Money);
        Assert.Single(player.Party);
    }

    [Fact]
    public void AWelcomeCarriesTheRestoredParty()
    {
        GameWorld world = World();

        var saved = new SavedCharacter("3.0", 1, 1, Direction.Up, [Mon(25), Mon(1)])
        {
            Items = [new BagEntry(TestRules.BallItem, 4)],
        };

        (_, List<Outgoing> send) = world.Join(1, "Mason", saved);

        Welcome welcome = send.Select(o => o.Message).OfType<Welcome>().Single();

        Assert.Equal(4, welcome.Bag.Single().Count);
        Assert.Equal(2, welcome.Party.Count);
        Assert.Equal(25, welcome.Party[0].Species);
    }

    [Fact]
    public void ASaveInsideAWallFallsBackToASpawn()
    {
        // A world file re-exported under a player standing somewhere that is now solid
        // would otherwise leave them stuck, with no way out from their side.
        GameWorld world = World();

        var saved = new SavedCharacter("3.0", 2, 0, Direction.Down, []);
        (ServerPlayer player, _) = world.Join(1, "Mason", saved);

        Assert.NotEqual(new GridPosition(2, 0), player.Square);
        Assert.True(world.Grid.IsWalkable(player.Square));
    }

    [Fact]
    public void ASaveFromAnotherMapFallsBackToASpawn()
    {
        GameWorld world = World();

        var saved = new SavedCharacter("9.9", 3, 2, Direction.Down, []);
        (ServerPlayer player, _) = world.Join(1, "Mason", saved);

        Assert.True(world.Grid.IsWalkable(player.Square));
    }

    [Fact]
    public void ASnapshotDescribesThePlayerWhereTheyStand()
    {
        GameWorld world = World();

        var saved = new SavedCharacter("3.0", 0, 0, Direction.Down, []);
        (ServerPlayer player, _) = world.Join(1, "Mason", saved);

        world.Move(player.Id, Direction.Right, nowSeconds: 10);

        SavedCharacter? snapshot = world.Snapshot(player.Id);

        Assert.NotNull(snapshot);
        Assert.Equal("3.0", snapshot.MapId);
        Assert.Equal(player.Square.X, snapshot.X);
        Assert.Equal(player.Square.Y, snapshot.Y);
        Assert.Equal(Direction.Right, snapshot.Facing);
        Assert.Equal(SavedCharacter.StartingMoney, snapshot.Money);
    }

    [Fact]
    public void APartyLongerThanSixIsCutDownRatherThanTrusted()
    {
        // This arrives over a socket. Real validation needs battles resolved
        // server-side; refusing an impossible party size is what can be done today.
        GameWorld world = World();

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        SavedMon[] tooMany = Enumerable.Range(0, 40).Select(i => Mon(i + 1)).ToArray();

        Assert.True(world.UpdateSave(player.Id, tooMany));
        Assert.Equal(Party.MaxSize, world.Snapshot(player.Id)!.Party.Count);
    }

    [Fact]
    public void SavingForSomebodyWhoLeftIsRefusedRatherThanCrashing()
    {
        GameWorld world = World();

        Assert.False(world.UpdateSave(playerId: 404, []));
        Assert.Null(world.Snapshot(404));
    }
}
