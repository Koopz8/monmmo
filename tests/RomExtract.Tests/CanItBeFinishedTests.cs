using PokeMmo.Core.Scripts;
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
    /// The walk starts from the flags a new game already sets.
    /// <para>
    /// <b>The bug the first real run found.</b> A fresh save is not an empty save: the
    /// cartridge sets forty-nine flags before the first frame and every one of them hides
    /// somebody not yet met. Started from nothing, MR. FUJI stands in his own front room
    /// holding the flute and the whole tower is scenery — milestone 56's lesson, walked
    /// straight past by the instrument written to check the story.
    /// </para>
    /// </summary>
    [Fact]
    public void ItStartsFromTheFlagsANewGameAlreadySets()
    {
        var world = new WorldData([Room("1.0")]) { FlagsAtStart = [0x0820, 0x0821] };

        Closure closed = StoryClosure.Walk(world, "1.0", (_, _) => Nothing);

        Assert.Contains(0x0820, closed.Flags);
        Assert.Contains(0x0821, closed.Flags);
    }

    /// <summary>
    /// And somebody a starting flag hides is not standing there to be talked to, which is
    /// the whole point of those flags being set.
    /// </summary>
    [Fact]
    public void AndSomebodyAStartingFlagHidesIsNotThere()
    {
        const int HidesHim = 0x0820;

        var fuji = new MapObject(1, 1, 1, 1, Direction.Down, 0, false)
        {
            ScriptAddress = 0x7000,
            HiddenBy = HidesHim,
        };

        MapData start = Room("1.0") with { Objects = [fuji] };

        var ran = new List<uint>();

        StoryClosure.Walk(
            new WorldData([start]) { FlagsAtStart = [HidesHim] },
            "1.0",
            (address, _) =>
            {
                ran.Add(address);

                return Nothing;
            });

        Assert.DoesNotContain(0x7000u, ran);
    }

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
            0x1B5, 8, 0, new Dictionary<int, int> { [1] = 1, [2] = 1, [3] = 1, [4] = 1 }, 8, 8, 0, 0,
            new Dictionary<int, int>(), []);

        var scattered = new SpecialContract(
            0x1B6, 3, 1, new Dictionary<int, int> { [0] = 1, [7] = 1, [40] = 1 }, 3, 3, 0, 0,
            new Dictionary<int, int>(), []);

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
            0x1B7, 20, 0, new Dictionary<int, int> { [0] = 10, [1] = 10 }, 20, 20, 0, 0,
            new Dictionary<int, int>(), []);

        Assert.False(yesNo.LooksLikeACount);
    }
}

/// <summary>
/// Playing the game from a fresh save, as far as it can get.
/// <para>
/// The closure walk answers where somebody could <em>stand</em>. This one talks to people,
/// takes what they hand over, and fights what fights back — so it can tell a door that will
/// not open from a fight that cannot be won, which are two very different things to fix.
/// </para>
/// </summary>
public class PlayingItThroughTests
{
    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    private static MapObject Person(uint at, int x = 1, int y = 1) =>
        new(1, 1, x, y, Direction.Down, 0, false) { ScriptAddress = at };

    /// <summary>
    /// A world with nothing in it stops on the first pass and says why, rather than running
    /// to its backstop.
    /// </summary>
    [Fact]
    public void AnEmptyWorldStopsAtOnceAndSaysWhy()
    {
        Attempt played = Autoplayer.Play(
            new WorldData([Room("1.0")]), "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.Equal(StoppedBecause.NothingMoreOpened, played.Stopped);
        Assert.Equal(1, played.Passes);
    }

    /// <summary>
    /// Somebody handing over a creature puts it in the party, which is what makes every fight
    /// after it possible.
    /// </summary>
    [Fact]
    public void SomebodyHandingOverACreaturePutsItInTheParty()
    {
        MapData start = Room("1.0") with { Objects = [Person(0x1000)] };

        var given = false;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) =>
            {
                if (given) return Nothing;

                given = true;

                return new PlayedScript([], [], [], [], (1, 5), null);
            });

