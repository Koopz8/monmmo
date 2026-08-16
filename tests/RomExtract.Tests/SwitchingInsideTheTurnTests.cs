using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Swapping a creature, which used to happen outside the fight and now happens inside it.
/// <para>
/// Two callers rebuilt a battle around the creature that had not moved: one in
/// <c>Encounter</c> and one in <c>Duels</c>. The first remembered what a rebuild costs. The
/// second did not, and nothing said so — a duel's weather stopped the moment anybody
/// swapped, and the moment anybody fainted, because replacing a fainted creature came
/// through the same method.
/// </para>
/// <para>
/// The fix is not the missing call. It is that a duel no longer tears the battle down at
/// all, so there is nothing left to carry across and nothing left to forget. And once the
/// switch is inside the turn, the rule milestone 167 removed — a move that goes first
/// against somebody leaving — is a rule something can finally observe.
/// </para>
/// </summary>
public class SwitchingInsideTheTurnTests
{
    private static SpeciesData Kind(int ability = 0, int speed = 60) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 100, BaseAttack = 60, BaseDefense = 60,
        BaseSpeed = (byte)speed, BaseSpAttack = 60, BaseSpDefense = 60,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Ability1 = (byte)ability,
    };

    private static MoveData Move(int effect, int power = 0, sbyte priority = 0) =>
        new(1, string.Empty, (byte)effect, (byte)power, PokemonType.Normal, 100, 20, 0, 0, priority);

    private static MoveData RainDance => Move(Skies.RainDance);

    private static Battler One(int ability = 0, int speed = 60, params MoveData[] moves) =>
        new Battler(Kind(ability, speed), 50).Knowing(moves.Length == 0 ? [Move(0, 40)] : moves);

    // ---- what a duel used to drop --------------------------------------------------------

    /// <summary>
    /// The weather outlives a switch in a duel.
    /// <para>
    /// <b>The bug.</b> A five-turn rule anybody could cancel for free by swapping — which is
    /// what the note on <c>ContinueFrom</c> has said must not happen since the day it was
    /// written, from the one caller that never called it.
    /// </para>
    /// </summary>
    [Fact]
    public void TheWeatherOutlivesASwitchInADuel()
    {
        Battler a = One(moves: [RainDance]);
        Battler spare = One();
        Battler b = One(moves: [RainDance]);

        var duel = new Duel(1, 2, [a, spare], [b], 7);

        duel.Current.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(Weather.Rain, duel.Current.Sky);

        int left = duel.Current.SkyTurns;

        duel.SwitchTo(1, 1);

        Assert.Equal(Weather.Rain, duel.Current.Sky);
        Assert.Equal(left, duel.Current.SkyTurns);
    }

    /// <summary>
    /// And a faint, which is the half nobody chooses.
    /// <para>
    /// Replacing somebody who has gone down comes through the same method as choosing to
    /// swap, so the weather stopped there too — and that one cannot be avoided by playing
    /// differently.
    /// </para>
    /// </summary>
    [Fact]
    public void AndOutlivesAFaintInADuel()
    {
        Battler a = One(moves: [RainDance]);
        Battler spare = One();
        Battler b = One(moves: [RainDance]);

        var duel = new Duel(1, 2, [a, spare], [b], 7);

        duel.Current.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        int left = duel.Current.SkyTurns;

        duel.SendNext(1);

        Assert.Equal(Weather.Rain, duel.Current.Sky);
        Assert.Equal(left, duel.Current.SkyTurns);
    }

    /// <summary>
    /// An ability that fires on arriving fires in a duel.
    /// <para>
    /// <c>Battle.Arrival</c> was called from <c>Encounter</c> twice and from nowhere else in
    /// the server, so INTIMIDATE, DRIZZLE, DROUGHT and SAND STREAM did nothing whatever in a
    /// fight between two people.
    /// </para>
    /// </summary>
    [Fact]
    public void AnAbilityThatFiresOnArrivingFiresInADuel()
    {
        Battler a = One();
        Battler storm = One(Abilities.SandStream);
        Battler b = One();

        var duel = new Duel(1, 2, [a, storm], [b], 7);

        Assert.Equal(Weather.None, duel.Current.Sky);

        duel.SwitchTo(1, 1);

        Assert.Equal(Weather.Sandstorm, duel.Current.Sky);
        Assert.Contains(duel.Arriving, e => e is BattleEvent.WeatherBegan);
    }

    /// <summary>
    /// A duel hands the engine both benches.
    /// <para>
    /// <c>PlayerParty</c> and <c>OpponentParty</c> exist for the one move that reaches past
    /// the field, and nothing in the whole server ever set either of them — so that move
    /// reached an empty list in every fight ever played, and only tests that supplied the
    /// party by hand could tell.
    /// </para>
    /// </summary>
    [Fact]
    public void ADuelHandsTheEngineBothBenches()
    {
        Battler a = One();
        Battler spare = One();
        Battler b = One();

        var duel = new Duel(1, 2, [a, spare], [b], 7);

        Assert.Equal(2, duel.Current.PlayerParty.Count);
        Assert.Single(duel.Current.OpponentParty);
    }

    // ---- the switch is inside the turn now -----------------------------------------------

    /// <summary>
    /// A switch is a thing the engine does, in the order it decided.
    /// <para>
    /// The event says so. Before this it was the server swapping between calls, so the fight
    /// itself had no idea a switch had happened and could not place it against the moves
    /// either side of it.
    /// </para>
    /// </summary>
    [Fact]
    public void TheEngineDoesTheSwitchItself()
    {
        Battler a = One();
        Battler spare = One();
        Battler b = One();

        var battle = new Battle(a, b, 7) { PlayerParty = [a, spare], OpponentParty = [b] };

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.SwitchTo(1), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.CameIn { Side: Side.Player });
        Assert.Same(spare, battle.Player);
    }

    /// <summary>
    /// And it goes before the other side's move, so whoever comes in takes the hit.
    /// <para>
    /// The point of switching at all. A switch resolved after the move would mean the
    /// creature you were trying to get out of the way took it anyway.
    /// </para>
    /// </summary>
    [Fact]
    public void AndItGoesBeforeTheOtherSidesMove()
    {
        Battler a = One(speed: 1);
        Battler spare = One(speed: 1);
        Battler b = One(speed: 200, moves: [Move(0, 60)]);

        var battle = new Battle(a, b, 7) { PlayerParty = [a, spare], OpponentParty = [b] };

        battle.ResolveTurn(new BattleAction.SwitchTo(1), new BattleAction.UseMove(0));

        // The one that left is untouched and the one that came in wore it, even though the
        // other side is two hundred times faster.
        Assert.Equal(a.MaxHp, a.CurrentHp);
        Assert.True(spare.CurrentHp < spare.MaxHp, "whoever came in did not take the hit");
    }

    /// <summary>
    /// Leaving the field lets go of what the creature had built.
    /// <para>
    /// Otherwise swapping out and back in is a way of banking a boost, which is not a rule
    /// this game has. What was <em>done to</em> it — a condition, a count of turns asleep —
    /// travels with it, because those are facts about the creature rather than about the
    /// square.
    /// </para>
    /// </summary>
    [Fact]
    public void LeavingLetsGoOfWhatWasBuiltAndKeepsWhatWasDone()
    {
        Battler a = One();
        Battler spare = One();
        Battler b = One();

        a.ChangeStage(Stat.Attack, 2);
        a.Status = StatusCondition.Poison;
        a.IsSeeded = true;

        var battle = new Battle(a, b, 7) { PlayerParty = [a, spare], OpponentParty = [b] };

        battle.ResolveTurn(new BattleAction.SwitchTo(1), new BattleAction.UseMove(0));

        Assert.Equal(0, a.StageOf(Stat.Attack));
        Assert.False(a.IsSeeded);

        // And what was done to it is still done to it.
        Assert.Equal(StatusCondition.Poison, a.Status);
    }

    /// <summary>
    /// A switch to a slot that is not there, or to somebody who cannot fight, does nothing
    /// rather than throwing — all three are things a client could ask for.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void ASwitchToNobodyDoesNothing(int slot)
    {
        Battler a = One();
        Battler spare = One();
        Battler b = One();

        var battle = new Battle(a, b, 7) { PlayerParty = [a, spare], OpponentParty = [b] };

        battle.ResolveTurn(new BattleAction.SwitchTo(slot), new BattleAction.UseMove(0));

        Assert.Same(a, battle.Player);
    }

    // ---- and the rule 167 removed --------------------------------------------------------

    /// <summary>
    /// The move that catches somebody leaving goes before they leave.
    /// <para>
    /// <b>This is the rule milestone 167 wrote and then removed.</b> Breaking it broke
    /// nothing, because a switch was never resolved inside the battle — so by the time an
    /// order was decided there was nobody left to go before. It was taken out rather than
    /// propped up with a test written to fit it, and a paragraph put where somebody would
    /// otherwise put it back.
    /// </para>
    /// <para>
    /// It can be observed now. The catcher is slower than the leaver and still lands first.
    /// </para>
    /// </summary>
    [Fact]
    public void TheMoveThatCatchesALeaverGoesFirst()
    {
        Battler leaving = One(speed: 200);
        Battler spare = One(speed: 200);
        Battler catcher = One(speed: 1, moves: [Move(MovePower.Chasing, 40)]);

        var battle = new Battle(leaving, catcher, 7)
        {
            PlayerParty = [leaving, spare],
            OpponentParty = [catcher],
        };

        battle.ResolveTurn(new BattleAction.SwitchTo(1), new BattleAction.UseMove(0));

        // It landed on the one who was leaving, not on the one who came in.
        Assert.True(leaving.CurrentHp < leaving.MaxHp, "the one leaving got away untouched");
        Assert.Equal(spare.MaxHp, spare.CurrentHp);
    }

    /// <summary>
    /// And against somebody who is not leaving it is an ordinary move at its own speed.
    /// <para>
    /// The half that stops this being a free first strike every turn, and the half a
    /// fixture with only a leaver in it could not tell apart.
    /// </para>
    /// </summary>
    [Fact]
    public void AndAgainstSomebodyStayingItIsAnOrdinaryMove()
    {
        Battler staying = One(speed: 200, moves: [Move(0, 40)]);
        Battler catcher = One(speed: 1, moves: [Move(MovePower.Chasing, 40)]);

        var battle = new Battle(staying, catcher, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        // The faster one went first, which it would not have done if the catcher were still
        // claiming its place against a leaver.
        int first = events.FindIndex(e => e is BattleEvent.MoveUsed { Side: Side.Player });
        int second = events.FindIndex(e => e is BattleEvent.MoveUsed { Side: Side.Opponent });

        Assert.True(first >= 0 && second >= 0, "both sides did not move");
        Assert.True(first < second, "the slower catcher went first against somebody who was staying");
    }

    /// <summary>
    /// A battle given no bench for a side does nothing when that side asks to switch.
    /// <para>
    /// This is what keeps a fight against the game working while a duel switches inside the
    /// turn. The server does the player's switch itself there and then hands the same action
    /// to the engine — and the engine must let it pass, because it has nobody to bring in.
    /// </para>
    /// <para>
    /// It used to be true by accident: nothing anywhere had ever set a bench, so the engine
    /// could not have switched even if it wanted to. Stated here so that wiring a bench in
    /// later cannot silently swap twice.
    /// </para>
    /// </summary>
    [Fact]
    public void ASwitchWithNoBenchDoesNothing()
    {
        Battler a = One();
        Battler b = One(moves: [Move(0, 40)]);

        var battle = new Battle(a, b, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.SwitchTo(1), new BattleAction.UseMove(0));

        Assert.Same(a, battle.Player);
        Assert.DoesNotContain(events, e => e is BattleEvent.CameIn);

        // And the turn still happened to the side that did move.
        Assert.Contains(events.OfType<BattleEvent.MoveUsed>(), e => e.Side == Side.Opponent);
    }

    /// <summary>
    /// Everything the room owns survives a battle being rebuilt around a new creature.
    /// <para>
    /// A duel no longer rebuilds, but a fight against the game still does, so this is still
    /// load-bearing there. The failure it guards against is not forgetting the call — it is
    /// carrying <em>some</em> of the fields, which looks exactly like carrying all of them.
    /// The weather was carried and the room's damping was not.
    /// </para>
    /// </summary>
    [Fact]
    public void EverythingTheRoomOwnsSurvivesARebuild()
    {
        Battler a = One();
        Battler b = One();

        var before = new Battle(a, b, 7);

        before.SetSkyForTest(Weather.Sandstorm, 4);
        before.SetDampingForTest(PokemonType.Electric, 3);

        var after = new Battle(One(), b, before.State);

        after.ContinueFrom(before);

        Assert.Equal(Weather.Sandstorm, after.Sky);
        Assert.Equal(4, after.SkyTurns);
        Assert.Equal(PokemonType.Electric, after.Damped);
        Assert.Equal(3, after.DampedTurns);
    }
}
