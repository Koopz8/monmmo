using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;

namespace PokeMmo.Server;

/// <summary>
/// What winning does to a party member.
/// <para>
/// Kept apart from the battle itself because it happens after one, and because it is
/// the part with a formula worth testing on its own: a level is a threshold in a
/// curve, and a battler can cross more than one of them in a single victory.
/// </para>
/// </summary>
public sealed class Progression(GameRules rules)
{
    /// <summary>The most levels one victory is allowed to grant, as a runaway guard.</summary>
    private const int MaxLevelsPerBattle = 20;

    /// <summary>
    /// How many times one level-up may turn something into something else.
    /// <para>
    /// Three would do on this cartridge and this is a guard rather than a rule: a table
    /// with a loop in it — A becomes B becomes A — would otherwise spin here forever,
    /// and a server that hangs on one party member is worse than one that stops early.
    /// </para>
    /// </summary>
    private const int MaxStages = 8;

    /// <summary>
    /// Awards experience for a defeated opponent and returns the member as it now is,
    /// along with everything that happened, in order.
    /// </summary>
    public (SavedMon Member, List<BattleEvent> Events) Award(SavedMon member, int faintedSpecies, int faintedLevel)
    {
        var events = new List<BattleEvent>();

        if (rules.SpeciesAt(member.Species) is not { } species) return (member, events);
        if (rules.SpeciesAt(faintedSpecies) is not { } fainted) return (member, events);

        // What the fight leaves behind, which is not experience and does not stop at the
        // level cap. A creature at a hundred earns nothing more from the curve and still
        // gets stronger for every fight, which is the whole reason this is a separate
        // number in the first place.
        member = member with { Evs = [.. member.Earned.Plus(fainted).Values] };

        if (member.Level >= Experience.MaxLevel) return (member, events);

        int gained = Experience.ForDefeating(fainted.ExpYield, faintedLevel);

        // A member restored from a save may have no recorded experience at all, if it
        // was caught before any of this existed. Its level is the truth in that case,
        // so the curve is entered at the bottom of it rather than at zero.
        int total = Math.Max(member.Experience, Experience.TotalForLevel(species.GrowthRate, member.Level)) + gained;

        events.Add(new BattleEvent.ExperienceGained(Side.Player, gained));

        int level = member.Level;
        int what = member.Species;
        var moves = member.Moves.ToList();

        for (int grown = 0; grown < MaxLevelsPerBattle; grown++)
        {
            if (level >= Experience.MaxLevel) break;
            if (total < Experience.TotalForLevel(species.GrowthRate, level + 1)) break;

            level++;
            events.Add(new BattleEvent.LevelledUp(Side.Player, level));

            LearnMovesFor(what, level, moves, events);

            // And then, if this is the level it stops being what it was. After the moves
            // rather than before: the level it reached is the level it learns at, and the
            // games teach the old form's move before the new form exists.
            //
            // In a loop because the threshold is "has reached", not "is exactly". An
            // operator hands out a CHARMANDER at fifty and it is two evolutions overdue;
            // one of them per level would take it two more fights to catch up with
            // itself, and there is no reading of the rule where that is right.
            for (int stage = 0; stage < MaxStages; stage++)
            {
                if (Becomes(what, level) is not { } into) break;

                events.Add(new BattleEvent.Evolved(Side.Player, what, into));

                what = into;

                // The curve is the new thing's curve, and so is everything after it —
                // the next level's threshold, and the next level's moves. A CHARMELEON
                // that went on levelling as a CHARMANDER would learn the wrong moves and
                // cross the wrong thresholds, and nothing would say so.
                if (rules.SpeciesAt(what) is { } now) species = now;
            }
        }

        if (level >= Experience.MaxLevel)
            total = Math.Min(total, Experience.TotalForLevel(species.GrowthRate, Experience.MaxLevel));

        return (member with { Species = what, Level = level, Moves = moves, Experience = total }, events);
    }

    /// <summary>
    /// What this becomes on reaching a level, if it becomes anything.
    /// <para>
    /// Only the level method. A stone is an item somebody has to use and a trade is two
    /// players; neither of those is something a victory can bring about, and evolving a
    /// GRAVELER because it hit level forty would be inventing a rule this cartridge does
    /// not have.
    /// </para>
    /// </summary>
    private int? Becomes(int species, int level) =>
        rules.EvolutionAt(species, level) is { } evolution && evolution.Into != species
            ? evolution.Into
            : null;

    /// <summary>
    /// Teaches whatever this species learns at a level.
    /// <para>
    /// Nothing is forgotten to make room. The games ask which move to drop, and until
    /// something can ask, silently discarding one a player chose is the worse mistake.
    /// </para>
    /// </summary>
    private void LearnMovesFor(int species, int level, List<int> moves, List<BattleEvent> events)
    {
        if (rules.LearnsetOf(species) is not { } learnset) return;

        foreach (LevelUpMove entry in learnset.Moves.Where(m => m.Level == level))
        {
            if (moves.Contains(entry.MoveId)) continue;

            if (moves.Count >= 4)
            {
                events.Add(new BattleEvent.MoveNotLearned(Side.Player, entry.MoveId));
                continue;
            }

            moves.Add(entry.MoveId);
            events.Add(new BattleEvent.MoveLearned(Side.Player, entry.MoveId));
        }
    }
}
