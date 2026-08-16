using PokeMmo.Core.Data;
using PokeMmo.RomExtract;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// How many boxes there are, and the negative that turned out to be the answer.
/// <para>
/// This project's rule is that a number is read or it is marked as modelled. How many boxes
/// there are was neither: it was one, because the comment on <see cref="BoxCapacity"/> said
/// nothing in the image says how many there are — which was an assumption nobody had tested,
/// sitting in a file whose whole point is not making assumptions.
/// </para>
/// <para>
/// So it was looked for. The locator below would find a run of default names on a cartridge
/// that had one, and on this one it finds the word BOX forty-six times and never numbered.
/// The count is modelled now, and it is modelled with evidence behind the choice rather than
/// instead of a search.
/// </para>
/// </summary>
public class BoxNamesTests
{
    /// <summary>
    /// An image with a run of names in it, so the locator is proved against something it can
    /// find rather than only against something it cannot.
    /// <para>
    /// This matters more than it looks. A locator that always returns nothing would pass
    /// every test written against this cartridge, and would then quietly answer "one box" on
    /// a cartridge that had twenty.
    /// </para>
    /// </summary>
    private static Rom WithNames(int howMany, int stride = 9)
    {
        var bytes = new byte[0x2000];

        // Filled with something that is not a name, so a run has to be the run rather than
        // the whole image agreeing with itself.
        Array.Fill(bytes, (byte)0xFF);

        int at = 0x100;

        for (int box = 1; box <= howMany; box++)
        {
            byte[] name = GameText.Encode($"BOX {box}", stride);

            Array.Copy(name, 0, bytes, at + (box - 1) * stride, Math.Min(name.Length, stride));
        }

        return new Rom(bytes);
    }

    [Fact]
    public void ARunOfNamesIsFoundAndCounted()
    {
        (int At, int Count, int Stride)? found = BoxNames.Locate(WithNames(14));

        Assert.NotNull(found);
        Assert.Equal(14, found!.Value.Count);
        Assert.Equal(9, found.Value.Stride);
        Assert.Equal(0x100, found.Value.At);
    }

    /// <summary>
    /// A different number of boxes gives a different answer, which is what says the count is
    /// counted rather than known.
    /// </summary>
    [Fact]
    public void ADifferentCartridgeGivesADifferentCount()
    {
        Assert.Equal(8, BoxNames.Locate(WithNames(8))?.Count);
        Assert.Equal(20, BoxNames.Locate(WithNames(20))?.Count);
    }

    /// <summary>
    /// And the stride is measured rather than assumed, so a table padded to a different width
    /// is still a table.
    /// </summary>
    [Fact]
    public void TheStrideIsMeasuredRatherThanAssumed()
    {
        (int At, int Count, int Stride)? wide = BoxNames.Locate(WithNames(12, stride: 16));

        Assert.Equal(12, wide?.Count);
        Assert.Equal(16, wide?.Stride);
    }

    /// <summary>
    /// One name is not a run. A single "BOX 1" somewhere in a sentence is a coincidence, and
    /// a locator that counted it would answer one box for the wrong reason.
    /// </summary>
    [Fact]
    public void OneNameOnItsOwnIsNotARun()
    {
        Assert.Null(BoxNames.Locate(WithNames(1)));
    }

    /// <summary>
    /// And on a cartridge with no run at all, nothing — which is what this project's own
    /// image gives and why the count is modelled.
    /// </summary>
    [Fact]
    public void NoRunMeansNothingRatherThanAGuess()
    {
        var empty = new byte[0x1000];

        Array.Fill(empty, (byte)0xFF);

        Assert.Null(BoxNames.Locate(new Rom(empty)));
    }

    // ---- what the count is used for ----------------------------------------------------

    [Fact]
    public void StorageIsTheProductAndIsComputedRatherThanStored()
    {
        var rules = new GameRules([], [], [], [], []) { BoxSize = 30, Boxes = 8 };

        Assert.Equal(240, rules.Storage);

        // And changing either changes it, which is what "computed" means and what a stored
        // third copy of the same fact would eventually stop doing.
        Assert.Equal(60, new GameRules([], [], [], [], []) { BoxSize = 30, Boxes = 2 }.Storage);
        Assert.Equal(160, new GameRules([], [], [], [], []) { BoxSize = 20, Boxes = 8 }.Storage);
    }

    /// <summary>
    /// A cartridge whose box size could not be read stores nothing, rather than storing
    /// eight boxes of nothing.
    /// </summary>
    [Fact]
    public void NoBoxSizeMeansNowhereToPutAnything()
    {
        var rules = new GameRules([], [], [], [], []) { BoxSize = 0 };

        Assert.Equal(0, rules.Storage);
    }

    /// <summary>
    /// The default is this project's number and is stated once, so that changing it is one
    /// edit rather than a hunt.
    /// </summary>
    [Fact]
    public void TheDefaultIsStatedInExactlyOnePlace()
    {
        var rules = new GameRules([], [], [], [], []) { BoxSize = 30 };

        Assert.Equal(GameRules.Default, rules.Boxes);
        Assert.Equal(30 * GameRules.Default, rules.Storage);

        // More than one, because one box was what this project had before anybody looked
        // and is the thing this milestone is about not being.
        Assert.True(GameRules.Default > 1);
    }
}
