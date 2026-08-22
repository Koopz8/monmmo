using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A border asked at the SQUARE rather than at the map (286).
/// <para>
/// 265 asked the borders whether the far map declares one back — 116 joins, 114 declared, 2 not —
/// and that is a question about two map records. A walker crosses at a square, and "the far map
/// names me back" does not say that stepping back lands where you started: the two offsets can
/// disagree, or the return side can name a third map.
/// </para>
/// <para>
/// It is the loose and tight halves 265 established for the doors, one list over: <em>does this
/// door name THIS door back</em> scored 920 where <em>does it come back to this map at all</em>
/// scored 237 against a control of 233. On this cartridge the square-level test finds
/// <b>50 crossings of 2646</b> that do not round-trip — and <b>0 of the 50 are walkable at either
/// end</b>, against 976 of the 2646 overall.
/// </para>
/// </summary>
public sealed class WhereABorderComesBackToTests
{
    /// <summary>A map, walkable everywhere, with the connections given.</summary>
    private static MapData Map(string id, int width, int height, params MapConnection[] joins) =>
        new(id, id, width, height, new byte[width * height]) { Connections = [.. joins] };

    private static ACrossing Only(WorldData world, string mapId) =>
        Assert.Single(WhereABorderComesBackTo.Every(world).Where(c => c.MapId == mapId));

    /// <summary>
    /// A pair whose offsets are negatives of one another round-trips: step off, step back, and
    /// you are on the square you left. That is what 2596 of this cartridge's 2646 crossings do.
    /// </summary>
    [Fact]
    public void OffsetsThatAreNegativesOfEachOtherRoundTrip()
    {
        var world = new WorldData(
        [
            Map("1.0", 1, 4, new MapConnection(ConnectionSide.Down, 0, "1.1")),
            Map("1.1", 1, 4, new MapConnection(ConnectionSide.Up, 0, "1.0")),
        ]);

        ACrossing crossing = Only(world, "1.0");

        Assert.Equal("1.1", crossing.Other);
        Assert.True(crossing.RoundTrips);
        Assert.Equal(crossing.From, crossing.BackAt);
    }

    /// <summary>
    /// <b>THE THING.</b> Two maps that name each other, whose offsets do NOT agree: the step back
    /// lands on the right map at the wrong square. The map-level test scores this join as
    /// declared back — it is one of the 114 — and it is exactly what <c>3.11</c> SAFFRON CITY and
    /// <c>3.24</c> ROUTE 6 do, off by twelve.
    /// </summary>
    [Fact]
    public void OffsetsThatDisagreeComeBackToTheWrongSquare()
    {
        var world = new WorldData(
        [
            Map("1.0", 6, 4, new MapConnection(ConnectionSide.Down, 2, "1.1")),
            Map("1.1", 6, 4, new MapConnection(ConnectionSide.Up, 0, "1.0")),
        ]);

        ACrossing crossing = Assert.Single(
            WhereABorderComesBackTo.Every(world)
                .Where(c => c.MapId == "1.0" && c.From == new GridPosition(3, 3)));

        Assert.Equal("1.1", crossing.Other);
        Assert.Equal(new GridPosition(1, 0), crossing.To);

        // Back to the right MAP and the wrong SQUARE — which is the whole distinction.
        Assert.Equal("1.0", crossing.BackTo);
        Assert.Equal(new GridPosition(1, 3), crossing.BackAt);
        Assert.False(crossing.RoundTrips);
    }

    /// <summary>
    /// And the far map naming a THIRD map back is the other way to fail — <c>3.50</c> SEVII ISLE 6
    /// crosses north to THREE ISLAND, whose downward join names THREE ISLE PORT. 265 found the
    /// pair and could not measure it because nothing reached them; 285's fix put them in reach.
    /// </summary>
    [Fact]
    public void TheFarMapCanNameAThirdMapBack()
    {
        var world = new WorldData(
        [
            Map("1.0", 1, 4, new MapConnection(ConnectionSide.Up, 0, "1.1")),
            Map("1.1", 1, 4, new MapConnection(ConnectionSide.Down, 0, "1.2")),
            Map("1.2", 1, 4),
        ]);

        ACrossing crossing = Only(world, "1.0");

        Assert.Equal("1.1", crossing.Other);
        Assert.Equal("1.2", crossing.BackTo);
        Assert.False(crossing.RoundTrips);
    }

