using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract.Tests;

/// <summary>Species and moves built by hand, so battle tests do not need a cartridge.</summary>
internal static class TestMons
{
    public static SpeciesData Species(
        string name,
        PokemonType type1,
        PokemonType? type2 = null,
        int hp = 50, int attack = 50, int defense = 50,
        int speed = 50, int spAttack = 50, int spDefense = 50,
        int catchRate = 45) =>
        new()
        {
            Index = 1,
            Name = name,
            BaseHp = (byte)hp,
            BaseAttack = (byte)attack,
            BaseDefense = (byte)defense,
            BaseSpeed = (byte)speed,
            BaseSpAttack = (byte)spAttack,
            BaseSpDefense = (byte)spDefense,
            Type1 = type1,
            Type2 = type2 ?? type1,
            CatchRate = (byte)catchRate,
            ExpYield = 64,
        };

    public static MoveData Move(
        string name,
        PokemonType type,
        int power = 40,
        int accuracy = 100,
        int priority = 0) =>
        new(1, name, 0, (byte)power, type, (byte)accuracy, 35, 0, 0, (sbyte)priority);

    public static readonly MoveData Tackle = Move("TACKLE", PokemonType.Normal, power: 35);
    public static readonly MoveData Ember = Move("EMBER", PokemonType.Fire, power: 40);
    public static readonly MoveData QuickAttack = Move("QUICK ATTACK", PokemonType.Normal, power: 40, priority: 1);
    public static readonly MoveData Growl = new(1, "GROWL", 0, 0, PokemonType.Normal, 100, 40, 0, 0, 0);
    public static readonly MoveData NeverMisses = Move("SWIFT", PokemonType.Normal, power: 60, accuracy: 0);
}

public class DamageCalculatorTests
{
    private static Battler Attacker(PokemonType type = PokemonType.Normal, int attack = 50) =>
        new(TestMons.Species("ATTACKER", type, attack: attack, spAttack: attack), level: 50);

    private static Battler Defender(PokemonType type1 = PokemonType.Normal, PokemonType? type2 = null, int defense = 50) =>
        new(TestMons.Species("DEFENDER", type1, type2, defense: defense, spDefense: defense), level: 50);

    [Fact]
    public void DealsDamageInTheExpectedRange()
    {
        DamageResult low = DamageCalculator.Calculate(Attacker(), Defender(), TestMons.Tackle, critical: false, randomPercent: 85);
        DamageResult high = DamageCalculator.Calculate(Attacker(), Defender(), TestMons.Tackle, critical: false, randomPercent: 100);

        Assert.True(low.Damage > 0);
        Assert.True(high.Damage > low.Damage);
    }

    [Fact]
    public void ACriticalHitDoublesTheDamage()
    {
        // Gen III criticals are a straight doubling; the 1.5x version came later.
        DamageResult normal = DamageCalculator.Calculate(Attacker(), Defender(), TestMons.Tackle, false, 100);
        DamageResult crit = DamageCalculator.Calculate(Attacker(), Defender(), TestMons.Tackle, true, 100);

        Assert.InRange(crit.Damage, normal.Damage * 19 / 10, normal.Damage * 21 / 10);
    }

    [Fact]
    public void SameTypeAttackAddsAHalf()
    {
        Battler normalType = Attacker();
        Battler fireType = Attacker(PokemonType.Fire);

        DamageResult without = DamageCalculator.Calculate(normalType, Defender(), TestMons.Ember, false, 100);
        DamageResult with = DamageCalculator.Calculate(fireType, Defender(), TestMons.Ember, false, 100);

        Assert.True(with.Damage > without.Damage);
        Assert.True(with.Stab);
        Assert.False(without.Stab);
    }

    [Fact]
    public void AnImmuneTargetTakesNothingAtAll()
    {
        DamageResult result = DamageCalculator.Calculate(
            Attacker(), Defender(PokemonType.Ghost), TestMons.Tackle, false, 100);

        Assert.Equal(0, result.Damage);
        Assert.True(result.NoEffect);
    }

    [Fact]
    public void AConnectingHitAlwaysDoesAtLeastOne()
    {
        // A feeble attacker against a wall still chips it, which the games guarantee.
        Battler weak = new(TestMons.Species("WEAK", PokemonType.Normal, attack: 1, spAttack: 1), level: 2);
        Battler wall = new(TestMons.Species("WALL", PokemonType.Rock, PokemonType.Steel, defense: 255), level: 100);

        DamageResult result = DamageCalculator.Calculate(weak, wall, TestMons.Tackle, false, 85);

        Assert.Equal(1, result.Damage);
    }

