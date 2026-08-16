using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>Non-volatile status. Only one at a time.</summary>
public enum StatusCondition
{
    None,
    Poison,
    Burn,
    Paralysis,
    Sleep,
    Freeze,
}

/// <summary>
/// One creature as it exists inside a battle: its computed stats, its current state,
/// and the stat stages it has accumulated.
/// </summary>
public sealed class Battler
{
    private readonly Dictionary<Stat, int> _stages = [];

    public Battler(
        SpeciesData species,
        int level,
        Nature nature = Nature.Hardy,
        string? nickname = null,
        Effort? effort = null,
        Genes? genes = null)
    {
        Species = species;
        Level = level;
        Nature = nature;
        Nickname = nickname;
        Name = nickname ?? species.Name;
        Effort = effort ?? Effort.None;

        // Perfect when nobody said otherwise, which is what every creature in this
        // project was before there was anything to say.
        Born = genes ?? Genes.Perfect;

        MaxHp = Stats.Hp(species.BaseHp, level, Born.In(Stat.Hp), Effort.In(Stat.Hp));
        CurrentHp = MaxHp;

        Attack = Stats.Other(Stat.Attack, species.BaseAttack, level, nature, Born.In(Stat.Attack), Effort.In(Stat.Attack));
        Defense = Stats.Other(Stat.Defense, species.BaseDefense, level, nature, Born.In(Stat.Defense), Effort.In(Stat.Defense));
        Speed = Stats.Other(Stat.Speed, species.BaseSpeed, level, nature, Born.In(Stat.Speed), Effort.In(Stat.Speed));
        SpAttack = Stats.Other(Stat.SpAttack, species.BaseSpAttack, level, nature, Born.In(Stat.SpAttack), Effort.In(Stat.SpAttack));
        SpDefense = Stats.Other(Stat.SpDefense, species.BaseSpDefense, level, nature, Born.In(Stat.SpDefense), Effort.In(Stat.SpDefense));
    }

    /// <summary>
    /// What this one has done, in the only form a battle can see it: six numbers that
    /// were already an argument to every stat this class computes and that nothing had
    /// ever supplied.
    /// </summary>
    public Effort Effort { get; }

    /// <summary>
    /// What this one was born with: the other half of what makes two of a species
    /// different, and the half that never changes.
    /// </summary>
    public Genes Born { get; }

    /// <summary>Which sex this one is, as its save recorded it.</summary>
    public Gender Sex { get; init; }

    /// <summary>
    /// Which of its species' two abilities this one was born with, as a slot.
    /// <para>
    /// The slot rather than the ability, because the slot is what the dice decided and the
    /// ability is a lookup. Storing the lookup as well would be two copies of one fact,
    /// and the second copy is the one that goes stale.
    /// </para>
    /// </summary>
    public int AbilitySlot { get; init; }

    /// <summary>What that comes to, which is the number every rule in the fight asks for.</summary>
    /// <summary>
    /// An ability put on this one by a move, for the length of the fight.
    /// <para>
    /// Null almost always, and when it is not it wins. Two moves in this game move an ability
    /// from one creature to another, and until they were written an ability was the one thing
    /// about a creature a fight could not change — it was a lookup on the species and the
    /// slot it was born with, with nowhere for an answer of its own to live.
    /// </para>
    /// <para>
    /// It goes when its owner does, like every other thing a fight starts. An ability that
    /// followed somebody out of the door would be a change to the creature rather than to the
    /// fight, and this project does not write to saves from inside a battle.
    /// </para>
    /// </summary>
    public int? BorrowedAbility { get; set; }

    public int Ability => BorrowedAbility ?? Abilities.Of(Species, AbilitySlot);

    public SpeciesData Species { get; }

    /// <summary>
    /// The name a player gave this one, or null.
    /// <para>
    /// Kept separate from <see cref="Name"/> because the server holds species with no
    /// names at all: it has to be able to store and return a nickname without ever
    /// being able to fall back to a species name it does not have.
    /// </para>
    /// </summary>
    public string? Nickname { get; }

    public string Name { get; }
    public int Level { get; }
    public Nature Nature { get; }

    public int MaxHp { get; }
    public int CurrentHp { get; private set; }

