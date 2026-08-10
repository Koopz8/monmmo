namespace PokeMmo.Core.World;

/// <summary>Which edge of a map a neighbour is joined to.</summary>
public enum ConnectionSide
{
    Down,
    Up,
    Left,
    Right,
}

/// <summary>
/// A neighbouring map joined along an edge.
/// <para>
/// <see cref="Offset"/> slides the neighbour along that edge, in squares, and is
/// signed — a route wider than the town below it hangs off in one direction or the
/// other. It is what makes walking off the bottom of Pallet Town arrive at the right
/// column of Route 1 rather than at column zero.
/// </para>
/// </summary>
public sealed record MapConnection(ConnectionSide Side, int Offset, string MapId);

/// <summary>
/// A square that moves a player somewhere else: a door, a stairway, a cave mouth.
/// <para>
/// The destination is named as a warp on the target map rather than as coordinates.
/// That is the cartridge's own arrangement and it is the right one — a door leads to
/// "the other side of that door", so the two ends stay consistent even when one map
/// is edited.
/// </para>
/// </summary>
public sealed record Warp(int X, int Y, int TargetWarpId, string TargetMapId)
{
    /// <summary>
    /// A destination warp id the games use to mean "no matching warp" — the player
    /// arrives at the target warp's own square instead.
    /// </summary>
    public const int Unspecified = 0xFF;

    public GridPosition Square => new(X, Y);
}

/// <summary>
/// Somebody standing on a map: a person, a sign-poster, a rooted tree.
/// <para>
/// Called an object event on the cartridge, which covers anything that occupies a
/// square and is not scenery. Only what is needed to place one and draw it is kept —
/// the script that decides what it says is a separate problem, and a large one.
/// </para>
/// </summary>
public sealed record MapObject(
    int LocalId,
    int GraphicsId,
    int X,
    int Y,
    Direction Facing,
    int MovementType,
    bool IsTrainer)
{
    public GridPosition Square => new(X, Y);

    /// <summary>
    /// Which way one of these starts out looking.
    /// <para>
    /// The movement type says both how it moves and where it faces to begin with.
    /// Wandering in a direction and standing still facing it are different numbers
    /// with the same starting look, which is why both map to the same facing here.
    /// </para>
    /// </summary>
    public static Direction FacingFor(int movementType) => movementType switch
    {
        3 or 7 => Direction.Up,
        4 or 8 => Direction.Down,
        5 or 9 => Direction.Left,
        6 or 10 => Direction.Right,
        _ => Direction.Down,
    };
}
