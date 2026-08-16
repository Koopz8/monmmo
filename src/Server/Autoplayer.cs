using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>What one script turned out to do, as far as playing the game goes.</summary>
/// <param name="FlagsSet">Flags it turned on.</param>
/// <param name="FlagsCleared">Flags it turned off.</param>
/// <param name="Teaches">Field moves it handed over, already translated from the item.</param>
/// <param name="Specials">Routines it asked for and did not get an answer from.</param>
/// <param name="Gives">
/// A creature it hands over and the level it names, if it hands one over. The level matters:
/// the first version took only the species and put everything in at five, so every gift in the
/// game arrived as a starter.
/// </param>
/// <param name="Fights">A trainer it picks a fight with, if it picks one.</param>
public sealed record PlayedScript(
    IReadOnlyList<int> FlagsSet,
    IReadOnlyList<int> FlagsCleared,
    IReadOnlyList<int> Teaches,
    IReadOnlyList<int> Specials,
    (int Species, int Level)? Gives,
    int? Fights);

/// <summary>
/// A door out of somewhere it reached, into somewhere it never did.
/// <para>
/// The number that matters when a run stops. "246 maps it never got to" is a list nobody can
/// act on; most of those are behind each other, and only a handful sit one step from ground
/// the player is already standing on. Those few are the story's actual walls.
/// </para>
/// </summary>
/// <param name="FromMapId">A map it reached.</param>
/// <param name="Square">The door, so it can be looked at.</param>
/// <param name="ToMapId">Where the door leads, which it never got to.</param>
/// <param name="ToName">That map's name.</param>
/// <param name="CouldStandOnIt">
/// Whether it could actually stand on the door. False means the door was never the problem —
/// something on this side of it was, and the frontier is where to look instead.
/// </param>
public sealed record ShutDoor(
    string FromMapId, GridPosition Square, string ToMapId, string ToName, bool CouldStandOnIt);

/// <summary>Why the playthrough stopped.</summary>
public enum StoppedBecause
{
    /// <summary>Nothing new opened. This is the ordinary end, and the interesting one.</summary>
    NothingMoreOpened,

    /// <summary>The backstop, which means something never settles.</summary>
    ItNeverSettled,

    /// <summary>Every creature it had was beaten and there was nothing left to send out.</summary>
    NobodyLeftToFight,
}

/// <summary>What the run came to.</summary>
public sealed record Attempt(
    int Passes,
    StoppedBecause Stopped,
    IReadOnlyCollection<string> Reached,
    IReadOnlyList<string> Unreached,
    IReadOnlyCollection<int> Flags,
    IReadOnlyCollection<int> Moves,
    IReadOnlyList<SavedMon> Party,
    int FightsWon,
    int FightsLost,
    int FightsSkipped,
    int PartiesHealed,
    IReadOnlyDictionary<int, int> Specials,
    IReadOnlyList<ShutDoor> ShutDoors,
    IReadOnlyList<Frontier> Blocked)
{
    /// <summary>The highest level anything in the party reached, which is the shape of a run.</summary>
    public int HighestLevel => Party.Count == 0 ? 0 : Party.Max(m => m.Level);
}

/// <summary>
/// Plays the game from a fresh save, as far as it can get.
/// <para>
/// <b>Not a rig.</b> <c>tools/rig</c> drives the client — Xvfb, xdotool, screenshots, one
/// keypress at a time — and a whole story that way is tens of thousands of presses through a
/// GUI, every one of them a place for the harness to lose its footing. Its own README records
/// two scripts that each cost a milestone by pressing something helpful at the wrong moment.
/// </para>
/// <para>
/// This drives the server instead. It walks the world with what it has, talks to everybody it
/// can stand in front of, fights whoever picks a fight, and takes what it is given. Then it
/// walks again. When a pass opens nothing it stops and says where it got to.
/// </para>
/// <para>
/// What that buys over playing it by hand is not speed. It is that it gives the same answer
/// twice, and that everything it could not do is a number rather than a memory of an evening.
/// </para>
/// <para>
/// <b>It is a floor.</b> Every limit of <see cref="StoryClosure"/> applies — a routine this
/// project cannot execute answers zero and its callers take the zero arm — plus two of its
/// own: it fights with a flat policy rather than well, and it never buys anything. Where it
/// stops, a person might get further. Where it *gets through*, a person certainly can.
/// </para>
/// </summary>
public static class Autoplayer
{
    /// <summary>How many passes before it gives up. <b>Modelled</b>, same reasoning as the walk.</summary>
    public const int MostPasses = 24;

