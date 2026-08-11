using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
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

    /// <summary>
    /// The battle this player is in, held by the server.
    /// <para>
    /// The server owns it because the server is the only side that should decide what
    /// happened. The client keeps the same battle code and can predict a turn, exactly
    /// as it predicts a step, but what it predicts is never what is recorded.
    /// </para>
    /// </summary>
    public Battle? Battle { get; set; }

    /// <summary>True while this player is in a battle and should not be walking.</summary>
    public bool InBattle => Battle is not null;

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

    private readonly BattleFactory? _battles;
    private readonly Progression? _progression;
    private readonly Dictionary<string, MapPopulation> _populated = [];
    private readonly BattleRng _objectRng = new(0x5EED);

    public GameWorld(WorldData world, string startingMapId, GameRules? rules = null, uint encounterSeed = 1)
    {
        _world = world;
        _rng = new BattleRng(encounterSeed);
        _battles = rules is null ? null : new BattleFactory(rules);
        _progression = rules is null ? null : new Progression(rules);

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

    /// <summary>True when this server has the numbers to decide a battle itself.</summary>
    public bool CanResolveBattles => _battles is not null;

    /// <summary>Where a brand new character starts, and what it starts with.</summary>
    public SavedCharacter FreshCharacter()
    {
        lock (_gate)
        {
            GridPosition spawn = FindSpawn(StartingMap.Id);
            SavedCharacter fresh = SavedCharacter.Fresh(StartingMap.Id, spawn.X, spawn.Y);

            // A starter at registration rather than one conjured at the first
            // encounter, so a party is never empty and the server never has to invent
            // a battler in the middle of a battle.
            return _battles?.Starter() is { } starter
                ? fresh with { Party = [starter] }
                : fresh;
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

            // A save from before healing existed, or one written mid-wipe, would leave
            // this account unable to start a battle for good. Waking up healthy is the
            // only state that is always recoverable.
            if (player.Party.Count > 0 && !CanFight(player)) HealParty(player);

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

            if (Populate(mapId, 0) is { } people)
                send.Add(new Outgoing(new ObjectsPlaced([.. people.Views]), OnlyTo: player.Id));

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
            // Cleared per step, not per refusal: sticky state would have the server
            // reporting the same reason over and over long after it stopped applying.
            LastEdgeRefusal = null;

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

            if (!grid.IsWalkable(wanted) || IsOccupied(player.MapId, wanted))
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

        if (map.ConnectionOn(side) is not { } connection)
        {
            LastEdgeRefusal = $"{side} edge of {map.Id}: no connection";
            return [Stay(player)];
        }

        if (_world.Find(connection.MapId) is not { } target)
        {
            LastEdgeRefusal = $"{side} edge of {map.Id}: {connection.MapId} is not in this world";
            return [Stay(player)];
        }

        GridPosition arrival = AcrossEdge(player.Square, side, map, target, connection.Offset);
        CollisionGrid targetGrid = GridFor(target.Id);

        if (!targetGrid.IsWalkable(arrival) || IsOccupied(target.Id, arrival))
        {
            // The neighbour exists but that particular square is solid. Treat it as the
            // wall it is rather than dropping the player into it.
            LastEdgeRefusal =
                $"{side} edge of {map.Id} from {player.Square}: {target.Id} {arrival} is solid " +
                $"(offset {connection.Offset}, target {target.Width}x{target.Height})";

            return [Stay(player)];
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

    /// <summary>
    /// True when somebody is standing on a square.
    /// <para>
    /// Read from the living population rather than the map file, because they move now.
    /// A map nobody is watching is not simulated, so its people are wherever the
    /// cartridge put them — which is also where they will be when somebody arrives.
    /// </para>
    /// </summary>
    private bool IsOccupied(string mapId, GridPosition square) =>
        _populated.TryGetValue(mapId, out MapPopulation? people)
            ? people.At(square) is not null
            : _world.Find(mapId)?.ObjectAt(square) is not null;

    /// <summary>
    /// The people on a map, brought to life if this is the first player to see it.
    /// <para>
    /// Lazily, because there are sixteen hundred of them across four hundred maps and
    /// stepping the ones nobody can see would be the largest thing this server does.
    /// </para>
    /// </summary>
    private MapPopulation? Populate(string mapId, double now)
    {
        if (_populated.TryGetValue(mapId, out MapPopulation? existing)) return existing;
        if (_world.Find(mapId) is not { } map) return null;

        var people = new MapPopulation(map, _objectRng, now);

        _populated[mapId] = people;
        return people;
    }

    /// <summary>
    /// Moves everybody who is due to move, on every map somebody is standing on.
    /// <para>
    /// Called on a timer rather than in response to anything, which makes it the first
    /// thing in this world that happens without a player asking for it.
    /// </para>
    /// </summary>
    public List<Outgoing> Tick(double nowSeconds)
    {
        lock (_gate)
        {
            var send = new List<Outgoing>();

            foreach (string mapId in _players.Values.Select(p => p.MapId).Distinct().ToList())
            {
                if (Populate(mapId, nowSeconds) is not { } people) continue;

                // A conversation ends when the client says so, and also when it cannot:
                // a player who disconnected or walked through a door is not talking to
                // anybody, whatever the last thing they sent was.
                people.Release(holder =>
                    !_players.TryGetValue(holder, out ServerPlayer? talker) || talker.MapId != mapId);

                foreach (ObjectView moved in people.Step(_objectRng, nowSeconds, square => IsFree(mapId, square)))
                    send.Add(new Outgoing(new ObjectMoved(moved.LocalId, moved.X, moved.Y, moved.Facing), OnMap: mapId));
            }

            // Maps nobody can see any more stop being simulated, and forget where their
            // people had wandered to. That is a deliberate simplification and worth
            // knowing: walk away and back, and the street is as the cartridge left it.
            foreach (string mapId in _populated.Keys.ToList())
            {
                if (_players.Values.Any(p => p.MapId == mapId)) continue;
                _populated.Remove(mapId);
            }

            return send;
        }
    }

    /// <summary>
    /// A player has started talking to somebody: hold them still and turn them round.
    /// <para>
    /// What is said is decided entirely on the client, from its own cartridge. This
    /// server has never seen a script and has nothing to contribute to the
    /// conversation — the one thing it owns is where everybody is standing, so that is
    /// the one thing it does about it.
    /// </para>
    /// <para>
    /// Refused when the named person is not on the square the player is facing. That is
    /// not anti-cheat — there is nothing here to cheat at yet — it is what keeps the
    /// hold from being a way to freeze anybody on the map from anywhere on it.
    /// </para>
    /// </summary>
    public List<Outgoing> StartTalking(int playerId, int localId)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];
            if (!_populated.TryGetValue(player.MapId, out MapPopulation? people)) return [];
            if (people.ById(localId) is not { } person) return [];

            // Anyone this player was already holding is let go first, so a client that
            // loses a "finished" message cannot accumulate frozen people behind it.
            people.Release(holder => holder == playerId);

            if (person.Square != player.Square.Step(player.Facing)) return [];

            person.HeldBy = playerId;

            Direction turned = Interaction.Opposite(player.Facing);
            if (person.Facing == turned) return [];

            person.Facing = turned;

            return [new Outgoing(
                new ObjectMoved(person.LocalId, person.Square.X, person.Square.Y, person.Facing),
                OnMap: player.MapId)];
        }
    }

    /// <summary>The text box is closed. Whoever this player was holding carries on.</summary>
    public void StopTalking(int playerId)
    {
        lock (_gate)
        {
            foreach (MapPopulation people in _populated.Values)
                people.Release(holder => holder == playerId);
        }
    }

    /// <summary>Who this player is currently holding still, for tests and for reporting.</summary>
    public int? TalkingTo(int playerId)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return null;
            if (!_populated.TryGetValue(player.MapId, out MapPopulation? people)) return null;

            return people.Objects.FirstOrDefault(o => o.HeldBy == playerId)?.LocalId;
        }
    }

    /// <summary>Whether anybody at all could stand on a square: not a wall, nobody there.</summary>
    private bool IsFree(string mapId, GridPosition square) =>
        GridFor(mapId).IsWalkable(square) &&
        !IsOccupied(mapId, square) &&
        !_players.Values.Any(p => p.MapId == mapId && p.Square == square);

    /// <summary>Standing still, announced so everyone still sees the turn.</summary>
    private Outgoing Stay(ServerPlayer player) =>
        new(new PlayerMoved(player.Id, player.Square.X, player.Square.Y, player.Facing), OnMap: player.MapId);

    /// <summary>
    /// Why the last attempt to walk off an edge did not move anyone.
    /// <para>
    /// An edge with no neighbour, a neighbour that is not in the world, and an arrival
    /// square that is solid all look identical from the player's side — they walk into
    /// what feels like a wall. Only the server can tell them apart, so it writes down
    /// which it was.
    /// </para>
    /// </summary>
    public string? LastEdgeRefusal { get; private set; }

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

        if (Populate(mapId, 0) is { } arrived)
            send.Add(new Outgoing(new ObjectsPlaced([.. arrived.Views]), OnlyTo: player.Id));

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
        if (_battles is null) return;

        if (_battles.Wild(encounter.Species, encounter.Level) is not { } wild) return;

        // No healthy lead, no encounter. Starting one here would start a battle that
        // was over before its first turn — which is exactly the freeze this fixes.
        if (LeadBattler(player) is not { } lead) return;

        player.Battle = new Battle(lead, wild, _rng.State);

        send.Add(new Outgoing(
            new BattleStarted(BattleFactory.View(lead), BattleFactory.View(wild), player.Balls),
            OnlyTo: player.Id));
    }

    /// <summary>The first party member still standing, rebuilt from what was saved.</summary>
    private Battler? LeadBattler(ServerPlayer player)
    {
        if (_battles is null) return null;

        foreach (SavedMon saved in player.Party)
        {
            if (_battles.Restore(saved) is { } battler && !battler.HasFainted) return battler;
        }

        // Nothing can fight. Starting a battle here would start one already lost: it
        // would be over before the first turn, and the player would be left pressing
        // buttons at a screen that has nothing left to say.
        return null;
    }

    /// <summary>
    /// Puts a wiped party back on its feet.
    /// <para>
    /// The games send you to a centre. There are none yet, so this stands in for one —
    /// and it has to exist in some form, because without it a single loss ends an
    /// account permanently: no healthy lead means no encounters, and no encounters
    /// means no way back.
    /// </para>
    /// </summary>
    private void HealParty(ServerPlayer player)
    {
        if (_battles is null) return;

        for (int i = 0; i < player.Party.Count; i++)
            player.Party[i] = _battles.Healed(player.Party[i]);
    }

    /// <summary>True when at least one party member could take a turn.</summary>
    private bool CanFight(ServerPlayer player) =>
        _battles is not null && player.Party.Any(_battles.CanFight);

    /// <summary>
    /// Resolves one turn of a battle.
    /// <para>
    /// The opponent's choice is made here too. A wild creature using its first move
    /// every turn is not much of an opponent, but it is the server's decision rather
    /// than something a client could ask for, which is the part that matters.
    /// </para>
    /// </summary>
    public List<Outgoing> TakeBattleTurn(int playerId, BattleAction action)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player) || player.Battle is not { } battle)
                return [new Outgoing(new Rejected("You are not in a battle."), OnlyTo: playerId)];

            // Throwing is refused here rather than trusted: the count is the server's.
            if (action is BattleAction.ThrowBall && player.Balls <= 0)
                action = new BattleAction.UseMove(0);
            else if (action is BattleAction.ThrowBall) player.Balls--;

            List<BattleEvent> events = battle.ResolveTurn(action, new BattleAction.UseMove(0));

            // Slotted in ahead of the closing event rather than appended, because the
            // games pay out between "it fainted" and the end of the battle. Appending
            // put it after "You won the battle!", which reads backwards and is the
            // easiest line in a battle to press past without reading.
            if (battle.IsOver && battle.Winner == Side.Player && !battle.OpponentCaught)
            {
                List<BattleEvent> payout = AwardExperience(player, battle);

                int ended = events.FindIndex(e => e is BattleEvent.Ended);
                events.InsertRange(ended < 0 ? events.Count : ended, payout);
            }

            var send = new List<Outgoing>
            {
                new(
                    new BattleUpdate(events, battle.Player.CurrentHp, battle.Opponent.CurrentHp, player.Balls),
                    OnlyTo: playerId),
            };

            if (!battle.IsOver) return send;

            send.Add(new Outgoing(FinishBattle(player, battle), OnlyTo: playerId));
            return send;
        }
    }

    /// <summary>
    /// Pays out for a win, and writes the result straight into the party.
    /// <para>
    /// Only the battler that fought is paid. Sharing it out across a party is a later
    /// problem, and one that needs a rule about who counts as having taken part.
    /// </para>
    /// </summary>
    private List<BattleEvent> AwardExperience(ServerPlayer player, Battle battle)
    {
        if (_progression is null) return [];

        int lead = player.Party.FindIndex(m => m.Species == battle.Player.Species.Index);
        if (lead < 0) return [];

        (SavedMon grown, List<BattleEvent> events) = _progression.Award(
            player.Party[lead], battle.Opponent.Species.Index, battle.Opponent.Level);

        player.Party[lead] = grown;

        return events;
    }

    /// <summary>
    /// Closes a battle and writes its consequences into the party.
    /// <para>
    /// Health and status carry out of a battle, and anything caught joins the party —
    /// both decided here, from the battle the server ran, rather than reported by the
    /// client afterwards.
    /// </para>
    /// </summary>
    private BattleFinished FinishBattle(ServerPlayer player, Battle battle)
    {
        Side? winner = battle.Winner;
        bool caught = battle.OpponentCaught;

        // The lead was rebuilt from a save, so what happened to it has to be written
        // back to that save rather than to the battler, which is about to be discarded.
        if (player.Party.Count > 0)
        {
            int lead = player.Party.FindIndex(m => m.Species == battle.Player.Species.Index);

            // Health and status only. The level, moves and experience have already been
            // written by the payout, and rebuilding from the battler would undo them —
            // that battler was built before the battle and never grew.
            if (lead >= 0)
            {
                player.Party[lead] = player.Party[lead] with
                {
                    CurrentHp = battle.Player.CurrentHp,
                    Status = battle.Player.Status,
                };
            }
        }

        if (caught && player.Party.Count < Party.MaxSize)
            player.Party.Add(BattleFactory.Save(battle.Opponent));

        player.Battle = null;

        // Losing costs nothing but the walk back, for now. What it must not cost is
        // the account: a wiped party can never start another battle, so it would have
        // no way to recover on its own.
        if (winner == Side.Opponent) HealParty(player);

        return new BattleFinished(winner, caught, player.Balls, [.. player.Party]);
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
