using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract.Tests;

public class CatchCalculatorTests
{
    private static Battler Target(int hp = 60, int catchRate = 45) =>
        new(TestMons.Species("TARGET", PokemonType.Normal, hp: hp, catchRate: catchRate), level: 10);

    [Fact]
    public void AMasterBallAlwaysCatches()
    {
        // Whatever the seed, whatever the target. It is the one throw with no roll.
        for (uint seed = 0; seed < 50; seed++)
        {
            CatchAttempt attempt = CatchCalculator.Throw(
                new BattleRng(seed), Target(catchRate: 3), catchRate: 3, BallKind.Master);

            Assert.True(attempt.Caught);
            Assert.Equal(CatchAttempt.ShakesToCatch, attempt.Shakes);
        }
    }

    [Fact]
    public void DamageRaisesTheCatchValue()
    {
        Battler healthy = Target();
        Battler hurt = Target();
        hurt.TakeDamage(hurt.MaxHp - 1);

        Assert.True(
            CatchCalculator.CatchValue(hurt, 45, BallKind.Poke) >
            CatchCalculator.CatchValue(healthy, 45, BallKind.Poke));
    }

    [Fact]
    public void StatusRaisesTheCatchValue()
    {
        Battler clear = Target();

        Battler asleep = Target();
        asleep.TryApplyStatus(StatusCondition.Sleep, sleepTurns: 3);

        Battler paralysed = Target();
        paralysed.TryApplyStatus(StatusCondition.Paralysis);

        int clearValue = CatchCalculator.CatchValue(clear, 200, BallKind.Poke);

        Assert.True(CatchCalculator.CatchValue(paralysed, 200, BallKind.Poke) > clearValue);
        Assert.True(
            CatchCalculator.CatchValue(asleep, 200, BallKind.Poke) >
            CatchCalculator.CatchValue(paralysed, 200, BallKind.Poke));
    }

    [Fact]
    public void ABetterBallRaisesTheCatchValue()
    {
        Battler target = Target();

        int poke = CatchCalculator.CatchValue(target, 60, BallKind.Poke);
        int great = CatchCalculator.CatchValue(target, 60, BallKind.Great);
        int ultra = CatchCalculator.CatchValue(target, 60, BallKind.Ultra);

        Assert.True(great > poke);
        Assert.True(ultra > great);
    }

    [Fact]
    public void AFullCatchValueIsAGuarantee()
    {
        // A catch value at the ceiling skips the shakes entirely, which is what makes
        // a weakened, sleeping, high-catch-rate target a sure thing.
        Battler target = Target(catchRate: 255);
        target.TakeDamage(target.MaxHp - 1);
        target.TryApplyStatus(StatusCondition.Sleep, sleepTurns: 3);

        Assert.Equal(255, CatchCalculator.CatchValue(target, 255, BallKind.Ultra));
        Assert.True(CatchCalculator.Throw(new BattleRng(1), target, 255, BallKind.Ultra).Caught);
    }

    [Fact]
    public void ShakesNeverLeaveTheirRange()
    {
        for (uint seed = 0; seed < 200; seed++)
        {
            CatchAttempt attempt = CatchCalculator.Throw(new BattleRng(seed), Target(), 45, BallKind.Poke);

            Assert.InRange(attempt.Shakes, 0, CatchAttempt.ShakesToCatch);
            Assert.Equal(attempt.Shakes == CatchAttempt.ShakesToCatch, attempt.Caught);
        }
    }

    [Fact]
    public void TheSameSeedThrowsTheSameWay()
    {
        CatchAttempt first = CatchCalculator.Throw(new BattleRng(0xC0FFEE), Target(), 45, BallKind.Poke);
        CatchAttempt second = CatchCalculator.Throw(new BattleRng(0xC0FFEE), Target(), 45, BallKind.Poke);

        Assert.Equal(first, second);
    }

    [Fact]
    public void AHarderTargetIsCaughtLessOften()
    {
        int easy = CaughtOutOf(200, catchRate: 200);
        int hard = CaughtOutOf(200, catchRate: 3);

        Assert.True(easy > hard, $"easy {easy}, hard {hard}");
    }

    private static int CaughtOutOf(int throws, int catchRate)
    {
        int caught = 0;

        for (uint seed = 0; seed < throws; seed++)
        {
            if (CatchCalculator.Throw(new BattleRng(seed), Target(catchRate: catchRate), catchRate, BallKind.Poke).Caught)
                caught++;
        }

        return caught;
    }
}

public class CatchingInBattleTests
{
    private static Battler Mon(string name, int catchRate = 45, int speed = 50) =>
        new Battler(TestMons.Species(name, PokemonType.Normal, speed: speed, catchRate: catchRate), level: 10)
            .Knowing(TestMons.Tackle);

    [Fact]
    public void CatchingEndsTheBattleWithTheThrowerWinning()
    {
        var battle = new Battle(Mon("PLAYER", speed: 90), Mon("WILD", catchRate: 255, speed: 10), seed: 5);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.ThrowBall(BallKind.Master),
            new BattleAction.UseMove(0));

