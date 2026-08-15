using PokeMmo.Core.Cosmetics;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Something you can see somebody wearing.
/// <para>
/// Twelve slots, a wardrobe, a catalogue, an owned list, a server that decides whether
/// somebody owns what they claim, and a message that tells everybody else on the map —
/// all of it working, and all of it invisible. What a hat looked like was a four-pixel
/// square floating above the head, one per slot, in a row.
/// </para>
/// <para>
/// That placeholder was honest and it proved the hard half. This is the easy half, and
/// the tests here are the ones worth having about art: that there is some for everything
/// the shop can sell, that none of it is drawn outside the figure it belongs to, and that
/// the things which hang off somebody's back are behind them from the front and in front
/// of them from behind.
/// </para>
/// <para>
/// Every number in <c>CosmeticArt</c> is invented, which is allowed in that namespace and
/// nowhere else in this project. A test that asserted a hat is three pixels tall would be
/// a test of somebody's taste; these assert the things that would be bugs.
/// </para>
/// </summary>
public class SomethingToLookAtTests
{
    public static TheoryData<int> Everything()
    {
        var all = new TheoryData<int>();

        foreach (Cosmetic thing in Wardrobe.All) all.Add(thing.Id);

        return all;
    }

    /// <summary>Everything this game can sell can be seen from at least one side.</summary>
    [Theory]
    [MemberData(nameof(Everything))]
    public void EverythingInTheCatalogueHasArt(int id)
    {
        Assert.NotEmpty(
            CosmeticArt.For(id, Aspect.Front)
                .Concat(CosmeticArt.For(id, Aspect.Back))
                .Concat(CosmeticArt.For(id, Aspect.Side)));
    }

    /// <summary>And none of it is drawn outside the figure wearing it.</summary>
    [Theory]
    [MemberData(nameof(Everything))]
    public void AndNoneOfItSpillsOutOfTheFigure(int id)
    {
        foreach (Aspect aspect in new[] { Aspect.Front, Aspect.Back, Aspect.Side })
            foreach (Patch patch in CosmeticArt.For(id, aspect))
                Assert.True(patch.IsInsideTheBox, $"{id} from {aspect}: {patch}");
    }

    /// <summary>
    /// Eyes are not visible from behind somebody, and neither are the glasses over them.
    /// A face drawn on the back of a head is the kind of thing only a screenshot catches,
    /// which is why it is a test.
    /// </summary>
    [Theory]
    [InlineData(201)]
    [InlineData(202)]
    [InlineData(203)]
    [InlineData(401)]
    [InlineData(402)]
    public void AndNothingOnAFaceIsDrawnOnTheBackOfAHead(int id)
    {
        Assert.Empty(CosmeticArt.For(id, Aspect.Back));
    }

    /// <summary>
    /// What hangs off a back is behind somebody facing you and in front of one walking
    /// away — which is the whole reason the drawing happens in two passes.
    /// </summary>
    [Theory]
    [InlineData(1101)]
    [InlineData(1201)]
    public void AndWhatHangsOffABackChangesSidesWithTheFigure(int id)
    {
        Assert.True(CosmeticArt.GoesBehind(id, Aspect.Front));
        Assert.True(CosmeticArt.GoesBehind(id, Aspect.Side));
        Assert.False(CosmeticArt.GoesBehind(id, Aspect.Back));
    }

    /// <summary>And nothing else changes sides at all.</summary>
    [Theory]
    [MemberData(nameof(Everything))]
    public void AndNothingElseDoes(int id)
    {
        if (id is 1101 or 1201) return;

        foreach (Aspect aspect in new[] { Aspect.Front, Aspect.Back, Aspect.Side })
            Assert.False(CosmeticArt.GoesBehind(id, aspect));
    }

    /// <summary>Something this game has never heard of is drawn as nothing, not as a crash.</summary>
    [Fact]
    public void AndSomethingUnknownIsDrawnAsNothing()
    {
        Assert.Empty(CosmeticArt.For(999_999, Aspect.Front));
        Assert.False(CosmeticArt.GoesBehind(999_999, Aspect.Front));
    }

    /// <summary>
    /// A dress covers what a shirt and trousers would, because it is worn instead of them
    /// — the rule the wardrobe already enforces, asserted here against the art so the two
    /// cannot drift apart.
    /// </summary>
    [Fact]
    public void ADressCoversWhatItIsWornInsteadOf()
    {
        int top = CosmeticArt.For(901, Aspect.Front).Min(p => p.Y);
        int bottom = CosmeticArt.For(901, Aspect.Front).Max(p => p.Y + p.Height);

        int shirtTop = CosmeticArt.For(601, Aspect.Front).Min(p => p.Y);
        int legsBottom = CosmeticArt.For(701, Aspect.Front).Max(p => p.Y + p.Height);

        Assert.True(top <= shirtTop);
        Assert.True(bottom > shirtTop);
        Assert.True(bottom <= legsBottom);
    }

    /// <summary>
    /// And every one of the twelve slots has something in the catalogue to put in it, so
    /// the wardrobe never shows an empty list.
    /// </summary>
    [Theory]
    [InlineData(CosmeticSlot.Hair)]
    [InlineData(CosmeticSlot.Eyes)]
    [InlineData(CosmeticSlot.Hat)]
    [InlineData(CosmeticSlot.Glasses)]
    [InlineData(CosmeticSlot.Scarf)]
    [InlineData(CosmeticSlot.Shirt)]
    [InlineData(CosmeticSlot.Pants)]
    [InlineData(CosmeticSlot.Skirt)]
    [InlineData(CosmeticSlot.Dress)]
    [InlineData(CosmeticSlot.Shoes)]
    [InlineData(CosmeticSlot.Cape)]
    [InlineData(CosmeticSlot.Backpack)]
    public void AndEverySlotHasSomethingToPutInIt(CosmeticSlot slot)
    {
        Assert.NotEmpty(Wardrobe.All.Where(c => c.Slot == slot));
    }
}
