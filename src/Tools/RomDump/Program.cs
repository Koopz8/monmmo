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

        if (!string.IsNullOrEmpty(options.ScriptRun))
            WriteScriptRuns(rom, options.ScriptRun);

        if (options.ScriptRuns) WriteRunHistogram(rom);

        if (options.Specials) WriteSpecials(rom);

        if (options.Shared) WriteSharedScripts(rom);

        if (options.Glyphs) WriteGlyphCandidates(rom, options.OutputDirectory);

        if (options.Font != 0) WriteFontSheet(rom, options.Font, options.OutputDirectory);

        if (!string.IsNullOrEmpty(options.WhoSays)) WriteWhoSays(rom, options.WhoSays);

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

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
            {
                people++;

                ScriptRun run = ScriptRunner.Run(rom, person.ScriptAddress);

                if (run.StoppedAt is { } code)
                {
                    counts[code] = counts.GetValueOrDefault(code) + 1;

                    List<uint> where = examples.TryGetValue(code, out List<uint>? seen) ? seen : examples[code] = [];

                    // Three addresses is enough to read the bytes at; the count above is
                    // the number that says how much it matters.
                    if (where.Count < 3) where.Add(person.ScriptAddress);

                    continue;
                }

                finished++;
                if (run.IsEmpty) silent++;
            }
        }

        Console.WriteLine($"  {people} people with a script");
        Console.WriteLine($"  {finished} run to a proper end, {people - finished} stop somewhere");
        Console.WriteLine($"  {silent} of those that finish do nothing at all — no line, no shop, no fight");

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
    private static void WriteSpecials(Rom rom, int top = 20)
    {
        Console.WriteLine();
        Console.WriteLine("special routines, by where they are called");

        MapLibrary library = MapLibrary.Open(rom);

        var maps = new Dictionary<int, HashSet<string>>();
        var callers = new Dictionary<int, int>();
        var says = new Dictionary<int, Dictionary<string, int>>();

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
            {
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, person.ScriptAddress))
                {
                    // 0x25 takes a routine number; 0x26 takes a variable to answer into
                    // and then the routine. Both are calls into code this cannot see.
                    int routine = command.Code switch
                    {
                        0x25 => command.Word(),
                        0x26 => command.Word(2),
                        _ => -1,
                    };

                    if (routine < 0) continue;

                    callers[routine] = callers.GetValueOrDefault(routine) + 1;

                    if (!maps.TryGetValue(routine, out HashSet<string>? on)) maps[routine] = on = [];

                    on.Add(map.Name);

                    // The commonest opening line among the callers, not the first one
                    // found. One chatty script calls half a dozen routines, so "the
                    // first caller who says anything" quoted the same wireless-club
                    // attendant beside six different routines and discriminated nothing.
                    if (ScriptRunner.Run(rom, person.ScriptAddress).Pages.FirstOrDefault() is not { } opening) continue;
                    if (opening.Trim().Length == 0) continue;

                    if (!says.TryGetValue(routine, out Dictionary<string, int>? lines))
                        says[routine] = lines = [];

                    lines[opening] = lines.GetValueOrDefault(opening) + 1;
                }
            }
        }

        Console.WriteLine($"  {callers.Count} distinct routines across {library.Count} maps");
        Console.WriteLine($"  showing the {Math.Min(top, callers.Count)} called from the most maps");

        foreach ((int routine, HashSet<string> on) in maps.OrderByDescending(e => e.Value.Count).Take(top))
        {
            string[] names = [.. on.OrderBy(n => n).Take(3)];

            Console.WriteLine(
                $"    0x{routine:X4}  {on.Count} maps, {callers[routine]} callers  e.g. {string.Join(", ", names)}");

            if (!says.TryGetValue(routine, out Dictionary<string, int>? lines)) continue;

            foreach ((string opening, int said) in lines.OrderByDescending(e => e.Value).Take(2))
            {
                string line = GameText.ToAscii(opening).Replace("\n", " ");

                Console.WriteLine($"            {said}x  \"{(line.Length > 88 ? line[..88] + "..." : line)}\"");
            }
        }
    }

    /// <summary>
    /// Everybody on the cartridge who says a given thing, and what their script calls.
    /// <para>
    /// The inverse of every question this tool has asked so far. Counting where a
    /// routine is called from narrows a search and cannot finish it — six routines land
    /// on the same nineteen maps and no count can tell them apart. Somebody who names
    /// the place they are standing in can, and the text has been readable since
    /// milestone 14.
    /// </para>
    /// </summary>
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

        /// <summary>The map whose scripts to run, rather than read.</summary>
        public string ScriptRun { get; private init; } = "";

        /// <summary>Count what stops a run, across every script on the cartridge.</summary>
        public bool ScriptRuns { get; private init; }

        /// <summary>Count which special routines get called, and on which maps.</summary>
        public bool Specials { get; private init; }

        /// <summary>Count the scripts map objects hand their work to.</summary>
        public bool Shared { get; private init; }

        /// <summary>Hunt for the cartridge's lettering and write the candidates out.</summary>
        public bool Glyphs { get; private init; }

        /// <summary>Draw the sheet at one address, with its rows numbered.</summary>
        public uint Font { get; private init; }

        /// <summary>Find everybody who says this, and report what their script calls.</summary>
        public string WhoSays { get; private init; } = "";

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
            string scriptRun = "";
            bool scriptRuns = false;
            bool specials = false;
            bool shared = false;
            bool glyphs = false;
            uint font = 0;
            string whoSays = "";
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
                    case "--script-run":
                        scriptRun = Next(args, ref i, "--script-run");
                        break;
                    case "--script-runs":
                        scriptRuns = true;
                        break;
                    case "--specials":
                        specials = true;
                        break;
                    case "--shared":
                        shared = true;
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
                ScriptRun = scriptRun,
                ScriptRuns = scriptRuns,
                Specials = specials,
                Shared = shared,
                Glyphs = glyphs,
                Font = font,
                WhoSays = whoSays,
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
