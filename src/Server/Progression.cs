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
    /// Awards experience for a defeated opponent and returns the member as it now is,
    /// along with everything that happened, in order.
    /// </summary>
    public (SavedMon Member, List<BattleEvent> Events) Award(SavedMon member, int faintedSpecies, int faintedLevel)
    {
        var events = new List<BattleEvent>();

        if (rules.SpeciesAt(member.Species) is not { } species) return (member, events);
        if (rules.SpeciesAt(faintedSpecies) is not { } fainted) return (member, events);
        if (member.Level >= Experience.MaxLevel) return (member, events);

        int gained = Experience.ForDefeating(fainted.ExpYield, faintedLevel);

        // A member restored from a save may have no recorded experience at all, if it
        // was caught before any of this existed. Its level is the truth in that case,
        // so the curve is entered at the bottom of it rather than at zero.
        int total = Math.Max(member.Experience, Experience.TotalForLevel(species.GrowthRate, member.Level)) + gained;

        events.Add(new BattleEvent.ExperienceGained(Side.Player, gained));

        int level = member.Level;
        var moves = member.Moves.ToList();

        for (int grown = 0; grown < MaxLevelsPerBattle; grown++)
        {
            if (level >= Experience.MaxLevel) break;
            if (total < Experience.TotalForLevel(species.GrowthRate, level + 1)) break;

            level++;
            events.Add(new BattleEvent.LevelledUp(Side.Player, level));

            LearnMovesFor(member.Species, level, moves, events);
        }

        if (level >= Experience.MaxLevel)
            total = Math.Min(total, Experience.TotalForLevel(species.GrowthRate, Experience.MaxLevel));

        return (member with { Level = level, Moves = moves, Experience = total }, events);
    }

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
