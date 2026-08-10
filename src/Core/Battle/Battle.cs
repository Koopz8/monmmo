namespace PokeMmo.Core.Battle;

/// <summary>Which side of a battle. Zero is the player.</summary>
public enum Side
{
    Player = 0,
    Opponent = 1,
}

/// <summary>Something that happened during a turn, in the order it happened.</summary>
public abstract record BattleEvent
{
    public sealed record MoveUsed(Side Side, string Attacker, string Move) : BattleEvent;

    public sealed record MoveMissed(Side Side, string Attacker, string Move) : BattleEvent;

    public sealed record NoEffect(Side Side, string Target) : BattleEvent;

    public sealed record Immobilised(Side Side, string Name, StatusCondition Cause) : BattleEvent;

    public sealed record WokeUp(Side Side, string Name) : BattleEvent;

    public sealed record DamageDealt(
        Side Side,
        string Target,
        int Damage,
        int RemainingHp,
        DamageResult Detail) : BattleEvent;

    public sealed record StatusHurt(
        Side Side,
        string Name,
        StatusCondition Status,
        int Damage,
        int RemainingHp) : BattleEvent;

    public sealed record Fainted(Side Side, string Name) : BattleEvent;

    /// <summary>The battle is over. A null winner means both sides fell in the same turn.</summary>
    public sealed record Ended(Side? Winner) : BattleEvent;
}

/// <summary>What a side chose to do this turn.</summary>
public abstract record BattleAction
{
    public sealed record UseMove(int Slot) : BattleAction;

    public sealed record Struggle : BattleAction;
}

/// <summary>
/// A one-against-one battle.
/// <para>
/// Given the same starting battlers, the same seed and the same actions, this
/// produces exactly the same events every time. That is what lets the server resolve
/// a battle authoritatively and the client replay it from the seed alone — the same
/// arrangement that keeps movement in step, applied to combat.
/// </para>
/// </summary>
public sealed class Battle(Battler player, Battler opponent, uint seed)
{
    private readonly BattleRng _rng = new(seed);

    public Battler Player { get; } = player;

    public Battler Opponent { get; } = opponent;

    public uint Seed => _rng.Seed;

    public int TurnNumber { get; private set; }

    public bool IsOver => Player.HasFainted || Opponent.HasFainted;

    public Side? Winner => (Player.HasFainted, Opponent.HasFainted) switch
    {
        (false, true) => Side.Player,
        (true, false) => Side.Opponent,
        _ => null,
    };

    public Battler Of(Side side) => side == Side.Player ? Player : Opponent;

    private static Side Other(Side side) => side == Side.Player ? Side.Opponent : Side.Player;

    /// <summary>Resolves one turn and returns everything that happened, in order.</summary>
    public List<BattleEvent> ResolveTurn(BattleAction playerAction, BattleAction opponentAction)
    {
        var events = new List<BattleEvent>();
        if (IsOver) return events;

        TurnNumber++;

        foreach (Side side in DecideOrder(playerAction, opponentAction))
        {
            if (IsOver) break;

            TakeTurn(side, side == Side.Player ? playerAction : opponentAction, events);
        }

        if (!IsOver) ApplyEndOfTurn(events);

        if (IsOver) events.Add(new BattleEvent.Ended(Winner));

        return events;
    }

    /// <summary>
    /// Move priority first, then effective Speed, then a coin flip. The speed
    /// comparison uses the stat with stages and paralysis applied, not the raw one.
    /// </summary>
    private Side[] DecideOrder(BattleAction playerAction, BattleAction opponentAction)
    {
        int playerPriority = PriorityOf(Player, playerAction);
        int opponentPriority = PriorityOf(Opponent, opponentAction);

        if (playerPriority != opponentPriority)
            return playerPriority > opponentPriority ? [Side.Player, Side.Opponent] : [Side.Opponent, Side.Player];

        int playerSpeed = Player.EffectiveStat(Stat.Speed);
        int opponentSpeed = Opponent.EffectiveStat(Stat.Speed);

        if (playerSpeed != opponentSpeed)
            return playerSpeed > opponentSpeed ? [Side.Player, Side.Opponent] : [Side.Opponent, Side.Player];

        return _rng.OneIn(2) ? [Side.Player, Side.Opponent] : [Side.Opponent, Side.Player];
    }

