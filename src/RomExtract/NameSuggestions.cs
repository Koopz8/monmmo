namespace PokeMmo.RomExtract;

/// <summary>
/// The names this cartridge offers when it asks somebody to name a character.
/// <para>
/// The rival is named during an intro this game does not run, and until this was found
/// he was called "RIVAL" — a placeholder, and deliberately one, because writing a name
/// from memory of the games is the guess this project refuses to make.
/// </para>
/// <para>
/// The list is not a table. Every other name in the image is a fixed-width record with
/// zero fill after the terminator, which is what a compiler does with an array of string
/// literals — and a scan for exactly that shape, at every stride from six to twelve and
/// at every byte, found the types, the natures and a word bank and missed this entirely.
/// These are terminator-separated and packed, one after another with nothing between:
/// </para>
/// <code>
///   C8 BF D1 00 C8 BB C7 BF FF  "NEW NAME"
///   C1 CC BF BF C8 FF           "GREEN"
///   CC BF BE FF                 "RED"
///   C6 BF BB C0 FF              "LEAF"
/// </code>
/// <para>
/// So it is found by its run rather than by its stride: forty-odd short uppercase names
/// end to end is not something that happens by accident anywhere else in sixteen
/// megabytes, and the search below has no address and no expected name in it.
/// </para>
/// </summary>
public static class NameSuggestions
{
    /// <summary>How long a name in this list can be before it is something else.</summary>
    private const int LongestName = 10;

    /// <summary>
    /// How many in a row make a list rather than a coincidence.
    /// <para>
    /// Twenty is well above anything else in the image and well below the forty-two that
    /// are actually there, which is the room a threshold wants on both sides.
    /// </para>
    /// </summary>
    private const int ShortestRun = 20;

    /// <summary>
    /// Every name the cartridge suggests, in the order it lists them, or nothing when
    /// this image does not have such a list.
    /// </summary>
    public static IReadOnlyList<string> Locate(Rom rom, Action<string>? log = null)
    {
        List<string> best = [];
        int bestAt = 0;

        for (int at = 0; at < rom.Length;)
        {
            (List<string> run, int after) = RunAt(rom, at);

            if (run.Count > best.Count && Heads(run))
            {
                best = run;
                bestAt = at;
            }

            at = after > at ? after : at + 1;
        }

        if (best.Count < ShortestRun)
        {
            log?.Invoke($"  no run of suggested names found — the longest was {best.Count}");
            return [];
        }

        log?.Invoke(
            $"  suggested names: {best.Count} at 0x{Rom.BaseAddress + (uint)bestAt:X8} " +
            $"({string.Join(", ", best.Take(6))}…)");

        return best;
    }

    /// <summary>
    /// The first name in the list that is a name rather than a menu option.
    /// <para>
    /// The list opens with the option that lets you type your own, which is a piece of
    /// interface and has a space in it. Nothing else in the run does — thirty-nine of the
    /// forty-one names after it are one word — so the space is what tells them apart, and
    /// it is the cartridge's own text saying so rather than a position anybody chose.
    /// </para>
    /// <para>
    /// Which of these the games give the rival is not settled by this file. What is
    /// settled is that it is one of them, which is a great deal better than a word this
    /// project made up.
    /// </para>
    /// </summary>
    public static string? FirstName(IReadOnlyList<string> suggestions) =>
        suggestions.FirstOrDefault(n => !n.Contains(' '));

    /// <summary>
    /// Whether a run begins the way this list begins, which is what tells it apart from
    /// the other long run of short uppercase words in the image.
    /// <para>
    /// The easy-chat word bank is thousands of them and beats this list on length alone,
    /// so length is not the test. The test is the head: a menu of names to pick from
    /// opens with the option that lets you type your own instead, and that option is the
    /// only entry with a space in it — every one of the names after it is a single word.
    /// The word bank has spaced entries too, but they are in the middle of it rather
    /// than at the front of a run of single words.
    /// </para>
    /// </summary>
    private static bool Heads(List<string> run) =>
        run.Count >= ShortestRun &&
        run[0].Contains(' ') &&
        run.Skip(1).All(n => !n.Contains(' '));

    /// <summary>Reads names end to end from one place until something is not one.</summary>
    private static (List<string> Names, int After) RunAt(Rom rom, int at)
    {
        var names = new List<string>();
        int i = at;

        while (i < rom.Length)
        {
            (string? name, int next) = NameAt(rom, i);

            if (name is null) break;

            names.Add(name);
            i = next;
        }

        return (names, i);
    }

    /// <summary>
    /// One terminator-ended name, or nothing.
    /// <para>
    /// Uppercase and spaces only. Lowercase is what the cartridge's prose is written in,
    /// and allowing it would make every sentence in the game a run of names.
    /// </para>
    /// </summary>
    private static (string? Name, int After) NameAt(Rom rom, int at)
    {
        var text = new System.Text.StringBuilder();

        for (int i = 0; i < LongestName + 1 && at + i < rom.Length; i++)
        {
            byte b = rom.ReadU8(at + i);

            if (b == GameText.Terminator)
                return text.Length >= 2 ? (text.ToString(), at + i + 1) : (null, at);

            if (b >= 0xBB && b < 0xBB + 26) text.Append((char)('A' + (b - 0xBB)));
            else if (b == 0x00 && text.Length > 0) text.Append(' ');
            else return (null, at);
        }

        return (null, at);
    }
}
