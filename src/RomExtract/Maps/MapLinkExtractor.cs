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
    public static List<Warp> ReadWarps(
        Rom rom,
        MapHeaderRecord header,
        int width,
        int height,
        Action<string>? log = null,
        ICollection<DroppedEvent>? dropped = null)
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
                dropped?.Add(new DroppedEvent(DroppedEvent.Warps, i, count, x, y, width, height, At: at));
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
    public static List<MapObject> ReadObjects(
        Rom rom,
        MapHeaderRecord header,
        int width,
        int height,
        Action<string>? log = null,
        ICollection<DroppedEvent>? dropped = null)
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

            // THE KIND BYTE. 0xFF is not a person on this map — it is a clone of a person on a
            // map beside it, and every field after the square means something else. The off-map
            // test below happened to catch all nine of this cartridge's, which is the right
            // answer for the wrong reason: a clone whose square landed inside its own map would
            // have been read as somebody with an elevation of ten and a trainer type of
            // twenty-seven. Decided by the byte that says so.
            int kind = rom.ReadU8(at + 2);
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

            // The word at +20, and it is a flag id. Six hundred and five of the
            // cartridge's sixteen hundred objects have a non-zero one and every single
            // one of those is in the range flags live in; the other two fields this
            // record has spare are zero on all sixteen hundred.
            //
            // Which flag it is settles what it does. Pallet Town has three people and
            // only one of them carries a number: object 3, the professor, and his is
            // 0x2C — which is the exact flag the opening script sets as its last act,
            // at the moment he has finished walking you to his lab and gone inside. Set
            // means hidden.
            int hiddenBy = rom.ReadU16(at + 20);

            if (kind == CloneKind)
            {
                log?.Invoke($"    object {i} is a clone of #{rom.ReadU8(at + 8)} on another map");
                dropped?.Add(new DroppedEvent(
                    DroppedEvent.Clones,
                    i,
                    count,
                    x,
                    y,
                    width,
                    height,
                    script,
                    Variable: rom.ReadU16(at + 12),
                    Value: rom.ReadU16(at + 14),
                    At: at,
                    LocalId: localId));
                continue;
            }

            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                log?.Invoke($"    object {i} at ({x}, {y}) is outside a {width}x{height} map — dropped");
                dropped?.Add(new DroppedEvent(
                    DroppedEvent.Objects, i, count, x, y, width, height, script,
                    At: at, LocalId: localId));
                continue;
            }

            bool hasScript = rom.IsRomAddress(script);

            // Which trainer somebody is is not in this record — only whether they watch
            // for one. The id is an argument to a command inside their script, and it is
            // asked of everybody with a script rather than only of the marked ones.
            //
            // BROCK is why. A gym leader's record says trainer type nothing at all: he
            // has no line of sight and never walks over, because a gym leader is fought
            // by being talked to. Reading the id only for the marked ones left him with
            // no trainer id, so the server had nobody to field — he said all seven pages
            // of "my POKeMON are all the ROCK type!" and then the box closed and nothing
            // happened. The mark means "watches"; the script means "fights".
            int trainerId = hasScript ? Scripts.ScriptReader.FindTrainer(rom, script) ?? 0 : 0;

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
                HiddenBy = hiddenBy,
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

    /// <summary>A trigger record: a square, an elevation, a condition, and a script.</summary>
    /// <summary>The byte after the graphics id, when the record is a clone rather than a person.</summary>
    private const int CloneKind = 0xFF;

    /// <summary>
    /// Where each of a map's four event tables sits, how many records it claims and how wide one
    /// of them is — exposed so a sweep can ask what a reader NEVER LOOKS AT.
    /// </summary>
    /// <remarks>
    /// <b>One definition of each width.</b> The sizes are the same constants the readers below
    /// use, and the two lists whose size this project derived rather than knew go through
    /// <see cref="EventLayout.Table"/> exactly as they do there. A sweep that wrote its own copy
    /// of "a trigger is sixteen bytes" would be the fault of 251 and 258 with a new face.
    /// </remarks>
    public static IEnumerable<(string List, int Table, int Count, int Size)> EventTables(
        Rom rom, MapHeaderRecord header)
    {
        if (Table(rom, header, first: true, MaxObjects, ObjectSizeBytes) is { } objects)
            yield return (DroppedEvent.Objects, objects.Table, objects.Count, ObjectSizeBytes);

        if (Table(rom, header, first: false, MaxWarps, WarpSizeBytes) is { } warps)
            yield return (DroppedEvent.Warps, warps.Table, warps.Count, WarpSizeBytes);

        if (EventLayout.Table(rom, header.EventsPointer, EventLayout.Triggers, TriggerSizeBytes)
            is { } triggers)
        {
            yield return (DroppedEvent.Triggers, triggers.Table, triggers.Count, TriggerSizeBytes);
        }

        if (EventLayout.Table(rom, header.EventsPointer, EventLayout.Signs, SignSizeBytes)
            is { } signs)
        {
            yield return (DroppedEvent.Signs, signs.Table, signs.Count, SignSizeBytes);
        }
    }

    /// <summary>
    /// The first two of the four lists, whose count and pointer the readers take by hand rather
    /// than through <see cref="EventLayout"/>.
    /// </summary>
    private static (int Table, int Count)? Table(
        Rom rom, MapHeaderRecord header, bool first, int most, int size)
    {
        if (header.EventsPointer == 0) return null;
        if (rom.ToOffsetOrNull(header.EventsPointer) is not { } events) return null;
        if (events + EventsPointersOffset + 8 > rom.Length) return null;

        int count = rom.ReadU8(events + (first ? 0 : 1));

        if (count is 0 || count > most) return null;

        uint pointer = rom.ReadU32(events + EventsPointersOffset + (first ? 0 : 4));

        if (rom.ToOffsetOrNull(pointer) is not { } table) return null;

        return table + count * size > rom.Length ? null : (table, count);
    }

    private const int TriggerSizeBytes = 16;

    private const int TriggerVariableOffset = 6;

    private const int TriggerPointerOffset = 12;

    /// <summary>
    /// Reads the squares that run a script when somebody walks onto them.
    /// <para>
    /// The third of the events record's four lists. Sixteen bytes, derived the same way
    /// as the signs — by scoring every plausible shape against every map and asking
    /// which one produces squares inside the map with readable scripts behind them.
    /// Two hundred and three of the two hundred and twenty-eight came out clean; the
    /// twenty-five that did not are scripts opening with a command still unknown, which
    /// is a gap in the reader rather than in the shape.
    /// </para>
    /// <para>
    /// The condition is a variable and a value, and it is what stops a story beat
    /// happening twice: the script's last act is to write the variable to something
    /// else, and the square goes quiet.
    /// </para>
    /// </summary>
    public static List<MapTrigger> ReadTriggers(
        Rom rom,
        MapHeaderRecord header,
        int width,
        int height,
        Action<string>? log = null,
        ICollection<DroppedEvent>? dropped = null)
    {
        var triggers = new List<MapTrigger>();

        if (EventLayout.Table(rom, header.EventsPointer, EventLayout.Triggers, TriggerSizeBytes)
            is not { } list)
        {
            return triggers;
        }

        (int table, int count) = list;

        for (int i = 0; i < count; i++)
        {
            int at = table + i * TriggerSizeBytes;

            int x = (short)rom.ReadU16(at);
            int y = (short)rom.ReadU16(at + 2);

            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                log?.Invoke($"    trigger {i} at ({x}, {y}) is outside a {width}x{height} map — dropped");
                dropped?.Add(new DroppedEvent(
                    DroppedEvent.Triggers,
                    i,
                    count,
                    x,
                    y,
                    width,
                    height,
                    rom.ReadU32(at + TriggerPointerOffset),
                    rom.ReadU16(at + TriggerVariableOffset),
                    rom.ReadU16(at + TriggerVariableOffset + 2),
                    at));
                continue;
            }

            uint script = rom.ReadU32(at + TriggerPointerOffset);
            bool real = rom.IsRomAddress(script);

            triggers.Add(new MapTrigger(
                x,
                y,
                rom.ReadU16(at + TriggerVariableOffset),
                rom.ReadU16(at + TriggerVariableOffset + 2),
                real ? script : 0,

                // Which trainer, if any, this square picks a fight as. The same question
                // asked of people, answered the same way: the id is an argument to a
                // command inside the script, not a field of the record. A rival waiting
                // on a route is a trigger, and the server has to be able to field him
                // without ever seeing a cartridge.
                real ? Scripts.ScriptReader.FindTrainers(rom, script) : []));
        }

        return triggers;
    }

    /// <summary>A sign record: a square, an elevation, a kind, and one word.</summary>
    private const int SignSizeBytes = 12;

    private const int SignKindOffset = 5;

    private const int SignPointerOffset = 8;

    /// <summary>
    /// Reads the signs on one map.
    /// <para>
    /// The fourth of the events record's four lists, and one this project has never
    /// opened. Its shape was derived by scoring every plausible one against the whole
    /// cartridge and asking which produced squares inside the map with readable scripts
    /// behind them — twelve bytes, the square first, the word last, at seventy per cent.
    /// </para>
    /// <para>
    /// The other thirty per cent were not a wrong shape. They are the buried items, and
    /// they say so: the byte at +5 is 7 for every one of the hundred and eighty-three
    /// records whose last word is not a usable pointer, and for none of the others. That
    /// is the difference between a shape that is wrong and a list that holds two kinds
    /// of thing, and the only way to tell them apart is to look at what the misses have
    /// in common.
    /// </para>
    /// </summary>
    public static List<MapSign> ReadSigns(
        Rom rom,
        MapHeaderRecord header,
        int width,
        int height,
        Action<string>? log = null,
        ICollection<DroppedEvent>? dropped = null)
    {
        var signs = new List<MapSign>();

        if (EventLayout.Table(rom, header.EventsPointer, EventLayout.Signs, SignSizeBytes)
            is not { } list)
        {
            return signs;
        }

        (int table, int count) = list;

        for (int i = 0; i < count; i++)
        {
            int at = table + i * SignSizeBytes;

            int x = (short)rom.ReadU16(at);
            int y = (short)rom.ReadU16(at + 2);
            int kind = rom.ReadU8(at + SignKindOffset);

            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                log?.Invoke($"    sign {i} at ({x}, {y}) is outside a {width}x{height} map — dropped");
                dropped?.Add(new DroppedEvent(
                    DroppedEvent.Signs,
                    i,
                    count,
                    x,
                    y,
                    width,
                    height,
                    kind == MapSign.HiddenItem ? 0 : rom.ReadU32(at + SignPointerOffset),
                    At: at));
                continue;
            }

            // The buried ones keep an item id in the same four bytes a script pointer
            // lives in, so reading it as an address is following a pointer to nowhere.
            uint script = kind == MapSign.HiddenItem ? 0 : rom.ReadU32(at + SignPointerOffset);

            signs.Add(new MapSign(x, y, kind, script));
        }

        return signs;
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
