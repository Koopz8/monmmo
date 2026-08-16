namespace PokeMmo.RomExtract.Sound;

/// <summary>The move-indexed animation table, and what reading it came to.</summary>
public sealed record AnimTable(int Offset, IReadOnlyList<int> Starts)
{
    public int Count => Starts.Count;
}

/// <summary>
/// Finding the table of animations by what the things it points at look like.
/// <para>
/// The same method as everywhere else in this project: a table is a run of pointers, and a
/// pointer is worth believing when what it points at parses. Here "parses" means a script
/// of the forty-eight defined opcodes that runs to an end command — which is a strong test,
/// because an undefined opcode stops a read dead and forty-eight values out of two hundred
/// and fifty-six is a one-in-five chance per byte of stopping by accident.
/// </para>
/// <para>
/// Hardcodes nothing.
/// </para>
/// </summary>
public static class AnimTableLocator
{
    /// <summary>
    /// The fewest entries a run needs before it is the animation table. <b>Modelled.</b>
    /// <para>
    /// This game has three hundred and fifty-four moves, so a real table is enormous by the
    /// standards of anything found by accident. The floor is set well below that so a
    /// cartridge with a shorter table is found rather than missed, and the count that was
    /// actually found is printed so the difference is visible.
    /// </para>
    /// </summary>
    public const int ShortestTable = 32;

    /// <summary>
    /// The fewest commands a script needs before its pointer counts towards a table.
    /// <b>Modelled.</b> A lone <c>end</c> byte parses perfectly and means nothing.
    /// </summary>
    private const int ShortestScript = 3;

    public static AnimTable? Locate(Rom rom, Action<string>? log = null)
    {
        (int offset, List<int> starts) best = (-1, []);

        int at = 0;

        while (at + 4 <= rom.Length)
        {
            if (!Points(rom, at, out int _))
            {
                at += 4;
                continue;
            }

            var run = new List<int>();

            int scan = at;

            while (scan + 4 <= rom.Length && Points(rom, scan, out int start))
            {
                run.Add(start);
                scan += 4;
            }

            if (run.Count > best.starts.Count) best = (at, run);

            at = Math.Max(at + 4, scan);
        }

        if (best.starts.Count < ShortestTable)
        {
            log?.Invoke(
                best.starts.Count == 0
                    ? "  no animation table"
                    : $"  no animation table — the longest run was {best.starts.Count}, under the {ShortestTable} needed");

            return null;
        }

        log?.Invoke($"  animation table at 0x{best.offset:X6} with {best.starts.Count} entries");

        return new AnimTable(best.offset, best.starts);
    }

    /// <summary>
    /// Whether the four bytes here are a pointer to something that reads as an animation.
    /// </summary>
    private static bool Points(Rom rom, int at, out int start)
    {
        start = -1;

        if (rom.ToOffsetOrNull(rom.ReadU32(at)) is not { } target) return false;

        AnimScript script = AnimScriptReader.Read(rom, target);

        if (!script.EndedProperly || script.Events.Count < ShortestScript) return false;

        start = target;

        return true;
    }
}
