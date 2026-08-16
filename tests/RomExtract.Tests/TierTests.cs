using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Strength bands, computed rather than decided.
/// <para>
/// PokeMMO's tiers are curated: a committee decides, and from outside there is no way to
/// check the list against anything. That is a reasonable way to run a competitive game and
/// it is the opposite of what this project can offer, so these are computed from the
/// cartridge — and what these tests are really about is that the boundaries are not written
/// anywhere.
/// </para>
/// <para>
/// The one thing that had to be decided is that there are five of them. Everything else
/// follows from the image, which means a different image gives different boundaries without
/// a line of the source changing, and there is a test for exactly that.
/// </para>
/// </summary>
public class TierTests
{
    private static SpeciesData Species(int index, int each) => new()
    {
        Index = index,
        Name = string.Empty,
        BaseHp = (byte)each, BaseAttack = (byte)each, BaseDefense = (byte)each,
        BaseSpeed = (byte)each, BaseSpAttack = (byte)each, BaseSpDefense = (byte)each,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    /// <summary>A hundred species whose totals run evenly from low to high.</summary>
    private static List<SpeciesData> Spread(int howMany = 100) =>
        [.. Enumerable.Range(1, howMany).Select(i => Species(i, i))];

    [Fact]
    public void ThereAreFiveBandsAndFourBoundariesBetweenThem()
    {
        Assert.Equal(5, Tiers.Bands);
        Assert.Equal(Tiers.Bands, Tiers.Names.Count);

        Assert.Equal(Tiers.Bands - 1, Tiers.Boundaries(Spread()).Count);
    }

    /// <summary>
    /// The boundaries come from the species, not from this file.
    /// <para>
    /// The claim the whole thing rests on, and the cheapest way to check it: give it a
    /// different set of creatures and watch the numbers move. A boundary written in the
    /// source would sit still.
    /// </para>
    /// </summary>
    [Fact]
    public void DifferentCreaturesGiveDifferentBoundaries()
    {
        IReadOnlyList<int> small = Tiers.Boundaries(Spread());
        IReadOnlyList<int> large = Tiers.Boundaries(
            [.. Enumerable.Range(1, 100).Select(i => Species(i, i + 20))]);

        Assert.Equal(small.Count, large.Count);

        for (int band = 0; band < small.Count; band++)
            Assert.True(large[band] > small[band], $"boundary {band} did not move");
    }

    /// <summary>
    /// Every band has somebody in it, which is what "at the quintiles" is supposed to buy
    /// and what a set of made-up boundaries would not.
    /// </summary>
    [Fact]
    public void EveryBandHasSomebodyInIt()
    {
        List<SpeciesData> species = Spread(500);
        IReadOnlyList<int> cuts = Tiers.Boundaries(species);

        int[] counts =
        [
            .. Enumerable.Range(0, Tiers.Bands)
                .Select(band => species.Count(s => Tiers.Of(s.BaseStatTotal, cuts) == band)),
        ];

        Assert.All(counts, c => Assert.True(c > 0));

        // And they are roughly the same size, which is the other half of what a quintile
        // means. Generous, because ties in the totals push species across a boundary and
        // real cartridges are full of ties.
        Assert.All(counts, c => Assert.InRange(c, species.Count / 10, species.Count / 2));
    }

    [Fact]
    public void TheWeakestIsInTheFirstBandAndTheStrongestInTheLast()
    {
        List<SpeciesData> species = Spread();
        IReadOnlyList<int> cuts = Tiers.Boundaries(species);

        Assert.Equal(0, Tiers.Of(species.Min(s => s.BaseStatTotal), cuts));
        Assert.Equal(Tiers.Bands - 1, Tiers.Of(species.Max(s => s.BaseStatTotal), cuts));

        // And anything stronger than anything on the cartridge is still in the last band
        // rather than in a sixth one that does not exist.
        Assert.Equal(Tiers.Bands - 1, Tiers.Of(9_999, cuts));
    }

    /// <summary>
    /// Placeholders are left out. The cartridge's table is longer than its list of
    /// creatures, and counting the slack as very weak things would drag every boundary down.
    /// </summary>
    [Fact]
    public void SpeciesWithNoStatsAreNotCounted()
    {
        List<SpeciesData> real = Spread();

        List<SpeciesData> padded =
        [
            .. real,
            .. Enumerable.Range(500, 200).Select(i => Species(i, 0)),
        ];

        Assert.Equal(Tiers.Boundaries(real), Tiers.Boundaries(padded));
    }

    /// <summary>
    /// A party is where its strongest member is, not where its average is.
    /// <para>
    /// Five weak creatures and one that wins on its own is a party that wins on its own, and
    /// an average would put it in the middle where it would meet nothing that could stop it.
    /// </para>
    /// </summary>
    [Fact]
    public void APartyIsWhereItsStrongestMemberIs()
    {
        IReadOnlyList<int> cuts = Tiers.Boundaries(Spread());

        int[] mostlyWeak = [180, 190, 200, 210, 220, 600];

        Assert.Equal(Tiers.Bands - 1, Tiers.OfParty(mostlyWeak, cuts));

        // And an empty party is in the first band rather than throwing, because "nobody" is
        // a thing a console can be asked about.
        Assert.Equal(0, Tiers.OfParty([], cuts));
    }

    [Fact]
    public void NothingToBandGivesNoBoundariesRatherThanThrowing()
    {
        Assert.Empty(Tiers.Boundaries([]));

        // And with no boundaries everything is in the first band, which is the honest answer
        // for a server that has never seen a cartridge.
        Assert.Equal(0, Tiers.Of(600, []));
    }

    [Fact]
    public void EveryBandHasAName()
    {
        for (int band = 0; band < Tiers.Bands; band++)
            Assert.False(string.IsNullOrWhiteSpace(Tiers.NameOf(band)));

        Assert.Equal(Tiers.Bands, Tiers.Names.Distinct().Count());

        // Nothing borrowed. This is not PokeMMO's list and calling a band "OU" would be
        // claiming that it was.
        Assert.DoesNotContain("OU", Tiers.Names);
        Assert.DoesNotContain("UU", Tiers.Names);
    }
}
