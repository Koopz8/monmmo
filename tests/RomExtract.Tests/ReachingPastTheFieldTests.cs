using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The last three, and the only ones that reach past the two creatures standing there.
/// <para>
/// A battle has never held a party and should not start. A party belongs to whoever owns the
/// save; switching is done by the server, outside this class; and every rule in here has only
/// ever been handed the two creatures on the field. So the party arrives the same way the
/// move table and the ground do — from outside — and the leaving itself stays where it
/// already is.
/// </para>
/// <para>
/// That split is what the three tests below are really about. What a battle can decide, it
/// decides; what it cannot, it hands back with enough in it for somebody else to finish.
/// </para>
/// </summary>
public class ReachingPastTheFieldTests
{
    private const byte HealBell = 0x66;
    private const byte BatonPass = 0x7F;
    private const byte Pursuit = 0x80;

    private static SpeciesData Species(int speed = 60) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 250, BaseAttack = 90, BaseDefense = 90,
        BaseSpeed = (byte)speed, BaseSpAttack = 90, BaseSpDefense = 90,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    private static MoveData Move(byte effect, byte power = 0, int id = 1) =>
        new(id, string.Empty, effect, power, PokemonType.Normal, 100, 20, 100, 0, 0);

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var battler = new Battler(Species(speed), 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Theory]
    [InlineData(HealBell)] [InlineData(BatonPass)] [InlineData(Pursuit)]
    public void NoneOfThemIsSilent(int effect) =>
        Assert.NotEqual(EffectKind.None, MoveEffects.Of((byte)effect).Kind);

    // ---- the bell ---------------------------------------------------------------------

    /// <summary>
    /// It reaches everybody on its side, including the ones not standing there — which is
    /// the only reason it is worth a turn.
    /// </summary>
    [Fact]
    public void TheBellReachesEverybodyOnItsSide()
    {
        Battler you = Make(200, Move(HealBell));
        Battler them = Make(10, Move(0x00, 0));

        Battler resting = Make(60, Move(0x00, 0));
        Battler alsoResting = Make(60, Move(0x00, 0));

        you.Status = StatusCondition.Burn;
        resting.Status = StatusCondition.Poison;
        alsoResting.Status = StatusCondition.Sleep;
        alsoResting.SleepTurns = 3;

        var battle = new Battle(you, them, 7) { PlayerParty = [you, resting, alsoResting] };

        List<BattleEvent> events = Turn(battle);

        Assert.Equal(StatusCondition.None, you.Status);
        Assert.Equal(StatusCondition.None, resting.Status);
        Assert.Equal(StatusCondition.None, alsoResting.Status);

        // And the count travels, because a bell that reached three is a different thing from
        // a bell that reached the one standing there.
        Assert.Equal(3, events.OfType<BattleEvent.RangClear>().Single().Cleared);
    }

    /// <summary>And it clears the turns owed by somebody asleep, not only the label.</summary>
    [Fact]
    public void AndItClearsTheTurnsOwedNotOnlyTheLabel()
    {
        Battler you = Make(200, Move(HealBell));
        Battler them = Make(10, Move(0x00, 0));

        // The sleeper is off the field, not the one ringing. A creature asleep cannot ring
        // anything — which is right, and is how the first version of this test failed: it
        // put the sleep on the user and then measured the user still being asleep.
        Battler resting = Make(60, Move(0x00, 0));

        resting.Status = StatusCondition.Sleep;
        resting.SleepTurns = 4;

        you.Status = StatusCondition.Burn;

        Turn(new Battle(you, them, 7) { PlayerParty = [you, resting] });

        Assert.Equal(StatusCondition.None, resting.Status);
        Assert.Equal(0, resting.SleepTurns);
    }

    /// <summary>It leaves the other side entirely alone.</summary>
    [Fact]
    public void AndItLeavesTheOtherSideAlone()
    {
        Battler you = Make(200, Move(HealBell));
        Battler them = Make(10, Move(0x00, 0));

        Battler theirs = Make(60, Move(0x00, 0));

        you.Status = StatusCondition.Burn;
        them.Status = StatusCondition.Poison;
        theirs.Status = StatusCondition.Poison;

        var battle = new Battle(you, them, 7)
        {
            PlayerParty = [you],
            OpponentParty = [them, theirs],
        };

        Turn(battle);

        Assert.Equal(StatusCondition.Poison, them.Status);
        Assert.Equal(StatusCondition.Poison, theirs.Status);
    }

    /// <summary>
    /// A battle told about no party heals only whoever is standing there — which is the
    /// right answer for a wild fight and a truthful one everywhere else.
    /// </summary>
    [Fact]
    public void AndABattleToldAboutNoPartyHealsWhoIsStandingThere()
    {
        Battler you = Make(200, Move(HealBell));
        Battler them = Make(10, Move(0x00, 0));

        you.Status = StatusCondition.Burn;

        Assert.Equal(1, Turn(new Battle(you, them, 7)).OfType<BattleEvent.RangClear>().Single().Cleared);
    }

