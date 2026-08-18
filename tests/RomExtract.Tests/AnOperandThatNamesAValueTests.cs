using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The operand that names a value, told apart from the ones that name a variable.
/// <para>
/// <b>243 reported 27 numbers used as a flag and as a variable, against a floor of 1.71.</b>
/// Twenty-six of them were one command's second word: <c>0x1A</c> takes a destination and a
/// SOURCE, and the source is a plain number unless it is a variable id. Counted as a look at a
/// variable, a literal 5 handed to a routine becomes a use of variable 5 — and 5 is also a real
/// flag, so it lands in both namespaces at once.
/// </para>
/// <para>
/// <b>The test needs no band boundary from outside the file.</b> A variable something looks at is
/// a variable something writes. Every other reading operand in this cartridge names numbers that
/// are written 86% to 100% of the time; <c>0x1A arg2</c> comes in at 2%, three of a hundred and
/// forty-nine, with nothing in between. Half is the threshold and it is deliberately doing no
/// work.
/// </para>
/// </summary>
public sealed class AnOperandThatNamesAValueTests
{
    /// <summary>A byte this project has no width for, so a drifting read stops at once.</summary>
    private const byte NoWidth = 0xFF;

    private const byte End = 0x02;

    private static Rom Image(params (int At, byte[] Bytes)[] pieces)
    {
        var data = new byte[0x1000];

        Array.Fill(data, NoWidth);

        foreach ((int at, byte[] bytes) in pieces) bytes.CopyTo(data, at);

        return new Rom(data);
    }

    private static byte[] SetVar(int variable, int value) =>
        [0x16, (byte)variable, (byte)(variable >> 8), (byte)value, (byte)(value >> 8)];

    private static byte[] Compare(int variable, int value) =>
        [0x21, (byte)variable, (byte)(variable >> 8), (byte)value, (byte)(value >> 8)];

    /// <summary>The one whose second word is a value unless it is a variable id.</summary>
    private static byte[] CopyIfNotZero(int to, int from) =>
        [0x1A, (byte)to, (byte)(to >> 8), (byte)from, (byte)(from >> 8)];

    private static byte[] SetFlag(int flag) => [0x29, (byte)flag, (byte)(flag >> 8)];

    private static BothNamespaces Of(Rom rom) => TwoNamespacesOneNumber.Of(rom, [0x08000100]);

    // ------------------------------------------------------------------ the written-ness test

    /// <summary>
    /// THE THING: an operand naming numbers nothing writes is naming values, and one naming
    /// numbers the file writes is naming variables.
    /// </summary>
    [Fact]
    public void AnOperandNamingWhatNothingWritesIsNamingValues()
    {
        // 0x8004 is written and looked at — a variable. The four numbers handed to 0x1A's
        // second word are written nowhere.
        BothNamespaces both = Of(Image((0x100,
        [
            .. SetVar(0x8004, 1),
            .. Compare(0x8004, 3),
            .. CopyIfNotZero(0x8004, 0x0005),
            .. CopyIfNotZero(0x8004, 0x0006),
            .. CopyIfNotZero(0x8004, 0x0007),
            .. CopyIfNotZero(0x8004, 0x0008),
            End,
        ])));

        Assert.Equal(["0x1A arg2"], [.. both.NameValues]);
    }

    /// <summary>
    /// And an operand naming things the file DOES write is left alone — the half without which
    /// "every reading operand names values" passes the test above.
    /// </summary>
    [Fact]
    public void AnOperandNamingWhatTheFileWritesIsNamingVariables()
    {
        BothNamespaces both = Of(Image((0x100,
        [
            .. SetVar(0x4050, 1),
            .. SetVar(0x4051, 1),
            .. CopyIfNotZero(0x8004, 0x4050),
            .. CopyIfNotZero(0x8004, 0x4051),
            End,
        ])));

        Assert.Empty(both.NameValues);
    }

    /// <summary>
    /// And the percentages are reported per operand, so the threshold can be seen not to be
    /// doing the work. A rule with a number in it that nobody can check is a number nothing
    /// computes.
    /// </summary>
    [Fact]
    public void ItSaysHowMuchOfEachReadingOperandIsEverWritten()
    {
        BothNamespaces both = Of(Image((0x100,
        [
            .. SetVar(0x4050, 1),
            .. Compare(0x4050, 3),
            .. CopyIfNotZero(0x8004, 0x0005),
            End,
        ])));

        (string operand, int written, int numbers) compare =
            Assert.Single(both.WrittenPerOperand, o => o.Operand == "0x21 arg0");

        Assert.Equal((1, 1), (compare.written, compare.numbers));

        (string operand, int written, int numbers) copies =
            Assert.Single(both.WrittenPerOperand, o => o.Operand == "0x1A arg2");

        Assert.Equal((0, 1), (copies.written, copies.numbers));
    }

    // ------------------------------------------------------------------ and what it costs

