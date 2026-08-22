using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The variable that counts a map's doors (297).
/// <para>
/// <c>0x403A</c> is named on four maps and on no other, is handed to <c>special 0x0132</c> at four
/// of that routine's four places, and takes exactly as many values on three of those four as there
/// are MAPS that can warp there — 3 of 3, 11 of 11, 5 of 5. The fourth, TRAINER TOWER, takes one
/// value and has nine.
/// </para>
/// <para>
/// <b>The floor is the whole of this test.</b> Asked of every (variable, map) pair the map scan
/// writes it is 45.2%, and that is almost entirely maps with ONE way in, which any variable
/// written once matches — the blank entry of 264's item table in another shape. Counted out, the
/// share falls to 8.9%.
/// </para>
/// </summary>
public sealed class WhatCountsTheDoorsTests
{
    private static AVariableOnAMap On(string mapId, int values, int doors) =>
        new(WhatCountsTheDoors.TheLift, mapId, mapId, [.. Enumerable.Range(0, values)], doors, doors);

    /// <summary>A variable takes one value per way in, or it does not.</summary>
    [Fact]
    public void CountingIsValuesAgainstMapsThatWarpIn()
    {
        Assert.True(On("1.58", values: 11, doors: 11).Counts);
        Assert.False(On("2.11", values: 1, doors: 9).Counts);
    }

    /// <summary>
    /// <b>THE THING.</b> A hit on a one-door map is not a hit, and the floor has to be able to say
    /// so. The fixture holds one trivial match and one real one, so a cut that keeps the trivial
    /// row reads 2 of 2 where the honest answer is 1 of 1.
    /// </summary>
    [Fact]
    public void TheOneDoorPairsAreCountedOutAboveACutOfOne()
    {
        List<AVariableOnAMap> all = [On("1.0", values: 1, doors: 1), On("1.58", values: 11, doors: 11)];

        Assert.Equal((2, 2), WhatCountsTheDoors.Floor(all, 1));
        Assert.Equal((1, 1), WhatCountsTheDoors.Floor(all, 2));
    }

    /// <summary>
    /// And the cut is on the DOORS rather than on the values, which is the direction that cannot
    /// be chosen by the answer: a variable is included or not by a property of the map it sits on,
    /// before anything about the variable is looked at (79).
    /// <para>
    /// The two rows disagree about which one survives each cut, so a version cutting on values
    /// gives the other answer rather than the same one.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCutIsOnTheDoorsAndNotOnTheValues()
    {
        List<AVariableOnAMap> all =
        [
            new(0x4001, "1.0", "ONE", [1, 2, 3, 4, 5], 1, 1),
            new(0x4002, "1.1", "TWO", [1], 5, 5),
        ];

        // Cut at five doors: the five-VALUE row goes and the five-DOOR row stays.
        (int pairs, int match) = WhatCountsTheDoors.Floor(all, 5);

        Assert.Equal(1, pairs);
        Assert.Equal(0, match);
    }

    /// <summary>
    /// A cut nothing reaches leaves no pairs at all, which is what an empty comparand has to look
    /// like — 276 found that an empty one is not an obviously wrong answer but a plausible one.
    /// </summary>
    [Fact]
    public void ACutNothingReachesLeavesNothing()
    {
        Assert.Equal((0, 0), WhatCountsTheDoors.Floor([On("1.0", values: 1, doors: 1)], 2));
    }
}
