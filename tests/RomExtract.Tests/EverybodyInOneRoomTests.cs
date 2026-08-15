using PokeMmo.Core.Net;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What one person moving costs everybody else.
/// <para>
/// The crowd tool's report named this the moment the door stopped being the wall: fifty
/// messages a second, per player, because everybody was on one map and every step was
/// told to everybody on it. That much is the game — people on a map can see each other.
/// What was not the game was <em>how</em> it was told to them.
/// </para>
/// <para>
/// Dispatch walked every connection on the server for every message and asked the world
/// where each one was, and that question took the server's single global lock. A hundred
/// people stepping once a second on one map is ten thousand lock acquisitions a second,
/// every one of them contending with the world's own clock. At a thousand it is a
/// million. The cost of one person moving grew with the number of people who existed.
/// </para>
/// <para>
/// So the world keeps an index of who is standing where, outside the lock, written only
/// where somebody joins, leaves or changes map — three places, all of them already inside
/// it. Reads are free, writes stay serialised, and a message aimed at a map now goes to
/// the people who are on it rather than to a filter over everybody who exists.
/// </para>
/// </summary>
public class EverybodyInOneRoomTests
{
    private const string Bedroom = "1.0";
    private const string Street = "1.1";

    private static GameWorld TwoMaps()
    {
        MapData bedroom = new(Bedroom, "BEDROOM", 4, 4, new byte[16]);
        MapData street = new(Street, "STREET", 4, 4, new byte[16]);

        return new GameWorld(new WorldData([bedroom, street]), Bedroom, TestRules.All);
    }

    [Fact]
    public void SomebodyWhoJoinsIsOnTheMapTheyJoinedOn()
    {
        GameWorld world = TwoMaps();

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        Assert.Equal(Bedroom, world.MapIdOf(player.Id));
        Assert.Contains(player.Id, world.WhoIsOn(Bedroom));
        Assert.Empty(world.WhoIsOn(Street));
    }

    [Fact]
    public void AndEverybodyOnItIsOnIt()
    {
        GameWorld world = TwoMaps();

        (ServerPlayer one, _) = world.Join(1, "One", world.FreshCharacter());
        (ServerPlayer two, _) = world.Join(2, "Two", world.FreshCharacter());

        Assert.Equal([one.Id, two.Id], world.WhoIsOn(Bedroom).OrderBy(id => id));
    }

    /// <summary>And somebody who leaves is nowhere, rather than still being told things.</summary>
    [Fact]
    public void AndSomebodyWhoLeavesIsNowhere()
    {
        GameWorld world = TwoMaps();

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        world.Leave(player.Id);

        Assert.Null(world.MapIdOf(player.Id));
        Assert.Empty(world.WhoIsOn(Bedroom));
    }

    /// <summary>
    /// And a map nobody is on is not a map at all, so the list of places worth simulating
    /// is the size of the crowd and not the size of the world.
    /// </summary>
    [Fact]
    public void AndAnEmptyMapIsNotAPlaceAnybodyIs()
    {
        GameWorld world = TwoMaps();

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        Assert.Equal([Bedroom], world.MapsWithAnybodyOn);

        world.Leave(player.Id);

        Assert.Empty(world.MapsWithAnybodyOn);
    }

    /// <summary>Somebody nobody has ever heard of is nowhere, and asking is not an error.</summary>
    [Fact]
    public void AndSomebodyWhoWasNeverHereIsNowhere()
    {
        GameWorld world = TwoMaps();

        Assert.Null(world.MapIdOf(404));
        Assert.Empty(world.WhoIsOn("no.such.map"));
    }
}

/// <summary>
/// One player's post, and why the world does not stop for it.
/// <para>
/// A broadcast used to await each send in turn, which is a rule saying the slowest socket
/// on the server decides how fast everybody else hears anything. One player whose machine
/// has stopped reading fills their kernel buffer and every other person on that map waits
/// behind them — including the world's clock, which is what moves the people on it.
/// </para>
/// </summary>
public class ThePostTests
{
    private static Outbox Posting(Stream to) => new(new MessageChannel(to), CancellationToken.None);

    /// <summary>What is posted arrives, in the order it was posted.</summary>
    [Fact]
    public async Task WhatIsPostedArrivesInOrder()
    {
        var both = new BlockingPipe();

        using Outbox outbox = Posting(both.Writing);

        for (int step = 0; step < 5; step++) Assert.True(outbox.Post(new PlayerMoved(7, step, 0, Direction.Down)));

        var reading = new MessageChannel(both.Reading);

        for (int step = 0; step < 5; step++)
        {
            var moved = Assert.IsType<PlayerMoved>(await reading.ReceiveAsync());

            Assert.Equal(step, moved.X);
        }
    }