    /// <summary>
    /// How many turns one fight may take before it is called a loss.
    /// <para>
    /// <b>Modelled.</b> Two parties that cannot hurt each other would otherwise run for ever —
    /// which is a real state, not a hypothetical: a creature with only status moves against
    /// one that resists them is a stalemate the games end by running out of PP, and PP is not
    /// what this loop is testing.
    /// </para>
    /// </summary>
    public const int MostTurns = 300;

    public static Attempt Play(
        WorldData world,
        string startMapId,
        GameRules rules,
        Func<uint, IReadOnlyCollection<int>, PlayedScript> runScript,
        Action<string>? log = null)
    {
        var battles = new BattleFactory(rules);
        var progress = new Progression(rules);

        // Whether anywhere in this world puts a party back on its feet. Every FireRed town
        // has a centre, so this is a fact about the export rather than about the game — and
        // if it is ever false, the run below is measuring a world with no centres in it and
        // should say so rather than quietly playing a much harder game.
        bool heals = world.Maps.Any(m => m.Objects.Any(o => o.Heals));

        var healed = 0;

        // A fresh save is not an empty save — see StoryClosure, and milestone 56. Forty-nine
        // flags are set before the first frame, and every one hides somebody not yet met.
        var flags = new HashSet<int>(world.FlagsAtStart);
        var moves = new HashSet<int>();
        var specials = new Dictionary<int, int>();
        var party = new List<SavedMon>();
        var fought = new HashSet<int>();

        var won = 0;
        var lost = 0;
        var skipped = 0;
        var passes = 0;

        StoppedBecause stopped = StoppedBecause.ItNeverSettled;

        for (int pass = 1; pass <= MostPasses; pass++)
        {
            passes = pass;

            Reach reach = WorldWalker.Walk(world, startMapId, moves, flagsSet: flags);

            var stood = reach.Stood.ToHashSet();

            // What was known when this pass began. Compared at the end rather than watching
            // for additions as they happen — a script that clears a flag another one sets
            // reports something new every pass for ever, which is what put the first real run
            // into its backstop with nothing changing from pass four onwards.
            int flagsWere = flags.Count;
            int movesWere = moves.Count;
            int partyWas = party.Count;

            foreach (MapData map in world.Maps.Where(m => reach.Maps.Contains(m.Id)))
            {
                foreach (uint address in Reachable(map, stood, flags))
                {
                    PlayedScript did = runScript(address, flags);

                    foreach (int routine in did.Specials)
                        specials[routine] = specials.GetValueOrDefault(routine) + 1;

                    foreach (int flag in did.FlagsSet) flags.Add(flag);

                    foreach (int flag in did.FlagsCleared) flags.Remove(flag);

                    foreach (int move in did.Teaches) moves.Add(move);

                    // Whatever it hands over. The first of these is the starter, and without
                    // it nothing after it can be fought at all.
                    // At the level the script says, which this used to throw away and replace
                    // with five — so every creature in the game arrived as a starter and the
                    // party could never be a match for anything.
                    if (did.Gives is { } gift && party.Count < 6
                        && battles.Wild(gift.Species, Math.Max(1, gift.Level)) is { } given)
                    {
                        party.Add(BattleFactory.Save(given));
                    }

                    // And whoever it picks a fight with. Once each: a trainer beaten stays
                    // beaten, which is what the flag they set means.
                    if (did.Fights is not { } trainerId || !fought.Add(trainerId)) continue;

                    if (party.Count == 0)
                    {
                        skipped++;

                        continue;
                    }

                    // Healed first. A player walks back to a centre; this one cannot walk, so
                    // it is given the same thing for nothing.
                    //
                    // Not healing was the single worst decision in the first version, and the
                    // output said so in one number: the first loss left the whole party down,
                    // and every one of the 156 fights after it was lost before it began. A run
                    // that measures "did the first fight go badly" and reports it 157 times is
                    // not measuring the game.
                    if (heals)
                    {
                        for (var i = 0; i < party.Count; i++) party[i] = battles.Healed(party[i]);

                        healed++;
                    }

                    switch (Fight(battles, progress, party, trainerId))
                    {
                        case true:
                            won++;
                            break;

                        case false:
                            lost++;
                            break;

                        default:
                            skipped++;
                            break;
                    }
                }
            }

            log?.Invoke(
                $"  pass {pass,2}: {reach.Maps.Count,3} maps, {flags.Count,4} flags, "
                + $"{party.Count} in the party (highest level {(party.Count == 0 ? 0 : party.Max(m => m.Level))}), "
                + $"{won} won / {lost} lost");

            if (flags.Count == flagsWere && moves.Count == movesWere && party.Count == partyWas)
            {
                stopped = StoppedBecause.NothingMoreOpened;

                break;
            }
        }

        Reach last = WorldWalker.Walk(world, startMapId, moves, flagsSet: flags);

        var reached = last.Maps.ToHashSet();
        var stoodAtTheEnd = last.Stood.ToHashSet();

        // Every door out of somewhere it got to, into somewhere it did not. This is the list
        // "246 maps unreached" should have been: most of those are behind each other, and only
        // these sit one step from ground already under the player's feet.
        List<ShutDoor> shut =
        [
            .. world.Maps
                .Where(m => reached.Contains(m.Id))
                .SelectMany(m => m.Warps
                    .Where(w => !reached.Contains(w.TargetMapId))
                    .Select(w => new ShutDoor(
                        m.Id,
                        w.Square,
                        w.TargetMapId,
                        world.Find(w.TargetMapId)?.Name ?? "(not exported)",
                        stoodAtTheEnd.Contains((m.Id, w.Square)))))
                .DistinctBy(d => (d.FromMapId, d.ToMapId, d.Square)),
        ];

        return new Attempt(
            passes,
            stopped,
            last.Maps,
            [.. world.Maps.Select(m => m.Id).Where(id => !last.Maps.Contains(id)).Order()],
            flags,
            moves,
            party,
            won,
            lost,
            skipped,
            healed,
            specials,
            shut,
            last.Blocked);
    }