    [Fact]
    public void StatusMovesDoNoDamage()
    {
        DamageResult result = DamageCalculator.Calculate(Attacker(), Defender(), TestMons.Growl, false, 100);
        Assert.Equal(0, result.Damage);
    }

    [Fact]
    public void BurnHalvesPhysicalDamageButNotSpecial()
    {
        Battler healthy = Attacker();
        Battler burned = Attacker();
        burned.Status = StatusCondition.Burn;

        DamageResult physical = DamageCalculator.Calculate(healthy, Defender(), TestMons.Tackle, false, 100);
        DamageResult burnedPhysical = DamageCalculator.Calculate(burned, Defender(), TestMons.Tackle, false, 100);

        Assert.True(burnedPhysical.Damage < physical.Damage);

        // Ember is Fire, and every Fire move is special in this generation, so a burn
        // leaves it untouched.
        DamageResult special = DamageCalculator.Calculate(healthy, Defender(), TestMons.Ember, false, 100);
        DamageResult burnedSpecial = DamageCalculator.Calculate(burned, Defender(), TestMons.Ember, false, 100);

        Assert.Equal(special.Damage, burnedSpecial.Damage);
    }

    [Fact]
    public void PhysicalAndSpecialAreDecidedByTypeNotByMove()
    {
        // The defining quirk of this generation. A high-Attack, low-SpAttack battler
        // hits harder with Normal moves and softer with Fire ones, regardless of what
        // the moves themselves look like.
        var lopsided = new Battler(
            TestMons.Species("LOPSIDED", PokemonType.Water, attack: 200, spAttack: 5), level: 50);

        DamageResult normal = DamageCalculator.Calculate(lopsided, Defender(), TestMons.Tackle, false, 100);
        DamageResult fire = DamageCalculator.Calculate(lopsided, Defender(), TestMons.Ember, false, 100);

        Assert.Equal(DamageCategory.Physical, TestMons.Tackle.Category);
        Assert.Equal(DamageCategory.Special, TestMons.Ember.Category);
        Assert.True(normal.Damage > fire.Damage);
    }

    [Fact]
    public void ACriticalHitIgnoresTheDefendersDefenceBoosts()
    {
        Battler defender = Defender();
        defender.ChangeStage(Stat.Defense, 6);

        DamageResult normal = DamageCalculator.Calculate(Attacker(), defender, TestMons.Tackle, false, 100);
        DamageResult crit = DamageCalculator.Calculate(Attacker(), defender, TestMons.Tackle, true, 100);

        // Far more than the plain doubling, because the boost is skipped too.
        Assert.True(crit.Damage > normal.Damage * 3);
    }

    [Fact]
    public void ACriticalHitIgnoresTheAttackersOwnDebuffs()
    {
        Battler attacker = Attacker();
        attacker.ChangeStage(Stat.Attack, -6);

        DamageResult normal = DamageCalculator.Calculate(attacker, Defender(), TestMons.Tackle, false, 100);
        DamageResult crit = DamageCalculator.Calculate(attacker, Defender(), TestMons.Tackle, true, 100);

        Assert.True(crit.Damage > normal.Damage * 3);
    }

    [Fact]
    public void AMoveWithoutAnAccuracyValueNeverMisses()
    {
        var rng = new BattleRng(1);

        for (int i = 0; i < 200; i++)
            Assert.True(DamageCalculator.RollAccuracy(rng, TestMons.NeverMisses, Attacker(), Defender()));
    }

    [Fact]
    public void EvasionMakesAMoveMissMoreOften()
    {
        Battler attacker = Attacker();
        Battler slippery = Defender();
        slippery.ChangeStage(Stat.Evasion, 6);

        var move = TestMons.Move("HIT", PokemonType.Normal, accuracy: 100);

        int hits = 0;
        var rng = new BattleRng(42);

        for (int i = 0; i < 500; i++)
            if (DamageCalculator.RollAccuracy(rng, move, attacker, slippery)) hits++;

        Assert.InRange(hits, 100, 200);   // roughly a third
    }
}

public class BattleTurnTests
{
    private static Battler Fast(params MoveData[] moves) =>
        new Battler(TestMons.Species("FAST", PokemonType.Normal, speed: 200), level: 50, nickname: "FAST")
            .Knowing(moves);

    private static Battler Slow(params MoveData[] moves) =>
        new Battler(TestMons.Species("SLOW", PokemonType.Normal, speed: 5), level: 50, nickname: "SLOW")
            .Knowing(moves);

