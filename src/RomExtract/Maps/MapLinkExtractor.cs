using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Maps;

/// <summary>
/// Reads the two things that join maps together: warps and edge connections.
/// <para>
/// Both hang off the map header, so nothing here needs locating — the headers were
/// already found by structure, and these are fixed offsets within a record that has
/// already proved itself. That is the payoff for having refused to hardcode addresses
/// earlier: everything reachable from a located structure is free.
/// </para>
/// </summary>
public static class MapLinkExtractor
{
    /// <summary>An events record: four counts, then four pointers.</summary>
    private const int EventsPointersOffset = 4;

    private const int WarpSizeBytes = 8;

    /// <summary>An object-event template: ids, a square, movement, a script and a flag.</summary>
    private const int ObjectSizeBytes = 24;

    private const int MaxObjects = 64;

    private const int ConnectionSizeBytes = 12;

    /// <summary>
    /// More than any real map has. A count past this means the pointer is not an
    /// events record at all, which is worth noticing rather than reading 60,000 warps.
    /// </summary>
    private const int MaxWarps = 128;

    private const int MaxConnections = 32;

    /// <summary>
    /// Reads the warps on one map.
    /// <para>
    /// Warps land outside a map's bounds occasionally in real images — an artefact of
    /// editing, and harmless to the games because nothing can stand there. They are
    /// dropped, because a warp nobody can reach is not worth a server checking every
    /// step against.
    /// </para>
    /// </summary>
    public static List<Warp> ReadWarps(Rom rom, MapHeaderRecord header, int width, int height, Action<string>? log = null)
    {
        var warps = new List<Warp>();

        if (header.EventsPointer == 0) return warps;
        if (rom.ToOffsetOrNull(header.EventsPointer) is not { } events) return warps;
        if (events + EventsPointersOffset + 8 > rom.Length) return warps;

        int count = rom.ReadU8(events + 1);
        if (count is 0 or > MaxWarps) return warps;

        uint pointer = rom.ReadU32(events + EventsPointersOffset + 4);
        if (rom.ToOffsetOrNull(pointer) is not { } table) return warps;
        if (table + count * WarpSizeBytes > rom.Length) return warps;

        for (int i = 0; i < count; i++)
        {
            int at = table + i * WarpSizeBytes;

            int x = rom.ReadU16(at);
            int y = rom.ReadU16(at + 2);
            int targetWarp = rom.ReadU8(at + 5);
            int targetMap = rom.ReadU8(at + 6);
            int targetBank = rom.ReadU8(at + 7);

            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                log?.Invoke($"    warp {i} at ({x}, {y}) is outside a {width}x{height} map — dropped");
                continue;
            }

            warps.Add(new Warp(x, y, targetWarp, WorldExporter.MapId(targetBank, targetMap)));
        }

