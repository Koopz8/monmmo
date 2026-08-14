using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.RomExtract;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Things become other things.
/// <para>
/// Nothing in this game had ever evolved. A starter taken to fifty was still the thing
/// it hatched as, which is not a missing detail — it is most of what levelling is for.
/// </para>
/// </summary>
public class EvolutionTests
{
    private const int Stride = 40;
    private const int Entry = 8;
    private const int Count = 240;

    /// <summary>Base stats where a higher index is a stronger creature, so a table can be scored.</summary>
    private static List<SpeciesData> Species() =>
    [
        .. Enumerable.Range(0, Count).Select(i => new SpeciesData
        {
            Index = i,
            BaseHp = (byte)(20 + i),
            BaseAttack = (byte)(20 + i),
            BaseDefense = (byte)(20 + i),
            BaseSpeed = (byte)(20 + i),
            BaseSpAttack = (byte)(20 + i),
            BaseSpDefense = (byte)(20 + i),
        }),
    ];

    private static void Put(byte[] data, int at, int method, int parameter, int into)
    {
        data[at] = (byte)method;
        data[at + 1] = (byte)(method >> 8);
        data[at + 2] = (byte)parameter;
        data[at + 3] = (byte)(parameter >> 8);
        data[at + 4] = (byte)into;
        data[at + 5] = (byte)(into >> 8);
    }

    /// <summary>
    /// An image with a table on it and something that looks like one.
    /// <para>
    /// The decoy points backwards — every one of its entries turns something into
    /// something weaker — which is the whole test. A locator that only checked the shape
    /// would have two answers and no way to choose.
    /// </para>
    /// </summary>
    private static Rom Image(int byLevel = 4, int byStone = 7, int stoneId = 96)
    {
        var data = new byte[0xA580];

        int table = 0x1000;

        // Three two-stage lines and one three-stage line, plus a stone.
        Put(data, table + 1 * Stride, byLevel, 16, 2);
        Put(data, table + 2 * Stride, byLevel, 32, 3);
        Put(data, table + 4 * Stride, byLevel, 16, 5);
        Put(data, table + 5 * Stride, byLevel, 36, 6);
        Put(data, table + 7 * Stride, byLevel, 7, 8);
        Put(data, table + 8 * Stride, byLevel, 10, 9);
        Put(data, table + 10 * Stride, byLevel, 22, 11);
        Put(data, table + 12 * Stride, byStone, stoneId, 13);

        // Enough plain entries to clear the "believe nothing small" bar. A hundred is
        // the bar on purpose: three evolutions that happen to point uphill is a
        // coincidence, and a hundred is a table.
        // Stepped by two so none of them chains into another: the chain evidence has to
        // come from the four lines above, or the test proves itself.
        for (int i = 14; i < Count - 1; i += 2) Put(data, table + i * Stride, byLevel, 20 + (i % 60), i + 1);

        // The decoy: same shape, same size, and every arrow points at the weakest thing
        // there is. Pointing them at "one back" was not enough — read at a shifted offset
        // the whole table turns uphill, which is a nice reminder that a shape test alone
        // proves nothing at all.
        int decoy = 0x8000;

        for (int i = 2; i < Count - 1; i++) Put(data, decoy + i * Stride, byLevel, 16, 1);

        return new Rom(data);
    }

    [Fact]
    public void TheTableIsTheOneWhoseArrowsPointAtSomethingStronger()
    {
        EvolutionTable table = EvolutionExtractor.Locate(Image(), Species())!;

        Assert.Equal(Rom.BaseAddress + 0x1000, table.Address);
        Assert.Equal(Stride, table.Stride);
        Assert.Equal(Entry, table.EntrySize);
        Assert.Equal(table.Evolutions.Count, table.Stronger);
    }

