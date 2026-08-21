using PokeMmo.Core.World;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The static half of "can it get back", which does not know the walk exists.
/// <para>
/// A warp record names a MAP and an INDEX into that map's own warp list. Nothing in the format
/// makes that a pair: the far warp is free to name a third map, the same map by another door, or
/// the sentinel meaning "wherever you came from". Whether it names this one back is a
/// measurement — <b>920 of this cartridge's 1275 ordinary warps do, 237 come back by another
/// door, and 118 are ONE WAY</b>, against a control of 219 / 233 / 823.
/// </para>
/// </summary>
public sealed class TheDoorOnTheOtherSideTests
{
    private static MapData Map(string id, params Warp[] warps) =>
        new(id, id, 8, 8, new byte[64]) { Warps = warps };

    /// <summary>
    /// A MIRRORED PAIR IS MIRRORED, and both halves of that are separate claims: the far door
    /// names this map, and it names THIS door on it. A test that only asked the first would pass
    /// on every house in the game whatever index its door held.
    /// </summary>
    [Fact]
    public void ADoorWhoseOtherSideNamesItBackIsMirrored()
    {
        ADoorAndItsOther door = TheDoorOnTheOtherSide.In(new WorldData([
            Map("1.0", new Warp(1, 1, 0, "1.1")),
            Map("1.1", new Warp(2, 2, 0, "1.0")),
        ])).First();

        Assert.True(door.NamesTheMapBack);
        Assert.True(door.NamesThisDoorBack);
    }

    /// <summary>
    /// COMING BACK TO THE MAP AND COMING BACK THROUGH THE DOOR ARE TWO ANSWERS. Most maps' doors
    /// all lead to the same place, so "names the map back" is nearly free — folding the two
    /// together is how 118 one-way doors would have been reported as none.
    /// </summary>
    [Fact]
    public void ComingBackByAnotherDoorIsNotTheSameAnswer()
    {
        ADoorAndItsOther door = TheDoorOnTheOtherSide.In(new WorldData([
            Map("1.0", new Warp(1, 1, 0, "1.1"), new Warp(4, 4, 0, "1.1")),
            Map("1.1", new Warp(2, 2, 1, "1.0")),
        ])).First();

        Assert.True(door.NamesTheMapBack);
        Assert.False(door.NamesThisDoorBack);
    }

    /// <summary>
    /// AND A ONE-WAY DOOR IS NEITHER. The lifts and SEAFOAM ISLANDS' holes are these, and nothing
    /// in this project could say so before.
    /// </summary>
    [Fact]
    public void ADoorWhoseOtherSideNamesSomewhereElseIsOneWay()
    {
        ADoorAndItsOther door = TheDoorOnTheOtherSide.In(new WorldData([
            Map("1.0", new Warp(1, 1, 0, "1.1")),
            Map("1.1", new Warp(2, 2, 0, "1.2")),
            Map("1.2", new Warp(3, 3, 0, "1.1")),
        ])).First();

        Assert.False(door.NamesTheMapBack);
        Assert.False(door.NamesThisDoorBack);
    }

    /// <summary>
    /// THE SENTINEL IS ITS OWN ANSWER AND NOT A MISS. Nineteen warps in this game name a map no
    /// bank has because the room decides at runtime where you came from; counting those as one
    /// way is counting the cartridge's own arrangement as a fault.
    /// </summary>
    [Fact]
    public void TheRuntimeSentinelIsNeitherMirroredNorOneWay()
    {
        ADoorAndItsOther door = TheDoorOnTheOtherSide.In(new WorldData([
            Map("1.0", new Warp(1, 1, 0, "127.127")),
        ])).Single();

        Assert.True(door.DecidedAtRuntime);
        Assert.False(door.NamesTheMapBack);
        Assert.False(door.NamesThisDoorBack);
    }

    /// <summary>
    /// AND THE UNSPECIFIED INDEX RESOLVES THE WAY THE WALK RESOLVES IT — warp nought. Two ideas
    /// of which door a warp even means is how the static half and the walk would come to disagree
    /// about the same door while both looked right.
    /// </summary>
    [Fact]
    public void TheUnspecifiedIndexMeansTheFirstWarpOnBothSides()
    {
        ADoorAndItsOther door = TheDoorOnTheOtherSide.In(new WorldData([
            Map("1.0", new Warp(1, 1, Warp.Unspecified, "1.1")),
            Map("1.1", new Warp(2, 2, Warp.Unspecified, "1.0")),
        ])).First();

        Assert.Equal(0, door.TargetIndex);
        Assert.True(door.NamesThisDoorBack);
    }

    /// <summary>
    /// THE CONTROL MOVES THE ANSWER, which is the only thing that makes 920 a number rather than
    /// an arithmetic certainty. Asked of the NEXT door along on the same map, a mirrored pair
    /// stops being mirrored.
    /// </summary>
    [Fact]
    public void AskingAboutTheNextDoorAlongIsADifferentAnswer()
    {
        var world = new WorldData([
            Map("1.0", new Warp(1, 1, 0, "1.1")),
            Map("1.1", new Warp(2, 2, 0, "1.0"), new Warp(5, 5, 0, "1.2")),
            Map("1.2", new Warp(3, 3, 1, "1.1")),
        ]);

        Assert.True(TheDoorOnTheOtherSide.In(world).First().NamesThisDoorBack);
        Assert.False(TheDoorOnTheOtherSide.In(world, shift: 1).First().NamesThisDoorBack);
    }

    /// <summary>
    /// THE BORDERS ARE THE THIRD KIND OF EDGE and nobody had asked them either. A join declared
    /// on one map and not the other is a one-way join with no ledge to explain it — this
    /// cartridge has exactly two, both onto THREE ISLAND.
    /// </summary>
    [Fact]
    public void AJoinDeclaredOnOneSideOnlyIsReportedAsSuch()
    {
        var world = new WorldData([
            new MapData("1.0", "ONE", 8, 8, new byte[64])
            {
                Connections = [new MapConnection(ConnectionSide.Up, 0, "1.1")],
            },
            new MapData("1.1", "TWO", 8, 8, new byte[64])
            {
                Connections = [new MapConnection(ConnectionSide.Down, 0, "1.2")],
            },
            new MapData("1.2", "THREE", 8, 8, new byte[64])
            {
                Connections = [new MapConnection(ConnectionSide.Up, 0, "1.1")],
            },
        ]);

        var borders = TheDoorOnTheOtherSide.Borders(world).ToList();

        Assert.False(borders.Single(b => b.MapId == "1.0").Back);
        Assert.True(borders.Single(b => b.MapId == "1.2").Back);
    }

    /// <summary>
    /// AND THE OPPOSITE OF A SIDE IS THE OTHER SIDE. Up against Down and Left against Right, all
    /// four — a mapping that sent Up to Up would call every border in the world one way and read
    /// like a discovery.
    /// </summary>
    [Fact]
    public void EverySideIsTheOppositeOfItsOpposite()
    {
        Assert.All(
            Enum.GetValues<ConnectionSide>(),
            side =>
            {
                Assert.NotEqual(side, TheDoorOnTheOtherSide.Opposite(side));
                Assert.Equal(
                    side,
                    TheDoorOnTheOtherSide.Opposite(TheDoorOnTheOtherSide.Opposite(side)));
            });
    }
}