    /// <summary>
    /// THE CONSEQUENCE: a literal that happens to equal a flag number is not a number used both
    /// ways, and the raw count says it is.
    /// </summary>
    [Fact]
    public void ALiteralThatEqualsAFlagNumberIsNotANumberUsedBothWays()
    {
        // 0x0005 is a real flag AND the literal handed to a routine. Nothing else names it.
        BothNamespaces both = Of(Image((0x100,
        [
            .. SetFlag(0x0005),
            .. SetVar(0x8004, 1),
            .. CopyIfNotZero(0x8004, 0x0005),
            .. CopyIfNotZero(0x8004, 0x0006),
            .. CopyIfNotZero(0x8004, 0x0007),
            End,
        ])));

        Assert.Equal([0x0005], [.. both.Shared.Select(n => n.Number)]);
        Assert.Empty(both.SharedRealVariables);
    }

    /// <summary>
    /// And a number genuinely used both ways survives — without this, "report nothing" passes
    /// the test above and 0x4001 would have gone with the other twenty-six.
    /// </summary>
    [Fact]
    public void ANumberGenuinelyUsedBothWaysSurvives()
    {
        BothNamespaces both = Of(Image((0x100,
        [
            .. SetFlag(0x4050),
            .. SetVar(0x4050, 1),
            .. Compare(0x4050, 2),
            .. CopyIfNotZero(0x8004, 0x0005),
            .. CopyIfNotZero(0x8004, 0x0006),
            .. CopyIfNotZero(0x8004, 0x0007),
            End,
        ])));

        SharedNumber real = Assert.Single(both.SharedRealVariables);

        Assert.Equal(0x4050, real.Number);
    }

    // ------------------------------------------------- written and never looked at

    /// <summary>
    /// THE THING 214 NEEDED: a variable something writes and no LOOKING operand ever names.
    /// </summary>
    [Fact]
    public void AVariableWrittenAndNeverLookedAtIsReported()
    {
        BothNamespaces both = Of(Image((0x100,
        [
            .. SetVar(0x4050, 1),
            .. SetVar(0x4051, 1),
            .. Compare(0x4051, 2),
            End,
        ])));

        Assert.Equal([0x4050], [.. both.WrittenAndNeverLookedAt]);
    }

    /// <summary>
    /// THE DISCRIMINATION: being handed to a routine as a LITERAL is not being looked at, so a
    /// deaf variable stays deaf however many times its number appears as a value.
    /// </summary>
    /// <remarks>
    /// Without this, one script passing the literal 0x4050 to a routine silently answers "no,
    /// something reads it" — which is the direction that makes a finding disappear rather than
    /// appear, and is therefore the one nothing would ever notice.
    /// </remarks>
    [Fact]
    public void BeingHandedToARoutineAsALiteralIsNotBeingLookedAt()
    {
        BothNamespaces both = Of(Image((0x100,
        [
            .. SetVar(0x4050, 1),
            .. SetVar(0x8004, 1),
            .. CopyIfNotZero(0x8004, 0x4050),
            .. CopyIfNotZero(0x8004, 0x0005),
            .. CopyIfNotZero(0x8004, 0x0006),
            .. CopyIfNotZero(0x8004, 0x0007),
            End,
        ])));

        // 0x1A arg2 names four numbers here and one of them is written, which is under half —
        // so it names values, and 0x4050 is deaf despite appearing as one.
        Assert.Contains(0x4050, both.WrittenAndNeverLookedAt);

        // And the raw reading loses it, which is what makes the line above worth printing.
        Assert.DoesNotContain(0x4050, both.WrittenAndNeverReadRaw);
    }

    /// <summary>
    /// And it is about what is WRITTEN: a number nothing writes is not a variable written and
    /// never looked at, however absent it is from every reading operand.
    /// </summary>
    [Fact]
    public void ANumberNothingWritesIsNotAVariableWrittenAndNeverLookedAt()
    {
        BothNamespaces both = Of(Image((0x100, [.. SetFlag(0x0025), End])));

        Assert.Empty(both.WrittenAndNeverLookedAt);
    }

    // ------------------------------------------------------------------------- the shape

    /// <summary>
    /// The bands are READ and not written down. Which numbers are legal variables is not
    /// something this project may assert from outside the file, so the spread is printed and
    /// the bands show themselves.
    /// </summary>
    [Fact]
    public void TheBandsAreCountedByNumberAndByPlace()
    {
        BothNamespaces both = Of(Image((0x100,
        [
            .. SetVar(0x8004, 1),
            .. SetVar(0x8004, 2),
            .. SetVar(0x4050, 1),
            End,
        ])));

        IReadOnlyList<(int From, int Numbers, int Places)> bands =
            BothNamespaces.Bands(both.Variables);

        Assert.Equal([(0x4000, 1, 1), (0x8000, 1, 2)], [.. bands]);
    }

    /// <summary>
    /// One number named twice and two numbers named once are the same count and not the same
    /// band — so places are carried beside numbers.
    /// </summary>
    [Fact]
    public void OneNumberTwiceAndTwoNumbersOnceAreNotTheSameBand()
    {
        BothNamespaces twice = Of(Image((0x100, [.. SetVar(0x4050, 1), .. SetVar(0x4050, 2), End])));
        BothNamespaces two = Of(Image((0x100, [.. SetVar(0x4050, 1), .. SetVar(0x4051, 1), End])));

        Assert.Equal([(0x4000, 1, 2)], [.. BothNamespaces.Bands(twice.Variables)]);
        Assert.Equal([(0x4000, 2, 2)], [.. BothNamespaces.Bands(two.Variables)]);
    }
}
