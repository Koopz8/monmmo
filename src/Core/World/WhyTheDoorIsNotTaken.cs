namespace PokeMmo.Core.World;

/// <summary>Why a run did not go through one door.</summary>
public enum WhyNotTaken
{
    /// <summary>The run stood on the square. Whatever stopped it, it was not the walking.</summary>
    StoodOnIt,

    /// <summary>
    /// It stood beside the square and not on it. <b>On this cartridge this never happens</b> —
    /// the walker steps onto a door's own square, so 0 of 1182 doors to reached maps are this.
    /// </summary>
    StoodBeside,

    /// <summary>The run never got to the square, and something could have.</summary>
    NeverGotNear,

    /// <summary>
    /// No square beside it is walkable at all — <b>on foot or from the water</b> — so nothing could
    /// ever reach it. A fact about the file rather than about the run (242's <c>10.6 (4,1)</c> is
    /// the sign version of this).
    /// </summary>
    WalledIn,
}

/// <summary>One door out of reached ground into a map the run never reaches.</summary>
/// <param name="WalkableNeighbours">Squares beside it somebody on foot could stand on.</param>
/// <param name="NeighboursFromTheWater">
/// The same count with the sea open. Never smaller than <see cref="WalkableNeighbours"/>, because
/// surfing opens water and leaves the land exactly as it was.
/// </param>
public sealed record ADoorNotTaken(
    string From,
    string To,
    GridPosition Square,
    bool IsDoor,
    int WalkableNeighbours,
    int NeighboursFromTheWater,
    WhyNotTaken Why)
{
    /// <summary>True when the only way beside this door is by sea.</summary>
    public bool OnlyFromTheWater => WalkableNeighbours == 0 && NeighboursFromTheWater > 0;
}

/// <summary>
/// Why the doors into the unreached maps are not taken (304).
/// <para>
/// 303 sorted the thirty-seven maps no run reaches and found <b>eight roots</b> — maps that ground
/// the run stands on names, and that it still cannot get to. Seven of the eight are named by warps,
/// and the obvious guesses are a shut flag, a landing square the far map calls solid, or a script
/// the walk never runs.
/// </para>
/// <para>
/// <b>It is none of those. Every one of them is a door the run never gets to.</b> Not one root's
/// square was ever stood on or stood beside, and every one has one or two walkable neighbours, so
/// none is walled in. They are inside 287's pockets: walkable ground on a map the run DOES reach,
/// that it never stands on.
/// </para>
/// <para>
/// <b>The calibration row is what makes that a reading.</b> Asked of every warp from a reached map
/// to a REACHED map, the run stood on <b>1165 of 1182</b> — 98.6% — so the instrument can say yes
/// and does (68, 78).
/// </para>
/// <para>
/// <b>And walled-in is asked of the surfing grid.</b> Water is solid on foot and walkable on the
/// sea, so a door whose one open neighbour is water reads nought neighbours in the walking grid —
/// one door here is exactly that (<c>1.4 (33,15) -> 1.5</c>) and calling it unreachable would file
/// a square the run floats up to as one nothing could ever reach.
/// </para>
/// </summary>
public static class WhyTheDoorIsNotTaken
{
    /// <summary>Every door from reached ground into a map the run never reaches.</summary>
    public static IReadOnlyList<ADoorNotTaken> Into(
        IReadOnlyCollection<MapData> maps,
        IReadOnlyCollection<string> reached,
        IReadOnlyCollection<(string MapId, GridPosition Square)> stood,
        IReadOnlyCollection<string> roots)
    {
        HashSet<(string, int, int)> on = [.. stood.Select(s => (s.MapId, s.Square.X, s.Square.Y))];

        var found = new List<ADoorNotTaken>();

        foreach (MapData map in maps.Where(m => reached.Contains(m.Id)))
        {
            foreach (Warp warp in map.Warps.Where(w => !w.IsDynamic && roots.Contains(w.TargetMapId)))
                found.Add(Read(map, warp, on));
        }

        return found;
    }

    /// <summary>
    /// The row whose answer is known: every warp from a reached map to a REACHED map.
    /// </summary>
    /// <remarks>
    /// Without this the reading is "the run never got to any of them", which is what a broken
    /// instrument says too. With it, the same test on doors the run demonstrably went through
    /// answers 97.9%.
    /// </remarks>
    public static IReadOnlyList<ADoorNotTaken> TheKnownRow(
        IReadOnlyCollection<MapData> maps,
        IReadOnlyCollection<string> reached,
        IReadOnlyCollection<(string MapId, GridPosition Square)> stood)
    {
        HashSet<(string, int, int)> on = [.. stood.Select(s => (s.MapId, s.Square.X, s.Square.Y))];

        var found = new List<ADoorNotTaken>();

        foreach (MapData map in maps.Where(m => reached.Contains(m.Id)))
        {
            foreach (Warp warp in map.Warps.Where(
                         w => !w.IsDynamic && reached.Contains(w.TargetMapId)))
                found.Add(Read(map, warp, on));
        }

        return found;
    }

    private static ADoorNotTaken Read(MapData map, Warp warp, HashSet<(string, int, int)> on)
    {
        var square = new GridPosition(warp.X, warp.Y);

        GridPosition[] beside =
        [
            new(warp.X + 1, warp.Y), new(warp.X - 1, warp.Y),
            new(warp.X, warp.Y + 1), new(warp.X, warp.Y - 1),
        ];

        int onFoot = beside.Count(map.ToGrid().IsWalkable);

        // AND THE SEA IS A WAY TO A DOOR. Water is solid to somebody on foot and walkable to
        // somebody on it, so a door whose only open neighbour is water has nought neighbours in
        // one grid and one in the other — and the run reaches it, because --surf stands there.
        int fromTheWater = beside.Count(map.ToGrid(surfing: true).IsWalkable);

        bool here = on.Contains((map.Id, warp.X, warp.Y));
        bool stoodBeside = beside.Any(s => on.Contains((map.Id, s.X, s.Y)));

        // WALLED IN IS ABOUT THE FILE and is asked before NEVER GOT NEAR, so a door nothing could
        // ever reach is not filed as a run that did not get there (242, 281).
        //
        // Its order against STOOD BESIDE is a spelling and not a rule, and a break swapping the
        // two is green on purpose: the walker only stands where a grid calls it walkable and the
        // surfing grid is the union of both, so a neighbour that was stood on is one this count
        // can see. Nought neighbours means nothing was stood beside. The fixtures carry that.
        WhyNotTaken why = here
            ? WhyNotTaken.StoodOnIt
            : fromTheWater == 0
                ? WhyNotTaken.WalledIn
                : stoodBeside
                    ? WhyNotTaken.StoodBeside
                    : WhyNotTaken.NeverGotNear;

        return new ADoorNotTaken(
            map.Id, warp.TargetMapId, square, map.IsDoor(square), onFoot, fromTheWater, why);
    }
}
