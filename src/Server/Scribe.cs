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
/// </summary>
public sealed class Scribe : IAsyncDisposable
{
    private readonly IPlayerStore _store;
    private readonly ConcurrentDictionary<long, SavedCharacter> _latest = new();
    private readonly Channel<long> _queue = Channel.CreateUnbounded<long>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    private readonly CancellationTokenSource _stopping;
    private readonly Task _writing;

    private long _noted;
    private long _written;
    private long _failed;

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
    public void Forget(long accountId) => _latest.TryRemove(accountId, out _);

    private async Task WriteAsync()
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync(_stopping.Token).ConfigureAwait(false))
            {
                while (_queue.Reader.TryRead(out long accountId))
                {
                    // Taken out rather than read, so that anything noted while this one
                    // is being written queues itself again and is not lost.
                    if (!_latest.TryRemove(accountId, out SavedCharacter? state)) continue;

                    try
                    {
                        await _store.SaveAsync(accountId, state, _stopping.Token).ConfigureAwait(false);

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
    /// </summary>
    public async ValueTask DisposeAsync()
    {
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
            if (!_latest.TryRemove(accountId, out _)) continue;

            try
            {
                await _store.SaveAsync(accountId, state, CancellationToken.None).ConfigureAwait(false);

                Interlocked.Increment(ref _written);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failed);

                Console.Error.WriteLine($"! could not write account {accountId} on the way out: {ex.Message}");
            }
        }

        _stopping.Cancel();
        _stopping.Dispose();
    }
}
