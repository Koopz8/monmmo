using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>What one hit did, and why.</summary>
public sealed record DamageResult(
    int Damage,
    bool Critical,
    int EffectivenessHundredths,
    bool Stab)
{
    public bool NoEffect => EffectivenessHundredths == 0;
    public bool SuperEffective => EffectivenessHundredths > 100;
    public bool NotVeryEffective => EffectivenessHundredths is > 0 and < 100;
}

/// <summary>
/// The Generation III damage formula.
/// <para>
/// Every step truncates, and the order is load-bearing: the division by defence
/// happens before the division by fifty, the flat two is added after both, and the
/// random factor lands after the critical multiplier. Algebraically equivalent
/// rearrangements give different answers, often enough to matter.
/// </para>
/// </summary>
public static class DamageCalculator
{
    /// <summary>Critical hits double damage in this generation — the halving to 1.5x came later.</summary>
    public const int CriticalMultiplier = 2;

    /// <summary>Chance denominators by critical-hit stage: 1 in 16, 8, 4, 3, 2.</summary>
    private static readonly int[] CriticalOdds = [16, 8, 4, 3, 2];

    public static bool RollCritical(BattleRng rng, int criticalStage) =>
        rng.OneIn(CriticalOdds[Math.Clamp(criticalStage, 0, CriticalOdds.Length - 1)]);

    /// <summary>
    /// Whether a move connects. Accuracy and evasion stages cancel out against each
    /// other before the move's own accuracy is applied.
    /// </summary>
    public static bool RollAccuracy(BattleRng rng, MoveData move, Battler attacker, Battler defender)
    {
        if (move.AlwaysHits) return true;

        int combinedStage = Math.Clamp(
            attacker.StageOf(Stat.Accuracy) - defender.StageOf(Stat.Evasion),
            -Stats.MaxStage,
            Stats.MaxStage);

        int accuracy = Stats.ApplyAccuracyStage(move.Accuracy, combinedStage);

        return rng.Next(100) < Math.Clamp(accuracy, 1, 100);
    }

    /// <summary>
    /// Computes damage for one hit. <paramref name="randomPercent"/> is the 85..100
    /// roll; pass it explicitly so tests can pin an exact number.
    /// </summary>
    public static DamageResult Calculate(
        Battler attacker,
        Battler defender,
        MoveData move,
        bool critical,
        int randomPercent)
    {
        int effectiveness = TypeChart.Effectiveness(move.Type, defender.Type1, defender.Type2);

        if (move.Category == DamageCategory.Status || move.Power == 0 || effectiveness == 0)
            return new DamageResult(0, false, effectiveness, false);

        bool physical = move.Category == DamageCategory.Physical;

        int attack = attacker.EffectiveStat(
            physical ? Stat.Attack : Stat.SpAttack,
            ignoreUnfavourableStages: critical);

        int defence = defender.EffectiveStat(
            physical ? Stat.Defense : Stat.SpDefense,
            ignoreUnfavourableStages: critical);

        // Burn halves physical damage, and does so by halving Attack here rather than
        // in the battler, so a burned attacker's Speed and displayed stats are untouched.
        if (physical && attacker.Status == StatusCondition.Burn) attack /= 2;

        int damage = 2 * attacker.Level / 5 + 2;
        damage = damage * move.Power;
        damage = damage * attack;
        damage = damage / Math.Max(1, defence);
        damage = damage / 50;

        if (critical) damage *= CriticalMultiplier;

        damage += 2;

        damage = damage * Math.Clamp(randomPercent, 85, 100) / 100;

        bool stab = move.Type == attacker.Type1 || move.Type == attacker.Type2;
        if (stab) damage = damage * 15 / 10;

        damage = TypeChart.Apply(damage, move.Type, defender.Type1, defender.Type2);

        // A hit that connects always does something, unless the type chart said the
        // move does not affect the target at all.
        if (damage < 1 && effectiveness > 0) damage = 1;

        return new DamageResult(damage, critical, effectiveness, stab);
    }

    /// <summary>Rolls the random factor and computes damage in one step.</summary>
    public static DamageResult Calculate(
        BattleRng rng,
        Battler attacker,
        Battler defender,
        MoveData move,
        bool critical)
    {
        // The hardware rolls 0..15 and subtracts, giving 85..100 inclusive.
        int randomPercent = 100 - rng.Next(16);
        return Calculate(attacker, defender, move, critical, randomPercent);
    }
}
