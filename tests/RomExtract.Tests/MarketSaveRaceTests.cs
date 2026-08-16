using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The one window the market had left, and the thing that closes it.
/// <para>
/// A character is saved from a photograph of memory, developed a moment later by a
/// background writer. The market writes the same character by hand, in a transaction, while
/// its owner is still playing. Those two are the same account written by two people, and
/// telling the writer to forget what it was holding is not enough — by the time it is told,
/// it may already have taken the photograph out and be inside the write.
/// </para>
/// <para>
/// What that costs is a creature in two places: the photograph, taken before the listing
/// and developed after it, puts it back in its seller's box while the listing still holds
/// it. Both halves are internally consistent and nothing throws.
/// </para>
/// <para>
/// The test below is not a probabilistic one. It pins the writer inside a save, runs the
/// listing against it, and lets the save through afterwards — so without the gate the
/// duplicate happens every time, and with it, never.
/// </para>
/// </summary>
public class MarketSaveRaceTests
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
    /// A player store that stops dead the first time it is asked to save, and stays stopped
    /// until it is let go.
    /// <para>
    /// Everything else it does is the real store's. Only <see cref="SaveAsync"/> is held,
    /// because only <see cref="SaveAsync"/> is what a background writer does — the market's
    /// own writes go through a different door and must not be affected, or the test would be
    /// arranging its own answer.
    /// </para>
    /// </summary>
    private sealed class Pinned(IPlayerStore real) : IPlayerStore
    {
        private readonly SemaphoreSlim _letGo = new(0, 1);
        private int _pinned;

        /// <summary>Completes the moment a save is actually inside this and waiting.</summary>
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void LetGo() => _letGo.Release();

        public async Task SaveAsync(
            long accountId,
            SavedCharacter character,
            CancellationToken cancellationToken = default,
            SavedCharacter? previous = null)
        {
            if (Interlocked.Exchange(ref _pinned, 1) == 0)
            {
                Arrived.TrySetResult();

                await _letGo.WaitAsync(cancellationToken);
            }

            await real.SaveAsync(accountId, character, cancellationToken, previous);
        }

        public Task<AuthOutcome> RegisterAsync(
            string username, string password, SavedCharacter fresh, CancellationToken cancellationToken = default) =>
            real.RegisterAsync(username, password, fresh, cancellationToken);

        public Task<AuthOutcome> LoginAsync(
            string username, string password, CancellationToken cancellationToken = default) =>
            real.LoginAsync(username, password, cancellationToken);

        public Task<int> ForgetStoryAsync(
            string username, SavedCharacter start, CancellationToken cancellationToken = default) =>
            real.ForgetStoryAsync(username, start, cancellationToken);

        public Task<bool> GiveAsync(
            string username, int species, int level, CancellationToken cancellationToken = default) =>
            real.GiveAsync(username, species, level, cancellationToken);

        public Task<bool> WipeAsync(
            string username, SavedCharacter fresh, CancellationToken cancellationToken = default) =>
            real.WipeAsync(username, fresh, cancellationToken);
    }

    [Fact]
    public async Task APhotographTakenBeforeAListingCannotBeDevelopedOnTopOfIt()
    {
        string path = TempDatabase.Path();

        try
        {
            // Two connections to one database, which is how the server runs: the writer has
            // its own and the market has its own.
            using var behind = new SqlitePlayerStore(path);
            using var counter = new SqlitePlayerStore(path);

            var pinned = new Pinned(behind);

            SavedCharacter fresh = new(Town, 3, 4, Direction.Down, [Mon(1)])
            {
                Box = [Mon(150)],
                Money = 5_000,
            };

            var made = Assert.IsType<AuthOutcome.Success>(
                await counter.RegisterAsync("Mason", "a-good-password", fresh));

            GameWorld world = World();

            (ServerPlayer mason, _) = world.Join(made.Account.Id, "Mason", fresh);

            await using var scribe = new Scribe(pinned, CancellationToken.None);

            var market = new Market(counter, scribe.HoldAsync);

            // The photograph, taken while the creature is still in the box, and pinned
            // half-developed.
            scribe.Note(made.Account.Id, world.Snapshot(mason.Id)!);

            await pinned.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // And now the listing, against a writer that is mid-save on this very account.
            Task<List<Outgoing>> listing = market.RunAsync(
                world, mason.Id, made.Account.Id, ConsoleLine.Of("sell 0 2500"));

            // Whichever comes first: the market blocking on the account (which is the whole
            // point), or the market finishing anyway (which is the bug). No sleeps — the
            // counter the scribe keeps is exactly the signal.
            while (scribe.WaitedFor == 0 && !listing.IsCompleted) await Task.Delay(5);

            pinned.LetGo();

            await listing.WaitAsync(TimeSpan.FromSeconds(10));

            // Everything drained before anybody looks, so this is about ordering rather
            // than about who happened to finish first.
            await scribe.DisposeAsync();

            var back = Assert.IsType<AuthOutcome.Success>(
                await counter.LoginAsync("Mason", "a-good-password"));

            // The whole claim, said as one number: this creature exists once.
            int copies =
                back.Character.Box.Count(m => m.Species == 150)
                + back.Character.Party.Count(m => m.Species == 150)
                + (await counter.BrowseAsync()).Count(l => l.Species == 150);

            Assert.Equal(1, copies);

            // And it is the listing that has it, not the box.
            Assert.Empty(back.Character.Box);
            Assert.Equal(150, Assert.Single(await counter.BrowseAsync()).Species);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// The gate is taken before the photograph is, and that order is the whole of it.
    /// <para>
    /// A writer that takes the photograph out of the queue first and <em>then</em> waits for
    /// the account has put it somewhere the hold cannot reach: forgetting clears the queue,
    /// and this one is no longer in the queue. It would then be written after the listing,
    /// which is the same duplicate by a slower road.
    /// </para>
    /// <para>
    /// It needs its own test because the obvious one cannot see it. Pinning a writer inside
    /// a save pins it past both the taking and the waiting, so the order of those two is
    /// invisible from there. This one pins it before either.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AndTheWriterTakesTheAccountBeforeItTakesThePhotograph()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            SavedCharacter fresh = new(Town, 3, 4, Direction.Down, [Mon(1)])
            {
                Box = [Mon(150)],
                Money = 5_000,
            };

            var made = Assert.IsType<AuthOutcome.Success>(
                await store.RegisterAsync("Mason", "a-good-password", fresh));

            GameWorld world = World();

            (ServerPlayer mason, _) = world.Join(made.Account.Id, "Mason", fresh);

            await using var scribe = new Scribe(store, CancellationToken.None);

            // Held first, so the writer meets a closed gate rather than a busy disk. This is
            // what the market does; it is done by hand here so the photograph can be handed
            // over in the middle of it.
            IAsyncDisposable holding = await scribe.HoldAsync(made.Account.Id);

            scribe.Note(made.Account.Id, world.Snapshot(mason.Id)!);

            // Long enough for the writer to have reached the queue entry either way. A
            // writer in the right order is still holding the photograph in the queue; one in
            // the wrong order has already taken it out, which is what this waits to see.
            for (int spun = 0; spun < 50 && scribe.Waiting > 0; spun++) await Task.Delay(5);

            var market = new Market(store);

            await market.RunAsync(world, mason.Id, made.Account.Id, ConsoleLine.Of("sell 0 2500"));

            // Letting go forgets whatever was noted meanwhile — which only reaches a
            // photograph that is still in the queue.
            await holding.DisposeAsync();

            await scribe.DisposeAsync();

            var back = Assert.IsType<AuthOutcome.Success>(
                await store.LoginAsync("Mason", "a-good-password"));

            int copies =
                back.Character.Box.Count(m => m.Species == 150)
                + back.Character.Party.Count(m => m.Species == 150)
                + (await store.BrowseAsync()).Count(l => l.Species == 150);

            Assert.Equal(1, copies);
            Assert.Empty(back.Character.Box);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// The same window at the other door: somebody who lists a creature and closes the game
    /// in the same breath.
    /// <para>
    /// A disconnect writes the character by hand too, from a photograph of memory, and that
    /// photograph is taken after the hold rather than before it. Taking it first and then
    /// waiting for the gate would be the same bug with a narrower window, which is the kind
    /// of fix that looks like one.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AndAHandWrittenSaveWaitsForTheMarketRatherThanRacingIt()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            SavedCharacter fresh = new(Town, 3, 4, Direction.Down, [Mon(1)])
            {
                Box = [Mon(150)],
                Money = 5_000,
            };

            var made = Assert.IsType<AuthOutcome.Success>(
                await store.RegisterAsync("Mason", "a-good-password", fresh));

            GameWorld world = World();

            (ServerPlayer mason, _) = world.Join(made.Account.Id, "Mason", fresh);

            await using var scribe = new Scribe(store, CancellationToken.None);

            var market = new Market(store, scribe.HoldAsync);

            // The photograph a disconnect would write, taken while the creature is still in
            // the box — which is what it would be if the two happened at once.
            SavedCharacter leaving = world.Snapshot(mason.Id)!;

            await market.RunAsync(world, mason.Id, made.Account.Id, ConsoleLine.Of("sell 0 2500"));

            // The disconnect, arriving after the listing with a stale photograph and taking
            // the account out of the scribe's hands the way the real one does.
            await using (await scribe.HoldAsync(made.Account.Id))
            {
                // Re-photographed under the hold, which is the ordering the real disconnect
                // uses and the only thing that makes this safe.
                await store.SaveAsync(made.Account.Id, world.Snapshot(mason.Id)!);
            }

            var back = Assert.IsType<AuthOutcome.Success>(
                await store.LoginAsync("Mason", "a-good-password"));

            Assert.Empty(back.Character.Box);
            Assert.Equal(150, Assert.Single(await store.BrowseAsync()).Species);

            // And the stale one, had it been written, would have been the duplicate — said
            // here so the fixture cannot quietly stop being about anything.
            Assert.Equal(150, Assert.Single(leaving.Box).Species);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }
}
