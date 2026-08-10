using System.Net.Sockets;
using PokeMmo.Core.Net;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Runs a real server on a real socket and connects real clients to it.
/// <para>
/// The world's rules are covered without a network elsewhere; what these prove is the
/// part that only breaks once sockets are involved — that a join is visible to
/// everyone already connected, that movement reaches other players, and that a
/// disconnect is noticed.
/// </para>
/// </summary>
public class ServerIntegrationTests : IAsyncLifetime
{
    private readonly CancellationTokenSource _shutdown = new();
    private GameServer _server = null!;
    private InMemoryPlayerStore _store = null!;
    private int _port;

    public async Task InitializeAsync()
    {
        var collision = new byte[12];
        collision[2] = 1;   // a wall at (2, 0)

        var world = new GameWorld(
            new WorldData([new MapData("3.0", "PALLET TOWN", 4, 3, collision)]),
            "3.0");

        _store = new InMemoryPlayerStore();
        _server = new GameServer(world, _store);

        // Port 0 asks the system for a free port, so tests never collide.
        _ = _server.RunAsync(0, _shutdown.Token);
        _port = await _server.Listening;
    }

    public Task DisposeAsync()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>A connected client, holding the socket open for the test's lifetime.</summary>
    private sealed record TestClient(TcpClient Socket, MessageChannel Channel) : IDisposable
    {
        public void Dispose() => Socket.Dispose();
    }

    private async Task<TestClient> ConnectAsync(string name)
    {
        var socket = new TcpClient { NoDelay = true };
        await socket.ConnectAsync("127.0.0.1", _port);

        var channel = new MessageChannel(socket.GetStream());
        await channel.SendAsync(new RegisterRequest(name, "a-good-password"));

        return new TestClient(socket, channel);
    }

    /// <summary>
    /// Reads until a message of the wanted kind arrives, or gives up. Bounded so a
    /// missing message fails the test rather than hanging the suite.
    /// </summary>
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
    public async Task AJoiningPlayerIsWelcomedWithAPositionInTheWorld()
    {
        using TestClient client = await ConnectAsync("Mason");
        Welcome welcome = await ExpectAsync<Welcome>(client.Channel);

        Assert.Equal("3.0", welcome.MapId);
        Assert.True(welcome.PlayerId > 0);
        Assert.InRange(welcome.X, 0, 3);
        Assert.InRange(welcome.Y, 0, 2);
    }

    [Fact]
    public async Task AnExistingPlayerSeesSomebodyElseArrive()
    {
        using TestClient first = await ConnectAsync("First");
        await ExpectAsync<Welcome>(first.Channel);

        using TestClient second = await ConnectAsync("Second");
        await ExpectAsync<Welcome>(second.Channel);

        PlayerAppeared appeared = await ExpectAsync<PlayerAppeared>(first.Channel);
        Assert.Equal("Second", appeared.Name);
    }

    [Fact]
    public async Task ANewcomerIsToldAboutPlayersAlreadyPresent()
    {
        using TestClient first = await ConnectAsync("First");
        await ExpectAsync<Welcome>(first.Channel);

        using TestClient second = await ConnectAsync("Second");
        await ExpectAsync<Welcome>(second.Channel);

        PlayerAppeared existing = await ExpectAsync<PlayerAppeared>(second.Channel);
        Assert.Equal("First", existing.Name);
    }

    [Fact]
    public async Task OnePlayerWalkingIsVisibleToAnother()
    {
        // The whole point of the milestone: two clients, one world, movement crossing
        // between them over a real socket.
        using TestClient first = await ConnectAsync("First");
        Welcome firstWelcome = await ExpectAsync<Welcome>(first.Channel);

        using TestClient second = await ConnectAsync("Second");
        await ExpectAsync<Welcome>(second.Channel);
        await ExpectAsync<PlayerAppeared>(second.Channel);

        await first.Channel.SendAsync(new MoveRequest(Direction.Down));

        PlayerMoved moved = await ExpectAsync<PlayerMoved>(second.Channel);

        Assert.Equal(firstWelcome.PlayerId, moved.PlayerId);
        Assert.Equal(Direction.Down, moved.Facing);
        Assert.Equal(firstWelcome.Y + 1, moved.Y);
    }

    [Fact]
    public async Task TheMoverIsAlsoToldWhereTheServerThinksTheyAre()
    {
        using TestClient client = await ConnectAsync("Mason");
        Welcome welcome = await ExpectAsync<Welcome>(client.Channel);

        await client.Channel.SendAsync(new MoveRequest(Direction.Down));
        PlayerMoved moved = await ExpectAsync<PlayerMoved>(client.Channel);

        Assert.Equal(welcome.PlayerId, moved.PlayerId);
    }

    [Fact]
    public async Task DisconnectingTellsTheOtherPlayers()
    {
        using TestClient watcher = await ConnectAsync("Watcher");
        await ExpectAsync<Welcome>(watcher.Channel);

        TestClient leaver = await ConnectAsync("Leaver");
        Welcome leaverWelcome = await ExpectAsync<Welcome>(leaver.Channel);
        await ExpectAsync<PlayerAppeared>(watcher.Channel);

        leaver.Dispose();

        PlayerLeft left = await ExpectAsync<PlayerLeft>(watcher.Channel);
        Assert.Equal(leaverWelcome.PlayerId, left.PlayerId);
    }

    [Fact]
    public async Task MovingBeforeJoiningIsRefused()
    {
        var socket = new TcpClient { NoDelay = true };
        await socket.ConnectAsync("127.0.0.1", _port);

        using (socket)
        {
            var channel = new MessageChannel(socket.GetStream());
            await channel.SendAsync(new MoveRequest(Direction.Up));

            Rejected rejected = await ExpectAsync<Rejected>(channel);
            Assert.Contains("Log in", rejected.Reason);
        }
    }

    [Fact]
    public async Task ManyPlayersCanBeConnectedAtOnce()
    {
        var clients = new List<TestClient>();

        try
        {
            for (int i = 0; i < 8; i++)
            {
                TestClient client = await ConnectAsync($"Player{i}");
                await ExpectAsync<Welcome>(client.Channel, maxMessages: 24);
                clients.Add(client);
            }

            Assert.Equal(8, clients.Count);
        }
        finally
        {
            foreach (TestClient client in clients) client.Dispose();
        }
    }
}
