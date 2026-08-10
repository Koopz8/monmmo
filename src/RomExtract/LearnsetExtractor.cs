using PokeMmo.Core.Battle;

namespace PokeMmo.RomExtract;

/// <summary>
/// Reads what each species learns by levelling.
/// <para>
/// The table is an array of pointers, one per species, each leading to a list of
/// packed level-and-move words ending in 0xFFFF. Located the same way as everything
/// else: by finding a long run of pointers that all lead to something shaped like a
/// learnset, rather than by knowing an address.
/// </para>
/// </summary>
public static class LearnsetExtractor
{
    /// <summary>Species in the Generation III table, including the placeholder at index zero.</summary>
    public const int DefaultSpeciesCount = 412;

    /// <summary>Pointers a candidate table must resolve before it is believed.</summary>
    private const int MinimumRun = 100;

    /// <summary>Longest learnset considered plausible; the real maximum is well under this.</summary>
    private const int MaxEntries = 40;

    private const int MaxMoveId = 354;

    public static int? LocateTable(Rom rom, Action<string>? log = null)
    {
        for (int offset = 0; offset + MinimumRun * 4 <= rom.Length; offset += 4)
        {
            int run = 0;

            while (offset + (run + 1) * 4 <= rom.Length && IsLearnsetPointer(rom, offset + run * 4))
                run++;

            if (run < MinimumRun) continue;

            log?.Invoke($"  learnsets: run of {run} pointers at 0x{Rom.BaseAddress + (uint)offset:X8}");
            return offset;
        }

        return null;
    }

    private static bool IsLearnsetPointer(Rom rom, int at)
    {
        if (rom.ToOffsetOrNull(rom.ReadU32(at)) is not { } target) return false;
        return ReadEntries(rom, target) is not null;
    }

    /// <summary>
    /// Reads one learnset, or returns null when the bytes are not one. A valid list
    /// holds at least one plausible entry and terminates within a sane length.
    /// </summary>
    private static List<LevelUpMove>? ReadEntries(Rom rom, int offset)
    {
        var moves = new List<LevelUpMove>();

        for (int i = 0; i < MaxEntries; i++)
        {
            int at = offset + i * 2;
            if (at + 2 > rom.Length) return null;

            ushort raw = rom.ReadU16(at);

            if (raw == LevelUpMove.Terminator)
                return moves.Count > 0 ? moves : null;

            LevelUpMove entry = LevelUpMove.Decode(raw);

            if (entry.MoveId is < 1 or > MaxMoveId) return null;
            if (entry.Level is < 1 or > 100) return null;

            moves.Add(entry);
        }

        return null;
    }

    /// <summary>Reads every species' learnset.</summary>
    public static Dictionary<int, Learnset> Extract(Rom rom, Action<string>? log = null)
    {
        var learnsets = new Dictionary<int, Learnset>();

        if (LocateTable(rom, log) is not { } table) return learnsets;

        for (int species = 0; species < DefaultSpeciesCount; species++)
        {
            int at = table + species * 4;
            if (at + 4 > rom.Length) break;

            if (rom.ToOffsetOrNull(rom.ReadU32(at)) is not { } target) continue;
            if (ReadEntries(rom, target) is not { } moves) continue;

            learnsets[species] = new Learnset(species, moves);
        }

        log?.Invoke($"  learnsets: {learnsets.Count} species");
        return learnsets;
    }
}
