using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>Somebody on a map, as the server has them right now.</summary>
public sealed class ServerObject(MapObject template)
{
    public MapObject Template { get; } = template;

    public int LocalId => Template.LocalId;

    public GridPosition Square { get; set; } = template.Square;

    public Direction Facing { get; set; } = template.Facing;

    /// <summary>When this one may next do something, in server seconds.</summary>
    public double NextMoveAt { get; set; }

    /// <summary>
    /// The player currently talking to this one, if anybody is.
    /// <para>
    /// Held rather than counted: only the player who started a conversation can end
    /// it, so a second player walking up and pressing the button cannot release
    /// somebody else's shopkeeper.
    /// </para>
    /// </summary>
    public int? HeldBy { get; set; }

    /// <summary>
    /// The player this one has spotted and is walking up to, if any.
    /// <para>
    /// A trainer who sees you across a route does not fight you from there. They walk
    /// over, and the walk is the part everybody remembers — it is why you learn to hug
    /// the far wall of a route rather than stroll down the middle of it.
    /// </para>
    /// </summary>
    public int? Approaching { get; set; }

    /// <summary>The squares left between them and whoever they spotted, nearest first.</summary>
    public Queue<GridPosition> Approach { get; } = new();

    /// <summary>When they may take the next step of that walk, in server seconds.</summary>
    public double NextApproachAt { get; set; }

    /// <summary>
    /// True once they are standing in front of the player and the fight has not started.
    /// <para>
    /// A beat, and the reason for it is that without one the whole thing is over in
    /// under a second: noticed, walked, fighting, with nothing in between long enough to
    /// see. The games hold on the mark before anybody moves and hold again on arrival,
    /// and those two pauses are most of what makes it read as somebody deciding to
    /// challenge you rather than as a battle screen appearing.
    /// </para>
    /// </summary>
    public bool Arrived { get; set; }

    public ObjectView ToView() =>
        new(LocalId, Template.GraphicsId, Square.X, Square.Y, Facing, Template.Heals);
}

/// <summary>
/// The living people on one map.
/// <para>
/// Only maps with somebody watching are simulated. There are sixteen hundred of these
/// across four hundred maps, and stepping all of them so that nobody sees it would be
/// the largest thing this server does.
/// </para>
/// </summary>
public sealed class MapPopulation
{
    /// <summary>Seconds between a wanderer's decisions. Roughly the games' pace.</summary>
    public const double MoveInterval = 1.2;

    /// <summary>How much of that interval is randomised, so a street does not march in step.</summary>
    private const double Jitter = 0.8;

    private readonly List<ServerObject> _objects;

    public MapPopulation(MapData map, BattleRng rng, double now)
    {
        _objects = map.Objects.Select(o => new ServerObject(o)).ToList();

        foreach (ServerObject entry in _objects)
            entry.NextMoveAt = now + rng.Next(100) / 100.0 * MoveInterval;
    }

    public IReadOnlyList<ServerObject> Objects => _objects;

    public IEnumerable<ObjectView> Views => _objects.Select(o => o.ToView());

    public ServerObject? At(GridPosition square) => _objects.FirstOrDefault(o => o.Square == square);

    public ServerObject? ById(int localId) => _objects.FirstOrDefault(o => o.LocalId == localId);

    /// <summary>
    /// Lets go of anybody whose holder no longer qualifies.
    /// <para>
    /// A hold has to be able to end without the client saying so. Players disconnect
    /// mid-sentence, walk through a door, and close the window — none of which sends
    /// anything, and all of which would otherwise leave somebody standing to attention
    /// forever.
    /// </para>
    /// </summary>
    public List<(int LocalId, int Holder)> Release(Func<int, bool> stale)
    {
        var let = new List<(int, int)>();

        foreach (ServerObject entry in _objects)
        {
            if (entry.HeldBy is not { } holder || !stale(holder)) continue;

            entry.HeldBy = null;
            let.Add((entry.LocalId, holder));
        }

        return let;
    }

    /// <summary>
    /// Moves whoever is due to move, and says who changed.
    /// <para>
    /// <paramref name="isFree"/> answers whether a square can be stepped onto, which
    /// only the world knows — it has to account for walls, for players, and for the
    /// other people on the same map.
    /// </para>
    /// </summary>
    public List<ObjectView> Step(BattleRng rng, double now, Func<GridPosition, bool> isFree)
    {
        var changed = new List<ObjectView>();

        foreach (ServerObject entry in _objects)
        {
            if (now < entry.NextMoveAt) continue;

            entry.NextMoveAt = now + MoveInterval + rng.Next(100) / 100.0 * Jitter;

            // Somebody being spoken to stays where they are and keeps looking at whoever
            // is speaking. Their turn comes round again as normal once released. The
            // same goes for somebody in the middle of walking up to a player: their
            // route is not a suggestion to be overridden by a wander.
            if (entry.HeldBy is not null || entry.Approaching is not null) continue;

            if (entry.Template.LooksAround)
            {
                Direction turned = Choose(rng, entry.Template);

                if (turned == entry.Facing) continue;

                entry.Facing = turned;
                changed.Add(entry.ToView());
                continue;
            }

            if (!entry.Template.Wanders) continue;

            Direction direction = Choose(rng, entry.Template);
            GridPosition wanted = entry.Square.Step(direction);

            // Facing changes whether or not the step happens, exactly as it does for a
            // player walking into a wall.
            bool turnedOnly = !entry.Template.IsWithinRange(wanted) || !isFree(wanted);

            entry.Facing = direction;
            if (!turnedOnly) entry.Square = wanted;

            changed.Add(entry.ToView());
        }

        return changed;
    }

    /// <summary>
    /// A direction for this one to try.
    /// <para>
    /// The types that wander along an axis pick between its two ends; everything else
    /// picks freely. Letting an up-and-down pacer choose sideways would have them
    /// leave their post the moment their range in x happened to be non-zero.
    /// </para>
    /// </summary>
    private static Direction Choose(BattleRng rng, MapObject template) => template.MovementType switch
    {
        3 or 4 => rng.OneIn(2) ? Direction.Up : Direction.Down,
        5 or 6 => rng.OneIn(2) ? Direction.Left : Direction.Right,
        _ => (Direction)rng.Next(4),
    };
}