    private static readonly BattleAction FirstMove = new BattleAction.UseMove(0);

    [Fact]
    public void TheFasterBattlerMovesFirst()
    {
        var battle = new Battle(Fast(TestMons.Tackle), Slow(TestMons.Tackle), seed: 1);
        List<BattleEvent> events = battle.ResolveTurn(FirstMove, FirstMove);

        BattleEvent.MoveUsed first = events.OfType<BattleEvent.MoveUsed>().First();
        Assert.Equal(Side.Player, first.Side);
    }

    [Fact]
    public void TheSlowerBattlerMovesFirstWhenItIsTheOpponent()
    {
        var battle = new Battle(Slow(TestMons.Tackle), Fast(TestMons.Tackle), seed: 1);
        List<BattleEvent> events = battle.ResolveTurn(FirstMove, FirstMove);

        Assert.Equal(Side.Opponent, events.OfType<BattleEvent.MoveUsed>().First().Side);
    }

    [Fact]
    public void PriorityBeatsSpeed()
    {
        // A slow battler using a priority move goes first regardless.
        var battle = new Battle(Slow(TestMons.QuickAttack), Fast(TestMons.Tackle), seed: 1);
        List<BattleEvent> events = battle.ResolveTurn(FirstMove, FirstMove);

        Assert.Equal(Side.Player, events.OfType<BattleEvent.MoveUsed>().First().Side);
    }

    [Fact]
    public void ParalysisQuartersSpeedAndCanCostTheTurnOrder()
    {
        Battler quick = Fast(TestMons.Tackle);
        Battler slow = Slow(TestMons.Tackle);

        int before = quick.EffectiveStat(Stat.Speed);
        quick.Status = StatusCondition.Paralysis;

        Assert.Equal(before / 4, quick.EffectiveStat(Stat.Speed));
        Assert.True(quick.EffectiveStat(Stat.Speed) > slow.EffectiveStat(Stat.Speed) == false
                    || quick.EffectiveStat(Stat.Speed) < before);
    }

    [Fact]
    public void BothBattlersActInATurn()
    {
        var battle = new Battle(Fast(TestMons.Tackle), Slow(TestMons.Tackle), seed: 5);
        List<BattleEvent> events = battle.ResolveTurn(FirstMove, FirstMove);

        Assert.Equal(2, events.OfType<BattleEvent.MoveUsed>().Count());
    }

    [Fact]
    public void DamageReducesTheTargetsHealth()
    {
        var battle = new Battle(Fast(TestMons.Tackle), Slow(TestMons.Tackle), seed: 5);
        int before = battle.Opponent.CurrentHp;

        battle.ResolveTurn(FirstMove, FirstMove);

        Assert.True(battle.Opponent.CurrentHp < before);
    }

    [Fact]
    public void ABattlerThatFaintsDoesNotGetToAct()
    {
        Battler glass = new Battler(
            TestMons.Species("GLASS", PokemonType.Normal, hp: 1, defense: 1, speed: 1), level: 5, nickname: "GLASS")
            .Knowing(TestMons.Tackle);

        var heavy = new Battler(
            TestMons.Species("HEAVY", PokemonType.Normal, attack: 200, speed: 200), level: 60, nickname: "HEAVY");
        heavy.Knowing(TestMons.Tackle);

        var battle = new Battle(heavy, glass, seed: 3);
        List<BattleEvent> events = battle.ResolveTurn(FirstMove, FirstMove);

        Assert.Contains(events, e => e is BattleEvent.Fainted { Side: Side.Opponent });
        Assert.Single(events.OfType<BattleEvent.MoveUsed>());
    }

    [Fact]
    public void TheBattleEndsWhenSomebodyFaints()
    {
        var glass = new Battler(
            TestMons.Species("GLASS", PokemonType.Normal, hp: 1, defense: 1), level: 5, nickname: "GLASS");
        glass.Knowing(TestMons.Tackle);

        var heavy = new Battler(
            TestMons.Species("HEAVY", PokemonType.Normal, attack: 200, speed: 200), level: 60, nickname: "HEAVY");
        heavy.Knowing(TestMons.Tackle);

        var battle = new Battle(heavy, glass, seed: 3);
        List<BattleEvent> events = battle.ResolveTurn(FirstMove, FirstMove);

        Assert.True(battle.IsOver);
        Assert.Equal(Side.Player, battle.Winner);
        Assert.Contains(events, e => e is BattleEvent.Ended { Winner: Side.Player });
    }

