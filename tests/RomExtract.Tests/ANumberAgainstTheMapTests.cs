using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Whether the numbers a command's operand is compared against are columns or rows.
/// <para>
/// <b>The test that named <c>0x42</c>.</b> 252 established that it writes its first operand and
/// 253 left it as the last thing the operand audit could not settle. Its eight places are
/// followed by compares against 6, 7, 9, 9, 18, 24 and 50, which say nothing on their own — and
/// a great deal against the map they are on. Twenty-four on a map twenty-four tall is not a row.
/// </para>
/// <para>
/// <b>The negative controls are the point.</b> Asked of <c>specialvar</c>'s answer variable — a
/// routine's reply, compared 326 times — every value fits both bounds and the test refuses to
/// name it. Same for <c>copyvar</c>'s destination. A discrimination that named those two would be
/// naming everything.
/// </para>
/// </summary>
public sealed class ANumberAgainstTheMapTests
{
    private const byte NoWidth = 0xFF;

    private const int Where = 0x100;

    private static Rom Image(params byte[] bytes)
    {
        var data = new byte[0x1000];

        Array.Fill(data, NoWidth);

        bytes.CopyTo(data, Where);

        return new Rom(data);
    }

    /// <summary>The command under test: two halfwords, the first of which is asked about.</summary>
    private static byte[] Leaves(int first, int second) =>
        [0x42, (byte)first, (byte)(first >> 8), (byte)second, (byte)(second >> 8)];

    private static byte[] Compare(int variable, int value) =>
        [0x21, (byte)variable, (byte)(variable >> 8), (byte)value, (byte)(value >> 8)];

    private static IReadOnlyList<AgainstTheMap> On(int width, int height, params byte[] bytes) =>
        ANumberAgainstTheMap.In(
            Image(bytes), [Where], 0, _ => "1.121", _ => (width, height));

    // -------------------------------------------------------------------- the discrimination

    /// <summary>
    /// THE THING: a value inside the width and outside the height can only be a column.
    /// </summary>
    [Fact]
    public void AValueTooBigForTheHeightIsAColumnAndNotARow()
    {
        AgainstTheMap one = Assert.Single(
            On(60, 32, [.. Leaves(0x4001, 0x4002), .. Compare(0x4001, 50)]));

        Assert.True(one.FitsTheWidth);
        Assert.False(one.FitsTheHeight);
        Assert.Equal((50, 60, 32), (one.Value, one.Width, one.Height));
    }

    /// <summary>
    /// AND THE ANSWER IT HAS TO BE ABLE TO GIVE: a value inside both bounds names nothing. On a
    /// square map every column is a row, and a test that called that a column would call anything
    /// one.
    /// </summary>
    [Fact]
    public void AValueInsideBothBoundsNamesNothing()
    {
        AgainstTheMap one = Assert.Single(
            On(24, 40, [.. Leaves(0x8004, 0x8005), .. Compare(0x8004, 9)]));

        Assert.True(one.FitsTheWidth);
        Assert.True(one.FitsTheHeight);
    }

    /// <summary>
    /// And the verdict carries all four counts, because "every value fits the width" is only a
    /// finding beside "and some of them do not fit the height".
    /// </summary>
    [Fact]
    public void TheVerdictCountsColumnsRowsOnlyAndNeither()
    {
        IReadOnlyList<AgainstTheMap> found =
        [
            new AgainstTheMap(0, "1.121", 50, 60, 32),
            new AgainstTheMap(1, "1.121", 9, 60, 32),
            new AgainstTheMap(2, "1.121", 99, 60, 32),
        ];

        Assert.Equal((3, 2, 1, 1, 1), ANumberAgainstTheMap.Verdict(found));
    }

    // ---------------------------------------------------------------------- what is skipped

    /// <summary>
    /// AND ONLY THE COMPARE ON THIS VERY NUMBER COUNTS. A script asking about something else in
    /// the next breath is common, and counting it reads that question's value as this command's.
    /// </summary>
    [Fact]
    public void ACompareOnAnotherNumberIsNotThisOperandsValue()
    {
        Assert.Empty(On(60, 32, [.. Leaves(0x4001, 0x4002), .. Compare(0x4002, 50)]));
    }

    /// <summary>And a place with no compare after it at all contributes nothing.</summary>
    [Fact]
    public void APlaceWithNoCompareAfterItIsSkipped()
    {
        Assert.Empty(On(60, 32, [.. Leaves(0x4001, 0x4002), 0x02]));
    }

    /// <summary>
    /// And a place on no map is skipped — a value with no bounds to be inside cannot be tested,
    /// and defaulting it to some map's size would be inventing the discrimination.
    /// </summary>
    [Fact]
    public void APlaceNothingOpenedIsSkipped()
    {
        Assert.Empty(
            ANumberAgainstTheMap.In(
                Image([.. Leaves(0x4001, 0x4002), .. Compare(0x4001, 50)]),
                [Where],
                0,
                _ => null,
                _ => (60, 32)));
    }

    /// <summary>And a map whose size is not known is skipped for the same reason.</summary>
    [Fact]
    public void AMapWithNoSizeIsSkipped()
    {
        Assert.Empty(
            ANumberAgainstTheMap.In(
                Image([.. Leaves(0x4001, 0x4002), .. Compare(0x4001, 50)]),
                [Where],
                0,
                _ => "1.121",
                _ => null));
    }
}
