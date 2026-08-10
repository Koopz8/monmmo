using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using PokeMmo.Core.Net;
using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>
/// The socket layer. Accepts connections, hands messages to <see cref="GameWorld"/>,
/// and sends back whatever it says to send.
/// <para>
/// Kept deliberately thin — every rule lives in the world, which is tested without a
/// network at all. What is here is connection lifetime and fan-out.
/// </para>
/// </summary>
public static class Program
{
    public const int DefaultPort = 7777;

    public static async Task<int> Main(string[] args)
    {
        string worldPath = ArgumentValue(args, "--world") ?? "world.dat";
        string startingMap = ArgumentValue(args, "--map") ?? "pallet town";
        int port = int.TryParse(ArgumentValue(args, "--port"), out int parsed) ? parsed : DefaultPort;

        if (!File.Exists(worldPath))
        {
            Console.Error.WriteLine($"No world file at {Path.GetFullPath(worldPath)}.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Generate one from your own cartridge:");
            Console.Error.WriteLine("  dotnet run --project src/Tools/RomDump -- your.gba --export-world world.dat");
            return 1;
        }

        WorldData world = WorldData.Load(worldPath);
        GameWorld game;

        try
        {
            game = new GameWorld(world, startingMap);
        }
        catch (ArgumentException)
        {
            // Naming a map that is not there is an easy mistake and a stack trace is
            // no help; show what is actually available instead.
            Console.Error.WriteLine($"No map matching '{startingMap}' in {worldPath}.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Some that are:");

            foreach (MapData map in world.Maps
                         .OrderByDescending(m => m.Width * m.Height)
                         .DistinctBy(m => m.Name)
                         .Take(10))
            {
                Console.Error.WriteLine($"  {map.Id,-8} {map.Name}");
            }

            return 1;
        }

        Console.WriteLine($"Loaded {world.Count} maps from {worldPath}");
        Console.WriteLine($"Hosting {game.Map.Name} ({game.Map.Id}) — {game.Map.Width}x{game.Map.Height}");

        await new GameServer(game).RunAsync(port);
        return 0;
    }

    private static string? ArgumentValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];

        return null;
    }
}

/// <summary>Accepts connections and fans messages out to them.</summary>
public sealed class GameServer(GameWorld world)
{
    private readonly ConcurrentDictionary<int, MessageChannel> _channels = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly TaskCompletionSource<int> _listening =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private double Now => _clock.Elapsed.TotalSeconds;

    /// <summary>
    /// Completes with the port actually bound. Passing port 0 asks the system for a
    /// free one, which is what lets tests run a real server without picking a port
    /// that might already be in use.
    /// </summary>
    public Task<int> Listening => _listening.Task;

    public async Task RunAsync(int port, CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        int boundPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        _listening.TrySetResult(boundPort);

        Console.WriteLine($"Listening on port {boundPort}. Ctrl+C to stop.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient connection = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = HandleAsync(connection, cancellationToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleAsync(TcpClient connection, CancellationToken cancellationToken)
    {
        // Movement is small and frequent; batching it would only add latency.
        connection.NoDelay = true;

        using (connection)
        await using (NetworkStream stream = connection.GetStream())
        {
            var channel = new MessageChannel(stream);
            int playerId = 0;

            try
            {
                while (await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false) is { } message)
                {
                    switch (message)
                    {
                        case JoinRequest join when playerId == 0:
                            (ServerPlayer player, List<Outgoing> welcome) = world.Join(join.Name);
                            playerId = player.Id;

                            // Registered only after the world knows about them, so no
                            // broadcast can reach a half-joined connection.
                            _channels[playerId] = channel;

                            Console.WriteLine($"+ {player.Name} (#{player.Id}) at {player.Square}, {world.PlayerCount} online");
                            await DispatchAsync(welcome, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case MoveRequest move when playerId != 0:
                            await DispatchAsync(world.Move(playerId, move.Direction, Now), playerId, cancellationToken)
                                .ConfigureAwait(false);
                            break;

                        default:
                            await channel
                                .SendAsync(new Rejected("Join first."), cancellationToken)
                                .ConfigureAwait(false);
                            break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException)
            {
                // A client vanishing mid-frame is ordinary, not an error worth a trace.
            }
            finally
            {
                if (playerId != 0)
                {
                    _channels.TryRemove(playerId, out _);
                    await DispatchAsync(world.Leave(playerId), playerId, CancellationToken.None).ConfigureAwait(false);
                    Console.WriteLine($"- #{playerId} left, {world.PlayerCount} online");
                }
            }
        }
    }

    private async Task DispatchAsync(List<Outgoing> outgoing, int sender, CancellationToken cancellationToken)
    {
        foreach (Outgoing item in outgoing)
        {
            foreach ((int id, MessageChannel channel) in _channels)
            {
                if (item.OnlyTo is { } only && only != id) continue;
                if (item.Except is { } except && except == id) continue;

                try
                {
                    await channel.SendAsync(item.Message, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
                {
                    // A send failing means that connection is gone; its own loop will
                    // clean it up. One dead client must not stop the broadcast.
                }
            }
        }
    }
}
