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
        List<ItemData> items,
        List<int> machineItems,
        MachineSets? machines)
    {
        Items = items;
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

        _itemAt = items.ToDictionary(i => i.Id);

        // The same reading the rules file makes, made here off the same image rather
        // than accepted from the other side. Non-zero hold effects appear in exactly the
        // pockets holding is for, and in no others.
        _holdingPockets = [.. items.Where(i => i.HoldEffect != 0).Select(i => i.Pocket).Distinct()];
    }

    private readonly Dictionary<int, int> _machineAt;
    private readonly Dictionary<int, ItemData> _itemAt;
    private readonly HashSet<Pocket> _holdingPockets;

    public Rom Rom { get; }

    public RomExtractor Extractor { get; }

    public IReadOnlyList<SpeciesData> Species { get; }

    public IReadOnlyList<MoveData> Moves { get; }

    public IReadOnlyDictionary<int, Learnset> Learnsets { get; }

    /// <summary>Every item on this cartridge, with no names on it — those come separately.</summary>
    public IReadOnlyList<ItemData> Items { get; }

    /// <summary>
    /// Whether this is something a party member could be handed to carry.
    /// <para>
    /// The client's half of the rule the server enforces, worked out the same way from
    /// the same field: the pockets whose items ever carry a hold effect. What it buys is
    /// a bag that does not offer to hand over a bicycle.
    /// </para>
    /// </summary>
    public bool CanBeHeld(int itemId) =>
        _itemAt.TryGetValue(itemId, out ItemData? item)
        && !item.IsKeyItem
        && _holdingPockets.Contains(item.Pocket);

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
    /// How many species in the world this machine works on.
    /// <para>
    /// The inverse question, and the one a player is actually asking when every name in
    /// their party has gone grey. "Can't learn it" six times over reads as a broken game;
    /// "a hundred and thirteen species can learn this, and none of yours is one" reads as
    /// a game telling you to go and find one.
    /// </para>
    /// <para>
    /// HM01 is what asked it. Six party members refused it, which looked exactly like the
    /// compatibility table being misread — and the table was right: BULBASAUR, CHARMANDER
    /// and RATTATA can all learn CUT and all three were sitting in the box.
    /// </para>
    /// </summary>
    public int SpeciesThatCanLearn(int itemId)
    {
        if (Machines is not { } sets) return 0;
        if (!_machineAt.TryGetValue(itemId, out int machine)) return 0;

        return Enumerable.Range(0, sets.Masks.Count).Count(species => sets.Allows(species, machine));
    }

    /// <summary>
    /// The names this cartridge offers when it asks somebody to name a character.
    /// <para>
    /// Read at load because locating them walks the whole image, and because the first
    /// line the rival says is in the first minute of the game.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> SuggestedNames { get; private init; } = [];

    /// <summary>
    /// What the abilities are called, index nought first.
    /// <para>
    /// On the client because the client owns the cartridge. The server is told which
    /// ability a creature has as a number and never learns what it is called, which is the
    /// same arrangement every other name in this project has.
    /// </para>
    /// <para>
    /// Empty when the table was not found, and the screens fall back to the number. A
    /// missing name is a cosmetic loss; refusing to start the game over one would not be.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Abilities { get; private init; } = [];

    /// <summary>What ability <paramref name="index"/> is called, or a number when unknown.</summary>
    public string AbilityNamed(int index) =>
        index >= 0 && index < Abilities.Count && Abilities[index].Length > 0
            ? Abilities[index]
            : $"ABILITY {index}";

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

        List<ItemData> items = ItemTable.Locate(rom) is { } at
            ? [.. ItemTable.Read(rom, at).Select(i => i.ToData())]
            : [];

        (List<int> machineItems, MachineSets? machines) =
            FindMachines(rom, species.Count, moves, learnsets, items, log);

        return new GameData(rom, extractor, species, moves, learnsets, items, machineItems, machines)
        {
            SuggestedNames = NameSuggestions.Locate(rom, log),
            Abilities = AbilityNames.Extract(rom, log),
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
        List<ItemData> items,
        Action<string>? log)
    {
        List<int> machineItems =
        [
            .. items
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
