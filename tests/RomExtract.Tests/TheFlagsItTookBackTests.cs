using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The flags a run turns off again, and the test that could not see them.
/// <para>
/// <b>239 put the first thing in this run that can take something back and left the settle test
/// made of counts.</b> Six of them — how many flags, how many moves, how big the party — and a
/// pass that cleared one flag and set another matched all six and stopped the run saying nothing
/// more had opened. The rule against exactly that is written down inside
/// <see cref="WhereItHasBeen"/>, three lines below the test that broke it.
/// </para>
/// <para>
/// And what the run RETURNED was the state of whichever pass it stopped on. That was the same
/// thing as "every flag it ever set" for as long as nothing could clear one; on this cartridge
/// the two differ by 4 flags at the floor and 9 with the levers on, and all of them were being
/// reported as gates nothing that can run opens.
/// </para>
/// </summary>
public sealed class TheFlagsItTookBackTests
{
    private const uint TheSign = 0x3000;
    private const int First = 0x0500;
    private const int Second = 0x0501;
    private const int Third = 0x0502;
    private const int Hides = 0x0503;

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>A four-by-four room anybody can walk anywhere in.</summary>
    private static MapData Room(string id = "1.0") => new(id, id, 4, 4, new byte[16]);

    private static Attempt Run(WorldData world, Func<uint, PlayedScript> what) =>
        Autoplayer.Play(world, world.Maps.First().Id, TestRules.All, (address, _, _) => what(address));

    private static Attempt Run(MapData map, Func<uint, PlayedScript> what) =>
        Run(new WorldData([map]), what);

    /// <summary>A sign that does something different on each pass it is read.</summary>
    private static Func<uint, PlayedScript> InTurn(params PlayedScript[] passes)
    {
        var read = 0;

        return address =>
        {
            if (address != TheSign) return Nothing;

            return read < passes.Length ? passes[read++] : Nothing;
        };
    }

    private static MapData WithASign(MapData map) =>
        map with { Signs = [new MapSign(1, 1, Kind: 0, TheSign)] };

    // ------------------------------------------------------- the test that was made of counts

    /// <summary>
    /// THE THING: a pass that clears one flag and sets another has changed the world and the
    /// run must not stop on it.
    /// </summary>
    /// <remarks>
    /// Under the six counts this stopped on pass 2 — one flag before, one flag after, same
    /// party, same bag — and <see cref="Third"/> was never set at all. It is the same
    /// discrimination <see cref="WhereItHasBeen"/> was built around and the settle test above
    /// it did not have.
    /// </remarks>
    [Fact]
    public void APassThatSwapsOneFlagForAnotherHasNotSettled()
    {
        Attempt played = Run(
            WithASign(Room()),
            InTurn(
                Nothing with { FlagsSet = [First] },
                Nothing with { FlagsSet = [Second], FlagsCleared = [First] },
                Nothing with { FlagsSet = [Third] }));

        Assert.Contains(Third, played.Flags);
        Assert.Equal(StoppedBecause.NothingMoreOpened, played.Stopped);
    }

    /// <summary>And a run that truly settles still stops, or the test above passes by never stopping.</summary>
    [Fact]
    public void AndAPassThatChangesNothingStillSettles()
    {
        Attempt played = Run(
            WithASign(Room()),
            InTurn(Nothing with { FlagsSet = [First] }));

        Assert.Equal(StoppedBecause.NothingMoreOpened, played.Stopped);
        Assert.True(played.Passes < Autoplayer.MostPasses);
    }

    // --------------------------------------------------------------- what it ever had on

    /// <summary>
    /// A flag on at the end of one pass and off at the end of the run is reported, and the
    /// count the run returns is the stopping pass rather than the union.
    /// </summary>
    [Fact]
    public void AFlagOnAtTheEndOfAPassAndOffAtTheEndIsTookBack()
    {
        Attempt played = Run(
            WithASign(Room()),
            InTurn(
                Nothing with { FlagsSet = [First] },
                Nothing with { FlagsCleared = [First] }));

        Assert.DoesNotContain(First, played.Flags);
        Assert.Contains(First, played.EverOn);
        Assert.Contains(First, played.TookBack);
    }

