using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
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

    private GameData(Rom rom, RomExtractor extractor, List<SpeciesData> species, List<MoveData> moves)
    {
        Rom = rom;
        Extractor = extractor;
        Species = species;
        Moves = moves;
    }

    public Rom Rom { get; }

    public RomExtractor Extractor { get; }

    public IReadOnlyList<SpeciesData> Species { get; }

    public IReadOnlyList<MoveData> Moves { get; }

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

        return new GameData(rom, extractor, species, moves);
    }

    public SpeciesData? SpeciesAt(int index) =>
        index >= 0 && index < Species.Count ? Species[index] : null;

    public MoveData? MoveAt(int index) =>
        index >= 0 && index < Moves.Count ? Moves[index] : null;

    /// <summary>Finds a move by name, for building a party without hardcoding indices.</summary>
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
