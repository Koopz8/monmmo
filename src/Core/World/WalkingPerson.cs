namespace PokeMmo.Core.World;

/// <summary>
/// Somebody on a map, drawn walking rather than teleporting.
/// <para>
/// The server sends a square. Sliding between squares is the client's job, and it is
/// the same job it already does for other players — so this wraps the same class,
/// adding the graphics id and the step count an animation needs.
/// </para>
/// <para>
/// Engine-free and here rather than in the client because the one piece of judgement
/// in it is worth testing: a step and a turn on the spot arrive as the same message
/// and must not look the same.
/// </para>
/// </summary>
public sealed class WalkingPerson(int graphicsId, GridPosition square, Direction facing, bool heals = false)
{
    public int GraphicsId { get; } = graphicsId;

    /// <summary>
    /// Whether this one is a counter that puts a party back on its feet.
    /// <para>
    /// Told rather than read. It is the one thing about a person the cartridge does not
    /// say in the person's own record — see <c>ObjectView</c> for why — and it lives here
    /// because the living population is already what the client asks "who is in front of
    /// me", and the answer to "does she ask me anything" belongs beside it.
    /// </para>
    /// </summary>
    public bool Heals { get; } = heals;

    public RemoteCharacter Body { get; } = new(0, "", square, facing);

    /// <summary>Steps taken, so consecutive strides change feet.</summary>
    public int Stride { get; private set; }

    public GridPosition Square => Body.Square;

    public Direction Facing => Body.Facing;

    public bool IsWalking => Body.IsMoving;

    /// <summary>
    /// Applies an update from the server.
    /// <para>
    /// Walking a square takes a fraction of a second and changes feet; a shopkeeper
    /// glancing about should not move a pixel or change anything at all except which
    /// way they look.
    /// </para>
    /// </summary>
    public void GoTo(GridPosition square, Direction facing)
    {
        if (square == Body.Square)
        {
            Body.SnapTo(square, facing);
            return;
        }

        Stride++;
        Body.MoveTo(square, facing);
    }

    public void Update(float deltaSeconds) => Body.Update(deltaSeconds);

    public (float X, float Y) PixelPosition => Body.PixelPosition;
}
