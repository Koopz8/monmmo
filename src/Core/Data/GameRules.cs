using System.Text;
using PokeMmo.Core.Battle;

namespace PokeMmo.Core.Data;

/// <summary>
/// The numbers that decide a battle, in a file the server can read.
/// <para>
/// This exists for the same reason the world file does, and draws the same line. The
/// server has to be authoritative about combat, and it cannot be authoritative about
/// something it has no numbers for — but it must never read a cartridge. So an
/// operator generates this from their own image and the server loads it.
/// </para>
/// <para>
/// <b>It carries no names.</b> Not for species, not for moves. Names are text from the
/// cartridge; base stats and type charts are arithmetic. The server resolves a battle
/// in indices and sends indices, and the client turns them back into words using the
/// image on the player's own machine. That is the whole reason a save, a world file
/// and this are all lists of numbers.
/// </para>
/// </summary>
public sealed class GameRules
{
    private static readonly byte[] Magic = "MONRULES"u8.ToArray();

    private const int Version = 12;

    private readonly Dictionary<int, SpeciesData> _species;
    private readonly Dictionary<int, MoveData> _moves;
    private readonly Dictionary<int, Learnset> _learnsets;
    private readonly Dictionary<int, TrainerParty> _trainers;
    private readonly Dictionary<int, ItemData> _items;
    private readonly Dictionary<int, List<Evolution>> _evolutions;
    private readonly List<ulong> _machineSets;
    private readonly Dictionary<int, int> _machineAt;

    public GameRules(
        IEnumerable<SpeciesData> species,
        IEnumerable<MoveData> moves,
        IEnumerable<Learnset> learnsets,
        IEnumerable<TrainerParty>? trainers = null,
        IEnumerable<ItemData>? items = null,
        IEnumerable<Evolution>? evolutions = null,
        IEnumerable<ulong>? machineSets = null)
    {
        _species = species.ToDictionary(s => s.Index);
        _moves = moves.ToDictionary(m => m.Id);
        _learnsets = learnsets.ToDictionary(l => l.Species);
        _trainers = (trainers ?? []).ToDictionary(t => t.Id);
        _items = (items ?? []).ToDictionary(i => i.Id);

        _evolutions = (evolutions ?? [])
            .GroupBy(e => e.Species)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Worked out here, while the names are still in memory, and written to the file
        // as a number. The same arrangement the ball kinds already use, and for the same
        // reason: this file carries no text, so anything that has to be read off the
        // cartridge's own words has to be read on the machine that has the cartridge.
        SurfMove = MoveNamed("SURF")?.Id ?? 0;
        StruggleMove = MoveNamed("STRUGGLE")?.Id ?? 0;

        _machineSets = [.. machineSets ?? []];

        // Which bit belongs to which machine, matched by position — the machines in the
        // pocket in id order against the bits in a species' word in order. The same
        // matching the exporter used to tell each machine what it teaches, done here
        // rather than written to the file, so the two cannot drift apart.
        _machineAt = _items.Values
            .Where(i => i.Pocket == Pocket.Machines)
            .OrderBy(i => i.Id)
            .Select((item, index) => (item.Id, index))
            .ToDictionary(m => m.Id, m => m.index);

        // Worked out rather than stored, from a field the file already carries. Nothing
        // new is written down: the answer was always in the item records and nobody had
        // asked them the right question.
        _holdingPockets = [.. _items.Values.Where(i => i.HoldEffect != 0).Select(i => i.Pocket).Distinct()];
    }

    private readonly HashSet<Pocket> _holdingPockets;

    /// <summary>
    /// The move that gets somebody onto the water, as an id.
    /// <para>
    /// Zero when this cartridge has no move by that name, and zero means no surfing —
    /// not a guess at which move it might have been. A number written here from memory
    /// of another game is the mistake this project keeps a standing rule against.
    /// </para>
    /// </summary>
    public int SurfMove { get; init; }

    /// <summary>
    /// The move a creature is left with when everything it knows is spent.
    /// <para>
    /// A move in the cartridge's own table like any other, with its own power, type and
    /// recoil — so this is a number to find rather than a rule to invent. Located at
    /// export off the name, exactly as <see cref="SurfMove"/> is, because that is the last
    /// moment anything knows what a move is called.
    /// </para>
    /// <para>
    /// A rules file whose cartridge has no move by that name has nought here, and a server
    /// reading one simply lets a spent creature do nothing — which is worse than
    /// struggling and better than inventing a move.
    /// </para>
    /// </summary>
    public int StruggleMove { get; init; }

