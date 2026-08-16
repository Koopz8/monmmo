namespace PokeMmo.Core.World;

/// <summary>Why one map could be got to, which is as interesting as whether.</summary>
public enum HowReached
{
    /// <summary>Where the game starts.</summary>
    TheBeginning,

    /// <summary>Walked over the edge of the map before it.</summary>
    OverTheEdge,

    /// <summary>Through a door standing on a square.</summary>
    ThroughADoor,

    /// <summary>Through a door a script makes, which is on no square.</summary>
    ThroughAScriptedDoor,

    /// <summary>By boat.</summary>
    ByBoat,
}

/// <summary>One map the walk got to, and how.</summary>
public sealed record Reached(string MapId, HowReached How, string From, int Steps);

/// <summary>
/// A door that names somewhere this world file does not contain.
/// <para>
/// The single most useful thing this walk can report. A door to nowhere is either a door the
/// cartridge never uses or a map the exporter dropped, and those two want opposite responses
/// — so it is counted and named rather than resolved.
/// </para>
/// </summary>
public sealed record DoorToNowhere(string OnMap, string Names, HowReached Kind);

/// <summary>What the walk found.</summary>
public sealed record StoryReach(
    IReadOnlyList<Reached> Got,
    IReadOnlyList<string> DidNot,
    IReadOnlyList<DoorToNowhere> Nowhere)
{
    public int Total => Got.Count + DidNot.Count;

    /// <summary>Whether a given map can be got to at all.</summary>
    public bool CanGetTo(string mapId) => Got.Any(r => r.MapId == mapId);

    /// <summary>How far from the beginning the furthest thing reached is.</summary>
    public int Furthest => Got.Count == 0 ? 0 : Got.Max(r => r.Steps);
}

/// <summary>
/// Walking the whole world from where the game starts, and saying where it stops.
/// <para>
/// <b>This derives the route rather than following one.</b> That distinction is the entire
/// point of the class and is worth stating before anybody improves it.
/// </para>
/// <para>
/// A walkthrough for this game is easy to find and would have been easy to encode: eight gyms
/// in a known order, a known list of doors between them. A report built that way can only ever
/// confirm that the walkthrough is right. It cannot find that a door is missing from the world
/// file, because it never asks the world file where the doors are — and a missing door is the
/// entire question being asked.
/// </para>
/// <para>
/// So this starts at one map and asks the exported world what leads out of it, over and over,
/// until nothing new is reachable. Whatever it arrives at is the answer. A walkthrough is
/// useful afterwards, as something to disagree with: if this walk cannot reach a gym that
/// every guide says is on the way to the next one, that disagreement is a finding, and it is a
/// finding this could not have produced if the guide had been the input.
/// </para>
/// </summary>
public static class StoryWalk
{
    /// <summary>
    /// Everywhere reachable from a starting map, everywhere not, and every door to nowhere.
    /// <para>
    /// No badges, no items, no obstacles — this is the walk a player could make if nothing
    /// were locked. That is deliberate for a first measurement: it separates "the world file
    /// does not connect" from "the player cannot yet get through", and only the first of
    /// those is a defect. Anything unreachable here is unreachable for <em>everyone</em>,
    /// for ever, however many badges they have.
    /// </para>
    /// </summary>
    public static StoryReach From(WorldData world, string startingMapId)
    {
        var known = world.Maps.ToDictionary(m => m.Id);

        var got = new Dictionary<string, Reached>();
        var nowhere = new List<DoorToNowhere>();

        if (!known.ContainsKey(startingMapId))
        {
            // Nowhere to begin. Said as an empty walk rather than as an exception, because
            // "the starting map is not in this world file" is itself a finding and the
            // caller should get to print it.
            return new StoryReach([], [.. world.Maps.Select(m => m.Id).Order()], []);
        }

        got[startingMapId] = new Reached(startingMapId, HowReached.TheBeginning, "", 0);

        var frontier = new Queue<string>();

        frontier.Enqueue(startingMapId);

        while (frontier.Count > 0)
        {
            string here = frontier.Dequeue();

            MapData map = known[here];

            int steps = got[here].Steps + 1;

            foreach ((string names, HowReached how) in WaysOut(map))
            {
                if (!known.ContainsKey(names))
                {
                    // Counted once per door rather than once per missing map, because two
                    // doors to the same missing place are two separate things somebody
                    // walked into.
                    if (!nowhere.Any(n => n.OnMap == here && n.Names == names && n.Kind == how))
                        nowhere.Add(new DoorToNowhere(here, names, how));

                    continue;
                }

                if (got.ContainsKey(names)) continue;

                got[names] = new Reached(names, how, here, steps);

                frontier.Enqueue(names);
            }
        }

        return new StoryReach(
            [.. got.Values.OrderBy(r => r.Steps).ThenBy(r => r.MapId)],
            [.. world.Maps.Select(m => m.Id).Where(id => !got.ContainsKey(id)).Order()],
            nowhere);
    }

