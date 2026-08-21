using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Where a population lives in the file, and whether the cut a reading takes can be measured at all
/// (278).
/// <para>
/// 277 gave this project two ways to cut a null and left which one to use as a judgement at each
/// call site — a knob that changes conclusions and fails no test. The cut is about how a sample of
/// the REFERENCE should be shaped, so it can only be measured off members of the population being
/// read that lie inside the reference's own span. For the 38 unnamed boundary sites, three do, and
/// the cut is MODELLED.
/// </para>
/// <para>
/// The measurement that came out of asking: <b>every block of script this cartridge is known to
/// hold lies in 2.5% of the file</b>, and every population read against it is spread over ninety
/// per cent or more.
/// </para>
/// </summary>
public sealed class WhereAPopulationLivesTests
{
    // ------------------------------------------------------------ touches

    /// <summary>A run of neighbours lands in one slice; a scatter lands in many.</summary>
    [Fact]
    public void ARunTouchesOneSliceAndAScatterTouchesMany()
    {
        uint[] run = [100, 101, 102, 103];
        uint[] scatter = [0, 250, 500, 750];

        Assert.Equal(1, WhatABlockIsMadeOf.Touches(run, 0, 1000, 10));
        Assert.Equal(4, WhatABlockIsMadeOf.Touches(scatter, 0, 1000, 10));
    }

    /// <summary>
    /// <b>The span is passed in, and it has to be.</b> The first version of this sliced the
    /// REFERENCE's own span, so anything beyond the reference's last member clamped into one
    /// slice — and read 38 sites spanning fourteen megabytes as touching THREE. Same members, two
    /// spans, two very different answers.
    /// </summary>
    [Fact]
    public void TheSpanIsWhatIsSlicedAndNotThePopulation()
    {
        uint[] members = [10, 2000, 4000, 6000, 8000];

        // Over the whole range they are a scatter.
        Assert.Equal(5, WhatABlockIsMadeOf.Touches(members, 0, 10000, 10));

        // Over a range that ends at 100, every one of them past it lands in the last slice.
        Assert.Equal(2, WhatABlockIsMadeOf.Touches(members, 0, 100, 10));
    }

    /// <summary>Nothing to slice by, or nothing to count, is nought rather than a guess.</summary>
    [Fact]
    public void NoSpanAndNoSlicesAreNought()
    {
        uint[] members = [1, 2, 3];

        Assert.Equal(0, WhatABlockIsMadeOf.Touches(members, 0, 1000, 0));
        Assert.Equal(0, WhatABlockIsMadeOf.Touches(members, 500, 500, 10));
        Assert.Equal(0, WhatABlockIsMadeOf.Touches([], 0, 1000, 10));
    }

    /// <summary>A member below the span is the first slice, not a negative one.</summary>
    [Fact]
    public void AMemberBelowTheSpanIsTheFirstSlice()
    {
        Assert.Equal(1, WhatABlockIsMadeOf.Touches([5u], 100, 1100, 10));
        Assert.Equal(2, WhatABlockIsMadeOf.Touches([5u, 1050u], 100, 1100, 10));
    }

    // ------------------------------------------------------------ which cut

    private static IReadOnlyList<uint> Spread(int howMany, uint every) =>
        [.. Enumerable.Range(0, howMany).Select(i => (uint)(1000 + (i * every)))];

    /// <summary>
    /// A population being read that sits INSIDE the reference and is spread through it reads as
    /// the interleaved cut, because that is the shape a spread sample of the reference has.
    /// </summary>
    [Fact]
    public void APopulationSpreadThroughTheReferenceTakesTheInterleavedCut()
    {
        IReadOnlyList<uint> reference = Spread(100, 10);

        // Ten members, every tenth of the reference's own span.
        IReadOnlyList<uint> read = [.. Enumerable.Range(0, 10).Select(i => (uint)(1000 + (i * 100)))];

        (Cut which, _, _, int inside, _, _) = WhatABlockIsMadeOf.WhichCut(reference, read);

        Assert.Equal(10, inside);
        Assert.Equal(Cut.Interleaved, which);
    }

    /// <summary>And one that is a run inside the reference reads as the consecutive cut.</summary>
    [Fact]
    public void ARunInsideTheReferenceTakesTheConsecutiveCut()
    {
        IReadOnlyList<uint> reference = Spread(100, 10);

        IReadOnlyList<uint> read = [.. Enumerable.Range(0, 10).Select(i => (uint)(1400 + (i * 10)))];

        (Cut which, _, _, int inside, _, _) = WhatABlockIsMadeOf.WhichCut(reference, read);

        Assert.Equal(10, inside);
        Assert.Equal(Cut.Consecutive, which);
    }

    /// <summary>
    /// <b>AND THE ANSWER THAT MATTERS: how many of the population being read are INSIDE the
    /// reference at all.</b> With the reference in one small region and the reading spread over
    /// the whole file, almost none are, and there is nothing to measure the shape against — which
    /// is the state the 38 are in and the reason their cut is MODELLED.
    /// </summary>
    [Fact]
    public void APopulationOutsideTheReferenceCannotHaveItsShapeMeasured()
    {
        IReadOnlyList<uint> reference = Spread(100, 10);

        // Ten members spread over a range twenty times the reference's, only one of them inside.
        IReadOnlyList<uint> read = [.. Enumerable.Range(0, 10).Select(i => (uint)(1500 + (i * 5000)))];

        (_, int touches, int footprint, int inside, _, _) =
            WhatABlockIsMadeOf.WhichCut(reference, read);

        Assert.Equal(1, inside);
        Assert.True(
            touches > footprint,
            $"the reading touched {touches} where the whole reference touched {footprint}");
    }

    /// <summary>An empty population on either side is no reading, not a default.</summary>
    [Fact]
    public void NothingOnEitherSideIsNoReading()
    {
        (_, int read, int reference, int inside, IReadOnlyList<int> runs, IReadOnlyList<int> scatter) =
            WhatABlockIsMadeOf.WhichCut([], [1, 2, 3]);

        Assert.Equal(0, read);
        Assert.Equal(0, reference);
        Assert.Equal(0, inside);
        Assert.Empty(runs);
        Assert.Empty(scatter);

        Assert.Empty(WhatABlockIsMadeOf.WhichCut([1, 2, 3], []).InRuns);
    }

    /// <summary>
    /// The two known rows are in the answer, because a lean between two numbers the caller cannot
    /// see is a verdict nobody can check (262).
    /// </summary>
    [Fact]
    public void TheTwoKnownRowsComeBackWithTheVerdict()
    {
        IReadOnlyList<uint> reference = Spread(100, 10);

        (_, _, _, _, IReadOnlyList<int> runs, IReadOnlyList<int> scatter) =
            WhatABlockIsMadeOf.WhichCut(reference, Spread(10, 100));

        Assert.Equal(10, runs.Count);
        Assert.Equal(10, scatter.Count);
        Assert.True(
            scatter.Min() > runs.Max(),
            $"a scattered group touched {scatter.Min()}..; a run touched ..{runs.Max()}");
    }
}
