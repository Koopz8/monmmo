using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.World;
using PokeMmo.Server.Storage;

namespace PokeMmo.Server;

/// <summary>
/// The socket layer. Accepts connections, hands messages to <see cref="GameWorld"/>,
/// and sends back whatever it says to send.
/// <para>
/// Kept deliberately thin — every rule lives in the world, which is tested without a
/// network at all. What is here is connection lifetime and fan-out.
/// </para>
/// </summary>
public static class Program
{
    public const int DefaultPort = 7777;

    public static async Task<int> Main(string[] args)
    {
        string worldPath = ArgumentValue(args, "--world") ?? "world.dat";
        string databasePath = ArgumentValue(args, "--db") ?? SqlitePlayerStore.DefaultFileName;
        string rulesPath = ArgumentValue(args, "--rules") ?? "rules.dat";
        string startingMap = ArgumentValue(args, "--map") ?? "pallet town";
        int port = int.TryParse(ArgumentValue(args, "--port"), out int parsed) ? parsed : DefaultPort;
        bool verbose = args.Contains("--verbose");

        if (!File.Exists(worldPath))
        {
            Console.Error.WriteLine($"No world file at {Path.GetFullPath(worldPath)}.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Generate one from your own cartridge:");
            Console.Error.WriteLine("  dotnet run --project src/Tools/RomDump -- your.gba --export-world world.dat");
            return 1;
        }

        WorldData world = WorldData.Load(worldPath);
        GameRules? rules = ReportRules(rulesPath);
        GameWorld game;

        try
        {
            game = new GameWorld(world, startingMap, rules);
        }
        catch (ArgumentException)
        {
            // Naming a map that is not there is an easy mistake and a stack trace is
            // no help; show what is actually available instead.
            Console.Error.WriteLine($"No map matching '{startingMap}' in {worldPath}.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Some that are:");

            foreach (MapData map in world.Maps
                         .OrderByDescending(m => m.Width * m.Height)
                         .DistinctBy(m => m.Name)
                         .Take(10))
            {
                Console.Error.WriteLine($"  {map.Id,-8} {map.Name}");
            }

            return 1;
        }

        Console.WriteLine($"Loaded {world.Count} maps from {worldPath}");
        Console.WriteLine(
            $"Starting players on {game.StartingMap.Name} ({game.StartingMap.Id}) — " +
            $"{game.StartingMap.Width}x{game.StartingMap.Height}");

        ReportWorldLinks(world);
        ReportStartingMapLinks(game);
        ReportEncounterReadiness(game.StartingMap);
        ReportTrainerReadiness(world, rules, game.StartingMap.Id);

        using var store = new SqlitePlayerStore(databasePath);
        Console.WriteLine($"Accounts in {Path.GetFullPath(databasePath)}");

        await new GameServer(game, store, verbose).RunAsync(port);
        return 0;
    }

    /// <summary>
    /// Whether the trainers standing on maps line up with the parties in the rules file.
    /// <para>
    /// This is the cross-check that catches a trainer table located one slot off. The
    /// two files come from the same image but by completely different routes — the ids
    /// out of scripts, the parties out of a table — so if the table start were wrong,
    /// the referenced ids would stop resolving. A world where every trainer resolves is
    /// not proof, but a world where half of them do not is a very loud symptom.
    /// </para>
    /// </summary>
    private static void ReportTrainerReadiness(WorldData world, GameRules? rules, string startingMapId)
    {
        List<MapObject> trainers = world.Maps
            .SelectMany(m => m.Objects)
            .Where(o => o.IsTrainer)
            .ToList();

        if (trainers.Count == 0)
        {
            Console.WriteLine("  no trainers in this world file — re-export it if you want anybody to challenge you");
            return;
        }

        int named = trainers.Count(o => o.CanBeFought);
        int seeing = trainers.Count(o => o.SightRange > 0);

        Console.WriteLine(
            $"  {trainers.Count} trainers on maps, {named} of them naming a trainer id, " +
            $"{seeing} with a line of sight");

        if (rules is null) return;

        int missing = trainers.Count(o => o.CanBeFought && rules.TrainerAt(o.TrainerId) is null);

        Console.WriteLine(missing == 0
            ? $"  every trainer id on a map has a party in {nameof(GameRules)}"
            : $"  {missing} trainers name an id with no party — the trainer table may be located wrongly");

        // Where they actually are. Most maps have none — the first routes of the games
        // deliberately have nobody who wants a fight — and "I walked past somebody and
        // nothing happened" is otherwise indistinguishable from a broken sight line.
        var busiest = world.Maps
            .Select(m => (Map: m, Count: m.Objects.Count(o => o.CanBeFought && o.SightRange > 0)))
            .Where(m => m.Count > 0)
            .OrderByDescending(m => m.Count)
            .Take(6)
            .ToList();

        foreach ((MapData map, int count) in busiest)
            Console.WriteLine($"    {map.Id,-8} {map.Name,-20} {count} looking for a fight");

        int shops = world.Maps.Sum(m => m.Objects.Count(o => o.IsShopkeeper));

        Console.WriteLine(shops == 0
            ? "  no shops in this world file — re-export it if you want anywhere to spend money"
            : $"  {shops} shopkeepers across the world");

        MapData starting = world.Find(startingMapId) ?? world.Maps.First();

        int here = starting.Objects.Count(o => o.CanBeFought && o.SightRange > 0);

        if (here == 0)
            Console.WriteLine($"  nobody on {starting.Name} wants a fight — walk to one of the above");
    }

