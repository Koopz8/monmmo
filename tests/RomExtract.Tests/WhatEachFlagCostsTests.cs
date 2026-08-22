using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What each flag costs in maps (306).
/// <para>
/// The wall list is ranked by <b>who stands in a doorway</b> — a 3x3 question about a door's own
/// square — and it has been right about every blocked door this project chased. 305 broke the
/// assumption under it: <c>2.1 TRAINER TOWER</c>'s fence stands FIVE squares from the door it
/// shuts, so <c>0x0005</c> is not on the wall list and is holding nine maps.
/// </para>
/// <para>
/// Ranked by cost the list is two flags long and the two disagree on everything: <c>0x0005</c>
/// costs nine maps, is invisible to the doorway test, and <b>a script can move it</b> — live
/// content the walk never opens. <c>0x0089</c> costs eight, is visible, and <b>nothing in the file
/// moves it</b> — no player of any build can ever open it.
/// </para>
/// </summary>
public sealed class WhatEachFlagCostsTests
{
    private static MapData Map(string id, int hiddenBy = 0, params Warp[] warps) =>
        new(id, id, 4, 4, new byte[16])
        {
            Warps = warps,
            Objects = hiddenBy == 0
                ? []
                : [new MapObject(3, 5, 1, 1, Direction.Down, 0, false) { HiddenBy = hiddenBy }],
        };

    private static ADoorFenced Fenced(string from, string to, GridPosition square, int who) =>
        new(from, square, to, WhatFences.SomebodyInTheWay, 4, [0], []) { OpenedBy = [who] };

    /// <summary>
    /// <b>THE THING.</b> A count of people in doorways is not a count of maps. The fixture has one
    /// flag whose door opens onto a chain of unreached maps and one whose door opens onto a map
    /// the run reaches anyway, so a version returning a constant fails on one of the two.
    /// </summary>
    [Fact]
    public void AFlagIsPricedByWhatTheRunLosesAndNotByHowManyItHides()
    {
        List<MapData> maps =
        [
            Map("1.0", hiddenBy: 0x89, new Warp(2, 2, 0, "1.1")),
            Map("1.1", 0, new Warp(0, 0, 0, "1.2")),
            Map("1.2"),
            Map("2.0", hiddenBy: 0x05, new Warp(2, 2, 0, "2.1")),
            Map("2.1"),
        ];

        HashSet<string> reached = ["1.0", "2.0", "2.1"];

        IReadOnlyList<WhatAFlagFences> costs = WhatEachFlagCosts.In(
            maps,
            [
                Fenced("1.0", "1.1", new GridPosition(2, 2), 3),
                Fenced("2.0", "2.1", new GridPosition(2, 2), 3),
            ],
            reached);

        // 1.1 and the 1.2 behind it.
        Assert.Equal(2, costs.Single(f => f.Flag == 0x89).Cost);

        // AND A DOOR INTO A MAP THE RUN REACHES ANYWAY COSTS NOTHING — without that rule a
        // villager in front of a second way into a town is charged for the town.
        Assert.Equal(0, costs.Single(f => f.Flag == 0x05).Cost);

        // Dearest first, which is the whole point of pricing them.
        Assert.Equal(0x89, costs[0].Flag);
    }

