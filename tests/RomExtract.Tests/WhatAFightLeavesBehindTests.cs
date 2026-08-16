using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a fight leaves behind.
/// <para>
/// Every species record on this cartridge says what beating one of them is worth, in six
/// two-bit fields packed into two bytes. Both bytes have been extracted since the
/// base-stat table was first located, and read by nothing — the fifth field in six
/// milestones to turn out to be already in the data and simply unused. Worse than
/// unused: <c>Stats.Hp</c> and <c>Stats.Other</c> have taken an effort argument since the
/// day they were written, and every caller in the project left it at nought. The machinery
/// was finished at both ends and there was no middle.
/// </para>
/// <para>
/// Neither the packing nor the order is remembered. Of the 27 byte pairs a 28-byte record
/// has, exactly one reads as six slices totalling one to three for every record this
/// cartridge fields, and the 25 where it does not are 252 to 276 — one unbroken run, the
/// block the game keeps and never uses. The order comes off a second census: a species
/// yielding in one slice only should be a species whose highest base stat is what that
/// slice means, and the diagonal is 94, 100, 97, 100, 100, 97 per cent with nothing off
/// it above 20. <c>--evs</c> prints both.
/// </para>
/// <para>
/// The two limits are the modelled part, and they are in one place with the word on them.
/// </para>
/// </summary>
public class WhatAFightLeavesBehindTests
{
    private static byte[] Record(int packed)
    {
        var r = new byte[SpeciesData.SizeBytes];

        r[0] = 45; r[1] = 49; r[2] = 49; r[3] = 45; r[4] = 65; r[5] = 65;
        r[8] = 45; r[9] = 64;
        r[10] = (byte)(packed & 0xFF);
        r[11] = (byte)(packed >> 8);

        return r;
    }

    private static SpeciesData Yielding(int index, params int[] evs) => new()
    {
        Index = index,
        Name = string.Empty,
        BaseHp = 60, BaseAttack = 60, BaseDefense = 60,
        BaseSpeed = 60, BaseSpAttack = 60, BaseSpDefense = 60,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        EvHp = (byte)evs[0], EvAttack = (byte)evs[1], EvDefense = (byte)evs[2],
        EvSpeed = (byte)evs[3], EvSpAttack = (byte)evs[4], EvSpDefense = (byte)evs[5],
    };

    // ---- what the record says ---------------------------------------------------------

    /// <summary>Six slices, lowest bits first, in the order the six stats are in.</summary>
    [Fact]
    public void TheSlicesAreReadInTheSixStatOrder()
    {
        // 1, 2, 3, 0, 2, 1 from the bottom up.
        SpeciesData species = SpeciesData.Parse(Record(0b01_10_00_11_10_01), 1);

        Assert.Equal(1, species.EvHp);
        Assert.Equal(2, species.EvAttack);
        Assert.Equal(3, species.EvDefense);
        Assert.Equal(0, species.EvSpeed);
        Assert.Equal(2, species.EvSpAttack);
        Assert.Equal(1, species.EvSpDefense);
    }

    /// <summary>And asking by stat gets the same answer as asking by name.</summary>
    [Fact]
    public void AndOneCanBeAskedForByStat()
    {
        SpeciesData species = SpeciesData.Parse(Record(0b00_00_11_00_00_00), 1);

        Assert.Equal(3, species.EvYield(Stat.Speed));
        Assert.Equal(0, species.EvYield(Stat.Attack));
        Assert.Equal(3, species.EvTotal);
    }

    /// <summary>Nothing outside the six is a stat this is counted in.</summary>
    [Fact]
    public void AndNothingOutsideTheSixYieldsAnything()
    {
        SpeciesData species = SpeciesData.Parse(Record(0xFFFF), 1);

        Assert.Equal(0, species.EvYield(Stat.Accuracy));
        Assert.Equal(0, species.EvYield(Stat.Evasion));
    }

    // ---- what it does to a creature ---------------------------------------------------

    /// <summary>
    /// Four of these are worth one point of a stat at level a hundred, which is the
    /// formula the stats were already computed with — this is the argument arriving, not
    /// a new rule.
    /// </summary>
    [Fact]
    public void FourOfThemAreWorthAPointAtTheTop()
    {
        int without = Stats.Other(Stat.Attack, 100, 100, Nature.Hardy);
        int with = Stats.Other(Stat.Attack, 100, 100, Nature.Hardy, ev: 252);

        Assert.Equal(without + 63, with);
    }

    /// <summary>And a battler carries them into its own stats.</summary>
    [Fact]
    public void AndABattlerCarriesThemIn()
    {
        SpeciesData species = Yielding(1, 0, 0, 0, 0, 0, 0);

        var plain = new Battler(species, 100);
        var trained = new Battler(species, 100, Nature.Hardy, null, Effort.Of([252, 252, 0, 0, 0, 0]));

        Assert.Equal(plain.MaxHp + 63, trained.MaxHp);
        Assert.Equal(plain.Attack + 63, trained.Attack);
        Assert.Equal(plain.Defense, trained.Defense);
    }

    /// <summary>
    /// And the fight goes differently, which is the only claim that matters. A shape test
    /// says the number arrived; this says the number is being used.
    /// </summary>
    [Fact]
    public void AndItHitsHarderForIt()
    {
        MoveData move = new(1, "", 0x00, 60, PokemonType.Normal, 100, 20, 0, 0, 0);
        SpeciesData species = Yielding(1, 0, 0, 0, 0, 0, 0);

        static int Hurt(SpeciesData species, MoveData move, Effort effort)
        {
            Battler you = new Battler(species, 50, Nature.Hardy, null, effort).Knowing(move);
            Battler them = new Battler(species, 50).Knowing(move);

            var battle = new Battle(you, them, 7);
            int before = them.CurrentHp;

            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

            return before - them.CurrentHp;
        }

        Assert.True(Hurt(species, move, Effort.Of([0, 252, 0, 0, 0, 0])) > Hurt(species, move, Effort.None));
    }

    // ---- earning it -------------------------------------------------------------------

    /// <summary>Beating something leaves what its own record says it leaves.</summary>
    [Fact]
    public void BeatingSomethingLeavesWhatItsRecordSays()
    {
        Effort earned = Effort.None.Plus(Yielding(1, 0, 2, 0, 0, 0, 0));

        Assert.Equal(2, earned.In(Stat.Attack));
        Assert.Equal(0, earned.In(Stat.Speed));
        Assert.Equal(2, earned.Total);
    }

