namespace PokeMmo.RomExtract.Graphics;

/// <summary>
/// One overworld sprite's description, as the cartridge records it.
/// <para>
/// The record is 36 bytes: tags, dimensions, a few packed flags, then five pointers.
/// The one that matters here is <see cref="ImagesPointer"/>, which leads to a list of
/// frames — a walking sprite is nine of them, three facings by three steps, with the
/// fourth facing drawn by mirroring.
/// </para>
/// </summary>
public sealed record ObjectGraphicsInfo(
    int Offset,
    int TileTag,
    int PaletteTag,
    int SizeBytes,
    int Width,
    int Height,
    int PaletteSlot,
    uint ImagesPointer)
{
    public const int RecordSizeBytes = 36;

    /// <summary>Bytes a frame of this size occupies at four bits a pixel.</summary>
    public int ExpectedFrameBytes => Width * Height / 2;

    public uint Address => Rom.BaseAddress + (uint)Offset;

    /// <summary>
    /// Reads a record, or returns null when the bytes are not one.
    /// <para>
    /// The discriminating check is that the recorded size matches the dimensions at
    /// four bits a pixel. Nearly anything can look like a pointer; very little
    /// accidentally satisfies an arithmetic relationship between three fields.
    /// </para>
    /// </summary>
    public static ObjectGraphicsInfo? TryParse(Rom rom, int offset)
    {
        if (offset < 0 || offset + RecordSizeBytes > rom.Length) return null;

        int size = rom.ReadU16(offset + 6);
        int width = (short)rom.ReadU16(offset + 8);
        int height = (short)rom.ReadU16(offset + 10);

        if (!IsSpriteDimension(width) || !IsSpriteDimension(height)) return null;
        if (size != width * height / 2) return null;

        byte packed = rom.ReadU8(offset + 12);
        int paletteSlot = packed & 0x0F;

        uint images = rom.ReadU32(offset + 28);
        if (!rom.IsRomAddress(images)) return null;

        // The other pointers are not read, but they still have to be pointers: a run of
        // plausible arithmetic in the middle of graphics data would otherwise pass.
        if (!rom.IsRomAddress(rom.ReadU32(offset + 16))) return null;
        if (!rom.IsRomAddress(rom.ReadU32(offset + 24))) return null;

        return new ObjectGraphicsInfo(
            offset,
            rom.ReadU16(offset),
            rom.ReadU16(offset + 2),
            size,
            width,
            height,
            paletteSlot,
            images);
    }

    /// <summary>Sizes the hardware can draw a sprite at.</summary>
    private static bool IsSpriteDimension(int value) => value is 8 or 16 or 32 or 64;
}

/// <summary>
/// Reads the overworld sprites — the little figures that walk around a map.
/// <para>
/// Located the same way as everything else: by finding a long run of pointers that all
/// lead to something record-shaped, rather than by knowing an address. The signature
/// here is unusually strong, because a graphics record states its own size and that
/// size has to equal its width times its height at four bits a pixel.
/// </para>
/// </summary>
public static class OverworldSprites
{
    /// <summary>Records a candidate table must resolve before it is believed.</summary>
    private const int MinimumRun = 48;

    /// <summary>
    /// Dead entries tolerated in a row before a run is considered finished.
    /// <para>
    /// Real tables have holes in them, and a run that ends at the first one is not a
    /// table — it is the part of a table before its first hole. Two shorter halves,
    /// neither long enough to be believed, is exactly how this table hid.
    /// </para>
    /// </summary>
    private const int MaxDeadInARow = 4;

    /// <summary>Longest frame list considered plausible; a walking sprite has nine.</summary>
    private const int MaxFrames = 32;

    /// <summary>A frame entry is a pointer and a size, padded to eight bytes.</summary>
    private const int FrameEntryBytes = 8;

    /// <summary>Overworld palettes are tagged in this range, which is what marks the table.</summary>
    private const int FirstPaletteTag = 0x1100;

    private const int LastPaletteTag = 0x11FF;

    private const int PaletteEntryBytes = 8;

    public static int? LocateGraphicsTable(Rom rom, Action<string>? log = null)
    {
        for (int offset = 0; offset + MinimumRun * 4 <= rom.Length; offset += 4)
        {
            // A table cannot begin with a hole: starting one entry early would shift
            // every graphics id by one.
            if (!IsGraphicsPointer(rom, offset)) continue;

            int valid = 0;
            int length = 0;
            int dead = 0;

            while (offset + (length + 1) * 4 <= rom.Length)
            {
                int at = offset + length * 4;

                if (rom.ReadU32(at) == 0)
                {
                    if (++dead > MaxDeadInARow) break;
                }
                else if (IsGraphicsPointer(rom, at))
                {
                    valid++;
                    dead = 0;
                }
                else
                {
                    break;
                }

                length++;
            }

            if (valid < MinimumRun)
            {
                // Skipping the whole run avoids re-testing every pointer inside it, and
                // the -4 keeps the next table's first entry in view when two sit
                // back-to-back — the mistake that hid a palette table once already.
                if (length > 1) offset += length * 4 - 4;
                continue;
            }

            log?.Invoke(
                $"  overworld sprites: {valid} records across {length} entries " +
                $"at 0x{Rom.BaseAddress + (uint)offset:X8}");

            return offset;
        }

        return null;
    }

    private static bool IsGraphicsPointer(Rom rom, int at)
    {
        if (rom.ToOffsetOrNull(rom.ReadU32(at)) is not { } target) return false;
        return ObjectGraphicsInfo.TryParse(rom, target) is not null;
    }

