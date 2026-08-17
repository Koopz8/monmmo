using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// "110 gating flags it never set" is four different findings added together.
/// <para>
/// A gate no <c>setflag</c> names is the boundary and will never open by walking; a gate set
/// only where the map scan cannot see is the wall list's shape; a gate set by a script on a map
/// is a walk that has not gone far enough; and a gate that is the hide flag of something lying
/// on the floor opens by picking that thing up, which the run already does.
/// </para>
/// <para>
/// <b>The fourth bucket is here because the numbers caught its absence.</b> With three, "nothing
/// in the file sets it" read 134 at the floor and 56 with the levers on — impossible for a
/// property of the FILE, and the impossibility was the finding: the run sets sixty-five flags
/// that no <c>setflag</c> in the cartridge names, because the routine that hands a thing over
/// sets the object's own hide flag in compiled code.
/// </para>
/// </summary>
public sealed class WhyTheGatesAreShutTests
{
    private const int AScriptOnAMapSetsIt = 0x0100;

    private const int OnlyUnopenedScriptSetsIt = 0x0200;

    private const int LyingOnTheFloor = 0x0300;

    private const int NothingSetsIt = 0x0400;

    private const int OnTheFloorAndSetOutOfSight = 0x0500;

    private const int OnTheFloorAndSetOnAMap = 0x0600;

    /// <summary>
    /// A world where every one of those six flags hides somebody, so all six are gates and the
    /// only thing separating them is what could set them.
    /// </summary>
    private static FlagGates SixGates() =>
        new(new WorldData(
        [
            new MapData("1.0", "1.0", 4, 4, new byte[16])
            {
                Objects =
                [
                    .. new[]
                    {
                        AScriptOnAMapSetsIt, OnlyUnopenedScriptSetsIt, LyingOnTheFloor,
                        NothingSetsIt, OnTheFloorAndSetOutOfSight, OnTheFloorAndSetOnAMap,
                    }
                    .Select((f, i) =>
                        new MapObject(i + 1, 1, i, 1, Direction.Down, 0, false) { HiddenBy = f }),
                ],
            },
        ]));

    private static FlagSite Site(int flag, bool sets, bool opened) =>
        new(0x1000 + flag, flag, sets, true, opened);

    private static IReadOnlyDictionary<int, IReadOnlyList<FlagSite>> Sites() =>
        new Dictionary<int, IReadOnlyList<FlagSite>>
        {
            [AScriptOnAMapSetsIt] = [Site(AScriptOnAMapSetsIt, true, true)],
            [OnlyUnopenedScriptSetsIt] = [Site(OnlyUnopenedScriptSetsIt, true, false)],
            [OnTheFloorAndSetOutOfSight] = [Site(OnTheFloorAndSetOutOfSight, true, false)],
            [OnTheFloorAndSetOnAMap] = [Site(OnTheFloorAndSetOnAMap, true, true)],
        };

    private static readonly int[] TheFloor =
        [LyingOnTheFloor, OnTheFloorAndSetOutOfSight, OnTheFloorAndSetOnAMap];

    private static IReadOnlyList<ShutGate> Shut(params int[] setByTheRun) =>
        WhyTheGatesAreShut.Of(SixGates(), setByTheRun, Sites(), TheFloor);

    private static ShutBecause Why(int flag) => Assert.Single(Shut(), g => g.Flag == flag).Why;

    /// <summary>
    /// Each of the four reasons, on its own gate. Four of them, because a fixture with one of
    /// each kind is the smallest thing that can tell four buckets apart.
    /// </summary>
    [Fact]
    public void EachGateGetsTheReasonThatFitsIt()
    {
        Assert.Equal(ShutBecause.NeverRan, Why(AScriptOnAMapSetsIt));
        Assert.Equal(ShutBecause.OnlyPastTheBoundary, Why(OnlyUnopenedScriptSetsIt));
        Assert.Equal(ShutBecause.TakenOffTheFloor, Why(LyingOnTheFloor));
        Assert.Equal(ShutBecause.NothingSetsIt, Why(NothingSetsIt));
    }

    /// <summary>
    /// THE ORDER IS A DECISION AND IT IS ASSERTED, both ways round.
    /// <para>
    /// A flag can be several of these at once, and the two overlaps go opposite ways: an opened
    /// setter beats being on the floor, because a walk can reach and prove it; being on the
    /// floor beats an unopened setter, because the run demonstrably opens those and calling one
    /// "past the boundary" would be false about a gate that is already opening.
    /// </para>
    /// </summary>
    [Fact]
    public void WhenAGateIsSeveralOfTheseAtOnceTheOrderDecides()
    {
        Assert.Equal(ShutBecause.NeverRan, Why(OnTheFloorAndSetOnAMap));
        Assert.Equal(ShutBecause.TakenOffTheFloor, Why(OnTheFloorAndSetOutOfSight));
    }

    /// <summary>
    /// A gate the run opened is not shut, which is the ordinary case and the one that makes the
    /// count above a count of anything.
    /// </summary>
    [Fact]
    public void AGateTheRunOpenedIsNotOnTheListAtAll()
    {
        IReadOnlyList<ShutGate> shut = Shut(AScriptOnAMapSetsIt, NothingSetsIt);

        Assert.Equal(4, shut.Count);
        Assert.DoesNotContain(shut, g => g.Flag == AScriptOnAMapSetsIt);
        Assert.DoesNotContain(shut, g => g.Flag == NothingSetsIt);
    }

    /// <summary>
    /// AND IT CAN COME BACK EMPTY, which is the answer that would mean there is nothing left to
    /// reach at all.
    /// </summary>
    [Fact]
    public void ARunThatOpenedEveryGateLeavesNothingShut()
    {
        Assert.Empty(Shut(
            AScriptOnAMapSetsIt, OnlyUnopenedScriptSetsIt, LyingOnTheFloor,
            NothingSetsIt, OnTheFloorAndSetOutOfSight, OnTheFloorAndSetOnAMap));
    }

    /// <summary>
    /// A place that CLEARS a flag is not a place that could have opened this gate.
    /// <para>
    /// Without this the third bucket fills up with scripts that turn the flag off, and "a walk
    /// could reach this" becomes true of gates whose only visible script shuts them.
    /// </para>
    /// </summary>
    [Fact]
    public void AScriptThatCLEARSAGateIsNotAScriptThatCouldOpenIt()
    {
        IReadOnlyDictionary<int, IReadOnlyList<FlagSite>> clearing =
            new Dictionary<int, IReadOnlyList<FlagSite>>
            {
                [NothingSetsIt] = [Site(NothingSetsIt, false, true)],
            };

        ShutGate gate = Assert.Single(
            WhyTheGatesAreShut.Of(SixGates(), [], clearing, TheFloor),
            g => g.Flag == NothingSetsIt);

        Assert.Equal(ShutBecause.NothingSetsIt, gate.Why);
        Assert.Equal(0, gate.Sites);
    }

    /// <summary>
    /// The counts, biggest first — and every gate in exactly one bucket, so the buckets add up
    /// to the number they are decomposing.
    /// </summary>
    [Fact]
    public void TheBucketsAddUpToTheNumberTheyDecompose()
    {
        IReadOnlyList<ShutGate> shut = Shut();

        Assert.Equal(6, shut.Count);
        Assert.Equal(shut.Count, WhyTheGatesAreShut.Counted(shut).Sum(c => c.Gates));

        Assert.Equal(
            WhyTheGatesAreShut.Counted(shut).Select(c => c.Gates).OrderDescending(),
            WhyTheGatesAreShut.Counted(shut).Select(c => c.Gates));
    }
}
