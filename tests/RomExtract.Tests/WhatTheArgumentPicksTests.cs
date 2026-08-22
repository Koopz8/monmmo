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

    // ---------------------------------------------------------------- the other slots (292)

    private static SpecialCall Slotted(int slot, int value, int at) =>
        new("9.6", "person", at, Routine, 0x800D, [(slot, value)], [], []);

    /// <summary>
    /// <b>THE SLOT IS A QUESTION AND IT HAD ONE ANSWER (292).</b> Every sweep in this project
    /// reads <c>0x8004</c>, so a routine handed its argument anywhere else reads as taking none —
    /// and on this cartridge <b>11 of the 44 routines that take an argument take it ONLY in some
    /// other slot</b>. The slots used are 0x8004 x33, 0x8005 x16, 0x8006 x7, and one each at
    /// 0x8007, 0x8008 and 0x800F.
    /// </summary>
    [Fact]
    public void ARoutineHandedItsValueElsewhereIsInvisibleUnderTheDefault()
    {
        SpecialCall[] calls = [Slotted(0x8008, 1, 0x100), Slotted(0x8008, 2, 0x200)];

        // Under the slot every sweep reads, both calls look like "no argument" and the routine
        // has one bucket — so it is not reported at all.
        Assert.Empty(WhatTheArgumentPicks.In(calls));

        // Under the slot the cartridge actually uses, it has two.
        ARoutinesArguments found = Assert.Single(WhatTheArgumentPicks.In(calls, 0x8008));

        Assert.Equal(2, found.Arguments.Count);
    }

    /// <summary>
    /// And the slots a routine is handed a value in are counted before anything is read off one —
    /// values and calls separately, because a slot set to the same number at forty places is one
    /// value and forty calls.
    /// </summary>
    [Fact]
    public void TheSlotsAreCountedByValuesAndByCalls()
    {
        IReadOnlyList<(int Slot, int Values, int Calls)> slots = WhatTheArgumentPicks.SlotsOf(
        [
            Slotted(0x8005, 1, 0x100),
            Slotted(0x8005, 1, 0x200),
            Slotted(0x8005, 2, 0x300),
            Slotted(0x8006, 9, 0x400),
        ]);

        Assert.Equal([(0x8005, 2, 3), (0x8006, 1, 1)], slots);
    }

    /// <summary>A routine nothing sets a slot for has no slots, rather than an empty 0x8004.</summary>
    [Fact]
    public void ARoutineHandedNothingHasNoSlots()
    {
        Assert.Empty(WhatTheArgumentPicks.SlotsOf([Call(null, 0x100, 0)]));
    }

    // ------------------------------------------------ the whole population, every slot (293)

    private static SpecialCall For(int routine, int slot, int? value, int at, params int[] compared) =>
        new("9.6", "person", at, routine, 0x800D,
            value is { } v ? [(slot, v)] : [],
            [.. compared.Select(c => (c, (byte)0x01))],
            []);

    /// <summary>
    /// <b>THE WHOLE POPULATION, IN EVERY SLOT (293).</b> 291's answer was "one routine of
    /// twenty-two" asked in <c>0x8004</c> alone, and 292 found that eleven routines are handed a
    /// value only in some other slot. Asked properly, the answer is still <c>0x194</c> and only
    /// <c>0x194</c> — the blind spot was real and hid nothing.
    /// </summary>
    [Fact]
    public void ARoutineIsAskedInTheSlotItIsActuallyHandedAValueIn()
    {
        SpecialCall[] calls =
        [
            For(0x100, 0x8005, 1, 0x10, 0),
            For(0x100, 0x8005, 2, 0x20, 1),
        ];

        // The slot every other sweep reads finds nothing: both calls look argument-less.
        Assert.Empty(WhatTheArgumentPicks.In(calls));

        Assert.Equal([0x100], WhatTheArgumentPicks.Selectors(calls));
    }

    /// <summary>
    /// A routine handed values in TWO slots is asked in both — either could be the one that picks,
    /// and <c>0x0138</c> on this cartridge is handed values in <c>0x8005</c> and <c>0x8006</c>.
    /// </summary>
    [Fact]
    public void ARoutineHandedValuesInTwoSlotsIsAskedInBoth()
    {
        // 0x8005 carries THREE values and 0x8006 two, so the slots are listed 0x8005 first — and
        // it is 0x8006 that picks the question. Every 0x8005 value pairs with both 0x8006 values,
        // so its own compare sets are identical and it finds nothing.
        //
        // The first version of this fixture had the discriminating slot carrying MORE values, so
        // it sorted first and a version reading only the first slot passed. A fixture whose
        // subject is "both slots" has to make the second one the one that matters.
        SpecialCall[] calls =
        [
            Two(0x10, 1, 9, 0), Two(0x20, 1, 8, 1),
            Two(0x30, 2, 9, 0), Two(0x40, 2, 8, 1),
            Two(0x50, 3, 9, 0), Two(0x60, 3, 8, 1),
        ];

        Assert.Equal(
            [0x8005, 0x8006],
            WhatTheArgumentPicks.SlotsOf(calls).Select(x => x.Slot));

        Assert.Empty(WhatTheArgumentPicks.In(calls, 0x8005).Where(p => p.TheValueChangesTheQuestion));
        Assert.NotEmpty(WhatTheArgumentPicks.In(calls, 0x8006).Where(p => p.TheValueChangesTheQuestion));

        Assert.Equal([0x138], WhatTheArgumentPicks.Selectors(calls));
    }

    private static SpecialCall Two(int at, int five, int six, int compared) =>
        new("9.6", "person", at, 0x138, 0x800D,
            [(0x8005, five), (0x8006, six)], [(compared, (byte)1)], []);

    /// <summary>
    /// And a routine nothing branches on is not a selector however many values it is handed —
    /// all eleven of the ones handed a value outside <c>0x8004</c> are exactly this.
    /// </summary>
    [Fact]
    public void ARoutineNothingBranchesOnIsNotASelector()
    {
        SpecialCall[] calls =
        [
            For(0x1A7, 0x8005, 1, 0x10),
            For(0x1A7, 0x8005, 2, 0x20),
        ];

        Assert.Empty(WhatTheArgumentPicks.Selectors(calls));
    }

    /// <summary>
    /// And <c>ArgumentOf</c> reads the slot it is asked for — the whole generalisation rests on
    /// this one line, and its default is the slot the rest of the project reads.
    /// </summary>
    [Fact]
    public void TheArgumentIsReadFromTheSlotAskedFor()
    {
        var call = new SpecialCall(
            "9.6", "person", 0x100, Routine, 0x800D, [(0x8004, 3), (0x8008, 7)], [], []);

        Assert.Equal(3, WhatTheArgumentPicks.ArgumentOf(call));
        Assert.Equal(3, WhatTheArgumentPicks.ArgumentOf(call, 0x8004));
        Assert.Equal(7, WhatTheArgumentPicks.ArgumentOf(call, 0x8008));
        Assert.Null(WhatTheArgumentPicks.ArgumentOf(call, 0x8005));
    }
}