        Assert.Single(played.Party);
    }

    /// <summary>
    /// A fight reached before anything has been handed over is counted rather than crashed
    /// on.
    /// <para>
    /// The case that would otherwise throw: a trainer standing on the first map, and a party
    /// of nobody. Counting it is what makes "it never got there" tellable from "it got there
    /// and could not fight".
    /// </para>
    /// </summary>
    [Fact]
    public void AFightWithNobodyToSendOutIsCountedRatherThanCrashedOn()
    {
        MapData start = Room("1.0") with { Objects = [Person(0x2000)] };

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) => new PlayedScript([], [], [], [], null, 1));

        Assert.True(played.FightsSkipped >= 1);
        Assert.Equal(0, played.FightsWon);
    }

    /// <summary>
    /// The same trainer is only fought once, however many passes walk past them.
    /// <para>
    /// A trainer beaten stays beaten — that is what the flag they set means — and a loop that
    /// re-fought everybody every pass would report a fight count that measured the number of
    /// passes rather than the number of trainers.
    /// </para>
    /// </summary>
    [Fact]
    public void ATrainerIsOnlyFoughtOnce()
    {
        MapData start = Room("1.0") with { Objects = [Person(0x3000)] };

        var asked = 0;
        var opened = 0x100;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) =>
            {
                // Keeps opening something for a few passes, so the loop keeps going and has
                // every chance to fight the same person again.
                asked++;

                return new PlayedScript(asked < 4 ? [opened++] : [], [], [], [], null, 7);
            });

        Assert.True(played.Passes > 1, "it only ran one pass, so nothing was given a second chance");
        Assert.True(played.FightsWon + played.FightsLost + played.FightsSkipped <= 1);
    }

    /// <summary>
    /// The playthrough starts from the flags a new game already sets, same as the walk.
    /// </summary>
    [Fact]
    public void ThePlaythroughStartsFromAFreshSaveRatherThanAnEmptyOne()
    {
        var world = new WorldData([Room("1.0")]) { FlagsAtStart = [0x0820] };

        Attempt played = Autoplayer.Play(world, "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.Contains(0x0820, played.Flags);
    }

    /// <summary>And the routines it could not answer are carried out, same as the walk.</summary>
    [Fact]
    public void TheRoutinesItCouldNotAnswerAreCarriedOut()
    {
        MapData start = Room("1.0") with { Objects = [Person(0x4000)] };

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) => new PlayedScript([], [], [], [0x1B5], null, null));

        Assert.True(played.Specials[0x1B5] >= 1);
    }
}

/// <summary>
/// The four things the first real run against a cartridge found.
/// <para>
/// It printed <c>0 fights won, 157 lost</c> and <c>highest level 5</c> for twenty-four
/// identical passes. None of that was difficulty; all four were faults in the player.
/// </para>
/// </summary>
public class WhatTheFirstRealRunFoundTests
{
    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static MapObject Person(uint at) =>
        new(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = at };

    /// <summary>
    /// A creature comes in at the level its script names, not at five.
    /// <para>
    /// The first version took only the species, so every gift in the game — the fossils, the
    /// snorlax, the lapras — arrived as a starter, and the party could never be a match for
    /// anything it met.
    /// </para>
    /// </summary>
    [Fact]
    public void ACreatureComesInAtTheLevelItsScriptNames()
    {
        MapData start = Room("1.0") with { Objects = [Person(0x1000)] };

        var given = false;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) =>
            {
                if (given) return new PlayedScript([], [], [], [], null, null);

                given = true;

                return new PlayedScript([], [], [], [], (1, 34), null);
            });

        Assert.Single(played.Party);
        Assert.Equal(34, played.Party[0].Level);
    }

    /// <summary>
    /// A pass that changes nothing ends the run, even when scripts keep reporting flags.
    /// <para>
    /// The loop used to end on "did any script report a flag that was not set a moment ago",
    /// and one script clearing what another sets answers yes for ever. The first real run sat
    /// at 179 maps and 62 flags from pass four to pass twenty-four and then reported the
    /// backstop. Ending on what is <em>known</em> rather than on what was <em>reported</em>
    /// makes it converge.
    /// </para>
    /// </summary>
    [Fact]
    public void APassThatChangesNothingEndsTheRun()
    {
        MapData start = Room("1.0") with { Objects = [Person(0x2000), Person(0x2001)] };

        var turn = 0;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,

            // One sets it, the next clears it, for ever. Nothing is ever actually learned.
            (_, _, _) => ++turn % 2 == 1
                ? new PlayedScript([0x0100], [], [], [], null, null)
                : new PlayedScript([], [0x0100], [], [], null, null));

        Assert.Equal(StoppedBecause.NothingMoreOpened, played.Stopped);
        Assert.True(
            played.Passes < Autoplayer.MostPasses,
            $"it ran to the backstop in {played.Passes} passes instead of settling");
    }

    /// <summary>
    /// A world with somewhere to heal heals between fights.
    /// <para>
    /// The worst decision in the first version, and its output said so in one number: the
    /// first loss left the whole party down and every one of the 156 fights after it was lost
    /// before it began. A run that measures "did the first fight go badly" and reports it 157
    /// times is not measuring the game.
    /// </para>
    /// </summary>
    [Fact]
    public void AWorldWithSomewhereToHealHealsBetweenFights()
    {
        var nurse = new MapObject(2, 1, 2, 2, Direction.Down, 0, false) { Heals = true };

        MapData start = Room("1.0") with { Objects = [Person(0x3000), nurse] };

        var handed = false;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) =>
            {
                if (handed) return new PlayedScript([], [], [], [], null, 1);

                handed = true;

                return new PlayedScript([], [], [], [], (1, 20), null);
            });

        Assert.True(played.PartiesHealed > 0, "nothing was healed, so one loss ends every run");
    }

    /// <summary>And a world with nowhere to heal does not, which is what makes that a finding.</summary>
    [Fact]
    public void AndAWorldWithNowhereToHealDoesNot()
    {
        MapData start = Room("1.0") with { Objects = [Person(0x4000)] };

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) => new PlayedScript([], [], [], [], null, 1));

        Assert.Equal(0, played.PartiesHealed);
    }
}

