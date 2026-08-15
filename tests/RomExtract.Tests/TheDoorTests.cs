using PokeMmo.Core.Battle;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A thousand people arriving.
/// <para>
/// The most expensive thing this server does is check a password, by three orders of
/// magnitude: a step is answered in about two milliseconds and a password costs ninety,
/// and nineteen megabytes for as long as it takes. That is deliberate — a hash that is
/// cheap to check is cheap to attack — and it was unbounded, so a hundred people
/// arriving together were a hundred hashes at once, each one holding memory and fighting
/// the others for a core it needed to itself.
/// </para>
/// <para>
/// Measured with the crowd tool, at a hundred: the median arrival took 24 seconds, the
/// worst took 44, and seven never got in at all — while the world, for everybody already
/// inside, answered steps in under three milliseconds. The wall was the door and not the
/// game, which is not what anybody would have guessed.
/// </para>
/// </summary>
public class TheDoorTests
{
    /// <summary>Never more people inside the door than it is wide.</summary>
    [Fact]
    public async Task NoMoreThanItsWidthAreLetThroughAtOnce()
    {
        var door = new Doorway(width: 3);

        int inside = 0, most = 0;
        object counter = new();

        async Task<int> Knock()
        {
            return await door.AdmitAsync(async () =>
            {
                lock (counter)
                {
                    inside++;
                    most = Math.Max(most, inside);
                }

                await Task.Delay(20);

                lock (counter) inside--;

                return 1;
            });
        }

        await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Knock()));

        Assert.True(most <= 3, $"{most} were inside a door three wide");
        Assert.Equal(0, inside);
    }

    /// <summary>And the width is never nothing, whatever it is asked for.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void AndADoorIsNeverShut(int asked)
    {
        Assert.True(new Doorway(asked).Width >= 1);
    }

    /// <summary>What the work returns is what the caller gets.</summary>
    [Fact]
    public async Task WhatIsInsideComesBackOut()
    {
        Assert.Equal("welcome", await new Doorway(1).AdmitAsync(() => Task.FromResult("welcome")));
    }

    /// <summary>
    /// And a check that throws still gives its permit back. Without this, one bad login
    /// narrows the door for ever and the server dies hours later of a cause nothing
    /// records.
    /// </summary>
    [Fact]
    public async Task AndAFailedCheckGivesItsPermitBack()
    {
        var door = new Doorway(width: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => door.AdmitAsync<int>(() => throw new InvalidOperationException("no")));

        Assert.Equal(2, await door.AdmitAsync(() => Task.FromResult(2)));
    }

    /// <summary>The door counts, because a queue nobody can see is a queue nobody fixes.</summary>
    [Fact]
    public async Task AndItCountsWhoCameThrough()
    {
        var door = new Doorway(width: 2);

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => door.AdmitAsync(() => Task.FromResult(0))));

        Assert.Equal(6, door.Admitted);
        Assert.Equal(0, door.Waiting);
    }

    /// <summary>
    /// The cost parameters are the published baseline rather than a number this project
    /// picked, and they are the thing a future decision to raise them has to beat.
    /// </summary>
    [Fact]
    public void ThePasswordCostIsTheBaselineItClaimsToBe()
    {
        Assert.Equal(19 * 1024, PasswordHasher.MemoryKib);
        Assert.Equal(2, PasswordHasher.Iterations);
        Assert.Equal(1, PasswordHasher.Parallelism);
    }

    /// <summary>
    /// And an account made under the old, dearer parameters still gets in. Lowering a
    /// cost must never lock anybody out, and it does not: every hash carries the
    /// parameters it was made under.
    /// </summary>
    [Fact]
    public void AndAnAccountMadeUnderTheOldCostStillOpens()
    {
        // What the hasher wrote when it was 64 MiB and three passes, made here by hand
        // rather than kept as a string, so this test says what it is testing.
        string dearer = Hashed("a-good-password", 64 * 1024, 3);

        Assert.True(PasswordHasher.Verify("a-good-password", dearer));
        Assert.False(PasswordHasher.Verify("the-wrong-one", dearer));

        // And it is not flagged for rehashing, because it is stronger and not weaker.
        Assert.False(PasswordHasher.NeedsRehash(dearer));
    }

    private static string Hashed(string password, int memoryKib, int iterations)
    {
        byte[] salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);

        using var argon = new Konscious.Security.Cryptography.Argon2id(
            System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = 1,
        };

        return string.Join('$',
            "",
            "argon2id",
            "v=19",
            $"m={memoryKib},t={iterations},p=1",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(argon.GetBytes(32)));
    }
}

