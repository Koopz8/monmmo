using System.Text;

namespace PokeMmo.Core.World;

/// <summary>One map's identity, size, walkability and encounters. No graphics.</summary>
public sealed record MapData(string Id, string Name, int Width, int Height, byte[] Collision)
{
    /// <summary>
    /// What each square is — grass, ledge, ordinary ground. Empty when unknown, in
    /// which case the map simply has no encounter squares.
    /// </summary>
    public byte[] Behaviours { get; init; } = [];

    public MapEncounters? Encounters { get; init; }

    /// <summary>Neighbouring maps joined along this one's edges.</summary>
    public IReadOnlyList<MapConnection> Connections { get; init; } = [];

    /// <summary>Doors, stairs and cave mouths on this map.</summary>
    public IReadOnlyList<Warp> Warps { get; init; } = [];

    /// <summary>People and other things standing on this map.</summary>
    public IReadOnlyList<MapObject> Objects { get; init; } = [];

    /// <summary>
    /// Squares that run a script when somebody walks onto them.
    /// <para>
    /// Carried for the server even though it cannot run one. What it can do is know that
    /// a square is a trigger at all — so a client claiming a cutscene started somewhere
    /// there is no cutscene can be told no — and which trainer that trigger fields, so a
    /// rival waiting on a route is a fight it can actually run.
    /// </para>
    /// </summary>
    public IReadOnlyList<MapTrigger> Triggers { get; init; } = [];

    /// <summary>
    /// What this map runs on arrival, when one of its variables says so.
    /// <para>
    /// Carried for the same reason triggers are, and with the same hole in it: no script
    /// address, because that is a cartridge address and this file is the server's. What
    /// the server needs is the condition, so it can agree that arriving here does start
    /// something and open a scene window for it.
    /// </para>
    /// </summary>
    public IReadOnlyList<MapEntryScript> OnEntry { get; init; } = [];

    /// <summary>The arrival script armed by what this player's variables hold, if any.</summary>
    public MapEntryScript? EntryFor(Func<int, int> read) =>
        OnEntry.FirstOrDefault(e => e.Armed(read(e.Variable)));

    /// <summary>The trigger on a square, if there is one.</summary>
    public MapTrigger? TriggerAt(GridPosition square) =>
        Triggers.FirstOrDefault(t => t.X == square.X && t.Y == square.Y);

    /// <summary>
    /// The trigger on a square that is actually armed for a given save, if any is.
    /// <para>
    /// A square can carry more than one, and the lab door carries two: one waiting for
    /// 0x4055 to hold 2 and one waiting for it to hold 3. Taking the first of them and
    /// asking whether it is armed answers no for the square whenever the other one is
    /// the live one, which is how the rival's challenge could play on the client and be
    /// refused by the server in the same breath — the client looks for an armed trigger
    /// and this side looked for any trigger.
    /// </para>
    /// </summary>
    /// <para>
    /// No <c>HasScript</c> here, deliberately. A trigger's script address is a cartridge
    /// address and this file is the server's, so every trigger the server loads has zero
    /// in that field — asking for one would find nothing anywhere, forever.
    /// </para>
    public MapTrigger? ArmedTriggerAt(GridPosition square, Func<int, int> read) =>
        Triggers.FirstOrDefault(t =>
            t.X == square.X && t.Y == square.Y && t.Armed(read(t.Variable)));

    /// <summary>Whatever is standing on a square, if anything.</summary>
    public MapObject? ObjectAt(GridPosition square) =>
        Objects.FirstOrDefault(o => o.X == square.X && o.Y == square.Y);

    /// <summary>
    /// Walkability, with every warp square opened.
    /// <para>
    /// A door is solid in the block data and the games let you stand on it anyway.
    /// Both sides build their grid this way, because a rule enforced on one side of a
    /// client and server split needs its counterpart on the other — which this project
    /// has now learned three times.
    /// </para>
    /// </summary>
    public CollisionGrid ToGrid() =>
        new CollisionGrid(Width, Height, Collision).WithOpen(Warps.Select(w => w.Square));

