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
