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

    public Battler(SpeciesData species, int level, Nature nature = Nature.Hardy, string? nickname = null)
    {
        Species = species;
        Level = level;
        Nature = nature;
        Nickname = nickname;
        Name = nickname ?? species.Name;

        MaxHp = Stats.Hp(species.BaseHp, level);
        CurrentHp = MaxHp;

        Attack = Stats.Other(Stat.Attack, species.BaseAttack, level, nature);
        Defense = Stats.Other(Stat.Defense, species.BaseDefense, level, nature);
        Speed = Stats.Other(Stat.Speed, species.BaseSpeed, level, nature);
        SpAttack = Stats.Other(Stat.SpAttack, species.BaseSpAttack, level, nature);
        SpDefense = Stats.Other(Stat.SpDefense, species.BaseSpDefense, level, nature);
    }

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

    public StatusCondition Status { get; set; }

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

    /// <summary>What is holding this one, and for how many more turns.</summary>
    public int TrappedTurns { get; set; }

    public int TrappedBy { get; set; }

    /// <summary>Everything a turn can owe the next one, forgotten at once.</summary>
    public void ForgetWhatWasStarted()
    {
        MustRecharge = false;
        ForcedSlot = null;
        ForcedTurns = 0;
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
