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

        if (options.ListMaps || options.Maps.Length > 0)
            WriteMaps(rom, options);

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

    private static void WriteMaps(Rom rom, Options options)
    {
        Console.WriteLine();
        Console.WriteLine("Locating maps");

        MapLayoutTable? table = MapLocator.Locate(rom, Console.WriteLine);

        if (table is null)
        {
            Console.Error.WriteLine("  no map layout table found");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"  {table}");

        if (options.ListMaps)
        {
            Console.WriteLine();
            Console.WriteLine("Layouts");

            foreach ((int index, MapLayoutRecord layout) in table.Valid)
                Console.WriteLine($"  [{index,4}] {layout}");
        }

        if (options.Maps.Length == 0) return;

        string mapDirectory = Path.Combine(options.OutputDirectory, "maps");
        Directory.CreateDirectory(mapDirectory);

        Console.WriteLine();

        foreach (int index in options.Maps)
        {
            try
            {
                MapLayoutRecord layout = table.Layouts.ElementAtOrDefault(index)
                    ?? throw new InvalidOperationException($"layout {index} did not resolve");

                RenderedMap rendered = MapRenderer.Create(rom, layout, options.Split).Render(layout);

                string path = Path.Combine(mapDirectory, $"{index:D3}.png");
                File.WriteAllBytes(path, rendered.ToPng());

                Console.WriteLine($"Wrote {path} ({rendered.Width}x{rendered.Height})");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  map {index}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("If colours look wrong in places, the primary/secondary tileset split");
        Console.WriteLine("may differ on this cartridge — try --tileset-split emerald.");
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
              --map <list>           comma-separated layout indices to render as PNGs
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
        public int[] Maps { get; private init; } = [];
        public TilesetSplit Split { get; private init; } = TilesetSplit.FireRed;
        public TileOrder TileOrder { get; private init; } = TileOrder.RowMajor;

        public static Options Parse(string[] args)
        {
            string romPath = args[0];
            if (!File.Exists(romPath))
                throw new FileNotFoundException($"No such file: {romPath}");

            string output = "out";
            int[] species = [1, 4, 7];
            bool shiny = false, back = false, skip = false, diagnose = false, listMaps = false;
            int[] maps = [];
            TilesetSplit split = TilesetSplit.FireRed;
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
                        maps = Next(args, ref i, "--map")
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(int.Parse)
                            .ToArray();
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
                Split = split,
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