    /// <summary>
    /// Loads the rules file if there is one, and says plainly when there is not.
    /// <para>
    /// Without it the server cannot decide a battle — it has no base stats and no
    /// catch rates — so battles stay where they are, resolved by the client and taken
    /// on trust. That is a thing to state at startup rather than leave someone to
    /// infer.
    /// </para>
    /// </summary>
    private static GameRules? ReportRules(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"No rules file at {Path.GetFullPath(path)} — encounters are disabled");
            Console.WriteLine("  generate one:  dotnet run --project src/Tools/RomDump -- your.gba --export-rules rules.dat");
            return null;
        }

        try
        {
            GameRules rules = GameRules.Load(path);

            Console.WriteLine(
                $"Rules from {path}: {rules.SpeciesCount} species, " +
                $"{rules.MoveCount} moves, {rules.LearnsetCount} learnsets, " +
                $"{rules.TrainerCount} trainers");

            return rules;
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine($"Could not read {path}: {ex.Message}");
            Console.WriteLine("  encounters are disabled until this is re-exported");
            return null;
        }
    }

    /// <summary>
    /// Says how connected the world is.
    /// <para>
    /// A world file exported before warps existed loads perfectly well and simply
    /// never lets anyone leave the first map, which from the outside is
    /// indistinguishable from a bug in the movement code.
    /// </para>
    /// </summary>
    private static void ReportWorldLinks(WorldData world)
    {
        int warps = world.Maps.Sum(m => m.Warps.Count);
        int connections = world.Maps.Sum(m => m.Connections.Count);

        if (warps == 0 && connections == 0)
        {
            Console.WriteLine("  no warps or connections in this world file — re-export it, nobody can leave the first map");
            return;
        }

        int doors = world.Maps.Sum(m => m.WarpsOnSolidSquares());

        Console.WriteLine($"  {warps} warps and {connections} edge connections across {world.Count} maps");

        // A door's square is solid in the block data, so this number being large is the
        // world behaving as the cartridge does. It being near zero would mean the doors
        // are somewhere other than where this thinks they are.
        Console.WriteLine($"  {doors} of those warps are on squares the map data calls solid — doors");
    }

    /// <summary>
    /// Says where you can actually leave the starting map from.
    /// <para>
    /// A player who walks into an edge and stops has no way to tell an edge with no
    /// neighbour from one whose arrival square is solid from a bug in the arithmetic.
    /// All three look like walking into a wall, and only the server knows which it is.
    /// </para>
    /// </summary>
    private static void ReportStartingMapLinks(GameWorld game)
    {
        MapData map = game.StartingMap;

        if (map.Connections.Count == 0)
            Console.WriteLine("  no edge connections on this map — its edges are all walls");

        foreach (MapConnection connection in map.Connections)
        {
            if (game.MapOf(connection.MapId) is not { } target)
            {
                Console.WriteLine($"  {connection.Side,-5} -> {connection.MapId} (not in this world)");
                continue;
            }

            // Walk the whole shared edge and count how much of the far side can be
            // stood on. Zero means the connection exists and nobody can ever use it.
            int usable = 0;
            bool vertical = connection.Side is ConnectionSide.Up or ConnectionSide.Down;
            int length = vertical ? map.Width : map.Height;

            for (int i = 0; i < length; i++)
            {
                GridPosition from = vertical ? new GridPosition(i, 0) : new GridPosition(0, i);
                GridPosition arrival = GameWorld.AcrossEdge(from, connection.Side, map, target, connection.Offset);

                if (game.GridOf(target.Id).IsWalkable(arrival)) usable++;
            }

            Console.WriteLine(
                $"  {connection.Side,-5} -> {target.Id} {target.Name} " +
                $"({target.Width}x{target.Height}, offset {connection.Offset}) " +
                $"{usable} of {length} land somewhere walkable");
        }
    }

    /// <summary>
    /// Says up front whether this map can produce encounters at all.
    /// <para>
    /// Without this, a world file exported before behaviours existed, or a map with no
    /// grass, looks exactly like a working server that simply never rolls anything —
    /// and there is no way to tell which from the outside.
    /// </para>
    /// </summary>
    private static void ReportEncounterReadiness(MapData map)
    {
        int grass = map.Behaviours.Count(MetatileBehaviour.IsEncounterGrass);

        if (map.Behaviours.Length == 0)
        {
            Console.WriteLine("  no square behaviours in this world file — re-export it, encounters cannot fire");
        }
        else
        {
            Console.WriteLine($"  {grass} grass squares of {map.Behaviours.Length}");
        }

        if (map.Encounters?.Land is { IsUsable: true } land)
        {
            Console.WriteLine($"  land encounters: rate {land.Rate}, {land.Slots.Count} slots");
        }
        else
        {
            Console.WriteLine("  no land encounter table for this map — nothing will appear");
        }

        if (grass > 0) ReportWhereTheGrassIs(map);
    }

    /// <summary>
    /// Says where the grass actually is.
    /// <para>
    /// Knowing a map has 52 grass squares is useless if you cannot find them — a
    /// player can wander a long way through a large map and touch none of them, which
    /// looks exactly like encounters being broken.
    /// </para>
    /// </summary>
    private static void ReportWhereTheGrassIs(MapData map)
    {
        var squares = new List<GridPosition>();

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var square = new GridPosition(x, y);
                if (MetatileBehaviour.IsEncounterGrass(map.BehaviourAt(square))) squares.Add(square);
            }
        }

        if (squares.Count == 0) return;

        int minX = squares.Min(s => s.X), maxX = squares.Max(s => s.X);
        int minY = squares.Min(s => s.Y), maxY = squares.Max(s => s.Y);

        Console.WriteLine($"  grass lies between ({minX}, {minY}) and ({maxX}, {maxY})");

        // Row summaries are more use than a list of coordinates: grass comes in
        // patches, and a row with a span is something you can walk to.
        foreach (var row in squares.GroupBy(s => s.Y).OrderBy(g => g.Key).Take(8))
            Console.WriteLine($"    y={row.Key,3}: x {row.Min(s => s.X)}-{row.Max(s => s.X)} ({row.Count()} squares)");
    }

    private static string? ArgumentValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];

        return null;
    }
}