/// <summary>
/// Everybody starts in the same place, and it is written down once.
/// <para>
/// The server had <c>"4.1"</c> in its argument default; the dump tool grew a second copy of
/// the same decision, and that copy began life as <c>world.Maps.First()</c> — map 0.0, a floor
/// of CELADON DEPT. The first playthrough ever run reached one map and stopped, and every
/// number it printed was about a shop.
/// </para>
/// <para>
/// This is the third time this session that one decision written down twice has been the bug:
/// <c>ContinueFrom</c> called by one of two callers, the benches set by neither, and now a
/// starting map. The guard is the same each time — one place, and a test that says so.
/// </para>
/// </summary>
public class EverybodyStartsInTheSamePlaceTests
{
    /// <summary>The beginning is PALLET TOWN, and it is a decision rather than a reading.</summary>
    [Fact]
    public void TheBeginningIsPalletTown()
    {
        Assert.Equal("4.1", Beginning.MapId);
    }

    /// <summary>
    /// And a walk started there is not a walk started at whatever happens to be first in the
    /// file.
    /// <para>
    /// The failure, reproduced: the first map in an export is not the first map of a game, and
    /// nothing about the list says which is which.
    /// </para>
    /// </summary>
    [Fact]
    public void AndItIsNotWhateverIsFirstInTheFile()
    {
        var world = new WorldData(
        [
            new MapData("0.0", "CELADON DEPT.", 4, 4, new byte[16]),
            new MapData(Beginning.MapId, "PALLET TOWN", 4, 4, new byte[16]),
        ]);

        Assert.NotEqual(world.Maps.First().Id, Beginning.MapId);
        Assert.Equal("PALLET TOWN", world.Find(Beginning.MapId)?.Name);
    }
}

/// <summary>
/// Telling a door behind a story gate from a door nothing can approach.
/// <para>
/// A walkable door that was never stood on has two very different causes and the same
/// appearance. Either something on this side of it is a gate — a guard, a tree — or the walk
/// arrived on the map and could not step off, which is what an <em>island</em> is.
/// </para>
/// <para>
/// Islands are made deliberately: <c>ToGrid</c> opens every warp square, because a door that
/// cannot be stood on is a map that cannot be entered. A warp sitting in a wall therefore
/// becomes a square nothing can reach from inside and nothing can leave.
/// </para>
/// </summary>
public class TellingAnIslandFromAGateTests
{
    /// <summary>A map walked end to end is not an island, whatever else is wrong with it.</summary>
    [Fact]
    public void AMapWalkedEndToEndIsNotAnIsland()
    {
        var door = new ShutDoor(
            "1.0", new GridPosition(9, 1), "0.1", "CELADON DEPT.",
            CouldStandOnIt: false, SquareIsWalkable: true, SomebodyIsInTheWay: false,
            StoodOnThisMap: 60, WalkableOnThisMap: 64);

        Assert.False(door.ArrivedOnAnIsland);
    }

    /// <summary>
    /// And one where the walk stood on a handful of squares out of many is.
    /// <para>
    /// This is the shape to look for: arrived, could not move, and every door on the map
    /// therefore reads as "never reached".
    /// </para>
    /// </summary>
    [Fact]
    public void AndOneWalkedAlmostNotAtAllIs()
    {
        var door = new ShutDoor(
            "1.0", new GridPosition(9, 1), "0.1", "CELADON DEPT.",
            CouldStandOnIt: false, SquareIsWalkable: true, SomebodyIsInTheWay: false,
            StoodOnThisMap: 1, WalkableOnThisMap: 64);

        Assert.True(door.ArrivedOnAnIsland);
    }

    /// <summary>
    /// A tiny map is never called an island, because on a map of four squares standing on one
    /// says nothing.
    /// </summary>
    [Fact]
    public void ButATinyMapIsNeverCalledOne()
    {
        var door = new ShutDoor(
            "1.0", new GridPosition(1, 1), "0.1", "SOMEWHERE",
            CouldStandOnIt: false, SquareIsWalkable: true, SomebodyIsInTheWay: false,
            StoodOnThisMap: 0, WalkableOnThisMap: 4);

        // Nought of four satisfies the ratio on its own. Without the floor on map size this
        // would call a cupboard an island, and there are a great many cupboards.
        Assert.False(door.ArrivedOnAnIsland);
    }
}
