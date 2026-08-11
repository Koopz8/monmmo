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
        IReadOnlyDictionary<int, GridPosition>? livePositions = null,
        Func<GridPosition, bool>? isSolid = null)
    {
        foreach (GridPosition target in Reachable(standing, facing, isSolid))
        {
            foreach (MapObject person in objects)
            {
                GridPosition where =
                    livePositions is not null && livePositions.TryGetValue(person.LocalId, out GridPosition live)
                        ? live
                        : person.Square;

                if (where == target) return person;
            }
        }

        return null;
    }

    /// <summary>
    /// The squares a player can talk to from where they stand: the one in front, and
    /// the one past it when what is in front is solid.
    /// <para>
    /// Counters. A shopkeeper stands behind one, so the square in front of the player is
    /// the counter itself and the clerk is two away — which meant every mart in the world
    /// had a clerk nobody could speak to, and the shop that was finally in the world file
    /// still could not be opened.
    /// </para>
    /// <para>
    /// Written as "solid" rather than as "a counter" on purpose. Which metatile behaviour
    /// means counter is a number that differs between these games, and this project has
    /// just spent three rounds paying for numbers it half-remembered. Somebody standing
    /// directly behind the thing you are facing is a counter, a desk or a window in every
    /// case that matters, and reaching across one costs nothing if it is a wall.
    /// </para>
    /// </summary>
    public static IEnumerable<GridPosition> Reachable(
        GridPosition standing, Direction facing, Func<GridPosition, bool>? isSolid = null)
    {
        GridPosition front = standing.Step(facing);

        yield return front;

        if (isSolid is not null && isSolid(front)) yield return front.Step(facing);
    }
}
