namespace PokeMmo.Core.World;

/// <summary>
/// Another player, drawn from position updates rather than from input.
/// <para>
/// Updates arrive one square at a time and a step takes longer than the gap between
/// frames, so drawing them at their reported square would make everyone else jump
/// about. This walks the character to each new square instead, which is what makes
/// remote movement look like walking rather than teleporting.
/// </para>
/// </summary>
public sealed class RemoteCharacter
{
    /// <summary>
    /// What they are wearing, as the server last said.
    /// <para>
    /// Held rather than asked for: an appearance arrives with the person and changes only
    /// when they change it, so a client that re-derived it every frame would be doing work
    /// to reach the same answer.
    /// </para>
    /// </summary>
    public Cosmetics.Appearance Looks { get; set; } = Cosmetics.Appearance.Bare;

    private float _fromX;
    private float _fromY;
    private float _progress = 1f;

    public RemoteCharacter(int id, string name, GridPosition square, Direction facing)
    {
        Id = id;
        Name = name;
        Square = square;
        Facing = facing;
        SnapTo(square, facing);
    }

    public int Id { get; }

    public string Name { get; }

    public GridPosition Square { get; private set; }

    public Direction Facing { get; private set; }

    public bool IsMoving => _progress < 1f;

    /// <summary>Places the character immediately, with no walk.</summary>
    public void SnapTo(GridPosition square, Direction facing)
    {
        Square = square;
        Facing = facing;
        _fromX = square.X * WalkingCharacter.SquarePixels;
        _fromY = square.Y * WalkingCharacter.SquarePixels;
        _progress = 1f;
    }

    /// <summary>
    /// Walks to a new square, starting from wherever the character is drawn right now
    /// rather than from its last reported square — otherwise an update arriving
    /// mid-step would visibly snap backwards before moving on.
    /// </summary>
    public void MoveTo(GridPosition square, Direction facing)
    {
        Facing = facing;

        if (square == Square) return;

        (_fromX, _fromY) = PixelPosition;
        Square = square;
        _progress = 0f;
    }

    /// <summary>
    /// Goes over a ledge: the same two-square movement the player's own character makes,
    /// drawn with the same arc so that watching somebody hop looks like hopping.
    /// </summary>
    public void HopTo(GridPosition square, Direction facing)
    {
        MoveTo(square, facing);

        _hopping = square != Square || _progress < 1f;
    }

    /// <summary>True while this character is in the air over a ledge.</summary>
    public bool IsHopping => _hopping && IsMoving;

    /// <summary>How far off the ground to draw them, in pixels.</summary>
    public float Arc => IsHopping ? MathF.Sin(MathF.PI * _progress) * 10f : 0f;

    private bool _hopping;

    public void Update(float deltaSeconds)
    {
        if (!IsMoving)
        {
            _hopping = false;
            return;
        }

        // Two squares' worth of time for a hop, which is what the character taking it
        // spends. Anything faster and everyone else sees a jump that finishes before the
        // jumper has landed.
        _progress += deltaSeconds / (WalkingCharacter.StepSeconds * (_hopping ? 2f : 1f));

        if (_progress > 1f) _progress = 1f;
    }

    public (float X, float Y) PixelPosition
    {
        get
        {
            float toX = Square.X * WalkingCharacter.SquarePixels;
            float toY = Square.Y * WalkingCharacter.SquarePixels;

            return (
                WalkingCharacter.Between(_fromX, toX, _progress),
                WalkingCharacter.Between(_fromY, toY, _progress));
        }
    }
}