    /// <summary>And it accumulates, one fight at a time.</summary>
    [Fact]
    public void AndItAddsUpOverManyFights()
    {
        SpeciesData beaten = Yielding(1, 0, 1, 0, 0, 0, 0);
        Effort earned = Effort.None;

        for (int fight = 0; fight < 10; fight++) earned = earned.Plus(beaten);

        Assert.Equal(10, earned.In(Stat.Attack));
    }

    /// <summary>One stat stops where the limit is, and the rest keeps going.</summary>
    [Fact]
    public void AndOneStatStopsAtItsLimit()
    {
        Effort earned = Effort.Of([0, Effort.MostInOneStat, 0, 0, 0, 0]).Plus(Yielding(1, 1, 3, 0, 0, 0, 0));

        Assert.Equal(Effort.MostInOneStat, earned.In(Stat.Attack));
        Assert.Equal(1, earned.In(Stat.Hp));
    }

    /// <summary>
    /// And what is left of the total is handed out rather than refused: a creature eight
    /// short of the ceiling gains eight of a three, not none of it.
    /// </summary>
    [Fact]
    public void AndTheLastFewFitInWhatIsLeft()
    {
        // Two short of the total, and no single stat anywhere near its own limit.
        Effort nearly = Effort.Of([Effort.MostInOneStat, Effort.MostInOneStat - 2, 0, 0, 0, 0]);

        Assert.Equal(Effort.MostAltogether - 2, nearly.Total);

        Effort earned = nearly.Plus(Yielding(1, 0, 0, 3, 0, 0, 0));

        Assert.Equal(2, earned.In(Stat.Defense));
        Assert.Equal(Effort.MostAltogether, earned.Total);

        // And once it is full, nothing more goes in at all.
        Assert.Equal(earned, earned.Plus(Yielding(1, 0, 0, 3, 0, 0, 0)));
    }

    // ---- through the server -----------------------------------------------------------

    private static GameRules RulesWith(params SpeciesData[] species) =>
        new(species, [new MoveData(1, "", 0, 40, PokemonType.Normal, 100, 20, 0, 0, 0)], [], [], []);

    /// <summary>Winning a fight is what hands it over.</summary>
    [Fact]
    public void WinningAFightHandsItOver()
    {
        GameRules rules = RulesWith(Yielding(1, 0, 0, 0, 0, 0, 0), Yielding(2, 0, 2, 0, 0, 0, 0));

        SavedMon before = new(1, 10, null, 20, StatusCondition.None, Nature.Hardy, [1]);

        (SavedMon after, _) = new Progression(rules).Award(before, faintedSpecies: 2, faintedLevel: 10);

        Assert.Equal(2, after.Earned.In(Stat.Attack));
    }

    /// <summary>
    /// And a creature at the top of the curve still earns it. Experience stops at a
    /// hundred; this does not, and putting it after that check would have made the last
    /// level the level a creature stopped getting stronger.
    /// </summary>
    [Fact]
    public void AndSomethingAtTheTopStillEarnsIt()
    {
        GameRules rules = RulesWith(Yielding(1, 0, 0, 0, 0, 0, 0), Yielding(2, 0, 2, 0, 0, 0, 0));

        SavedMon capped = new(1, Experience.MaxLevel, null, 20, StatusCondition.None, Nature.Hardy, [1]);

        (SavedMon after, List<BattleEvent> events) =
            new Progression(rules).Award(capped, faintedSpecies: 2, faintedLevel: 50);

        Assert.Equal(2, after.Earned.In(Stat.Attack));
        Assert.Empty(events);
    }