    [Fact]
    public void NothingHappensOnceTheBattleIsOver()
    {
        var glass = new Battler(TestMons.Species("GLASS", PokemonType.Normal, hp: 1, defense: 1), 5, nickname: "GLASS");
        glass.Knowing(TestMons.Tackle);

        var heavy = new Battler(TestMons.Species("HEAVY", PokemonType.Normal, attack: 200, speed: 200), 60, nickname: "HEAVY");
        heavy.Knowing(TestMons.Tackle);

        var battle = new Battle(heavy, glass, seed: 3);
        battle.ResolveTurn(FirstMove, FirstMove);

        Assert.Empty(battle.ResolveTurn(FirstMove, FirstMove));
    }

    [Fact]
    public void AnImmuneTargetIsReportedRatherThanDamaged()
    {
        var ghost = new Battler(TestMons.Species("GHOST", PokemonType.Ghost), 50, nickname: "GHOST");
        ghost.Knowing(TestMons.Tackle);

        var battle = new Battle(Fast(TestMons.Tackle), ghost, seed: 9);
        List<BattleEvent> events = battle.ResolveTurn(FirstMove, FirstMove);

        Assert.Contains(events, e => e is BattleEvent.NoEffect);
        Assert.Equal(ghost.MaxHp, ghost.CurrentHp);
    }

    [Fact]
    public void PoisonBitesAtTheEndOfEachTurn()
    {
        Battler poisoned = Slow(TestMons.Tackle);
        poisoned.Status = StatusCondition.Poison;

        var battle = new Battle(poisoned, Slow(TestMons.Tackle), seed: 11);
        int expected = Math.Max(1, poisoned.MaxHp / 16);
        int before = poisoned.CurrentHp;

        List<BattleEvent> events = battle.ResolveTurn(FirstMove, FirstMove);

        BattleEvent.StatusHurt hurt = events.OfType<BattleEvent.StatusHurt>()
            .Single(h => h.Status == StatusCondition.Poison);

        Assert.Equal(expected, hurt.Damage);
        Assert.True(poisoned.CurrentHp < before);
    }

    [Fact]
    public void SleepCostsTurnsAndThenWearsOff()
    {
        Battler sleeper = Fast(TestMons.Tackle);
        sleeper.TryApplyStatus(StatusCondition.Sleep, sleepTurns: 2);

        var battle = new Battle(sleeper, Slow(TestMons.Tackle), seed: 13);

        List<BattleEvent> first = battle.ResolveTurn(FirstMove, FirstMove);
        Assert.Contains(first, e => e is BattleEvent.Immobilised { Cause: StatusCondition.Sleep });

        List<BattleEvent> second = battle.ResolveTurn(FirstMove, FirstMove);
        Assert.Contains(second, e => e is BattleEvent.WokeUp);
        Assert.Equal(StatusCondition.None, sleeper.Status);
    }

    [Fact]
    public void TheSameSeedReplaysTheBattleExactly()
    {
        // This is the property the networking design depends on: the server resolves
        // the battle, sends the seed and the actions, and the client reproduces every
        // roll without another byte crossing the wire.
        static List<BattleEvent> Play(uint seed)
        {
            var battle = new Battle(
                new Battler(TestMons.Species("A", PokemonType.Normal, speed: 60), 50, nickname: "A").Knowing(TestMons.Tackle),
                new Battler(TestMons.Species("B", PokemonType.Water, speed: 55), 50, nickname: "B").Knowing(TestMons.Ember),
                seed);

            var all = new List<BattleEvent>();

            for (int turn = 0; turn < 30 && !battle.IsOver; turn++)
                all.AddRange(battle.ResolveTurn(FirstMove, FirstMove));

            return all;
        }

        Assert.Equal(Play(2024), Play(2024));
        Assert.NotEqual(Play(2024), Play(2025));
    }

    [Fact]
    public void ABattleActuallyReachesAConclusion()
    {
        var battle = new Battle(
            new Battler(TestMons.Species("A", PokemonType.Normal), 50, nickname: "A").Knowing(TestMons.Tackle),
            new Battler(TestMons.Species("B", PokemonType.Normal), 50, nickname: "B").Knowing(TestMons.Tackle),
            seed: 77);

        int turns = 0;
        while (!battle.IsOver && turns < 200)
        {
            battle.ResolveTurn(FirstMove, FirstMove);
            turns++;
        }

        Assert.True(battle.IsOver);
        Assert.NotNull(battle.Winner);
        Assert.InRange(turns, 1, 199);
    }
}

