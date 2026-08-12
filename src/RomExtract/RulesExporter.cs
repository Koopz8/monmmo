using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.RomExtract.Items;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.RomExtract.Trainers;

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

        List<TrainerParty> trainers = ExtractTrainers(rom, species.Count, log);

        List<ItemData> items = ExtractItems(rom, moves.Count, log);

        var rules = new GameRules(anonymousSpecies, anonymousMoves, learnsets.Values, trainers, items);

        log?.Invoke(
            $"  rules: {rules.SpeciesCount} species, {rules.MoveCount} moves, " +
            $"{rules.LearnsetCount} learnsets, {rules.TrainerCount} trainers, " +
            $"{rules.ItemCount} items (no names)");

        ReportUsableness(rules, log);

        return rules;
    }

    /// <summary>
    /// Reads the trainer table, and drops every name on the way out.
    /// <para>
    /// A cartridge with no findable trainer table is not a failure worth refusing an
    /// export over — everything else in the file still works, and the world simply has
    /// nobody in it who wants a fight.
    /// </para>
    /// </summary>
    private static List<TrainerParty> ExtractTrainers(Rom rom, int speciesCount, Action<string>? log)
    {
        if (TrainerTable.Locate(rom, speciesCount, log) is not { } table)
        {
            log?.Invoke("  trainers: no table found — nobody will challenge anybody");
            return [];
        }

        List<TrainerRecord> records = TrainerTable.Read(rom, table, speciesCount);

        // The names came off the cartridge and stop here. Same rule as species and
        // moves: the server gets an id, and the client that owns an image turns it back
        // into "BUG CATCHER RICK".
        return records
            .Select(r => new TrainerParty(r.Id, r.IsDouble, r.Party.Select(m => m.ToMember()).ToList()))
            .ToList();
    }

    /// <summary>
    /// Reads the item table, and drops every name and description on the way out.
    /// <para>
    /// A cartridge with no findable item table is not worth refusing an export over.
    /// Everything else in the file still works and there is simply nothing to buy.
    /// </para>
    /// </summary>
    private static List<ItemData> ExtractItems(Rom rom, int moveCount, Action<string>? log)
    {
        if (ItemTable.Locate(rom, log) is not { } table)
        {
            log?.Invoke("  items: no table found — there will be nothing to buy or carry");
            return [];
        }

        List<ItemData> items =
            [.. ItemTable.Read(rom, table).Select(i => i.ToData() with { Ball = BallFor(i) })];

        return Teach(rom, items, moveCount, log);
    }

    /// <summary>
    /// Tells each teaching machine what it teaches.
    /// <para>
    /// Matched by position rather than by id arithmetic. The machines are a contiguous
    /// stretch of item ids on this cartridge and the list is fifty-eight entries in the
    /// same order, but "the machines, in id order" is a thing the item table can be
    /// asked for and "289" is a thing that would have to be believed.
    /// </para>
    /// <para>
    /// The counts have to agree. If the pocket holds a different number of machines than
    /// the list holds moves then one of the two was found wrongly, and teaching every
    /// machine the move one along from its own is worse than teaching nothing.
    /// </para>
    /// </summary>
    private static List<ItemData> Teach(Rom rom, List<ItemData> items, int moveCount, Action<string>? log)
    {
        List<int> known = ObstacleMoves.Find(rom, log);

        if (MachineMoves.Locate(rom, moveCount, known, log) is not { } at) return items;

        List<int> moves = MachineMoves.Read(rom, at);

        List<ItemData> machines = [.. items.Where(i => i.Pocket == Pocket.Machines).OrderBy(i => i.Id)];

        if (machines.Count != moves.Count)
        {
            log?.Invoke(
                $"  machines: {machines.Count} in the pocket but {moves.Count} in the list — " +
                "not matching them up, since one of the two is located wrongly");

            return items;
        }

        var taught = machines
            .Select((machine, index) => (machine.Id, Move: moves[index]))
            .ToDictionary(m => m.Id, m => m.Move);

        log?.Invoke(
            $"  machines: {machines.Count} of them teach something, " +
            $"{machines.Count(m => m.IsKeyItem)} of those reusable");

        return [.. items.Select(i => taught.TryGetValue(i.Id, out int move) ? i with { Teaches = move } : i)];
    }

    /// <summary>
    /// Which kind of ball an item is, decided from its name.
    /// <para>
    /// Nothing on the cartridge says this in data. A ball's behaviour lives in the
    /// game's code, not in a field, so the only thing left to read it off is the name —
    /// and this is the one place in the project allowed to do that, because it is the
    /// place where names stop. What crosses into the rules file is a number.
    /// </para>
    /// <para>
    /// Anything else in the ball pocket is treated as an ordinary ball rather than
    /// dropped. There are a dozen of them and this project models three; a Nest Ball
    /// that works like a Poké Ball is wrong in a small way, and a Nest Ball you cannot
    /// throw is wrong in a way somebody would file a bug about.
    /// </para>
    /// </summary>
    private static BallKind? BallFor(ItemRecord item)
    {
        if (item.Pocket != Pocket.Balls) return null;

        string name = item.Name.ToUpperInvariant();

        if (name.StartsWith("MASTER")) return BallKind.Master;
        if (name.StartsWith("ULTRA")) return BallKind.Ultra;
        if (name.StartsWith("GREAT")) return BallKind.Great;

        return BallKind.Poke;
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
