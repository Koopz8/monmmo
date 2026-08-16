using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>
/// The moves whose power is not the number on their record.
/// <para>
/// Four groups, and they are worth keeping together because they share the one property that
/// makes them awkward: the record says a power and the record is wrong on purpose. Every one
/// of them carries a placeholder — one, or the base of a range — and the real number is
/// computed at the moment the move is used, from something the engine knows.
/// </para>
/// <para>
/// The arithmetic in each is <b>modelled</b>: it lives in the game's code and no amount of
/// dumping crosses that boundary. What each formula is computed <em>from</em> is read — a
/// creature's health, its condition, the six numbers it was born with — which is the
/// difference between modelling a rule and inventing a number, and is why HIDDEN POWER is in
/// here rather than in the list of things this project will not guess at.
/// </para>
/// </summary>
public static class MovePower
{
    /// <summary>FLAIL and REVERSAL: harder the less there is left.</summary>
    public const byte Cornered = 0x63;

    /// <summary>ERUPTION and WATER SPOUT: weaker the less there is left.</summary>
    public const byte Spending = 0xBE;

    /// <summary>FACADE: twice as hard while its user is suffering.</summary>
    public const byte Regardless = 0xA9;

    /// <summary>HIDDEN POWER: a type and a power out of the six a creature was born with.</summary>
    public const byte Hidden = 0x87;

    /// <summary>GUST and TWISTER: twice as hard against something that is not on the ground.</summary>
    public const byte Overhead = 0x95;

    public const byte Whirling = 0x92;

    /// <summary>EARTHQUAKE: twice as hard against something that has gone under it.</summary>
    public const byte Underfoot = 0x93;

    /// <summary>SMELLINGSALT: twice as hard against something that cannot move properly.</summary>
    public const byte Rousing = 0xAB;

    /// <summary>REVENGE: twice as hard when its user has already been hit this turn.</summary>
    public const byte Answering = 0xB9;

    /// <summary>WEATHER BALL: whatever the sky is, and twice as hard when the sky is anything.</summary>
    public const byte Skyward = 0xCB;

    /// <summary>PURSUIT: twice as hard against somebody on their way out.</summary>
    public const byte Chasing = 0x80;

    /// <summary>FURY CUTTER: twice as hard for every turn running it has already landed.</summary>
    public const byte Building = 0x77;

    /// <summary>ROLLOUT and ICE BALL: the same climb, with no choice about staying on it.</summary>
    public const byte Rolling = 0x75;

    /// <summary>
    /// How many doublings the climb is allowed. <b>Modelled.</b>
    /// <para>
    /// Nothing that doubles without a ceiling belongs in a game that people play for months:
    /// four doublings turns a twenty into three hundred and twenty, which is already the
    /// hardest hit in the game, and a fifth would make one move the answer to everything. The
    /// number is not on any record — the cap is in the game's code — so it is named here and
    /// tested as existing rather than as being four.
    /// </para>
    /// </summary>
    public const int MostDoublings = 4;

    /// <summary>
    /// The five steps FLAIL and REVERSAL climb, as forty-eighths of full health and the
    /// power at each. <b>Modelled.</b>
    /// <para>
    /// Forty-eighths because that is the granularity the games use, and the five steps rather
    /// than a curve because a curve fitted to five points is a guess dressed up as arithmetic.
    /// </para>
    /// </summary>
    private static readonly (int OutOf48, int Power)[] Steps =
    [
        (2, 200), (5, 150), (10, 100), (17, 80), (33, 40),
    ];

    private const int Fortyeighths = 48;

    /// <summary>The most ERUPTION and WATER SPOUT reach, on full health. <b>Modelled.</b></summary>
    private const int Full = 150;

    /// <summary>What HIDDEN POWER runs between. <b>Modelled.</b></summary>
    private const int Weakest = 30;

    private const int Strongest = 70;

