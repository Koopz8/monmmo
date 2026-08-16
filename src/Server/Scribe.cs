using System.Collections.Concurrent;
using System.Threading.Channels;
using PokeMmo.Core.Save;
using PokeMmo.Server.Storage;

namespace PokeMmo.Server;

/// <summary>
/// Writes characters down, behind the players rather than in front of them.
/// <para>
/// A save used to happen inside the loop that reads a player's messages: the message was
/// handled, the character was written to disk, and only then was the next message read.
/// So the disk was in the path of that player's input. Measured, a save is sixteen
/// milliseconds and occasionally four hundred and fifty — which is a player's game
/// freezing for half a second because a disk was busy, with nothing on screen to say so.
/// </para>
/// <para>
/// It also could not be batched, because there was nowhere to batch it. Two things
/// happening to the same character a tenth of a second apart were two full rewrites, and
/// a save is a rewrite of everything: the row, the party, every move, the bag, the flags.
/// </para>
/// <para>
/// This is the usual answer and it is worth being precise about why it is safe. The
/// queue holds <em>the latest state per account</em>, not a list of states: a character
/// noted twice before either is written is written once, with the newer of the two, and
/// the older one was never anything anybody could observe. Nothing is lost by skipping
/// it — it is not an event, it is a photograph, and only the last photograph matters.
/// </para>
/// <para>
/// What is not safe is letting a player disconnect with a photograph still in the queue,
/// so the connection's own last save stays where it is, synchronous, and takes precedence
/// by simply being newer.
/// </para>
/// <para>
/// And what is <em>also</em> not safe — the thing <see cref="HoldAsync"/> exists for — is
/// anything else writing the same account while a photograph of it is being developed.
/// <see cref="Forget"/> alone cannot stop that: by the time it is called the pump may
/// already have taken the state out of the queue and be inside the write. The market is the
/// one place that writes a character by hand while its owner is still playing, and a stale
/// photograph landing on top of a listing is a creature in two places at once.
/// </para>
/// </summary>
public sealed class Scribe : IAsyncDisposable
{
    private readonly IPlayerStore _store;
    private readonly ConcurrentDictionary<long, SavedCharacter> _latest = new();

    /// <summary>
    /// What was last written for each account, so the store can skip the parts that have
    /// not changed since.
    /// <para>
    /// Held here rather than in the store because this is the only thing that knows a
    /// write finished. A wrong answer here is a section silently not written, so it is
    /// set only after a write returns, dropped the moment anything else writes that
    /// account by hand, and never guessed at.
    /// </para>
    /// </summary>
    private readonly ConcurrentDictionary<long, SavedCharacter> _onDisk = new();
    private readonly Channel<long> _queue = Channel.CreateUnbounded<long>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    private readonly CancellationTokenSource _stopping;
    private readonly Task _writing;

    /// <summary>
    /// One gate per account, held by whoever is writing that account.
    /// <para>
    /// Per account rather than one for everybody, because two players saving at once is the
    /// ordinary case and a single gate would put every save on this server behind the
    /// slowest one.
    /// </para>
    /// <para>
    /// Never removed. A gate is a few dozen bytes and the set is bounded by how many
    /// accounts have played since this process started; taking one out is a race against
    /// whoever is about to ask for it, and losing that race is the bug this whole thing
    /// exists to prevent.
    /// </para>
    /// </summary>
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _gates = new();

    private long _noted;
    private long _written;
    private long _failed;
    private long _waitedFor;
    private int _stopped;

    public Scribe(IPlayerStore store, CancellationToken cancellationToken)
    {
        _store = store;
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _writing = Task.Run(WriteAsync);
    }

    /// <summary>How many characters have been handed over to be written.</summary>
    public long Noted => Interlocked.Read(ref _noted);

    /// <summary>How many writes actually happened.</summary>
    public long Written => Interlocked.Read(ref _written);

    /// <summary>
    /// How many writes were saved by two changes to one character arriving before either
    /// was written. The number that says whether this was worth building.
    /// </summary>
    public long Coalesced => Math.Max(0, Noted - Written - Waiting);

    /// <summary>How many characters are waiting to be written right now.</summary>
    public int Waiting => _latest.Count;

    /// <summary>How many writes threw. Counted rather than swallowed.</summary>
    public long Failed => Interlocked.Read(ref _failed);

    /// <summary>
    /// How many times this had to wait for somebody else to finish writing an account.
    /// <para>
    /// Counted because it is the number that says whether the gate below is doing anything.
    /// Nought forever would mean the window it closes was never open, and a large number
    /// would mean the market is busy enough to be worth a different arrangement.
    /// </para>
    /// </summary>
    public long WaitedFor => Interlocked.Read(ref _waitedFor);

