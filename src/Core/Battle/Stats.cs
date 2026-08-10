namespace PokeMmo.Core.Battle;

public enum Stat
{
    Hp,
    Attack,
    Defense,
    Speed,
    SpAttack,
    SpDefense,
    Accuracy,
    Evasion,
}

/// <summary>
/// The twenty-five natures. Each raises one stat by a tenth and lowers another;
/// five raise and lower the same stat and so do nothing.
/// </summary>
public enum Nature
{
    Hardy, Lonely, Brave, Adamant, Naughty,
    Bold, Docile, Relaxed, Impish, Lax,
    Timid, Hasty, Serious, Jolly, Naive,
    Modest, Mild, Quiet, Bashful, Rash,
    Calm, Gentle, Sassy, Careful, Quirky,
}

/// <summary>
/// Stat calculation, stat stages and natures, with the games' integer truncation.
/// <para>
/// Every division here truncates, and the order matters: rearranging the arithmetic
/// into something algebraically equivalent produces numbers that are off by one often
/// enough to be noticed in real battles.
/// </para>
/// </summary>
public static class Stats
{
    /// <summary>Stat stages run from -6 to +6.</summary>
    public const int MaxStage = 6;

    /// <summary>Stage multipliers for the battle stats, as (numerator, denominator).</summary>
    private static readonly (int Numerator, int Denominator)[] StageRatios =
    [
        (2, 8), (2, 7), (2, 6), (2, 5), (2, 4), (2, 3),
        (2, 2),
        (3, 2), (4, 2), (5, 2), (6, 2), (7, 2), (8, 2),
    ];

    /// <summary>Accuracy and evasion use their own, gentler table.</summary>
    private static readonly (int Numerator, int Denominator)[] AccuracyStageRatios =
    [
        (33, 100), (36, 100), (43, 100), (50, 100), (60, 100), (75, 100),
        (100, 100),
        (133, 100), (166, 100), (200, 100), (250, 100), (266, 100), (300, 100),
    ];

    /// <summary>Which stat each nature raises, and which it lowers.</summary>
    private static readonly (Stat Raised, Stat Lowered)[] NatureEffects = BuildNatureEffects();

    private static (Stat Raised, Stat Lowered)[] BuildNatureEffects()
    {
        // Natures are laid out as a 5x5 grid over Attack, Defense, Speed, SpAttack,
        // SpDefense: the row is raised and the column lowered, so the diagonal is
        // neutral.
        Stat[] order = [Stat.Attack, Stat.Defense, Stat.Speed, Stat.SpAttack, Stat.SpDefense];
        var effects = new (Stat, Stat)[25];

        for (int raised = 0; raised < 5; raised++)
            for (int lowered = 0; lowered < 5; lowered++)
                effects[raised * 5 + lowered] = (order[raised], order[lowered]);

        return effects;
    }

    /// <summary>Hit points, which use their own formula and ignore nature.</summary>
    public static int Hp(int baseStat, int level, int iv = 31, int ev = 0) =>
        (2 * baseStat + iv + ev / 4) * level / 100 + level + 10;

    /// <summary>Any stat other than hit points.</summary>
    public static int Other(Stat stat, int baseStat, int level, Nature nature, int iv = 31, int ev = 0)
    {
        int value = (2 * baseStat + iv + ev / 4) * level / 100 + 5;
        return ApplyNature(value, stat, nature);
    }

    /// <summary>
    /// A nature adjusts by a tenth, truncated — so it is worth slightly less than ten
    /// per cent on a raised stat and slightly more on a lowered one.
    /// </summary>
    public static int ApplyNature(int value, Stat stat, Nature nature)
    {
        (Stat raised, Stat lowered) = NatureEffects[(int)nature];

        if (raised == lowered) return value;
        if (stat == raised) return value * 110 / 100;
        if (stat == lowered) return value * 90 / 100;

        return value;
    }

    public static bool IsNeutral(Nature nature)
    {
        (Stat raised, Stat lowered) = NatureEffects[(int)nature];
        return raised == lowered;
    }

    public static (Stat Raised, Stat Lowered) EffectOf(Nature nature) => NatureEffects[(int)nature];

    /// <summary>Applies a stat stage to a battle stat.</summary>
    public static int ApplyStage(int value, int stage)
    {
        (int numerator, int denominator) = StageRatios[Math.Clamp(stage, -MaxStage, MaxStage) + MaxStage];
        return value * numerator / denominator;
    }

    /// <summary>Applies a stage to accuracy or evasion, which use a separate table.</summary>
    public static int ApplyAccuracyStage(int value, int stage)
    {
        (int numerator, int denominator) = AccuracyStageRatios[Math.Clamp(stage, -MaxStage, MaxStage) + MaxStage];
        return value * numerator / denominator;
    }
}
