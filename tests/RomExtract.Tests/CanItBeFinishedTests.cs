using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// How far a player can actually get by playing.
/// <para>
/// Every reach figure this project has printed is one photograph: given these flags and these
/// moves, where can somebody stand. Playing is not one photograph — you walk as far as you
/// can, talk to whoever is there, and what they do opens more world. This is that loop, run
/// until it stops opening anything, and it is what "can this game be finished" means.
/// </para>
/// <para>
/// The worlds below are small and their answers are known beforehand, which is the only way
/// to test a fixpoint: a loop that stops too early and a loop that stops correctly produce the
/// same shape of output, and the difference is a number somebody has to have known in advance.
/// </para>
/// </summary>
public class CanItBeFinishedTests
{
    /// <summary>Two rooms joined by a door, with a walkable floor.</summary>
    private static MapData Room(string id, string name = "") =>
        new(id, name.Length > 0 ? name : id, 4, 4, new byte[16]);

    private static ScriptOutcome Nothing => new([], [], [], []);

    /// <summary>
    /// A world nothing gates opens completely on the first pass, and the loop notices there
    /// is nothing more to do rather than running to its backstop.
    /// </summary>
    [Fact]
    public void AWorldNothingGatesOpensAtOnce()
    {
        var world = new WorldData([Room("1.0"), Room("1.1")]);

        Closure closed = StoryClosure.Walk(world, "1.0", (_, _) => Nothing);

        Assert.Single(closed.Rounds);
        Assert.Empty(closed.Flags);
    }

    /// <summary>
    /// Somebody standing in a doorway is a wall until a flag takes them off the map — and the
    /// loop has to run twice to see it, because the flag is set by talking to somebody who is
    /// only reachable on the first pass.
    /// <para>
    /// This is the whole shape of the story: one pass opens the next.
    /// </para>
    /// </summary>
    [Fact]
    public void OnePassOpensTheNext()
    {
        const int Opens = 0x0100;

        var talker = new MapObject(1, 1, 1, 1, Direction.Down, 0, false)
        {
            ScriptAddress = 0x1000,
        };

        MapData start = Room("1.0") with { Objects = [talker] };

        var world = new WorldData([start]);

        var passes = 0;

        Closure closed = StoryClosure.Walk(
            world,
            "1.0",
            (address, flags) =>
            {
                passes++;

                // The first time anybody talks to them, a flag moves. After that, nothing —
                // which is what makes the loop stop.
                return flags.Contains(Opens) ? Nothing : new ScriptOutcome([Opens], [], [], []);
            });

        Assert.Contains(Opens, closed.Flags);

        // Two passes: one that learned the flag and one that found nothing new.
        Assert.Equal(2, closed.Rounds.Count);
        Assert.True(passes >= 2, "the script was never run a second time, so the loop never confirmed it had finished");
    }

    /// <summary>
    /// A script nobody can stand in front of is never run.
    /// <para>
    /// <b>The rule the whole answer rests on.</b> A map counts as reached the moment one
    /// square of it is, so a person on the far side of a wall is on a map you have been to and
    /// is not somebody you can talk to. Counting those would open the entire game on the first
    /// pass and produce a confident, wrong answer — which is exactly the failure this
    /// instrument exists to avoid.
    /// </para>
    /// </summary>
    [Fact]
    public void AScriptNobodyCanStandInFrontOfIsNeverRun()
    {
        // A floor with a wall down the middle: everything at x >= 2 is unreachable.
        var collision = new byte[16];

        for (var y = 0; y < 4; y++) collision[y * 4 + 2] = 1;

        var farSide = new MapObject(1, 1, 3, 1, Direction.Down, 0, false)
        {
            ScriptAddress = 0x2000,
        };

        MapData start = new MapData("1.0", "PALLET TOWN", 4, 4, collision) { Objects = [farSide] };

        var ran = new List<uint>();

        StoryClosure.Walk(
            new WorldData([start]),
            "1.0",
            (address, _) =>
            {
                ran.Add(address);

                return Nothing;
            });

        Assert.DoesNotContain(0x2000u, ran);
    }