    /// <summary>
    /// And a far map that declares nothing on the way back comes back to NOTHING, which is a
    /// third answer rather than a round trip that happens to land oddly.
    /// </summary>
    [Fact]
    public void AFarMapThatDeclaresNothingBackComesBackToNothing()
    {
        var world = new WorldData(
        [
            Map("1.0", 1, 4, new MapConnection(ConnectionSide.Up, 0, "1.1")),
            Map("1.1", 1, 4),
        ]);

        ACrossing crossing = Only(world, "1.0");

        Assert.Null(crossing.BackTo);
        Assert.Null(crossing.BackAt);
        Assert.False(crossing.RoundTrips);
    }

    /// <summary>
    /// Every square along the edge is asked, not one per join — the fault this test class exists
    /// to find is per-square, and half of SAFFRON's bottom edge round-trips while half does not.
    /// </summary>
    [Fact]
    public void EverySquareAlongTheEdgeIsAsked()
    {
        var world = new WorldData(
        [
            Map("1.0", 5, 4, new MapConnection(ConnectionSide.Down, 0, "1.1")),
            Map("1.1", 5, 4, new MapConnection(ConnectionSide.Up, 0, "1.0")),
        ]);

        IReadOnlyList<ACrossing> found = WhereABorderComesBackTo.Every(world);

        Assert.Equal(5, found.Count(c => c.MapId == "1.0"));
        Assert.Equal(5, found.Count(c => c.MapId == "1.1"));
        Assert.All(found, c => Assert.True(c.RoundTrips));
    }

    /// <summary>
    /// A square whose row no neighbour covers is not a crossing at all (285) — so the count is
    /// the squares that can cross rather than the length of the edge.
    /// </summary>
    [Fact]
    public void ASquareNoNeighbourCoversIsNotACrossing()
    {
        var world = new WorldData(
        [
            Map("1.0", 6, 4, new MapConnection(ConnectionSide.Down, 0, "1.1")),
            Map("1.1", 2, 4, new MapConnection(ConnectionSide.Up, 0, "1.0")),
        ]);

        // Only x = 0 and x = 1 of the wide map's bottom edge reach the narrow one.
        Assert.Equal(2, WhereABorderComesBackTo.Every(world).Count(c => c.MapId == "1.0"));
    }

    /// <summary>
    /// A join to a map this world file does not hold is not a crossing either — there is nowhere
    /// to arrive and nothing to measure, and inventing an arrival is what 265's sentinel reading
    /// was written to stop.
    /// </summary>
    [Fact]
    public void AJoinToAMapTheWorldDoesNotHoldIsNotACrossing()
    {
        var world = new WorldData(
            [Map("1.0", 1, 4, new MapConnection(ConnectionSide.Down, 0, "9.9"))]);

        Assert.Empty(WhereABorderComesBackTo.Every(world));
    }

    /// <summary>
    /// The left and right edges are measured along Y, which is the other half of the arithmetic —
    /// and both of this cartridge's misaligned pairs are on the top and bottom, so nothing on the
    /// real file would have exercised this.
    /// </summary>
    [Fact]
    public void TheLeftAndRightEdgesAreMeasuredAlongTheOtherAxis()
    {
        var world = new WorldData(
        [
            Map("1.0", 4, 6, new MapConnection(ConnectionSide.Left, 2, "1.1")),
            Map("1.1", 4, 6, new MapConnection(ConnectionSide.Right, -2, "1.0")),
        ]);

        ACrossing crossing = Assert.Single(
            WhereABorderComesBackTo.Every(world)
                .Where(c => c.MapId == "1.0" && c.From == new GridPosition(0, 3)));

        Assert.Equal(new GridPosition(3, 1), crossing.To);
        Assert.True(crossing.RoundTrips);
    }
}
