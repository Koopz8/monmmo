using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>One turn of the loop: what was known, and what it opened.</summary>
/// <param name="Round">Which pass this was, counting from one.</param>
/// <param name="Maps">How many maps were reachable with what was known at the start of it.</param>
/// <param name="Flags">How many flags were known at the start of it.</param>
/// <param name="Moves">How many field moves the party could use.</param>
/// <param name="ScriptsRun">How many reachable scripts were run.</param>
/// <param name="NewFlags">Flags this pass turned up that were not known before.</param>
/// <param name="NewMoves">Field moves this pass turned up.</param>
public sealed record ClosureRound(
    int Round,
    int Maps,
    int Flags,
    int Moves,
    int ScriptsRun,
    IReadOnlyList<int> NewFlags,
    IReadOnlyList<int> NewMoves);

/// <summary>Where the world finally closes, and what is standing in the way.</summary>
/// <param name="Rounds">Every pass, so the shape of the opening can be seen.</param>
/// <param name="Reached">Every map a player can get to, at the fixpoint.</param>
/// <param name="Unreached">Every map they cannot.</param>
/// <param name="Flags">Every flag a player can set by playing.</param>
/// <param name="Moves">Every field move they can bring to bear.</param>
/// <param name="Blocked">The frontier: squares that want a move nobody has.</param>
/// <param name="Standing">People in the way who never move.</param>
/// <param name="Specials">
/// The routines the scripts asked for and did not get an answer from, counted. This is the
/// one number that says how much of the answer is untrustworthy rather than how much of the
/// world is shut.
/// </param>
public sealed record Closure(
    IReadOnlyList<ClosureRound> Rounds,
    IReadOnlyCollection<string> Reached,
    IReadOnlyList<string> Unreached,
    IReadOnlyCollection<int> Flags,
    IReadOnlyCollection<int> Moves,
    IReadOnlyList<Frontier> Blocked,
    IReadOnlyList<Standing> Standing,
    IReadOnlyDictionary<int, int> Specials);

/// <summary>
/// How far a player can actually get by playing — the whole way, not one walk.
/// <para>
/// <b>The question this project has never been able to ask.</b> <see cref="WorldWalker"/>
/// answers "given these flags and these moves, where can somebody get", which is one
/// photograph. Playing a game is not one photograph: you walk as far as you can, talk to
/// whoever is there, and what they do opens more world, and then you walk again.
/// </para>
/// <para>
/// So this is that loop, run to a fixpoint. Walk with what is known; find every script a
/// player can actually stand in front of; run it with the flags they have; collect what it
/// sets, hands over and teaches; walk again. Stop when a pass opens nothing.
/// </para>
/// <para>
/// What comes out is the answer to "can this game be finished": the maps a player can reach
/// by playing, the ones they cannot, and what is standing between them.
/// </para>
/// <para>
/// <b>The honest limits, and they are large.</b> A <c>special</c> is a call into the
/// cartridge's own code and this project does not read code — the runner steps over it and
/// the answer variable keeps its zero, which at a great many branching sites makes the script
/// skip what it was about to do. Every badge check in the game is one of those. So the number
/// this produces is a <b>floor</b>: the world is never smaller than this and may be larger.
/// The count of unanswered routines is reported beside it, because a floor with no error bar
/// is a number people will quote as a ceiling.
/// </para>
/// </summary>
public static class StoryClosure
{
    /// <summary>
    /// How many passes before this gives up. <b>Modelled.</b>
    /// <para>
    /// A pass that opens nothing ends the loop, so this is only a backstop against a script
    /// that alternates between setting and clearing the same flag. Twenty is far above the
    /// number of times this game actually widens — the story is a few dozen gates deep, not
    /// hundreds — and reaching it is itself a finding.
    /// </para>
    /// </summary>
    public const int MostRounds = 20;