    public int Attack { get; }
    public int Defense { get; }
    public int Speed { get; }
    public int SpAttack { get; }
    public int SpDefense { get; }

    /// <summary>The moves this battler knows, in slot order.</summary>
    public List<MoveData> Moves { get; } = [];

    /// <summary>
    /// What is left of each move, in the same order as <see cref="Moves"/>.
    /// <para>
    /// Read, not modelled: every move record on the cartridge carries its own PP, and
    /// that field has travelled in the rules file since there was one without anything
    /// ever spending it. A move with none left cannot be chosen, and a creature with
    /// nothing left struggles.
    /// </para>
    /// <para>
    /// Filled lazily from the moves themselves, because a battler is built by adding
    /// moves to a list and there is no moment afterwards that is obviously "now it is
    /// ready" — one that has never been asked has full PP, which is the same answer.
    /// </para>
    /// </summary>
    private readonly Dictionary<int, int> _spent = [];

    /// <summary>How many uses of one slot are left.</summary>
    public int PpLeft(int slot) =>
        MoveAt(slot) is { } move ? Math.Max(0, move.Pp - _spent.GetValueOrDefault(slot)) : 0;

    /// <summary>Spends one use of a slot, if there is one to spend.</summary>
    public bool Spend(int slot)
    {
        if (PpLeft(slot) <= 0) return false;

        _spent[slot] = _spent.GetValueOrDefault(slot) + 1;

        return true;
    }

    /// <summary>Puts every use back, which is what resting anywhere does.</summary>
    public void RefillPp() => _spent.Clear();

    /// <summary>
    /// Puts some uses back into one slot, and says how many actually went in.
    /// <para>
    /// Says how many rather than whether, for the reason a bag says how many items went in:
    /// something that puts ten back into a move missing three has put three back, and
    /// whoever asked needs to know that before deciding it was worth using up.
    /// </para>
    /// </summary>
    public int Refill(int slot, int uses)
    {
        if (uses <= 0 || MoveAt(slot) is not { } move) return 0;

        int missing = _spent.GetValueOrDefault(slot);
        int back = Math.Min(uses, missing);

        if (back <= 0) return 0;

        _spent[slot] = missing - back;

        return back;
    }

    /// <summary>The first slot with nothing left in it, or nothing.</summary>
    public int? FirstSpentSlot()
    {
        for (int slot = 0; slot < Moves.Count; slot++)
        {
            if (PpLeft(slot) <= 0) return slot;
        }

        return null;
    }

    /// <summary>Every stat this one has been made worse at, back where it started.</summary>
    public int RaiseWhatWasLowered()
    {
        var lowered = _stages.Where(s => s.Value < 0).Select(s => s.Key).ToList();

        foreach (Stat stat in lowered) _stages.Remove(stat);

        return lowered.Count;
    }

    /// <summary>
    /// Sets what is left of each slot from a save, ignoring anything the moves do not
    /// reach and treating a missing entry as full.
    /// </summary>
    public void RestorePp(IReadOnlyList<int> left)
    {
        _spent.Clear();

        for (int slot = 0; slot < Moves.Count && slot < left.Count; slot++)
        {
            int missing = Math.Clamp(Moves[slot].Pp - left[slot], 0, Moves[slot].Pp);

            if (missing > 0) _spent[slot] = missing;
        }
    }

    /// <summary>
    /// True when nothing this one knows has a use left in it.
    /// <para>
    /// A creature with no moves at all counts as spent too. That is not a state the
    /// cartridge can produce, and it is one a rules file with a gap in it can.
    /// </para>
    /// </summary>
    public bool IsSpent => Moves.Count == 0 || Enumerable.Range(0, Moves.Count).All(s => PpLeft(s) <= 0);

    public StatusCondition Status { get; set; }

    /// <summary>
    /// What this one is carrying, as an item id, or nothing.
    /// <para>
    /// Every trainer's party on the cartridge says what its members hold, and this
    /// project has been extracting that number, writing it into the rules file and never
    /// reading it since trainers existed.
    /// </para>
    /// <para>
    /// What a held item <em>does</em> is another matter and mostly not answerable: the
    /// item record carries a hold effect as a number, and what each number means is in
    /// the game's code. So this is the item, not its effect — which is enough for the
    /// one thing in the move table that is about held items rather than about their
    /// effects.
    /// </para>
    /// </summary>
    public int Holding { get; set; }

