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
    public static readonly MoveData Growl = new(1, "GROWL", 0x12, 0, PokemonType.Normal, 100, 40, 0, 0, 0);

    /// <summary>Effect 0x01, which on a real image is the six sleep moves and nothing else.</summary>
    public static readonly MoveData SleepPowder = new(2, "SLEEP POWDER", 0x01, 0, PokemonType.Grass, 100, 15, 0, 0, 0);

    /// <summary>Effect 0x3B: SCREECH, alone in its group, two stages off the target's defence.</summary>
    public static readonly MoveData Screech = new(3, "SCREECH", 0x3B, 0, PokemonType.Normal, 100, 40, 0, 0, 0);

    /// <summary>Effect 0x32: SWORDS DANCE, two stages onto the user's own attack.</summary>
    public static readonly MoveData SwordsDance = new(4, "SWORDS DANCE", 0x32, 0, PokemonType.Normal, 0, 30, 0, 0, 0);

    /// <summary>Effect 0x06 with a secondary chance: a hit that may paralyse, like THUNDERBOLT.</summary>
    public static readonly MoveData Thunderbolt =
        new(5, "THUNDERBOLT", 0x06, 95, PokemonType.Electric, 100, 15, 100, 0, 0);
    public static readonly MoveData NeverMisses = Move("SWIFT", PokemonType.Normal, power: 60, accuracy: 0);

    /// <summary>Effect 0x1D: the twelve that land more than once, like DOUBLESLAP.</summary>
    public static readonly MoveData DoubleSlap = new(6, "DOUBLESLAP", 0x1D, 15, PokemonType.Normal, 100, 10, 0, 0, 0);

    /// <summary>Effect 0x2B: the eight that crit far more often, like SLASH.</summary>
    public static readonly MoveData Slash = new(7, "SLASH", 0x2B, 70, PokemonType.Normal, 100, 20, 0, 0, 0);

    /// <summary>Effect 0x1F: the six that can make somebody lose their turn, like BITE.</summary>
    public static readonly MoveData Bite = new(8, "BITE", 0x1F, 60, PokemonType.Dark, 100, 25, 100, 0, 0);

    /// <summary>Effect 0x03: the four that give back what they take, like ABSORB.</summary>
    public static readonly MoveData Absorb = new(9, "ABSORB", 0x03, 40, PokemonType.Grass, 100, 25, 0, 0, 0);

    /// <summary>Effect 0x30: the three that cost the user, like TAKE DOWN.</summary>
    public static readonly MoveData TakeDown = new(10, "TAKE DOWN", 0x30, 90, PokemonType.Normal, 85, 20, 0, 0, 0);

    /// <summary>Effect 0x20: RECOVER and SLACK OFF.</summary>
    public static readonly MoveData Recover = new(11, "RECOVER", 0x20, 0, PokemonType.Normal, 0, 10, 0, 0, 0);

    /// <summary>Effect 0x31: SUPERSONIC, CONFUSE RAY and SWEET KISS, which only confuse.</summary>
    public static readonly MoveData ConfuseRay = new(12, "CONFUSE RAY", 0x31, 0, PokemonType.Ghost, 100, 10, 0, 0, 0);

    /// <summary>Effect 0x4C: the six that damage and carry confusion on a roll.</summary>
    public static readonly MoveData Psybeam = new(13, "PSYBEAM", 0x4C, 65, PokemonType.Psychic, 100, 20, 100, 0, 0);
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

    [Fact]
    public void AStatusMoveDoesWhatItsEffectByteSays()
    {
        // The line this replaces was `if (move.Category == DamageCategory.Status) return;`
        // — 138 of this cartridge's 354 moves, doing nothing at all. A level 30 BULBASAUR
        // could spend a whole fight announcing "used POISONPOWDER!" at a level 9 PIDGEY
        // and never touch it.
        var battle = new Battle(Fast(TestMons.SleepPowder), Slow(TestMons.Tackle), seed: 1);

        List<BattleEvent> events = battle.ResolveTurn(FirstMove, FirstMove);

        BattleEvent.StatusInflicted put = events.OfType<BattleEvent.StatusInflicted>().Single();

        Assert.Equal(Side.Opponent, put.Side);
        Assert.Equal(StatusCondition.Sleep, put.Status);
        Assert.Equal(StatusCondition.Sleep, battle.Opponent.Status);
    }

    [Fact]
    public void SomebodyAsleepLosesTheTurn()
    {
        // The half that already worked. Sleep counts down, paralysis skips a quarter of
        // turns, poison takes a sixteenth — all of it was there, and nothing on the
        // cartridge could ever bring any of it about.
        var battle = new Battle(Fast(TestMons.SleepPowder), Slow(TestMons.Tackle), seed: 1);

        battle.ResolveTurn(FirstMove, FirstMove);

        // Either still asleep or waking, and both cost the turn: a sleep that runs one
        // turn used to cost nothing, which made SLEEP POWDER do nothing a third of the
        // time it landed. Nobody had ever seen that, because nothing could inflict it.
        List<BattleEvent> next = battle.ResolveTurn(FirstMove, FirstMove);

        Assert.True(
            next.OfType<BattleEvent.Immobilised>().Any(e => e.Side == Side.Opponent) ||
            next.OfType<BattleEvent.WokeUp>().Any(e => e.Side == Side.Opponent));

        Assert.DoesNotContain(next.OfType<BattleEvent.MoveUsed>(), e => e.Side == Side.Opponent);
    }

    [Fact]
    public void LoweringAStatMovesItTwiceForTheTwoStageMoves()
    {
        // SCREECH is in effect group 0x3B, which is 0x3A plus one — and 0x3A is CHARM,
        // which lowers attack. One run of seven, in the order the stats are already in.
        var battle = new Battle(Fast(TestMons.Screech), Slow(TestMons.Tackle), seed: 1);

        battle.ResolveTurn(FirstMove, FirstMove);

        Assert.Equal(-2, battle.Opponent.StageOf(Stat.Defense));
    }

    [Fact]
    public void RaisingOnesOwnStatIsRaisingOnesOwn()
    {
        // The difference between the two runs is who it happens to, and it is not written
        // anywhere in the record: 0x32 SWORDS DANCE raises the user, 0x3A CHARM lowers
        // the other one. That is read off the members and nowhere else.
        var battle = new Battle(Fast(TestMons.SwordsDance), Slow(TestMons.Tackle), seed: 1);

        battle.ResolveTurn(FirstMove, FirstMove);

        Assert.Equal(2, battle.Player.StageOf(Stat.Attack));
        Assert.Equal(0, battle.Opponent.StageOf(Stat.Attack));
    }

    [Fact]
    public void AStatAtItsLimitSaysSoRatherThanMovingAgain()
    {
        var battle = new Battle(Fast(TestMons.SwordsDance), Slow(TestMons.Tackle), seed: 1);

        for (int turn = 0; turn < 5; turn++) battle.ResolveTurn(FirstMove, FirstMove);

        Assert.Equal(Stats.MaxStage, battle.Player.StageOf(Stat.Attack));

        List<BattleEvent> again = battle.ResolveTurn(FirstMove, FirstMove);

        Assert.False(again.OfType<BattleEvent.StageChanged>().First().Moved);
    }

    [Fact]
    public void ARiderOnAHitStillDoesDamage()
    {
        // THUNDERBOLT and THUNDER WAVE carry the same paralysis and are not the same
        // promise: one is the move, the other rolls against the move's own secondary
        // chance. This one is set to a hundred so the roll is not what is under test.
        var battle = new Battle(Fast(TestMons.Thunderbolt), Slow(TestMons.Tackle), seed: 7);

        List<BattleEvent> events = battle.ResolveTurn(FirstMove, FirstMove);

        Assert.Contains(events.OfType<BattleEvent.DamageDealt>(), e => e.Side == Side.Opponent);
        Assert.Equal(StatusCondition.Paralysis, battle.Opponent.Status);
    }

    [Fact]
    public void NothingRidesOnAKnockout()
    {
        // A creature that has fainted cannot be paralysed, and saying it was would put a
        // line on the screen about somebody who is no longer standing.
        var battle = new Battle(
            Fast(TestMons.Thunderbolt),
            new Battler(TestMons.Species("FRAIL", PokemonType.Water, hp: 1, defense: 1, spDefense: 1), level: 2)
                .Knowing(TestMons.Tackle),
            seed: 7);

        List<BattleEvent> events = battle.ResolveTurn(FirstMove, FirstMove);

        Assert.Contains(events.OfType<BattleEvent.Fainted>(), e => e.Side == Side.Opponent);
        Assert.Empty(events.OfType<BattleEvent.StatusInflicted>());
    }
}