    /// <summary>
    /// Which method number means "on reaching a level".
    /// <para>
    /// Derived at export from the cartridge's own table and written here as a number,
    /// for the same reason the ball kinds and SURF are: the server never sees a name and
    /// never sees a cartridge, and a constant written here from memory of another game
    /// is the mistake this project keeps a standing rule against. Zero means this image
    /// had no such method, and nothing evolves.
    /// </para>
    /// </summary>
    public int EvolveByLevel { get; init; }

    /// <summary>
    /// Which method number means "somebody used this item on it", or zero.
    /// <para>
    /// The only kind of evolution a player brings about on purpose, and so the only one
    /// the bag has anything to do with. Derived at export, like the level method, and
    /// zero on an image where the reading did not come out.
    /// </para>
    /// </summary>
    public int EvolveByItem { get; init; }

    public int EvolutionCount => _evolutions.Sum(e => e.Value.Count);

    /// <summary>
    /// How many a box holds, or zero for a cartridge that never said.
    /// <para>
    /// Read at export out of the game's own sentence — "Each BOX can hold up to 30
    /// POKéMON" — for the same reason the ball kinds and SURF are read at export: the
    /// server never sees a cartridge and never sees a word, and a number written here
    /// from memory of another game is the mistake this project keeps a standing rule
    /// against. Zero means there is nowhere to put a seventh, and a seventh is refused
    /// rather than lost.
    /// </para>
    /// </summary>
    public int BoxSize { get; init; }

    /// <summary>
    /// What using this item on this species would turn it into, if anything.
    /// <para>
    /// Asked by the server when somebody uses something out of the bag, and answered
    /// from the same table the level one comes from. Nothing here knows the item is a
    /// stone or what a stone is; it knows the number matched.
    /// </para>
    /// </summary>
    public Evolution? EvolutionWith(int species, int itemId) =>
        EvolveByItem == 0 || itemId == 0
            ? null
            : EvolutionsOf(species).FirstOrDefault(e => e.Method == EvolveByItem && e.Parameter == itemId);

    /// <summary>True when using this item on something could ever turn it into something.</summary>
    public bool IsEvolutionStone(int itemId) =>
        EvolveByItem != 0 &&
        itemId != 0 &&
        _evolutions.Values.Any(list => list.Any(e => e.Method == EvolveByItem && e.Parameter == itemId));

    /// <summary>Everything this species can turn into, by any means.</summary>
    public IReadOnlyList<Evolution> EvolutionsOf(int species) =>
        _evolutions.GetValueOrDefault(species) ?? (IReadOnlyList<Evolution>)[];

    /// <summary>
    /// What this species becomes on reaching a level, if anything.
    /// <para>
    /// The level is a threshold rather than a match: a member that crossed two levels in
    /// one victory, or that was handed over above its own evolution level, still evolves.
    /// The alternative is a creature that missed its moment and can never have it back.
    /// </para>
    /// </summary>
    public Evolution? EvolutionAt(int species, int level) =>
        EvolveByLevel == 0
            ? null
            : EvolutionsOf(species)
                .Where(e => e.Method == EvolveByLevel && e.Parameter <= level)
                .OrderByDescending(e => e.Parameter)
                .FirstOrDefault();

    /// <summary>How many species this file has a machine word for, which may be none.</summary>
    public int MachineSetCount => _machineSets.Count;

    /// <summary>
    /// Whether this is a thing somebody can be given to carry.
    /// <para>
    /// No field says so, and the obvious reading — "anything with a hold effect" — is
    /// wrong in a way that would be hard to notice: most of what a player can hand over
    /// has no hold effect at all. A Potion held does nothing and is still held.
    /// </para>
    /// <para>
    /// The pocket is what says it. Across three hundred and eight items on this
    /// cartridge the hold effect field is non-zero in exactly two pockets — ordinary
    /// items and berries — and never once among the balls, the machines or the key
    /// items. That is the cartridge saying which pockets holding is <em>for</em>, and it
    /// is a stronger statement than any single item's record makes: forty-eight of a
    /// hundred and forty items use the field and none of the twelve balls do.
    /// </para>
    /// <para>
    /// Key items are refused on top of that, because they are refused everywhere else
    /// too — a thing the player is never allowed to lose is not a thing to hand to
    /// something that can be stolen from.
    /// </para>
    /// </summary>
    public bool CanBeHeld(int itemId) =>
        ItemAt(itemId) is { } item && !item.IsKeyItem && _holdingPockets.Contains(item.Pocket);

