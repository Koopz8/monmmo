namespace PokeMmo.RomExtract.Maps;

/// <summary>The map layout pointer table, and the layouts it points at.</summary>
public sealed record MapLayoutTable(int Offset, int EntryCount, IReadOnlyList<MapLayoutRecord?> Layouts)
{
    public uint Address => Rom.BaseAddress + (uint)Offset;

    /// <summary>Entries that resolved to a valid layout record.</summary>
    public IEnumerable<(int Index, MapLayoutRecord Layout)> Valid =>
        Layouts.Select((l, i) => (Index: i, Layout: l))
               .Where(x => x.Layout is not null)
               .Select(x => (x.Index, x.Layout!));

    public override string ToString() =>
        $"MapLayouts       0x{Address:X8}  {EntryCount,4} entries, {Valid.Count()} resolved";
}

/// <summary>
/// Finds map data the same way the rest of the extractor finds things: by structure.
/// <para>
/// A map layout is a distinctive record — two small positive dimensions followed by
/// pointers that must land inside the cartridge, with block data that must fit. The
/// layout <em>table</em> is then a long run of pointers each targeting one of those
/// records. Neither needs an address to be known in advance.
/// </para>
/// </summary>
public static class MapLocator
{
    /// <summary>
    /// Pointers a candidate table must resolve before it is believed. Set well above
    /// the length of any incidental pointer run in the image.
    /// </summary>
    private const int MinimumTableLength = 32;

    /// <summary>
    /// Fraction of a candidate table's entries that must resolve to valid layouts.
    /// Not all entries do in practice, so this is a majority rather than a demand for
    /// perfection.
    /// </summary>
    private const double MinimumValidFraction = 0.75;

    /// <summary>
    /// How many consecutive unusable entries a table may contain before the run is
    /// considered over.
    /// <para>
    /// Real tables contain dead slots — a null pointer for a layout that was cut, or
    /// one that fails validation. Treating the first of those as the end of the table
    /// truncates it and, worse, shifts every index in whatever run is found next.
    /// </para>
    /// </summary>
    private const int MaxConsecutiveDeadEntries = 4;

    public static MapLayoutTable? Locate(Rom rom, Action<string>? log = null)
    {
        MapLayoutTable? best = null;

        for (int offset = 0; offset + MinimumTableLength * 4 <= rom.Length; offset += 4)
        {
            // Cheap rejection first: the run has to start with a pointer to a layout.
            if (MapLayoutRecord.TryParse(rom, rom.ToOffsetOrNull(rom.ReadU32(offset)) ?? -1) is null)
                continue;

            var layouts = new List<MapLayoutRecord?>();
            int valid = 0;

            int consecutiveDead = 0;

            for (int i = 0; offset + (i + 1) * 4 <= rom.Length; i++)
            {
                uint pointer = rom.ReadU32(offset + i * 4);

                MapLayoutRecord? layout = rom.ToOffsetOrNull(pointer) is { } target
                    ? MapLayoutRecord.TryParse(rom, target)
                    : null;

                if (layout is null)
                {
                    // Step over an isolated dead slot rather than ending the table on
                    // it. The null is kept so every later index stays where it belongs.
                    if (++consecutiveDead > MaxConsecutiveDeadEntries) break;
                }
                else
                {
                    consecutiveDead = 0;
                    valid++;
                }

                layouts.Add(layout);

                // Stop once the run stops being mostly layouts; scattered dead entries
                // are normal, a tail of unrelated data is not.
                if (layouts.Count >= 8 && valid < layouts.Count * MinimumValidFraction) break;
            }

            while (layouts.Count > 0 && layouts[^1] is null) layouts.RemoveAt(layouts.Count - 1);

            if (layouts.Count < MinimumTableLength) continue;

            var candidate = new MapLayoutTable(offset, layouts.Count, layouts);
            log?.Invoke($"  map layouts: run of {layouts.Count} pointers at 0x{candidate.Address:X8} ({valid} resolved)");

            if (best is null || candidate.Valid.Count() > best.Valid.Count())
                best = candidate;

            offset += layouts.Count * 4 - 4;
        }

        return best;
    }

    /// <summary>
    /// Every layout record in the image, found without reference to any table. Useful
    /// when the table itself cannot be identified, and for diagnostics.
    /// </summary>
    public static List<MapLayoutRecord> ScanLayouts(Rom rom, int limit = int.MaxValue)
    {
        var found = new List<MapLayoutRecord>();

        for (int offset = 0; offset + MapLayoutRecord.SizeBytes <= rom.Length && found.Count < limit; offset += 4)
        {
            if (MapLayoutRecord.TryParse(rom, offset) is { } layout)
                found.Add(layout);
        }

        return found;
    }
}
