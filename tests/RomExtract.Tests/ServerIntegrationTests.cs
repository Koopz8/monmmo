using System.Diagnostics;
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
        long started = Stopwatch.GetTimestamp();

        var socket = new TcpClient { NoDelay = true };
        await socket.ConnectAsync("127.0.0.1", _port);

        var channel = new MessageChannel(socket.GetStream());
        await channel.SendAsync(new RegisterRequest(name, "a-good-password"));

        // HOW SLOW THIS MACHINE IS, MEASURED (304). Connecting and sending is the same work a
        // message wait is doing, so how long it took here is the honest unit for how long to wait
        // there. Kept as the WORST seen in this class, because a budget from the fastest connect
        // is a budget for a machine that is not the one running now.
        TimeSpan took = Stopwatch.GetElapsedTime(started);

        if (took > _slowest) _slowest = took;

        return new TestClient(socket, channel);
    }

    /// <summary>
    /// The longest a connect has taken in this class — the measured unit the message budget is
    /// counted in (304).
    /// </summary>
    private static TimeSpan _slowest = TimeSpan.Zero;

    /// <summary>
    /// How long to wait for one message: a large multiple of what this machine actually took to
    /// connect, and never less than the floor.
    /// </summary>
    /// <remarks>
    /// <b>A budget that has to be chosen is a budget that gets quoted as though it were
    /// measured</b> — which is what 294 through 300 spent six milestones on, in the script
    /// readings. This one was 5 seconds, then 30 at some point, then 120 at 289 "with the
    /// evidence", and it STILL fired twice in one session at 289's own hands: the container that
    /// runs the suite in 30 seconds idle runs it in 157 under a break-guard, and 120 is inside
    /// that noise.
    /// <para>
    /// So it is not chosen any more. A connect is the same socket work a message wait is, so the
    /// budget is a hundred of them — on an idle machine that is well under the floor and the floor
    /// wins; on a machine six times slower it scales with the machine. A server that never sends
    /// the message still fails, just later, which is the direction this is allowed to be wrong in.
    /// </para>
    /// </remarks>
    private static TimeSpan Budget =>
        _slowest * 100 > TheFloor ? _slowest * 100 : TheFloor;

    /// <summary>The least this will ever wait, however fast the machine looks.</summary>
    private static readonly TimeSpan TheFloor = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Reads until a message of the wanted kind arrives, or gives up. Bounded so a
    /// missing message fails the test rather than hanging the suite.
    /// </summary>
    private static async Task<T> ExpectAsync<T>(MessageChannel channel, int maxMessages = 12) where T : NetMessage
    {
        // Generous on purpose. Five seconds was the original figure and it is plenty of
        // time for a message on an idle machine — but these tests start a whole server,
        // world file and all, and when the suite runs alongside anything else the start
        // alone has taken ten. Two false failures came from that, both of them a slow
        // machine rather than a broken one.
        //
        // Nothing is hidden by the larger number: a server that never sends the message
        // still fails, just later. A test that fails when the machine is busy is worse
        // than a slow one, because it teaches everybody to re-run the suite instead of
        // reading it.
        // RAISED FROM 30 AT 289 AND FROM 120 AT 304 — and at 304 it stopped being a number at
        // all. See `Budget`: it is a hundred of this machine's own measured connects, floored at
        // thirty seconds. 289 raised it "with the evidence" and it still fired twice in one
        // session, because the evidence was a measurement of a DIFFERENT machine-load than the
        // one that fails.
        using var timeout = new CancellationTokenSource(Budget);

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
