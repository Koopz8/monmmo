using System.Threading.Channels;
using PokeMmo.Core.Net;

namespace PokeMmo.Server;

/// <summary>
/// One player's outgoing post, and the reason the world does not stop for it.
/// <para>
/// A broadcast used to be a loop that awaited each send in turn. That is correct and it
/// is also a rule that says: <em>the slowest socket on the server decides how fast
/// everybody else hears anything</em>. One player on a bad connection, or one whose
/// machine has stopped reading, fills their kernel buffer; the write blocks; and every
/// other person on that map waits behind them — including the world's own clock, which
/// is what dispatches the people walking about on it.
/// </para>
/// <para>
/// So each connection gets a queue and a pump. Posting is not asynchronous at all: it
/// writes to a bounded queue and returns immediately, and the pump drains it at whatever
/// speed that one socket can manage. Nothing anybody does can make anybody else wait.
/// </para>
/// <para>
/// The queue is bounded, and what happens when it fills is the interesting decision.
/// Waiting would reintroduce exactly the problem this exists to solve. Dropping messages
/// would desynchronise that client silently, which this project has learned twice is the
/// worst kind of bug. So a full queue closes the connection: a client that cannot keep up
/// with its own game is disconnected and can reconnect, which is a thing it knows how to
/// handle, and the server says so in the log rather than leaving it to be discovered.
/// </para>
/// </summary>
public sealed class Outbox : IDisposable
{
    /// <summary>
    /// How many messages may be waiting for one player before they are dropped.
    /// <para>
    /// <b>Modelled.</b> A player on a busy map receives about fifty messages a second, so
    /// this is several seconds of falling behind — long enough to ride out a hiccup, short
    /// enough that the memory a stalled connection can hold is bounded and small.
    /// </para>
    /// </summary>
    public const int Room = 256;

    private readonly Channel<NetMessage> _waiting =
        Channel.CreateBounded<NetMessage>(new BoundedChannelOptions(Room)
        {
            SingleReader = true,

            // Wait, not DropWrite — and this was written the other way round first. With
            // DropWrite a full queue accepts the message, throws it away, and reports
            // success, which is the silent desynchronisation this whole class exists to
            // prevent. With Wait, TryWrite refuses instead of waiting, and the refusal is
            // what tells the server this player has stopped keeping up.
            FullMode = BoundedChannelFullMode.Wait,
        });

    private readonly MessageChannel _channel;
    private readonly CancellationTokenSource _stopping;
    private readonly Task _pump;

    private long _sent;
    private long _dropped;

    public Outbox(MessageChannel channel, CancellationToken cancellationToken)
    {
        _channel = channel;
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pump = Task.Run(PumpAsync);
    }

    /// <summary>True once this connection has fallen too far behind to be kept.</summary>
    public bool HasFallenBehind { get; private set; }

    /// <summary>How many messages have actually gone out.</summary>
    public long Sent => Interlocked.Read(ref _sent);

    /// <summary>How many were refused because the queue was full.</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>
    /// Queues one message. Never waits, never throws, never blocks the caller.
    /// <para>
    /// Returns false when this connection has fallen behind and should be closed. The
    /// caller does not have to act on that — the pump stops on its own — but the loop
    /// that owns the connection uses it to stop pretending somebody is still there.
    /// </para>
    /// </summary>
    public bool Post(NetMessage message)
    {
        if (HasFallenBehind) return false;

        if (_waiting.Writer.TryWrite(message))
        {
            return true;
        }

        Interlocked.Increment(ref _dropped);

        // One dropped message is a client that is now wrong about the world, and this
        // project's own field notes say a client that is quietly wrong is worse than one
        // that is disconnected. So it is not "one dropped message" for long.
        HasFallenBehind = true;
        _waiting.Writer.TryComplete();

        return false;
    }

    private async Task PumpAsync()
    {
        try
        {
            while (await _waiting.Reader.WaitToReadAsync(_stopping.Token).ConfigureAwait(false))
            {
                while (_waiting.Reader.TryRead(out NetMessage? message))
                {
                    await _channel.SendAsync(message, _stopping.Token).ConfigureAwait(false);

                    Interlocked.Increment(ref _sent);
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // The connection is gone, or the server is stopping. Either way this pump has
            // nothing left to do and the loop that owns the socket will tidy up.
        }
        finally
        {
            HasFallenBehind = true;
        }
    }

    /// <summary>Stops the pump and waits for it, so a closing connection leaves nothing running.</summary>
    public void Dispose()
    {
        _waiting.Writer.TryComplete();
        _stopping.Cancel();
        _stopping.Dispose();
    }

    /// <summary>Waits for everything queued to go out, or for the connection to give up.</summary>
    public Task DrainedAsync() => _pump;
}
