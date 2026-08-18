using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The numbers this cartridge uses as a flag and as a variable at once.
/// <para>
/// <b>240 printed "nothing the run executed touched 0x003F" about a flag a script had cleared on
/// the same run.</b> <c>--trace</c> watches a VARIABLE; the command line takes a bare number and
/// cannot tell the two apart, and 0x003F is named three times each way. The instruments are all
/// safe — every one of them decides by the COMMAND — and the argument is not.
/// </para>
/// <para>
/// Asked of the map scan and never of the image: the same sweep over sixteen megabytes answers
/// 2117 / 12659 / 1182, which is a fact about three-byte patterns in graphics and not about the
/// game. 233 threw a raw sweep away for the same reason.
/// </para>
/// </summary>
public sealed class OneNumberTwoNamespacesTests
{
    /// <summary>
    /// Filled with a byte this project has no width for, so a read that drifts by one stops at
    /// once rather than sliding through a field of no-ops.
    /// </summary>
    private const byte NoWidth = 0xFF;

    private static Rom Image(params (int At, byte[] Bytes)[] pieces)
    {
        var data = new byte[0x1000];

        Array.Fill(data, NoWidth);

        foreach ((int at, byte[] bytes) in pieces) bytes.CopyTo(data, at);

        return new Rom(data);
    }

    private static byte[] SetFlag(int flag) => [0x29, (byte)flag, (byte)(flag >> 8)];

    private static byte[] SetVar(int variable, int value) =>
        [0x16, (byte)variable, (byte)(variable >> 8), (byte)value, (byte)(value >> 8)];

    private static byte[] CopyVar(int to, int from) =>
        [0x19, (byte)to, (byte)(to >> 8), (byte)from, (byte)(from >> 8)];

    private static byte[] CompareVars(int first, int second) =>
        [0x22, (byte)first, (byte)(first >> 8), (byte)second, (byte)(second >> 8)];

    private static byte[] Goto(uint to) => [0x05, .. BitConverter.GetBytes(to)];

    private const byte End = 0x02;

    private static BothNamespaces Of(Rom rom, params uint[] from) =>
        TwoNamespacesOneNumber.Of(rom, from);

    // ------------------------------------------------------------------ the two namespaces

    /// <summary>
    /// THE THING: one number named by a <c>setflag</c> and by a <c>setvar</c> is used both ways,
    /// and the two counts are kept apart.
    /// </summary>
    [Fact]
    public void ANumberNamedAsAFlagAndAsAVariableIsUsedBothWays()
    {
        BothNamespaces both = Of(
            Image((0x100, [.. SetFlag(0x4001), .. SetVar(0x4001, 3), End])), 0x08000100);

        SharedNumber shared = Assert.Single(both.Shared);

        Assert.Equal(0x4001, shared.Number);
        Assert.Equal(1, shared.AsAFlag);
        Assert.Equal(1, shared.AsAVariable);
    }

    /// <summary>
    /// And a number named only one way is not — the half without which "everything is shared"
    /// passes the test above.
    /// </summary>
    [Fact]
    public void ANumberNamedOnlyOneWayIsNotShared()
    {
        BothNamespaces both = Of(
            Image((0x100, [.. SetFlag(0x0025), .. SetVar(0x4050, 1), End])), 0x08000100);

        Assert.Empty(both.Shared);
        Assert.Equal([0x0025], [.. both.Flags.Keys]);
        Assert.Equal([0x4050], [.. both.Variables.Keys]);
    }

    /// <summary>
    /// THE DISCRIMINATION: <c>copyvar</c> names two variables and only the SOURCE is a read.
    /// Counting the destination as well makes every write a read and every copied-into variable
    /// look like one something looks at.
    /// </summary>
    /// <remarks>
    /// Both halves are asserted, because a rule that counted neither operand would pass a test
    /// that only checked the destination was absent.
    /// </remarks>
    [Fact]
    public void CopyVarNamesBothItsOperandsAndTheyAreCountedOnce()
    {
        BothNamespaces both = Of(
            Image((0x100, [.. CopyVar(0x4060, 0x4061), End])), 0x08000100);

        // The source is read; the destination is named by the same command and this sweep does
        // not care which side it was — what it must not do is count one command twice.
        Assert.Equal(1, both.Variables.GetValueOrDefault(0x4061));
        Assert.Equal(0, both.Variables.GetValueOrDefault(0x4060));
    }