    /// <summary>Reads every graphics record the table points at.</summary>
    public static List<ObjectGraphicsInfo?> ReadGraphics(Rom rom, int table, int count)
    {
        var records = new List<ObjectGraphicsInfo?>(count);

        for (int i = 0; i < count; i++)
        {
            int at = table + i * 4;
            if (at + 4 > rom.Length) break;

            // Nulls are kept rather than skipped: an index into this table is a
            // graphics id, and dropping a dead entry would shift every id after it.
            records.Add(rom.ToOffsetOrNull(rom.ReadU32(at)) is { } target
                ? ObjectGraphicsInfo.TryParse(rom, target)
                : null);
        }

        return records;
    }

    /// <summary>
    /// Reads a sprite's frames, stopping where the next sprite's list begins.
    /// <para>
    /// The frame lists carry no count and no terminator — they are packed back to back,
    /// and the games know how long each one is because their animation tables say so.
    /// Reading until the entries stop looking like frames therefore does not work: the
    /// next list is made of frames too, often of exactly the same size, so a sprite
    /// would appear to have every frame in the cartridge after it.
    /// </para>
    /// <para>
    /// <paramref name="endOffset"/> is where the next list starts. Pass null only when
    /// there genuinely is no next list.
    /// </para>
    /// </summary>
    public static List<IndexedImage> ReadFrames(Rom rom, ObjectGraphicsInfo info, int? endOffset = null)
    {
        var frames = new List<IndexedImage>();

        if (rom.ToOffsetOrNull(info.ImagesPointer) is not { } list) return frames;

        int limit = endOffset is { } end && end > list
            ? Math.Min(MaxFrames, (end - list) / FrameEntryBytes)
            : MaxFrames;

        for (int i = 0; i < limit; i++)
        {
            int entry = list + i * FrameEntryBytes;
            if (entry + FrameEntryBytes > rom.Length) break;

            if (rom.ToOffsetOrNull(rom.ReadU32(entry)) is not { } pixels) break;

            int size = rom.ReadU16(entry + 4);
            if (size != info.ExpectedFrameBytes) break;
            if (pixels + size > rom.Length) break;

            frames.Add(TileDecoder.Decode4Bpp(rom.Slice(pixels, size), info.Width, info.Height));
        }

        return frames;
    }

    /// <summary>
    /// Where each sprite's frame list ends: the start of the next one along.
    /// <para>
    /// Derived from the table itself rather than from anything on the cartridge, which
    /// is the only source there is — nothing records a frame count.
    /// </para>
    /// </summary>
    public static Dictionary<int, int> FrameListBoundaries(Rom rom, IReadOnlyList<ObjectGraphicsInfo?> records)
    {
        List<int> starts = records
            .Where(r => r is not null)
            .Select(r => rom.ToOffsetOrNull(r!.ImagesPointer))
            .Where(o => o is not null)
            .Select(o => o!.Value)
            .Distinct()
            .OrderBy(o => o)
            .ToList();

        var boundaries = new Dictionary<int, int>();

        for (int i = 0; i < starts.Count - 1; i++)
            boundaries[starts[i]] = starts[i + 1];

        return boundaries;
    }

    /// <summary>Reads a sprite's frames, working out the boundary from the whole table.</summary>
    public static List<IndexedImage> ReadFrames(
        Rom rom, ObjectGraphicsInfo info, IReadOnlyDictionary<int, int> boundaries)
    {
        int? end = rom.ToOffsetOrNull(info.ImagesPointer) is { } start && boundaries.TryGetValue(start, out int e)
            ? e
            : null;

        return ReadFrames(rom, info, end);
    }

    /// <summary>
    /// Finds the overworld palette table.
    /// <para>
    /// Entries are a pointer and a tag, like the creature palettes, but the tags do not
    /// count from anywhere — they are identifiers in a fixed range. That range is what
    /// marks the table.
    /// </para>
    /// </summary>
    public static int? LocatePaletteTable(Rom rom, Action<string>? log = null)
    {
        const int minimumRun = 8;

        for (int offset = 0; offset + minimumRun * PaletteEntryBytes <= rom.Length; offset += 4)
        {
            int run = 0;

            while (offset + (run + 1) * PaletteEntryBytes <= rom.Length &&
                   IsPaletteEntry(rom, offset + run * PaletteEntryBytes))
            {
                run++;
            }

            if (run < minimumRun) continue;

            log?.Invoke($"  overworld palettes: run of {run} entries at 0x{Rom.BaseAddress + (uint)offset:X8}");
            return offset;
        }

        return null;
    }

    private static bool IsPaletteEntry(Rom rom, int at)
    {
        if (!rom.IsRomAddress(rom.ReadU32(at))) return false;

        int tag = rom.ReadU16(at + 4);
        return tag is >= FirstPaletteTag and <= LastPaletteTag;
    }

    /// <summary>The palette carrying a tag, or null when the table has no such tag.</summary>
    public static GbaPalette? PaletteForTag(Rom rom, int table, int tag, int maxEntries = 64)
    {
        for (int i = 0; i < maxEntries; i++)
        {
            int at = table + i * PaletteEntryBytes;
            if (at + PaletteEntryBytes > rom.Length) break;

            if (!IsPaletteEntry(rom, at)) break;
            if (rom.ReadU16(at + 4) != tag) continue;

            if (rom.ToOffsetOrNull(rom.ReadU32(at)) is not { } colours) return null;
            if (colours + GbaPalette.SizeBytes > rom.Length) return null;

            return GbaPalette.FromBytes(rom.Slice(colours, GbaPalette.SizeBytes));
        }

        return null;
    }
}