    /// <summary>
    /// Everywhere one map says you can go, of every kind.
    /// <para>
    /// Four kinds, and the last two are the reason this is worth doing at all. A world file
    /// that knew only about squares left 179 maps of 425 with nothing leading in — the doors
    /// a script makes and the boat are what join the rest of the world on.
    /// </para>
    /// </summary>
    private static IEnumerable<(string Names, HowReached How)> WaysOut(MapData map)
    {
        foreach (MapConnection joined in map.Connections)
            yield return (joined.MapId, HowReached.OverTheEdge);

        foreach (Warp door in map.Warps)
            yield return (door.TargetMapId, HowReached.ThroughADoor);

        foreach (ScriptedDoor made in map.Doors)
            yield return (made.TargetMapId, HowReached.ThroughAScriptedDoor);
    }

    /// <summary>
    /// The boat, joined separately because it is the one door whose far side is a number
    /// rather than a map.
    /// <para>
    /// Every dock is reachable from every other dock, so they are all joined to each other
    /// once any one of them is reached. Kept out of <see cref="WaysOut"/> because that method
    /// answers "what does this map say", and a dock says only that it is a dock.
    /// </para>
    /// </summary>
    public static StoryReach WithTheBoat(WorldData world, string startingMapId)
    {
        StoryReach walked = From(world, startingMapId);

        List<string> docks = [.. world.Maps.Where(m => m.Ferry is not null).Select(m => m.Id)];

        // No dock reached means the boat is no help, and saying so is the answer rather than
        // an omission.
        if (!docks.Any(walked.CanGetTo)) return walked;

        var got = walked.Got.ToDictionary(r => r.MapId);

        string from = docks.First(walked.CanGetTo);

        int steps = got[from].Steps + 1;

        foreach (string dock in docks.Where(d => !got.ContainsKey(d)))
            got[dock] = new Reached(dock, HowReached.ByBoat, from, steps);

        // And then carry on walking from wherever the boat put us, because a dock is a map
        // like any other and has doors of its own.
        var reachable = new StoryReach(
            [.. got.Values.OrderBy(r => r.Steps).ThenBy(r => r.MapId)],
            [.. walked.DidNot.Where(id => !got.ContainsKey(id))],
            walked.Nowhere);

        return Onwards(world, reachable);
    }

    /// <summary>Keeps walking from everywhere already reached until nothing new turns up.</summary>
    private static StoryReach Onwards(WorldData world, StoryReach so_far)
    {
        var known = world.Maps.ToDictionary(m => m.Id);
        var got = so_far.Got.ToDictionary(r => r.MapId);
        var nowhere = so_far.Nowhere.ToList();

        var frontier = new Queue<string>(got.Keys);

        while (frontier.Count > 0)
        {
            string here = frontier.Dequeue();

            if (!known.TryGetValue(here, out MapData? map)) continue;

            int steps = got[here].Steps + 1;

            foreach ((string names, HowReached how) in WaysOut(map))
            {
                if (!known.ContainsKey(names))
                {
                    if (!nowhere.Any(n => n.OnMap == here && n.Names == names && n.Kind == how))
                        nowhere.Add(new DoorToNowhere(here, names, how));

                    continue;
                }

                if (got.ContainsKey(names)) continue;

                got[names] = new Reached(names, how, here, steps);

                frontier.Enqueue(names);
            }
        }

        return new StoryReach(
            [.. got.Values.OrderBy(r => r.Steps).ThenBy(r => r.MapId)],
            [.. world.Maps.Select(m => m.Id).Where(id => !got.ContainsKey(id)).Order()],
            nowhere);
    }
}