        Assert.True(battle.OpponentCaught);
        Assert.True(battle.IsOver);
        Assert.Equal(Side.Player, battle.Winner);
        Assert.Contains(events, e => e is BattleEvent.BallThrown { Caught: true });
    }

    [Fact]
    public void ASuccessfulThrowStopsTheOpponentActing()
    {
        var player = Mon("PLAYER", speed: 90);
        var battle = new Battle(player, Mon("WILD", catchRate: 255, speed: 10), seed: 5);

        battle.ResolveTurn(new BattleAction.ThrowBall(BallKind.Master), new BattleAction.UseMove(0));

        Assert.Equal(player.MaxHp, player.CurrentHp);
    }

    [Fact]
    public void AFailedThrowStillSpendsTheTurn()
    {
        var player = Mon("PLAYER", speed: 90);

        // Catch rate three, at full health: the throw all but cannot work, and the
        // wild one gets its swing in regardless.
        var battle = new Battle(player, Mon("WILD", catchRate: 3, speed: 10), seed: 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.ThrowBall(BallKind.Poke),
            new BattleAction.UseMove(0));

        Assert.False(battle.OpponentCaught);
        Assert.Contains(events, e => e is BattleEvent.BallThrown { Caught: false });
        Assert.Contains(events, e => e is BattleEvent.MoveUsed { Attacker: "WILD" });
    }

    [Fact]
    public void AWildOpponentThrowingABallCatchesNothing()
    {
        // Only the player can catch. The action existing on both sides is an accident
        // of the shared type, and it must not hand the opponent a win condition.
        var battle = new Battle(Mon("PLAYER", speed: 10), Mon("WILD", speed: 90), seed: 3);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.ThrowBall(BallKind.Master));

        Assert.False(battle.OpponentCaught);
    }

    [Fact]
    public void TheNarratorReadsTheShakeCount()
    {
        Assert.Equal("Oh no! It broke free!", BattleNarrator.Describe(new BattleEvent.BallThrown("WILD", 0, false)));
        Assert.Equal("Aargh! Almost had it!", BattleNarrator.Describe(new BattleEvent.BallThrown("WILD", 2, false)));
        Assert.Equal("Gotcha! WILD was caught!", BattleNarrator.Describe(new BattleEvent.BallThrown("WILD", 4, true)));
    }
}

public class PartyTests
{
    private static Battler Mon(string name = "MON") =>
        new(TestMons.Species(name, PokemonType.Normal), level: 10);

    [Fact]
    public void HoldsSixAndNoMore()
    {
        var party = new Party();

        for (int i = 0; i < Party.MaxSize; i++)
            Assert.True(party.TryAdd(Mon($"MON{i}")));

        Assert.True(party.IsFull);
        Assert.False(party.TryAdd(Mon("SEVENTH")));
        Assert.Equal(Party.MaxSize, party.Count);
    }

    [Fact]
    public void LeadsWithTheFirstMemberStillStanding()
    {
        var party = new Party();

        Battler fainted = Mon("FAINTED");
        fainted.TakeDamage(fainted.MaxHp);

        Battler healthy = Mon("HEALTHY");

        party.TryAdd(fainted);
        party.TryAdd(healthy);

        Assert.Equal(healthy, party.Lead);
    }

    [Fact]
    public void StillLeadsWithSomethingWhenEverythingHasFainted()
    {
        // A wiped party still has to name a battler for the screen to draw, even
        // though it cannot fight.
        var party = new Party();

        Battler only = Mon();
        only.TakeDamage(only.MaxHp);
        party.TryAdd(only);

        Assert.False(party.HasHealthyMember);
        Assert.Equal(only, party.Lead);
    }

    [Fact]
    public void AnEmptyPartyLeadsWithNothing()
    {
        var party = new Party();

        Assert.True(party.IsEmpty);
        Assert.Null(party.Lead);
        Assert.False(party.HasHealthyMember);
    }

    [Fact]
    public void HealingRestoresHealthAndClearsStatus()
    {
        var party = new Party();

        Battler hurt = Mon();
        hurt.TakeDamage(hurt.MaxHp - 1);
        hurt.TryApplyStatus(StatusCondition.Sleep, sleepTurns: 3);
        hurt.ChangeStage(Stat.Attack, -2);

        party.TryAdd(hurt);
        party.HealAll();

        Assert.Equal(hurt.MaxHp, hurt.CurrentHp);
        Assert.Equal(StatusCondition.None, hurt.Status);
        Assert.Equal(0, hurt.SleepTurns);
        Assert.Equal(0, hurt.StageOf(Stat.Attack));
    }

    [Fact]
    public void ReturnsNothingForAnIndexItDoesNotHave()
    {
        var party = new Party();
        party.TryAdd(Mon());

        Assert.NotNull(party.At(0));
        Assert.Null(party.At(1));
        Assert.Null(party.At(-1));
    }
}
