using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>What holds one door's own square away from the ground a walk stood on.</summary>
public enum WhatFences
{
    /// <summary>Nothing — the walk stood on this very square.</summary>
    Nothing,

    /// <summary>
    /// <b>SOMEBODY IS IN THE WAY.</b> Steps reach the door over ordinary ground, and every path
    /// that gets there goes through a square the walk itself refused because a person was standing
    /// on it — a tree nobody can shift yet, or somebody rooted to the spot.
    /// <para>
    /// The fourth fence, and 288 has no name for it because 288 asked about GROUND. The blocked
    /// squares are the walk's OWN list and are not re-derived here: a second copy of a rule about
    /// who is a wall would be a second walker to keep honest (223).
    /// </para>
    /// </summary>
    SomebodyInTheWay,

    /// <summary>
    /// Ordinary steps reach it from where the walk stood. <b>This must be nought.</b> Steps are
    /// symmetric over walkable ground, so a door the walk could have walked to and did not is a
    /// walk that stopped early or an instrument disagreeing with it (288, 240).
    /// </summary>
    SameGround,

    /// <summary>A ledge hop reaches it and no step does — ground you get into and not out of.</summary>
    BehindALedge,

    /// <summary>Neither steps nor hops reach it. Nothing but another door opens this one.</summary>
    Sealed,
}

/// <summary>One door, and what is holding it shut against the walk.</summary>
/// <param name="Pocket">Squares joined to the door's own square, hops included.</param>
/// <param name="WarpsInThePocket">
/// This map's own warps that stand inside that pocket — <b>every way in there is</b>, because a
/// pocket steps and hops cannot leave is a pocket nothing but a door enters.
/// </param>
/// <param name="LandedInFrom">
/// The maps whose warps put somebody down inside the pocket. Empty means the pocket has no way in
/// from anywhere in the world, and the door can only ever be used from its far side.
/// </param>
public sealed record ADoorFenced(
    string MapId,
    GridPosition Square,
    string To,
    WhatFences Fenced,
    int Pocket,
    IReadOnlyList<int> WarpsInThePocket,
    IReadOnlyList<string> LandedInFrom)
{
    /// <summary>
    /// Which of the maps that land somebody in this pocket the run actually reaches. <b>This is
    /// the one that recurses</b>: a pocket landed in only from maps that are themselves unreached
    /// is 303's closure again and not a reason of its own.
    /// </summary>
    public IReadOnlyList<string> LandedInFromReached { get; init; } = [];

    /// <summary>
    /// The people any ONE of whom stepping aside would let the walk through — the walk's own
    /// refused squares, tried one at a time. Empty when they fence the door together, or when
    /// nobody is in the way at all.
    /// </summary>
    public IReadOnlyList<int> OpenedBy { get; init; } = [];

    /// <summary>True when nothing in the world puts anybody inside this door's pocket.</summary>
    public bool NoWayIn => Fenced == WhatFences.Sealed && LandedInFrom.Count == 0;

    /// <summary>True when the only ways into the pocket start somewhere the run never gets.</summary>
    public bool OnlyFromUnreached =>
        Fenced == WhatFences.Sealed && LandedInFrom.Count > 0 && LandedInFromReached.Count == 0;
}

