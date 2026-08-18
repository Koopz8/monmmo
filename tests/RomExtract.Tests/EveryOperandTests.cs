using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Every operand of every command, asked which of them name a variable.
/// <para>
/// <b>251 found <c>copyvar</c>'s destination missing from both of this repository's write
/// tables</b> — two lists, written separately, wrong in the same place, so having two of them
/// caught nothing. This stops reading tables and sweeps instead, and it found two more:
/// <c>specialvar</c>'s destination, which five files in this repository already read as the
/// answer variable, and <c>0x42</c>'s first operand, whose own width comment says it is "a
/// command taking two variables and then being asked about one of them".
/// </para>
/// <para>
/// <b>The test needs no band boundary and no outside knowledge.</b> A variable something looks at
/// is a variable something writes (244), so every operand is scored by how much of what it names
/// the known writers ever write. On this cartridge that comes out bimodal with a chasm: 83
/// operands under 10%, three between, and ten above 90%.
/// </para>
/// </summary>
public sealed class EveryOperandTests
{
    private const byte NoWidth = 0xFF;

    private const byte End = 0x02;

    private static readonly (byte Code, int At)[] KnownWriters = [(0x16, 0)];

    private static Rom Image(params byte[] bytes)
    {
        var data = new byte[0x1000];

        Array.Fill(data, NoWidth);

        bytes.CopyTo(data, 0x100);

        return new Rom(data);
    }

    private static byte[] SetVar(int variable, int value) =>
        [0x16, (byte)variable, (byte)(variable >> 8), (byte)value, (byte)(value >> 8)];

    private static byte[] Compare(int variable, int value) =>
        [0x21, (byte)variable, (byte)(variable >> 8), (byte)value, (byte)(value >> 8)];

    /// <summary>A command with two halfwords that no table in this project names.</summary>
    private static byte[] Unknown(int first, int second) =>
        [0x42, (byte)first, (byte)(first >> 8), (byte)second, (byte)(second >> 8)];

    private static IReadOnlyList<OneOperand> Of(params byte[] bytes) =>
        EveryOperand.In(Image(bytes), [0x08000100], KnownWriters, leastNumbers: 2);

    // ------------------------------------------------------------------- the naming test

    /// <summary>
    /// THE THING: an operand no table names is scored by what the WRITERS write, and comes back
    /// looking like a variable operand.
    /// </summary>
    [Fact]
    public void AnOperandNoTableNamesIsFoundAndScored()
    {
        IReadOnlyList<OneOperand> all = Of(
        [
            .. SetVar(0x8004, 1),
            .. SetVar(0x8008, 1),
            .. Unknown(0x8004, 0x0009),
            .. Unknown(0x8008, 0x000A),
            End,
        ]);

        OneOperand names = Assert.Single(all, o => o.Name == "0x42 arg0");

        Assert.Equal((2, 2, 2), (names.Numbers, names.Places, names.Written));
        Assert.Equal(1.0, names.Share);
    }

    /// <summary>
    /// And the operand beside it, naming numbers nothing writes, scores nought — the half
    /// without which every operand of every command reads as a variable operand.
    /// </summary>
    [Fact]
    public void AnOperandNamingWhatNothingWritesScoresNought()
    {
        IReadOnlyList<OneOperand> all = Of(
        [
            .. SetVar(0x8004, 1),
            .. SetVar(0x8008, 1),
            .. Unknown(0x8004, 0x0009),
            .. Unknown(0x8008, 0x000A),
            End,
        ]);

        OneOperand values = Assert.Single(all, o => o.Name == "0x42 arg2");

        Assert.Equal(0, values.Written);
    }

    /// <summary>
    /// AND THE SEED IS THE WRITERS AND NOTHING ELSE. Scoring against every number any operand
    /// names would make every operand 100% by construction — the question would answer itself.
    /// </summary>
    [Fact]
    public void WhatCountsAsWrittenComesFromTheWritersAlone()
    {
        // No setvar anywhere, so nothing is written and EVERY operand must score nought. If the
        // seed were "every number anything names", 0x42 arg0 would score 100% here — it names
        // two numbers and both are named — and the question would answer itself.
        IReadOnlyList<OneOperand> all = Of(
        [
            .. Unknown(0x8004, 0x0009),
            .. Unknown(0x8008, 0x000A),
            End,
        ]);

        Assert.NotEmpty(all);
        Assert.All(all, o => Assert.Equal(0, o.Written));
    }

