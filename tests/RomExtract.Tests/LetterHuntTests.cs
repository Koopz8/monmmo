using PokeMmo.RomExtract.Graphics;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The tool that says where the lettering is not.
/// <para>
/// A search that finds nothing is worth exactly as much as the confidence that it would
/// have found something. So the pieces it is built from are tested against glyphs made
/// by hand: an even letter, a lopsided one, a narrow one sitting in a wide box, and a
/// whole alphabet's worth of evenness planted where the reader should find it.
/// </para>
/// <para>
/// Without these, "nothing anywhere on the image" means nothing at all.
/// </para>
/// </summary>
public class LetterHuntTests
{
    /// <summary>An eight-by-eight glyph as eight strings of dots and hashes.</summary>
    private static byte[] Glyph(params string[] rows)
    {
        var bytes = new byte[8];

        for (int y = 0; y < 8 && y < rows.Length; y++)
        {
            byte row = 0;

            for (int x = 0; x < 8 && x < rows[y].Length; x++)
                if (rows[y][x] == '#') row |= (byte)(1 << (7 - x));

            bytes[y] = row;
        }

        return bytes;
    }

    private static readonly string[] Even =
    [
        "..###...",
        ".#...#..",
        ".#...#..",
        ".#####..",
        ".#...#..",
        ".#...#..",
        ".#...#..",
        "........",
    ];

    private static readonly string[] Lopsided =
    [
        ".####...",
        ".#...#..",
        ".#...#..",
        ".####...",
        ".#...#..",
        ".#...#..",
        ".####...",
        "........",
    ];

    [Fact]
    public void ALetterThatReadsTheSameBackwardsSaysSo()
    {
        Assert.True(LetterHunt.ReadsTheSameBackwards(Glyph(Even), 0, depth: 1, height: 8));
    }

    [Fact]
    public void ALetterThatDoesNotSaysThat()
    {
        Assert.False(LetterHunt.ReadsTheSameBackwards(Glyph(Lopsided), 0, depth: 1, height: 8));
    }

    /// <summary>
    /// The whole point of judging inside the ink. A narrow letter sitting on the left of
    /// a wide box has six empty columns to its right, and measuring the box would call
    /// every one of them lopsided.
    /// </summary>
    [Fact]
    public void ANarrowLetterInAWideBoxIsStillJudgedOnItself()
    {
        byte[] narrow = Glyph(
            "##......",
            "##......",
            "##......",
            "##......",
            "##......",
            "##......",
            "##......",
            "........");

        Assert.True(LetterHunt.ReadsTheSameBackwards(narrow, 0, depth: 1, height: 8));
    }

    /// <summary>Nothing to judge is not the same as judged and found even.</summary>
    [Fact]
    public void AnEmptyGlyphIsNoAnswerAtAll()
    {
        Assert.Null(LetterHunt.ReadsTheSameBackwards(new byte[8], 0, depth: 1, height: 8));
    }

    [Fact]
    public void ReadingPastTheEndIsNoAnswerEither()
    {
        Assert.Null(LetterHunt.ReadsTheSameBackwards(new byte[4], 0, depth: 1, height: 8));
    }

    // ---- and the search built on it --------------------------------------------------

    /// <summary>
    /// An alphabet planted where the reader should find it: twenty-six glyphs whose
    /// evenness follows A to Z, and the search finds it at twenty-six of twenty-six.
    /// A search nobody has watched succeed is not a search.
    /// </summary>
    [Fact]
    public void APlantedAlphabetIsFound()
    {
        const string Mirrored = "10000001100010100000111111";

        var image = new byte[26 * 8];

        for (int i = 0; i < 26; i++)
            Glyph(Mirrored[i] == '1' ? Even : Lopsided).CopyTo(image, i * 8);

        List<LetterHit> hits = LetterHunt.LooksLikeAnAlphabet(image, 0x08000000);

        Assert.Contains(hits, h => h is { Depth: 1, Height: 8, Offset: 0, Score: 26 });
    }

    /// <summary>
    /// And a stretch of one letter repeated is not an alphabet, however even it is.
    /// Twenty-six evens score eleven, which is the eleven the pattern expects and not
    /// one more.
    /// </summary>
    [Fact]
    public void TwentySixOfTheSameLetterIsNotAnAlphabet()
    {
        var image = new byte[26 * 8];

        for (int i = 0; i < 26; i++) Glyph(Even).CopyTo(image, i * 8);

        Assert.DoesNotContain(
            LetterHunt.LooksLikeAnAlphabet(image, 0x08000000),
            h => h is { Depth: 1, Height: 8, Offset: 0 });
    }

    /// <summary>
    /// The count the code-indexed search is built on, read off this project's own
    /// encoding rather than stated. If the encoding gains a character, this moves.
    /// </summary>
    [Fact]
    public void TheEncodingPrintsSomethingForSeventySixCodes()
    {
        Assert.Equal(76, LetterHunt.PrintableCodes());
    }
}
