namespace PokeMmo.RomExtract.Items;

/// <summary>
/// Which move each teaching machine teaches.
/// <para>
/// Not in the item record. Every machine's four data fields — hold effect, parameter,
/// battle usage, secondary id — are zero for all fifty-eight of them, which is the
/// cartridge saying plainly that this is somewhere else. It is a separate array of move
/// ids, in machine order, and nothing points at it from anything already located.
/// </para>
/// <para>
/// It is findable anyway, because this project already knows three of the answers. The
/// obstacle scripts name three moves by id — 15, 70 and 249, which the move table read
/// off the same image calls CUT, STRENGTH and ROCK SMASH — and those three are machine
/// moves and nothing else is. A run of fifty-eight distinct valid move ids whose last
/// eight contain all three is a signature specific enough to find one table in sixteen
/// megabytes, and specific enough that finding it is also the check that it is right.
/// </para>
/// </summary>
public static class MachineMoves
{
    /// <summary>Fifty TMs and eight HMs, which is what the item pocket holds.</summary>
    public const int Count = 58;

    /// <summary>
    /// How many of the last entries are looked at for the known moves.
    /// <para>
    /// Eight, the HMs, and it matters that this is a window rather than three fixed
    /// positions. Which HM is which is exactly what this is trying to find out, and a
    /// search that assumed the order would only ever confirm the assumption.
    /// </para>
    /// </summary>
    private const int HardwareMachines = 8;

    /// <summary>
    /// Finds the table, or nothing.
    /// <para>
    /// Every candidate is reported rather than the first taken quietly. On a real
    /// FireRed there are two, 0x268 apart and byte-for-byte identical, and a locator
    /// that hid the second one would be hiding the fact that it had a choice to make.
    /// </para>
    /// </summary>
    public static int? Locate(Rom rom, int moveCount, IReadOnlyCollection<int> known, Action<string>? log = null)
    {
        if (known.Count == 0 || moveCount <= 1) return null;

        var found = new List<int>();

        for (int at = 0; at + Count * 2 <= rom.Length; at += 2)
        {
            if (!IsTable(rom, at, moveCount, known)) continue;

            found.Add(at);
        }

        if (found.Count == 0)
        {
            log?.Invoke("  machines: no move list found — nothing will teach anything");
            return null;
        }

        foreach (int at in found.Take(4))
        {
            log?.Invoke(
                $"  machines: run of {Count} move ids at 0x{Rom.BaseAddress + (uint)at:X8}" +
                $"  (last {HardwareMachines}: {string.Join(", ", Read(rom, at).TakeLast(HardwareMachines))})");
        }

        if (found.Count > 1)
        {
            // Identical copies are not a tie to be broken carefully; they are the same
            // answer written twice. Saying so is worth more than picking silently.
            bool same = found
                .Select(at => Read(rom, at))
                .Distinct(new SequenceComparer())
                .Count() == 1;

            log?.Invoke(same
                ? $"  machines: {found.Count} copies of the same list — taking the first"
                : $"  machines: {found.Count} different lists — taking the first, which may be wrong");
        }

        return found[0];
    }

    private static bool IsTable(Rom rom, int at, int moveCount, IReadOnlyCollection<int> known)
    {
        var seen = new HashSet<int>();

        for (int i = 0; i < Count; i++)
        {
            int move = rom.ReadU16(at + i * 2);

            // A machine that teaches nothing, or teaches move number nine hundred, is
            // not a machine. Both bounds do real work: without the upper one the scan
            // latches onto any block of small numbers, and there are a great many.
            if (move < 1 || move >= moveCount) return false;
            if (!seen.Add(move)) return false;
        }

        List<int> tail = [.. Read(rom, at).TakeLast(HardwareMachines)];

        return known.All(tail.Contains);
    }

    public static List<int> Read(Rom rom, int at) =>
        [.. Enumerable.Range(0, Count).Select(i => rom.ReadU16(at + i * 2))];

    private sealed class SequenceComparer : IEqualityComparer<List<int>>
    {
        public bool Equals(List<int>? left, List<int>? right) =>
            left is not null && right is not null && left.SequenceEqual(right);

        public int GetHashCode(List<int> value) => value.Count;
    }
}
