using PokeMmo.Core.Data;
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
[JsonDerivedType(typeof(StagesCleared), "hazed")]
[JsonDerivedType(typeof(MistRose), "mist")]
[JsonDerivedType(typeof(Safeguarded), "safeguard")]
[JsonDerivedType(typeof(TookAim), "aimed")]
[JsonDerivedType(typeof(Shielded), "shielded")]
[JsonDerivedType(typeof(Immobilised), "immobilised")]
[JsonDerivedType(typeof(WokeUp), "woke")]
[JsonDerivedType(typeof(DamageDealt), "damage")]
[JsonDerivedType(typeof(StatusHurt), "statushurt")]
[JsonDerivedType(typeof(StatusInflicted), "status")]
[JsonDerivedType(typeof(StageChanged), "stage")]
[JsonDerivedType(typeof(NothingHappened), "nothing")]
[JsonDerivedType(typeof(HitSeveralTimes), "severaltimes")]
[JsonDerivedType(typeof(Drained), "drained")]
[JsonDerivedType(typeof(Recoiled), "recoiled")]
[JsonDerivedType(typeof(Crashed), "crashed")]
[JsonDerivedType(typeof(BlewUp), "blewup")]
[JsonDerivedType(typeof(Flinched), "flinched")]
[JsonDerivedType(typeof(Recovered), "recovered")]
[JsonDerivedType(typeof(Confused), "confused")]
[JsonDerivedType(typeof(SnappedOut), "snappedout")]
[JsonDerivedType(typeof(HurtItself), "hurtitself")]
[JsonDerivedType(typeof(Fainted), "fainted")]
[JsonDerivedType(typeof(HealthRestored), "healed")]
[JsonDerivedType(typeof(PutRight), "putright")]
[JsonDerivedType(typeof(BallThrown), "ball")]
[JsonDerivedType(typeof(ExperienceGained), "exp")]
[JsonDerivedType(typeof(LevelledUp), "levelup")]
[JsonDerivedType(typeof(MoveLearned), "learned")]
[JsonDerivedType(typeof(MoveNotLearned), "notlearned")]
[JsonDerivedType(typeof(Evolved), "evolved")]
[JsonDerivedType(typeof(WentAway), "wentaway")]
[JsonDerivedType(typeof(Recharging), "recharging")]
[JsonDerivedType(typeof(Trapped), "trapped")]
[JsonDerivedType(typeof(TrapHurt), "traphurt")]
[JsonDerivedType(typeof(BrokeFree), "brokefree")]
[JsonDerivedType(typeof(OneHitKnockout), "onehit")]
[JsonDerivedType(typeof(Unaffected), "unaffected")]
[JsonDerivedType(typeof(Protected), "protected")]
[JsonDerivedType(typeof(CannotUse), "cannotuse")]
[JsonDerivedType(typeof(CanUseAgain), "canuseagain")]
[JsonDerivedType(typeof(Grazed), "grazed")]
[JsonDerivedType(typeof(WeatherBegan), "weatheron")]
[JsonDerivedType(typeof(WeatherEnded), "weatheroff")]
[JsonDerivedType(typeof(WeatherHurt), "weatherhurt")]
[JsonDerivedType(typeof(WeatherHealed), "weatherheal")]
[JsonDerivedType(typeof(ItemHealed), "itemhealed")]
[JsonDerivedType(typeof(HeldOn), "heldon")]
[JsonDerivedType(typeof(WentFirst), "wentfirst")]
[JsonDerivedType(typeof(AteIt), "ateit")]
[JsonDerivedType(typeof(ScreenRose), "screenrose")]
[JsonDerivedType(typeof(Seeded), "seeded")]
[JsonDerivedType(typeof(Sapped), "sapped")]
[JsonDerivedType(typeof(WallsBroke), "wallsbroke")]
[JsonDerivedType(typeof(KnockedOff), "knockedoff")]
[JsonDerivedType(typeof(ShookFree), "shookfree")]
[JsonDerivedType(typeof(Identified), "identified")]
[JsonDerivedType(typeof(HurtBySleep), "hurtbysleep")]
[JsonDerivedType(typeof(Drowsy), "drowsy")]
[JsonDerivedType(typeof(TookRoot), "tookroot")]
[JsonDerivedType(typeof(PerishCount), "perishcount")]
[JsonDerivedType(typeof(Taunted), "taunted")]
[JsonDerivedType(typeof(Tormented), "tormented")]
[JsonDerivedType(typeof(BracedItself), "braced")]
[JsonDerivedType(typeof(Endured), "endured")]
[JsonDerivedType(typeof(Bonded), "bonded")]
[JsonDerivedType(typeof(TookThemWith), "tookthemwith")]
[JsonDerivedType(typeof(HealthShared), "healthshared")]
[JsonDerivedType(typeof(CopiedStages), "copiedstages")]
[JsonDerivedType(typeof(UsedInstead), "usedinstead")]
[JsonDerivedType(typeof(LearnedMove), "learnedmove")]
[JsonDerivedType(typeof(AbilityMoved), "abilitymoved")]
[JsonDerivedType(typeof(LostItsNerve), "lostitsnerve")]
[JsonDerivedType(typeof(Damped), "damped")]
[JsonDerivedType(typeof(MustRepeat), "mustrepeat")]
[JsonDerivedType(typeof(GotAway), "gotaway")]
[JsonDerivedType(typeof(CouldNotGetAway), "couldnotgetaway")]
[JsonDerivedType(typeof(HeldFast), "heldfast")]
[JsonDerivedType(typeof(BlownAway), "blownaway")]
[JsonDerivedType(typeof(Stole), "stole")]
[JsonDerivedType(typeof(Ended), "ended")]
public abstract record BattleEvent
{
    public sealed record MoveUsed(Side Side, int MoveId) : BattleEvent;

    public sealed record MoveMissed(Side Side, int MoveId) : BattleEvent;

    /// <summary>Every stage on both sides is back to nothing. The side is who did it.</summary>
    public sealed record StagesCleared(Side Side) : BattleEvent;

    /// <summary>Mist is up on this side.</summary>
    public sealed record MistRose(Side Side) : BattleEvent;

    /// <summary>A safeguard is up on this side.</summary>
    public sealed record Safeguarded(Side Side) : BattleEvent;

    /// <summary>This one has taken aim, and what it does next cannot miss.</summary>
    public sealed record TookAim(Side Side) : BattleEvent;

    /// <summary>Something was refused because this side is shielded from it.</summary>
    public sealed record Shielded(Side Side) : BattleEvent;

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

    /// <summary>A move that landed more than once, and how many times.</summary>
    public sealed record HitSeveralTimes(Side Side, int Times) : BattleEvent;

    /// <summary>The user got some of what it dealt back.</summary>
    public sealed record Drained(Side Side, int Amount) : BattleEvent;

    /// <summary>The user hurt itself doing that.</summary>
    public sealed record Recoiled(Side Side, int Amount) : BattleEvent;

    /// <summary>A move that missed and hurt the one who used it.</summary>
    public sealed record Crashed(Side Side, int Amount) : BattleEvent;

    /// <summary>The user took itself out along with the move.</summary>
    public sealed record BlewUp(Side Side) : BattleEvent;

    /// <summary>Somebody lost their turn to a flinch.</summary>
    public sealed record Flinched(Side Side) : BattleEvent;

    /// <summary>Health restored by a move rather than by an item.</summary>
    public sealed record Recovered(Side Side, int Amount) : BattleEvent;

    /// <summary>Somebody became confused.</summary>
    public sealed record Confused(Side Side) : BattleEvent;

    /// <summary>Confusion wore off before the turn was taken.</summary>
    public sealed record SnappedOut(Side Side) : BattleEvent;

    /// <summary>Confusion cost the turn, and hurt.</summary>
    public sealed record HurtItself(Side Side, int Amount) : BattleEvent;

    public sealed record Fainted(Side Side) : BattleEvent;

    /// <summary>Somebody drank something. The amount is what actually went back on.</summary>
    public sealed record HealthRestored(Side Side, int ItemId, int Amount) : BattleEvent;

    /// <summary>
    /// Something that was wrong is no longer wrong.
    /// <para>
    /// Separate from the health event rather than a field on it, because one item does
    /// both and one does only this — a Full Heal restores nothing and is not a wasted
    /// turn, and an event that carried "restored zero" would read as one.
    /// </para>
    /// </summary>
    public sealed record PutRight(Side Side, int ItemId, Ailments Cleared) : BattleEvent;

    /// <summary>
    /// A ball was thrown. <paramref name="Shakes"/> is how many times it wobbled,
    /// which is what tells a player how close they came.
    /// </summary>
    public sealed record BallThrown(Side Target, int Shakes, bool Caught) : BattleEvent;

    public sealed record ExperienceGained(Side Side, int Amount) : BattleEvent;

    public sealed record LevelledUp(Side Side, int Level) : BattleEvent;

    public sealed record MoveLearned(Side Side, int MoveId) : BattleEvent;

    /// <summary>
    /// Something became something else.
    /// <para>
    /// Both species travel, not just the new one. The sentence is about a change and a
    /// change needs both ends of it — and the client is the only half that can turn
    /// either number into a name.
    /// </para>
    /// </summary>
    public sealed record Evolved(Side Side, int From, int Into) : BattleEvent;

    /// <summary>
    /// Went somewhere a move cannot reach, and will land next turn.
    /// <para>
    /// The move id travels because the sentence is about the move — FLY and DIG are the
    /// same rule and not the same picture — and because only the client can name it.
    /// </para>
    /// </summary>
    public sealed record WentAway(Side Side, int MoveId) : BattleEvent;

    /// <summary>The turn the last one cost.</summary>
    public sealed record Recharging(Side Side, int MoveId) : BattleEvent;

    public sealed record Trapped(Side Side, int MoveId) : BattleEvent;

    public sealed record TrapHurt(Side Side, int MoveId, int Damage, int RemainingHp) : BattleEvent;

    public sealed record BrokeFree(Side Side, int MoveId) : BattleEvent;

    /// <summary>Ended outright, however much was left.</summary>
    public sealed record OneHitKnockout(Side Side) : BattleEvent;

    /// <summary>
    /// A move whose number came out at nothing to do.
    /// <para>
    /// Separate from missing and from having no effect, because it is neither: SUPER
    /// FANG on something with one health left connects perfectly and takes nothing.
    /// </para>
    /// </summary>
    public sealed record Unaffected(Side Side) : BattleEvent;

    /// <summary>Put a guard up, and nothing got through it.</summary>
    public sealed record Protected(Side Side) : BattleEvent;

    /// <summary>A move is blocked, either just now or when it was tried.</summary>
    public sealed record CannotUse(Side Side, int MoveId) : BattleEvent;

    /// <summary>The block ran out.</summary>
    public sealed record CanUseAgain(Side Side) : BattleEvent;

    /// <summary>The sky changed.</summary>
    public sealed record WeatherBegan(Weather Weather) : BattleEvent;

    /// <summary>And it stopped.</summary>
    public sealed record WeatherEnded(Weather Weather) : BattleEvent;

    /// <summary>Touching somebody cost the toucher.</summary>
    public sealed record Grazed(Side Side, int Damage, int RemainingHp) : BattleEvent;

    /// <summary>Somebody is standing in weather that does not suit them.</summary>
    public sealed record WeatherHurt(Side Side, Weather Weather, int Damage, int RemainingHp) : BattleEvent;

    /// <summary>And somebody the weather agrees with.</summary>
    public sealed record WeatherHealed(Side Side, Weather Weather, int Healed, int RemainingHp) : BattleEvent;

    /// <summary>
    /// Something being carried put health back. The item id rather than a name, like every
    /// other message in this project that is about an item.
    /// </summary>
    public sealed record ItemHealed(Side Side, int ItemId, int Healed, int RemainingHp) : BattleEvent;

    /// <summary>Something being carried turned a knockout into one point of health.</summary>
    public sealed record HeldOn(Side Side, int ItemId) : BattleEvent;

    /// <summary>Something being carried took the turn out of order.</summary>
    public sealed record WentFirst(Side Side, int ItemId) : BattleEvent;

    /// <summary>
    /// Something being carried was used up, and is gone.
    /// <para>
    /// Its own message rather than folded into whatever it did, because the two facts have
    /// different lifetimes: the healing is over when the turn is, and the item not being
    /// there any more outlives the battle.
    /// </para>
    /// </summary>
    public sealed record AteIt(Side Side, int ItemId) : BattleEvent;

    /// <summary>A screen went up on this side. Physical, or the other one.</summary>
    public sealed record ScreenRose(Side Side, bool Physical) : BattleEvent;