    /// <summary>
    /// And posting does not wait for the socket. This is the whole point: the write here
    /// goes nowhere until something reads it, and the caller is not held up by that.
    /// </summary>
    [Fact]
    public void AndPostingDoesNotWaitForTheSocket()
    {
        using Outbox outbox = Posting(new Wall());

        // Nothing is reading the other end. Every one of these still returns at once.
        for (int step = 0; step < 50; step++) outbox.Post(new PlayerMoved(7, step, 0, Direction.Down));

        Assert.True(outbox.Dropped < Outbox.Room);
    }

    /// <summary>
    /// And somebody who stops reading altogether is let go rather than kept, because a
    /// client that is quietly missing messages is worse than one that is disconnected.
    /// </summary>
    [Fact]
    public void AndSomebodyWhoStopsReadingIsLetGo()
    {
        using Outbox outbox = Posting(new Wall());

        bool refused = false;

        for (int step = 0; step < Outbox.Room * 4 && !refused; step++)
            refused = !outbox.Post(new PlayerMoved(7, step, 0, Direction.Down));

        Assert.True(refused);
        Assert.True(outbox.HasFallenBehind);

        // And once let go, nothing more is queued for them at all.
        Assert.False(outbox.Post(new PlayerMoved(7, 0, 0, Direction.Down)));
    }
}

/// <summary>
/// A socket that has stopped taking anything at all: every write waits for ever, which
/// is what a full kernel buffer looks like from inside this server.
/// </summary>
public sealed class Wall : Stream
{
    private readonly ManualResetEventSlim _never = new(false);

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override void Write(byte[] buffer, int offset, int count) => _never.Wait();

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(Task.Delay(Timeout.Infinite, cancellationToken));

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}

/// <summary>
/// A pipe whose far end nobody is reading, for measuring what happens to whoever writes
/// into it. Two streams over one buffer, with the reader deliberately absent unless a
/// test asks for it.
/// </summary>
public sealed class BlockingPipe
{
    private readonly Pipe _pipe = new();

    public Stream Writing => _pipe.Writer;

    public Stream Reading => _pipe.Reader;

    /// <summary>The buffer, and the two ends that share it.</summary>
    private sealed class Pipe
    {
        private readonly Queue<byte> _bytes = new();
        private readonly SemaphoreSlim _arrived = new(0);

        public Stream Writer { get; }

        public Stream Reader { get; }

        public Pipe()
        {
            Writer = new End(this, writing: true);
            Reader = new End(this, writing: false);
        }

        private sealed class End(Pipe pipe, bool writing) : Stream
        {
            public override bool CanRead => !writing;
            public override bool CanSeek => false;
            public override bool CanWrite => writing;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                pipe._arrived.Wait();

                lock (pipe._bytes)
                {
                    int taken = 0;

                    while (taken < count && pipe._bytes.Count > 0) buffer[offset + taken++] = pipe._bytes.Dequeue();

                    // One wait per byte would be exact and slow; this keeps the count
                    // honest for however many were actually taken.
                    for (int more = 1; more < taken; more++) pipe._arrived.Wait(0);

                    return taken;
                }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                lock (pipe._bytes)
                    for (int at = 0; at < count; at++) pipe._bytes.Enqueue(buffer[offset + at]);

                pipe._arrived.Release(count);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}

/// <summary>
/// How far somebody can see, and who therefore has to be told when anybody moves.
/// <para>
/// The map was the unit of sight, which is right about what a player sees and wrong
/// about what it costs: four hundred people who can all see each other, stepping twice a
/// second, is a hundred and sixty thousand sightings a second, and no index makes that
/// number smaller. It is quadratic inside one map, and the only thing that changes it is
/// a circle.
/// </para>
/// <para>
/// The radius comes off the client's own viewport rather than being picked: 960 pixels at
/// three times life size is twenty squares across, half of that is ten, and one square of
/// margin means nobody arrives on screen out of nothing.
/// </para>
/// </summary>
public class HowFarSomebodyCanSeeTests
{
    private const string Field = "1.0";

    private static GameWorld OneBigField()
    {
        MapData field = new(Field, "FIELD", 64, 64, new byte[64 * 64]);

        return new GameWorld(new WorldData([field]), Field, TestRules.All);
    }

