using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Walkable ground inside a REACHED map that the walk never stood on (287).
/// <para>
/// 282 established that reaching a map is not standing on a square. The corollary nothing had
/// asked is that a map can be reached and still hold ground nothing walks to: on this cartridge
/// <b>163 of the 405 maps the widest run reaches hold 4019 such squares</b>, and the largest
/// single explanation is nineteen POKéMON CENTERS with the same 12-of-86 pocket behind the
/// counter — which is where the CABLE CLUB's door is, and why <c>0.1</c> and <c>0.4</c> have
/// nineteen warps naming them and are entered at no setting.
/// </para>
/// <para>
/// It also closed 283's leftover: <b>18 of the 18</b> sign scripts filed as "it reached the map
/// and never got to that wall" stand in front of walkable ground no run stands on. The whole
/// bucket was pockets.
/// </para>
/// </summary>
public sealed class WhatTheWalkFencedOffTests
{
    /// <summary>A one-by-N corridor, walkable except where said.</summary>
    private static MapData Strip(string id, params byte[] collision) =>
        new(id, id, 1, collision.Length, collision);

    private static (string, GridPosition) At(string map, int y) => (map, new GridPosition(0, y));

    /// <summary>
    /// <b>THE THING.</b> A map the walk reached, with walkable squares it never stood on, is a
    /// pocket — and the number is the squares, not the map.
    /// </summary>
    [Fact]
    public void GroundOnAReachedMapThatNothingStoodOnIsAPocket()
    {
        var world = new WorldData([Strip("1.0", 0, 0, 0, 0)]);

        APocket pocket = Assert.Single(
            WhatTheWalkFencedOff.In(world, [At("1.0", 0), At("1.0", 1)], ["1.0"], surfing: false));

        Assert.Equal("1.0", pocket.MapId);
        Assert.Equal(4, pocket.Walkable);
        Assert.Equal(2, pocket.StoodOn);
        Assert.Equal(2, pocket.Fenced);
    }

    /// <summary>A map the walk stood on all of holds no pocket and is not in the list.</summary>
    [Fact]
    public void AMapItStoodOnAllOfIsNotAPocket()
    {
        var world = new WorldData([Strip("1.0", 0, 0)]);

        Assert.Empty(WhatTheWalkFencedOff.In(
            world, [At("1.0", 0), At("1.0", 1)], ["1.0"], surfing: false));
    }

    /// <summary>
    /// <b>AND AN UNREACHED MAP IS NOT A POCKET.</b> Nothing stood on any of it, which is a REACH
    /// problem — and 249's fault was exactly letting those two share a number. Without this the
    /// biggest "pocket" in the game is every map the run never gets to.
    /// </summary>
    [Fact]
    public void AMapItNeverReachedIsAReachProblemAndNotAPocket()
    {
        var world = new WorldData([Strip("1.0", 0, 0, 0, 0), Strip("2.0", 0, 0, 0, 0)]);

        APocket pocket = Assert.Single(
            WhatTheWalkFencedOff.In(world, [At("1.0", 0)], ["1.0"], surfing: false));

        Assert.Equal("1.0", pocket.MapId);
    }

    /// <summary>
    /// A wall is not fenced-off ground — the denominator is what somebody could stand on, not
    /// the size of the map. Otherwise every wall in the game is a pocket.
    /// </summary>
    [Fact]
    public void AWallIsNotGroundNothingStoodOn()
    {
        var world = new WorldData([Strip("1.0", 0, 1, 1, 0)]);

        APocket pocket = Assert.Single(
            WhatTheWalkFencedOff.In(world, [At("1.0", 0)], ["1.0"], surfing: false));

        Assert.Equal(2, pocket.Walkable);
        Assert.Equal(1, pocket.Fenced);
    }

    /// <summary>
    /// <b>The grid has to be the WALK's.</b> Water is ground to a swimmer and a wall to everybody
    /// else, so measuring a walking run's pocket with the water open reports the sea as ground it
    /// failed to reach.
    /// </summary>
    [Fact]
    public void TheWaterCountsOnlyWhenTheWalkCouldSwim()
    {
        MapData map = Strip("1.0", 0, 0, 0, 0) with
        {
            Behaviours =
            [
                0, 0, (byte)MetatileBehaviour.PondWater, (byte)MetatileBehaviour.PondWater,
            ],
        };

        var world = new WorldData([map]);

        Assert.Empty(WhatTheWalkFencedOff.In(
            world, [At("1.0", 0), At("1.0", 1)], ["1.0"], surfing: false));

        APocket swimming = Assert.Single(WhatTheWalkFencedOff.In(
            world, [At("1.0", 0), At("1.0", 1)], ["1.0"], surfing: true));

        Assert.Equal(2, swimming.Fenced);
    }

    /// <summary>
    /// A square stood on twice is one square. The walk records an arrival every time it enters a
    /// square, so counting arrivals would make a busy map report a NEGATIVE pocket.
    /// </summary>
    [Fact]
    public void ASquareStoodOnTwiceIsOneSquare()
    {
        var world = new WorldData([Strip("1.0", 0, 0, 0)]);

        APocket pocket = Assert.Single(WhatTheWalkFencedOff.In(
            world, [At("1.0", 0), At("1.0", 0), At("1.0", 1)], ["1.0"], surfing: false));

        Assert.Equal(2, pocket.StoodOn);
        Assert.Equal(1, pocket.Fenced);
    }

    /// <summary>Biggest pocket first, because a list ranked by map id buries the finding.</summary>
    [Fact]
    public void TheBiggestPocketComesFirst()
    {
        var world = new WorldData([Strip("1.0", 0, 0), Strip("2.0", 0, 0, 0, 0, 0)]);

        IReadOnlyList<APocket> found = WhatTheWalkFencedOff.In(
            world, [At("1.0", 0), At("2.0", 0)], ["1.0", "2.0"], surfing: false);

        Assert.Equal(["2.0", "1.0"], found.Select(p => p.MapId));
    }
}