    /// <summary>Something is now taking a share of this side's health every turn.</summary>
    public sealed record Seeded(Side Side) : BattleEvent;

    /// <summary>And it took some. The side named is the one that lost it.</summary>
    public sealed record Sapped(Side Side, int Amount, int RemainingHp) : BattleEvent;

    /// <summary>Whatever this side was hiding behind is gone.</summary>
    public sealed record WallsBroke(Side Side) : BattleEvent;

    /// <summary>What this side was carrying has been taken off it, and is gone.</summary>
    public sealed record KnockedOff(Side Side, int ItemId) : BattleEvent;

    /// <summary>This side shook off whatever was holding or draining it.</summary>
    public sealed record ShookFree(Side Side) : BattleEvent;

    /// <summary>This side can be found now, whatever it is and however well it was hiding.</summary>
    public sealed record Identified(Side Side) : BattleEvent;

    /// <summary>Sleep is costing this side health.</summary>
    public sealed record HurtBySleep(Side Side, int Damage, int RemainingHp) : BattleEvent;

    /// <summary>This side will be asleep shortly.</summary>
    public sealed record Drowsy(Side Side) : BattleEvent;

    /// <summary>This side has taken root: health every turn, and no leaving.</summary>
    public sealed record TookRoot(Side Side) : BattleEvent;

    /// <summary>How many turns this side has left before it goes down regardless.</summary>
    public sealed record PerishCount(Side Side, int Turns) : BattleEvent;

    /// <summary>This side has nothing to do but attack for a while.</summary>
    public sealed record Taunted(Side Side) : BattleEvent;

    /// <summary>This side may not do the same thing twice running.</summary>
    public sealed record Tormented(Side Side) : BattleEvent;

    /// <summary>This side is ready to survive whatever lands this turn.</summary>
    public sealed record BracedItself(Side Side) : BattleEvent;

    /// <summary>And did.</summary>
    public sealed record Endured(Side Side) : BattleEvent;

    /// <summary>This side will take whoever finishes it down as well.</summary>
    public sealed record Bonded(Side Side) : BattleEvent;

    /// <summary>And did. The side named is the one taken down with it.</summary>
    public sealed record TookThemWith(Side Side) : BattleEvent;

    /// <summary>Both sides ended up on the same health.</summary>
    public sealed record HealthShared(Side Side, int Each) : BattleEvent;

    /// <summary>Took the other one's stat changes for its own — all of them.</summary>
    public sealed record CopiedStages(Side Side) : BattleEvent;

    /// <summary>Used a move that was not the one chosen, and which one it turned out to be.</summary>
    public sealed record UsedInstead(Side Side, int MoveId) : BattleEvent;

    /// <summary>Took a move into a slot, for this fight or for good.</summary>
    public sealed record LearnedMove(Side Side, int MoveId, bool ForGood) : BattleEvent;

    /// <summary>An ability moved from one creature to another, or between them.</summary>
    public sealed record AbilityMoved(Side Side, int Ability) : BattleEvent;

    /// <summary>
    /// Was hit while winding up something that needed quiet, and so did not throw it.
    /// <para>
    /// Its own line rather than the general "nothing happened", because this one is the
    /// whole risk of the move: a player who cannot tell the difference between "I was
    /// interrupted" and "it did not work" cannot learn to play around it.
    /// </para>
    /// </summary>
    public sealed record LostItsNerve(Side Side) : BattleEvent;

    /// <summary>Turned one kind of move down, for the room rather than for anybody.</summary>
    public sealed record Damped(Side Side) : BattleEvent;

    /// <summary>Made to do the same thing again.</summary>
    public sealed record MustRepeat(Side Side, int MoveId) : BattleEvent;

    /// <summary>Left.</summary>
    public sealed record GotAway(Side Side) : BattleEvent;

    /// <summary>Tried to leave and did not, which costs the turn.</summary>
    public sealed record CouldNotGetAway(Side Side) : BattleEvent;

    /// <summary>Tried to leave and is being held, which also costs the turn.</summary>
    public sealed record HeldFast(Side Side, int MoveId) : BattleEvent;

    /// <summary>Sent off, which ends a fight with something wild in it.</summary>
    public sealed record BlownAway(Side Side, int MoveId) : BattleEvent;

    /// <summary>Took what the other one was carrying.</summary>
    public sealed record Stole(Side Side, int ItemId) : BattleEvent;

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
[JsonDerivedType(typeof(RunAway), "run")]
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

    /// <summary>
    /// Leave. The one thing a player could not do.
    /// <para>
    /// Only from something wild — a trainer does not let you walk off, and that is a
    /// rule both halves have to know, because a client that offers it and a server that
    /// refuses it is a button that does nothing.
    /// </para>
    /// </summary>
    public sealed record RunAway : BattleAction;

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

        /// <summary>What this would put right, decided by the server for the same reason.</summary>
        public Ailments Cures { get; init; }
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

    /// <summary>
    /// What the sky is doing, and for how much longer.
    /// <para>
    /// The first state in this engine that belongs to the battle rather than to either side
    /// of it. Everything else that lasts turns hangs off one battler, and there was nowhere
    /// for a fact about the room to live until now.
    /// </para>
    /// </summary>
    public Weather Sky { get; private set; }

    public int SkyTurns { get; private set; }

    /// <summary>
    /// Which type the room has been turned down for, and for how much longer.
    /// <para>
    /// The second fact about the room, and it hangs off the battle for the same reason the
    /// sky does: somebody who damped the electricity damped it for both sides, including
    /// their own. A flag on the battler who used the move would have made it a shield.
    /// </para>
    /// <para>
    /// Normal when nothing is damped, which is safe because no move in this game damps
    /// Normal — the count beside it is what says whether it means anything.
    /// </para>
    /// </summary>
    public PokemonType Damped { get; private set; }

    public int DampedTurns { get; private set; }

    /// <summary>
    /// What a move of this type is worth as a percentage. Fifty or a hundred, and never
    /// anything in between — this is a switch rather than a dial.
    /// </summary>
    private int Damping(PokemonType type) => DampedTurns > 0 && Damped == type ? 50 : 100;

    /// <summary>
    /// The weather as far as anybody in this fight is concerned.
    /// <para>
    /// CLOUD NINE and AIR LOCK switch it off for everybody, including their own side. So
    /// every rule asks this rather than <see cref="Sky"/> — the countdown is still running
    /// underneath, and if the ability leaves the field the weather is still there.
    /// </para>
    /// </summary>
    public Weather Overhead =>
        Abilities.Ignores(Player.Ability) || Abilities.Ignores(Opponent.Ability) ? Weather.None : Sky;

    /// <summary>
    /// How fast somebody is once the sky has had its say.
    /// <para>
    /// Applied where the order is decided rather than on the battler, because it is not a
    /// fact about the creature — the same creature in a different fight is a different
    /// speed, and a stat that changed when the weather did would show a doubled number on
    /// every screen that draws one.
    /// </para>
    /// </summary>
    /// <summary>
    /// A chance in a hundred, rolled only when there is one.
    /// <para>
    /// The guard is the whole point rather than an optimisation. Every fight in this project
    /// runs off one seeded stream, so a die rolled for something that cannot happen moves
    /// every die after it — and the first version of this rolled a QUICK CLAW for two
    /// creatures carrying nothing and changed the outcome of a DISABLE test three turns
    /// later. Dice are only rolled for things that could go either way.
    /// </para>
    /// </summary>
    private bool Rolls(int chance) => chance > 0 && _rng.Next(100) < chance;

    /// <summary>
    /// What somebody carrying something eats it for, if anything.
    /// <para>
    /// One place for all twenty-one, asked after every turn and again at the end of one,
    /// because a berry answers two different things: a condition that has just been
    /// inflicted, and health that has just fallen. Both happen inside a turn and both can
    /// also arrive at the end of one — poison at the end of a turn is the case that needs
    /// the second call.
    /// </para>
    /// <para>
    /// At most one thing per item, because an item is used up by the first thing it does.
    /// The order between them is arbitrary where two could apply at once and is fixed here
    /// so that it is at least the same every time: health, then being nearly finished, then
    /// conditions, then uses, then stats.
    /// </para>
    /// </summary>
    /// <summary>Puts somebody onto a particular number, whichever direction that is.</summary>
    private static void Settle(Battler battler, int to)
    {
        if (to > battler.CurrentHp) battler.Heal(to - battler.CurrentHp);
        else if (to < battler.CurrentHp) battler.TakeDamage(battler.CurrentHp - to);
    }

    private void Nibble(Side side, Battler battler, List<BattleEvent> events)
    {
        if (battler.HasFainted || battler.Carried is not { } carried) return;
        if (!HeldItems.IsEaten(carried)) return;

        int item = battler.Holding;
        bool hurt = battler.CurrentHp * HeldItems.HurtShare <= battler.MaxHp;
        bool missing = battler.CurrentHp < battler.MaxHp;

        // Flat health, whose amount is on the record: ten, twenty or thirty depending on
        // which of the three this is.
        if (hurt && missing && HeldItems.Restoring(carried) is { } flat)
        {
            Ate(side, battler, item, events, new BattleEvent.HealthRestored(side, item, battler.Heal(flat)));

            return;
        }

        // A share of its own maximum, and a mouthful of something its nature dislikes.
        if (hurt && missing && HeldItems.Feeding(carried) is { } feeding)
        {
            int put = battler.Heal(Math.Max(1, battler.MaxHp / feeding.Share));

            Ate(side, battler, item, events, new BattleEvent.HealthRestored(side, item, put));

            // Which natures dislike what is entirely read — the raised and lowered stat of
            // every nature comes off the same table stats are computed from. A neutral one
            // dislikes nothing, which is why it is asked rather than compared.
            if (!Stats.IsNeutral(battler.Nature)
                && Stats.EffectOf(battler.Nature).Lowered == feeding.Disliked
                && battler.ConfusedTurns == 0
                && !Abilities.RefusesConfusion(battler.Ability))
            {
                battler.ConfusedTurns = _rng.Next(4) + 2;

                events.Add(new BattleEvent.Confused(side));
            }

            return;
        }

        // Nearly finished, at the quarter its own record names.
        if (HeldItems.PinchedAt(carried) is { } share && battler.CurrentHp * share <= battler.MaxHp)
        {
            Pinched(side, battler, carried, item, events);

            return;
        }

        // A condition, cleared.
        if (HeldItems.Clearing(carried) is var clearing && clearing != Ailments.None)
        {
            Ailments has = battler.Status.AsAilment()
                | (battler.ConfusedTurns > 0 ? Ailments.Confusion : Ailments.None);

            Ailments cleared = clearing & has;

            if (cleared != Ailments.None)
            {
                if ((cleared & Ailments.Confusion) != 0) battler.ConfusedTurns = 0;
                if (cleared != Ailments.Confusion) battler.Status = StatusCondition.None;

                Ate(side, battler, item, events, new BattleEvent.PutRight(side, item, cleared));
            }

            return;
        }

        // Uses, put back into the first move that has run out.
        if (HeldItems.Refilling(carried) is { } uses && battler.FirstSpentSlot() is { } empty)
        {
            if (battler.Refill(empty, uses) > 0)
                Ate(side, battler, item, events, new BattleEvent.CanUseAgain(side));

            return;
        }

        // And everything that was lowered, back where it started.
        if (HeldItems.Restoring(carried, stages: true) && battler.RaiseWhatWasLowered() > 0)
            Ate(side, battler, item, events, new BattleEvent.StagesCleared(side));
    }

    /// <summary>What one of the seven does when its carrier is nearly finished.</summary>
    private void Pinched(Side side, Battler battler, ItemData carried, int item, List<BattleEvent> events)
    {
        // Five raise one stat. The sixth sharpens instead, and the seventh picks one of the
        // five at random and raises it by two — which is the only place in this file that
        // needs a die, and it is only rolled once the berry has already decided to go off.
        if (HeldItems.Raises(carried) is { } stat)
        {
            int moved = battler.ChangeStage(stat, 1);

            if (moved != 0) Ate(side, battler, item, events, new BattleEvent.StageChanged(side, stat, 1, true));

            return;
        }

        if (carried.HoldEffect == HeldItems.Sharpening)
        {
            battler.HasAimed = true;

            Ate(side, battler, item, events, new BattleEvent.TookAim(side));

            return;
        }

        if (carried.HoldEffect != HeldItems.Wild) return;

        Stat[] any = [Stat.Attack, Stat.Defense, Stat.Speed, Stat.SpAttack, Stat.SpDefense];
        Stat picked = any[_rng.Next(any.Length)];

        if (battler.ChangeStage(picked, 2) != 0)
            Ate(side, battler, item, events, new BattleEvent.StageChanged(side, picked, 2, true));
    }

