namespace PokeMmo.RomExtract.Scripts;

/// <summary>One place a number is compared, and the size of the map it is compared on.</summary>
/// <param name="At">Where the command sits.</param>
/// <param name="MapId">Which map's script it is.</param>
/// <param name="Value">The number the next command compares it against.</param>
/// <param name="Width">That map's width in squares.</param>
/// <param name="Height">That map's height.</param>
public sealed record AgainstTheMap(int At, string MapId, int Value, int Width, int Height)
{
    /// <summary>The value could be a column on this map.</summary>
    public bool FitsTheWidth => Value >= 0 && Value < Width;

    /// <summary>The value could be a row on this map.</summary>
    public bool FitsTheHeight => Value >= 0 && Value < Height;

    public override string ToString() =>
        $"0x{At:X6}  {MapId,-7} compared against {Value,3} on a {Width}x{Height} map"
        + $"  — {(FitsTheWidth ? "a column" : "TOO WIDE")}, {(FitsTheHeight ? "a row" : "TOO TALL")}";
}

/// <summary>
/// Whether the numbers a command's operand is compared against are columns or rows.
/// <para>
/// <b>The test that names <c>0x42</c>.</b> 252 established that it writes its first operand and
/// 253 left it as the last thing the operand audit could not settle. Every one of its eight
/// places is followed by a <c>compare</c> on what it left, and the values are 6, 7, 9, 9, 18, 24
/// and 50 — which say nothing on their own.
/// </para>
/// <para>
/// They say a great deal against the map they are on. A number compared on <c>PATTERN BUSH</c>
/// against fifty is a column on a map fifty-six wide and cannot be a row on one twenty-six tall.
/// So: take every place, take the map's own width and height — which this project reads off the
/// cartridge for every map already — and ask which of the two the value could be.
/// </para>
/// <para>
/// <b>It can come back saying neither, or both.</b> A command whose numbers all fit inside both
/// bounds is one this cannot name, and that is the answer it gives; the discrimination only
/// exists because some maps are much wider than they are tall.
/// </para>
/// </summary>
public static class ANumberAgainstTheMap
{
    private const byte Compare = 0x21;

    /// <summary>
    /// For each place, the value the very next command compares that operand against, and the
    /// size of the map the place is on.
    /// </summary>
    /// <param name="rom">The cartridge.</param>
    /// <param name="places">Where the command sits, from the operand sweep.</param>
    /// <param name="at">Which of its operands is being asked about.</param>
    /// <param name="mapOf">The map a place belongs to, or null when nothing opened it.</param>
    /// <param name="sizeOf">That map's width and height.</param>
    public static IReadOnlyList<AgainstTheMap> In(
        Rom rom,
        IEnumerable<int> places,
        int at,
        Func<int, string?> mapOf,
        Func<string, (int Width, int Height)?> sizeOf)
    {
        var found = new List<AgainstTheMap>();

        foreach (int place in places)
        {
            if (mapOf(place) is not { } mapId) continue;
            if (sizeOf(mapId) is not { } size) continue;

            // THE NUMBER THIS OPERAND NAMED, and then the compare that follows the whole
            // command. Reading the block rather than the bytes, so a command whose width this
            // project has wrong cannot quietly become a compare.
            List<ScriptCommand> read = [.. ScriptReader.Read(rom, Rom.BaseAddress + (uint)place)];

            if (read.Count < 2) continue;

            ScriptCommand named = read[0];

            if (named.Arguments.Length < at + 2) continue;

            ScriptCommand next = read[1];

            if (next.Code != Compare || next.Arguments.Length < 4) continue;

            // AND ONLY WHEN THE COMPARE IS ON THIS VERY NUMBER. A compare on something else is
            // the script asking a different question in the next breath, which is common.
            if (next.Word() != named.Word(at)) continue;

            found.Add(new AgainstTheMap(place, mapId, next.Word(2), size.Width, size.Height));
        }

        return found;
    }

    /// <summary>
    /// The verdict: how many of the values could be a column, a row, both, or neither.
    /// </summary>
    /// <remarks>
    /// <b>Both counts, and the both-and-neither ones too.</b> "Every value fits the width" is only
    /// a finding beside "and some of them do not fit the height" — on a square map every column is
    /// also a row and the test says nothing, which it has to be able to report.
    /// </remarks>
    public static (int Places, int Columns, int Rows, int Only, int Neither) Verdict(
        IReadOnlyList<AgainstTheMap> found) =>
        (found.Count,
            found.Count(f => f.FitsTheWidth),
            found.Count(f => f.FitsTheHeight),
            found.Count(f => f.FitsTheWidth && !f.FitsTheHeight),
            found.Count(f => !f.FitsTheWidth && !f.FitsTheHeight));
}
