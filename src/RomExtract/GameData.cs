using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.RomExtract.Items;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.RomExtract.Graphics;

namespace PokeMmo.RomExtract;

/// <summary>
/// Everything the client needs out of the player's cartridge, read once.
/// <para>
/// The cartridge is opened a single time and kept, rather than re-read whenever a
/// sprite or a move is wanted. Locating the tables involves scanning sixteen megabytes
/// several times over, which is fine at startup and not fine when a wild encounter
/// begins.
/// </para>
/// </summary>
public sealed class GameData
{
    private readonly Dictionary<(int Species, bool Back, bool Shiny), ExtractedSprite> _sprites = [];

    private GameData(
        Rom rom,
        RomExtractor extractor,
        List<SpeciesData> species,
        List<MoveData> moves,
        Dictionary<int, Learnset> learnsets,
        List<int> machineItems,
        MachineSets? machines)
    {
        Rom = rom;
        Extractor = extractor;
        Species = species;
        Moves = moves;
        Learnsets = learnsets;
        MachineItems = machineItems;
        Machines = machines;

        _machineAt = machineItems
            .Select((id, index) => (id, index))
            .ToDictionary(m => m.id, m => m.index);
    }

    private readonly Dictionary<int, int> _machineAt;

    public Rom Rom { get; }

    public RomExtractor Extractor { get; }

    public IReadOnlyList<SpeciesData> Species { get; }

    public IReadOnlyList<MoveData> Moves { get; }

    public IReadOnlyDictionary<int, Learnset> Learnsets { get; }

    /// <summary>The machine items in pocket order, which is the order of the bits.</summary>
    public IReadOnlyList<int> MachineItems { get; }

    /// <summary>Who each machine may be used on, or nothing if the table was not found.</summary>
    public MachineSets? Machines { get; }

    /// <summary>
    /// Whether this machine would work on this species — the client's half of a rule the
    /// server also enforces.
    /// <para>
    /// Both sides read it off a cartridge rather than one being told by the other, and
    /// both sides find it the same way, down to using the obstacle scripts to name the
    /// three moves that anchor the machine list. That costs the client about a second at
    /// startup and buys the thing worth having: if the two ever disagree it is because
    /// the images differ, not because the code does.
    /// </para>
    /// <para>
    /// True when nothing was found, which matches the server: a client that greyed out
    /// the whole party because a table was missing would be worse than one that lets a
    /// player try and be told no.
    /// </para>
    /// </summary>
    public bool CanBeTaught(int species, int itemId)
    {
        if (Machines is not { } sets) return true;

        return _machineAt.TryGetValue(itemId, out int machine) && sets.Allows(species, machine);
    }

    /// <summary>Whether this item is one of the machines at all.</summary>
    public bool IsMachine(int itemId) => _machineAt.ContainsKey(itemId);

    /// <summary>
    /// The names this cartridge offers when it asks somebody to name a character.
    /// <para>
    /// Read at load because locating them walks the whole image, and because the first
    /// line the rival says is in the first minute of the game.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> SuggestedNames { get; private init; } = [];

    public static GameData Load(string romPath, Action<string>? log = null)
    {
        Rom rom = Rom.Load(romPath);
        RomExtractor extractor = RomExtractor.Open(rom, log);

        List<SpeciesData> species = extractor.ExtractSpecies();

        // Moves are optional: without them a battle simply has nothing to choose from,
        // which is better than refusing to start the game at all.
        List<MoveData> moves;

        try
        {
            moves = MoveExtractor.Extract(rom, log);
        }
        catch (InvalidDataException)
        {
            moves = [];
        }

        Dictionary<int, Learnset> learnsets = LearnsetExtractor.Extract(rom, log);

        (List<int> machineItems, MachineSets? machines) = FindMachines(rom, species.Count, moves, learnsets, log);

        return new GameData(rom, extractor, species, moves, learnsets, machineItems, machines)
        {
            SuggestedNames = NameSuggestions.Locate(rom, log),
        };
    }

    /// <summary>
    /// The machines and who they work on, or empty when any part of the chain is missing.
    /// <para>
    /// Three tables have to come out for this to mean anything — the items, the list of
    /// what each machine teaches, and the compatibility words — and each of them is
    /// allowed to fail without taking the game down with it. A client that cannot find
    /// them simply offers every machine to everybody and lets the server say no.
    /// </para>
    /// </summary>
    private static (List<int> Items, MachineSets? Sets) FindMachines(
        Rom rom,
        int speciesCount,
        List<MoveData> moves,
        Dictionary<int, Learnset> learnsets,
        Action<string>? log)
    {
        if (ItemTable.Locate(rom) is not { } table) return ([], null);

        List<int> machineItems =
        [
            .. ItemTable.Read(rom, table)
                .Select(i => i.ToData())
                .Where(i => i.Pocket == Pocket.Machines)
                .OrderBy(i => i.Id)
                .Select(i => i.Id)
        ];

        if (machineItems.Count != MachineMoves.Count) return ([], null);

        if (MachineMoves.Locate(rom, moves.Count, ObstacleMoves.Find(rom)) is not { } at) return (machineItems, null);

        return (machineItems, MachineCompatibility.Locate(
            rom, speciesCount, MachineMoves.Read(rom, at), learnsets, log));
    }

    public SpeciesData? SpeciesAt(int index) =>
        index >= 0 && index < Species.Count ? Species[index] : null;

    public MoveData? MoveAt(int index) =>
        index >= 0 && index < Moves.Count ? Moves[index] : null;

    /// <summary>
    /// The moves a wild creature of this species and level would know — the last four
    /// it would have learned. Empty when learnsets could not be read.
    /// </summary>
    public List<MoveData> MovesKnownAt(int species, int level)
    {
        if (!Learnsets.TryGetValue(species, out Learnset? learnset)) return [];

        return learnset.MovesKnownAt(level)
            .Select(MoveAt)
            .Where(move => move is not null)
            .Select(move => move!)
            .ToList();
    }

    /// <summary>
    /// Finds a move by name, for building a party without hardcoding indices — and now
    /// for the field moves as well.
    /// <para>
    /// This is the client's half of a derivation the server makes off its rules file.
    /// Both sides have to arrive at the same id or one will offer a swim the other
    /// refuses, and the only way to be sure of that is for both to read it off the same
    /// image rather than either one remembering a number.
    /// </para>
    /// </summary>
    public MoveData? MoveNamed(string name) =>
        Moves.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A sprite, decoded once and kept. Decoding involves decompressing and detiling,
    /// which is not something to repeat every frame of a battle.
    /// </summary>
    public ExtractedSprite? Sprite(int species, bool back = false, bool shiny = false)
    {
        var key = (species, back, shiny);
        if (_sprites.TryGetValue(key, out ExtractedSprite? cached)) return cached;

        try
        {
            ExtractedSprite sprite = Extractor.ExtractSprite(species, shiny, back);
            _sprites[key] = sprite;
            return sprite;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException or InvalidDataException)
        {
            return null;
        }
    }
}
