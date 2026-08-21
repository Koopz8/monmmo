using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// How a population is cut into groups, and why there are two ways (277).
/// <para>
/// 273 argued for CONSECUTIVE groups: neighbours in the cartridge are alike, so a group of them is
/// farther from the whole and any band off them is wider than the truth. True — and only
/// conservative when the population being READ is itself a run of neighbours. The 38 unnamed
/// boundary sites are scattered from <c>0x028514</c> to <c>0xEA7A8F</c>, and against a null made of
/// runs the null carries the file's regional structure while the reading carries none of it: real
/// script reaches the 38's distance in 6 of 102 run-shaped samples and NONE of 102 scattered ones.
/// </para>
/// </summary>
public sealed class HowAGroupIsCutTests
{
    private const byte SetFlag = 0x29;
    private const byte End = 0x02;
    private const byte FacePlayer = 0x5A;

    /// <summary>
    /// Blocks of two commands at 16-byte spacing; <c>true</c> is <c>setflag ; end</c> and
    /// <c>false</c> is <c>faceplayer ; end</c>.
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

    /// <summary>Interleaved is every n-th item, written out rather than described.</summary>
    [Fact]
    public void InterleavedGroupsAreEveryNth()
    {
        List<IReadOnlyList<int>> cut = [.. WhatABlockIsMadeOf.Cuts(12, 4, Cut.Interleaved)];

        Assert.Equal(3, cut.Count);
        Assert.Equal([0, 3, 6, 9], cut[0]);
        Assert.Equal([1, 4, 7, 10], cut[1]);
        Assert.Equal([2, 5, 8, 11], cut[2]);
    }

    /// <summary>Consecutive is a run, which is what it has always been.</summary>
    [Fact]
    public void ConsecutiveGroupsAreRuns()
    {
        List<IReadOnlyList<int>> cut = [.. WhatABlockIsMadeOf.Cuts(12, 4, Cut.Consecutive)];

        Assert.Equal(3, cut.Count);
        Assert.Equal([0, 1, 2, 3], cut[0]);
        Assert.Equal([4, 5, 6, 7], cut[1]);
        Assert.Equal([8, 9, 10, 11], cut[2]);
    }

    /// <summary>
    /// <b>Both cuts are disjoint and neither invents an index.</b> An off-by-one in the
    /// interleaved arithmetic produces groups that overlap or run off the end, and either would
    /// still look like a list of groups.
    /// </summary>
    [Theory]
    [InlineData(Cut.Consecutive)]
    [InlineData(Cut.Interleaved)]
    public void EveryGroupIsDisjointAndInsideThePopulation(Cut how)
    {
        foreach ((int inAll, int howMany) in new[] { (12, 4), (13, 4), (100, 7), (9, 3) })
        {
            List<IReadOnlyList<int>> cut = [.. WhatABlockIsMadeOf.Cuts(inAll, howMany, how)];

            Assert.Equal(inAll / howMany, cut.Count);

            List<int> all = [.. cut.SelectMany(g => g)];

            Assert.Equal(all.Count, all.Distinct().Count());
            Assert.All(all, i => Assert.InRange(i, 0, inAll - 1));
            Assert.All(cut, g => Assert.Equal(howMany, g.Count));
        }
    }

    /// <summary>
    /// <b>THE WHOLE POINT.</b> A population laid out in runs of one kind: a CONSECUTIVE group is a
    /// region and sits far from the whole; an INTERLEAVED group holds both kinds in the population's
    /// own proportion and sits on top of it. The band is the null, so which cut you take decides
    /// what the null says — and a reading of a scattered population against a run-shaped null is
    /// being compared to the file's regional structure rather than to sampling noise.
    /// </summary>
    [Fact]
    public void ARunSeesOneRegionWhereAScatterSeesThePopulation()
    {
        (Rom rom, List<uint> blocks) =
            Image(true, true, true, true, true, true, false, false, false, false, false, false);

        IReadOnlyList<double> runs = WhatABlockIsMadeOf.SamplingBand(rom, blocks, 4);
        IReadOnlyList<double> scattered =
            WhatABlockIsMadeOf.SamplingBand(rom, blocks, 4, Cut.Interleaved);

        Assert.Equal(3, runs.Count);
        Assert.Equal(3, scattered.Count);

        // Every scattered group is two of each kind, which is the whole population's own mix.
        Assert.All(scattered, d => Assert.Equal(0, d, 6));

        // And the run-shaped null is not nought, which is what makes the two different nulls.
        Assert.True(runs.Max() > 0.2, $"the run-shaped band topped out at {runs.Max():F3}");
    }

    /// <summary>
    /// The rate gets a band the same two ways, because a rate is a number and 276 quoted two of
    /// them with nothing under either.
    /// </summary>
    [Fact]
    public void TheRateIsBandedOverBlocksOfGroups()
    {
        // Four blocks of three: the far values are all in the last block.
        double[] band = [0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.9, 0.9, 0.9];

        IReadOnlyList<double> runs = WhatABlockIsMadeOf.RateBand(band, 0.5, 3);

        Assert.Equal(4, runs.Count);
        Assert.Equal(0.0, runs[0], 6);
        Assert.Equal(1.0, runs[^1], 6);

        // Scattered, every block holds one of the three far values.
        IReadOnlyList<double> scattered =
            WhatABlockIsMadeOf.RateBand(band, 0.5, 3, Cut.Interleaved);

        Assert.Equal(4, scattered.Count);
        Assert.All(scattered, r => Assert.InRange(r, 0.0, 1.0 / 3 + 1e-9));
        Assert.True(scattered.Max() < runs.Max());
    }

    /// <summary>
    /// The start-index form is the consecutive one, so every caller written before 277 kept the
    /// cut it had.
    /// </summary>
    [Fact]
    public void TheStartIndexFormIsTheConsecutiveCut()
    {
        Assert.Equal(
            [.. WhatABlockIsMadeOf.Cuts(25, 6, Cut.Consecutive).Select(g => g[0])],
            [.. WhatABlockIsMadeOf.Cuts(25, 6)]);
    }

    /// <summary>A group bigger than the population is no group, either way.</summary>
    [Theory]
    [InlineData(Cut.Consecutive)]
    [InlineData(Cut.Interleaved)]
    public void AGroupBiggerThanThePopulationIsNoGroup(Cut how)
    {
        Assert.Empty(WhatABlockIsMadeOf.Cuts(5, 6, how));
        Assert.Empty(WhatABlockIsMadeOf.Cuts(5, 0, how));
        Assert.Empty(WhatABlockIsMadeOf.Cuts(5, -1, how));
    }
}
