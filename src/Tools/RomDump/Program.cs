using System.Text.Json;
using System.Text.Json.Serialization;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Graphics;
using PokeMmo.RomExtract.Maps;

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

        for (int index = 0; index < records.Count && written < 4; index++)
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

            Console.WriteLine($"  wrote sprite {index}: {info.Width}x{info.Height}, {frames.Count} frames");
            written++;
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
