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

    public void Update(float deltaSeconds)
    {
        if (!IsMoving) return;

        _progress += deltaSeconds / WalkingCharacter.StepSeconds;
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