        return warps;
    }

    /// <summary>
    /// Reads the people and things standing on a map.
    /// <para>
    /// The same events record the warps come from, and the <em>first</em> of its four
    /// counts and pointers rather than the second. Reading the wrong pair here gives a
    /// plausible number of plausible-looking objects standing in the wrong places,
    /// which is the same trap as the warps and worth naming twice.
    /// </para>
    /// </summary>
    public static List<MapObject> ReadObjects(Rom rom, MapHeaderRecord header, int width, int height, Action<string>? log = null)
    {
        var objects = new List<MapObject>();

        if (header.EventsPointer == 0) return objects;
        if (rom.ToOffsetOrNull(header.EventsPointer) is not { } events) return objects;
        if (events + EventsPointersOffset + 4 > rom.Length) return objects;

        int count = rom.ReadU8(events);
        if (count is 0 or > MaxObjects) return objects;

        uint pointer = rom.ReadU32(events + EventsPointersOffset);
        if (rom.ToOffsetOrNull(pointer) is not { } table) return objects;
        if (table + count * ObjectSizeBytes > rom.Length) return objects;

        for (int i = 0; i < count; i++)
        {
            int at = table + i * ObjectSizeBytes;

            int localId = rom.ReadU8(at);
            int graphicsId = rom.ReadU8(at + 1);
            int x = (short)rom.ReadU16(at + 4);
            int y = (short)rom.ReadU16(at + 6);
            int movementType = rom.ReadU8(at + 9);

            // One byte holds both halves of the beat: x in the low nibble, y in the
            // high one.
            byte range = rom.ReadU8(at + 10);

            int trainerType = rom.ReadU16(at + 12);

            // The same field is a sight range on a trainer and a berry-tree id on a
            // tree, which is why it is only read as one when the object is the other.
            int sight = trainerType != 0 ? rom.ReadU16(at + 14) : 0;

            uint script = rom.ReadU32(at + 16);

            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                log?.Invoke($"    object {i} at ({x}, {y}) is outside a {width}x{height} map — dropped");
                continue;
            }

            bool hasScript = rom.IsRomAddress(script);

            // Which trainer somebody is is not in this record — only that they are one.
            // The id is an argument to a command inside their script, and so is the flag
            // that means somebody has already beaten them.
            (int Id, int Flag) fight = trainerType != 0 && hasScript
                ? Scripts.ScriptReader.FindTrainerBattle(rom, script) ?? (0, 0)
                : (0, 0);

            int trainerId = fight.Id;

            // Shopkeepers are not marked in any way. The only thing that distinguishes
            // one is a pokemart command in their script, so every scriptable person has
            // to be read to find out — which is cheap, and only happens at export.
            List<int> stock = hasScript && trainerId == 0
                ? Scripts.ScriptReader.FindMart(rom, script)
                : [];

            objects.Add(new MapObject(
                localId,
                graphicsId,
                x,
                y,
                MapObject.FacingFor(movementType),
                movementType,
                trainerType != 0,
                range & 0x0F,
                (range >> 4) & 0x0F,
                hasScript ? script : 0,
                trainerId,
                sight,
                stock)
            {
                TrainerFlag = fight.Flag,
            });
        }

        return objects;
    }

    /// <summary>
    /// Reads the maps joined to this one's edges.
    /// <para>
    /// Dive and emerge connections are read and discarded. They join a surface map to
    /// an underwater one, which is a Hoenn feature and not something walking off an
    /// edge can reach.
    /// </para>
    /// </summary>
    public static List<MapConnection> ReadConnections(Rom rom, MapHeaderRecord header, Action<string>? log = null)
    {
        var connections = new List<MapConnection>();

        if (header.ConnectionsPointer == 0) return connections;
        if (rom.ToOffsetOrNull(header.ConnectionsPointer) is not { } record) return connections;
        if (record + 8 > rom.Length) return connections;

        uint rawCount = rom.ReadU32(record);
        if (rawCount is 0 or > MaxConnections) return connections;

        int count = (int)rawCount;

        if (rom.ToOffsetOrNull(rom.ReadU32(record + 4)) is not { } table) return connections;
        if (table + count * ConnectionSizeBytes > rom.Length) return connections;

        for (int i = 0; i < count; i++)
        {
            int at = table + i * ConnectionSizeBytes;

            uint direction = rom.ReadU32(at);
            int offset = (int)rom.ReadU32(at + 4);
            int bank = rom.ReadU8(at + 8);
            int number = rom.ReadU8(at + 9);

            if (SideOf(direction) is not { } side)
            {
                if (direction is not (5 or 6)) log?.Invoke($"    connection {i} has direction {direction} — ignored");
                continue;
            }

            connections.Add(new MapConnection(side, offset, WorldExporter.MapId(bank, number)));
        }

        return connections;
    }

    /// <summary>
    /// The cartridge's direction numbering. One-based, and in an order that is not
    /// the one anybody would choose — which is exactly why it is written down here
    /// rather than assumed to match an enum's declaration order.
    /// </summary>
    private static ConnectionSide? SideOf(uint direction) => direction switch
    {
        1 => ConnectionSide.Down,
        2 => ConnectionSide.Up,
        3 => ConnectionSide.Left,
        4 => ConnectionSide.Right,
        _ => null,
    };
}
