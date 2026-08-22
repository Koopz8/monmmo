using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What fences the doors the run never got near (305).
/// <para>
/// 304 answered "never got near it" 43 times out of 43 and stopped at "they are inside 287's
/// pockets", which names the ground and not the fence. Asked of 288's three fences: <b>41 sealed
/// and 2 that ordinary steps reach</b> — and that second count is one 288 says MUST BE NOUGHT.
/// </para>
/// <para>
/// It is nought once a fourth fence is named. <b>Somebody is standing in the way</b>, and it is
/// one person each: <c>1.97 MT. EMBER (42,39)</c> is fenced by person 3 one square below it behind
/// flag <c>0x0089</c>, and <c>2.1 TRAINER TOWER (15,6)</c> by person 5 behind flag <c>0x0005</c>.
/// Those two doors carry seventeen of the twenty-six maps behind 303's roots.
/// </para>
/// </summary>
public sealed class WhatFencesTheDoorTests
{
    /// <summary>A corridor: one row of open ground with walls above and below it.</summary>
    private static MapData Corridor(string id, params Warp[] warps)
    {
        var collision = new byte[24];

        for (var x = 0; x < 8; x++)
        {
            collision[x] = 1;             // row 0 solid
            collision[16 + x] = 1;        // row 2 solid
        }

        return new MapData(id, id, 8, 3, collision) { Warps = warps };
    }

    private static ADoorFenced Only(
        IReadOnlyCollection<MapData> maps,
        MapData map,
        GridPosition door,
        IEnumerable<(string, GridPosition)> stood,
        IEnumerable<(string, GridPosition, int)>? blocked = null) =>
        Assert.Single(
            WhatFencesTheDoor.For(
                maps, [(map.Id, door)], [.. stood], surfing: false, [.. blocked ?? []]));

    /// <summary>
    /// <b>THE THING.</b> The four answers told apart in one world — stood on, somebody in the way,
    /// reachable by steps with nobody there, and sealed. A fixture with one of them cannot tell a
    /// classification from a constant.
    /// </summary>
    [Fact]
    public void TheFourAnswersAreToldApart()
    {
        // Two corridors: 1.0 open end to end, 1.1 cut in half by a solid column.
        MapData open = Corridor(
            "1.0", new Warp(0, 1, 0, "9.9"), new Warp(4, 1, 0, "9.9"), new Warp(7, 1, 0, "9.9"));

        var walled = new byte[24];

        for (var x = 0; x < 8; x++)
        {
            walled[x] = 1;
            walled[16 + x] = 1;
        }

        walled[8 + 4] = 1;   // the column that seals the right half of row 1

        var cut = new MapData("1.1", "1.1", 8, 3, walled)
        {
            Warps = [new Warp(0, 1, 0, "9.9"), new Warp(7, 1, 0, "9.9")],
        };

        List<MapData> maps = [open, cut];

        List<(string, GridPosition)> stood =
            [("1.0", new GridPosition(0, 1)), ("1.1", new GridPosition(0, 1))];

        // Somebody rooted to (2,1) on the open corridor, which is the only way past.
        List<(string, GridPosition, int)> blocked = [("1.0", new GridPosition(2, 1), 7)];

        IReadOnlyList<ADoorFenced> read = WhatFencesTheDoor.For(
            maps,
            [("1.0", new GridPosition(0, 1)), ("1.0", new GridPosition(4, 1)),
             ("1.1", new GridPosition(0, 1)), ("1.1", new GridPosition(7, 1))],
            stood,
            surfing: false,
            blocked);

        Assert.Equal(WhatFences.Nothing, read[0].Fenced);
        Assert.Equal(WhatFences.SomebodyInTheWay, read[1].Fenced);
        Assert.Equal(WhatFences.Nothing, read[2].Fenced);
        Assert.Equal(WhatFences.Sealed, read[3].Fenced);
    }

    /// <summary>
    /// <b>AND THE FOURTH FENCE IS ASKED BEFORE THE THIRD</b>, because the third is a count that
    /// must be nought (288, 240). Steps reach both of these doors; what tells them apart is
    /// whether the walk's own refused squares are on every path there.
    /// </summary>
    [Fact]
    public void StepsReachingItIsNotTheAnswerWhenSomebodyIsOnTheWay()
    {
        MapData map = Corridor("1.0", new Warp(6, 1, 0, "9.9"));

        List<(string, GridPosition)> stood = [("1.0", new GridPosition(0, 1))];

        // With nobody in the way this is the count 288 says cannot happen — a door the walk could
        // have walked to and did not.
        Assert.Equal(
            WhatFences.SameGround,
            Only([map], map, new GridPosition(6, 1), stood).Fenced);

        Assert.Equal(
            WhatFences.SomebodyInTheWay,
            Only([map], map, new GridPosition(6, 1), stood,
                [("1.0", new GridPosition(3, 1), 4)]).Fenced);
    }

