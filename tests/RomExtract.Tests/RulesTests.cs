using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract.Tests;

public class GameRulesTests
{
    private static GameRules Sample() => new(
        [
            new SpeciesData
            {
                Index = 16,
                Name = "PIDGEY",
                BaseHp = 40, BaseAttack = 45, BaseDefense = 40,
                BaseSpeed = 56, BaseSpAttack = 35, BaseSpDefense = 35,
                Type1 = PokemonType.Normal, Type2 = PokemonType.Flying,
                CatchRate = 255, ExpYield = 55, GenderRatio = 127,
                GrowthRate = GrowthRate.MediumSlow,
            },
        ],
        [
            new MoveData(33, "TACKLE", 0, 35, PokemonType.Normal, 95, 35, 0, 0, 0),
            new MoveData(98, "QUICK ATTACK", 0, 40, PokemonType.Normal, 100, 30, 0, 0, 1),
        ],
        [
            new Learnset(16, [new LevelUpMove(1, 33), new LevelUpMove(9, 98)]),
        ]);

    private static GameRules RoundTrip(GameRules rules)
    {
        using var buffer = new MemoryStream();
        rules.Save(buffer);

        buffer.Position = 0;
        return GameRules.Load(buffer);
    }

    [Fact]
    public void EveryNumberSurvivesASaveAndLoad()
    {
        GameRules loaded = RoundTrip(Sample());

        SpeciesData species = loaded.SpeciesAt(16)!;

        Assert.Equal(40, species.BaseHp);
        Assert.Equal(56, species.BaseSpeed);
        Assert.Equal(PokemonType.Flying, species.Type2);
        Assert.Equal(255, species.CatchRate);
        Assert.Equal(GrowthRate.MediumSlow, species.GrowthRate);

        MoveData move = loaded.MoveAt(98)!;

        Assert.Equal(40, move.Power);
        Assert.Equal(1, move.Priority);
        Assert.Equal(PokemonType.Normal, move.Type);
    }

    [Fact]
    public void NoNameSurvivesASaveAndLoad()
    {
        // The whole point of the format. Names are cartridge text; base stats are
        // arithmetic. A server holding one and not the other is the line this project
        // is built around, so it is asserted rather than assumed.
        GameRules loaded = RoundTrip(Sample());

        Assert.Equal(string.Empty, loaded.SpeciesAt(16)!.Name);
        Assert.Equal(string.Empty, loaded.MoveAt(33)!.Name);
    }

    [Fact]
    public void LearnsetsSurviveWithTheirLevels()
    {
        GameRules loaded = RoundTrip(Sample());

        Learnset learnset = loaded.LearnsetOf(16)!;

        Assert.Equal(2, learnset.Moves.Count);
        Assert.Equal(new LevelUpMove(9, 98), learnset.Moves[1]);
        Assert.Equal(new[] { 33 }, loaded.MovesKnownAt(16, 5).Select(m => m.Id));
        Assert.Equal(new[] { 33, 98 }, loaded.MovesKnownAt(16, 20).Select(m => m.Id));
    }

    [Fact]
    public void AskingForSomethingThatIsNotThereGivesNothing()
    {
        GameRules loaded = RoundTrip(Sample());

        Assert.Null(loaded.SpeciesAt(999));
        Assert.Null(loaded.MoveAt(999));
        Assert.Null(loaded.LearnsetOf(999));
        Assert.Empty(loaded.MovesKnownAt(999, 50));
    }

    [Fact]
    public void SomethingThatIsNotARulesFileIsRefused()
    {
        using var buffer = new MemoryStream("MONWORLD\0\0\0"u8.ToArray());

        Assert.Throws<InvalidDataException>(() => GameRules.Load(buffer));
    }

    [Fact]
    public void AnImplausibleCountIsRefusedRatherThanAllocated()
    {
        // A single corrupted byte in a length should fail loudly, not spend a minute
        // allocating for a species table with two billion entries.
        using var buffer = new MemoryStream();
        Sample().Save(buffer);

        byte[] bytes = buffer.ToArray();

        // The species count sits immediately after the magic and the version.
        BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 12);

        using var corrupted = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => GameRules.Load(corrupted));
    }
}

public class RulesExportTests
{
    private static readonly GameRules Exported = RulesExporter.Export(new SyntheticRom().ToRom());

    [Fact]
    public void ExportsWhatTheCartridgeHolds()
    {
        SpeciesData? species = Exported.SpeciesAt(SyntheticRom.TestSpecies);

        Assert.NotNull(species);
        Assert.True(species.BaseHp > 0);
        Assert.Equal(SyntheticRom.LearnsetFor(SyntheticRom.TestSpecies), Exported.LearnsetOf(SyntheticRom.TestSpecies)!.Moves);
    }

    [Fact]
    public void ExportsNoNames()
    {
        // The extractor reads names perfectly well; this file deliberately drops them.
        // Stripping at export rather than at write means anything holding a record
        // afterwards holds the same thing the server will.
        for (int index = 0; index < 32; index++)
        {
            if (Exported.SpeciesAt(index) is { } species)
                Assert.Equal(string.Empty, species.Name);
        }
    }

    [Fact]
    public void SurvivesTheRoundTripToDiskAndBack()
    {
        using var buffer = new MemoryStream();
        Exported.Save(buffer);

        buffer.Position = 0;
        GameRules loaded = GameRules.Load(buffer);

        Assert.Equal(Exported.SpeciesCount, loaded.SpeciesCount);
        Assert.Equal(Exported.LearnsetCount, loaded.LearnsetCount);
        Assert.Equal(
            Exported.SpeciesAt(SyntheticRom.TestSpecies)!.BaseAttack,
            loaded.SpeciesAt(SyntheticRom.TestSpecies)!.BaseAttack);
    }

    [Fact]
    public void CarriesEnoughToDecideABattle()
    {
        // What a server actually needs: stats to compute damage from, and a catch rate
        // to throw against. A file that loads but has neither would make every attack
        // do nothing and every throw fail, which reads as a broken battle engine.
        SpeciesData species = Exported.SpeciesAt(SyntheticRom.TestSpecies)!;

        Assert.True(species.CatchRate > 0);
        Assert.True(species.BaseAttack > 0);
        Assert.True(species.BaseDefense > 0);

        // Not "not zero" — Normal is zero, and asserting against it would be a test
        // that quietly demands every species be something other than a Normal type.
        Assert.True(Enum.IsDefined(species.Type1));
        Assert.True(Enum.IsDefined(species.Type2));
    }
}
