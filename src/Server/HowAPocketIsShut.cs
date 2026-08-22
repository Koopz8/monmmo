using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>How the ground a walk never stood on is shut off from it (288).</summary>
/// <param name="MapId">The map.</param>
/// <param name="StoodOn">Squares the walk stood on.</param>
/// <param name="SameGround">
/// Walkable squares joined to a stood-on one by ordinary steps and never stood on. <b>This should
/// be nought.</b> A walk's steps are symmetric over walkable ground, so a square reachable by
/// them and not visited is a walk that stopped early or an instrument that disagrees with it.
/// </param>
/// <param name="BehindALedge">
/// Squares no step reaches but a LEDGE HOP does, taken either way. A ledge is one-way in play, so
/// these are ground somebody can get into and not out of — or out of and not into.
/// </param>
/// <param name="Sealed">
/// Squares neither steps nor hops reach from anywhere the walk stood. Nothing but a door or a
/// warp opens these.
/// </param>
public sealed record HowShut(
    string MapId, int StoodOn, int SameGround, int BehindALedge, int Sealed)
{
    public int Fenced => SameGround + BehindALedge + Sealed;

    public override string ToString() =>
        $"{MapId}  {Fenced} fenced: {SameGround} same ground, {BehindALedge} behind a ledge,"
        + $" {Sealed} sealed";
}

/// <summary>
/// The three ways ground can be fenced off inside a map the walk reached (288).
/// <para>
/// <b>287 counted 4019 squares of reached-but-never-stood-on ground and said in as many words
/// that a pocket is not proof of a fence</b> — a square behind a one-way ledge looks exactly like
/// one behind a wall. This separates them, and it separates a third case nobody had named: ground
/// joined to where the walk stood by ordinary walkable steps. That count must be nought, and a
/// count that must be nought is the best check an instrument can carry (240).
/// </para>
/// <para>
/// Hops are taken UNDIRECTED here on purpose. A ledge is one-way in play, so joining both ends of
/// it answers "could anybody be on either side of this without a door", which is the question a
/// fence asks. Which way it actually goes is 266's reading and is not re-derived.
/// </para>
/// </summary>
public static class HowAPocketIsShut
{
    /// <summary>How the ground the walk missed on one map is shut off from the ground it took.</summary>
    /// <param name="map">The map, which must be one the walk reached.</param>
    /// <param name="stood">The squares the walk stood on, on this map.</param>
    /// <param name="surfing">Whether the walk could cross water — the grid must be the walk's.</param>
    public static HowShut On(MapData map, IReadOnlyCollection<GridPosition> stood, bool surfing)
    {
        CollisionGrid grid = map.ToGrid(surfing);

        List<GridPosition> open =
        [
            .. from y in Enumerable.Range(0, map.Height)
               from x in Enumerable.Range(0, map.Width)
               let square = new GridPosition(x, y)
               where grid.IsWalkable(square)
               select square,
        ];

        HashSet<GridPosition> here = [.. stood.Where(grid.IsWalkable)];

        HashSet<GridPosition> byStep = Reaching(here, grid, map, hops: false);
        HashSet<GridPosition> byHop = Reaching(here, grid, map, hops: true);

        var sameGround = 0;
        var behindALedge = 0;
        var shut = 0;

        foreach (GridPosition square in open)
        {
            if (here.Contains(square)) continue;

            if (byStep.Contains(square)) sameGround++;
            else if (byHop.Contains(square)) behindALedge++;
            else shut++;
        }

        return new HowShut(map.Id, here.Count, sameGround, behindALedge, shut);
    }

    /// <summary>Everywhere reachable from <paramref name="from"/>, optionally across ledges.</summary>
    /// <remarks>
    /// Public because 305 asks this same flood of ONE DOOR'S OWN SQUARE rather than of the ground
    /// the walk stood on. A second copy of a walk is a second walk to keep honest (223).
    /// </remarks>
    public static HashSet<GridPosition> Reaching(
        IReadOnlyCollection<GridPosition> from, CollisionGrid grid, MapData map, bool hops)
    {
        HashSet<GridPosition> seen = [.. from];
        var queue = new Queue<GridPosition>(from);

        while (queue.Count > 0)
        {
            GridPosition at = queue.Dequeue();

            foreach (Direction way in Enum.GetValues<Direction>())
            {
                GridPosition next = at.Step(way);

                if (grid.IsWalkable(next) && seen.Add(next)) queue.Enqueue(next);

                if (!hops) continue;

                // A ledge, joined BOTH ways. The square the hop starts from is solid — that is
                // what a ledge is — so the step above can never cross one, and without this
                // every landing strip in the game reads as sealed.
                if (map.HopOnto(next, way) is { } landing && seen.Add(landing))
                    queue.Enqueue(landing);

                // And the other end: the square somebody would hop FROM to land where we are.
                //
                // The arithmetic is worth writing down because the first version had it wrong and
                // the wrong version reports NOUGHT, which is also the right answer on this
                // cartridge. A hop over the ledge `over` in direction `way` lands on
                // `over.Step(way)`, so for that landing to be here, `over` is one step BACK from
                // here — and the hopper stood one step back from `over`.
                GridPosition over = at.Step(Back(way));

                if (map.HopOnto(over, way) == at)
                {
                    GridPosition before = over.Step(Back(way));

                    if (grid.IsWalkable(before) && seen.Add(before)) queue.Enqueue(before);
                }
            }
        }

        return seen;
    }

    private static Direction Back(Direction way) => way switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        _ => Direction.Left,
    };
}