    /// <summary>
    /// <b>AND WHICH PERSON IS THE FENCE.</b> Each refused square is opened on its own: somebody
    /// whose stepping aside alone lets the walk through is the one to name, and a door where none
    /// of them alone does is fenced by all of them together. On this cartridge both are the first
    /// kind — one person each, in front of doors worth eight and nine maps.
    /// </summary>
    [Fact]
    public void ThePersonWhoAloneOpensItIsNamedAndTwoAbreastAreNot()
    {
        MapData map = Corridor("1.0", new Warp(6, 1, 0, "9.9"));

        List<(string, GridPosition)> stood = [("1.0", new GridPosition(0, 1))];

        // One person in a corridor one square wide: stepping aside opens it.
        Assert.Equal(
            [4],
            Only([map], map, new GridPosition(6, 1), stood,
                [("1.0", new GridPosition(3, 1), 4)]).OpenedBy);

        // Two, one behind the other: neither alone opens the way, so neither is THE fence.
        Assert.Empty(
            Only([map], map, new GridPosition(6, 1), stood,
                [("1.0", new GridPosition(3, 1), 4), ("1.0", new GridPosition(4, 1), 5)]).OpenedBy);
    }

    /// <summary>
    /// <b>THE POCKET IS FLOODED FROM THE DOOR AND NOT FROM THE WALK.</b> Asking it the other way
    /// round answers "how much ground did the walk miss", which is a fact about the run; this asks
    /// what the door's own square is joined to, which is a fact about the file (211).
    /// <para>
    /// The fixture's walk stands on a big open half and the door sits in a small sealed one, so
    /// the two floods give different numbers and only one of them is this reading.
    /// </para>
    /// </summary>
    [Fact]
    public void ThePocketIsTheDoorsOwnGround()
    {
        var collision = new byte[24];

        for (var x = 0; x < 8; x++)
        {
            collision[x] = 1;
            collision[16 + x] = 1;
        }

        collision[8 + 5] = 1;   // seals (6,1) and (7,1) off from the rest of row 1

        var map = new MapData("1.0", "1.0", 8, 3, collision)
        {
            Warps = [new Warp(7, 1, 0, "9.9")],
        };

        ADoorFenced door = Only(
            [map], map, new GridPosition(7, 1), [("1.0", new GridPosition(0, 1))]);

        Assert.Equal(WhatFences.Sealed, door.Fenced);

        // (6,1) and (7,1). The walk's own side is five squares and is not what is counted.
        Assert.Equal(2, door.Pocket);
    }

    /// <summary>
    /// <b>THE WAYS IN ARE THE WARPS INSIDE THE POCKET</b>, because steps and hops cannot leave one
    /// — so a pocket with no warp but this door is a pocket nothing in the world can put anybody
    /// in, and the door can only ever be used from its far side. Thirty-nine of the forty-three
    /// are that, the nineteen POKéMON CENTER doors among them: <b>they are exits, not entrances.</b>
    /// </summary>
    [Fact]
    public void APocketWithNoOtherWarpHasNoWayIn()
    {
        var collision = new byte[24];

        for (var x = 0; x < 8; x++)
        {
            collision[x] = 1;
            collision[16 + x] = 1;
        }

        collision[8 + 5] = 1;

        var map = new MapData("1.0", "1.0", 8, 3, collision)
        {
            Warps = [new Warp(7, 1, 0, "9.9"), new Warp(0, 1, 0, "9.9")],
        };

        // Somewhere else names this map — but at warp 1, which is outside the door's pocket.
        var elsewhere = new MapData("2.0", "2.0", 8, 3, collision)
        {
            Warps = [new Warp(0, 1, 1, "1.0")],
        };

        ADoorFenced door = Only(
            [map, elsewhere], map, new GridPosition(7, 1), [("1.0", new GridPosition(0, 1))]);

        Assert.Equal([0], door.WarpsInThePocket);
        Assert.Empty(door.LandedInFrom);
        Assert.True(door.NoWayIn);

        // And when the far side names warp 0 instead, the same pocket has a way in.
        var landing = new MapData("2.0", "2.0", 8, 3, collision)
        {
            Warps = [new Warp(0, 1, 0, "1.0")],
        };

        ADoorFenced open = Only(
            [map, landing], map, new GridPosition(7, 1), [("1.0", new GridPosition(0, 1))]);

        Assert.Equal(["2.0"], open.LandedInFrom);
        Assert.False(open.NoWayIn);
    }