public class BattleNarratorTests
{
    /// <summary>
    /// Names for the two sides and for moves. Events carry neither, because the server
    /// that will produce them has neither — so narration takes them as an argument.
    /// </summary>
    private static readonly BattleNames Names =
        new("BULBASAUR", "PIDGEY", id => id == 33 ? "TACKLE" : $"move {id}");

    private const int Tackle = 33;

    [Fact]
    public void AnnouncesAMove()
    {
        Assert.Equal(
            "PIDGEY used TACKLE!",
            BattleNarrator.Describe(new BattleEvent.MoveUsed(Side.Opponent, Tackle), Names));
    }

    [Fact]
    public void AnnouncesAMiss()
    {
        Assert.Contains("missed", BattleNarrator.Describe(
            new BattleEvent.MoveMissed(Side.Player, Tackle), Names));
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
            new BattleEvent.DamageDealt(Side.Opponent, 12, 10, neutral), Names));

        Assert.Contains("super effective", BattleNarrator.Describe(
            new BattleEvent.DamageDealt(Side.Opponent, 24, 10, strong), Names));

        Assert.Contains("not very effective", BattleNarrator.Describe(
            new BattleEvent.DamageDealt(Side.Opponent, 6, 10, weak), Names));
    }

    [Fact]
    public void CallsOutACriticalHit()
    {
        var critical = new DamageResult(30, true, 100, false);

        Assert.StartsWith("A critical hit!", BattleNarrator.Describe(
            new BattleEvent.DamageDealt(Side.Opponent, 30, 0, critical), Names));
    }

    [Fact]
    public void ReportsTheDamageDone()
    {
        Assert.Contains("took 12 damage", BattleNarrator.Describe(
            new BattleEvent.DamageDealt(Side.Opponent, 12, 5, new DamageResult(12, false, 100, false)), Names));
    }

    [Theory]
    [InlineData(StatusCondition.Sleep, "asleep")]
    [InlineData(StatusCondition.Freeze, "frozen")]
    [InlineData(StatusCondition.Paralysis, "paralysed")]
    public void ExplainsWhyABattlerCouldNotMove(StatusCondition cause, string expected)
    {
        Assert.Contains(expected, BattleNarrator.Describe(
            new BattleEvent.Immobilised(Side.Player, cause), Names));
    }

    [Fact]
    public void DistinguishesBurnFromPoison()
    {
        Assert.Contains("burn", BattleNarrator.Describe(
            new BattleEvent.StatusHurt(Side.Player, StatusCondition.Burn, 3, 10), Names));

        Assert.Contains("poison", BattleNarrator.Describe(
            new BattleEvent.StatusHurt(Side.Player, StatusCondition.Poison, 3, 10), Names));
    }

    [Fact]
    public void AnnouncesTheOutcome()
    {
        Assert.Contains("won", BattleNarrator.Describe(new BattleEvent.Ended(Side.Player), Names));
        Assert.Contains("no more usable", BattleNarrator.Describe(new BattleEvent.Ended(Side.Opponent), Names));
        Assert.Contains("draw", BattleNarrator.Describe(new BattleEvent.Ended(null), Names));
    }

    [Fact]
    public void SkipsEventsWithNothingToSay()
    {
        var events = new List<BattleEvent>
        {
            new BattleEvent.MoveUsed(Side.Player, Tackle),
            new BattleEvent.Fainted(Side.Opponent),
        };

        List<string> lines = BattleNarrator.Describe(events, Names).ToList();

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
            .Describe(
                battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0)),
                new BattleNames("BULBASAUR", "PIDGEY", id => TestMons.Tackle.Name))
            .ToList();

        Assert.NotEmpty(lines);
        Assert.Contains(lines, l => l.Contains("used TACKLE"));
    }

}

