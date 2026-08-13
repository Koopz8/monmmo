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
    /// True when this player is on the water rather than on the land.
    /// <para>
    /// Held here rather than worked out from the square they are on, because the two
    /// are not the same question. Standing on a water square is what surfing looks
    /// like; being allowed onto one is what it is.
    /// </para>
    /// </summary>
    public bool Surfing { get; set; }

    /// <summary>
    /// Who is waiting to fight this player as soon as they stop reading.
    /// <para>
    /// A trainer who has to be talked to has words first, and often a whole scene of
    /// them. Kept here rather than found again when the box closes, because by then the
    /// only thing that says who was being spoken to is this.
    /// </para>
    /// </summary>
    public int FightingWhenDone { get; set; }

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
    /// Moves a level-up offered and could not fit, and who to.
    /// <para>
    /// The question the games ask and this project has always shrugged at: four are
    /// known, a fifth is learned, and something has to go. It is kept here rather than
    /// answered on the spot because the answer is a person's, and a person is reading a
    /// battle screen — the fight may be over by the time they get to it.
    /// </para>
    /// <para>
    /// It is also what makes the answer safe: a client cannot teach anybody anything, it
    /// can only reply to something already on this list.
    /// </para>
    /// </summary>
    /// <summary>
    /// Moves this player has been offered and not yet answered about.
    /// <para>
    /// The item is carried alongside because a machine is spent by being used and a
    /// level-up is not. A TM that vanished when the question was asked would be a TM
    /// lost by declining it.
    /// </para>
    /// </summary>
    public List<(int Slot, int MoveId, int FromItem)> MovesOffered { get; } = [];

    /// <summary>
    /// How long a scene may go on walking this player about.
    /// <para>
    /// Ordinary movement is rate limited and a scripted walk cannot be — the games step
    /// somebody eight squares in a row faster than anybody could ask for it. This is what
    /// replaces the limit: a window opened by a trigger the server itself agreed to fire,
    /// so a client cannot send a walk out of nowhere and cannot keep sending them.
    /// </para>
    /// </summary>
    public double SceneUntil { get; set; }

    /// <summary>
    /// The map the running scene belongs to.
    /// <para>
    /// A scene can now put the player through a door — that is how the professor gets
    /// anybody into his lab — which means the messages behind a scene can arrive after
    /// the player has left the map the scene was about. Object 3 in Pallet Town and
    /// object 3 in the lab are different people, and a placement that only checked the
    /// window would move the second one to where the first one was told to stand.
    /// </para>
    /// </summary>
    public string SceneOn { get; set; } = "";

    /// <summary>Who this player has been told is on their map, so a change can be sent.</summary>
    public HashSet<int> Seeing { get; } = [];

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
    /// How many a party holds. Six, and it is the cartridge's number rather than this
    /// project's — with no storage boxes yet, a seventh has nowhere to go at all.
    /// </summary>
    public const int MaxPartySize = 6;

    /// <summary>
    /// Shortest interval between a player's steps, defined in <see cref="WalkingCharacter"/>
    /// so the client can obey the same one rather than a second copy of it.
    /// </summary>
    public static double MinimumStepInterval => WalkingCharacter.MinimumStepSeconds;

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


    /// <summary>
    /// Who is allowed to run the console, by name.
    /// <para>
    /// Named on the server's own command line and nowhere else. A console is a way to
    /// write anything into anybody's save, so the question of who may use one is not a
    /// question a client gets to answer, and it is not a mode the server can be left in
    /// by accident — an empty set is the default and it refuses everybody.
    /// </para>
    /// </summary>
    public HashSet<string> Operators { get; } = new(StringComparer.OrdinalIgnoreCase);

    public GameWorld(
        WorldData world,
        string startingMapId,
        GameRules? rules = null,
        uint encounterSeed = 1,
        GridPosition? startingSquare = null)
    {
        _startingSquare = startingSquare;

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

    /// <summary>
    /// The square on it, when somebody has said which.
    /// <para>
    /// Without one, the first walkable square of the map is used — which is fine for a
    /// route and arbitrary for a bedroom, where there is a bed and a television and a
    /// place somebody would actually be standing.
    /// </para>
    /// </summary>
    private readonly GridPosition? _startingSquare;

    /// <summary>Where a brand new character will be put, for the startup report.</summary>
    public GridPosition StartingSquare =>
        _startingSquare is { } chosen && GridFor(StartingMap.Id).IsWalkable(chosen)
            ? chosen
            : FindSpawn(StartingMap.Id);

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

    private CollisionGrid GridFor(string mapId) => GridFor(mapId, surfing: false);

    /// <summary>
    /// A map's walkability, for somebody on the water or off it.
    /// <para>
    /// Two grids per map rather than a rule at every call site. There are a dozen places
    /// in this file that ask whether a square can be stood on — a step, a warp, a scene,
    /// a spawn — and threading "unless they are surfing" through all of them is how one
    /// of them ends up not asking.
    /// </para>
    /// </summary>
    private CollisionGrid GridFor(string mapId, bool surfing)
    {
        string key = surfing ? $"{mapId}~" : mapId;

        if (_grids.TryGetValue(key, out CollisionGrid? cached)) return cached;

        MapData map = _world.Find(mapId) ?? StartingMap;
        CollisionGrid grid = map.ToGrid(surfing);

        _grids[key] = grid;
        return grid;
    }

    /// <summary>The grid this particular player walks on, which depends on where they are standing.</summary>
    private CollisionGrid GridFor(ServerPlayer player) => GridFor(player.MapId, player.Surfing);

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
            GridPosition spawn = StartingSquare;
            SavedCharacter fresh = SavedCharacter.Fresh(StartingMap.Id, spawn.X, spawn.Y);

            // No party. There used to be one handed out here, from before this game had
            // an opening: a party that was never empty meant the server never had to
            // invent a battler mid-battle, and mid-battle was the only place a party
            // came from. It has an opening now — the professor takes you to his lab and
            // there are three balls on the table — and starting with the thing the first
            // hour of the game is about is worse than starting with nothing.
            //
            // Nothing needs the party to be non-empty. Grass is already declined for
            // somebody with nobody able to fight, and so is being challenged.
            return fresh with { Items = StartingItems() };
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

            var send = new List<Outgoing> { new(WelcomeFor(player), OnlyTo: player.Id) };

            // Tell the newcomer about everyone already on this map, before announcing them.
            foreach (ServerPlayer existing in _players.Values.Where(p => p.MapId == mapId))
                send.Add(new Outgoing(existing.ToAppeared(), OnlyTo: player.Id));

            if (Populate(mapId, 0) is { } people)
                send.Add(new Outgoing(new ObjectsPlaced([.. VisibleTo(player, people)]), OnlyTo: player.Id));

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
            LastStepRefusal = null;

            // Cleared here rather than only where it is set, or it is reported on every
            // step for as long as the player stays on the map — which is exactly what it
            // did, twenty times in one visit to one room.
            LastArrivalScript = null;

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

            CollisionGrid grid = GridFor(player);
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
                // Said out loud, because a refused step is the one thing a player can see
                // and the server could not previously name. It reads as being dragged
                // backwards, and every explanation for that looks the same from outside.
                LastStepRefusal =
                    $"too fast: {nowSeconds - player.LastStepAt:F2}s since the last step, " +
                    $"and the limit is {MinimumStepInterval:F2}s";

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

            // Stepping ashore is how surfing ends. Not announced as a choice, because it
            // is not one — the step was onto land, and there is nowhere to be but on it.
            if (player.Surfing && !IsWater(player.MapId, wanted))
            {
                player.Surfing = false;
                send.Add(new Outgoing(new SurfingChanged(false, wanted.X, wanted.Y), OnlyTo: playerId));
            }

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

        List<Outgoing> send = Transfer(player, target.Id, arrival, player.Facing, nowSeconds);

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
        !(mapId == player.MapId && player.Shifted.Contains(who)) &&
        !(mapId == player.MapId && !player.Seeing.Contains(who) && HiddenOn(mapId, who));

    /// <summary>Whether an object on a map is one of the six hundred that can be hidden.</summary>
    private bool HiddenOn(string mapId, int localId) =>
        (_populated.TryGetValue(mapId, out MapPopulation? people)
            ? people.ById(localId)?.Template
            : _world.Find(mapId)?.Objects.FirstOrDefault(o => o.LocalId == localId))?.HiddenBy != 0;

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
    /// <summary>
    /// Who is on a map, as far as one player is concerned.
    /// <para>
    /// Six hundred objects in this game carry a flag that takes them off the map, and
    /// which flags are set is a fact about a save rather than about a world — so the
    /// population is shared and the view of it is not. Same arrangement a felled tree
    /// already has.
    /// </para>
    /// </summary>
    private static IEnumerable<ObjectView> VisibleTo(ServerPlayer player, MapPopulation people)
    {
        player.Seeing.Clear();

        foreach (ServerObject entry in people.Objects)
        {
            if (!entry.Template.IsHereFor(player.Script.Has)) continue;

            player.Seeing.Add(entry.LocalId);

            yield return entry.ToView();
        }
    }

    /// <summary>
    /// Everybody on this player's map whose visibility disagrees with what they were
    /// last told, and the messages that put it right.
    /// <para>
    /// Called after a script has written its flags, which is the only thing that changes
    /// the answer. A script that hides the professor and one that reveals a rival are the
    /// same event from here — a flag moved and somebody is now on the wrong side of it.
    /// </para>
    /// </summary>
    private List<Outgoing> Reconcile(ServerPlayer player)
    {
        if (!_populated.TryGetValue(player.MapId, out MapPopulation? people)) return [];

        var send = new List<Outgoing>();

        foreach (ServerObject entry in people.Objects)
        {
            if (entry.Template.HiddenBy == 0) continue;

            bool here = entry.Template.IsHereFor(player.Script.Has);

            if (here == player.Seeing.Contains(entry.LocalId)) continue;

            if (here)
            {
                player.Seeing.Add(entry.LocalId);
                send.Add(new Outgoing(new ObjectsPlaced([entry.ToView()]), OnlyTo: player.Id));
            }
            else
            {
                player.Seeing.Remove(entry.LocalId);
                send.Add(new Outgoing(new WentInside(entry.LocalId), OnlyTo: player.Id));
            }
        }

        return send;
    }

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

            LastTickAt = nowSeconds;

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

            // A conversation opens a scene window, for the same reason a trigger does.
            // Scenes do not only start on squares: saying yes to the ball on the
            // professor's table runs straight on into the rival taking his and walking
            // over, and the only thing the server agreed to there was the conversation.
            // It is the same warrant — something this side decided was allowed to happen.
            player.SceneUntil = LastTickAt + SceneSeconds;
            player.SceneOn = player.MapId;

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

            // Somebody who wants a fight still has to be allowed to finish talking.
            //
            // The fight used to start here, and the note above this line used to say
            // that the battle arriving is what closes the text box. It closed rather
            // more than that. The man at the top of NUGGET BRIDGE congratulates you,
            // hands over a NUGGET, offers you a place in TEAM ROCKET and asks four
            // times — and every word of it was skipped, because the first thing the
            // server did on being told he had been spoken to was field his fight.
            //
            // So it waits for the box to close, which the client says out loud already.
            // Nothing is trusted that was not trusted before: whether there is a fight,
            // and with whom, is still read off this map's own record on this side.
            if (person.Template.CanBeFought && !player.DefeatedTrainers.Contains(person.Template.TrainerId))
            {
                person.HeldBy = playerId;
                player.FightingWhenDone = localId;

                LastTalkOutcome =
                    $"a fight with trainer {person.Template.TrainerId}, once they have finished talking";

                return [];
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

            // What they take, before what they give. Oak receiving the parcel is the
            // other half of a delivery, and it only happens to somebody carrying one:
            // the branch his script reaches it on turns on exactly that, so taking it
            // only when it is there is the cartridge's own behaviour and needs nothing
            // from the client.
            if (person.Template.Takes && player.Bag.Has(person.Template.TakesItemId, person.Template.TakesCount))
            {
                player.Bag.Remove(person.Template.TakesItemId, person.Template.TakesCount);

                gift = $"item {person.Template.TakesItemId} handed over to {person.LocalId}";

                // And said out loud, because a bag the client is still drawing the
                // parcel in is a bag that disagrees with the server about what happened
                // in the conversation the player is reading.
                given.Add(new Outgoing(
                    new BagUpdated(player.Bag.Entries, [.. player.Party], ""), OnlyTo: playerId));
            }

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
                // A counter that heals is not healed at here any more — it is asked at.
                // She asks "Would you like me to heal your POKeMON back to perfect
                // health?" and this used to answer for the player, because the yes and
                // the no are inside a standard routine and this project cannot follow
                // one. The words are hers; the box is the client's; the answer comes
                // back as a HealRequest.
                //
                // Where somebody rests is still settled by standing at the counter,
                // whatever they answer. Finding the place is what makes it yours.
                if (person.Template.Heals && player.Party.Count > 0)
                {
                    player.RestingAt = player.MapId;
                    player.RestingSquare = player.Square;
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
                    trainer.TrainerId,
                    lead.Slot),
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
    public List<Outgoing> RunScript(int playerId, ScriptRan ran)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];

            foreach (int flag in ran.Set) player.Script.Set(flag);
            foreach (int flag in ran.Cleared) player.Script.Clear(flag);
            foreach (SavedVariable variable in ran.Written) player.Script.Write(variable.Id, variable.Value);

            return [.. Reconcile(player), .. HandOverMonster(player)];
        }
    }

    /// <summary>What the last attempt to hand over a monster came to.</summary>
    public string? LastGift { get; private set; }

    /// <summary>
    /// Gives the player whatever the person they are talking to hands over.
    /// <para>
    /// Here rather than at the moment the conversation starts, and the difference is the
    /// whole of it: the species is often a variable, and the variable is set by the very
    /// script this message is reporting. Handing over at the start of the conversation
    /// reads the value the <em>last</em> ball wrote — which is how pressing a different
    /// ball produced a second one of the same creature.
    /// </para>
    /// <para>
    /// The party is one of the two things this server keeps for itself, so what is given
    /// still comes from the world file and never from the client. All that has changed is
    /// when the question is asked.
    /// </para>
    /// </summary>
    private List<Outgoing> HandOverMonster(ServerPlayer player)
    {
        LastGift = null;

        if (_rules is null || _battles is null) return [];
        if (!_populated.TryGetValue(player.MapId, out MapPopulation? people)) return [];

        if (people.Objects.FirstOrDefault(o => o.HeldBy == player.Id && o.Template.GivesMon) is not { } person)
            return [];

        MapObject template = person.Template;

        // One of a set, or one of a kind, and the cartridge says which. Of the seven
        // people in the game who hand over a monster, five name a variable rather than a
        // species — and those five are exactly the two places where you choose: three
        // balls on the professor's table, two fighting types in Saffron. The other two,
        // Lapras on Silph's top floor and Eevee in Celadon, name a species outright and
        // are nobody's alternative.
        //
        // So a variable is not only how the species is found, it is the mark of a choice,
        // and taking one closes the rest of the room.
        bool oneOfASet = template.GivesSpecies >= MapObject.FirstVariable;

        string what = oneOfASet ? $"mon:{player.MapId}" : $"mon:{player.MapId}:{person.LocalId}";

        int species = oneOfASet ? player.Script.Read(template.GivesSpecies) : template.GivesSpecies;

        if (player.Party.Count >= MaxPartySize)
        {
            LastGift = "a monster, but there is no room in the party for it";
            return [];
        }

        if (species <= 0)
        {
            LastGift = $"a monster whose species 0x{template.GivesSpecies:X4} has not been chosen yet";
            return [];
        }

        if (!player.ItemsTaken.Add(what))
        {
            LastGift = oneOfASet ? "one of these, and one is all anybody gets" : "a monster already taken";
            return [];
        }

        if (_battles.Wild(species, Math.Max(1, template.GivesLevel)) is not { } handed)
        {
            LastGift = $"species {species}, which is not one this server can field";
            return [];
        }

        player.Party.Add(BattleFactory.Save(handed));

        LastGift = $"species {species} at level {template.GivesLevel}";

        return [new Outgoing(
            new BagUpdated(player.Bag.Entries, [.. player.Party], "Received a new team member!"),
            OnlyTo: player.Id)];
    }

    /// <summary>What the last attempt to name somebody came to.</summary>
    public string? LastNaming { get; private set; }

    /// <summary>
    /// Names one of the party.
    /// <para>
    /// The screen it comes from is the client's own, because the cartridge's keyboard is
    /// code and cannot be read. That makes the name a thing the player typed rather than
    /// a thing the world decided, which is exactly why it is checked here: the slot has
    /// to be one they have, and the text has to be text.
    /// </para>
    /// <para>
    /// In a fight it is refused. A nickname changing mid-turn would rename somebody
    /// half-way through the sentence describing what they just did.
    /// </para>
    /// </summary>
    public List<Outgoing> NameMon(int playerId, int slot, string name)
    {
        lock (_gate)
        {
            LastNaming = null;

            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];

            if (player.InBattle)
            {
                LastNaming = "refused: in the middle of a fight";
                return [];
            }

            if (slot < 0 || slot >= player.Party.Count)
            {
                LastNaming = $"refused: slot {slot} of a party of {player.Party.Count}";
                return [];
            }

            // Letters, digits and single spaces, and no longer than the longest name the
            // cartridge itself offers. A name goes into a save and onto other players'
            // screens, so what arrives here is a suggestion rather than an instruction.
            // Trimmed before it is cut, not after. Cutting first spends the length on
            // whatever whitespace arrived in front of the name — "  AVERYLONGNAME" came
            // out as eight letters rather than ten.
            string clean = new string(name.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim();

            if (clean.Length > MaxNameLength) clean = clean[..MaxNameLength];

            if (clean.Length == 0)
            {
                LastNaming = "refused: nothing in it";
                return [];
            }

            player.Party[slot] = player.Party[slot] with { Nickname = clean };
            LastNaming = $"slot {slot} is called {clean}";

            return [new Outgoing(
                new BagUpdated(player.Bag.Entries, [.. player.Party], $"Called {clean}."),
                OnlyTo: player.Id)];
        }
    }

    /// <summary>
    /// As long as the longest name this cartridge offers to give a character.
    /// <para>
    /// Ten, and it is the client that reads that out of the image — this side has never
    /// seen one. Kept as a number here because a bound the server does not own is not a
    /// bound.
    /// </para>
    /// </summary>
    private const int MaxNameLength = 10;

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
            // Asked rather than refused. This used to say "there is no way to forget one
            // yet", and it went on saying it after there was one — the level-up path
            // learned to ask and the machine path never did, so the question existed for
            // moves nobody chose and not for the one move a player went and bought.
            //
            // The machine is not spent here. It is spent by being used, and declining is
            // not using it.
            if (player.MovesOffered.Any(o => o.Slot == slot && o.MoveId == machine.Teaches))
                return [];

            player.MovesOffered.Add((slot, machine.Teaches, machine.Id));

            return
            [
                new Outgoing(new MoveOffered(slot, machine.Teaches), OnlyTo: player.Id),
            ];
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

    /// <summary>
    /// How long after a trigger a scene may still be walking the player.
    /// <para>
    /// Generous, because a scene is as long as its text and somebody reads at their own
    /// pace. It is a bound rather than a schedule — what it stops is a walk arriving with
    /// no scene behind it at all.
    /// </para>
    /// </summary>
    private const double SceneSeconds = 120;

    /// <summary>
    /// The clock as of the last tick, for the places that are told nothing about time.
    /// <para>
    /// Talking and finishing talking arrive from a socket with no timestamp on them, and
    /// they need one only to answer "was this during a scene". The last tick is close
    /// enough for that and does not mean threading a clock through a protocol.
    /// </para>
    /// </summary>
    private double LastTickAt { get; set; }

    /// <summary>
    /// Whether a scene is running for this player, on the map it started on.
    /// <para>
    /// Both halves matter. The window is what makes a scene's messages something other
    /// than a client rearranging a map it happens to be standing on; the map is what
    /// stops the tail of one scene landing on the map the same scene just walked the
    /// player into.
    /// </para>
    /// </summary>
    private static bool InScene(ServerPlayer player, double nowSeconds) =>
        nowSeconds <= player.SceneUntil && player.MapId == player.SceneOn;

    /// <summary>What the last attempt to hold a scene's cast came to.</summary>
    public string? LastSceneCast { get; private set; }

    /// <summary>
    /// Holds everybody a scene is about, wherever they are standing.
    /// <para>
    /// Deliberately without the reachability check that talking has. Talking has it
    /// because a conversation with somebody across the map is not a conversation; a scene
    /// is precisely that, and using the talking message for this refused every cast member
    /// out of arm's reach. They then wandered through the scene and had its final
    /// placement refused as well, since nothing was holding them.
    /// </para>
    /// <para>
    /// The bound is the scene window instead, the same one a scene placement uses: a
    /// trigger this server agreed to fire, recently. Without one, holding is refused —
    /// otherwise this is a way to freeze anybody on a map from anywhere on it, which is
    /// the exact thing the reachability check was protecting against.
    /// </para>
    /// </summary>
    public List<Outgoing> HoldSceneCast(int playerId, IReadOnlyList<int> localIds, double nowSeconds)
    {
        lock (_gate)
        {
            LastSceneCast = null;

            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];

            if (!InScene(player, nowSeconds))
            {
                LastSceneCast = "refused: no scene is running for them here";
                return [];
            }

            if (!_populated.TryGetValue(player.MapId, out MapPopulation? people)) return [];

            int held = 0;

            foreach (int localId in localIds.Distinct())
            {
                if (people.ById(localId) is not { } person) continue;

                person.HeldBy = playerId;
                    held++;
            }

            LastSceneCast = $"holding {held} of {localIds.Count} still for the scene";

            return [];
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
    public List<Outgoing> PlaceAfterScene(
        int playerId, int localId, GridPosition square, Direction facing, double nowSeconds = 0)
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

            // The scene window, not the hold. The hold was only ever a proxy for "there
            // is a scene going on", and it is a bad one: it depends on two messages
            // arriving in the order they were sent, and in play they did not — a text box
            // closing let go of the cast between the walk and the placement, and the
            // placement was refused for a reason that had nothing to do with it.
            //
            // The window is the real thing, it is already what bounds a scene's cast, and
            // it does not care what order anything arrives in.
            if (!InScene(player, nowSeconds))
            {
                LastScenePlacement = $"object {localId}: no scene is running for them here";
                return [];
            }

            // Still worth saying when somebody else is holding them, because that is a
            // different situation entirely — two players inside one scene — and it is
            // worth not being silent about it.
            if (person.HeldBy is { } holder && holder != playerId)
            {
                LastScenePlacement = $"object {localId} is held by #{holder}, not by them";
                return [];
            }

            // Onto a door, which means through it. Nobody is left standing in a doorway:
            // a door's square is solid in the block data and this game opens it so people
            // can walk through, not so they can stand there. The professor walks to his
            // lab at the end of the opening and the cartridge takes him inside; leaving
            // him on the doormat blocks the only way in, because a doorway has exactly
            // one walkable neighbour and he is standing on the other end of it.
            //
            // Mirror of the rule one milestone back. That one was "a scene may not leave
            // somebody standing on a player"; this is "a scene may not leave somebody
            // standing on a door", and both are the same sentence about squares nobody
            // should be left on.
            if (_world.Find(player.MapId)?.IsDoor(square) == true)
            {
                // Off the map the way a felled tree is: for this player, until they
                // leave. That mechanism already exists and already means exactly this —
                // something the map has that this player can walk through.
                person.HeldBy = null;
                player.Shifted.Add(localId);

                LastScenePlacement = $"object {localId} went in through the door at {square}";

                return [new Outgoing(new WentInside(localId), OnlyTo: playerId)];
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

            // And nobody playing, either. This was checked for objects and not for
            // players, and the difference was not academic: the professor's scene ends
            // with both of them at his door, the professor was placed on top of whoever
            // he had just walked there, and a person standing in a doorway that only has
            // one walkable neighbour is a person who can never get back onto the door.
            if (_players.Values.FirstOrDefault(p => p.MapId == player.MapId && p.Square == square) is { } who)
            {
                LastScenePlacement = $"{square} has #{who.Id} standing on it";
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

    /// <summary>True when this square on this map is water.</summary>
    private bool IsWater(string mapId, GridPosition square) =>
        _world.Find(mapId)?.IsWater(square) ?? false;

    /// <summary>
    /// The move that gets somebody onto the water, by the name this cartridge gives it.
    /// <para>
    /// Read off the rules file rather than written down here. A number remembered from
    /// another game is the one mistake this project has a standing rule against, and a
    /// rules file with no move of that name simply has no surfing in it.
    /// </para>
    /// </summary>
    public int? SurfMove => _battles?.Rules.SurfMove is > 0 and var id ? id : null;

    /// <summary>
    /// Getting onto the water in front of you.
    /// <para>
    /// Three things have to hold and the server owns all three: the square ahead is
    /// water, somebody in the party knows how, and the player is not already out there.
    /// The client asks because the client is where the button is; it does not decide,
    /// because a client that decided could put itself in the middle of the sea.
    /// </para>
    /// </summary>
    public List<Outgoing> Surf(int playerId)
    {
        lock (_gate)
        {
            LastSurf = null;

            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];
            if (player.InBattle || player.Surfing) return [];

            if (SurfMove is not { } surf)
            {
                LastSurf = "refused: this server's rules have no move called SURF";
                return [];
            }

            if (ScriptState.SlotKnowing(player.Party.Select(m => m.Moves), surf) == ScriptState.NoSlot)
            {
                LastSurf = "refused: nobody in the party knows SURF";
                return [];
            }

            GridPosition ahead = player.Square.Step(player.Facing);

            if (!IsWater(player.MapId, ahead))
            {
                LastSurf = $"refused: {ahead} is not water";
                return [];
            }

            if (IsOccupiedFor(player, player.MapId, ahead))
            {
                LastSurf = $"refused: somebody is standing on {ahead}";
                return [];
            }

            player.Surfing = true;
            player.Square = ahead;

            LastSurf = $"onto the water at {ahead}";

            var send = new List<Outgoing>
            {
                new(new SurfingChanged(true, ahead.X, ahead.Y), OnlyTo: playerId),
                new(new PlayerMoved(playerId, ahead.X, ahead.Y, player.Facing), OnMap: player.MapId),
            };

            AfterArrival(player, send, LastTickAt);

            return send;
        }
    }

    /// <summary>What the last attempt to get onto the water came to.</summary>
    public string? LastSurf { get; private set; }

    /// <summary>
    /// The text box is closed. Whoever this player was holding carries on — and if what
    /// they were holding was somebody who wanted a fight, the fight starts now.
    /// </summary>
    public List<Outgoing> StopTalking(int playerId)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];

            player.Shopping = [];

            foreach (MapPopulation people in _populated.Values)
                people.Release(holder => holder == playerId);

            int localId = player.FightingWhenDone;

            player.FightingWhenDone = 0;

            if (localId == 0) return [];
            if (!_populated.TryGetValue(player.MapId, out MapPopulation? here)) return [];
            if (here.ById(localId) is not { } person) return [];

            List<Outgoing> fight = StartTrainerBattle(player, person.Template);

            if (fight.Count > 0) LastTalkOutcome = $"a fight with trainer {person.Template.TrainerId}";

            return fight;
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
    public List<Outgoing> FireTrigger(int playerId, int x, int y, int? trainerId = null, double nowSeconds = 0)
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

            if (_world.Find(player.MapId) is not { } here)
            {
                LastTriggerOutcome = "refused: nothing on that square runs anything";
                return [];
            }

            // The armed one, not the first one. Two triggers share the square at the lab
            // door and only ever one of them is live; asking the first whether it is
            // armed refused the square for the whole of the rival's beat.
            if (here.ArmedTriggerAt(square, player.Script.Read) is not { } trigger)
            {
                if (here.TriggerAt(square) is not { } disarmed)
                {
                    LastTriggerOutcome = "refused: nothing on that square runs anything";
                    return [];
                }

                LastTriggerOutcome =
                    $"refused: nothing armed here — variable 0x{disarmed.Variable:X4} holds " +
                    $"{player.Script.Read(disarmed.Variable)}";

                return [];
            }

            // The window opens whether or not there is a fight here: a scene that walks
            // the player is exactly the kind with nothing to arbitrate, and it is the one
            // that needs it.
            player.SceneUntil = nowSeconds + SceneSeconds;
            player.SceneOn = player.MapId;

            if (!trigger.CanBeFought || trainerId is null)
            {
                LastTriggerOutcome = "a script the client runs; nothing here to arbitrate";
                return [];
            }

            // Which of them is a fact about the save, and the save's script ran on the
            // other side of the split. So the client names one and this side checks the
            // name is on the list — which is the whole of the trust here. A client that
            // names the champion at the lab door gets nothing.
            if (!trigger.Fields(trainerId.Value))
            {
                LastTriggerOutcome =
                    $"refused: trainer {trainerId} is not one this square fields " +
                    $"({string.Join(", ", trigger.Fights)})";

                return [];
            }

            // Built as a person for a moment, because a fight is a fight and the one
            // routine that starts them has been right about parties, beaten trainers and
            // healthy leads since trainers existed.
            var asPerson = new MapObject(
                0, 0, x, y, player.Facing, 0, IsTrainer: true, TrainerId: trainerId.Value);

            if (StartTrainerBattle(player, asPerson) is not { Count: > 0 } challenge)
            {
                LastTriggerOutcome = $"trainer {trainerId} is not one this server can field right now";
                return [];
            }

            LastTriggerOutcome = $"a fight with trainer {trainerId}";

            return challenge;
        }
    }

    /// <summary>
    /// Why the square a player is standing on ran nothing, when it is a square that
    /// could have.
    /// <para>
    /// The one part of a trigger the server never hears about. A disarmed trigger is
    /// refused by the client, which reads the variable itself and sends nothing — so a
    /// story square that has already been used looks exactly like open ground in every
    /// log this server writes. It cost an evening: the professor's scene had run once,
    /// written down that it happened, and the report was "nothing happened", which was
    /// true and which nothing on either side would say out loud.
    /// </para>
    /// </summary>
    public string? WhySilent(int playerId)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return null;
            if (_world.Find(player.MapId)?.TriggerAt(player.Square) is not { } trigger) return null;

            int held = player.Script.Read(trigger.Variable);

            return trigger.Armed(held)
                ? null
                : $"a story square, spent: variable 0x{trigger.Variable:X4} holds {held} and this wants {trigger.Value}";
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

    /// <summary>Why the last step was refused outright, when it was.</summary>
    public string? LastStepRefusal { get; private set; }

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
            send.AddRange(TakeWarp(player, warp, nowSeconds));
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
    private List<Outgoing> TakeWarp(ServerPlayer player, Warp warp, double nowSeconds)
    {
        if (_world.Find(warp.TargetMapId) is not { } target)
            return [];

        GridPosition arrival = warp.TargetWarpId >= 0 && warp.TargetWarpId < target.Warps.Count
            ? target.Warps[warp.TargetWarpId].Square
            : FindSpawn(target.Id);

        if (!GridFor(target.Id).IsWalkable(arrival)) arrival = FindSpawn(target.Id);

        Direction facing = player.Facing;

        // And then out of the doorway, because the cartridge's warp puts you in it. The
        // games walk you through a door — out of one onto the street, in through one onto
        // the floor — and skipping that step leaves both ends of every building somewhere
        // a player is stuck: the arrival that would use the warp again has already
        // happened, so pressing towards it does nothing at all and the only way through
        // is to step off the square and back onto it.
        if (StepClearOf(player, _world.Find(player.MapId), warp.Square, target, arrival) is { } step)
        {
            arrival = step.Square;
            facing = step.Facing;
        }

        return Transfer(player, target.Id, arrival, facing, nowSeconds);
    }

    /// <summary>
    /// Where a doorway lets you out, and which way you are looking when it does.
    /// <para>
    /// Which warps are doorways is asked of the cartridge, and asked of <em>both</em>
    /// ends, because the two ends are one thing: a door is a square the block data calls
    /// solid and this project opens for walking through, and a shop has one on the street
    /// while the mat inside it is ordinary floor. Either end being a door makes the pair
    /// a building, and the step happens in both directions.
    /// </para>
    /// <para>
    /// That is 558 of this cartridge's warps, and it leaves 717 alone — the stairs, cave
    /// mouths and ladders, which the games do let you stand on. Of the 558, down is the
    /// first way out at 326 and up at 220, which is exactly the two halves of a building:
    /// out onto the street, and in onto the floor. Six arrive somewhere with nowhere to
    /// step, and standing in the doorway beats being moved into a wall.
    /// </para>
    /// <para>
    /// A neighbour that is itself a warp is not a way out. Shop fronts are three doors
    /// side by side, and stepping along one lands on another — which is a walk out of a
    /// building that puts you straight back inside it.
    /// </para>
    /// </summary>
    private (GridPosition Square, Direction Facing)? StepClearOf(
        ServerPlayer player, MapData? from, GridPosition left, MapData map, GridPosition door)
    {
        if (from?.IsDoor(left) != true && !map.IsDoor(door)) return null;

        CollisionGrid grid = GridFor(map.Id);

        foreach (Direction way in (Direction[])[Direction.Down, Direction.Up, Direction.Left, Direction.Right])
        {
            GridPosition next = door.Step(way);

            if (!grid.IsWalkable(next) || map.WarpAt(next) is not null) continue;
            if (IsOccupiedFor(player, map.Id, next)) continue;

            return (next, way);
        }

        // Six arrivals on this cartridge have nothing but more warps around them.
        // Standing in the doorway is better than being moved into a wall.
        return null;
    }

    /// <summary>
    /// Moves a player between maps: gone to everyone on the old one, arrived to
    /// everyone on the new one, and a fresh view of the world for the player.
    /// </summary>
    private List<Outgoing> Transfer(
        ServerPlayer player, string mapId, GridPosition arrival, Direction facing, double nowSeconds)
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
            send.Add(new Outgoing(new ObjectsPlaced([.. VisibleTo(player, arrived)]), OnlyTo: player.Id));

        foreach (ServerPlayer existing in _players.Values.Where(p => p.MapId == mapId && p.Id != player.Id))
            send.Add(new Outgoing(existing.ToAppeared(), OnlyTo: player.Id));

        send.Add(new Outgoing(player.ToAppeared(), Except: player.Id, OnMap: mapId));

        OpenWindowForArrival(player, nowSeconds);
        send.AddRange(HandOverOnArrival(player));

        return send;
    }

    /// <summary>
    /// Opens a scene window when the map somebody just arrived on runs something.
    /// <para>
    /// The counterpart of the trigger doing it, and it has to be here rather than asked
    /// for: an arrival script is a scene like any other, and a scene needs a window
    /// before its first message. Unlike a trigger, nothing has to be taken on trust —
    /// the server has the conditions in its own world file and the variables in its own
    /// copy of the save, so it decides on its own that a scene starts here.
    /// </para>
    /// <para>
    /// Tenth time this project has needed both halves of one rule, and the first time the
    /// server's half needs no message at all.
    /// </para>
    /// </summary>
    private void OpenWindowForArrival(ServerPlayer player, double nowSeconds)
    {
        LastArrivalScript = null;

        if (_world.Find(player.MapId)?.EntryFor(player.Script.Read) is not { } entry) return;

        player.SceneUntil = nowSeconds + SceneSeconds;
        player.SceneOn = player.MapId;

        LastArrivalScript = $"arriving runs something here: 0x{entry.Variable:X4} holds {entry.Value}";
    }

    /// <summary>
    /// Gives the player whatever arriving somewhere hands over.
    /// <para>
    /// Walking into the shop in Viridian is what hands over the parcel the rest of the
    /// story turns on, and nobody is talked to in that exchange — so none of the
    /// machinery that gives a person's gift applies to it.
    /// </para>
    /// <para>
    /// Decided here rather than asked for, exactly as the scene window above is. The
    /// server has the condition in its own world file and the variable in its own copy of
    /// the save, and now it has what the script hands over too — so nothing about this is
    /// taken on trust and no message could disagree with it.
    /// </para>
    /// <para>
    /// Once. The same set that stops a second starter stops a second parcel: a doorway
    /// somebody walks back through is a doorway whose script is armed exactly as it was
    /// the first time, and the variable that disarms it is not written until the scene
    /// the client is still playing gets to the end of itself.
    /// </para>
    /// </summary>
    private List<Outgoing> HandOverOnArrival(ServerPlayer player)
    {
        if (_world.Find(player.MapId)?.EntryFor(player.Script.Read) is not { Gives: true } entry) return [];
        // Keyed by the map and the condition rather than by the item, because two
        // doorways in the world could reasonably hand over the same thing and neither
        // should spend the other.
        if (!player.ItemsTaken.Add($"{player.MapId}:entry:{entry.Variable:X4}={entry.Value}")) return [];

        int count = Math.Max(1, entry.GivesCount);

        player.Bag.Add(entry.GivesItemId, count);

        LastGift = $"item {entry.GivesItemId} x{count} for arriving";

        return [new Outgoing(
            new ItemFound(entry.GivesItemId, count, player.Bag.Entries),
            OnlyTo: player.Id)];
    }

    /// <summary>
    /// Runs a line typed into the operator console.
    /// <para>
    /// Everything a console does, it does here. The client sends text and nothing else,
    /// because a console the client acted on would be a cheat menu with extra steps —
    /// and because the account allowed to run one is named on this server's own command
    /// line, which is a place a player cannot reach.
    /// </para>
    /// <para>
    /// Refusing is silent to everyone but the person who asked. Somebody probing for a
    /// console on a server that has none should learn nothing from the reply that they
    /// could not have guessed, and the person who mistyped a command should be told
    /// exactly what went wrong.
    /// </para>
    /// </summary>
    /// <summary>
    /// Hands over what a script says it handed over, once the world agrees it could have.
    /// <para>
    /// The client runs the script and knows which branch was taken; this side has never
    /// seen a script and never will. So the claim travels and is checked against the set
    /// the world file carries for that person — every item id that appears in a give
    /// command anywhere in their script. Naming an item they could not produce is
    /// refused, which is what stops this being a way to ask for anything in the game.
    /// </para>
    /// <para>
    /// Three more checks, all the ordinary ones: they have to be in reach, it has to be
    /// the first time, and there has to be a rules file to know an item is an item.
    /// </para>
    /// </summary>
    public List<Outgoing> ScriptGave(int playerId, int localId, int itemId)
    {
        lock (_gate)
        {
            LastGift = null;

            if (_rules is null) return [];
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];
            if (!_populated.TryGetValue(player.MapId, out MapPopulation? people)) return [];
            if (people.ById(localId) is not { } person) return [];

            if (!person.Template.CanGive.Contains(itemId))
            {
                LastGift = $"refused: object {localId} never hands over item {itemId}";
                return [];
            }

            var reachable = Interaction
                .Reachable(player.Square, player.Facing, square => !GridFor(player.MapId).IsWalkable(square))
                .ToHashSet();

            if (!reachable.Contains(person.Square))
            {
                LastGift = $"refused: object {localId} is not within reach";
                return [];
            }

            if (!player.ItemsTaken.Add($"{player.MapId}:{localId}:gift"))
            {
                LastGift = "an item that has already been handed over";
                return [];
            }

            player.Bag.Add(itemId, 1);

            LastGift = $"item {itemId} from object {localId}";

            return [new Outgoing(new ItemFound(itemId, 1, player.Bag.Entries), OnlyTo: playerId)];
        }
    }

    /// <summary>
    /// Takes a move that was offered and could not fit, in place of one already known.
    /// <para>
    /// Offered, not asked for: the server put it on a list when a level-up produced a
    /// fifth move, and nothing a client sends can put anything on that list. So this is
    /// not a way to teach anybody anything — it can only answer a question this side
    /// asked, and the answer is which of the four to drop.
    /// </para>
    /// <para>
    /// Declining is an answer too, and a real one: the games let you keep what you have.
    /// </para>
    /// </summary>
    public List<Outgoing> LearnMove(int playerId, int moveId, int forget)
    {
        lock (_gate)
        {
            LastLearned = null;

            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];

            int at = player.MovesOffered.FindIndex(o => o.MoveId == moveId);

            if (at < 0)
            {
                LastLearned = $"refused: nobody was offered move {moveId}";
                return [];
            }

            (int slot, int _, int fromItem) = player.MovesOffered[at];

            player.MovesOffered.RemoveAt(at);

            if (slot < 0 || slot >= player.Party.Count) return [];

            SavedMon member = player.Party[slot];

            // Out of range is "keep what you have", which the games allow and which is
            // one of the two answers rather than a mistake.
            if (forget < 0 || forget >= member.Moves.Count)
            {
                LastLearned = $"move {moveId} was not learned";
                return [];
            }

            var moves = member.Moves.ToList();
            int dropped = moves[forget];

            moves[forget] = moveId;

            player.Party[slot] = member with { Moves = moves };

            // And now the machine is spent, if it was a machine and if it is the kind
            // that is used up. The eight the cartridge marks too important to sell are
            // the eight that survive being used, which is the same line the teaching
            // path has always drawn.
            if (fromItem != 0 && _battles?.Rules.ItemAt(fromItem) is { IsReusableMachine: false })
                player.Bag.Remove(fromItem);

            LastLearned = $"forgot move {dropped} and learned {moveId}";

            return [new Outgoing(new BagUpdated(player.Bag.Entries, [.. player.Party], ""), OnlyTo: playerId)];
        }
    }

    /// <summary>What the last answer about a move came to.</summary>
    public string? LastLearned { get; private set; }

    public List<Outgoing> RunConsole(int playerId, string text, double nowSeconds = 0)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];

            if (!Operators.Contains(player.Name))
            {
                LastConsole = $"refused: {player.Name} is not an operator";
                return [Said(player, "There is no console here.")];
            }

            ConsoleLine line = ConsoleLine.Of(text);

            List<Outgoing> done = Run(player, line, nowSeconds);

            LastConsole = $"{player.Name}: {text}";

            return done;
        }
    }

    /// <summary>
    /// Everything about a player that the client keeps its own copy of.
    /// <para>
    /// Sent on arrival and again whenever the console changes any of it, because which
    /// flags are set decides which of somebody's lines they are on — and a client
    /// working from the flags it had a second ago reads the wrong one.
    /// </para>
    /// </summary>
    private Welcome WelcomeFor(ServerPlayer player) =>
        new(
            player.Id, player.MapId, player.Square.X, player.Square.Y, player.Facing,
            player.Money, player.Bag.Entries, player.Party)
        {
            Flags = [.. player.Script.Flags],
            Variables = [.. player.Script.Variables.Select(v => new SavedVariable(v.Key, v.Value))],
            Beaten = [.. player.DefeatedTrainers],
        };

    /// <summary>
    /// Heals a party, if there is somebody within reach who does that.
    /// <para>
    /// Asked for rather than done at the counter, because the counter asks. What this
    /// side keeps is the check: somebody who heals has to actually be there, so a client
    /// sending this in the middle of a route gets nothing.
    /// </para>
    /// </summary>
    public List<Outgoing> Heal(int playerId)
    {
        lock (_gate)
        {
            LastHeal = null;

            if (!_players.TryGetValue(playerId, out ServerPlayer? player)) return [];
            if (_battles is null || player.Party.Count == 0) return [];

            if (!_populated.TryGetValue(player.MapId, out MapPopulation? people)) return [];

            // In front of them, by the same rule a conversation uses — a counter two
            // rooms away is not a counter you are standing at.
            var reachable = Interaction
                .Reachable(player.Square, player.Facing, square => !GridFor(player.MapId).IsWalkable(square))
                .ToHashSet();

            if (!people.Objects.Any(o => o.Template.Heals && reachable.Contains(o.Square)))
            {
                LastHeal = "refused: nobody here heals anybody";
                return [];
            }

            bool needed = player.Party.Any(m => !_battles.IsWell(m));

            HealParty(player);

            LastHeal = needed
                ? $"{player.Party.Count} back on their feet"
                : "nobody needed it";

            return [new Outgoing(new PartyHealed([.. player.Party], needed), OnlyTo: playerId)];
        }
    }

    /// <summary>What the last visit to a counter came to.</summary>
    public string? LastHeal { get; private set; }

    /// <summary>What the console was last asked to do, and by whom.</summary>
    public string? LastConsole { get; private set; }

    private Outgoing Said(ServerPlayer player, string line) =>
        new(new ConsoleReply(line), OnlyTo: player.Id);

    /// <summary>
    /// The commands themselves.
    /// <para>
    /// Each one ends by sending the player their save again, because a console changes
    /// things the client is holding its own copy of — which flags are set decides which
    /// of somebody's lines they are on, and a client working from the flags it had a
    /// second ago would read the wrong one.
    /// </para>
    /// </summary>
    private List<Outgoing> Run(ServerPlayer player, ConsoleLine line, double nowSeconds)
    {
        switch (line.Verb)
        {
            case "" or "help":
                return [.. ConsoleHelp.Lines.Select(l => Said(player, l))];

            case "where":
                return [Said(player, $"{player.MapId} {_world.Find(player.MapId)?.Name} at {player.Square}")];

            case "tp":
            {
                if (_world.Find(line.Word(0)) is not { } target)
                    return [Said(player, $"no map {line.Word(0)}")];

                if (line.Number(1) is not { } x || line.Number(2) is not { } y)
                    return [Said(player, "/tp <map> <x> <y>")];

                var square = new GridPosition(x, y);

                if (!GridFor(target.Id).IsWalkable(square))
                    return [Said(player, $"{square} is not somewhere anybody can stand on {target.Id}")];

                return
                [
                    .. Transfer(player, target.Id, square, player.Facing, nowSeconds),
                    Said(player, $"{target.Name} at {square}"),
                ];
            }

            case "give":
            {
                if (_battles is null) return [Said(player, "this server has no rules loaded")];
                if (line.Number(0) is not { } species) return [Said(player, "/give <species> <level>")];

                int level = line.Number(1) ?? 5;

                if (player.Party.Count >= MaxPartySize) return [Said(player, "the party is full")];
                if (_battles.Wild(species, level) is not { } made)
                    return [Said(player, $"species {species} is not one this server can field")];

                player.Party.Add(BattleFactory.Save(made));

                return [Said(player, $"species {species} at level {level}"), .. Resend(player)];
            }

            case "item":
            {
                if (line.Number(0) is not { } itemId) return [Said(player, "/item <id> [count]")];

                int count = Math.Max(1, line.Number(1) ?? 1);

                player.Bag.Add(itemId, count);

                return [Said(player, $"item {itemId} x{count}"), .. Resend(player)];
            }

            case "flag":
            {
                if (line.Number(0) is not { } flag) return [Said(player, "/flag <id> [on|off]")];

                bool on = !string.Equals(line.Word(1), "off", StringComparison.OrdinalIgnoreCase);

                if (on) player.Script.Set(flag);
                else player.Script.Clear(flag);

                return [Said(player, $"flag 0x{flag:X4} is {(on ? "set" : "clear")}"), .. Resend(player)];
            }

            case "var":
            {
                if (line.Number(0) is not { } id || line.Number(1) is not { } value)
                    return [Said(player, "/var <id> <value>")];

                player.Script.Write(id, value);

                return [Said(player, $"0x{id:X4} holds {value}"), .. Resend(player)];
            }

            case "read":
            {
                if (line.Number(0) is not { } id) return [Said(player, "/read <id>")];

                return [Said(player, $"0x{id:X4} holds {player.Script.Read(id)}")];
            }

            case "heal":
                HealParty(player);
                return [Said(player, $"{player.Party.Count} back on their feet"), .. Resend(player)];

            // The counterpart, and it exists for one reason: nothing else in this game
            // hurts a party on demand. Testing that a counter heals meant walking into
            // grass until something hit back, and the answer that came out the far end
            // of that was always "nobody needed it" — a heal that heals nobody proves
            // the box opened and nothing else.
            case "hurt":
            {
                if (player.Party.Count == 0) return [Said(player, "nobody to hurt")];

                int left = Math.Max(0, line.Number(0) ?? 1);

                // Capped at what each one actually has, because a console that can write
                // 900 into a level 5's health would make every other number in a battle
                // a lie, and the first thing anybody would blame is the battle.
                for (int i = 0; i < player.Party.Count; i++)
                {
                    int most = _battles?.Restore(player.Party[i])?.MaxHp ?? left;

                    player.Party[i] = player.Party[i] with { CurrentHp = Math.Min(left, most) };
                }

                return
                [
                    Said(player, left == 0
                        ? $"{player.Party.Count} down"
                        : $"{player.Party.Count} on {left} HP"),
                    .. Resend(player),
                ];
            }

            case "money":
            {
                if (line.Number(0) is not { } amount) return [Said(player, "/money <amount>")];

                player.Money = Math.Clamp(amount, 0, MaxMoney);

                return [Said(player, $"money is {player.Money}"), .. Resend(player)];
            }

            case "forget":
                player.Script.Forget();
                return [Said(player, "every flag and variable, gone"), .. Resend(player)];

            default:
                return [Said(player, $"no command \"{line.Verb}\" — try /help")];
        }
    }

    /// <summary>
    /// Sends a player their own save again, which is what makes a console change stick
    /// on the side that has to read it.
    /// </summary>
    private List<Outgoing> Resend(ServerPlayer player) =>
    [
        new Outgoing(WelcomeFor(player), OnlyTo: player.Id),
    ];

    /// <summary>What the map somebody last arrived on had to say for itself.</summary>
    public string? LastArrivalScript { get; private set; }

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

        // Which table depends on what you are standing on rather than on where the map
        // is. The same route has grass down one side and sea down the other, and a step
        // in one has nothing to do with what lives in the other.
        EncounterTable? table = player.Surfing
            ? map.Encounters?.Water
            : map.IsEncounterSquare(player.Square) ? map.Encounters?.Land : null;

        if (table is not { IsUsable: true }) return;

        GrassSteps++;

        if (WildEncounters.RollStep(_rng, table) is not { } encounter) return;
        if (_battles is null) return;

        if (_battles.Wild(encounter.Species, encounter.Level) is not { } wild) return;

        // No healthy lead, no encounter. Starting one here would start a battle that
        // was over before its first turn — which is exactly the freeze this fixes.
        if (LeadBattler(player) is not { } lead) return;

        player.Battle = new Encounter(lead.Slot, lead.Battler, [wild], _rng.State);

        send.Add(new Outgoing(
            new BattleStarted(
                BattleFactory.View(lead.Battler), BattleFactory.View(wild),
                BallsOf(player), MedicineOf(player), Slot: lead.Slot),
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
    /// Sends out one of the party by choice rather than because somebody fainted.
    /// <para>
    /// Refused for a slot that is not in the party, for the one already out, and for
    /// anybody who cannot fight — all three are things a client could ask for and none
    /// of them is something a player could do, which is the whole reason this is decided
    /// here.
    /// </para>
    /// <para>
    /// Whoever was out is written back first. A switch that forgot to do that would heal
    /// the one leaving, because what the party holds is the health they went in with.
    /// </para>
    /// </summary>
    private BattlerView? SwapIn(ServerPlayer player, Encounter encounter, int slot)
    {
        if (_battles is null) return null;
        if (slot < 0 || slot >= player.Party.Count) return null;
        if (slot == encounter.PlayerSlot) return null;

        if (_battles.Restore(player.Party[slot]) is not { HasFainted: false } coming) return null;

        WriteBackActive(player, encounter);

        encounter.SendPlayer(slot, coming);

        return BattleFactory.View(coming);
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

        List<Outgoing> send = Transfer(player, mapId, square, player.Facing, LastTickAt);

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

            // Somebody else, before anything else. A switch is not a move and the engine
            // has no idea a party exists — so it happens here, and what reaches the
            // engine is a side that does nothing this turn, which is exactly what a
            // switch costs.
            List<Outgoing> swapped = [];

            if (action is BattleAction.SwitchTo going)
            {
                if (SwapIn(player, encounter, going.Slot) is not { } sent)
                {
                    return [new Outgoing(new Rejected("Nobody there can fight."), OnlyTo: playerId)];
                }

                swapped.Add(new Outgoing(
                    new BattlerSentOut(Side.Player, sent, encounter.PlayerSlot), OnlyTo: playerId));
            }

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

            var send = new List<Outgoing>(swapped)
            {
                new(
                    new BattleUpdate(
                        events, battle.Player.CurrentHp, battle.Opponent.CurrentHp,
                        BallsOf(player), MedicineOf(player), [.. player.Party]),
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
                {
                    send.Add(new Outgoing(new TrainerBeaten(won), OnlyTo: playerId));
                    send.AddRange(PrizeFor(player, won));
                }

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
                    new BattlerSentOut(Side.Player, BattleFactory.View(replacement.Battler), replacement.Slot),
                    OnlyTo: playerId));
            }

            return send;
        }
    }

    /// <summary>
    /// What beating somebody hands over, if beating them hands anything over.
    /// <para>
    /// Eight fights in this cartridge pay out more than money, and every one of them is
    /// a gym: the TM is inside the script the <c>trainerbattle</c> runs on being won,
    /// which is not a place any conversation goes. Talking to BROCK before the fight
    /// gets "I'm PEWTER's GYM LEADER" and after it gets "there are all kinds of
    /// TRAINERS"; neither branch has ever mentioned TM39.
    /// </para>
    /// <para>
    /// Decided here rather than taken from the client for the ordinary reason — the
    /// client runs the same script and shows the same words, and if it could also fill
    /// its own bag it could fill it with anything. Once per person, by the same ledger a
    /// ball on the ground uses.
    /// </para>
    /// </summary>
    private List<Outgoing> PrizeFor(ServerPlayer player, int trainerId)
    {
        // Cleared first, or the second fight of the evening reports the first one's TM.
        LastPrize = null;

        if (_rules is null) return [];
        if (_world.Find(player.MapId) is not { } map) return [];

        if (map.Objects.FirstOrDefault(o => o.TrainerId == trainerId && o.WinsItem) is not { } leader) return [];

        // The same ledger entry the script route uses, because they are two views of one
        // handover: the export reads the won-fight script for its giveitem, and the client
        // runs that same script and names what came out of it. Whichever arrives first
        // takes it, and MISTY handed over TM03 twice before this said so.
        if (!player.ItemsTaken.Add($"{player.MapId}:{leader.LocalId}:gift")) return [];

        int count = Math.Max(1, leader.WinsCount);

        player.Bag.Add(leader.WinsItemId, count);

        LastPrize = $"item {leader.WinsItemId} x{count} for beating trainer {trainerId}";

        return [new Outgoing(new ItemFound(leader.WinsItemId, count, player.Bag.Entries), OnlyTo: player.Id)];
    }

    /// <summary>What the last fight paid out beyond money, if anything.</summary>
    public string? LastPrize { get; private set; }

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

        // A move that could not fit is a question, not a shrug. The games ask which one
        // to drop; this remembers what was offered so the asking can happen once the
        // player has finished reading, and the answer can arrive after the fight is over.
        foreach (BattleEvent.MoveNotLearned offered in events.OfType<BattleEvent.MoveNotLearned>())
            player.MovesOffered.Add((slot, offered.MoveId, 0));

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

        // A fight is a warrant for a scene, and the fights that most need to be are the
        // ones a story square started. The rival's script has him walk off after losing,
        // and that walk was refused every time: the window his challenge opened is two
        // minutes long and the fight outlasted it.
        //
        // Reopened here rather than made longer, because a longer window is a worse
        // answer to "how long is a fight" than no window at all. This is the same rule
        // as a conversation and a trigger — the server arbitrates fights one at a time,
        // so a fight it just finished is something it can vouch for.
        player.SceneUntil = LastTickAt + SceneSeconds;
        player.SceneOn = player.MapId;

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
