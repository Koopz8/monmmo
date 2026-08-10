using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>
/// How much experience a level costs, and what a defeat is worth.
/// <para>
/// Six curves, and the differences between them are large — a Slow species needs
/// twice what an Erratic one does to reach level 100. Getting a curve wrong is not a
/// visible error; it is a species that levels at subtly the wrong pace forever, which
/// is why each one is pinned to its published total at level 100 in the tests.
/// </para>
/// <para>
/// All of it is integer arithmetic, as the hardware does it. Floating point here would
/// drift from the real values by a point or two at the boundaries, and a boundary is
/// exactly where a level-up happens.
/// </para>
/// </summary>
public static class Experience
{
    public const int MaxLevel = 100;

    /// <summary>Total experience needed to have reached a level.</summary>
    public static int TotalForLevel(GrowthRate rate, int level)
    {
        int n = Math.Clamp(level, 1, MaxLevel);
        if (n == 1) return 0;

        long cube = (long)n * n * n;

        return rate switch
        {
            GrowthRate.Fast => (int)(4 * cube / 5),
            GrowthRate.MediumFast => (int)cube,
            GrowthRate.MediumSlow => (int)(6 * cube / 5 - 15L * n * n + 100L * n - 140),
            GrowthRate.Slow => (int)(5 * cube / 4),
            GrowthRate.Erratic => (int)Erratic(n, cube),
            GrowthRate.Fluctuating => (int)Fluctuating(n, cube),
            _ => (int)cube,
        };
    }

    private static long Erratic(int n, long cube) => n switch
    {
        < 50 => cube * (100 - n) / 50,
        < 68 => cube * (150 - n) / 100,
        < 98 => cube * ((1911 - 10 * n) / 3) / 500,
        _ => cube * (160 - n) / 100,
    };

    private static long Fluctuating(int n, long cube) => n switch
    {
        < 15 => cube * ((n + 1) / 3 + 24) / 50,
        < 36 => cube * (n + 14) / 50,
        _ => cube * (n / 2 + 32) / 50,
    };

    /// <summary>The level a total amount of experience corresponds to.</summary>
    public static int LevelAt(GrowthRate rate, int experience)
    {
        int level = 1;

        while (level < MaxLevel && experience >= TotalForLevel(rate, level + 1)) level++;

        return level;
    }

    /// <summary>
    /// What beating something is worth.
    /// <para>
    /// The Generation III wild formula: the loser's base yield times its level,
    /// divided by seven. Trainers pay half again as much, which is a thing to add when
    /// there are trainers.
    /// </para>
    /// </summary>
    public static int ForDefeating(int baseYield, int faintedLevel) =>
        Math.Max(1, baseYield * faintedLevel / 7);

    /// <summary>Experience still needed to reach the next level, or zero at the cap.</summary>
    public static int ToNextLevel(GrowthRate rate, int level, int experience) =>
        level >= MaxLevel ? 0 : Math.Max(0, TotalForLevel(rate, level + 1) - experience);
}
