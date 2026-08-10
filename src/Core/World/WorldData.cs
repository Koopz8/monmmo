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

    public CollisionGrid ToGrid() => new(Width, Height, Collision);

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

    private const int Version = 3;

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

            maps.Add(new MapData(id, name, width, height, collision)
            {
                Behaviours = behaviours,
                Encounters = mapEncounters,
                Connections = connections,
                Warps = warps,
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

    public static WorldData Load(string path)
    {
        using FileStream file = File.OpenRead(path);
        return Load(file);
    }
}