/// <summary>
/// The effect table itself, apart from any battle.
/// <para>
/// What each number means was read off the members of its group on a real image, and the
/// shape is what makes it more than a list of special cases: four runs of exactly seven,
/// in the order attack, defence, speed, special attack, special defence, accuracy,
/// evasion.
/// </para>
/// </summary>
public class MoveEffectTests
{
    [Theory]
    [InlineData(0x0A, Stat.Attack, 1)]      // MEDITATE, SHARPEN, HOWL
    [InlineData(0x0B, Stat.Defense, 1)]     // HARDEN, WITHDRAW
    [InlineData(0x0D, Stat.SpAttack, 1)]    // GROWTH
    [InlineData(0x10, Stat.Evasion, 1)]     // DOUBLE TEAM
    [InlineData(0x32, Stat.Attack, 2)]      // SWORDS DANCE
    [InlineData(0x33, Stat.Defense, 2)]     // BARRIER, ACID ARMOR, IRON DEFENSE
    [InlineData(0x34, Stat.Speed, 2)]       // AGILITY
    [InlineData(0x35, Stat.SpAttack, 2)]    // TAIL GLOW
    [InlineData(0x36, Stat.SpDefense, 2)]   // AMNESIA
    public void TheRunsThatRaiseTheUsersOwnStat(byte effect, Stat stat, int stages)
    {
        MoveEffect read = MoveEffects.Of(effect);

        Assert.Equal(EffectKind.Stage, read.Kind);
        Assert.True(read.OnUser);
        Assert.Equal(stat, read.Stat);
        Assert.Equal(stages, read.Stages);
    }

