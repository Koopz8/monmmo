using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract.Tests;

public class BattleRngTests
{
    [Fact]
    public void TheSameSeedProducesTheSameSequence()
    {
        // The whole replay design rests on this: server and client run the same seed
        // and must see identical rolls.
        var a = new BattleRng(12345);
        var b = new BattleRng(12345);

        for (int i = 0; i < 200; i++) Assert.Equal(a.Next(), b.Next());
    }

    [Fact]
    public void DifferentSeedsDiverge()
    {
        var a = new BattleRng(1);
        var b = new BattleRng(2);

        Assert.NotEqual(
            Enumerable.Range(0, 20).Select(_ => a.Next()).ToArray(),
            Enumerable.Range(0, 20).Select(_ => b.Next()).ToArray());
    }

    [Fact]
    public void CanBeResumedMidSequence()
    {
        var original = new BattleRng(999);
        for (int i = 0; i < 10; i++) original.Next();

        BattleRng resumed = BattleRng.Resume(original.Seed, original.State);

        for (int i = 0; i < 20; i++) Assert.Equal(original.Next(), resumed.Next());
    }

    [Fact]
    public void StaysInsideRequestedBounds()
    {
        var rng = new BattleRng(7);

        for (int i = 0; i < 2000; i++) Assert.InRange(rng.Next(16), 0, 15);
    }

    [Fact]
    public void CertaintiesAreCertain()
    {
        var rng = new BattleRng(3);

        Assert.False(rng.Chance(0));
        Assert.True(rng.Chance(100));
        Assert.False(rng.OneIn(0));
    }
}

public class TypeChartTests
{
    [Theory]
    [InlineData(PokemonType.Water, PokemonType.Fire, TypeChart.SuperEffective)]
    [InlineData(PokemonType.Fire, PokemonType.Water, TypeChart.NotVeryEffective)]
    [InlineData(PokemonType.Normal, PokemonType.Ghost, TypeChart.Immune)]
    [InlineData(PokemonType.Ghost, PokemonType.Normal, TypeChart.Immune)]
    [InlineData(PokemonType.Electric, PokemonType.Ground, TypeChart.Immune)]
    [InlineData(PokemonType.Ground, PokemonType.Flying, TypeChart.Immune)]
    [InlineData(PokemonType.Poison, PokemonType.Steel, TypeChart.Immune)]
    [InlineData(PokemonType.Psychic, PokemonType.Dark, TypeChart.Immune)]
    [InlineData(PokemonType.Normal, PokemonType.Normal, TypeChart.Neutral)]
    public void MatchesTheChart(PokemonType attack, PokemonType defend, int expected)
    {
        Assert.Equal(expected, TypeChart.Against(attack, defend));
    }

    [Fact]
    public void GhostHitsGhostSuperEffectivelyInThisGeneration()
    {
        Assert.Equal(TypeChart.SuperEffective, TypeChart.Against(PokemonType.Ghost, PokemonType.Ghost));
    }

    [Fact]
    public void DarkAndSteelResistancesAreGenerationThree()
    {
        // Steel resisting Ghost and Dark is a Gen II-V rule that later editions dropped.
        Assert.Equal(TypeChart.NotVeryEffective, TypeChart.Against(PokemonType.Ghost, PokemonType.Steel));
        Assert.Equal(TypeChart.NotVeryEffective, TypeChart.Against(PokemonType.Dark, PokemonType.Steel));
    }

    [Fact]
    public void BothDefendingTypesApply()
    {
        // Rock/Ground against Water: super effective twice over.
        Assert.Equal(400, TypeChart.Effectiveness(PokemonType.Water, PokemonType.Rock, PokemonType.Ground));
    }

    [Fact]
    public void ASingleTypeIsNotCountedTwice()
    {
        Assert.Equal(200, TypeChart.Effectiveness(PokemonType.Water, PokemonType.Fire, PokemonType.Fire));
    }

    [Fact]
    public void OneImmunityBeatsAnyNumberOfWeaknesses()
    {
        Assert.Equal(0, TypeChart.Effectiveness(PokemonType.Ground, PokemonType.Electric, PokemonType.Flying));
    }

    [Fact]
    public void EffectivenessIsAppliedAsSuccessiveIntegerDivisions()
    {
        // Doubly resisted: 100 -> 50 -> 25 with truncation at each step, which is not
        // the same as multiplying by a quarter once.
        int damage = TypeChart.Apply(101, PokemonType.Fighting, PokemonType.Flying, PokemonType.Psychic);
        Assert.Equal(25, damage);
    }

