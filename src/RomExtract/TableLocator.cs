namespace PokeMmo.RomExtract;

/// <summary>Where a table was found, and how.</summary>
public sealed record TableLocation(
    string Name,
    int Offset,
    int EntrySize,
    int EntryCount,
    string Method)
{
    /// <summary>
    /// The tag carried by this table's first entry. Most tables tag entries with the
    /// bare species index, but some offset every tag by a constant so two tables can
    /// be resident in the sprite palette manager at once without colliding.
    /// </summary>
    public int TagBase { get; init; }

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

        (TableLocation? front, TableLocation? back) = ChoosePicTables(ScanPicRuns(rom, MinimumRunLength, Log));
        (TableLocation? normal, TableLocation? shiny) = ChoosePaletteTables(ScanPaletteRuns(rom, MinimumRunLength, Log));

        return new RomTables
        {
            SpeciesNames = names,
            BaseStats = stats,
            FrontPics = front,
            BackPics = back,
            NormalPalettes = normal,
            ShinyPalettes = shiny,
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


    /// <summary>Reads the identifying tag from an entry. Its position depends on the table shape.</summary>
    private delegate int TagReader(Rom rom, int entryOffset);

    /// <summary>Checks the fields of an entry that do not vary with its index.</summary>
    private delegate bool EntryValidator(Rom rom, int entryOffset);

    private const int TableEntrySize = 8;

    // Sprite-sheet entry: {pointer, size, tag}. Mon pics are always 0x800 bytes.
    private static bool IsPicEntry(Rom rom, int entry) =>
        rom.IsRomAddress(rom.ReadU32(entry)) && rom.ReadU16(entry + 4) == MonPicSizeBytes;

    private static int ReadPicTag(Rom rom, int entry) => rom.ReadU16(entry + 6);

    // Sprite-palette entry: {pointer, tag, padding}. The trailing halfword is
    // structure padding and is zero in every statically initialised table.
    private static bool IsPaletteEntry(Rom rom, int entry) =>
        rom.IsRomAddress(rom.ReadU32(entry)) && rom.ReadU16(entry + 6) == 0;

    private static int ReadPaletteTag(Rom rom, int entry) => rom.ReadU16(entry + 4);

    /// <summary>
    /// Walks the image looking for runs of consecutive well-formed entries whose tags
    /// increase by one.
    /// <para>
    /// The tag base is read from each candidate's first entry rather than assumed to be
    /// zero. Some tables offset every tag by a constant, and demanding a zero base makes
    /// the scanner walk straight past them — which is exactly how the shiny palette
    /// table went missing while sitting in plain sight.
    /// </para>
    /// </summary>
    private static List<TableLocation> ScanRuns(
        Rom rom,
        EntryValidator isWellFormed,
        TagReader readTag,
        int minimumRun,
        string label,
        Action<string>? log = null)
    {
        var found = new List<TableLocation>();

        for (int offset = 0; offset + TableEntrySize * minimumRun <= rom.Length; offset += 4)
        {
            if (!isWellFormed(rom, offset)) continue;

            int tagBase = readTag(rom, offset);
            int run = 0;

            while (offset + (run + 1) * TableEntrySize <= rom.Length
                   && isWellFormed(rom, offset + run * TableEntrySize)
                   && readTag(rom, offset + run * TableEntrySize) == tagBase + run)
            {
                run++;
            }

            if (run < minimumRun) continue;

            string baseNote = tagBase == 0 ? "tags from 0" : $"tags from {tagBase}";
            log?.Invoke($"  {label}: run of {run} entries at 0x{Rom.BaseAddress + (uint)offset:X8} ({baseNote})");

            found.Add(new TableLocation(label, offset, TableEntrySize, run, $"run of {run} entries, {baseNote}")
            {
                TagBase = tagBase,
            });

            // Resume exactly at the first byte past this table, not four bytes beyond
            // it. Tables sit back-to-back, and the loop's own increment supplies the
            // remaining step — overshooting skips the next table's first entry.
            offset += run * TableEntrySize - 4;
        }

        return found;
    }

    /// <summary>All sprite-sheet table candidates, in address order.</summary>
    public static List<TableLocation> ScanPicRuns(Rom rom, int minimumRun = MinimumRunLength, Action<string>? log = null) =>
        ScanRuns(rom, IsPicEntry, ReadPicTag, minimumRun, "pic table", log);

    /// <summary>All sprite-palette table candidates, in address order.</summary>
    public static List<TableLocation> ScanPaletteRuns(Rom rom, int minimumRun = MinimumRunLength, Action<string>? log = null) =>
        ScanRuns(rom, IsPaletteEntry, ReadPaletteTag, minimumRun, "palette table", log);

    /// <summary>
    /// Picks the front and back sprite tables: the two longest runs, in address order.
    /// Both are tagged by bare species index, so they cannot be told apart by tag base.
    /// </summary>
    private static (TableLocation? Front, TableLocation? Back) ChoosePicTables(List<TableLocation> runs)
    {
        if (runs.Count == 0) return (null, null);

        int longest = runs.Max(r => r.EntryCount);

        List<TableLocation> fullSized = runs
            .Where(r => r.EntryCount == longest)
            .OrderBy(r => r.Offset)
            .ToList();

        return (
            fullSized.ElementAtOrDefault(0) with { Name = "FrontPics" },
            fullSized.ElementAtOrDefault(1) is { } back ? back with { Name = "BackPics" } : null);
    }

    /// <summary>
    /// Picks the normal and shiny palette tables.
    /// <para>
    /// These are distinguished by <em>tag base</em> rather than by position: the normal
    /// table tags entries with the bare species index, the shiny table offsets every tag
    /// by a constant. That is a property of the data, whereas "whichever comes second"
    /// is an assumption about layout — and there are other tagged palette tables in the
    /// image (trainer sprites, for one) that would otherwise be mistaken for it.
    /// </para>
    /// </summary>
    private static (TableLocation? Normal, TableLocation? Shiny) ChoosePaletteTables(List<TableLocation> runs)
    {
        TableLocation? normal = runs
            .Where(r => r.TagBase == 0)
            .OrderByDescending(r => r.EntryCount)
            .ThenBy(r => r.Offset)
            .FirstOrDefault();

        if (normal is null) return (null, null);

        TableLocation? shiny = runs
            .Where(r => r.TagBase != 0 && r.Offset != normal.Offset)
            .OrderByDescending(r => r.EntryCount == normal.EntryCount)
            .ThenBy(r => Math.Abs((long)r.Offset - normal.Offset))
            .FirstOrDefault();

        return (
            normal with { Name = "NormalPalettes" },
            shiny is null ? null : shiny with { Name = "ShinyPalettes" });
    }
}
