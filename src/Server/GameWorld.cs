using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>A player as the server sees them.</summary>
public sealed record ServerPlayer(int Id, long AccountId, string Name)
{
    public GridPosition Square { get; set; }

    public Direction Facing { get; set; } = Direction.Down;

    /// <summary>When this player last completed a step, in server seconds.</summary>
    public double LastStepAt { get; set; } = double.NegativeInfinity;

    /// <summary>True while this player is in a battle and should not be walking.</summary>
    public bool InBattle { get; set; }

    public int Balls { get; set; } = SavedCharacter.StartingBalls;

    /// <summary>
    /// The party, stored as the numbers a save holds rather than as battlers. The
    /// server has no cartridge and so cannot build a battler at all — it has no base
    /// stats to build one from. That constraint is the reason this is a save shape
    /// and not a game object.
    /// </summary>
    public List<SavedMon> Party { get; set; } = [];

    public PlayerAppeared ToAppeared() => new(Id, Name, Square.X, Square.Y, Facing);
}

/// <summary>A message and who should receive it.</summary>
public sealed record Outgoing(NetMessage Message, int? OnlyTo = null, int? Except = null);

/// <summary>
/// The authoritative world.
/// <para>
/// Every rule lives here and nothing in this file knows what a socket is. That is
/// deliberate: the interesting failures in a server are join/leave races, stale
/// positions and movement validation, and none of those should require a network to
/// reproduce in a test. The socket layer above only moves bytes.
/// </para>
/// <para>
/// Time is passed in rather than read from the clock, so rate limiting can be tested
/// without sleeping.
/// </para>
/// </summary>
public sealed class GameWorld(WorldData world, string startingMapId, uint encounterSeed = 1)
{
    private readonly BattleRng _rng = new(encounterSeed);

    /// <summary>
    /// Shortest interval between a player's steps. A client walking at the normal pace
    /// stays comfortably under this; one sending moves in a loop does not.
    /// </summary>
    public static readonly double MinimumStepInterval = WalkingCharacter.StepSeconds * 0.75;

    private readonly Dictionary<int, ServerPlayer> _players = [];
    private readonly object _gate = new();

    private int _nextPlayerId = 1;

    public MapData Map { get; } = world.Find(startingMapId)
        ?? world.FindByName(startingMapId)
        ?? throw new ArgumentException($"No map '{startingMapId}' in this world.", nameof(startingMapId));

    private CollisionGrid? _grid;

    /// <summary>
    /// The same collision grid the client predicts with — identical data, identical
    /// code, so the two cannot disagree about whether a step was legal.
    /// </summary>
    public CollisionGrid Grid => _grid ??= Map.ToGrid();

    /// <summary>
    /// How many steps have landed on an encounter square. Counted because a server
    /// that never rolls and a server that rolls and misses look identical otherwise,
    /// and at a typical rate most steps in grass do miss.
    /// </summary>
    public int GrassSteps { get; private set; }

    public int PlayerCount
    {
        get { lock (_gate) return _players.Count; }
    }

    public IReadOnlyList<ServerPlayer> Players
    {
        get { lock (_gate) return _players.Values.ToList(); }
    }

    public ServerPlayer? Find(int id)
    {
        lock (_gate) return _players.GetValueOrDefault(id);
    }

    /// <summary>Where a brand new character starts, and what it starts with.</summary>
    public SavedCharacter FreshCharacter()
    {
        lock (_gate)
        {
            GridPosition spawn = FindSpawn();
            return SavedCharacter.Fresh(Map.Id, spawn.X, spawn.Y);
        }
    }

    /// <summary>
    /// Admits a player where their save left them. Returns the messages to send: a
    /// welcome and the existing players to the newcomer, and the newcomer to everyone
    /// else.
    /// </summary>
    public (ServerPlayer Player, List<Outgoing> Send) Join(long accountId, string name, SavedCharacter saved)
    {
        lock (_gate)
        {
            var player = new ServerPlayer(_nextPlayerId++, accountId, Sanitise(name))
            {
                Square = ResumeSquare(saved),
                Facing = saved.Facing,
                Balls = saved.Balls,
                Party = [.. saved.Party],
            };

            var send = new List<Outgoing>
            {
                new(
                    new Welcome(
                        player.Id, Map.Id, player.Square.X, player.Square.Y, player.Facing,
                        player.Balls, player.Party),
                    OnlyTo: player.Id),
            };

            // Tell the newcomer about everyone already here, before announcing them.
            foreach (ServerPlayer existing in _players.Values)
                send.Add(new Outgoing(existing.ToAppeared(), OnlyTo: player.Id));

            _players[player.Id] = player;
            send.Add(new Outgoing(player.ToAppeared(), Except: player.Id));

            return (player, send);
        }
    }