    /// <summary>
    /// What that item's record says, when the rules were to hand.
    /// <para>
    /// Beside the id rather than instead of it, because the two answer different questions
    /// and one of them is answerable without a rules file. <see cref="Holding"/> is what a
    /// save carries and what THIEF moves; this is the two bytes that say what carrying it is
    /// worth, and it is null on any battler built without rules — a battle that does not know
    /// what an item does is a battle where items do nothing, rather than one that throws.
    /// </para>
    /// </summary>
    public ItemData? Carried { get; set; }

    /// <summary>Turns of sleep remaining, counted down at the start of each of this battler's turns.</summary>
    public int SleepTurns { get; set; }

    /// <summary>
    /// Turns of confusion remaining.
    /// <para>
    /// Beside <see cref="Status"/> rather than one of its values, because the games let
    /// you be poisoned and confused at once and a condition that replaced poison would be
    /// a different rule. It lives on the battler rather than on the battle so that it
    /// follows the one it happened to: switching out builds a new battler, which is
    /// exactly where confusion should stop.
    /// </para>
    /// <para>
    /// And it is never written down. <c>BattleFactory.Save</c> does not carry it, which
    /// is right — walking out of a battle confused is not something these games do.
    /// </para>
    /// </summary>
    public int ConfusedTurns { get; set; }

    public bool IsConfused => ConfusedTurns > 0;

    /// <summary>
    /// True while the next turn is owed to the last one.
    /// <para>
    /// HYPER BEAM's whole cost. On the battler beside confusion and for the same reason:
    /// it belongs to the creature that did it, so switching out ends it — which is the
    /// rule these games have, and it falls out of the arrangement rather than needing a
    /// line.
    /// </para>
    /// </summary>
    public bool MustRecharge { get; set; }

    /// <summary>Which move the debt is owed to, so the sentence can name it.</summary>
    public int RechargingAfter { get; set; }

    /// <summary>
    /// Which move slot this battler has no choice about, and for how many more turns.
    /// <para>
    /// One field for two things that look different and are the same: FLY has gone
    /// somewhere and must come down, THRASH has started and must finish. Both mean the
    /// player is not asked this turn, and the engine takes the move it is holding.
    /// </para>
    /// </summary>
    /// <summary>
    /// The last move this one actually used, as a slot, or nothing.
    /// <para>
    /// Needed by the two moves that take a choice away — one blocks what you just did,
    /// the other makes you do it again — and by nothing else. Written when a move is
    /// made rather than when it lands, because a miss is still what you did.
    /// </para>
    /// </summary>
    public int? LastSlot { get; set; }

    /// <summary>
    /// The last move this one actually used, as the move itself.
    /// <para>
    /// The slot beside it is not enough for the moves that copy. A slot is an index into
    /// <em>this</em> creature's four, and the thing MIRROR MOVE wants is the move the
    /// <em>other</em> one used — which is an index into a list this side has no business
    /// reading, and which may not be four long, and which stops being true the moment
    /// somebody switches out.
    /// </para>
    /// <para>
    /// So the move travels rather than the number. It is the same record every other part of
    /// this engine works in, and it is read off the cartridge like all of them.
    /// </para>
    /// </summary>
    public MoveData? LastMove { get; set; }

    /// <summary>
    /// How much this one has been hurt by a move so far this turn, and of which kind.
    /// <para>
    /// Cleared at the top of every turn. Six moves in this game are answers to being hit
    /// rather than things done on their own account, and all six need the same two facts:
    /// whether it happened, and — for the two that give it back doubled — how much and by
    /// which of the two kinds.
    /// </para>
    /// <para>
    /// It belongs to the creature rather than to the battle because the answer is different
    /// for each of them, and it is a number rather than a flag because giving back twice
    /// what you took requires knowing what you took.
    /// </para>
    /// </summary>
    public int HurtThisTurn { get; set; }

    /// <summary>Which kind did it, or nothing when nothing has.</summary>
    public DamageCategory? HurtThisTurnBy { get; set; }