    [Theory]
    [InlineData(0x12, Stat.Attack, -1)]     // GROWL
    [InlineData(0x13, Stat.Defense, -1)]    // TAIL WHIP, LEER
    [InlineData(0x14, Stat.Speed, -1)]      // STRING SHOT
    [InlineData(0x17, Stat.Accuracy, -1)]   // SAND-ATTACK, SMOKESCREEN, KINESIS, FLASH
    [InlineData(0x18, Stat.Evasion, -1)]    // SWEET SCENT
    [InlineData(0x3A, Stat.Attack, -2)]     // CHARM, FEATHERDANCE
    [InlineData(0x3B, Stat.Defense, -2)]    // SCREECH
    [InlineData(0x3C, Stat.Speed, -2)]      // COTTON SPORE, SCARY FACE
    [InlineData(0x3E, Stat.SpDefense, -2)]  // FAKE TEARS, METAL SOUND
    [InlineData(0x44, Stat.Attack, -1)]     // AURORA BEAM
    [InlineData(0x45, Stat.Defense, -1)]    // ACID, IRON TAIL, ROCK SMASH, CRUSH CLAW
    [InlineData(0x46, Stat.Speed, -1)]      // BUBBLEBEAM, ICY WIND, ROCK TOMB, MUD SHOT
    public void TheRunsThatLowerTheOtherOnes(byte effect, Stat stat, int stages)
    {
        MoveEffect read = MoveEffects.Of(effect);

        Assert.Equal(EffectKind.Stage, read.Kind);
        Assert.False(read.OnUser);
        Assert.Equal(stat, read.Stat);
        Assert.Equal(stages, read.Stages);
    }

