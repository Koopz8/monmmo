using System.Net.Sockets;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The claim this milestone is actually making: something caught is still there after
/// the server stops.
/// <para>
/// Proved over a real socket, through a real database file, across two server
/// processes' worth of lifetime. Every piece of it is covered in isolation elsewhere;
/// this is the one that would notice if they were covered but not connected.
/// </para>
/// </summary>
public class RestartTests : IDisposable
{
    private readonly string _databasePath = TempDatabase.Path();

    private static GameWorld World()
    {
        var collision = new byte[12];
        collision[2] = 1;   // a wall at (2, 0)

        return new GameWorld(new WorldData([new MapData("3.0", "PALLET TOWN", 4, 3, collision)]), "3.0");
    }

    /// <summary>Runs a server for the duration of one block of work, then stops it.</summary>
    private async Task WithServerAsync(Func<int, Task> body)
    {
        using var shutdown = new CancellationTokenSource();
        using var store = new SqlitePlayerStore(_databasePath);

        var server = new GameServer(World(), store);
        _ = server.RunAsync(0, shutdown.Token);

        int port = await server.Listening;

        try
        {
            await body(port);
        }
        finally
        {
            await shutdown.CancelAsync();
        }
    }

    private static async Task<T> ExpectAsync<T>(MessageChannel channel, int maxMessages = 12) where T : NetMessage
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        for (int i = 0; i < maxMessages; i++)
        {
            NetMessage? message = await channel.ReceiveAsync(timeout.Token);
            if (message is null) break;
            if (message is T wanted) return wanted;
        }

        throw new InvalidOperationException($"Never received a {typeof(T).Name}.");
    }

    [Fact]
    public async Task ACaughtCreatureSurvivesARestart()
    {
        var caught = new SavedMon(16, 3, null, 9, StatusCondition.None, Nature.Bold, [33, 45]);

        await WithServerAsync(async port =>
        {
            using var socket = new TcpClient { NoDelay = true };
            await socket.ConnectAsync("127.0.0.1", port);

            var channel = new MessageChannel(socket.GetStream());
            await channel.SendAsync(new RegisterRequest("Mason", "a-good-password"));

            Welcome welcome = await ExpectAsync<Welcome>(channel);
            Assert.Empty(welcome.Party);

            await channel.SendAsync(new SaveRequest(19, [caught]));

            // Give the save a moment to land before pulling the socket out from under
            // it — the client sends and forgets, deliberately.
            await Task.Delay(300);
        });

        // Everything above is gone now: the server, the world, the connection. Only
        // the file remains.
        await WithServerAsync(async port =>
        {
            using var socket = new TcpClient { NoDelay = true };
            await socket.ConnectAsync("127.0.0.1", port);

            var channel = new MessageChannel(socket.GetStream());
            await channel.SendAsync(new LoginRequest("Mason", "a-good-password"));

            Welcome welcome = await ExpectAsync<Welcome>(channel);

            Assert.Equal(19, welcome.Balls);
            Assert.Equal(caught, Assert.Single(welcome.Party));
        });
    }

    [Fact]
    public async Task WhereYouStoodIsWhereYouComeBack()
    {
        int x = 0, y = 0;

        await WithServerAsync(async port =>
        {
            using var socket = new TcpClient { NoDelay = true };
            await socket.ConnectAsync("127.0.0.1", port);

            var channel = new MessageChannel(socket.GetStream());
            await channel.SendAsync(new RegisterRequest("Mason", "a-good-password"));

            Welcome welcome = await ExpectAsync<Welcome>(channel);
            await channel.SendAsync(new MoveRequest(Direction.Down));

            PlayerMoved moved = await ExpectAsync<PlayerMoved>(channel);
            (x, y) = (moved.X, moved.Y);

            Assert.NotEqual((welcome.X, welcome.Y), (x, y));

            // The disconnect itself is what writes this one — there is no save request.
            socket.Close();
            await Task.Delay(300);
        });

        await WithServerAsync(async port =>
        {
            using var socket = new TcpClient { NoDelay = true };
            await socket.ConnectAsync("127.0.0.1", port);

            var channel = new MessageChannel(socket.GetStream());
            await channel.SendAsync(new LoginRequest("Mason", "a-good-password"));

            Welcome welcome = await ExpectAsync<Welcome>(channel);

            Assert.Equal((x, y), (welcome.X, welcome.Y));
            Assert.Equal(Direction.Down, welcome.Facing);
        });
    }

    [Fact]
    public async Task TheSameNameCannotBeRegisteredOnASecondRun()
    {
        await WithServerAsync(async port =>
        {
            using var socket = new TcpClient { NoDelay = true };
            await socket.ConnectAsync("127.0.0.1", port);

            var channel = new MessageChannel(socket.GetStream());
            await channel.SendAsync(new RegisterRequest("Mason", "a-good-password"));

            await ExpectAsync<Welcome>(channel);
        });

        await WithServerAsync(async port =>
        {
            using var socket = new TcpClient { NoDelay = true };
            await socket.ConnectAsync("127.0.0.1", port);

            var channel = new MessageChannel(socket.GetStream());
            await channel.SendAsync(new RegisterRequest("Mason", "another-password"));

            AuthFailed refused = await ExpectAsync<AuthFailed>(channel);
            Assert.Contains("taken", refused.Reason);

            // And the connection is still usable, so the player can simply log in.
            await channel.SendAsync(new LoginRequest("Mason", "a-good-password"));
            await ExpectAsync<Welcome>(channel);
        });
    }

    public void Dispose() => TempDatabase.Delete(_databasePath);
}