    /// <summary>
    /// And somebody a flag has taken off the map is not there to be talked to either.
    /// <para>
    /// The other half. A person hidden by a flag that is already set is gone, and running
    /// their script would be taking a story beat from somebody who has left.
    /// </para>
    /// </summary>
    [Fact]
    public void AndSomebodyAFlagHasRemovedIsNotThereEither()
    {
        const int Removes = 0x0100;

        var goes = new MapObject(1, 1, 1, 1, Direction.Down, 0, false)
        {
            ScriptAddress = 0x3000,
            HiddenBy = Removes,
        };

        MapData start = Room("1.0") with { Objects = [goes] };

        var ran = new List<uint>();

        StoryClosure.Walk(
            new WorldData([start]),
            "1.0",
            (address, flags) =>
            {
                ran.Add(address);

                // Talking to them once sets the flag that removes them.
                return flags.Contains(Removes) ? Nothing : new ScriptOutcome([Removes], [], [], []);
            });

        // Once, on the pass where they were still standing there. Not on the pass after.
        Assert.Single(ran);
    }

    /// <summary>
    /// Maps nobody can get to are reported, which is the answer somebody actually wants.
    /// </summary>
    [Fact]
    public void MapsNobodyCanGetToAreReported()
    {
        var world = new WorldData([Room("1.0"), Room("9.9", "INDIGO PLATEAU")]);

        Closure closed = StoryClosure.Walk(world, "1.0", (_, _) => Nothing);

        // Nothing joins them, so the second is unreachable and says so by name.
        Assert.Contains("9.9", closed.Unreached);
        Assert.DoesNotContain("1.0", closed.Unreached);
    }

    /// <summary>
    /// The routines nobody could answer are counted.
    /// <para>
    /// The error bar, and the reason this figure is a floor rather than a ceiling. A special
    /// is a call into the cartridge's own code; the runner steps over it and the answer keeps
    /// its zero, so a script that branches on it takes the zero arm. Every badge check in the
    /// game is one of those, and a reach figure printed without this number beside it would
    /// be quoted as though the world really were that small.
    /// </para>
    /// </summary>
    [Fact]
    public void TheRoutinesNobodyCouldAnswerAreCounted()
    {
        var asks = new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x4000 };

        MapData start = Room("1.0") with { Objects = [asks] };

        Closure closed = StoryClosure.Walk(
            new WorldData([start]),
            "1.0",
            (_, _) => new ScriptOutcome([], [], [], [0x1B5]));

        Assert.Equal(1, closed.Specials.Count);
        Assert.True(closed.Specials[0x1B5] >= 1);
    }

    /// <summary>
    /// A script that flips a flag back and forth still settles.
    /// <para>
    /// Worth a test because the obvious worry about a fixpoint is that it will not reach one,
    /// and the obvious worry is wrong here for a reason worth stating: the loop ends on a pass
    /// that learned nothing <em>new</em>, and clearing a flag it already knew about teaches it
    /// nothing. So the pathological case terminates in two passes rather than running to the
    /// backstop.
    /// </para>
    /// </summary>
    [Fact]
    public void AScriptThatFlipsAFlagBackAndForthStillSettles()
    {
        var flipflop = new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x5000 };

        MapData start = Room("1.0") with { Objects = [flipflop] };

        var turn = 0;

        Closure closed = StoryClosure.Walk(
            new WorldData([start]),
            "1.0",
            (_, _) => ++turn % 2 == 1
                ? new ScriptOutcome([0x0100], [], [], [])
                : new ScriptOutcome([], [0x0100], [], []));

        Assert.True(
            closed.Rounds.Count < StoryClosure.MostRounds,
            $"it ran to the backstop in {closed.Rounds.Count} passes instead of settling");
    }

    /// <summary>
    /// And a script that genuinely never stops opening things is stopped by the backstop.
    /// <para>
    /// The half that has to exist for the round count to mean anything. A walk that ran for
    /// ever would hang the tool; one that stopped at the backstop and said so is a finding.
    /// </para>
    /// </summary>
    [Fact]
    public void AndOneThatNeverStopsOpeningIsStoppedByTheBackstop()
    {
        var endless = new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x6000 };

        MapData start = Room("1.0") with { Objects = [endless] };

        var next = 0x0100;

        Closure closed = StoryClosure.Walk(
            new WorldData([start]),
            "1.0",
            (_, _) => new ScriptOutcome([next++], [], [], []));

        Assert.Equal(StoryClosure.MostRounds, closed.Rounds.Count);
    }
}