/// <summary>
/// What fences the doors the run never got near (305).
/// <para>
/// 304 asked what the run did at each door into an unreached map and got one answer 43 times out
/// of 43: <b>it never got near it</b>. That is where 304 stopped — "they are inside 287's pockets"
/// names the ground without naming the fence. 288 had already sorted fences into three kinds and
/// had never been asked about a door.
/// </para>
/// <para>
/// This asks it. For each door: which of the three kinds holds its square, how big the pocket
/// around it is, which of the map's own warps stand in that pocket — those are the only ways in,
/// since steps and hops cannot leave it — and which maps in the world actually land somebody
/// there.
/// </para>
/// <para>
/// <b>The row whose answer is known</b> is the same question asked of the doors the walk
/// demonstrably went through: those must come back <see cref="WhatFences.Nothing"/>, and an
/// instrument that cannot say "nothing fences this" cannot be believed when it says otherwise
/// (68, 78).
/// </para>
/// </summary>
public static class WhatFencesTheDoor
{
    /// <summary>What fences each of the given doors, on the map each one stands on.</summary>
    /// <param name="maps">Every map in the world — the landing side is read from all of them.</param>
    /// <param name="doors">The doors to ask about, as map id and square.</param>
    /// <param name="stood">Every square the walk stood on, map by map.</param>
    /// <param name="surfing">Whether the walk could cross water. The grid must be the walk's.</param>
    /// <param name="blocked">
    /// The squares the walk refused because somebody was standing on them, with who that was —
    /// the walk's own two lists, not a second copy of the rule about who counts as a wall (223).
    /// </param>
    /// <param name="reached">
    /// The maps the run reaches, for <see cref="ADoorFenced.LandedInFromReached"/>. Pass ONE run's
    /// maps beside ONE run's stood squares — a union of six runs is not a run (283).
    /// </param>
    public static IReadOnlyList<ADoorFenced> For(
        IReadOnlyCollection<MapData> maps,
        IReadOnlyCollection<(string MapId, GridPosition Square)> doors,
        IReadOnlyCollection<(string MapId, GridPosition Square)> stood,
        bool surfing,
        IReadOnlyCollection<(string MapId, GridPosition Square, int LocalId)>? blocked = null,
        IReadOnlyCollection<string>? reached = null)
    {
        Dictionary<string, MapData> byId = maps.ToDictionary(m => m.Id);

        Dictionary<string, List<GridPosition>> stoodOn = stood
            .GroupBy(s => s.MapId)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Square).ToList());

        Dictionary<string, List<(GridPosition Square, int LocalId)>> refused = (blocked ?? [])
            .GroupBy(b => b.MapId)
            .ToDictionary(g => g.Key, g => g.Select(b => (b.Square, b.LocalId)).Distinct().ToList());

        var found = new List<ADoorFenced>();

        foreach ((string mapId, GridPosition square) in doors)
        {
            if (!byId.TryGetValue(mapId, out MapData? map)) continue;

            Warp? warp = map.Warps.FirstOrDefault(w => w.Square == square);

            if (warp is null) continue;

            found.Add(Read(
                maps,
                map,
                warp,
                stoodOn.GetValueOrDefault(mapId, []),
                surfing,
                refused.GetValueOrDefault(mapId, []),
                reached ?? []));
        }

        return found;
    }

    private static ADoorFenced Read(
        IReadOnlyCollection<MapData> maps,
        MapData map,
        Warp warp,
        IReadOnlyCollection<GridPosition> stood,
        bool surfing,
        IReadOnlyList<(GridPosition Square, int LocalId)> refused,
        IReadOnlyCollection<string> reached)
    {
        CollisionGrid grid = map.ToGrid(surfing);

        HashSet<GridPosition> here = [.. stood.Where(grid.IsWalkable)];

        // The same ground twice: once as the file has it, and once with the squares the walk
        // itself refused taken out. A door in the first and not the second is one somebody is
        // standing in front of, and that is a fence 288 has no name for.
        bool byStep = HowAPocketIsShut.Reaching(here, grid, map, hops: false).Contains(warp.Square);

        bool pastThePeople = Reaches(here, map, grid, refused, warp.Square, without: -1);

        // AND WHICH OF THEM IS THE ONE. Each refused square is opened on its own and the flood
        // asked again: a person whose stepping aside alone lets the walk through is the fence,
        // and a door where none of them does is fenced by all of them together.
        List<int> opened = byStep && !pastThePeople
            ?
            [
                .. refused
                    .Where(r => Reaches(here, map, grid, refused, warp.Square, without: r.LocalId))
                    .Select(r => r.LocalId)
                    .Distinct()
                    .Order(),
            ]
            : [];

        WhatFences fenced = here.Contains(warp.Square)
            ? WhatFences.Nothing
            : byStep && !pastThePeople
                ? WhatFences.SomebodyInTheWay
                : byStep
                    ? WhatFences.SameGround
                    : HowAPocketIsShut.Reaching(here, grid, map, hops: true).Contains(warp.Square)
                        ? WhatFences.BehindALedge
                        : WhatFences.Sealed;

        // The pocket is flooded FROM THE DOOR, not from the walk. Asking it the other way round
        // would answer "how much did the walk miss" — a fact about the run — where this asks what
        // the door's own square is joined to, which is a fact about the file (211).
        HashSet<GridPosition> pocket =
            HowAPocketIsShut.Reaching([warp.Square], grid, map, hops: true);

        List<int> inside =
        [
            .. map.Warps
                .Select((w, at) => (Warp: w, At: at))
                .Where(w => pocket.Contains(w.Warp.Square))
                .Select(w => w.At),
        ];

        HashSet<int> ways = [.. inside];

        List<string> landedFrom =
        [
            .. maps
                .Where(m => m.Warps.Any(w =>
                    w.TargetMapId == map.Id && !w.IsDynamic && LandsInside(w, ways, map)))
                .Select(m => m.Id)
                .Order(StringComparer.Ordinal),
        ];

        return new ADoorFenced(
            map.Id, warp.Square, warp.TargetMapId, fenced, pocket.Count, inside, landedFrom)
        {
            LandedInFromReached = [.. landedFrom.Where(reached.Contains)],
            OpenedBy = opened,
        };
    }

    /// <summary>Whether steps reach the door with the refused squares shut, bar one person.</summary>
    private static bool Reaches(
        IReadOnlyCollection<GridPosition> here,
        MapData map,
        CollisionGrid grid,
        IReadOnlyList<(GridPosition Square, int LocalId)> refused,
        GridPosition door,
        int without) =>
        HowAPocketIsShut
            .Reaching(
                here,
                grid.With(refused.Where(r => r.LocalId != without).Select(r => r.Square)),
                map,
                hops: false)
            .Contains(door);

    /// <summary>
    /// Whether a warp pointed at this map puts somebody down inside the pocket.
    /// </summary>
    /// <remarks>
    /// <b>An unspecified destination is not "warp nought".</b> The games use 0xFF to mean "no
    /// matching warp" and put the arrival on the target warp's own square, so a reader treating it
    /// as an index would land everybody on whichever door happens to be written first in the file.
    /// It is out of range here, which is the honest answer: a door that names no landing.
    /// </remarks>
    private static bool LandsInside(Warp arriving, IReadOnlySet<int> ways, MapData map) =>
        arriving.TargetWarpId >= 0
        && arriving.TargetWarpId < map.Warps.Count
        && ways.Contains(arriving.TargetWarpId);
}
