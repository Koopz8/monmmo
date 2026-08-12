using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>Why the walk stopped at a particular square.</summary>
public sealed record Frontier(string MapId, GridPosition Square, int ShiftedBy, int LocalId)
{
    public override string ToString() =>
        $"{MapId} {Square} needs move {ShiftedBy} (object {LocalId})";
}

/// <summary>What a walk of the world found.</summary>
public sealed record Reach(
    IReadOnlyCollection<string> Maps,
    IReadOnlyList<Frontier> Blocked,
    IReadOnlyCollection<string> Beyond);

/// <summary>
/// Walks the world from where a new character starts and reports how far it gets.
/// <para>
/// The instrument the roadmap asks for first. "What is stopping the story" has been an
/// impression up to now, and it does not have to be: the world file holds every square's
/// walkability, every door, every edge between maps and every tree in the way, so how much
/// of the game a character can actually reach is a computation rather than an opinion.
/// </para>
/// <para>
/// It walks squares, not maps. A map with a door into it is only reachable if somebody can
/// stand on the door, and reachability that skipped the walking would count Cerulean Cave
/// as reachable from the beginning because a warp points at it.
/// </para>
/// <para>
/// What it cannot see is anything a script decides. A guard who steps aside once a flag is
/// set is, to this, a person standing in a doorway forever. That is the honest half of the
/// answer — the frontier it reports is the *earliest* place the world closes, and the real
/// one is never further away.
/// </para>
/// </summary>
public static class WorldWalker
{
    /// <summary>
    /// Walks the world, optionally with some moves in the party and optionally as though
    /// nobody were standing in a doorway.
    /// <para>
    /// The second is the measurement that matters for planning. A person in a doorway is
    /// a wall to a walker and a wall a script opens in the game, so the difference between
    /// walking with people and walking through them is exactly the amount of the world
    /// that is gated on scripts rather than on geometry.
    /// </para>
    /// </summary>
    public static Reach Walk(
        WorldData world,
        string startMapId,
        IReadOnlyCollection<int>? moves = null,
        bool throughPeople = false)
    {
        IReadOnlyCollection<int> known = moves ?? [];

        var reached = new HashSet<string>();
        var blocked = new List<Frontier>();
        var beyond = new HashSet<string>();

        // Built once each, not once per square. Rebuilding a map's grid inside the walk
        // is the same mistake the special sweep made — an expensive thing in a loop that
        // turns four hundred maps into an afternoon — and making it twice in one day is
        // reason enough to write it down here.
        var grids = new Dictionary<string, CollisionGrid>();
        var objects = new Dictionary<string, Dictionary<GridPosition, MapObject>>();
        var maps = world.Maps.ToDictionary(m => m.Id);

        CollisionGrid GridOf(MapData map)
        {
            if (grids.TryGetValue(map.Id, out CollisionGrid? cached)) return cached;

            return grids[map.Id] = map.ToGrid();
        }

        MapObject? ObjectOn(MapData map, GridPosition square)
        {
            if (!objects.TryGetValue(map.Id, out Dictionary<GridPosition, MapObject>? on))
            {
                on = objects[map.Id] = [];

                foreach (MapObject entry in map.Objects) on.TryAdd(entry.Square, entry);
            }

            return on.GetValueOrDefault(square);
        }

        if (!maps.TryGetValue(startMapId, out MapData? start)) return new Reach(reached, blocked, beyond);

        var queue = new Queue<(MapData Map, GridPosition Square)>();

        // Where a fresh character stands is not in the world file, so the walk starts from
        // anywhere on the starting map that a person could be. Which square hardly matters
        // — a town is one connected piece — and it avoids inventing a spawn point.
        queue.Enqueue((start, GridOf(start).FirstWalkable()));

        var seen = new HashSet<(string, GridPosition)>();

        while (queue.Count > 0)
        {
            (MapData map, GridPosition from) = queue.Dequeue();

            if (!seen.Add((map.Id, from))) continue;

            reached.Add(map.Id);

            CollisionGrid grid = GridOf(map);

            foreach (Direction direction in Enum.GetValues<Direction>())
            {
                GridPosition next = from.Step(direction);

                if (!grid.Contains(next))
                {
                    // Off the edge, which is a way between maps rather than a wall.
                    if (map.ConnectionOn(SideFor(direction)) is not { } edge) continue;
                    if (!maps.TryGetValue(edge.MapId, out MapData? neighbour))
                    {
                        beyond.Add(edge.MapId);
                        continue;
                    }

                    GridPosition arrival = GameWorld.AcrossEdge(
                        from, SideFor(direction), map, neighbour, edge.Offset);

                    if (GridOf(neighbour).IsWalkable(arrival)) queue.Enqueue((neighbour, arrival));

                    continue;
                }

                if (!grid.IsWalkable(next)) continue;

                if (ObjectOn(map, next) is { } standing)
                {
                    // A tree is a wall until somebody in the party can shift it, and that
                    // is exactly the kind of wall worth reporting rather than skipping.
                    if (standing.IsObstacle && !known.Contains(standing.ShiftedBy))
                    {
                        blocked.Add(new Frontier(map.Id, next, standing.ShiftedBy, standing.LocalId));
                        continue;
                    }

                    // Anybody else standing there is in the way too, and a script may well
                    // move them — so this is not counted as a frontier, only not walked.
                    if (!standing.IsObstacle && !throughPeople) continue;
                }

                queue.Enqueue((map, next));
            }

            // Doors, which are squares like any other once they have been stood on.
            if (map.WarpAt(from) is not { } warp) continue;
            if (!maps.TryGetValue(warp.TargetMapId, out MapData? target))
            {
                beyond.Add(warp.TargetMapId);
                continue;
            }

            queue.Enqueue((target, Arrival(target, warp, GridOf(target))));
        }

        return new Reach(
            reached,
            [.. blocked.DistinctBy(b => (b.MapId, b.Square))],
            beyond);
    }

    /// <summary>
    /// Where a door puts somebody down.
    /// <para>
    /// The cartridge names a warp on the target map rather than a square, and one value
    /// means "no matching warp" — in which case the games land the player on the target
    /// warp's own square. Falling back to the first warp is the same rule the world file's
    /// own comment describes.
    /// </para>
    /// </summary>
    private static GridPosition Arrival(MapData target, Warp warp, CollisionGrid grid)
    {
        if (warp.TargetWarpId != Warp.Unspecified &&
            warp.TargetWarpId >= 0 &&
            warp.TargetWarpId < target.Warps.Count)
        {
            return target.Warps[warp.TargetWarpId].Square;
        }

        return target.Warps.Count > 0 ? target.Warps[0].Square : grid.FirstWalkable();
    }

    private static ConnectionSide SideFor(Direction direction) => direction switch
    {
        Direction.Up => ConnectionSide.Up,
        Direction.Down => ConnectionSide.Down,
        Direction.Left => ConnectionSide.Left,
        _ => ConnectionSide.Right,
    };
}
