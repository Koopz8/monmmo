namespace PokeMmo.Core.Battle;

/// <summary>What a move does besides damage, and to whom.</summary>
public enum EffectKind
{
    /// <summary>Nothing this engine knows how to do.</summary>
    None,

    /// <summary>
    /// There is nothing more to do, and that is the answer rather than a gap.
    /// <para>
    /// Told apart from <see cref="None"/> on purpose, because the two used to be the same
    /// value and a count of "moves this engine understands" cannot be right while "hits
    /// and does nothing else" and "has a part nobody has written" share an answer. Effect
    /// 0 is 23 moves that only hit. 0x11 never misses and 0x67 moves out of turn, and both
    /// of those are in the record already — the accuracy field and the priority field —
    /// so by the time the engine looks at the effect byte there is genuinely nothing left.
    /// </para>
    /// </summary>
    Nothing,

    /// <summary>Cannot be hit this turn.</summary>
    Guard,

    /// <summary>Sleeps, and wakes whole.</summary>
    Sleeps,

    /// <summary>Inflicts a lasting condition.</summary>
    Status,

    /// <summary>Moves a stat stage up or down.</summary>
    Stage,

    /// <summary>Lands several times in one turn.</summary>
    MultiHit,

    /// <summary>Lands a critical hit far more often than usual.</summary>
    HighCritical,

    /// <summary>Makes the target lose its turn, if it has not taken it yet.</summary>
    Flinch,

    /// <summary>Gives the user back a share of what it dealt.</summary>
    Drain,

    /// <summary>Costs the user a share of what it dealt.</summary>
    Recoil,

    /// <summary>Restores the user's own health.</summary>
    Heal,

    /// <summary>Muddles the target, which may then hurt itself instead of acting.</summary>
    Confuse,

    /// <summary>Costs the user its next turn.</summary>
    Recharge,

    /// <summary>Spends one turn going somewhere unreachable and lands on the next.</summary>
    TwoTurn,

    /// <summary>Repeats itself for a few turns and leaves the user confused.</summary>
    LockedIn,

    /// <summary>Holds the target for a few turns, hurting it at the end of each.</summary>
    Trap,

    /// <summary>Ends it outright, however much was left.</summary>
    Knockout,

    /// <summary>Deals as much as the user's level.</summary>
    LevelDamage,

    /// <summary>Takes half of what the target has left.</summary>
    HalfTheirHealth,

    /// <summary>Brings the target down to whatever the user has left.</summary>
    DownToMine,

    /// <summary>Stops the target leaving, for as long as it is standing there.</summary>
    NoEscape,

    /// <summary>Sends the target off, which ends a fight with something wild in it.</summary>
    BlowAway,

    /// <summary>Takes what the target is carrying, if the user is carrying nothing.</summary>
    Steal,

    /// <summary>Lands exactly twice, however the roll goes.</summary>
    Twice,

    /// <summary>Costs the user something when it misses, and nothing when it lands.</summary>
    CrashOnMiss,

    /// <summary>The user faints, whatever else the turn came to.</summary>
    UserFaints,
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

