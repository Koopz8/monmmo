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

    private const int Version = 4;

    private readonly Dictionary<int, SpeciesData> _species;
    private readonly Dictionary<int, MoveData> _moves;
    private readonly Dictionary<int, Learnset> _learnsets;
    private readonly Dictionary<int, TrainerParty> _trainers;
    private readonly Dictionary<int, ItemData> _items;

    public GameRules(
        IEnumerable<SpeciesData> species,
        IEnumerable<MoveData> moves,
        IEnumerable<Learnset> learnsets,
        IEnumerable<TrainerParty>? trainers = null,
        IEnumerable<ItemData>? items = null)
    {
        _species = species.ToDictionary(s => s.Index);
        _moves = moves.ToDictionary(m => m.Id);
        _learnsets = learnsets.ToDictionary(l => l.Species);
        _trainers = (trainers ?? []).ToDictionary(t => t.Id);
        _items = (items ?? []).ToDictionary(i => i.Id);
    }

    public int SpeciesCount => _species.Count;

    public int MoveCount => _moves.Count;

    public int LearnsetCount => _learnsets.Count;

    public int TrainerCount => _trainers.Count;

    public int ItemCount => _items.Count;

    public SpeciesData? SpeciesAt(int index) => _species.GetValueOrDefault(index);

    public MoveData? MoveAt(int id) => _moves.GetValueOrDefault(id);

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
        }
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
                reader.ReadInt32() is var ball && ball >= 0 ? (BallKind)ball : null));
        }

        return new GameRules(species, moves, learnsets, trainers, items);
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
