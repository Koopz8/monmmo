namespace PokeMmo.Core.World;

/// <summary>
/// A character that walks the grid one square at a time.
/// <para>
/// Movement is grid-locked the way the original games are: once a step starts it runs
/// to completion, and input is only read between steps. Pressing into a wall turns the
/// character without moving them.
/// </para>
/// <para>
/// This is deliberately engine-free. Timing and interpolation are the part most
/// tempting to bury inside a renderer's update loop, and burying it there would make
/// it untestable and impossible for the server to agree with.
/// </para>
/// </summary>
public sealed class WalkingCharacter
{
    /// <summary>Map squares are 16 pixels across.</summary>
    public const int SquarePixels = 16;

    /// <summary>Seconds to cross one square, roughly the original walking speed.</summary>
    public const float StepSeconds = 0.16f;

    private CollisionGrid _grid = new(1, 1, [0]);
    private GridPosition _stepFrom;
    private float _stepProgress = 1f;

    /// <summary>The square the character occupies, or is walking into.</summary>
    public GridPosition Square { get; private set; }

    public Direction Facing { get; private set; } = Direction.Down;

    public bool IsStepping { get; private set; }

    /// <summary>How many steps have completed. Useful for driving a walk animation.</summary>
    public int StepsTaken { get; private set; }

    public void Place(CollisionGrid grid, GridPosition start)
    {
        _grid = grid;
        _stepFrom = start;
        _stepProgress = 1f;
        Square = start;
        IsStepping = false;
    }

    /// <summary>
    /// Advances by <paramref name="deltaSeconds"/>, starting a new step in
    /// <paramref name="input"/> if one is not already underway.
    /// </summary>
    public void Update(float deltaSeconds, Direction? input)
    {
        // Input is only read between steps: once a step is underway it runs to
        // completion, so a character can never stop between squares.
        if (!IsStepping && input is { } direction)
        {
            // Face the way we are trying to go even when blocked, which is what lets a
            // character turn on the spot.
            Facing = direction;

            if (_grid.TryStep(Square, direction, out GridPosition destination))
            {
                _stepFrom = Square;
                Square = destination;
                _stepProgress = 0f;
                IsStepping = true;
            }
        }

        if (!IsStepping) return;

        // The frame that starts a step also advances it, rather than losing that
        // frame's worth of motion to bookkeeping.
        _stepProgress += deltaSeconds / StepSeconds;

        if (_stepProgress >= 1f)
        {
            _stepProgress = 1f;
            IsStepping = false;
            StepsTaken++;
        }
    }

    /// <summary>Interpolated position in pixels, for drawing mid-step.</summary>
    public (float X, float Y) PixelPosition
    {
        get
        {
            float fromX = _stepFrom.X * SquarePixels;
            float fromY = _stepFrom.Y * SquarePixels;
            float toX = Square.X * SquarePixels;
            float toY = Square.Y * SquarePixels;

            return (
                fromX + (toX - fromX) * _stepProgress,
                fromY + (toY - fromY) * _stepProgress);
        }
    }
}