    /// <summary>
    /// THE DISCRIMINATION: the union is folded at the END of a pass, so a flag set and cleared
    /// inside one pass was never in a state any walk was computed from and is not one it had.
    /// </summary>
    /// <remarks>
    /// Folding as the flags move would report every scratch flag in the game as taken back —
    /// on this cartridge `0x026C` and `0x0807` are set and cleared within a pass six and seven
    /// times over. That is a different finding, printed as its own line, and conflating the two
    /// would make the taken-back list mostly noise.
    /// </remarks>
    [Fact]
    public void SetAndClearedInsideOnePassIsNotAFlagItEverHad()
    {
        Attempt played = Run(
            WithASign(Room()),
            InTurn(Nothing with { FlagsSet = [First], FlagsCleared = [First] }));

        Assert.DoesNotContain(First, played.EverOn);
        Assert.DoesNotContain(First, played.TookBack);

        // And the moves are still recorded, because that is the other line.
        Assert.Contains(played.FlagMoves, m => m.Flag == First && !m.Cleared);
        Assert.Contains(played.FlagMoves, m => m.Flag == First && m.Cleared);
    }

    /// <summary>
    /// And nothing is taken back when nothing is taken back — the half without which "report
    /// every flag" passes the two tests above.
    /// </summary>
    [Fact]
    public void ARunThatOnlyEverSetsThingsTookNothingBack()
    {
        Attempt played = Run(
            WithASign(Room()),
            InTurn(Nothing with { FlagsSet = [First] }, Nothing with { FlagsSet = [Second] }));

        Assert.Empty(played.TookBack);
        Assert.Equal([.. played.Flags.Order()], [.. played.EverOn.Order()]);
    }

    /// <summary>
    /// Which script moved it, and when. <c>--trace</c> watches a VARIABLE and answers about
    /// something else when it is handed a flag number, so this is the only thing that can say
    /// who took one back.
    /// </summary>
    [Fact]
    public void ItSaysWhichScriptMovedTheFlagAndOnWhichPass()
    {
        Attempt played = Run(
            WithASign(Room()),
            InTurn(
                Nothing with { FlagsSet = [First] },
                Nothing with { FlagsCleared = [First] }));

        MovedAFlag set = Assert.Single(played.FlagMoves, m => m.Flag == First && !m.Cleared);
        MovedAFlag cleared = Assert.Single(played.FlagMoves, m => m.Flag == First && m.Cleared);

        Assert.Equal(TheSign, set.Address);
        Assert.Equal("1.0", set.MapId);
        Assert.Equal(1, set.Pass);
        Assert.Equal(2, cleared.Pass);
    }

    // ------------------------------------------------------------------ and what it costs

    /// <summary>
    /// Two rooms, and the only way between them is past somebody a flag hides. The run turns
    /// the flag on, walks through, turns it off again — and the world it REPORTS is the one
    /// where the second room was never reached.
    /// </summary>
    private static WorldData ThroughSomebodyAFlagHides() =>
        new(
        [
            new MapData("1.0", "1.0", 1, 5, new byte[5])
            {
                Signs = [new MapSign(0, 1, Kind: 0, TheSign)],
                Objects =
                [
                    new MapObject(1, 1, 0, 2, Direction.Down, 0, false) { HiddenBy = Hides },
                ],
                Warps = [new Warp(0, 4, 0, "2.0")],
            },
            new MapData("2.0", "2.0", 1, 5, new byte[5]) { Warps = [new Warp(0, 4, 0, "1.0")] },
        ]);

    /// <summary>
    /// THE COST, and the reason the taken-back list is worth having at all: the stopping pass
    /// decides which world gets reported.
    /// </summary>
    [Fact]
    public void AMapReachedOnlyWhileTheFlagWasOnIsNotInWhatItReports()
    {
        Attempt played = Run(
            ThroughSomebodyAFlagHides(),
            InTurn(
                Nothing with { FlagsSet = [Hides] },
                Nothing with { FlagsCleared = [Hides] }));

        Assert.Contains(Hides, played.TookBack);
        Assert.DoesNotContain("2.0", played.Reached);
        Assert.Contains("2.0", played.ReachedOnlyWithWhatItTookBack);
    }

