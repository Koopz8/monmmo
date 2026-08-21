using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Which map is across an edge, when a side carries more than one (285).
/// <para>
/// <b><c>ConnectionOn</c> took the first connection on a side for twenty milestones.</b> Exactly
/// one side in this cartridge carries more than one neighbour — <c>3.60</c> WATER PATH declares
/// GREEN PATH at offset 0, SIX ISLAND at 40 and RUIN VALLEY at 80 off its left edge — and every
/// square stepping west off it was sent to GREEN PATH whatever row it stood on. The arrival then
/// landed outside GREEN PATH's grid, failed the walkability check, and the crossing did not
/// happen at all: a fault that DELETES edges and says nothing.
/// </para>
/// <para>
/// It cost <b>7 maps, 1305 squares and 5848 squares that could not get back</b> — the whole Sevii
/// 6/7 cluster, whose only way out is that one edge. A blast radius of one join (9).
/// </para>
/// </summary>
public sealed class WhichNeighbourIsOnThatSideTests
{
    private const int Wide = 4;

    /// <summary>A map of a given size with the connections given.</summary>
    private static MapData Map(string id, int width, int height, params MapConnection[] joins) =>
        new(id, id, width, height, new byte[width * height]) { Connections = [.. joins] };

    /// <summary>
    /// The tall map, with three short neighbours stacked along its left edge — the shape
    /// <c>3.60</c> has and the only shape in the cartridge that can tell the two rules apart.
    /// </summary>
    private static (MapData Tall, Dictionary<string, MapData> World) Stacked()
    {
        MapData tall = Map(
            "1.0", Wide, 30,
            new MapConnection(ConnectionSide.Left, 0, "1.1"),
            new MapConnection(ConnectionSide.Left, 10, "1.2"),
            new MapConnection(ConnectionSide.Left, 20, "1.3"));

        Dictionary<string, MapData> world = new()
        {
            ["1.0"] = tall,
            ["1.1"] = Map("1.1", Wide, 10),
            ["1.2"] = Map("1.2", Wide, 10),
            ["1.3"] = Map("1.3", Wide, 10),
        };

        return (tall, world);
    }

    private static MapConnection? Across(GridPosition from)
    {
        (MapData tall, Dictionary<string, MapData> world) = Stacked();

        return tall.ConnectionOn(ConnectionSide.Left, from, id => world.GetValueOrDefault(id));
    }

    /// <summary>
    /// <b>THE THING.</b> Three neighbours on one side, and which one you get depends on the row
    /// you step off from. The old rule answered the first for all thirty rows.
    /// </summary>
    [Theory]
    [InlineData(0, "1.1")]
    [InlineData(9, "1.1")]
    [InlineData(10, "1.2")]
    [InlineData(19, "1.2")]
    [InlineData(20, "1.3")]
    [InlineData(29, "1.3")]
    public void TheNeighbourIsTheOneWhoseGridCoversTheCrossing(int y, string expected)
    {
        Assert.Equal(expected, Across(new GridPosition(0, y))?.MapId);
    }

    /// <summary>
    /// And a row no neighbour reaches is no crossing, rather than the first one — which is the
    /// honest answer and the one the walk needs, because the alternative is an arrival square
    /// off the far map's grid.
    /// </summary>
    [Fact]
    public void ARowNoNeighbourReachesIsNoCrossing()
    {
        MapData tall = Map(
            "1.0", Wide, 30, new MapConnection(ConnectionSide.Left, 0, "1.1"));

        Dictionary<string, MapData> world = new()
        {
            ["1.0"] = tall,
            ["1.1"] = Map("1.1", Wide, 10),
        };

        Assert.NotNull(tall.ConnectionOn(
            ConnectionSide.Left, new GridPosition(0, 5), id => world.GetValueOrDefault(id)));

        Assert.Null(tall.ConnectionOn(
            ConnectionSide.Left, new GridPosition(0, 25), id => world.GetValueOrDefault(id)));
    }

    /// <summary>
    /// A neighbour this world file does not hold cannot be measured, so it is the answer of last
    /// resort rather than no answer — a caller counting "maps this file lacks" must still see it.
    /// </summary>
    [Fact]
    public void ANeighbourTheWorldDoesNotHoldIsStillReported()
    {
        MapData map = Map("1.0", Wide, 30, new MapConnection(ConnectionSide.Left, 0, "9.9"));

        Assert.Equal("9.9", map.ConnectionOn(
            ConnectionSide.Left, new GridPosition(0, 25), _ => null)?.MapId);
    }

    /// <summary>
    /// And a KNOWN neighbour that covers the crossing beats an unknown one, whichever comes
    /// first in the list — otherwise a missing map at the top of a side would hide a real join.
    /// </summary>
    [Fact]
    public void AKnownNeighbourThatCoversItBeatsAnUnknownOne()
    {
        MapData map = Map(
            "1.0", Wide, 30,
            new MapConnection(ConnectionSide.Left, 0, "9.9"),
            new MapConnection(ConnectionSide.Left, 10, "1.2"));

        Dictionary<string, MapData> world = new() { ["1.2"] = Map("1.2", Wide, 10) };

        Assert.Equal("1.2", map.ConnectionOn(
            ConnectionSide.Left, new GridPosition(0, 15), id => world.GetValueOrDefault(id))?.MapId);
    }