    /// <summary>
    /// The derivation that makes this more than a lookup: the level method is the only
    /// one that follows itself, and it does so at a bigger number every time.
    /// </summary>
    [Fact]
    public void TheLevelMethodIsTheOneThatFollowsItself()
    {
        Assert.Equal(4, EvolutionExtractor.Locate(Image(), Species())!.ByLevel);
    }

    [Fact]
    public void AnImageThatNumbersTheMethodsDifferentlyReadsDifferently()
    {
        EvolutionTable table = EvolutionExtractor.Locate(Image(byLevel: 9, byStone: 2), Species())!;

        Assert.Equal(9, table.ByLevel);
        Assert.Contains(table.Evolutions, e => e.Method == 2);
    }

    [Fact]
    public void AnImageWithNoTableOnItSaysSo()
    {
        Assert.Null(EvolutionExtractor.Locate(new Rom(new byte[0xA580]), Species()));
    }

    [Fact]
    public void WhatANewGameKnowsAboutEvolutionSurvivesTheRulesFile()
    {
        var rules = new GameRules(
            Species(),
            [],
            [],
            evolutions: [new Evolution(1, 4, 16, 2), new Evolution(1, 7, 96, 3)])
        {
            EvolveByLevel = 4,
        };

        using var file = new MemoryStream();

        rules.Save(file);
        file.Position = 0;

        GameRules read = GameRules.Load(file);

        Assert.Equal(4, read.EvolveByLevel);
        Assert.Equal(2, read.EvolutionsOf(1).Count);
        Assert.Equal(2, read.EvolutionAt(1, 16)?.Into);
        Assert.Null(read.EvolutionAt(1, 15));
    }

    /// <summary>
    /// A stone is an item somebody uses. Reaching a level is not using it, and a
    /// GRAVELER that turned into a GOLEM because it won a fight would be a rule this
    /// cartridge does not have.
    /// </summary>
    [Fact]
    public void OnlyTheLevelMethodHappensOnLevellingUp()
    {
        var rules = new GameRules(
            Species(),
            [],
            [],
            evolutions: [new Evolution(1, 7, 96, 2)])
        {
            EvolveByLevel = 4,
        };

        Assert.Null(rules.EvolutionAt(1, 100));
    }
}

/// <summary>What a victory does to something standing on the edge of being something else.</summary>
public class LevellingUpEvolutionTests
{
    private const int Little = 1;
    private const int Middle = 2;
    private const int Big = 3;

    /// <summary>
    /// Three forms and a two-step line, with a curve slow enough that one fight is one
    /// level and fast enough that a big one is several.
    /// </summary>
    private static GameRules Rules() =>
        new(
            [
                Form(Little, 40),
                Form(Middle, 60),
                Form(Big, 80),
            ],
            [new MoveData(TestRules.FirstMove, "", 0, 40, PokemonType.Normal, 100, 35, 0, 0, 0)],
            [
                new Learnset(Little, [new LevelUpMove(1, TestRules.FirstMove)]),
                new Learnset(Middle, [new LevelUpMove(1, TestRules.FirstMove)]),
                new Learnset(Big, [new LevelUpMove(1, TestRules.FirstMove)]),
            ],
            evolutions:
            [
                new Evolution(Little, 4, 6, Middle),
                new Evolution(Middle, 4, 12, Big),
            ])
        {
            EvolveByLevel = 4,
        };

    private static SpeciesData Form(int index, byte stat) => new()
    {
        Index = index,
        BaseHp = stat,
        BaseAttack = stat,
        BaseDefense = stat,
        BaseSpeed = stat,
        BaseSpAttack = stat,
        BaseSpDefense = stat,
        Type1 = PokemonType.Normal,
        Type2 = PokemonType.Normal,
        CatchRate = 45,
        ExpYield = 200,
        GenderRatio = 127,
        GrowthRate = GrowthRate.MediumFast,
    };

