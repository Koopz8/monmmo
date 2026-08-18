using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a variable can be made to hold, from the writes whose value is in the bytes.
/// <para>
/// <b>A limitation this project declared at 229 and never measured.</b> <c>--arrivals</c> asks
/// whether anything writes the value a condition wants, answers off <c>setvar</c> alone, and says
/// out loud that a copy or a counter writes something the bytes do not carry. That caveat has
/// been quoted for twenty-five milestones with no number on it — and most of it is readable.
/// </para>
/// <para>
/// <c>setvar 0x8004, 3 ; copyvar 0x406F, 0x8004</c> writes THREE into <c>0x406F</c>. 229's
/// headline was that twenty maps want 1/2/3/5/6/7/8 of that variable and the only writer in the
/// scan writes nought. Three and six are written, by this idiom, on one map.
/// </para>
/// </summary>
public sealed class WhatAVariableCanHoldTests
{
    private static ScriptCommand SetVar(int variable, int value) =>
        new(0, 0x16, [(byte)variable, (byte)(variable >> 8), (byte)value, (byte)(value >> 8)]);

    private static ScriptCommand AddVar(int variable, int step) =>
        new(0, 0x17, [(byte)variable, (byte)(variable >> 8), (byte)step, (byte)(step >> 8)]);

    private static ScriptCommand CopyVar(int to, int from) =>
        new(0, 0x19, [(byte)to, (byte)(to >> 8), (byte)from, (byte)(from >> 8)]);

    /// <summary>A command whose effect on a variable this project cannot read.</summary>
    private static ScriptCommand Special(int routine) =>
        new(0, 0x25, [(byte)routine, (byte)(routine >> 8)]);

    // ------------------------------------------------------------------ what one command does

    /// <summary>Each of the readable writes says which of the three things it is.</summary>
    [Fact]
    public void EachKindOfWriteSaysWhichItIs()
    {
        Assert.Equal((0x406F, 3, null, false), WhatAVariableCanHold.WhatItDoes(SetVar(0x406F, 3)));
        Assert.Equal((0x4001, null, 1, false), WhatAVariableCanHold.WhatItDoes(AddVar(0x4001, 1)));
        Assert.Equal((0x406F, null, null, true), WhatAVariableCanHold.WhatItDoes(CopyVar(0x406F, 0x8004)));
        Assert.Null(WhatAVariableCanHold.WhatItDoes(Special(0x014B)));
    }

    // ------------------------------------------------------------------------- the one hop

    /// <summary>
    /// THE THING: a copy whose source the command before it just set to a literal leaves that
    /// literal.
    /// </summary>
    [Fact]
    public void ACopyFromAVariableJustSetToALiteralLeavesThatLiteral()
    {
        Assert.Equal(
            3,
            WhatAVariableCanHold.CopiedLiteral(CopyVar(0x406F, 0x8004), SetVar(0x8004, 3)));
    }

    /// <summary>
    /// AND THE HALF THAT KEEPS IT HONEST: a copy after a command this project cannot read leaves
    /// nothing.
    /// </summary>
    /// <remarks>
    /// This is not hypothetical. The third of <c>0x406F</c>'s three copies has
    /// <c>special 0x014B</c> immediately before it, and a reading that carried the literal from
    /// an earlier <c>setvar</c> past that would invent a value the cartridge never writes.
    /// Adjacency is what makes that impossible, and it is why the rule is adjacency rather than
    /// a list of commands that count as barriers — this project has had to fix such a list twice.
    /// </remarks>
    [Fact]
    public void ACopyAfterSomethingUnreadableLeavesNothing()
    {
        Assert.Null(WhatAVariableCanHold.CopiedLiteral(CopyVar(0x406F, 0x8004), Special(0x014B)));
    }

    /// <summary>
    /// And a literal put in a DIFFERENT variable leaves nothing — without this every copy after
    /// any setvar reads as writing that setvar's value.
    /// </summary>
    [Fact]
    public void ALiteralPutInAnotherVariableLeavesNothing()
    {
        Assert.Null(
            WhatAVariableCanHold.CopiedLiteral(CopyVar(0x406F, 0x8004), SetVar(0x8005, 3)));
    }

    /// <summary>And a copy with nothing before it at all leaves nothing.</summary>
    [Fact]
    public void ACopyWithNothingBeforeItLeavesNothing()
    {
        Assert.Null(WhatAVariableCanHold.CopiedLiteral(CopyVar(0x406F, 0x8004), null));
    }

    /// <summary>And a command that is not a copy is not asked this question.</summary>
    [Fact]
    public void ACommandThatIsNotACopyLeavesNothing()
    {
        Assert.Null(WhatAVariableCanHold.CopiedLiteral(SetVar(0x406F, 1), SetVar(0x8004, 3)));
    }

    // ---------------------------------------------------------------------- what it reaches

    /// <summary>A value something sets is reachable, with no counting involved.</summary>
    [Fact]
    public void AValueSomethingSetsIsReachable()
    {
        Assert.True(new WhatItCanHold(0x406F, [0, 3, 6], [], false).CanReach(3, 99));
    }

    /// <summary>
    /// THE COUNTER: a variable set to nought and added to by one can hold two.
    /// </summary>
    [Fact]
    public void AValueACounterReachesIsReachable()
    {
        Assert.True(new WhatItCanHold(0x4002, [0], [1], false).CanReach(2, 99));
    }

    /// <summary>
    /// And a variable nothing steps can only hold what something sets — the half without which
    /// every condition in the game reads as satisfiable.
    /// </summary>
    [Fact]
    public void WithoutAStepOnlyWhatSomethingSetsIsReachable()
    {
        Assert.False(new WhatItCanHold(0x406F, [0, 3, 6], [], false).CanReach(5, 99));
    }

    /// <summary>
    /// And a step that cannot land on the value does not reach it: two from nought is never five.
    /// </summary>
    [Fact]
    public void AStepThatCannotLandOnTheValueDoesNotReachIt()
    {
        Assert.False(new WhatItCanHold(0x4002, [0], [2], false).CanReach(5, 99));
        Assert.True(new WhatItCanHold(0x4002, [0], [2], false).CanReach(4, 99));
    }

    /// <summary>
    /// AND THE CEILING IS REAL: a step of one reaches every number eventually, so the walk is
    /// bounded by the largest value anybody asks about and says no past it.
    /// </summary>
    /// <remarks>
    /// An unbounded closure answers yes to every question ever asked, which is a reading that
    /// cannot come back empty.
    /// </remarks>
    [Fact]
    public void TheWalkStopsAtTheCeiling()
    {
        Assert.True(new WhatItCanHold(0x4002, [0], [1], false).CanReach(9, 9));
        Assert.False(new WhatItCanHold(0x4002, [0], [1], false).CanReach(10, 9));
    }
}