    [Theory]
    [InlineData(0x01, StatusCondition.Sleep)]        // SING, SLEEP POWDER, HYPNOSIS, SPORE
    [InlineData(0x42, StatusCondition.Poison)]       // POISONPOWDER, POISON GAS
    [InlineData(0x43, StatusCondition.Paralysis)]    // STUN SPORE, THUNDER WAVE, GLARE
    [InlineData(0x02, StatusCondition.Poison)]       // POISON STING, SLUDGE
    [InlineData(0x04, StatusCondition.Burn)]         // EMBER, FLAMETHROWER
    [InlineData(0x05, StatusCondition.Freeze)]       // ICE BEAM, BLIZZARD
    [InlineData(0x06, StatusCondition.Paralysis)]    // THUNDERBOLT, BODY SLAM
    public void TheGroupsThatAreOneConditionEach(byte effect, StatusCondition status)
    {
        MoveEffect read = MoveEffects.Of(effect);

        Assert.Equal(EffectKind.Status, read.Kind);
        Assert.False(read.OnUser);
        Assert.Equal(status, read.Status);
    }

    [Theory]
    [InlineData(0x11)]  // SWIFT, FAINT ATTACK, AERIAL ACE — never misses
    [InlineData(0x39)]  // TRANSFORM
    [InlineData(0x41)]  // REFLECT
    [InlineData(0x53)]  // METRONOME
    public void ARunIsSevenWideAndWhatFollowsItIsSomethingElse(byte effect)
    {
        // The runs would each be one longer if the slot after them were claimed, and the
        // move sitting in that slot is the check: SWIFT is not "raise evasion by one" and
        // TRANSFORM is not "raise accuracy by two".
        //
        // Not a stage, whichever kind of not-a-stage it is: 0x11's whole job is in the
        // accuracy field and the other three are simply unwritten.
        Assert.NotEqual(EffectKind.Stage, MoveEffects.Of(effect).Kind);
        Assert.Equal(0, MoveEffects.Of(effect).Stages);
    }
}

/// <summary>
/// The rest of what a hit can carry.
/// <para>
/// Six groups of the effect table that had nothing behind them: the twelve that land
/// more than once, the eight that crit, the ten that make somebody lose a turn, the four
/// that drink, the three that cost the user, and the four that heal. Forty-one moves,
/// every one of which announced itself and did exactly as much as a move with no effect
/// byte at all.
/// </para>
/// <para>
/// The group membership is read off the cartridge. The amounts — how many times, what
/// share back, what share paid, how much healed — are modelled, and the tests below
/// assert the shape of them rather than the exact numbers, because the exact numbers are
/// a judgement and a test that pins a judgement is a test that has to be rewritten the
/// day the judgement improves.
/// </para>
/// </summary>
public class HitRiderTests
{
    private static Battler One(params MoveData[] moves) =>
        new Battler(TestMons.Species("ONE", PokemonType.Normal, hp: 120, speed: 200), level: 50, nickname: "ONE")
            .Knowing(moves);

    private static Battler Two(params MoveData[] moves) =>
        new Battler(TestMons.Species("TWO", PokemonType.Normal, hp: 250, speed: 5), level: 50, nickname: "TWO")
            .Knowing(moves);

    private static readonly BattleAction First = new BattleAction.UseMove(0);

    [Fact]
    public void AMultiHitMoveLandsBetweenTwoAndFiveTimes()
    {
        var seen = new HashSet<int>();

        for (uint seed = 1; seed <= 60; seed++)
        {
            var battle = new Battle(One(TestMons.DoubleSlap), Two(TestMons.Tackle), seed);

            List<BattleEvent> events = battle.ResolveTurn(First, First);

            if (events.OfType<BattleEvent.HitSeveralTimes>().FirstOrDefault() is not { } several) continue;

            seen.Add(several.Times);

            Assert.InRange(several.Times, 2, 5);

            // And the damage lines match the count, because the count is what the player
            // is being told and the lines are what they watched.
            Assert.Equal(
                several.Times,
                events.OfType<BattleEvent.DamageDealt>().Count(d => d.Side == Side.Opponent));
        }

        Assert.True(seen.Count > 1, $"only ever landed {string.Join(",", seen)} times");
    }

