using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The pieces of walkable ground a map is made of (289).
/// <para>
/// 287 counted 4019 squares of reached-but-never-stood-on ground; 288 found every one of them
/// sealed. Sealed from WHAT is this: <b>the 405 maps the widest run reaches are 945 pieces</b>,
/// 193 maps are in more than one, and the most is nineteen (SEAFOAM ISLANDS). "The run reached
/// this map" has always meant "it stood in at least one of its pieces".
/// </para>
/// <para>
/// Of the 439 pieces the walk never stands in, 47 hold a warp, 20 run along a border a neighbour
/// crosses in from, and <b>372 hold neither — 2948 squares nothing in this world file opens</b>.
/// </para>
/// </summary>
public sealed class WhatAMapIsMadeOfTests
{
    /// <summary>A one-wide column; 1 is solid.</summary>
    private static MapData Column(string id, byte[] collision, params MapConnection[] joins) =>
        new(id, id, 1, collision.Length, collision) { Connections = [.. joins] };

    private static GridPosition At(int y) => new(0, y);

    private static IReadOnlyList<APiece> Pieces(
        MapData map, IEnumerable<int>? stood = null, params MapData[] neighbours)
    {
        Dictionary<string, MapData> world = neighbours.ToDictionary(m => m.Id);

        return WhatAMapIsMadeOf.In(
            [map],
            (stood ?? []).Select(y => (map.Id, At(y))),
            surfing: false,
            id => world.GetValueOrDefault(id));
    }

    /// <summary>
    /// <b>THE THING.</b> A wall across a map makes two places, and a walk that arrives in one of
    /// them has not reached the other. CINNABAR's gym is eight of these and SAFFRON CITY is
    /// eighteen.
    /// </summary>
    [Fact]
    public void AWallAcrossAMapMakesTwoPieces()
    {
        IReadOnlyList<APiece> found = Pieces(Column("1.0", [0, 0, 1, 0]), stood: [0]);

        Assert.Equal(2, found.Count);
        Assert.Equal([2, 1], found.Select(p => p.Size));
        Assert.Equal([true, false], found.Select(p => p.StoodOn));
    }

    /// <summary>
    /// And standing anywhere in a piece marks the PIECE — the walk stands on squares and the
    /// question is about places, which is 282's rule the other way up.
    /// </summary>
    [Fact]
    public void StandingAnywhereInAPieceMarksThePiece()
    {
        IReadOnlyList<APiece> found = Pieces(Column("1.0", [0, 0, 0, 1, 0]), stood: [2]);

        Assert.Equal([3, 1], found.Select(p => p.Size));
        Assert.True(found[0].StoodOn);
        Assert.False(found[1].StoodOn);
    }

    /// <summary>A warp is counted against the piece its square is in, not against the map.</summary>
    [Fact]
    public void AWarpBelongsToThePieceItsSquareIsIn()
    {
        MapData map = Column("1.0", [0, 1, 0, 0]) with
        {
            Warps = [new Warp(0, 3, TargetWarpId: 0, "2.0")],
        };

        IReadOnlyList<APiece> found = Pieces(map, stood: [0]);

        Assert.Equal(0, found.Single(p => p.StoodOn).Warps);
        Assert.Equal(1, found.Single(p => !p.StoodOn).Warps);
    }

    /// <summary>
    /// <b>A DOOR IS NOT THE ONLY WAY IN, and leaving that out is what the first version did.</b>
    /// ROUTE 25's second piece is 270 squares of sea holding no warp — which read as ground
    /// nothing opens until the border was asked. Here the far piece runs along the map's own edge
    /// and a neighbour reaches it.
    /// </summary>
    [Fact]
    public void APieceOnADeclaredBorderIsOpenedByIt()
    {
        MapData other = Column("2.0", [0, 0, 0, 0]);

        MapData map = Column(
            "1.0", [0, 1, 0, 0], new MapConnection(ConnectionSide.Down, 0, "2.0"));

        APiece far = Pieces(map, [0], other).Single(p => !p.StoodOn);

        Assert.Equal(1, far.Crossings);
        Assert.False(far.NothingOpensIt);
    }

    /// <summary>
    /// And the same map with no join declared is a piece nothing opens — so the finding is the
    /// CONNECTION and not the fact that the piece touches an edge. ROUTE 25 declares only a Left
    /// join, which is why its sea stays in the sealed bucket after the fix.
    /// </summary>
    [Fact]
    public void AndTheSameEdgeWithNoJoinOpensNothing()
    {
        APiece far = Pieces(Column("1.0", [0, 1, 0, 0]), [0]).Single(p => !p.StoodOn);

        Assert.Equal(0, far.Crossings);
        Assert.True(far.NothingOpensIt);
    }

    /// <summary>
    /// A border a neighbour does not actually cover opens nothing either — 285's rule, and the
    /// reason the lookup is handed in rather than assumed.
    /// </summary>
    [Fact]
    public void ABorderTheNeighbourDoesNotReachOpensNothing()
    {
        // A Down join is measured along X, and this column is one square wide — so the offset is
        // what puts the neighbour out of reach of it. At 5 the arrival is at x = -5.
        //
        // The first version of this fixture set the offset to nought and made the neighbour one
        // square tall, which tests nothing at all: the join covers, and the test passed while
        // asserting the opposite of its own name.
        MapData other = Column("2.0", [0, 0, 0, 0]);

        MapData map = Column(
            "1.0", [0, 1, 0, 0], new MapConnection(ConnectionSide.Down, 5, "2.0"));

        APiece far = Pieces(map, [0], other).Single(p => !p.StoodOn);

        Assert.Equal(0, far.Crossings);
        Assert.True(far.NothingOpensIt);
    }

    /// <summary>
    /// <c>NothingOpensIt</c> takes all three: stood in, a warp, or a border. Any one of them is
    /// a way in, and a test that took two would have called ROUTE 25 open or the gym rooms shut.
    /// </summary>
    [Fact]
    public void NothingOpensItTakesAllThree()
    {
        Assert.True(new APiece("1.0", 4, StoodOn: false, Warps: 0, Crossings: 0).NothingOpensIt);
        Assert.False(new APiece("1.0", 4, StoodOn: true, Warps: 0, Crossings: 0).NothingOpensIt);
        Assert.False(new APiece("1.0", 4, StoodOn: false, Warps: 1, Crossings: 0).NothingOpensIt);
        Assert.False(new APiece("1.0", 4, StoodOn: false, Warps: 0, Crossings: 1).NothingOpensIt);
    }

    /// <summary>
    /// A map with no wall in it is one piece — otherwise "193 maps are in more than one" is a
    /// statement about the flood and not about the world.
    /// </summary>
    [Fact]
    public void AMapWithNoWallIsOnePiece()
    {
        APiece only = Assert.Single(Pieces(Column("1.0", [0, 0, 0, 0]), stood: [1]));

        Assert.Equal(4, only.Size);
        Assert.True(only.StoodOn);
    }

    /// <summary>
    /// The pieces partition the walkable squares: every one is in exactly one piece and no wall
    /// is in any. Without this a flood that visits a square twice inflates every count here.
    /// </summary>
    [Fact]
    public void ThePiecesPartitionTheWalkableGround()
    {
        MapData map = Column("1.0", [0, 1, 0, 0, 1, 0, 0, 0]);

        IReadOnlyList<APiece> found = Pieces(map, stood: [0]);

        Assert.Equal(3, found.Count);
        Assert.Equal(6, found.Sum(p => p.Size));
    }
}