        // What a hit can carry besides a condition. Each of these is read off its group's
        // membership, the same way the four stat runs above were: a group is what its
        // members have in common, and these six groups each have something obvious.
        //
        //   0x1D  12 moves  DOUBLESLAP, COMET PUNCH, FURY ATTACK, PIN MISSILE, BARRAGE
        //   0x2B   8 moves  KARATE CHOP, RAZOR LEAF, CRABHAMMER, SLASH, CROSS CHOP
        //   0x1F   6 moves  ROLLING KICK, HEADBUTT, BITE, BONE CLUB, ROCK SLIDE
        //   0x96   4 moves  STOMP, NEEDLE ARM, ASTONISH, EXTRASENSORY
        //   0x03   4 moves  ABSORB, MEGA DRAIN, LEECH LIFE, GIGA DRAIN
        //   0x30   3 moves  TAKE DOWN, SUBMISSION, STRUGGLE
        //   0x20   2 moves  RECOVER, SLACK OFF
        //   0x9D   2 moves  SOFTBOILED, MILK DRINK
        //
        // The membership is read; the amounts are not, and are marked as modelled where
        // the engine applies them. Nothing in a move's record says how many times
        // DOUBLESLAP lands or what share of the damage ABSORB gives back — those numbers
        // are in the game's code, which this project does not read.
        0x1D => new MoveEffect(EffectKind.MultiHit, OnUser: false),
        //
        // Twice, exactly. A separate group from 0x1D and a separate kind, because two is
        // not a roll: 0x1D's dozen land two to five times and these three land twice
        // every time. Nothing in a record says either number — both are modelled, and
        // this one is the easier of the two to be sure of, since a group of two whose
        // members are DOUBLE KICK and BONEMERANG is a group about the number two.
        //
        // TWINEEDLE is the third and carries a rider as well, which is why the poison is
        // written here rather than left to the group's shape. Its record says a secondary
        // chance of twenty; every other member of these two groups says nought.
        0x2C => new MoveEffect(EffectKind.Twice, OnUser: false),
        0x4D => new MoveEffect(EffectKind.Twice, OnUser: false, Status: StatusCondition.Poison),
        //
        // The two that hurt the user when they miss. JUMP KICK and HI JUMP KICK, alone in
        // their group, and the only moves on this cartridge whose accuracy is a risk to
        // the one using them.
        0x2D => new MoveEffect(EffectKind.CrashOnMiss, OnUser: true),
        //
        // And the two that end the user. SELFDESTRUCT and EXPLOSION — two hundred power
        // and two hundred and fifty, the two largest numbers in the whole move table, and
        // a group of exactly two. What it costs is not a number that has to be modelled:
        // it is everything.
        0x07 => new MoveEffect(EffectKind.UserFaints, OnUser: true),
        //
        // Four groups this engine settled by having already settled a type. Every FIRE
        // group it applies a status to burns and is FIRE the whole way through; every
        // POISON group it applies a status to poisons and is POISON the whole way
        // through. These four are damaging, one type throughout, and carry a secondary
        // chance in every record — the same claim asked a second time, of the same
        // engine, about the same types.
        //
        // Not read off the cartridge, which says only which group a move is in. Derived
        // from what this engine had already committed to, and derived by a rule rather
        // than by hand: RidersByType.Accounted names exactly these four and refuses
        // everything else, including the groups whose evidence is a mixed-type precedent
        // — THUNDER carries a thirty percent rider that stays unnamed here, because the
        // only group that would name it also holds BODY SLAM, LICK and DRAGONBREATH and
        // so is evidence for four contradictory things.
        //
        // The chance is the record's own. SACRED FIRE burns half the time and FLAME
        // WHEEL one time in ten, because that is what their records say and nothing here
        // has to know it.
        0x7D or 0xC8 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Burn),
        0xCA or 0xD1 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Poison),
        0x2B => new MoveEffect(EffectKind.HighCritical, OnUser: false),
        0x1F or 0x96 => new MoveEffect(EffectKind.Flinch, OnUser: false),
        0x03 => new MoveEffect(EffectKind.Drain, OnUser: true),
        //
        // 0xC6 is DOUBLE-EDGE and VOLT TACKLE, and it is the same idea as 0x30 with a
        // steeper price. The group was sitting one line away from working for as long as
        // recoil has existed, doing nothing, because nobody had read its two members.
        0x30 or 0xC6 => new MoveEffect(EffectKind.Recoil, OnUser: true),
        0x20 or 0x9D => new MoveEffect(EffectKind.Heal, OnUser: true),

        // Confusion, and it arrives both ways — which is the distinction this table was
        // built to keep. 0x31 is the three moves that do nothing else and always land it;
        // 0x4C is the six that damage and carry it on a roll.
        //
        //   0x31   3 moves  SUPERSONIC, CONFUSE RAY, SWEET KISS
        //   0x4C   6 moves  PSYBEAM, CONFUSION, DIZZY PUNCH, DYNAMICPUNCH, SIGNAL BEAM, WATER PULSE
        0x31 or 0x4C => new MoveEffect(EffectKind.Confuse, OnUser: false),

        // The four that take more than one turn, and the reason a fight with HYPER BEAM
        // in it played out exactly like a fight without one. Read off membership like
        // everything above — each of these groups is a list of one idea:
        //
        //   0x50   4 moves  HYPER BEAM, BLAST BURN, HYDRO CANNON, FRENZY PLANT
        //   0x9B   4 moves  FLY, DIG, DIVE, BOUNCE
        //   0x1B   3 moves  THRASH, PETAL DANCE, OUTRAGE
        //   0x2A   6 moves  BIND, WRAP, FIRE SPIN, CLAMP, WHIRLPOOL, SAND TOMB
        //
        // Two of them corroborate themselves out of the records. The 0x50 four are all
        // 150 power with 5 PP and nothing else on the cartridge is; the 0x2A six all
        // carry a secondary chance of 100, which on a move that inflicts no condition is
        // the record saying "this always does its other thing".
        //
        // How long each lasts is not in any record and is modelled where it is applied,
        // beside the multi-hit count and the drain share, which were arrived at the same
        // way and are marked the same way.
        0x50 => new MoveEffect(EffectKind.Recharge, OnUser: true),
        0x9B => new MoveEffect(EffectKind.TwoTurn, OnUser: true),
        0x1B => new MoveEffect(EffectKind.LockedIn, OnUser: true),
        0x2A => new MoveEffect(EffectKind.Trap, OnUser: false),

        // The moves whose record says one power. One is not a power — it is the record
        // saying the number is somewhere else, and it says it about twenty-one moves in
        // seventeen groups, every one of them entirely: not a single group on this
        // cartridge mixes a power of one with a real one.
        //
        //   BIDE, GUILLOTINE, SUPER FANG, DRAGON RAGE, SEISMIC TOSS, PSYWAVE, COUNTER,
        //   FLAIL, RETURN, PRESENT, FRUSTRATION, MAGNITUDE, SONICBOOM, HIDDEN POWER,
        //   MIRROR COAT, ENDEAVOR, LOW KICK
        //
        // Four of them can be answered without inventing anything, because "somewhere
        // else" turns out to be inside the fight: nothing at all, the user's level, the
        // target's health, the user's health.
        //
        //   0x26   4 moves  GUILLOTINE, HORN DRILL, FISSURE, SHEER COLD
        //   0x57   2 moves  SEISMIC TOSS, NIGHT SHADE
        //   0x28   1 move   SUPER FANG
        //   0xBD   1 move   ENDEAVOR
        //
        // The rest stay silent on purpose. DRAGON RAGE's forty and SONICBOOM's twenty
        // are in the game's code and nowhere in its data; writing them here from memory
        // of another game is the one thing this project keeps a standing rule against,
        // and a wrong number that looks right is worse than a move that says so.
        0x26 => new MoveEffect(EffectKind.Knockout, OnUser: false),
        0x57 => new MoveEffect(EffectKind.LevelDamage, OnUser: false),
        0x28 => new MoveEffect(EffectKind.HalfTheirHealth, OnUser: false),
        0xBD => new MoveEffect(EffectKind.DownToMine, OnUser: false),

        // And the two that are about leaving, which had nothing to be about until there
        // was a way to leave. Both read off membership, both lists of one idea:
        //
        //   0x6A   3 moves  SPIDER WEB, MEAN LOOK, BLOCK
        //   0x1C   2 moves  WHIRLWIND, ROAR
        0x6A => new MoveEffect(EffectKind.NoEscape, OnUser: false),
        0x1C => new MoveEffect(EffectKind.BlowAway, OnUser: false),

        // And the one group that is about a held item rather than about what holding it
        // does — which is the only kind this project can answer, because the hold effects
        // themselves are numbers whose meaning lives in the game's code.
        //
        //   0x69   2 moves  THIEF, COVET
        0x69 => new MoveEffect(EffectKind.Steal, OnUser: false),

        // The three that have nothing left to say by the time anybody reads the effect
        // byte, which is a different answer from not knowing.
        //
        //   0x00  23 moves  POUND, SCRATCH, CUT, WING ATTACK — they hit, and that is all
        //   0x11   6 moves  SWIFT, AERIAL ACE — never miss, and their records carry no accuracy
        //   0x67   3 moves  QUICK ATTACK, EXTREMESPEED — move first, off the priority field
        0x00 or 0x11 or 0x67 => new MoveEffect(EffectKind.Nothing, OnUser: false),

        // Cannot be hit this turn.
        //
        //   0x6F   2 moves  PROTECT, DETECT
        //
        // Two moves, one idea, and no number to invent: what it does is not a magnitude,
        // it is a yes or a no. The games make it fail more often the more it is used in a
        // row, and that share is in their code — so it is modelled as always working and
        // this sentence is the note saying which half is which.
        0x6F => new MoveEffect(EffectKind.Guard, OnUser: true),

        // Sleeps, and wakes whole.
        //
        //   0x25   1 move   REST
        //
        // Its record aims it at the user — target byte 0x10, the group of 67 whose whole
        // effect is on whoever used it — so nothing here has to decide who it lands on.
        // How much it gives back is not modelled either: all of it is the only amount a
        // move that puts you to sleep to heal could mean, and the record's own zero power
        // says the number is not in the data. How long the sleep runs is this engine's
        // ordinary sleep, which is what every other sleep in it already gets.
        0x25 => new MoveEffect(EffectKind.Sleeps, OnUser: true, Status: StatusCondition.Sleep),

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

    /// <summary>
    /// True when this effect has a part nobody has written yet.
    /// <para>
    /// The question the engine asks at run time, so that a move which quietly did half of
    /// what it should can say so instead. Reading it off the kind rather than off a list
    /// means the answer changes by itself the moment an effect is modelled.
    /// </para>
    /// </summary>
    public static bool IsSilent(byte effect) => Of(effect).Kind == EffectKind.None;
}
