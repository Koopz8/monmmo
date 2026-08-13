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
[JsonDerivedType(typeof(StatusInflicted), "status")]
[JsonDerivedType(typeof(StageChanged), "stage")]
[JsonDerivedType(typeof(NothingHappened), "nothing")]
[JsonDerivedType(typeof(Fainted), "fainted")]
[JsonDerivedType(typeof(HealthRestored), "healed")]
[JsonDerivedType(typeof(BallThrown), "ball")]
[JsonDerivedType(typeof(ExperienceGained), "exp")]
[JsonDerivedType(typeof(LevelledUp), "levelup")]
[JsonDerivedType(typeof(MoveLearned), "learned")]
[JsonDerivedType(typeof(MoveNotLearned), "notlearned")]
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

    /// <summary>Somebody was put to sleep, poisoned, paralysed, burned or frozen.</summary>
    public sealed record StatusInflicted(Side Side, StatusCondition Status) : BattleEvent;

    /// <summary>
    /// A stat moved, or refused to. <paramref name="Stages"/> is what was asked for and
    /// <paramref name="Moved"/> is whether it went anywhere — a stat already at its limit
    /// has its own line in the games, and without it "SCREECH" twice reads as working
    /// twice.
    /// </summary>
    public sealed record StageChanged(Side Side, Stat Stat, int Stages, bool Moved) : BattleEvent;

    /// <summary>
    /// A move that did nothing, because nothing was left to do or because it was already
    /// done. Not the same as a move this engine has never heard of, which says only that
    /// it was used — pretending an unimplemented move failed would be a lie about the
    /// cartridge rather than about the battle.
    /// </summary>
    public sealed record NothingHappened(Side Side) : BattleEvent;

    public sealed record Fainted(Side Side) : BattleEvent;

    /// <summary>Somebody drank something. The amount is what actually went back on.</summary>
    public sealed record HealthRestored(Side Side, int ItemId, int Amount) : BattleEvent;

    /// <summary>
    /// A ball was thrown. <paramref name="Shakes"/> is how many times it wobbled,
    /// which is what tells a player how close they came.
    /// </summary>
    public sealed record BallThrown(Side Target, int Shakes, bool Caught) : BattleEvent;

    public sealed record ExperienceGained(Side Side, int Amount) : BattleEvent;

    public sealed record LevelledUp(Side Side, int Level) : BattleEvent;

    public sealed record MoveLearned(Side Side, int MoveId) : BattleEvent;

    /// <summary>
    /// A move was offered and could not be taken, because four are already known.
    /// <para>
    /// The games ask which to forget. Until something can ask, nothing is forgotten:
    /// silently dropping a move a player chose is worse than not learning a new one.
    /// </para>
    /// </summary>
    public sealed record MoveNotLearned(Side Side, int MoveId) : BattleEvent;

    /// <summary>The battle is over. A null winner means both sides fell in the same turn.</summary>
    public sealed record Ended(Side? Winner) : BattleEvent;
}

/// <summary>What a side chose to do this turn.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "a")]
[JsonDerivedType(typeof(UseMove), "move")]
[JsonDerivedType(typeof(Struggle), "struggle")]
[JsonDerivedType(typeof(ThrowBall), "ball")]
[JsonDerivedType(typeof(UseItem), "item")]
[JsonDerivedType(typeof(SwitchTo), "switch")]
public abstract record BattleAction
{
    public sealed record UseMove(int Slot) : BattleAction;

    /// <summary>
    /// Send out somebody else. The slot is a party index, and the party is the server's.
    /// <para>
    /// Costs the turn, like every other thing that is not a move — the one who comes out
    /// arrives to whatever the other side had already decided to do. Nothing in the
    /// engine acts on this: it is resolved before the turn by whoever owns the party, and
    /// what reaches the engine is a side that does nothing, which is what a switch is
    /// from the arithmetic's point of view.
    /// </para>
    /// <para>
    /// Stat stages go with the one who left, which falls out of the arrangement rather
    /// than needing a rule: a switch builds a fresh battle around the new pair, and a
    /// fresh battle has no stages in it.
    /// </para>
    /// </summary>
    public sealed record SwitchTo(int Slot) : BattleAction;

    public sealed record Struggle : BattleAction;

