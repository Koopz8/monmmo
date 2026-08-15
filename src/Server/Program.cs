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
        // Your bedroom, which is where this game begins and not where it used to put
        // anybody. It is a choice rather than a derivation, and worth saying so: nothing
        // in the map data marks the player's own house, so this is 4.1 because 4.1 is
        // the room with the bed, the television and the stairs down into 4.0, whose door
        // opens onto Pallet Town four squares from where the professor stops you.
        //
        // --map moves it, and --spawn puts you on a particular square of it.
        string startingMap = ArgumentValue(args, "--map") ?? "4.1";
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
            game = new GameWorld(world, startingMap, rules, startingSquare: Square(ArgumentValue(args, "--spawn")));
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

        Console.WriteLine($"Loaded {world.Count} maps from {Path.GetFullPath(worldPath)}");
        Console.WriteLine(
            $"Starting players on {game.StartingMap.Name} ({game.StartingMap.Id}) — " +
            $"{game.StartingMap.Width}x{game.StartingMap.Height}, on {game.StartingSquare}");

        ReportWorldLinks(world);
        ReportStartingMapLinks(game);
        ReportEncounterReadiness(game.StartingMap);
        ReportTrainerReadiness(world, rules, game.StartingMap.Id);
        ReportReach(world, game.StartingMap.Id, args.Contains("--gates"));

        using var store = new SqlitePlayerStore(databasePath);
        Console.WriteLine($"Accounts in {Path.GetFullPath(databasePath)}");

        if (ArgumentValue(args, "--wipe") is { } wiping)
        {
            bool wiped = await store.WipeAsync(wiping, game.FreshCharacter());

            Console.WriteLine(
                wiped
                    ? $"  {wiping} is a new character again, keeping the login"
                    : $"  no character called {wiping} — nothing wiped");
        }

        // An operator's shortcut past the first hour, and it earned its keep the
        // afternoon it was written: every check on a battle, a move menu or a story beat
        // past the lab meant driving the whole opening again to get a party, and the
        // opening takes a quarter of an hour to play. It hands something over; it does
        // not decide anything a player could not have reached honestly.
        // Who may run the console, named here and nowhere else. An empty set refuses
        // everybody, which is what a server nobody has thought about should do.
        foreach (string name in ArgumentValues(args, "--operator"))
        {
            game.Operators.Add(name);
            Console.WriteLine($"  {name} may use the console");
        }

        if (ArgumentValue(args, "--give") is { } giving)
        {
            if (Gift(giving) is not { } gift)
            {
                Console.WriteLine($"  --give wants name,species,level — \"{giving}\" is not that");
            }
            else
            {
                bool given = await store.GiveAsync(gift.Name, gift.Species, gift.Level);

                Console.WriteLine(
                    given
                        ? $"  {gift.Name} has been given species {gift.Species} at level {gift.Level}"
                        : $"  no character called {gift.Name} — nothing given");
            }
        }

        // Before anybody connects, because the state being thrown away is state a
        // connected character is holding in memory and would write straight back.
        if (ArgumentValue(args, "--forget") is { } forgetting)
        {
            int forgotten = await store.ForgetStoryAsync(forgetting, game.FreshCharacter());

            Console.WriteLine(
                forgotten < 0
                    ? $"  no character called {forgetting} — nothing forgotten"
                    : $"  {forgetting} has forgotten {forgotten} flags and variables; the story starts over");
        }

        await new GameServer(game, store, verbose).RunAsync(port);
        return 0;
    }

    /// <summary>Reads a <c>name,species,level</c> argument, or nothing when it is not one.</summary>
    private static (string Name, int Species, int Level)? Gift(string text) =>
        text.Split(',', StringSplitOptions.TrimEntries) is [string name, string species, string level] &&
        name.Length > 0 &&
        int.TryParse(species, out int which) && which > 0 &&
        int.TryParse(level, out int at) && at is > 0 and <= 100
            ? (name, which, at)
            : null;

    /// <summary>Reads an <c>x,y</c> argument, or nothing when it was not given or is not one.</summary>
    private static GridPosition? Square(string? text) =>
        text?.Split(',', StringSplitOptions.TrimEntries) is [string x, string y] &&
        int.TryParse(x, out int atX) && int.TryParse(y, out int atY)
            ? new GridPosition(atX, atY)
            : null;

    /// <summary>
    /// How much of the world a new character can actually walk to.
    /// <para>
    /// Printed at startup beside everything else that would otherwise be an impression.
    /// "The story stops somewhere" is not a fact anybody can act on; "sixty maps of four
    /// hundred and twenty-five, and here is the first tree" is.
    /// </para>
    /// <para>
    /// It counts only what walking can open. A guard who steps aside when a flag is set is
    /// a wall to this, so the frontier it reports is the *earliest* place the world closes
    /// and the real one is never nearer.
    /// </para>
    /// </summary>
    private static void ReportReach(WorldData world, string startingMapId, bool gates = false)
    {
        Reach reach = WorldWalker.Walk(world, startingMapId);

        Console.WriteLine(
            $"  {reach.Maps.Count} of {world.Count} maps are walkable from {startingMapId} " +
            $"with no move, no flag and nobody stepping aside");

        // And with the ledges treated as the walls they are in the block data, which is
        // what this game did until they were hopped. Reported as a difference rather
        // than as a number, because the number on its own says nothing: the point is
        // that one behaviour byte is the difference between a corner of the map and a
        // country.
        Reach walled = WorldWalker.Walk(world, startingMapId, hops: new Dictionary<byte, Direction>());

        Console.WriteLine(
            $"  {reach.Maps.Count - walled.Maps.Count} of those are behind a ledge, " +
            $"and {WorldWalker.Walk(world, startingMapId, throughPeople: true).Maps.Count - WorldWalker.Walk(world, startingMapId, throughPeople: true, hops: new Dictionary<byte, Direction>()).Maps.Count} " +
            "would be if nobody stood in a doorway");

        // And with the three the game can already teach. The difference is what the
        // obstacle work is worth in maps rather than in objects, which is the unit
        // anybody planning the rest of this actually cares about.
        int[] field = [.. world.Maps.SelectMany(m => m.Objects).Where(o => o.IsObstacle).Select(o => o.ShiftedBy).Distinct()];

        if (field.Length > 0)
        {
            Reach shifted = WorldWalker.Walk(world, startingMapId, field);

            Console.WriteLine(
                $"  {shifted.Maps.Count} with moves {string.Join(", ", field.Order())} — " +
                $"{shifted.Maps.Count - reach.Maps.Count} more maps, and {shifted.Blocked.Count} still in the way");
        }

        Console.WriteLine(
            $"  {reach.People.Count} people are in the way somewhere, which is not the same as " +
            "being a gate — most of them are standing in the open");

        // What the opening is worth, in the only unit that means anything here. A new
        // game sets flags before the first frame and almost every one of them hides
        // somebody; walked without them, everybody who arrives later is already standing
        // there, and the difference is how much of the world that was quietly costing.
        if (world.FlagsAtStart.Count > 0)
        {
            Reach crowded = WorldWalker.Walk(world, startingMapId, flagsSet: []);

            Console.WriteLine(
                $"  {world.FlagsAtStart.Count} flags are set before the first frame; without them " +
                $"{crowded.People.Count - reach.People.Count} more people stand in the way " +
                $"and {reach.Maps.Count - crowded.Maps.Count} maps close");
        }

        // Who is actually costing something, asked properly: walk again as if each of
        // them were not there and see what opens. It is one walk per person, so it is
        // behind a flag rather than in everybody's startup — but it is the question the
        // roadmap keeps wanting an answer to, and the answer is rarely who you would
        // guess. Two fossils lying in a corridor in MT. MOON were worth 137 maps each.
        if (gates)
        {
            var worth = new List<(Standing Who, int Maps)>();

            foreach (Standing who in reach.People)
            {
                Reach without = WorldWalker.Walk(world, startingMapId, asIfGone: [(who.MapId, who.LocalId)]);

                if (without.Maps.Count > reach.Maps.Count)
                    worth.Add((who, without.Maps.Count - reach.Maps.Count));
            }

            Console.WriteLine(
                worth.Count == 0
                    ? "    and none of them opens anything on their own"
                    : $"    {worth.Count} of them open something on their own:");

            foreach ((Standing who, int maps) in worth.OrderByDescending(w => w.Maps).Take(8))
            {
                Console.WriteLine(
                    $"      {who.MapId,-6} {world.Find(who.MapId)?.Name,-16} {who.Square} " +
                    $"object {who.LocalId}: {maps} more maps");
            }
        }

        // What is behind somebody who is not there yet.
        //
        // The walker measures walls, and the largest thing in this game's way is not a
        // wall. Sixty-six maps sit behind two sleepers; the sleepers were solved four
        // milestones ago; what is left is the POKé FLUTE, and the flute is behind MR.
        // FUJI, and MR. FUJI is hidden by a flag the opening sets before the first
        // frame. A report that only counts walls will never say that, and this project
        // spent two roadmaps planning around a measurement of the wrong thing.
        //
        // So: everything anybody hands over, and whether the one holding it is on the
        // map at all for a player who has just started.
        List<(string MapId, MapObject Who)> givers =
        [
            .. world.Maps.SelectMany(m => m.Objects.Select(o => (m.Id, o)))
                .Where(g => g.o.GivesItemId != 0)
        ];

        List<(string MapId, MapObject Who)> away =
        [
            .. givers
                .Where(g => reach.Maps.Contains(g.MapId))
                .Where(g => !g.Who.IsHereFor(world.FlagsAtStart.Contains))
        ];

        Console.WriteLine(
            $"  {givers.Count} people hand something over; {away.Count} of them are on a map " +
            "you can already reach and are not there yet");

        foreach ((string mapId, MapObject who) in away.OrderBy(a => a.Who.GivesItemId).Take(8))
        {
            Console.WriteLine(
                $"    {mapId,-6} {world.Find(mapId)?.Name,-16} ({who.X}, {who.Y}) " +
                $"holds item {who.GivesItemId}, hidden by flag 0x{who.HiddenBy:X4}");
        }

        var byMove = reach.Blocked
            .GroupBy(b => b.ShiftedBy)
            .OrderByDescending(g => g.Count())
            .ToList();

        foreach (var move in byMove)
            Console.WriteLine($"    {move.Count(),3} things in the way need move {move.Key}, e.g. {move.First()}");

        // And as though nobody were standing in a doorway. The difference between this
        // and the line above is the share of the world gated on scripts rather than on
        // geometry — a person in a doorway is a wall to a walker and a wall a script
        // opens in the game.
        Reach past = WorldWalker.Walk(world, startingMapId, field, throughPeople: true);

        Console.WriteLine(
            $"  {past.Maps.Count} if nobody stood in a doorway — so {past.Maps.Count - reach.Maps.Count} " +
            $"maps are behind a person, and {world.Count - past.Maps.Count} are behind something else " +
            $"(water, or a door nobody can stand on)");

        // And with SURF, both on its own and alongside the obstacle moves, because the
        // two are not the same question and the difference between them is the answer
        // to "what is water actually worth".
        //
        // Worth reading twice: until water became a wall, this walk crossed the sea. The
        // 231 maps an earlier roadmap called walkable were walkable by strolling out
        // onto the harbour at VERMILION, and the ordering that document argued for was
        // argued from a number produced by walking on water.
        Reach afloat = WorldWalker.Walk(world, startingMapId, surfing: true);
        Reach both = WorldWalker.Walk(world, startingMapId, field, surfing: true);

        Console.WriteLine(
            $"  {afloat.Maps.Count} with SURF alone, {both.Maps.Count} with SURF and those moves together");

        // Everything at once, which is the number that names the next gate. Whatever is
        // still out of reach with the water crossed, the obstacles shifted and nobody
        // standing in a doorway is behind something this project has not modelled at all.
        Reach everything = WorldWalker.Walk(world, startingMapId, field, throughPeople: true, surfing: true);

        Console.WriteLine(
            $"  {everything.Maps.Count} with all of it — so {world.Count - everything.Maps.Count} maps " +
            "are behind something other than water, an obstacle or a person");

        // And the question that turns a list into a direction: is there a door into it
        // from somewhere already reached? A map with one is a map this walk failed to
        // follow into. A map without one is genuinely somewhere else.
        var doorsInto = new Dictionary<string, int>();

        foreach (MapData from in world.Maps.Where(m => everything.Maps.Contains(m.Id)))
        {
            foreach (Warp warp in from.Warps)
            {
                if (everything.Maps.Contains(warp.TargetMapId)) continue;

                doorsInto[warp.TargetMapId] = doorsInto.GetValueOrDefault(warp.TargetMapId) + 1;
            }
        }

        Console.WriteLine(
            $"    {doorsInto.Count} of them have a door leading in from somewhere already reached, " +
            $"{world.Count - everything.Maps.Count - doorsInto.Count} have none at all");

        // Only the ones with a door in are the frontier. A map reached only from another
        // unreached map is behind those, not beside them, and listing it says nothing.
        foreach (MapData missing in world.Maps
                     .Where(m => !everything.Maps.Contains(m.Id) && doorsInto.ContainsKey(m.Id)))
        {
            Console.WriteLine($"    {missing.Id} {missing.Name} — {doorsInto[missing.Id]} doors in");

            // Every door into it, with where it lands and whether that is somewhere a
            // person could be. A door the walker refused is either a door it never stood
            // on or a door that puts you inside a wall, and those are different problems.
            CollisionGrid inside = missing.ToGrid(surfing: true);

            foreach ((MapData from, Warp warp) in world.Maps
                         .Where(m => everything.Maps.Contains(m.Id))
                         .SelectMany(m => m.Warps.Where(w => w.TargetMapId == missing.Id).Select(w => (m, w)))
                         .Take(4))
            {
                GridPosition lands = WorldWalker.Arrival(missing, warp, inside);

                CollisionGrid outside = from.ToGrid(surfing: true);

                bool stoodOn = everything.Stood.Contains((from.Id, warp.Square));

                Console.WriteLine(
                    $"      from {from.Id} {from.Name} at {warp.Square} " +
                    $"(warp {warp.TargetWarpId}) -> {lands}, " +
                    $"{(inside.IsWalkable(lands) ? "standable" : "SOLID")}; " +
                    $"door {(outside.IsWalkable(warp.Square) ? "standable" : "SOLID")}; " +
                    $"{(stoodOn ? "and it was stood on" : "NEVER STOOD ON")}");
            }
        }

        // And the doors that are on no square at all. A script can warp somebody
        // anywhere, so the boats and the lifts are doors this walk has never had — and
        // the "none at all" figure above was only ever a statement about map records.
        //
        // The answer is a negative one and is printed anyway. All the scripted doors in
        // this cartridge lead somewhere a doorway already leads, so whatever carries a
        // player to the islands is not a warp written in script: it is one written in
        // code, which is the boundary this project cannot read across.
        Reach sailing = WorldWalker.Walk(
            world, startingMapId, field, throughPeople: true, surfing: true, throughScriptedDoors: true);

        int scripted = world.Maps.Sum(m => m.Doors.Count);

        Console.WriteLine(
            $"    {scripted} doors are made by scripts rather than by squares; walking those as well " +
            $"reaches {sailing.Maps.Count} — {sailing.Maps.Count - everything.Maps.Count} more");

        // What the unreached maps are, once. They are not a list of odds and ends: they
        // are other worlds, and counting them as such is the difference between "some
        // maps are missing" and "there is an archipelago nothing sails to".
        //
        // One walk per piece rather than one per map: whatever the first unassigned map
        // reaches is a piece, and everything it reached is answered for.
        var accounted = new HashSet<string>(sailing.Maps);
        var pieces = new List<(string Id, string Name, int Size)>();

        foreach (MapData island in world.Maps)
        {
            if (accounted.Contains(island.Id)) continue;

            Reach piece = WorldWalker.Walk(
                world, island.Id, field, throughPeople: true, surfing: true, throughScriptedDoors: true);

            // Only what this piece adds. A walk starting inside one piece can still see
            // out into the first world through a one-way door, and counting those again
            // would make the pieces add up to more than the world.
            foreach (string id in piece.Maps) accounted.Add(id);

            pieces.Add((island.Id, island.Name, piece.Maps.Count));
        }

        if (pieces.Count > 0)
        {
            Console.WriteLine($"    the rest is {pieces.Count} separate pieces on foot");

            foreach ((string id, string name, int size) in pieces.OrderByDescending(p => p.Size).Take(4))
                Console.WriteLine($"      {size,3} maps from {id} {name}");
        }

        // And the boat, which is the only thing that joins any of them. Ten docks,
        // numbered by the scripts that call them and nothing else — see Ferries.
        List<MapData> docks = [.. world.Maps.Where(m => m.Ferry is not null).OrderBy(m => m.Ferry!.Number)];

        if (docks.Count > 0)
        {
            Reach byBoat = WorldWalker.Walk(
                world, startingMapId, field, throughPeople: true, surfing: true,
                throughScriptedDoors: true);

            var everywhere = new HashSet<string>(byBoat.Maps);

            // Every dock is one crossing from every other, so the moment one of them is
            // reachable all of them are — which makes this a single pass rather than a
            // search.
            if (docks.Any(d => everywhere.Contains(d.Id)))
            {
                foreach (MapData dock in docks)
                {
                    Reach from = WorldWalker.Walk(
                        world, dock.Id, field, throughPeople: true, surfing: true,
                        throughScriptedDoors: true, startSquare: dock.Ferry!.Arrival);

                    foreach (string id in from.Maps) everywhere.Add(id);
                }
            }

            Console.WriteLine(
                $"    the boat calls at {docks.Count} of them; with it, {everywhere.Count} of {world.Count} " +
                $"maps are reachable — {everywhere.Count - byBoat.Maps.Count} more");

            foreach (MapData dock in docks)
                Console.WriteLine($"      {dock.Ferry!.Number,2}  {dock.Id,-6} {dock.Name}");

            // And the ticket it asks for, which is a gate with no key anywhere in this
            // cartridge — so it is carried and reported rather than enforced. See
            // GameWorld.HasAPass, where the reason is written down.
            foreach (FerryPass pass in world.FerryPasses)
            {
                bool obtainable = world.ItemsHandedOut.Contains(pass.ItemId);

                Console.WriteLine(
                    $"      it asks for {pass} — " +
                    (obtainable ? "which something on a map hands over" : "which nothing anywhere hands over"));
            }

            if (world.FerryPasses.Count > 0 && !world.AnyPassCanBeHadHere)
                Console.WriteLine("      so the boat is not gated: a gate with no key is a wall");
        }

        if (reach.Beyond.Count > 0)
            Console.WriteLine($"    {reach.Beyond.Count} doors lead to maps this world file does not have");
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
            .Where(o => o.IsTrainer || o.CanBeFought)
            .ToList();

        if (trainers.Count == 0)
        {
            Console.WriteLine("  no trainers in this world file — re-export it if you want anybody to challenge you");
            return;
        }

        int named = trainers.Count(o => o.CanBeFought);
        int seeing = trainers.Count(o => o.SightRange > 0);

        // Counted apart because they are two different things. A line of sight is how
        // most fights start; a gym leader has none and is fought by being talked to, and
        // for a while that difference was the difference between BROCK fighting and
        // BROCK saying seven pages and going quiet.
        int spoken = trainers.Count(o => o.CanBeFought && o.SightRange == 0);

        Console.WriteLine(
            $"  {trainers.Count} trainers on maps, {named} of them naming a trainer id, " +
            $"{seeing} with a line of sight, {spoken} fought by talking to them");

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
                $"Rules from {Path.GetFullPath(path)}: {rules.SpeciesCount} species, " +
                $"{rules.MoveCount} moves, {rules.LearnsetCount} learnsets, " +
                $"{rules.TrainerCount} trainers");

            // What beating each one is worth. Said out loud because a rules file exported
            // before this existed loads perfectly well and quietly hands out nothing —
            // which from the outside is a party that never gets stronger for its fights.
            List<SpeciesData> yielding = [.. rules.AllSpecies.Where(s => s.EvTotal > 0)];

            Console.WriteLine(
                yielding.Count == 0
                    ? "  none of them yields effort — re-export the rules file to hand any out"
                    : $"  {yielding.Count} of them yield effort, " +
                      $"{yielding.Count(s => s.EvTotal > 1)} more than a single point, " +
                      $"{yielding.Count(s => Effort.Order.Count(t => s.EvYield(t) > 0) > 1)} across more than one stat");

            return rules;
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine($"Could not read {Path.GetFullPath(path)}: {ex.Message}");
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

    /// <summary>Every value given for a flag, because some may be given more than once.</summary>
    private static IEnumerable<string> ArgumentValues(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) yield return args[i + 1];
    }
}