    /// <summary>What is earned goes into the save and comes back out of it.</summary>
    [Fact]
    public void WhatIsEarnedSurvivesTheFight()
    {
        var factory = new BattleFactory(TestRules.All);

        SavedMon saved = new(1, 20, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
        {
            Evs = [4, 8, 12, 0, 0, 0],
        };

        Battler battler = factory.Restore(saved)!;

        Assert.Equal(8, battler.Effort.In(Stat.Attack));
        Assert.Equal([4, 8, 12, 0, 0, 0], BattleFactory.Save(battler).Evs);
    }

    /// <summary>And something that has never won anything carries nothing, not six noughts.</summary>
    [Fact]
    public void AndSomethingWithNoneCarriesNothingAtAll()
    {
        var factory = new BattleFactory(TestRules.All);

        SavedMon saved = new(1, 20, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

        Assert.Empty(BattleFactory.Save(factory.Restore(saved)!).Evs);
    }

    /// <summary>And a visit to a counter does not undo it. A centre heals; it does not untrain.</summary>
    [Fact]
    public void AndACounterDoesNotUndoIt()
    {
        var factory = new BattleFactory(TestRules.All);

        SavedMon hurt = new(1, 20, null, 1, StatusCondition.Poison, Nature.Hardy, [TestRules.FirstMove])
        {
            Evs = [0, 40, 0, 0, 0, 0],
        };

        Assert.Equal([0, 40, 0, 0, 0, 0], factory.Healed(hurt).Evs);
    }

    /// <summary>And it survives the process.</summary>
    [Fact]
    public async Task AndItSurvivesTheProcess()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon trained = new(16, 3, null, 8, StatusCondition.None, Nature.Bold, [33, 45])
            {
                Evs = [1, 2, 3, 4, 5, 6],
            };

            using (var writing = new SqlitePlayerStore(path))
            {
                await writing.RegisterAsync(
                    "Mason",
                    "a-good-password",
                    new SavedCharacter("1.0", 3, 4, Direction.Down, [trained]));
            }

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                Assert.Equal([1, 2, 3, 4, 5, 6], Assert.Single(login.Character.Party).Evs);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And a party saved before any of this reads back as having earned nothing — which
    /// is what it had earned. A column added only to a fresh table would have been a
    /// server that could not open yesterday's database at all.
    /// </summary>
    [Fact]
    public async Task AndAPartyFromBeforeThisComesBackWithNone()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon plain = new(16, 3, null, 8, StatusCondition.None, Nature.Bold, [33]);

            using (var writing = new SqlitePlayerStore(path))
            {
                await writing.RegisterAsync(
                    "Mason",
                    "a-good-password",
                    new SavedCharacter("1.0", 3, 4, Direction.Down, [plain]));
            }

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                Assert.Empty(Assert.Single(login.Character.Party).Evs);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And it gets out of the extractor at all.
    /// <para>
    /// The rules export copies a species field by field on purpose — nothing reaches the
    /// server that was not named — so a field left off that list is extracted, stored,
    /// serialised, awarded and nought the whole way. This is that list, asserted.
    /// </para>
    /// </summary>
    [Fact]
    public void AndItSurvivesTheExport()
    {
        GameRules exported = RulesExporter.Export(new SyntheticRom().ToRom());

        // The synthetic cartridge plants one point of HP on its anchor species.
        Assert.Equal(1, exported.SpeciesAt(SyntheticRom.TestSpecies)!.EvHp);
    }

    /// <summary>And what a species yields travels in the rules file.</summary>
    [Fact]
    public void AndTheYieldTravelsInTheRulesFile()
    {
        GameRules rules = RulesWith(Yielding(1, 1, 0, 2, 0, 0, 0));

        using var buffer = new MemoryStream();
        rules.Save(buffer);

        buffer.Position = 0;

        SpeciesData read = GameRules.Load(buffer).SpeciesAt(1)!;

        Assert.Equal(1, read.EvHp);
        Assert.Equal(2, read.EvDefense);
        Assert.Equal(3, read.EvTotal);
    }
}

/// <summary>
/// What a creature was born with.
/// <para>
/// The other half of the pair that makes two of a species different: effort is what a
/// creature has done, and this is what it was. <c>Stats.Hp</c> and <c>Stats.Other</c>
/// have taken this argument since they were written, and every caller in this project
/// left it at its default of thirty-one — so every creature in the game was perfect.
/// </para>
/// <para>
/// That is not a missing feature. It is the feature the rest of an MMO is about: with
/// nothing to breed for, nothing to hunt for and nothing that makes one PIDGEY worth more
/// than another, there is nothing worth trading either. Every market in every game like
/// this one is a market in these six numbers.
/// </para>
/// </summary>
public class WhatItWasBornWithTests
{
    private static SpeciesData Plain() => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 60, BaseAttack = 60, BaseDefense = 60,
        BaseSpeed = 60, BaseSpAttack = 60, BaseSpDefense = 60,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    /// <summary>Nought to thirty-one, and never outside it however it is asked for.</summary>
    [Fact]
    public void TheyAreNeverOutsideTheirRange()
    {
        for (uint seed = 1; seed <= 200; seed++)
        {
            Genes rolled = Genes.Roll(new BattleRng(seed));

            foreach (Stat stat in Genes.Order) Assert.InRange(rolled.In(stat), 0, Genes.Best);
        }

        Assert.Equal([0, 31, 31, 31, 31, 31], Genes.Of([-4, 99, 31, 31, 31, 31]).Values);
    }

    /// <summary>And they are not all the same number, which a bad roll would look like.</summary>
    [Fact]
    public void AndTheyAreSixNumbersRatherThanOne()
    {
        var seen = new HashSet<string>();

        for (uint seed = 1; seed <= 50; seed++) seen.Add(Genes.Roll(new BattleRng(seed)).ToString());

        Assert.True(seen.Count > 40, $"{seen.Count} different creatures out of fifty rolls");
    }

    /// <summary>A better creature is a better creature, which is the whole of what they do.</summary>
    [Fact]
    public void ABetterOneIsBetter()
    {
        var poor = new Battler(Plain(), 50, genes: Genes.Of([0, 0, 0, 0, 0, 0]));
        var perfect = new Battler(Plain(), 50, genes: Genes.Perfect);

        Assert.True(perfect.MaxHp > poor.MaxHp);
        Assert.True(perfect.Attack > poor.Attack);
        Assert.True(perfect.Speed > poor.Speed);
    }

    /// <summary>
    /// And a creature nobody said anything about is perfect, which is what every creature
    /// in this project was before this existed. A save from yesterday holds one.
    /// </summary>
    [Fact]
    public void AndSayingNothingIsWhatEverythingUsedToBe()
    {
        Assert.Equal(new Battler(Plain(), 50).MaxHp, new Battler(Plain(), 50, genes: Genes.Perfect).MaxHp);
        Assert.True(Genes.Of([]).IsPerfect);
    }

    /// <summary>What is born survives the save, and perfect is stored as nothing at all.</summary>
    [Fact]
    public void WhatIsBornSurvivesTheSave()
    {
        var factory = new BattleFactory(TestRules.All);

        SavedMon saved = new(1, 20, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
        {
            Ivs = [3, 14, 15, 9, 26, 5],
        };

        Battler battler = factory.Restore(saved)!;

        Assert.Equal(14, battler.Born.In(Stat.Attack));
        Assert.Equal([3, 14, 15, 9, 26, 5], BattleFactory.Save(battler).Ivs);

        SavedMon plain = new(1, 20, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

        Assert.Empty(BattleFactory.Save(factory.Restore(plain)!).Ivs);
    }

    /// <summary>And a wild one is rolled, while one built without dice is not.</summary>
    [Fact]
    public void AndAWildOneIsRolled()
    {
        var factory = new BattleFactory(TestRules.All);

        Assert.True(factory.Wild(1, 10)!.Born.IsPerfect);

        var seen = new HashSet<string>();

        for (uint seed = 1; seed <= 40; seed++)
            seen.Add(factory.Wild(1, 10, new BattleRng(seed))!.Born.ToString());

        Assert.True(seen.Count > 30, $"{seen.Count} different wild creatures out of forty");
    }

    /// <summary>And it survives the process.</summary>
    [Fact]
    public async Task AndItSurvivesTheProcess()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon born = new(16, 3, null, 8, StatusCondition.None, Nature.Bold, [33])
            {
                Ivs = [0, 31, 7, 22, 13, 30],
            };

            using (var writing = new SqlitePlayerStore(path))
            {
                await writing.RegisterAsync(
                    "Mason",
                    "a-good-password",
                    new SavedCharacter("1.0", 3, 4, Direction.Down, [born]));
            }

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                Assert.Equal([0, 31, 7, 22, 13, 30], Assert.Single(login.Character.Party).Ivs);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And a party saved before this column existed comes back perfect rather than
    /// empty-handed, because perfect is what it actually was.
    /// </summary>
    [Fact]
    public async Task AndAPartyFromBeforeThisComesBackPerfect()
    {
        string path = TempDatabase.Path();

        try
        {
            using (var writing = new SqlitePlayerStore(path))
            {
                await writing.RegisterAsync(
                    "Mason",
                    "a-good-password",
                    new SavedCharacter(
                        "1.0", 3, 4, Direction.Down,
                        [new SavedMon(16, 3, null, 8, StatusCondition.None, Nature.Bold, [33])]));
            }

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                Assert.True(Assert.Single(login.Character.Party).Born.IsPerfect);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }
}

/// <summary>
/// Two creatures, and what comes of leaving them together.
/// <para>
/// What genes are for. Six numbers nobody can change are a curiosity; six numbers a
/// player can work towards over generations are the reason to keep playing, the reason
/// one creature is worth more than another, and therefore the reason a market can exist.
/// </para>
/// <para>
/// Three fields make it possible and all three were already in the data and read by
/// nothing: the egg groups, the gender ratio, and how many cycles a species' eggs take.
/// What is modelled is the inheritance rule and the length of a cycle.
/// </para>
/// </summary>
public class LeavingThemTogetherTests
{
    private static SpeciesData Species(int index, EggGroup one, EggGroup two = EggGroup.None, byte ratio = 127) => new()
    {
        Index = index,
        Name = string.Empty,
        BaseHp = 60, BaseAttack = 60, BaseDefense = 60,
        BaseSpeed = 60, BaseSpAttack = 60, BaseSpDefense = 60,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        EggGroup1 = one, EggGroup2 = two,
        EggCycles = 20,
        GenderRatio = ratio,
    };

    private static SavedMon Mon(int species, params int[] ivs) =>
        new(species, 20, null, 20, StatusCondition.None, Nature.Hardy, [1]) { Ivs = ivs };

    private static GameRules Rules(params SpeciesData[] species) =>
        new(species, [new MoveData(1, "", 0, 40, PokemonType.Normal, 100, 20, 0, 0, 0)], [], [], []);

    [Fact]
    public void TwoOfAGroupAndOppositeSexesCan()
    {
        SpeciesData field = Species(1, EggGroup.Field);

        Assert.True(Breeding.CanBreed(field, Gender.Male, field, Gender.Female));
    }

    [Fact]
    public void AndTwoOfTheSameSexCannot()
    {
        SpeciesData field = Species(1, EggGroup.Field);

        Assert.False(Breeding.CanBreed(field, Gender.Male, field, Gender.Male));
    }

    [Fact]
    public void AndNothingInCommonCannot()
    {
        Assert.False(Breeding.CanBreed(
            Species(1, EggGroup.Field), Gender.Male,
            Species(2, EggGroup.Mineral), Gender.Female));
    }

    /// <summary>Anything breeds with the one that breeds with anything, and it cannot with itself.</summary>
    [Fact]
    public void AndTheOneThatBreedsWithAnythingDoes()
    {
        SpeciesData ditto = Species(132, EggGroup.Ditto, ratio: 255);
        SpeciesData field = Species(1, EggGroup.Field);

        Assert.True(Breeding.CanBreed(ditto, Gender.None, field, Gender.Male));
        Assert.True(Breeding.CanBreed(field, Gender.Female, ditto, Gender.None));
        Assert.False(Breeding.CanBreed(ditto, Gender.None, ditto, Gender.None));
    }

    /// <summary>And nothing in the group that has no eggs breeds at all, whatever it is put with.</summary>
    [Fact]
    public void AndNothingUndiscoveredBreedsAtAll()
    {
        SpeciesData legend = Species(150, EggGroup.Undiscovered, ratio: 255);

        Assert.False(Breeding.CanBreed(legend, Gender.None, Species(132, EggGroup.Ditto), Gender.None));
        Assert.False(Breeding.CanBreed(legend, Gender.None, legend, Gender.None));
    }

    /// <summary>Sex comes off the ratio, and the two ends of it are certainties.</summary>
    [Fact]
    public void SexComesOffTheRatio()
    {
        Assert.Equal(Gender.None, Breeding.SexOf(Species(1, EggGroup.Field, ratio: 255), new BattleRng(1)));
        Assert.Equal(Gender.Male, Breeding.SexOf(Species(1, EggGroup.Field, ratio: 0), new BattleRng(1)));
        Assert.Equal(Gender.Female, Breeding.SexOf(Species(1, EggGroup.Field, ratio: 254), new BattleRng(1)));

        var seen = new HashSet<Gender>();

        for (uint seed = 1; seed <= 60; seed++)
            seen.Add(Breeding.SexOf(Species(1, EggGroup.Field, ratio: 127), new BattleRng(seed)));

        Assert.Equal(2, seen.Count);
    }

    /// <summary>The egg is the mother's line wound back to the bottom of it.</summary>
    [Fact]
    public void TheEggIsTheBottomOfTheMothersLine()
    {
        GameRules rules = new(
            [Species(1, EggGroup.Field), Species(2, EggGroup.Field), Species(3, EggGroup.Field)],
            [new MoveData(1, "", 0, 40, PokemonType.Normal, 100, 20, 0, 0, 0)],
            [], [], [],
            [new Evolution(1, 1, 16, 2), new Evolution(2, 1, 32, 3)]);

        // The mother is the fully grown one; the egg is what she started as.
        Assert.Equal(1, Breeding.EggOf(rules, rules.SpeciesAt(3)!, Gender.Female, rules.SpeciesAt(1)!));
    }

    /// <summary>Three of the six come from the parents, and the rest is dice.</summary>
    [Fact]
    public void ThreeOfTheSixComeFromTheParents()
    {
        Genes mother = Genes.Of([31, 31, 31, 31, 31, 31]);
        Genes father = Genes.Of([31, 31, 31, 31, 31, 31]);

        for (uint seed = 1; seed <= 50; seed++)
        {
            Genes child = Breeding.Inherit(mother, father, new BattleRng(seed));

            Assert.True(child.Perfectly >= Breeding.Inherited, $"{child} took less than three from two perfect parents");
        }
    }

    /// <summary>And a child can be better than either parent, which is why anybody does it twice.</summary>
    [Fact]
    public void AndAChildCanBeatBothParents()
    {
        Genes mother = Genes.Of([31, 31, 31, 0, 0, 0]);
        Genes father = Genes.Of([0, 0, 0, 31, 31, 31]);

        bool better = false;

        for (uint seed = 1; seed <= 300 && !better; seed++)
            better = Breeding.Inherit(mother, father, new BattleRng(seed)).Perfectly > 3;

        Assert.True(better);
    }

    /// <summary>An egg is a creature at level one with the moves that species starts with.</summary>
    [Fact]
    public void AnEggIsACreatureAtLevelOne()
    {
        GameRules rules = Rules(Species(1, EggGroup.Field));

        SavedMon egg = Breeding.Egg(
            rules,
            Mon(1, 31, 31, 31, 0, 0, 0), Gender.Female,
            Mon(1, 0, 0, 0, 31, 31, 31), Gender.Male,
            new BattleRng(7))!;

        Assert.Equal(1, egg.Species);
        Assert.Equal(1, egg.Level);
        Assert.Equal(0, egg.Experience);
        Assert.Equal(6, egg.Ivs.Count);
    }

    /// <summary>And two that cannot breed make nothing rather than something.</summary>
    [Fact]
    public void AndTwoThatCannotMakeNothing()
    {
        GameRules rules = Rules(Species(1, EggGroup.Field), Species(2, EggGroup.Mineral));

        Assert.Null(Breeding.Egg(rules, Mon(1), Gender.Female, Mon(2), Gender.Male, new BattleRng(1)));
    }

    /// <summary>How long it takes is read off the record, times a modelled cycle.</summary>
    [Fact]
    public void HowLongItTakesIsRead()
    {
        Assert.Equal(20 * Breeding.StepsPerCycle, Breeding.StepsToHatch(Species(1, EggGroup.Field)));
    }
}

/// <summary>
/// Which sex a creature is, written down.
/// <para>
/// The ratio on a species record gives the chance, not the answer, so asking it twice
/// gives two answers — and a creature whose sex changed between questions is a creature
/// that could breed with itself on the second one. So it is rolled once, where the
/// creature is made, and stored from then on.
/// </para>
/// <para>
/// Nought means both "genderless" and "written down before anybody asked", which the
/// column cannot tell apart and does not try to. A MAGNEMITE and a creature caught last
/// week look the same, and breeding treats them the same way: it takes neither. The first
/// is right and the second is the honest cost of adding a field to a save that exists.
/// </para>
/// </summary>
public class WhichSexItIsTests
{
    /// <summary>A wild one is rolled, and both answers turn up.</summary>
    [Fact]
    public void AWildOneIsRolledOnceAndKept()
    {
        var factory = new BattleFactory(TestRules.All);

        var seen = new HashSet<Gender>();

        for (uint seed = 1; seed <= 60; seed++) seen.Add(factory.Wild(1, 10, new BattleRng(seed))!.Sex);

        Assert.Contains(Gender.Male, seen);
        Assert.Contains(Gender.Female, seen);
    }

    /// <summary>And one built with no dice has none recorded rather than a guess.</summary>
    [Fact]
    public void AndOneWithNoDiceHasNoneRecorded()
    {
        Assert.Equal(Gender.None, new BattleFactory(TestRules.All).Wild(1, 10)!.Sex);
    }

    /// <summary>It survives the save, and the process.</summary>
    [Fact]
    public async Task ItSurvivesTheProcess()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon hers = new(16, 3, null, 8, StatusCondition.None, Nature.Bold, [33])
            {
                Sex = Gender.Female,
            };

            using (var writing = new SqlitePlayerStore(path))
            {
                await writing.RegisterAsync(
                    "Mason",
                    "a-good-password",
                    new SavedCharacter("1.0", 3, 4, Direction.Down, [hers]));
            }

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                Assert.Equal(Gender.Female, Assert.Single(login.Character.Party).Sex);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And a creature written before the column existed has none, not a wrong one.</summary>
    [Fact]
    public async Task AndOneFromBeforeHasNoneRatherThanAWrongOne()
    {
        string path = TempDatabase.Path();

        try
        {
            using (var writing = new SqlitePlayerStore(path))
            {
                await writing.RegisterAsync(
                    "Mason",
                    "a-good-password",
                    new SavedCharacter(
                        "1.0", 3, 4, Direction.Down,
                        [new SavedMon(16, 3, null, 8, StatusCondition.None, Nature.Bold, [33])]));
            }

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                Assert.Equal(Gender.None, Assert.Single(login.Character.Party).Sex);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And a battler carries it back out to whoever saves it.</summary>
    [Fact]
    public void AndItGoesBackOutAgain()
    {
        var factory = new BattleFactory(TestRules.All);

        SavedMon his = new(1, 5, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
        {
            Sex = Gender.Male,
        };

        Assert.Equal(Gender.Male, BattleFactory.Save(factory.Restore(his)!).Sex);
    }
}

/// <summary>
/// The daycare's shelf.
/// <para>
/// A daycare holds creatures for hours of real time, so the one thing it must not do is
/// lose them. Holding them in memory on the server is the version that eats somebody's
/// creature the first time a process restarts, and a silent loss is this project's worst
/// known class of bug.
/// </para>
/// <para>
/// So the daycare is a third value in the column that already says whether a row is in
/// the party or the box. It inherits storage, loading, saving, the change comparison and
/// the migration from machinery that is already written and already tested — and a second
/// table, to hold two rows, would have been a second read path, a second write path and a
/// second set of bugs.
/// </para>
/// <para>
/// This is the test the whole feature turns on: what is left there is still there
/// afterwards.
/// </para>
/// </summary>
public class TheDaycareShelfTests
{
    private static SavedMon Mon(int species, Gender sex) =>
        new(species, 20, null, 20, StatusCondition.None, Nature.Hardy, [33]) { Sex = sex, Ivs = [31, 0, 0, 0, 0, 0] };

    [Fact]
    public async Task WhatIsLeftThereIsStillThereAfterwards()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter character = new(
                "1.0", 3, 4, Direction.Down, [Mon(1, Gender.Male)])
            {
                Box = [Mon(4, Gender.Male)],
                Daycare = [Mon(16, Gender.Female), Mon(19, Gender.Male)],
            };

            using (var writing = new SqlitePlayerStore(path))
                await writing.RegisterAsync("Mason", "a-good-password", character);

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                // Each list holds what it held, and nothing has wandered between them.
                Assert.Equal([1], login.Character.Party.Select(m => m.Species));
                Assert.Equal([4], login.Character.Box.Select(m => m.Species));
                Assert.Equal([16, 19], login.Character.Daycare.Select(m => m.Species));

                // And what was left there is the same creature, not a copy of the shape.
                Assert.Equal(Gender.Female, login.Character.Daycare[0].Sex);
                Assert.Equal([31, 0, 0, 0, 0, 0], login.Character.Daycare[1].Ivs);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And an empty daycare comes back empty rather than absent.</summary>
    [Fact]
    public async Task AndAnEmptyOneComesBackEmpty()
    {
        string path = TempDatabase.Path();

        try
        {
            using (var writing = new SqlitePlayerStore(path))
            {
                await writing.RegisterAsync(
                    "Mason", "a-good-password",
                    new SavedCharacter("1.0", 3, 4, Direction.Down, [Mon(1, Gender.Male)]));
            }

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                Assert.Empty(login.Character.Daycare);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And putting somebody in the daycare counts as a change, so the save that skips
    /// what has not changed does not skip this.
    /// </summary>
    [Fact]
    public void AndLeavingSomebodyThereIsAChange()
    {
        SavedCharacter before = new("1.0", 3, 4, Direction.Down, [Mon(1, Gender.Male)]);

        Assert.NotEqual(before, before with { Daycare = [Mon(16, Gender.Female)] });
    }
}

/// <summary>
/// The daycare itself: two on a shelf, a counter that only moves when somebody is
/// playing, and an egg at the end of it.
/// <para>
/// The step counter is the part worth arguing about. It counts the player's steps rather
/// than the world's clock, which means a daycare cannot hatch anything while its owner is
/// asleep — and that is the whole point. A wait measured in real time rewards logging off,
/// and a game whose endgame is breeding would then be a game whose endgame is not playing
/// it.
/// </para>
/// <para>
/// What is <b>read</b> here is how long the wait is: the egg cycles on the species record
/// the egg would be, which is a field this project extracted long ago and asked nothing
/// until there was a daycare to ask it.
/// </para>
/// </summary>
public class TheDaycareTests
{
    private const string Town = "1.0";

    /// <summary>
    /// Somewhere flat to walk, with a wall at (4, 4) so a refused step can be told from a
    /// taken one, and somebody standing on it who minds creatures.
    /// <para>
    /// The minder matters as much as the nurse does in the box tests: every one of these
    /// operations is refused anywhere else, so a fixture on bare ground would be testing
    /// the refusal and nothing else.
    /// </para>
    /// </summary>
    private static (GameWorld World, ServerPlayer Player) Walking(params SavedMon[] party) =>
        Standing(true, party);

    private static (GameWorld World, ServerPlayer Player) Standing(bool minder, params SavedMon[] party)
    {
        var collision = new byte[64];
        collision[(4 * 8) + 4] = 1;

        MapObject attendant = new(1, 61, 3, 3, Direction.Down, 0, false) { MindsCreatures = true, Talks = true };

        MapData map = new(Town, "PALLET TOWN", 8, 8, collision)
        {
            Objects = minder ? [attendant] : [],
        };

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));

        player.Party = [.. party];
        world.Operators.Add(player.Name);

        return (world, player);
    }

    private static SavedMon Mon(int species, Gender sex, int level = 20) =>
        new(species, level, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
        {
            Sex = sex,
            Ivs = [31, 31, 31, 31, 31, 31],
        };

    [Fact]
    public void EveryStepIsCounted()
    {
        (GameWorld world, ServerPlayer player) = Walking(Mon(1, Gender.Male));

        Assert.Equal(0, player.Steps);

        world.Move(player.Id, Direction.Left, 10);
        world.Move(player.Id, Direction.Left, 20);

        Assert.Equal(2, player.Steps);
    }

    /// <summary>
    /// And a step that did not happen is not one. This is the reason the count is taken
    /// where an arrival is handled rather than where a move is asked for: a player walking
    /// into a wall is holding a direction down, and a counter that believed them would run
    /// at the speed of the key repeat.
    /// </summary>
    [Fact]
    public void AndARefusedStepIsNot()
    {
        (GameWorld world, ServerPlayer player) = Walking(Mon(1, Gender.Male));

        world.Move(player.Id, Direction.Right, 10);
        world.Move(player.Id, Direction.Right, 20);

        // (3, 4) to (4, 4) is the wall, so the first one turns and the second is refused.
        Assert.Equal(new GridPosition(3, 4), player.Square);
        Assert.Equal(0, player.Steps);
    }

    [Fact]
    public void OneLeftThereIsOutOfThePartyAndComesBack()
    {
        (GameWorld world, ServerPlayer player) = Walking(Mon(1, Gender.Male), Mon(2, Gender.Female));

        world.LeaveAtDaycare(player.Id, 1);

        Assert.Equal([1], player.Party.Select(m => m.Species));
        Assert.Equal([2], player.Daycare.Select(m => m.Species));

        world.TakeFromDaycare(player.Id, 0);

        Assert.Equal([1, 2], player.Party.Select(m => m.Species));
        Assert.Empty(player.Daycare);
    }

    /// <summary>
    /// The last one able to fight never goes. A daycare makes this worse than a box does:
    /// a box can be opened at the next machine, and what is left here is meant to stay.
    /// </summary>
    [Fact]
    public void TheLastOneThatCanFightStays()
    {
        (GameWorld world, ServerPlayer player) = Walking(Mon(1, Gender.Male));

        world.LeaveAtDaycare(player.Id, 0);

        Assert.Single(player.Party);
        Assert.Empty(player.Daycare);
        Assert.Contains("last one that can fight", world.LastDaycare ?? "");
    }

    [Fact]
    public void AThirdOneWillNotFit()
    {
        (GameWorld world, ServerPlayer player) = Walking(
            Mon(1, Gender.Male), Mon(2, Gender.Female), Mon(3, Gender.Male), Mon(4, Gender.Female));

        world.LeaveAtDaycare(player.Id, 3);
        world.LeaveAtDaycare(player.Id, 2);
        world.LeaveAtDaycare(player.Id, 1);

        Assert.Equal(TheDaycareSize, player.Daycare.Count);
        Assert.Contains("full", world.LastDaycare ?? "");
    }

    private static int TheDaycareSize => GameWorld.DaycareSize;

    /// <summary>
    /// A pair who can breed start a clock, and the length of it is read off the species
    /// record rather than chosen here.
    /// </summary>
    [Fact]
    public void APairStartsAClockOfTheLengthTheirEggTakes()
    {
        (GameWorld world, ServerPlayer player) = Walking(
            Mon(1, Gender.Male), Mon(2, Gender.Female), Mon(3, Gender.Male));

        world.LeaveAtDaycare(player.Id, 2);

        Assert.Equal(0, player.EggAt);

        world.LeaveAtDaycare(player.Id, 1);

        Assert.Equal(
            Breeding.StepsToHatch(TestRules.All.SpeciesAt(1)!),
            player.EggAt - player.Steps);
    }

    /// <summary>Two of the same sex are two creatures sharing a room and nothing more.</summary>
    [Fact]
    public void TwoWhoCannotBreedStartNothing()
    {
        (GameWorld world, ServerPlayer player) = Walking(
            Mon(1, Gender.Male), Mon(2, Gender.Male), Mon(3, Gender.Male));

        world.LeaveAtDaycare(player.Id, 2);
        world.LeaveAtDaycare(player.Id, 1);

        Assert.Equal(TheDaycareSize, player.Daycare.Count);
        Assert.Equal(0, player.EggAt);
    }

    /// <summary>
    /// And taking either one back stops the clock, so the wait belongs to the pair rather
    /// than to the building. Without this a player could walk the long half of it with one
    /// parent and swap a better one in at the end.
    /// </summary>
    [Fact]
    public void TakingOneBackStopsTheClock()
    {
        (GameWorld world, ServerPlayer player) = Walking(
            Mon(1, Gender.Male), Mon(2, Gender.Female), Mon(3, Gender.Male));

        world.LeaveAtDaycare(player.Id, 2);
        world.LeaveAtDaycare(player.Id, 1);

        Assert.True(player.EggAt > 0);

        world.TakeFromDaycare(player.Id, 0);

        Assert.Equal(0, player.EggAt);
    }

    /// <summary>
    /// The egg, on the step it is due: in the box, at level one, and wound back to what
    /// the mother was before she was anything else.
    /// </summary>
    [Fact]
    public void TheEggArrivesOnTheStepItIsDue()
    {
        // The mother is species 2, which species 1 becomes. So the egg is a 1.
        (GameWorld world, ServerPlayer player) = Walking(
            Mon(1, Gender.Male), Mon(2, Gender.Female), Mon(3, Gender.Male));

        world.LeaveAtDaycare(player.Id, 2);
        world.LeaveAtDaycare(player.Id, 1);

        // Walked rather than waited. Setting the counter one short of the due step is the
        // same arithmetic as five thousand steps and does not take five thousand steps.
        player.Steps = player.EggAt - 1;

        List<Outgoing> stepped = world.Move(player.Id, Direction.Left, 10);

        SavedMon egg = Assert.Single(player.Box);

        Assert.Equal(1, egg.Species);
        Assert.Equal(1, egg.Level);
        Assert.Contains("egg", Said(stepped), StringComparison.OrdinalIgnoreCase);

        // And the clock starts again, because a pair left together goes on producing.
        Assert.True(player.EggAt > player.Steps);
    }

    /// <summary>
    /// Three of the six come from the parents. Both are perfect here, so a child of theirs
    /// has at least three thirty-ones however the dice fall — which is the property that
    /// makes a chain of these worth running, and the one an inheritance that quietly
    /// rolled everything fresh would fail.
    /// </summary>
    [Fact]
    public void AndItIsBornWithSomeOfWhatItsParentsHad()
    {
        (GameWorld world, ServerPlayer player) = Walking(
            Mon(1, Gender.Male), Mon(2, Gender.Female), Mon(3, Gender.Male));

        world.LeaveAtDaycare(player.Id, 2);
        world.LeaveAtDaycare(player.Id, 1);

        player.Steps = player.EggAt - 1;
        world.Move(player.Id, Direction.Left, 10);

        Assert.True(Assert.Single(player.Box).Ivs.Count(v => v == 31) >= Breeding.Inherited);
    }

    /// <summary>
    /// A full box does not lose the egg. Nothing is made, nothing is said, the due step
    /// stays where it is, and the next step after a slot opens delivers it — which is the
    /// difference between a player who is waiting and a player who was robbed.
    /// </summary>
    [Fact]
    public void AFullBoxDoesNotLoseTheEgg()
    {
        (GameWorld world, ServerPlayer player) = Walking(
            Mon(1, Gender.Male), Mon(2, Gender.Female), Mon(3, Gender.Male));

        world.LeaveAtDaycare(player.Id, 2);
        world.LeaveAtDaycare(player.Id, 1);

        player.Box = [.. Enumerable.Range(10, world.Storage).Select(s => Mon(s, Gender.Male))];

        int due = player.EggAt;

        player.Steps = due - 1;
        world.Move(player.Id, Direction.Left, 10);

        Assert.Equal(world.Storage, player.Box.Count);
        Assert.Equal(due, player.EggAt);

        // Room, and then the very next step.
        player.Box.RemoveAt(0);
        world.Move(player.Id, Direction.Left, 20);

        Assert.Equal(world.Storage, player.Box.Count);
        Assert.True(player.EggAt > due);
    }

    /// <summary>
    /// And the counter and the clock survive a restart, which is the whole reason they are
    /// numbers on a save rather than numbers on a session. A wait measured in thousands of
    /// steps that resets when somebody closes the game is a wait nobody would ever finish.
    /// </summary>
    [Fact]
    public async Task TheCounterAndTheClockSurviveARestart()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedCharacter character = new("1.0", 3, 4, Direction.Down, [Mon(1, Gender.Male)])
            {
                Daycare = [Mon(2, Gender.Female), Mon(3, Gender.Male)],
                Steps = 4_312,
                EggAt = 9_000,
            };

            using (var writing = new SqlitePlayerStore(path))
                await writing.RegisterAsync("Mason", "a-good-password", character);

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                Assert.Equal(4_312, login.Character.Steps);
                Assert.Equal(9_000, login.Character.EggAt);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And a save written before either column existed reads back as nought.</summary>
    [Fact]
    public void AndWalkingIsAChangeWorthWritingDown()
    {
        SavedCharacter before = new("1.0", 3, 4, Direction.Down, [Mon(1, Gender.Male)]);

        Assert.Equal(0, before.Steps);
        Assert.Equal(0, before.EggAt);
        Assert.NotEqual(before, before with { Steps = 1 });
        Assert.NotEqual(before, before with { EggAt = 5_100 });
    }

    /// <summary>The console is the only door onto any of this today, so it is tested.</summary>
    [Fact]
    public void TheConsoleCanLeaveOneAndTakeItBack()
    {
        (GameWorld world, ServerPlayer player) = Walking(
            Mon(1, Gender.Male), Mon(2, Gender.Female), Mon(3, Gender.Male));

        world.RunConsole(player.Id, "/daycare leave 2");
        world.RunConsole(player.Id, "/daycare leave 1");

        Assert.Equal(TheDaycareSize, player.Daycare.Count);

        Assert.Contains("an egg in", Said(world.RunConsole(player.Id, "/daycare")));

        world.RunConsole(player.Id, "/daycare take 0");

        Assert.Single(player.Daycare);
        Assert.Contains("it takes two", Said(world.RunConsole(player.Id, "/daycare")));
    }

    /// <summary>
    /// And nowhere else. The gate is the same shape as the box's — a room with the right
    /// person in it — and without it the console would be a way to leave creatures in the
    /// middle of a route with nobody to mind them.
    /// </summary>
    [Fact]
    public void NobodyMindsThemInTheMiddleOfNowhere()
    {
        (GameWorld world, ServerPlayer player) = Standing(
            false, Mon(1, Gender.Male), Mon(2, Gender.Female));

        world.LeaveAtDaycare(player.Id, 1);

        Assert.Empty(player.Daycare);
        Assert.Equal(2, player.Party.Count);
        Assert.Contains("nobody here minds creatures", world.LastDaycare ?? "");
    }

    /// <summary>
    /// And taking one back needs the place too, which is the half of this that is easy to
    /// forget: a shelf you can empty from anywhere is a shelf with no reason to be visited.
    /// </summary>
    [Fact]
    public void AndTheyCannotBeCollectedFromAnywhereEither()
    {
        (GameWorld world, ServerPlayer player) = Walking(
            Mon(1, Gender.Male), Mon(2, Gender.Female), Mon(3, Gender.Male));

        world.LeaveAtDaycare(player.Id, 2);

        (GameWorld elsewhere, ServerPlayer walker) = Standing(false, Mon(1, Gender.Male));

        walker.Daycare = [.. player.Daycare];

        elsewhere.TakeFromDaycare(walker.Id, 0);

        Assert.Single(walker.Daycare);
        Assert.Contains("nobody here is minding them", elsewhere.LastDaycare ?? "");
    }

    /// <summary>
    /// Talking to the attendant shows the shelf, the way talking to a shopkeeper shows the
    /// stock. Nothing is asked for and nothing is taken — a player has to see what is there
    /// before they can decide anything about it.
    /// </summary>
    [Fact]
    public void TalkingToTheAttendantShowsTheShelf()
    {
        (GameWorld world, ServerPlayer player) = Walking(
            Mon(1, Gender.Male), Mon(2, Gender.Female), Mon(3, Gender.Male));

        world.LeaveAtDaycare(player.Id, 2);
        world.LeaveAtDaycare(player.Id, 1);

        player.Facing = Direction.Up;

        DaycareUpdated shown = Assert.Single(
            world.StartTalking(player.Id, 1).Select(o => o.Message).OfType<DaycareUpdated>());

        Assert.Equal(2, shown.Minded.Count);
        Assert.Equal(GameWorld.DaycareSize, shown.Holds);

        // A distance rather than the step it lands on, because that is the number a player
        // can act on without being told the counter as well.
        Assert.Equal(Breeding.StepsToHatch(TestRules.All.SpeciesAt(1)!), shown.StepsToEgg);
    }

    /// <summary>And a pair that will never produce anything says nothing is coming.</summary>
    [Fact]
    public void AndSaysNothingIsComingWhenNothingIs()
    {
        (GameWorld world, ServerPlayer player) = Walking(
            Mon(1, Gender.Male), Mon(2, Gender.Male), Mon(3, Gender.Male));

        world.LeaveAtDaycare(player.Id, 2);
        world.LeaveAtDaycare(player.Id, 1);

        player.Facing = Direction.Up;

        DaycareUpdated shown = Assert.Single(
            world.StartTalking(player.Id, 1).Select(o => o.Message).OfType<DaycareUpdated>());

        Assert.Equal(0, shown.StepsToEgg);
    }

    /// <summary>
    /// And the flag survives the world file, which is the only way it ever reaches a
    /// server: the locator runs at export, on a machine with a cartridge, and everything
    /// after that is reading a number out of a file.
    /// <para>
    /// Worth its own test because the failure mode is silent. A field written and not read
    /// — or read in the wrong order — does not throw; it shifts every field after it, and
    /// the first symptom is a shopkeeper who heals.
    /// </para>
    /// </summary>
    [Fact]
    public void TheFlagSurvivesTheWorldFile()
    {
        MapObject attendant = new(1, 61, 3, 3, Direction.Down, 0, false) { MindsCreatures = true, Talks = true };
        MapObject bystander = new(2, 61, 5, 3, Direction.Down, 0, false) { Talks = true };

        var written = new WorldData([
            new MapData(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = [attendant, bystander] },
        ]);

        using var file = new MemoryStream();

        written.Save(file);
        file.Position = 0;

        MapData read = Assert.Single(WorldData.Load(file).Maps);

        Assert.True(read.Objects.Single(o => o.LocalId == 1).MindsCreatures);
        Assert.False(read.Objects.Single(o => o.LocalId == 2).MindsCreatures);
    }

    /// <summary>
    /// And the client is told, because a rule enforced on one side of the split needs its
    /// counterpart on the other. Without this the only way to discover that somebody minds
    /// creatures is to walk up to them and be refused by everybody who does not.
    /// </summary>
    [Fact]
    public void AndThePeopleTheServerPlacesSayWhoMinds()
    {
        (GameWorld world, ServerPlayer player) = Walking(Mon(1, Gender.Male));

        (_, List<Outgoing> send) = world.Join(2, "Koop", SavedCharacter.Fresh(Town, 3, 5));

        ObjectView placed = Assert.Single(
            send.Select(o => o.Message).OfType<ObjectsPlaced>().SelectMany(p => p.Objects));

        Assert.True(placed.MindsCreatures);
        Assert.Equal(1, player.Id);
    }

    private static string Said(IEnumerable<Outgoing> from) =>
        string.Join("\n", from.Select(o => o.Message).OfType<ConsoleReply>().Select(r => r.Text)) +
        "\n" +
        string.Join("\n", from.Select(o => o.Message).OfType<DaycareUpdated>().Select(b => b.Message)) +
        "\n" +
        string.Join("\n", from.Select(o => o.Message).OfType<BoxUpdated>().Select(b => b.Message));
}
