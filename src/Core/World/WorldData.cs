using System.Text;

namespace PokeMmo.Core.World;

/// <summary>A script variable a new character starts holding.</summary>
public sealed record StartingVariable(int Id, int Value);

/// <summary>One map's identity, size, walkability and encounters. No graphics.</summary>
/// <summary>
/// Where a new character wakes up.
/// <para>
/// One place, because it was two. The server had <c>"4.1"</c> in its argument default and the
/// dump tool grew a second copy of it — and the copy started life as
/// <c>world.Maps.First()</c>, which is map 0.0, a floor of CELADON DEPT. The first
/// playthrough ever run reached one map and stopped, and every number it printed was about a
/// shop.
/// </para>
/// <para>
/// It is <b>modelled</b>: nothing on the cartridge says where a new game begins in a form this
/// project reads, so it is a decision — and a decision written down twice is a decision that
/// will differ. Everything that starts somebody starts them here.
/// </para>
/// </summary>
public static class Beginning
{
    /// <summary>PALLET TOWN, which is where everybody starts.</summary>
    public const string MapId = "4.1";
}

public sealed record MapData(string Id, string Name, int Width, int Height, byte[] Collision)
{
    /// <summary>
    /// What each square is — grass, ledge, ordinary ground. Empty when unknown, in
    /// which case the map simply has no encounter squares.
    /// </summary>
    public byte[] Behaviours { get; init; } = [];

    /// <summary>
    /// Which song plays here, as the number the cartridge's own map header gives.
    /// <para>
    /// A number and never a name, for the same reason every other id in this file is a
    /// number: the server has no idea what any of them sound like and must not be able to
    /// find out. What it can do is say "this place is song 277", which is the one fact
    /// about music that has to be agreed on by both sides — otherwise two people standing
    /// in the same room hear different things.
    /// </para>
    /// <para>
    /// Zero means the header said zero, which the cartridge uses for "carry on playing
    /// whatever was already playing". That is a real value rather than a missing one, and
    /// it is why this is not nullable.
    /// </para>
    /// </summary>
    public int Music { get; init; }

    public MapEncounters? Encounters { get; init; }

    /// <summary>Neighbouring maps joined along this one's edges.</summary>
    public IReadOnlyList<MapConnection> Connections { get; init; } = [];

    /// <summary>Doors, stairs and cave mouths on this map.</summary>
    public IReadOnlyList<Warp> Warps { get; init; } = [];

    /// <summary>Doors the scripts on this map can make, which are on no square.</summary>
    public IReadOnlyList<ScriptedDoor> Doors { get; init; } = [];

    /// <summary>The boat this map is a stop on, if it is one.</summary>
    public FerryDock? Ferry { get; init; }

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
    /// Things written on this map that can be read from the square in front of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The fourth list, and the one the run could not see.</b> `MapSign` has existed since the
    /// map work and the map scan has read all 519 of the scripted ones for as long as it has had
    /// five kinds — but this record, which is what the playthrough and the server walk, carried
    /// people, triggers and arrival scripts and no signs at all. So "the playthrough never runs
    /// signs" was not a choice anybody made; there was nothing for it to run.
    /// </para>
    /// <para>
    /// That is 224's fault — <i>check the enumerator before the count</i> — standing in the other
    /// half of the project. 224 unified the READING onto a list that knows five kinds and nothing
    /// compared it with the one the RUN walks.
    /// </para>
    /// <para>
    /// The hidden-item records are here too and marked, because a reader that took their item id
    /// for a script address would follow a pointer to nowhere — <see cref="MapSign.HasScript"/>
    /// is what separates the 519 from the 183.
    /// </para>
    /// </remarks>
    public IReadOnlyList<MapSign> Signs { get; init; } = [];

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
    public CollisionGrid ToGrid() => ToGrid(surfing: false);

    /// <summary>
    /// Walkability for somebody who is, or is not, on the water.
    /// <para>
    /// Water is not a wall in the block data. Two thirds of it is passable — the
    /// cartridge keeps people out of the sea with a rule about the behaviour byte, not
    /// with collision — so on 47 maps of this game a player could simply walk out onto
    /// it. The S.S. ANNE's harbour is 1446 walkable squares of open water.
    /// </para>
    /// <para>
    /// Which makes this one grid rather than two rules. Off the water, water is solid.
    /// On it, water is walkable and the land is left exactly as it was, which is what
    /// makes stepping ashore the way off.
    /// </para>
    /// </summary>
    public CollisionGrid ToGrid(bool surfing)
    {
        var grid = new CollisionGrid(Width, Height, Collision);

        if (Behaviours.Length > 0)
            grid = surfing ? grid.WithOpen(WaterSquares()) : grid.With(WaterSquares());

        // The doors last, and that order is the whole point. A warp on a water square
        // would otherwise be sealed by the pass above — the sea would close a door, and
        // a door that cannot be stood on is a map that cannot be entered.
        return grid.WithOpen(Warps.Select(w => w.Square));
    }

