using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// How many times a routine is called and how many BYTE POSITIONS call it are different numbers,
/// and until 231 this reading only had the first.
/// <para>
/// 224 built `--the-scan` to ask exactly this of a command CODE and found that 97 of this
/// cartridge's 108 codes answer differently. Nobody asked it of a routine NUMBER — and the fault
/// surfaced from the other end: the session prompt quoted *"0x194 is 747 calls at 26 places"* and
/// <b>no instrument in the repository printed the second half</b>. A number nothing computes
/// cannot come back wrong.
/// </para>
/// <para>
/// Measured, it is 118 of 178 called once per byte position, and the worst is `0x0AB` at
/// <b>97 calls at one address</b> — a bigger inflation than `findmove`'s 66.7, which 224 called
/// the sharpest thing in its table.
/// </para>
/// </summary>
public sealed class ARoutineAskedInOnePlaceTests
{
    /// <summary>
    /// THE DISCRIMINATION: one routine called three times at ONE address, one called twice at
    /// TWO. Two routines and two addresses, because a fixture with one of either cannot tell
    /// "counts places" from "counts calls" (fixtures lie, 7).
    /// </summary>
    [Fact]
    public void CallsAndPlacesAreDifferentNumbers()
    {
        IReadOnlyDictionary<int, (int Calls, int Places)> tally = SpecialContracts.CallsAndPlaces(
        [
            // A block hanging off three triggers: read three times, at one byte position.
            (0x194, 0x16F15B),
            (0x194, 0x16F15B),
            (0x194, 0x16F15B),

            // And one genuinely asked in two places.
            (0x039, 0x1A9589),
            (0x039, 0x1A94CF),
        ]);

        Assert.Equal((3, 1), tally[0x194]);
        Assert.Equal((2, 2), tally[0x039]);
    }

    /// <summary>
    /// And the ordinary case, unasserted everywhere else: a routine nothing repeats answers the
    /// same either way, which is what makes the other answer worth printing (fixtures lie, 8).
    /// </summary>
    [Fact]
    public void ARoutineAskedOncePerPlaceAnswersTheSameEitherWay()
    {
        IReadOnlyDictionary<int, (int Calls, int Places)> tally = SpecialContracts.CallsAndPlaces(
            [(0x0A7, 0x16F15B)]);

        Assert.Equal((1, 1), tally[0x0A7]);

        Assert.True(Contract(sites: 1, places: 1).CalledOncePerPlace);
        Assert.False(Contract(sites: 3, places: 1).CalledOncePerPlace);
    }

    /// <summary>
    /// A routine nothing calls is absent rather than nought, so a reading cannot quietly credit
    /// every routine in the file with a place it does not have.
    /// </summary>
    [Fact]
    public void ARoutineNothingCallsIsNotInTheTallyAtAll()
    {
        IReadOnlyDictionary<int, (int Calls, int Places)> tally =
            SpecialContracts.CallsAndPlaces([(0x039, 0x1A9589)]);

        Assert.False(tally.ContainsKey(0x194));
        Assert.Single(tally);
    }

    /// <summary>
    /// The inflation is calls PER PLACE and not the other way up — 97 calls at one address is
    /// ninety-seven, not a hundredth.
    /// </summary>
    [Fact]
    public void TheInflationIsCallsPerPlace()
    {
        Assert.Equal(97.0, Contract(sites: 97, places: 1).CallInflation);
        Assert.Equal(1.0, Contract(sites: 234, places: 234).CallInflation);

        // And a routine with no place at all does not divide by nought.
        Assert.Equal(0, Contract(sites: 0, places: 0).CallInflation);
    }

    /// <summary>
    /// And the row itself puts the two halves of the pair in the right columns — the wiring, in
    /// a place a fixture can reach.
    /// <para>
    /// This test exists because the break that swapped them came back GREEN: the decision lived
    /// inside a sweep that needs a whole cartridge, which is the fifth green break in this
    /// project with exactly one cause. The numbers are deliberately different in every column so
    /// no two of them can be confused for each other.
    /// </para>
    /// </summary>
    [Fact]
    public void TheRowPutsCallsAndPlacesInTheirOwnColumns()
    {
        SpecialContract row = SpecialContracts.Assemble(
            routine: 0x194,
            called: (Calls: 1066, Places: 34),
            arguments: 3,
            compared: new Dictionary<int, int> { [0] = 30, [1] = 27 },
            branches: (Sites: 57, Places: 8),
            across: (Sites: 5, Places: 4),
            nothingClean: 2,
            comparedAcross: new Dictionary<int, int> { [1] = 5 },
            where: ["3.21 person 2 at 0x1A9589"]);

        Assert.Equal(0x194, row.Routine);
        Assert.Equal(1066, row.Sites);
        Assert.Equal(34, row.CallPlaces);
        Assert.Equal(57, row.Branches);
        Assert.Equal(8, row.Places);
        Assert.Equal(5, row.AcrossABarrier);
        Assert.Equal(4, row.PlacesAcross);
        Assert.Equal(2, row.NothingClean);
        Assert.Equal(3, row.TakesArguments);
        Assert.False(row.CalledOncePerPlace);
    }

    private static SpecialContract Contract(int sites, int places) =>
        new(0x194, sites, places, 0, new Dictionary<int, int>(), 0, 0, 0, 0, 0,
            new Dictionary<int, int>(), []);
}