    /// <summary>Which pockets this cartridge ever puts a hold effect in.</summary>
    public IReadOnlyCollection<Pocket> HoldingPockets => _holdingPockets;

    /// <summary>
    /// Whether this machine may be used on this species.
    /// <para>
    /// True when there is nothing to go on. A file written before this table was located
    /// carries no words, and on such a file every machine works on everything — which is
    /// what this project did for its whole life until now, and is a better failure than
    /// refusing every machine on the whole party because a table was not found.
    /// </para>
    /// <para>
    /// The item is asked about rather than the move, because a machine is a thing in the
    /// bag with a position in the pocket and the word's bits are in that same order. Two
    /// machines teaching one move would be two bits, and asking by move could not tell
    /// them apart.
    /// </para>
    /// </summary>
    public bool CanBeTaught(int species, int itemId)
    {
        if (_machineSets.Count == 0) return true;
        if (!_machineAt.TryGetValue(itemId, out int machine)) return false;
        if (species < 0 || species >= _machineSets.Count) return false;

        return (_machineSets[species] & (1UL << machine)) != 0;
    }

    /// <summary>
    /// Everything in the bag's machine pocket this species could be taught by, in pocket
    /// order. Used by the interface to say why a machine is greyed out before a player
    /// spends a walk to the counter finding out.
    /// </summary>
    public IReadOnlyList<int> MachinesFor(int species) =>
    [
        .. _machineAt
            .Where(m => CanBeTaught(species, m.Key))
            .OrderBy(m => m.Value)
            .Select(m => m.Key)
    ];

    public int SpeciesCount => _species.Count;

    public int MoveCount => _moves.Count;

    public int LearnsetCount => _learnsets.Count;

    public int TrainerCount => _trainers.Count;

    public int ItemCount => _items.Count;

    public SpeciesData? SpeciesAt(int index) => _species.GetValueOrDefault(index);

    public MoveData? MoveAt(int id) => _moves.GetValueOrDefault(id);

    /// <summary>
    /// A move looked up by the name the cartridge gives it.
    /// <para>
    /// Used for the field moves, and it is a derivation rather than a memory: the id is
    /// read off this image's own move table by matching this image's own text. Writing
    /// 0x39 here because SURF is move 57 in some other game would be exactly the mistake
    /// this project keeps a rule against — and a name that is not found returns nothing,
    /// so the feature is simply unavailable rather than pointed at the wrong move.
    /// </para>
    /// </summary>
    public MoveData? MoveNamed(string name) =>
        _moves.Values.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

    public Learnset? LearnsetOf(int species) => _learnsets.GetValueOrDefault(species);

    public TrainerParty? TrainerAt(int id) => _trainers.GetValueOrDefault(id);

    public ItemData? ItemAt(int id) => _items.GetValueOrDefault(id);

    /// <summary>
    /// The moves a wild creature of this species and level would know — the last four
    /// it would have learned. Empty when the learnset is missing.
    /// </summary>
    public List<MoveData> MovesKnownAt(int species, int level)
    {
        if (LearnsetOf(species) is not { } learnset) return [];

        return learnset.MovesKnownAt(level)
            .Select(MoveAt)
            .Where(move => move is not null)
            .Select(move => move!)
            .ToList();
    }

    public void Save(Stream output)
    {
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);

        writer.Write(Magic);
        writer.Write(Version);

        writer.Write(_species.Count);

        foreach (SpeciesData species in _species.Values)
        {
            writer.Write(species.Index);
            writer.Write(species.BaseHp);
            writer.Write(species.BaseAttack);
            writer.Write(species.BaseDefense);
            writer.Write(species.BaseSpeed);
            writer.Write(species.BaseSpAttack);
            writer.Write(species.BaseSpDefense);
            writer.Write((int)species.Type1);
            writer.Write((int)species.Type2);
            writer.Write(species.CatchRate);
            writer.Write(species.ExpYield);
            writer.Write(species.GenderRatio);
            writer.Write((int)species.GrowthRate);
        }

