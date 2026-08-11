using System.Net.Sockets;
using PokeMmo.Core.Net;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// That a login is answered with a welcome, and answered with it <em>first</em>.
/// <para>
/// The client reads exactly one message to decide whether it is logged in. Anything
/// that arrives in front of the welcome is reported to the player as a failure, however
/// well the login actually went — which is what a wandering person on the starting map
/// managed to do, five times a second, from the moment the world grew a clock.
/// </para>
/// <para>
/// <b>This test is a net, not a proof, and it is worth being straight about that.</b> It
/// caught the bug once, on its first run, with the old ordering in place. It does not
/// reliably tell the two orderings apart: the window is a fraction of a millisecond and
/// hitting it is luck. Attempts to make it deterministic — more accounts, all at once,
/// a busier map — reproduced nothing and only made it slower.
/// </para>
/// <para>
/// What actually defends this is the order of four lines in the server: the player's own
/// messages are written to the socket <em>before</em> the channel is registered, so there
/// is no window for a broadcast to arrive in. The test is here because a login that
/// answers with anything else is worth failing over, not because it can prove the window
/// is shut.
/// </para>
/// </summary>
public class FirstMessageTests
{
    [Fact]
    public async Task ALoginIsAnsweredWithAWelcomeAndNothingInFrontOfIt()
    {
        // A crowd, because the race needs somebody to be moving in the window between
        // a channel being registered and its welcome being written. One wanderer makes
        // this pass by luck; sixteen of them make it fail by arithmetic.
        MapObject[] crowd = Enumerable.Range(1, 16)
            .Select(i => new MapObject(i, 5, i % 8, i / 8, Direction.Down, 2, false, 3, 3))
            .ToArray();

        var map = new MapData("3.0", "PALLET TOWN", 8, 8, new byte[64]) { Objects = crowd };

        var world = new GameWorld(new WorldData([map]), "3.0", TestRules.All);

        using var shutdown = new CancellationTokenSource();
        using var store = SqlitePlayerStore.InMemory();

        var server = new GameServer(world, store);
        _ = server.RunAsync(0, shutdown.Token);

        int port = await server.Listening;

        // All at once, on purpose. One login is one roll of the dice — the window this
        // used to lose in is a fraction of a millisecond wide — but a crowd of them
        // arriving together means every join after the first happens while the world is
        // already broadcasting somebody's step down these very sockets.
        var sockets = new List<TcpClient>();

        try
        {
            NetMessage?[] firsts = await Task.WhenAll(Enumerable.Range(0, 8).Select(async account =>
            {
                var socket = new TcpClient { NoDelay = true };

                lock (sockets) sockets.Add(socket);

                await socket.ConnectAsync("127.0.0.1", port);

                var channel = new MessageChannel(socket.GetStream());
                await channel.SendAsync(new RegisterRequest($"Koop{account}", "a-good-password"));

                return await channel.ReceiveAsync(new CancellationTokenSource(60_000).Token);
            }));

            Assert.All(firsts, first => Assert.IsType<Welcome>(first));
        }
        finally
        {
            foreach (TcpClient socket in sockets) socket.Dispose();
            await shutdown.CancelAsync();
        }
    }
}
