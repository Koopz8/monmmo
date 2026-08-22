namespace PokeMmo.Core.World;

/// <summary>Why one map is not reached.</summary>
public enum WhyUnreached
{
    /// <summary>The run gets there.</summary>
    Reached,

    /// <summary>
    /// Nothing in the whole file names it — no warp, no border. A fact about the FILE, so it must
    /// not move with a lever (211).
    /// </summary>
    NoWayInAtAll,

    /// <summary>
    /// Everything that names it is itself unreached. Not a reason of its own — it follows from
    /// whatever stopped the maps upstream.
    /// </summary>
    OnlyFromSomewhereUnreached,

    /// <summary>
    /// Something the run DOES reach names it, and the run still does not get there. <b>These are
    /// the reasons.</b>
    /// </summary>
    NamedFromReachedGround,
}

/// <summary>One unreached map and what names it.</summary>
public sealed record Unreached(
    string MapId,
    string Name,
    WhyUnreached Why,
    IReadOnlyList<string> NamedByWarp,
    IReadOnlyList<string> NamedByBorder)
{
    /// <summary>The maps that name this one and that the run reaches.</summary>
    public IReadOnlyList<string> FromReached { get; init; } = [];
}

/// <summary>
/// Why the maps a run never reaches are never reached (303).
/// <para>
/// The floor table has said "388 of 425" for milestones and nothing has ever asked what the other
/// thirty-seven are. <b>A count of unreached maps is not a count of reasons.</b> Most of them are
/// downstream of one another — a map behind a map behind a map — and the number worth having is
/// the count of ROOTS: the maps something the run actually stands on names, that it still cannot
/// get to.
/// </para>
/// <para>
/// The three buckets are one classification and the first is about the FILE rather than about the
/// run, so it must read the same at every lever setting. That is 211's rule and it is what caught
/// a wrong label there: <c>--the-floor</c> prints the bucket at all six.
/// </para>
/// </summary>
public static class WhyTheRestAreUnreached
{
    /// <summary>
    /// Sort every map by whether the run reaches it and, when it does not, by what names it.
    /// </summary>
    /// <param name="reached">
    /// The maps the run stands on. Pass the UNION of every setting to ask the question of the
    /// world rather than of one lever (283).
    /// </param>
    public static IReadOnlyList<Unreached> In(
        IReadOnlyCollection<MapData> maps, IReadOnlyCollection<string> reached)
    {
        var byWarp = new Dictionary<string, SortedSet<string>>();
        var byBorder = new Dictionary<string, SortedSet<string>>();

        foreach (MapData map in maps)
        {
            // THE SENTINEL IS NOT A WAY IN. A warp whose target is the runtime's own marker names
            // no map at all, and counting it would give every lift cabin a door from everywhere
            // (265, 287).
            foreach (Warp warp in map.Warps.Where(w => !w.IsDynamic))
                Add(byWarp, warp.TargetMapId, map.Id);

            foreach (MapConnection border in map.Connections) Add(byBorder, border.MapId, map.Id);
        }

        var found = new List<Unreached>();

        foreach (MapData map in maps)
        {
            SortedSet<string> warps = byWarp.GetValueOrDefault(map.Id, []);
            SortedSet<string> borders = byBorder.GetValueOrDefault(map.Id, []);

            List<string> from = [.. warps.Concat(borders).Distinct().Where(reached.Contains).Order()];

            WhyUnreached why = reached.Contains(map.Id)
                ? WhyUnreached.Reached
                : warps.Count == 0 && borders.Count == 0
                    ? WhyUnreached.NoWayInAtAll
                    : from.Count == 0
                        ? WhyUnreached.OnlyFromSomewhereUnreached
                        : WhyUnreached.NamedFromReachedGround;

            found.Add(new Unreached(map.Id, map.Name, why, [.. warps], [.. borders])
            {
                FromReached = from,
            });
        }

        return found;
    }

    /// <summary>
    /// How many unreached maps sit behind each root — the forward closure over what each unreached
    /// map names, starting at the root.
    /// </summary>
    /// <remarks>
    /// <b>A count is not a ranking</b> (trap 3). Eight roots is the honest count of reasons, and
    /// the number that says which reason MATTERS is how much of the world is behind it. A map can
    /// sit behind two roots and is counted for both, because taking either away would not open it
    /// — the alternative is to credit it to neither.
    /// </remarks>
    public static IReadOnlyDictionary<string, int> WhatEachRootCosts(
        IReadOnlyCollection<MapData> maps, IReadOnlyList<Unreached> sorted)
    {
        HashSet<string> unreached = [.. sorted.Where(u => u.Why != WhyUnreached.Reached).Select(u => u.MapId)];

        var leadsTo = new Dictionary<string, List<string>>();

        foreach (MapData map in maps.Where(m => unreached.Contains(m.Id)))
        {
            var onwards = new List<string>();

            foreach (Warp warp in map.Warps.Where(w => !w.IsDynamic))
                if (unreached.Contains(warp.TargetMapId)) onwards.Add(warp.TargetMapId);

            foreach (MapConnection border in map.Connections)
                if (unreached.Contains(border.MapId)) onwards.Add(border.MapId);

            leadsTo[map.Id] = onwards;
        }

        var cost = new Dictionary<string, int>();

        foreach (Unreached root in sorted.Where(u => u.Why == WhyUnreached.NamedFromReachedGround))
        {
            var seen = new HashSet<string> { root.MapId };
            var queue = new Queue<string>([root.MapId]);

            while (queue.Count > 0)
                foreach (string next in leadsTo.GetValueOrDefault(queue.Dequeue(), []))
                    if (seen.Add(next))
                        queue.Enqueue(next);

            cost[root.MapId] = seen.Count;
        }

        return cost;
    }

    private static void Add(Dictionary<string, SortedSet<string>> into, string key, string value)
    {
        if (!into.TryGetValue(key, out SortedSet<string>? set)) into[key] = set = [];

        set.Add(value);
    }
}
