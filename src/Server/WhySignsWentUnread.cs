using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>Why a run never read one of the cartridge's signs.</summary>
public enum UnreadBecause
{
    /// <summary>
    /// <b>Nothing could ever stand where it is read from, at any lever setting.</b> Not one of the
    /// squares this sign's kind allows is walkable, and that is asked with the water opened, so no
    /// run and no player reads this one.
    /// <para>
    /// WHICH squares those are is the kind's business (279, 280): one square for the 97 that name
    /// a side, and its own plus the four around it for the rest. Asking the five-square question
    /// about a sign the walk reads from one is a verdict about a walk nobody takes.
    /// </para>
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
    /// <param name="obeySignSides">
    /// Whether a sign whose kind names a side is read from that square alone (279, 280). It has to
    /// be the rule the run itself used or the buckets describe a walk nobody took, and it is a
    /// parameter rather than a constant so both answers come out of one process (241).
    /// </param>
    public static IReadOnlyList<UnreadSign> Of(
        WorldData world,
        IEnumerable<RanASign> read,
        IReadOnlyCollection<string> reached,
        bool obeySignSides = true)
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
                    !CanBeStoodBeside(grid, sign, obeySignSides)
                        ? UnreadBecause.NothingCouldStandBesideIt
                    : !reached.Contains(map.Id) ? UnreadBecause.OnAMapItNeverReached
                    : UnreadBecause.ItNeverGotToThatWall));
            }
        }

        return unread;
    }

    /// <summary>
    /// The signs no run in <paramref name="runs"/> read, with the reason — a sign read at ANY
    /// setting is read, and a map reached at any setting is reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One run's unread list is a fact about one lever setting.</b> The six settings differ in
    /// how far they walk, so "it never got to that wall" at the floor can be "it read it" three
    /// rows down, and a single run's buckets cannot say which signs are out of everybody's reach.
    /// That is the number this project wanted when it wrote "the sign scripts that run at no
    /// setting" and never had: it takes all six runs at once.
    /// </para>
    /// <para>
    /// The union is over the READ list and the REACHED list separately, because they answer
    /// separate questions and a sign can be on a map one setting reaches and another does not.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<UnreadSign> AtNoSetting(
        WorldData world,
        IEnumerable<(IEnumerable<RanASign> Read, IEnumerable<string> Reached)> runs,
        bool obeySignSides = true)
    {
        var read = new List<RanASign>();
        var reached = new HashSet<string>();

        foreach ((IEnumerable<RanASign> those, IEnumerable<string> maps) in runs)
        {
            read.AddRange(those);
            reached.UnionWith(maps);
        }

        return Of(world, read, reached, obeySignSides);
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
    /// Whether any square this sign can be read from is walkable — the ONE its kind names, or its
    /// own and the four around it when its kind names none. The same rule the walk itself uses,
    /// which is the whole point of it being here.
    /// </summary>
    /// <remarks>
    /// Public because the grid is the caller's choice and that choice is a question. This class
    /// asks it with the water OPEN, which is what makes its own answer a fact about the file; ask
    /// it with the water shut and the same sign says whether a swimmer is needed to read it.
    /// </remarks>
    public static bool CanBeStoodBeside(CollisionGrid grid, MapSign sign, bool obeySignSides = true) =>
        obeySignSides && sign.MustBeReadFrom is { } only
            ? grid.IsWalkable(only)
            : Around(sign.Square).Any(grid.IsWalkable);

    /// <summary>242's five squares: its own, and the four around it.</summary>
    private static IEnumerable<GridPosition> Around(GridPosition at) =>
    [
        at,
        at with { Y = at.Y - 1 },
        at with { Y = at.Y + 1 },
        at with { X = at.X - 1 },
        at with { X = at.X + 1 },
    ];
}
