namespace PokeMmo.Core.World;

/// <summary>The four directions a character can face and move.</summary>
public enum Direction
{
    Down,
    Up,
    Left,
    Right,
}

/// <summary>A position on the map grid, measured in whole squares.</summary>
public readonly record struct GridPosition(int X, int Y)
{
    /// <summary>The square one step in <paramref name="direction"/>.</summary>
    public GridPosition Step(Direction direction) => direction switch
    {
        Direction.Up => this with { Y = Y - 1 },
        Direction.Down => this with { Y = Y + 1 },
        Direction.Left => this with { X = X - 1 },
        Direction.Right => this with { X = X + 1 },
        _ => this,
    };

    public override string ToString() => $"({X}, {Y})";
}

/// <summary>
/// Which squares of a map can be walked on.
/// <para>
/// This lives in <c>Core</c> rather than in the client on purpose. The server has to
/// validate every move a client claims to make, and the only way for its answer to
/// agree with the client's prediction in every case is for both to run this same
/// code. A reimplementation on either side is a desync waiting to happen.
/// </para>
/// </summary>
public sealed class CollisionGrid
{
    private readonly byte[] _collision;

    /// <param name="collision">
    /// One value per square, row-major: zero is walkable, anything else is not.
    /// </param>
    public CollisionGrid(int width, int height, byte[] collision)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException($"A grid needs positive dimensions, got {width}x{height}.");

        if (collision.Length < width * height)
            throw new ArgumentException(
                $"Expected {width * height} collision values, got {collision.Length}.", nameof(collision));

        Width = width;
        Height = height;
        _collision = collision;
    }

    public int Width { get; }

    public int Height { get; }

    public bool Contains(GridPosition position) =>
        position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

    /// <summary>The raw collision value of a square; zero means walkable.</summary>
    public byte CollisionAt(GridPosition position) =>
        Contains(position) ? _collision[position.Y * Width + position.X] : (byte)1;

    /// <summary>True when a character may stand on this square.</summary>
    public bool IsWalkable(GridPosition position) => Contains(position) && CollisionAt(position) == 0;

    /// <summary>
    /// Applies one step. Returns false and leaves <paramref name="destination"/> at the
    /// starting square when the way is blocked or off the edge of the map.
    /// </summary>
    public bool TryStep(GridPosition from, Direction direction, out GridPosition destination)
    {
        GridPosition target = from.Step(direction);

        if (!IsWalkable(target))
        {
            destination = from;
            return false;
        }

        destination = target;
        return true;
    }

    /// <summary>
    /// The first walkable square, scanning row by row. Somewhere to put a character
    /// when nothing better is known.
    /// </summary>
    public GridPosition FirstWalkable()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var position = new GridPosition(x, y);
                if (IsWalkable(position)) return position;
            }
        }

        return new GridPosition(0, 0);
    }
}
