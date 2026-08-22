using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>One border crossing, and where stepping straight back lands (286).</summary>
/// <param name="MapId">The map stepped off.</param>
/// <param name="Side">Which edge.</param>
/// <param name="From">The square stepped off from.</param>
/// <param name="Other">The map arrived on.</param>
/// <param name="To">The square arrived at.</param>
/// <param name="BackTo">The map a step straight back lands on, or nought for no join that way.</param>
/// <param name="BackAt">The square it lands on.</param>
public sealed record ACrossing(
    string MapId,
    ConnectionSide Side,
    GridPosition From,
    string Other,
    GridPosition To,
    string? BackTo,
    GridPosition? BackAt)
{
    /// <summary>True when stepping back lands on the very square that was left.</summary>
    public bool RoundTrips => BackTo == MapId && BackAt == From;

    public override string ToString() =>
        $"{MapId} {From} -{Side}-> {Other} {To}"
        + (BackTo is null ? "  and nothing comes back" : $"  back to {BackTo} {BackAt}");
}

/// <summary>
/// Every border crossing in the world, asked at the SQUARE rather than at the map.
/// <para>
/// <b>265 asked the borders whether the far map declares one back</b> — 116 joins, 114 declared,
/// 2 not — and that is a question about two map records. A walker crosses at a SQUARE, and
/// "the far map declares a join back to me" does not say that stepping back lands where you
/// started: the offsets can disagree, the return side can carry a different neighbour at your
/// row, or the far map can name a THIRD map back.
/// </para>
/// <para>
/// It is the same mirror 265 held up to the doors, one list over: <em>does this door name THIS
/// door back</em> scored 920 where <em>does it come back to this map at all</em> scored 237
/// against a control of 233, which is to say nothing. The tight half is the one that moves.
/// </para>
/// </summary>
public static class WhereABorderComesBackTo
{
    /// <summary>Every square-level crossing the world's borders allow.</summary>
    /// <remarks>
    /// Arithmetic only — no collision, no walk. Whether the arrival can be stood on is a separate
    /// question and it is carried beside this rather than folded into it (211): a join that is
    /// sound and lands in the sea is a different finding from one that lands on another island.
    /// </remarks>
    public static IReadOnlyList<ACrossing> Every(WorldData world)
    {
        Dictionary<string, MapData> maps = world.Maps.ToDictionary(m => m.Id);

        MapData? Find(string id) => maps.GetValueOrDefault(id);

        var crossings = new List<ACrossing>();

        foreach (MapData map in world.Maps)
        {
            foreach (ConnectionSide side in map.Connections.Select(c => c.Side).Distinct())
            {
                foreach (GridPosition from in Along(map, side))
                {
                    if (map.ConnectionOn(side, from, Find) is not { } join) continue;
                    if (Find(join.MapId) is not { } other) continue;

                    GridPosition to = GameWorld.AcrossEdge(from, side, map, other, join.Offset);

                    ConnectionSide back = TheDoorOnTheOtherSide.Opposite(side);

                    (string? backTo, GridPosition? backAt) =
                        other.ConnectionOn(back, to, Find) is { } returning
                        && Find(returning.MapId) is { } home
                            ? (returning.MapId,
                                (GridPosition?)GameWorld.AcrossEdge(to, back, other, home, returning.Offset))
                            : (null, null);

                    crossings.Add(new ACrossing(map.Id, side, from, join.MapId, to, backTo, backAt));
                }
            }
        }

        return crossings;
    }

    /// <summary>The squares along one edge of a map, in order.</summary>
    private static IEnumerable<GridPosition> Along(MapData map, ConnectionSide side) =>
        side switch
        {
            ConnectionSide.Up => Enumerable.Range(0, map.Width).Select(x => new GridPosition(x, 0)),
            ConnectionSide.Down =>
                Enumerable.Range(0, map.Width).Select(x => new GridPosition(x, map.Height - 1)),
            ConnectionSide.Left =>
                Enumerable.Range(0, map.Height).Select(y => new GridPosition(0, y)),
            _ => Enumerable.Range(0, map.Height).Select(y => new GridPosition(map.Width - 1, y)),
        };
}