    /// <summary>
    /// How many turns this one has been on the field, counted from nought.
    /// <para>
    /// One move cares, and it cares a great deal: it only works on the turn its user
    /// arrives. That is what makes it a move somebody leads with rather than a free flinch
    /// every turn, and it is the only thing in this engine that has ever needed to know how
    /// long somebody has been standing there.
    /// </para>
    /// </summary>
    public int TurnsOut { get; set; }

    /// <summary>
    /// How much is left of the thing standing in front of this one, or nought for none.
    /// <para>
    /// It takes hits instead of its owner and it takes them from its own small pool of
    /// health. When that runs out it is gone and the next hit reaches the creature — not the
    /// remainder of the hit that broke it, which is the rule that matters and the one worth
    /// getting right: a stand-in absorbs the <em>whole</em> of the blow that finishes it.
    /// </para>
    /// <para>
    /// A number rather than an object, because there is nothing else about it to know. It has
    /// no type, no stats and no name; what it has is an amount left.
    /// </para>
    /// </summary>
    public int StandInHp { get; set; }

    public bool HasStandIn => StandInHp > 0;

    /// <summary>
    /// How much this one has taken while gathering itself, and how much longer it will.
    /// <para>
    /// Nothing else in this engine accumulates across turns and then spends it. The count
    /// runs down at the end of each turn and the total is given back doubled when it reaches
    /// nought — so a creature part-way through this is committed, and being hit hard is what
    /// makes it worth having committed.
    /// </para>
    /// </summary>
    public int Gathered { get; set; }

    public int GatheringTurns { get; set; }

    public bool IsGathering => GatheringTurns > 0;

    /// <summary>
    /// Puts a different move in a slot for the rest of this fight.
    /// <para>
    /// Two moves in this game do this and they differ in one thing only: whether it survives
    /// the fight. Neither writes to a save from in here — what is permanent about the
    /// permanent one is decided outside, by whoever owns the creature.
    /// </para>
    /// </summary>
    public void PutInSlot(int slot, MoveData move)
    {
        if (slot < 0 || slot >= Moves.Count) return;

        Moves[slot] = move;
    }

    /// <summary>The slot this one may not use, and for how much longer.</summary>
    public int? DisabledSlot { get; set; }

    public int DisabledTurns { get; set; }

    /// <summary>True when this slot is the one currently blocked.</summary>
    public bool IsDisabled(int slot) => DisabledSlot == slot && DisabledTurns > 0;

    public int? ForcedSlot { get; set; }

    public int ForcedTurns { get; set; }

    /// <summary>
    /// True while this one is somewhere a move cannot reach.
    /// <para>
    /// The half of FLY that matters. Without it the move is a turn thrown away for a
    /// slightly better hit, which is worse than not having it.
    /// </para>
    /// </summary>
    public bool IsAway { get; set; }

    /// <summary>
    /// True for the rest of this turn, after PROTECT or DETECT.
    /// <para>
    /// Cleared at the end of every turn rather than at the start of the next, so that a
    /// guard put up by whoever moved first is still up when the other one swings — which
    /// is the whole of what the move is for.
    /// </para>
    /// </summary>
    public bool IsGuarded { get; set; }

    /// <summary>
    /// Turns of mist left on this side. While it holds, nothing may lower this one's
    /// stats.
    /// <para>
    /// A count rather than a flag, and the count is <b>modelled</b>: MIST's record says
    /// what it costs and that it does no damage, and nothing anywhere says how long it
    /// lasts. Only what somebody else does is refused — this one may still lower its own
    /// stats, which every move that trades a stat for power does.
    /// </para>
    /// </summary>
    public int MistTurns { get; set; }

    /// <summary>
    /// Turns left of the screen that halves physical damage, and of the one that halves
    /// special.
    /// <para>
    /// On the battler rather than on the battle because everything else that lasts a count
    /// of turns is, and because this engine fights one creature a side — the day it does not,
    /// these move to the side along with MIST and SAFEGUARD, which have the same problem and
    /// have had it longer.
    /// </para>
    /// </summary>
    public int ReflectTurns { get; set; }

    public int ScreenTurns { get; set; }

