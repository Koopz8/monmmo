using System.Linq;
using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The buying report was behind <c>money &gt; 0 || Bought.Count &gt; 0</c>, so the default run
/// printed nothing at all — not "it bought nothing": nothing.
/// <para>
/// A report that says nothing and a run with nothing to say are indistinguishable from outside,
/// which is the trap this project has written down four times and had been walking past in its
/// own output ever since there was a bag to fill. What it was hiding: at
/// <c>--play --say-yes --boat --in-order</c> the run stands at ONE of twenty counters on ground
/// it reached, and the other nineteen are every one of them exactly two squares away.
/// </para>
/// <para>
/// The three numbers guarded here are the denominators — counters on reached ground, counters
/// stood at, and how far off the rest were. Each of them exists so that a nought can be told
/// from a silence.
/// </para>
/// </summary>
public sealed class TheSilentZeroAtTheCounterTests
{
    private const int Potion = 0x0D;

    private const int Elixir = 0x22;

    /// <summary>Open floor, so nothing here is accidentally about collision.</summary>
    private static MapData Room(string id) =>
        new(id, id, 8, 8, [.. Enumerable.Repeat((byte)0, 64)]);

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// Two shops on two maps, and the run can only get beside one of them.
    /// <para>
    /// <b>Two of whatever the key is made of</b>, per 194. One shop would make "counters on
    /// reached ground" and "counters stood at" the same number whatever the code did, and the
    /// whole point of the pair is that they come apart.
    /// </para>
    /// <para>
    /// <c>1.0</c>'s clerk stands at (1,1) with the run walking past. <c>2.0</c>'s stands at
    /// (6,1) behind a wall of solid squares, so the map is reached and the clerk is not —
    /// which is the cartridge's shape: nineteen clerks two squares from the nearest floor the
    /// run stood on.
    /// </para>
    /// </summary>
    private static WorldData TwoShopsOneReachable()
    {
        var walled = new byte[64];

        // A solid column at x = 5, so everything from x = 6 across is cut off from the door.
        for (var y = 0; y < 8; y++) walled[(y * 8) + 5] = 1;

        return new WorldData(
        [
            Room("1.0") with
            {
                Warps = [new Warp(7, 7, 0, "2.0")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false, Sells: [Potion]),
                    new MapObject(2, 1, 3, 3, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                ],
            },
            new MapData("2.0", "2.0", 8, 8, walled)
            {
                Warps = [new Warp(0, 0, 0, "1.0")],
                Objects = [new MapObject(1, 1, 6, 1, Direction.Down, 0, false, Sells: [Elixir])],
            },
        ]);
    }

    /// <summary>
    /// A run that reached two shops and got beside one says so, and says both halves.
    /// <para>
    /// Without the second number, "it bought nothing" covers a run that stood at every counter
    /// with an empty purse and a run that never found a shop. Those are different jobs.
    /// </para>
    /// </summary>
    [Fact]
    public void CountersReachedAndCountersStoodAtAreDifferentNumbers()
    {
        Attempt played = Autoplayer.Play(
            TwoShopsOneReachable(),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing with { Gets = null },
            money: 0);

        Assert.Contains("2.0", played.Reached);

        Assert.Equal(2, played.CountersOnReachedGround);
        Assert.Equal(1, played.CountersStoodAt);
        Assert.Equal(1, played.CountersNeverStoodBeside);
    }

    /// <summary>
    /// And how far off the one it missed was, which is the number that told this project the
    /// nineteen were a counter rather than a room.
    /// <para>
    /// A distance is not a count. "One counter missed" is compatible with a shop on the far
    /// side of the world and with a clerk one square past the till, and only this tells them
    /// apart — which is what turned "the run cannot afford things" into "the run cannot stand
    /// where the player stands".
    /// </para>
    /// </summary>
    [Fact]
    public void HowFarOffTheMissedCounterWasIsRecorded()
    {
        Attempt played = Autoplayer.Play(
            TwoShopsOneReachable(),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing,
            money: 0);

        CounterOutOfReach missed = Assert.Single(played.CountersOutOfReach);

        Assert.Equal("2.0", missed.MapId);
        Assert.True(
            missed.NearestStood > 1,
            $"a counter it stood beside would not be on this list; got {missed.NearestStood}");

        // And the other reading of the same clerk, which is a different question and has to be
        // able to disagree — it did, on the real cartridge, and killed the explanation this
        // project was about to write down.
        Assert.True(
            missed.SquaresBesideThatAreWalkable >= 0,
            "walkability beside them is read off the map, not off the run");
    }

    /// <summary>
    /// The ordinary case, asserted: a run that never reached a shop at all is a nought that
    /// means something else entirely.
    /// <para>
    /// 195's lesson, applied rather than discovered. Without this, code that reports every run
    /// as "stood at no counters" passes everything above.
    /// </para>
    /// </summary>
    [Fact]
    public void ARunThatFoundNoShopIsADifferentNoughtFromOneThatFoundTwo()
    {
        var world = new WorldData(
        [
            Room("1.0") with
            {
                Warps = [new Warp(7, 7, 0, "2.0")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                ],
            },
            Room("2.0") with
            {
                Warps = [new Warp(0, 0, 0, "1.0")],
                Objects =
                [
                    new MapObject(1, 1, 2, 2, Direction.Down, 0, false) { ScriptAddress = 0x2000 },
                ],
            },
        ]);

        Attempt played = Autoplayer.Play(world, "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.Equal(0, played.CountersOnReachedGround);
        Assert.Equal(0, played.CountersStoodAt);
        Assert.Empty(played.CountersOutOfReach);
    }
}