    /// <summary>Says what it did, and that it is gone.</summary>
    private static void Ate(
        Side side, Battler battler, int item, List<BattleEvent> events, BattleEvent what)
    {
        events.Add(what);
        events.Add(new BattleEvent.AteIt(side, item));

        battler.Holding = 0;
        battler.Carried = null;
    }

    private int SpeedOf(Battler battler) =>
        battler.EffectiveStat(Stat.Speed)
        * Abilities.Speed(battler.Ability, Overhead) / 100
        * HeldItems.Multiplies(battler.Carried, battler.Species.Index, Stat.Speed) / 100;

    /// <summary>
    /// Carries what is already true across a switch.
    /// <para>
    /// Sending somebody out builds a whole new battle, keeping only where the dice had got
    /// to — which is right for everything that belongs to a creature and wrong for the one
    /// thing that does not. Weather belongs to the room, and a rain that stopped because
    /// somebody swapped a creature would be a five-turn rule anybody could cancel for free.
    /// </para>
    /// <para>
    /// Explicit rather than a constructor argument, because every caller that builds a
    /// battle from a previous one should have to say that it is doing so.
    /// </para>
    /// </summary>
    public void ContinueFrom(Battle previous)
    {
        Sky = previous.Sky;
        SkyTurns = previous.SkyTurns;
    }

    /// <summary>
    /// Somebody has taken the field, and whatever their ability does about that.
    /// <para>
    /// The event this engine did not have. Everything else an ability does is asked at the
    /// moment it matters — when damage is worked out, when a status is handed over — and
    /// asking is enough because the ability is a property of a creature that is already
    /// standing there. These four are different: they happen <em>because</em> the creature
    /// arrived, and an arrival was not a thing that happened to anybody.
    /// </para>
    /// <para>
    /// Called by whoever sends somebody out rather than by the constructor, because a
    /// constructor that had side effects would fire them again every time a switch rebuilt
    /// the battle around the creature who had not moved.
    /// </para>
    /// </summary>
    public List<BattleEvent> Arrival(Side side)
    {
        var events = new List<BattleEvent>();

        Battler arriving = Of(side);

        if (arriving.HasFainted) return events;

        if (Abilities.Brings(arriving.Ability) is not Weather.None and var sky && Sky != sky)
            BeginWeather(sky, events);

        if (Abilities.Cows(arriving.Ability) is not 0 and var stages)
        {
            Side at = side.Other();
            Battler cowed = Of(at);

            // The same shield a move's stat drop respects. Being frightened by somebody
            // walking in is still somebody else lowering your Attack.
            if (cowed.HasFainted || cowed.IsMisted || Abilities.Protects(cowed.Ability, Stat.Attack))
            {
                if (!cowed.HasFainted) events.Add(new BattleEvent.Shielded(at));

                return events;
            }

            int before = cowed.StageOf(Stat.Attack);

            cowed.ChangeStage(Stat.Attack, stages);

            events.Add(new BattleEvent.StageChanged(
                at, Stat.Attack, stages, cowed.StageOf(Stat.Attack) != before));
        }

        return events;
    }

    /// <summary>
    /// What touching somebody costs the toucher.
    /// <para>
    /// Five abilities answer being hit by something that reaches them, and none of them
    /// could do anything at all until the flag on a move record was read. Bit nought of the
    /// flags byte has been on every move this project has ever parsed and had never been
    /// looked at.
    /// </para>
    /// <para>
    /// Nothing happens when the attacker is already down: an ability that finished somebody
    /// who was already finished would be the one thing in this engine that could kill twice.
    /// </para>
    /// </summary>
    private void Touching(Side side, Battler attacker, Battler defender, List<BattleEvent> events)
    {
        if (attacker.HasFainted) return;

        if (Abilities.Grazes(defender.Ability))
        {
            int cost = attacker.TakeDamage(Math.Max(1, attacker.MaxHp / Abilities.SkinShare));

            events.Add(new BattleEvent.Grazed(side, cost, attacker.CurrentHp));

            if (attacker.HasFainted) events.Add(new BattleEvent.Fainted(side));

            return;
        }

        // TryApplyStatus is the one door a condition goes through, so an attacker who is
        // already ill, or whose own ability refuses this, is refused here too without this
        // method having to know either rule.
        if (Abilities.Touched(defender.Ability, _rng) is not { } caught) return;

        if (attacker.TryApplyStatus(caught, sleepTurns: _rng.Next(3) + 1))
            events.Add(new BattleEvent.StatusInflicted(side, caught));
    }

    /// <summary>
    /// True when the creature on this side may not leave the field at all.
    /// <para>
    /// Here as well as inside the run because leaving happens two ways and the engine only
    /// ever knew about one of them. Running away is a move the engine resolves; switching
    /// is done by the server, which builds a new battle around somebody else — so the rule
    /// has to be askable from outside or it is a rule about half of leaving.
    /// </para>
    /// </summary>
    public bool MayNotLeave(Side side)
    {
        Battler leaving = Of(side);
        Battler opposite = Of(side.Other());

        return leaving.CannotEscape
            || leaving.TrappedTurns > 0
            || Abilities.Traps(opposite.Ability, leaving.Type1, leaving.Type2, leaving.Ability);
    }

    /// <summary>Starts weather, or starts it again, and says so.</summary>
    private void BeginWeather(Weather weather, List<BattleEvent> events)
    {
        Sky = weather;
        SkyTurns = Skies.Turns;

        events.Add(new BattleEvent.WeatherBegan(weather));
    }

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

    private readonly List<int> _steppedOver = [];

    /// <summary>
    /// Every move used in this fight whose effect this engine has no answer for.
    /// <para>
    /// One entry per use, not per move, because the question worth answering afterwards is
    /// how much of a fight went unmodelled rather than which moves exist. A fight with no
    /// entries is a fight this engine did the whole of.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> SteppedOver => _steppedOver;

    /// <summary>True once the opponent has been caught, which ends the battle.</summary>
    public bool OpponentCaught { get; private set; }

    /// <summary>
    /// True once somebody walked out of it — either the player ran or the opponent was
    /// sent off. A fight nobody won, which is a thing that had never happened here.
    /// </summary>
    public bool Escaped { get; private set; }

    /// <summary>
    /// Whether the thing on the other side is wild.
    /// <para>
    /// Set by whoever built the battle. Running, and being blown away, are both only
    /// possible against something wild — and the client is told the same thing, because
    /// a button that is offered and refused is worse than one that is not offered.
    /// </para>
    /// </summary>
    public bool IsWild { get; init; } = true;

    /// <summary>
    /// The move to fall back on when everything is spent, or nothing.
    /// <para>
    /// Handed in rather than looked up, because a battle has no rules file — it has two
    /// creatures and some dice. A battle built without one simply lets a spent creature
    /// do nothing, which is worse than struggling and better than inventing a move.
    /// </para>
    /// </summary>
    public MoveData? Struggle { get; init; }

    /// <summary>
    /// Every move in the game, for the one move that picks any of them.
    /// <para>
    /// Supplied from outside exactly as <see cref="Struggle"/> is, and for the same reason:
    /// this engine works in move records and has never held the table they came out of. A
    /// battle given none simply finds nothing to pick, and says so.
    /// </para>
    /// </summary>
    public IReadOnlyList<MoveData> EveryMove { get; init; } = [];

    public bool IsOver => OpponentCaught || Escaped || Player.HasFainted || Opponent.HasFainted;

    public Side? Winner => OpponentCaught
        ? Side.Player
        : Escaped
            ? null
            : (Player.HasFainted, Opponent.HasFainted) switch
            {
                (false, true) => Side.Player,
                (true, false) => Side.Opponent,
                _ => null,
            };

    /// <summary>
    /// How many times this side has tried to leave.
    /// <para>
    /// Counted because trying again is meant to be easier than trying the first time —
    /// which is the one part of the escape rule that is a fact about these games rather
    /// than a number, and it is the part that makes a fight you cannot win survivable.
    /// </para>
    /// </summary>
    private int _attempts;

    public Battler Of(Side side) => side == Side.Player ? Player : Opponent;

    private static Side Other(Side side) => side == Side.Player ? Side.Opponent : Side.Player;

    /// <summary>Whether this kind does nothing itself and uses another move instead.</summary>
    private static bool Borrows(EffectKind kind) =>
        kind is EffectKind.Mirrors or EffectKind.AtRandom or EffectKind.Sleeping;

