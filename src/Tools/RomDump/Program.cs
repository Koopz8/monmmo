using System.Text.Json;
using System.Text.Json.Serialization;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Graphics;
using PokeMmo.RomExtract.Maps;
using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Items;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.RomExtract.Trainers;

namespace PokeMmo.Tools.RomDump;

/// <summary>
/// Milestone 0: prove that a player-supplied cartridge can be read.
/// <para>
/// Everything this writes is derived from the player's own file and stays on their
/// machine. Nothing here is redistributable and nothing here is checked into the repo.
/// </para>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            return Run(Options.Parse(args));
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Run(Options options)
    {
        Console.WriteLine($"Reading {Path.GetFileName(options.RomPath)}");

        Rom rom = Rom.Load(options.RomPath);
        Console.WriteLine($"  title      {rom.Title}");
        Console.WriteLine($"  game code  {rom.GameCode} (rev {rom.Version})");
        Console.WriteLine($"  size       {rom.Length / 1024 / 1024} MiB");
        Console.WriteLine($"  sha1       {rom.Sha1}");
        Console.WriteLine();

        Console.WriteLine("Locating tables");
        RomExtractor extractor = RomExtractor.Open(rom, message => Console.WriteLine(message));
        Console.WriteLine();

        Console.WriteLine($"Cartridge: {extractor.Identity.Description}");
        if (!extractor.Identity.Sha1IsKnown)
        {
            Console.WriteLine("  note: this image is not one of the known-good hashes. Extraction will");
            Console.WriteLine("        still be attempted, and the table scan below is the real check.");
        }

        Console.WriteLine();
        Console.WriteLine("Tables");
        foreach (TableLocation table in extractor.Tables.All)
            Console.WriteLine($"  {table}");

        if (!extractor.Tables.All.Any())
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("error: no tables were located. This does not look like a supported cartridge.");
            return 2;
        }

        if (options.Diagnose)
        {
            Directory.CreateDirectory(options.OutputDirectory);
            string diagnosticsPath = Path.Combine(options.OutputDirectory, "diagnostics.txt");

            var lines = new List<string>();
            RomDiagnostics.Report(rom, extractor.Tables, line =>
            {
                Console.WriteLine(line);
                lines.Add(line);
            });

            File.WriteAllLines(diagnosticsPath, lines);
            Console.WriteLine();
            Console.WriteLine($"Wrote {diagnosticsPath}");
        }

        Directory.CreateDirectory(options.OutputDirectory);

        WriteTableReport(extractor, options.OutputDirectory);
        int speciesCount = WriteSpecies(extractor, options.OutputDirectory);

        if (!options.SkipSprites)
            WriteSprites(extractor, options, speciesCount);

        if (options.ListMaps || options.Maps.Length > 0 || options.RenderAllMaps
            || !string.IsNullOrEmpty(options.MapNameFilter))
            WriteMaps(rom, options);

        if (options.DumpMoves)
            WriteMoves(rom, options.OutputDirectory);

        if (options.DumpEncounters)
            WriteEncounters(rom, options);

        if (!string.IsNullOrEmpty(options.ExportWorldPath))
            ExportWorld(rom, options.ExportWorldPath);

        if (!string.IsNullOrEmpty(options.ExportRulesPath))
            ExportRules(rom, options.ExportRulesPath);

        if (options.DumpOverworld)
            WriteOverworldSprites(rom, options.OutputDirectory);

        if (options.DumpTrainers)
            WriteTrainers(rom, speciesCount);

        if (options.DumpItems)
            WriteItems(rom);

        if (options.DumpScripts)
            WriteScripts(rom);

        if (!string.IsNullOrEmpty(options.ScriptMap))
            WriteMapScripts(rom, options.ScriptMap);

        if (!string.IsNullOrEmpty(options.At))
            WriteSquare(rom, options.At);

        if (!string.IsNullOrEmpty(options.ScriptRun))
            WriteScriptRuns(rom, options.ScriptRun);

        if (options.ScriptRuns) WriteRunHistogram(rom);

        if (options.Specials) WriteSpecials(rom);

        if (options.Special is { } routine) WriteSpecial(rom, routine);

        if (options.Answers is { } answering) WriteAnswers(rom, answering);

        if (options.AnswerSweep) WriteAnswerSweep(rom);

        if (!string.IsNullOrEmpty(options.SpecialsOn)) WriteSpecialsOn(rom, options.SpecialsOn);

        if (options.Shared) WriteSharedScripts(rom);

        if (options.Silent) WriteSilentPeople(rom);

        if (options.Derive) WriteDerivedLengths(rom);

        if (options.BytesAfter is { } after) WriteBytesAfter(rom, after);

        if (options.Glyphs) WriteGlyphCandidates(rom, options.OutputDirectory);

        if (options.Font != 0) WriteFontSheet(rom, options.Font, options.OutputDirectory);

        if (!string.IsNullOrEmpty(options.WhoSays)) WriteWhoSays(rom, options.WhoSays);

        if (options.WhoGives is { } wanted) WriteWhoGives(rom, wanted);

        if (options.Events) WriteEventShapes(rom);

        if (options.Movements) WriteMovements(rom);

        if (options.Step is { } stepByte) WriteStep(rom, stepByte);

        if (options.Doors) WriteDoorCommands(rom);

        if (options.PlayerWalks) WritePlayerWalks(rom);

        if (options.Variable is { } variable) WriteVariable(rom, variable);

        if (options.Probe) WriteMapScripts(rom);

        if (options.ScriptAt != 0) WriteScriptAt(rom, options.ScriptAt);

        Console.WriteLine();
        Console.WriteLine($"Done. Output in {Path.GetFullPath(options.OutputDirectory)}");
        return 0;
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static void WriteTableReport(RomExtractor extractor, string outputDirectory)
    {
        var report = new
        {
            cartridge = extractor.Identity,
            tables = extractor.Tables.All.Select(t => new
            {
                t.Name,
                address = $"0x{t.Address:X8}",
                fileOffset = $"0x{t.Offset:X}",
                t.EntrySize,
                t.EntryCount,
                t.Method,
            }),
        };

        string path = Path.Combine(outputDirectory, "tables.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, Json));
        Console.WriteLine();
        Console.WriteLine($"Wrote {path}");
    }

    private static int WriteSpecies(RomExtractor extractor, string outputDirectory)
    {
        if (extractor.Tables.BaseStats is null)
        {
            Console.WriteLine("Skipping species dump: base-stat table not found.");
            return 0;
        }

        List<SpeciesData> species = extractor.ExtractSpecies();

        string path = Path.Combine(outputDirectory, "species.json");
        File.WriteAllText(path, JsonSerializer.Serialize(species, Json));
        Console.WriteLine($"Wrote {path} ({species.Count} entries)");

        Console.WriteLine();
        Console.WriteLine("Spot check:");
        foreach (int index in new[] { 1, 4, 7, 25, 150 })
        {
            if (index < species.Count)
                Console.WriteLine($"  {species[index]}");
        }

        return species.Count;
    }

    private static void WriteSprites(RomExtractor extractor, Options options, int speciesCount)
    {
        if (extractor.Tables.FrontPics is null || extractor.Tables.NormalPalettes is null)
        {
            Console.WriteLine("Skipping sprites: sprite or palette table not found.");
            return;
        }

        string spriteDirectory = Path.Combine(options.OutputDirectory, "sprites");
        Directory.CreateDirectory(spriteDirectory);

        Console.WriteLine();
        int written = 0;

        foreach (int index in options.Species)
        {
            try
            {
                ExtractedSprite sprite = extractor.ExtractSprite(
                    index, options.Shiny, options.Back, options.TileOrder);

                string name = $"{index:D3}{(options.Back ? "_back" : "")}{(options.Shiny ? "_shiny" : "")}.png";
                string path = Path.Combine(spriteDirectory, name);
                File.WriteAllBytes(path, sprite.ToPng());

                Console.WriteLine($"Wrote {path}");
                written++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  species {index}: {ex.Message}");
            }
        }

        if (written > 0)
        {
            Console.WriteLine();
            Console.WriteLine("If a sprite looks shuffled into 8-pixel blocks, re-run with");
            Console.WriteLine("  --tile-order column");
        }
    }

    /// <summary>How many maps to preview on the console before deferring to the file.</summary>
    private const int PreviewCount = 20;

    /// <summary>One map, as the game itself addresses it.</summary>
    private sealed record NamedMap(int Bank, int Map, string Name, MapHeaderRecord Header)
    {
        /// <summary>A filename that sorts by bank and map but is still readable.</summary>
        public string FileName
        {
            get
            {
                var slug = new string(Name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
                return $"{Bank:D2}-{Map:D2}{(slug.Length > 0 ? "_" + slug : "")}.png";
            }
        }

        public override string ToString() =>
            $"[{Bank,2}.{Map,-2}] {Name,-18} {Header.Layout}";
    }

    private static void WriteMaps(Rom rom, Options options)
    {
        Console.WriteLine();
        Console.WriteLine("Locating maps");

        MapBankTable? banks = MapBankLocator.Locate(rom, Console.WriteLine);

        if (banks is null)
        {
            Console.Error.WriteLine("  no map bank table found");
            return;
        }

        RegionNameTable? regions = RegionNameLocator.Locate(rom, Console.WriteLine);

        Console.WriteLine();
        Console.WriteLine($"  {banks}");
        Console.WriteLine(regions is null
            ? "  no region name table found — maps will be labelled by section id"
            : $"  region names: {regions}");

        // Section ids need not start at zero, so align the lowest one in use with the
        // table's first name before resolving anything.
        List<int> sectionIds = banks.AllMaps.Select(m => (int)m.Header.RegionSectionId).ToList();
        int indexBase = regions?.InferIndexBase(sectionIds) ?? 0;

        if (indexBase != 0)
            Console.WriteLine($"  section ids start at {indexBase}, aligned to the first name");

        List<NamedMap> maps = banks.AllMaps
            .Select(entry => new NamedMap(
                entry.Bank,
                entry.Map,
                regions?.Resolve(entry.Header.RegionSectionId, indexBase)
                    ?? $"SECTION {entry.Header.RegionSectionId}",
                entry.Header))
            .ToList();

        string listPath = WriteMapList(options.OutputDirectory, maps);
        Console.WriteLine($"  Wrote {listPath}");

        if (options.ListMaps)
        {
            Console.WriteLine();
            Console.WriteLine($"Largest maps (the full list is in {Path.GetFileName(listPath)}):");

            foreach (NamedMap map in maps.OrderByDescending(m => m.Header.Layout.BlockCount).Take(PreviewCount))
                Console.WriteLine($"  {map}");
        }

        List<NamedMap> toRender = SelectMaps(maps, options);
        if (toRender.Count == 0) return;

        string mapDirectory = Path.Combine(options.OutputDirectory, "maps");
        Directory.CreateDirectory(mapDirectory);

        Console.WriteLine();
        int written = 0, failed = 0;

        foreach (NamedMap map in toRender)
        {
            try
            {
                RenderedMap rendered = MapRenderer
                    .Create(rom, map.Header.Layout, options.Split)
                    .Render(map.Header.Layout);

                File.WriteAllBytes(Path.Combine(mapDirectory, map.FileName), rendered.ToPng());
                written++;

                // Rendering everything is hundreds of files; report progress instead
                // of a line each.
                if (toRender.Count <= PreviewCount)
                    Console.WriteLine($"Wrote {map.FileName} ({rendered.Width}x{rendered.Height})");
            }
            catch (Exception ex)
            {
                failed++;
                if (toRender.Count <= PreviewCount)
                    Console.Error.WriteLine($"  {map.Bank}.{map.Map}: {ex.Message}");
            }
        }

        if (toRender.Count > PreviewCount)
            Console.WriteLine($"Rendered {written} maps into {mapDirectory}{(failed > 0 ? $" ({failed} failed)" : "")}");

        Console.WriteLine();
        Console.WriteLine("If colours look wrong in places, the primary/secondary tileset split");
        Console.WriteLine("may differ on this cartridge — try --tileset-split emerald.");
    }

    /// <summary>
    /// Works out which maps to render: everything, a name match, or specific
    /// <c>bank.map</c> addresses.
    /// </summary>
    private static List<NamedMap> SelectMaps(List<NamedMap> maps, Options options)
    {
        if (options.RenderAllMaps) return maps;

        if (!string.IsNullOrEmpty(options.MapNameFilter))
        {
            List<NamedMap> matched = Core.World.MapNameMatch
                .Rank(maps, m => m.Name, options.MapNameFilter, m => m.Header.Layout.BlockCount)
                .ToList();

            if (matched.Count == 0)
                Console.Error.WriteLine($"  no map name contains '{options.MapNameFilter}'");

            return matched;
        }

        var selected = new List<NamedMap>();

        foreach (string spec in options.Maps)
        {
            string[] parts = spec.Split('.');

            NamedMap? match = parts.Length == 2
                && int.TryParse(parts[0], out int bank)
                && int.TryParse(parts[1], out int number)
                    ? maps.FirstOrDefault(m => m.Bank == bank && m.Map == number)
                    : int.TryParse(spec, out int flat) ? maps.ElementAtOrDefault(flat) : null;

            if (match is null) Console.Error.WriteLine($"  no map matches '{spec}'");
            else selected.Add(match);
        }

        return selected;
    }

    /// <summary>
    /// Writes the map list to disk. The console listing is a preview only: a real
    /// cartridge holds hundreds of maps, which scrolls out of a terminal's buffer and
    /// leaves nothing to search through afterwards.
    /// </summary>
    private static string WriteMapList(string outputDirectory, List<NamedMap> maps)
    {
        Directory.CreateDirectory(outputDirectory);
        string path = Path.Combine(outputDirectory, "maps.json");

        var report = maps.Select(map => new
        {
            bank = map.Bank,
            map = map.Map,
            name = map.Name,
            file = map.FileName,
            header = $"0x{map.Header.Address:X8}",
            layout = $"0x{map.Header.Layout.Address:X8}",
            widthBlocks = map.Header.Layout.Width,
            heightBlocks = map.Header.Layout.Height,
            widthPixels = map.Header.Layout.Width * MapRenderer.BlockPixels,
            heightPixels = map.Header.Layout.Height * MapRenderer.BlockPixels,
            music = map.Header.Music,
            mapType = map.Header.MapType,
            regionSectionId = map.Header.RegionSectionId,
        });

        File.WriteAllText(path, JsonSerializer.Serialize(report, Json));
        return path;
    }

    private static void WriteMoves(Rom rom, string outputDirectory)
    {
        Console.WriteLine();
        Console.WriteLine("Locating moves");

        List<MoveData> moves;

        try
        {
            moves = MoveExtractor.Extract(rom, Console.WriteLine);
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"  {ex.Message}");
            return;
        }

        string path = Path.Combine(outputDirectory, "moves.json");
        File.WriteAllText(path, JsonSerializer.Serialize(moves, Json));
        Console.WriteLine($"Wrote {path} ({moves.Count} moves)");

        Console.WriteLine();
        Console.WriteLine("Spot check:");

        // Familiar moves at known indices, so a wrong anchor is obvious at a glance.
        foreach (int id in new[] { 1, 2, 57, 63, 85 })
        {
            if (id < moves.Count) Console.WriteLine($"  #{id,3} {moves[id]} [{moves[id].Category}]");
        }
    }

    private static void WriteEncounters(Rom rom, Options options)
    {
        Console.WriteLine();
        Console.WriteLine("Locating encounters");

        List<Core.World.MapEncounters> encounters = EncounterExtractor.Extract(rom, Console.WriteLine);

        if (encounters.Count == 0) return;

        string path = Path.Combine(options.OutputDirectory, "encounters.json");
        File.WriteAllText(path, JsonSerializer.Serialize(encounters, Json));
        Console.WriteLine($"Wrote {path}");

        ReportNamedEncounters(rom, options, encounters);
        ReportBehaviours(rom, options);
    }

    /// <summary>
    /// Prints the encounter table for a named map, and for a couple of others, with
    /// species names rather than numbers — the only way to tell at a glance whether
    /// the table found is the right one.
    /// </summary>
    private static void ReportNamedEncounters(
        Rom rom, Options options, List<Core.World.MapEncounters> encounters)
    {
        MapBankTable? banks = MapBankLocator.Locate(rom);
        if (banks is null) return;

        RegionNameTable? names = RegionNameLocator.Locate(rom);
        List<int> sectionIds = banks.AllMaps.Select(m => (int)m.Header.RegionSectionId).ToList();
        int indexBase = names?.InferIndexBase(sectionIds) ?? 0;

        Dictionary<string, string> mapNames = banks.AllMaps.ToDictionary(
            m => WorldExporter.MapId(m.Bank, m.Map),
            m => names?.Resolve(m.Header.RegionSectionId, indexBase) ?? $"SECTION {m.Header.RegionSectionId}");

        List<SpeciesData> species = RomExtractor.Open(rom).ExtractSpecies();

        string SpeciesName(int index) =>
            index >= 0 && index < species.Count && species[index].Name.Length > 0
                ? species[index].Name
                : $"#{index}";

        string wanted = options.BehaviourMap ?? "route 1";

        var withLand = encounters
            .Where(e => e.Land is not null && mapNames.ContainsKey(e.MapId))
            .Select(e => (Encounters: e, Name: mapNames[e.MapId]))
            .ToList();

        var chosen = Core.World.MapNameMatch
            .Rank(withLand, x => x.Name, wanted)
            .Concat(withLand)
            .DistinctBy(x => x.Encounters.MapId)
            .Take(3)
            .ToList();

        Console.WriteLine();
        Console.WriteLine("Spot check (land tables):");

        foreach ((Core.World.MapEncounters map, string name) in chosen)
        {
            Console.WriteLine($"  {name} ({map.MapId}), rate {map.Land!.Rate}");

            foreach (Core.World.WildSlot slot in map.Land.Slots.Take(5))
                Console.WriteLine($"    {SpeciesName(slot.Species),-12} L{slot.MinLevel}-{slot.MaxLevel}");
        }
    }

    /// <summary>
    /// Counts how often each metatile behaviour appears on a map.
    /// <para>
    /// Which behaviour value means "tall grass" is a per-game constant, and guessing
    /// it is how the last several bugs happened. A histogram against a map whose
    /// layout is already known says which value it is, rather than assuming.
    /// </para>
    /// </summary>
    private static void ReportBehaviours(Rom rom, Options options)
    {
        MapBankTable? banks = MapBankLocator.Locate(rom);
        if (banks is null) return;

        RegionNameTable? names = RegionNameLocator.Locate(rom);
        List<int> sectionIds = banks.AllMaps.Select(m => (int)m.Header.RegionSectionId).ToList();
        int indexBase = names?.InferIndexBase(sectionIds) ?? 0;

        // Its own option, so asking for an encounter report does not also render maps.
        string wanted = options.BehaviourMap ?? "route 1";

        var match = Core.World.MapNameMatch.Rank(
            banks.AllMaps,
            m => names?.Resolve(m.Header.RegionSectionId, indexBase) ?? "",
            wanted,
            m => m.Header.Layout.BlockCount).FirstOrDefault();

        if (match.Header is null) return;

        string mapLabel = $"{names?.Resolve(match.Header.RegionSectionId, indexBase)} ({match.Bank}.{match.Map})";

        // Both stride interpretations, drawn out. Real terrain forms solid patches;
        // a wrong stride scatters it, and the shape says which is which far faster
        // than any amount of reasoning about the format.
        foreach (int stride in new[] { 1, 2, 4 })
        {
            byte[] behaviours = match.Header.Layout.ReadBehaviours(rom, options.Split, stride);

            Console.WriteLine();
            Console.WriteLine($"Behaviours on {mapLabel} reading {stride} byte(s) per metatile:");

            foreach (var group in behaviours.GroupBy(b => b).OrderByDescending(g => g.Count()).Take(6))
                Console.WriteLine($"  0x{group.Key:X2}  {group.Count(),5} squares");

            // Draw whichever value is most common after ordinary ground, whatever it
            // turns out to be. Naming the grass constant in advance is what went wrong
            // last time; the shape of the data can identify it instead.
            byte candidate = behaviours
                .Where(b => b != Core.World.MetatileBehaviour.Normal)
                .GroupBy(b => b)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            if (candidate != 0)
                DrawBehaviourMap(behaviours, match.Header.Layout.Width, match.Header.Layout.Height, candidate);
        }
    }

    /// <summary>
    /// Draws where one behaviour value appears. Terrain in the real games comes in
    /// solid rectangular patches, so the picture shows immediately whether a value is
    /// real terrain or an artifact of reading the table wrongly.
    /// </summary>
    private static void DrawBehaviourMap(byte[] behaviours, int width, int height, byte wanted)
    {
        int count = behaviours.Count(b => b == wanted);
        Console.WriteLine($"  0x{wanted:X2} appears on {count} squares, '#' below:");

        for (int y = 0; y < height; y++)
        {
            var row = new System.Text.StringBuilder("    ");

            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;

                byte behaviour = index < behaviours.Length
                    ? behaviours[index]
                    : Core.World.MetatileBehaviour.Normal;

                row.Append(behaviour == wanted ? '#' : '.');
            }

            Console.WriteLine(row.ToString());
        }
    }

    /// <summary>
    /// Writes the collision-only world file the server runs on. Dimensions and
    /// walkability only — no graphics, no text, nothing redistributable.
    /// </summary>
    private static void ExportWorld(Rom rom, string path)
    {
        Console.WriteLine();
        Console.WriteLine("Exporting world");

        Core.World.WorldData world = WorldExporter.Export(rom, Console.WriteLine);
        world.Save(path);

        Console.WriteLine($"Wrote {Path.GetFullPath(path)} ({world.Count} maps, collision only)");
    }

    private static void ExportRules(Rom rom, string path)
    {
        Console.WriteLine();
        Console.WriteLine("Exporting rules");

        Core.Data.GameRules rules = RulesExporter.Export(rom, Console.WriteLine);
        rules.Save(path);

        Console.WriteLine(
            $"Wrote {Path.GetFullPath(path)} " +
            $"({rules.SpeciesCount} species, {rules.MoveCount} moves, no names)");
    }

    /// <summary>
    /// Reads the little figures that walk around a map, and writes a few out.
    /// <para>
    /// A new table locator is only ever really tested against a real cartridge, so this
    /// reports what it found in enough detail to tell "found the wrong run" from "found
    /// nothing" from "found it" — a count, the dimensions, and pictures to look at.
    /// </para>
    /// </summary>
    /// <summary>
    /// Reports the trainer table, in the shape that answers the question a located
    /// address cannot: whether the ids line up.
    /// <para>
    /// The table's first entry is a placeholder with no party, so a locator that starts
    /// on the first <em>readable</em> record starts one slot late and hands every
    /// trainer somebody else's creatures. Nothing about that looks wrong from the
    /// outside — the counts are healthy either way — so what is printed here is what
    /// came just before the start, and a sample of parties to eyeball against the games.
    /// </para>
    /// </summary>
    private static void WriteTrainers(Rom rom, int speciesCount)
    {
        Console.WriteLine();
        Console.WriteLine("Trainers");

        if (speciesCount <= 0) speciesCount = 512;

        if (TrainerTable.Locate(rom, speciesCount, Console.WriteLine) is not { } table)
        {
            Console.WriteLine("  no trainer table found");
            return;
        }

        List<TrainerRecord> trainers = TrainerTable.Read(rom, table, speciesCount);

        Console.WriteLine($"  {trainers.Count} trainers, highest id {trainers.Max(t => t.Id)}");

        Console.WriteLine(
            $"  the slot before the table: {TrainerRecord.Explain(rom, table - TrainerRecord.RecordSizeBytes, speciesCount)}");

        Console.WriteLine($"  the first slot itself:     {TrainerRecord.Explain(rom, table, speciesCount)}");

        var shapes = trainers
            .GroupBy(t => (t.Party.Any(m => m.HeldItem != 0), t.Party.Any(m => m.Moves.Count > 0)))
            .OrderByDescending(g => g.Count());

        foreach (var shape in shapes)
        {
            (bool item, bool moves) = shape.Key;
            Console.WriteLine(
                $"    {shape.Count(),4} with {(item ? "items" : "no items"),8}, {(moves ? "custom moves" : "level-up moves")}");
        }

        Console.WriteLine($"    {trainers.Count(t => t.IsDouble)} double battles");

        // How many have no name at all. A handful at the front of the table is
        // ordinary — placeholders left over from development. Most of them having no
        // name would mean the name is not where this thinks it is, which is a different
        // problem entirely and one worth telling apart at a glance.
        int nameless = trainers.Count(t => string.IsNullOrWhiteSpace(t.Name));

        Console.WriteLine($"    {nameless} with no name, {trainers.Count - nameless} named");

        var classes = trainers.GroupBy(t => t.Class).OrderByDescending(g => g.Count()).Take(5);
        Console.WriteLine($"    commonest classes: {string.Join(", ", classes.Select(c => $"{c.Key} x{c.Count()}"))}");

        Console.WriteLine();
        Console.WriteLine("  Spot check — compare these names and parties against the games:");

        // Spread across the whole table rather than taken off the front. The front of a
        // real table is placeholders, which say nothing about whether the rest lines up.
        foreach (TrainerRecord trainer in Spread(trainers, 12))
        {
            string party = string.Join(", ", trainer.Party.Select(m =>
                $"#{m.Species} L{m.Level}" +
                (m.HeldItem != 0 ? $" holding {m.HeldItem}" : "") +
                (m.Moves.Count > 0 ? $" [{string.Join(" ", m.Moves)}]" : "")));

            string name = string.IsNullOrWhiteSpace(trainer.Name) ? "(no name)" : trainer.Name;

            Console.WriteLine($"    {trainer.Id,4} {name,-12} class {trainer.Class,3}  {party}");
        }
    }

    /// <summary>Evenly spaced entries, so a sample says something about the whole table.</summary>
    private static IEnumerable<T> Spread<T>(IReadOnlyList<T> items, int wanted)
    {
        if (items.Count <= wanted) return items;

        int step = items.Count / wanted;

        return Enumerable.Range(0, wanted).Select(i => items[i * step]);
    }

    /// <summary>
    /// Reports the item table.
    /// <para>
    /// Shorter than the others because there is less to doubt. Every record contains
    /// its own index, so a table that counts from zero for four hundred entries is the
    /// table — there is no question of starting a slot early or late.
    /// </para>
    /// </summary>
    /// <summary>
    /// Reports how far the scripts on people actually read.
    /// <para>
    /// A script that stops at a command this project does not know is not an error and
    /// produces no symptom: it simply contains less than it does. Everything past that
    /// point — what somebody says, who they fight, what they sell — is invisible, and
    /// the only way to find out which command is in the way is to count them.
    /// </para>
    /// </summary>
    private static void WriteScripts(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("Scripts");

        MapLibrary library = MapLibrary.Open(rom);

        int withScripts = 0;
        int cleanEnd = 0;
        int withMart = 0;
        int withTrainer = 0;

        var stoppers = new Dictionary<byte, int>();
        var examples = new Dictionary<byte, (int Start, int Stop)>();

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
            {
                withScripts++;

                if (ScriptReader.StoppedAt(rom, person.ScriptAddress) is { } code)
                {
                    stoppers[code] = stoppers.GetValueOrDefault(code) + 1;

                    if (!examples.ContainsKey(code) &&
                        rom.ToOffsetOrNull(person.ScriptAddress) is { } start &&
                        ScriptReader.StoppedAtOffset(rom, person.ScriptAddress) is { } stop)
                    {
                        examples[code] = (start, stop);
                    }
                }
                else
                {
                    cleanEnd++;
                }

                if (ScriptReader.FindMart(rom, person.ScriptAddress).Count > 0) withMart++;
                if (ScriptReader.FindTrainer(rom, person.ScriptAddress) is not null) withTrainer++;
            }
        }

        Console.WriteLine($"  {withScripts} people with a script");
        Console.WriteLine($"  {cleanEnd} read to a proper end, {withScripts - cleanEnd} stopped at a command we do not know");
        Console.WriteLine($"  {withTrainer} name a trainer, {withMart} open a shop");
        Console.WriteLine();
        Console.WriteLine("  The commands stopping the most reads:");

        foreach ((byte code, int count) in stoppers.OrderByDescending(s => s.Value).Take(20))
            Console.WriteLine($"    0x{code:X2}  stops {count}");

        // A count says which command is in the way; it does not say how long that
        // command is, and guessing a length is worse than not knowing one — a wrong
        // length resumes inside an argument and invents every instruction after it.
        // The bytes are what settle it: a pointer is recognisable on sight.
        Console.WriteLine();
        Console.WriteLine("  One script for each, from the start, with ^ under where it stopped:");

        foreach (byte code in stoppers.OrderByDescending(s => s.Value).Take(6).Select(s => s.Key))
        {
            if (!examples.TryGetValue(code, out (int Start, int Stop) example)) continue;

            Console.WriteLine();
            Console.WriteLine($"    stopped by 0x{code:X2} at 0x{Rom.BaseAddress + (uint)example.Stop:X8}");

            for (int row = 0; row < 3; row++)
            {
                int from = example.Start + row * 16;
                if (from + 16 > rom.Length) break;

                string hex = string.Join(" ", Enumerable.Range(0, 16).Select(i => $"{rom.ReadU8(from + i):X2}"));
                Console.WriteLine($"      {Rom.BaseAddress + (uint)from:X8}  {hex}");

                if (example.Stop < from || example.Stop >= from + 16) continue;

                Console.WriteLine($"      {new string(' ', 8)}  {new string(' ', (example.Stop - from) * 3)}^^");
            }
        }
    }

    /// <summary>
    /// The fifth list, checked against itself across every map on the cartridge.
    /// <para>
    /// Every number here would come out differently if the shape were wrong: a list that
    /// ran past its terminator, a pointer that did not resolve, a condition naming a
    /// variable outside the range every other part of this project sees. That is the only
    /// kind of evidence available for a record with no length field and no header.
    /// </para>
    /// </summary>
    private static void WriteMapScripts(Rom rom)
    {
        MapBankTable banks = MapBankLocator.Locate(rom)!;
        MapScripts.Survey survey = MapScripts.Check(rom, banks);

        Console.WriteLine();
        Console.WriteLine("The fifth list — what a map runs by itself");
        Console.WriteLine();
        Console.WriteLine($"  {survey.Maps} maps, {survey.WithNone} with an empty list");
        Console.WriteLine($"  longest list {survey.LongestSeen} entries, {survey.Entries} entries in all");
        Console.WriteLine($"  {survey.PointersThatResolve} of {survey.Entries} pointers resolve into the cartridge");
        Console.WriteLine();

        foreach ((byte kind, int count) in survey.ByKind.OrderBy(e => e.Key))
        {
            Console.WriteLine(
                $"    kind {kind}: {count,4} entries — " +
                (MapScripts.IsConditional(kind) ? "a table of conditions" : "a script, with no condition attached"));
        }

        Console.WriteLine();
        Console.WriteLine($"  {survey.Conditions} conditions across the two table kinds");
        Console.WriteLine(
            $"  {survey.ConditionsInVariableRange} of them name a variable in the range everything " +
            $"else in this project uses, over {survey.DistinctVariables} distinct variables");
    }

    /// <summary>
    /// Everything in the world that writes one variable, and everything that waits on it.
    /// <para>
    /// The story is a chain of these and nothing else. At any moment the next thing that
    /// can happen is a square or a person gated on a variable holding a particular value,
    /// and the only question worth asking is who sets it to that. Standing in the
    /// professor's lab with 0x4055 holding 1 and three triggers wanting 2 is not a bug
    /// report, it is the next link, and finding it by reading maps one at a time is how
    /// an evening goes.
    /// </para>
    /// <para>
    /// Both halves are printed because both halves are the answer. A variable with
    /// writers and no readers is bookkeeping; one with readers and no writers is a wall.
    /// </para>
    /// </summary>
    private static void WriteVariable(Rom rom, int variable)
    {
        MapLibrary library = MapLibrary.Open(rom);

        var writes = new List<string>();
        var waits = new List<string>();

        foreach (LoadedMap map in library.All())
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            List<(string Who, uint Script)> scripts =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => ($"person {o.LocalId} at {o.Square}", o.ScriptAddress)),
                .. map.Triggers.Where(t => t.HasScript).Select(t => ($"square {t.Square}", t.ScriptAddress)),
                .. map.Signs.Where(s => s.HasScript).Select(s => ($"sign at {s.Square}", s.ScriptAddress)),

                // The fifth list. Left out of the first version of this sweep, which is
                // exactly why it reported that nothing in the world sets 0x4055 to 2.
                .. map.OnEntry.Where(e => e.HasScript).Select(e => ("arriving here", e.ScriptAddress)),
            ];

            foreach (MapEntryScript arrival in map.OnEntry.Where(e => e.Variable == variable))
            {
                waits.Add(
                    $"    {mapId} {map.Name}: arriving runs 0x{arrival.ScriptAddress:X8} " +
                    $"while it holds {arrival.Value}");
            }

            // The gate side comes off the map, not the script: a trigger names its
            // variable and its value in the event record, which is why the server can
            // refuse one without ever having read a byte of script.
            foreach (MapTrigger trigger in map.Triggers.Where(t => t.Variable == variable))
            {
                waits.Add(
                    $"    {mapId} {map.Name}: square {trigger.Square} runs 0x{trigger.ScriptAddress:X8} " +
                    $"while it holds {trigger.Value}");
            }

            foreach ((string who, uint script) in scripts.DistinctBy(s => s.Script))
            {
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, script))
                {
                    string what = command.Code switch
                    {
                        0x16 when command.Arguments.Length >= 4 && command.Word() == variable
                            => $"sets it to {command.Word(2)}",
                        0x17 when command.Arguments.Length >= 4 && command.Word() == variable
                            => $"adds {command.Word(2)} to it",
                        0x21 when command.Arguments.Length >= 4 && command.Word() == variable
                            => $"compares it with {command.Word(2)}",
                        _ => "",
                    };

                    if (what.Length == 0) continue;

                    (what.StartsWith("compares", StringComparison.Ordinal) ? waits : writes)
                        .Add($"    {mapId} {map.Name}: {who} {what} (0x{script:X8})");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Variable 0x{variable:X4}");
        Console.WriteLine();
        Console.WriteLine($"  written by {writes.Count}");

        foreach (string line in writes.Take(30)) Console.WriteLine(line);

        Console.WriteLine();
        Console.WriteLine($"  waited on by {waits.Count}");

        foreach (string line in waits.Take(30)) Console.WriteLine(line);
    }

    /// <summary>
    /// What the cartridge uses scripted player movement for, before deciding to stop
    /// performing it.
    /// <para>
    /// The question a design choice turns on. A scene that walks the player somewhere on
    /// the same map costs nothing to drop — they walk there themselves, which is the
    /// point. A scene that walks them onto a door is the only thing that puts them
    /// through it, and dropping that one leaves them outside a building the story is
    /// about. So: how many of each.
    /// </para>
    /// </summary>
    private static void WritePlayerWalks(Rom rom)
    {
        MapLibrary library = MapLibrary.Open(rom);

        int scenes = 0, moveThePlayer = 0, ontoADoor = 0;
        var doors = new List<string>();

        foreach (LoadedMap map in library.All())
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            foreach (MapTrigger trigger in map.Triggers.Where(t => t.HasScript))
            {
                ScriptRun run = ScriptRunner.Run(rom, trigger.ScriptAddress);

                if (!run.IsScene) continue;

                scenes++;

                // Every walk the player is given, in the order the scene performs them,
                // chained from the square that started it. Beats rather than a read of
                // the script, because a read follows both arms of every branch and a
                // scene only walks one of them — chaining across arms would put the
                // player somewhere the game never puts them.
                GridPosition at = trigger.Square;
                bool walked = false;

                foreach (SceneBeat.Walk walk in run.Beats.OfType<SceneBeat.Walk>().Where(w => w.IsPlayer))
                {
                    walked = true;

                    foreach (byte step in walk.Steps)
                    {
                        if (MovementLists.DirectionOf(step) is { } direction) at = at.Step(direction);
                    }
                }

                if (!walked) continue;

                moveThePlayer++;

                if (!map.Warps.Any(w => w.Square == at)) continue;

                ontoADoor++;

                if (doors.Count < 12)
                    doors.Add($"    {mapId} {map.Name}: from {trigger.Square} to the door at {at}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("What a script uses the player's own feet for");
        Console.WriteLine();
        Console.WriteLine($"  {scenes} squares in the world start a scene");
        Console.WriteLine($"  {moveThePlayer} of those scenes walk the player somewhere");
        Console.WriteLine($"  {ontoADoor} of those leave them standing on a door");

        foreach (string door in doors) Console.WriteLine(door);
    }

    /// <summary>
    /// Whether the two commands bracketing a scripted walk are naming doors.
    /// <para>
    /// 0xAC and 0xAD were adopted as four-byte commands because the column said four, and
    /// four bytes is where the reading stopped. The opening of this game reads them as
    /// (16, 13) — which is exactly where the professor's lab door is, on the map whose
    /// script it is. That is either the answer or a coincidence, and the difference is
    /// four hundred maps wide.
    /// </para>
    /// </summary>
    private static void WriteDoorCommands(Rom rom)
    {
        MapLibrary library = MapLibrary.Open(rom);

        var counts = new Dictionary<byte, (int Total, int OnDoor)>();
        var examples = new List<string>();

        foreach (LoadedMap map in library.All())
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            IEnumerable<uint> scripts =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
                .. map.Signs.Where(s => s.HasScript).Select(s => s.ScriptAddress),
            ];

            foreach (uint script in scripts.Distinct())
            {
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, script))
                {
                    if (command.Code is not (0xAC or 0xAD)) continue;
                    if (command.Arguments.Length < 4) continue;

                    var named = new GridPosition(command.Word(), command.Word(2));
                    bool onDoor = map.Warps.Any(w => w.Square == named);

                    (int total, int hit) = counts.GetValueOrDefault(command.Code);
                    counts[command.Code] = (total + 1, hit + (onDoor ? 1 : 0));

                    if (!onDoor && examples.Count < 12)
                        examples.Add($"    0x{command.Code:X2} on {mapId} names {named}, which is no door there");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("The commands that bracket a scripted walk");

        foreach ((byte code, (int total, int onDoor)) in counts.OrderBy(c => c.Key))
        {
            Console.WriteLine(
                $"  0x{code:X2}  {total,4} uses, {onDoor,4} naming a door on their own map " +
                $"({(total == 0 ? 0 : (double)onDoor / total):P0})");
        }

        foreach (string example in examples) Console.WriteLine(example);
    }

    /// <summary>
    /// Where one step byte turns up, and what is under the walker when it does.
    /// <para>
    /// For the steps outside the three walking families, which this project treats as
    /// standing still. Standing still is the honest reading of a byte nobody has
    /// evidence about, but it is not an answer, and one of these bytes is sitting at the
    /// end of the opening scene keeping the player outside the professor's lab.
    /// </para>
    /// </summary>
    private static void WriteStep(Rom rom, byte step)
    {
        MapLibrary library = MapLibrary.Open(rom);

        List<MovementLists.Appearance> found =
            MovementLists.Where(rom, library, step, MovementLists.DirectionOf);

        Console.WriteLine();
        Console.WriteLine($"Step 0x{step:X2}");
        Console.WriteLine($"  {found.Count} appearances in {found.Select(f => f.Address).Distinct().Count()} lists");

        int last = found.Count(f => f.IsLast);

        Console.WriteLine($"  {last} of them the last step of their list, {found.Count - last} not");

        List<MovementLists.Appearance> placed = [.. found.Where(f => f.Square is not null)];

        Console.WriteLine();
        Console.WriteLine($"  {placed.Count} on a walker whose square is known");
        Console.WriteLine($"  {placed.Count(p => p.OnWarp)} of those standing on a door");

        foreach (MovementLists.Appearance one in placed.Take(20))
        {
            Console.WriteLine(
                $"    {one.MapId} person {one.PersonId} at {one.Square}: step {one.Position + 1} of {one.Length}" +
                (one.OnWarp ? " — on a door" : ""));
        }
    }

    /// <summary>
    /// Everything a map says about one square and the ground around it.
    /// <para>
    /// The instrument for "I got stuck here". Being stuck is a fact about a square, and
    /// every previous answer to one of these reports was reached by reasoning about what
    /// the square probably contained. It contains four lists and a collision grid, all of
    /// which this project already reads, and none of which it could previously print for
    /// a place a player was standing.
    /// </para>
    /// </summary>
    private static void WriteSquare(Rom rom, string where)
    {
        string[] parts = where.Split(',', StringSplitOptions.TrimEntries);

        if (parts.Length != 3 || !int.TryParse(parts[1], out int atX) || !int.TryParse(parts[2], out int atY))
        {
            Console.WriteLine("  --at wants map,x,y — for example --at 3.0,16,13");
            return;
        }

        MapLibrary library = MapLibrary.Open(rom);

        if (library.TryLoad(parts[0]) is not { } map)
        {
            Console.WriteLine($"  no map {parts[0]} on this cartridge");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"({atX}, {atY}) on {parts[0]} {map.Name}, {map.Collision.Width}x{map.Collision.Height}");
        Console.WriteLine();

        const int Radius = 4;

        // The picture first. A square is stuck or not stuck because of its neighbours,
        // and a list of coordinates makes the reader rebuild the neighbourhood in their
        // head — which is where the reasoning went wrong last time.
        Console.WriteLine("  . walkable   # blocked   D door   P person   T trigger   S sign   @ here");
        Console.WriteLine();

        for (int y = atY - Radius; y <= atY + Radius; y++)
        {
            var row = new System.Text.StringBuilder($"  {y,3}  ");

            for (int x = atX - Radius; x <= atX + Radius; x++)
            {
                var square = new GridPosition(x, y);

                char cell =
                    x == atX && y == atY ? '@'
                    : map.Warps.Any(w => w.Square == square) ? 'D'
                    : map.Objects.Any(o => o.Square == square) ? 'P'
                    : map.Triggers.Any(t => t.Square == square) ? 'T'
                    : map.Signs.Any(s => s.Square == square) ? 'S'
                    : map.Collision.IsWalkable(square) ? '.'
                    : '#';

                row.Append(cell).Append(' ');
            }

            Console.WriteLine(row.ToString());
        }

        Console.WriteLine();
        Console.WriteLine($"  here: {(map.Collision.IsWalkable(new GridPosition(atX, atY)) ? "walkable" : "blocked")}");

        foreach (Direction direction in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
        {
            GridPosition next = new GridPosition(atX, atY).Step(direction);

            string what = map.Warps.FirstOrDefault(w => w.Square == next) is { } door
                ? $"a door to {door.TargetMapId} warp {door.TargetWarpId}"
                : map.Objects.FirstOrDefault(o => o.Square == next) is { } who
                    ? $"person {who.LocalId}" + (who.HasScript ? $", script 0x{who.ScriptAddress:X8}" : ", no script")
                    : map.Collision.IsWalkable(next) ? "open ground" : "blocked";

            Console.WriteLine($"  {direction,-6} ({next.X}, {next.Y}): {what}");
        }

        Console.WriteLine();

        foreach (Warp warp in map.Warps.OrderBy(w => Math.Abs(w.X - atX) + Math.Abs(w.Y - atY)).Take(6))
        {
            int away = Math.Abs(warp.X - atX) + Math.Abs(warp.Y - atY);

            Console.WriteLine(
                $"  door at ({warp.X}, {warp.Y}), {away} away: to {warp.TargetMapId} warp {warp.TargetWarpId}, " +
                (map.Collision.IsWalkable(warp.Square) ? "standable" : "SOLID"));
        }

        foreach (MapObject person in map.Objects.OrderBy(o => Math.Abs(o.X - atX) + Math.Abs(o.Y - atY)).Take(6))
        {
            int away = Math.Abs(person.X - atX) + Math.Abs(person.Y - atY);

            Console.WriteLine(
                $"  person {person.LocalId} at ({person.X}, {person.Y}), {away} away" +
                (person.HasScript ? $", script 0x{person.ScriptAddress:X8}" : ", no script") +
                (person.Talks ? ", talks" : ""));
        }

        foreach (MapTrigger trigger in map.Triggers.OrderBy(t => Math.Abs(t.X - atX) + Math.Abs(t.Y - atY)).Take(6))
        {
            int away = Math.Abs(trigger.X - atX) + Math.Abs(trigger.Y - atY);

            Console.WriteLine(
                $"  trigger at ({trigger.X}, {trigger.Y}), {away} away: armed while 0x{trigger.Variable:X4} " +
                $"holds {trigger.Value}, script 0x{trigger.ScriptAddress:X8}");
        }
    }

    /// <summary>
    /// Dumps every script on one map, as instructions and as bytes.
    /// <para>
    /// The histogram says which command is in the way. It does not say how long that
    /// command is, and a guessed length is worse than no length — it resumes inside an
    /// argument and invents every instruction after it. The bytes are what settle it: a
    /// pointer into this cartridge is recognisable on sight, and the shape of the
    /// arguments is readable once you can see them.
    /// </para>
    /// </summary>
    private static void WriteMapScripts(Rom rom, string mapId)
    {
        Console.WriteLine();
        Console.WriteLine($"Scripts on {mapId}");

        MapLibrary library = MapLibrary.Open(rom);

        if (library.TryLoad(mapId) is not { } map)
        {
            Console.WriteLine($"  no map {mapId} on this cartridge");
            return;
        }

        Console.WriteLine($"  {map.Name}, {map.Objects.Count} people");

        foreach (MapObject person in map.Objects.Where(o => o.HasScript))
        {
            Console.WriteLine();
            Console.WriteLine($"  person {person.LocalId} at ({person.X}, {person.Y}), script 0x{person.ScriptAddress:X8}");

            foreach (ScriptCommand command in ScriptReader.Read(rom, person.ScriptAddress))
                Console.WriteLine($"    {command}");

            if (ScriptReader.StoppedAt(rom, person.ScriptAddress) is { } stopper)
                Console.WriteLine($"    stopped at 0x{stopper:X2}");

            if (rom.ToOffsetOrNull(person.ScriptAddress) is not { } start) continue;

            for (int row = 0; row < 4; row++)
            {
                int from = start + row * 16;
                if (from + 16 > rom.Length) break;

                string hex = string.Join(" ", Enumerable.Range(0, 16).Select(i => $"{rom.ReadU8(from + i):X2}"));
                Console.WriteLine($"      {Rom.BaseAddress + (uint)from:X8}  {hex}");
            }
        }

        // The other two lists. A map's people are only a third of what runs on it, and
        // a dump that shows one third makes the other two invisible.
        foreach (MapTrigger trigger in map.Triggers.Where(t => t.HasScript))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  trigger at ({trigger.X}, {trigger.Y}), script 0x{trigger.ScriptAddress:X8}, " +
                $"armed while variable 0x{trigger.Variable:X4} holds {trigger.Value}" +
                (trigger.CanBeFought ? $", fights trainer {trigger.TrainerId}" : ""));

            foreach (ScriptCommand command in ScriptReader.Read(rom, trigger.ScriptAddress))
                Console.WriteLine($"    {command}");

            if (ScriptReader.StoppedAt(rom, trigger.ScriptAddress) is { } stopper)
                Console.WriteLine($"    stopped at 0x{stopper:X2}");

            ScriptRun run = ScriptRunner.Run(rom, trigger.ScriptAddress);

            Console.WriteLine(
                run.Pages.Count == 0
                    ? "    says nothing on a fresh save"
                    : $"    says: \"{GameText.ToAscii(run.Pages[0]).Replace('\n', ' ')}\"");
        }

        foreach (MapSign sign in map.Signs.Where(s => s.HasScript))
        {
            ScriptRun run = ScriptRunner.Run(rom, sign.ScriptAddress);

            Console.WriteLine(
                $"  sign at ({sign.X}, {sign.Y}): " +
                (run.Pages.Count == 0
                    ? "says nothing"
                    : $"\"{GameText.ToAscii(run.Pages[0]).Replace('\n', ' ')}\""));
        }
    }

    /// <summary>
    /// What everybody on a map actually says, twice: once from a fresh save and once
    /// with whatever their own script would have set already lit.
    /// <para>
    /// The instrument for this milestone. Running a script rather than reading it turns
    /// on a handful of numbers that this project has no way to check from a fixture —
    /// which condition byte means "less", whether a set trainer flag skips the fight or
    /// the whole script — and the last time numbers were half-remembered here it cost
    /// three rounds of the same bug. Two runs side by side is the cheapest way to see
    /// whether the second one is a different sentence rather than the same one.
    /// </para>
    /// </summary>
    private static void WriteScriptRuns(Rom rom, string mapId)
    {
        Console.WriteLine();
        Console.WriteLine($"Running the scripts on {mapId}");

        MapLibrary library = MapLibrary.Open(rom);

        if (library.TryLoad(mapId) is not { } map)
        {
            Console.WriteLine($"  no map {mapId} on this cartridge");
            return;
        }

        Console.WriteLine($"  {map.Name}, {map.Objects.Count(o => o.HasScript)} of {map.Objects.Count} with a script");

        foreach (MapObject person in map.Objects.Where(o => o.HasScript))
        {
            ScriptRun fresh = ScriptRunner.Run(rom, person.ScriptAddress);

            Console.WriteLine();
            Console.WriteLine($"  person {person.LocalId} at ({person.X}, {person.Y}), script 0x{person.ScriptAddress:X8}");

            Describe("    on a fresh save", fresh);

            // Where it gave up, which is almost never inside the script on the map. A
            // pointer into the cartridge is recognisable on sight — xx xx xx 08 — and
            // that is how every wrong argument length in milestone 14 was found.
            if (fresh.StoppedAtOffset is { } stop)
            {
                for (int row = 0; row < 2; row++)
                {
                    int from = Math.Max(0, stop - 8) + row * 16;
                    if (from + 16 > rom.Length) break;

                    string hex = string.Join(" ", Enumerable.Range(0, 16).Select(i => $"{rom.ReadU8(from + i):X2}"));
                    Console.WriteLine($"        {Rom.BaseAddress + (uint)from:X8}  {hex}");
                }
            }

            // What this person's own script turns on, plus their fight having happened.
            // Anything else would be a guess about what the rest of the world had done
            // first.
            var later = new ScriptState(fresh.FlagsSet);

            if (fresh.TrainerId is { } fought) later.MarkBeaten(fought);

            if (later.Flags.Count == 0 && later.Beaten.Count == 0) continue;

            string[] since =
            [
                .. later.Beaten.Select(t => $"trainer {t} beaten"),
                .. later.Flags.Select(f => $"flag 0x{f:X} set"),
            ];

            Describe($"    with {string.Join(", ", since)}", ScriptRunner.Run(rom, person.ScriptAddress, later));
        }

        static void Describe(string heading, ScriptRun run)
        {
            Console.WriteLine(heading);

            if (run.TrainerId is { } trainer) Console.WriteLine($"      fights trainer {trainer}");

            if (run.Stock.Count > 0) Console.WriteLine($"      opens a shop of {run.Stock.Count} things");

            foreach (int flag in run.FlagsSet) Console.WriteLine($"      sets flag 0x{flag:X}");
            foreach (int flag in run.FlagsCleared) Console.WriteLine($"      clears flag 0x{flag:X}");

            foreach ((int id, int value) in run.VariablesWritten)
                Console.WriteLine($"      writes {value} to variable 0x{id:X}");

            foreach (string page in run.Pages)
                Console.WriteLine($"      \"{GameText.ToAscii(page).Replace("\n", " ")}\"");

            if (run.StoppedAt is { } stopper) Console.WriteLine($"      stopped at 0x{stopper:X2}");

            if (run.IsEmpty && run.StoppedAt is null) Console.WriteLine("      nothing at all");
        }
    }

    /// <summary>
    /// How many conversations on this cartridge stop mid-sentence, and at what.
    /// <para>
    /// <c>--scripts</c> counts what stops a <em>read</em>, which follows both arms of
    /// every conditional and so answers a question nobody asks any more. This counts
    /// what stops a <em>run</em>: one path, from a fresh save, which is what a player
    /// standing in front of somebody actually gets. The two numbers are not the same
    /// and the second one is the one that matters.
    /// </para>
    /// </summary>
    private static void WriteRunHistogram(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("Running every script on the cartridge");

        MapLibrary library = MapLibrary.Open(rom);

        var counts = new Dictionary<byte, int>();
        var examples = new Dictionary<byte, List<uint>>();

        int people = 0;
        int silent = 0;
        int finished = 0;
        int pages = 0;
        int shops = 0;
        int fights = 0;

        foreach (LoadedMap map in library.All())
        {
            // People, and what the map itself runs on arrival. The fifth list was outside
            // this count for as long as it was unread, which is the same thing as saying
            // the wrongness detector could not see the part of the cartridge the story
            // is carried in.
            IEnumerable<uint> addresses =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
            ];

            foreach (uint address in addresses)
            {
                people++;

                ScriptRun run = ScriptRunner.Run(rom, address);

                if (run.StoppedAt is { } code)
                {
                    counts[code] = counts.GetValueOrDefault(code) + 1;

                    List<uint> where = examples.TryGetValue(code, out List<uint>? seen) ? seen : examples[code] = [];

                    // Three addresses is enough to read the bytes at; the count above is
                    // the number that says how much it matters.
                    if (where.Count < 3) where.Add(address);

                    continue;
                }

                finished++;
                if (run.IsEmpty) silent++;

                pages += run.Pages.Count;
                shops += run.Stock.Count > 0 ? 1 : 0;
                fights += run.TrainerId is not null ? 1 : 0;
            }
        }

        Console.WriteLine($"  {people} scripts on people and doorways");
        Console.WriteLine($"  {finished} run to a proper end, {people - finished} stop somewhere");
        Console.WriteLine($"  {silent} of those that finish do nothing at all — no line, no shop, no fight");

        // What actually comes out, which is a far sharper measure than whether a read
        // ended. A wrong argument width resumes inside an argument and every command
        // after it is invented, so the pages it produces change even when the count of
        // clean endings does not.
        Console.WriteLine($"  {pages} pages of dialogue, {shops} shops, {fights} fights");

        foreach ((byte code, int count) in counts.OrderByDescending(e => e.Value))
        {
            Console.WriteLine(
                $"    0x{code:X2}  stops {count}  e.g. " +
                string.Join(", ", examples[code].Select(a => $"0x{a:X8}")));
        }
    }

    /// <summary>
    /// Which <c>special</c> routines the people on this cartridge call, and where.
    /// <para>
    /// A Pokémon Centre heals through one of these. The command carries a routine number
    /// and nothing else — the routine itself is code, not data, so there is no table to
    /// read and no name anywhere in the image. Picking a number out of memory is exactly
    /// what cost milestone 14 three rounds and milestone 15 a whole commit.
    /// </para>
    /// <para>
    /// What can be derived is where each one is used and who uses it. The map name was
    /// the first idea and it does not work: an indoor map takes its <em>town's</em> name
    /// from the region table, so every centre in the game is called CELADON CITY or
    /// CERULEAN CITY and none of them is called a centre.
    /// </para>
    /// <para>
    /// What does work is asking the caller what they say. A nurse names the place she is
    /// standing in, in a line this project has been able to decode since milestone 14 —
    /// so the routine whose callers say "welcome to our centre" is the healer, on the
    /// cartridge's own authority rather than anybody's memory.
    /// </para>
    /// </summary>
    /// <summary>
    /// One routine, and what the script says on each side of the answer.
    /// <para>
    /// What a special does cannot be read. What the script does about each answer can be,
    /// and the words on the two arms are the cartridge's own — which is evidence, in a way
    /// that recalling another game is not. This project named <c>giveitem</c> from the
    /// shape of what surrounded it and the obstacle family from move ids looked up in the
    /// game's own move table. This is the same move, applied to the one thing left.
    /// </para>
    /// </summary>
    private static void WriteSpecial(Rom rom, int routine, int sites = 12)
    {
        MapLibrary library = MapLibrary.Open(rom);

        WriteForks(
            rom,
            $"special 0x{routine:X4} — what the script says on each side of the answer",
            [.. SpecialCalls.All(rom, library).Where(c => c.Routine == routine)],
            sites);
    }

    /// <summary>
    /// The same reading for an ordinary command that answers into the result variable.
    /// <para>
    /// Written because one of them, 0xA0, is why a special was misidentified this
    /// milestone: it answers between the call and the compare, so the compare is about
    /// it. Being able to ask a command the same question as a routine is what turns that
    /// from an embarrassment into an instrument.
    /// </para>
    /// </summary>
    private static void WriteAnswers(Rom rom, byte code, int sites = 16)
    {
        MapLibrary library = MapLibrary.Open(rom);

        WriteForks(
            rom,
            $"command 0x{code:X2} ({ScriptCommands.NameOf(code)}) — what the script says on each side of its answer",
            SpecialCalls.AllOf(rom, library, code),
            sites);
    }

    /// <summary>
    /// Every command with a test of the result variable immediately after it, ranked by
    /// how readable the fork is.
    /// <para>
    /// <b>Followed by, not answered by.</b> A conditional jump answers nothing and turns
    /// up here anyway, because a test can legitimately sit after one. Calling the column
    /// "commands whose answer is branched on" claims more than the instrument can see —
    /// which is the same mistake as crediting a special with the reply of the command
    /// that ran between it and the compare, made in a heading instead of a sentence.
    /// </para>
    /// <para>
    /// The sweep rather than one guess at a time. A fork whose two arms both speak is the
    /// strongest shape this project has: the cartridge saying, in its own words, what it
    /// thinks the difference is. That is how 0xA0 was read, and it is worth asking which
    /// other commands are sitting in front of one.
    /// </para>
    /// <para>
    /// Ranked by *speaking* sites and not by call count on purpose. A command called four
    /// hundred times whose arms never say anything is unreadable by this instrument, and
    /// a command called four times with eight lines of dialogue around it is not.
    /// </para>
    /// </summary>
    private static void WriteAnswerSweep(Rom rom, int top = 16)
    {
        Console.WriteLine();
        Console.WriteLine("Commands followed immediately by a test of the result variable");
        Console.WriteLine("  Followed by, which is not the same as answered by. A conditional jump does not");
        Console.WriteLine("  answer anything; it is here because a test legitimately sits after it. What the");
        Console.WriteLine("  words on the two arms are evidence about is whatever last wrote that variable.");

        MapLibrary library = MapLibrary.Open(rom);

        var ranked = new List<(byte Code, int Sites, int Speaking, string Example)>();

        // One pass over the world, not one per opcode. Opening a map decompresses and
        // renders it, so asking for all of them 256 times over is the difference between
        // a few seconds and an afternoon.
        foreach ((byte code, List<SpecialCall> calls) in SpecialCalls.Sweep(rom, library))
        {
            List<Branch> forks = [.. calls.SelectMany(c => c.Branches)];
            if (forks.Count == 0) continue;

            int speaking = 0;
            string example = "";

            foreach (Branch fork in forks)
            {
                string said = Says(rom, fork.Taken);
                string other = Says(rom, fork.NotTaken);

                if (said.Length == 0 || other.Length == 0) continue;

                speaking++;

                if (example.Length == 0) example = $"{said}  /  {other}";
            }

            ranked.Add((code, forks.Count, speaking, example));
        }

        foreach ((byte code, int sites, int speaking, string example) in ranked
                     .OrderByDescending(r => r.Speaking)
                     .ThenByDescending(r => r.Sites)
                     .Take(top))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  0x{code:X2} {ScriptCommands.NameOf(code),-16} {sites,4} forks, " +
                $"{speaking,3} with words on both arms");

            if (example.Length > 0) Console.WriteLine($"       {example}");
        }
    }

    /// <summary>The first thing an arm says, or nothing when it says nothing.</summary>
    private static string Says(Rom rom, uint address)
    {
        if (address == 0 || rom.ToOffsetOrNull(address) is null) return "";

        ScriptRun run = ScriptRunner.Run(rom, address);

        return run.Pages.Count == 0
            ? ""
            : $"\"{Shorten(GameText.ToAscii(run.Pages[0]).Replace('\n', ' '))}\"";
    }

    private static string Shorten(string line, int most = 44) =>
        line.Length <= most ? line : line[..most] + "...";

    private static void WriteForks(Rom rom, string title, List<SpecialCall> calls, int sites)
    {
        Console.WriteLine();
        Console.WriteLine(title);

        if (calls.Count == 0)
        {
            Console.WriteLine("  nothing on any map calls it");
            return;
        }

        // What the arm says if it says anything, and what it does if it does not. Plenty
        // of these forks are between two silences, and a report that only quotes speech
        // reports nothing about them at all.
        string First(uint address)
        {
            if (address == 0 || rom.ToOffsetOrNull(address) is null) return "(nowhere)";

            ScriptRun run = ScriptRunner.Run(rom, address);

            if (run.Pages.Count > 0)
                return $"\"{GameText.ToAscii(run.Pages[0]).Replace('\n', ' ')}\"";

            List<ScriptCommand> commands = ScriptReader.Read(rom, address);

            return commands.Count == 0
                ? "(nothing readable)"
                : string.Join(", ", commands.Take(4).Select(c => ScriptCommands.NameOf(c.Code)));
        }

        int shown = 0;

        foreach (SpecialCall call in calls.Where(c => c.Branches.Count > 0))
        {
            foreach (Branch fork in call.Branches)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"  {call.MapId} {call.What}" +
                    (call.Arguments.Count == 0
                        ? ""
                        : "  preceded by " + string.Join(", ", call.Arguments.Select(a => $"0x{a.Variable:X4}={a.Value}"))));

                Console.WriteLine($"    answer is {fork.Value}  -> {First(fork.Taken)}");
                Console.WriteLine($"    otherwise      -> {First(fork.NotTaken)}");

                if (++shown >= sites) return;
            }
        }

        if (shown == 0) Console.WriteLine("  called, but never branched on — it does something rather than answering");
    }

    /// <summary>
    /// Which routines the scripts on a few named maps call.
    /// <para>
    /// Written to size a stretch of the story rather than the whole game. "How much work
    /// is the opening" is answerable — it is the set of routines the opening's scripts
    /// call, and that is a list rather than an impression.
    /// </para>
    /// </summary>
    private static void WriteSpecialsOn(Rom rom, string maps)
    {
        string[] wanted = maps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Console.WriteLine();
        Console.WriteLine($"Routines called by scripts on {string.Join(", ", wanted)}");

        MapLibrary library = MapLibrary.Open(rom);

        List<SpecialCall> here =
            [.. SpecialCalls.All(rom, library).Where(c => wanted.Contains(c.MapId))];

        if (here.Count == 0)
        {
            Console.WriteLine("  nothing on those maps calls anything");
            return;
        }

        List<SpecialCall> everywhere = SpecialCalls.All(rom, library);

        Console.WriteLine(
            $"  {here.Count} calls to {here.Select(c => c.Routine).Distinct().Count()} routines");

        foreach (var routine in here
                     .GroupBy(c => c.Routine)
                     .OrderByDescending(g => g.Count()))
        {
            int elsewhere = everywhere.Count(c => c.Routine == routine.Key);

            Console.WriteLine(
                $"    0x{routine.Key:X4}  {routine.Count(),3} here, {elsewhere,4} in the whole game  " +
                $"{(elsewhere == routine.Count() ? "— only ever called here" : "")}");
        }
    }

    /// <summary>
    /// The calls into the game's own code, and the shape of what is asked of each.
    /// <para>
    /// This is the boundary. What a special routine does is ARM code, not data, and no
    /// amount of reading an image will say — everything else in this project was
    /// somewhere in the file waiting to be found, and this is not.
    /// </para>
    /// <para>
    /// The shape of the expectation is readable, though, and it is the useful thing: how
    /// many arguments a routine is given and in which slots, whether its answer is ever
    /// looked at, and which answers the scripts actually distinguish. That is the
    /// specification a hand-written stand-in has to meet. It is not what the routine
    /// does, but unlike a guess it can be checked.
    /// </para>
    /// <para>
    /// An earlier version of this report quoted "the first caller who says anything"
    /// beside each routine and put the same wireless-club attendant next to six of them.
    /// A degenerate answer given confidently is worse than no answer.
    /// </para>
    /// </summary>
    private static void WriteSpecials(Rom rom, int top = 24)
    {
        Console.WriteLine();
        Console.WriteLine("special routines, by what the scripts around them expect");

        MapLibrary library = MapLibrary.Open(rom);

        List<SpecialCall> calls = SpecialCalls.All(rom, library);
        List<SpecialCalls.Profile> profiles = SpecialCalls.Profiles(calls);

        Console.WriteLine(
            $"  {calls.Count} calls to {profiles.Count} different routines; " +
            $"{profiles.Count(p => p.Answers)} of them are asked a question, " +
            $"{profiles.Count(p => p.ArgumentSlots.Count > 0)} are given arguments");

        // The number that matters. Nothing calls these routines, so the answer variable
        // keeps its zero — and a zero is an answer, not an absence. Every site where the
        // script says "if the answer is zero, skip this" is a piece of the game being
        // skipped right now, quietly, on a technicality.
        Console.WriteLine(
            $"  {profiles.Count(p => p.ZeroIsMisleading)} routines branch away on the zero they " +
            $"are getting by default, at " +
            $"{profiles.Sum(p => p.BranchesTakenByZero)} of {profiles.Sum(p => p.Branches)} branching sites");

        Console.WriteLine();

        foreach (SpecialCalls.Profile profile in profiles.Take(top))
            Console.WriteLine($"    {profile}");

        // The ones that gate the beginning of the game, named rather than left to be
        // found: whatever stops a player leaving the first town is the first routine
        // worth standing in for.
        Console.WriteLine();
        Console.WriteLine("  Called from a square you walk onto, which is where the story is:");

        foreach (SpecialCalls.Profile profile in profiles
                     .Where(p => calls.Any(c => c.Routine == p.Routine && c.What.StartsWith("trigger")))
                     .Take(10))
        {
            SpecialCall example = calls.First(c => c.Routine == profile.Routine && c.What.StartsWith("trigger"));

            Console.WriteLine($"    {profile}");
            Console.WriteLine($"      e.g. {example.MapId} {example.What}");
        }
    }

    private static void WriteMovements(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("Movement lists");

        MapLibrary library = MapLibrary.Open(rom);

        List<MovementList> lists = MovementLists.All(rom, library);

        if (lists.Count == 0)
        {
            Console.WriteLine("  none — no script on any map applies one");
            return;
        }

        Console.WriteLine(
            $"  {lists.Count} lists across {lists.Select(l => l.MapId).Distinct().Count()} maps, " +
            $"{lists.Count(l => l.IsPlayer)} of them applied to person 0x{MovementList.Player:X2}");

        Console.WriteLine($"  longest is {lists.Max(l => l.Steps.Length)} steps");

        // Is 0xFE really what ends these? Asked without assuming it: how many of the
        // pointers have a given byte anywhere within reach. A byte that ends every list
        // is near every pointer, and nothing else has any reason to be.
        List<(byte Byte, int Within)> terminators =
            MovementLists.Terminators(rom, lists.Select(l => l.Address));

        Console.WriteLine();
        Console.WriteLine("  Bytes found within reach of a movement pointer, commonest first:");

        foreach ((byte value, int count) in terminators.Take(4))
            Console.WriteLine($"    0x{value:X2}  {count,4} of {lists.Select(l => l.Address).Distinct().Count()} pointers");

        Console.WriteLine();
        Console.WriteLine("  Step bytes, commonest first:");

        foreach ((byte step, int count) in MovementLists.Histogram(lists).Take(16))
            Console.WriteLine($"    0x{step:X2}  {count,5}");

        // The oracle: a cutscene walks people over squares people can stand on. Only
        // people, never the player — where the player is standing when a scene starts is
        // not a fact about the cartridge.
        Dictionary<string, LoadedMap> maps = library.All()
            .ToDictionary(m => WorldExporter.MapId(m.Bank, m.Number));

        foreach (byte family in (byte[])[0x10, 0x1C, 0x08])
        {
            List<MovementLists.Reading> readings = MovementLists.Derive(lists, maps, family);

            if (readings.Count == 0 || readings[0].Paths == 0) continue;

            Console.WriteLine();
            Console.WriteLine($"  Reading 0x{family:X2}..0x{family + 3:X2} as steps, best four of twenty-four:");

            foreach (MovementLists.Reading reading in readings.Take(4))
                Console.WriteLine($"    {reading}");
        }

        List<MovementLists.Reading> joint =
            MovementLists.DeriveJoint(lists, maps, [0x08, 0x10, 0x1C]);

        if (joint.Count > 0 && joint[0].Paths > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  All three families at once, one ordering shared, best six of twenty-four:");

            foreach (MovementLists.Reading reading in joint.Take(6))
                Console.WriteLine($"    {reading}");
        }

        List<MovementLists.Reading> fromTriggers =
            MovementLists.DeriveFromTriggers(lists, maps, [0x08, 0x10, 0x1C]);

        if (fromTriggers.Count > 0 && fromTriggers[0].Paths > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  The player's own lists, walked from the trigger square that started them:");

            foreach (MovementLists.Reading reading in fromTriggers.Take(4))
                Console.WriteLine($"    {reading}");
        }

        // The verdict, stated rather than left to be eyeballed. Two samples that share
        // no scripts, no maps and no starting squares; the answer is whichever ordering
        // is top of both, and there is no answer at all unless exactly one is.
        if (joint.Count > 0 && fromTriggers.Count > 0 && joint[0].Paths > 0 && fromTriggers[0].Paths > 0)
        {
            double bestJoint = joint[0].Share;
            double bestPlayer = fromTriggers[0].Share;

            List<string> agreed =
            [
                .. joint.Where(r => r.Share >= bestJoint - 0.0001)
                    .Select(r => string.Join(",", r.Directions))
                    .Intersect(fromTriggers
                        .Where(r => r.Share >= bestPlayer - 0.0001)
                        .Select(r => string.Join(",", r.Directions))),
            ];

            Console.WriteLine();
            Console.WriteLine(agreed.Count == 1
                ? $"  Both samples put one ordering first and it is the same one: {agreed[0].ToLowerInvariant()}"
                : $"  undecided — {agreed.Count} orderings are top of both samples");
        }

        Console.WriteLine();
        Console.WriteLine("  A few lists as they are:");

        foreach (MovementList list in lists.Take(8))
        {
            Console.WriteLine(
                $"    {list.MapId,-8} person 0x{list.PersonId:X2} 0x{list.Address:X8}  " +
                string.Join(" ", list.Steps.Select(s => $"{s:X2}")));
        }
    }

    /// <summary>
    /// The shape of the four event lists, scored against the whole cartridge.
    /// <para>
    /// Two of them have been read since the beginning and their answers are known, which
    /// is what makes this trustworthy rather than merely plausible: the same scan is run
    /// over the people and the warps, and if it does not rediscover 24 and 8 then it is
    /// not a scan worth believing about the two lists nobody has read.
    /// </para>
    /// </summary>
    private static void WriteEventShapes(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("Event lists, scored by record size and where the script pointer sits");

        MapBankTable banks = MapBankLocator.Locate(rom, Console.WriteLine)
            ?? throw new InvalidDataException("No map bank table found.");

        List<(int Width, int Height, uint EventsPointer)> maps =
        [
            .. banks.AllMaps.Select(m => (m.Header.Layout.Width, m.Header.Layout.Height, m.Header.EventsPointer)),
        ];

        (string Name, int List)[] lists =
        [
            ("people   (known: 24 bytes)", EventLayout.People),
            ("warps    (known:  8 bytes)", EventLayout.Warps),
            ("triggers (never read)", EventLayout.Triggers),
            ("signs    (never read)", EventLayout.Signs),
        ];

        foreach ((string name, int list) in lists)
        {
            Console.WriteLine();
            Console.WriteLine($"  {name}");

            List<EventShape> scored = EventLayout.Derive(rom, maps, list);

            if (scored.Count == 0)
            {
                Console.WriteLine("    nothing scored — no map has any of these");
                continue;
            }

            foreach (EventShape shape in scored.Take(5)) Console.WriteLine($"    {shape}");

            if (list is not (EventLayout.Triggers or EventLayout.Signs)) continue;

            // What the misses have in common. A share short of a hundred per cent is a
            // question rather than an answer: either the shape is wrong, or the rest are
            // a second kind of record sharing one list.
            List<(EventLayout.Miss Why, byte Kind)> explained =
                EventLayout.Explain(rom, maps, list, scored[0], kindOffset: 5);

            Console.WriteLine(
                "      why: " +
                string.Join(", ", explained
                    .GroupBy(e => e.Why)
                    .OrderByDescending(g => g.Count())
                    .Select(g => $"{g.Key} {g.Count()}")));

            Console.WriteLine(
                "      byte at +5, by outcome: " +
                string.Join("; ", explained
                    .GroupBy(e => e.Why == EventLayout.Miss.Fine ? "read" : "missed")
                    .OrderBy(g => g.Key)
                    .Select(g =>
                        $"{g.Key} -> " +
                        string.Join(" ", g.GroupBy(e => e.Kind).OrderBy(k => k.Key).Select(k => $"{k.Key}x{k.Count()}")))));
        }

        // And then the point of all of it: things nobody in this game has ever been able
        // to read. A derivation that stops at a percentage has not been checked.
        Console.WriteLine();
        Console.WriteLine("  What some of them say:");

        int shown = 0;

        foreach (LoadedMap map in MapLibrary.Open(rom).All())
        {
            foreach (MapSign sign in map.Signs.Where(s => s.HasScript))
            {
                ScriptRun run = ScriptRunner.Run(rom, sign.ScriptAddress);

                if (run.Pages.Count == 0) continue;

                Console.WriteLine(
                    $"    {map.Name,-20} ({sign.X,3},{sign.Y,3})  " +
                    $"\"{GameText.ToAscii(run.Pages[0]).Replace('\n', ' ')}\"");

                if (++shown >= 10) return;
            }
        }
    }

    /// <summary>
    /// Where an item is handed over, and by whom.
    /// <para>
    /// Written for one question — "can a player actually get an HM in this world, or is
    /// every field move unreachable?" — and it is the sort of question that comes up
    /// once a feature exists. A count of two hundred cut trees means nothing if nobody
    /// in the game will give you CUT.
    /// </para>
    /// </summary>
    private static void WriteWhoGives(Rom rom, int itemId)
    {
        Console.WriteLine();
        Console.WriteLine(itemId == 0
            ? "Everywhere something is handed over"
            : $"Everywhere item {itemId} is handed over");

        List<ItemRecord> items = ItemTable.Locate(rom) is { } at ? ItemTable.Read(rom, at) : [];

        string NameOf(int id) => items.FirstOrDefault(i => i.Id == id)?.Name ?? $"item {id}";

        int shown = 0;

        foreach (LoadedMap map in MapLibrary.Open(rom).All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
            {
                // Read rather than run: what somebody hands over is a fact about them,
                // and half the game's gifts sit behind a flag a fresh save has not set.
                //
                // Both ways of handing something over. There is the giveitem command,
                // and there is the older shape a ball on the ground uses: write the item
                // into 0x8000 and the count into 0x8001, then call a standard routine.
                // Looking for only the first finds a hundred and seventy fewer things,
                // and every HM in the game is handed over the second way.
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, person.ScriptAddress))
                {
                    int given = command.Code switch
                    {
                        0x46 => command.Word(),
                        0x1A when command.Word() == 0x8000 => command.Word(2),
                        _ => 0,
                    };

                    if (given == 0) continue;
                    if (itemId != 0 && given != itemId) continue;

                    Console.WriteLine(
                        $"  {WorldExporter.MapId(map.Bank, map.Number),-8} {map.Name,-24} " +
                        $"person {person.LocalId,-3} {NameOf(given)}");

                    shown++;
                }
            }
        }

        if (shown == 0) Console.WriteLine("  nowhere — nobody in this world hands that over");
    }

    private static void WriteWhoSays(Rom rom, string needle, int top = 12)
    {
        Console.WriteLine();
        Console.WriteLine($"People who say \"{needle}\"");

        MapLibrary library = MapLibrary.Open(rom);

        int found = 0;

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
            {
                ScriptRun run = ScriptRunner.Run(rom, person.ScriptAddress);

                string? page = run.Pages
                    .FirstOrDefault(p => GameText.ToAscii(p).Contains(needle, StringComparison.OrdinalIgnoreCase));

                if (page is null) continue;

                found++;

                if (found > top) continue;

                Console.WriteLine();
                Console.WriteLine(
                    $"  {WorldExporter.MapId(map.Bank, map.Number)} {map.Name}, person {person.LocalId} " +
                    $"at ({person.X}, {person.Y}), script 0x{person.ScriptAddress:X8}");

                Console.WriteLine($"    \"{GameText.ToAscii(page).Replace("\n", " ")}\"");

                int[] routines =
                [
                    .. ScriptReader.ReadAll(rom, person.ScriptAddress)
                        .Select(c => c.Code switch { 0x25 => c.Word(), 0x26 => c.Word(2), _ => -1 })
                        .Where(r => r >= 0)
                        .Distinct(),
                ];

                Console.WriteLine(
                    routines.Length == 0
                        ? "    calls no special routine at all"
                        : $"    calls {string.Join(", ", routines.Select(r => $"0x{r:X4}"))}");

                if (run.StoppedAt is { } stopper) Console.WriteLine($"    the run stopped at 0x{stopper:X2}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(found > top ? $"  {found} in all, {found - top} not shown" : $"  {found} in all");
    }

    /// <summary>
    /// Everything reachable from one address, decoded and in hex.
    /// <para>
    /// The map-based dumps can only show a script somebody is standing on, and almost
    /// nobody in FireRed does their own work. The Pokémon Centre nurse is five bytes:
    /// lock, faceplayer, <c>call 0x081A6578</c>, release, end — every nurse in the game
    /// is that same call, and what actually heals a party is at the other end of it,
    /// where no map object points.
    /// </para>
    /// <para>
    /// Prints each script it reaches whole: the instructions as this project reads them,
    /// and the bytes they were read from. A pointer into the cartridge is recognisable
    /// on sight — <c>xx xx xx 08</c> — which is how every wrong argument length so far
    /// has been found.
    /// </para>
    /// </summary>
    private static void WriteScriptAt(Rom rom, uint address, int maxScripts = 12)
    {
        Console.WriteLine();
        Console.WriteLine($"Everything reachable from 0x{address:X8}");

        var seen = new HashSet<uint>();
        var queue = new Queue<uint>();

        queue.Enqueue(address);
        seen.Add(address);

        while (queue.Count > 0 && seen.Count <= maxScripts)
        {
            uint at = queue.Dequeue();

            Console.WriteLine();
            Console.WriteLine($"  0x{at:X8}");

            List<ScriptCommand> commands = ScriptReader.Read(rom, at);

            foreach (ScriptCommand command in commands)
            {
                string arguments = string.Join(" ", command.Arguments.Select(b => $"{b:X2}"));

                Console.WriteLine(
                    $"    0x{command.Offset:X6}  {ScriptCommands.NameOf(command.Code),-14} {arguments}");

                uint target = command.Code switch
                {
                    ScriptCommands.Call or ScriptCommands.Goto => command.Pointer(),
                    ScriptCommands.CallIf or ScriptCommands.GotoIf => command.Pointer(1),
                    _ => 0,
                };

                if (target == 0 || !rom.IsRomAddress(target)) continue;
                if (!seen.Add(target)) continue;

                queue.Enqueue(target);
            }

            if (ScriptReader.StoppedAt(rom, at) is { } stopper)
                Console.WriteLine($"    stopped at 0x{stopper:X2}");

            if (rom.ToOffsetOrNull(at) is not { } start) continue;

            for (int row = 0; row < 3; row++)
            {
                int from = start + row * 16;
                if (from + 16 > rom.Length) break;

                string hex = string.Join(" ", Enumerable.Range(0, 16).Select(i => $"{rom.ReadU8(from + i):X2}"));
                Console.WriteLine($"      {Rom.BaseAddress + (uint)from:X8}  {hex}");
            }
        }
    }

    /// <summary>
    /// The addresses map objects hand their work to, and how many of them do.
    /// <para>
    /// A different question from <c>--specials</c>, and a better one. What a special
    /// routine <em>does</em> is code this project cannot see and will not guess at; what
    /// a shared script <em>is</em> can be counted. Every Pokémon Centre nurse in the game
    /// is <c>call 0x081A6578</c> and nothing else, so the routine called by exactly one
    /// person on each of nineteen maps is the nurse — on the cartridge's own arithmetic,
    /// with no English in it and no number remembered from anywhere.
    /// </para>
    /// <para>
    /// This is the shape milestone 0 settled on for the tables: locate it by what it
    /// looks like, print the evidence, and hardcode nothing.
    /// </para>
    /// </summary>
    private static void WriteSharedScripts(Rom rom, int top = 20)
    {
        Console.WriteLine();
        Console.WriteLine("Scripts that map objects hand their work to");

        MapLibrary library = MapLibrary.Open(rom);

        var people = new Dictionary<uint, int>();
        var maps = new Dictionary<uint, HashSet<string>>();
        var says = new Dictionary<uint, Dictionary<string, int>>();

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
            {
                // Only what this person hands off directly. Following further would
                // count every helper the helper uses and drown the one-per-town shape
                // this is looking for.
                foreach (ScriptCommand command in ScriptReader.Read(rom, person.ScriptAddress))
                {
                    uint target = command.Code switch
                    {
                        ScriptCommands.Call or ScriptCommands.Goto => command.Pointer(),
                        ScriptCommands.CallIf or ScriptCommands.GotoIf => command.Pointer(1),
                        _ => 0,
                    };

                    if (target == 0 || !rom.IsRomAddress(target)) continue;

                    people[target] = people.GetValueOrDefault(target) + 1;

                    if (!maps.TryGetValue(target, out HashSet<string>? on)) maps[target] = on = [];

                    on.Add(map.Name);

                    if (ScriptRunner.Run(rom, person.ScriptAddress).Pages.FirstOrDefault() is not { } opening) continue;
                    if (opening.Trim().Length == 0) continue;

                    if (!says.TryGetValue(target, out Dictionary<string, int>? lines)) says[target] = lines = [];

                    lines[opening] = lines.GetValueOrDefault(opening) + 1;
                }
            }
        }

        Console.WriteLine($"  {people.Count} shared scripts, called from {library.Count} maps");

        foreach ((uint target, HashSet<string> on) in maps.OrderByDescending(e => e.Value.Count).Take(top))
        {
            Console.WriteLine($"    0x{target:X8}  {on.Count} maps, {people[target]} callers");

            if (!says.TryGetValue(target, out Dictionary<string, int>? lines)) continue;

            foreach ((string opening, int said) in lines.OrderByDescending(e => e.Value).Take(1))
            {
                string line = GameText.ToAscii(opening).Replace("\n", " ");

                Console.WriteLine($"            {said}x  \"{(line.Length > 84 ? line[..84] + "..." : line)}\"");
            }
        }
    }

    /// <summary>
    /// Looks for the cartridge's lettering and writes what it finds as PNGs to look at.
    /// <para>
    /// The one part of this client that does not come off the player's own image is the
    /// part they noticed. Every menu draws with the graphics library's own font on
    /// rectangles this project picked the colours of, and it shows next to a map and a
    /// walking figure that are the real thing.
    /// </para>
    /// <para>
    /// A font has no header and no table pointing at it, so this finds candidates by
    /// shape and hands them over as pictures. Whether something is an alphabet is a
    /// question a person answers instantly and a heuristic answers badly, so it is not
    /// asked here.
    /// </para>
    /// </summary>
    private static void WriteGlyphCandidates(Rom rom, string directory)
    {
        Console.WriteLine();
        Console.WriteLine("Looking for lettering");

        List<GlyphRun> runs = GlyphScanner.Scan(rom);

        if (runs.Count == 0)
        {
            Console.WriteLine("  nothing on this image reads as lettering");
            return;
        }

        string into = Path.Combine(directory, "glyphs");
        Directory.CreateDirectory(into);

        foreach (GlyphRun run in runs)
        {
            string name = $"{run.Address:X8}-{run.Tiles}.png";

            File.WriteAllBytes(Path.Combine(into, name), GlyphScanner.Sheet(rom, run));

            Console.WriteLine(
                $"  0x{run.Address:X8}  {run.Tiles,5} tiles  {run.Colours} colours  " +
                $"{run.Ink:P0} ink   glyphs/{name}");
        }

        Console.WriteLine();
        Console.WriteLine("  Open those. One of them is the alphabet; the rest are a lesson in what");
        Console.WriteLine("  else on a cartridge looks like writing.");
    }

    /// <summary>
    /// One address, drawn as a numbered sheet, so somebody can say where a letter is.
    /// <para>
    /// Finding lettering turned out to be the easy half. A block at 0x08231A60 holds
    /// kana and a clean Latin lowercase alphabet and no capitals or digits at all, and
    /// treating a character's code as its tile index renders lowercase in nearly the
    /// right order with nonsense on both sides. So the mapping from code to glyph is not
    /// identity, and guessing at it from a thumbnail is how an afternoon goes missing.
    /// </para>
    /// <para>
    /// This asks instead. The rows are counted in dots down the margin; one person
    /// saying "the capital A is row six, third along" fixes the whole mapping, and no
    /// heuristic here is going to do better.
    /// </para>
    /// </summary>
    private static void WriteFontSheet(Rom rom, uint address, string directory, int tiles = 256)
    {
        Console.WriteLine();
        Console.WriteLine($"The sheet at 0x{address:X8}");

        if (rom.ToOffsetOrNull(address) is not { } offset)
        {
            Console.WriteLine("  that address is not on this cartridge");
            return;
        }

        Directory.CreateDirectory(Path.Combine(directory, "glyphs"));

        string name = $"sheet-{address:X8}.png";

        File.WriteAllBytes(
            Path.Combine(directory, "glyphs", name),
            GlyphScanner.NumberedSheet(rom, offset, tiles));

        Console.WriteLine($"  {tiles} tiles, 16 across, rows counted in dots down the left");
        Console.WriteLine($"  glyphs/{name}");
        Console.WriteLine();
        Console.WriteLine("  Row n holds tiles n*16 to n*16+15. If a capital A is in there, saying");
        Console.WriteLine("  which row and how far along fixes the mapping for every other letter.");
    }

    /// <summary>
    /// Why a conversation that read perfectly says nothing at all.
    /// <para>
    /// The run histogram counts the scripts that stop somewhere. It also turned up a
    /// bigger number that nobody had asked about: 170 people whose script runs to a
    /// proper end and produces no line, no shop and no fight. A script that stops is
    /// visibly broken. A script that finishes empty is the failure that looks like
    /// success, and one number covering all of them is no use — some of those people
    /// are rocks.
    /// </para>
    /// <para>
    /// So this splits the number. The interesting causes are the ones where something
    /// was found and thrown away: a text pointer this project decided was not text, or a
    /// handoff to a standard script it has never been able to follow.
    /// </para>
    /// </summary>
    private static void WriteSilentPeople(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("People whose script finishes and says nothing");

        MapLibrary library = MapLibrary.Open(rom);

        var causes = new Dictionary<string, int>();
        var examples = new Dictionary<string, List<string>>();

        int people = 0;
        int silent = 0;

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
            {
                people++;

                ScriptRun run = ScriptRunner.Run(rom, person.ScriptAddress);

                if (run.GivesItem is { } spoken && run.Pages.Count > 0)
                {
                    string talks = "hands over an item AND says something";

                    causes[talks] = causes.GetValueOrDefault(talks) + 1;

                    if (!examples.TryGetValue(talks, out List<string>? chatty)) examples[talks] = chatty = [];

                    if (chatty.Count < 4)
                    {
                        chatty.Add(
                            $"{WorldExporter.MapId(map.Bank, map.Number)} person {person.LocalId}: item {spoken} — " +
                            $"\"{GameText.ToAscii(run.Pages[0]).Replace("\n", " ")}\"");
                    }

                    continue;
                }

                if (run.GivesItem is { } given)
                {
                    string handed = $"hands over an item — {given} of them so far";

                    handed = "hands over an item";

                    causes[handed] = causes.GetValueOrDefault(handed) + 1;

                    if (!examples.TryGetValue(handed, out List<string>? found)) examples[handed] = found = [];

                    if (found.Count < 4)
                    {
                        found.Add(
                            $"{WorldExporter.MapId(map.Bank, map.Number)} person {person.LocalId}: " +
                            $"item {given} x{run.GivesCount}, drawn with graphics {person.GraphicsId}");
                    }

                    continue;
                }

                if (run.StoppedAt is not null || !run.IsEmpty) continue;

                silent++;

                List<ScriptCommand> commands = [.. ScriptReader.ReadAll(rom, person.ScriptAddress)];

                // Text was found and rejected. LooksLikeDialogue is the only thing that
                // can throw a page away once a pointer has been read, so if a script
                // loaded one and nothing came out, that check is the reason.
                bool loaded = commands.Any(c =>
                    c.Code is ScriptCommands.LoadPointer or ScriptCommands.Message &&
                    rom.IsRomAddress(c.Code == ScriptCommands.Message ? c.Pointer() : c.Pointer(1)));

                // A handoff into the table of standard scripts, which this has never
                // located and so has never followed.
                bool standard = commands.Any(c => c.Code is ScriptCommands.CallStandard or 0x08 or 0x0A or 0x0B);

                // Asked the game something. Ahead of the other causes because it is the
                // one that is not this project's fault and cannot be fixed by reading
                // harder: a special is a call into code, and stepping over it leaves a
                // zero the script then reads as an answer.
                string cause =
                    run.SpecialsCalled.Count > 0
                        ? $"asked the game {run.SpecialsCalled.Count} thing(s) it cannot be asked " +
                          $"(routine 0x{run.SpecialsCalled[0]:X4})"
                    : loaded ? "carried text this project decided was not text"
                    : standard ? "handed off to a standard script, which is never followed"
                    : commands.Count <= 2 ? "genuinely empty — a sign post with nothing on it"
                    : "ran several commands, none of which this models";

                causes[cause] = causes.GetValueOrDefault(cause) + 1;

                if (!examples.TryGetValue(cause, out List<string>? where)) examples[cause] = where = [];

                if (where.Count < 3)
                    where.Add($"{WorldExporter.MapId(map.Bank, map.Number)} person {person.LocalId} 0x{person.ScriptAddress:X8}");
            }
        }

        Console.WriteLine($"  {silent} of {people} people with a script");

        foreach ((string cause, int count) in causes.OrderByDescending(e => e.Value))
        {
            Console.WriteLine($"    {count,4}  {cause}");

            foreach (string where in examples[cause]) Console.WriteLine($"          {where}");
        }
    }

    /// <summary>
    /// Works out how long an unknown command's arguments are, by trying every width.
    /// <para>
    /// Milestone 14 derived six of these by hand: print the bytes, try a width, see
    /// whether what follows parses as a sensible script and whether its pointers land on
    /// anything. That method was right and doing it by eye was the slow part — and eyes
    /// are also how a wrong width gets talked into looking plausible.
    /// </para>
    /// <para>
    /// So it is scored instead, over every place the command actually appears. A correct
    /// width leaves a run of well-formed instructions ending properly, at site after
    /// site. A wrong one resumes inside an argument, and the further it reads the worse
    /// it gets. The difference between the two is not subtle across two hundred sites,
    /// which is exactly why it is worth counting rather than reading.
    /// </para>
    /// <para>
    /// It proposes. It does not decide, and nothing here writes a length into the table —
    /// this project has twice been sure of a number it could not see.
    /// </para>
    /// </summary>
    private static void WriteDerivedLengths(Rom rom, int maxWidth = 8, int sitesPer = 200)
    {
        Console.WriteLine();
        Console.WriteLine("Argument widths, scored against every place the command appears");

        MapLibrary library = MapLibrary.Open(rom);

        // Where each unknown command actually stopped a run. Only real sites: a search
        // of the whole image for a byte would mostly find that byte inside an argument.
        var sites = new Dictionary<byte, List<int>>();

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
            {
                ScriptRun run = ScriptRunner.Run(rom, person.ScriptAddress);

                if (run.StoppedAt is not { } code || run.StoppedAtOffset is not { } at) continue;

                if (!sites.TryGetValue(code, out List<int>? where)) sites[code] = where = [];

                if (where.Count < sitesPer && !where.Contains(at)) where.Add(at);
            }
        }

        foreach ((byte code, List<int> where) in sites.OrderByDescending(e => e.Value.Count))
        {
            var scores = new List<(int Width, double Clean, double Pointers, double Depth)>();

            for (int width = 0; width <= maxWidth; width++)
            {
                int clean = 0;
                int pointers = 0;
                int landed = 0;

                int depth = 0;
                int carries = 0;

                foreach (int at in where)
                {
                    if (CarriesAPointer(rom, at + 1, width)) carries++;

                    (bool ended, int good, int total, int read) = ReadsOn(rom, at + 1 + width);

                    if (ended) clean++;

                    pointers += good;
                    landed += total;
                    depth += read;
                }

                // No pointers at all is not evidence of anything, and scoring it as
                // perfect would hand the answer to whichever width happened to avoid
                // them. It counts as nothing either way.
                scores.Add((
                    width,
                    clean / (double)where.Count,
                    landed == 0 ? 0 : pointers / (double)landed,
                    carries / (double)where.Count));
            }

            // Scored on what the argument holds, not on what follows it. What follows
            // is a trap: the correct width often lands on the *next* unknown command and
            // stops dead, while a wrong one skips over that unknown and reads on into
            // something plausible. At 0x08164D84 the right answer scores worst on every
            // continuation test, and the bytes it swallows are 01 00 E5 75 1A 08 — a
            // pointer to 0x081A75E5, sitting in plain sight.
            // The pointer test only gets to decide when it actually fires. Half the
            // sites is the bar: below that it is a coincidence being promoted over a
            // real signal, which is how 0x51 was briefly declared eight bytes wide on
            // the strength of three sites out of twenty-one.
            double top = scores.Max(s => s.Depth) >= 0.5 ? scores.Max(s => s.Depth) : 0;

            // Everything close to the top, not just the top. A single arrow claims more
            // than this evidence supports, and claiming more than the evidence supports
            // is the specific mistake this whole method exists to avoid.
            int[] shortlist = top <= 0
                ? [.. scores.Where(s => s.Clean + s.Pointers >= scores.Max(x => x.Clean + x.Pointers) - 0.1).Select(s => s.Width)]
                : [.. scores.Where(s => s.Depth >= top - 0.05).Select(s => s.Width)];

            Console.WriteLine();
            Console.WriteLine($"  0x{code:X2}  stops {where.Count} people");

            foreach ((int width, double cleanly, double pointing, double deep) in scores)
            {
                string mark = top > 0 && shortlist.Contains(width) ? " <-" : "";

                Console.WriteLine(
                    $"      {width} bytes:  {deep,5:P0} carry a real pointer, " +
                    $"{cleanly,5:P0} read on to an end, {pointing,5:P0} of those pointers land{mark}");
            }

            Console.WriteLine(
                top <= 0
                    ? "      -> undecided. No width ends on a real pointer, and the continuation " +
                      "test is not to be trusted alone: read the bytes"
                    : shortlist.Length == 1
                        ? $"      -> {shortlist[0]} bytes, ending on a pointer that lands on something real"
                        : $"      -> {string.Join(" or ", shortlist)} bytes, equally");
        }
    }

    /// <summary>
    /// True when the argument bytes hold something that is recognisably a pointer.
    /// <para>
    /// The sharpest test there is, and the one milestone 14 actually used: a pointer
    /// into a GBA cartridge is recognisable on sight, and a width that swallows one
    /// whole is a width that swallowed an argument. A width that cuts one in half is
    /// not.
    /// </para>
    /// </summary>
    private static bool CarriesAPointer(Rom rom, int from, int width)
    {
        // Ending exactly at the argument boundary, not merely sitting somewhere inside
        // it. Any width longer than the true one still contains the same pointer, with
        // extra bytes swallowed after it — so "carries one" alone cannot tell six from
        // eight, and "ends with one" can. A trailing pointer is the commonest shape a
        // command of this kind has.
        for (int at = width - 4; at >= width - 4 && at >= 0; at--)
        {
            if (from + at + 4 > rom.Length) break;

            uint candidate = rom.ReadU32(from + at);

            if (!rom.IsRomAddress(candidate)) continue;

            // It has to lead somewhere, and the bar is one recognisable command or one
            // page of speech. Demanding a whole readable script was too much: these
            // pointers often lead to more of the same unknown territory, which is the
            // whole reason the command is unknown.
            if (rom.ToOffsetOrNull(candidate) is not { } target) continue;

            if (ScriptCommands.ArgumentLength(rom.ReadU8(target), rom.ReadU8(target + 1)) is not null) return true;

            if (GameText.LooksLikeDialogue(rom.Span[target..])) return true;
        }

        return false;
    }

    /// <summary>
    /// Reads forward from an offset and says whether it goes anywhere sensible.
    /// <para>
    /// Two questions, because either alone is fooled. Ending properly is cheap to hit by
    /// accident — a stray 0x02 in the middle of a pointer is a perfectly good <c>end</c>.
    /// Pointers landing on real addresses is the harder test, and a width that satisfies
    /// both across every site is not a coincidence.
    /// </para>
    /// </summary>
    private static (bool Ended, int GoodPointers, int TotalPointers, int Read) ReadsOn(
        Rom rom, int from, int maxCommands = 12)
    {
        int offset = from;
        int good = 0;
        int total = 0;
        int read = 0;

        for (int i = 0; i < maxCommands; i++)
        {
            if (offset < 0 || offset + 1 >= rom.Length) return (false, good, total, read);

            byte code = rom.ReadU8(offset);
            byte first = rom.ReadU8(offset + 1);

            if (ScriptCommands.ArgumentLength(code, first) is not { } length) return (false, good, total, read);
            if (offset + 1 + length > rom.Length) return (false, good, total, read);

            read++;

            // Only the commands that are pointers by definition, and only the real
            // test: does the thing it points at read as a script? Counting any word
            // whose top byte is 0x08 was circular — that is the same check twice.
            uint target = code switch
            {
                ScriptCommands.Call or ScriptCommands.Goto => new ScriptCommand(
                    offset, code, rom.Slice(offset + 1, length).ToArray()).Pointer(),
                ScriptCommands.CallIf or ScriptCommands.GotoIf => new ScriptCommand(
                    offset, code, rom.Slice(offset + 1, length).ToArray()).Pointer(1),
                _ => 0,
            };

            if (target != 0)
            {
                total++;

                // Two commands and a proper ending is a low bar and a real one: a
                // pointer that resumed inside an argument lands on a byte that means
                // nothing, and reading from there stops almost immediately.
                if (rom.IsRomAddress(target) &&
                    ScriptReader.Read(rom, target) is { Count: >= 2 } landed &&
                    landed[^1].Code is ScriptCommands.End or ScriptCommands.Return or ScriptCommands.Goto)
                {
                    good++;
                }
            }

            offset += 1 + length;

            if (code is ScriptCommands.End or ScriptCommands.Return or ScriptCommands.Goto)
                return (true, good, total, read);
        }

        return (false, good, total, read);
    }

    /// <summary>
    /// The bytes after one unknown command, at every place it stops a run.
    /// <para>
    /// The oldest instrument in this project and still the one that settles things.
    /// Scoring narrows and the tie it leaves is real; what breaks it is looking at every
    /// site at once, where a constant first argument or a length that never varies shows
    /// up as a column rather than as a hunch.
    /// </para>
    /// </summary>
    private static void WriteBytesAfter(Rom rom, byte code, int width = 12)
    {
        Console.WriteLine();
        Console.WriteLine($"Every place 0x{code:X2} turns up in a read, and what follows it");

        MapLibrary library = MapLibrary.Open(rom);

        var sites = new Dictionary<int, List<string>>();

        foreach (LoadedMap map in library.All())
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            // Every script on the map, not just the people. A trigger's script is where
            // the story is, and a view that could not see one would go on answering
            // questions about the third of the cartridge it happened to look at.
            List<(string What, uint Address)> scripts =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => ($"person {o.LocalId}", o.ScriptAddress)),
                .. map.Triggers.Where(t => t.HasScript).Select(t => ($"trigger ({t.X},{t.Y})", t.ScriptAddress)),
                .. map.Signs.Where(s => s.HasScript).Select(s => ($"sign ({s.X},{s.Y})", s.ScriptAddress)),

                // And the doorways. Left out until the fifth list was read at all, which
                // meant asking this tool about 0x5B — the first command of an arrival
                // script — and being told it turns up nowhere in the game.
                .. map.OnEntry.Where(e => e.HasScript).Select(e => ("arriving", e.ScriptAddress)),
            ];

            foreach ((string what, uint address) in scripts)
            {
                // Occurrences as well as stops. A stop is only an occurrence the reader
                // could not get past, so asking about stops alone makes every width
                // already in the table invisible — including the wrong ones, which are
                // exactly the ones worth looking at.
                //
                // The reader rather than the runner, for the reason 0x7C proved: a run
                // walks the one path today's flags choose, and a command behind a
                // condition it does not satisfy is one it never sees.
                var found = new List<int>();

                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
                {
                    if (command.Code == code) found.Add(command.Offset);
                }

                // Every script reachable from here, not just this one. A linear read
                // stops at the first goto, so a command behind one was invisible to the
                // instrument built to find exactly that — and the command blocking the
                // opening of the game was behind two.
                foreach (uint reachable in ScriptReader.Reachable(rom, address))
                {
                    if (ScriptReader.StoppedAt(rom, reachable) != code) continue;
                    if (ScriptReader.StoppedAtOffset(rom, reachable) is not { } stopped) continue;

                    found.Add(stopped);
                }

                foreach (int at in found)
                {
                    if (!sites.TryGetValue(at, out List<string>? who))
                        sites[at] = who = [];

                    if (!who.Contains($"{mapId} {what}")) who.Add($"{mapId} {what}");
                }
            }
        }

        foreach ((int at, List<string> who) in sites.OrderByDescending(s => s.Value.Count))
        {
            string hex = string.Join(
                " ",
                Enumerable.Range(1, width)
                    .Where(i => at + i < rom.Length)
                    .Select(i => $"{rom.ReadU8(at + i):X2}"));

            Console.WriteLine($"  {Rom.BaseAddress + (uint)at:X8}  {hex}   {who.Count,4} x  e.g. {who[0]}");
        }

        Console.WriteLine();
        Console.WriteLine($"  {sites.Count} sites. A column that never changes is an argument, not a coincidence.");

        // Sites, not stops. Twenty people reading one shared script is one piece of
        // evidence written by one person on one afternoon, and three sites a few hundred
        // bytes apart are probably one script file. Counting stops flatters both.
        Console.WriteLine(
            $"  {sites.Values.Sum(w => w.Count)} people stop here, across " +
            $"{sites.Values.SelectMany(w => w).Select(w => w.Split(' ')[0]).Distinct().Count()} maps");
    }

    private static void WriteItems(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("Items");

        if (ItemTable.Locate(rom, Console.WriteLine) is not { } table)
        {
            Console.WriteLine("  no item table found");
            return;
        }

        List<ItemRecord> items = ItemTable.Read(rom, table);

        int highest = items.Count > 0 ? items[^1].Id : 0;

        Console.WriteLine(
            $"  {items.Count} items across {highest + 1} slots, " +
            $"{items.Count(i => i.ToData().CanBeBought)} of them for sale");

        // Reserved slots that were never filled in claim to be item zero wherever they
        // sit, so a reader keyed on self-indexing stops at the first one. Saying how
        // many were stepped over is what tells a short read from a short table.
        Console.WriteLine($"  {highest + 1 - items.Count} reserved slots stepped over");

        Console.WriteLine(
            $"  the slot after the last: {ItemRecord.Explain(rom, table + (highest + 1) * ItemRecord.RecordSizeBytes, highest + 1)}");

        foreach (var pocket in items.GroupBy(i => i.Pocket).OrderByDescending(g => g.Count()))
            Console.WriteLine($"    {pocket.Key,-9} {pocket.Count()}");

        // Before going looking for an effect table, look at what is already in hand.
        // A Potion restores 20, a Super Potion 50, a Hyper Potion 200 — if any of these
        // four fields holds those numbers then the amounts are already extracted and
        // there is no second format to read at all. Cheapest question first.
        Console.WriteLine();
        Console.WriteLine("  Medicine, with every field already read:");
        Console.WriteLine("        id name           hold param usage secondary");

        foreach (ItemRecord item in items
                     .Where(i => i.Pocket == Pocket.Items && i.BattleUsage != 0)
                     .Take(14))
        {
            Console.WriteLine(
                $"    {item.Id,6} {item.Name,-14} {item.HoldEffect,4} {item.HoldEffectParam,5} " +
                $"{item.BattleUsage,5} {item.SecondaryId,9}");
        }

        // The machines, and the one question worth asking of them: does a record say
        // which move it teaches, or is that a second table somewhere else?
        //
        // There is an answer already in hand to check against. Three move ids came out
        // of the obstacle scripts — 15, 70 and 249, CUT, STRENGTH and ROCK SMASH — and
        // whichever column holds those three among the HMs is the column that holds the
        // move. Asking a question you already know part of the answer to is the cheapest
        // way to identify a field, and the only way to be sure it is not a coincidence.
        List<ItemRecord> machines = [.. items.Where(i => i.Pocket == Pocket.Machines)];

        if (machines.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  Machines — {machines.Count} of them, first and last few:");
            Console.WriteLine("        id name           hold param usage secondary importance price");

            foreach (ItemRecord item in machines.Take(3).Concat(machines.TakeLast(9)))
            {
                Console.WriteLine(
                    $"    {item.Id,6} {item.Name,-14} {item.HoldEffect,4} {item.HoldEffectParam,5} " +
                    $"{item.BattleUsage,5} {item.SecondaryId,9} {item.Importance,10} {item.Price,5}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  Spot check — compare these names and prices against the games:");

        foreach (ItemRecord item in Spread(items, 12))
        {
            string name = string.IsNullOrWhiteSpace(item.Name) ? "(no name)" : item.Name;

            Console.WriteLine(
                $"    {item.Id,4} {name,-14} {item.Pocket,-9} " +
                $"{(item.Price > 0 ? $"{item.Price}" : "not for sale"),12}");
        }
    }

    private static void WriteOverworldSprites(Rom rom, string outputDirectory)
    {
        Console.WriteLine();
        Console.WriteLine("Overworld sprites");

        if (OverworldSprites.LocateGraphicsTable(rom, Console.WriteLine) is not { } table)
        {
            Console.WriteLine("  no graphics table found");
            return;
        }

        int? palettes = OverworldSprites.LocatePaletteTable(rom, Console.WriteLine);

        List<ObjectGraphicsInfo?> records = OverworldSprites.ReadGraphics(rom, table, 256);
        Dictionary<int, int> boundaries = OverworldSprites.FrameListBoundaries(rom, records);

        int found = records.Count(r => r is not null);
        Console.WriteLine($"  {found} graphics records of {records.Count} slots");

        var sizes = records
            .Where(r => r is not null)
            .GroupBy(r => $"{r!.Width}x{r.Height}")
            .OrderByDescending(g => g.Count());

        foreach (var size in sizes.Take(5))
            Console.WriteLine($"    {size.Key,-7} {size.Count()} sprites");

        ExplainWhatCameBefore(rom, table);

        var frameCounts = records
            .Where(r => r is not null)
            .Select(r => OverworldSprites.ReadFrames(rom, r!, boundaries).Count)
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count());

        foreach (var count in frameCounts.Take(5))
            Console.WriteLine($"    {count.Key} frames: {count.Count()} sprites");

        if (palettes is null)
        {
            Console.WriteLine("  no palette table found — sprites will have no colour");
            return;
        }

        string directory = Path.Combine(outputDirectory, "overworld");
        Directory.CreateDirectory(directory);

        int written = 0;

        for (int index = 0; index < records.Count && written < 12; index++)
        {
            if (records[index] is not { } info) continue;

            List<IndexedImage> frames = OverworldSprites.ReadFrames(rom, info, boundaries);
            if (frames.Count == 0) continue;

            if (OverworldSprites.PaletteForTag(rom, palettes.Value, info.PaletteTag) is not { } palette) continue;

            for (int frame = 0; frame < frames.Count && frame < 3; frame++)
            {
                string path = Path.Combine(directory, $"{index:D3}_{frame}.png");
                PngWriter.Write(path, frames[frame].Width, frames[frame].Height, frames[frame].ToRgba(palette));
            }

            Console.WriteLine(
                $"  wrote sprite {index,3}: {info.Width}x{info.Height}, {frames.Count} frames, " +
                $"palette tag 0x{info.PaletteTag:X4}");
            written++;
        }
    }

    /// <summary>
    /// Says why the entries before a located table were not part of it.
    /// <para>
    /// A table found a few entries late reads as a complete success — the records are
    /// real, the pictures decode, and every graphics id is quietly wrong by that many.
    /// The only way to tell is to look at what was rejected and why.
    /// </para>
    /// </summary>
    private static void ExplainWhatCameBefore(Rom rom, int table)
    {
        Console.WriteLine("  entries immediately before the run:");

        for (int back = 8; back >= 1; back--)
        {
            int at = table - back * 4;
            if (at < 0) continue;

            uint pointer = rom.ReadU32(at);

            if (pointer == 0)
            {
                Console.WriteLine($"    -{back}: zero");
                continue;
            }

            if (rom.ToOffsetOrNull(pointer) is not { } target)
            {
                Console.WriteLine($"    -{back}: 0x{pointer:X8} is not a ROM address");
                continue;
            }

            Console.WriteLine($"    -{back}: -> 0x{pointer:X8}  {ObjectGraphicsInfo.Explain(rom, target)}");
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            romdump — reads a player-supplied Generation III cartridge and dumps its data.

            usage:
              romdump <rom.gba> [options]

            options:
              --out <dir>            output directory (default: ./out)
              --species <list>       comma-separated species indices to render (default: 1,4,7)
              --shiny                use the alternate palette table
              --back                 use the back-sprite table
              --tile-order <o>       row (default) or column
              --no-sprites           dump data tables only
              --list-maps            list every map layout the cartridge holds
              --map <list>           comma-separated bank.map addresses to render,
                                     or "all" to render every map
              --map-name <text>      render every map whose name contains this text
              --moves                dump the move table with names and categories
              --encounters           dump wild encounter tables
              --behaviours <name>    report metatile behaviours for a named map
                                     (implies --encounters, does not render anything)
              --overworld            report the overworld sprite tables and write a
                                     few of the walking figures as PNGs
              --trainers             report the trainer table: where it starts, what
                                     was rejected just before it, and a few parties
              --items                report the item table, its pockets and prices
              --scripts              report how far object scripts read, and which
                                     commands stop them
              --script-map <b.m>     dump every script on one map, decoded and as bytes
              --export-rules <path>  write the rules file the server resolves battles
                                     against: base stats, move power, catch rates and
                                     learnsets, with no names of any kind
              --export-world <path>  write the collision-only world file the server
                                     runs on (no graphics, no text)
              --tileset-split <g>    firered (default) or emerald — how tile, metatile
                                     and palette slots divide between the two tilesets
              --diagnose             report every candidate table run and dump raw
                                     entries, for investigating an unexpected layout

            The ROM is read locally and never leaves this machine.
            """);
    }

    private sealed class Options
    {
        public string RomPath { get; private init; } = "";
        public string OutputDirectory { get; private init; } = "out";
        public int[] Species { get; private init; } = [1, 4, 7];
        public bool Shiny { get; private init; }
        public bool Back { get; private init; }
        public bool SkipSprites { get; private init; }
        public bool Diagnose { get; private init; }
        public bool ListMaps { get; private init; }
        public string[] Maps { get; private init; } = [];
        public string? MapNameFilter { get; private init; }
        public bool RenderAllMaps { get; private init; }
        public TilesetSplit Split { get; private init; } = TilesetSplit.FireRed;
        public string? ExportWorldPath { get; private init; }

        public string? ExportRulesPath { get; private init; }

        public bool DumpOverworld { get; private init; }
        public bool DumpTrainers { get; private init; }
        public bool DumpItems { get; private init; }
        public bool DumpScripts { get; private init; }
        public string ScriptMap { get; private init; } = "";

        /// <summary>Where a player got stuck, as <c>map,x,y</c>.</summary>
        public string At { get; private init; } = "";

        /// <summary>A step byte to place on the map, to find out what it does.</summary>
        public byte? Step { get; private init; }

        /// <summary>Check the two commands that bracket a scripted walk against the warps.</summary>
        public bool Doors { get; private init; }

        /// <summary>What the cartridge uses scripted player movement for.</summary>
        public bool PlayerWalks { get; private init; }

        /// <summary>A script variable to trace: who writes it, and who is waiting on it.</summary>
        public int? Variable { get; private init; }

        /// <summary>Check the map's own script list — the fifth pointer in every header.</summary>
        public bool Probe { get; private init; }

        /// <summary>The map whose scripts to run, rather than read.</summary>
        public string ScriptRun { get; private init; } = "";

        /// <summary>Count what stops a run, across every script on the cartridge.</summary>
        public bool ScriptRuns { get; private init; }

        /// <summary>Count which special routines get called, and on which maps.</summary>
        public bool Specials { get; private init; }

        public int? Special { get; private init; }

        public byte? Answers { get; private init; }

        public bool AnswerSweep { get; private init; }

        public string SpecialsOn { get; private init; } = "";

        /// <summary>Count the scripts map objects hand their work to.</summary>
        public bool Shared { get; private init; }

        /// <summary>Split the people whose script finishes and says nothing by cause.</summary>
        public bool Silent { get; private init; }

        /// <summary>Score every argument width for the commands that stop a run.</summary>
        public bool Derive { get; private init; }

        /// <summary>Print what follows one unknown command, everywhere it appears.</summary>
        public byte? BytesAfter { get; private init; }

        /// <summary>Hunt for the cartridge's lettering and write the candidates out.</summary>
        public bool Glyphs { get; private init; }

        /// <summary>Draw the sheet at one address, with its rows numbered.</summary>
        public uint Font { get; private init; }

        /// <summary>Find everybody who says this, and report what their script calls.</summary>
        public string WhoSays { get; private init; } = "";

        public int? WhoGives { get; private init; }

        public bool Events { get; private init; }

        public bool Movements { get; private init; }

        /// <summary>Dump everything reachable from one cartridge address.</summary>
        public uint ScriptAt { get; private init; }
        public bool DumpMoves { get; private init; }
        public bool DumpEncounters { get; private init; }
        public string? BehaviourMap { get; private init; }
        public TileOrder TileOrder { get; private init; } = TileOrder.RowMajor;

        public static Options Parse(string[] args)
        {
            string romPath = args[0];
            if (!File.Exists(romPath))
                throw new FileNotFoundException($"No such file: {romPath}");

            string output = "out";
            int[] species = [1, 4, 7];
            bool shiny = false, back = false, skip = false, diagnose = false, listMaps = false;
            bool renderAll = false;
            string[] maps = [];
            string? nameFilter = null;
            TilesetSplit split = TilesetSplit.FireRed;
            string? exportWorld = null;
            string? exportRules = null;
            bool overworld = false;
            bool trainers = false;
            bool items = false;
            bool scripts = false;
            string scriptMap = "";
            string at = "";
            string scriptRun = "";
            bool scriptRuns = false;
            bool specials = false;
            int? special = null;
            byte? answers = null;
            bool answerSweep = false;
            string specialsOn = "";
            bool shared = false;
            bool silent = false;
            bool derive = false;
            byte? bytesAfter = null;
            bool glyphs = false;
            uint font = 0;
            string whoSays = "";
            int? whoGives = null;
            bool events = false;
            bool movements = false;
            byte? step = null;
            bool doors = false;
            bool playerWalks = false;
            int? variable = null;
            bool probe = false;
            uint scriptAt = 0;
            bool dumpMoves = false, dumpEncounters = false;
            string? behaviourMap = null;
            TileOrder order = TileOrder.RowMajor;

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--out":
                        output = Next(args, ref i, "--out");
                        break;
                    case "--species":
                        species = Next(args, ref i, "--species")
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(int.Parse)
                            .ToArray();
                        break;
                    case "--shiny":
                        shiny = true;
                        break;
                    case "--back":
                        back = true;
                        break;
                    case "--no-sprites":
                        skip = true;
                        break;
                    case "--diagnose":
                        diagnose = true;
                        break;
                    case "--list-maps":
                        listMaps = true;
                        break;
                    case "--map":
                        string list = Next(args, ref i, "--map");
                        if (list.Equals("all", StringComparison.OrdinalIgnoreCase))
                        {
                            renderAll = true;
                        }
                        else
                        {
                            maps = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        }
                        break;
                    case "--map-name":
                        nameFilter = Next(args, ref i, "--map-name");
                        break;
                    case "--export-world":
                        exportWorld = Next(args, ref i, "--export-world");
                        break;
                    case "--export-rules":
                        exportRules = Next(args, ref i, "--export-rules");
                        break;
                    case "--overworld":
                        overworld = true;
                        break;
                    case "--trainers":
                        trainers = true;
                        break;
                    case "--items":
                        items = true;
                        break;
                    case "--scripts":
                        scripts = true;
                        break;
                    case "--script-map":
                        scriptMap = Next(args, ref i, "--script-map");
                        break;
                    case "--at":
                        at = Next(args, ref i, "--at");
                        break;
                    case "--script-run":
                        scriptRun = Next(args, ref i, "--script-run");
                        break;
                    case "--script-runs":
                        scriptRuns = true;
                        break;
                    case "--specials-on":
                        specialsOn = Next(args, ref i, "--specials-on");
                        break;
                    case "--answered":
                        answerSweep = true;
                        break;
                    case "--answers":
                        string answerer = Next(args, ref i, "--answers");
                        answers = Convert.ToByte(
                            answerer.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? answerer[2..] : answerer, 16);
                        break;
                    case "--special":
                        string routineId = Next(args, ref i, "--special");
                        special = routineId.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? Convert.ToInt32(routineId[2..], 16)
                            : int.Parse(routineId);
                        break;
                    case "--specials":
                        specials = true;
                        break;
                    case "--shared":
                        shared = true;
                        break;
                    case "--silent":
                        silent = true;
                        break;
                    case "--derive":
                        derive = true;
                        break;
                    case "--bytes-after":
                        string which = Next(args, ref i, "--bytes-after");
                        bytesAfter = Convert.ToByte(
                            which.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? which[2..] : which, 16);
                        break;
                    case "--glyphs":
                        glyphs = true;
                        break;
                    case "--font":
                        string sheetAt = Next(args, ref i, "--font");
                        font = Convert.ToUInt32(
                            sheetAt.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? sheetAt[2..] : sheetAt, 16);
                        break;
                    case "--who-says":
                        whoSays = Next(args, ref i, "--who-says");
                        break;
                    case "--events":
                        events = true;
                        break;
                    case "--movements":
                        movements = true;
                        break;
                    case "--step":
                        step = (byte)Convert.ToInt32(Next(args, ref i, "--step"), 16);
                        break;
                    case "--doors":
                        doors = true;
                        break;
                    case "--player-walks":
                        playerWalks = true;
                        break;
                    case "--map-scripts":
                        probe = true;
                        break;
                    case "--variable":
                        variable = Convert.ToInt32(Next(args, ref i, "--variable"), 16);
                        break;
                    case "--who-gives":
                        string item = Next(args, ref i, "--who-gives");
                        whoGives = item.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? Convert.ToInt32(item[2..], 16)
                            : int.Parse(item);
                        break;
                    case "--script-at":
                        string where = Next(args, ref i, "--script-at");
                        scriptAt = Convert.ToUInt32(
                            where.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? where[2..] : where, 16);
                        break;
                    case "--moves":
                        dumpMoves = true;
                        break;
                    case "--encounters":
                        dumpEncounters = true;
                        break;
                    case "--behaviours":
                        behaviourMap = Next(args, ref i, "--behaviours");
                        dumpEncounters = true;
                        break;
                    case "--tileset-split":
                        string game = Next(args, ref i, "--tileset-split");
                        split = game.StartsWith("em", StringComparison.OrdinalIgnoreCase)
                            ? TilesetSplit.Emerald
                            : TilesetSplit.FireRed;
                        break;
                    case "--tile-order":
                        string value = Next(args, ref i, "--tile-order");
                        order = value.StartsWith("col", StringComparison.OrdinalIgnoreCase)
                            ? TileOrder.ColumnMajor
                            : TileOrder.RowMajor;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{args[i]}'.");
                }
            }

            return new Options
            {
                RomPath = romPath,
                OutputDirectory = output,
                Species = species,
                Shiny = shiny,
                Back = back,
                SkipSprites = skip,
                Diagnose = diagnose,
                ListMaps = listMaps,
                Maps = maps,
                RenderAllMaps = renderAll,
                MapNameFilter = nameFilter,
                Split = split,
                ExportWorldPath = exportWorld,
                ExportRulesPath = exportRules,
                DumpOverworld = overworld,
                DumpTrainers = trainers,
                DumpItems = items,
                DumpScripts = scripts,
                ScriptMap = scriptMap,
                At = at,
                Step = step,
                Doors = doors,
                PlayerWalks = playerWalks,
                Variable = variable,
                Probe = probe,
                ScriptRun = scriptRun,
                ScriptRuns = scriptRuns,
                Specials = specials,
                Special = special,
                Answers = answers,
                AnswerSweep = answerSweep,
                SpecialsOn = specialsOn,
                Shared = shared,
                Silent = silent,
                Derive = derive,
                BytesAfter = bytesAfter,
                Glyphs = glyphs,
                Font = font,
                WhoSays = whoSays,
                WhoGives = whoGives,
                Events = events,
                Movements = movements,
                ScriptAt = scriptAt,
                DumpMoves = dumpMoves,
                DumpEncounters = dumpEncounters,
                BehaviourMap = behaviourMap,
                TileOrder = order,
            };
        }

        private static string Next(string[] args, ref int i, string flag)
        {
            if (++i >= args.Length) throw new ArgumentException($"{flag} needs a value.");
            return args[i];
        }
    }
}