    [Fact]
    public void TheMysteryTypeIsInert()
    {
        Assert.Equal(TypeChart.Neutral, TypeChart.Against(PokemonType.Mystery, PokemonType.Water));
        Assert.Equal(TypeChart.Neutral, TypeChart.Against(PokemonType.Water, PokemonType.Mystery));
    }
}

public class StatsTests
{
    [Fact]
    public void HitPointsMatchTheFormula()
    {
        // Blissey, base 255 HP, level 100, perfect IVs, no EVs.
        Assert.Equal(651, Stats.Hp(baseStat: 255, level: 100));

        // The lowest base HP in the game still yields 143 by the formula — Shedinja's
        // single hit point is a special case the games apply afterwards, not something
        // the arithmetic produces, so it does not belong here.
        Assert.Equal(143, Stats.Hp(baseStat: 1, level: 100));

        // Level scales it: base 100 at level 50 is (2*100 + 31)*50/100 + 50 + 10.
        Assert.Equal(175, Stats.Hp(baseStat: 100, level: 50));
    }

    [Fact]
    public void EffortValuesAreWorthAQuarterOfAPoint()
    {
        // 4 EVs buy one point of the pre-level calculation, which is why they are
        // spent in multiples of four.
        Assert.Equal(
            Stats.Hp(baseStat: 100, level: 100, iv: 31, ev: 0) + 1,
            Stats.Hp(baseStat: 100, level: 100, iv: 31, ev: 4));
    }

    [Fact]
    public void OtherStatsMatchTheFormula()
    {
        // Base 100, level 100, perfect IVs, no EVs, neutral nature.
        Assert.Equal(236, Stats.Other(Stat.Attack, 100, 100, Nature.Hardy));
    }

    [Fact]
    public void ANatureIsWorthATenthEitherWay()
    {
        int neutral = Stats.Other(Stat.Attack, 100, 100, Nature.Hardy);

        Assert.Equal(neutral * 110 / 100, Stats.Other(Stat.Attack, 100, 100, Nature.Adamant));
        Assert.Equal(neutral * 90 / 100, Stats.Other(Stat.Attack, 100, 100, Nature.Modest));
    }

    [Fact]
    public void ANatureLeavesUnrelatedStatsAlone()
    {
        Assert.Equal(
            Stats.Other(Stat.Defense, 100, 100, Nature.Hardy),
            Stats.Other(Stat.Defense, 100, 100, Nature.Adamant));
    }

    [Theory]
    [InlineData(Nature.Hardy)]
    [InlineData(Nature.Docile)]
    [InlineData(Nature.Serious)]
    [InlineData(Nature.Bashful)]
    [InlineData(Nature.Quirky)]
    public void FiveNaturesDoNothing(Nature nature)
    {
        Assert.True(Stats.IsNeutral(nature));
    }

    [Fact]
    public void TwentyOfTheTwentyFiveNaturesDoSomething()
    {
        int neutral = Enum.GetValues<Nature>().Count(Stats.IsNeutral);
        Assert.Equal(5, neutral);
    }

    [Fact]
    public void AdamantRaisesAttackAndLowersSpecialAttack()
    {
        (Stat raised, Stat lowered) = Stats.EffectOf(Nature.Adamant);

        Assert.Equal(Stat.Attack, raised);
        Assert.Equal(Stat.SpAttack, lowered);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 150)]
    [InlineData(2, 200)]
    [InlineData(6, 400)]
    [InlineData(-1, 66)]
    [InlineData(-2, 50)]
    [InlineData(-6, 25)]
    public void StatStagesMatchTheRatioTable(int stage, int expected)
    {
        Assert.Equal(expected, Stats.ApplyStage(100, stage));
    }

    [Fact]
    public void StatStagesAreClampedToSix()
    {
        Assert.Equal(Stats.ApplyStage(100, 6), Stats.ApplyStage(100, 99));
        Assert.Equal(Stats.ApplyStage(100, -6), Stats.ApplyStage(100, -99));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 133)]
    [InlineData(-1, 75)]
    [InlineData(6, 300)]
    [InlineData(-6, 33)]
    public void AccuracyStagesUseTheirOwnGentlerTable(int stage, int expected)
    {
        Assert.Equal(expected, Stats.ApplyAccuracyStage(100, stage));
    }
}