    /// <summary>
    /// And the same world with nothing taken back reports no cost, so the assertion above is
    /// about the taking back and not about the fixture having two maps in it.
    /// </summary>
    [Fact]
    public void AndHoldingItOnCostsNothing()
    {
        Attempt played = Run(
            ThroughSomebodyAFlagHides(),
            InTurn(Nothing with { FlagsSet = [Hides] }));

        Assert.Empty(played.TookBack);
        Assert.Contains("2.0", played.Reached);
        Assert.Empty(played.ReachedOnlyWithWhatItTookBack);
    }

    /// <summary>
    /// WHY ONLY ONE DIRECTION IS PRINTED. A flag in this walk does exactly one thing — hide
    /// somebody — and a hidden person cannot block a square, so more flags is always a superset
    /// of the reach.
    /// </summary>
    /// <remarks>
    /// The first version of the report printed the other half as well, on the reasoning that a
    /// one-directional number cannot say which way it went. That number can only ever say
    /// nought, and a line that cannot come back non-empty is trap 8 written the other way up.
    /// This is the claim that replaced it, asserted rather than believed.
    /// </remarks>
    [Fact]
    public void MoreFlagsIsAlwaysASupersetOfTheReach()
    {
        WorldData world = ThroughSomebodyAFlagHides();

        Reach without = WorldWalker.Walk(world, "1.0", [], flagsSet: []);
        Reach with = WorldWalker.Walk(world, "1.0", [], flagsSet: [Hides]);

        Assert.Empty(without.Maps.Except(with.Maps));
        Assert.NotEmpty(with.Maps.Except(without.Maps));
    }

    // ------------------------------------------------------- and it is not the code boundary

    /// <summary>
    /// A gate the run opened and shut again is filed as that, ahead of every bucket about what
    /// the FILE can do.
    /// </summary>
    /// <remarks>
    /// All four of the floor's went into "set only where the map scan cannot see — past the
    /// code boundary", which is a claim contradicted by the same <see cref="Attempt"/> the
    /// bucket was computed from. Ordering it anywhere but first re-creates that: three of the
    /// four have a setter in the image that the map scan never opened, so the old answer is
    /// still available and still wrong.
    /// </remarks>
    [Fact]
    public void AGateTheRunTookBackIsNotTheCodeBoundary()
    {
        var world = new WorldData(
        [
            Room() with
            {
                Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { HiddenBy = Hides }],
            },
        ]);

        var gates = new FlagGates(world);

        ShutGate gate = Assert.Single(
            WhyTheGatesAreShut.Of(
                gates,
                setByTheRun: [],
                movedInTheImage: new Dictionary<int, IReadOnlyList<FlagSite>>
                {
                    [Hides] = [new FlagSite(0x000000, Hides, Sets: true, ReadsAsAScript: true, Opened: false)],
                },
                onTheFloor: [],
                obstacles: [],
                tookBack: [Hides]),
            g => g.Flag == Hides);

        Assert.Equal(ShutBecause.TheRunTookItBack, gate.Why);
    }

    /// <summary>
    /// And the same gate with nothing taken back is still the boundary — without this, "always
    /// answer TheRunTookItBack" passes the test above.
    /// </summary>
    [Fact]
    public void AndTheSameGateIsStillTheBoundaryWhenNothingWasTakenBack()
    {
        var world = new WorldData(
        [
            Room() with
            {
                Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { HiddenBy = Hides }],
            },
        ]);

        ShutGate gate = Assert.Single(
            WhyTheGatesAreShut.Of(
                new FlagGates(world),
                setByTheRun: [],
                movedInTheImage: new Dictionary<int, IReadOnlyList<FlagSite>>
                {
                    [Hides] = [new FlagSite(0x000000, Hides, Sets: true, ReadsAsAScript: true, Opened: false)],
                },
                onTheFloor: [],
                obstacles: []),
            g => g.Flag == Hides);

        Assert.Equal(ShutBecause.OnlyPastTheBoundary, gate.Why);
    }
}
