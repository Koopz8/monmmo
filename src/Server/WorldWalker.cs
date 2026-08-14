using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>Why the walk stopped at a particular square.</summary>
public sealed record Frontier(string MapId, GridPosition Square, int ShiftedBy, int LocalId)
{
    public override string ToString() =>
        $"{MapId} {Square} needs move {ShiftedBy} (object {LocalId})";
}

/// <summary>Somebody standing where a walk wanted to go, who will never move on their own.</summary>
public sealed record Standing(string MapId, GridPosition Square, int LocalId, int MovementType)
{
    public override string ToString() =>
        $"{MapId} {Square} is somebody (object {LocalId}, movement {MovementType})";
}

/// <summary>What a walk of the world found.</summary>
public sealed record Reach(
    IReadOnlyCollection<string> Maps,
    IReadOnlyList<Frontier> Blocked,
    IReadOnlyCollection<string> Beyond)
{
    /// <summary>
    /// Everybody the walk could not walk through: rooted to the spot, and not a thing
    /// that can be picked up.
    /// <para>
    /// This is "who was in the way", not "who is costing you the world". Most of them
    /// are standing in the open and gate nothing. Which of them is a gate is a second
    /// question with a second answer — walk again with <c>asIfGone</c> naming them and
    /// see what opens — and that is how two fossils on the floor of MT. MOON turned out
    /// to be worth 137 maps each.
    /// </para>
    /// </summary>
    public IReadOnlyList<Standing> People { get; init; } = [];


    /// <summary>
    /// Every square actually stood on, not just every map arrived at.
    /// <para>
    /// A map counts as reached the moment one square of it is, and most of the awkward
    /// questions are about the rest of it: a door on a map you have been to is not a
    /// door you can get to. This is what makes "why did it not go through there" a
    /// question with an answer.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<(string MapId, GridPosition Square)> Stood { get; init; } = [];
}

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
        bool throughPeople = false,
        bool surfing = false,
        IReadOnlyDictionary<byte, Direction>? hops = null,
        IReadOnlyCollection<(string MapId, int LocalId)>? asIfGone = null)
    {
        IReadOnlyCollection<int> known = moves ?? [];

        var reached = new HashSet<string>();
        var blocked = new List<Frontier>();
        var standing = new List<Standing>();
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

            // Surfing is walked as one grid rather than as two states, because the walker
            // is measuring reach rather than playing: somebody who can surf can be on
            // the water or off it wherever they like, and the union is what they reach.
            return grids[map.Id] = map.ToGrid(surfing);
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

                // A ledge, hopped the way it is meant to be hopped. Checked before
                // walkability rather than after, because every ledge square in the world
                // is solid in the block data — that is what makes a ledge a ledge — so a
                // walker that asked "can I stand there" first would never ask this.
                if (map.HopOnto(next, direction, hops) is { } landing)
                {
                    if (ObjectOn(map, landing) is null || throughPeople) queue.Enqueue((map, landing));

                    continue;
                }

                if (!grid.IsWalkable(next)) continue;

                if (ObjectOn(map, next) is { } person)
                {
                    // A tree is a wall until somebody in the party can shift it, and that
                    // is exactly the kind of wall worth reporting rather than skipping.
                    if (person.IsObstacle && !known.Contains(person.ShiftedBy))
                    {
                        blocked.Add(new Frontier(map.Id, next, person.ShiftedBy, person.LocalId));
                        continue;
                    }

                    // Anybody else is in the way for as long as they are there, and how
                    // long that is depends on what they are: somebody with a beat to
                    // walk will step off this square by themselves, and somebody rooted
                    // to it never will.
                    //
                    // Walking through the first kind is not optimism, it is what a
                    // player does — you wait a second and go past. Nor is walking through
                    // a ball on the floor: you pick it up and it is gone. Both were walls
                    // here, and between them they are why this walker reported a world 34
                    // maps large. What was standing between the player and CERULEAN was a
                    // POKe BALL lying in a corridor in MT. MOON.
                    bool gone = asIfGone?.Contains((map.Id, person.LocalId)) == true;

                    if (!gone && !person.IsObstacle && !throughPeople && !person.CanStepAside && !person.CanBeTakenAway)
                    {
                        if (!standing.Any(s => s.MapId == map.Id && s.Square == next))
                            standing.Add(new Standing(map.Id, next, person.LocalId, person.MovementType));

                        continue;
                    }
                }

                queue.Enqueue((map, next));
            }

            // Doors, which are squares like any other once they have been stood on.
            if (map.WarpAt(from) is not { } warp) continue;

            // A door that leads back the way you came leads nowhere new, and counting it
            // as a map this world file does not have is how nineteen ordinary exits came
            // to be reported as holes.
            if (warp.IsDynamic) continue;

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
            beyond)
        {
            Stood = seen,

            // Only the ones still standing at the end. A person the walk got past by
            // another route is not a gate, and reporting them as one would bury the
            // handful that are in a list of six hundred that are not.
            People = [.. standing.Where(s => !seen.Contains((s.MapId, s.Square)))],
        };
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
    public static GridPosition Arrival(MapData target, Warp warp, CollisionGrid grid)
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