    /// <summary>
    /// <b>AND THE DOORWAY TEST IS CARRIED BESIDE THE COST, because the two disagree.</b> Somebody
    /// standing beside the door is what the wall list can see; somebody standing across the room
    /// on the only path is what it cannot, and on this cartridge the second is the dearer of the
    /// two.
    /// </summary>
    [Fact]
    public void WhetherTheDoorwayTestCouldSeeItIsCarriedToo()
    {
        var beside = new MapData("1.0", "1.0", 8, 8, new byte[64])
        {
            Warps = [new Warp(4, 4, 0, "1.1")],
            Objects = [new MapObject(3, 5, 4, 5, Direction.Down, 0, false) { HiddenBy = 0x89 }],
        };

        var acrossTheRoom = new MapData("2.0", "2.0", 8, 8, new byte[64])
        {
            Warps = [new Warp(4, 4, 0, "2.1")],
            Objects = [new MapObject(3, 5, 0, 0, Direction.Down, 0, false) { HiddenBy = 0x05 }],
        };

        List<MapData> maps = [beside, acrossTheRoom, Map("1.1"), Map("2.1")];

        IReadOnlyList<WhatAFlagFences> costs = WhatEachFlagCosts.In(
            maps,
            [
                Fenced("1.0", "1.1", new GridPosition(4, 4), 3),
                Fenced("2.0", "2.1", new GridPosition(4, 4), 3),
            ],
            new HashSet<string> { "1.0", "2.0" });

        Assert.True(costs.Single(f => f.Flag == 0x89).InADoorway);
        Assert.False(costs.Single(f => f.Flag == 0x05).InADoorway);

        // Both cost one map, so the fixture is about the doorway test and not about the price.
        Assert.All(costs, f => Assert.Equal(1, f.Cost));
    }

    /// <summary>
    /// Only a door somebody is standing in the way of can cost anything. A sealed door is shut by
    /// the ground, and charging a flag for it would put 287's pockets on the wall list.
    /// </summary>
    [Fact]
    public void ADoorShutByTheGroundIsNobodysFault()
    {
        List<MapData> maps = [Map("1.0", hiddenBy: 0x89, new Warp(2, 2, 0, "1.1")), Map("1.1")];

        Assert.Empty(
            WhatEachFlagCosts.In(
                maps,
                [
                    new ADoorFenced(
                        "1.0", new GridPosition(2, 2), "1.1", WhatFences.Sealed, 4, [0], [])
                    {
                        OpenedBy = [3],
                    },
                ],
                new HashSet<string> { "1.0" }));
    }

    /// <summary>
    /// And the closure behind a door stops at ground the run already has, so a chain that leads
    /// back out does not charge a flag for the world on the far side.
    /// </summary>
    [Fact]
    public void TheClosureStopsAtGroundTheRunAlreadyHas()
    {
        List<MapData> maps =
        [
            Map("1.0", hiddenBy: 0x89, new Warp(2, 2, 0, "1.1")),
            Map("1.1", 0, new Warp(0, 0, 0, "1.0"), new Warp(1, 0, 0, "1.2")),
            Map("1.2"),
        ];

        WhatAFlagFences flag = Assert.Single(
            WhatEachFlagCosts.In(
                maps,
                [Fenced("1.0", "1.1", new GridPosition(2, 2), 3)],
                new HashSet<string> { "1.0" }));

        Assert.Equal(["1.1", "1.2"], flag.Behind);
    }

    /// <summary>
    /// <b>AND SOMEBODY BEHIND NO FLAG IS FILED UNDER NOUGHT rather than dropped.</b> A fence with
    /// no flag on it is one nothing can ever move either, and it is a different thing from a fence
    /// behind a flag nothing sets — an empty bucket is a fact about the population (31).
    /// </summary>
    [Fact]
    public void SomebodyBehindNoFlagIsStillAFence()
    {
        List<MapData> maps =
        [
            new MapData("1.0", "1.0", 4, 4, new byte[16])
            {
                Warps = [new Warp(2, 2, 0, "1.1")],
                Objects = [new MapObject(3, 5, 1, 1, Direction.Down, 0, false)],
            },
            Map("1.1"),
        ];

        WhatAFlagFences flag = Assert.Single(
            WhatEachFlagCosts.In(
                maps,
                [Fenced("1.0", "1.1", new GridPosition(2, 2), 3)],
                new HashSet<string> { "1.0" }));

        Assert.Equal(0, flag.Flag);
        Assert.Equal(1, flag.Cost);
    }
}
