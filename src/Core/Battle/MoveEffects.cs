namespace PokeMmo.Core.Battle;

/// <summary>What a move does besides damage, and to whom.</summary>
public enum EffectKind
{
    /// <summary>Nothing this engine knows how to do.</summary>
    None,

    /// <summary>Inflicts a lasting condition.</summary>
    Status,

    /// <summary>Moves a stat stage up or down.</summary>
    Stage,
}

/// <summary>
/// One move's effect, read out of the effect byte in its own record.
/// <para>
/// <see cref="Chance"/> is nought for an effect that always happens and the move's own
/// secondary chance otherwise. A status move's effect is the move; a damaging move's is
/// a rider on the hit.
/// </para>
/// </summary>
public readonly record struct MoveEffect(
    EffectKind Kind,
    bool OnUser,
    StatusCondition Status = StatusCondition.None,
    Stat Stat = Stat.Hp,
    int Stages = 0);

/// <summary>
/// The effect byte in a move's record, turned into something the battle engine can do.
/// <para>
/// Until this existed the engine had one line for every move with no power —
/// <c>if (move.Category == DamageCategory.Status) return;</c> — and that is 138 of this
/// cartridge's 354 moves. A level 30 BULBASAUR could spend an entire fight announcing
/// "used POISONPOWDER!" at a level 9 PIDGEY and never touch it, and every trainer in the
/// game who opens with GROWL or LEER was wasting the turn as far as the arithmetic was
/// concerned. Everything downstream already worked: sleep counts down, paralysis skips a
/// quarter of turns, poison and burn take a sixteenth, the stat stages apply. Nothing
/// ever inflicted any of it.
/// </para>
/// <para>
/// What each number means is read off the members rather than remembered, the same way
/// every script width in this project was derived. Group the cartridge's moves by their
/// effect byte and the table has a <em>shape</em>: four runs of exactly seven, in the
/// order attack, defence, speed, special attack, special defence, accuracy, evasion —
/// which is the order the stats are already in.
/// </para>
/// <code>
///   0x0A..0x10  raise the user's stat by one     MEDITATE, HARDEN, -, GROWTH, -, -, DOUBLE TEAM
///   0x12..0x18  lower the target's by one        GROWL, TAIL WHIP, STRING SHOT, -, -, SAND-ATTACK, SWEET SCENT
///   0x32..0x38  raise the user's by two          SWORDS DANCE, BARRIER, AGILITY, TAIL GLOW, AMNESIA
///   0x3A..0x40  lower the target's by two        CHARM, SCREECH, COTTON SPORE, -, FAKE TEARS
/// </code>
/// <para>
/// Eleven moves land where that shape says they should and none lands anywhere else, and
/// the runs are exactly seven wide: 0x11 is SWIFT and 0x39 is TRANSFORM, both of which
/// are something else entirely. The gaps are stats no move on this cartridge raises or
/// lowers on its own, and a gap is not a counter-example — it is a slot the table has and
/// this game never used.
/// </para>
/// <para>
/// The conditions read the same way. 0x01 is SING, SLEEP POWDER, HYPNOSIS, LOVELY KISS,
/// SPORE and GRASSWHISTLE, which is a list of one thing; 0x42 is POISONPOWDER and POISON
/// GAS; 0x43 is STUN SPORE, THUNDER WAVE and GLARE. And the riders on damaging moves are
/// a run of their own: 0x02 POISON STING and SLUDGE, 0x04 EMBER and FLAMETHROWER, 0x05
/// ICE BEAM and BLIZZARD, 0x06 THUNDERBOLT and BODY SLAM — poison, burn, freeze,
/// paralysis, in a block. 0x44 AURORA BEAM, 0x45 ACID, 0x46 BUBBLEBEAM lower attack,
/// defence and speed, which is the same stat order a third time.
/// </para>
/// <para>
/// Everything not in this table returns <see cref="EffectKind.None"/> and the move does
/// nothing, which is honest: METRONOME and TRANSFORM are not implemented and pretending
/// otherwise would be the one failure this project is arranged against. What is not
/// honest is silence, so the count of moves this table understands is reported.
/// </para>
/// </summary>
public static class MoveEffects
{
    /// <summary>The stats the four runs step through, in the cartridge's own order.</summary>
    private static readonly Stat[] Order =
    [
        Stat.Attack, Stat.Defense, Stat.Speed, Stat.SpAttack, Stat.SpDefense, Stat.Accuracy, Stat.Evasion,
    ];

    private const byte RaiseOne = 0x0A;
    private const byte LowerOne = 0x12;
    private const byte RaiseTwo = 0x32;
    private const byte LowerTwo = 0x3A;

    /// <summary>How wide each run is, which is one slot per stat that has a stage.</summary>
    private const int RunLength = 7;

    /// <summary>What a move does, or nothing when this table does not know it.</summary>
    public static MoveEffect Of(byte effect) => effect switch
    {
        // Conditions, on the target and without a roll. The whole of each group is one
        // condition: 0x01 is the six sleep moves, 0x42 the two powders that poison, 0x43
        // the three that paralyse.
        0x01 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Sleep),
        0x42 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Poison),
        0x43 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Paralysis),

        // TOXIC, alone in its group. Worse than poison in the games — the damage grows
        // each turn — and plain poison here, because growing damage is a thing the engine
        // does not have and half of a move is better than none of it.
        0x21 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Poison),

        // The riders. These sit on moves that already do damage, so they roll against the
        // move's own secondary chance rather than always happening.
        0x02 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Poison),
        0x04 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Burn),
        0x05 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Freeze),
        0x06 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Paralysis),

        _ => Stages(effect),
    };

    /// <summary>The four runs, and the fifth that rides on a hit.</summary>
    private static MoveEffect Stages(byte effect)
    {
        if (In(effect, RaiseOne)) return Stage(effect - RaiseOne, +1, onUser: true);
        if (In(effect, LowerOne)) return Stage(effect - LowerOne, -1, onUser: false);
        if (In(effect, RaiseTwo)) return Stage(effect - RaiseTwo, +2, onUser: true);
        if (In(effect, LowerTwo)) return Stage(effect - LowerTwo, -2, onUser: false);

        // AURORA BEAM, ACID, BUBBLEBEAM: attack, defence, speed. The same order a third
        // time, on moves that do damage as well.
        if (In(effect, 0x44)) return Stage(effect - 0x44, -1, onUser: false);

        return default;
    }

    private static bool In(byte effect, byte start) => effect >= start && effect < start + RunLength;

    private static MoveEffect Stage(int index, int stages, bool onUser) =>
        new(EffectKind.Stage, onUser, Stat: Order[index], Stages: stages);

    /// <summary>
    /// How many of a set of moves this table understands.
    /// <para>
    /// An instrument rather than a nicety. A move whose effect is unknown does nothing at
    /// all, and "nothing happened" is indistinguishable from "the engine has never heard
    /// of this" unless somebody counts.
    /// </para>
    /// </summary>
    public static int Known(IEnumerable<MoveData> moves) =>
        moves.Count(m => Of(m.Effect).Kind != EffectKind.None);
}