    /// <summary>
    /// What this move actually hits for, or nothing when its record already says.
    /// <para>
    /// Nothing rather than the record's own number, so a caller cannot use this without
    /// knowing whether it answered — a method that returned the record's power for every
    /// move would be one nobody could tell had done anything.
    /// </para>
    /// </summary>
    public static int? Of(
        MoveData move, Battler attacker, Battler? defender = null, Weather weather = Weather.None,
        bool leaving = false) =>
        move.Effect switch
    {
        // The three that answer somebody who is not standing where they were. This engine has
        // one "away" state rather than one per move — a creature halfway through FLY and one
        // halfway through DIG are both simply not there — so all three read the same flag,
        // and that is a simplification worth naming rather than hiding.
        Overhead or Whirling or Underfoot when defender is { IsAway: true } => move.Power * 2,

        Rousing when defender is { Status: StatusCondition.Paralysis } => move.Power * 2,

        // The one power here that depends on something the attacker had done to it rather
        // than on anything about either creature. It goes last in a turn, so by the time it
        // is asked the answer is settled.
        Answering when attacker.HurtThisTurn > 0 => move.Power * 2,

        // Twice as hard under any sky at all, and its record's own power under none. The
        // doubling is modelled; that the sky is what it depends on is the move's group.
        Skyward when weather != Weather.None => move.Power * 2,

        // And the one whose power depends on what the other side chose rather than on
        // anything about either creature. It is told; it does not look.
        Chasing when leaving => move.Power * 2,

        // The two that climb. What they climb from is the battle's count of how many turns
        // running this same slot has been used — the only power here that depends on a turn
        // other than this one, which is why the count cannot live in this class.
        Building or Rolling => move.Power << Math.Min(attacker.RunningCount, MostDoublings),

        Cornered => Climbing(attacker),
        Spending => Math.Max(1, Full * attacker.CurrentHp / Math.Max(1, attacker.MaxHp)),
        Regardless when attacker.Status != StatusCondition.None => move.Power * 2,
        Hidden => From(attacker.Born, Weakest, Strongest - Weakest, second: true),
        _ => null,
    };

    private static int Climbing(Battler battler)
    {
        int left = Fortyeighths * battler.CurrentHp / Math.Max(1, battler.MaxHp);

        foreach ((int outOf48, int power) in Steps)
        {
            if (left < outOf48) return power;
        }

        return 20;
    }

    /// <summary>
    /// The type HIDDEN POWER comes out as, or nothing for every other move.
    /// <para>
    /// Sixteen types rather than seventeen: it is never Normal, which is the games' rule and
    /// is why the ordering below starts at Fighting.
    /// </para>
    /// </summary>
    public static PokemonType? TypeOf(
        MoveData move, Battler attacker, Weather weather = Weather.None) => move.Effect switch
    {
        Hidden => Sixteen[From(attacker.Born, 0, Sixteen.Count - 1, second: false)],

        // The second move whose type is not the one on its record, and the only one whose
        // type is a fact about the room rather than about its user. Under a clear sky it is
        // Normal, which is also what its record says — so this returns an answer either way
        // rather than falling through, because "the sky decided Normal" and "nobody asked"
        // are different claims and only one of them is true here.
        Skyward => Skies.Lends(weather),

        _ => null,
    };

    /// <summary>
    /// The sixteen, in the order the games count them. <b>Modelled</b> — the order is in the
    /// code, and it is the one thing here that could be wrong without anything looking wrong.
    /// </summary>
    private static readonly IReadOnlyList<PokemonType> Sixteen =
    [
        PokemonType.Fighting, PokemonType.Flying, PokemonType.Poison, PokemonType.Ground,
        PokemonType.Rock, PokemonType.Bug, PokemonType.Ghost, PokemonType.Steel,
        PokemonType.Fire, PokemonType.Water, PokemonType.Grass, PokemonType.Electric,
        PokemonType.Psychic, PokemonType.Ice, PokemonType.Dragon, PokemonType.Dark,
    ];

    /// <summary>
    /// Six bits out of the six numbers a creature was born with, scaled onto a range.
    /// <para>
    /// The lowest bit of each gives the type and the next one up gives the power, which is
    /// the whole of why this move is different for two creatures of the same species. The
    /// bits are read; where they are taken from and what they are scaled onto is modelled.
    /// </para>
    /// </summary>
    private static int From(Genes born, int lowest, int span, bool second)
    {
        int bits = 0;

        for (int stat = 0; stat < Order.Count; stat++)
        {
            int value = born.In(Order[stat]);
            int bit = second ? (value >> 1) & 1 : value & 1;

            bits |= bit << stat;
        }

        // Sixty-three is what six bits reach, and dividing by it rather than by sixty-four is
        // what makes the top of the range reachable at all.
        return lowest + (bits * span / 63);
    }

    /// <summary>
    /// The order the six bits are taken in. <b>Modelled</b>, and not the order this project
    /// uses everywhere else — which is exactly the kind of difference worth writing down
    /// rather than assuming away.
    /// </summary>
    private static readonly IReadOnlyList<Stat> Order =
        [Stat.Hp, Stat.Attack, Stat.Defense, Stat.Speed, Stat.SpAttack, Stat.SpDefense];
}