/// <summary>
/// Standing in for a routine this project cannot execute.
/// <para>
/// A <c>special</c> is a call into the cartridge's own ARM code. The runner steps over it and
/// the answer variable keeps its zero, so every script that branches on the answer takes the
/// zero arm — and every badge check in this game is one of those, which is why the boundary
/// lands squarely on the endgame.
/// </para>
/// <para>
/// A stand-in is <b>modelled and is an experiment</b>, never a fact. What makes it worth
/// having is that it is checkable: supply an answer, walk the story again, and see how much of
/// the world opens. A number that opens nothing was wrong or irrelevant.
/// </para>
/// </summary>
public class StandingInForARoutineTests
{
    /// <summary>
    /// With no answer supplied the variable keeps its zero, which is the behaviour every
    /// figure this project has printed so far was measured under.
    /// </summary>
    [Fact]
    public void WithNoAnswerTheVariableKeepsItsZero()
    {
        var state = new PokeMmo.Core.Scripts.ScriptState();

        Assert.Equal(0, state.Read(SpecialContracts.AnswerVariable));
    }

    /// <summary>
    /// And a supplied answer reaches the variable the call answers into, so the compare and
    /// the branch after it work exactly as they would have.
    /// <para>
    /// Written against the state rather than against a cartridge, because the thing being
    /// checked is that the answer lands where a script would look for it — and that is a fact
    /// about the variable, not about any image.
    /// </para>
    /// </summary>
    [Fact]
    public void AndASuppliedAnswerLandsWhereAScriptLooksForIt()
    {
        var state = new PokeMmo.Core.Scripts.ScriptState();

        state.Write(SpecialContracts.AnswerVariable, 8);

        Assert.Equal(8, state.Read(SpecialContracts.AnswerVariable));
    }

    /// <summary>
    /// A routine compared against a run from one upwards is counting something, and one
    /// compared against a scatter of values is not.
    /// <para>
    /// The one piece of shape-reading in here, and the reason it is a property rather than a
    /// judgement made while printing: a badge check reads as eight comparisons against one
    /// through eight, and a routine asked "which of these three things happened" reads as
    /// three unrelated numbers. Telling those apart is what makes the report worth reading.
    /// </para>
    /// </summary>
    [Fact]
    public void ARunFromOneUpwardsLooksLikeACount()
    {
        var counting = new SpecialContract(
            0x1B5, 8, 0, new Dictionary<int, int> { [1] = 1, [2] = 1, [3] = 1, [4] = 1 }, 8, []);

        var scattered = new SpecialContract(
            0x1B6, 3, 1, new Dictionary<int, int> { [0] = 1, [7] = 1, [40] = 1 }, 3, []);

        Assert.True(counting.LooksLikeACount);
        Assert.Equal(4, counting.Highest);

        Assert.False(scattered.LooksLikeACount);
    }

    /// <summary>
    /// And a yes-or-no is not a count either, which is the case a run of two would otherwise
    /// be mistaken for.
    /// </summary>
    [Fact]
    public void ButAYesOrNoIsNot()
    {
        var yesNo = new SpecialContract(
            0x1B7, 20, 0, new Dictionary<int, int> { [0] = 10, [1] = 10 }, 20, []);

        Assert.False(yesNo.LooksLikeACount);
    }
}
