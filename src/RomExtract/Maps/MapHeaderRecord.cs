namespace PokeMmo.RomExtract.Maps;

/// <summary>
/// A map's header: what it looks like, where it sits on the region map, and what it
/// plays. The header is the map's real identity — it is what the game's own
/// (bank, map) numbering addresses, so it is a sounder basis than an index into a
/// layout table whose boundaries have to be inferred.
/// <para>
/// On the cartridge this is a 28-byte record: four pointers, then music and layout
/// ids, then a handful of small enumerations.
/// </para>
/// </summary>
public sealed record MapHeaderRecord(
    int Offset,
    uint LayoutPointer,
    uint EventsPointer,
    uint ScriptsPointer,
    uint ConnectionsPointer,
    ushort Music,
    ushort LayoutId,
    byte RegionSectionId,
    byte Cave,
    byte Weather,
    byte MapType,
    MapLayoutRecord Layout)
{
    public const int SizeBytes = 28;

    /// <summary>Generous upper bound on the map-type enumeration, used only to reject noise.</summary>
    private const byte MaxMapType = 20;

    public uint Address => Rom.BaseAddress + (uint)Offset;

    public static MapHeaderRecord? TryParse(Rom rom, int offset)
    {
        if (offset < 0 || offset + SizeBytes > rom.Length) return null;

        uint layoutPointer = rom.ReadU32(offset);

        // The layout pointer carries almost all the discriminating power here: it has
        // to resolve to a record that itself passes every layout invariant.
        if (rom.ToOffsetOrNull(layoutPointer) is not { } layoutOffset) return null;
        if (MapLayoutRecord.TryParse(rom, layoutOffset) is not { } layout) return null;

        uint events = rom.ReadU32(offset + 4);
        uint scripts = rom.ReadU32(offset + 8);
        uint connections = rom.ReadU32(offset + 12);

        // These three are genuinely optional, but a non-zero value must be a pointer.
        if (!IsNullOrRomAddress(rom, events)) return null;
        if (!IsNullOrRomAddress(rom, scripts)) return null;
        if (!IsNullOrRomAddress(rom, connections)) return null;

        byte mapType = rom.ReadU8(offset + 23);
        if (mapType > MaxMapType) return null;

        return new MapHeaderRecord(
            offset,
            layoutPointer,
            events,
            scripts,
            connections,
            rom.ReadU16(offset + 16),
            rom.ReadU16(offset + 18),
            rom.ReadU8(offset + 20),
            rom.ReadU8(offset + 21),
            rom.ReadU8(offset + 22),
            mapType,
            layout);
    }

    private static bool IsNullOrRomAddress(Rom rom, uint pointer) =>
        pointer == 0 || rom.IsRomAddress(pointer);
}

/// <summary>
/// One entry of the region map table: where a place sits on the town map, and its
/// name. A map header's region-section id indexes this table, which is what lets a
/// map be labelled with something a person recognises.
/// </summary>
public sealed record RegionMapLocation(
    int Index,
    byte X,
    byte Y,
    byte Width,
    byte Height,
    uint NamePointer,
    string Name)
{
    public const int SizeBytes = 8;

    /// <summary>The region map grid is small, so every coordinate and span is bounded.</summary>
    private const byte MaxCoordinate = 40;

    public static RegionMapLocation? TryParse(Rom rom, int offset, int index)
    {
        if (offset < 0 || offset + SizeBytes > rom.Length) return null;

        byte x = rom.ReadU8(offset);
        byte y = rom.ReadU8(offset + 1);
        byte width = rom.ReadU8(offset + 2);
        byte height = rom.ReadU8(offset + 3);

        if (x > MaxCoordinate || y > MaxCoordinate || width > MaxCoordinate || height > MaxCoordinate)
            return null;

        uint namePointer = rom.ReadU32(offset + 4);
        if (rom.ToOffsetOrNull(namePointer) is not { } nameOffset) return null;

        string name = GameText.Decode(rom.Slice(nameOffset, Math.Min(24, rom.Length - nameOffset)));
        if (!LooksLikeLocationName(name)) return null;

        return new RegionMapLocation(index, x, y, width, height, namePointer, name);
    }

    /// <summary>
    /// Location names are short, upper-case and punctuated sparsely. Anything else
    /// means the pointer was not a name.
    /// </summary>
    public static bool LooksLikeLocationName(string name)
    {
        if (name.Length < 3) return false;

        bool hasLetter = false;

        foreach (char c in name)
        {
            if (char.IsAsciiLetterUpper(c)) { hasLetter = true; continue; }
            if (char.IsAsciiDigit(c) || c is ' ' or '.' or '-' or '’') continue;
            return false;
        }

        return hasLetter;
    }
}