    /// <summary>
    /// Which move one of the borrowers turns out to make, or nothing when there is none.
    /// <para>
    /// Nothing is a real answer for all three and it is the common answer for two of them: a
    /// creature that is awake cannot talk in its sleep, and there is nothing to mirror until
    /// the other one has moved. Returning nothing rather than a substitute is what lets the
    /// turn say so out loud instead of quietly doing something.
    /// </para>
    /// </summary>
    private MoveData? Borrowed(Side side, Battler attacker, Battler defender, EffectKind kind)
    {
        switch (kind)
        {
            case EffectKind.Mirrors:
                // Whatever they last did, whatever that was — including another borrower.
                // Nothing is filtered here on purpose: the once-only rule above already
                // makes mirroring a mirror come to nothing, and a second rule saying the
                // same thing would be a rule no test could ever fail. It was written that
                // way first, and breaking it proved exactly that.
                return defender.LastMove;

            case EffectKind.AtRandom:
            {
                // Anything at all, which is the only place in this engine that reaches for
                // the whole table. A borrower cannot come out of it, for the same reason.
                List<MoveData> anything =
                    [.. EveryMove.Where(m => !Borrows(MoveEffects.Of(m.Effect).Kind))];

                return anything.Count == 0 ? null : anything[_rng.Next(anything.Count)];
            }

            case EffectKind.Sleeping:
            {
                // Only while asleep, which is the whole of the move: it is not a way of
                // choosing at random, it is a way of doing anything at all while you cannot.
                if (attacker.Status != StatusCondition.Sleep) return null;

                List<MoveData> own =
                [
                    .. attacker.Moves.Where(m =>
                        !Borrows(MoveEffects.Of(m.Effect).Kind)

                        // And not one that takes a turn to wind up. A creature asleep cannot
                        // be halfway through FLY, and letting it start one would leave a
                        // forced slot nobody can ever discharge.
                        && MoveEffects.Of(m.Effect).Kind != EffectKind.TwoTurn),
                ];

                return own.Count == 0 ? null : own[_rng.Next(own.Count)];
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// Who is flinching, which lasts exactly one turn and belongs to the battle rather
    /// than to the battler.
    /// <para>
    /// On the battle because it is a fact about this turn, not about the creature: a
    /// flinch that survived being switched out, or a save, would be a condition, and it
    /// is not one.
    /// </para>
    /// </summary>
    /// <summary>
    /// Who a claw let in front this turn, so the line can be said once the turn starts.
    /// <para>
    /// Held rather than said where it happens, because the order is decided before anybody
    /// has done anything and a message about an item arriving before the move it changed the
    /// order of would read as though it had happened out of nowhere.
    /// </para>
    /// </summary>
    private Side? _clawed;

    private bool _playerFlinching;

    private bool _opponentFlinching;

    private bool Flinching(Side side) => side == Side.Player ? _playerFlinching : _opponentFlinching;

    private void SetFlinching(Side side, bool value)
    {
        if (side == Side.Player) _playerFlinching = value;
        else _opponentFlinching = value;
    }

    /// <summary>Resolves one turn and returns everything that happened, in order.</summary>
    public List<BattleEvent> ResolveTurn(BattleAction playerAction, BattleAction opponentAction)
    {
        var events = new List<BattleEvent>();
        if (IsOver) return events;

        TurnNumber++;

        // Before the order is decided, because a forced move is the move whose priority
        // counts. A QUICK ATTACK against somebody halfway through THRASH is still first,
        // and it is first against the move they are actually about to make.
        playerAction = Forced(Player) ?? playerAction;
        opponentAction = Forced(Opponent) ?? opponentAction;

        _clawed = null;

        // What has happened to each of them this turn, which is nothing yet. Cleared here
        // rather than at the end, so that a fight inspected between turns still shows what
        // the turn did — and so a move that answers being hit cannot answer last turn's hit.
        Player.HurtThisTurn = 0;
        Player.HurtThisTurnBy = null;
        Opponent.HurtThisTurn = 0;
        Opponent.HurtThisTurnBy = null;

        Side[] order = DecideOrder(playerAction, opponentAction);

        if (_clawed is { } hurried) events.Add(new BattleEvent.WentFirst(hurried, Of(hurried).Holding));

        foreach (Side side in order)
        {
            if (IsOver) break;

            TakeTurn(side, side == Side.Player ? playerAction : opponentAction, events);

            // Both sides, after every turn: one of them may have just been poisoned and the
            // other may have just been brought low, and a berry answers either.
            if (!IsOver)
            {
                Nibble(Side.Player, Player, events);
                Nibble(Side.Opponent, Opponent, events);
            }
        }

        if (!IsOver) ApplyEndOfTurn(events);

        // A guard lasts the turn it was put up and no longer. Cleared here rather than at
        // the start of the next turn so that the two are the same thing even when the
        // fight ends in between.
        Player.IsGuarded = false;
        Player.IsEnduring = false;
        Player.IsBonded = false;
        Opponent.IsEnduring = false;
        Opponent.IsBonded = false;
        Opponent.IsGuarded = false;

        // And the two that hold for a count rather than for the turn. Ticked in the same
        // place for the same reason: whatever a fight does to a turn, it ends here.
        // And how long each of them has been standing there, which goes up at the end so
        // that the turn somebody arrives is nought for the whole of it. Counting at the top
        // would make the move that only works on arrival never work at all.
        Player.TurnsOut++;
        Opponent.TurnsOut++;

        if (Player.MistTurns > 0) Player.MistTurns--;
        if (Opponent.MistTurns > 0) Opponent.MistTurns--;
        if (Player.SafeguardTurns > 0) Player.SafeguardTurns--;
        if (Opponent.SafeguardTurns > 0) Opponent.SafeguardTurns--;

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

        // A claw, which reaches inside the bracket rather than over it. In these games it
        // does not beat a priority move — it beats Speed — so it is asked here, after
        // priority has already decided and before Speed gets to.
        //
        // Both may roll it. When both do, it has decided nothing and Speed decides, which is
        // simpler than picking a winner between two identical claws and is what the games do.
        bool playerClawed = Rolls(HeldItems.Hurries(Player.Carried));
        bool opponentClawed = Rolls(HeldItems.Hurries(Opponent.Carried));

        if (playerClawed != opponentClawed)
        {
            _clawed = playerClawed ? Side.Player : Side.Opponent;

            return playerClawed ? [Side.Player, Side.Opponent] : [Side.Opponent, Side.Player];
        }

        int playerSpeed = SpeedOf(Player);
        int opponentSpeed = SpeedOf(Opponent);

        if (playerSpeed != opponentSpeed)
            return playerSpeed > opponentSpeed ? [Side.Player, Side.Opponent] : [Side.Opponent, Side.Player];

        return _rng.OneIn(2) ? [Side.Player, Side.Opponent] : [Side.Opponent, Side.Player];
    }

    /// <summary>
    /// The move this one has no choice about, when it has none.
    /// <para>
    /// Whatever the player pressed is discarded, which is the point: a creature in the
    /// middle of THRASH or halfway through FLY is not being asked. The client is not
    /// stopped from asking — it does not know — and the answer is simply not used.
    /// </para>
    /// </summary>
    private static BattleAction? Forced(Battler battler)
    {
        if (battler.HasFainted) return null;

        if (battler.ForcedSlot is { } slot) return new BattleAction.UseMove(slot);

        // And the band, which is the same discarding of what the player pressed for a
        // different reason. Only while that move can still be made: a creature locked into a
        // move it has run out of is a creature that must Struggle, and forcing an empty slot
        // here would hand it a turn of nothing instead.
        return battler.ChoiceSlot is { } only && battler.PpLeft(only) > 0
            ? new BattleAction.UseMove(only)
            : null;
    }

    private static int PriorityOf(Battler battler, BattleAction action) =>
        action is BattleAction.UseMove use && battler.MoveAt(use.Slot) is { } move ? move.Priority : 0;

    /// <summary>
    /// One side's go.
    /// <para>
    /// <paramref name="instead"/> is the move to make in place of the one the slot holds,
    /// and it is null for every turn a player ever takes. Five moves in this game do not do
    /// anything themselves — they use another move — and this is how: the copy is resolved
    /// first, and then this method calls itself once with the move it chose.
    /// </para>
    /// <para>
    /// Once, and only once. A copied move that is itself a copy finds this already set and
    /// does nothing, which is a rule rather than a shortcut: without it METRONOME picking
    /// METRONOME is a stack that does not come back, and with it the answer is the one the
    /// games give.
    /// </para>
    /// <para>
    /// Everything that belongs to <em>choosing</em> a move rather than to making one is
    /// skipped on the second pass — the cost, the blocks, the running count, what this one
    /// last did. A borrowed move is not in a slot, so it cannot be the blocked slot, cannot
    /// be spent, and cannot be the thing you did twice running.
    /// </para>
    /// </summary>
    private void TakeTurn(
        Side side, BattleAction action, List<BattleEvent> events, MoveData? instead = null)
    {
        Battler attacker = Of(side);
        Battler defender = Of(Other(side));

        if (attacker.HasFainted) return;

        // The bag, the door and a ball come before the question of whether this one can
        // move, because none of them are it moving — a trainer reaching into a bag is
        // not something sleep can stop. Written the other way round it made a Full Heal
        // useless on the only thing it is for: the check ran first, the creature slept
        // through its own cure, and the item was spent on nothing.
        //
        // It also means the sleep counter does not tick. In these games it comes down
        // when the creature tries to move, and this turn it never tried.
        if (action is BattleAction.UseItem item)
        {
            // Spends the turn whether or not it did much, exactly as a throw does.
            int healed = attacker.Heal(item.Restores);

            events.Add(new BattleEvent.HealthRestored(side, item.ItemId, healed));

            // And what it puts right, which is the half a Full Heal is entirely made of.
            Ailments cleared = Ailments.None;

            if (item.Cures.Clears(attacker.Status))
            {
                cleared |= attacker.Status.AsAilment();
                attacker.Status = StatusCondition.None;
            }

            if (item.Cures.HasFlag(Ailments.Confusion) && attacker.IsConfused)
            {
                cleared |= Ailments.Confusion;
                attacker.ConfusedTurns = 0;
            }

            if (cleared != Ailments.None) events.Add(new BattleEvent.PutRight(side, item.ItemId, cleared));

            return;
        }

        if (action is BattleAction.RunAway)
        {
            RunFrom(side, attacker, defender, events);
            return;
        }

        if (action is BattleAction.ThrowBall throwBall)
        {
            // Only a wild opponent can be caught, and throwing spends the turn whether
            // or not it works.
            ThrowAt(side, defender, throwBall.Kind, events);
            return;
        }

        // And here is where it matters, which is everything else: a move.
        // What was chosen, before the question of whether this one can move at all — because
        // for exactly one move in the game the answer depends on which move it is.
        MoveData? chose = action is BattleAction.UseMove picked ? attacker.MoveAt(picked.Slot) : null;

        if (!CanAct(side, attacker, events, chose)) return;

        MoveData? move = instead ?? (action is BattleAction.UseMove use ? attacker.MoveAt(use.Slot) : null);

        // Nothing left to swing with. The move a creature is left with is a move in the
        // cartridge's own table — its power, its type and its recoil are all read — so
        // this is a substitution rather than an invention. What it costs is what its own
        // record says it costs.
        //
        // Checked before the slot is spent and before anything is announced, because a
        // creature that has run dry never used the move it chose.
        if (instead is null && move is not null && attacker.IsSpent && Struggle is { } struggling)
        {
            move = struggling;
            action = new BattleAction.Struggle();
        }
        else if (instead is null && move is not null && !attacker.ForcedSlot.HasValue)
        {
            // And the ordinary case: one use, spent as the move is made rather than as it
            // lands. A miss costs the same as a hit, which is the games' own rule and the
            // only sane one — otherwise missing would be free.
            if (action is BattleAction.UseMove spending && !attacker.Spend(spending.Slot))
            {
                events.Add(new BattleEvent.NothingHappened(side));

                return;
            }
        }

        if (move is null) return;

        MoveEffect kind = MoveEffects.Of(move.Effect);

        // The first half of FLY. Nothing is announced as used yet — what happened is
        // that something left — and the slot is held so the next turn takes it back.
        if (kind.Kind == EffectKind.TwoTurn && attacker.ForcedSlot is null)
        {
            attacker.ForcedSlot = (action as BattleAction.UseMove)!.Slot;
            attacker.ForcedTurns = 1;
            attacker.IsAway = true;

            events.Add(new BattleEvent.WentAway(side, move.Id));

            return;
        }

        // And the landing: it is here, it is hittable again, and it is not forced any
        // more. Cleared before the move resolves so that a knockout leaves nothing owed.
        if (kind.Kind == EffectKind.TwoTurn)
        {
            attacker.IsAway = false;
            attacker.ForcedSlot = null;
            attacker.ForcedTurns = 0;
        }

        // A blocked slot cannot be swung. Checked after the substitution above, so a
        // creature whose only move is blocked and whose others are spent still struggles
        // rather than standing there.
        if (instead is null && action is BattleAction.UseMove blocked && attacker.IsDisabled(blocked.Slot))
        {
            events.Add(new BattleEvent.CannotUse(side, move.Id));

            return;
        }

        // Nothing but attacking, while that lasts. Checked here with the other refusals
        // rather than where a move is chosen, because a client chooses and a server decides
        // — and everything a server decides about a move belongs in one place.
        if (instead is null && attacker.TauntTurns > 0 && move.Category == DamageCategory.Status)
        {
            events.Add(new BattleEvent.CannotUse(side, move.Id));

            return;
        }

        // And not the same thing twice running.
        if (instead is null && attacker.IsTormented && action is BattleAction.UseMove again && attacker.LastSlot == again.Slot)
        {
            events.Add(new BattleEvent.CannotUse(side, move.Id));

            return;
        }

        // What this one just did, for the two moves that care. Written when the move is
        // made rather than when it lands, because a miss is still what you did.
        if (instead is null && action is BattleAction.UseMove made)
        {
            attacker.LastSlot = made.Slot;

            // And the move itself, which is what the other side's copies want. The slot is
            // no use to them: it indexes this creature's four, and a creature that switches
            // out takes its slots with it.
            attacker.LastMove = move;

            // And what it has now committed to, if it is carrying the thing that commits.
            // Set after the move rather than before it, so the move that decided is itself
            // freely chosen — a band that locked before the first choice would be a band
            // that chose for you.
            if (HeldItems.Locks(attacker.Carried)) attacker.ChoiceSlot ??= made.Slot;

            // And how many turns running this same slot has been used, for the two moves
            // whose power is a count rather than a number.
            //
            // It is kept here, beside the slot it counts for, and it is kept by the battle
            // because only the battle can see the turn before this one. Any of the three
            // things below can happen: the same slot again and the count climbs, one of
            // these moves fresh and the count starts, or anything else at all and the count
            // is gone — not paused, gone, which is what "running" means and is the whole of
            // why these moves are a gamble rather than a ramp.
            MoveEffect building = MoveEffects.Of(move.Effect);

            if (building.Kind is EffectKind.BuildsUp or EffectKind.BuildsUpLocked
                && attacker.RunningSlot == made.Slot)
            {
                attacker.RunningCount++;
            }
            else if (building.Kind is EffectKind.BuildsUp or EffectKind.BuildsUpLocked)
            {
                attacker.RunningCount = 0;
                attacker.RunningSlot = made.Slot;
            }
            else
            {
                attacker.RunningCount = 0;
                attacker.RunningSlot = null;
            }

            // And the one that does not let go once it has started. Started only at the
            // bottom of the climb, because a lock renewed every turn would be a lock that
            // never ended.
            if (building.Kind == EffectKind.BuildsUpLocked && attacker.ForcedSlot is null
                && attacker.RunningCount == 0)
            {
                attacker.ForcedSlot = made.Slot;
                attacker.ForcedTurns = MovePower.MostDoublings;
            }
        }

        events.Add(new BattleEvent.MoveUsed(side, move.Id));

        // What this engine cannot do, said out loud rather than skipped. Recorded rather
        // than announced: a line in the middle of a fight reading "this move has a part
        // nobody has written" is a worse experience than the move quietly under-doing —
        // but a fight that never mentions it anywhere is how 138 moves came to be half
        // implemented without anybody counting.
        if (MoveEffects.IsSilent(move.Effect)) _steppedOver.Add(move.Id);

        // The two that refuse on account of what has already happened this turn. Checked
        // here, after the move is announced and before anything is rolled, because both of
        // them are the move failing rather than the move missing — and the difference is
        // visible: a miss spends a guard's turn and a refusal does not.
        if (kind.Kind == EffectKind.FirstImpression && attacker.TurnsOut > 0)
        {
            events.Add(new BattleEvent.NothingHappened(side));

            return;
        }

        if (kind.Kind == EffectKind.NeedsQuiet && attacker.HurtThisTurn > 0)
        {
            events.Add(new BattleEvent.LostItsNerve(side));

            return;
        }

        // The five that use a move that is not this one. Resolved here rather than with the
        // other effects because they are not effects — nothing about this move happens at
        // all, and everything after this point is about a move that has not been chosen yet.
        //
        // Only on the first pass. A borrowed move that borrows finds nothing to borrow with,
        // which is what stops METRONOME picking METRONOME for ever.
        if (instead is null && Borrows(kind.Kind))
        {
            if (Borrowed(side, attacker, defender, kind.Kind) is not { } borrowed)
            {
                events.Add(new BattleEvent.NothingHappened(side));

                return;
            }

            events.Add(new BattleEvent.UsedInstead(side, borrowed.Id));

            TakeTurn(side, action, events, borrowed);

            return;
        }

        // Behind a guard, and nothing else happens. Before accuracy, because PROTECT is
        // not evasion — the move is not missing, it is being stopped.
        //
        // Only what is aimed at them, which is what the record's target byte is for and
        // the first thing in this project ever to read it. Without that, a guard put up
        // by one side stopped the other side sharpening its own claws.
        if (defender.IsGuarded && move.AimsAtSomebodyElse)
        {
            events.Add(new BattleEvent.Unaffected(Other(side)));

            EndLockedIn(side, attacker, events);

            return;
        }

        // And putting one up, which is the whole of the move.
        if (kind.Kind == EffectKind.Guard)
        {
            attacker.IsGuarded = true;

            events.Add(new BattleEvent.Protected(side));

            return;
        }

        // The lock starts when the move is used, not when it lands. A THRASH that misses
        // is still a THRASH: the games do not let go because the swing went wide, and
        // starting the count on the hit would have made a miss on the first turn a way
        // out of it.
        if (kind.Kind == EffectKind.LockedIn && attacker.ForcedSlot is null && action is BattleAction.UseMove chosen)
        {
            // Two turns or three. Modelled, not read: nothing in THRASH's record says
            // how long it goes on for.
            attacker.ForcedSlot = chosen.Slot;
            attacker.ForcedTurns = _rng.OneIn(2) ? 2 : 3;
        }

        // Somewhere a move cannot reach. Checked before accuracy rather than folded into
        // it, because a move that would never have missed still cannot hit what is not
        // there — and SWIFT never misses.
        // Aim taken on an earlier turn, spent on this move whatever it does. Read before
        // both of the ways a move fails to connect, because the whole of what it is for
        // is that neither of them applies: an aimed move reaches somewhere a move cannot
        // reach, and does not roll.
        bool sure = attacker.HasAimed;

        attacker.HasAimed = false;

        if (defender.IsAway && !sure)
        {
            events.Add(new BattleEvent.MoveMissed(side, move.Id));

            EndLockedIn(side, attacker, events);

            return;
        }

        if (!sure && !DamageCalculator.RollAccuracy(_rng, move, attacker, defender, Overhead))
        {
            events.Add(new BattleEvent.MoveMissed(side, move.Id));

            // What a miss costs, for the two moves it costs anything. Half of what the
            // hit would have taken off the target — modelled, not read: the share is in
            // the game's code. What is read is which moves pay it, which is the whole of
            // group 0x2D and nothing else.
            if (kind.Kind == EffectKind.CrashOnMiss)
            {
                DamageResult missed = DamageCalculator.Calculate(_rng, attacker, defender, move, critical: false, Overhead);

                int hurt = attacker.TakeDamage(Math.Max(1, missed.Damage / 2));

                events.Add(new BattleEvent.Crashed(side, hurt));

                if (attacker.HasFainted) events.Add(new BattleEvent.Fainted(side));
            }

            // The user goes down whether or not it connected. That is the group, and it
            // is the one number in this file that did not have to be modelled.
            if (kind.Kind == EffectKind.UserFaints) BlowUp(side, attacker, events);

            // A thrash that misses is still a thrash. It goes on, and it still ends in
            // the same place.
            EndLockedIn(side, attacker, events);

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

        MoveEffect carried = kind;

        // The moves whose damage is not worked out from their power. Before the ordinary
        // loop because they do not go through it at all — no critical, no random factor,
        // no same-type bonus, and the only thing the type chart still decides is whether
        // the move can touch this defender at all. NIGHT SHADE is a ghost move and a
        // PIDGEY is not somewhere it can reach.
        if (WhateverElseSays(carried.Kind, attacker, defender) is { } elsewhere)
        {
            if (TypeChart.Effectiveness(move.Type, defender.Type1, defender.Type2) == 0)
            {
                events.Add(new BattleEvent.NoEffect(Other(side)));
                return;
            }

            // Higher up than this one and it does not happen. Modelled, not read: the
            // rule is in the game's code, and without it a level-two DIGLETT ends a
            // level-seventy MEWTWO three times in ten.
            if (carried.Kind == EffectKind.Knockout && defender.Level > attacker.Level)
            {
                events.Add(new BattleEvent.Unaffected(Other(side)));
                return;
            }

            if (elsewhere <= 0)
            {
                events.Add(new BattleEvent.Unaffected(Other(side)));
                return;
            }

            int taken = defender.TakeDamage(elsewhere);

            events.Add(new BattleEvent.DamageDealt(
                Other(side), taken, defender.CurrentHp, new DamageResult(taken, false, 100, false)));

            if (carried.Kind == EffectKind.Knockout) events.Add(new BattleEvent.OneHitKnockout(Other(side)));

            if (defender.HasFainted) events.Add(new BattleEvent.Fainted(Other(side)));

            return;
        }

        // How many times, and how likely a critical. Both are read off the move's group
        // rather than off its record — see MoveEffects — and the numbers here are
        // modelled rather than read: nothing in a move's record says how many times
        // DOUBLESLAP lands.
        int times = carried.Kind switch
        {
            EffectKind.MultiHit => RollHits(),
            EffectKind.Twice => 2,

            // Three, and not a roll: this is the one multi-hit move in the game whose count
            // is fixed and whose hits are not all worth the same.
            EffectKind.ThreeGoes => 3,
            _ => 1,
        };
        // The move's own sharpness, plus whatever the attacker is carrying to add to it.
        // They add rather than replace, which is the games' rule and the reason a FARFETCH'D
        // holding a STICK and using SLASH crits about half the time.
        int criticalStage = (carried.Kind == EffectKind.HighCritical ? 1 : 0)
            + HeldItems.Sharpens(attacker.Carried, attacker.Species.Index)
            + (attacker.IsFocused ? 1 : 0);

        int total = 0;
        int landed = 0;

        for (int hit = 0; hit < times; hit++)
        {
            bool critical = DamageCalculator.RollCritical(_rng, criticalStage);
            DamageResult result = DamageCalculator.Calculate(
                _rng, attacker, defender, move, critical, Overhead, Damping(move.Type), hit);

            if (result.NoEffect)
            {
                events.Add(new BattleEvent.NoEffect(Other(side)));
                return;
            }

            // A band, asked only of a hit that would finish it. One point left rather than
            // none, which is the whole rule — and asked per hit rather than per move, so a
            // DOUBLESLAP has to get through it five times.
            int coming = result.Damage;
            bool held = false;

            // FALSE SWIPE, applied here because here is where a number becomes a knockout.
            // One point left rather than none, and no message: the whole point of the move
            // is that nothing happens.
            if (MoveEffects.Of(move.Effect).Kind == EffectKind.LeavesOne && coming >= defender.CurrentHp)
                coming = Math.Max(0, defender.CurrentHp - 1);

            if (coming >= defender.CurrentHp
                && defender.CurrentHp > 1
                && Rolls(HeldItems.Endures(defender.Carried)))
            {
                coming = defender.CurrentHp - 1;
                held = true;
            }

            // And bracing, which is the same shape and is certain rather than a chance —
            // that is the whole difference between a move somebody chose and an item they
            // happened to be carrying.
            bool braced = false;

            if (coming >= defender.CurrentHp && defender.IsEnduring)
            {
                coming = Math.Max(0, defender.CurrentHp - 1);
                braced = true;
            }

            int dealt = defender.TakeDamage(coming);

            // Written down for the six moves that are answers to being hit. The running
            // total rather than the last hit, because a DOUBLESLAP that lands five times hurt
            // five times — and the kind of the last thing that did it, which is what the two
            // that give it back doubled are choosy about.
            defender.HurtThisTurn += dealt;
            defender.HurtThisTurnBy = move.Category;

            total += dealt;
            landed++;

            events.Add(new BattleEvent.DamageDealt(Other(side), dealt, defender.CurrentHp, result));

            if (held) events.Add(new BattleEvent.HeldOn(Other(side), defender.Holding));
            if (braced) events.Add(new BattleEvent.Endured(Other(side)));

            // And the promise, kept. Whoever finished it goes down too, which is the only
            // thing in this engine that can end a fight in a draw.
            if (defender.HasFainted && defender.IsBonded)
            {
                attacker.TakeDamage(attacker.CurrentHp);

                events.Add(new BattleEvent.TookThemWith(side));
                events.Add(new BattleEvent.Fainted(side));
            }

            if (defender.HasFainted) break;
        }

        if (times > 1) events.Add(new BattleEvent.HitSeveralTimes(side, landed));

        // And what the thing that was touched does about being touched. After the damage,
        // because it is an answer to a hit that landed rather than a condition on it, and
        // once for the whole move rather than once per hit — DOUBLESLAP is one act of
        // touching somebody five times, and a five-times-more-dangerous DOUBLESLAP is not
        // a thing anybody would call this rule.
        if (move.MakesContact && landed > 0) Touching(side, attacker, defender, events);

        // What the user gets back, or pays, for what it dealt. Both happen whether or not
        // the target fainted — a knockout does not un-hurt the thing that took the hit,
        // and TAKE DOWN costs whether or not it worked.
        //
        // Half back and a quarter paid. Modelled, not read: the shares are in the game's
        // code and this project does not read code. They are stated here so that anybody
        // checking them knows what they are checking.
        if (carried.Kind == EffectKind.Drain && total > 0)
        {
            int back = Math.Max(1, total / 2);
            int given = attacker.Heal(back);

            if (given > 0) events.Add(new BattleEvent.Drained(side, given));
        }

        // A bell, which takes a share of what was dealt regardless of what the move was.
        // After the move's own drain rather than instead of it: a creature holding one and
        // using ABSORB gets both, which is what the games do.
        if (total > 0 && HeldItems.Drains(attacker.Carried) is { } share)
        {
            int back = attacker.Heal(Math.Max(1, total / share));

            if (back > 0)
                events.Add(new BattleEvent.ItemHealed(side, attacker.Holding, back, attacker.CurrentHp));
        }

        // And a rock, which is a flinch that belongs to the carrier rather than to the move.
        // Only on a hit that landed, and not on something already finished — a flinch is a
        // lost turn, and something that has fainted has no turn to lose.
        if (landed > 0
            && !defender.HasFainted
            && Rolls(HeldItems.Startles(attacker.Carried)))
        {
            SetFlinching(Other(side), true);
        }

        if (carried.Kind == EffectKind.Recoil && total > 0)
        {
            int cost = attacker.TakeDamage(Math.Max(1, total / 4));

            events.Add(new BattleEvent.Recoiled(side, cost));

            if (attacker.HasFainted) events.Add(new BattleEvent.Fainted(side));
        }

        // And the one that ends the user. After the damage, so the target takes the hit
        // first — an EXPLOSION that killed its user before it landed would be a move that
        // does nothing at all.
        if (carried.Kind == EffectKind.UserFaints) BlowUp(side, attacker, events);

        // What the turn owes. All three are settled here rather than in Apply, because
        // they are not riders on a hit — they do not roll against the move's secondary
        // chance, and two of them are about the user rather than the target.
        if (carried.Kind == EffectKind.Recharge && landed > 0)
        {
            attacker.MustRecharge = true;
            attacker.RechargingAfter = move.Id;
        }

        if (carried.Kind == EffectKind.LockedIn) EndLockedIn(side, attacker, events);

        if (carried.Kind == EffectKind.Trap && total > 0 && !defender.HasFainted && defender.TrappedTurns == 0)
        {
            // Two to five turns, weighted low, like the multi-hit count and marked the
            // same way: what is read is that the group holds on at all.
            defender.TrappedTurns = _rng.Next(8) switch { < 3 => 2, < 6 => 3, < 7 => 4, _ => 5 };
            defender.TrappedBy = move.Id;

            events.Add(new BattleEvent.Trapped(Other(side), move.Id));
        }

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
    /// Leaving, or failing to.
    /// <para>
    /// Refused outright against a trainer and against anything holding on, and otherwise
    /// a roll: the faster you are the better your chances, and every attempt makes the
    /// next one better. Both of those are facts about these games; the numbers that turn
    /// them into odds are in the game's code, so they are <b>modelled, not read</b>, and
    /// they are written here rather than buried — a hundred and twenty-eight over the
    /// speeds, plus thirty a try, out of two hundred and fifty-six.
    /// </para>
    /// </summary>
    private void RunFrom(Side side, Battler runner, Battler from, List<BattleEvent> events)
    {
        if (!IsWild)
        {
            events.Add(new BattleEvent.CouldNotGetAway(side));
            return;
        }

        if (runner.TrappedTurns > 0)
        {
            events.Add(new BattleEvent.HeldFast(side, runner.TrappedBy));
            return;
        }

        // What is standing opposite, as well as what is holding on. The only rule in this
        // engine where somebody else's ability decides what you may do rather than what
        // happens to you.
        if (runner.CannotEscape || Abilities.Traps(from.Ability, runner.Type1, runner.Type2, runner.Ability))
        {
            events.Add(new BattleEvent.CouldNotGetAway(side));
            return;
        }

        _attempts++;

        int mine = Math.Max(1, runner.EffectiveStat(Stat.Speed));
        int theirs = Math.Max(1, from.EffectiveStat(Stat.Speed));

        int odds = mine * 128 / theirs + 30 * _attempts;

        if (odds >= 256 || _rng.Next(256) < odds)
        {
            Escaped = true;
            events.Add(new BattleEvent.GotAway(side));

            return;
        }

        events.Add(new BattleEvent.CouldNotGetAway(side));
    }

    /// <summary>
    /// What a move with no power of its own takes, when the number is somewhere the
    /// fight already knows.
    /// <para>
    /// Nothing invented. Four groups, four numbers already on the table: everything the
    /// target has, the user's own level, half of what the target has left, and the gap
    /// between them. The other thirteen groups that carry a power of one keep their
    /// number in the game's code, and this returns nothing for them so that they stay
    /// visibly unfinished rather than quietly wrong.
    /// </para>
    /// </summary>
    private static int? WhateverElseSays(EffectKind kind, Battler attacker, Battler defender) => kind switch
    {
        EffectKind.Knockout => defender.CurrentHp,
        EffectKind.LevelDamage => attacker.Level,
        EffectKind.HalfTheirHealth => defender.CurrentHp / 2,
        EffectKind.DownToMine => defender.CurrentHp - attacker.CurrentHp,

        // Twice what was just taken, and only from the kind each one is for. A number worked
        // out from what happened rather than from the formula, which is why they belong here
        // with the other moves whose damage is not a calculation.
        //
        // Nought when nothing of the right kind landed, and nought is a refusal rather than
        // a hit for nothing — the caller turns it into "it had no effect", which is exactly
        // what these do when used on a quiet turn.
        EffectKind.Counters =>
            attacker.HurtThisTurnBy == DamageCategory.Physical ? attacker.HurtThisTurn * 2 : 0,

        EffectKind.CountersSpecial =>
            attacker.HurtThisTurnBy == DamageCategory.Special ? attacker.HurtThisTurn * 2 : 0,

        _ => null,
    };

    /// <summary>
    /// Counts a locked-in move down, and ends it the way it ends.
    /// <para>
    /// THRASH's price is not the turns — it is what is left standing there afterwards.
    /// Called on the miss as well as the hit, because a thrash that misses is still a
    /// thrash and still tires the thing doing it.
    /// </para>
    /// </summary>
    private void EndLockedIn(Side side, Battler attacker, List<BattleEvent> events)
    {
        if (attacker.ForcedSlot is null) return;

        attacker.ForcedTurns--;

        if (attacker.ForcedTurns > 0) return;

        attacker.ForcedSlot = null;

        if (attacker.HasFainted || attacker.IsConfused) return;

        // Two to five turns of it, and the same numbers CONFUSE RAY uses, because it is
        // the same confusion and there is only one of it to model.
        attacker.ConfusedTurns = 2 + _rng.Next(4);

        events.Add(new BattleEvent.Confused(side));
    }

    /// <summary>
    /// How many times a multi-hit move lands.
    /// <para>
    /// Two to five, weighted towards the low end. <b>Modelled, not read.</b> Nothing in
    /// a move's record carries this — the record says DOUBLESLAP has fifteen power and
    /// eighty-five accuracy and nothing about repetition — so the distribution is a
    /// judgement, stated here rather than buried, and the only thing derived is that the
    /// group does repeat at all.
    /// </para>
    /// </summary>
    private int RollHits()
    {
        int roll = _rng.Next(8);

        return roll < 3 ? 2 : roll < 6 ? 3 : roll < 7 ? 4 : 5;
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
    /// <summary>
    /// Takes everything the user had left.
    /// <para>
    /// Written as damage rather than as a flag so that everything downstream — the
    /// fainted event, whether the fight is over, who is left to send out — is the same
    /// code path a fatal hit takes. A separate "is dead" state would be a second way to
    /// be dead, and two ways to be dead is how a battle ends up with nobody in it.
    /// </para>
    /// </summary>
    private static void BlowUp(Side side, Battler attacker, List<BattleEvent> events)
    {
        if (attacker.HasFainted) return;

        attacker.TakeDamage(attacker.CurrentHp);

        events.Add(new BattleEvent.BlewUp(side));
        events.Add(new BattleEvent.Fainted(side));
    }

    private void Apply(
        Side side, Battler attacker, Battler defender, MoveData move, List<BattleEvent> events, bool rolled)
    {
        MoveEffect effect = MoveEffects.Of(move.Effect);

        if (effect.Kind == EffectKind.None) return;

        // SHIELD DUST, which is about the riders rather than the move. A rider is exactly
        // the thing that rolled against the move's secondary chance, and one aimed at
        // somebody else is the only kind this refuses — a move whose whole point is its
        // effect is not a rider, and neither is anything the user does to itself.
        if (rolled && !effect.OnUser && Abilities.ShrugsOffRiders(defender.Ability)) return;

        // The ones that are about turns rather than about the hit. They were settled
        // where the turn was taken, and falling through to here gave them the default
        // effect — a stage change of nothing, to a stat that has no stages — so WRAP
        // landed and then said "The wild PIDGEY's HP won't go any lower!"
        // And the one that means there is nothing more to do, which belongs in this list for
        // exactly the reason the others do and was not in it. Effect 0 is twenty-three moves
        // — TACKLE among them — and every one of them fell through to the stage code and
        // said a stat had not moved. Nothing in the fight changed, so nothing looked wrong;
        // what it cost was a message per hit that a client could draw.
        //
        // Found by a test counting the stage changes a different move produced and getting
        // one more than there were stats.
        if (effect.Kind is EffectKind.Recharge or EffectKind.TwoTurn or EffectKind.LockedIn or EffectKind.Trap
            or EffectKind.Knockout or EffectKind.LevelDamage or EffectKind.HalfTheirHealth or EffectKind.DownToMine
            or EffectKind.CrashOnMiss or EffectKind.UserFaints or EffectKind.Nothing)
        {
            return;
        }

        if (rolled && !_rng.Chance(move.SecondaryChance)) return;

        // Made sure of. It lasts as long as they are standing there rather than for a
        // count, which is what makes it worse than being wrapped.
        if (effect.Kind == EffectKind.NoEscape)
        {
            if (defender.CannotEscape) events.Add(new BattleEvent.NothingHappened(Other(side)));
            else
            {
                defender.CannotEscape = true;
                events.Add(new BattleEvent.Trapped(Other(side), move.Id));
            }

            return;
        }

        // Taken. Only when the user is carrying nothing, which is the games' rule and
        // also the only one that does not need somewhere to put a second item.
        if (effect.Kind == EffectKind.Steal)
        {
            if (attacker.Holding != 0 || defender.Holding == 0)
            {
                events.Add(new BattleEvent.NothingHappened(Other(side)));
                return;
            }

            attacker.Holding = defender.Holding;
            attacker.Carried = defender.Carried;
            defender.Holding = 0;
            defender.Carried = null;

            events.Add(new BattleEvent.Stole(side, attacker.Holding));

            return;
        }

        // And sent off, which ends a fight with something wild in it and does nothing at
        // all to somebody's trainer — the games make them switch instead, and switching
        // somebody else's party is not a thing this engine can do.
        if (effect.Kind == EffectKind.BlowAway)
        {
            // SUCTION CUPS with the others, because being blown off the field is a way of
            // being made to leave and this is the ability that refuses to be.
            if (!IsWild || defender.CannotEscape || Abilities.HoldsGround(defender.Ability))
            {
                events.Add(new BattleEvent.NothingHappened(Other(side)));
                return;
            }

            Escaped = true;
            events.Add(new BattleEvent.BlownAway(Other(side), move.Id));

            return;
        }

        Side at = effect.OnUser ? side : Other(side);
        Battler target = effect.OnUser ? attacker : defender;

        // The four that switch a rule off. Written together because they are one idea
        // four times, and every rule they switch off was already here with one caller.
        if (effect.Kind == EffectKind.Haze)
        {
            // Both sides, including the one that used it. That is what makes it a move
            // somebody has to mean rather than a free reset, and it needs no count at
            // all — the only one of the four with nothing modelled about it.
            Player.ResetStages();
            Opponent.ResetStages();

            events.Add(new BattleEvent.StagesCleared(side));

            return;
        }

        if (effect.Kind == EffectKind.Taunt)
        {
            if (target.TauntTurns > 0)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            // Three turns. Modelled, and deliberately not the five every wall in this engine
            // uses — this one is a nuisance rather than a wall, and giving it the same number
            // would make it one.
            target.TauntTurns = 3;

            events.Add(new BattleEvent.Taunted(at));

            return;
        }

        if (effect.Kind == EffectKind.Torment)
        {
            if (target.IsTormented)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            target.IsTormented = true;

            events.Add(new BattleEvent.Tormented(at));

            return;
        }

        if (effect.Kind == EffectKind.Endure)
        {
            target.IsEnduring = true;

            events.Add(new BattleEvent.BracedItself(at));

            return;
        }

        if (effect.Kind == EffectKind.Bond)
        {
            target.IsBonded = true;

            events.Add(new BattleEvent.Bonded(at));

            return;
        }

        if (effect.Kind == EffectKind.Split)
        {
            // Both onto the average, which is the only move in this game that can put health
            // back on somebody by hurting them and hurt somebody by healing them.
            int each = (attacker.CurrentHp + defender.CurrentHp) / 2;

            Settle(attacker, each);
            Settle(defender, each);

            events.Add(new BattleEvent.HealthShared(at, each));

            return;
        }

        if (effect.Kind is EffectKind.Takes or EffectKind.Learns)
        {
            // Both take the other one's last move; they differ only in whether it survives
            // the fight. Neither writes to a save from in here — what is permanent about the
            // permanent one is decided by whoever owns the creature, outside this class.
            if (defender.LastMove is not { } copying || attacker.LastSlot is not { } into)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            // It goes in the slot the move that took it came from, which is the games' own
            // rule and the only one that is not arbitrary: something has to be given up, and
            // the thing given up is the taking.
            attacker.PutInSlot(into, copying);

            events.Add(new BattleEvent.LearnedMove(at, copying.Id, effect.Kind == EffectKind.Learns));

            return;
        }

        if (effect.Kind is EffectKind.TakesAbility or EffectKind.SwapsAbility)
        {
            // The first thing in this engine that changes an ability. Until now an ability
            // was a lookup on the species and the slot a creature was born with, with
            // nowhere for an answer of its own to live.
            int mine = attacker.Ability;
            int theirs = defender.Ability;

            if (mine == theirs)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            attacker.BorrowedAbility = theirs;

            // And the other half, for the one that trades rather than takes. Written as the
            // borrowed value on both sides rather than by swapping the slots they were born
            // with, because being born with something is not a thing a fight may change.
            if (effect.Kind == EffectKind.SwapsAbility) defender.BorrowedAbility = mine;

            events.Add(new BattleEvent.AbilityMoved(at, theirs));

            return;
        }

        if (effect.Kind == EffectKind.CopiesStages)
        {
            // Every stage, and the user's own are replaced rather than added to. Both halves
            // matter: a move that took only the good ones would be a move nobody could play
            // around, and one that added would turn two of these into six stages of anything.
            attacker.CopyStagesFrom(defender);

            events.Add(new BattleEvent.CopiedStages(at));

            return;
        }

        if (effect.Kind == EffectKind.Damps)
        {
            // Which type each of them turns down is on the effect rather than in a branch
            // here, because the two moves differ in nothing else at all.
            //
            // The stat field carries it: Speed for the one that damps electricity, Attack for
            // the one that damps fire. That is a field being used for something other than
            // its name, which is worth saying out loud — the effect table has no type field
            // and adding one for two moves would be a column of nulls.
            PokemonType damping = effect.Stat == Stat.Speed
                ? PokemonType.Electric
                : PokemonType.Fire;

            if (DampedTurns > 0 && Damped == damping)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            Damped = damping;
            DampedTurns = Skies.Turns;

            events.Add(new BattleEvent.Damped(at));

            return;
        }

        if (effect.Kind == EffectKind.Nightmare)
        {
            // Only on somebody asleep, which is the whole rule: it is not a condition of its
            // own, it is a thing sleep does once somebody has made it do it.
            if (target.Status != StatusCondition.Sleep || target.InNightmare)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            target.InNightmare = true;

            events.Add(new BattleEvent.Drowsy(at));

            return;
        }

        if (effect.Kind == EffectKind.Yawn)
        {
            if (target.Status != StatusCondition.None || target.DrowsyTurns > 0)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            // Two, so that it lands at the end of the turn after this one. Modelled, and the
            // delay is the entire move — one that put somebody to sleep now would be a
            // different and much better move.
            target.DrowsyTurns = 2;

            events.Add(new BattleEvent.Drowsy(at));

            return;
        }

        if (effect.Kind == EffectKind.Ingrain)
        {
            if (target.IsRooted)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            target.IsRooted = true;

            // And it cannot leave, which is the price. The same field a wrap uses, because
            // being unable to leave is one state however it was arrived at.
            target.CannotEscape = true;

            events.Add(new BattleEvent.TookRoot(at));

            return;
        }

        if (effect.Kind == EffectKind.Perish)
        {
            // Everybody, including whoever sang it. That is the games' rule and it is what
            // makes the move a threat rather than a win — and it is why this is the one thing
            // on a battler that leaving the field does not clear.
            foreach (Side heard in new[] { Side.Player, Side.Opponent })
            {
                Battler who = Of(heard);

                if (who.PerishTurns > 0) continue;

                who.PerishTurns = 4;

                events.Add(new BattleEvent.PerishCount(heard, who.PerishTurns));
            }

            return;
        }

        if (effect.Kind == EffectKind.Goad)
        {
            // Stronger first and then confused, because a creature that fainted to its own
            // confusion before the stage landed would be a move that sometimes did half of
            // itself.
            target.ChangeStage(effect.Stat, effect.Stages);

            events.Add(new BattleEvent.StageChanged(at, effect.Stat, effect.Stages, true));

            if (target.ConfusedTurns == 0 && !Abilities.RefusesConfusion(target.Ability))
            {
                target.ConfusedTurns = _rng.Next(4) + 2;

                events.Add(new BattleEvent.Confused(at));
            }

            return;
        }

        if (effect.Kind == EffectKind.HealByWeather)
        {
            // How much depends on what the sky is doing: more in sun, less in anything else
            // that is happening, half otherwise. Modelled — a move's record says nothing
            // about weather — but which weather is up is read, so only the shares are ours.
            // Quarters of its own maximum: half in a clear sky, three quarters in sun, and
            // one quarter in anything else that is happening. Modelled — a move's record says
            // nothing about weather — but which weather is up is read, so only the shares
            // are this project's.
            int share = Overhead switch
            {
                Weather.None => 2,
                Weather.Sun => 3,
                _ => 1,
            };

            int given = target.Heal(Math.Max(1, target.MaxHp * share / 4));

            if (given > 0) events.Add(new BattleEvent.Recovered(at, given));
            else events.Add(new BattleEvent.NothingHappened(at));

            return;
        }

        if (effect.Kind == EffectKind.Spite)
        {
            // Whatever they last did, and nothing if they have not done anything. Four uses
            // is modelled; that there is a slot to take them from at all is the engine having
            // spent PP since moves could run out.
            if (target.LastSlot is not { } slot || target.PpLeft(slot) <= 0)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            for (int taken = 0; taken < 4 && target.PpLeft(slot) > 0; taken++) target.Spend(slot);

            events.Add(new BattleEvent.CannotUse(at, target.MoveAt(slot)?.Id ?? 0));

            return;
        }

        if (effect.Kind == EffectKind.Rouse)
        {
            // The damage has already been doubled where damage is worked out. This is the
            // other half: it wakes them up out of what it hit them for, which makes it the
            // only move in this game that is worth less the second time you use it.
            if (target.Status != StatusCondition.Paralysis) return;

            target.Status = StatusCondition.None;

            events.Add(new BattleEvent.PutRight(at, 0, Ailments.Paralysis));

            return;
        }

        if (effect.Kind == EffectKind.BreaksWalls)
        {
            // The damage has already landed by the time this runs, which is the right order:
            // a wall the move went through is a wall that was still up when it did.
            if (target.ReflectTurns + target.ScreenTurns == 0) return;

            target.ReflectTurns = 0;
            target.ScreenTurns = 0;

            events.Add(new BattleEvent.WallsBroke(at));

            return;
        }

        if (effect.Kind == EffectKind.KnocksOff)
        {
            if (target.Holding == 0) return;

            events.Add(new BattleEvent.KnockedOff(at, target.Holding));

            // Gone rather than taken. THIEF is the move that takes one, and an item that
            // ended up in the user's hands here would be THIEF by another name.
            target.Holding = 0;
            target.Carried = null;

            return;
        }

        if (effect.Kind == EffectKind.Spins)
        {
            // Everything at once, because it is one act. A move that shook off a wrap and
            // left a seed would be two moves sharing a name.
            bool anything = target.IsSeeded || target.TrappedTurns > 0;

            target.IsSeeded = false;
            target.TrappedTurns = 0;
            target.TrappedBy = 0;

            if (anything) events.Add(new BattleEvent.ShookFree(at));

            return;
        }

        if (effect.Kind == EffectKind.Identifies)
        {
            if (target.IsIdentified)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            target.IsIdentified = true;

            events.Add(new BattleEvent.Identified(at));

            return;
        }

        if (effect.Kind == EffectKind.Screen)
        {
            bool physical = effect.Stat == Stat.Defense;

            int already = physical ? target.ReflectTurns : target.ScreenTurns;

            if (already > 0)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            // Five turns. Modelled, and deliberately the same five MIST and SAFEGUARD use —
            // one number for every wall in this engine rather than three that could drift.
            if (physical) target.ReflectTurns = Skies.Turns;
            else target.ScreenTurns = Skies.Turns;

            events.Add(new BattleEvent.ScreenRose(at, physical));

            return;
        }

        if (effect.Kind == EffectKind.Seed)
        {
            // Nothing may be seeded twice, and there is no Grass type in this engine to be
            // immune — that rule is about a type chart this move does not consult.
            if (target.IsSeeded)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            target.IsSeeded = true;

            events.Add(new BattleEvent.Seeded(at));

            return;
        }

        if (effect.Kind == EffectKind.Leave)
        {
            // The same code running away uses, reached by a move instead of by a choice.
            Escaped = true;

            events.Add(new BattleEvent.GotAway(side));

            return;
        }

        if (effect.Kind == EffectKind.Mist)
        {
            // Five turns. Modelled, not read.
            target.MistTurns = 5;

            events.Add(new BattleEvent.MistRose(at));

            return;
        }

        if (effect.Kind == EffectKind.Safeguard)
        {
            target.SafeguardTurns = 5;

            events.Add(new BattleEvent.Safeguarded(at));

            return;
        }

        if (effect.Kind == EffectKind.TakeAim)
        {
            attacker.HasAimed = true;

            events.Add(new BattleEvent.TookAim(side));

            return;
        }

        // The sky, from the four moves that change it. Which one each brings comes from
        // Skies, so the effect table and the rule share one mapping rather than two.
        if (effect.Kind == EffectKind.Weather && Skies.Of(move.Effect) is not Weather.None and var sky)
        {
            if (Sky == sky && SkyTurns > 0)
            {
                events.Add(new BattleEvent.NothingHappened(at));
                return;
            }

            BeginWeather(sky, events);
            return;
        }

        if (effect.Kind == EffectKind.Confuse)
        {
            // Two to five turns, and it does not stack: somebody already confused is
            // already confused. Modelled, not read — nothing in a move's record says how
            // long CONFUSE RAY muddles anybody for.
            if (!effect.OnUser && target.IsGuardedFromHarm)
            {
                if (!rolled) events.Add(new BattleEvent.Shielded(at));
                return;
            }

            // OWN TEMPO, which refuses to be muddled at all. Said as "nothing happened"
            // rather than silently, because a move that does nothing and says nothing is
            // the failure this project's narrator guardrail exists to catch.
            if (target.IsConfused || target.HasFainted || Abilities.RefusesConfusion(target.Ability))
            {
                if (!rolled) events.Add(new BattleEvent.NothingHappened(at));
                return;
            }

            target.ConfusedTurns = _rng.Next(4) + 2;
            events.Add(new BattleEvent.Confused(at));

            return;
        }

        if (effect.Kind is EffectKind.Flinch or EffectKind.FirstImpression)
        {
            // Set on the target, and it only costs them anything if they have not gone
            // yet. Nothing clears it at the end of a turn because nothing needs to: the
            // one place it is read also unsets it.
            SetFlinching(Other(side), true);

            return;
        }

        if (effect.Kind == EffectKind.Heal)
        {
            // Half of the user's own maximum, rounded up. Modelled, not read — a move's
            // record says RECOVER has no power and nothing about how much it gives back.
            int given = target.Heal((target.MaxHp + 1) / 2);

            if (given > 0) events.Add(new BattleEvent.Recovered(at, given));
            else events.Add(new BattleEvent.NothingHappened(at));

            return;
        }

        // TWINEEDLE, which is the only move on this cartridge that lands twice and
        // carries a condition. Treated as the rider it is rather than given a kind of its
        // own: what makes it a rider is the secondary chance in its own record, and that
        // has already been rolled by the time this is reached.
        if (effect.Kind == EffectKind.Twice && effect.Status != StatusCondition.None)
        {
            if (defender.IsGuardedFromHarm) return;

            if (defender.TryApplyStatus(effect.Status, sleepTurns: _rng.Next(3) + 1))
                events.Add(new BattleEvent.StatusInflicted(Other(side), effect.Status));

            return;
        }

        // Sleeps, and wakes whole. Both halves happen or neither does: a creature already
        // asleep cannot rest, and one on full health has nothing to rest for — which is
        // the games' own rule and the only one here that is not read off a field.
        if (effect.Kind == EffectKind.Sleeps)
        {
            if (target.CurrentHp >= target.MaxHp || !target.TryApplyStatus(StatusCondition.Sleep, _rng.Next(3) + 1))
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            int back = target.Heal(target.MaxHp);

            events.Add(new BattleEvent.StatusInflicted(at, StatusCondition.Sleep));
            events.Add(new BattleEvent.Recovered(at, back));

            return;
        }

        // Blocks whatever they just did. Nothing to block if they have not moved yet, and
        // nothing to do if something is already blocked — one at a time, which is the
        // games' rule and the only one that needs no second counter.
        if (effect.Kind == EffectKind.Disable)
        {
            if (target.LastSlot is not { } theirs || target.DisabledTurns > 0)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            target.DisabledSlot = theirs;

            // Modelled, not read: nothing in DISABLE's record says how long it holds.
            target.DisabledTurns = _rng.Next(4) + 2;

            events.Add(new BattleEvent.CannotUse(at, target.MoveAt(theirs)?.Id ?? 0));

            return;
        }

        // And the other way round: makes them do it again. The holding is the same
        // holding THRASH uses, which is why this needed no machinery of its own.
        if (effect.Kind == EffectKind.Encore)
        {
            if (target.LastSlot is not { } again || target.ForcedSlot is not null)
            {
                events.Add(new BattleEvent.NothingHappened(at));

                return;
            }

            target.ForcedSlot = again;

            // Modelled for the same reason, and deliberately the same shape of number.
            target.ForcedTurns = _rng.Next(4) + 2;

            events.Add(new BattleEvent.MustRepeat(at, target.MoveAt(again)?.Id ?? 0));

            return;
        }

        if (effect.Kind == EffectKind.Status)
        {
            // Refused while a safeguard holds — and only from outside. REST puts its own
            // user to sleep through one, which is the games' rule and also the only
            // reading where a move that shields a side does not shield it from itself.
            if (!effect.OnUser && target.IsGuardedFromHarm)
            {
                if (!rolled) events.Add(new BattleEvent.Shielded(at));

                return;
            }

            // Sleep runs one to three turns. Chosen here rather than in the battler
            // because how long anything lasts is a rule of the battle, and the battler is
            // only the thing it happens to.
            if (target.TryApplyStatus(effect.Status, sleepTurns: _rng.Next(3) + 1))
                events.Add(new BattleEvent.StatusInflicted(at, effect.Status));
            else if (!rolled)
                events.Add(new BattleEvent.NothingHappened(at));

            return;
        }

        // Refused while mist holds, and only from outside: a move that trades one of its
        // user's own stats for something is not somebody else lowering it.
        // Somebody else lowering it, which is the only kind either shield answers. Using
        // your own stats for something is not somebody else lowering them, and an ability
        // that refused BELLY DRUM would be refusing a move its owner chose.
        if (effect.Stages < 0 && !effect.OnUser
            && (target.IsMisted || Abilities.Protects(target.Ability, effect.Stat)))
        {
            if (!rolled) events.Add(new BattleEvent.Shielded(at));

            return;
        }

        // All five at once, on one roll. Said as one message per stat that actually moved,
        // because a screen has one line per stat and a single "everything went up" would be
        // a line that is sometimes a lie — CLEAR BODY and a stage already at six both stop
        // some of them.
        if (effect.Kind == EffectKind.AllStages)
        {
            foreach (Stat stat in effect.Many ?? MoveEffects.Five)
            {
                int had = target.StageOf(stat);

                target.ChangeStage(stat, effect.Stages);

                events.Add(new BattleEvent.StageChanged(
                    at, stat, effect.Stages, target.StageOf(stat) != had));
            }

            return;
        }

        // The user's own condition, gone. Nothing else clears one without an item, which is
        // why this is its own kind rather than a status of None — applying "none" through the
        // status path would have to mean something everywhere else that path is used.
        if (effect.Kind == EffectKind.Refresh)
        {
            if (target.Status == StatusCondition.None)
            {
                events.Add(new BattleEvent.NothingHappened(at));
            }
            else
            {
                target.Status = StatusCondition.None;

                events.Add(new BattleEvent.PutRight(at, 0, Ailments.Everything));
            }

            return;
        }

        // Sharper until it leaves the field, which is what makes it a flag on the battler
        // rather than a stage: HAZE does not clear it and nothing lowers it.
        if (effect.Kind == EffectKind.Focus)
        {
            if (target.IsFocused)
            {
                events.Add(new BattleEvent.NothingHappened(at));
            }
            else
            {
                target.IsFocused = true;

                events.Add(new BattleEvent.TookAim(at));
            }

            return;
        }

        // FALSE SWIPE, whose whole rule is applied where the damage is and has nothing to do
        // here. Named so it is not silent rather than left out so it looks unwritten.
        if (effect.Kind == EffectKind.LeavesOne) return;

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
    /// <param name="chose">
    /// The move this one is about to make, when there is one. Needed for the single move in
    /// this game whose whole point is that it works while its user cannot act — a creature
    /// that can only talk in its sleep has to get past the check that stops it moving, and
    /// that check has to know what it was going to do to let it through.
    /// </param>
    private bool CanAct(Side side, Battler battler, List<BattleEvent> events, MoveData? chose = null)
    {
        // Before everything, because it is not a condition and not a flinch — it is a
        // debt. Paralysis does not get a chance to also take a turn that is already gone.
        if (battler.MustRecharge)
        {
            battler.MustRecharge = false;

            events.Add(new BattleEvent.Recharging(side, battler.RechargingAfter));

            return false;
        }

        // Before the conditions, because a flinch is not one. It lasts exactly as long
        // as the turn it was caused in, and it only reaches somebody who had not moved
        // yet — which is why nothing has to clear it for the loser of the speed roll.
        if (Flinching(side))
        {
            SetFlinching(side, false);
            events.Add(new BattleEvent.Flinched(side));

            return false;
        }

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

                // Except the one move that is for this. It does not wake anybody and it does
                // not shorten the sleep — the count above has already come down, exactly as
                // it would have if nothing were carried, so a creature holding it sleeps for
                // precisely as long as one that is not.
                if (chose is not null && MoveEffects.Of(chose.Effect).Kind == EffectKind.Sleeping)
                    return true;

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
        }

        return NotTooConfused(side, battler, events);
    }

    /// <summary>
    /// Confusion, which is checked after the conditions because it is not one of them.
    /// <para>
    /// It counts down whether or not it costs the turn, wears off before the turn rather
    /// than after it — snapping out and then acting is what the games do — and half the
    /// time replaces the move with a hit on oneself. That half is modelled: nothing in a
    /// move's record says how often a confused creature misjudges.
    /// </para>
    /// <para>
    /// The damage is worked out as an ordinary physical hit of forty power against one's
    /// own defence, and typeless — which is why it is dealt here rather than routed
    /// through a move nobody has.
    /// </para>
    /// </summary>
    private bool NotTooConfused(Side side, Battler battler, List<BattleEvent> events)
    {
        if (!battler.IsConfused) return true;

        battler.ConfusedTurns--;

        if (battler.ConfusedTurns <= 0)
        {
            events.Add(new BattleEvent.SnappedOut(side));
            return true;
        }

        if (!_rng.Chance(50)) return true;

        int hurt = battler.TakeDamage(DamageCalculator.Confusion(battler));

        events.Add(new BattleEvent.HurtItself(side, hurt));

        if (battler.HasFainted) events.Add(new BattleEvent.Fainted(side));

        return false;
    }

    /// <summary>Poison and burn each take a sixteenth of maximum health, minimum one.</summary>
    /// <summary>
    /// What the sky does at the end of a turn, and how it runs out.
    /// <para>
    /// Before everything else in the turn's tail, because a sandstorm that finishes
    /// somebody should finish them before their own poison is asked about — one cause of
    /// death per turn, and the room's is the one that was there first.
    /// </para>
    /// <para>
    /// The countdown runs on <see cref="Sky"/> rather than on what anybody can feel. An
    /// AIR LOCK does not make the weather last longer; it makes nobody notice it, and when
    /// it leaves the field whatever is left of the five turns is still there.
    /// </para>
    /// </summary>
    private void ApplyWeather(List<BattleEvent> events)
    {
        if (Sky == Weather.None) return;

        Weather felt = Overhead;

        if (felt != Weather.None)
        {
            foreach (Side side in new[] { Side.Player, Side.Opponent })
            {
                Battler battler = Of(side);

                if (battler.HasFainted) continue;

                if (Abilities.DrinksFrom(battler.Ability, felt) && battler.CurrentHp < battler.MaxHp)
                {
                    int healed = battler.Heal(Math.Max(1, battler.MaxHp / Skies.Share));

                    events.Add(new BattleEvent.WeatherHealed(side, felt, healed, battler.CurrentHp));

                    continue;
                }

                if (!Skies.Bites(felt)) continue;
                if (Skies.Shrugs(felt, battler.Type1, battler.Type2)) continue;
                if (Abilities.ShrugsOffWeather(battler.Ability, felt)) continue;

                int taken = battler.TakeDamage(Math.Max(1, battler.MaxHp / Skies.Share));

                events.Add(new BattleEvent.WeatherHurt(side, felt, taken, battler.CurrentHp));

                if (battler.HasFainted) events.Add(new BattleEvent.Fainted(side));
            }
        }

        if (--SkyTurns > 0) return;

        events.Add(new BattleEvent.WeatherEnded(Sky));

        Sky = Weather.None;
    }

    private void ApplyEndOfTurn(List<BattleEvent> events)
    {
        ApplyWeather(events);

        foreach (Side side in new[] { Side.Player, Side.Opponent })
        {
            Battler battler = Of(side);
            if (battler.HasFainted) continue;

            // What it is carrying, before anything that hurts. A sixteenth, which is
            // modelled — the item's own parameter is ten and the two are not the same
            // number, so the ten is left alone rather than pressed into meaning something.
            if (HeldItems.Feeds(battler.Carried) && battler.CurrentHp < battler.MaxHp)
            {
                int fed = battler.Heal(Math.Max(1, battler.MaxHp / HeldItems.ScrapsFraction));

                if (fed > 0)
                    events.Add(new BattleEvent.ItemHealed(side, battler.Holding, fed, battler.CurrentHp));
            }

            // What is being drained, before what is holding on. A share of its own maximum
            // to whoever put it there, and it lasts as long as its target is standing on the
            // field rather than for a count — which is what makes leaving the only answer.
            if (battler.IsSeeded)
            {
                int taken = battler.TakeDamage(Math.Max(1, battler.MaxHp / Skies.Share));

                events.Add(new BattleEvent.Sapped(side, taken, battler.CurrentHp));

                Battler other = Of(Other(side));

                if (!other.HasFainted && other.Heal(taken) > 0)
                    events.Add(new BattleEvent.Recovered(Other(side), taken));

                if (battler.HasFainted)
                {
                    events.Add(new BattleEvent.Fainted(side));

                    continue;
                }
            }

            // What sleep is costing, which is only ever a thing while sleep lasts.
            if (battler.InNightmare)
            {
                if (battler.Status != StatusCondition.Sleep)
                {
                    battler.InNightmare = false;
                }
                else
                {
                    int cost = battler.TakeDamage(Math.Max(1, battler.MaxHp / 4));

                    events.Add(new BattleEvent.HurtBySleep(side, cost, battler.CurrentHp));

                    if (battler.HasFainted)
                    {
                        events.Add(new BattleEvent.Fainted(side));

                        continue;
                    }
                }
            }

            // What roots are giving back.
            if (battler.IsRooted && battler.CurrentHp < battler.MaxHp)
            {
                int fed = battler.Heal(Math.Max(1, battler.MaxHp / Skies.Share));

                if (fed > 0) events.Add(new BattleEvent.Recovered(side, fed));
            }

            // And the drowsiness, which lands rather than lapses.
            if (battler.DrowsyTurns > 0 && --battler.DrowsyTurns <= 0
                && battler.TryApplyStatus(StatusCondition.Sleep, sleepTurns: _rng.Next(3) + 1))
            {
                events.Add(new BattleEvent.StatusInflicted(side, StatusCondition.Sleep));
            }

            // And the count nobody can leave behind.
            if (battler.PerishTurns > 0)
            {
                battler.PerishTurns--;

                events.Add(new BattleEvent.PerishCount(side, battler.PerishTurns));

                if (battler.PerishTurns == 0)
                {
                    battler.TakeDamage(battler.CurrentHp);

                    events.Add(new BattleEvent.Fainted(side));

                    continue;
                }
            }

            if (battler.TauntTurns > 0) battler.TauntTurns--;

            // And the walls, counted down with everything else that lasts turns.
            if (battler.ReflectTurns > 0) battler.ReflectTurns--;
            if (battler.ScreenTurns > 0) battler.ScreenTurns--;

            // And the room's own count, taken off once rather than once per battler — this
            // loop runs for both sides and a fact about the room does not tick twice.
            if (side == Side.Player && DampedTurns > 0) DampedTurns--;

            // And again at the end, because poison and a sandstorm both land here and a
            // berry that only ever answered a move would sit uneaten while its carrier
            // fainted to the weather.
            Nibble(side, battler, events);

            // A block runs out. Counted down here with everything else that lasts turns,
            // and cleared rather than left at nought so the slot is free again.
            if (battler.DisabledTurns > 0 && --battler.DisabledTurns <= 0)
            {
                battler.DisabledSlot = null;

                events.Add(new BattleEvent.CanUseAgain(side));
            }

            // What is holding on, before what is inside. Both take a sixteenth and both
            // can finish somebody; the order between them is arbitrary and stated here
            // so that it is at least the same every time.
            if (battler.TrappedTurns > 0)
            {
                battler.TrappedTurns--;

                int held = battler.TakeDamage(Math.Max(1, battler.MaxHp / 16));

                events.Add(new BattleEvent.TrapHurt(side, battler.TrappedBy, held, battler.CurrentHp));

                if (battler.HasFainted)
                {
                    events.Add(new BattleEvent.Fainted(side));
                    continue;
                }

                if (battler.TrappedTurns == 0)
                {
                    events.Add(new BattleEvent.BrokeFree(side, battler.TrappedBy));
                    battler.TrappedBy = 0;
                }
            }

            if (battler.Status is not (StatusCondition.Poison or StatusCondition.Burn)) continue;

            int damage = Math.Max(1, battler.MaxHp / 16);
            int dealt = battler.TakeDamage(damage);

            events.Add(new BattleEvent.StatusHurt(side, battler.Status, dealt, battler.CurrentHp));

            if (battler.HasFainted) events.Add(new BattleEvent.Fainted(side));
        }
    }
}