/// <summary>Accepts connections and fans messages out to them.</summary>
public sealed class GameServer(GameWorld world, IPlayerStore store, bool verbose = false)
{
    private readonly ConcurrentDictionary<int, MessageChannel> _channels = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly TaskCompletionSource<int> _listening =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private double Now => _clock.Elapsed.TotalSeconds;

    /// <summary>
    /// Completes with the port actually bound. Passing port 0 asks the system for a
    /// free one, which is what lets tests run a real server without picking a port
    /// that might already be in use.
    /// </summary>
    public Task<int> Listening => _listening.Task;

    public async Task RunAsync(int port, CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        int boundPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        _listening.TrySetResult(boundPort);

        Console.WriteLine($"Listening on port {boundPort}. Ctrl+C to stop.");

        _ = TickAsync(cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient connection = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = HandleAsync(connection, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Ordinary shutdown, same as the tick loop's. Being asked to stop is not a
            // fault, and leaving it as one means nobody can await this task to find out
            // when the server has actually let go of its port and its database.
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// The world's own clock.
    /// <para>
    /// Everything else here happens because a client asked for it. This is the one loop
    /// that runs whether or not anybody says anything, which is what lets the people on
    /// a map move while a player stands still and watches them.
    /// </para>
    /// </summary>
    private async Task TickAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMilliseconds(200);

        try
        {
            using var timer = new PeriodicTimer(interval);

            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                List<Outgoing> moved = world.Tick(Now);

                if (moved.Count > 0)
                    await DispatchAsync(moved, 0, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Ordinary shutdown.
        }
    }

    private async Task HandleAsync(TcpClient connection, CancellationToken cancellationToken)
    {
        // Movement is small and frequent; batching it would only add latency.
        connection.NoDelay = true;

        using (connection)
        await using (NetworkStream stream = connection.GetStream())
        {
            var channel = new MessageChannel(stream);
            int playerId = 0;
            long accountId = 0;

            try
            {
                while (await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false) is { } message)
                {
                    switch (message)
                    {
                        case RegisterRequest or LoginRequest when playerId == 0:
                            AuthOutcome outcome = message switch
                            {
                                RegisterRequest register => await store
                                    .RegisterAsync(register.Username, register.Password, world.FreshCharacter(), cancellationToken)
                                    .ConfigureAwait(false),

                                LoginRequest login => await store
                                    .LoginAsync(login.Username, login.Password, cancellationToken)
                                    .ConfigureAwait(false),

                                _ => new AuthOutcome.Failed("Unknown request."),
                            };

                            if (outcome is AuthOutcome.Failed failed)
                            {
                                Console.WriteLine($"? refused a login: {failed.Reason}");
                                await channel.SendAsync(new AuthFailed(failed.Reason), cancellationToken).ConfigureAwait(false);
                                break;
                            }

                            var success = (AuthOutcome.Success)outcome;
                            accountId = success.Account.Id;

                            (ServerPlayer player, List<Outgoing> welcome) =
                                world.Join(accountId, success.Account.Username, success.Character);

                            playerId = player.Id;

                            Console.WriteLine(
                                $"+ {player.Name} (#{player.Id}) at {player.Square}, " +
                                $"{player.Party.Count} in party, {world.PlayerCount} online");

                            // Everything meant for this player alone goes out before the
                            // channel is registered, and the welcome is the first of it.
                            //
                            // Registering first was a race the world could win: it ticks
                            // five times a second, and a person wandering on the starting
                            // map could have their step broadcast down this socket in
                            // front of the welcome. The client reads exactly one message
                            // to decide whether it is logged in, so what it saw was a
                            // successful login reported as "the server said something
                            // unexpected".
                            foreach (Outgoing mine in welcome.Where(o => o.OnlyTo == playerId))
                                await channel.SendAsync(mine.Message, cancellationToken).ConfigureAwait(false);

                            _channels[playerId] = channel;

                            await DispatchAsync(
                                    welcome.Where(o => o.OnlyTo != playerId).ToList(),
                                    playerId,
                                    cancellationToken)
                                .ConfigureAwait(false);
                            break;

                        case BattleTurn turn when playerId != 0:
                            List<Outgoing> battleResult = world.TakeBattleTurn(playerId, turn.Action);

                            foreach (Outgoing outgoing in battleResult)
                            {
                                if (outgoing.Message is BattleUpdate battleUpdate)
                                {
                                    foreach (BattleEvent battleEvent in battleUpdate.Events)
                                    {
                                        // Said out loud because "I saw no experience" and
                                        // "no experience was awarded" are different
                                        // problems with the same symptom.
                                        if (battleEvent is BattleEvent.ExperienceGained gained)
                                            Console.WriteLine($"+ #{playerId} gained {gained.Amount} exp");

                                        if (battleEvent is BattleEvent.LevelledUp grew)
                                            Console.WriteLine($"^ #{playerId} reached level {grew.Level}");

                                        if (battleEvent is BattleEvent.MoveLearned learned)
                                            Console.WriteLine($"^ #{playerId} learned move {learned.MoveId}");
                                    }
                                }

                                if (outgoing.Message is not BattleFinished finished) continue;

                                Console.WriteLine(
                                    $"= #{playerId} battle over: {finished.Winner?.ToString() ?? "draw"}" +
                                    $"{(finished.Caught ? ", caught it" : "")}, {finished.Party.Count} in party");

                                // Written the moment a battle ends rather than on
                                // disconnect, because disconnects are not always polite.
                                if (world.Snapshot(playerId) is { } state)
                                    await store.SaveAsync(accountId, state, cancellationToken).ConfigureAwait(false);
                            }

                            await DispatchAsync(battleResult, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case TalkRequest talk when playerId != 0:
                            List<Outgoing> talked = world.StartTalking(playerId, talk.LocalId);

                            // Four outcomes, three of which look the same from the
                            // player's side. Only the server knows which it was.
                            if (world.LastTalkOutcome is { } talkOutcome)
                                Console.WriteLine($"* #{playerId} talked to {talk.LocalId}: {talkOutcome}");

                            await DispatchAsync(talked, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case TalkFinished when playerId != 0:
                            world.StopTalking(playerId);
                            break;

                        case ScriptRan ran when playerId != 0:
                            world.RunScript(playerId, ran);

                            if (ran.Set.Count + ran.Cleared.Count + ran.Written.Count > 0)
                            {
                                Console.WriteLine(
                                    $"* #{playerId} ran a script: " +
                                    $"{ran.Set.Count} flags set, {ran.Cleared.Count} cleared, " +
                                    $"{ran.Written.Count} variables written");
                            }

                            break;

                        case UseItemRequest use when playerId != 0:
                            await DispatchAsync(
                                world.UseItem(playerId, use.ItemId, use.Slot), playerId, cancellationToken)
                                .ConfigureAwait(false);
                            break;

                        case BuyRequest buy when playerId != 0:
                            await DispatchAsync(world.Buy(playerId, buy.ItemId, buy.Count), playerId, cancellationToken)
                                .ConfigureAwait(false);
                            break;

                        case SellRequest sell when playerId != 0:
                            await DispatchAsync(world.Sell(playerId, sell.ItemId, sell.Count), playerId, cancellationToken)
                                .ConfigureAwait(false);
                            break;

                        case MoveRequest move when playerId != 0:
                            int grassBefore = world.GrassSteps;
                            List<Outgoing> result = world.Move(playerId, move.Direction, Now);

                            bool met = false;

                            foreach (Outgoing outgoing in result)
                            {
                                if (outgoing.Message is BattleStarted started)
                                {
                                    met = true;
                                    Console.WriteLine(
                                        $"! #{playerId} met species {started.Opponent.Species} " +
                                        $"at level {started.Opponent.Level}");
                                }
                            }

                            // Reporting the misses too is the only way to tell "grass
                            // is not being detected" from "the roll simply failed",
                            // and at a typical rate most steps in grass do fail.
                            if (!met && world.GrassSteps > grassBefore)
                                Console.WriteLine($"~ #{playerId} stepped in grass ({world.GrassSteps} so far, nothing appeared)");

                            // With --verbose, every step is reported with the square it
                            // landed on and what that square is. Silence otherwise says
                            // nothing: a client that never sends a move and a player
                            // who never crosses grass look the same.
                            foreach (Outgoing outgoing in result)
                            {
                                if (outgoing.Message is MapChanged changed && outgoing.OnlyTo == playerId)
                                {
                                    Console.WriteLine(
                                        $"> #{playerId} moved to {changed.MapId} at ({changed.X}, {changed.Y})");
                                }
                            }

                            if (world.LastEdgeRefusal is { } refusal)
                            {
                                Console.WriteLine($"| #{playerId} could not cross: {refusal}");
                            }

                            if (world.LastSightRefusal is { } unseen)
                            {
                                Console.WriteLine($"| #{playerId} was not challenged: {unseen}");
                            }

                            if (verbose && world.Find(playerId) is { } walker)
                            {
                                Console.WriteLine(
                                    $"  #{playerId} {move.Direction} -> {walker.MapId} {walker.Square} " +
                                    $"behaviour 0x{world.MapOf(walker.MapId)?.BehaviourAt(walker.Square) ?? 0:X2}");
                            }

                            await DispatchAsync(result, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        default:
                            await channel
                                .SendAsync(new Rejected("Log in first."), cancellationToken)
                                .ConfigureAwait(false);
                            break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException)
            {
                // A client vanishing mid-frame is ordinary, not an error worth a trace.
            }
            finally
            {
                if (playerId != 0)
                {
                    // Written before the player is removed, because a snapshot needs
                    // them still in the world. A crash between here and the last save
                    // costs whatever happened since — which is why catching saves
                    // immediately rather than waiting for a clean disconnect.
                    if (world.Snapshot(playerId) is { } state)
                    {
                        try
                        {
                            await store.SaveAsync(accountId, state, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"! could not save #{playerId}: {ex.Message}");
                        }
                    }

                    _channels.TryRemove(playerId, out _);
                    await DispatchAsync(world.Leave(playerId), playerId, CancellationToken.None).ConfigureAwait(false);
                    Console.WriteLine($"- #{playerId} left, {world.PlayerCount} online");
                }
            }
        }
    }

    private async Task DispatchAsync(List<Outgoing> outgoing, int sender, CancellationToken cancellationToken)
    {
        foreach (Outgoing item in outgoing)
        {
            foreach ((int id, MessageChannel channel) in _channels)
            {
                if (item.OnlyTo is { } only && only != id) continue;
                if (item.Except is { } except && except == id) continue;

                // A world of 425 maps would behave like one enormous room without
                // this — every step anyone took anywhere, sent to everyone.
                if (item.OnMap is { } scope && world.MapIdOf(id) != scope) continue;

                try
                {
                    await channel.SendAsync(item.Message, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
                {
                    // A send failing means that connection is gone; its own loop will
                    // clean it up. One dead client must not stop the broadcast.
                }
            }
        }
    }
}
