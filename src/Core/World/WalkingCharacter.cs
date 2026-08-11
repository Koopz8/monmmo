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

    /// <summary>
    /// The direction the other side has to be told about after this update, or nothing.
    /// <para>
    /// A step and a turn on the spot are both news and only one of them was ever sent.
    /// A step goes out the moment it begins; a turn went nowhere, so the server's idea
    /// of which way a player looked was whichever way they last walked. Everything the
    /// server decides from facing was then decided from that, and talking is decided
    /// from nothing else.
    /// </para>
    /// <para>
    /// It is a property here rather than two conditions in the render loop because the
    /// render loop is the one part of this that no test can reach. Anything the two
    /// sides have to agree on belongs where agreeing can be checked.
    /// </para>
    /// </summary>
    public Direction? ToReport { get; private set; }

    /// <summary>
    /// How far through the current step, from zero to one.
    /// <para>
    /// Exposed so a renderer can pick a walking frame from it. The alternative is a
    /// renderer keeping its own timer, which would drift from the movement it is
    /// supposed to be drawing.
    /// </para>
    /// </summary>
    public float StepProgress => _stepProgress;

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
        ToReport = null;

        // Input is only read between steps: once a step is underway it runs to
        // completion, so a character can never stop between squares.
        if (!IsStepping && input is { } direction)
        {
            // Face the way we are trying to go even when blocked, which is what lets a
            // character turn on the spot.
            bool turned = direction != Facing;

            Facing = direction;

            if (_grid.TryStep(Square, direction, out GridPosition destination))
            {
                _stepFrom = Square;
                Square = destination;
                _stepProgress = 0f;
                IsStepping = true;
                ToReport = direction;
            }
            else if (turned)
            {
                // Only a change is worth reporting. Holding a direction against a wall
                // is one turn and then sixty frames a second of nothing new.
                ToReport = direction;
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
