using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a wild one walks out of the grass carrying.
/// <para>
/// Every species record on this cartridge names up to two items, and 112 of them name at
/// least one. Both numbers have been extracted since the species table was first located
/// and read by nothing — the fourth field in five milestones to turn out to be already in
/// the data and simply unused.
/// </para>
/// <para>
/// It closes a loop that was open at both ends. <c>Battler.Holding</c> existed, THIEF and
/// COVET could take an item off somebody, and <c>SavedMon.HeldItem</c> kept one — and
/// nothing anywhere was ever carrying anything, so all three were machinery for an event
/// that could not happen.
/// </para>
/// <para>
/// The one thing not read is how often. The two rates are modelled, in one place, and
/// marked as such where they are written.
/// </para>
/// </summary>
public class WhatTheyAreCarryingTests
{
    private const int Common = 101;
    private const int Rare = 202;

    private static SpeciesData Carrying(int index, int common, int rare) => new()
    {
        Index = index,
        Name = string.Empty,
        BaseHp = 45, BaseAttack = 49, BaseDefense = 49,
        BaseSpeed = 45, BaseSpAttack = 65, BaseSpDefense = 65,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Item1 = (ushort)common,
        Item2 = (ushort)rare,
    };

    /// <summary>Whatever a hundred rolls produce, it is one of the two or nothing.</summary>
    [Fact]
    public void AWildOneCarriesOneOfItsOwnTwoOrNothing()
    {
        var factory = new BattleFactory(TestRules.All);
        var species = Carrying(1, Common, Rare);

        var seen = new HashSet<int>();

        for (uint seed = 1; seed <= 200; seed++) seen.Add(factory.HeldBy(species, new BattleRng(seed)));

        Assert.Subset(new HashSet<int> { 0, Common, Rare }, seen);
    }

    /// <summary>And a species naming nothing carries nothing, however many times it is asked.</summary>
    [Fact]
    public void AndOneNamingNothingCarriesNothing()
    {
        var factory = new BattleFactory(TestRules.All);
        var species = Carrying(1, 0, 0);

        for (uint seed = 1; seed <= 50; seed++)
            Assert.Equal(0, factory.HeldBy(species, new BattleRng(seed)));
    }

    /// <summary>
    /// The rare slot is asked first, so a species naming both can still produce the rarer
    /// one. Asked the other way round, the rare item would be unreachable every time the
    /// common roll succeeded — which is most of the time.
    /// </summary>
    [Fact]
    public void AndTheRareOneIsStillReachableWhenThereIsACommonOneToo()
    {
        var factory = new BattleFactory(TestRules.All);
        var species = Carrying(1, Common, Rare);

        bool rare = false;

        for (uint seed = 1; seed <= 400 && !rare; seed++)
            rare = factory.HeldBy(species, new BattleRng(seed)) == Rare;

        Assert.True(rare);
    }

    /// <summary>The common one is the common one, which is the only thing the two rates say.</summary>
    [Fact]
    public void AndTheCommonOneIsCommoner()
    {
        var factory = new BattleFactory(TestRules.All);
        var species = Carrying(1, Common, Rare);

        int common = 0, rare = 0;

        for (uint seed = 1; seed <= 600; seed++)
        {
            int held = factory.HeldBy(species, new BattleRng(seed));

            if (held == Common) common++;
            if (held == Rare) rare++;
        }

        Assert.True(common > rare);
    }

    /// <summary>
    /// Nothing rolls without being handed dice. A wild builder with none produces a
    /// creature carrying nothing, which is what every test written before this expects
    /// and what a trainer's party should be.
    /// </summary>
    [Fact]
    public void AndNothingIsRolledWithoutDice()
    {
        var factory = new BattleFactory(TestRules.All);

        Assert.Equal(0, factory.Wild(1, 10)!.Holding);
    }

    /// <summary>And what it is carrying travels in the rules file.</summary>
    [Fact]
    public void AndItTravelsInTheRulesFile()
    {
        GameRules rules = new(
            [Carrying(1, Common, Rare)],
            [new MoveData(1, "", 0, 40, PokemonType.Normal, 100, 20, 0, 0, 0)],
            [],
            [],
            []);

        using var buffer = new MemoryStream();
        rules.Save(buffer);

        buffer.Position = 0;

        SpeciesData read = GameRules.Load(buffer).SpeciesAt(1)!;

        Assert.Equal(Common, read.Item1);
        Assert.Equal(Rare, read.Item2);
    }
}