    [Fact]
    public void AnOrdinaryMoveSaysNothingAboutHowManyTimesItLanded()
    {
        var battle = new Battle(One(TestMons.Tackle), Two(TestMons.Tackle), seed: 1);

        Assert.Empty(battle.ResolveTurn(First, First).OfType<BattleEvent.HitSeveralTimes>());
    }

    [Fact]
    public void TheHighCriticalGroupCritsMoreOftenThanAnOrdinaryMove()
    {
        // A proportion rather than a number. What is being tested is that the group does
        // something at all — an engine that ignored the effect byte would give these two
        // the same answer, and that is the failure this catches.
        // Half again rather than double, because doubling is what the model does and a
        // test that asserts the model's exact number is a test that has to be rewritten
        // the day the model improves. What must not happen is the two coming out equal.
        Assert.True(Crits(TestMons.Slash) * 2 > Crits(TestMons.Tackle) * 3,
            $"SLASH crit {Crits(TestMons.Slash)} times, TACKLE {Crits(TestMons.Tackle)}");

        static int Crits(MoveData move)
        {
            int count = 0;

            for (uint seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(One(move), Two(TestMons.Tackle), seed);

                count += battle.ResolveTurn(First, First)
                    .OfType<BattleEvent.DamageDealt>()
                    .Count(d => d.Side == Side.Opponent && d.Detail.Critical);
            }

            return count;
        }
    }

    [Fact]
    public void ADrainingMoveGivesBackSomeOfWhatItDealt()
    {
        var mine = One(TestMons.Absorb);

        mine.TakeDamage(mine.MaxHp - 10);

        var battle = new Battle(mine, Two(TestMons.Tackle), seed: 3);

        List<BattleEvent> events = battle.ResolveTurn(First, First);

        BattleEvent.Drained drained = events.OfType<BattleEvent.Drained>().Single();

        int dealt = events.OfType<BattleEvent.DamageDealt>().First(d => d.Side == Side.Opponent).Damage;

        Assert.True(drained.Amount > 0);
        Assert.True(drained.Amount <= dealt, "gave back more than it took");
    }

    [Fact]
    public void ARecoilMoveCostsTheUserSomething()
    {
        var battle = new Battle(One(TestMons.TakeDown), Two(TestMons.Tackle), seed: 5);

        List<BattleEvent> events = battle.ResolveTurn(First, First);

        // It may miss — eighty-five accuracy — so this asserts the pairing rather than
        // the occurrence: recoil happens exactly when damage was dealt.
        bool hit = events.OfType<BattleEvent.DamageDealt>().Any(d => d.Side == Side.Opponent);

        Assert.Equal(hit, events.OfType<BattleEvent.Recoiled>().Any());
    }

    [Fact]
    public void AFlinchCostsSomebodyWhoHasNotMovedYetTheirTurn()
    {
        // The fast one bites; the slow one never gets to act. BITE carries a hundred per
        // cent secondary chance in this fixture so the roll is not what is being tested.
        var battle = new Battle(One(TestMons.Bite), Two(TestMons.Tackle), seed: 1);

        List<BattleEvent> events = battle.ResolveTurn(First, First);

        Assert.Contains(events, e => e is BattleEvent.Flinched { Side: Side.Opponent });
        Assert.DoesNotContain(events, e => e is BattleEvent.MoveUsed { Side: Side.Opponent });
    }

    [Fact]
    public void AFlinchOnSomebodyWhoHasAlreadyMovedCostsThemNothing()
    {
        // The slow one bites second, so the fast one has already had its turn. A flinch
        // that reached backwards would be a flinch that costs a turn already spent.
        var battle = new Battle(One(TestMons.Tackle), Two(TestMons.Bite), seed: 1);

        List<BattleEvent> events = battle.ResolveTurn(First, First);

        Assert.Contains(events, e => e is BattleEvent.MoveUsed { Side: Side.Player });
        Assert.DoesNotContain(events, e => e is BattleEvent.Flinched);
    }

