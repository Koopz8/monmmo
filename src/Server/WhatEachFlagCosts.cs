using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>One flag, the doors the people it hides are fencing, and what that costs.</summary>
/// <param name="Flag">The flag hiding whoever is standing in the way. Nought means nobody's is.</param>
/// <param name="Doors">The doors out of reached ground that person alone is holding shut.</param>
/// <param name="Behind">The maps on the far side, and everything unreached behind those.</param>
/// <param name="InADoorway">
/// Whether any of them is standing within a square of the door it fences — <b>which is the test
/// the existing wall list is ranked by</b>, and 305 showed a fence need not be near a door.
/// </param>
public sealed record WhatAFlagFences(
    int Flag,
    IReadOnlyList<ADoorFenced> Doors,
    IReadOnlyList<string> Behind,
    bool InADoorway)
{
    /// <summary>How many maps the run loses to this one flag.</summary>
    public int Cost => Behind.Count;

    public override string ToString() =>
        $"0x{Flag:X4} fences {Doors.Count} door(s) and costs {Cost} map(s)";
}

/// <summary>
/// What each flag costs in maps (306).
/// <para>
/// The wall list is <b>ranked by who stands in a doorway</b> — a 3x3 question about a door's own
/// square — and it has been right about every blocked door this project chased. 305 broke the
/// assumption underneath it: <c>2.1 TRAINER TOWER</c>'s fence stands <b>five squares</b> from the
/// door it shuts, so flag <c>0x0005</c> is not on the wall list and is holding nine maps.
/// </para>
/// <para>
/// <b>A count of people in doorways is not a count of maps.</b> This ranks the same flags by what
/// the run actually loses: the doors each one's people fence, the maps on the far side of those,
/// and everything unreached behind them (303's closure). Ranked that way it is a different list,
/// and the reason to have both is that they disagree.
/// </para>
/// </summary>
public static class WhatEachFlagCosts
{
    /// <summary>What is behind each flag whose people fence a door.</summary>
    /// <param name="maps">Every map in the world.</param>
    /// <param name="fenced">
    /// The doors out of reached ground, already read by <see cref="WhatFencesTheDoor"/>. Only the
    /// ones somebody is standing in the way of can cost anything.
    /// </param>
    /// <param name="reached">The maps the run gets to.</param>
    public static IReadOnlyList<WhatAFlagFences> In(
        IReadOnlyCollection<MapData> maps,
        IReadOnlyCollection<ADoorFenced> fenced,
        IReadOnlyCollection<string> reached)
    {
        Dictionary<string, MapData> byId = maps.ToDictionary(m => m.Id);

        var doors = new Dictionary<int, List<ADoorFenced>>();
        var doorway = new Dictionary<int, bool>();

        foreach (ADoorFenced door in fenced.Where(d => d.Fenced == WhatFences.SomebodyInTheWay))
        {
            if (!byId.TryGetValue(door.MapId, out MapData? map)) continue;

            foreach (int who in door.OpenedBy)
            {
                if (map.Objects.FirstOrDefault(o => o.LocalId == who) is not { } person) continue;

                if (!doors.TryGetValue(person.HiddenBy, out List<ADoorFenced>? had))
                    doors[person.HiddenBy] = had = [];

                had.Add(door);

                doorway[person.HiddenBy] =
                    doorway.GetValueOrDefault(person.HiddenBy) || Near(person, door.Square);
            }
        }

        var found = new List<WhatAFlagFences>();

        foreach ((int flag, List<ADoorFenced> shut) in doors)
        {
            found.Add(new WhatAFlagFences(
                flag,
                shut,
                Behind(maps, reached, [.. shut.Select(d => d.To).Distinct()]),
                doorway.GetValueOrDefault(flag)));
        }

        return [.. found.OrderByDescending(f => f.Cost).ThenByDescending(f => f.Doors.Count).ThenBy(f => f.Flag)];
    }

    /// <summary>
    /// The unreached maps on the far side of a set of doors, and everything unreached behind them.
    /// </summary>
    /// <remarks>
    /// <b>A door into a map the run reaches anyway costs nothing</b>, and this is where that is
    /// said: the far side has to be unreached before anything behind it counts. Without it a
    /// villager standing in front of a second way into a town would be charged for the town.
    /// </remarks>
    public static IReadOnlyList<string> Behind(
        IReadOnlyCollection<MapData> maps,
        IReadOnlyCollection<string> reached,
        IReadOnlyCollection<string> targets)
    {
        Dictionary<string, MapData> byId = maps.ToDictionary(m => m.Id);

        HashSet<string> seen = [.. targets.Where(t => !reached.Contains(t) && byId.ContainsKey(t))];

        var queue = new Queue<string>(seen);

        while (queue.Count > 0)
        {
            if (!byId.TryGetValue(queue.Dequeue(), out MapData? map)) continue;

            foreach (string next in map.Warps
                         .Where(w => !w.IsDynamic)
                         .Select(w => w.TargetMapId)
                         .Concat(map.Connections.Select(c => c.MapId)))
            {
                if (reached.Contains(next) || !byId.ContainsKey(next)) continue;

                if (seen.Add(next)) queue.Enqueue(next);
            }
        }

        return [.. seen.Order(StringComparer.Ordinal)];
    }

    /// <summary>The 3x3 question the existing wall list asks.</summary>
    private static bool Near(MapObject who, GridPosition door) =>
        Math.Abs(who.X - door.X) <= 1 && Math.Abs(who.Y - door.Y) <= 1;
}