    /// <summary>
    /// True while something is taking a share of this one's health every turn.
    /// <para>
    /// A flag rather than a count, because it lasts as long as its target is standing there —
    /// which is what makes it worse than being wrapped, and what makes leaving the field the
    /// only answer to it.
    /// </para>
    /// </summary>
    public bool IsSeeded { get; set; }

    /// <summary>
    /// True once somebody has made this one findable.
    /// <para>
    /// Two rules in one flag, because the cartridge groups them: its evasion stops counting,
    /// and a type chart immunity it was relying on stops applying. Both last until it leaves
    /// the field, which is why this is a flag and not a count.
    /// </para>
    /// </summary>
    public bool IsIdentified { get; set; }

    /// <summary>True while sleep is costing this one health every turn.</summary>
    public bool InNightmare { get; set; }

    /// <summary>
    /// Turns until this one falls asleep, or nought.
    /// <para>
    /// A count rather than a flag because the whole point of the move is the delay: it is
    /// answerable, and a version that put somebody to sleep at once would be a different and
    /// much better move.
    /// </para>
    /// </summary>
    public int DrowsyTurns { get; set; }

    /// <summary>True once this one has taken root: health every turn, and no leaving.</summary>
    public bool IsRooted { get; set; }

    /// <summary>Turns until this one goes down regardless, or nought.</summary>
    public int PerishTurns { get; set; }

    /// <summary>Turns left of having nothing to do but attack.</summary>
    public int TauntTurns { get; set; }

    /// <summary>True while this one may not do the same thing twice running.</summary>
    public bool IsTormented { get; set; }

    /// <summary>True for the one turn this one survives whatever lands.</summary>
    public bool IsEnduring { get; set; }

    /// <summary>
    /// True while this one will take whoever finishes it down as well.
    /// <para>
    /// Lasts one turn, like a guard, because a promise that outlived the turn it was made in
    /// would be a promise nobody could play around.
    /// </para>
    /// </summary>
    public bool IsBonded { get; set; }

    /// <summary>
    /// How many times running this one has used the move it is building up, and which slot.
    /// <para>
    /// Two fields because one is meaningless without the other: a count with no slot cannot
    /// tell "used again" from "used something else", which is the entire rule both moves that
    /// read it are made of.
    /// </para>
    /// </summary>
    public int RunningCount { get; set; }

    public int? RunningSlot { get; set; }

    /// <summary>Every stage this one has, copied from somebody else's.</summary>
    public void CopyStagesFrom(Battler other)
    {
        ResetStages();

        foreach (Stat stat in Enum.GetValues<Stat>())
        {
            int theirs = other.StageOf(stat);

            if (theirs != 0) ChangeStage(stat, theirs);
        }
    }

    /// <summary>True while nothing may lower this one's stats from outside.</summary>
    public bool IsMisted => MistTurns > 0;

    /// <summary>
    /// Turns of safeguard left on this side. While it holds, nothing may afflict this
    /// one. Modelled for the same reason and written the same way.
    /// </summary>
    public int SafeguardTurns { get; set; }

    /// <summary>True while nothing may afflict this one.</summary>
    public bool IsGuardedFromHarm => SafeguardTurns > 0;

    /// <summary>
    /// True when this one has taken aim, and the next move it uses cannot miss.
    /// <para>
    /// A flag with no count at all: what MIND READER's record does not say is how long
    /// it lasts, and "the next one" is the only reading that needs no number. Cleared by
    /// the move it is spent on, hit or miss.
    /// </para>
    /// </summary>
    public bool HasAimed { get; set; }

    /// <summary>
    /// Sharper, until it leaves the field.
    /// <para>
    /// A flag rather than a stage, and that is the rule rather than an implementation
    /// choice: HAZE clears every stage on the field and does not clear this, and nothing
    /// lowers it. It goes when its owner does, which is what
    /// <see cref="ForgetWhatWasStarted"/> is for.
    /// </para>
    /// </summary>
    public bool IsFocused { get; set; }

    /// <summary>
    /// True while something has made sure this one is not going anywhere.
    /// <para>
    /// MEAN LOOK, and it lasts as long as the creature is standing there rather than for
    /// a count of turns — which is why it is a flag and not a number, and why switching
    /// out ends it without anything having to say so.
    /// </para>
    /// </summary>
    public bool CannotEscape { get; set; }

