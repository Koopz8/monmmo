using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The ends of a scale, measured at the size of the thing being read (276).
/// <para>
/// 273 read a thirty-eight-block distance against a band of eleven groups and called the answer
/// OUTSIDE it. Two things are wrong with that and neither is the distance. A band scored against a
/// whole that CONTAINS the group is pulled toward nought by the share of the whole the group is;
/// and a band's top is a MAXIMUM, which grows with how many groups were taken — the maps' own
/// scripts top out at 0.236 over eleven groups and 0.826 over a hundred and two. What is left is a
/// rate, which has a denominator.
/// </para>
/// </summary>
public sealed class TheEndsAtTheSampleSizeTests
{
    private const byte SetFlag = 0x29;
    private const byte End = 0x02;
    private const byte FacePlayer = 0x5A;

    /// <summary>
    /// Blocks of two commands at 16-byte spacing. <paramref name="kinds"/> says what each block is:
    /// <c>true</c> for <c>setflag ; end</c> and <c>false</c> for <c>faceplayer ; end</c>, so a
    /// population's layout is written out rather than computed.
    /// </summary>
    private static (Rom Rom, List<uint> Blocks) Image(params bool[] kinds)
    {
        var image = new byte[0x8000];

        Array.Fill(image, (byte)0xFF);

        var blocks = new List<uint>();

        for (var i = 0; i < kinds.Length; i++)
        {
            int at = 0x100 + (i * 16);

            if (kinds[i])
            {
                image[at] = SetFlag;
                image[at + 1] = (byte)(i & 0xFF);
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

    private static bool[] Half(int howMany) =>
        [.. Enumerable.Range(0, howMany).Select(i => i < howMany / 2)];

    /// <summary>
    /// How far the two kinds are from each other, computed rather than written down.
    /// <para>
    /// <b>They share <c>end</c>, so the distance between them is a half and not one</b> — every
    /// block in this cartridge ends, and a fixture whose two kinds have literally nothing in
    /// common cannot be built out of blocks that decode. Writing 0.5 into the assertions would be
    /// a magic number nobody could check; taking it off the same function under test would be
    /// circular in the other direction. This takes it off <c>Distance</c> applied to the two
    /// PURE populations, which is the quantity the assertions are actually about.
    /// </para>
    /// </summary>
    private static double Apart(Rom rom, IReadOnlyList<uint> blocks, int firstKind) =>
        WhatABlockIsMadeOf.Distance(
            WhatABlockIsMadeOf.In(rom, blocks.Take(firstKind)),
            WhatABlockIsMadeOf.In(rom, blocks.Skip(firstKind)));

    // ------------------------------------------------------------ against the rest

    /// <summary>
    /// <b>The group is not in what it is compared against.</b> Half one kind and half the other,
    /// cut into two groups: each group is entirely one kind and the rest is entirely the other, so
    /// the distance is the whole pure-against-pure distance. Scored against a whole that CONTAINS
    /// the group it is half that, because half of that whole is the group itself.
    /// </summary>
    [Fact]
    public void AGroupIsScoredAgainstTheRestAndNotAgainstAWholeThatHoldsIt()
    {
        (Rom rom, List<uint> blocks) = Image(Half(20));

        double apart = Apart(rom, blocks, 10);

        IReadOnlyList<double> rest = WhatABlockIsMadeOf.AgainstTheRest(rom, blocks, 10);
        IReadOnlyList<double> whole = WhatABlockIsMadeOf.SamplingBand(rom, blocks, 10);

        Assert.Equal(2, rest.Count);
        Assert.All(rest, d => Assert.Equal(apart, d, 6));

        // Against a whole that is half the group's own kind, it reads HALF as far.
        Assert.Equal(2, whole.Count);
        Assert.All(whole, d => Assert.Equal(apart / 2, d, 6));
    }

    /// <summary>
    /// <b>The rest is everything on BOTH sides of the cut.</b> A version that took only what
    /// follows the group would score the last group against nothing and the first against
    /// everything, and this fixture makes those two different: the middle third is the odd kind,
    /// so a rest-is-the-tail reading gives the first group a different answer from the real one.
    /// </summary>
    [Fact]
    public void TheRestIsBothSidesOfTheCut()
    {
        // Three groups of four: one kind, the other kind, one kind.
        (Rom rom, List<uint> blocks) =
            Image(true, true, true, true, false, false, false, false, true, true, true, true);

        // The first eight are four of one kind then four of the other, which is the pure-against-
        // pure distance this test's expectations are fractions of.
        double apart = Apart(rom, [.. blocks.Take(8)], 4);

        IReadOnlyList<double> rest = WhatABlockIsMadeOf.AgainstTheRest(rom, blocks, 4);

        Assert.Equal(3, rest.Count);

        // The middle group is alone against eight of the other kind: the whole distance.
        Assert.Equal(apart, rest[^1], 6);

        // The two outer groups each face four of their own kind and four of the other, so each is
        // half as far. A tail-only rest would put the LAST group against nothing at all.
        Assert.Equal(apart / 2, rest[0], 6);
        Assert.Equal(apart / 2, rest[1], 6);
    }

    /// <summary>One kind throughout is nought from itself however the cut falls.</summary>
    [Fact]
    public void OneKindThroughoutIsNoughtFromTheRest()
    {
        (Rom rom, List<uint> blocks) = Image([.. Enumerable.Repeat(true, 12)]);

        IReadOnlyList<double> rest = WhatABlockIsMadeOf.AgainstTheRest(rom, blocks, 4);

        Assert.Equal(3, rest.Count);
        Assert.All(rest, d => Assert.Equal(0, d, 6));
    }

    // ------------------------------------------------------------ against another

    /// <summary>
    /// <b>A group is scored against the population it was GIVEN</b>, not against its own whole —
    /// which is the difference between the junk end of a scale and a sampling band. A population
    /// that is all one kind scores nought against itself and one against the other kind, and only
    /// the second is the end of a scale.
    /// </summary>
    [Fact]
    public void AGroupIsScoredAgainstThePopulationItWasGiven()
    {
        (Rom rom, List<uint> blocks) = Image(Half(20));

        List<uint> junk = [.. blocks.Skip(10)];

        HowOftenEachCommand real = WhatABlockIsMadeOf.In(rom, blocks.Take(10));

        IReadOnlyList<double> against = WhatABlockIsMadeOf.AgainstAnother(rom, junk, 5, real);

        Assert.Equal(2, against.Count);
        Assert.All(against, d => Assert.Equal(Apart(rom, blocks, 10), d, 6));

        // Its own whole says nought — a band and an end are different questions.
        Assert.All(WhatABlockIsMadeOf.SamplingBand(rom, junk, 5), d => Assert.Equal(0, d, 6));
    }

    // ------------------------------------------------------------ the rate

    /// <summary>
    /// The rate counts groups at or beyond the distance, over all of them — a denominator that
    /// taking more groups does not inflate.
    /// </summary>
    [Fact]
    public void TheRateIsHowManyGroupsReachItOverHowManyThereAre()
    {
        double[] band = [0.1, 0.2, 0.3, 0.4];

        Assert.Equal(1.0, WhatABlockIsMadeOf.AtLeastAsFar(band, 0.05), 6);
        Assert.Equal(0.5, WhatABlockIsMadeOf.AtLeastAsFar(band, 0.3), 6);
        Assert.Equal(0.0, WhatABlockIsMadeOf.AtLeastAsFar(band, 0.5), 6);
    }

    /// <summary>
    /// <b>AND IT DOES NOT MOVE WHEN THE SAME POPULATION IS SAMPLED MORE</b>, which is the whole
    /// reason it replaced "outside the band". Doubling the groups doubles the maximum's chances
    /// and leaves the rate where it was.
    /// </summary>
    [Fact]
    public void TheRateSurvivesMoreGroupsWhereAMaximumDoesNot()
    {
        double[] few = [0.1, 0.2, 0.3, 0.4];
        double[] many = [.. few, .. few, .. few];

        Assert.Equal(
            WhatABlockIsMadeOf.AtLeastAsFar(few, 0.3),
            WhatABlockIsMadeOf.AtLeastAsFar(many, 0.3),
            6);

        // The same list with one far group added: the maximum jumps and the rate barely moves.
        double[] withATail = [.. many, 0.9];

        Assert.True(withATail.Max() > many.Max() + 0.4);
        Assert.True(
            WhatABlockIsMadeOf.AtLeastAsFar(withATail, 0.5)
            < WhatABlockIsMadeOf.AtLeastAsFar(withATail, 0.3));
    }

    /// <summary>No groups is no rate, rather than a rate of nought.</summary>
    [Fact]
    public void NoGroupsIsNoRate()
    {
        Assert.True(double.IsNaN(WhatABlockIsMadeOf.AtLeastAsFar([], 0.5)));
    }

    // ------------------------------------------------------------ ends that are bands

    /// <summary>
    /// Ends of one value each read exactly as the two-number version does — the band version is a
    /// generalisation and not a second opinion.
    /// </summary>
    [Fact]
    public void EndsOfOneValueReadTheSameAsTheTwoNumberVersion()
    {
        (double least, double most) =
            WhatABlockIsMadeOf.BetweenTheEnds(0.4565, [0.178], [0.735]);

        Assert.Equal(WhatABlockIsMadeOf.BetweenTheEnds(0.4565, 0.178, 0.735), least, 6);
        Assert.Equal(least, most, 6);
    }

    /// <summary>
    /// <b>Ends that cross read nought at the bottom</b> — a scale with no length cannot exclude
    /// anything, and saying so is the answer rather than a failure.
    /// </summary>
    [Fact]
    public void EndsThatCrossCannotExcludeAnything()
    {
        (double least, double most) =
            WhatABlockIsMadeOf.BetweenTheEnds(0.5, [0.3, 0.6], [0.5, 0.9]);

        Assert.Equal(0.0, least, 6);
        Assert.True(most > 0.5);
    }

    /// <summary>
    /// Ends that do not cross give a range narrower than everything, so the corners are doing
    /// their job rather than always producing nought to one.
    /// </summary>
    [Fact]
    public void EndsThatDoNotCrossGiveARealRange()
    {
        (double least, double most) =
            WhatABlockIsMadeOf.BetweenTheEnds(0.5, [0.10, 0.20], [0.80, 0.90]);

        Assert.True(least > 0.3, $"least was {least:F3}");
        Assert.True(most < 0.7, $"most was {most:F3}");
        Assert.True(most > least);
    }

    /// <summary>An end with no groups in it is no reading at all.</summary>
    [Fact]
    public void AnEndWithNoGroupsIsNoReading()
    {
        Assert.True(double.IsNaN(WhatABlockIsMadeOf.BetweenTheEnds(0.5, [], [0.9]).Least));
        Assert.True(double.IsNaN(WhatABlockIsMadeOf.BetweenTheEnds(0.5, [0.1], []).Most));
    }

    // ------------------------------------------------------------ the shared cut

    /// <summary>
    /// <b>All four questions cut the population the same way.</b> 258's fault was a second copy of
    /// a walk written for a second question; there are four now and one <c>Cuts</c>, and this says
    /// so at three sizes rather than trusting that they were written alike.
    /// </summary>
    [Fact]
    public void EveryQuestionCutsThePopulationTheSameWay()
    {
        (Rom rom, List<uint> blocks) = Image(Half(30));

        HowOftenEachCommand other = WhatABlockIsMadeOf.In(rom, blocks.Take(15));

        foreach (int howMany in new[] { 5, 7, 30 })
        {
            int cuts = WhatABlockIsMadeOf.Cuts(blocks, howMany).Count();

            Assert.Equal(cuts, WhatABlockIsMadeOf.SamplingBand(rom, blocks, howMany).Count);
            Assert.Equal(cuts, WhatABlockIsMadeOf.AgainstTheRest(rom, blocks, howMany).Count);
            Assert.Equal(cuts, WhatABlockIsMadeOf.AgainstAnother(rom, blocks, howMany, other).Count);
            Assert.Equal(
                cuts,
                WhatABlockIsMadeOf.BoundPerGroup(rom, blocks, howMany, other, other).Count);
        }
    }
}