    private static SavedMon At(int species, int level) =>
        new(species, level, null, 1, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
        {
            Experience = Experience.TotalForLevel(GrowthRate.MediumFast, level),
        };

    [Fact]
    public void CrossingTheLevelTurnsItIntoTheNextThing()
    {
        var progression = new Progression(Rules());

        (SavedMon grown, List<BattleEvent> events) = progression.Award(At(Little, 5), Little, 20);

        Assert.Equal(Middle, grown.Species);
        Assert.Contains(events, e => e is BattleEvent.Evolved { From: Little, Into: Middle });
    }

    [Fact]
    public void StoppingShortOfItDoesNot()
    {
        var progression = new Progression(Rules());

        (SavedMon grown, List<BattleEvent> events) = progression.Award(At(Little, 2), Little, 1);

        Assert.Equal(Little, grown.Species);
        Assert.DoesNotContain(events, e => e is BattleEvent.Evolved);
    }

    /// <summary>
    /// Handed out above its own evolution level and two stages overdue, it catches up in
    /// one go. The alternative is a creature that has to win two more fights to become
    /// what it already should have been.
    /// </summary>
    [Fact]
    public void SomethingOverdueCatchesUpInOne()
    {
        var progression = new Progression(Rules());

        (SavedMon grown, List<BattleEvent> events) = progression.Award(At(Little, 30), Little, 100);

        Assert.Equal(Big, grown.Species);

        Assert.Equal(
            [(Little, Middle), (Middle, Big)],
            events.OfType<BattleEvent.Evolved>().Select(e => (e.From, e.Into)));
    }

    /// <summary>
    /// And what it learns afterwards is the new thing's list, not the old one's. This is
    /// the half that goes wrong silently: a CHARMELEON levelling on CHARMANDER's
    /// learnset reads perfectly and quietly teaches the wrong moves forever.
    /// </summary>
    [Fact]
    public void AfterwardsItLearnsWhatTheNewThingLearns()
    {
        const int OnlyTheBigOneKnows = 99;

        var rules = new GameRules(
            [Form(Little, 40), Form(Middle, 60), Form(Big, 80)],
            [
                new MoveData(TestRules.FirstMove, "", 0, 40, PokemonType.Normal, 100, 35, 0, 0, 0),
                new MoveData(OnlyTheBigOneKnows, "", 0, 90, PokemonType.Normal, 100, 10, 0, 0, 0),
            ],
            [
                new Learnset(Little, [new LevelUpMove(1, TestRules.FirstMove)]),
                new Learnset(Middle, [new LevelUpMove(7, OnlyTheBigOneKnows)]),
                new Learnset(Big, []),
            ],
            evolutions: [new Evolution(Little, 4, 6, Middle)])
        {
            EvolveByLevel = 4,
        };

        (SavedMon grown, List<BattleEvent> events) = new Progression(rules).Award(At(Little, 5), Little, 20);

        Assert.Equal(Middle, grown.Species);
        Assert.Contains(OnlyTheBigOneKnows, grown.Moves);
        Assert.Contains(events, e => e is BattleEvent.MoveLearned { MoveId: OnlyTheBigOneKnows });
    }

    /// <summary>A table that loops has to stop somewhere rather than spin.</summary>
    [Fact]
    public void ATableThatLoopsStopsRatherThanSpins()
    {
        var rules = new GameRules(
            [Form(Little, 40), Form(Middle, 60)],
            [new MoveData(TestRules.FirstMove, "", 0, 40, PokemonType.Normal, 100, 35, 0, 0, 0)],
            [new Learnset(Little, []), new Learnset(Middle, [])],
            evolutions:
            [
                new Evolution(Little, 4, 2, Middle),
                new Evolution(Middle, 4, 2, Little),
            ])
        {
            EvolveByLevel = 4,
        };

        (SavedMon grown, List<BattleEvent> events) = new Progression(rules).Award(At(Little, 5), Little, 20);

        Assert.True(events.OfType<BattleEvent.Evolved>().Count() <= 200);
        Assert.True(grown.Species is Little or Middle);
    }
}