    [Fact]
    public void AndItDoesNotCarryIntoTheNextTurn()
    {
        // The first draft of this test bit twice and asserted the second turn was free,
        // which it never could be: BITE flinches every time in this fixture, so a flinch
        // that carried and a flinch that happened again look identical. So the second
        // turn is a different move, and the only thing that could still flinch is a
        // flinch left lying about.
        var battle = new Battle(One(TestMons.Bite, TestMons.Tackle), Two(TestMons.Tackle), seed: 1);

        battle.ResolveTurn(First, First);

        List<BattleEvent> next = battle.ResolveTurn(new BattleAction.UseMove(1), First);

        Assert.Contains(next, e => e is BattleEvent.MoveUsed { Side: Side.Opponent });
        Assert.DoesNotContain(next, e => e is BattleEvent.Flinched);
    }

    [Fact]
    public void AHealingMoveGivesBackAboutHalf()
    {
        var mine = One(TestMons.Recover);

        mine.TakeDamage(mine.MaxHp - 1);

        var battle = new Battle(mine, Two(TestMons.Tackle), seed: 9);

        BattleEvent.Recovered healed = battle.ResolveTurn(First, First)
            .OfType<BattleEvent.Recovered>()
            .Single();

        Assert.InRange(healed.Amount, mine.MaxHp / 3, mine.MaxHp);
    }

    [Fact]
    public void HealingSomebodyAlreadyWellSaysSoRatherThanNothing()
    {
        // The difference between "this did nothing" and "this is not implemented" is the
        // whole reason NothingHappened exists.
        var battle = new Battle(One(TestMons.Recover), Two(TestMons.Tackle), seed: 9);

        List<BattleEvent> events = battle.ResolveTurn(First, First);

        Assert.Contains(events, e => e is BattleEvent.NothingHappened);
        Assert.Empty(events.OfType<BattleEvent.Recovered>());
    }
}

/// <summary>
/// Confusion, which is not a condition.
/// <para>
/// It sits beside <c>Status</c> rather than among its values, because the games let you
/// be poisoned and confused at once. It lives on the battler rather than on the battle
/// so that it follows the one it happened to — switching out builds a new battler, which
/// is exactly where confusion should stop — and it is never written down, because
/// walking out of a battle confused is not something these games do.
/// </para>
/// </summary>
public class ConfusionTests
{
    private static Battler Quick(params MoveData[] moves) =>
        new Battler(TestMons.Species("QUICK", PokemonType.Normal, hp: 200, speed: 200), level: 50, nickname: "QUICK")
            .Knowing(moves);

    private static Battler Slowly(params MoveData[] moves) =>
        new Battler(TestMons.Species("SLOWLY", PokemonType.Normal, hp: 200, speed: 5), level: 50, nickname: "SLOWLY")
            .Knowing(moves);

    private static readonly BattleAction First = new BattleAction.UseMove(0);

    [Fact]
    public void AMoveThatOnlyConfusesDoesSo()
    {
        var battle = new Battle(Quick(TestMons.ConfuseRay), Slowly(TestMons.Tackle), seed: 2);

        Assert.Contains(
            battle.ResolveTurn(First, First),
            e => e is BattleEvent.Confused { Side: Side.Opponent });
    }

    [Fact]
    public void AMoveThatDamagesCanCarryItToo()
    {
        // The distinction the effect table exists to keep: CONFUSE RAY is the move,
        // PSYBEAM is a hit with confusion riding on it.
        var battle = new Battle(Quick(TestMons.Psybeam), Slowly(TestMons.Tackle), seed: 2);

        List<BattleEvent> events = battle.ResolveTurn(First, First);

        Assert.Contains(events, e => e is BattleEvent.DamageDealt { Side: Side.Opponent });
        Assert.Contains(events, e => e is BattleEvent.Confused { Side: Side.Opponent });
    }

