namespace PokeMmo.Core.Battle;

/// <summary>The balls this generation offers, with their catch multipliers in tenths.</summary>
public enum BallKind
{
    Poke,
    Great,
    Ultra,
    Master,
}

/// <summary>How a throw went, and how nearly it worked.</summary>
public sealed record CatchAttempt(bool Caught, int Shakes)
{
    /// <summary>Four shakes means caught; fewer is how close it came.</summary>
    public const int ShakesToCatch = 4;
}

/// <summary>
/// Whether a throw catches.
/// <para>
/// The shake count is not decoration. The games decide the outcome up front and then
/// animate that many wobbles, so a throw that fails on the fourth shake is genuinely
/// nearer than one that fails on the first — and players read that. Computing the
/// shakes rather than just the result keeps that information available.
/// </para>
/// </summary>
public static class CatchCalculator
{
    public static int BallBonusTenths(BallKind ball) => ball switch
    {
        BallKind.Great => 15,
        BallKind.Ultra => 20,
        _ => 10,
    };

    /// <summary>
    /// Status makes a target easier to catch: sleep and freeze double the odds, the
    /// others add half again.
    /// </summary>
    public static int StatusBonusTenths(StatusCondition status) => status switch
    {
        StatusCondition.Sleep or StatusCondition.Freeze => 20,
        StatusCondition.Paralysis or StatusCondition.Burn or StatusCondition.Poison => 15,
        _ => 10,
    };

    /// <summary>
    /// The intermediate catch value. Higher is easier; 255 or more is a guaranteed
    /// catch. Damage matters because the formula weighs missing health heavily.
    /// </summary>
    public static int CatchValue(Battler target, int catchRate, BallKind ball)
    {
        int maxHp = Math.Max(1, target.MaxHp);
        int currentHp = Math.Clamp(target.CurrentHp, 1, maxHp);

        long value = (3L * maxHp - 2L * currentHp) * catchRate * BallBonusTenths(ball) / 10;
        value /= 3L * maxHp;
        value = value * StatusBonusTenths(target.Status) / 10;

        return (int)Math.Clamp(value, 0, 255);
    }

    /// <summary>Throws a ball at a target and reports what happened.</summary>
    public static CatchAttempt Throw(BattleRng rng, Battler target, int catchRate, BallKind ball)
    {
        if (ball == BallKind.Master) return new CatchAttempt(true, CatchAttempt.ShakesToCatch);

        int a = CatchValue(target, catchRate, ball);

        if (a >= 255) return new CatchAttempt(true, CatchAttempt.ShakesToCatch);
        if (a <= 0) return new CatchAttempt(false, 0);

        // The shake threshold is a fourth root, which is what makes the odds fall away
        // so sharply as the catch value drops.
        double b = 1048560.0 / Math.Sqrt(Math.Sqrt(16711680.0 / a));
        int threshold = (int)Math.Clamp(b, 0, 65535);

        int shakes = 0;

        for (int i = 0; i < CatchAttempt.ShakesToCatch; i++)
        {
            if (rng.Next() >= threshold) break;
            shakes++;
        }

        return new CatchAttempt(shakes >= CatchAttempt.ShakesToCatch, shakes);
    }
}