        writer.Write(_moves.Count);

        foreach (MoveData move in _moves.Values)
        {
            writer.Write(move.Id);
            writer.Write(move.Effect);
            writer.Write(move.Power);
            writer.Write((int)move.Type);
            writer.Write(move.Accuracy);
            writer.Write(move.Pp);
            writer.Write(move.SecondaryChance);
            writer.Write(move.Target);
            writer.Write(move.Priority);
        }

        writer.Write(_learnsets.Count);

        foreach (Learnset learnset in _learnsets.Values)
        {
            writer.Write(learnset.Species);
            writer.Write(learnset.Moves.Count);

            foreach (LevelUpMove entry in learnset.Moves)
            {
                writer.Write(entry.Level);
                writer.Write(entry.MoveId);
            }
        }

        writer.Write(_trainers.Count);

        foreach (TrainerParty trainer in _trainers.Values)
        {
            writer.Write(trainer.Id);
            writer.Write(trainer.IsDouble);
            writer.Write(trainer.Members.Count);

            foreach (TrainerMember member in trainer.Members)
            {
                writer.Write(member.Species);
                writer.Write(member.Level);
                writer.Write(member.HeldItem);
                writer.Write(member.Moves.Count);

                foreach (int move in member.Moves) writer.Write(move);
            }
        }

        writer.Write(_items.Count);

        foreach (ItemData item in _items.Values)
        {
            writer.Write(item.Id);
            writer.Write(item.Price);
            writer.Write((int)item.Pocket);
            writer.Write(item.HoldEffect);
            writer.Write(item.HoldEffectParam);
            writer.Write(item.Importance);
            writer.Write(item.BattleUsage);
            writer.Write(item.SecondaryId);

            // Which ball this is, worked out from its name at export time and written
            // as a number. Minus one for "not a ball", so the field is always present.
            writer.Write(item.Ball is { } ball ? (int)ball : -1);

            // What this teaches, which is in no field of the item's own record on the
            // cartridge — it comes from a separate list matched to the machines by
            // position. Zero for everything that teaches nothing, which is most of it.
            writer.Write(item.Teaches);

            // And what it clears, which is in no field of that record either. Written as
            // this project's own set rather than the cartridge's byte, because the bits
            // were given their meanings at export by the names of the items that claim
            // them, and the server has never seen a name.
            writer.Write((int)item.Cures);
        }

        // Derived from this cartridge's own move names at export, for the same reason
        // the ball kinds are: the server never sees a name.
        writer.Write(SurfMove);
        writer.Write(StruggleMove);

        // And out of a sentence, which is stranger and the same principle.
        writer.Write(BoxSize);

        writer.Write(EvolveByLevel);
        writer.Write(EvolveByItem);
        writer.Write(EvolutionCount);

        foreach (Evolution evolution in _evolutions.Values.SelectMany(e => e))
        {
            writer.Write(evolution.Species);
            writer.Write(evolution.Method);
            writer.Write(evolution.Parameter);
            writer.Write(evolution.Into);
        }

        // One word per species, in species order, including the empty ones. A file that
        // only listed the species that can learn something would have no way to say the
        // difference between "this one learns nothing" and "this file predates the
        // table" — and those two have to mean opposite things.
        writer.Write(_machineSets.Count);