    /// <summary>
    /// <b>And the crossing has to be ABOVE the neighbour's offset as well as below its far end.</b>
    /// A neighbour set ten rows down does not reach row five, and a rule that only checked the far
    /// end would hand it back with a negative arrival — an off-grid square, and the same silent
    /// refusal the whole fault was made of. Listed first here on purpose: with only the far end
    /// checked it wins.
    /// </summary>
    [Fact]
    public void ANeighbourSetBelowTheCrossingDoesNotCoverIt()
    {
        MapData map = Map(
            "1.0", Wide, 30,
            new MapConnection(ConnectionSide.Left, 10, "1.1"),
            new MapConnection(ConnectionSide.Left, 0, "1.2"));

        Dictionary<string, MapData> world = new()
        {
            ["1.1"] = Map("1.1", Wide, 10),
            ["1.2"] = Map("1.2", Wide, 10),
        };

        Assert.Equal("1.2", map.ConnectionOn(
            ConnectionSide.Left, new GridPosition(0, 5), id => world.GetValueOrDefault(id))?.MapId);
    }

    /// <summary>
    /// The top and bottom edges are measured along X rather than Y, which is the other half of
    /// the rule and the half a fixture about one side alone would never reach.
    /// </summary>
    [Fact]
    public void TheTopAndBottomEdgesAreMeasuredAlongTheOtherAxis()
    {
        MapData map = Map(
            "1.0", 30, Wide,
            new MapConnection(ConnectionSide.Up, 0, "1.1"),
            new MapConnection(ConnectionSide.Up, 10, "1.2"));

        Dictionary<string, MapData> world = new()
        {
            ["1.1"] = Map("1.1", 10, Wide),
            ["1.2"] = Map("1.2", 10, Wide),
        };

        Assert.Equal("1.1", map.ConnectionOn(
            ConnectionSide.Up, new GridPosition(5, 0), id => world.GetValueOrDefault(id))?.MapId);
        Assert.Equal("1.2", map.ConnectionOn(
            ConnectionSide.Up, new GridPosition(15, 0), id => world.GetValueOrDefault(id))?.MapId);
    }

    // ------------------------------------------------------------------ and the walk

    /// <summary>
    /// <b>THE WALK, which is where the fault actually lived.</b> Stepping west off the far row
    /// of the tall map reaches the third neighbour — and under the old rule it reached nothing at
    /// all, because the arrival landed off the first neighbour's grid and was refused.
    /// </summary>
    [Fact]
    public void TheWalkCrossesToTheNeighbourThatIsActuallyThere()
    {
        (MapData tall, Dictionary<string, MapData> maps) = Stacked();

        var world = new WorldData([.. maps.Values]);

        Reach reach = WorldWalker.Walk(world, tall.Id);

        Assert.Contains("1.1", reach.Maps);
        Assert.Contains("1.2", reach.Maps);
        Assert.Contains("1.3", reach.Maps);
    }

    /// <summary>
    /// And the control, in the same process: with one neighbour per side the walk reaches the
    /// first and neither of the others. This is the difference the fix is worth, and it is a
    /// subtraction rather than a memory of an earlier build (241).
    /// </summary>
    [Fact]
    public void AndUnderTheOldRuleItReachesOnlyTheFirst()
    {
        (MapData tall, Dictionary<string, MapData> maps) = Stacked();

        var world = new WorldData([.. maps.Values]);

        Reach reach = WorldWalker.Walk(world, tall.Id, firstOnEachSide: true);

        Assert.Contains("1.1", reach.Maps);
        Assert.DoesNotContain("1.2", reach.Maps);
        Assert.DoesNotContain("1.3", reach.Maps);
    }

    /// <summary>
    /// And it is a way BACK as well as a way there — which is the half that cost 5848 squares,
    /// because the Sevii cluster is entered from the middle neighbour and left the same way.
    /// </summary>
    [Fact]
    public void AndTheCrossingGoesBothWays()
    {
        (MapData tall, Dictionary<string, MapData> maps) = Stacked();

        // The middle neighbour declares the tall map back, which is what SIX ISLAND does. The
        // other two are left out here: they declare nothing back and would be stranded on their
        // own account, which is a true answer about a different question.
        var world = new WorldData(
        [
            tall,
            maps["1.2"] with
            {
                Connections = [new MapConnection(ConnectionSide.Right, -10, "1.0")],
            },
        ]);

        var steps = new List<AStepTaken>();

        Reach reach = WorldWalker.Walk(world, "1.2", steps: steps);

        Assert.Contains("1.0", reach.Maps);

        Assert.Empty(TheWayBack.Stranded(
            reach.Stood.Select(s => (s.MapId, s.Square)),
            steps.Select(s => ((s.From.MapId, s.From.Square), (s.To.MapId, s.To.Square))),
            (reach.Start.MapId, reach.Start.Square)));
    }
}