    /// <summary>What is holding this one, and for how many more turns.</summary>
    public int TrappedTurns { get; set; }

    public int TrappedBy { get; set; }

    /// <summary>
    /// The one move this one is allowed, once something it is carrying has decided.
    /// <para>
    /// Separate from <see cref="ForcedSlot"/> even though both mean "you are using this".
    /// A forced slot is the middle of something — THRASH, FLY — and ends when that thing
    /// does; this one has no end except leaving the field, and folding the two together
    /// would mean a CHOICE BAND that let go the moment a two-turn move finished.
    /// </para>
    /// </summary>
    public int? ChoiceSlot { get; set; }

    /// <summary>Everything a turn can owe the next one, forgotten at once.</summary>
    public void ForgetWhatWasStarted()
    {
        MustRecharge = false;
        ForcedSlot = null;
        ForcedTurns = 0;
        ChoiceSlot = null;
        IsFocused = false;
        IsSeeded = false;
        IsIdentified = false;
        InNightmare = false;
        DrowsyTurns = 0;
        IsRooted = false;
        TauntTurns = 0;
        IsTormented = false;
        IsEnduring = false;
        IsBonded = false;
        RunningCount = 0;
        RunningSlot = null;

        // Not the perish count. Everything else here is forgotten by leaving the field and
        // that is the point of leaving; this one follows whoever heard it, which is the whole
        // of what makes it worth using.
        ReflectTurns = 0;
        ScreenTurns = 0;
        LastSlot = null;
        LastMove = null;
        BorrowedAbility = null;
        BorrowedType = null;
        HurtThisTurn = 0;
        HurtThisTurnBy = null;

        // Back to nought, which is the whole of what makes the move that only works on the
        // turn its user arrives work again when they arrive again.
        TurnsOut = 0;

        // The stand-in does not follow anybody out of the door, and neither does a
        // half-gathered total. Both are things this creature started and neither is a thing
        // it is.
        StandInHp = 0;
        Gathered = 0;
        GatheringTurns = 0;
        DisabledSlot = null;
        DisabledTurns = 0;
        IsAway = false;
    }

    /// <summary>
    /// A type put on this one by a move, for the length of the fight.
    /// <para>
    /// Null almost always. When it is not, it replaces <em>both</em> of them and the creature
    /// is that one type and nothing else — which is what the four moves that do this all
    /// mean, and is why this is one field rather than two. A creature that kept half of what
    /// it was would be a different rule than any of them.
    /// </para>
    /// <para>
    /// It goes when its owner does. What a creature <em>is</em> is not something a battle may
    /// write to a save, and every one of these moves is over when the fight is.
    /// </para>
    /// </summary>
    public PokemonType? BorrowedType { get; set; }

    public PokemonType Type1 => BorrowedType ?? Species.Type1;

    public PokemonType Type2 => BorrowedType ?? Species.Type2;

    /// <summary>True when this one is that type, by birth or by a move.</summary>
    public bool Is(PokemonType type) => Type1 == type || Type2 == type;

    public bool HasFainted => CurrentHp <= 0;

    public int StageOf(Stat stat) => _stages.GetValueOrDefault(stat);

    /// <summary>Adjusts a stat stage, clamped to the -6..+6 range. Returns the change actually applied.</summary>
    public int ChangeStage(Stat stat, int delta)
    {
        int before = StageOf(stat);
        int after = Math.Clamp(before + delta, -Stats.MaxStage, Stats.MaxStage);
        _stages[stat] = after;
        return after - before;
    }

    public void ResetStages() => _stages.Clear();