    /// <summary>
    /// And an operand naming too few numbers is dropped: one number is 0% or 100% by arithmetic,
    /// and neither figure is about the cartridge.
    /// </summary>
    [Fact]
    public void AnOperandNamingTooFewNumbersIsNotScored()
    {
        IReadOnlyList<OneOperand> all = EveryOperand.In(
            Image([.. SetVar(0x8004, 1), .. Unknown(0x8004, 0x0009), End]),
            [0x08000100],
            KnownWriters,
            leastNumbers: 2);

        // 0x42 names one number at each position here, so neither survives the floor.
        Assert.DoesNotContain(all, o => o.Code == 0x42);
    }

    // ---------------------------------------------------------------- and which way it goes

    /// <summary>
    /// THE DIRECTION TEST, which is a different question: an operand whose number the very next
    /// command compares left something there.
    /// </summary>
    /// <remarks>
    /// Written-ness says an operand names a variable and says nothing about which way the number
    /// goes. On this cartridge the floor is 1.5% of all operand places and the three that clear
    /// it are <c>specialvar</c>'s destination at 91%, <c>0x42 arg0</c> at 75%, and
    /// <c>copyvar</c>'s destination — a write this project already knew about — at 65%, which is
    /// the positive control sitting between the two unknowns.
    /// </remarks>
    [Fact]
    public void AnOperandComparedInTheNextBreathIsCounted()
    {
        IReadOnlyList<OneOperand> all = Of(
        [
            .. SetVar(0x8004, 1),
            .. SetVar(0x8008, 1),
            .. Unknown(0x8004, 0x0009),
            .. Compare(0x8004, 9),
            .. Unknown(0x8008, 0x000A),
            .. Compare(0x8008, 10),
            End,
        ]);

        Assert.Equal(2, Assert.Single(all, o => o.Name == "0x42 arg0").ComparedNext);
    }

    /// <summary>
    /// And a compare on a DIFFERENT number does not count — without the number check this counts
    /// every command that happens to be followed by a compare, which is a great many of them.
    /// </summary>
    [Fact]
    public void ACompareOnAnotherNumberIsNotThisOperandsAnswer()
    {
        IReadOnlyList<OneOperand> all = Of(
        [
            .. SetVar(0x8004, 1),
            .. SetVar(0x8008, 1),
            .. Unknown(0x8004, 0x0009),
            .. Compare(0x8008, 9),
            .. Unknown(0x8008, 0x000A),
            .. Compare(0x8004, 10),
            End,
        ]);

        Assert.Equal(0, Assert.Single(all, o => o.Name == "0x42 arg0").ComparedNext);
    }

    // ------------------------------------------------------------------------ the answer

    /// <summary>
    /// And the candidate list leaves out what the tables already name, or the answer is a list of
    /// the operands somebody already knew about.
    /// </summary>
    [Fact]
    public void WhatTheTablesAlreadyNameIsNotACandidate()
    {
        IReadOnlyList<OneOperand> all = Of(
        [
            .. SetVar(0x8004, 1),
            .. SetVar(0x8008, 1),
            .. Unknown(0x8004, 0x0009),
            .. Unknown(0x8008, 0x000A),
            End,
        ]);

        Assert.Equal(
            ["0x42 arg0"],
            [.. EveryOperand.Unknown(all, [(0x16, 0)]).Select(o => o.Name)]);

        Assert.Empty(EveryOperand.Unknown(all, [(0x16, 0), (0x42, 0)]));
    }

    /// <summary>
    /// And the spread is printed so the threshold can be seen doing no work. A rule with a number
    /// in it that nobody can check is a number nothing computes (231).
    /// </summary>
    [Fact]
    public void TheSpreadIsCountedInTenths()
    {
        IReadOnlyList<OneOperand> all = Of(
        [
            .. SetVar(0x8004, 1),
            .. SetVar(0x8008, 1),
            .. Unknown(0x8004, 0x0009),
            .. Unknown(0x8008, 0x000A),
            End,
        ]);

        IReadOnlyList<(int Tenth, int Operands)> spread = EveryOperand.Spread(all);

        Assert.Contains(spread, t => t.Tenth == 0);
        Assert.Contains(spread, t => t.Tenth == 9);
    }
}
