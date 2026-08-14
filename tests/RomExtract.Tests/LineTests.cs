using PokeMmo.Core.Text;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Text broken to fit a box.
/// <para>
/// Written after a battle ended with "You have no more usable Pokemon! Your party was
/// hea" — the half of the sentence that said what had been done about it ran off the
/// right-hand edge and was simply not drawn. Nothing failed; the box just said less.
/// </para>
/// </summary>
public class LineTests
{
    [Fact]
    public void AShortLineIsLeftAlone()
    {
        Assert.Equal(["PIDGEY fainted!"], Lines.Wrap("PIDGEY fainted!", 40));
    }

    [Fact]
    public void ALongLineIsBrokenAtASpace()
    {
        List<string> lines = Lines.Wrap("You have no more usable Pokemon! Your party was healed.", 32);

        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.True(line.Length <= 32, line));

        // And nothing was lost or invented on the way.
        Assert.Equal("You have no more usable Pokemon! Your party was healed.", string.Join(" ", lines));
    }

    [Fact]
    public void AWordLongerThanTheBoxKeepsItsOwnLine()
    {
        // It runs over the edge, which is the lesser of the two wrongs: the alternative
        // is a name cut in half, and a name is what a player is reading for.
        List<string> lines = Lines.Wrap("BULBASAUR ABCDEFGHIJKLMNOPQRSTUVWXYZ used it", 12);

        Assert.Contains("ABCDEFGHIJKLMNOPQRSTUVWXYZ", lines);
    }

    [Fact]
    public void NothingWrapsToNothing()
    {
        Assert.Empty(Lines.Wrap("", 40));
        Assert.Empty(Lines.Wrap("   ", 40));
    }

    [Fact]
    public void AWidthOfNothingStillReturnsTheWords()
    {
        // Guards the arithmetic that feeds this rather than the wrapping: a box measured
        // as zero characters wide is a layout bug, and looping forever on it would hang
        // the client instead of showing one.
        Assert.Equal(["a", "b"], Lines.Wrap("a b", 0));
    }
}
