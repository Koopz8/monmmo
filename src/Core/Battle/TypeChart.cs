using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>
/// Type effectiveness, as Generation III computes it.
/// <para>
/// Multipliers are held as tenths and applied with integer division, because that is
/// what the games do. A move that is doubly resisted loses a little more than a
/// straight multiply by 0.25 would suggest, and the difference is visible in real
/// damage rolls — so the arithmetic is reproduced rather than approximated.
/// </para>
/// </summary>
public static class TypeChart
{
    public const int Neutral = 10;
    public const int NotVeryEffective = 5;
    public const int SuperEffective = 20;
    public const int Immune = 0;

    /// <summary>Exceptions to neutral, as (attacking, defending, multiplier in tenths).</summary>
    private static readonly (PokemonType Attack, PokemonType Defend, int Tenths)[] Matchups =
    [
        (PokemonType.Normal, PokemonType.Rock, NotVeryEffective),
        (PokemonType.Normal, PokemonType.Steel, NotVeryEffective),
        (PokemonType.Normal, PokemonType.Ghost, Immune),

        (PokemonType.Fighting, PokemonType.Normal, SuperEffective),
        (PokemonType.Fighting, PokemonType.Rock, SuperEffective),
        (PokemonType.Fighting, PokemonType.Steel, SuperEffective),
        (PokemonType.Fighting, PokemonType.Ice, SuperEffective),
        (PokemonType.Fighting, PokemonType.Dark, SuperEffective),
        (PokemonType.Fighting, PokemonType.Flying, NotVeryEffective),
        (PokemonType.Fighting, PokemonType.Poison, NotVeryEffective),
        (PokemonType.Fighting, PokemonType.Bug, NotVeryEffective),
        (PokemonType.Fighting, PokemonType.Psychic, NotVeryEffective),
        (PokemonType.Fighting, PokemonType.Ghost, Immune),

        (PokemonType.Flying, PokemonType.Fighting, SuperEffective),
        (PokemonType.Flying, PokemonType.Bug, SuperEffective),
        (PokemonType.Flying, PokemonType.Grass, SuperEffective),
        (PokemonType.Flying, PokemonType.Rock, NotVeryEffective),
        (PokemonType.Flying, PokemonType.Steel, NotVeryEffective),
        (PokemonType.Flying, PokemonType.Electric, NotVeryEffective),

        (PokemonType.Poison, PokemonType.Grass, SuperEffective),
        (PokemonType.Poison, PokemonType.Poison, NotVeryEffective),
        (PokemonType.Poison, PokemonType.Ground, NotVeryEffective),
        (PokemonType.Poison, PokemonType.Rock, NotVeryEffective),
        (PokemonType.Poison, PokemonType.Ghost, NotVeryEffective),
        (PokemonType.Poison, PokemonType.Steel, Immune),

        (PokemonType.Ground, PokemonType.Poison, SuperEffective),
        (PokemonType.Ground, PokemonType.Rock, SuperEffective),
        (PokemonType.Ground, PokemonType.Steel, SuperEffective),
        (PokemonType.Ground, PokemonType.Fire, SuperEffective),
        (PokemonType.Ground, PokemonType.Electric, SuperEffective),
        (PokemonType.Ground, PokemonType.Bug, NotVeryEffective),
        (PokemonType.Ground, PokemonType.Grass, NotVeryEffective),
        (PokemonType.Ground, PokemonType.Flying, Immune),

        (PokemonType.Rock, PokemonType.Flying, SuperEffective),
        (PokemonType.Rock, PokemonType.Bug, SuperEffective),
        (PokemonType.Rock, PokemonType.Fire, SuperEffective),
        (PokemonType.Rock, PokemonType.Ice, SuperEffective),
        (PokemonType.Rock, PokemonType.Fighting, NotVeryEffective),
        (PokemonType.Rock, PokemonType.Ground, NotVeryEffective),
        (PokemonType.Rock, PokemonType.Steel, NotVeryEffective),

        (PokemonType.Bug, PokemonType.Grass, SuperEffective),
        (PokemonType.Bug, PokemonType.Psychic, SuperEffective),
        (PokemonType.Bug, PokemonType.Dark, SuperEffective),
        (PokemonType.Bug, PokemonType.Fighting, NotVeryEffective),
        (PokemonType.Bug, PokemonType.Flying, NotVeryEffective),
        (PokemonType.Bug, PokemonType.Poison, NotVeryEffective),
        (PokemonType.Bug, PokemonType.Ghost, NotVeryEffective),
        (PokemonType.Bug, PokemonType.Steel, NotVeryEffective),
        (PokemonType.Bug, PokemonType.Fire, NotVeryEffective),

        (PokemonType.Ghost, PokemonType.Psychic, SuperEffective),
        (PokemonType.Ghost, PokemonType.Ghost, SuperEffective),
        (PokemonType.Ghost, PokemonType.Steel, NotVeryEffective),
        (PokemonType.Ghost, PokemonType.Dark, NotVeryEffective),
        (PokemonType.Ghost, PokemonType.Normal, Immune),

        (PokemonType.Steel, PokemonType.Rock, SuperEffective),
        (PokemonType.Steel, PokemonType.Ice, SuperEffective),
        (PokemonType.Steel, PokemonType.Steel, NotVeryEffective),
        (PokemonType.Steel, PokemonType.Fire, NotVeryEffective),
        (PokemonType.Steel, PokemonType.Water, NotVeryEffective),
        (PokemonType.Steel, PokemonType.Electric, NotVeryEffective),

        (PokemonType.Fire, PokemonType.Bug, SuperEffective),
        (PokemonType.Fire, PokemonType.Steel, SuperEffective),
        (PokemonType.Fire, PokemonType.Grass, SuperEffective),
        (PokemonType.Fire, PokemonType.Ice, SuperEffective),
        (PokemonType.Fire, PokemonType.Rock, NotVeryEffective),
        (PokemonType.Fire, PokemonType.Fire, NotVeryEffective),
        (PokemonType.Fire, PokemonType.Water, NotVeryEffective),
        (PokemonType.Fire, PokemonType.Dragon, NotVeryEffective),

        (PokemonType.Water, PokemonType.Ground, SuperEffective),
        (PokemonType.Water, PokemonType.Rock, SuperEffective),
        (PokemonType.Water, PokemonType.Fire, SuperEffective),
        (PokemonType.Water, PokemonType.Water, NotVeryEffective),
        (PokemonType.Water, PokemonType.Grass, NotVeryEffective),
        (PokemonType.Water, PokemonType.Dragon, NotVeryEffective),

        (PokemonType.Grass, PokemonType.Ground, SuperEffective),
        (PokemonType.Grass, PokemonType.Rock, SuperEffective),
        (PokemonType.Grass, PokemonType.Water, SuperEffective),
        (PokemonType.Grass, PokemonType.Flying, NotVeryEffective),
        (PokemonType.Grass, PokemonType.Poison, NotVeryEffective),
        (PokemonType.Grass, PokemonType.Bug, NotVeryEffective),
        (PokemonType.Grass, PokemonType.Steel, NotVeryEffective),
        (PokemonType.Grass, PokemonType.Fire, NotVeryEffective),
        (PokemonType.Grass, PokemonType.Grass, NotVeryEffective),
        (PokemonType.Grass, PokemonType.Dragon, NotVeryEffective),

        (PokemonType.Electric, PokemonType.Flying, SuperEffective),
        (PokemonType.Electric, PokemonType.Water, SuperEffective),
        (PokemonType.Electric, PokemonType.Grass, NotVeryEffective),
        (PokemonType.Electric, PokemonType.Electric, NotVeryEffective),
        (PokemonType.Electric, PokemonType.Dragon, NotVeryEffective),
        (PokemonType.Electric, PokemonType.Ground, Immune),

        (PokemonType.Psychic, PokemonType.Fighting, SuperEffective),
        (PokemonType.Psychic, PokemonType.Poison, SuperEffective),
        (PokemonType.Psychic, PokemonType.Steel, NotVeryEffective),
        (PokemonType.Psychic, PokemonType.Psychic, NotVeryEffective),
        (PokemonType.Psychic, PokemonType.Dark, Immune),

        (PokemonType.Ice, PokemonType.Flying, SuperEffective),
        (PokemonType.Ice, PokemonType.Ground, SuperEffective),
        (PokemonType.Ice, PokemonType.Grass, SuperEffective),
        (PokemonType.Ice, PokemonType.Dragon, SuperEffective),
        (PokemonType.Ice, PokemonType.Steel, NotVeryEffective),
        (PokemonType.Ice, PokemonType.Fire, NotVeryEffective),
        (PokemonType.Ice, PokemonType.Water, NotVeryEffective),
        (PokemonType.Ice, PokemonType.Ice, NotVeryEffective),

        (PokemonType.Dragon, PokemonType.Dragon, SuperEffective),
        (PokemonType.Dragon, PokemonType.Steel, NotVeryEffective),

        (PokemonType.Dark, PokemonType.Psychic, SuperEffective),
        (PokemonType.Dark, PokemonType.Ghost, SuperEffective),
        (PokemonType.Dark, PokemonType.Fighting, NotVeryEffective),
        (PokemonType.Dark, PokemonType.Steel, NotVeryEffective),
        (PokemonType.Dark, PokemonType.Dark, NotVeryEffective),
    ];

