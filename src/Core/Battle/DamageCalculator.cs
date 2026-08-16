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
    public static bool RollAccuracy(
        BattleRng rng, MoveData move, Battler attacker, Battler defender, Weather weather = Weather.None)
    {
        if (move.AlwaysHits) return true;

        // And the one that never misses without its record saying so. A move that comes last
        // in exchange for certainty is a trade, and the certainty is the half that is not on
        // the record.
        if (MoveEffects.Of(move.Effect).Kind == EffectKind.SlowAndSure) return true;

        // The one move the sky overrules. THUNDER cannot miss in rain and is half as
        // likely to land in sun — and it is a group of one in the cartridge's own effect
        // table, so this is a rule about a move rather than about a family.
        if (Skies.Accuracy(weather, move.Effect) is { } instead)
            return rng.Next(100) < Math.Clamp(instead, 1, 100);

        // Somebody who has been made findable is not hiding any more, so their evasion stops
        // counting. Their accuracy stages are untouched — this is about being found, not
        // about being worse at things.
        int hiding = defender.IsIdentified ? 0 : defender.StageOf(Stat.Evasion);

        int combinedStage = Math.Clamp(
            attacker.StageOf(Stat.Accuracy) - hiding,
            -Stats.MaxStage,
            Stats.MaxStage);

        int accuracy = Stats.ApplyAccuracyStage(move.Accuracy, combinedStage);

        // What the defender is carrying to make itself harder to find. Taken off the
        // accuracy as a percentage of it rather than as a stage, because it is not a stage:
        // it survives a HAZE, it does not stack with itself, and CLEAR BODY has nothing to
        // say about it.
        accuracy = accuracy * (100 - HeldItems.Slipperiness(defender.Carried)) / 100;

        return rng.Next(100) < Math.Clamp(accuracy, 1, 100);
    }

    /// <summary>
    /// What a confused creature does to itself.
    /// <para>
    /// The games treat it as an ordinary physical hit of forty power, with the same
    /// creature on both sides of the sum and no type on it at all — so there is no
    /// effectiveness, no same-type bonus and no critical. Forty is modelled rather than
    /// read: it is a number in the game's code and this project does not read code.
    /// </para>
    /// </summary>
    public static int Confusion(Battler battler)
    {
        int level = battler.Level;
        int attack = battler.EffectiveStat(Stat.Attack);
        int defense = battler.EffectiveStat(Stat.Defense);

        int damage = (((2 * level / 5 + 2) * 40 * attack / Math.Max(1, defense)) / 50) + 2;

        return Math.Max(1, damage);
    }

    /// <summary>
    /// Computes damage for one hit. <paramref name="randomPercent"/> is the 85..100
    /// roll; pass it explicitly so tests can pin an exact number.
    /// <para>
    /// <paramref name="damping"/> is what the field is doing to this type as a percentage —
    /// a hundred when nothing is. It is a percentage rather than a flag because it is the
    /// same shape as every other multiplier here, and it arrives as an argument rather than
    /// being read off either creature because it is a fact about the room.
    /// </para>
    /// <para>
    /// <paramref name="hit"/> is which go of a multi-hit move this is, counting from zero.
    /// Every move in this game ignores it except the one that climbs, and that one is the
    /// reason it exists at all.
    /// </para>
    /// </summary>
    public static DamageResult Calculate(
        Battler attacker,
        Battler defender,
        MoveData move,
        bool critical,
        int randomPercent,
        Weather weather = Weather.None,
        int damping = 100,
        int hit = 0,
        bool leaving = false)
    {
        // What the defender has put up against this kind of move. Halved, and applied at the
        // end with the other multipliers on the finished number rather than to the defence
        // stat — a screen is a wall in front of somebody rather than somebody being tougher,
        // and a critical hit goes through it for exactly that reason.
        // And what type it is, when the record does not say that either. HIDDEN POWER is the
        // only move in this game whose type depends on the creature using it, and the six
        // numbers it depends on are read off a save rather than guessed at.
        PokemonType type = MovePower.TypeOf(move, attacker, weather) ?? move.Type;

        int effectiveness = TypeChart.Effectiveness(type, defender.Type1, defender.Type2);

        // And a type chart immunity it was relying on stops applying, which is the other half
        // of being found. Only an immunity: being resistant is not hiding, and a move that
        // turned resistance into neutral would be a different move.
        bool foundOut = effectiveness == 0 && defender.IsIdentified;

        if (foundOut) effectiveness = TypeChart.Neutral;

        // What the defender's ability says about being hit by this at all. Asked before
        // anything is worked out, because the four immunities and WONDER GUARD are answers
        // to "does this land", not adjustments to how hard it lands.
        //
        // The type chart's own answer is passed in rather than recomputed, because WONDER
        // GUARD's rule is about that answer: only what is already super effective gets
        // through.
        if (Abilities.Against(defender.Ability, move, effectiveness) is { } refused)
            effectiveness = refused;

        // What this move actually hits for, when its record's number is a placeholder. Asked
        // before the "no power" refusal below, because FLAIL's record says one and one is a
        // record saying the number is somewhere else.
        int power = MovePower.Of(move, attacker, defender, weather, leaving) ?? move.Power;

        // The one move whose power depends on which go of itself this is. Multiplying rather
        // than doubling, because the three goes are worth one, two and three of it rather
        // than one, two and four — a climb rather than a doubling, and the difference is the
        // whole character of the move.
        if (MoveEffects.Of(move.Effect).Kind == EffectKind.ThreeGoes) power *= hit + 1;

        if (move.Category == DamageCategory.Status || power == 0 || effectiveness == 0)
            return new DamageResult(0, false, effectiveness, false);

        bool physical = move.Category == DamageCategory.Physical;

        int attack = attacker.EffectiveStat(
            physical ? Stat.Attack : Stat.SpAttack,
            ignoreUnfavourableStages: critical);

        int defence = defender.EffectiveStat(
            physical ? Stat.Defense : Stat.SpDefense,
            ignoreUnfavourableStages: critical);

        // What the attacker's own ability is worth, applied to the stat rather than to the
        // damage. That is where the games put it and it is also where it composes: a
        // doubled Attack and a halved one are the same arithmetic in either order, and a
        // multiplier on the far end of the division is not.
        attack = attack * Abilities.Attacking(attacker.Ability, attacker, move, physical) / 100;

        // And what it is carrying, on the same stat and for the same reason an ability goes
        // here: a CHOICE BAND and a HUGE POWER are the same arithmetic in either order only
        // while they are both on this side of the division.
        attack = attack * HeldItems.Multiplies(
            attacker.Carried, attacker.Species.Index, physical ? Stat.Attack : Stat.SpAttack) / 100;

        defence = defence * HeldItems.Multiplies(
            defender.Carried, defender.Species.Index, physical ? Stat.Defense : Stat.SpDefense) / 100;

        // Burn halves physical damage, and does so by halving Attack here rather than
        // in the battler, so a burned attacker's Speed and displayed stats are untouched.
        //
        // Except for GUTS, which is the ability whose whole point is that being ill helps.
        // Halving its Attack for the burn it is being rewarded for would leave it doing
        // three quarters of what an unburned one does, which is the opposite of the rule.
        if (physical && attacker.Status == StatusCondition.Burn && attacker.Ability != Abilities.Guts)
            attack /= 2;

        int damage = 2 * attacker.Level / 5 + 2;
        damage = damage * power;
        damage = damage * attack;
        damage = damage / Math.Max(1, defence);
        damage = damage / 50;

        if (critical) damage *= CriticalMultiplier;

        damage += 2;

        damage = damage * Math.Clamp(randomPercent, 85, 100) / 100;

        bool stab = type == attacker.Type1 || type == attacker.Type2;
        if (stab) damage = damage * 15 / 10;

        // The type multiplier — skipped entirely for a defender whose immunity has just been
        // taken away, because asking the chart again would put it straight back. Without this
        // the whole thing still "worked": the chart returned nothing, the damage floor made it
        // one point, and one point looks like a hit until somebody counts.
        if (!foundOut) damage = TypeChart.Apply(damage, type, defender.Type1, defender.Type2);

        // And what the defender's ability takes off the end. Last, because it is a
        // reduction of the finished number rather than of any part of the sum.
        damage = damage * Abilities.Defending(defender.Ability, move) / 100;

        // And what the sky is doing to it. Last, with the other multiplier on the finished
        // number — rain makes water and unmakes fire, and sun does the reverse.
        damage = damage * Skies.Damage(weather, type) / 100;

        // And the wall, which a critical hit ignores. That is the games' rule and it is also
        // the only reading that makes a screen a wall rather than a stat: something that got
        // through cleanly got through it.
        if (!critical && (physical ? defender.ReflectTurns : defender.ScreenTurns) > 0) damage /= 2;

        // And the seventeen items that are worth a percentage on one type of move, whose
        // percentage is the number on their own record rather than one written here.
        damage = damage * HeldItems.Boosting(attacker.Carried, type) / 100;

        // And what the room is doing to this type, which is the last word because it is the
        // only multiplier here that neither creature owns. Somebody who turned the electricity
        // down turned it down for everybody, including themselves.
        damage = damage * Math.Clamp(damping, 0, 100) / 100;

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
        bool critical,
        Weather weather = Weather.None,
        int damping = 100,
        int hit = 0,
        bool leaving = false)
    {
        // The hardware rolls 0..15 and subtracts, giving 85..100 inclusive.
        int randomPercent = 100 - rng.Next(16);
        return Calculate(attacker, defender, move, critical, randomPercent, weather, damping, hit, leaving);
    }
}
