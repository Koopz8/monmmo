using System.Text.Json.Serialization;

namespace PokeMmo.Core.Battle;

/// <summary>Which side of a battle. Zero is the player.</summary>
public enum Side
{
    Player = 0,
    Opponent = 1,
}

/// <summary>
/// Something that happened during a turn, in the order it happened.
/// <para>
/// Every event names its participants by <see cref="Side"/> and its moves by index —
/// never by name. That is not brevity. These events are produced by the server, which
/// has no cartridge and so has no names to give: it knows a battler is species 16 and
/// a move is number 33, and the client turns those into "PIDGEY" and "TACKLE" using
/// the image on the player's own machine. A single string in here would mean shipping
/// cartridge text to a server, which is the one thing this project must not do.
/// </para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "e")]
[JsonDerivedType(typeof(MoveUsed), "used")]
[JsonDerivedType(typeof(MoveMissed), "missed")]
[JsonDerivedType(typeof(NoEffect), "noeffect")]
[JsonDerivedType(typeof(Immobilised), "immobilised")]
[JsonDerivedType(typeof(WokeUp), "woke")]
[JsonDerivedType(typeof(DamageDealt), "damage")]
[JsonDerivedType(typeof(StatusHurt), "statushurt")]
[JsonDerivedType(typeof(Fainted), "fainted")]
[JsonDerivedType(typeof(BallThrown), "ball")]
[JsonDerivedType(typeof(Ended), "ended")]
public abstract record BattleEvent
{
    public sealed record MoveUsed(Side Side, int MoveId) : BattleEvent;

    public sealed record MoveMissed(Side Side, int MoveId) : BattleEvent;

    public sealed record NoEffect(Side Side) : BattleEvent;

    public sealed record Immobilised(Side Side, StatusCondition Cause) : BattleEvent;

    public sealed record WokeUp(Side Side) : BattleEvent;

    public sealed record DamageDealt(
        Side Side,
        int Damage,
        int RemainingHp,
        DamageResult Detail) : BattleEvent;

    public sealed record StatusHurt(
        Side Side,
        StatusCondition Status,
        int Damage,
        int RemainingHp) : BattleEvent;

    public sealed record Fainted(Side Side) : BattleEvent;

    /// <summary>
    /// A ball was thrown. <paramref name="Shakes"/> is how many times it wobbled,
    /// which is what tells a player how close they came.
    /// </summary>
    public sealed record BallThrown(Side Target, int Shakes, bool Caught) : BattleEvent;

    /// <summary>The battle is over. A null winner means both sides fell in the same turn.</summary>
    public sealed record Ended(Side? Winner) : BattleEvent;
}

/// <summary>What a side chose to do this turn.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "a")]
[JsonDerivedType(typeof(UseMove), "move")]
[JsonDerivedType(typeof(Struggle), "struggle")]
[JsonDerivedType(typeof(ThrowBall), "ball")]
public abstract record BattleAction
{
    public sealed record UseMove(int Slot) : BattleAction;

    public sealed record Struggle : BattleAction;

    /// <summary>Throwing a ball uses the turn; the target still gets to act if it stays free.</summary>
    public sealed record ThrowBall(BallKind Ball) : BattleAction;
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

    /// <summary>True once the opponent has been caught, which ends the battle.</summary>
    public bool OpponentCaught { get; private set; }

    public bool IsOver => OpponentCaught || Player.HasFainted || Opponent.HasFainted;

    public Side? Winner => OpponentCaught
        ? Side.Player
        : (Player.HasFainted, Opponent.HasFainted) switch
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

        if (action is BattleAction.ThrowBall throwBall)
        {
            // Only a wild opponent can be caught, and throwing spends the turn whether
            // or not it works.
            ThrowAt(side, defender, throwBall.Ball, events);
            return;
        }

        MoveData? move = action is BattleAction.UseMove use ? attacker.MoveAt(use.Slot) : null;
        if (move is null) return;

        events.Add(new BattleEvent.MoveUsed(side, move.Id));

        if (!DamageCalculator.RollAccuracy(_rng, move, attacker, defender))
        {
            events.Add(new BattleEvent.MoveMissed(side, move.Id));
            return;
        }

        if (move.Category == DamageCategory.Status) return;

        bool critical = DamageCalculator.RollCritical(_rng, criticalStage: 0);
        DamageResult result = DamageCalculator.Calculate(_rng, attacker, defender, move, critical);

        if (result.NoEffect)
        {
            events.Add(new BattleEvent.NoEffect(Other(side)));
            return;
        }

        int dealt = defender.TakeDamage(result.Damage);
        events.Add(new BattleEvent.DamageDealt(Other(side), dealt, defender.CurrentHp, result));

        if (defender.HasFainted)
            events.Add(new BattleEvent.Fainted(Other(side)));
    }

    private void ThrowAt(Side thrower, Battler target, BallKind ball, List<BattleEvent> events)
    {
        CatchAttempt attempt = CatchCalculator.Throw(_rng, target, target.Species.CatchRate, ball);

        events.Add(new BattleEvent.BallThrown(Other(thrower), attempt.Shakes, attempt.Caught));

        if (attempt.Caught && thrower == Side.Player) OpponentCaught = true;
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
                    events.Add(new BattleEvent.WokeUp(side));
                    return true;
                }

                events.Add(new BattleEvent.Immobilised(side, StatusCondition.Sleep));
                return false;

            case StatusCondition.Freeze:
                // A fifth of the time the thaw happens before the turn is lost.
                if (_rng.Chance(20))
                {
                    battler.Status = StatusCondition.None;
                    return true;
                }

                events.Add(new BattleEvent.Immobilised(side, StatusCondition.Freeze));
                return false;

            case StatusCondition.Paralysis when _rng.Chance(25):
                events.Add(new BattleEvent.Immobilised(side, StatusCondition.Paralysis));
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

            events.Add(new BattleEvent.StatusHurt(side, battler.Status, dealt, battler.CurrentHp));

            if (battler.HasFainted) events.Add(new BattleEvent.Fainted(side));
        }
    }
}