    /// <summary>
    /// What a creature handed over by a script comes out at. <b>Modelled</b> — the level is in
    /// the script and this loop does not read it, so five is the starter's and everything else
    /// is levelled by fighting anyway.
    /// </summary>
    public const int StartingLevel = 5;

    /// <summary>
    /// One fight, front to back. True won, false lost, null could not be fought at all.
    /// <para>
    /// The policy is flat on purpose: send out whoever can still fight, use the move that
    /// would hurt most, never switch, never use an item. A cleverer player wins more fights,
    /// so a fight this loses is not proof a person would — but a fight this <em>wins</em> is
    /// proof the fight works, which is what is being measured.
    /// </para>
    /// </summary>
    private static bool? Fight(
        BattleFactory battles, Progression progress, List<SavedMon> party, int trainerId)
    {
        List<Battler> theirs = battles.TrainerParty(trainerId);

        if (theirs.Count == 0) return null;

        List<Battler> mine = [.. party.Select(battles.Restore).OfType<Battler>()];

        if (mine.Count == 0) return null;

        var mySlot = 0;
        var theirSlot = 0;

        var battle = new Battle(mine[0], theirs[0], 1) { IsWild = false, Struggle = battles.Struggle };

        for (var turn = 0; turn < MostTurns; turn++)
        {
            if (battle.Player.HasFainted)
            {
                int next = mine.FindIndex(m => !m.HasFainted);

                if (next < 0) break;

                mySlot = next;
                battle = Continue(battles, mine[mySlot], theirs[theirSlot], battle);

                continue;
            }

            if (battle.Opponent.HasFainted)
            {
                int next = theirs.FindIndex(t => !t.HasFainted);

                if (next < 0) break;

                theirSlot = next;
                battle = Continue(battles, mine[mySlot], theirs[theirSlot], battle);

                continue;
            }

            battle.ResolveTurn(Best(battle.Player, battle.Opponent), Best(battle.Opponent, battle.Player));
        }

        bool ours = mine.Any(m => !m.HasFainted);

        // Whatever survived, written back.
        for (var i = 0; i < mine.Count && i < party.Count; i++) party[i] = BattleFactory.Save(mine[i]);

        // And what winning is worth. Without this the party stays at the level it was handed
        // over at for the whole game — which the first real run printed twenty-four times in a
        // row as "highest level 5" and which makes every fight after the first few unwinnable
        // by arithmetic rather than by anything the engine did.
        if (!ours) return false;

        foreach (Battler beaten in theirs)
        {
            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].Level >= 100) continue;

