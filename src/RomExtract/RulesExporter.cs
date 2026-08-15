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

        (List<ItemData> items, List<int> machineMoves) = ExtractItems(rom, moves.Count, log);

        // Who each machine is allowed to work on. Located last of everything, because it
        // is found by agreeing with the level-up lists about the moves the machines
        // teach — it needs both of those to have come out before it can be looked for.
        MachineSets? machineSets = machineMoves.Count == 0
            ? null
            : MachineCompatibility.Locate(rom, species.Count, machineMoves, learnsets, log);

        // Read off the names while they are still here, and written to the file as a
        // number. This is the last moment anything knows what the moves are called: the
        // list above has already been stripped, on purpose, and the server never sees a
        // name at all.
        int surf = moves.FirstOrDefault(m => string.Equals(m.Name, "SURF", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;

        // And the one a creature is left with when every move it knows is spent. Located
        // the same way and for the same reason: it is a move in this cartridge's own
        // table, with its own power, type and recoil, so nothing about it has to be
        // invented — only found.
        int struggle = moves
            .FirstOrDefault(m => string.Equals(m.Name, "STRUGGLE", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;

        // What turns into what, which nothing in this game has ever done. Located here
        // rather than in the world export because it is arithmetic about creatures, and
        // this is the file the server decides fights with.
        EvolutionTable? evolutions = EvolutionExtractor.Locate(rom, species, FieldRoutines(rom), log);

        // How many a box holds, out of the sentence the man in the Pokémon Center says.
        int boxSize = BoxCapacity.Locate(rom, log) ?? 0;

        var rules = new GameRules(
            anonymousSpecies, anonymousMoves, learnsets.Values, trainers, items, evolutions?.Evolutions,
            machineSets?.Masks)
        {
            SurfMove = surf,
            StruggleMove = struggle,
            BoxSize = boxSize,
            EvolveByLevel = evolutions?.ByLevel ?? 0,
            EvolveByItem = evolutions?.ByItem ?? 0,
        };

        log?.Invoke(surf > 0
            ? $"  rules: the move called SURF is {surf}"
            : "  rules: no move on this cartridge is called SURF, so this server has no surfing");

        log?.Invoke(struggle > 0
            ? $"  rules: the move called STRUGGLE is {struggle}"
            : "  rules: no move on this cartridge is called STRUGGLE, so nothing here runs out of moves");

        // What wild ones are carrying, which their own records have said all along.
        log?.Invoke(
            $"  rules: {anonymousSpecies.Count(s => s.Item1 != 0 || s.Item2 != 0)} species name " +
            "something a wild one may be carrying " +
            $"({anonymousSpecies.Count(s => s.Item1 != 0)} a common one, " +
            $"{anonymousSpecies.Count(s => s.Item2 != 0)} a rare one)");

        log?.Invoke(
            $"  rules: {rules.SpeciesCount} species, {rules.MoveCount} moves, " +
            $"{rules.LearnsetCount} learnsets, {rules.TrainerCount} trainers, " +
            $"{rules.ItemCount} items, {rules.EvolutionCount} evolutions (no names)");

        // What trainers' parties are carrying, which this file has been writing down
        // since trainers existed and nothing had ever read.
        List<TrainerMember> carrying =
            [.. trainers.SelectMany(t => t.Members).Where(m => m.HeldItem != 0)];

        log?.Invoke(
            carrying.Count == 0
                ? "  rules: nobody's party carries anything"
                : $"  rules: {carrying.Count} of {trainers.Sum(t => t.Members.Count)} party members carry something, " +
                  $"across {carrying.Select(m => m.HeldItem).Distinct().Count()} different items");

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
    /// <summary>
    /// What each item runs when it is used out of a bag, as an address, per item id.
    /// <para>
    /// Never written to the rules file — an address on a cartridge means nothing to a
    /// server that has none. It exists for one question at export time: which evolution
    /// method is the one a player brings about on purpose. The stones are the six items
    /// that share a routine, and nothing else on the image runs it.
    /// </para>
    /// </summary>
    private static Dictionary<int, uint> FieldRoutines(Rom rom)
    {
        if (ItemTable.Locate(rom) is not { } table) return [];

        var routines = new Dictionary<int, uint>();

        foreach (ItemRecord record in ItemTable.Read(rom, table))
            routines[record.Id] = record.FieldUse;

        return routines;
    }

    private static (List<ItemData> Items, List<int> MachineMoves) ExtractItems(
        Rom rom, int moveCount, Action<string>? log)
    {
        if (ItemTable.Locate(rom, log) is not { } table)
        {
            log?.Invoke("  items: no table found — there will be nothing to buy or carry");
            return ([], []);
        }

        List<ItemRecord> records = ItemTable.Read(rom, table);

        List<ItemData> items = [.. records.Select(i => i.ToData() with { Ball = BallFor(i) })];

        // What each one clears, from a second table this project had written down as
        // unknown since potions worked. Read here rather than in the world export
        // because it needs the names, and this is where names stop.
        if (ItemEffects.Locate(rom, records, log) is { } cures)
        {
            items =
            [
                .. items.Select(i =>
                    cures.Cures.TryGetValue(i.Id, out Ailments clears) ? i with { Cures = clears } : i)
            ];
        }

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
    private static (List<ItemData> Items, List<int> MachineMoves) Teach(
        Rom rom, List<ItemData> items, int moveCount, Action<string>? log)
    {
        List<int> known = ObstacleMoves.Find(rom, log);

        if (MachineMoves.Locate(rom, moveCount, known, log) is not { } at) return (items, []);

        List<int> moves = MachineMoves.Read(rom, at);

        List<ItemData> machines = [.. items.Where(i => i.Pocket == Pocket.Machines).OrderBy(i => i.Id)];

        if (machines.Count != moves.Count)
        {
            log?.Invoke(
                $"  machines: {machines.Count} in the pocket but {moves.Count} in the list — " +
                "not matching them up, since one of the two is located wrongly");

            return (items, []);
        }

        var taught = machines
            .Select((machine, index) => (machine.Id, Move: moves[index]))
            .ToDictionary(m => m.Id, m => m.Move);

        log?.Invoke(
            $"  machines: {machines.Count} of them teach something, " +
            $"{machines.Count(m => m.IsKeyItem)} of those reusable");

        return ([.. items.Select(i => taught.TryGetValue(i.Id, out int move) ? i with { Teaches = move } : i)], moves);
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

        // What a wild one of these is carrying. Numbers, like everything else that
        // travels — what the numbers are called stays on the cartridge.
        Item1 = species.Item1,
        Item2 = species.Item2,

        // And what beating one is worth. This copy is written by hand rather than taken
        // wholesale, which is the point of it — nothing reaches the server that was not
        // named here — and the cost is that a field left off this list is a field the
        // server never hears about however well it was extracted. That is what happened:
        // the yields were read, stored, serialised and awarded, and every one of them
        // was nought by the time it left this method. The startup line said so on the
        // first run.
        // What it can breed with and how long its eggs take. Named here or it does not
        // travel, which is the lesson milestone 108 learned the hard way.
        EggGroup1 = species.EggGroup1,
        EggGroup2 = species.EggGroup2,
        EggCycles = species.EggCycles,

        EvHp = species.EvHp,
        EvAttack = species.EvAttack,
        EvDefense = species.EvDefense,
        EvSpeed = species.EvSpeed,
        EvSpAttack = species.EvSpAttack,
        EvSpDefense = species.EvSpDefense,
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
