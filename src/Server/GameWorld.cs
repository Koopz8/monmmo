using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>A player as the server sees them.</summary>
public sealed record ServerPlayer(int Id, long AccountId, string Name)
{
    public string MapId { get; set; } = "";

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

/// <summary>
/// A message, who should receive it, and which map they have to be on.
/// <para>
/// The map scope is what stops a world of 425 maps behaving like one enormous room.
/// Without it every step anyone took anywhere would be sent to everyone.
/// </para>
/// </summary>
public sealed record Outgoing(NetMessage Message, int? OnlyTo = null, int? Except = null, string? OnMap = null);

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
public sealed class GameWorld
{
    /// <summary>
    /// Shortest interval between a player's steps. A client walking at the normal pace
    /// stays comfortably under this; one sending moves in a loop does not.
    /// </summary>
    public static readonly double MinimumStepInterval = WalkingCharacter.StepSeconds * 0.75;

    private readonly BattleRng _rng;
    private readonly WorldData _world;
    private readonly Dictionary<string, CollisionGrid> _grids = [];
    private readonly Dictionary<int, ServerPlayer> _players = [];
    private readonly object _gate = new();

    private int _nextPlayerId = 1;

    public GameWorld(WorldData world, string startingMapId, uint encounterSeed = 1)
    {
        _world = world;
        _rng = new BattleRng(encounterSeed);

        StartingMap = world.Find(startingMapId)
            ?? world.FindByName(startingMapId)
            ?? throw new ArgumentException($"No map '{startingMapId}' in this world.", nameof(startingMapId));
    }

    /// <summary>Where a new character begins. Every other map is reachable by walking.</summary>
    public MapData StartingMap { get; }

    /// <summary>Kept for callers that still think of the server as hosting one map.</summary>
    public MapData Map => StartingMap;

    public int MapCount => _world.Count;

    public MapData? MapOf(string mapId) => _world.Find(mapId);

    /// <summary>
    /// The collision grid for a map, built once and kept. Building one copies a whole
    /// map's walkability, which is not something to do on every step.
    /// </summary>
    public CollisionGrid GridOf(string mapId)
    {
        lock (_gate) return GridFor(mapId);
    }

    private CollisionGrid GridFor(string mapId)
    {
        if (_grids.TryGetValue(mapId, out CollisionGrid? cached)) return cached;

        MapData map = _world.Find(mapId) ?? StartingMap;
        CollisionGrid grid = map.ToGrid();

        _grids[mapId] = grid;
        return grid;
    }

    /// <summary>The starting map's grid, for callers that predate a world of many maps.</summary>
    public CollisionGrid Grid => GridOf(StartingMap.Id);

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

    /// <summary>Which map a player is on, for scoping a broadcast.</summary>
    public string? MapIdOf(int playerId)
    {
        lock (_gate) return _players.GetValueOrDefault(playerId)?.MapId;
    }

    /// <summary>Where a brand new character starts, and what it starts with.</summary>
    public SavedCharacter FreshCharacter()
    {
        lock (_gate)
        {
            GridPosition spawn = FindSpawn(StartingMap.Id);
            return SavedCharacter.Fresh(StartingMap.Id, spawn.X, spawn.Y);
        }
    }

    /// <summary>
    /// Admits a player where their save left them. Returns the messages to send: a
    /// welcome and the players sharing their map to the newcomer, and the newcomer to
    /// everyone on that map.
    /// </summary>
    public (ServerPlayer Player, List<Outgoing> Send) Join(long accountId, string name, SavedCharacter saved)
    {
        lock (_gate)
        {
            (string mapId, GridPosition square) = Resume(saved);

            var player = new ServerPlayer(_nextPlayerId++, accountId, Sanitise(name))
            {
                MapId = mapId,
                Square = square,
                Facing = saved.Facing,
                Balls = saved.Balls,
                Party = [.. saved.Party],
            };

            var send = new List<Outgoing>
            {
                new(
                    new Welcome(
                        player.Id, mapId, square.X, square.Y, player.Facing,
                        player.Balls, player.Party),
                    OnlyTo: player.Id),
            };

            // Tell the newcomer about everyone already on this map, before announcing them.
            foreach (ServerPlayer existing in _players.Values.Where(p => p.MapId == mapId))
                send.Add(new Outgoing(existing.ToAppeared(), OnlyTo: player.Id));

            _players[player.Id] = player;
            send.Add(new Outgoing(player.ToAppeared(), Except: player.Id, OnMap: mapId));

            return (player, send);
        }
    }

    public List<Outgoing> Leave(int playerId)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];