    /// <summary>
    /// How many warps sit on squares the block data calls solid.
    /// <para>
    /// Reported at startup. Doors are the overwhelming majority of warps, so a world
    /// where this is near zero is a world whose doors are being read wrongly, and a
    /// world where it is near the warp count is behaving exactly as the cartridge does.
    /// </para>
    /// </summary>
    public int WarpsOnSolidSquares()
    {
        var raw = new CollisionGrid(Width, Height, Collision);

        return Warps.Count(w => !raw.IsWalkable(w.Square));
    }

    /// <summary>
    /// Whether a square is a door: a warp on a square the map data itself calls solid.
    /// <para>
    /// The distinction matters and the cartridge draws it. Of this game's 1294 warps, 279
    /// sit on squares that are solid in the block data — those are doors, and they are
    /// opened for walking through rather than for standing on. The other thousand are
    /// stairs, cave mouths and mats, which are ordinary floor and which people do stand
    /// on.
    /// </para>
    /// </summary>
    public bool IsDoor(GridPosition square) =>
        square.X >= 0 && square.Y >= 0 && square.X < Width && square.Y < Height &&
        Collision[square.Y * Width + square.X] != 0 &&
        Warps.Any(w => w.X == square.X && w.Y == square.Y);

    /// <summary>The warp on a square, if there is one.</summary>
    public Warp? WarpAt(GridPosition square) =>
        Warps.FirstOrDefault(w => w.X == square.X && w.Y == square.Y);

    public MapConnection? ConnectionOn(ConnectionSide side) =>
        Connections.FirstOrDefault(c => c.Side == side);

    public byte BehaviourAt(GridPosition square)
    {
        if (Behaviours.Length == 0) return MetatileBehaviour.Normal;
        if (square.X < 0 || square.X >= Width || square.Y < 0 || square.Y >= Height)
            return MetatileBehaviour.Normal;

        int index = square.Y * Width + square.X;
        return index < Behaviours.Length ? Behaviours[index] : MetatileBehaviour.Normal;
    }

    /// <summary>True when standing on this square can start a wild encounter.</summary>
    public bool IsEncounterSquare(GridPosition square) =>
        Encounters?.Land is { IsUsable: true } && MetatileBehaviour.IsEncounterGrass(BehaviourAt(square));
}

/// <summary>
/// The collision-only world the server runs on.
/// <para>
/// This format exists to keep the server out of the cartridge business. The extractor
/// is client-only by design — the player supplies their own image and it is read on
/// their machine — but the server still has to know which squares are walkable or it
/// cannot be authoritative about anything.
/// </para>
/// <para>
/// The resolution is that an operator generates this file from their own image and
/// the server reads it. The server links no extractor, the repository ships no world
/// file, and nothing here contains graphics, text or audio — only map dimensions and
/// one byte of walkability per square.
/// </para>
/// </summary>
public sealed class WorldData
{
    /// <summary>Identifies the format, so a wrong or stale file fails loudly.</summary>
    private static readonly byte[] Magic = "MONWORLD"u8.ToArray();

    private const int Version = 18;

    private readonly Dictionary<string, MapData> _maps;

    public WorldData(IEnumerable<MapData> maps) =>
        _maps = maps.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<MapData> Maps => _maps.Values;

    public int Count => _maps.Count;

    public MapData? Find(string id) => _maps.GetValueOrDefault(id);

    /// <summary>
    /// Finds a map by name, preferring an exact match and then the largest — so
    /// "route 1" cannot quietly resolve to Route 17.
    /// </summary>
    public MapData? FindByName(string name) =>
        MapNameMatch.Rank(_maps.Values, m => m.Name, name, m => m.Width * m.Height).FirstOrDefault();

    public void Save(Stream output)
    {
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);

        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(_maps.Count);

