using System.Collections.Concurrent;
using System.Net.Sockets;
using PokeMmo.Core.Net;
using PokeMmo.Core.World;

namespace PokeMmo.Client;

/// <summary>
/// The client's half of the connection.
/// <para>
/// Receiving runs on its own task and drops messages into a queue; the render loop
/// drains it once a frame. That keeps every piece of game state owned by one thread —
/// touching it from the receive task would mean a position could change part-way
/// through drawing a frame.
/// </para>
/// </summary>
public sealed class NetworkClient : IDisposable
{
    private readonly ConcurrentQueue<NetMessage> _inbox = new();
    private readonly CancellationTokenSource _shutdown = new();

    private TcpClient? _connection;
    private MessageChannel? _channel;

    /// <summary>Assigned by the server on join; zero until then.</summary>
    public int PlayerId { get; private set; }

    public bool IsConnected => _connection?.Connected ?? false;

    /// <summary>Set when the connection drops, so the client can say why.</summary>
    public string? Failure { get; private set; }

    public async Task ConnectAsync(string host, int port, string playerName)
    {
        _connection = new TcpClient { NoDelay = true };
        await _connection.ConnectAsync(host, port).ConfigureAwait(false);

        _channel = new MessageChannel(_connection.GetStream());
        await _channel.SendAsync(new JoinRequest(playerName), _shutdown.Token).ConfigureAwait(false);

        _ = ReceiveLoopAsync();
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested &&
                   await _channel!.ReceiveAsync(_shutdown.Token).ConfigureAwait(false) is { } message)
            {
                if (message is Welcome welcome) PlayerId = welcome.PlayerId;
                _inbox.Enqueue(message);
            }

            Failure ??= "The server closed the connection.";
        }
        catch (OperationCanceledException)
        {
            // Ordinary shutdown.
        }
        catch (Exception ex)
        {
            Failure = ex.Message;
        }
    }

    /// <summary>Everything that has arrived since the last call.</summary>
    public IEnumerable<NetMessage> Drain()
    {
        while (_inbox.TryDequeue(out NetMessage? message)) yield return message;
    }

    /// <summary>
    /// Tells the server which way we just stepped. Fire and forget: the client has
    /// already predicted the result, and waiting for confirmation would add a round
    /// trip of input lag to every square walked.
    /// </summary>
    public void SendMove(Direction direction)
    {
        if (_channel is null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _channel.SendAsync(new MoveRequest(direction), _shutdown.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
                Failure ??= "Lost the connection.";
            }
        });
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _connection?.Dispose();
        _shutdown.Dispose();
    }
}