    /// <summary>And with nobody ailing it says so rather than ringing at nothing.</summary>
    [Fact]
    public void AndWithNobodyAilingItSaysSo()
    {
        Battler you = Make(200, Move(HealBell));
        Battler them = Make(10, Move(0x00, 0));

        Assert.Contains(
            Turn(new Battle(you, them, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    // ---- passing it on ---------------------------------------------------------------------

    /// <summary>
    /// What it built is kept for whoever comes in, and handed over when somebody does.
    /// </summary>
    [Fact]
    public void WhatItBuiltIsKeptForWhoeverComesIn()
    {
        Battler you = Make(200, Move(BatonPass));
        Battler them = Make(10, Move(0x00, 0));

        you.ChangeStage(Stat.Attack, 3);
        you.ChangeStage(Stat.Speed, -1);

        var battle = new Battle(you, them, 7);

        Assert.Contains(Turn(battle), e => e is BattleEvent.PassedItOn { Side: Side.Player });

        Assert.NotNull(battle.WaitingToBePassed(Side.Player));

        Battler next = Make(60, Move(0x00, 0));

        battle.GiveWhatWasPassed(Side.Player, next);

        Assert.Equal(3, next.StageOf(Stat.Attack));
        Assert.Equal(-1, next.StageOf(Stat.Speed));

        // And it is handed over once. A second creature does not inherit it again.
        Assert.Null(battle.WaitingToBePassed(Side.Player));
    }

    /// <summary>
    /// The bad half goes too. A move that passed on only the good would be a move with no
    /// cost at all, and the seed following the creature that was seeded is the point.
    /// </summary>
    [Fact]
    public void AndWhatWasStartedOnItGoesTooRatherThanOnlyTheGood()
    {
        Battler you = Make(200, Move(BatonPass));
        Battler them = Make(10, Move(0x00, 0));

        you.IsSeeded = true;
        you.PerishTurns = 2;

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Battler next = Make(60, Move(0x00, 0));

        battle.GiveWhatWasPassed(Side.Player, next);

        Assert.True(next.IsSeeded);
        Assert.Equal(2, next.PerishTurns);
    }

    /// <summary>
    /// And what was done to the creature itself stays with it. A condition is not something
    /// its owner built, and handing one on would be handing on a problem rather than an
    /// advantage.
    /// </summary>
    [Fact]
    public void AndWhatWasDoneToItStaysWithIt()
    {
        Battler you = Make(200, Move(BatonPass));
        Battler them = Make(10, Move(0x00, 0));

        you.Status = StatusCondition.Poison;
        you.ChangeStage(Stat.Attack, 2);

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Battler next = Make(60, Move(0x00, 0));

        battle.GiveWhatWasPassed(Side.Player, next);

        Assert.Equal(StatusCondition.None, next.Status);
        Assert.Equal(2, next.StageOf(Stat.Attack));
    }

    /// <summary>And a side nobody passed anything on gets nothing.</summary>
    [Fact]
    public void AndASideNobodyPassedAnythingGetsNothing()
    {
        Battler you = Make(200, Move(0x00, 0));
        Battler them = Make(10, Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        Battler next = Make(60, Move(0x00, 0));

        next.ChangeStage(Stat.Attack, 1);

        battle.GiveWhatWasPassed(Side.Player, next);

        Assert.Equal(1, next.StageOf(Stat.Attack));
        Assert.Null(battle.WaitingToBePassed(Side.Player));
    }

    // ---- catching them leaving -------------------------------------------------------------

    /// <summary>
    /// It is twice as hard against somebody on their way out, and its ordinary power against
    /// anybody else.
    /// </summary>
    [Fact]
    public void ItIsTwiceAsHardAgainstSomebodyLeaving()
    {
        Battler you = Make(60, Move(Pursuit, 40));
        Battler them = Make(60, Move(0x00, 0));

        MoveData chasing = you.MoveAt(0)!;

        Assert.Null(MovePower.Of(chasing, you, them, Weather.None, leaving: false));
        Assert.Equal(80, MovePower.Of(chasing, you, them, Weather.None, leaving: true));
    }

    /// <summary>
    /// It takes its record's own place in the order, and there is no special case making it
    /// first against a leaver.
    /// <para>
    /// That absence is deliberate and was arrived at by trying the opposite. A rule giving
    /// it first place against somebody switching out was written, and then breaking that
    /// rule broke no test — because a switch is not resolved inside the battle at all. The
    /// server does both switches before it calls in, precisely because a switch is not a
    /// turn, so by the time an order is decided there is nobody left to go before.
    /// </para>
    /// <para>
    /// So the rule was removed rather than propped up with a test written to fit it. What
    /// this move needs to work is only that the battle be told somebody is leaving, which it
    /// can be. Making the order matter too means moving the switch inside the turn, which is
    /// a change to how a duel is run and is written down as not yet done.
    /// </para>
    /// </summary>
    [Fact]
    public void ItTakesItsRecordsOwnPlaceInTheOrder()
    {
        Battler you = Make(5, Move(Pursuit, 40));
        Battler them = Make(200, Move(0x00, 10, id: 2));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Equal(Side.Opponent, events.OfType<BattleEvent.MoveUsed>().First().Side);
    }

    /// <summary>And the doubling reaches the damage rather than stopping at the power.</summary>
    [Fact]
    public void AndTheDoublingReachesTheDamage()
    {
        Battler you = Make(5, Move(Pursuit, 40));
        Battler them = Make(200, Move(0x00, 0));

        Battler alsoYou = Make(5, Move(Pursuit, 40));
        Battler alsoThem = Make(200, Move(0x00, 0));

        var leaving = new Battle(you, them, 7);
        var staying = new Battle(alsoYou, alsoThem, 7);

        leaving.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.SwitchTo(1));
        Turn(staying);

        Assert.True(
            them.MaxHp - them.CurrentHp > alsoThem.MaxHp - alsoThem.CurrentHp,
            "chasing somebody who was leaving did no more than chasing somebody who stayed");
    }
}
