using PokeMmo.Core.Battle;

namespace PokeMmo.RomExtract;

/// <summary>
/// Reads the move table and move names off the cartridge.
/// <para>
/// Located by structure like everything else: anchored on the first move's record,
/// then confirmed by range-checking a long run of the records that follow. No
/// addresses are hardcoded.
/// </para>
/// </summary>
public static class MoveExtractor
{
    /// <summary>Moves in the Generation III table, including the empty entry at index zero.</summary>
    public const int DefaultMoveCount = 355;

    /// <summary>Consecutive plausible records a candidate must produce before it is believed.</summary>
    private const int MinimumRun = 100;

    /// <summary>
    /// Move 1 is Pound: no effect, 40 power, Normal, 100% accurate, 35 PP, no
    /// secondary chance. Six bytes is a weak key on its own, which is why the run
    /// that follows has to pass range checks too.
    /// </summary>
    private static readonly byte[] Anchor = [0, 40, 0, 100, 35, 0];

    public static TableLocation? LocateTable(Rom rom, Action<string>? log = null)
    {
        foreach (int match in rom.FindAll(Anchor))
        {
            int tableStart = match - MoveData.SizeBytes;
            if (tableStart < 0) continue;

            int valid = CountPlausible(rom, tableStart);

            if (valid < MinimumRun)
            {
                log?.Invoke($"  moves: candidate at 0x{Rom.BaseAddress + (uint)tableStart:X8} rejected ({valid} plausible)");
                continue;
            }

            log?.Invoke($"  moves: {valid} consecutive records passed range checks");

            return new TableLocation(
                "Moves", tableStart, MoveData.SizeBytes, DefaultMoveCount, "anchored on move 1");
        }

        return null;
    }

    private static int CountPlausible(Rom rom, int tableStart)
    {
        int count = 0;

        for (int i = 1; i < 200; i++)
        {
            int offset = tableStart + i * MoveData.SizeBytes;
            if (offset + MoveData.SizeBytes > rom.Length) break;
            if (!MoveData.LooksPlausible(rom.Slice(offset, MoveData.SizeBytes))) break;

            count++;
        }

        return count;
    }

    /// <summary>Finds the move-name table, anchored on the name of move 1.</summary>
    public static TableLocation? LocateNames(Rom rom, Action<string>? log = null)
    {
        byte[] anchor = GameText.EncodeAnchor("POUND");

        foreach (int match in rom.FindAll(anchor))
        {
            int tableStart = match - GameText.MoveNameLength;
            if (tableStart < 0) continue;

            int valid = 0;

            for (int i = 1; i < 151; i++)
            {
                int offset = tableStart + i * GameText.MoveNameLength;
                if (offset + GameText.MoveNameLength > rom.Length) break;

                string decoded = GameText.Decode(rom.Slice(offset, GameText.MoveNameLength));
                if (!GameText.LooksLikeName(decoded)) break;

                valid++;
            }

            if (valid < MinimumRun) continue;

            log?.Invoke($"  move names: {valid + 1} consecutive names decoded cleanly");

            return new TableLocation(
                "MoveNames", tableStart, GameText.MoveNameLength, DefaultMoveCount, "anchored on move 1 name");
        }

        return null;
    }

    /// <summary>Reads every move, pairing records with names where both tables were found.</summary>
    public static List<MoveData> Extract(Rom rom, Action<string>? log = null)
    {
        TableLocation? table = LocateTable(rom, log)
            ?? throw new InvalidDataException("The move table was not located in this ROM.");

        TableLocation? names = LocateNames(rom, log);
        var moves = new List<MoveData>(table.EntryCount);

        for (int i = 0; i < table.EntryCount; i++)
        {
            int offset = table.Offset + i * MoveData.SizeBytes;
            if (offset + MoveData.SizeBytes > rom.Length) break;

            string name = names is not null
                ? GameText.Decode(rom.Slice(
                    names.Offset + i * GameText.MoveNameLength,
                    Math.Min(GameText.MoveNameLength, rom.Length - (names.Offset + i * GameText.MoveNameLength))))
                : $"MOVE {i}";

            moves.Add(MoveData.Parse(rom.Slice(offset, MoveData.SizeBytes), i, name));
        }

        return moves;
    }
}
