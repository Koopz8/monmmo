using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract;

/// <summary>
/// Produces the rules file the server resolves battles against.
/// <para>
/// The second bridge between a cartridge and the server, built exactly like the first.
/// An operator runs this against their own image; the server then knows base stats,
/// catch rates and move power, and nothing else. No names, no sprites, no text, and no
/// extractor in the server's dependency graph.
/// </para>
/// </summary>
public static class RulesExporter
{
    public static GameRules Export(Rom rom, Action<string>? log = null)
    {
        RomExtractor extractor = RomExtractor.Open(rom, log);

        List<SpeciesData> species = extractor.ExtractSpecies();

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

        // Names are stripped here rather than simply not written, so that anything
        // holding one of these records after export is holding the same thing the
        // server will.
        List<SpeciesData> anonymousSpecies = species
            .Select(s => Anonymise(s))
            .ToList();

        List<MoveData> anonymousMoves = moves
            .Select(m => m with { Name = string.Empty })
            .ToList();

        var rules = new GameRules(anonymousSpecies, anonymousMoves, learnsets.Values);

        log?.Invoke(
            $"  rules: {rules.SpeciesCount} species, {rules.MoveCount} moves, " +
            $"{rules.LearnsetCount} learnsets (no names)");

        ReportUsableness(rules, log);

        return rules;
    }

    private static SpeciesData Anonymise(SpeciesData species) => new()
    {
        Index = species.Index,
        Name = string.Empty,
        BaseHp = species.BaseHp,
        BaseAttack = species.BaseAttack,
        BaseDefense = species.BaseDefense,
        BaseSpeed = species.BaseSpeed,
        BaseSpAttack = species.BaseSpAttack,
        BaseSpDefense = species.BaseSpDefense,
        Type1 = species.Type1,
        Type2 = species.Type2,
        CatchRate = species.CatchRate,
        ExpYield = species.ExpYield,
        GenderRatio = species.GenderRatio,
        GrowthRate = species.GrowthRate,
    };

    /// <summary>
    /// Says whether these rules can actually decide a battle.
    /// <para>
    /// A rules file with every catch rate at zero, or no moves at all, loads perfectly
    /// well and then makes every throw fail and every attack do nothing — which looks
    /// like a bug in the battle engine rather than a bad export.
    /// </para>
    /// </summary>
    private static void ReportUsableness(GameRules rules, Action<string>? log)
    {
        if (log is null) return;

        if (rules.MoveCount == 0)
        {
            log("  rules: no moves were read — battles will have nothing to choose from");
            return;
        }

        int catchable = Enumerable
            .Range(0, 512)
            .Select(rules.SpeciesAt)
            .Count(s => s is { CatchRate: > 0 });

        int damaging = Enumerable
            .Range(0, 512)
            .Select(rules.MoveAt)
            .Count(m => m is { Power: > 0 });

        log($"  rules: {catchable} species have a catch rate, {damaging} moves do damage");
    }
}
