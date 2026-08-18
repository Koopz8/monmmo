using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// One question asked of any command that takes a byte and then a word: is the byte an index?
/// <para>
/// Three of this cartridge's unnamed commands have that width and they do <b>not</b> mean the same
/// thing. <c>0x9D</c> runs three in a row on <c>10.14</c> saying <c>0, 255</c> / <c>1, 10</c> /
/// <c>2, 14</c> and one alone in each of the three obstacle scripts saying <c>0, 0x800D</c> —
/// every run counting from nought, nine byte positions, one in 3⁹. <c>0x82</c>'s byte is
/// <b>1 at all seven of its places</b>, and <c>0x7F</c>'s is 0 at all three.
/// </para>
/// <para>
/// So two of the three come back <b>unanswerable</b> rather than yes, and that is the behaviour
/// this file mostly guards: a byte with one value counts from nought whenever that value is
/// nought, and reporting it as an index would be a finding made of nothing.
/// </para>
/// </summary>
public sealed class IsTheFirstByteAnIndexTests
{
    private static ScriptCommand One(int at, byte code, int first, int word) =>
        new(at, code, [(byte)first, (byte)(word & 0xFF), (byte)(word >> 8)]);

    private static ScriptCommand Other(int at) => new(at, 0x68, []);

    // ------------------------------------------------------------- counting from nought

    /// <summary>
    /// THE DISCRIMINATION: every element, not most. A run whose bytes are 0, 1, 3 is not counting,
    /// and calling it counting makes the answer out of the two that happened to line up.
    /// </summary>
    [Fact]
    public void EveryElementOrItIsNotCounting()
    {
        Assert.True(new AByteThenAWord.Run(0x100, [0, 1, 2], [1, 2, 3]).CountsFromNought);
        Assert.False(new AByteThenAWord.Run(0x100, [0, 1, 3], [1, 2, 3]).CountsFromNought);
        Assert.False(new AByteThenAWord.Run(0x100, [1, 2, 3], [1, 2, 3]).CountsFromNought);
    }

    /// <summary>
    /// And a run of ONE counts only if its byte is nought — which is the whole difference between
    /// <c>0x7F</c> (0 at three places) and <c>0x82</c> (1 at seven).
    /// </summary>
    [Fact]
    public void ARunOfOneCountsOnlyIfItsByteIsNought()
    {
        Assert.True(new AByteThenAWord.Run(0x100, [0], [0x800D]).CountsFromNought);
        Assert.False(new AByteThenAWord.Run(0x100, [1], [15]).CountsFromNought);
    }

    // ------------------------------------------------------------------- what a run is

    /// <summary>
    /// A run is CONSECUTIVE. Two stretches of the same command with something else between them
    /// are two runs, and joining them would invent a count that never appears in the bytes.
    /// </summary>
    [Fact]
    public void SomethingElseBetweenThemMakesThemTwoRuns()
    {
        var runs = new Dictionary<int, AByteThenAWord.Run>();

        AByteThenAWord.Gather(
            [
                One(0x100, 0x9D, 0, 255), One(0x104, 0x9D, 1, 10),
                Other(0x108),
                One(0x109, 0x9D, 0, 14),
            ],
            0x9D,
            runs);

        Assert.Equal(2, runs.Count);
        Assert.Equal([0, 1], runs[0x100].Bytes);
        Assert.Equal([0], runs[0x109].Bytes);
    }

    /// <summary>
    /// And one block read twice is ONE run. A block hanging off twenty doors is decoded twenty
    /// times; <c>0x9D</c>'s five runs read as twenty-three without this.
    /// </summary>
    [Fact]
    public void OneRunReadTwiceIsOneRun()
    {
        var runs = new Dictionary<int, AByteThenAWord.Run>();

        ScriptCommand[] block = [One(0x100, 0x9D, 0, 255), One(0x104, 0x9D, 1, 10)];

        AByteThenAWord.Gather(block, 0x9D, runs);
        AByteThenAWord.Gather(block, 0x9D, runs);

        Assert.Single(runs);
        Assert.Equal(2, runs[0x100].Bytes.Count);
    }

    // ---------------------------------------------------------------------- the floor

    /// <summary>
    /// THE HONESTY: a byte that only ever takes ONE value counts from nought whenever that value
    /// is nought, so the question cannot be answered — not answered yes.
    /// </summary>
    [Fact]
    public void OneValueMeansTheQuestionCannotBeAnswered()
    {
        AByteThenAWord.Reading only = AByteThenAWord.Of(
            0x7F,
            [
                new AByteThenAWord.Run(0x100, [0], [0x800D]),
                new AByteThenAWord.Run(0x200, [0], [0x800D]),
            ]);

        Assert.True(only.AlwaysCounts);
        Assert.False(only.CanSayAnything);
        Assert.Equal(1, only.OneIn);
    }

    /// <summary>
    /// The floor is the alphabet to the power of the PLACES, not of the runs — every position has
    /// to be right, and two runs of three is six bytes rather than two.
    /// </summary>
    [Fact]
    public void TheFloorIsOverEveryPlaceAndNotEveryRun()
    {
        AByteThenAWord.Reading counting = AByteThenAWord.Of(
            0x9D,
            [
                new AByteThenAWord.Run(0x100, [0, 1, 2], [1, 2, 3]),
                new AByteThenAWord.Run(0x200, [0, 1, 2], [4, 5, 6]),
            ]);

        Assert.True(counting.AlwaysCounts);
        Assert.True(counting.CanSayAnything);
        Assert.Equal(6, counting.Places);
        Assert.Equal(3, counting.Alphabet);
        Assert.Equal(729, counting.OneIn);
    }

    /// <summary>
    /// And a command whose runs do not all count gets no floor at all — the answer is no, and a
    /// number beside it would read as evidence for something.
    /// </summary>
    [Fact]
    public void ACommandThatDoesNotCountGetsNoFloor()
    {
        AByteThenAWord.Reading mixed = AByteThenAWord.Of(
            0x82,
            [
                new AByteThenAWord.Run(0x100, [0, 1], [1, 2]),
                new AByteThenAWord.Run(0x200, [1], [15]),
            ]);

        Assert.False(mixed.AlwaysCounts);
        Assert.Equal(1, mixed.Counting);
        Assert.Equal(1, mixed.OneIn);
    }

    /// <summary>
    /// And the ordinary argument-column test alongside it: how many DISTINCT words, and how many
    /// are variable ids rather than literals. <c>0x82</c> is seven distinct across seven places.
    /// </summary>
    [Fact]
    public void TheWordsAreCountedDistinctAndAsVariables()
    {
        AByteThenAWord.Reading reading = AByteThenAWord.Of(
            0x9D,
            [
                new AByteThenAWord.Run(0x100, [0, 1], [0x800D, 10]),
                new AByteThenAWord.Run(0x200, [0], [0x800D]),
            ]);

        Assert.Equal(2, reading.Words);
        Assert.Equal(2, reading.Variables);
        Assert.Equal(3, reading.Places);
    }
}
