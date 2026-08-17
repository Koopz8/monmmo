using System.Linq;
using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The same fault 193 shipped and 194 fixed, still live in the run's record of what it ran.
/// <para>
/// 194 found that <c>SceneBeat.Walk</c> was keyed on the address of the <c>applymovement</c>
/// alone, and that this is true on one map and false across the cartridge: one nurse's script
/// is attached to person 1 on nineteen Pokémon Centres, one shopkeeper's on nineteen marts,
/// one gym guide's on eight. It fixed the walking and said, in as many words, that anything
/// else in the repository keyed on a script address alone is wrong about the twelve blocks
/// this cartridge reaches from more than one map.
/// </para>
/// <para>
/// <c>Attempt.Ran</c> was one of those, and it is the one <c>--flags</c> asks before it prints
/// <em>why</em> a script did not set its flag. So the printed reason could be borrowed: the
/// run stands in front of a setter in PEWTER it never touched, finds the block in the
/// dictionary because a different town ran it, and prints a confident diagnosis merged from
/// there. A fallback that names a cause is worse than one that says nothing — which is the
/// finding of milestone 188, arriving here for the second time.
/// </para>
/// <para>
/// <b>Every fixture below has two maps in it</b>, because one map is exactly what 193's tests
/// had and one map is exactly what could not see this.
/// </para>
/// </summary>
public sealed class TheMapIsHalfTheKeyHereTooTests
{
    private const uint Shared = 0x1000;

    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// Two rooms joined by a warp. The shared block hangs off a person you can walk up to in
    /// <c>1.0</c>, and off a trigger square that is off the edge of <c>2.0</c> — reached, and
    /// never stood on.
    /// <para>
    /// The decoy is the whole fixture: both maps are reached and both name the same block, so
    /// "did the run run this" is the only thing that can tell them apart.
    /// </para>
    /// </summary>
    private static WorldData TwoTownsOneNurse() =>
        new(
        [
            Room("1.0") with
            {
                Warps = [new Warp(3, 1, 0, "2.0")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = Shared },
                ],
            },
            Room("2.0") with
            {
                Warps = [new Warp(3, 1, 0, "1.0")],
                Triggers = [new MapTrigger(9, 9, 0, 0, Shared)],
            },
        ]);

    /// <summary>
    /// The finding, stated as the thing itself: running a block in one town is not running it
    /// in the next one, however identical the bytes.
    /// </summary>
    [Fact]
    public void RunningABlockInOneTownIsNotRunningItInTheNext()
    {
        Attempt played = Autoplayer.Play(TwoTownsOneNurse(), "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.Contains("1.0", played.Reached);
        Assert.Contains("2.0", played.Reached);

        Assert.Contains(("1.0", Shared), played.Ran.Keys);
        Assert.DoesNotContain(("2.0", Shared), played.Ran.Keys);
    }

    /// <summary>
    /// And the verdict <c>--flags</c> prints follows the map, not the address.
    /// <para>
    /// This is the assertion the printer used to make with a conditional of its own, in a file
    /// no test can reach. It is a rule about the world, so it lives on the run now — the sixth
    /// time this project has moved the same kind of line out of <c>Program.cs</c>.
    /// </para>
    /// </summary>
    [Fact]
    public void TheVerdictFollowsTheMapAndNotTheAddress()
    {
        Attempt played = Autoplayer.Play(TwoTownsOneNurse(), "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.Equal(WhereItStands.ItRanTheScriptHere, played.HowItStands("1.0", Shared));

        // The answer that did not exist while the key was an address. Not "it never ran this
        // script" either: the block DID run, in the other town, and that is worth saying.
        Assert.Equal(
            WhereItStands.ItRanTheSameBlockOnAnotherMap,
            played.HowItStands("2.0", Shared));
    }

    /// <summary>
    /// The ordinary case, asserted — which is what 195 found nobody had done.
    /// <para>
    /// A block on a map the run reached and never ran ANYWHERE is not the shared-block answer.
    /// Without this, "everything is the shared-block answer" is a break that comes back green
    /// and the discrimination the fixture is named for is not being made at all.
    /// </para>
    /// </summary>
    [Fact]
    public void ABlockNobodyRanAnywhereIsNotABlockRunSomewhereElse()
    {
        Attempt played = Autoplayer.Play(TwoTownsOneNurse(), "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.Equal(WhereItStands.ItNeverRanTheScript, played.HowItStands("2.0", 0xDEAD));
        Assert.Equal(WhereItStands.OnAMapItNeverReached, played.HowItStands("9.9", Shared));
    }

    /// <summary>
    /// The denominator is a different number from the count, and it has to be, or neither can
    /// come back empty.
    /// <para>
    /// Two places ran; one block ran. A run that reported those as one number could not say
    /// whether keying on the address cost anything, which is the trap this project has now
    /// written down four times and fallen into three.
    /// </para>
    /// </summary>
    [Fact]
    public void TwoPlacesRunningOneBlockIsTwoPlacesAndOneBlock()
    {
        var world = new WorldData(
        [
            Room("1.0") with
            {
                Warps = [new Warp(3, 1, 0, "2.0")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = Shared },
                ],
            },
            Room("2.0") with
            {
                Warps = [new Warp(3, 1, 0, "1.0")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = Shared },
                ],
            },
        ]);

        Attempt played = Autoplayer.Play(world, "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.Equal(2, played.Ran.Count);
        Assert.Single(played.RanAnywhere);
        Assert.Contains(Shared, played.RanAnywhere);

        // And the two are the same shape of number when nothing is shared, which is the half
        // that says the denominator is measuring rather than asserting.
        Assert.Equal(
            1,
            played.Ran.Keys.GroupBy(k => k.Address).Count(g => g.Count() > 1));
    }

    /// <summary>
    /// Why a script stopped does not travel between towns.
    /// <para>
    /// The sharpest half of the fault, and the one that reaches the printed output: the reason
    /// is merged across every pass, which is right, and it was merged across every MAP as
    /// well, which is not. A run that stopped at a yes-or-no in one town reported the same
    /// yes-or-no as the reason it got nowhere in the other.
    /// </para>
    /// </summary>
    [Fact]
    public void WhyItStoppedDoesNotTravelBetweenTowns()
    {
        var times = 0;

        var world = new WorldData(
        [
            Room("1.0") with
            {
                Warps = [new Warp(3, 1, 0, "2.0")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = Shared },
                ],
            },
            Room("2.0") with
            {
                Warps = [new Warp(3, 1, 0, "1.0")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = Shared },
                ],
            },
        ]);

        // The first town to run it stops at a yes-or-no. Nobody else ever does — including
        // this same block on the same map on a later pass, which is the ordinary case and is
        // covered by the fold being a union.
        Attempt played = Autoplayer.Play(
            world,
            "1.0",
            TestRules.All,
            (_, _, _) => ++times == 1 ? Nothing with { StoppedAtAQuestion = true } : Nothing);

        Assert.True(times >= 2, "both towns have to run it or there is nothing to keep apart");

        Assert.True(
            played.Ran[("1.0", Shared)].StoppedAtAQuestion,
            "the town that stopped at the question is the town that stopped at the question");

        Assert.False(
            played.Ran[("2.0", Shared)].StoppedAtAQuestion,
            "and the other town never asked anybody anything");
    }
}
