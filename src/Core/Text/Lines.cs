namespace PokeMmo.Core.Text;

/// <summary>
/// Breaking a sentence into the lines a box can hold.
/// <para>
/// Here rather than beside the font because it is arithmetic on a string and nothing
/// else — the font's only contribution is how many characters fit, which it works out
/// from a glyph width that is the same for every letter it draws. Put here, it can be
/// tested; put beside the font, it could only be looked at.
/// </para>
/// </summary>
public static class Lines
{
    /// <summary>
    /// The text broken at spaces, no line longer than the given number of characters.
    /// <para>
    /// A word longer than a whole line is left alone on one and allowed to run over.
    /// Breaking it would be breaking a name in half, and a name is the thing on a battle
    /// screen a player most needs to be able to read.
    /// </para>
    /// </summary>
    public static List<string> Wrap(string text, int charactersPerLine)
    {
        var lines = new List<string>();

        if (string.IsNullOrEmpty(text)) return lines;

        int width = Math.Max(1, charactersPerLine);

        var line = new System.Text.StringBuilder();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0) line.Append(' ');

            line.Append(word);
        }

        if (line.Length > 0) lines.Add(line.ToString());

        return lines;
    }
}