    private static ServerPlayer At(GameWorld world, int account, string name, int x, int y)
    {
        (ServerPlayer player, _) = world.Join(account, name, world.FreshCharacter() with { X = x, Y = y });

        return player;
    }

    /// <summary>The radius is the screen's, and it is bigger than half a screen.</summary>
    [Fact]
    public void TheRadiusComesOffTheScreen()
    {
        Assert.Equal(20, Sight.SquaresAcross);
        Assert.Equal(13, Sight.SquaresDown);
        Assert.Equal(11, Sight.Squares);
    }

    /// <summary>Somebody standing on you is somebody you can see; the far corner is not.</summary>
    [Fact]
    public void AndItIsTheShapeOfAScreen()
    {
        Assert.True(Sight.CanSee(new GridPosition(10, 10), new GridPosition(10, 10)));

        // The corner of the box, which a straight-line rule would have excluded.
        Assert.True(Sight.CanSee(new GridPosition(0, 0), new GridPosition(Sight.Squares, Sight.Squares)));

        Assert.False(Sight.CanSee(new GridPosition(0, 0), new GridPosition(Sight.Squares + 1, 0)));
    }

    /// <summary>Joining tells you about the people near you, and nobody else.</summary>
    [Fact]
    public void JoiningTellsYouAboutThePeopleNearYou()
    {
        GameWorld world = OneBigField();

        ServerPlayer near = At(world, 1, "Near", 4, 4);
        ServerPlayer far = At(world, 2, "Far", 60, 60);

        (ServerPlayer joining, List<Outgoing> send) =
            world.Join(3, "Joining", world.FreshCharacter() with { X = 5, Y = 5 });

        List<int> told =
        [
            .. send
                .Where(o => o.OnlyTo == joining.Id && o.Message is PlayerAppeared)
                .Select(o => ((PlayerAppeared)o.Message).PlayerId)
        ];

        Assert.Contains(near.Id, told);
        Assert.DoesNotContain(far.Id, told);
    }

    /// <summary>
    /// And walking out of somebody's sight tells them so, in the same words a disconnect
    /// uses — a client that knows how to forget somebody needs no new case for somebody
    /// who has simply walked far enough away.
    /// </summary>
    [Fact]
    public void AndWalkingOutOfSightSaysSo()
    {
        GameWorld world = OneBigField();

        ServerPlayer watcher = At(world, 1, "Watcher", 4, 20);
        ServerPlayer walker = At(world, 2, "Walker", 4 + Sight.Squares, 20);

        // One step further away crosses the edge of what the watcher can see.
        List<Outgoing> send = Walk(world, walker, Direction.Right);

        Assert.Contains(send, o =>
            o.OnlyTo == watcher.Id && o.Message is PlayerLeft left && left.PlayerId == walker.Id);

        Assert.Contains(send, o =>
            o.OnlyTo == walker.Id && o.Message is PlayerLeft gone && gone.PlayerId == watcher.Id);
    }

    /// <summary>And walking back into it says that too.</summary>
    [Fact]
    public void AndWalkingBackIntoItSaysThatToo()
    {
        GameWorld world = OneBigField();

        ServerPlayer watcher = At(world, 1, "Watcher", 4, 20);
        ServerPlayer walker = At(world, 2, "Walker", 5 + Sight.Squares, 20);

        List<Outgoing> send = Walk(world, walker, Direction.Left);

        Assert.Contains(send, o =>
            o.OnlyTo == watcher.Id && o.Message is PlayerAppeared seen && seen.PlayerId == walker.Id);

        Assert.Contains(send, o =>
            o.OnlyTo == walker.Id && o.Message is PlayerAppeared back && back.PlayerId == watcher.Id);
    }

    /// <summary>
    /// And an ordinary step is about the square it ends on, so the people who cannot see
    /// that square are not written to at all. This is the whole saving.
    /// </summary>
    [Fact]
    public void AndAnOrdinaryStepIsAboutTheSquareItEndsOn()
    {
        GameWorld world = OneBigField();

        At(world, 1, "Watcher", 4, 20);

        ServerPlayer walker = At(world, 2, "Walker", 10, 20);

        Outgoing step = Assert.Single(
            Walk(world, walker, Direction.Right).Where(o => o.Message is PlayerMoved));

        Assert.Equal(new GridPosition(11, 20), step.Near);
    }

    private static List<Outgoing> Walk(GameWorld world, ServerPlayer player, Direction way)
    {
        // One step. Facing and moving are the same message here, so a step in a new
        // direction is still a step — which two calls made two squares, and cost the
        // first draft of these tests an hour.
        return world.Move(player.Id, way, 100);
    }
}
