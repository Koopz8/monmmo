namespace PokeMmo.RomExtract;

/// <summary>Where a table was found, and how.</summary>
public sealed record TableLocation(
    string Name,
    int Offset,
    int EntrySize,
    int EntryCount,
    string Method)
{
    /// <summary>The cartridge address this table would be referenced by in code.</summary>
    public uint Address => Rom.BaseAddress + (uint)Offset;

    public override string ToString() =>
        $"{Name,-16} 0x{Address:X8}  {EntryCount,4} x {EntrySize,2}B  ({Method})";
}

/// <summary>Every table the extractor knows how to find.</summary>
public sealed class RomTables
{
    public TableLocation? SpeciesNames { get; init; }
    public TableLocation? BaseStats { get; init; }
    public TableLocation? FrontPics { get; init; }
    public TableLocation? BackPics { get; init; }
    public TableLocation? NormalPalettes { get; init; }
    public TableLocation? ShinyPalettes { get; init; }

    public IEnumerable<TableLocation> All =>
        new[] { SpeciesNames, BaseStats, FrontPics, BackPics, NormalPalettes, ShinyPalettes }
            .Where(t => t is not null)!
            .Cast<TableLocation>();
}

/// <summary>
/// Finds the cartridge's data tables by searching for structural invariants rather
/// than by hardcoding addresses.
/// <para>
/// Hardcoded offsets are the classic way this kind of tool breaks: feed it a
/// different revision or region and every offset shifts, so it reads plausible-looking
/// garbage and reports success. Searching for structure instead means the extractor
/// either finds a table that genuinely satisfies the format's invariants, or fails
/// loudly. It also means new ROM revisions tend to work without a code change.
/// </para>
/// </summary>
public static class TableLocator
{
    /// <summary>
    /// Number of consecutive well-formed entries a candidate run must have before it
    /// is accepted. High enough that random ROM data will not satisfy it by chance.
    /// </summary>
    private const int MinimumRunLength = 100;

    /// <summary>Species records in the Gen III FireRed table, including the placeholder at index 0.</summary>
    public const int DefaultSpeciesCount = 412;

    /// <summary>Mon sprites are 64x64 at 4bpp, so every pic-table entry declares this size.</summary>
    private const ushort MonPicSizeBytes = 0x0800;

    public static RomTables Locate(Rom rom, Action<string>? log = null)
    {
        void Log(string message) => log?.Invoke(message);

        TableLocation? names = LocateSpeciesNames(rom, Log);
        TableLocation? stats = LocateBaseStats(rom, Log);

        List<TableLocation> picTables = LocatePicTables(rom, Log);
        List<TableLocation> paletteTables = LocatePaletteTables(rom, Log);

        return new RomTables
        {
            SpeciesNames = names,
            BaseStats = stats,
            // The front table precedes the back table in every Gen III layout.
            FrontPics = picTables.ElementAtOrDefault(0),
            BackPics = picTables.ElementAtOrDefault(1),
            // Likewise the normal palettes precede the shiny palettes.
            NormalPalettes = paletteTables.ElementAtOrDefault(0),
            ShinyPalettes = paletteTables.ElementAtOrDefault(1),
        };
    }

    /// <summary>
    /// Anchors on the encoded name of species 1, which is a fixed 11-byte record.
    /// Index 0 holds a placeholder, so the table starts one record earlier.
    /// </summary>
    private static TableLocation? LocateSpeciesNames(Rom rom, Action<string> log)
    {
        // Anchor on the characters plus the terminator only. The bytes after the
        // terminator are zero fill, not more terminators, so a full-width key
        // would never match.
        byte[] anchor = GameText.EncodeAnchor("BULBASAUR");

        foreach (int match in rom.FindAll(anchor))
        {
            int tableStart = match - GameText.SpeciesNameLength;
            if (tableStart < 0) continue;

            // Confirm by decoding a stretch of following records: real names decode
            // cleanly, misaligned data produces '?' almost immediately.
            int valid = 0;
            for (int i = 1; i < 151; i++)
            {
                int offset = tableStart + i * GameText.SpeciesNameLength;
                if (offset + GameText.SpeciesNameLength > rom.Length) break;

                string decoded = GameText.Decode(rom.Slice(offset, GameText.SpeciesNameLength));
                if (!GameText.LooksLikeName(decoded)) break;
                valid++;
            }

            if (valid < MinimumRunLength)
            {
                log($"  species names: candidate at 0x{Rom.BaseAddress + (uint)tableStart:X8} rejected ({valid} clean names)");
                continue;
            }

            log($"  species names: {valid + 1} consecutive names decoded cleanly");
            return new TableLocation(
                "SpeciesNames", tableStart, GameText.SpeciesNameLength, DefaultSpeciesCount,
                "anchored on species 1 name");
        }

        return null;
    }

