namespace PokeMmo.RomExtract;

/// <summary>
/// Investigative output for when the locator's assumptions do not match a real
/// cartridge. Lowers the run threshold and dumps raw entries so the actual layout
/// can be read off directly instead of guessed at.
/// </summary>
public static class RomDiagnostics
{
    /// <summary>Run length low enough to surface short or partial tables the normal scan ignores.</summary>
    public const int ExploratoryRunLength = 16;

    public static void Report(Rom rom, RomTables tables, Action<string> write)
    {
        write("");
        write("=== diagnostics ===");
        write($"ROM {rom.GameCode} rev {rom.Version}, {rom.Length / 1024 / 1024} MiB, sha1 {rom.Sha1}");

        ReportRuns(rom, "sprite-sheet", TableLocator.ScanPicRuns(rom, ExploratoryRunLength), write);
        ReportRuns(rom, "palette", TableLocator.ScanPaletteRuns(rom, ExploratoryRunLength), write);

        if (tables.NormalPalettes is { } normal)
        {
            int after = normal.Offset + normal.EntryCount * normal.EntrySize;
            write("");
            write($"Raw entries following the normal palette table (0x{Rom.BaseAddress + (uint)after:X8}):");
            DumpEntries(rom, after, 40, write);
        }

        if (tables.FrontPics is { } front)
        {
            int after = front.Offset + front.EntryCount * front.EntrySize;
            write("");
            write($"Raw entries following the front-sprite table (0x{Rom.BaseAddress + (uint)after:X8}):");
            DumpEntries(rom, after, 16, write);
        }
    }

    private static void ReportRuns(Rom rom, string kind, List<TableLocation> runs, Action<string> write)
    {
        write("");
        write($"{kind} runs of at least {ExploratoryRunLength} entries: {runs.Count} found");

        foreach (TableLocation run in runs)
        {
            int end = run.Offset + run.EntryCount * run.EntrySize;
            write($"  0x{run.Address:X8} .. 0x{Rom.BaseAddress + (uint)end:X8}   {run.EntryCount,4} entries");
        }
    }

    /// <summary>
    /// Prints raw 8-byte records interpreted as {pointer, halfword, halfword}, which
    /// covers both table shapes, so a mismatched field is visible at a glance.
    /// </summary>
    public static void DumpEntries(Rom rom, int offset, int count, Action<string> write)
    {
        for (int i = 0; i < count; i++)
        {
            int entry = offset + i * 8;
            if (entry + 8 > rom.Length) break;

            uint pointer = rom.ReadU32(entry);
            ushort a = rom.ReadU16(entry + 4);
            ushort b = rom.ReadU16(entry + 6);

            string note = rom.IsRomAddress(pointer) ? "rom-ptr" : "not-a-ptr";
            if (rom.IsRomAddress(pointer) && a == i) note += ", tag-matches-index";
            if (a == 0x0800) note += ", size-0x800";

            write($"  [{i,3}] 0x{Rom.BaseAddress + (uint)entry:X8}  ptr=0x{pointer:X8}  +4=0x{a:X4}  +6=0x{b:X4}  ({note})");
        }
    }
}