    private static readonly int[,] Table = BuildTable();

    private static int[,] BuildTable()
    {
        const int types = 18;
        var table = new int[types, types];

        for (int attack = 0; attack < types; attack++)
            for (int defend = 0; defend < types; defend++)
                table[attack, defend] = Neutral;

        foreach ((PokemonType attack, PokemonType defend, int tenths) in Matchups)
            table[(int)attack, (int)defend] = tenths;

        return table;
    }

    /// <summary>Effectiveness against a single type, in tenths.</summary>
    public static int Against(PokemonType attacking, PokemonType defending)
    {
        int attack = (int)attacking;
        int defend = (int)defending;

        // TYPE_MYSTERY sits in the middle of the numbering and takes part in no
        // matchups; anything outside the chart is simply neutral.
        if (attack is < 0 or > 17 || defend is < 0 or > 17) return Neutral;

        return Table[attack, defend];
    }

    /// <summary>
    /// Applies both of a defender's types in turn, exactly as the games do — each as a
    /// separate multiply-then-divide rather than one combined multiplier.
    /// </summary>
    public static int Apply(int damage, PokemonType attacking, PokemonType defendingFirst, PokemonType defendingSecond)
    {
        damage = damage * Against(attacking, defendingFirst) / 10;

        if (defendingSecond != defendingFirst)
            damage = damage * Against(attacking, defendingSecond) / 10;

        return damage;
    }

    /// <summary>The combined multiplier in hundredths, for describing a hit rather than computing one.</summary>
    public static int Effectiveness(PokemonType attacking, PokemonType defendingFirst, PokemonType defendingSecond)
    {
        int combined = Against(attacking, defendingFirst) * 10;

        if (defendingSecond != defendingFirst)
            combined = combined * Against(attacking, defendingSecond) / 10;

        return combined;
    }
}