                (SavedMon grown, _) = progress.Award(party[i], beaten.Species.Index, beaten.Level);

                party[i] = grown;
            }
        }

        return true;
    }

    /// <summary>
    /// The next one-on-one of the same fight, carrying the dice and the room across.
    /// <para>
    /// <see cref="Battle.ContinueFrom"/> rather than a fresh battle, because the weather
    /// belongs to the room and not to either creature — the fault milestone 169 found in the
    /// one caller that had forgotten it.
    /// </para>
    /// </summary>
    private static Battle Continue(BattleFactory battles, Battler mine, Battler theirs, Battle before)
    {
        var next = new Battle(mine, theirs, before.State)
        {
            IsWild = false,
            Struggle = battles.Struggle,
        };

        next.ContinueFrom(before);

        return next;
    }

    /// <summary>
    /// The move that would hurt most, or the first one when none of them would.
    /// <para>
    /// Power alone, without type or stats. A better chooser would win more fights and would
    /// also be a second engine to keep correct, and what is being measured is whether the
    /// fight can be had at all.
    /// </para>
    /// </summary>
    private static BattleAction Best(Battler attacker, Battler defender)
    {
        var bestSlot = 0;
        var bestPower = -1;

        for (var slot = 0; slot < 4; slot++)
        {
            if (attacker.MoveAt(slot) is not { } move) continue;
            if (attacker.PpLeft(slot) <= 0) continue;

            if (move.Power > bestPower)
            {
                bestPower = move.Power;
                bestSlot = slot;
            }
        }

        return new BattleAction.UseMove(bestSlot);
    }

    /// <summary>
    /// Every script a player could actually stand in front of. Same rule as the closure walk,
    /// and for the same reason: a person behind a locked door is on a map you have been to and
    /// is not somebody you can talk to.
    /// </summary>
    private static IEnumerable<uint> Reachable(
        MapData map, HashSet<(string MapId, GridPosition Square)> stood, HashSet<int> flags)
    {
        foreach (MapObject person in map.Objects)
        {
            if (!person.HasScript) continue;
            if (!person.IsHereFor(flags.Contains)) continue;

            if (Beside(map.Id, person.Square).Any(stood.Contains)) yield return person.ScriptAddress;
        }

        foreach (MapTrigger trigger in map.Triggers)
        {
            if (trigger.HasScript && stood.Contains((map.Id, trigger.Square)))
                yield return trigger.ScriptAddress;
        }

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