    public List<Outgoing> Leave(int playerId)
    {
        lock (_gate)
        {
            if (!_players.Remove(playerId)) return [];
            return [new Outgoing(new PlayerLeft(playerId))];
        }
    }

    /// <summary>
    /// Validates and applies a step. The rules are the client's rules — the same
    /// <see cref="CollisionGrid"/> — plus a rate limit the client has no reason to hit.
    /// </summary>
    public List<Outgoing> Move(int playerId, Direction direction, double nowSeconds)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player))
                return [new Outgoing(new Rejected("Not in the world."), OnlyTo: playerId)];

            // Facing changes even when the step does not, so turning on the spot works.
            player.Facing = direction;

            if (nowSeconds - player.LastStepAt < MinimumStepInterval)
            {
                return [new Outgoing(
                    new MoveRejected(player.Square.X, player.Square.Y, player.Facing, "Too fast."),
                    OnlyTo: playerId)];
            }

            if (!Grid.TryStep(player.Square, direction, out GridPosition destination))
            {
                // Blocked is not an error: the client predicted the same thing and is
                // already standing still, facing the wall. Everyone else still needs
                // to see the turn.
                return [new Outgoing(new PlayerMoved(playerId, player.Square.X, player.Square.Y, player.Facing))];
            }

            player.Square = destination;
            player.LastStepAt = nowSeconds;

            var send = new List<Outgoing>
            {
                new(new PlayerMoved(playerId, destination.X, destination.Y, player.Facing)),
            };

            // The encounter roll is the server's, not the client's — otherwise a
            // modified client could simply decline to meet anything, or meet whatever
            // it liked.
            if (Map.IsEncounterSquare(destination))
            {
                GrassSteps++;

                if (WildEncounters.RollStep(_rng, Map.Encounters!.Land) is { } encounter)
                {
                    player.InBattle = true;

                    send.Add(new Outgoing(
                        new WildEncounterStarted(encounter.Species, encounter.Level, _rng.State),
                        OnlyTo: playerId));
                }
            }

            return send;
        }
    }

    /// <summary>
    /// The square a returning player resumes on.
    /// <para>
    /// A save from another map, or one pointing at a square this map no longer allows,
    /// falls back to a spawn rather than being refused. A player stuck inside a wall
    /// because a world file was re-exported has no way out from their side.
    /// </para>
    /// </summary>
    private GridPosition ResumeSquare(SavedCharacter saved)
    {
        if (saved.MapId != Map.Id) return FindSpawn();

        var square = new GridPosition(saved.X, saved.Y);
        return Grid.IsWalkable(square) ? square : FindSpawn();
    }

    /// <summary>What to write down for a player, as they stand right now.</summary>
    public SavedCharacter? Snapshot(int playerId)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return null;

            return new SavedCharacter(
                Map.Id, player.Square.X, player.Square.Y, player.Facing, player.Balls, [.. player.Party]);
        }
    }

    /// <summary>
    /// Records what a client says it came out of a battle with.
    /// <para>
    /// Capped at a legal party size here rather than trusted, because this arrives
    /// over a socket. It is not real validation — that needs the battle resolved
    /// server-side — but a client cannot at least claim a party of two hundred.
    /// </para>
    /// </summary>
    public bool UpdateSave(int playerId, int balls, IReadOnlyList<SavedMon> party)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return false;

            player.Balls = Math.Clamp(balls, 0, 999);
            player.Party = [.. party.Take(Party.MaxSize)];
            player.InBattle = false;

            return true;
        }
    }

    /// <summary>
    /// Somewhere to put an arriving player: the first open square that nobody is
    /// standing on, so two players joining together do not overlap.
    /// </summary>
    private GridPosition FindSpawn()
    {
        var taken = _players.Values.Select(p => p.Square).ToHashSet();

        for (int y = 0; y < Grid.Height; y++)
        {
            for (int x = 0; x < Grid.Width; x++)
            {
                var candidate = new GridPosition(x, y);
                if (Grid.IsWalkable(candidate) && !taken.Contains(candidate)) return candidate;
            }
        }

        return Grid.FirstWalkable();
    }

    /// <summary>Names come from clients, so they are length-capped and stripped of control characters.</summary>
    private static string Sanitise(string name)
    {
        string trimmed = new(name.Where(c => !char.IsControl(c)).Take(16).ToArray());
        return string.IsNullOrWhiteSpace(trimmed) ? "Player" : trimmed.Trim();
    }
}
