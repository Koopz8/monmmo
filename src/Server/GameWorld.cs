using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Scripts;
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
    public Encounter? Battle { get; set; }

    /// <summary>True while this player is in a battle and should not be walking.</summary>
    public bool InBattle => Battle is not null;

    /// <summary>
    /// The person walking over to fight this player, if somebody has spotted them.
    /// <para>
    /// Held on the player rather than only on the person, because what it decides is
    /// whether this player may move — and a rule about a player belongs where the moving
    /// is checked rather than at the far end of a search through everybody on the map.
    /// </para>
    /// </summary>
    public int? WatchedBy { get; set; }

    /// <summary>
    /// Trainers this player has already beaten, so they do not start again the moment
    /// you walk back past them.
    /// </summary>
    public HashSet<int> DefeatedTrainers { get; } = [];

    /// <summary>
    /// The centre this player last rested at, and the square they stood on to do it.
    /// <para>
    /// Where they wake up after blacking out. Nothing until they have visited one, which
    /// is the correct answer rather than a missing one — a character who has never been
    /// to a centre wakes where every character starts.
    /// </para>
    /// </summary>
    public string? RestingAt { get; set; }

    public GridPosition RestingSquare { get; set; }

    /// <summary>
    /// The cartridge's own bookkeeping for this player: which script flags are set and
    /// what the script variables hold.
    /// <para>
    /// Stored and handed back without being understood. The server cannot run a script
    /// — the bytes are on an image it has never seen — so it cannot know that one of
    /// these numbers is the parcel and another is the bicycle. What it can do is be the
    /// one place they live, which is what stops two machines disagreeing about whether
    /// something has already happened.
    /// </para>
    /// </summary>
    public ScriptState Script { get; init; } = new();

    /// <summary>Balls already picked up off the ground, as "map:person".</summary>
    public HashSet<string> ItemsTaken { get; } = [];

    /// <summary>
    /// Things this player has moved out of the way on the map they are standing on.
    /// <para>
    /// Per player, because a felled tree that everybody could walk through would let one
    /// person open every route in the world for strangers. Cleared on leaving the map,
    /// because that is what the games do and because the alternative — remembering it
    /// forever — is a save file that grows by one entry per tree.
    /// </para>
    /// </summary>
    public HashSet<int> Shifted { get; } = [];

    /// <summary>
    /// Everything this player is carrying.
    /// <para>
    /// A bag rather than a count of balls, which is what this used to be. The count was
    /// a stand-in from before there was an item table to read, and it could only ever
    /// have described one item.
    /// </para>
    /// </summary>
    public Bag Bag { get; set; } = new();

    public int Money { get; set; } = SavedCharacter.StartingMoney;

    /// <summary>
    /// What the shop this player has open sells, or nothing when none is.
    /// <para>
    /// Held rather than looked up on each purchase. A player who walks away mid-shop
    /// would otherwise keep buying from wherever they now stand, and a player standing
    /// between two shopkeepers would be buying from whichever the loop reached first.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Shopping { get; set; } = [];

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

    private readonly GameRules? _rules;
    private readonly BattleFactory? _battles;
    private readonly Progression? _progression;
    private readonly Dictionary<string, MapPopulation> _populated = [];
    private readonly BattleRng _objectRng = new(0x5EED);


    public GameWorld(WorldData world, string startingMapId, GameRules? rules = null, uint encounterSeed = 1)
    {
        _world = world;
        _rng = new BattleRng(encounterSeed);
        _rules = rules;
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
            SavedCharacter withBalls = fresh with { Items = StartingItems() };

            return _battles?.Starter() is { } starter
                ? withBalls with { Party = [starter] }
                : withBalls;
        }
    }

    /// <summary>
    /// What a new account is handed: some ordinary balls, if this world knows what one
    /// is. A server with no rules file hands out nothing, because it has no idea which
    /// number means a ball.
    /// </summary>
    private List<BagEntry> StartingItems()
    {
        if (_rules is null) return [];

        ItemData? ball = Enumerable.Range(1, 512)
            .Select(_rules.ItemAt)
            .FirstOrDefault(i => i is { Ball: BallKind.Poke });

        return ball is null ? [] : [new BagEntry(ball.Id, SavedCharacter.StartingBalls)];
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
                Bag = new Bag(saved.Items),
                Money = saved.Money,
                Party = [.. saved.Party],
                Script = new ScriptState(
                    saved.Flags,
                    saved.Variables.Select(v => new KeyValuePair<int, int>(v.Id, v.Value))),
                RestingAt = saved.RestingAt,
                RestingSquare = new GridPosition(saved.RestingX, saved.RestingY),
            };

            foreach (int beaten in saved.DefeatedTrainers) player.DefeatedTrainers.Add(beaten);
            foreach (string taken in saved.ItemsTaken) player.ItemsTaken.Add(taken);

            // A save from before healing existed, or one written mid-wipe, would leave
            // this account unable to start a battle for good. Waking up healthy is the
            // only state that is always recoverable.
            if (player.Party.Count > 0 && !CanFight(player)) HealParty(player);

            var send = new List<Outgoing>
            {
                new(
                    new Welcome(
                        player.Id, mapId, square.X, square.Y, player.Facing,
                        player.Money, player.Bag.Entries, player.Party)
                    {
                        Flags = [.. player.Script.Flags],
                        Variables = [.. player.Script.Variables.Select(v => new SavedVariable(v.Key, v.Value))],
                        Beaten = [.. player.DefeatedTrainers],
                    },
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

            AbandonApproaches(playerId);

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

            // Somebody is walking over to fight. Standing still for it is the rule, and
            // it is enforced here rather than asked for politely, because a client that
            // decided when it had been seen could decide it never had.
            if (player.WatchedBy is not null)
            {
                return [new Outgoing(
                    new MoveRejected(player.Square.X, player.Square.Y, player.Facing, "Somebody wants a word."),
                    OnlyTo: playerId)];
            }

            // Facing changes even when the step does not, so turning on the spot works.
            Direction before = player.Facing;
            player.Facing = direction;

            CollisionGrid grid = GridFor(player.MapId);
            GridPosition wanted = player.Square.Step(direction);

            if (grid.Contains(wanted) && (!grid.IsWalkable(wanted) || IsOccupiedFor(player, player.MapId, wanted)))
            {
                // Blocked is not an error: the client predicted the same thing and is
                // already standing still, facing the wall. Everyone else still needs
                // to see the turn.
                //
                // Checked before the interval, because nobody has moved. Rate limiting a
                // turn would refuse the last thing a player does before pressing the
                // button — walk up to somebody, turn to face them, speak — and refuse it
                // precisely when they did it briskly.
                return before == direction
                    ? []
                    : [new Outgoing(
                        new PlayerMoved(playerId, player.Square.X, player.Square.Y, player.Facing),
                        OnMap: player.MapId)];
            }

            if (nowSeconds - player.LastStepAt < MinimumStepInterval)
            {
                return [new Outgoing(
                    new MoveRejected(player.Square.X, player.Square.Y, player.Facing, "Too fast."),
                    OnlyTo: playerId)];
            }

            // Off the edge of the map is not a wall when a neighbour is joined there.
            if (!grid.Contains(wanted))
                return StepAcrossEdge(player, direction, nowSeconds);

            player.Square = wanted;
            player.LastStepAt = nowSeconds;

            var send = new List<Outgoing>
            {
                new(new PlayerMoved(playerId, wanted.X, wanted.Y, player.Facing), OnMap: player.MapId),
            };

            AfterArrival(player, send, nowSeconds);
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

        if (!targetGrid.IsWalkable(arrival) || IsOccupiedFor(player, target.Id, arrival))
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
        AfterArrival(player, send, nowSeconds);

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
    private bool IsOccupied(string mapId, GridPosition square) => Standing(mapId, square) is not null;

    /// <summary>
    /// The same question asked on behalf of somebody, which is not the same question.
    /// <para>
    /// A tree one player has cut is still standing for everybody else, so whether a
    /// square is blocked depends on who is asking. This is the only place in the server
    /// where that is true, and it is why the plain <see cref="IsOccupied"/> above is
    /// kept for the callers who genuinely have no player in hand.
    /// </para>
    /// </summary>
    private bool IsOccupiedFor(ServerPlayer player, string mapId, GridPosition square) =>
        Standing(mapId, square) is { } who &&
        !(mapId == player.MapId && player.Shifted.Contains(who));

    /// <summary>Who is on a square, by local id, or nothing.</summary>
    private int? Standing(string mapId, GridPosition square) =>
        _populated.TryGetValue(mapId, out MapPopulation? people)
            ? people.At(square)?.LocalId
            : _world.Find(mapId)?.ObjectAt(square)?.LocalId;

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

                send.AddRange(StepApproaches(mapId, people, nowSeconds));
            }

            // Maps nobody can see any more stop being simulated, and forget where their
            // people had wandered to. That is a deliberate simplification and worth
            // knowing: walk away and back, and the street is as the cartridge left it.
            foreach (string mapId in _populated.Keys.ToList())
            {
                if (_players.Values.Any(p => p.MapId == mapId)) continue;

                // A map about to stop being simulated may still hold somebody walking
                // towards a player who left it by some route that did not go through a
                // transfer. Letting it go quietly leaves that player standing still.
                foreach (ServerObject person in _populated[mapId].Objects)
                {
                    if (person.Approaching is not { } waiting) continue;
                    if (!_players.TryGetValue(waiting, out ServerPlayer? gone)) continue;
                    if (gone.WatchedBy is null) continue;

                    gone.WatchedBy = null;
                    send.Add(new Outgoing(new ApproachEnded(), OnlyTo: waiting));
                }

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
            LastTalkOutcome = null;

            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];

            if (!_populated.TryGetValue(player.MapId, out MapPopulation? people))
            {
                LastTalkOutcome = "that map is not populated";
                return [];
            }

            if (people.ById(localId) is not { } person)
            {
                LastTalkOutcome = $"no object {localId} on {player.MapId}";
                return [];
            }

            // Anyone this player was already holding is let go first, so a client that
            // loses a "finished" message cannot accumulate frozen people behind it.
            people.Release(holder => holder == playerId);

            // The square in front, or the one past it when a counter is in the way.
            CollisionGrid grid = GridFor(player.MapId);

            bool within = Interaction
                .Reachable(player.Square, player.Facing, square => !grid.IsWalkable(square))
                .Contains(person.Square);

            if (!within)
            {
                LastTalkOutcome =
                    $"object {localId} is at {person.Square}, and from {player.Square} facing " +
                    $"{player.Facing} the reachable squares are " +
                    string.Join(" and ", Interaction
                        .Reachable(player.Square, player.Facing, square => !grid.IsWalkable(square)));

                return [];
            }

            // Somebody who wants a fight gets one. The client opened a text box on the
            // way in; the battle arriving is what closes it.
            if (StartTrainerBattle(player, person.Template) is { Count: > 0 } challenge)
            {
                LastTalkOutcome = $"a fight with trainer {person.Template.TrainerId}";
                return challenge;
            }

            // Something in the way, and somebody in the party who can move it. Before
            // the item branch because an obstacle is not a person: there is nothing to
            // hold still and nothing to hand over.
            if (person.Template.IsObstacle)
            {
                // Asked of the party rather than of the script state: what a party knows
                // changes every time one of them learns a move, and a copy kept anywhere
                // else is a copy that goes stale between here and the next level-up.
                int slot = ScriptState.SlotKnowing(
                    player.Party.Select(m => m.Moves), person.Template.ShiftedBy);

                if (slot == ScriptState.NoSlot)
                {
                    // Not a refusal to report. The client ran the same script against the
                    // same party and is already showing the cartridge's own line about
                    // needing somebody who can do this.
                    LastTalkOutcome =
                        $"something needing move {person.Template.ShiftedBy}, which nobody in the party knows";

                    return [];
                }

                player.Shifted.Add(person.LocalId);

                LastTalkOutcome =
                    $"something moved out of the way with move {person.Template.ShiftedBy}, " +
                    $"by party member {slot + 1}";

                return
                [
                    new Outgoing(
                        new ObstacleShifted(person.LocalId, person.Template.ShiftedBy, slot),
                        OnlyTo: playerId),
                ];
            }

            // Something handed over, before anything else. For a ball on the ground that
            // is the whole interaction and there is nobody to hold still. For the fifteen
            // people who hand something over *while* talking it is the first thing that
            // happens and not the last, so those fall through to be held and read.
            List<Outgoing> given = [];
            string? gift = null;

            if (person.Template.GivesItem && _rules is not null)
            {
                string what = $"{player.MapId}:{person.LocalId}";

                if (player.ItemsTaken.Add(what))
                {
                    int count = Math.Max(1, person.Template.GivesCount);

                    player.Bag.Add(person.Template.GivesItemId, count);

                    gift = $"item {person.Template.GivesItemId} x{count} off the ground";

                    given.Add(new Outgoing(
                        new ItemFound(person.Template.GivesItemId, count, player.Bag.Entries),
                        OnlyTo: playerId));
                }
                else
                {
                    gift = "an item that has already been picked up";
                }

                if (!person.Template.Talks)
                {
                    LastTalkOutcome = gift;
                    return given;
                }
            }

            List<Outgoing> said = Talk();

            LastTalkOutcome = gift is null
                ? LastTalkOutcome
                : $"{gift}, and then {LastTalkOutcome ?? "nothing"}";

            return given.Count == 0 ? said : [.. given, .. said];

            // The rest of the conversation, as a step of its own so that handing
            // something over can happen before it rather than instead of it.
            List<Outgoing> Talk()
            {
                // A counter that heals, before the one that sells. Both are counters and
                // nobody is both, but the order says which this project would rather get
                // wrong: a shop that healed you would be strange and a nurse that charged
                // you would be worse.
                if (person.Template.Heals && _battles is not null && player.Party.Count > 0)
                {
                    bool needed = player.Party.Any(m => !_battles.IsWell(m));

                    HealParty(player);

                    // Remembered here rather than on arriving at the map: what makes a
                    // centre yours is having stood at the counter, and a player who walked
                    // through one on the way somewhere has not.
                    player.RestingAt = player.MapId;
                    player.RestingSquare = player.Square;

                    person.HeldBy = playerId;

                    LastTalkOutcome = needed
                        ? $"a centre: {player.Party.Count} back on their feet"
                        : "a centre, though nobody needed it";

                    return
                    [
                        new Outgoing(
                            new PartyHealed([.. player.Party], needed),
                            OnlyTo: playerId),
                    ];
                }

                // A shop opens on top of the hold rather than instead of it: the shopkeeper
                // still has to stand still while somebody is buying from them.
                List<Outgoing> shop = OpenShop(player, person.Template);

                person.HeldBy = playerId;

                LastTalkOutcome = person.Template.IsShopkeeper && shop.Count == 0
                    ? $"object {localId} sells {person.Template.Stock.Count} things, none of which this server has an item for"
                    : shop.Count > 0
                        ? $"a shop with {person.Template.Stock.Count} things in it"
                        : $"object {localId} held still; they sell nothing";

                if (shop.Count > 0)
                {
                    Direction facing = Interaction.Opposite(player.Facing);

                    if (person.Facing != facing)
                    {
                        person.Facing = facing;

                        shop.Add(new Outgoing(
                            new ObjectMoved(person.LocalId, person.Square.X, person.Square.Y, person.Facing),
                            OnMap: player.MapId));
                    }

                    return shop;
                }

                Direction turned = Interaction.Opposite(player.Facing);
                if (person.Facing == turned) return [];

                person.Facing = turned;

                return [new Outgoing(
                    new ObjectMoved(person.LocalId, person.Square.X, person.Square.Y, person.Facing),
                    OnMap: player.MapId)];
            }
        }
    }

    /// <summary>
    /// Starts a fight with somebody standing on a map, if there is one to be had.
    /// <para>
    /// Refused for anybody who is not a trainer, who names no trainer id, who has
    /// already been beaten, or who this server has no party for. All four are ordinary
    /// — most people on a map are none of these things — so none of them is an error.
    /// </para>
    /// </summary>
    private List<Outgoing> StartTrainerBattle(ServerPlayer player, MapObject trainer)
    {
        if (_battles is null || player.InBattle) return [];
        if (!trainer.CanBeFought) return [];
        if (player.DefeatedTrainers.Contains(trainer.TrainerId)) return [];

        List<Battler> party = _battles.TrainerParty(trainer.TrainerId);
        if (party.Count == 0) return [];

        // Same rule as a wild encounter: no healthy lead, no battle. Starting one here
        // would start a fight that was already lost before its first turn.
        if (LeadBattler(player) is not { } lead) return [];

        player.Battle = new Encounter(lead.Slot, lead.Battler, party, _rng.State, trainer.TrainerId);

        return
        [
            new Outgoing(
                new BattleStarted(
                    BattleFactory.View(lead.Battler),
                    BattleFactory.View(party[0]),
                    BallsOf(player),
                    MedicineOf(player),
                    trainer.TrainerId),
                OnlyTo: player.Id),
        ];
    }

    /// <summary>
    /// Starts somebody walking up to the player who wandered into their line of sight.
    /// <para>
    /// The fight does not begin here. What begins is a walk, and until it finishes the
    /// player stands still — which is the server's business rather than the client's,
    /// because a client that decided when it was allowed to move again could simply
    /// decide never to have been seen.
    /// </para>
    /// </summary>
    private List<Outgoing> BeginApproach(ServerPlayer player, MapObject watcher, double nowSeconds)
    {
        if (!_populated.TryGetValue(player.MapId, out MapPopulation? people)) return [];
        if (people.ById(watcher.LocalId) is not { } person) return [];
        if (person.Approaching is not null) return [];

        person.Approaching = player.Id;
        person.Arrived = false;
        person.Approach.Clear();

        // They stand there a moment first. The mark over their head is up for about this
        // long on the client, and a walk that begins underneath it looks like somebody
        // who was already moving.
        person.NextApproachAt = nowSeconds + NoticePause;

        foreach (GridPosition square in watcher.ApproachTo(player.Square)) person.Approach.Enqueue(square);

        player.WatchedBy = watcher.LocalId;

        return [new Outgoing(new TrainerSpotted(watcher.LocalId), OnlyTo: player.Id)];
    }

    /// <summary>
    /// One step of every walk in progress, and the fight at the end of it.
    /// <para>
    /// Stepped on the clock rather than all at once, because the whole point is that it
    /// takes time. A walk whose player has gone — through a door, or off the end of a
    /// connection — is abandoned rather than followed.
    /// </para>
    /// </summary>
    private List<Outgoing> StepApproaches(string mapId, MapPopulation people, double nowSeconds)
    {
        var send = new List<Outgoing>();

        foreach (ServerObject person in people.Objects)
        {
            if (person.Approaching is not { } playerId) continue;

            if (!_players.TryGetValue(playerId, out ServerPlayer? player) || player.MapId != mapId)
            {
                person.Approaching = null;
                person.Approach.Clear();

                // The player may still be connected and simply somewhere else, in which
                // case they are standing still on another map waiting for somebody who
                // is not following them.
                if (_players.ContainsKey(playerId))
                {
                    _players[playerId].WatchedBy = null;
                    send.Add(new Outgoing(new ApproachEnded(), OnlyTo: playerId));
                }

                continue;
            }

            if (nowSeconds < person.NextApproachAt) continue;

            person.NextApproachAt = nowSeconds + ApproachStepSeconds;

            if (person.Approach.Count > 0)
            {
                GridPosition next = person.Approach.Dequeue();

                person.Facing = Toward(person.Square, next);
                person.Square = next;

                send.Add(new Outgoing(
                    new ObjectMoved(person.LocalId, next.X, next.Y, person.Facing),
                    OnMap: mapId));

                continue;
            }

            // Arrived, but not yet fighting. Turning to face somebody and then pausing
            // is the difference between being challenged and having a battle screen
            // appear at you.
            if (!person.Arrived)
            {
                person.Arrived = true;
                person.NextApproachAt = nowSeconds + ArrivalPause;

                Direction turn = Toward(person.Square, player.Square);

                if (person.Facing != turn)
                {
                    person.Facing = turn;

                    send.Add(new Outgoing(
                        new ObjectMoved(person.LocalId, person.Square.X, person.Square.Y, person.Facing),
                        OnMap: mapId));
                }

                continue;
            }

            Direction looking = Toward(person.Square, player.Square);

            person.Approaching = null;
            player.WatchedBy = null;

            // Only if it is news. Somebody who walked straight down a route is already
            // facing the player they walked at, and the same rule that keeps a repeated
            // turn off the wire for a player keeps it off for them.
            if (person.Facing != looking)
            {
                person.Facing = looking;

                send.Add(new Outgoing(
                    new ObjectMoved(person.LocalId, person.Square.X, person.Square.Y, person.Facing),
                    OnMap: mapId));
            }

            // A fight that cannot start — no rules file, no healthy lead, a party
            // already wiped — must still end the walk, and must say so. Otherwise the
            // player stands still waiting for something that is not coming.
            List<Outgoing> fight = StartTrainerBattle(player, person.Template with
            {
                X = person.Square.X,
                Y = person.Square.Y,
                Facing = person.Facing,
            });

            send.AddRange(fight);

            if (fight.Count == 0) send.Add(new Outgoing(new ApproachEnded(), OnlyTo: player.Id));
        }

        return send;
    }

    /// <summary>
    /// How long somebody stands there having noticed you before they set off.
    /// <para>
    /// The three timings below are this project's, not the cartridge's, and they are
    /// stated rather than tuned into the code. Without them the whole encounter — the
    /// notice, the walk, the fight — lands inside a second, which is too fast to read as
    /// anything at all.
    /// </para>
    /// </summary>
    private const double NoticePause = 0.9;

    /// <summary>Seconds a square of the walk takes. Slower than a player's own step.</summary>
    private const double ApproachStepSeconds = 0.28;

    /// <summary>How long they stand in front of you before the fight begins.</summary>
    private const double ArrivalPause = 0.5;

    /// <summary>Which way one square is from another, for a step of exactly one.</summary>
    private static Direction Toward(GridPosition from, GridPosition to) =>
        to.Y < from.Y ? Direction.Up
        : to.Y > from.Y ? Direction.Down
        : to.X < from.X ? Direction.Left
        : Direction.Right;

    /// <summary>
    /// Whoever on this map has just spotted the player, if anybody has.
    /// <para>
    /// A straight line in the direction they are facing, out to their range, with
    /// nothing solid and nobody standing in between. The line is <see cref="MapObject"/>'s
    /// job; the part about what is in the way needs the map, which is here.
    /// </para>
    /// </summary>
    private MapObject? WhoSpotted(ServerPlayer player)
    {
        LastSightRefusal = null;

        if (_world.Find(player.MapId) is not { } map) return null;

        _populated.TryGetValue(player.MapId, out MapPopulation? people);

        foreach (MapObject template in map.Objects)
        {
            if (!template.IsTrainer) continue;

            // Where they are and which way they are looking both have to come from the
            // living world. Taking the square from one source and the direction from
            // the other gives a line neither of them is looking along.
            MapObject trainer = people?.ById(template.LocalId) is { } live
                ? template with { X = live.Square.X, Y = live.Square.Y, Facing = live.Facing }
                : template;

            // Beyond here, everything is a reason this particular person did not start
            // a fight — and only the ones a player standing nearby would find puzzling
            // are written down. A refusal for every trainer on the map would bury the
            // one that matters.
            bool nearby = Nearby(trainer.Square, player.Square);

            if (_battles is null)
            {
                if (nearby) LastSightRefusal = "there is no rules file, so nobody has a party to fight with";
                continue;
            }

            if (trainer.TrainerId == 0)
            {
                // Their script could not be read as far as the fight. Said out loud
                // because from the outside it looks exactly like a broken sight line.
                if (nearby) LastSightRefusal = $"object {trainer.LocalId} is a trainer but names no id";
                continue;
            }

            if (player.DefeatedTrainers.Contains(trainer.TrainerId))
            {
                if (nearby) LastSightRefusal = $"trainer {trainer.TrainerId} has already been beaten";
                continue;
            }

            if (trainer.SightRange == 0)
            {
                if (nearby) LastSightRefusal ??= $"trainer {trainer.TrainerId} has no line of sight — talk to them instead";
                continue;
            }

            if (!trainer.CanSee(player.Square)) continue;

            if (trainer.ApproachTo(player.Square).Any(square => !IsFree(player.MapId, square)))
            {
                LastSightRefusal = $"trainer {trainer.TrainerId} has something in the way";
                continue;
            }

            return trainer;
        }

        return null;
    }

    /// <summary>
    /// Close enough that a player would expect something to happen.
    /// <para>
    /// Only used to decide whether a refusal is worth writing down. Being generous here
    /// costs a log line; being mean costs the one line that would have explained a
    /// puzzling walk past somebody.
    /// </para>
    /// </summary>
    private static bool Nearby(GridPosition a, GridPosition b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) <= 4;

    /// <summary>
    /// Why nobody challenged a player who has just stepped somewhere.
    /// <para>
    /// Only set when somebody could see them and it came to nothing anyway. Walking past
    /// an ordinary person, and walking past a trainer who has already been beaten, look
    /// identical from the player's side — and the commonest answer of all is that the map
    /// simply has nobody on it who wants a fight, which is what the startup report is for.
    /// </para>
    /// </summary>
    public string? LastSightRefusal { get; private set; }

    /// <summary>
    /// Opens a shop, if the person being spoken to keeps one.
    /// <para>
    /// The prices come out of the rules file rather than the shop. A cartridge shop is
    /// a list of ids and nothing else — what each one costs is a property of the item,
    /// which is why a Poké Ball is the same price in every town.
    /// </para>
    /// </summary>
    private List<Outgoing> OpenShop(ServerPlayer player, MapObject keeper)
    {
        if (_rules is null || !keeper.IsShopkeeper) return [];

        List<ShopEntry> stock = keeper.Stock
            .Select(id => (Id: id, Item: _rules.ItemAt(id)))
            .Where(entry => entry.Item is not null)
            .Select(entry => new ShopEntry(entry.Id, entry.Item!.Price))
            .ToList();

        if (stock.Count == 0) return [];

        player.Shopping = [.. keeper.Stock];

        return [new Outgoing(new ShopOpened(stock, player.Money, player.Bag.Entries), OnlyTo: player.Id)];
    }

    /// <summary>
    /// Buys some of one thing, and says what the money and the bag are afterwards.
    /// <para>
    /// Everything is checked here and nothing is taken on trust: that a shop is open,
    /// that it stocks this, what it costs, that the money is there, and that the bag has
    /// room. A client sends an id and a count and has no say in any of the rest.
    /// </para>
    /// </summary>
    public List<Outgoing> Buy(int playerId, int itemId, int count)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];
            if (_rules is null) return [];

            if (!player.Shopping.Contains(itemId)) return [Told(player, "They don't sell that.")];
            if (_rules.ItemAt(itemId) is not { } item) return [Told(player, "They don't sell that.")];

            int wanted = Math.Clamp(count, 1, Bag.MaxStack);

            // Priced before anything is taken, and capped by what can be afforded rather
            // than refused outright — a player asking for ten with money for four gets
            // four, which is what a shop does.
            int affordable = item.Price > 0 ? Math.Min(wanted, player.Money / item.Price) : 0;

            if (affordable <= 0) return [Told(player, "You don't have enough money.")];

            // Added first, because the bag is what might refuse. Charging for items that
            // never went in is the one failure here that costs a player something.
            int taken = player.Bag.Add(itemId, affordable);

            if (taken <= 0) return [Told(player, "You can't carry any more.")];

            player.Money -= taken * item.Price;

            return [Told(player, $"Bought {taken}.")];
        }
    }

    /// <summary>Sells some of one thing, at half price and never a key item.</summary>
    public List<Outgoing> Sell(int playerId, int itemId, int count)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];
            if (_rules is null || player.Shopping.Count == 0) return [];

            if (_rules.ItemAt(itemId) is not { } item) return [Told(player, "They don't want that.")];
            if (item.SellPrice <= 0) return [Told(player, "They won't take that.")];

            int sold = player.Bag.Remove(itemId, Math.Max(1, count));

            if (sold <= 0) return [Told(player, "You don't have any.")];

            player.Money = Math.Min(MaxMoney, player.Money + sold * item.SellPrice);

            return [Told(player, $"Sold {sold} for {sold * item.SellPrice}.")];
        }
    }

    private Outgoing Told(ServerPlayer player, string message) =>
        new(new ShopUpdated(player.Money, player.Bag.Entries, message), OnlyTo: player.Id);

    /// <summary>
    /// Records what a script the player just ran did to their save.
    /// <para>
    /// Taken on trust, and worth saying why rather than leaving it to be discovered.
    /// Only the client can run a script — the bytes are on a cartridge and this side has
    /// never seen one — so either the flags live here and arrive from there, or they
    /// live on the client and stop being a save at all. The two things worth guarding,
    /// money and what is in the party, are decided here and are not in this message.
    /// </para>
    /// </summary>
    public void RunScript(int playerId, ScriptRan ran)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return;

            foreach (int flag in ran.Set) player.Script.Set(flag);
            foreach (int flag in ran.Cleared) player.Script.Clear(flag);
            foreach (SavedVariable variable in ran.Written) player.Script.Write(variable.Id, variable.Value);
        }
    }

    /// <summary>
    /// Uses something out of the bag on a party member, outside a fight.
    /// <para>
    /// The same rules the battle screen has followed since potions worked there — that
    /// it is a thing, that it restores anything, that it is actually carried — and one
    /// more that only applies out here: it has to do something. Spending a Full Restore
    /// on somebody at full health is not a refusal in the games either, but it is worth
    /// not charging for, because out of a fight there is no turn being used up and
    /// nothing else to lose.
    /// </para>
    /// </summary>
    public List<Outgoing> UseItem(int playerId, int itemId, int slot)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];

            if (player.InBattle)
                return [new Outgoing(new Rejected("Not in the middle of a battle."), OnlyTo: playerId)];

            if (slot < 0 || slot >= player.Party.Count) return [];
            if (player.Bag.CountOf(itemId) == 0) return [];

            if (_rules?.ItemAt(itemId) is { CanTeach: true } machine)
                return Teach(player, slot, machine);

            if (_battles is null || _rules?.ItemAt(itemId) is not { Restores: not null } medicine) return [];

            (SavedMon healed, int restored) = _battles.Restored(player.Party[slot], medicine);

            if (restored <= 0)
            {
                return [new Outgoing(
                    new BagUpdated(player.Bag.Entries, [.. player.Party], "It won't have any effect."),
                    OnlyTo: playerId)];
            }

            player.Bag.Remove(itemId);
            player.Party[slot] = healed;

            return [new Outgoing(
                new BagUpdated(player.Bag.Entries, [.. player.Party], $"Restored {restored} health."),
                OnlyTo: playerId)];
        }
    }

    /// <summary>
    /// How many moves one of them can hold. Four, everywhere, forever.
    /// </summary>
    private const int MoveSlots = 4;

    /// <summary>
    /// Teaches a party member what a machine teaches.
    /// <para>
    /// Which move that is came off the cartridge and is in the rules file; whether this
    /// particular species is allowed to learn it did not. The games keep a compatibility
    /// bitfield per species and this project has not located it, so for now anybody can
    /// learn anything. That is wrong in a way worth writing down rather than hiding: a
    /// PIDGEY that knows STRENGTH is not what the cartridge says.
    /// </para>
    /// <para>
    /// A full set of four is refused rather than overwritten. Choosing which move to
    /// lose is a decision belonging to the player, and there is nowhere yet for them to
    /// make it — silently discarding the first one would be the server making it for
    /// them.
    /// </para>
    /// </summary>
    private List<Outgoing> Teach(ServerPlayer player, int slot, ItemData machine)
    {
        SavedMon member = player.Party[slot];

        if (member.Moves.Contains(machine.Teaches))
        {
            return [new Outgoing(
                new BagUpdated(player.Bag.Entries, [.. player.Party], "It already knows that move."),
                OnlyTo: player.Id)];
        }

        if (member.Moves.Count >= MoveSlots)
        {
            return [new Outgoing(
                new BagUpdated(
                    player.Bag.Entries,
                    [.. player.Party],
                    "It already knows four moves, and there is no way to forget one yet."),
                OnlyTo: player.Id)];
        }

        player.Party[slot] = member with { Moves = [.. member.Moves, machine.Teaches] };

        // The cartridge draws this line itself: the fifty TMs have a price and no
        // importance, and the eight HMs have importance and no price. The reusable ones
        // are exactly the ones already marked too important to sell.
        if (!machine.IsReusableMachine) player.Bag.Remove(machine.Id);

        return [new Outgoing(
            new BagUpdated(player.Bag.Entries, [.. player.Party], "Learned a new move."),
            OnlyTo: player.Id)];
    }

    /// <summary>
    /// Lets go of anybody walking towards a player who is no longer there to be walked
    /// towards. Called wherever a player leaves a map, for the same reason a hold is.
    /// </summary>
    private void AbandonApproaches(int playerId)
    {
        foreach (MapPopulation people in _populated.Values)
        {
            foreach (ServerObject person in people.Objects)
            {
                if (person.Approaching != playerId) continue;

                person.Approaching = null;
                person.Approach.Clear();
            }
        }
    }

    /// <summary>What the last scene placement came to. Same arrangement as the rest.</summary>
    public string? LastScenePlacement { get; private set; }

    /// <summary>
    /// Accepts where a scene left somebody, if it is a place they could be.
    /// <para>
    /// The client plays the scene because the movements are on a cartridge this has never
    /// seen, so the two sides end a scene disagreeing about where its cast is standing.
    /// Refusing to be told means every scene in the game snaps its people back the moment
    /// it ends, which is worse than the alternative and worse in a way everybody can see.
    /// </para>
    /// <para>
    /// Trusted narrowly: only somebody already held still for this player, only on the
    /// map they are both on, and only onto a square that is walkable and empty. What that
    /// buys a determined client is shuffling an NPC they are already standing in front of
    /// onto a square a person could stand on, which is not worth defending against at the
    /// cost of every cutscene in the game.
    /// </para>
    /// </summary>
    public List<Outgoing> PlaceAfterScene(int playerId, int localId, GridPosition square, Direction facing)
    {
        lock (_gate)
        {
            LastScenePlacement = null;

            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];
            if (!_populated.TryGetValue(player.MapId, out MapPopulation? people)) return [];

            if (people.ById(localId) is not { } person)
            {
                LastScenePlacement = $"no object {localId} on {player.MapId}";
                return [];
            }

            if (person.HeldBy != playerId)
            {
                LastScenePlacement = $"object {localId} is not being held by them";
                return [];
            }

            if (!GridFor(player.MapId).IsWalkable(square))
            {
                LastScenePlacement = $"{square} is not somewhere anybody can stand";
                return [];
            }

            if (people.At(square) is { } standing && standing.LocalId != localId)
            {
                LastScenePlacement = $"{square} already has object {standing.LocalId} on it";
                return [];
            }

            if (person.Square == square && person.Facing == facing)
            {
                LastScenePlacement = $"object {localId} is already at {square}";
                return [];
            }

            person.Square = square;
            person.Facing = facing;

            LastScenePlacement = $"object {localId} left at {square} facing {facing}";

            return [new Outgoing(
                new ObjectMoved(localId, square.X, square.Y, facing),
                OnMap: player.MapId)];
        }
    }

    /// <summary>The text box is closed. Whoever this player was holding carries on.</summary>
    public void StopTalking(int playerId)
    {
        lock (_gate)
        {
            if (_players.TryGetValue(playerId, out ServerPlayer? shopper)) shopper.Shopping = [];

            foreach (MapPopulation people in _populated.Values)
                people.Release(holder => holder == playerId);
        }
    }

    /// <summary>
    /// What the last attempt to talk to somebody came to.
    /// <para>
    /// Talking has four possible outcomes and three of them look identical from the
    /// player's side: a fight, a shop, somebody held still to be spoken to, and nothing
    /// at all. Only the server knows which it was, so it writes it down — the same
    /// arrangement the map edges and the sight lines already use.
    /// </para>
    /// </summary>
    public string? LastTalkOutcome { get; private set; }

    /// <summary>What the last square somebody stood on came to. Same arrangement.</summary>
    public string? LastTriggerOutcome { get; private set; }

    /// <summary>
    /// A square was stepped onto that runs a script.
    /// <para>
    /// The server cannot run one and never will — the bytes are on an image it has never
    /// seen — so almost all of this is the client's work, and almost all of this method
    /// is refusing. What it does own is the fight: nineteen of the two hundred and
    /// twenty-eight of these squares field a trainer, and a rival waiting on a route has
    /// to be a fight the server runs rather than one the client announces.
    /// </para>
    /// <para>
    /// Both checks matter and neither is redundant with the client's. The player has to
    /// actually be standing there, and the trigger's own condition has to still hold —
    /// otherwise "I stepped on the rival's square again" is a fight that can be had
    /// forever, and the variable that was supposed to spend it counts for nothing.
    /// </para>
    /// </summary>
    public List<Outgoing> FireTrigger(int playerId, int x, int y)
    {
        lock (_gate)
        {
            LastTriggerOutcome = null;

            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];

            var square = new GridPosition(x, y);

            if (player.Square != square)
            {
                LastTriggerOutcome = $"refused: they are at {player.Square}, not {square}";
                return [];
            }

            if (_world.Find(player.MapId)?.TriggerAt(square) is not { } trigger)
            {
                LastTriggerOutcome = "refused: nothing on that square runs anything";
                return [];
            }

            if (!trigger.Armed(player.Script.Read(trigger.Variable)))
            {
                LastTriggerOutcome =
                    $"refused: variable 0x{trigger.Variable:X4} holds " +
                    $"{player.Script.Read(trigger.Variable)}, and this wants {trigger.Value}";

                return [];
            }

            if (!trigger.CanBeFought)
            {
                LastTriggerOutcome = "a script the client runs; nothing here to arbitrate";
                return [];
            }

            // Built as a person for a moment, because a fight is a fight and the one
            // routine that starts them has been right about parties, beaten trainers and
            // healthy leads since trainers existed.
            var asPerson = new MapObject(
                0, 0, x, y, player.Facing, 0, IsTrainer: true, TrainerId: trigger.TrainerId);

            if (StartTrainerBattle(player, asPerson) is not { Count: > 0 } challenge)
            {
                LastTriggerOutcome = $"trainer {trigger.TrainerId} is not one this server can field right now";
                return [];
            }

            LastTriggerOutcome = $"a fight with trainer {trigger.TrainerId}";

            return challenge;
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
    private void AfterArrival(ServerPlayer player, List<Outgoing> send, double nowSeconds)
    {
        if (_world.Find(player.MapId)?.WarpAt(player.Square) is { } warp)
        {
            send.AddRange(TakeWarp(player, warp));
            return;
        }

        if (WhoSpotted(player) is { } watcher &&
            BeginApproach(player, watcher, nowSeconds) is { Count: > 0 } noticed)
        {
            send.AddRange(noticed);

            // A fight and something in the grass on the same square would be two battles
            // at once. The person who saw you gets to go first, and they get to walk.
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

        // Whoever was walking over is walking over to an empty square now. Saying so
        // matters: the player is standing still because somebody was coming, and
        // nothing else is going to tell them that stopped being true.
        AbandonApproaches(player.Id);

        if (player.WatchedBy is not null)
        {
            player.WatchedBy = null;
            send.Add(new Outgoing(new ApproachEnded(), OnlyTo: player.Id));
        }

        // The trees grow back. Held per map rather than per world, so walking out of a
        // cave and back in puts the boulders where the cartridge left them — which is
        // both what the games do and what stops this set growing without limit.
        player.Shifted.Clear();

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

        player.Battle = new Encounter(lead.Slot, lead.Battler, [wild], _rng.State);

        send.Add(new Outgoing(
            new BattleStarted(BattleFactory.View(lead.Battler), BattleFactory.View(wild), BallsOf(player), MedicineOf(player)),
            OnlyTo: player.Id));
    }

    /// <summary>
    /// The first party member still standing, rebuilt from what was saved, and which
    /// slot it came out of.
    /// <para>
    /// The slot is what identifies it from here on. Matching on species was fine while
    /// exactly one creature ever fought; a party with two of the same species in it
    /// would have written one's health onto the other.
    /// </para>
    /// </summary>
    private (int Slot, Battler Battler)? LeadBattler(ServerPlayer player, int after = -1)
    {
        if (_battles is null) return null;

        for (int slot = after + 1; slot < player.Party.Count; slot++)
        {
            if (_battles.Restore(player.Party[slot]) is { } battler && !battler.HasFainted)
                return (slot, battler);
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

    /// <summary>
    /// What a share of the money is worth on the way down.
    /// <para>
    /// <b>This project's rule, not the cartridge's.</b> The games work it out from the
    /// level of the strongest thing in the party against a small table, which this does
    /// not read. Half is stated plainly instead, rather than inventing a formula and
    /// implying it came off an image.
    /// </para>
    /// </summary>
    public const int LossShare = 2;

    /// <summary>
    /// Waking up after losing: back at the last centre, lighter.
    /// <para>
    /// This replaces healing on the spot, which was a stand-in from before there was
    /// anywhere to wake up. It mattered more than it looked: a loss that costs nothing
    /// makes a centre a place with no reason to exist, and every potion in the bag a
    /// souvenir.
    /// </para>
    /// <para>
    /// A character who has never rested anywhere goes to the starting map. That is not a
    /// fallback for an error — it is where they started, and it is the only place the
    /// server knows is safe.
    /// </para>
    /// </summary>
    private List<Outgoing> BlackOut(ServerPlayer player)
    {
        player.Money /= LossShare;

        string mapId = player.RestingAt is { } rested && _world.Find(rested) is not null
            ? rested
            : StartingMap.Id;

        GridPosition square = player.RestingAt is null
            ? GridFor(mapId).FirstWalkable()
            : player.RestingSquare;

        // A centre that has been re-exported may have moved a counter, and a square that
        // is solid now would wake somebody inside a wall with no way out.
        if (!GridFor(mapId).IsWalkable(square)) square = GridFor(mapId).FirstWalkable();

        List<Outgoing> send = Transfer(player, mapId, square, player.Facing);

        send.Add(new Outgoing(new BlackedOut(mapId, square.X, square.Y, player.Money, [.. player.Party]), OnlyTo: player.Id));

        return send;
    }

    /// <summary>The most money an account can hold, which is the games' own ceiling.</summary>
    public const int MaxMoney = 999_999;

    /// <summary>
    /// What beating a trainer pays.
    /// <para>
    /// <b>This project's rule, not the cartridge's.</b> The games multiply a per-class
    /// base by the level of the last creature, and that base lives in a small table this
    /// project does not read. Rather than invent a number and imply it came off an
    /// image, the formula here is stated plainly: a flat rate per level of the strongest
    /// thing they brought.
    /// </para>
    /// </summary>
    public const int PrizePerLevel = 40;

    private static int PrizeFor(Encounter encounter) =>
        PrizePerLevel * encounter.Opponents.Max(o => o.Level);

    /// <summary>The ball pocket, which is all a battle screen needs of a bag.</summary>
    private List<BagEntry> BallsOf(ServerPlayer player) =>
        _rules is null ? [] : player.Bag.InPocket(_rules, Pocket.Balls);

    /// <summary>What in the bag would put health back on somebody.</summary>
    private List<BagEntry> MedicineOf(ServerPlayer player) =>
        _rules is null
            ? []
            : player.Bag.InPocket(_rules, Pocket.Items)
                .Where(e => _rules.ItemAt(e.ItemId)?.Restores is not null)
                .ToList();

    /// <summary>
    /// Turns a request into what will actually happen, and spends whatever it costs.
    /// <para>
    /// The only place a ball leaves a bag. A throw that cannot happen becomes an
    /// attack rather than a refusal, because refusing mid-battle leaves a client
    /// waiting for a turn that never comes.
    /// </para>
    /// </summary>
    private BattleAction Resolve(ServerPlayer player, Encounter encounter, BattleAction action)
    {
        if (action is BattleAction.UseItem using_)
        {
            // Everything checked here: that it is a thing, that it restores anything,
            // that it is actually carried. A client sends an id and nothing else.
            if (_rules?.ItemAt(using_.ItemId) is not { Restores: not null } medicine)
                return new BattleAction.UseMove(0);

            if (player.Bag.Remove(using_.ItemId) == 0) return new BattleAction.UseMove(0);

            return using_ with { Restores = medicine.RestoreFor(encounter.Player.MaxHp) };
        }

        if (action is not BattleAction.ThrowBall throwing) return action;

        if (encounter.IsTrainerBattle) return new BattleAction.UseMove(0);
        if (_rules?.ItemAt(throwing.ItemId) is not { Ball: { } kind }) return new BattleAction.UseMove(0);
        if (player.Bag.Remove(throwing.ItemId) == 0) return new BattleAction.UseMove(0);

        return throwing with { Kind = kind };
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
            if (!_players.TryGetValue(playerId, out ServerPlayer? player) || player.Battle is not { } encounter)
                return [new Outgoing(new Rejected("You are not in a battle."), OnlyTo: playerId)];

            Battle battle = encounter.Current;

            // Three ways a throw turns into an attack instead: nothing of that kind in
            // the bag, an item that is not a ball at all, and somebody else's creature.
            // The client hides the option for each of them, and this is what makes it
            // true — a hidden option is a courtesy, not a rule.
            action = Resolve(player, encounter, action);

            List<BattleEvent> events = battle.ResolveTurn(action, new BattleAction.UseMove(0));

            // Slotted in ahead of the closing event rather than appended, because the
            // games pay out between "it fainted" and the end of the battle. Appending
            // put it after "You won the battle!", which reads backwards and is the
            // easiest line in a battle to press past without reading.
            if (battle.Opponent.HasFainted && !battle.OpponentCaught)
            {
                List<BattleEvent> payout = AwardExperience(player, encounter);

                int ended = events.FindIndex(e => e is BattleEvent.Ended);
                events.InsertRange(ended < 0 ? events.Count : ended, payout);
            }

            // Whoever is out has to be written back before anything replaces them, or
            // a fight of six creatures records only what happened to the last one.
            WriteBackActive(player, encounter);

            bool finished = Conclude(encounter, player, out Side? winner);

            // The engine says a battle ended because somebody fainted. When there is
            // another one to send out, that was the end of a battle and not of the
            // fight — so the word is taken back out rather than shown to a player who
            // is about to be handed a fresh opponent.
            if (!finished) events.RemoveAll(e => e is BattleEvent.Ended);

            var send = new List<Outgoing>
            {
                new(
                    new BattleUpdate(events, battle.Player.CurrentHp, battle.Opponent.CurrentHp, BallsOf(player), MedicineOf(player)),
                    OnlyTo: playerId),
            };

            if (finished)
            {
                send.Add(new Outgoing(FinishBattle(player, encounter, winner), OnlyTo: playerId));

                // Losing costs the walk back, and now the walk back is somewhere. The
                // transfer comes after the result so the client has finished the battle
                // before the world moves underneath it.
                if (winner == Side.Opponent) send.AddRange(BlackOut(player));

                // Beating somebody is decided here and nowhere else. Which line they
                // read next is decided by running their script, and the thing that
                // script asks is whether this fight has already happened — so a win the
                // client is not told about is a trainer who goes on greeting the player
                // who beat them.
                if (winner == Side.Player && encounter.TrainerId is { } won)
                    send.Add(new Outgoing(new TrainerBeaten(won), OnlyTo: playerId));
                return send;
            }

            if (battle.Opponent.HasFainted)
            {
                Battler next = encounter.SendNextOpponent();
                send.Add(new Outgoing(new BattlerSentOut(Side.Opponent, BattleFactory.View(next)), OnlyTo: playerId));
            }

            if (battle.Player.HasFainted && LeadBattler(player, encounter.PlayerSlot) is { } replacement)
            {
                encounter.SendPlayer(replacement.Slot, replacement.Battler);

                send.Add(new Outgoing(
                    new BattlerSentOut(Side.Player, BattleFactory.View(replacement.Battler)), OnlyTo: playerId));
            }

            return send;
        }
    }

    /// <summary>
    /// Whether the fight as a whole is over, and who won it.
    /// <para>
    /// Distinct from the battle being over, which happens every time anybody faints.
    /// A trainer with three creatures ends three battles and one fight.
    /// </para>
    /// </summary>
    private bool Conclude(Encounter encounter, ServerPlayer player, out Side? winner)
    {
        Battle battle = encounter.Current;

        winner = null;

        if (battle.OpponentCaught)
        {
            winner = Side.Player;
            return true;
        }

        if (encounter.OpponentIsBeaten)
        {
            winner = Side.Player;
            return true;
        }

        if (battle.Player.HasFainted && LeadBattler(player, encounter.PlayerSlot) is null)
        {
            winner = Side.Opponent;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Copies whoever is out back into the party, health and status only.
    /// <para>
    /// The level, moves and experience have already been written by the payout, and
    /// rebuilding from the battler would undo them — that battler was built before the
    /// battle and never grew. This cost a level once already.
    /// </para>
    /// </summary>
    private static void WriteBackActive(ServerPlayer player, Encounter encounter)
    {
        if (encounter.PlayerSlot < 0 || encounter.PlayerSlot >= player.Party.Count) return;

        player.Party[encounter.PlayerSlot] = player.Party[encounter.PlayerSlot] with
        {
            CurrentHp = encounter.Player.CurrentHp,
            Status = encounter.Player.Status,
        };
    }

    /// <summary>
    /// Pays out for a knockout, and writes the result straight into the party.
    /// <para>
    /// Only the battler that fought is paid. Sharing it out across a party is a later
    /// problem, and one that needs a rule about who counts as having taken part.
    /// </para>
    /// </summary>
    private List<BattleEvent> AwardExperience(ServerPlayer player, Encounter encounter)
    {
        if (_progression is null) return [];

        int slot = encounter.PlayerSlot;
        if (slot < 0 || slot >= player.Party.Count) return [];

        (SavedMon grown, List<BattleEvent> events) = _progression.Award(
            player.Party[slot], encounter.Opponent.Species.Index, encounter.Opponent.Level);

        player.Party[slot] = grown;

        return events;
    }

    /// <summary>
    /// Closes a fight and writes its consequences into the party.
    /// <para>
    /// Anything caught joins the party, a beaten trainer is remembered so they do not
    /// start again on the walk back, and a wiped party is put on its feet.
    /// </para>
    /// </summary>
    private BattleFinished FinishBattle(ServerPlayer player, Encounter encounter, Side? winner)
    {
        bool caught = encounter.Current.OpponentCaught;

        if (caught && player.Party.Count < Party.MaxSize)
            player.Party.Add(BattleFactory.Save(encounter.Opponent));

        if (winner == Side.Player && encounter.TrainerId is { } beaten)
            player.DefeatedTrainers.Add(beaten);

        player.Battle = null;

        // A wiped party can never start another battle, so waking up healthy is the one
        // state that is always recoverable. Where that happens is BlackOut's business.
        if (winner == Side.Opponent) HealParty(player);

        int prize = winner == Side.Player && encounter.IsTrainerBattle ? PrizeFor(encounter) : 0;

        player.Money = Math.Min(MaxMoney, player.Money + prize);

        return new BattleFinished(winner, caught, player.Money, prize, BallsOf(player), [.. player.Party]);
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
                player.MapId, player.Square.X, player.Square.Y, player.Facing, [.. player.Party])
            {
                DefeatedTrainers = [.. player.DefeatedTrainers],
                Items = player.Bag.Entries,
                Money = player.Money,
                Flags = [.. player.Script.Flags],
                Variables = [.. player.Script.Variables.Select(v => new SavedVariable(v.Key, v.Value))],
                ItemsTaken = [.. player.ItemsTaken],
                RestingAt = player.RestingAt,
                RestingX = player.RestingSquare.X,
                RestingY = player.RestingSquare.Y,
            };
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
    public bool UpdateSave(int playerId, IReadOnlyList<SavedMon> party)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return false;

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