    /// <summary>
    /// Everything that was true only while this one was standing there, let go of.
    /// <para>
    /// The line is the same one <c>Passed</c> draws and is worth stating once here: what a
    /// creature <em>built</em> or had <em>started on it in this fight</em> ends when it
    /// leaves; what was <em>done to</em> it — a condition, a count of turns asleep, what it
    /// is carrying — travels with it, because those are facts about the creature rather than
    /// about the square it was standing on.
    /// </para>
    /// <para>
    /// <b>Not in here, deliberately:</b> the four that are properties of a <em>side</em>
    /// rather than of a creature — mist, safeguard, and the two screens. They are kept on
    /// this class because there is nowhere else for a side's state to live yet, and a
    /// switch ending a screen the whole team was under would be wrong. That is a modelling
    /// limit and it is written down rather than quietly corrected here.
    /// </para>
    /// </summary>
    public void LeaveTheField()
    {
        ConfusedTurns = 0;
        IsSeeded = false;
        IsIdentified = false;
        IsRooted = false;
        PerishTurns = 0;
        TauntTurns = 0;
        IsTormented = false;
        DisabledTurns = 0;
        ForcedTurns = 0;
        HasAimed = false;
        IsFocused = false;
        CannotEscape = false;
        TrappedTurns = 0;
        TrappedBy = 0;
        InNightmare = false;
        DrowsyTurns = 0;

        // The stand-in goes with it — it is a thing this creature put up, and one left
        // behind would absorb hits meant for whoever came in.
        StandInHp = 0;

        // What it had gathered, and what it was locked into. Both are half-finished things
        // belonging to a creature that is no longer there to finish them.
        Gathered = 0;
        GatheringTurns = 0;
        MustRecharge = false;
        RechargingAfter = 0;
        RunningCount = 0;

        // How long it has been standing there, which is nought again the moment it comes
        // back — the move that only works on arrival has to work on a second arrival too.
        TurnsOut = 0;

        LastMove = null;
        HurtThisTurn = 0;
        HurtThisTurnBy = null;

        IsGuarded = false;
        IsEnduring = false;
        IsBonded = false;
        IsAway = false;
    }

    /// <summary>
    /// A battle stat with its stage applied. Critical hits ignore stages that would
    /// help the defender or hinder the attacker, which is what
    /// <paramref name="ignoreUnfavourableStages"/> models.
    /// </summary>
    public int EffectiveStat(Stat stat, bool ignoreUnfavourableStages = false)
    {
        int raw = stat switch
        {
            Stat.Attack => Attack,
            Stat.Defense => Defense,
            Stat.Speed => Speed,
            Stat.SpAttack => SpAttack,
            Stat.SpDefense => SpDefense,
            _ => 0,
        };

        int stage = StageOf(stat);

        if (ignoreUnfavourableStages)
        {
            bool isAttacking = stat is Stat.Attack or Stat.SpAttack;
            if (isAttacking && stage < 0) stage = 0;
            if (!isAttacking && stage > 0) stage = 0;
        }

        int value = Stats.ApplyStage(raw, stage);

        // Paralysis quarters Speed. Burn halves Attack, but that belongs with the
        // damage calculation rather than here, so a burned battler still moves at its
        // normal speed.
        if (stat == Stat.Speed && Status == StatusCondition.Paralysis) value /= 4;

        return Math.Max(1, value);
    }

    public int TakeDamage(int amount)
    {
        int dealt = Math.Clamp(amount, 0, CurrentHp);
        CurrentHp -= dealt;
        return dealt;
    }

    public int Heal(int amount)
    {
        int healed = Math.Clamp(amount, 0, MaxHp - CurrentHp);
        CurrentHp += healed;
        return healed;
    }

    /// <summary>Applies a status, which only sticks if the battler is currently clear.</summary>
    public bool TryApplyStatus(StatusCondition status, int sleepTurns = 0)
    {
        if (Status != StatusCondition.None || HasFainted) return false;

        // And what this one's ability says about it. Checked here rather than at each of
        // the four places a status is handed out, because there are four of them and the
        // fifth would be the one that forgot.
        if (Abilities.Refuses(Ability, status)) return false;

        Status = status;
        if (status == StatusCondition.Sleep) SleepTurns = Math.Max(1, sleepTurns);

        return true;
    }

    /// <summary>The move in a slot, or null when the slot is empty or out of range.</summary>
    public MoveData? MoveAt(int slot) => slot >= 0 && slot < Moves.Count ? Moves[slot] : null;

    public Battler Knowing(params MoveData[] moves)
    {
        Moves.AddRange(moves);
        return this;
    }

    public override string ToString() => $"{Name} L{Level} {CurrentHp}/{MaxHp}";
}