    private static int PriorityOf(Battler battler, BattleAction action) =>
        action is BattleAction.UseMove use && battler.MoveAt(use.Slot) is { } move ? move.Priority : 0;

    private void TakeTurn(Side side, BattleAction action, List<BattleEvent> events)
    {
        Battler attacker = Of(side);
        Battler defender = Of(Other(side));

        if (attacker.HasFainted) return;
        if (!CanAct(side, attacker, events)) return;

        MoveData? move = action is BattleAction.UseMove use ? attacker.MoveAt(use.Slot) : null;
        if (move is null) return;

        events.Add(new BattleEvent.MoveUsed(side, attacker.Name, move.Name));

        if (!DamageCalculator.RollAccuracy(_rng, move, attacker, defender))
        {
            events.Add(new BattleEvent.MoveMissed(side, attacker.Name, move.Name));
            return;
        }

        if (move.Category == DamageCategory.Status) return;

        bool critical = DamageCalculator.RollCritical(_rng, criticalStage: 0);
        DamageResult result = DamageCalculator.Calculate(_rng, attacker, defender, move, critical);

        if (result.NoEffect)
        {
            events.Add(new BattleEvent.NoEffect(Other(side), defender.Name));
            return;
        }

        int dealt = defender.TakeDamage(result.Damage);
        events.Add(new BattleEvent.DamageDealt(Other(side), defender.Name, dealt, defender.CurrentHp, result));

        if (defender.HasFainted)
            events.Add(new BattleEvent.Fainted(Other(side), defender.Name));
    }

    /// <summary>
    /// Sleep, freeze and paralysis are checked before a move is announced, so a
    /// battler that cannot act never appears to try.
    /// </summary>
    private bool CanAct(Side side, Battler battler, List<BattleEvent> events)
    {
        switch (battler.Status)
        {
            case StatusCondition.Sleep:
                battler.SleepTurns--;

                if (battler.SleepTurns <= 0)
                {
                    battler.Status = StatusCondition.None;
                    events.Add(new BattleEvent.WokeUp(side, battler.Name));
                    return true;
                }

                events.Add(new BattleEvent.Immobilised(side, battler.Name, StatusCondition.Sleep));
                return false;

            case StatusCondition.Freeze:
                // A fifth of the time the thaw happens before the turn is lost.
                if (_rng.Chance(20))
                {
                    battler.Status = StatusCondition.None;
                    return true;
                }

                events.Add(new BattleEvent.Immobilised(side, battler.Name, StatusCondition.Freeze));
                return false;

            case StatusCondition.Paralysis when _rng.Chance(25):
                events.Add(new BattleEvent.Immobilised(side, battler.Name, StatusCondition.Paralysis));
                return false;

            default:
                return true;
        }
    }

    /// <summary>Poison and burn each take a sixteenth of maximum health, minimum one.</summary>
    private void ApplyEndOfTurn(List<BattleEvent> events)
    {
        foreach (Side side in new[] { Side.Player, Side.Opponent })
        {
            Battler battler = Of(side);
            if (battler.HasFainted) continue;

            if (battler.Status is not (StatusCondition.Poison or StatusCondition.Burn)) continue;

            int damage = Math.Max(1, battler.MaxHp / 16);
            int dealt = battler.TakeDamage(damage);

            events.Add(new BattleEvent.StatusHurt(side, battler.Name, battler.Status, dealt, battler.CurrentHp));

            if (battler.HasFainted) events.Add(new BattleEvent.Fainted(side, battler.Name));
        }
    }
}