    /// <summary>Throwing a ball uses the turn; the target still gets to act if it stays free.</summary>
    /// <summary>
    /// A ball, named by the item it came out of the bag as.
    /// <para>
    /// The item id rather than the kind, because the count that has to be decremented
    /// is a count of that item. A request naming a kind would let a client spend a Poké
    /// Ball and throw a Master Ball.
    /// </para>
    /// </summary>
    public sealed record ThrowBall(int ItemId) : BattleAction
    {
        /// <summary>
        /// How well this one catches.
        /// <para>
        /// Filled in by the server from its rules, never by whoever sent the request.
        /// Nothing on a cartridge states a ball's behaviour in data — it lives in the
        /// game's code — so the id becomes a kind at export time, from the name, and
        /// the answer is the server's from then on.
        /// </para>
        /// </summary>
        public BallKind Kind { get; init; } = BallKind.Poke;
    }

    /// <summary>
    /// Uses something out of the bag on whoever is out.
    /// <para>
    /// The item id, and how much it restores decided by the server — same arrangement as
    /// a ball. A request that carried the amount would let a client drink a Potion for
    /// two hundred.
    /// </para>
    /// </summary>
    public sealed record UseItem(int ItemId) : BattleAction
    {
        public int Restores { get; init; }
    }
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

    /// <summary>
    /// Where the dice have got to.
    /// <para>
    /// A trainer fight is a run of one-on-one battles rather than one long one, and the
    /// next of them starts from here. Starting it from the seed instead would replay
    /// the same rolls in the same order against every creature they send out.
    /// </para>
    /// </summary>
    public uint State => _rng.State;

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

        if (action is BattleAction.UseItem item)
        {
            // Spends the turn whether or not it did much, exactly as a throw does.
            int healed = attacker.Heal(item.Restores);

            events.Add(new BattleEvent.HealthRestored(side, item.ItemId, healed));
            return;
        }

        if (action is BattleAction.ThrowBall throwBall)
        {
            // Only a wild opponent can be caught, and throwing spends the turn whether
            // or not it works.
            ThrowAt(side, defender, throwBall.Kind, events);
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

        // A move with no power is its effect, and for two years of this project's life
        // that was a line that read `return`. See MoveEffects: 138 of this cartridge's
        // 354 moves land here, and every one of them did nothing at all.
        if (move.Category == DamageCategory.Status)
        {
            Apply(side, attacker, defender, move, events, rolled: false);
            return;
        }

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
        {
            events.Add(new BattleEvent.Fainted(Other(side)));
            return;
        }

        // And whatever rides on the hit. Nothing rides on a knockout, which is why this
        // is after the faint rather than beside the damage.
        Apply(side, attacker, defender, move, events, rolled: true);
    }

    /// <summary>
    /// Does whatever the move's effect byte says, to whichever side it says.
    /// <para>
    /// <paramref name="rolled"/> separates the two ways an effect arrives: a status move
    /// <em>is</em> its effect and happens whenever it lands, while the same effect on a
    /// move that also does damage is a rider and rolls against the move's own secondary
    /// chance. THUNDERBOLT and THUNDER WAVE carry the same paralysis and are not the same
    /// promise.
    /// </para>
    /// </summary>
    private void Apply(
        Side side, Battler attacker, Battler defender, MoveData move, List<BattleEvent> events, bool rolled)
    {
        MoveEffect effect = MoveEffects.Of(move.Effect);

        if (effect.Kind == EffectKind.None) return;
        if (rolled && !_rng.Chance(move.SecondaryChance)) return;

        Side at = effect.OnUser ? side : Other(side);
        Battler target = effect.OnUser ? attacker : defender;

        if (effect.Kind == EffectKind.Status)
        {
            // Sleep runs one to three turns. Chosen here rather than in the battler
            // because how long anything lasts is a rule of the battle, and the battler is
            // only the thing it happens to.
            if (target.TryApplyStatus(effect.Status, sleepTurns: _rng.Next(3) + 1))
                events.Add(new BattleEvent.StatusInflicted(at, effect.Status));
            else if (!rolled)
                events.Add(new BattleEvent.NothingHappened(at));

            return;
        }

        int before = target.StageOf(effect.Stat);

        target.ChangeStage(effect.Stat, effect.Stages);

        bool moved = target.StageOf(effect.Stat) != before;

        events.Add(new BattleEvent.StageChanged(at, effect.Stat, effect.Stages, moved));
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
                    // Waking costs the turn. This used to return true, which made a
                    // one-turn sleep cost nothing at all — and a field whose smallest
                    // value does nothing is a field that means something else. Nothing
                    // could inflict sleep until now, so nobody had ever seen it: SLEEP
                    // POWDER would have done nothing a third of the time it landed.
                    battler.Status = StatusCondition.None;
                    events.Add(new BattleEvent.WokeUp(side));
                    return false;
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
