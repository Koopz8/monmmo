using PokeMmo.RomExtract.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Which entry of the cry table belongs to which creature, which is not the species number.
/// <para>
/// A real cartridge names 412 species and carries 388 cries, and the twenty-four missing are
/// not missing from the end. They are a block in the middle of the numbering carrying no
/// creature at all — slots that share one placeholder name — and the cry table skips them.
/// </para>
/// <para>
/// So reading the table by species number is right about every early creature and wrong about
/// more than a hundred later ones, which is the worst way for something to be wrong: it works
/// for as long as anybody is likely to be testing it.
/// </para>
/// </summary>
public class WhichEntryIsWhoseTests
{
    private static IReadOnlyList<string> Names(int count, int gapFrom, int gapLength) =>
    [
        .. Enumerable.Range(0, count)
            .Select(i => i >= gapFrom && i < gapFrom + gapLength ? "?" : $"MON{i:D3}"),
    ];

    /// <summary>
    /// The block is found by what it looks like — a run of one name — rather than by number.
    /// </summary>
    [Fact]
    public void TheBlockIsFoundByItsRepeatedName()
    {
        (_, CryIndexResult found) = CryIndex.Derive(Names(412, 252, 25), 386);

        Assert.Equal(252, found.GapFrom);
        Assert.Equal(25, found.GapLength);
        Assert.Equal("?", found.GapName);
    }

    /// <summary>A creature before the block sits at its own number, less one.</summary>
    [Fact]
    public void ACreatureBeforeTheBlockSitsAtItsOwnNumberLessOne()
    {
        (Dictionary<int, int> at, _) = CryIndex.Derive(Names(412, 252, 25), 386);

        Assert.Equal(0, at[1]);
        Assert.Equal(250, at[251]);
    }

    /// <summary>
    /// And one after it sits that many places earlier again, which is the whole point.
    /// </summary>
    [Fact]
    public void AndOneAfterItSitsTheBlockEarlierAgain()
    {
        (Dictionary<int, int> at, _) = CryIndex.Derive(Names(412, 252, 25), 386);

        // The first creature after the block follows the last one before it.
        Assert.Equal(251, at[277]);

        // And the last species named lands on the last entry, which is the arithmetic
        // closing: 412 species less 25 empty less the unused nought is 386.
        Assert.Equal(385, at[411]);
    }

    /// <summary>A slot inside the block is not a creature and has no noise.</summary>
    [Fact]
    public void ASlotInsideTheBlockHasNoNoise()
    {
        (Dictionary<int, int> at, _) = CryIndex.Derive(Names(412, 252, 25), 386);

        for (int species = 252; species < 277; species++)
            Assert.False(at.ContainsKey(species), $"species {species} is a placeholder and got an entry");
    }

    /// <summary>
    /// And neither is species nought, which is the slot the game uses to mean nothing.
    /// </summary>
    [Fact]
    public void AndNeitherIsSpeciesNought()
    {
        (Dictionary<int, int> at, _) = CryIndex.Derive(Names(412, 252, 25), 386);

        Assert.False(at.ContainsKey(0));
    }

    /// <summary>
    /// A species the table is too short to reach gets nothing rather than somebody else's
    /// noise, and it is counted rather than dropped quietly.
    /// </summary>
    [Fact]
    public void ASpeciesTheTableCannotReachIsCounted()
    {
        (Dictionary<int, int> at, CryIndexResult found) = CryIndex.Derive(Names(412, 252, 25), 300);

        Assert.Equal(86, found.Unreachable);
        Assert.Equal(300, found.Mapped);
        Assert.False(at.ContainsKey(411));

        // Nought is the number that says the arithmetic and the file agree, so it has to be
        // reachable — otherwise this count could never be good news.
        (_, CryIndexResult exact) = CryIndex.Derive(Names(412, 252, 25), 386);

        Assert.Equal(0, exact.Unreachable);
    }

    /// <summary>
    /// A cartridge with no such block maps every species to its own number less one, and
    /// says that is what it did.
    /// </summary>
    [Fact]
    public void ACartridgeWithNoBlockMapsStraightThrough()
    {
        (Dictionary<int, int> at, CryIndexResult found) = CryIndex.Derive(Names(400, 0, 0), 399);

        Assert.True(found.NoGap);
        Assert.Equal(0, at[1]);
        Assert.Equal(398, at[399]);
    }

    /// <summary>
    /// Two names next to each other are a coincidence rather than a block. A cartridge has
    /// one run of placeholders and a great many pairs of similar names.
    /// </summary>
    [Fact]
    public void TwoNamesTheSameAreNotABlock()
    {
        (_, CryIndexResult found) = CryIndex.Derive(Names(400, 100, 2), 399);

        Assert.True(found.NoGap);
    }

    /// <summary>
    /// And the longest run wins, so a shorter accidental run does not become the block.
    /// </summary>
    [Fact]
    public void AndTheLongestRunWins()
    {
        List<string> names = [.. Names(412, 252, 25)];

        // A shorter run of something else, earlier in the list.
        for (int i = 40; i < 46; i++) names[i] = "SAME";

        (_, CryIndexResult found) = CryIndex.Derive(names, 386);

        Assert.Equal(252, found.GapFrom);
        Assert.Equal(25, found.GapLength);
    }

    // ---- against the fixture ---------------------------------------------------------

    /// <summary>
    /// And it works off the names the cartridge actually carries rather than a list written
    /// here — the fixture has a block of its own, and the numbers close.
    /// </summary>
    [Fact]
    public void ItWorksOffTheCartridgesOwnNames()
    {
        var synthetic = new SyntheticRom();

        IReadOnlyList<string> names =
        [
            .. Enumerable.Range(0, SyntheticRom.SpeciesCount).Select(SyntheticRom.NameFor),
        ];

        int creatures = SyntheticRom.SpeciesCount - SyntheticRom.UnusedSpeciesCount - 1;

        (Dictionary<int, int> at, CryIndexResult found) = CryIndex.Derive(names, creatures);

        Assert.Equal(SyntheticRom.UnusedSpeciesFrom, found.GapFrom);
        Assert.Equal(SyntheticRom.UnusedSpeciesCount, found.GapLength);
        Assert.Equal(0, found.Unreachable);
        Assert.Equal(creatures, at.Count);

        // And the cartridge really is being read, rather than the fixture's constants being
        // compared with themselves: the same names come back off the file.
        Rom rom = synthetic.ToRom();

        Assert.Equal(SyntheticRom.UnusedSpeciesName, NameOnTheCartridge(rom, SyntheticRom.UnusedSpeciesFrom));
    }

    private static string NameOnTheCartridge(Rom rom, int species)
    {
        int at = SyntheticRom.SpeciesNamesOffset + species * GameText.SpeciesNameLength;

        return GameText.Decode(
            rom.Slice(at, GameText.SpeciesNameLength));
    }
}