/// <summary>Accepts connections and fans messages out to them.</summary>
public sealed class GameServer(GameWorld world, IPlayerStore store, bool verbose = false)
{
    private readonly ConcurrentDictionary<int, MessageChannel> _channels = new();

    /// <summary>How many people may be having their password checked at once.</summary>
    private readonly Doorway _door = new();
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

    /// <summary>
    /// What one password check cost when it was last measured, in milliseconds.
    /// <para>
    /// <b>Modelled, and it is a measurement rather than a rule</b> — it is only used to
    /// turn the door's width into a rate for the startup line. The machine this runs on
    /// decides the real number, and a server on better hardware than the one this was
    /// measured on will quietly beat its own report.
    /// </para>
    /// </summary>
    private const double MeasuredCheckMilliseconds = 91;

    public async Task RunAsync(int port, CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        int boundPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        _listening.TrySetResult(boundPort);

        Console.WriteLine($"Listening on port {boundPort}. Ctrl+C to stop.");

        // What the door can do, before anybody is at it. This is the number that decides
        // whether a launch works, and it is invisible from inside a running server.
        Console.WriteLine($"  {_door.Rate(MeasuredCheckMilliseconds)}");

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

            // When this character was last written down, so that a run of anything does
            // not rewrite the same row several times a second.
            double lastSaved = double.NegativeInfinity;

            try
            {
                while (await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false) is { } message)
                {
                    switch (message)
                    {
                        case RegisterRequest or LoginRequest when playerId == 0:
                            // Through the door, which is the only bounded thing in this
                            // server and the only one that has to be: checking a password
                            // costs a hundred times what anything else here costs, and
                            // nineteen megabytes for as long as it takes.
                            AuthOutcome outcome = await _door.AdmitAsync(async () => message switch
                            {
                                RegisterRequest register => await store
                                    .RegisterAsync(register.Username, register.Password, world.FreshCharacter(), cancellationToken)
                                    .ConfigureAwait(false),

                                LoginRequest login => await store
                                    .LoginAsync(login.Username, login.Password, cancellationToken)
                                    .ConfigureAwait(false),

                                _ => new AuthOutcome.Failed("Unknown request."),
                            }, cancellationToken).ConfigureAwait(false);

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

                            if (world.TakeSilence() is { } quiet) Console.WriteLine($"? {quiet}");

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

                                        // For the same reason as experience: confusion
                                        // is a one-in-five rider on a move that also
                                        // does damage, and "I never saw it" and "it
                                        // never happened" look identical from a screen.
                                        if (battleEvent is BattleEvent.Confused confused)
                                            Console.WriteLine($"? #{playerId} confused {confused.Side}");
                                    }
                                }

                                if (outgoing.Message is not BattleFinished finished) continue;

                                Console.WriteLine(
                                    $"= #{playerId} battle over: {finished.Winner?.ToString() ?? "draw"}" +
                                    $"{(finished.Caught ? ", caught it" : "")}, {finished.Party.Count} in party");

                                if (world.LastPrize is { } prize) Console.WriteLine($"+ #{playerId} {prize}");

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
                        {
                            List<Outgoing> done = world.StopTalking(playerId);

                            if (done.Count > 0)
                                await DispatchAsync(done, playerId, cancellationToken).ConfigureAwait(false);

                            break;
                        }

                        case SceneCast cast when playerId != 0:
                            world.HoldSceneCast(playerId, cast.LocalIds, Now);

                            if (world.LastSceneCast is { } held)
                                Console.WriteLine($"* #{playerId} scene cast: {held}");

                            break;

                        case ScenePlaced placed when playerId != 0:
                            List<Outgoing> after = world.PlaceAfterScene(
                                playerId, placed.LocalId, new GridPosition(placed.X, placed.Y), placed.Facing, Now);

                            if (world.LastScenePlacement is { } placement)
                                Console.WriteLine($"* #{playerId} scene: {placement}");

                            await DispatchAsync(after, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case TriggerFired stepped when playerId != 0:
                            List<Outgoing> fired = world.FireTrigger(playerId, stepped.X, stepped.Y, stepped.TrainerId, Now);

                            if (world.LastTriggerOutcome is { } triggerOutcome)
                            {
                                Console.WriteLine(
                                    $"* #{playerId} stood on ({stepped.X}, {stepped.Y}): {triggerOutcome}");
                            }

                            await DispatchAsync(fired, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case ScriptFought fought when playerId != 0:
                            List<Outgoing> ambush = world.ScriptFought(
                                playerId, fought.LocalId, fought.Species, fought.Level);

                            if (world.LastScriptFight is { } startedBy)
                                Console.WriteLine($"! #{playerId} a script started a fight: {startedBy}");

                            await DispatchAsync(ambush, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case ScriptGave gave when playerId != 0:
                            List<Outgoing> fromScript = world.ScriptGave(playerId, gave.LocalId, gave.ItemId);

                            if (world.LastGift is { } scripted) Console.WriteLine($"+ #{playerId} {scripted}");

                            await DispatchAsync(fromScript, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case LearnMoveRequest learning when playerId != 0:
                            List<Outgoing> taught = world.LearnMove(playerId, learning.MoveId, learning.Forget);

                            if (world.LastLearned is { } taught2) Console.WriteLine($"+ #{playerId} {taught2}");

                            await DispatchAsync(taught, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case TradeRequest asking when playerId != 0:
                            List<Outgoing> asked = world.AskToTrade(playerId, asking.WithPlayerId);

                            if (world.LastTrade is { } about) Console.WriteLine($"= #{playerId} trade: {about}");
                            if (world.TakeTradeLog() is { } opened) Console.WriteLine($"= trade {opened}");

                            await DispatchAsync(asked, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case TradeOffer offering when playerId != 0:
                            List<Outgoing> tabled = world.OfferInTrade(playerId, offering.Slot);

                            if (world.TakeTradeLog() is { } moved) Console.WriteLine($"= trade {moved}");

                            await DispatchAsync(tabled, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case TradeConfirm agreeing when playerId != 0:
                            List<Outgoing> agreed = world.ConfirmTrade(playerId, agreeing.Ready);

                            if (world.LastTrade is { } swapped) Console.WriteLine($"= #{playerId} trade: {swapped}");
                            if (world.TakeTradeLog() is { } settled) Console.WriteLine($"= trade {settled}");

                            await DispatchAsync(agreed, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case TradeCancel when playerId != 0:
                            List<Outgoing> off = world.CancelTrade(playerId);

                            if (world.TakeTradeLog() is { } stopped) Console.WriteLine($"= trade {stopped}");

                            await DispatchAsync(off, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case WearRequest wearing when playerId != 0:
                            List<Outgoing> shown = world.Wear(playerId, wearing.CosmeticId, wearing.Slot);

                            if (world.LastWorn is { } dressed) Console.WriteLine($"~ #{playerId} {dressed}");

                            await DispatchAsync(shown, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case ScriptRan ran when playerId != 0:
                            List<Outgoing> handed = world.RunScript(playerId, ran);

                            if (world.LastGift is { } gift)
                                Console.WriteLine($"* #{playerId} was handed {gift}");

                            await DispatchAsync(handed, playerId, cancellationToken).ConfigureAwait(false);

                            if (ran.Set.Count + ran.Cleared.Count + ran.Written.Count > 0)
                            {
                                // Named rather than counted. "1 flags set" is true of every
                                // flag in the game, and an hour went into working out which
                                // one had hidden GIOVANNI from a line that could have said.
                                static string Flags(string what, IReadOnlyList<int> which) =>
                                    which.Count == 0
                                        ? ""
                                        : $" {what} {string.Join(",", which.Select(f => $"0x{f:X4}"))}";

                                Console.WriteLine(
                                    $"* #{playerId} ran a script:" +
                                    Flags("set", ran.Set) +
                                    Flags("cleared", ran.Cleared) +
                                    (ran.Written.Count > 0 ? $" wrote {ran.Written.Count} variables" : ""));
                            }

                            break;

                        case UseItemRequest use when playerId != 0:
                            await DispatchAsync(
                                world.UseItem(playerId, use.ItemId, use.Slot), playerId, cancellationToken)
                                .ConfigureAwait(false);
                            break;

                        case GiveItemRequest give when playerId != 0:
                            await DispatchAsync(
                                world.GiveItem(playerId, give.ItemId, give.Slot), playerId, cancellationToken)
                                .ConfigureAwait(false);

                            if (world.LastHandedOver is { } handover) Console.WriteLine($"* #{playerId} {handover}");
                            break;

                        case TakeItemRequest take when playerId != 0:
                            await DispatchAsync(
                                world.TakeItem(playerId, take.Slot), playerId, cancellationToken)
                                .ConfigureAwait(false);

                            if (world.LastHandedOver is { } tookBack) Console.WriteLine($"* #{playerId} {tookBack}");
                            break;

                        case DepositRequest put when playerId != 0:
                            await DispatchAsync(
                                world.Deposit(playerId, put.Slot), playerId, cancellationToken)
                                .ConfigureAwait(false);

                            if (world.LastBoxed is { } stored) Console.WriteLine($"* #{playerId} {stored}");
                            break;

                        case WithdrawRequest out_ when playerId != 0:
                            await DispatchAsync(
                                world.Withdraw(playerId, out_.Slot), playerId, cancellationToken)
                                .ConfigureAwait(false);

                            if (world.LastBoxed is { } fetched) Console.WriteLine($"* #{playerId} {fetched}");
                            break;

                        case SwapPartyRequest swap when playerId != 0:
                            await DispatchAsync(
                                world.SwapParty(playerId, swap.A, swap.B), playerId, cancellationToken)
                                .ConfigureAwait(false);

                            if (world.LastOrdered is { } order) Console.WriteLine($"* #{playerId} {order}");
                            break;

                        case SurfRequest when playerId != 0:
                            await DispatchAsync(world.Surf(playerId), playerId, cancellationToken)
                                .ConfigureAwait(false);

                            if (world.LastSurf is { } afloat) Console.WriteLine($"~ #{playerId} {afloat}");
                            break;

                        case HealRequest when playerId != 0:
                            await DispatchAsync(world.Heal(playerId), playerId, cancellationToken)
                                .ConfigureAwait(false);

                            if (world.LastHeal is { } rested) Console.WriteLine($"+ #{playerId} {rested}");
                            break;

                        case ConsoleCommand typed when playerId != 0:
                            await DispatchAsync(
                                    world.RunConsole(playerId, typed.Text, Now), playerId, cancellationToken)
                                .ConfigureAwait(false);

                            if (world.LastConsole is { } logged) Console.WriteLine($"$ {logged}");

                            // The console deserves the same line the wire gets. Without it a
                            // trade driven from /trade was invisible in the log, which is
                            // twice now that a live ask has vanished with no explanation.
                            if (world.LastTrade is { } console) Console.WriteLine($"= trade: {console}");
                            if (world.TakeTradeLog() is { } byHand) Console.WriteLine($"= trade {byHand}");
                            break;

                        case NameMonRequest named when playerId != 0:
                            await DispatchAsync(
                                    world.NameMon(playerId, named.Slot, named.Name), playerId, cancellationToken)
                                .ConfigureAwait(false);
                            break;

                        case BuyRequest buy when playerId != 0:
                            await DispatchAsync(world.Buy(playerId, buy.ItemId, buy.Count), playerId, cancellationToken)
                                .ConfigureAwait(false);
                            break;

                        case DuelRequest duelling when playerId != 0:
                            List<Outgoing> challenged = world.AskToDuel(playerId, duelling.WithPlayerId);

                            if (world.LastDuel is { } challenge) Console.WriteLine($"% #{playerId} duel: {challenge}");

                            await DispatchAsync(challenged, playerId, cancellationToken).ConfigureAwait(false);
                            break;

                        case SailRequest sail when playerId != 0:
                            List<Outgoing> sailed = world.Sail(playerId, sail.Number, Now);

                            if (world.LastSail is { } crossing) Console.WriteLine($"~ #{playerId} sails {crossing}");

                            await DispatchAsync(sailed, playerId, cancellationToken).ConfigureAwait(false);
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

                            if (world.LastStepRefusal is { } tooFast)
                            {
                                Console.WriteLine($"| #{playerId} step refused, {tooFast}");
                            }

                            if (world.LastEdgeRefusal is { } refusal)
                            {
                                Console.WriteLine($"| #{playerId} could not cross: {refusal}");
                            }

                            if (world.WhySilent(playerId) is { } silent)
                            {
                                Console.WriteLine($"| #{playerId} stood on {silent}");
                            }

                            if (world.LastArrivalScript is { } arrival)
                            {
                                Console.WriteLine($"* #{playerId} {arrival}");
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

                    // Written down here as well as after a battle, because everything
                    // else that lasts happens somewhere in that switch: an item handed
                    // over, a flag a script set, a move taught, money spent. The S.S.
                    // TICKET is the case that made this obvious — it is given by a
                    // conversation, and a server that stopped before the next battle
                    // handed it over for nothing.
                    //
                    // Movement is left out on purpose. It is by far the commonest thing
                    // a client says and much the cheapest to lose: somebody who comes
                    // back where they last did something has lost a walk, not a ticket.
                    if (playerId != 0 && message is not MoveRequest && Now - lastSaved > 1.0
                        && world.Snapshot(playerId) is { } sinceThen)
                    {
                        lastSaved = Now;

                        try
                        {
                            await store.SaveAsync(accountId, sinceThen, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // Best effort, and said out loud. A save that fails quietly
                            // is one that looks like it worked until somebody logs in.
                            Console.Error.WriteLine($"! could not save #{playerId}: {ex.Message}");
                        }
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