            string mapId = player.MapId;
            _players.Remove(playerId);

            return [new Outgoing(new PlayerLeft(playerId), OnMap: mapId)];
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

            CollisionGrid grid = GridFor(player.MapId);
            GridPosition wanted = player.Square.Step(direction);

            // Off the edge of the map is not a wall when a neighbour is joined there.
            if (!grid.Contains(wanted))
                return StepAcrossEdge(player, direction, nowSeconds);

            if (!grid.IsWalkable(wanted))
            {
                // Blocked is not an error: the client predicted the same thing and is
                // already standing still, facing the wall. Everyone else still needs
                // to see the turn.
                return [new Outgoing(
                    new PlayerMoved(playerId, player.Square.X, player.Square.Y, player.Facing),
                    OnMap: player.MapId)];
            }

            player.Square = wanted;
            player.LastStepAt = nowSeconds;

            var send = new List<Outgoing>
            {
                new(new PlayerMoved(playerId, wanted.X, wanted.Y, player.Facing), OnMap: player.MapId),
            };

            AfterArrival(player, send);
            return send;
        }
    }

    /// <summary>
    /// A step that leaves the map. Where a neighbour is joined to that edge the player
    /// walks straight onto it; otherwise the edge is a wall.
    /// </summary>
    private List<Outgoing> StepAcrossEdge(ServerPlayer player, Direction direction, double nowSeconds)
    {
        MapData map = _world.Find(player.MapId) ?? StartingMap;
        ConnectionSide side = SideFor(direction);

        if (map.ConnectionOn(side) is not { } connection ||
            _world.Find(connection.MapId) is not { } target)
        {
            return [new Outgoing(
                new PlayerMoved(player.Id, player.Square.X, player.Square.Y, player.Facing),
                OnMap: player.MapId)];
        }

        GridPosition arrival = AcrossEdge(player.Square, side, map, target, connection.Offset);
        CollisionGrid targetGrid = GridFor(target.Id);

        if (!targetGrid.IsWalkable(arrival))
        {
            // The neighbour exists but that particular square is solid. Treat it as the
            // wall it is rather than dropping the player into it.
            return [new Outgoing(
                new PlayerMoved(player.Id, player.Square.X, player.Square.Y, player.Facing),
                OnMap: player.MapId)];
        }

        player.LastStepAt = nowSeconds;

        List<Outgoing> send = Transfer(player, target.Id, arrival, player.Facing);

        // An edge crossing is ordinary walking: grass on the far side counts, and so
        // does a door on the far side.
        AfterArrival(player, send);

        return send;
    }

    /// <summary>
    /// Where a player lands after walking off an edge.
    /// <para>
    /// The connection's offset slides the neighbour along the shared edge, so a
    /// position on this map maps to that one by subtracting it. Getting the sign wrong
    /// puts every arrival in the same wrong column, consistently enough to look
    /// deliberate.
    /// </para>
    /// </summary>
    public static GridPosition AcrossEdge(
        GridPosition from, ConnectionSide side, MapData map, MapData target, int offset) => side switch
    {
        ConnectionSide.Down => new GridPosition(from.X - offset, 0),
        ConnectionSide.Up => new GridPosition(from.X - offset, target.Height - 1),
        ConnectionSide.Left => new GridPosition(target.Width - 1, from.Y - offset),
        _ => new GridPosition(0, from.Y - offset),
    };

    private static ConnectionSide SideFor(Direction direction) => direction switch
    {
        Direction.Up => ConnectionSide.Up,
        Direction.Down => ConnectionSide.Down,
        Direction.Left => ConnectionSide.Left,
        _ => ConnectionSide.Right,
    };

    /// <summary>
    /// What happens on the square a player has just arrived at, however they got there.
    /// <para>
    /// A warp fires at most once per step. That is what stops a door pair bouncing a
    /// player between two maps forever, and it needs no memory of where they came
    /// from: arriving somewhere is not the same as stepping onto it, so the warp at
    /// the far end of a door simply does not fire until the player steps off and back
    /// on.
    /// </para>
    /// </summary>
    private void AfterArrival(ServerPlayer player, List<Outgoing> send)
    {
        if (_world.Find(player.MapId)?.WarpAt(player.Square) is { } warp)
        {
            send.AddRange(TakeWarp(player, warp));
            return;
        }

        AddEncounterIfAny(player, send);
    }

    /// <summary>
    /// Sends a player through a warp, landing on the matching warp at the other end.
    /// <para>
    /// The destination is a warp id rather than coordinates, so a door leads to "the
    /// other side of that door" and the two ends cannot drift apart. When the id names
    /// nothing — which real cartridges do, for warps whose destination is decided at
    /// run time — the player arrives at a spawn instead of nowhere.
    /// </para>
    /// </summary>
    private List<Outgoing> TakeWarp(ServerPlayer player, Warp warp)
    {
        if (_world.Find(warp.TargetMapId) is not { } target)
            return [];

        GridPosition arrival = warp.TargetWarpId >= 0 && warp.TargetWarpId < target.Warps.Count
            ? target.Warps[warp.TargetWarpId].Square
            : FindSpawn(target.Id);

        if (!GridFor(target.Id).IsWalkable(arrival)) arrival = FindSpawn(target.Id);

        return Transfer(player, target.Id, arrival, player.Facing);
    }

    /// <summary>
    /// Moves a player between maps: gone to everyone on the old one, arrived to
    /// everyone on the new one, and a fresh view of the world for the player.
    /// </summary>
    private List<Outgoing> Transfer(ServerPlayer player, string mapId, GridPosition arrival, Direction facing)
    {
        string previous = player.MapId;

        var send = new List<Outgoing>
        {
            new(new PlayerLeft(player.Id), Except: player.Id, OnMap: previous),
        };

        player.MapId = mapId;
        player.Square = arrival;
        player.Facing = facing;

        send.Add(new Outgoing(
            new MapChanged(mapId, arrival.X, arrival.Y, facing),
            OnlyTo: player.Id));

        foreach (ServerPlayer existing in _players.Values.Where(p => p.MapId == mapId && p.Id != player.Id))
            send.Add(new Outgoing(existing.ToAppeared(), OnlyTo: player.Id));

        send.Add(new Outgoing(player.ToAppeared(), Except: player.Id, OnMap: mapId));

        return send;
    }

    /// <summary>
    /// Rolls for a wild encounter on whatever square the player is now standing on.
    /// <para>
    /// The roll is the server's, not the client's — otherwise a modified client could
    /// simply decline to meet anything, or meet whatever it liked.
    /// </para>
    /// </summary>
    private void AddEncounterIfAny(ServerPlayer player, List<Outgoing> send)
    {
        if (_world.Find(player.MapId) is not { } map) return;
        if (!map.IsEncounterSquare(player.Square)) return;

        GrassSteps++;

        if (WildEncounters.RollStep(_rng, map.Encounters!.Land) is not { } encounter) return;

        player.InBattle = true;

        send.Add(new Outgoing(
            new WildEncounterStarted(encounter.Species, encounter.Level, _rng.State),
            OnlyTo: player.Id));
    }

    /// <summary>
    /// The map and square a returning player resumes on.
    /// <para>
    /// A save naming a map this world no longer has, or a square it no longer allows,
    /// falls back to a spawn rather than being refused. A player stuck inside a wall
    /// because a world file was re-exported has no way out from their side.
    /// </para>
    /// </summary>
    private (string MapId, GridPosition Square) Resume(SavedCharacter saved)
    {
        if (_world.Find(saved.MapId) is not { } map)
            return (StartingMap.Id, FindSpawn(StartingMap.Id));

        var square = new GridPosition(saved.X, saved.Y);

        return GridFor(map.Id).IsWalkable(square)
            ? (map.Id, square)
            : (map.Id, FindSpawn(map.Id));
    }

    /// <summary>What to write down for a player, as they stand right now.</summary>
    public SavedCharacter? Snapshot(int playerId)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return null;

            return new SavedCharacter(
                player.MapId, player.Square.X, player.Square.Y, player.Facing, player.Balls, [.. player.Party]);
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
    /// Somewhere to put an arriving player: the first open square on that map that
    /// nobody is standing on, so two players joining together do not overlap.
    /// </summary>
    private GridPosition FindSpawn(string mapId)
    {
        CollisionGrid grid = GridFor(mapId);

        var taken = _players.Values
            .Where(p => p.MapId == mapId)
            .Select(p => p.Square)
            .ToHashSet();

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                var candidate = new GridPosition(x, y);
                if (grid.IsWalkable(candidate) && !taken.Contains(candidate)) return candidate;
            }
        }

        return grid.FirstWalkable();
    }

    /// <summary>Names come from clients, so they are length-capped and stripped of control characters.</summary>
    private static string Sanitise(string name)
    {
        string trimmed = new(name.Where(c => !char.IsControl(c)).Take(16).ToArray());
        return string.IsNullOrWhiteSpace(trimmed) ? "Player" : trimmed.Trim();
    }
}
