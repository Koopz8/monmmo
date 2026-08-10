using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;

namespace PokeMmo.Server;

/// <summary>
/// Turns saved numbers into battlers, using the rules file.
/// <para>
/// This is the server's half of the join the client does with a cartridge. Where
/// <c>PartyBuilder.Restore</c> reads a species index and finds a name and a sprite,
/// this reads the same index and finds base stats — everything needed to decide a
/// battle and nothing that could be called content.
/// </para>
/// </summary>
public sealed class BattleFactory(GameRules rules)
{
    /// <summary>What a new account is given, so a first encounter is not a loss by default.</summary>
    public const int StarterSpecies = 1;

    public const int StarterLevel = 5;

    /// <summary>Rebuilds a party member exactly as it was saved.</summary>
    public Battler? Restore(SavedMon saved)
    {
        if (rules.SpeciesAt(saved.Species) is not { } species) return null;

        var battler = new Battler(species, saved.Level, saved.Nature, saved.Nickname);

        foreach (int moveId in saved.Moves)
            if (rules.MoveAt(moveId) is { } move) battler.Moves.Add(move);

        if (battler.Moves.Count == 0) battler.Moves.AddRange(rules.MovesKnownAt(saved.Species, saved.Level));

        // Damage is applied rather than health assigned, because maximum health is
        // computed from base stats — writing a stored number straight in would drift
        // the first time anything about that calculation changed.
        int missing = Math.Clamp(battler.MaxHp - saved.CurrentHp, 0, battler.MaxHp);
        if (missing > 0) battler.TakeDamage(missing);

        battler.Status = saved.Status;

        return battler;
    }

    /// <summary>Builds a wild encounter with the moves that species would know by then.</summary>
    public Battler? Wild(int species, int level)
    {
        if (rules.SpeciesAt(species) is not { } record) return null;

        var battler = new Battler(record, level);
        battler.Moves.AddRange(rules.MovesKnownAt(species, level));

        // A creature with no moves at all cannot take a turn, so it gets the first
        // move in the table rather than standing there.
        if (battler.Moves.Count == 0 && rules.MoveAt(1) is { } fallback) battler.Moves.Add(fallback);

        return battler;
    }

    /// <summary>What a battler looks like to the other end: numbers, no names.</summary>
    public static BattlerView View(Battler battler) => new(
        battler.Species.Index,
        battler.Level,
        battler.Nickname,
        battler.CurrentHp,
        battler.MaxHp,
        battler.Status,
        battler.Moves.Select(m => m.Id).ToList());

    /// <summary>What to write down for a battler.</summary>
    public static SavedMon Save(Battler battler) => new(
        battler.Species.Index,
        battler.Level,
        battler.Nickname,
        battler.CurrentHp,
        battler.Status,
        battler.Nature,
        battler.Moves.Select(m => m.Id).ToList());

    /// <summary>
    /// A party member restored to full health, as a visit to a centre would leave it.
    /// <para>
    /// Rebuilt rather than patched, because maximum health is computed from base stats
    /// — the stored number is not something to raise to a value worked out elsewhere.
    /// </para>
    /// </summary>
    public SavedMon Healed(SavedMon saved)
    {
        if (Restore(saved) is not { } battler) return saved;

        battler.Heal(battler.MaxHp);
        battler.Status = StatusCondition.None;
        battler.SleepTurns = 0;

        return Save(battler);
    }

    /// <summary>True when this one can still fight.</summary>
    public bool CanFight(SavedMon saved) => Restore(saved) is { HasFainted: false };

    /// <summary>
    /// The creature a new account begins with.
    /// <para>
    /// Given at registration rather than conjured at the first encounter, so that a
    /// party is never empty and the server never has to invent a battler mid-battle.
    /// </para>
    /// </summary>
    public SavedMon? Starter()
    {
        Battler? starter = Wild(StarterSpecies, StarterLevel);
        return starter is null ? null : Save(starter);
    }
}
