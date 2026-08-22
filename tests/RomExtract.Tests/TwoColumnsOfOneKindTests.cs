using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Two columns of one kind (302).
/// <para>
/// <c>0xA2</c> is four halfwords — a species, a species, an index and a nought-or-one — at 533 byte
/// positions on 30 maps. That the two are the same KIND of field is read off the PAIR and not off
/// the range: of the 134 pairs of operands of one command in the map scan where both take eight or
/// more distinct values, <c>0xA2 arg0</c> and <c>0xA2 arg2</c> share more of their union than any
/// other, 83.8% against the runner-up's 68.6%.
/// </para>
/// </summary>
public sealed class TwoColumnsOfOneKindTests
{
    /// <summary>
    /// <b>THE UNION, NOT THE SMALLER SET.</b> Scored against the smaller one, a pair where one
    /// operand takes two values and the other takes two hundred wins outright by containing it —
    /// which is a fact about the sizes and not about the fields.
    /// <para>
    /// The fixture is that shape: a contained pair that would score 100% against the smaller set
    /// and scores badly against the union, beside an equal pair that shares most of both. A version
    /// dividing by the smaller set ranks them the other way round.
    /// </para>
    /// </summary>
    [Fact]
    public void TheShareIsOfTheUnionAndNotOfTheSmallerSet()
    {
        HashSet<int> small = [1, 2];
        HashSet<int> big = [.. Enumerable.Range(1, 200)];

        HashSet<int> left = [.. Enumerable.Range(1, 35)];
        HashSet<int> right = [.. Enumerable.Range(5, 33)];

        // The contained pair shares ALL of the smaller set and almost none of the union.
        Assert.True(
            TwoColumnsOfOneKind.Share(left, right) > TwoColumnsOfOneKind.Share(small, big));

        // And against the smaller set the contained pair would win outright, which is the reading
        // this one is not: 2 of 2 beats 31 of 33.
        Assert.Equal(2, small.Intersect(big).Count());
        Assert.Equal(31, left.Intersect(right).Count());
    }

    /// <summary>
    /// A base at which every value lands on a ROM address is a candidate, and the reading counts
    /// them rather than picking one. On the cartridge there are 462 of them and NOUGHT in the
    /// reversed image — so the shape is real — and one of the 98 targets reads as text.
    /// <para>
    /// The fixture holds a table where both ids land on pointers and a second image where one of
    /// them does not, so a version that accepted a base on ANY id passes neither.
    /// </para>
    /// </summary>
    [Fact]
    public void ABaseCountsOnlyWhenEveryValueLandsOnAPointer()
    {
        var image = new byte[0x2000];

        // A table at 0x100 whose entries 3 and 5 are ROM addresses and whose entry 4 is not.
        Write(image, 0x100 + (4 * 3), 0x08001000);
        Write(image, 0x100 + (4 * 4), 0x00000000);
        Write(image, 0x100 + (4 * 5), 0x08001100);

        Assert.Contains(
            TwoColumnsOfOneKind.WhereTheTableCouldBe(image, [3, 5]),
            c => c.Base == 0x100);

        Assert.DoesNotContain(
            TwoColumnsOfOneKind.WhereTheTableCouldBe(image, [3, 4, 5]),
            c => c.Base == 0x100);
    }

    /// <summary>
    /// And a base whose entries all point at the SAME place is a candidate with one distinct
    /// target — which is what the distinct column is for, because a table of ninety-eight pointers
    /// to one address indexes nothing.
    /// </summary>
    [Fact]
    public void TheDistinctTargetsAreCountedSeparately()
    {
        var image = new byte[0x2000];

        Write(image, 0x200 + (4 * 3), 0x08001000);
        Write(image, 0x200 + (4 * 5), 0x08001000);

        (int _, int _, int distinct) = Assert.Single(
            TwoColumnsOfOneKind.WhereTheTableCouldBe(image, [3, 5]).Where(c => c.Base == 0x200));

        Assert.Equal(1, distinct);
    }

    /// <summary>An empty list of values is no hunt at all, rather than every base in the file.</summary>
    [Fact]
    public void NoValuesFindNoBases()
    {
        Assert.Empty(TwoColumnsOfOneKind.WhereTheTableCouldBe(new byte[0x2000], []));
    }

    private static void Write(byte[] image, int at, uint value)
    {
        for (var i = 0; i < 4; i++) image[at + i] = (byte)(value >> (i * 8));
    }
}