    /// <summary>
    /// Anchors on the base-stat record of species 1, whose leading ten bytes
    /// (stats, both types, catch rate, exp yield) form a distinctive key.
    /// </summary>
    private static TableLocation? LocateBaseStats(Rom rom, Action<string> log)
    {
        // HP 45, Atk 49, Def 49, Spe 45, SpA 65, SpD 65, Grass, Poison, catch 45, exp 64.
        byte[] anchor = [45, 49, 49, 45, 65, 65, 12, 3, 45, 64];

        foreach (int match in rom.FindAll(anchor))
        {
            int tableStart = match - SpeciesRecordSize;
            if (tableStart < 0) continue;

            int valid = CountPlausibleStatRecords(rom, tableStart);
            if (valid < MinimumRunLength)
            {
                log($"  base stats: candidate at 0x{Rom.BaseAddress + (uint)tableStart:X8} rejected ({valid} plausible records)");
                continue;
            }

            log($"  base stats: {valid} consecutive records passed range checks");
            return new TableLocation(
                "BaseStats", tableStart, SpeciesRecordSize, DefaultSpeciesCount,
                "anchored on species 1 base stats");
        }

        return null;
    }

    private const int SpeciesRecordSize = Core.Data.SpeciesData.SizeBytes;

    private static int CountPlausibleStatRecords(Rom rom, int tableStart)
    {
        int count = 0;

        for (int i = 1; i < 200; i++)
        {
            int offset = tableStart + i * SpeciesRecordSize;
            if (offset + SpeciesRecordSize > rom.Length) break;

            ReadOnlySpan<byte> r = rom.Slice(offset, SpeciesRecordSize);

            bool plausible =
                r[0] > 0 &&                       // no species has zero HP
                r[6] <= 17 && r[7] <= 17 &&       // both type ids are in range
                r[19] <= 5 &&                     // growth rate is one of six curves
                r[20] <= 15 && r[21] <= 15;       // both egg groups are in range

            if (!plausible) break;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Finds sprite-sheet tables. Each entry is {pointer, size, tag}; for mon pics the
    /// size is always 0x800 and the tag is the species index, so a genuine table is a
    /// long run of entries whose tags count up from zero.
    /// </summary>
    private static List<TableLocation> LocatePicTables(Rom rom, Action<string> log)
    {
        var found = new List<TableLocation>();
        const int entrySize = 8;

        for (int offset = 0; offset + entrySize * MinimumRunLength <= rom.Length; offset += 4)
        {
            int run = 0;
            while (true)
            {
                int entry = offset + run * entrySize;
                if (entry + entrySize > rom.Length) break;

                uint pointer = rom.ReadU32(entry);
                ushort size = rom.ReadU16(entry + 4);
                ushort tag = rom.ReadU16(entry + 6);

                if (!rom.IsRomAddress(pointer) || size != MonPicSizeBytes || tag != run) break;
                run++;
            }

            if (run >= MinimumRunLength)
            {
                log($"  pic table: run of {run} entries at 0x{Rom.BaseAddress + (uint)offset:X8}");
                found.Add(new TableLocation(
                    found.Count == 0 ? "FrontPics" : "BackPics", offset, entrySize, run,
                    $"run of {run} sized/tagged entries"));

                // Resume exactly at the first byte past this table, not four bytes
                // beyond it. These tables sit back-to-back on the cartridge, and the
                // loop's own increment supplies the remaining step — overshooting here
                // skips the next table's first entry and loses the table entirely.
                offset += run * entrySize - 4;
            }
        }

        return found;
    }

    /// <summary>
    /// Finds sprite-palette tables. Each entry is {pointer, tag, padding}; a genuine
    /// table is a long run whose tags count up from zero with zero padding.
    /// </summary>
    private static List<TableLocation> LocatePaletteTables(Rom rom, Action<string> log)
    {
        var found = new List<TableLocation>();
        const int entrySize = 8;

        for (int offset = 0; offset + entrySize * MinimumRunLength <= rom.Length; offset += 4)
        {
            int run = 0;
            while (true)
            {
                int entry = offset + run * entrySize;
                if (entry + entrySize > rom.Length) break;

                uint pointer = rom.ReadU32(entry);
                ushort tag = rom.ReadU16(entry + 4);
                ushort padding = rom.ReadU16(entry + 6);

                if (!rom.IsRomAddress(pointer) || tag != run || padding != 0) break;
                run++;
            }

            if (run >= MinimumRunLength)
            {
                log($"  palette table: run of {run} entries at 0x{Rom.BaseAddress + (uint)offset:X8}");
                found.Add(new TableLocation(
                    found.Count == 0 ? "NormalPalettes" : "ShinyPalettes", offset, entrySize, run,
                    $"run of {run} tagged entries"));

                // Resume exactly at the first byte past this table, not four bytes
                // beyond it. These tables sit back-to-back on the cartridge, and the
                // loop's own increment supplies the remaining step — overshooting here
                // skips the next table's first entry and loses the table entirely.
                offset += run * entrySize - 4;
            }
        }

        return found;
    }
}
