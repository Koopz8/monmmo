using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>Why a run never read one of the cartridge's signs.</summary>
public enum UnreadBecause
{
    /// <summary>
    /// <b>Nothing could ever stand beside it, at any lever setting.</b> Not one of the five
    /// squares a sign is read from — its own and the four around it — is walkable, and that is
    /// asked with the water opened, so no run and no player reads this one.
    /// <para>
    /// A property of the FILE. It cannot move when a lever moves, and 211's rule is that a
    /// bucket which does move was named wrongly. This is the one to check.
    /// </para>
    /// </summary>
    NothingCouldStandBesideIt,

    /// <summary>The map itself was never reached — a REACH problem, closed by walking further.</summary>
    OnAMapItNeverReached,

    /// <summary>
    /// It reached the map, something could have stood beside this sign, and the walk never did.
    /// The interesting one: a part of a map the run does not get to.
    /// </summary>
    ItNeverGotToThatWall,
}

/// <summary>One sign the run never read, and why.</summary>
/// <param name="MapId">The map it is on.</param>
/// <param name="Square">Which wall.</param>
/// <param name="Address">The block behind it.</param>
/// <param name="Why">The reason.</param>
public sealed record UnreadSign(string MapId, GridPosition Square, uint Address, UnreadBecause Why)
{
    public override string ToString() => $"{MapId} ({Square.X},{Square.Y})  0x{Address:X8}";
}

/// <summary>
/// The sign scripts a run never ran, sorted by whether anything could have run them.
/// <para>
/// <b>241 printed "215 of the 519 sign scripts ran" and nothing about the other 304.</b> That
/// number reads the same whether the run is a few rooms short or three hundred signs are written
/// on walls nothing can walk up to, and those are opposite findings — one is a walk that has not
/// gone far enough and the other is a property of the cartridge. It is the same join
/// <see cref="WhyTheGatesAreShut"/> makes for flags, one list over.
/// </para>
/// <para>
/// Every bucket can be empty, including all three at once.
/// </para>
/// </summary>
public static class WhySignsWentUnread
{
    /// <summary>
    /// Every scripted sign in <paramref name="world"/> that is not in <paramref name="read"/>,
    /// with the reason.
    /// </summary>
    /// <param name="world">The world the run walked.</param>
    /// <param name="read">The signs it read, from <see cref="Attempt.SignsRead"/>.</param>
    /// <param name="reached">The maps it reached.</param>
    public static IReadOnlyList<UnreadSign> Of(
        WorldData world,
        IEnumerable<RanASign> read,
        IReadOnlyCollection<string> reached)
    {
        HashSet<(string, GridPosition)> already = [.. read.Select(r => (r.MapId, r.Square))];

        var unread = new List<UnreadSign>();

        foreach (MapData map in world.Maps)
        {
            // Asked with the WATER OPENED, which is what makes this a fact about the file. A
            // sign a surfing run can read and a walking one cannot is not a sign nothing can
            // stand beside, and filing it as one would put a bucket about the cartridge under
            // a lever — which is exactly the shape 211 caught.
            CollisionGrid grid = map.ToGrid(surfing: true);

            foreach (MapSign sign in map.Signs)
            {
                if (!sign.HasScript) continue;
                if (already.Contains((map.Id, sign.Square))) continue;

                // THE ORDER IS A DECISION AND IT IS SAID OUT LOUD.
                //
                // The file's answer comes first. A sign nothing can stand beside would not be
                // read on a map the run walked end to end, so calling it a reach problem is a
                // claim that walking further would fix something that walking cannot fix.
                unread.Add(new UnreadSign(
                    map.Id,
                    sign.Square,
                    sign.ScriptAddress,
                    !CanBeStoodBeside(grid, sign.Square) ? UnreadBecause.NothingCouldStandBesideIt
                    : !reached.Contains(map.Id) ? UnreadBecause.OnAMapItNeverReached
                    : UnreadBecause.ItNeverGotToThatWall));
            }
        }

        return unread;
    }

    /// <summary>How many went unread for each reason, biggest first.</summary>
    public static IReadOnlyList<(UnreadBecause Why, int Signs)> Counted(
        IEnumerable<UnreadSign> unread) =>
    [
        .. unread.GroupBy(s => s.Why)
            .Select(g => (Why: g.Key, Signs: g.Count()))
            .OrderByDescending(g => g.Signs)
            .ThenBy(g => g.Why),
    ];

    /// <summary>
    /// Whether any of the five squares a sign is read from is walkable — its own and the four
    /// around it, which is the rule the walk itself uses.
    /// </summary>
    private static bool CanBeStoodBeside(CollisionGrid grid, GridPosition at) =>
        grid.IsWalkable(at)
        || grid.IsWalkable(at with { Y = at.Y - 1 })
        || grid.IsWalkable(at with { Y = at.Y + 1 })
        || grid.IsWalkable(at with { X = at.X - 1 })
        || grid.IsWalkable(at with { X = at.X + 1 });
}
