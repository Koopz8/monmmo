namespace PokeMmo.Core.World;

/// <summary>Why a run did not go through one door.</summary>
public enum WhyNotTaken
{
    /// <summary>The run stood on the square. Whatever stopped it, it was not the walking.</summary>
    StoodOnIt,

    /// <summary>
    /// It stood beside the square and not on it. <b>On this cartridge this never happens</b> —
    /// the walker steps onto a door's own square, so 0 of 1156 doors to reached maps are this.
    /// </summary>
    StoodBeside,

    /// <summary>The run never got to the square, and something could have.</summary>
    NeverGotNear,

    /// <summary>
    /// No square beside it is walkable at all, so nothing could ever reach it — a fact about the
    /// file rather than about the run (242's <c>10.6 (4,1)</c> is the sign version of this).
    /// </summary>
    WalledIn,
}

/// <summary>One door out of reached ground into a map the run never reaches.</summary>
public sealed record ADoorNotTaken(
    string From,
    string To,
    GridPosition Square,
    bool IsDoor,
    int WalkableNeighbours,
    WhyNotTaken Why);

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
/// to a REACHED map, the run stood on <b>1132 of 1156</b> — 97.9% — so the instrument can say yes
/// and does (68, 78).
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

        CollisionGrid grid = map.ToGrid();

        int neighbours = new[]
            {
                new GridPosition(warp.X + 1, warp.Y), new GridPosition(warp.X - 1, warp.Y),
                new GridPosition(warp.X, warp.Y + 1), new GridPosition(warp.X, warp.Y - 1),
            }
            .Count(grid.IsWalkable);

        bool here = on.Contains((map.Id, warp.X, warp.Y));

        bool beside =
            on.Contains((map.Id, warp.X + 1, warp.Y)) || on.Contains((map.Id, warp.X - 1, warp.Y)) ||
            on.Contains((map.Id, warp.X, warp.Y + 1)) || on.Contains((map.Id, warp.X, warp.Y - 1));

        // WALLED IN IS ABOUT THE FILE and is asked first, so a door nothing could ever reach is
        // not filed as a run that did not get there (242, 281).
        WhyNotTaken why = here
            ? WhyNotTaken.StoodOnIt
            : neighbours == 0
                ? WhyNotTaken.WalledIn
                : beside
                    ? WhyNotTaken.StoodBeside
                    : WhyNotTaken.NeverGotNear;

        return new ADoorNotTaken(
            map.Id, warp.TargetMapId, square, map.IsDoor(square), neighbours, why);
    }
}
