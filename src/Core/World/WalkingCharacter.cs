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

    /// <summary>
    /// How many sixtieths of a second one square takes.
    /// <para>
    /// Sixteen, because a square is sixteen pixels and the screen is drawn sixty times a
    /// second: one pixel a frame, exactly, for every frame of the step. That is the whole
    /// reason this is a frame count rather than a duration. The previous value was 0.16
    /// seconds, which is 9.6 frames, which is 1.67 pixels a frame — and a scene drawn at
    /// three times scale with point filtering does not have a third of a pixel to put
    /// anything in, so every tile in the world jumped between two and five screen pixels
    /// at random. That is what "janky" was.
    /// </para>
    /// </summary>
    public const int StepFrames = 16;

    /// <summary>Seconds to cross one square.</summary>
    public const float StepSeconds = StepFrames / 60f;

    /// <summary>
    /// The shortest gap the server will accept between two of a player's steps.
    /// <para>
    /// It lives here, in the one place both sides can see, because it is a rule with two
    /// halves and the client's half was missing. Walking normally, a client cannot break
    /// this: its own step animation is the limit, and a step cannot begin until the last
    /// one has finished.
    /// </para>
    /// <para>
    /// Going through a door breaks that, which is what the rubber-banding was. Arriving
    /// somewhere places the character outright, so the step they were half-way through
    /// ends the instant the server answers — and the client, with nothing else stopping
    /// it, asks for the next step immediately. The server refuses it as too fast and
    /// says where they really are, and the player is pulled back onto the doormat.
    /// </para>
    /// </summary>
    public static readonly double MinimumStepSeconds = StepSeconds * 0.75;

    /// <summary>
    /// Where something is drawn, part-way between two squares, on a whole pixel.
    /// <para>
    /// Rounded rather than interpolated freely, and shared so that everything walking —
    /// the player, the people, other players, a scene's cast — lands on the same grid.
    /// A character on a half pixel is a character whose outline shimmers, and one on a
    /// different half pixel from the map it is standing on is worse.
    /// </para>
    /// </summary>
    public static float Between(float from, float to, float progress) =>
        MathF.Round(from + (to - from) * progress);

    private CollisionGrid _grid = new(1, 1, [0]);
    private GridPosition _stepFrom;
    private float _stepProgress = 1f;
    private Func<GridPosition, Direction, GridPosition?>? _hop;

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

    /// <summary>
    /// Puts the character on a grid, and optionally tells it how to read a ledge.
    /// <para>
    /// Passed in rather than looked up, because this class knows about walkability and
    /// nothing else — it has never needed to know what a square <em>is</em>. The
    /// function is asked one question: stepping onto that square, going that way, where
    /// does it land? A map with no behaviour data answers nothing and the character
    /// walks exactly as it always has.
    /// </para>
    /// </summary>
    public void Place(
        CollisionGrid grid, GridPosition start, Func<GridPosition, Direction, GridPosition?>? hop = null)
    {
        _grid = grid;
        _stepFrom = start;
        _stepProgress = 1f;
        Square = start;
        IsStepping = false;
        IsHopping = false;
        _hop = hop;
    }

    /// <summary>True while the character is in the air over a ledge.</summary>
    public bool IsHopping { get; private set; }

    /// <summary>
    /// How far off the ground the character is drawn, in pixels.
    /// <para>
    /// A half sine over the whole hop, so it leaves and lands on the ground exactly. The
    /// height is the only part of this that is a choice rather than a measurement, and
    /// it is a small one: high enough to read as a jump at three times scale, low enough
    /// not to clear the tree the character is jumping past.
    /// </para>
    /// </summary>
    public float Arc =>
        IsHopping ? MathF.Sin(MathF.PI * _stepProgress) * 10f : 0f;

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
                IsHopping = false;
                ToReport = direction;
            }

            // A ledge, which is a wall to the step above — every one of them is solid in
            // the block data — so this is asked only once walking has been refused.
            else if (_hop?.Invoke(Square.Step(direction), direction) is { } landing)
            {
                _stepFrom = Square;
                Square = landing;
                _stepProgress = 0f;
                IsStepping = true;
                IsHopping = true;
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
        //
        // Two squares' worth of time for a hop, so that the character crosses the ground
        // at walking pace rather than twice it. This is also the rule the server keeps:
        // a hop costs two steps of the interval it holds people to.
        _stepProgress += deltaSeconds / (IsHopping ? StepSeconds * 2f : StepSeconds);

        if (_stepProgress >= 1f)
        {
            _stepProgress = 1f;
            IsStepping = false;
            IsHopping = false;
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
                Between(fromX, toX, _stepProgress),
                Between(fromY, toY, _stepProgress));
        }
    }
}
