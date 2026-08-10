using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Maps;

/// <summary>
/// Reads the wild encounter tables.
/// <para>
/// A header is twenty bytes: the map's bank and number, then four pointers — land,
/// water, rock smash and fishing — any of which may be null. Each pointer leads to an
/// encounter rate and a list of slots. The table ends with a header whose bank is
/// 0xFF, which is the cartridge's own terminator rather than something inferred.
/// </para>
/// </summary>
public static class EncounterExtractor
{
    public const int HeaderSizeBytes = 20;

    /// <summary>The bank value that marks the end of the header table.</summary>
    private const byte Terminator = 0xFF;

    /// <summary>Headers a candidate run must produce before it is believed.</summary>
    private const int MinimumRun = 40;

    private const int MaxBank = 60;
    private const int MaxMapNumber = 120;

    /// <summary>Highest species index in this generation, used to reject nonsense slots.</summary>
    private const int MaxSpecies = 439;

    public static List<MapEncounters> Extract(Rom rom, Action<string>? log = null)
    {
        int? start = LocateHeaders(rom, log);

        if (start is null)
        {
            log?.Invoke("  encounters: no header table found");
            return [];
        }

        var result = new List<MapEncounters>();

        for (int i = 0; ; i++)
        {
            int offset = start.Value + i * HeaderSizeBytes;
            if (offset + HeaderSizeBytes > rom.Length) break;
            if (rom.ReadU8(offset) == Terminator) break;

            if (ReadHeader(rom, offset) is { } encounters) result.Add(encounters);
        }

        log?.Invoke($"  encounters: {result.Count} maps, {result.Sum(m => m.All.Count())} tables");
        return result;
    }

    private static int? LocateHeaders(Rom rom, Action<string>? log)
    {
        for (int offset = 0; offset + HeaderSizeBytes * MinimumRun <= rom.Length; offset += 4)
        {
            int run = 0;

            while (offset + (run + 1) * HeaderSizeBytes <= rom.Length
                   && LooksLikeHeader(rom, offset + run * HeaderSizeBytes))
            {
                run++;
            }

            if (run < MinimumRun) continue;

            log?.Invoke($"  encounters: run of {run} headers at 0x{Rom.BaseAddress + (uint)offset:X8}");
            return offset;
        }

        return null;
    }

    /// <summary>
    /// A header is plausible when its map address is in range and every non-null
    /// pointer leads to something that parses as an encounter table.
    /// </summary>
    private static bool LooksLikeHeader(Rom rom, int offset)
    {
        if (offset + HeaderSizeBytes > rom.Length) return false;

        byte bank = rom.ReadU8(offset);
        byte number = rom.ReadU8(offset + 1);

        if (bank > MaxBank || number > MaxMapNumber) return false;

        int usable = 0;

        for (int i = 0; i < 4; i++)
        {
            uint pointer = rom.ReadU32(offset + 4 + i * 4);
            if (pointer == 0) continue;
            if (rom.ToOffsetOrNull(pointer) is not { } target) return false;
            if (!LooksLikeTable(rom, target, (EncounterKind)i)) return false;

            usable++;
        }

        // A header with no tables at all carries no information and is more likely to
        // be unrelated data that happened to hold small numbers.
        return usable > 0;
    }

    private static bool LooksLikeTable(Rom rom, int offset, EncounterKind kind)
    {
        if (offset + 8 > rom.Length) return false;

        byte rate = rom.ReadU8(offset);
        if (rate is 0 or > 100) return false;

        uint slots = rom.ReadU32(offset + 4);
        if (rom.ToOffsetOrNull(slots) is not { } slotOffset) return false;

        int count = WildEncounters.SlotCount(kind);
        if (slotOffset + count * WildSlot.SizeBytes > rom.Length) return false;

        for (int i = 0; i < count; i++)
        {
            int at = slotOffset + i * WildSlot.SizeBytes;

            byte min = rom.ReadU8(at);
            byte max = rom.ReadU8(at + 1);
            ushort species = rom.ReadU16(at + 2);

            if (min == 0 || min > 100 || max > 100 || max < min) return false;
            if (species == 0 || species > MaxSpecies) return false;
        }

        return true;
    }

    private static MapEncounters? ReadHeader(Rom rom, int offset)
    {
        byte bank = rom.ReadU8(offset);
        byte number = rom.ReadU8(offset + 1);

        EncounterTable? land = ReadTable(rom, rom.ReadU32(offset + 4), EncounterKind.Land);
        EncounterTable? water = ReadTable(rom, rom.ReadU32(offset + 8), EncounterKind.Water);
        EncounterTable? rock = ReadTable(rom, rom.ReadU32(offset + 12), EncounterKind.RockSmash);
        EncounterTable? fishing = ReadTable(rom, rom.ReadU32(offset + 16), EncounterKind.Fishing);

        if (land is null && water is null && rock is null && fishing is null) return null;

        return new MapEncounters(WorldExporter.MapId(bank, number), land, water, rock, fishing);
    }

    private static EncounterTable? ReadTable(Rom rom, uint pointer, EncounterKind kind)
    {
        if (rom.ToOffsetOrNull(pointer) is not { } offset) return null;
        if (offset + 8 > rom.Length) return null;

        byte rate = rom.ReadU8(offset);
        if (rate == 0) return null;

        if (rom.ToOffsetOrNull(rom.ReadU32(offset + 4)) is not { } slotOffset) return null;

        int count = WildEncounters.SlotCount(kind);
        var slots = new List<WildSlot>(count);

        for (int i = 0; i < count; i++)
        {
            int at = slotOffset + i * WildSlot.SizeBytes;
            if (at + WildSlot.SizeBytes > rom.Length) break;

            slots.Add(new WildSlot(rom.ReadU16(at + 2), rom.ReadU8(at), rom.ReadU8(at + 1)));
        }

        return slots.Count > 0 ? new EncounterTable(kind, rate, slots) : null;
    }
}