    /// <summary>
    /// Runs the loop.
    /// </summary>
    /// <param name="world">The exported world.</param>
    /// <param name="startMapId">Where a new character wakes up.</param>
    /// <param name="runScript">
    /// Runs one script with a set of flags and says what it did. Supplied from outside
    /// because running a script needs the cartridge, and this assembly has never had one —
    /// the same split every other part of the server keeps.
    /// </param>
    /// <param name="log">Told about each pass as it happens, since the whole thing is slow.</param>
    public static Closure Walk(
        WorldData world,
        string startMapId,
        Func<uint, IReadOnlyCollection<int>, ScriptOutcome> runScript,
        Action<string>? log = null)
    {
        var flags = new HashSet<int>();
        var moves = new HashSet<int>();
        var specials = new Dictionary<int, int>();
        var rounds = new List<ClosureRound>();

        Reach reach = WorldWalker.Walk(world, startMapId);

        for (int round = 1; round <= MostRounds; round++)
        {
            // Where a player can stand right now, with what they have opened so far.
            reach = WorldWalker.Walk(world, startMapId, moves, flagsSet: flags);

            var stood = reach.Stood.ToHashSet();

            var newFlags = new List<int>();
            var newMoves = new List<int>();
            var ran = 0;

            foreach (MapData map in world.Maps.Where(m => reach.Maps.Contains(m.Id)))
            {
                foreach (uint address in Scripts(map, stood, flags))
                {
                    ran++;

                    ScriptOutcome did = runScript(address, flags);

                    foreach (int flag in did.FlagsSet)
                    {
                        if (flags.Add(flag)) newFlags.Add(flag);
                    }

                    // A flag the story turns off is as much a change as one it turns on, and
                    // the whole middle of this game is flags being cleared.
                    foreach (int flag in did.FlagsCleared) flags.Remove(flag);

                    // A move somebody teaches you is a move you can use on the world. This
                    // is how CUT opens thirty-eight maps.
                    foreach (int move in did.Teaches)
                    {
                        if (moves.Add(move)) newMoves.Add(move);
                    }

                    foreach (int routine in did.Specials)
                        specials[routine] = specials.GetValueOrDefault(routine) + 1;
                }
            }

            rounds.Add(new ClosureRound(
                round, reach.Maps.Count, flags.Count - newFlags.Count, moves.Count - newMoves.Count,
                ran, newFlags, newMoves));

            log?.Invoke(
                $"  pass {round,2}: {reach.Maps.Count,3} maps, {ran,4} scripts run, "
                + $"+{newFlags.Count} flags, +{newMoves.Count} moves");

            // A pass that opened nothing is the fixpoint. Checked on what was learned rather
            // than on the map count, because a flag can be set several passes before the map
            // it opens becomes walkable.
            if (newFlags.Count == 0 && newMoves.Count == 0) break;
        }

        // One last walk, so the reported reach is the one the last pass's flags bought rather
        // than the one that produced them.
        reach = WorldWalker.Walk(world, startMapId, moves, flagsSet: flags);

        return new Closure(
            rounds,
            reach.Maps,
            [.. world.Maps.Select(m => m.Id).Where(id => !reach.Maps.Contains(id)).Order()],
            flags,
            moves,
            reach.Blocked,
            reach.People,
            specials);
    }

    /// <summary>
    /// Every script a player could actually stand in front of on this map.
    /// <para>
    /// <b>Stood on, not merely on the map.</b> A map counts as reached the moment one square
    /// of it is, and a person on the far side of a locked door is on a map you have been to
    /// and is not somebody you can talk to. Counting those would open the whole game on the
    /// first pass and produce a confident, wrong answer.
    /// </para>
    /// <para>
    /// A person is talked to from the square beside them and a trigger is walked onto, so a
    /// person counts if any of the four squares round them was stood on and a trigger counts
    /// if its own square was. An arrival script counts as soon as the map does, because
    /// arriving is what runs it.
    /// </para>
    /// </summary>
    private static IEnumerable<uint> Scripts(
        MapData map, HashSet<(string MapId, GridPosition Square)> stood, HashSet<int> flags)
    {
        foreach (MapObject person in map.Objects)
        {
            if (!person.HasScript) continue;

            // Somebody a flag has taken off the map is not there to be talked to.
            if (!person.IsHereFor(flags.Contains)) continue;

            if (Beside(map.Id, person.Square).Any(stood.Contains)) yield return person.ScriptAddress;
        }

        foreach (MapTrigger trigger in map.Triggers)
        {
            if (trigger.HasScript && stood.Contains((map.Id, trigger.Square)))
                yield return trigger.ScriptAddress;
        }

        // And what running onto the map does, which is where a great deal of this story
        // actually happens — the shop in Viridian hands over the parcel the whole middle of
        // the game turns on, and nobody is being talked to.
        foreach (MapEntryScript entry in map.OnEntry)
        {
            if (entry.ScriptAddress != 0) yield return entry.ScriptAddress;
        }
    }

    private static IEnumerable<(string, GridPosition)> Beside(string mapId, GridPosition at) =>
    [
        (mapId, at),
        (mapId, at with { Y = at.Y - 1 }),
        (mapId, at with { Y = at.Y + 1 }),
        (mapId, at with { X = at.X - 1 }),
        (mapId, at with { X = at.X + 1 }),
    ];
}

/// <summary>
/// What one script did, as far as opening the world goes.
/// <para>
/// Deliberately not <c>ScriptRun</c>. That type lives in the extractor and knows about
/// cartridges; this assembly does not, and the walk above only needs four things. The caller
/// converts, which is the same seam every other part of this server keeps.
/// </para>
/// </summary>
public sealed record ScriptOutcome(
    IReadOnlyList<int> FlagsSet,
    IReadOnlyList<int> FlagsCleared,
    IReadOnlyList<int> Teaches,
    IReadOnlyList<int> Specials);