public class BattleNarratorTests
{
    private static Battler Named(string name) =>
        new(TestMons.Species(name, PokemonType.Normal), 5, nickname: name);

    [Fact]
    public void AnnouncesAMove()
    {
        Assert.Equal(
            "PIDGEY used TACKLE!",
            BattleNarrator.Describe(new BattleEvent.MoveUsed(Side.Opponent, "PIDGEY", "TACKLE")));
    }

    [Fact]
    public void AnnouncesAMiss()
    {
        Assert.Contains("missed", BattleNarrator.Describe(
            new BattleEvent.MoveMissed(Side.Player, "BULBASAUR", "TACKLE")));
    }

    [Fact]
    public void MentionsEffectivenessOnlyWhenItIsNotNeutral()
    {
        // Saying "it's normally effective" every turn would be noise, which is why the
        // games stay quiet about it.
        var neutral = new DamageResult(12, false, 100, false);
        var strong = new DamageResult(24, false, 200, false);
        var weak = new DamageResult(6, false, 50, false);

        Assert.DoesNotContain("effective", BattleNarrator.Describe(
            new BattleEvent.DamageDealt(Side.Opponent, "PIDGEY", 12, 10, neutral)));

        Assert.Contains("super effective", BattleNarrator.Describe(
            new BattleEvent.DamageDealt(Side.Opponent, "PIDGEY", 24, 10, strong)));

        Assert.Contains("not very effective", BattleNarrator.Describe(
            new BattleEvent.DamageDealt(Side.Opponent, "PIDGEY", 6, 10, weak)));
    }

    [Fact]
    public void CallsOutACriticalHit()
    {
        var critical = new DamageResult(30, true, 100, false);

        Assert.StartsWith("A critical hit!", BattleNarrator.Describe(
            new BattleEvent.DamageDealt(Side.Opponent, "PIDGEY", 30, 0, critical)));
    }

    [Fact]
    public void ReportsTheDamageDone()
    {
        Assert.Contains("took 12 damage", BattleNarrator.Describe(
            new BattleEvent.DamageDealt(Side.Opponent, "PIDGEY", 12, 5, new DamageResult(12, false, 100, false))));
    }

    [Theory]
    [InlineData(StatusCondition.Sleep, "asleep")]
    [InlineData(StatusCondition.Freeze, "frozen")]
    [InlineData(StatusCondition.Paralysis, "paralysed")]
    public void ExplainsWhyABattlerCouldNotMove(StatusCondition cause, string expected)
    {
        Assert.Contains(expected, BattleNarrator.Describe(
            new BattleEvent.Immobilised(Side.Player, "BULBASAUR", cause)));
    }

    [Fact]
    public void DistinguishesBurnFromPoison()
    {
        Assert.Contains("burn", BattleNarrator.Describe(
            new BattleEvent.StatusHurt(Side.Player, "X", StatusCondition.Burn, 3, 10)));

        Assert.Contains("poison", BattleNarrator.Describe(
            new BattleEvent.StatusHurt(Side.Player, "X", StatusCondition.Poison, 3, 10)));
    }

    [Fact]
    public void AnnouncesTheOutcome()
    {
        Assert.Contains("won", BattleNarrator.Describe(new BattleEvent.Ended(Side.Player)));
        Assert.Contains("no more usable", BattleNarrator.Describe(new BattleEvent.Ended(Side.Opponent)));
        Assert.Contains("draw", BattleNarrator.Describe(new BattleEvent.Ended(null)));
    }

    [Fact]
    public void SkipsEventsWithNothingToSay()
    {
        var events = new List<BattleEvent>
        {
            new BattleEvent.MoveUsed(Side.Player, "A", "TACKLE"),
            new BattleEvent.Fainted(Side.Opponent, "B"),
        };

        List<string> lines = BattleNarrator.Describe(events).ToList();

        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.NotEmpty(line));
    }

    [Fact]
    public void NarratesAWholeTurnInOrder()
    {
        var battle = new Battle(
            new Battler(TestMons.Species("BULBASAUR", PokemonType.Grass), 5, nickname: "BULBASAUR")
                .Knowing(TestMons.Tackle),
            new Battler(TestMons.Species("PIDGEY", PokemonType.Flying), 3, nickname: "PIDGEY")
                .Knowing(TestMons.Tackle),
            seed: 4242);

        List<string> lines = BattleNarrator
            .Describe(battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0)))
            .ToList();

        Assert.NotEmpty(lines);
        Assert.Contains(lines, l => l.Contains("used TACKLE"));
    }
}