    /// <summary>
    /// <b>AN UNSPECIFIED DESTINATION IS NOT WARP NOUGHT.</b> The games write 0xFF to mean "no
    /// matching warp" and put the arrival on the target warp's own square; a reader treating it as
    /// an index would land everybody on whichever door is written first in the file, which is
    /// exactly the pocket this reading is asking about.
    /// </summary>
    [Fact]
    public void AnUnspecifiedDestinationDoesNotLandInWarpNought()
    {
        MapData map = Corridor("1.0", new Warp(7, 1, 0, "9.9"));

        var elsewhere = new MapData("2.0", "2.0", 8, 3, new byte[24])
        {
            Warps = [new Warp(0, 0, Warp.Unspecified, "1.0")],
        };

        Assert.Empty(
            Only([map, elsewhere], map, new GridPosition(7, 1), [("1.0", new GridPosition(0, 1))])
                .LandedInFrom);
    }

    /// <summary>
    /// <b>AND THE LIST OF WHO IS IN THE WAY IS THE WALK'S OWN.</b> The rule about who counts as a
    /// wall — a tree nobody can shift, somebody rooted to a square, a ball on the floor that is
    /// not a wall at all — lives in the walker, and a second copy of it here would be a second
    /// walker to keep honest (223). So this walks a world and hands the walk's answer straight to
    /// the reading, which is the seam that would otherwise be guarded nowhere.
    /// </summary>
    [Fact]
    public void TheWalkersOwnAnswerIsWhatFencesTheDoor()
    {
        // A corridor with a door at the far end and somebody rooted in the middle of it.
        MapObject rooted = new(9, 5, 3, 1, Direction.Down, MovementType: 0, IsTrainer: false);

        MapData map = Corridor("1.0", new Warp(6, 1, 0, "1.0"));

        var world = new WorldData([map with { Objects = [rooted] }]);

        Reach walked = WorldWalker.Walk(world, "1.0");

        Standing who = Assert.Single(walked.People);

        Assert.Equal(new GridPosition(3, 1), who.Square);

        ADoorFenced door = Assert.Single(
            WhatFencesTheDoor.For(
                world.Maps,
                [("1.0", new GridPosition(6, 1))],
                [.. walked.Stood.Select(s => (s.MapId, s.Square))],
                surfing: false,
                [.. walked.People.Select(p => (p.MapId, p.Square, p.LocalId))]));

        Assert.Equal(WhatFences.SomebodyInTheWay, door.Fenced);
        Assert.Equal([9], door.OpenedBy);
    }

    /// <summary>
    /// And a pocket landed in only from maps the run never reaches is 303's closure again rather
    /// than a reason of its own — the reading tells the two apart, because the door worth going to
    /// look at is the one whose pocket the run could be put down in and never is.
    /// </summary>
    [Fact]
    public void LandedInFromIsSplitByWhetherTheRunGetsThere()
    {
        MapData map = Corridor("1.0", new Warp(7, 1, 0, "9.9"));

        var reachedSide = new MapData("2.0", "2.0", 8, 3, new byte[24])
        {
            Warps = [new Warp(0, 0, 0, "1.0")],
        };

        var unreachedSide = new MapData("3.0", "3.0", 8, 3, new byte[24])
        {
            Warps = [new Warp(0, 0, 0, "1.0")],
        };

        ADoorFenced door = Assert.Single(
            WhatFencesTheDoor.For(
                [map, reachedSide, unreachedSide],
                [("1.0", new GridPosition(7, 1))],
                [("1.0", new GridPosition(0, 1))],
                surfing: false,
                [],
                new HashSet<string> { "1.0", "2.0" }));

        Assert.Equal(["2.0", "3.0"], door.LandedInFrom);
        Assert.Equal(["2.0"], door.LandedInFromReached);
        Assert.False(door.OnlyFromUnreached);
    }
}
