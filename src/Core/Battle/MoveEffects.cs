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

    /// <summary>Changes what the sky is doing.</summary>
    Weather,

    /// <summary>Sleeps, and wakes whole.</summary>
    Sleeps,

    /// <summary>Blocks the move the target just used.</summary>
    Disable,

    /// <summary>Makes the target repeat the move it just used.</summary>
    Encore,

    /// <summary>Every stage on both sides goes back to nothing.</summary>
    Haze,

    /// <summary>Nothing may lower this side's stats while it holds.</summary>
    Mist,

    /// <summary>Nothing may afflict this side while it holds.</summary>
    Safeguard,

    /// <summary>The next move this one uses cannot miss.</summary>
    TakeAim,

    /// <summary>Inflicts a lasting condition.</summary>
    Status,

    /// <summary>Moves a stat stage up or down.</summary>
    Stage,

    /// <summary>
    /// Every one of the five, on the user, at once.
    /// <para>
    /// Its own kind rather than five <see cref="Stage"/> effects, because it is one roll:
    /// ANCIENTPOWER either raises all five or raises none, and five separate chances would
    /// be a move that usually raises two of them.
    /// </para>
    /// </summary>
    AllStages,

    /// <summary>The user's own condition, cleared.</summary>
    Refresh,

    /// <summary>Sharper until it leaves the field.</summary>
    Focus,

    /// <summary>Never takes the last point.</summary>
    LeavesOne,

    /// <summary>Halves what comes at this side for a count of turns.</summary>
    Screen,

    /// <summary>Takes a share of its target's health every turn and gives it away.</summary>
    Seed,

    /// <summary>Leaves the fight, whatever the speeds say.</summary>
    Leave,

    /// <summary>Takes down whatever the other side is hiding behind.</summary>
    BreaksWalls,

    /// <summary>Takes away what the other side is carrying.</summary>
    KnocksOff,

    /// <summary>Shakes off everything that is holding or draining its user.</summary>
    Spins,

    /// <summary>Makes the other side findable, whatever it is and however well it is hiding.</summary>
    Identifies,

    /// <summary>Puts health back, by an amount the sky decides.</summary>
    HealByWeather,

    /// <summary>Takes uses off whatever the other side last did.</summary>
    Spite,

    /// <summary>Shakes somebody out of being unable to move properly.</summary>
    Rouse,

    /// <summary>Hurts whoever is asleep, every turn, until they wake.</summary>
    Nightmare,

    /// <summary>Puts somebody to sleep at the end of the turn after next.</summary>
    Yawn,

    /// <summary>Puts health back every turn, and gives up leaving in exchange.</summary>
    Ingrain,

    /// <summary>Everybody who hears it goes down in three turns.</summary>
    Perish,

    /// <summary>Makes somebody stronger and too confused to use it.</summary>
    Goad,

    /// <summary>Leaves them nothing to do but attack.</summary>
    Taunt,

    /// <summary>Leaves them unable to do the same thing twice running.</summary>
    Torment,

    /// <summary>Survives whatever lands this turn on a single point.</summary>
    Endure,

    /// <summary>Takes whoever finished it down as well.</summary>
    Bond,

    /// <summary>Both sides end up on the same health, whatever that comes to.</summary>
    Split,

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

    /// <summary>
    /// More than one stat, when a move moves several at once.
    /// <para>
    /// Beside <see cref="Stat"/> rather than replacing it, because the overwhelming majority
    /// of these groups move exactly one and a list of one everywhere would be ceremony. Null
    /// means "the single one"; <see cref="EffectKind.AllStages"/> with no list means all five.
    /// </para>
    /// </summary>
    IReadOnlyList<Stat>? Many = null,
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
        // METAL CLAW and METEOR MASH: a chance, on the record, of the user coming out of
        // it stronger. The chance is read and the stat is not — which stat it raises is in
        // the game's code — so the stat is modelled and the odds are the cartridge's.
        0x8B => new MoveEffect(EffectKind.Stage, OnUser: true, Stat: Stat.Attack, Stages: 1),

        // ANCIENTPOWER and SILVER WIND: the same shape, on all five at once.
        0x8C => new MoveEffect(EffectKind.AllStages, OnUser: true, Stages: 1),

        // OVERHEAT and PSYCHO BOOST: the first two moves in this table that cost their own
        // user something. Every stat drop the engine had ever applied came from the other
        // side, which is why both shields ask "was this somebody else" — and there was a
        // test asserting no such move existed, because until this line none did.
        0xCC => new MoveEffect(EffectKind.Stage, OnUser: true, Stat: Stat.SpAttack, Stages: -2),

        // The four that raise two of the user's own stats at once. Which two is in the
        // game's code and is modelled; that they are one act rather than two is the shape of
        // the group, and it is why they go through the same one-roll path all five do.
        0xCE => Several(EffectKind.AllStages, +1, Stat.Defense, Stat.SpDefense),
        0xD0 => Several(EffectKind.AllStages, +1, Stat.Attack, Stat.Defense),
        0xD3 => Several(EffectKind.AllStages, +1, Stat.SpAttack, Stat.SpDefense),
        0xD4 => Several(EffectKind.AllStages, +1, Stat.Attack, Stat.Speed),

        // And one that takes two off somebody else.
        0xCD => new MoveEffect(EffectKind.AllStages, OnUser: false, Stages: -1, Many: [Stat.Attack, Stat.Defense]),

        // SUPERPOWER: it lands, and then costs its user the two stats it just used.
        0xB6 => Several(EffectKind.AllStages, -1, Stat.Attack, Stat.Defense),

        // Two that move one of the user's own, and one chance on a damaging move.
        0x6C => new MoveEffect(EffectKind.Stage, OnUser: true, Stat: Stat.Evasion, Stages: 1),
        0x9C => new MoveEffect(EffectKind.Stage, OnUser: true, Stat: Stat.Defense, Stages: 1),
        0x8A => new MoveEffect(EffectKind.Stage, OnUser: true, Stat: Stat.Defense, Stages: 1),

        // The four that take a turn to wind up. The winding is machinery this engine has had
        // since FLY, and these four were never pointed at it.
        0x27 or 0x4B or 0x91 or 0x97 => new MoveEffect(EffectKind.TwoTurn, OnUser: true),

        // SPLASH, which is the one move in this game whose whole joke is that it does
        // nothing. It is not silent — it is finished.
        0x55 => new MoveEffect(EffectKind.Nothing, OnUser: true),

        // Two more ways to inflict what this engine already inflicts. Neither does damage,
        // which is the only thing that made them look different from the groups above them.
        0xA7 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Burn),
        0xC7 => new MoveEffect(EffectKind.Confuse, OnUser: false),

        // REFRESH: the user's own condition, gone.
        0xC1 => new MoveEffect(EffectKind.Refresh, OnUser: true),

        // FOCUS ENERGY: sharper until it leaves.
        0x2F => new MoveEffect(EffectKind.Focus, OnUser: true),

        // FALSE SWIPE: always leaves one.
        0x65 => new MoveEffect(EffectKind.LeavesOne, OnUser: false),

        // The two screens, told apart by which stat they are about — Defense for the one
        // that answers a physical hit and SpDefense for the other. Which is which is in the
        // game's code; that they are a pair with one shape is the record.
        0x41 => new MoveEffect(EffectKind.Screen, OnUser: true, Stat: Stat.Defense),
        0x23 => new MoveEffect(EffectKind.Screen, OnUser: true, Stat: Stat.SpDefense),

        // LEECH SEED, which needed the end-of-turn hook the berries were built on.
        0x54 => new MoveEffect(EffectKind.Seed, OnUser: false),

        // TELEPORT, which is running away by another name and reaches the same code.
        0x99 => new MoveEffect(EffectKind.Leave, OnUser: true),

        // The three that heal by however much the sky allows. One group by behaviour, three
        // effect bytes, and the only reason they are three is that the cartridge gives each
        // its own — which is read, so they are written as read rather than folded.
        0x84 or 0x85 or 0x86 => new MoveEffect(EffectKind.HealByWeather, OnUser: true),

        // SPITE, which needed nothing except PP, and PP has been spent here since moves ran
        // out.
        0x64 => new MoveEffect(EffectKind.Spite, OnUser: false),

        // Five that happen later, which is the end-of-turn hook the berries were built on
        // being used for the fifth, sixth, seventh and eighth time.
        0x6B => new MoveEffect(EffectKind.Nightmare, OnUser: false),
        0xBB => new MoveEffect(EffectKind.Yawn, OnUser: false),
        0xB5 => new MoveEffect(EffectKind.Ingrain, OnUser: true),
        0x72 => new MoveEffect(EffectKind.Perish, OnUser: false),

        0xAF => new MoveEffect(EffectKind.Taunt, OnUser: false),
        0xA5 => new MoveEffect(EffectKind.Torment, OnUser: false),
        0x74 => new MoveEffect(EffectKind.Endure, OnUser: true),
        0x62 => new MoveEffect(EffectKind.Bond, OnUser: true),
        0x5B => new MoveEffect(EffectKind.Split, OnUser: false),

        // Two that make somebody stronger and then too confused to use it. Which stat is in
        // the game's code and is modelled; that they are one act is the group.
        0x76 => new MoveEffect(EffectKind.Goad, OnUser: false, Stat: Stat.Attack, Stages: 2),
        0xA6 => new MoveEffect(EffectKind.Goad, OnUser: false, Stat: Stat.SpAttack, Stages: 2),

        // Four that answer other moves, and every one of them was impossible until the thing
        // it answers existed. Three of the four became writable in the last three milestones.
        0xBA => new MoveEffect(EffectKind.BreaksWalls, OnUser: false),
        0xBC => new MoveEffect(EffectKind.KnocksOff, OnUser: false),
        0x81 => new MoveEffect(EffectKind.Spins, OnUser: true),
        0x71 => new MoveEffect(EffectKind.Identifies, OnUser: false),

        // The four whose power is not the number on their record. Everything they do happens
        // where the damage is worked out, so there is nothing for the effect handler to do —
        // but they are named here so they are finished rather than silent.
        MovePower.Cornered or MovePower.Spending or MovePower.Regardless or MovePower.Hidden
            or MovePower.Overhead or MovePower.Whirling or MovePower.Underfoot =>
            new MoveEffect(EffectKind.Nothing, OnUser: true),

        // SMELLINGSALT hits harder for the same reason and then puts right what it hit them
        // for, which is the one of these five with a second half.
        MovePower.Rousing => new MoveEffect(EffectKind.Rouse, OnUser: false),

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

        // The sky. Four groups of exactly one move each, which is unusual enough in this
        // table to be worth saying: nothing else in the cartridge shares any of them, so
        // naming them costs nothing and guesses nothing.
        //
        //   0x73  1 move   SANDSTORM
        //   0x88  1 move   RAIN DANCE
        //   0x89  1 move   SUNNY DAY
        //   0xA4  1 move   HAIL
        //
        // Which sky each brings is decided by Skies.Of rather than here, because the same
        // four numbers are wanted by the code that applies it and a second copy of the
        // mapping is a second thing to keep in step.
        0x73 or 0x88 or 0x89 or 0xA4 => new MoveEffect(EffectKind.Weather, OnUser: true),

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

        // The two that take a choice away. One removes an option, the other removes all
        // the others, and both need the same thing: what the target just did.
        //
        //   0x56   1 move   DISABLE
        //   0x5A   1 move   ENCORE
        //
        // Both aim at somebody else in their records, and neither has anything in its
        // record about how long it lasts — so the count of turns is modelled and marked
        // as such where it is written, and nothing else about them is.
        0x56 => new MoveEffect(EffectKind.Disable, OnUser: false),
        0x5A => new MoveEffect(EffectKind.Encore, OnUser: false),

        // The four that switch off a rule this engine already follows.
        //
        //   0x19   1 move   HAZE        — the stages stop counting
        //   0x2E   1 move   MIST        — nothing may lower ours
        //   0x7C   1 move   SAFEGUARD   — nothing may afflict ours
        //   0x5E   2 moves  MIND READER, LOCK-ON — the next one cannot miss
        //
        // They are together because they are one idea four times: none of them does
        // anything to anybody, each of them stops something else from happening. That
        // shape is why they were cheap to write — every rule they turn off was already
        // there, in one place, with one caller.
        //
        // Their records agree with the reading as far as records can. All four have no
        // power, all four are status moves, and the first three aim at the user's own
        // side by the target byte — a move that shields cannot be aimed at somebody
        // else. What is modelled is how long two of them hold, and that is written where
        // it is chosen.
        0x19 => new MoveEffect(EffectKind.Haze, OnUser: true),
        0x2E => new MoveEffect(EffectKind.Mist, OnUser: true),
        0x7C => new MoveEffect(EffectKind.Safeguard, OnUser: true),
        0x5E => new MoveEffect(EffectKind.TakeAim, OnUser: true),

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

    /// <summary>The five a stage can be raised or lowered on, which is every stat but health.</summary>
    public static readonly IReadOnlyList<Stat> Five =
        [Stat.Attack, Stat.Defense, Stat.Speed, Stat.SpAttack, Stat.SpDefense];

    private static MoveEffect Stage(int index, int stages, bool onUser) =>
        new(EffectKind.Stage, onUser, Stat: Order[index], Stages: stages);

    private static MoveEffect Several(EffectKind kind, int stages, params Stat[] stats) =>
        new(kind, OnUser: true, Stages: stages, Many: stats);

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
