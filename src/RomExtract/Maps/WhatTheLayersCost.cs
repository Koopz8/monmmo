using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Maps;

/// <summary>What one map's walkable squares look like flat and layered.</summary>
/// <param name="MapId">The map.</param>
/// <param name="Walkable">How many of its squares can be stood on at all.</param>
/// <param name="From">How many squares the fill started from.</param>
/// <param name="Flat">How many squares a fill reaches ignoring elevation.</param>
/// <param name="Layered">How many it reaches when a step has to stay on a layer.</param>
/// <param name="Elevations">How many distinct elevations its walkable squares carry.</param>
public sealed record LayerReach(
    string MapId, int Walkable, int From, int Flat, int Layered, int Elevations)
{
    /// <summary>Squares the flat fill reaches and the layered one does not.</summary>
    public int Lost => Flat - Layered;
}

/// <summary>
/// What this project's flat walk would lose if a step had to stay on its own layer.
/// </summary>
/// <remarks>
/// <para>
/// 260 read an elevation out of all four event records and found that <b>423 of 425 maps carry
/// more than one elevation among their own squares</b> while this project's collision reading is
/// two-dimensional. That is a worry, not a number. This is the number.
/// </para>
/// <para>
/// <b>It changes nothing.</b> Whether the walk should enforce layers is a decision and it is
/// deliberately not made here — 249 did the same for the buried items it found the run standing
/// on, and the measurement is what a decision needs.
/// </para>
/// <para>
/// <b>The rule is MODELLED and small.</b> Two squares are on the same layer when their elevations
/// are equal, or when either is nought — the value the games use where a walker may change layer.
/// Nothing in this repository has read the engine's own rule, and the honest thing is to name the
/// assumption and print what it is worth rather than to bury it.
/// </para>
/// </remarks>
public static class WhatTheLayersCost
{
    /// <summary>The elevation a walker may step onto from anywhere.</summary>
    /// <remarks>
    /// MODELLED. Nought is the commonest non-three value in every one of the four event lists and
    /// it is what 260's naming test had to treat as a wildcard to get three disagreements out of
    /// 3863 records — which is evidence, and it is not the engine's own rule.
    /// </remarks>
    public const int Transition = 0;

    /// <summary>Whether a step between two squares stays on one layer.</summary>
    public static bool Connects(int from, int to) =>
        from == to || from == Transition || to == Transition;

    /// <summary>Every walkable square a fill reaches from <paramref name="starts"/>.</summary>
    /// <param name="connects">
    /// Whether a step between two elevations is allowed. The FLAT fill passes a rule that always
    /// says yes, so the two answers come out of one function with one predicate swapped — a
    /// before-and-after built from two different fills would be a measurement with no instrument
    /// (241).
    /// </param>
    public static HashSet<GridPosition> Fill(
        CollisionGrid grid,
        byte[] elevations,
        IEnumerable<GridPosition> starts,
        Func<int, int, bool> connects)
    {
        var reached = new HashSet<GridPosition>();
        var todo = new Queue<GridPosition>();

        int At(GridPosition square) => elevations[(square.Y * grid.Width) + square.X];

        foreach (GridPosition start in starts)
        {
            if (!grid.IsWalkable(start)) continue;
            if (reached.Add(start)) todo.Enqueue(start);
        }

        while (todo.Count > 0)
        {
            GridPosition at = todo.Dequeue();

            foreach (Direction direction in Enum.GetValues<Direction>())
            {
                GridPosition next = at.Step(direction);

                if (!grid.IsWalkable(next)) continue;
                if (!connects(At(at), At(next))) continue;
                if (!reached.Add(next)) continue;

                todo.Enqueue(next);
            }
        }

        return reached;
    }

    /// <summary>The elevation of every square of a map, in the same order the grid uses.</summary>
    public static byte[] Elevations(ushort[] blocks) =>
        [.. blocks.Select(b => (byte)new MapBlock(b).Elevation)];
}
