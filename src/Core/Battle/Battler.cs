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

    /// <summary>Everything a turn can owe the next one, forgotten at once.</summary>
    public void ForgetWhatWasStarted()
    {
        MustRecharge = false;
        ForcedSlot = null;
        ForcedTurns = 0;
        LastSlot = null;
        DisabledSlot = null;
        DisabledTurns = 0;
        IsAway = false;
    }

    public PokemonType Type1 => Species.Type1;
    public PokemonType Type2 => Species.Type2;

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