    [Fact]
    public void ConfusingSomebodyAlreadyConfusedSaysSoRatherThanStacking()
    {
        var battle = new Battle(Quick(TestMons.ConfuseRay), Slowly(TestMons.Tackle), seed: 2);

        battle.ResolveTurn(First, First);

        List<BattleEvent> again = battle.ResolveTurn(First, First);

        Assert.Empty(again.OfType<BattleEvent.Confused>());
        Assert.Contains(again, e => e is BattleEvent.NothingHappened);
    }

    [Fact]
    public void AConfusedBattlerSometimesHurtsItselfInsteadOfActing()
    {
        // Over many seeds, because half is a proportion. What is being tested is that
        // both outcomes happen: a confusion that never fired and one that always fired
        // would each be a rule with no roll in it.
        int hurt = 0;
        int acted = 0;

        for (uint seed = 1; seed <= 40; seed++)
        {
            var battle = new Battle(Quick(TestMons.ConfuseRay), Slowly(TestMons.Tackle), seed);

            battle.ResolveTurn(First, First);

            List<BattleEvent> next = battle.ResolveTurn(First, First);

            if (next.Any(e => e is BattleEvent.HurtItself { Side: Side.Opponent })) hurt++;
            if (next.Any(e => e is BattleEvent.MoveUsed { Side: Side.Opponent })) acted++;
        }

        Assert.True(hurt > 0, "never once hurt itself");
        Assert.True(acted > 0, "never once got a turn");
    }

    [Fact]
    public void HurtingItselfNamesNoMove()
    {
        // There is no move — a confused creature hits itself — and printing one would be
        // printing something that did not happen.
        for (uint seed = 1; seed <= 40; seed++)
        {
            var battle = new Battle(Quick(TestMons.ConfuseRay), Slowly(TestMons.Tackle), seed);

            battle.ResolveTurn(First, First);

            List<BattleEvent> next = battle.ResolveTurn(First, First);

            if (!next.Any(e => e is BattleEvent.HurtItself { Side: Side.Opponent })) continue;

            Assert.DoesNotContain(next, e => e is BattleEvent.MoveUsed { Side: Side.Opponent });
            return;
        }

        Assert.Fail("never hurt itself across forty seeds");
    }

    [Fact]
    public void ItWearsOffAndTheTurnIsStillTaken()
    {
        // Snapping out happens before the turn rather than after it, so the turn it wears
        // off on is a turn that gets used.
        var battle = new Battle(Quick(TestMons.ConfuseRay), Slowly(TestMons.Tackle), seed: 2);

        battle.ResolveTurn(First, First);

        for (int turn = 0; turn < 8; turn++)
        {
            List<BattleEvent> events = battle.ResolveTurn(First, First);

            if (!events.Any(e => e is BattleEvent.SnappedOut { Side: Side.Opponent })) continue;

            Assert.Contains(events, e => e is BattleEvent.MoveUsed { Side: Side.Opponent });
            return;
        }

        Assert.Fail("confusion never wore off in eight turns");
    }

    [Fact]
    public void ItIsNotAConditionAndDoesNotDisplaceOne()
    {
        // The whole reason it is not a StatusCondition. Poisoned and confused at once is
        // an ordinary state of affairs and a condition that replaced poison would be a
        // different game.
        var target = Slowly(TestMons.Tackle);

        target.TryApplyStatus(StatusCondition.Poison);

        var battle = new Battle(Quick(TestMons.ConfuseRay), target, seed: 2);

        battle.ResolveTurn(First, First);

        Assert.Equal(StatusCondition.Poison, target.Status);
        Assert.True(target.IsConfused);
    }

    [Fact]
    public void ItIsNotWrittenDown()
    {
        // Walking out of a battle confused is not something these games do, and the
        // record a battler is saved into has nowhere to put it. Asserted on the record
        // rather than on a round trip, because the record is the thing that persists.
        var target = Slowly(TestMons.Tackle);
        target.ConfusedTurns = 4;

        PokeMmo.Core.Save.SavedMon written = PokeMmo.Server.BattleFactory.Save(target);

        Assert.Equal(StatusCondition.None, written.Status);
    }
}