/// <summary>
/// Writing everybody down.
/// <para>
/// A save is the one thing this server does that touches a disk. It happens on anything
/// a player does that is not walking, at most once a second each — and it rewrites the
/// whole character every time: the row, the party, every move, every item, every flag.
/// </para>
/// <para>
/// Measured at a hundred players doing something every two seconds: 21 ms a save on
/// average and 458 ms at worst. A thousand players at that rate is five hundred saves a
/// second, which is ten times more writing than those numbers allow. So the first
/// question is how many of them were needed at all.
/// </para>
/// </summary>
public class WritingEverybodyDownTests
{
    private static SavedCharacter Somebody() => new(
        "1.0", 4, 5, Direction.Down,
        [new SavedMon(1, 5, null, 20, StatusCondition.None, Nature.Hardy, [33])])
    {
        Money = 3000,
        Items = [new BagEntry(4, 1)],
        Flags = [7, 9],
    };

    /// <summary>
    /// Two snapshots of somebody who has not moved a muscle are the same snapshot.
    /// <para>
    /// They were not, and could not have been: a record compares its members with
    /// <c>Equals</c>, and for a list that is reference equality. <see cref="SavedMon"/>
    /// closed that trap on itself and said why; the type holding it did not, so the
    /// question "has anything changed since the last save?" could only ever be answered
    /// yes — and every non-movement message any player sent rewrote everything about
    /// them.
    /// </para>
    /// </summary>
    [Fact]
    public void TwoSnapshotsOfAnUnchangedCharacterAreEqual()
    {
        Assert.Equal(Somebody(), Somebody());
    }

    /// <summary>And one step apart is not.</summary>
    [Fact]
    public void AndOneStepApartIsNot()
    {
        Assert.NotEqual(Somebody(), Somebody() with { X = 5 });
    }

    /// <summary>Nor is one item, one flag, one coin or one creature apart.</summary>
    [Fact]
    public void NorIsAnythingElseThatChanged()
    {
        Assert.NotEqual(Somebody(), Somebody() with { Money = 2999 });
        Assert.NotEqual(Somebody(), Somebody() with { Flags = [7] });
        Assert.NotEqual(Somebody(), Somebody() with { Items = [] });

        Assert.NotEqual(
            Somebody(),
            Somebody() with
            {
                Party = [new SavedMon(1, 6, null, 20, StatusCondition.None, Nature.Hardy, [33])],
            });
    }

    /// <summary>
    /// And a party member whose health has changed by one point is a different save,
    /// which is the case that matters: the party is what a fight changes and the party is
    /// what would silently fail to be written.
    /// </summary>
    [Fact]
    public void AndOneHitPointIsADifferentSave()
    {
        SavedCharacter hurt = Somebody() with
        {
            Party = [new SavedMon(1, 5, null, 19, StatusCondition.None, Nature.Hardy, [33])],
        };

        Assert.NotEqual(Somebody(), hurt);
    }
}

/// <summary>
/// Writing behind the players rather than in front of them.
/// <para>
/// A save used to happen inside the loop that reads a player's messages, so the disk was
/// in the path of that player's input — sixteen milliseconds usually and four hundred and
/// fifty occasionally, with nothing on screen to say why the game had stopped.
/// </para>
/// <para>
/// The queue holds the latest state per account rather than a list of states, and that is
/// the whole idea: a character noted twice before either is written is written once, with
/// the newer. The older one was never anything anybody could observe — it is a photograph,
/// not an event, and only the last photograph matters.
/// </para>
/// </summary>
public class WritingBehindTests
{
    private static SavedCharacter Somewhere(int x) =>
        new("1.0", x, 5, Direction.Down, []);

