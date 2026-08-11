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
    bool IsTrainer,
    int RangeX = 0,
    int RangeY = 0,
    uint ScriptAddress = 0,
    int TrainerId = 0,
    int SightRange = 0)
{
    /// <summary>True when talking to this one would do something.</summary>
    public bool HasScript => ScriptAddress != 0;
    public GridPosition Square => new(X, Y);

    /// <summary>True when this one has a party the server can actually field.</summary>
    public bool CanBeFought => IsTrainer && TrainerId != 0;

    /// <summary>
    /// Whether a player standing here is in this trainer's line of sight.
    /// <para>
    /// A straight line in the direction they are facing, out to their sight range, and
    /// nothing to either side. Written as "same column, in front, within range" rather
    /// than as a distance, because a distance would have them notice somebody standing
    /// diagonally — which they famously do not.
    /// </para>
    /// <para>
    /// Whether anything is <em>between</em> the two is not decided here. That needs the
    /// map, and this record has never seen one.
    /// </para>
    /// </summary>
    public bool CanSee(GridPosition square)
    {
        if (SightRange <= 0) return false;

        (int alongX, int alongY) = Facing switch
        {
            Direction.Up => (0, -1),
            Direction.Down => (0, 1),
            Direction.Left => (-1, 0),
            _ => (1, 0),
        };

        for (int step = 1; step <= SightRange; step++)
        {
            if (square == new GridPosition(X + alongX * step, Y + alongY * step)) return true;
        }

        return false;
    }

    /// <summary>The squares between this one and a player they can see, nearest first.</summary>
    public IEnumerable<GridPosition> ApproachTo(GridPosition square)
    {
        (int alongX, int alongY) = Facing switch
        {
            Direction.Up => (0, -1),
            Direction.Down => (0, 1),
            Direction.Left => (-1, 0),
            _ => (1, 0),
        };

        for (int step = 1; step <= SightRange; step++)
        {
            var next = new GridPosition(X + alongX * step, Y + alongY * step);

            // Stops one short: walking onto the player rather than up to them would put
            // two characters on one square.
            if (next == square) yield break;

            yield return next;
        }
    }

    /// <summary>True when this one paces about rather than standing still.</summary>
    public bool Wanders => MovementType is 2 or 3 or 4 or 5 or 6;

    /// <summary>True when this one turns on the spot without going anywhere.</summary>
    public bool LooksAround => MovementType == 1;

    /// <summary>
    /// Whether a square is within this one's beat.
    /// <para>
    /// The range is a box around where they started, and it is per-axis: a shopkeeper
    /// pacing left and right has a range in x and none in y. Ignoring it would let
    /// everybody wander off across the map, which is both wrong and a good way to
    /// block a doorway nobody expected to be blocked.
    /// </para>
    /// </summary>
    public bool IsWithinRange(GridPosition square) =>
        Math.Abs(square.X - X) <= RangeX && Math.Abs(square.Y - Y) <= RangeY;

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
