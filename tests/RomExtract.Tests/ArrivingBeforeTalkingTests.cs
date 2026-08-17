using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A map's own arrival script runs before anybody on that map is talked to.
/// <para>
/// <b>It ran last, and the order was never chosen.</b> The walk yielded every person, then the
/// triggers, then the arrival scripts — which is the order the three loops happened to be
/// written in, and an order the cartridge cannot produce. Nobody has ever talked to somebody on
/// a map they had not yet arrived on.
/// </para>
/// <para>
/// It is not a lever and it has no defensible other reading. That distinguishes it from
/// <c>--in-order</c>, <c>--boat</c> and <c>--surf</c>, each of which is a modelled choice with a
/// case on both sides.
/// </para>
/// <para>
/// PALLET TOWN is the case, and it is the whole opening of the game. The trigger north of town
/// writes ONE into <c>0x4055</c>; the lab's arrival script reads that one and writes TWO; and
/// TWO is the only number that makes the three balls hand anything over. Running the people
/// first, all three read ONE and answer "you are not ready" — and the two arrives immediately
/// after the last of them has been asked. The counter was correct for one instant with nobody
/// looking at it, and every instrument this project has could only print the number it ended on.
/// </para>
/// <para>
/// <b>Not one of 2721 tests noticed when the order changed.</b> Which is why these exist.
/// </para>
/// </summary>
public class ArrivingBeforeTalkingTests
{
    private const int Counter = 0x4055;
    private const int Ready = 2;
    private const int TookIt = 0x0400;

    private const uint OnArrival = 0x1000;
    private const uint TheBall = 0x2000;

    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// One map: an arrival script that advances the counter, and one person who hands something
    /// over only while the counter is on the number the arrival script puts there.
    /// </summary>
    /// <remarks>
    /// The memory is a dictionary the stand-in writes and reads, which is exactly how the real
    /// one works — the reader and the walk share one object, so a number a scene leaves is
    /// visible to the very next scene.
    /// </remarks>
    private static Attempt Run(out IReadOnlyDictionary<int, int> memory)
    {
        var remembered = new Dictionary<int, int>();

        memory = remembered;

        MapData start = Room("1.0") with
        {
            OnEntry = [new MapEntryScript(0, 0, OnArrival)],
            Objects =
            [
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = TheBall },
            ],
        };

        return Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, _) =>
            {
                if (address == OnArrival)
                {
                    int was = remembered.GetValueOrDefault(Counter);

                    remembered[Counter] = Ready;

                    return Nothing with { Touched = [new VariableTouch(Counter, true, was, Ready)] };
                }

                if (address != TheBall) return Nothing;

                int held = remembered.GetValueOrDefault(Counter);

                return (held == Ready ? Nothing with { FlagsSet = [TookIt] } : Nothing) with
                {
                    Touched = [new VariableTouch(Counter, false, held, Ready)],
                };
            });
    }

    /// <summary>
    /// The finding, at the size that matters: the person reads the number the arrival script
    /// left, and hands the thing over.
    /// </summary>
    [Fact]
    public void ThePersonReadsWhatTheArrivalScriptLeft()
    {
        Attempt played = Run(out _);

        Assert.Contains(TookIt, played.Flags);
    }

    /// <summary>
    /// And in the trace, in order — the arrival script's write before the person's read, and the
    /// read holding what the write put there.
    /// <para>
    /// The order is asserted rather than the final value on purpose. A dictionary of what each
    /// variable ended up holding says two either way, and cannot tell the run that worked from
    /// the run that read one and then wrote two a moment too late.
    /// </para>
    /// </summary>
    [Fact]
    public void AndTheTraceShowsTheWriteBeforeTheRead()
    {
        Attempt played = Run(out _);

        int wrote = played.Trace.ToList().FindIndex(t => t.What.Wrote);
        int looked = played.Trace.ToList().FindIndex(t => !t.What.Wrote);

        Assert.True(
            wrote >= 0 && looked >= 0 && wrote < looked,
            $"the write is at {wrote} and the read at {looked}, so the person was talked to "
            + "before the map was arrived on");

        Assert.Equal(Ready, played.Trace[looked].What.Held);
    }

    /// <summary>
    /// A read is recorded at all, which is the half <c>--who-writes</c> and
    /// <c>VariablesWritten</c> both throw away.
    /// </summary>
    [Fact]
    public void AReadIsRecordedAsWellAsAWrite()
    {
        Attempt played = Run(out _);

        Assert.Contains(played.Trace, t => !t.What.Wrote);
        Assert.Contains(played.Trace, t => t.What.Wrote);
    }

    /// <summary>
    /// And the trace says where and when, not just what. A list of values in order is a list
    /// nobody can act on: the finding it produced was "the write is on the map's own arrival
    /// script and the read is person five", and neither half of that is a number.
    /// </summary>
    [Fact]
    public void AndSaysWhereAndWhenEachOneHappened()
    {
        Attempt played = Run(out _);

        Traced read = played.Trace.First(t => !t.What.Wrote);

        Assert.Equal("1.0", read.MapId);
        Assert.Equal(1, read.LocalId);
        Assert.Equal(TheBall, read.Address);
        Assert.True(read.Pass >= 1, "a touch with no pass number cannot be placed in the run");
    }

    /// <summary>
    /// A trace that fills up says how much it dropped.
    /// <para>
    /// A silent cap reads as "that is all that happened", which is the exact failure this
    /// project has spent a session finding in its own output — a number that is quietly not the
    /// whole number and looks identical to one that is.
    /// </para>
    /// </summary>
    [Fact]
    public void AFullTraceSaysHowMuchItDropped()
    {
        int tooMany = Autoplayer.MostTraced + 50;

        MapData start = Room("1.0") with { OnEntry = [new MapEntryScript(0, 0, OnArrival)] };

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing with
            {
                Touched = [.. Enumerable.Range(0, tooMany).Select(n => new VariableTouch(Counter, true, n, n + 1))],
            });

        Assert.Equal(Autoplayer.MostTraced, played.Trace.Count);
        Assert.True(
            played.TraceDropped >= 50,
            $"it dropped {played.TraceDropped}, so the overflow is invisible");
    }
}
