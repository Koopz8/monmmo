using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// "396 places call 33 routines it could not answer — every one took the zero arm" is three
/// different findings added together, and for most of them there is no arm.
/// <para>
/// A routine whose answer is only ever compared against 2 does the same thing for nought as for
/// 3, 4 or 9: the silence costs nothing a wrong answer would not. A routine whose answer nobody
/// ever looks at has no arm at all. A routine compared against nought takes its branch
/// <em>because</em> the run said nothing — and that one is the only one that is a ceiling.
/// </para>
/// <para>
/// <c>--routines</c> knows the shape and has never seen a run; the run knows what it asked and
/// nothing about the shape. The join is the finding, and it lives here rather than in the
/// printer for the ninth time.
/// </para>
/// </summary>
public sealed class WhatTheSilenceAmountedToTests
{
    private const int NobodyLooks = 0x100;
    private const int TestedAgainstTwo = 0x200;
    private const int TestedAgainstZero = 0x300;
    private const int TestedAgainstBoth = 0x400;
    private const int NotInTheFileAtAll = 0x500;

    private static SpecialCalls.Profile Profile(int routine, params int[] tested) =>
        new(routine, 1, 1, tested.Length > 0, [], tested, tested.Length, 0);

    private static IReadOnlyList<SpecialCalls.Profile> Profiles() =>
    [
        Profile(NobodyLooks),
        Profile(TestedAgainstTwo, 2),
        Profile(TestedAgainstZero, 0),
        Profile(TestedAgainstBoth, 0, 1),
    ];

    private static SpecialCalls.WhatZeroDid Of(int routine, int asked = 1) =>
        Assert.Single(
            SpecialCalls.ZeroAt(Profiles(), new Dictionary<int, int> { [routine] = asked }));

    /// <summary>
    /// Each of the four, on its own routine — the smallest fixture that can tell them apart.
    /// </summary>
    [Fact]
    public void EachRoutineGetsWhatItsSilenceActuallyDid()
    {
        Assert.Equal(SpecialCalls.ZeroWas.NeverTested, Of(NobodyLooks).Was);
        Assert.Equal(SpecialCalls.ZeroWas.ARefusal, Of(TestedAgainstTwo).Was);
        Assert.Equal(SpecialCalls.ZeroWas.AnAssertion, Of(TestedAgainstZero).Was);
        Assert.Equal(SpecialCalls.ZeroWas.Both, Of(TestedAgainstBoth).Was);
    }

    /// <summary>
    /// A routine the run asked that the map scan never saw is not an assertion by default.
    /// <para>
    /// It has no profile, so nothing is known about what its answer is tested against — and
    /// "unknown" has to read as "the answer decides nothing here" rather than being quietly
    /// folded into whichever bucket is nearest.
    /// </para>
    /// </summary>
    [Fact]
    public void ARoutineWithNoProfileIsNotClassifiedAsAnythingElse()
    {
        SpecialCalls.WhatZeroDid what = Of(NotInTheFileAtAll);

        Assert.Equal(SpecialCalls.ZeroWas.NeverTested, what.Was);
        Assert.Empty(what.Tested);
    }

    /// <summary>
    /// The counts are the RUN's, and the order is by how often it asked — not by how many sites
    /// exist in the file.
    /// </summary>
    [Fact]
    public void TheCountsAreWhatTheRunAskedAndTheOrderFollowsThem()
    {
        IReadOnlyList<SpecialCalls.WhatZeroDid> found = SpecialCalls.ZeroAt(
            Profiles(),
            new Dictionary<int, int>
            {
                [TestedAgainstTwo] = 3,
                [TestedAgainstZero] = 90,
                [NobodyLooks] = 40,
            });

        Assert.Equal(new[] { TestedAgainstZero, NobodyLooks, TestedAgainstTwo },
            found.Select(z => z.Routine));

        Assert.Equal(new[] { 90, 40, 3 }, found.Select(z => z.Asked));
    }

    /// <summary>
    /// AND IT COMES BACK EMPTY when the run answered everything it was asked — the answer that
    /// would mean this ceiling is not one.
    /// </summary>
    [Fact]
    public void ARunThatCouldAnswerEverythingHasNoSilenceToAccountFor()
    {
        Assert.Empty(SpecialCalls.ZeroAt(Profiles(), new Dictionary<int, int>()));
    }
}
