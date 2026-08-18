using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Every number this project has quoted about routines was a count of READS, and a block hanging
/// off nineteen maps is read nineteen times at one address.
/// <para>
/// 220 caught this in the other arm — <c>--routines</c> printing 1037 sites that are 411 byte
/// positions. This is the same correction on <see cref="SpecialCalls"/>, which is what
/// <c>--play</c>'s ceiling and <c>--specials</c> are read off, and the numbers move further:
/// <c>0x0194</c> is <b>747 calls at 26 places</b>, and the ceiling headline goes from
/// "212 of 1055 branching sites" to <b>25 of 411 byte positions</b>.
/// </para>
/// <para>
/// The inflation is not a constant to divide by. In one table it runs from <c>0x0039</c> at 234
/// calls and 234 places — no inflation at all — to <c>0x0194</c> at twenty-nine times. A count of
/// reads says nothing about how many places, which is why both are printed rather than one being
/// replaced.
/// </para>
/// </summary>
public sealed class TheSameAddressReadTwiceTests
{
    private const int Shared = 0x1BB567;
    private const int Elsewhere = 0x162526;

    /// <summary>Nought takes this: compared against 1, jumped on LESS.</summary>
    private static readonly (int Value, byte Condition)[] NoughtTakesIt = [(1, 0)];

    /// <summary>Nought does not: compared against 1, jumped on EQUAL.</summary>
    private static readonly (int Value, byte Condition)[] NoughtDoesNot = [(1, 1)];

    private static SpecialCall Call(
        string map, int at, int routine = 0x1C, (int Value, byte Condition)[]? compared = null) =>
        new(map, "person 3", at, routine, null, [], compared ?? [], []);

    private static SpecialCalls.Profile Only(params SpecialCall[] calls) =>
        Assert.Single(SpecialCalls.Profiles(calls));

    /// <summary>
    /// ONE ADDRESS ON THREE MAPS IS THREE CALLS AND ONE PLACE — the department-store script, which
    /// is where this was noticed.
    /// </summary>
    [Fact]
    public void OneAddressReadFromThreeMapsIsThreeCallsAndOnePlace()
    {
        SpecialCalls.Profile profile = Only(
            Call("5.5", Shared), Call("6.6", Shared), Call("7.4", Shared));

        Assert.Equal(3, profile.Calls);
        Assert.Equal(1, profile.Places);

        // And the map count is a third thing again, which is why it was never a substitute.
        Assert.Equal(3, profile.Maps);
    }

    /// <summary>
    /// AND TWO ADDRESSES ARE TWO PLACES, so the rule is not "always one".
    /// </summary>
    [Fact]
    public void TwoAddressesAreTwoPlacesHoweverManyMapsReadThem()
    {
        SpecialCalls.Profile profile = Only(
            Call("5.5", Shared), Call("6.6", Shared), Call("1.74", Elsewhere));

        Assert.Equal(3, profile.Calls);
        Assert.Equal(2, profile.Places);
    }

    /// <summary>
    /// THE BRANCH COUNTS SPLIT THE SAME WAY, and this is the one the ceiling headline is made of.
    /// <para>
    /// Three reads of one address that branches: three branching sites, one byte position. The
    /// project has been quoting the first of those as though it were the second since the table
    /// was written.
    /// </para>
    /// </summary>
    [Fact]
    public void ThreeReadsOfOneBranchingAddressAreThreeBranchesAtOnePlace()
    {
        SpecialCalls.Profile profile = Only(
            Call("5.5", Shared, compared: NoughtDoesNot),
            Call("6.6", Shared, compared: NoughtDoesNot),
            Call("7.4", Shared, compared: NoughtDoesNot));

        Assert.Equal(3, profile.Branches);
        Assert.Equal(1, profile.BranchPlaces);
    }

    /// <summary>
    /// AND SO DOES WHAT NOUGHT TAKES — separately, because a routine can branch at two places and
    /// have nought take only one of them.
    /// </summary>
    [Fact]
    public void WhatNoughtTakesIsCountedInPlacesToo()
    {
        SpecialCalls.Profile profile = Only(
            Call("5.5", Shared, compared: NoughtTakesIt),
            Call("6.6", Shared, compared: NoughtTakesIt),
            Call("1.74", Elsewhere, compared: NoughtDoesNot));

        Assert.Equal(3, profile.Branches);
        Assert.Equal(2, profile.BranchPlaces);

        Assert.Equal(2, profile.BranchesTakenByZero);

        // Two reads of one address, so nought decides at ONE place — not two.
        Assert.Equal(1, profile.PlacesTakenByZero);
    }

    /// <summary>
    /// A PLACE THAT BRANCHES AND IS NOT TAKEN BY NOUGHT COUNTS IN ONE COLUMN AND NOT THE OTHER.
    /// Without this the two place-counts could be the same field twice and every test above would
    /// still pass.
    /// </summary>
    [Fact]
    public void ABranchingPlaceNoughtDoesNotTakeIsNotAPlaceNoughtDecides()
    {
        SpecialCalls.Profile profile = Only(Call("1.74", Elsewhere, compared: NoughtDoesNot));

        Assert.Equal(1, profile.BranchPlaces);
        Assert.Equal(0, profile.PlacesTakenByZero);
    }

    /// <summary>
    /// A routine nothing branches on has no branching places, however many times it is called —
    /// the answer that has to be possible or the columns are just the call count again.
    /// </summary>
    [Fact]
    public void ARoutineNobodyBranchesOnHasNoBranchingPlaces()
    {
        SpecialCalls.Profile profile = Only(Call("5.5", Shared), Call("6.6", Shared));

        Assert.Equal(2, profile.Calls);
        Assert.Equal(1, profile.Places);
        Assert.Equal(0, profile.Branches);
        Assert.Equal(0, profile.BranchPlaces);
        Assert.Equal(0, profile.PlacesTakenByZero);
    }
}