    /// <summary>What is noted is written.</summary>
    [Fact]
    public async Task WhatIsNotedIsWritten()
    {
        var store = new CountingStore();

        await using (var scribe = new Scribe(store, CancellationToken.None))
        {
            scribe.Note(1, Somewhere(4));
        }

        Assert.Equal([(1L, 4)], store.Written);
    }

    /// <summary>
    /// And two notes for one account before either is written are one write, with the
    /// newer of the two. This is the saving, and it is also the correctness argument.
    /// </summary>
    [Fact]
    public async Task AndTwoNotesBeforeAWriteAreOneWrite()
    {
        var store = new CountingStore { Slow = TimeSpan.FromMilliseconds(60) };

        await using (var scribe = new Scribe(store, CancellationToken.None))
        {
            scribe.Note(1, Somewhere(1));
            scribe.Note(1, Somewhere(2));
            scribe.Note(1, Somewhere(3));

            await Task.Delay(200);
        }

        Assert.Equal(3, store.Written[^1].X);
        Assert.True(store.Written.Count < 3, $"{store.Written.Count} writes for three notes");
    }

    /// <summary>And two different accounts are two writes, however close together.</summary>
    [Fact]
    public async Task AndTwoAccountsAreTwoWrites()
    {
        var store = new CountingStore();

        await using (var scribe = new Scribe(store, CancellationToken.None))
        {
            scribe.Note(1, Somewhere(1));
            scribe.Note(2, Somewhere(2));
        }

        Assert.Equal(2, store.Written.Count);
    }

    /// <summary>
    /// And what is forgotten is not written. A disconnect writes the newest state by
    /// hand, so anything still queued for that account must not land on top of it
    /// afterwards.
    /// </summary>
    [Fact]
    public async Task AndWhatIsForgottenIsNotWritten()
    {
        var store = new CountingStore { Slow = TimeSpan.FromMilliseconds(40) };

        await using (var scribe = new Scribe(store, CancellationToken.None))
        {
            scribe.Note(1, Somewhere(1));
            scribe.Note(2, Somewhere(2));
            scribe.Forget(2);

            await Task.Delay(200);
        }

        Assert.DoesNotContain(store.Written, w => w.Account == 2);
    }

    /// <summary>
    /// And a server on its way out writes what it is still holding. Stopping must not
    /// throw away the last few seconds of everybody's play.
    /// </summary>
    [Fact]
    public async Task AndStoppingWritesWhatIsLeft()
    {
        var store = new CountingStore { Slow = TimeSpan.FromMilliseconds(50) };

        var scribe = new Scribe(store, CancellationToken.None);

        for (int account = 1; account <= 5; account++) scribe.Note(account, Somewhere(account));

        await scribe.DisposeAsync();

        Assert.Equal(5, store.Written.Select(w => w.Account).Distinct().Count());
    }

    /// <summary>A store that remembers what it was asked to write, and can be made slow.</summary>
    private sealed class CountingStore : IPlayerStore
    {
        private readonly List<(long Account, int X)> _written = [];

        public TimeSpan Slow { get; init; }

        public IReadOnlyList<(long Account, int X)> Written
        {
            get { lock (_written) return [.. _written]; }
        }

        public async Task SaveAsync(
            long accountId, SavedCharacter character, CancellationToken cancellationToken = default)
        {
            if (Slow > TimeSpan.Zero) await Task.Delay(Slow, cancellationToken);

            lock (_written) _written.Add((accountId, character.X));
        }

        public Task<AuthOutcome> RegisterAsync(
            string username, string password, SavedCharacter fresh, CancellationToken cancellationToken = default) =>
            Task.FromResult<AuthOutcome>(new AuthOutcome.Failed("not here"));

        public Task<AuthOutcome> LoginAsync(
            string username, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult<AuthOutcome>(new AuthOutcome.Failed("not here"));

        // The rest of the interface, which this test has no opinion about.
        public Task<int> ForgetStoryAsync(
            string username, SavedCharacter fresh, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> GiveAsync(
            string username, int species, int level, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> WipeAsync(
            string username, SavedCharacter fresh, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