    /// <summary>And <c>comparevars</c> looks at BOTH of its operands, which is a different rule.</summary>
    [Fact]
    public void CompareVarsLooksAtBothOfItsOperands()
    {
        BothNamespaces both = Of(
            Image((0x100, [.. CompareVars(0x4060, 0x4061), End])), 0x08000100);

        Assert.Equal(1, both.Variables.GetValueOrDefault(0x4060));
        Assert.Equal(1, both.Variables.GetValueOrDefault(0x4061));
    }

    // ------------------------------------------------------------------------ the counting

    /// <summary>
    /// A block reached from two places is read ONCE. Counting it twice inflates both namespaces
    /// by the same factor and the floor by the square of it, which is the direction that turns
    /// nothing into a finding.
    /// </summary>
    [Fact]
    public void ABlockReachedTwiceIsReadOnce()
    {
        Rom rom = Image(
            (0x100, [.. Goto(0x08000200)]),
            (0x180, [.. Goto(0x08000200)]),
            (0x200, [.. SetFlag(0x4001), .. SetVar(0x4001, 1), End]));

        BothNamespaces once = Of(rom, 0x08000100);
        BothNamespaces twice = Of(rom, 0x08000100, 0x08000180);

        Assert.Equal(1, twice.Flags[0x4001]);
        Assert.Equal(1, twice.Variables[0x4001]);
        Assert.Equal(once.Flags[0x4001], twice.Flags[0x4001]);
    }

    /// <summary>
    /// And the commands are counted, so the two sets above have a denominator. A sweep that read
    /// nothing and one that found nothing print the same two zeroes otherwise.
    /// </summary>
    [Fact]
    public void ItSaysHowManyCommandsItRead()
    {
        BothNamespaces both = Of(
            Image((0x100, [.. SetFlag(0x0025), .. SetVar(0x4050, 1), End])), 0x08000100);

        Assert.Equal(3, both.Commands);
        Assert.Equal(0, Of(Image()).Commands);
    }

    // ---------------------------------------------------------------------------- the floor

    /// <summary>
    /// The floor is nought when either namespace is empty — an overlap of nought against a floor
    /// of nought is the honest way to say the question does not arise.
    /// </summary>
    [Fact]
    public void WithOnlyOneNamespaceInUseTheFloorIsNought()
    {
        BothNamespaces both = Of(Image((0x100, [.. SetFlag(0x0025), End])), 0x08000100);

        Assert.Empty(both.Shared);
        Assert.Equal(0, both.Floor);
    }

    /// <summary>
    /// And it is bigger when the two sets are crowded into a narrow span than when they are
    /// spread over a wide one — which is what makes the span version the CONSERVATIVE floor.
    /// </summary>
    [Fact]
    public void TheFloorIsHigherWhenTheNumbersAreCrowded()
    {
        BothNamespaces crowded = Of(
            Image((0x100,
            [
                .. SetFlag(0x0001), .. SetFlag(0x0002),
                .. SetVar(0x0003, 0), .. SetVar(0x0004, 0), End,
            ])),
            0x08000100);

        BothNamespaces spread = Of(
            Image((0x100,
            [
                .. SetFlag(0x0001), .. SetFlag(0x0002),
                .. SetVar(0x4003, 0), .. SetVar(0x4004, 0), End,
            ])),
            0x08000100);

        Assert.Empty(crowded.Shared);
        Assert.Empty(spread.Shared);
        Assert.True(
            crowded.Floor > spread.Floor,
            $"crowded {crowded.Floor:F4} should be above spread {spread.Floor:F4}");
    }
}
