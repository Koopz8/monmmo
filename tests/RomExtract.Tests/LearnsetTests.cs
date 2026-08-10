using PokeMmo.Core.Battle;

namespace PokeMmo.RomExtract.Tests;

public class LevelUpMoveTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 33)]
    [InlineData(100, 354)]
    [InlineData(64, 511)]
    public void PacksAndUnpacksToTheSameEntry(int level, int move)
    {
        var entry = new LevelUpMove(level, move);
        LevelUpMove round = LevelUpMove.Decode(entry.Encode());

        Assert.Equal(entry, round);
    }

    [Fact]
    public void UsesTheLowNineBitsForTheMove()
    {
        // The packing is what makes 511 the ceiling on move ids, which is why this
        // generation's table stops where it does.
        LevelUpMove entry = LevelUpMove.Decode(0b0000_0111_0000_0001);

        Assert.Equal(3, entry.Level);
        Assert.Equal(257, entry.MoveId);
    }

    [Fact]
    public void TheTerminatorIsNotAPlausibleEntry()
    {
        LevelUpMove entry = LevelUpMove.Decode(LevelUpMove.Terminator);

        // Level 127 is past anything the games allow, which is part of why 0xFFFF is
        // safe to use as an end marker.
        Assert.True(entry.Level > 100);
    }
}

public class LearnsetTests
{
    private static Learnset Learnset(params (int Level, int Move)[] moves) =>
        new(1, moves.Select(m => new LevelUpMove(m.Level, m.Move)).ToList());

    [Fact]
    public void KnowsNothingLearnedAboveItsLevel()
    {
        Learnset learnset = Learnset((1, 10), (7, 20), (13, 30));

        Assert.Equal(new[] { 10, 20 }, learnset.MovesKnownAt(7));
    }

    [Fact]
    public void KeepsOnlyTheLastFourLearned()
    {
        Learnset learnset = Learnset((1, 10), (5, 20), (9, 30), (13, 40), (17, 50), (21, 60));

        // A creature only holds four moves, and the games fill a wild one with the
        // most recent four rather than the first four.
        Assert.Equal(new[] { 30, 40, 50, 60 }, learnset.MovesKnownAt(50));
    }

    [Fact]
    public void KnowsNothingBelowItsFirstMove()
    {
        Learnset learnset = Learnset((10, 10));

        Assert.Empty(learnset.MovesKnownAt(5));
    }
}

public class LearnsetExtractorTests
{
    private static readonly SyntheticRom Fixture = new();

    [Fact]
    public void FindsTheTableWithoutBeingToldWhereItIs()
    {
        int? table = LearnsetExtractor.LocateTable(Fixture.ToRom());

        Assert.Equal(SyntheticRom.LearnsetTableOffset, table);
    }

    [Fact]
    public void ReadsEveryPopulatedSpecies()
    {
        Dictionary<int, Learnset> learnsets = LearnsetExtractor.Extract(Fixture.ToRom());

        int expected = Enumerable
            .Range(0, LearnsetExtractor.DefaultSpeciesCount)
            .Count(SyntheticRom.SpeciesHasLearnset);

        Assert.Equal(expected, learnsets.Count);
    }

    [Fact]
    public void RecoversEntriesExactly()
    {
        Dictionary<int, Learnset> learnsets = LearnsetExtractor.Extract(Fixture.ToRom());

        foreach (int species in new[] { 0, 1, 5, 150, 251, 277, 411 })
        {
            Assert.True(learnsets.ContainsKey(species), $"species {species} missing");
            Assert.Equal(SyntheticRom.LearnsetFor(species), learnsets[species].Moves);
        }
    }

    [Fact]
    public void StepsOverTheUnusedSlotsWithoutShiftingWhatFollows()
    {
        Dictionary<int, Learnset> learnsets = LearnsetExtractor.Extract(Fixture.ToRom());

        for (int species = SyntheticRom.FirstUnusedSpecies; species <= SyntheticRom.LastUnusedSpecies; species++)
            Assert.False(learnsets.ContainsKey(species), $"species {species} should be empty");

        // The species immediately after the gap is the one that would be wrong if the
        // reader had treated an empty learnset as the end of the table.
        int after = SyntheticRom.LastUnusedSpecies + 1;
        Assert.Equal(SyntheticRom.LearnsetFor(after), learnsets[after].Moves);
    }
}
