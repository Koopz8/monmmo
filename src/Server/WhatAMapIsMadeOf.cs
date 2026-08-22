using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>One connected piece of a map's walkable ground (289).</summary>
/// <param name="MapId">The map.</param>
/// <param name="Size">Walkable squares in it.</param>
/// <param name="StoodOn">Whether the walk stood anywhere in it.</param>
/// <param name="Warps">Warps standing on its squares — the doors that open it.</param>
/// <param name="Crossings">
/// Squares in it that sit on a declared border a walker could cross in from.
/// <para>
/// <b>Left out of the first version and it mattered.</b> ROUTE 25's second piece is 270 squares
/// of sea holding no warp, which read as ground nothing opens — and it runs along the map's own
/// edge, where a walker on the next map crosses in. A door is not the only way into a place.
/// </para>
/// </param>
public sealed record APiece(
    string MapId, int Size, bool StoodOn, int Warps, int Crossings)
{
    /// <summary>
    /// A piece the walk never stood in that no warp and no border opens: ground nothing in this
    /// world file can put anybody on.
    /// </summary>
    public bool NothingOpensIt => !StoodOn && Warps == 0 && Crossings == 0;

    public override string ToString() =>
        $"{MapId}  {Size} square(s), {Warps} warp(s), {Crossings} crossing(s)"
        + (StoodOn ? ", stood on" : "");
}

/// <summary>
/// The pieces of walkable ground a map is made of (289).
/// <para>
/// <b>287 counted 4019 squares of reached-but-never-stood-on ground and 288 found every one of
/// them sealed.</b> Sealed from WHAT is the question underneath: a map's walkable ground is not
/// one place. CINNABAR ISLAND is eight pieces and the walk stands in one of them; SAFFRON CITY is
/// eighteen; ROUTE 25 is two, and the second is 270 squares of sea.
/// </para>
/// <para>
/// So "the run reached this map" has always meant "the run stood in at least one of its pieces",
/// and this is the instrument that says which. It is 282's rule a third time: reaching a map is
/// not standing on a square, standing on a square is not standing on the map, and the map itself
/// is not one thing.
/// </para>
/// <para>
/// Steps only. A ledge hop joins two pieces in play and 288 measured that nought of this
/// cartridge's fenced ground needs one, so folding hops in here would blur a boundary that is
/// already known to be clean.
/// </para>
/// </summary>
public static class WhatAMapIsMadeOf
{
    /// <summary>Every piece of every map given, biggest first within each map.</summary>
    /// <param name="maps">The maps to break up.</param>
    /// <param name="stood">Where the walk stood.</param>
    /// <param name="surfing">Whether the walk could cross water — the grid must be the walk's.</param>
    /// <param name="find">
    /// How to look a neighbouring map up. Required rather than optional: the first version of this
    /// left the border test out and read ROUTE 25's sea as ground nothing opens, and a parameter
    /// a caller can forget is that fault with a default value on it.
    /// </param>
    public static IReadOnlyList<APiece> In(
        IEnumerable<MapData> maps,
        IEnumerable<(string MapId, GridPosition Square)> stood,
        bool surfing,
        Func<string, MapData?> find)
    {
        Dictionary<string, HashSet<GridPosition>> standing = stood
            .GroupBy(s => s.MapId)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Square).ToHashSet());

        var pieces = new List<APiece>();

        foreach (MapData map in maps)
        {
            CollisionGrid grid = map.ToGrid(surfing);
            HashSet<GridPosition> here = standing.GetValueOrDefault(map.Id, []);
            HashSet<GridPosition> doors = [.. map.Warps.Select(w => w.Square)];

            var seen = new HashSet<GridPosition>();

            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var start = new GridPosition(x, y);

                    if (!grid.IsWalkable(start) || !seen.Add(start)) continue;

                    var queue = new Queue<GridPosition>();

                    queue.Enqueue(start);

                    var size = 0;
                    var walked = false;
                    var warps = 0;
                    var crossings = 0;

                    while (queue.Count > 0)
                    {
                        GridPosition at = queue.Dequeue();

                        size++;
                        if (here.Contains(at)) walked = true;
                        if (doors.Contains(at)) warps++;
                        if (OnACrossing(map, at, find)) crossings++;

                        foreach (Direction way in Enum.GetValues<Direction>())
                        {
                            GridPosition next = at.Step(way);

                            if (grid.IsWalkable(next) && seen.Add(next)) queue.Enqueue(next);
                        }
                    }

                    pieces.Add(new APiece(map.Id, size, walked, warps, crossings));
                }
            }
        }

        return [.. pieces.OrderBy(p => p.MapId, StringComparer.Ordinal).ThenByDescending(p => p.Size)];
    }

    /// <summary>
    /// Whether this square sits on an edge of its map that a neighbour actually reaches — 285's
    /// rule, because a side can carry several neighbours and only one of them covers a given row.
    /// </summary>
    private static bool OnACrossing(MapData map, GridPosition at, Func<string, MapData?> find)
    {
        if (at.X == 0 && map.ConnectionOn(ConnectionSide.Left, at, find) is not null) return true;
        if (at.X == map.Width - 1
            && map.ConnectionOn(ConnectionSide.Right, at, find) is not null) return true;
        if (at.Y == 0 && map.ConnectionOn(ConnectionSide.Up, at, find) is not null) return true;

        return at.Y == map.Height - 1
            && map.ConnectionOn(ConnectionSide.Down, at, find) is not null;
    }
}
