namespace PokeMmo.Core.World;

/// <summary>
/// Working out who a player is talking to.
/// <para>
/// Both sides need this and they must agree. The client decides whether pressing the
/// button opens a text box; the server decides whether that person is close enough to
/// be worth holding still. If those two answered differently you would get a
/// conversation with somebody who walks away mid-sentence, which is worse than no
/// conversation at all.
/// </para>
/// </summary>
public static class Interaction
{
    /// <summary>Which way somebody turns to face a player looking in a direction.</summary>
    public static Direction Opposite(Direction direction) => direction switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        _ => Direction.Left,
    };

    /// <summary>
    /// Whoever is standing on the square a player is facing, or nothing.
    /// <para>
    /// <paramref name="livePositions"/> is where the running world says people are,
    /// keyed by local id. It is consulted first and the cartridge's placement is the
    /// fallback: once somebody wanders, where they started is a fact about an image on
    /// disk rather than about the world, and reading from it would have players talking
    /// to empty squares.
    /// </para>
    /// </summary>
    public static MapObject? InFrontOf(
        GridPosition standing,
        Direction facing,
        IReadOnlyList<MapObject> objects,
        IReadOnlyDictionary<int, GridPosition>? livePositions = null)
    {
        GridPosition target = standing.Step(facing);

        foreach (MapObject person in objects)
        {
            GridPosition where =
                livePositions is not null && livePositions.TryGetValue(person.LocalId, out GridPosition live)
                    ? live
                    : person.Square;

            if (where == target) return person;
        }

        return null;
    }
}
