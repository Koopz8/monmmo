using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The sampling band (273): what a population of N blocks scores against its own whole, so a
/// distance measured on tens of blocks can be read beside one measured on thousands.
/// <para>
/// 268's mixture bound divides by the distance between two whole populations. The 38 unnamed
/// boundary sites sit 0.601 from the maps' own scripts where the reversal sits 0.504 — which
/// reads as "farther from real script than junk is" and is nothing of the sort: 38 blocks is a
/// small sample and a small sample is farther from everything. The band says a sample of 38 drawn
/// from the maps' OWN scripts scores 0.220 to 0.360, so 0.601 is outside it and 0.373 from the
/// reversal is inside the reversal's.
/// </para>
/// </summary>
public sealed class SamplingBandTests
{
    private const byte SetFlag = 0x29;
    private const byte End = 0x02;
    private const byte Nop = 0x00;
    private const byte FacePlayer = 0x5A;

    /// <summary>
    /// Blocks of two commands each, at 16-byte spacing: <paramref name="setflags"/> of
    /// <c>setflag ; end</c> and the rest <c>faceplayer ; end</c>.
    /// </summary>
    private static (Rom Rom, List<uint> Blocks) Image(int howMany, int setflags)
    {
        var image = new byte[0x4000];

        Array.Fill(image, (byte)0xFF);

        var blocks = new List<uint>();

        for (var i = 0; i < howMany; i++)
        {
            int at = 0x100 + (i * 16);

            if (i < setflags)
            {
                image[at] = SetFlag;
                image[at + 1] = (byte)i;
                image[at + 2] = 0x00;
                image[at + 3] = End;
            }
            else
            {
                image[at] = FacePlayer;
                image[at + 1] = End;
            }

            blocks.Add(0x08000000 + (uint)at);
        }

        return (new Rom(image), blocks);
    }

    [Fact]
    public void APopulationDrawnFromOneKindScoresNoughtAgainstItself()
    {
        (Rom rom, List<uint> blocks) = Image(40, 40);

        IReadOnlyList<double> band = WhatABlockIsMadeOf.SamplingBand(rom, blocks, 10);

        Assert.Equal(4, band.Count);
        Assert.All(band, d => Assert.Equal(0, d, 6));
    }

    /// <summary>
    /// The band widens as the sample shrinks — the whole reason 273 needs one.
    /// <para>
    /// <b>The first version of this fixture could not show it</b>, and the miss was mine rather
    /// than the code's (61). Half the blocks one kind and half the other, in file order, makes
    /// EVERY group pure at both sizes — a group of twenty is blocks 0-19, which is one kind
    /// entirely — so the two sizes tied at 0.250 and the assertion failed against correct code.
    /// What actually drives the effect is a proportion a small group cannot represent: ONE block
    /// of forty is 2.5% of the whole, 5% of a group of twenty and 10% of a group of ten.
    /// </para>
    /// </summary>
    [Fact]
    public void ASmallerSampleSitsFartherFromItsOwnWhole()
    {
        (Rom rom, List<uint> blocks) = Image(40, 1);

        IReadOnlyList<double> twenty = WhatABlockIsMadeOf.SamplingBand(rom, blocks, 20);
        IReadOnlyList<double> ten = WhatABlockIsMadeOf.SamplingBand(rom, blocks, 10);

        Assert.Equal(2, twenty.Count);
        Assert.Equal(4, ten.Count);

        // THE WHOLE POINT: the same population, sampled smaller, is farther from itself.
        Assert.True(ten.Max() > twenty.Max(), $"ten {ten.Max():F3} should exceed twenty {twenty.Max():F3}");
    }

    [Fact]
    public void TheBandIsSortedAndCountsOnlyWholeGroups()
    {
        (Rom rom, List<uint> blocks) = Image(25, 12);

        IReadOnlyList<double> band = WhatABlockIsMadeOf.SamplingBand(rom, blocks, 10);

        // Two whole groups of ten; the last five are not a group and are not scored.
        Assert.Equal(2, band.Count);
        Assert.Equal([.. band.Order()], band);
    }

    [Fact]
    public void ASampleBiggerThanThePopulationHasNoBand()
    {
        (Rom rom, List<uint> blocks) = Image(5, 5);

        Assert.Empty(WhatABlockIsMadeOf.SamplingBand(rom, blocks, 10));
        Assert.Empty(WhatABlockIsMadeOf.SamplingBand(rom, blocks, 0));
    }

    /// <summary>
    /// Every group is measured against THE WHOLE, not against the first group.
    /// <para>
    /// <b>The fixture a green break asked for.</b> Aiming the reference at
    /// <c>population.Take(howMany)</c> instead of the whole passed all five tests, because in
    /// every other fixture here the first group either is the whole or is identical to it. Ten
    /// blocks of one kind followed by thirty of another tell them apart: against the whole the
    /// first group scores 0.375 and against itself it scores nought.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryGroupIsMeasuredAgainstTheWholeAndNotAgainstTheFirstGroup()
    {
        (Rom rom, List<uint> blocks) = Image(40, 10);

        IReadOnlyList<double> band = WhatABlockIsMadeOf.SamplingBand(rom, blocks, 10);

        Assert.Equal(4, band.Count);
        Assert.True(band.Min() > 0, $"no group is the whole, but one scored {band.Min():F3}");
        Assert.Equal(0.375, band.Max(), 3);
        Assert.Equal(0.125, band.Min(), 3);
    }

    /// <summary>
    /// The reading 273 rests on, in miniature: a population that IS the other kind sits outside
    /// the first kind's band, and a distance alone could not have said so.
    /// </summary>
    [Fact]
    public void APopulationOfTheOtherKindSitsOutsideTheBand()
    {
        (Rom rom, List<uint> mapsOwn) = Image(40, 40);

        // Ten blocks of the other kind, elsewhere in the same file.
        var image = rom.Span.ToArray();
        var theirs = new List<uint>();

        for (var i = 0; i < 10; i++)
        {
            int at = 0x1000 + (i * 16);

            image[at] = FacePlayer;
            image[at + 1] = Nop;
            image[at + 2] = End;

            theirs.Add(0x08000000 + (uint)at);
        }

        var both = new Rom(image);

        IReadOnlyList<double> band = WhatABlockIsMadeOf.SamplingBand(both, mapsOwn, 10);

        double theirDistance = WhatABlockIsMadeOf.Distance(
            WhatABlockIsMadeOf.In(both, theirs), WhatABlockIsMadeOf.In(both, mapsOwn));

        Assert.True(theirDistance > band.Max(), $"{theirDistance:F3} should be outside {band.Max():F3}");
    }
}
