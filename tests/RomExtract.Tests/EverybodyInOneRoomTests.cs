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
