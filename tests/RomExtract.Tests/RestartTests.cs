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

        return new GameWorld(
            new WorldData([new MapData("3.0", "PALLET TOWN", 4, 3, collision)]), "3.0", TestRules.All);
    }

    /// <summary>Runs a server for the duration of one block of work, then stops it.</summary>
    private async Task WithServerAsync(Func<int, SqlitePlayerStore, Task> body)
    {
        using var shutdown = new CancellationTokenSource();
        using var store = new SqlitePlayerStore(_databasePath);

        var server = new GameServer(World(), store);
        _ = server.RunAsync(0, shutdown.Token);

        int port = await server.Listening;

        try
        {
            await body(port, store);
        }
        finally
        {
            await shutdown.CancelAsync();
        }
    }

    /// <summary>
    /// Waits for a saved character to satisfy something, rather than sleeping and
    /// hoping.
    /// <para>
    /// A fixed delay is a guess about how busy the machine is. These tests used one and
    /// it held until the server grew a clock of its own — then several servers ticking
    /// at once made the guess wrong, and two tests that had nothing to do with the
    /// change started failing only when run alongside everything else.
    /// </para>
    /// </summary>
    private static async Task<SavedCharacter> WaitForSaveAsync(
        SqlitePlayerStore store, Func<SavedCharacter, bool> until, string what)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (await store.LoginAsync("Mason", "a-good-password") is AuthOutcome.Success success &&
                until(success.Character))
            {
                return success.Character;
            }

            await Task.Delay(50);
        }

        throw new InvalidOperationException($"The save never {what}.");
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
    public async Task YourPartySurvivesARestart()
    {
        SavedMon starter = null!;

        await WithServerAsync(async (port, store) =>
        {
            using var socket = new TcpClient { NoDelay = true };
            await socket.ConnectAsync("127.0.0.1", port);

            var channel = new MessageChannel(socket.GetStream());
            await channel.SendAsync(new RegisterRequest("Mason", "a-good-password"));

            Welcome welcome = await ExpectAsync<Welcome>(channel);

            // Registration hands out a starter, so a party is never empty and the
            // server never has to invent a battler in the middle of a battle.
            starter = Assert.Single(welcome.Party);

            socket.Close();
            await WaitForSaveAsync(store, c => c.Party.Count == 1, "held a party");
        });

        // Everything above is gone now: the server, the world, the connection. Only
        // the file remains.
        await WithServerAsync(async (port, _) =>
        {
            using var socket = new TcpClient { NoDelay = true };
            await socket.ConnectAsync("127.0.0.1", port);

            var channel = new MessageChannel(socket.GetStream());
            await channel.SendAsync(new LoginRequest("Mason", "a-good-password"));

            Welcome welcome = await ExpectAsync<Welcome>(channel);

            Assert.Equal(starter, Assert.Single(welcome.Party));
        });
    }

    [Fact]
    public async Task WhereYouStoodIsWhereYouComeBack()
    {
        int x = 0, y = 0;

        await WithServerAsync(async (port, store) =>
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

            int wantedX = x, wantedY = y;
            await WaitForSaveAsync(store, c => c.X == wantedX && c.Y == wantedY, "recorded the new square");
        });

        await WithServerAsync(async (port, _) =>
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
        await WithServerAsync(async (port, _) =>
        {
            using var socket = new TcpClient { NoDelay = true };
            await socket.ConnectAsync("127.0.0.1", port);

            var channel = new MessageChannel(socket.GetStream());
            await channel.SendAsync(new RegisterRequest("Mason", "a-good-password"));

            await ExpectAsync<Welcome>(channel);
        });

        await WithServerAsync(async (port, _) =>
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