        foreach (MapData map in _maps.Values)
        {
            writer.Write(map.Id);
            writer.Write(map.Name);
            writer.Write(map.Width);
            writer.Write(map.Height);
            writer.Write(map.Collision.Length);
            writer.Write(map.Collision);

            writer.Write(map.Behaviours.Length);
            writer.Write(map.Behaviours);

            WriteEncounters(writer, map.Encounters);
            WriteLinks(writer, map);
        }
    }

    public void Save(string path)
    {
        using FileStream file = File.Create(path);
        Save(file);
    }

    public static WorldData Load(Stream input)
    {
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);

        byte[] magic = reader.ReadBytes(Magic.Length);

        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("Not a world file.");

        int version = reader.ReadInt32();

        if (version != Version)
            throw new InvalidDataException($"World file is version {version}, expected {Version}.");

        int count = reader.ReadInt32();

        if (count < 0)
            throw new InvalidDataException($"World file claims {count} maps.");

        var maps = new List<MapData>(Math.Min(count, 4096));

        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            string name = reader.ReadString();
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            int collisionLength = reader.ReadInt32();

            if (width <= 0 || height <= 0 || collisionLength != width * height)
                throw new InvalidDataException($"Map '{id}' has inconsistent dimensions.");

            byte[] collision = reader.ReadBytes(collisionLength);

            int behaviourLength = reader.ReadInt32();

            if (behaviourLength != 0 && behaviourLength != width * height)
                throw new InvalidDataException($"Map '{id}' has {behaviourLength} behaviours for {width * height} squares.");

            byte[] behaviours = reader.ReadBytes(behaviourLength);

            MapEncounters? mapEncounters = ReadEncounters(reader, id);
            (IReadOnlyList<MapConnection> connections, IReadOnlyList<Warp> warps) = ReadLinks(reader, id);
            IReadOnlyList<MapObject> objects = ReadObjects(reader, id);
            IReadOnlyList<MapTrigger> triggers = ReadTriggers(reader);
            IReadOnlyList<MapEntryScript> onEntry = ReadEntryScripts(reader);

            maps.Add(new MapData(id, name, width, height, collision)
            {
                Behaviours = behaviours,
                Encounters = mapEncounters,
                Connections = connections,
                Warps = warps,
                Objects = objects,
                Triggers = triggers,
                OnEntry = onEntry,
            });
        }

        return new WorldData(maps);
    }

    private static void WriteLinks(BinaryWriter writer, MapData map)
    {
        writer.Write(map.Connections.Count);

        foreach (MapConnection connection in map.Connections)
        {
            writer.Write((int)connection.Side);
            writer.Write(connection.Offset);
            writer.Write(connection.MapId);
        }

        writer.Write(map.Warps.Count);

        foreach (Warp warp in map.Warps)
        {
            writer.Write(warp.X);
            writer.Write(warp.Y);
            writer.Write(warp.TargetWarpId);
            writer.Write(warp.TargetMapId);
        }

        writer.Write(map.Objects.Count);

        foreach (MapObject entry in map.Objects)
        {
            writer.Write(entry.LocalId);
            writer.Write(entry.GraphicsId);
            writer.Write(entry.X);
            writer.Write(entry.Y);
            writer.Write((int)entry.Facing);
            writer.Write(entry.MovementType);
            writer.Write(entry.IsTrainer);
            writer.Write(entry.RangeX);
            writer.Write(entry.RangeY);

            // The trainer id is a number, not an address — the script it was read out
            // of stays on the cartridge, and so does the address of that script.
            writer.Write(entry.TrainerId);
            writer.Write(entry.SightRange);
            writer.Write(entry.Heals);
            writer.Write(entry.GivesItemId);
            writer.Write(entry.GivesCount);
            writer.Write(entry.Talks);
            writer.Write(entry.ShiftedBy);

            // A species or a variable holding one, and a level. Numbers either way, like
            // everything else that travels: what they mean needs the cartridge.
            writer.Write(entry.GivesSpecies);
            writer.Write(entry.GivesLevel);
            writer.Write(entry.HiddenBy);


            // Item ids, which are numbers. The list itself lived at a cartridge address
            // and that address stays where it was.
            writer.Write(entry.Stock.Count);
            foreach (int itemId in entry.Stock) writer.Write(itemId);
        }

        writer.Write(map.Triggers.Count);

        foreach (MapTrigger trigger in map.Triggers)
        {
            writer.Write(trigger.X);
            writer.Write(trigger.Y);
            writer.Write(trigger.Variable);
            writer.Write(trigger.Value);

            // No script address, for the same reason an object carries none: it is a
            // cartridge address and this file is the server's.
            //
            // A count and then the ids, because the rival is three trainers behind one
            // square and which of them shows up is a fact about the save rather than
            // about the square. The server keeps the set it is allowed to accept.
            writer.Write(trigger.Fights.Count);

            foreach (int id in trigger.Fights) writer.Write(id);
        }

        writer.Write(map.OnEntry.Count);

        foreach (MapEntryScript entry in map.OnEntry)
        {
            writer.Write(entry.Variable);
            writer.Write(entry.Value);
        }
    }

    private static List<MapEntryScript> ReadEntryScripts(BinaryReader reader)
    {
        int count = reader.ReadInt32();

        if (count is < 0 or > 64)
            throw new InvalidDataException($"A map claims {count} arrival scripts.");

        var entries = new List<MapEntryScript>(count);

        for (int i = 0; i < count; i++)
            entries.Add(new MapEntryScript(reader.ReadInt32(), reader.ReadInt32(), ScriptAddress: 0));

        return entries;
    }

    private static List<MapTrigger> ReadTriggers(BinaryReader reader)
    {
        int count = reader.ReadInt32();

        // Generous, and there to fail on a wrong file rather than allocate gigabytes
        // from a bad length. The busiest map in FireRed has fewer than twenty.
        if (count is < 0 or > 256)
            throw new InvalidDataException($"A map claims {count} triggers.");

        var triggers = new List<MapTrigger>(count);

        for (int i = 0; i < count; i++)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            int variable = reader.ReadInt32();
            int value = reader.ReadInt32();
            int fightCount = reader.ReadInt32();

            if (fightCount is < 0 or > 16)
                throw new InvalidDataException($"A trigger claims {fightCount} fights.");

            var fights = new List<int>(fightCount);

            for (int f = 0; f < fightCount; f++) fights.Add(reader.ReadInt32());

            triggers.Add(new MapTrigger(x, y, variable, value, ScriptAddress: 0, Fights: fights));
        }

        return triggers;
    }

    /// <summary>
    /// Reads links back, refusing counts that could only come from a corrupted file.
    /// The bounds are generous — the point is to fail on a wrong file rather than to
    /// allocate gigabytes from a bad length.
    /// </summary>
    private static (IReadOnlyList<MapConnection>, IReadOnlyList<Warp>) ReadLinks(BinaryReader reader, string mapId)
    {
        int connectionCount = reader.ReadInt32();

        if (connectionCount is < 0 or > 64)
            throw new InvalidDataException($"Map '{mapId}' claims {connectionCount} connections.");

        var connections = new List<MapConnection>(connectionCount);

        for (int i = 0; i < connectionCount; i++)
        {
            int side = reader.ReadInt32();

            if (!Enum.IsDefined(typeof(ConnectionSide), side))
                throw new InvalidDataException($"Map '{mapId}' has a connection on side {side}.");

            connections.Add(new MapConnection((ConnectionSide)side, reader.ReadInt32(), reader.ReadString()));
        }

        int warpCount = reader.ReadInt32();

        if (warpCount is < 0 or > 1024)
            throw new InvalidDataException($"Map '{mapId}' claims {warpCount} warps.");

        var warps = new List<Warp>(warpCount);

        for (int i = 0; i < warpCount; i++)
            warps.Add(new Warp(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadString()));

        return (connections, warps);
    }

    /// <summary>What one object sells, refusing a count that could only be corruption.</summary>
    private static List<int> ReadStock(BinaryReader reader, string mapId)
    {
        int count = reader.ReadInt32();

        if (count is < 0 or > 64)
            throw new InvalidDataException($"Map '{mapId}' has a shop claiming {count} items.");

        var stock = new List<int>(count);

        for (int i = 0; i < count; i++) stock.Add(reader.ReadInt32());

        return stock;
    }

    private static IReadOnlyList<MapObject> ReadObjects(BinaryReader reader, string mapId)
    {
        int count = reader.ReadInt32();

        if (count is < 0 or > 1024)
            throw new InvalidDataException($"Map '{mapId}' claims {count} objects.");

        var objects = new List<MapObject>(count);

        for (int i = 0; i < count; i++)
        {
            int localId = reader.ReadInt32();
            int graphicsId = reader.ReadInt32();
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            int facing = reader.ReadInt32();

            if (!Enum.IsDefined(typeof(Direction), facing))
                throw new InvalidDataException($"Map '{mapId}' has an object facing {facing}.");

            objects.Add(new MapObject(
                localId,
                graphicsId,
                x,
                y,
                (Direction)facing,
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                // Deliberately zero. A script address is a cartridge address and this
                // file does not carry any.
                0,
                reader.ReadInt32(),
                reader.ReadInt32())
            {
                Heals = reader.ReadBoolean(),
                GivesItemId = reader.ReadInt32(),
                GivesCount = reader.ReadInt32(),
                Talks = reader.ReadBoolean(),
                ShiftedBy = reader.ReadInt32(),
                GivesSpecies = reader.ReadInt32(),
                GivesLevel = reader.ReadInt32(),
                HiddenBy = reader.ReadInt32(),
                Stock = ReadStock(reader, mapId),
            });
        }

        return objects;
    }

    private static void WriteEncounters(BinaryWriter writer, MapEncounters? encounters)
    {
        writer.Write(encounters is not null);
        if (encounters is null) return;

        foreach (EncounterKind kind in Enum.GetValues<EncounterKind>())
        {
            EncounterTable? table = encounters.For(kind);
            writer.Write(table is not null);
            if (table is null) continue;

            writer.Write(table.Rate);
            writer.Write(table.Slots.Count);

            foreach (WildSlot slot in table.Slots)
            {
                writer.Write(slot.Species);
                writer.Write(slot.MinLevel);
                writer.Write(slot.MaxLevel);
            }
        }
    }

    private static MapEncounters? ReadEncounters(BinaryReader reader, string mapId)
    {
        if (!reader.ReadBoolean()) return null;

        var tables = new Dictionary<EncounterKind, EncounterTable>();

        foreach (EncounterKind kind in Enum.GetValues<EncounterKind>())
        {
            if (!reader.ReadBoolean()) continue;

            int rate = reader.ReadInt32();
            int slotCount = reader.ReadInt32();

            if (rate is < 0 or > 100 || slotCount is < 0 or > 64)
                throw new InvalidDataException($"Map '{mapId}' has an implausible encounter table.");

            var slots = new List<WildSlot>(slotCount);

            for (int i = 0; i < slotCount; i++)
                slots.Add(new WildSlot(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));

            tables[kind] = new EncounterTable(kind, rate, slots);
        }

        return new MapEncounters(
            mapId,
            tables.GetValueOrDefault(EncounterKind.Land),
            tables.GetValueOrDefault(EncounterKind.Water),
            tables.GetValueOrDefault(EncounterKind.RockSmash),
            tables.GetValueOrDefault(EncounterKind.Fishing));
    }

    /// <summary>
    /// Loads a world file, naming it in anything that goes wrong.
    /// <para>
    /// The path matters more than it looks. The server reads a relative name against
    /// whatever directory it was started from, so "world.dat is the wrong version" is
    /// only half a sentence — there can be two of them, and the one being read is not
    /// always the one that was just written. An operator who is told the version and
    /// not the path goes and re-exports the file that was already correct.
    /// </para>
    /// </summary>
    public static WorldData Load(string path)
    {
        using FileStream file = File.OpenRead(path);

        try
        {
            return Load(file);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"{Path.GetFullPath(path)}: {ex.Message}", ex);
        }
    }
}
