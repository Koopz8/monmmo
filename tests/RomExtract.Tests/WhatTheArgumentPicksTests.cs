using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Whether a routine's argument picks WHICH question is being asked (291).
/// <para>
/// <b>236 counted <c>0x194</c>'s places and this prompt has called them "nineteen doors" ever
/// since.</b> They are not doors. It is 1066 calls at 34 places, with eighteen different values in
/// <c>0x8004</c> running 0..20 and skipping 13, 14 and 15 — and what the script does with the
/// answer depends on which value it handed over: at <c>= 16</c> a nought means "This is a
/// two-on-two battle", at <c>= 18</c> a one runs a warp.
/// </para>
/// <para>
/// The floor: <b>22 routines are called with more than one argument value and only 2 of them have
/// the answer compared against different things depending on which</b> — <c>0x194</c> and
/// <c>0x17C</c>.
/// </para>
/// </summary>
public sealed class WhatTheArgumentPicksTests
{
    private const int Routine = 0x194;

    /// <summary>One call: an argument, a byte position, and what the answer is compared against.</summary>
    private static SpecialCall Call(int? argument, int at, params int[] compared) =>
        new(
            "2.1",
            "trigger",
            at,
            Routine,
            AnswersInto: 0x800D,
            Arguments: argument is { } value
                ? [(WhatTheArgumentPicks.TheArgument, value)]
                : [],
            Compared: [.. compared.Select(v => (v, (byte)0x01))],
            Branches: []);

    private static ARoutinesArguments? Only(params SpecialCall[] calls) =>
        WhatTheArgumentPicks.In(calls).FirstOrDefault(r => r.Routine == Routine);

    /// <summary>
    /// <b>THE THING.</b> Two arguments whose answers are compared against different things is a
    /// routine whose argument picks the question — which is what a selector is, said in the
    /// script's own words rather than guessed from what the routine might do.
    /// </summary>
    [Fact]
    public void DifferentComparesUnderDifferentArgumentsIsASelector()
    {
        ARoutinesArguments? found = Only(Call(16, 0x100, 0), Call(20, 0x200, 1));

        Assert.NotNull(found);
        Assert.True(found.TheArgumentChangesTheQuestion);
    }

    /// <summary>
    /// And the same compare set under both is NOT — otherwise every routine called twice reads as
    /// a selector and the finding is about arithmetic rather than about the cartridge.
    /// </summary>
    [Fact]
    public void TheSameCompareUnderBothArgumentsIsNotASelector()
    {
        ARoutinesArguments? found = Only(Call(16, 0x100, 0), Call(20, 0x200, 0));

        Assert.NotNull(found);
        Assert.False(found.TheArgumentChangesTheQuestion);
    }

    /// <summary>
    /// <b>An argument nothing compares says nothing.</b> Fourteen of <c>0x194</c>'s nineteen
    /// arguments have the answer compared nowhere at all, and counting "compared against nothing"
    /// as a distinct question would make every routine with one asked argument a selector.
    /// </summary>
    [Fact]
    public void AnArgumentWhoseAnswerNothingComparesIsNotAQuestion()
    {
        ARoutinesArguments? found = Only(Call(16, 0x100, 0), Call(3, 0x200));

        Assert.NotNull(found);
        Assert.Single(found.Asked);
        Assert.False(found.TheArgumentChangesTheQuestion);
    }

    /// <summary>
    /// <b>CALLS AND PLACES ARE TWO NUMBERS.</b> <c>0x194</c> is 1066 calls at 34 places and the
    /// routine inflation runs to 97x, so a report that says "236 places" when it means 236 calls
    /// has made 224's mistake in a new list.
    /// </summary>
    [Fact]
    public void CallsAndPlacesAreCountedSeparately()
    {
        ARoutinesArguments? found = Only(Call(3, 0x100), Call(3, 0x100), Call(3, 0x200), Call(9, 0x300));

        Assert.NotNull(found);

        OneArgument three = found.Arguments.Single(a => a.Argument == 3);

        Assert.Equal(3, three.Calls);
        Assert.Equal(2, three.Places);
    }

    /// <summary>
    /// A routine called with one argument value is not in the list at all — the question is what
    /// the argument SELECTS, and one value selects nothing.
    /// </summary>
    [Fact]
    public void ARoutineWithOneArgumentValueIsNotAsked()
    {
        Assert.Null(Only(Call(16, 0x100, 0), Call(16, 0x200, 1)));
    }

    /// <summary>
    /// A call with nothing in <c>0x8004</c> is its own bucket rather than being dropped — three of
    /// <c>0x194</c>'s 34 places set nothing, and a sweep that hid them would report 31 as 34.
    /// </summary>
    [Fact]
    public void ACallWithNoArgumentIsItsOwnBucket()
    {
        ARoutinesArguments? found = Only(Call(16, 0x100, 0), Call(null, 0x200));

        Assert.NotNull(found);
        Assert.Contains(found.Arguments, a => a.Argument is null);
    }

    /// <summary>
    /// The argument is the LAST value put in the slot before the call, and nothing else in the
    /// run counts — a script that sets it twice means the second one.
    /// </summary>
    [Fact]
    public void TheArgumentIsTheLastValuePutInTheSlot()
    {
        var call = new SpecialCall(
            "2.1", "trigger", 0x100, Routine, 0x800D,
            [(WhatTheArgumentPicks.TheArgument, 3), (0x8005, 9), (WhatTheArgumentPicks.TheArgument, 16)],
            [], []);

        Assert.Equal(16, WhatTheArgumentPicks.ArgumentOf(call));
    }

    /// <summary>
    /// <b>And the two claims are not the same claim.</b> A routine whose no-argument call is
    /// compared one way and whose argument-bearing calls are all compared another has a
    /// difference between HAVING an argument and not — which says nothing about what the value
    /// selects. <c>0x17C</c> is exactly that, and it is why this cartridge's "2 of 22" is really
    /// one.
    /// </summary>
    [Fact]
    public void TheValueChangingTheQuestionIsNotTheSameAsTheArgumentChangingIt()
    {
        ARoutinesArguments? found = Only(Call(null, 0x100, 1), Call(129, 0x200, 0), Call(214, 0x300, 0));

        Assert.NotNull(found);
        Assert.True(found.TheArgumentChangesTheQuestion);
        Assert.False(found.TheValueChangesTheQuestion);
    }

    /// <summary>And a routine whose VALUES disagree is a hit under both.</summary>
    [Fact]
    public void ARoutineWhoseValuesDisagreeIsAHitUnderBoth()
    {
        ARoutinesArguments? found = Only(Call(16, 0x100, 0), Call(20, 0x200, 1));

        Assert.NotNull(found);
        Assert.True(found.TheArgumentChangesTheQuestion);
        Assert.True(found.TheValueChangesTheQuestion);
    }

    /// <summary>And a call that sets some other slot has no argument, rather than that slot's.</summary>
    [Fact]
    public void AValueInAnotherSlotIsNotTheArgument()
    {
        var call = new SpecialCall(
            "2.1", "trigger", 0x100, Routine, 0x800D, [(0x8005, 9)], [], []);

        Assert.Null(WhatTheArgumentPicks.ArgumentOf(call));
    }
}
