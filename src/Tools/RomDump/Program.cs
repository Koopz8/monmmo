using System.Text.Json;
using System.Text.Json.Serialization;
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

        if (!string.IsNullOrEmpty(options.ExportWorldPath))
            ExportWorld(rom, options.ExportWorldPath);

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
            List<NamedMap> matched = maps
                .Where(m => m.Name.Contains(options.MapNameFilter, StringComparison.OrdinalIgnoreCase))
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