        foreach (ulong word in _machineSets) writer.Write(word);
    }

    public void Save(string path)
    {
        using FileStream file = File.Create(path);
        Save(file);
    }

    public static GameRules Load(Stream input)
    {
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);

        if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Not a rules file.");

        int version = reader.ReadInt32();

        if (version != Version)
            throw new InvalidDataException($"Rules file is version {version}, expected {Version}.");

        var species = new List<SpeciesData>();

        foreach (int _ in Counted(reader, "species", 4096))
        {
            species.Add(new SpeciesData
            {
                Index = reader.ReadInt32(),
                BaseHp = reader.ReadByte(),
                BaseAttack = reader.ReadByte(),
                BaseDefense = reader.ReadByte(),
                BaseSpeed = reader.ReadByte(),
                BaseSpAttack = reader.ReadByte(),
                BaseSpDefense = reader.ReadByte(),
                Type1 = (PokemonType)reader.ReadInt32(),
                Type2 = (PokemonType)reader.ReadInt32(),
                CatchRate = reader.ReadByte(),
                ExpYield = reader.ReadByte(),
                GenderRatio = reader.ReadByte(),
                GrowthRate = (GrowthRate)reader.ReadInt32(),
            });
        }

        var moves = new List<MoveData>();

        foreach (int _ in Counted(reader, "moves", 4096))
        {
            moves.Add(new MoveData(
                reader.ReadInt32(),
                // Deliberately blank. The server has no business holding cartridge text,
                // and the client names these from the player's own image.
                string.Empty,
                reader.ReadByte(),
                reader.ReadByte(),
                (PokemonType)reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadSByte()));
        }

        var learnsets = new List<Learnset>();

        foreach (int _ in Counted(reader, "learnsets", 4096))
        {
            int index = reader.ReadInt32();
            int entryCount = reader.ReadInt32();

            if (entryCount is < 0 or > 256)
                throw new InvalidDataException($"Learnset for species {index} claims {entryCount} moves.");

            var entries = new List<LevelUpMove>(entryCount);

            for (int i = 0; i < entryCount; i++)
                entries.Add(new LevelUpMove(reader.ReadInt32(), reader.ReadInt32()));

            learnsets.Add(new Learnset(index, entries));
        }

        var trainers = new List<TrainerParty>();

        foreach (int _ in Counted(reader, "trainers", 8192))
        {
            int id = reader.ReadInt32();
            bool isDouble = reader.ReadBoolean();
            int memberCount = reader.ReadInt32();

            if (memberCount is < 0 or > 6)
                throw new InvalidDataException($"Trainer {id} claims a party of {memberCount}.");

            var members = new List<TrainerMember>(memberCount);

            for (int i = 0; i < memberCount; i++)
            {
                int memberSpecies = reader.ReadInt32();
                int level = reader.ReadInt32();
                int held = reader.ReadInt32();
                int moveCount = reader.ReadInt32();

                if (moveCount is < 0 or > 4)
                    throw new InvalidDataException($"Trainer {id} member {i} claims {moveCount} moves.");

                var moveIds = new List<int>(moveCount);
                for (int m = 0; m < moveCount; m++) moveIds.Add(reader.ReadInt32());

                members.Add(new TrainerMember(memberSpecies, level, held, moveIds));
            }

            trainers.Add(new TrainerParty(id, isDouble, members));
        }

        var items = new List<ItemData>();

        foreach (int _ in Counted(reader, "items", 4096))
        {
            items.Add(new ItemData(
                reader.ReadInt32(),
                reader.ReadInt32(),
                (Pocket)reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32() is var ball && ball >= 0 ? (BallKind)ball : null)
            {
                Teaches = reader.ReadInt32(),
                Cures = (Ailments)reader.ReadInt32(),
            });
        }

        int surf = reader.ReadInt32();
        int struggle = reader.ReadInt32();
        int boxSize = reader.ReadInt32();
        int byLevel = reader.ReadInt32();
        int byItem = reader.ReadInt32();

        var evolutions = new List<Evolution>();

        foreach (int _ in Counted(reader, "evolutions", 8192))
        {
            evolutions.Add(new Evolution(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32()));
        }

        var machineSets = new List<ulong>();

        foreach (int _ in Counted(reader, "machine sets", 4096)) machineSets.Add(reader.ReadUInt64());

        return new GameRules(species, moves, learnsets, trainers, items, evolutions, machineSets)
        {
            SurfMove = surf,
            StruggleMove = struggle,
            BoxSize = boxSize,
            EvolveByLevel = byLevel,
            EvolveByItem = byItem,
        };
    }

    /// <summary>
    /// Reads a length and refuses one that could only come from a corrupted file, so a
    /// bad byte fails loudly rather than by allocating for an hour.
    /// </summary>
    private static IEnumerable<int> Counted(BinaryReader reader, string what, int limit)
    {
        int count = reader.ReadInt32();

        if (count < 0 || count > limit)
            throw new InvalidDataException($"Rules file claims {count} {what}.");

        for (int i = 0; i < count; i++) yield return i;
    }

    public static GameRules Load(string path)
    {
        using FileStream file = File.OpenRead(path);
        return Load(file);
    }
}