    /// <summary>Every square of this map that is water.</summary>
    public IEnumerable<GridPosition> WaterSquares()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int at = y * Width + x;

                if (at < Behaviours.Length && MetatileBehaviour.IsWater(Behaviours[at]))
                    yield return new GridPosition(x, y);
            }
        }
    }

    /// <summary>
    /// True when everybody on this map is deliberately quiet.
    /// <para>
    /// The trade and battle rooms — the CABLE CLUB above every POKeMON CENTER and the
    /// rooms beside it. They are told apart by a property of their own data rather than
    /// by a list somebody has to keep: every exit they have leads back the way you came,
    /// because a room reached from twelve places cannot write down which one it came
    /// from. Nothing else in the world is shaped like that except the lifts, which have
    /// nobody standing in them.
    /// </para>
    /// <para>
    /// Quiet on purpose, and this is a decision rather than a discovery: what those
    /// rooms are for is two players meeting, and there is no point giving their
    /// attendants lines until there is something on the other side of the counter. So
    /// they stand there and say nothing, on both sides of the split, and the day they
    /// have a job this is the one line to delete.
    /// </para>
    /// </summary>
    public bool PeopleAreSilent => Warps.Count > 0 && Warps.All(w => w.IsDynamic);

    /// <summary>True when this square is water.</summary>
    public bool IsWater(GridPosition square)
    {
        if (square.X < 0 || square.X >= Width || square.Y < 0 || square.Y >= Height) return false;

        int at = square.Y * Width + square.X;

        return at < Behaviours.Length && MetatileBehaviour.IsWater(Behaviours[at]);
    }

    /// <summary>
    /// Where a step onto this square lands, when the square is a ledge being hopped the
    /// way it is meant to be hopped.
    /// <para>
    /// Null for anything else, and that covers three quite different cases on purpose: a
    /// square that is not a ledge, a ledge approached from any of the other three sides,
    /// and a ledge whose landing square is not somewhere anybody could stand. All three
    /// mean the same thing to whoever is asking — you cannot go that way — and none of
    /// them is a special case worth a caller knowing about.
    /// </para>
    /// <para>
    /// The landing is two squares from where the walker started, because a ledge is not
    /// somewhere you end up. Nobody stands on one.
    /// </para>
    /// </summary>
    public GridPosition? HopOnto(GridPosition square, Direction facing, IReadOnlyDictionary<byte, Direction>? hops = null)
    {
        if (square.X < 0 || square.X >= Width || square.Y < 0 || square.Y >= Height) return null;

        int at = square.Y * Width + square.X;

        if (at >= Behaviours.Length) return null;

        IReadOnlyDictionary<byte, Direction> table = hops ?? MetatileBehaviour.Hops;

        if (!table.TryGetValue(Behaviours[at], out Direction way) || way != facing) return null;

        GridPosition landing = square.Step(facing);

        return ToGrid().IsWalkable(landing) ? landing : null;
    }

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

    /// <summary>
    /// The map across one edge of this one, at the square being stepped off from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A side can carry MORE THAN ONE neighbour, and this returned the first for twenty
    /// milestones (285).</b> <c>3.60</c> WATER PATH declares three maps off its left edge —
    /// GREEN PATH at offset 0, SIX ISLAND at 40, <c>3.61</c> at 80 — and every square stepping
    /// west off it was sent to GREEN PATH whatever row it stood on. The arrival then landed
    /// outside GREEN PATH's grid, failed the walkability check, and the crossing simply did not
    /// happen: a fault that DELETES edges and reports nothing.
    /// </para>
    /// <para>
    /// Which one is the right one is not a guess. <c>AcrossEdge</c> puts the arrival at
    /// <c>from.Y - offset</c> (or <c>from.X - offset</c> along the top and bottom), so the
    /// neighbour that covers this crossing is the one whose grid contains that coordinate. At
    /// most one can, because the offsets lay them end to end.
    /// </para>
    /// <para>
    /// A neighbour this world file does not hold cannot be measured that way, so it is kept as
    /// the answer of last resort — a caller reporting "a map this file lacks" must still see it.
    /// </para>
    /// </remarks>
    /// <param name="side">Which edge is being stepped off.</param>
    /// <param name="from">The square being stepped off from, on THIS map.</param>
    /// <param name="find">How to look a neighbour up; nought for a map this world does not hold.</param>
    public MapConnection? ConnectionOn(
        ConnectionSide side, GridPosition from, Func<string, MapData?> find)
    {
        MapConnection? unknown = null;

        foreach (MapConnection candidate in Connections)
        {
            if (candidate.Side != side) continue;

            if (find(candidate.MapId) is not { } neighbour)
            {
                unknown ??= candidate;

                continue;
            }

            if (Covers(side, from, neighbour, candidate.Offset)) return candidate;
        }

        return unknown;
    }

    /// <summary>Whether a crossing off <paramref name="side"/> lands inside this neighbour.</summary>
    private static bool Covers(
        ConnectionSide side, GridPosition from, MapData neighbour, int offset)
    {
        int along = side is ConnectionSide.Up or ConnectionSide.Down
            ? from.X - offset
            : from.Y - offset;

        return along >= 0
            && along < (side is ConnectionSide.Up or ConnectionSide.Down
                ? neighbour.Width
                : neighbour.Height);
    }

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

    private const int Version = 29;

    private readonly Dictionary<string, MapData> _maps;

    public WorldData(IEnumerable<MapData> maps) =>
        _maps = maps.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<MapData> Maps => _maps.Values;

    /// <summary>
    /// Flags a brand new character already has, before anything has happened.
    /// <para>
    /// A fresh save is not an empty save. Almost every one of these hides somebody: the
    /// old man in his front room who is supposed to still be up the tower, the shopkeeper
    /// who arrives later, the rival standing where he will one day stand. Start a
    /// character with none of them and every one of those people is on the map at once,
    /// which is not the beginning of the story — it is all of its endings, together.
    /// </para>
    /// <para>
    /// It belongs in the world file rather than in the server, for the ordinary reason:
    /// they are the cartridge's numbers, an operator's image is the only thing that
    /// knows them, and a server with no cartridge cannot invent them.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> FlagsAtStart { get; init; } = [];

    /// <summary>Variables a new character starts holding, as (id, value).</summary>
    public IReadOnlyList<StartingVariable> VariablesAtStart { get; init; } = [];

    /// <summary>
    /// What the boat asks for, if it asks for anything.
    /// <para>
    /// Either pass opens it. Carried here rather than on a dock because it is the ferry's
    /// question and not one harbour's — only VERMILION asks it, and it asks on behalf of
    /// all ten.
    /// </para>
    /// </summary>
    public IReadOnlyList<FerryPass> FerryPasses { get; init; } = [];

    /// <summary>
    /// Whether anything anywhere in this world can put a pass in a bag.
    /// <para>
    /// The reason this is asked at all: a gate whose key exists is a gate, and a gate
    /// whose key does not exist is a wall. Three of this cartridge's 2681 map scripts
    /// mention a pass and all three of them are the ferry <em>asking</em> for one —
    /// nothing gives one, no shop sells one, and nothing sets either flag. Enforcing it
    /// would seal 152 maps behind a door with no key anywhere in the world.
    /// </para>
    /// <para>
    /// So the gate is carried, reported, and enforced only when it can be opened. An
    /// operator handing over item 370 with <c>/item</c> and setting its flag makes it
    /// real; nothing else does.
    /// </para>
    /// </summary>
    public bool AnyPassCanBeHadHere =>
        FerryPasses.Count > 0 && FerryPasses.Any(p => ItemsHandedOut.Contains(p.ItemId));

    /// <summary>Every item id something on a map hands over or sells.</summary>
    public IReadOnlySet<int> ItemsHandedOut =>
        _handedOut ??=
        [
            .. Maps.SelectMany(m => m.Objects)
                .SelectMany(o => o.CanGive.Append(o.GivesItemId).Append(o.WinsItemId).Concat(o.Stock)),
            .. Maps.SelectMany(m => m.OnEntry).Select(e => e.GivesItemId),
        ];

    private HashSet<int>? _handedOut;

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

            // Which song plays here. One number a map, written beside the behaviours
            // because it is the same kind of fact: something the header said about the
            // place, carried across the split as a number and never as a name.
            writer.Write(map.Music);

            WriteEncounters(writer, map.Encounters);
            WriteLinks(writer, map);
        }

        writer.Write(FlagsAtStart.Count);
        foreach (int flag in FlagsAtStart) writer.Write(flag);

        writer.Write(FerryPasses.Count);

        foreach (FerryPass pass in FerryPasses)
        {
            writer.Write(pass.Flag);
            writer.Write(pass.ItemId);
        }

        writer.Write(VariablesAtStart.Count);

        foreach (StartingVariable variable in VariablesAtStart)
        {
            writer.Write(variable.Id);
            writer.Write(variable.Value);
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

            int music = reader.ReadInt32();

            MapEncounters? mapEncounters = ReadEncounters(reader, id);
            (IReadOnlyList<MapConnection> connections, IReadOnlyList<Warp> warps) = ReadLinks(reader, id);
            IReadOnlyList<MapObject> objects = ReadObjects(reader, id);
            IReadOnlyList<MapTrigger> triggers = ReadTriggers(reader);
            IReadOnlyList<MapSign> signs = ReadSigns(reader);
            IReadOnlyList<MapEntryScript> onEntry = ReadEntryScripts(reader);
            IReadOnlyList<ScriptedDoor> doors = ReadDoors(reader, id);
            FerryDock? ferry = ReadFerry(reader);

            maps.Add(new MapData(id, name, width, height, collision)
            {
                Behaviours = behaviours,
                Music = music,
                Encounters = mapEncounters,
                Connections = connections,
                Warps = warps,
                Objects = objects,
                Triggers = triggers,
                Signs = signs,
                OnEntry = onEntry,
                Doors = doors,
                Ferry = ferry,
            });
        }

        int flagCount = reader.ReadInt32();

        if (flagCount < 0)
            throw new InvalidDataException($"World file claims {flagCount} starting flags.");

        var flags = new List<int>(Math.Min(flagCount, 4096));

        for (int i = 0; i < flagCount; i++) flags.Add(reader.ReadInt32());

        int passCount = reader.ReadInt32();

        if (passCount is < 0 or > 64)
            throw new InvalidDataException($"World file claims {passCount} ferry passes.");

        var passes = new List<FerryPass>(passCount);

        for (int i = 0; i < passCount; i++)
            passes.Add(new FerryPass(reader.ReadInt32(), reader.ReadInt32()));

        int variableCount = reader.ReadInt32();

        if (variableCount < 0)
            throw new InvalidDataException($"World file claims {variableCount} starting variables.");

        var variables = new List<StartingVariable>(Math.Min(variableCount, 4096));

        for (int i = 0; i < variableCount; i++)
            variables.Add(new StartingVariable(reader.ReadInt32(), reader.ReadInt32()));

        return new WorldData(maps)
        {
            FlagsAtStart = flags,
            VariablesAtStart = variables,
            FerryPasses = passes,
        };
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

            // What a fight pays out, which is a different question from what a person
            // hands over on being talked to. A gym leader's TM is inside the script the
            // fight runs on being won, and nothing reaches it by talking.
            writer.Write(entry.WinsItemId);
            writer.Write(entry.WinsCount);

            // Not what it gives but what it could ever give. Twenty-nine objects hand
            // something over on a branch a fresh run does not walk — both fossils in MT.
            // MOON among them — so the server holds the set and checks a client's claim
            // against it, exactly as it already does for a trigger's trainer ids.
            writer.Write(entry.CanGive.Count);
            foreach (int itemId in entry.CanGive) writer.Write(itemId);

            // And the fights it could ever start, on the same terms.
            writer.Write(entry.CanFight.Count);

            foreach (WildFight fight in entry.CanFight)
            {
                writer.Write(fight.Species);
                writer.Write(fight.Level);
            }
            writer.Write(entry.TakesItemId);
            writer.Write(entry.TakesCount);
            writer.Write(entry.Talks);
            writer.Write(entry.ShiftedBy);

            // A species or a variable holding one, and a level. Numbers either way, like
            // everything else that travels: what they mean needs the cartridge.
            writer.Write(entry.GivesSpecies);
            writer.Write(entry.GivesLevel);
            writer.Write(entry.HiddenBy);

            // Whether two can be left with this one. Written beside the healing flag
            // because it is the same kind of fact and was found the same way — a routine
            // in the game's own code, identified by who calls it rather than by what it
            // does.
            writer.Write(entry.MindsCreatures);


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

        // The fourth list. The script address stays on the cartridge for the same reason a
        // trigger's does; what travels is where it is and what kind the cartridge tagged it,
        // because the kind is what says whether there is a script behind it at all.
        writer.Write(map.Signs.Count);

        foreach (MapSign sign in map.Signs)
        {
            writer.Write(sign.X);
            writer.Write(sign.Y);
            writer.Write(sign.Kind);
        }

        writer.Write(map.OnEntry.Count);

        foreach (MapEntryScript entry in map.OnEntry)
        {
            writer.Write(entry.Variable);
            writer.Write(entry.Value);

            // No script address, as ever — but what the script hands over is a number
            // and travels, because the server is the only thing that may hand it over.
            writer.Write(entry.GivesItemId);
            writer.Write(entry.GivesCount);
        }

        // The doors that are on no square. Nineteen of them across the world, and they
        // are what four hundred maps' worth of "nothing leads in here" was always about.
        writer.Write(map.Doors.Count);

        foreach (ScriptedDoor door in map.Doors)
        {
            writer.Write(door.What);
            writer.Write(door.TargetMapId);
            writer.Write(door.TargetWarpId);
            writer.Write(door.X);
            writer.Write(door.Y);
        }

        // And the boat, if this map is a stop on it.
        writer.Write(map.Ferry is not null);

        if (map.Ferry is { } dock)
        {
            writer.Write(dock.Number);
            writer.Write(dock.Attendant);
            writer.Write(dock.ArrivalX);
            writer.Write(dock.ArrivalY);
        }
    }

    private static FerryDock? ReadFerry(BinaryReader reader) =>
        reader.ReadBoolean()
            ? new FerryDock(
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32())
            : null;

    /// <summary>The doors this map's scripts can make.</summary>
    private static List<ScriptedDoor> ReadDoors(BinaryReader reader, string mapId)
    {
        int count = reader.ReadInt32();

        if (count is < 0 or > 256)
            throw new InvalidDataException($"Map '{mapId}' claims {count} scripted doors.");

        var doors = new List<ScriptedDoor>(count);

        for (int i = 0; i < count; i++)
        {
            doors.Add(new ScriptedDoor(
                reader.ReadString(), reader.ReadString(),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
        }

        return doors;
    }

    private static List<MapEntryScript> ReadEntryScripts(BinaryReader reader)
    {
        int count = reader.ReadInt32();

        if (count is < 0 or > 64)
            throw new InvalidDataException($"A map claims {count} arrival scripts.");

        var entries = new List<MapEntryScript>(count);

        for (int i = 0; i < count; i++)
        {
            entries.Add(new MapEntryScript(reader.ReadInt32(), reader.ReadInt32(), ScriptAddress: 0)
            {
                GivesItemId = reader.ReadInt32(),
                GivesCount = reader.ReadInt32(),
            });
        }

        return entries;
    }

    private static List<MapSign> ReadSigns(BinaryReader reader)
    {
        int count = reader.ReadInt32();

        // The busiest map in FireRed has twenty-two, and this is here to fail on a wrong file
        // rather than allocate from a bad length.
        if (count is < 0 or > 256) throw new InvalidDataException($"A map claims {count} signs.");

        var signs = new List<MapSign>(count);

        for (var i = 0; i < count; i++)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            int kind = reader.ReadInt32();

            signs.Add(new MapSign(x, y, kind, ScriptAddress: 0));
        }

        return signs;
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
    /// <summary>The fights an object's script can start, with the same guard as a shop.</summary>
    private static List<WildFight> ReadFights(BinaryReader reader)
    {
        int count = reader.ReadInt32();

        if (count is < 0 or > 16)
            throw new InvalidDataException($"A person claiming {count} scripted battles.");

        var fights = new List<WildFight>(count);

        for (int i = 0; i < count; i++) fights.Add(new WildFight(reader.ReadInt32(), reader.ReadInt32()));

        return fights;
    }

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
                WinsItemId = reader.ReadInt32(),
                WinsCount = reader.ReadInt32(),
                CanGive = ReadStock(reader, mapId),
                CanFight = ReadFights(reader),
                TakesItemId = reader.ReadInt32(),
                TakesCount = reader.ReadInt32(),
                Talks = reader.ReadBoolean(),
                ShiftedBy = reader.ReadInt32(),
                GivesSpecies = reader.ReadInt32(),
                GivesLevel = reader.ReadInt32(),
                HiddenBy = reader.ReadInt32(),
                MindsCreatures = reader.ReadBoolean(),
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