    /// <summary>
    /// Takes one account out of this scribe's hands until the returned handle is disposed.
    /// <para>
    /// For whoever is about to write that character by hand. While it is held, the pump
    /// will not write this account — it waits — so a photograph taken before the hand-written
    /// change cannot land on top of it afterwards.
    /// </para>
    /// <para>
    /// Anything already queued is dropped on the way in <em>and</em> on the way out. On the
    /// way in because it is older than what is about to be written; on the way out because
    /// something may have been noted from memory during the hold, and memory did not know
    /// about the change until the very end of it.
    /// </para>
    /// </summary>
    public async ValueTask<IAsyncDisposable> HoldAsync(
        long accountId, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = GateFor(accountId);

        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            Interlocked.Increment(ref _waitedFor);

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        Forget(accountId);

        return new Hold(this, accountId, gate);
    }

    private SemaphoreSlim GateFor(long accountId) =>
        _gates.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));

    /// <summary>
    /// One account, held. Disposing it forgets whatever was noted meanwhile and lets the
    /// pump have the account back.
    /// </summary>
    private sealed class Hold(Scribe scribe, long accountId, SemaphoreSlim gate) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            scribe.Forget(accountId);
            gate.Release();

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Hands over the newest state of one character. Returns at once, always.
    /// <para>
    /// Noting the same account twice before the first is written replaces the state and
    /// does not queue a second write, which is the whole saving.
    /// </para>
    /// </summary>
    public void Note(long accountId, SavedCharacter state)
    {
        Interlocked.Increment(ref _noted);

        bool queued = _latest.ContainsKey(accountId);

        _latest[accountId] = state;

        if (!queued) _queue.Writer.TryWrite(accountId);
    }

    /// <summary>
    /// Forgets anything queued for one account, because something newer is being written
    /// by hand — which is what a disconnect does.
    /// </summary>
    public void Forget(long accountId)
    {
        _latest.TryRemove(accountId, out _);

        // And what is on disk, because somebody else is about to write it: the next
        // write through here must be a whole one rather than a difference against a
        // state this no longer knows.
        _onDisk.TryRemove(accountId, out _);
    }

    private async Task WriteAsync()
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync(_stopping.Token).ConfigureAwait(false))
            {
                while (_queue.Reader.TryRead(out long accountId))
                {
                    SemaphoreSlim gate = GateFor(accountId);

                    // The gate before the state, and that order is the whole point. Taking
                    // the state out first would put it beyond the reach of the Forget that
                    // whoever holds this account has already done, and it would then be
                    // written after their change — which is the duplicate this prevents.
                    await gate.WaitAsync(_stopping.Token).ConfigureAwait(false);

                    try
                    {
                        // Taken out rather than read, so that anything noted while this one
                        // is being written queues itself again and is not lost.
                        if (!_latest.TryRemove(accountId, out SavedCharacter? state)) continue;

                        try
                        {
                            _onDisk.TryGetValue(accountId, out SavedCharacter? already);

                            await _store
                                .SaveAsync(accountId, state, _stopping.Token, already)
                                .ConfigureAwait(false);

                            _onDisk[accountId] = state;

                            Interlocked.Increment(ref _written);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            Interlocked.Increment(ref _failed);

                            // Said out loud, because a save that fails quietly is one that
                            // looks like it worked until somebody logs in.
                            Console.Error.WriteLine($"! could not write account {accountId}: {ex.Message}");
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ordinary shutdown.
        }
    }

    /// <summary>
    /// Stops taking anything new and writes out whatever is still queued.
    /// <para>
    /// A server that is asked to stop must not throw away the last few seconds of
    /// everybody's play, so this is a flush and not a cancel.
    /// </para>
    /// <para>
    /// Being asked twice does nothing the second time. Disposing twice is allowed of
    /// anything disposable and this used to throw on the way out, which turns a tidy-up into
    /// an error — and a shutdown path that throws is a shutdown path that skips whatever
    /// came after it.
    /// </para>
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1) return;

        _queue.Writer.TryComplete();

        // The pump first, and before anything is cancelled — it is very likely part way
        // through a write, and cancelling that loses exactly the character whose turn it
        // was. Which is what the test for this caught: four of five written, at random.
        try
        {
            await _writing.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        // And then whatever is left, which is everything noted after the server was asked
        // to stop, plus everything the pump never reached because it was cancelled.
        foreach ((long accountId, SavedCharacter state) in _latest.ToArray())
        {
            // The same gate as the pump takes, because a market transaction can still be in
            // flight while a server is being asked to stop, and "on the way out" is no
            // reason to write over one.
            SemaphoreSlim gate = GateFor(accountId);

            await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                if (!_latest.TryRemove(accountId, out _)) continue;

                await _store.SaveAsync(accountId, state, CancellationToken.None).ConfigureAwait(false);

                Interlocked.Increment(ref _written);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failed);

                Console.Error.WriteLine($"! could not write account {accountId} on the way out: {ex.Message}");
            }
            finally
            {
                gate.Release();
            }
        }

        _stopping.Cancel();
        _stopping.Dispose();
    }
}
