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
using PokeMmo.RomExtract.Sound;
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

        if (options.Character is { } who)
            WriteOneCharacter(rom, who, options.OutputDirectory);

        if (options.DumpTrainers)
            WriteTrainers(rom, speciesCount);

        if (options.DumpItems)
            WriteItems(rom);

        if (options.DumpHolds)
            WriteHolds(rom);

        if (options.DumpTiers)
            WriteTiers(rom);

        if (options.DumpScripts)
            WriteScripts(rom);

        if (!string.IsNullOrEmpty(options.ScriptMap))
            WriteMapScripts(rom, options.ScriptMap);

        if (!string.IsNullOrEmpty(options.At))
            WriteSquare(rom, options.At);

        if (!string.IsNullOrEmpty(options.ScriptRun))
            WriteScriptRuns(rom, options.ScriptRun);

        if (options.ScriptRuns) WriteRunHistogram(rom);

        if (options.Substitutions) WriteSubstitutions(rom);

        if (options.HideFlags) WriteHideFlags(rom);

        if (options.NameRuns) WriteNameRuns(rom, extractor);

        if (options.SightLines) WriteSightLines(rom);

        if (options.Gaps) WriteGaps(rom, options.GapLike);

        if (options.FightKinds) WriteFightKinds(rom);
        if (options.Challenges) WriteChallenges(rom);

        if (options.DoorSteps) WriteDoorSteps(rom);

        if (options.MoveEffects) WriteMoveEffects(rom);
        if (options.Efforts) WriteEfforts(rom);
        if (options.FifthMove) WriteFifthMove(rom);
        if (options.Water) WriteWater(rom);
        if (options.Doors2) WriteDoors(rom);
        if (options.WarpShape) WriteWarpShape(rom);
        if (!string.IsNullOrEmpty(options.WaterMap)) WriteWaterMap(rom, options.WaterMap);

        if (options.RivalFights) WriteRivalFights(rom);

        if (!string.IsNullOrEmpty(options.Walkable)) WriteWalkable(rom, options.Walkable);

        if (options.Specials) WriteSpecials(rom);

        if (options.ScriptedDoors) WriteScriptedDoors(rom);

        if (options.Special is { } routine) WriteSpecial(rom, routine);

        if (options.Answers is { } answering) WriteAnswers(rom, answering);

        if (options.AnswerSweep) WriteAnswerSweep(rom);

        if (!string.IsNullOrEmpty(options.SpecialsOn)) WriteSpecialsOn(rom, options.SpecialsOn);

        if (options.Shared) WriteSharedScripts(rom);

        if (options.Silent) WriteSilentPeople(rom);

        if (options.Sound) WriteSound(rom);

        if (options.Derive) WriteDerivedLengths(rom);

        if (options.Opcodes) WriteOpcodeCounts(rom);

        if (options.Audit) WriteWidthAudit(rom);

        if (options.Ledges) WriteLedges(rom);

        if (options.NewGame) WriteNewGame(rom);

        if (options.AfterFights) WriteAfterFights(rom);

        if (options.Evolutions) WriteEvolutions(rom);
        if (options.Machines) WriteMachineCompatibility(rom);
        if (options.Computers) WriteComputers(rom);
        if (options.Letters) WriteLetterHunt(rom);
        if (options.Clears is { } askedAbout) WriteFlagClearers(rom, askedAbout);

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

        if (options.Gifts) { WriteGiftMons(rom); WriteStandardRoutines(rom); }

        if (options.Variable is { } variable) WriteVariable(rom, variable);

        if (options.Probe) WriteMapScripts(rom);

        if (options.ScriptAt != 0)
        {
            WriteScriptAt(rom, options.ScriptAt);
            RunAsTheClientWould(rom, options.ScriptAt);
        }


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

    /// <summary>
    /// Which trainers are the boy who follows you around the game.
    /// <para>
    /// The battle screen calls him TERRY and his own script calls him GREEN, because the
    /// first is the name in the cartridge's trainer table and the second is the name the
    /// player chose from the cartridge's own list. Both are the cartridge's and they
    /// disagree, so one of them is a placeholder — and which is decided here rather than
    /// remembered.
    /// </para>
    /// <para>
    /// 0x06 in a run of text is already known to be the rival: 33 sites, always a
    /// speaker's label before a colon, always in the mouth of the boy. So a fight picked
    /// by a script that also says <c>{FD}{06}</c> is a fight with him, and this counts
    /// how many trainers that finds and what the table calls each of them.
    /// </para>
    /// </summary>
    private static void WriteRivalFights(Rom rom)
    {
        MapLibrary library = MapLibrary.Open(rom);
        int speciesCount = RomExtractor.Open(rom).ExtractSpecies().Count;

        List<TrainerRecord> table = TrainerTable.Locate(rom, speciesCount) is { } located
            ? TrainerTable.Read(rom, located, speciesCount)
            : [];

        Dictionary<int, string> trainers = table.ToDictionary(t => t.Id, t => t.Name);

        var named = new Dictionary<int, string>();
        var his = new HashSet<int>();
        var others = new HashSet<int>();

        Console.WriteLine();
        Console.WriteLine("Fights picked by a script that says {FD}{06}");

        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> addresses =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
            ];

            foreach (uint address in addresses.Distinct())
            {
                List<ScriptCommand> read = ScriptReader.ReadAll(rom, address);

                var fights = read
                    .Where(c => c.Code == ScriptCommands.TrainerBattle)
                    .Select(c => c.Word(1))
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (fights.Count == 0) continue;

                bool saysHim = false;

                foreach (ScriptCommand command in read)
                {
                    if (command.Code is not (ScriptCommands.Message or ScriptCommands.LoadPointer)) continue;

                    uint text = command.Code == ScriptCommands.Message ? command.Pointer() : command.Pointer(1);

                    if (rom.ToOffsetOrNull(text) is not { } at) continue;
                    if (!GameText.LooksLikeDialogue(rom.Span[at..])) continue;

                    if (string.Join(" ", GameText.DecodeDialogue(rom.Span[at..])).Contains("{FD}{06}"))
                        saysHim = true;
                }

                foreach (int id in fights)
                {
                    if (saysHim) his.Add(id);
                    else others.Add(id);

                    if (trainers.TryGetValue(id, out string? was)) named[id] = was;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  {his.Count} trainers, and {his.Intersect(others).Count()} of them also fought by somebody else");
        Console.WriteLine();

        foreach (int id in his.Order())
            Console.WriteLine($"    trainer {id,4}  {named.GetValueOrDefault(id, "?")}");

        // The name most of them share is the placeholder, and the question that settles
        // whether it can be used as the rule is how many others wear it.
        string placeholder = his
            .Select(id => named.GetValueOrDefault(id, ""))
            .Where(name => name.Length > 0)
            .GroupBy(name => name)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "";

        int wearing = table.Count(t => t.Name == placeholder);
        int wearingAndHis = his.Count(id => named.GetValueOrDefault(id) == placeholder);

        Console.WriteLine();
        Console.WriteLine(
            $"  \"{placeholder}\" is on {wearing} trainers in the table, {wearingAndHis} of them here — " +
            $"{wearing - wearingAndHis} wear it and are picked by no script that says {{FD}}{{06}}");

        Console.WriteLine();
        Console.WriteLine("  The names the table repeats most");

        foreach (var group in table.Where(t => t.Name.Length > 0)
                     .GroupBy(t => t.Name)
                     .OrderByDescending(g => g.Count())
                     .Take(6))
        {
            Console.WriteLine($"    {group.Key,-12} {group.Count(),3}");
        }

        Console.WriteLine();
        Console.WriteLine($"  and {others.Count} trainers picked by scripts that never say it, e.g.");

        foreach (int id in others.Order().Take(6))
            Console.WriteLine($"    trainer {id,4}  {named.GetValueOrDefault(id, "?")}");
    }

    /// <summary>
    /// Where in the game a fifth move first has to be turned away.
    /// <para>
    /// Asked because the question "which of your four do you want to lose" is
    /// unreachable by ordinary play until somebody knows four, and finding a creature
    /// that gets there in one win is otherwise a matter of remembering a learnset. It
    /// is not: every species that knows four at some level and learns another at the
    /// very next one is right here in the table, and the cheapest of them is the one
    /// worth walking into grass with.
    /// </para>
    /// </summary>
    /// <summary>
    /// Doors that lead somewhere which does not lead back.
    /// <para>
    /// A door is two ways. Almost every warp on this cartridge has a partner on the map
    /// it points at, pointing home — which makes the ones that do not a shape worth
    /// counting rather than a curiosity. If the proportion is high, warps are being read
    /// correctly and the odd ones out are real one-way passages; if it is low, something
    /// about the reading is wrong and every conclusion drawn from where doors go is
    /// drawn from noise.
    /// </para>
    /// <para>
    /// Asked because five doors were found holding a hundred and seventy-four maps out of
    /// reach, and four of the twelve leading to the same place turned out to sit under
    /// the windows of a POKeMON CENTER.
    /// </para>
    /// </summary>
    /// <summary>
    /// Whether a command byte is the one that moves a player to another map.
    /// <para>
    /// Asked because the Cable Club above every POKeMON CENTER turned out to be entered
    /// by talking to somebody rather than by walking through a door, and because 0x39 sits
    /// in this project's width table at one byte — a width that was never derived, and a
    /// wrong width does not fail, it makes a script read cleanly and quietly contain less.
    /// </para>
    /// <para>
    /// The test needs no guessing. A warp's arguments are a bank, a map, a warp id and a
    /// square, and a bank and map either name a map this cartridge has or they do not. A
    /// candidate whose sites overwhelmingly name real maps at squares inside them is the
    /// warp; a candidate that lands on real maps a third of the time is arithmetic on
    /// noise.
    /// </para>
    /// </summary>
    private static void WriteWarpShape(Rom rom)
    {
        Core.World.WorldData world = WorldExporter.Export(rom);
        MapLibrary library = MapLibrary.Open(rom);

        var byId = world.Maps.ToDictionary(m => m.Id);

        // Every script this project can find, read as raw bytes rather than as commands:
        // the point is to test a width, so nothing may depend on the widths being right.
        var addresses = new HashSet<uint>();

        foreach (LoadedMap map in library.All())
        {
            foreach (uint at in map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress)) addresses.Add(at);
            foreach (uint at in map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress)) addresses.Add(at);
            foreach (uint at in map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress)) addresses.Add(at);
        }

        // Counted at real command boundaries rather than anywhere the byte appears.
        // A byte inside a pointer is not a command, and a scan that counts it is a scan
        // measuring the cartridge's spare zeroes.
        var sites = new Dictionary<byte, int>();
        var realMap = new Dictionary<byte, int>();
        var inside = new Dictionary<byte, int>();
        var examples = new Dictionary<byte, List<string>>();

        foreach (uint address in addresses)
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                int at = command.Offset;

                if (at + 8 >= rom.Length) continue;

                byte code = command.Code;

                sites[code] = sites.GetValueOrDefault(code) + 1;

                int bank = rom.Span[at + 1];
                int number = rom.Span[at + 2];
                int x = rom.Span[at + 4] | (rom.Span[at + 5] << 8);
                int y = rom.Span[at + 6] | (rom.Span[at + 7] << 8);

                if (!byId.TryGetValue($"{bank}.{number}", out Core.World.MapData? target)) continue;

                realMap[code] = realMap.GetValueOrDefault(code) + 1;

                if (x >= target.Width || y >= target.Height) continue;

                inside[code] = inside.GetValueOrDefault(code) + 1;

                List<string> seen = examples.TryGetValue(code, out List<string>? had) ? had : examples[code] = [];

                if (seen.Count < 3) seen.Add($"-> {target.Id} {target.Name} at ({x}, {y})");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Which command byte moves somebody to another map");
        Console.WriteLine();
        Console.WriteLine("  Read as bank, map, warp id, x, y — the shape a warp has. A byte whose");
        Console.WriteLine("  sites almost all name a map this cartridge has, at a square inside it,");
        Console.WriteLine("  is not doing that by chance.");
        Console.WriteLine();
        Console.WriteLine("  byte  sites   names a map   inside it");

        foreach ((byte code, int count) in sites
                     .Where(e => e.Value >= 8)
                     .OrderByDescending(e => inside.GetValueOrDefault(e.Key) / (double)e.Value)
                     .Take(8))
        {
            Console.WriteLine(
                $"  0x{code:X2}  {count,5}   {realMap.GetValueOrDefault(code),5} " +
                $"({(count == 0 ? 0 : 100 * realMap.GetValueOrDefault(code) / count),3}%)   " +
                $"{inside.GetValueOrDefault(code),5} ({(count == 0 ? 0 : 100 * inside.GetValueOrDefault(code) / count),3}%)");

            foreach (string one in examples.GetValueOrDefault(code, [])) Console.WriteLine($"          {one}");
        }
    }

    private static void WriteDoors(Rom rom)
    {
        Core.World.WorldData world = WorldExporter.Export(rom);

        var byId = world.Maps.ToDictionary(m => m.Id);

        int total = 0;
        int reciprocal = 0;
        int missingMap = 0;
        int dynamic = 0;

        var oneWay = new List<string>();

        foreach (Core.World.MapData map in world.Maps)
        {
            foreach (Warp warp in map.Warps)
            {
                total++;

                if (warp.IsDynamic)
                {
                    dynamic++;
                    continue;
                }

                if (!byId.TryGetValue(warp.TargetMapId, out Core.World.MapData? target))
                {
                    missingMap++;
                    continue;
                }

                if (target.Warps.Any(w => w.TargetMapId == map.Id))
                {
                    reciprocal++;
                    continue;
                }

                if (oneWay.Count < 14)
                    oneWay.Add($"{map.Id} {map.Name} at {warp.Square} -> {target.Id} {target.Name}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Doors, and whether what they lead to leads back");
        Console.WriteLine();
        Console.WriteLine(
            $"  {total} warps, {reciprocal} lead somewhere that leads back " +
            $"({(total == 0 ? 0 : 100 * reciprocal / total)}%)");
        Console.WriteLine($"  {dynamic} lead back the way you came, which is a destination rather than a hole");
        Console.WriteLine($"  {missingMap} point at a map this world file does not have");
        Console.WriteLine($"  {total - reciprocal - missingMap - dynamic} lead somewhere with no way back");
        Console.WriteLine();

        foreach (string one in oneWay) Console.WriteLine($"    {one}");
    }

    private static void WriteWater(Rom rom)
    {
        Core.World.WorldData world = WorldExporter.Export(rom);

        // The foothold. A map with a water encounter table is a map somebody can fish or
        // surf on, and that fact is in a completely different structure from the one
        // being asked about — so the two can be laid against each other without either
        // one being assumed.
        var wet = new HashSet<string>();
        var dry = new HashSet<string>();

        foreach (Core.World.MapData map in world.Maps)
        {
            if (map.Encounters?.Water is { IsUsable: true }) wet.Add(map.Id);
            else dry.Add(map.Id);
        }

        // For each behaviour byte: how many wet maps carry it, how many dry maps do, and
        // how many squares it covers on a wet map. Water should be on nearly every wet
        // map, on very few dry ones, and cover a great deal of ground where it is.
        var onWet = new Dictionary<byte, int>();
        var onDry = new Dictionary<byte, int>();
        var squares = new Dictionary<byte, int>();

        foreach (Core.World.MapData map in world.Maps)
        {
            if (map.Behaviours.Length == 0) continue;

            bool isWet = wet.Contains(map.Id);

            foreach (byte behaviour in map.Behaviours.Distinct())
            {
                if (isWet) onWet[behaviour] = onWet.GetValueOrDefault(behaviour) + 1;
                else onDry[behaviour] = onDry.GetValueOrDefault(behaviour) + 1;
            }

            if (!isWet) continue;

            foreach (byte behaviour in map.Behaviours)
                squares[behaviour] = squares.GetValueOrDefault(behaviour) + 1;
        }

        Console.WriteLine();
        Console.WriteLine("Which behaviour byte is water");
        Console.WriteLine();
        Console.WriteLine(
            $"  {wet.Count} maps have a water encounter table, {dry.Count} do not");
        Console.WriteLine();
        Console.WriteLine("  byte   wet maps / dry maps   squares on wet maps   solid");
        Console.WriteLine();
        Console.WriteLine("  every byte on five or more wet maps and under a twentieth of the dry ones");

        foreach ((byte behaviour, int seen) in onWet
                     .Where(e => e.Value >= 5 && onDry.GetValueOrDefault(e.Key) * 20 < dry.Count)
                     .OrderByDescending(e => e.Value))
        {
            int total = 0;
            int blocked = 0;

            foreach (Core.World.MapData map in world.Maps)
            {
                if (map.Behaviours.Length == 0) continue;

                var grid = new Core.World.CollisionGrid(map.Width, map.Height, map.Collision);

                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        int at = y * map.Width + x;
                        if (at >= map.Behaviours.Length || map.Behaviours[at] != behaviour) continue;

                        total++;
                        if (!grid.IsWalkable(new Core.World.GridPosition(x, y))) blocked++;
                    }
                }
            }

            Console.WriteLine(
                $"  0x{behaviour:X2}   {seen,4} of {wet.Count,4} / {onDry.GetValueOrDefault(behaviour),4} of {dry.Count,4}   " +
                $"{total,7} squares   {(total == 0 ? 0 : 100 * blocked / total),3}% solid");
        }

        Console.WriteLine();
        Console.WriteLine("  the eight best separators, ranked");
        Console.WriteLine();
        Console.WriteLine("  byte   wet maps / dry maps   squares on wet maps");

        // Ranked by how much better it separates the two sets than chance would. A byte
        // on every map of both kinds says nothing; a byte on most wet maps and almost no
        // dry ones is the answer.
        foreach ((byte behaviour, int seen) in onWet
                     .OrderByDescending(e => e.Value / (double)wet.Count - onDry.GetValueOrDefault(e.Key) / (double)dry.Count)
                     .Take(8))
        {
            Console.WriteLine(
                $"  0x{behaviour:X2}   {seen,4} of {wet.Count,4} / {onDry.GetValueOrDefault(behaviour),4} of {dry.Count,4}   " +
                $"{squares.GetValueOrDefault(behaviour),7}");
        }

        // And the check that matters more than the ranking: a byte that is water should
        // be walled off from the land. Every square of it on a wet map ought to be one
        // the collision data already refuses, because until now the only thing standing
        // between a player and the sea has been the wall around it.
        Console.WriteLine();
        Console.WriteLine("  and how much of each is already solid ground the player cannot enter");

        foreach ((byte behaviour, int _) in onWet
                     .OrderByDescending(e => e.Value / (double)wet.Count - onDry.GetValueOrDefault(e.Key) / (double)dry.Count)
                     .Take(4))
        {
            int total = 0;
            int blocked = 0;

            foreach (Core.World.MapData map in world.Maps)
            {
                if (!wet.Contains(map.Id) || map.Behaviours.Length == 0) continue;

                var grid = new Core.World.CollisionGrid(map.Width, map.Height, map.Collision);

                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        int at = y * map.Width + x;
                        if (at >= map.Behaviours.Length || map.Behaviours[at] != behaviour) continue;

                        total++;
                        if (!grid.IsWalkable(new Core.World.GridPosition(x, y))) blocked++;
                    }
                }
            }

            Console.WriteLine(
                $"    0x{behaviour:X2}  {blocked} of {total} " +
                $"({(total == 0 ? 0 : 100 * blocked / total)}%)");
        }

        // And the drawing test, which is what named every other behaviour byte in this
        // project. A sea route is mostly sea. If a byte is water it will cover most of
        // one, and the byte that covers most of a route full of grass will not.
        Console.WriteLine();
        Console.WriteLine("  how much of a few maps each candidate covers");

        List<byte> candidates =
        [
            .. onWet
                .OrderByDescending(e => e.Value / (double)wet.Count - onDry.GetValueOrDefault(e.Key) / (double)dry.Count)
                .Take(4)
                .Select(e => e.Key),
        ];

        Console.WriteLine("    map                          " + string.Join("  ", candidates.Select(c => $"0x{c:X2}")));

        foreach (Core.World.MapData map in world.Maps
                     .Where(m => wet.Contains(m.Id) && m.Behaviours.Length > 0)
                     .OrderByDescending(m => m.Behaviours.Count(b => b == candidates[0]))
                     .Take(6))
        {
            string counts = string.Join(
                "  ",
                candidates.Select(c => $"{100 * map.Behaviours.Count(b => b == c) / map.Behaviours.Length,3}%"));

            Console.WriteLine($"    {map.Id,-6} {map.Name,-22} {counts}");
        }

        // And the same for somewhere with no water at all, as the control.
        foreach (Core.World.MapData map in world.Maps
                     .Where(m => dry.Contains(m.Id) && m.Behaviours.Length > 400)
                     .Take(3))
        {
            string counts = string.Join(
                "  ",
                candidates.Select(c => $"{100 * map.Behaviours.Count(b => b == c) / map.Behaviours.Length,3}%"));

            Console.WriteLine($"    {map.Id,-6} {map.Name,-22} {counts}   (no water table)");
        }

        // Where the water is not walled off, which is a different question from how much
        // of it is. A map whose sea is passable in the block data is a map a player can
        // already walk out into, and nothing would stop them.
        Console.WriteLine();
        Console.WriteLine("  maps where most of the water is not a wall");

        int open = 0;

        foreach (Core.World.MapData map in world.Maps)
        {
            if (map.Behaviours.Length == 0) continue;

            var grid = new Core.World.CollisionGrid(map.Width, map.Height, map.Collision);

            int here = 0;
            int walkable = 0;

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    int at = y * map.Width + x;
                    if (at >= map.Behaviours.Length || map.Behaviours[at] != candidates[0]) continue;

                    here++;
                    if (grid.IsWalkable(new Core.World.GridPosition(x, y))) walkable++;
                }
            }

            if (here < 20 || walkable * 2 <= here) continue;

            open++;

            if (open <= 8)
                Console.WriteLine($"    {map.Id,-6} {map.Name,-22} {walkable} of {here} squares");
        }

        Console.WriteLine($"    {open} maps in all");

        // And where the other near-clean separators live, so that naming two bytes and
        // stopping is a decision rather than an oversight.
        foreach (byte other in (byte[])[0x21, 0x17, 0x0C])
        {
            Core.World.MapData? most = world.Maps
                .Where(m => m.Behaviours.Length > 0)
                .OrderByDescending(m => m.Behaviours.Count(b => b == other))
                .FirstOrDefault();

            if (most is null) continue;

            Console.WriteLine(
                $"    0x{other:X2} is thickest on {most.Id} {most.Name} " +
                $"({most.Behaviours.Count(b => b == other)} squares)");
        }

        // The last question, and the one that decides how much this is worth: how many
        // maps become reachable if that byte stops being a wall.
        Console.WriteLine();
        Console.WriteLine("  wet maps with none of the leading byte on them at all");

        byte best = onWet
            .OrderByDescending(e => e.Value / (double)wet.Count - onDry.GetValueOrDefault(e.Key) / (double)dry.Count)
            .First().Key;

        var missing = new List<string>();

        foreach (Core.World.MapData map in world.Maps)
        {
            if (!wet.Contains(map.Id) || map.Behaviours.Length == 0) continue;
            if (map.Behaviours.Contains(best)) continue;

            missing.Add($"{map.Id} {map.Name}");
        }

        Console.WriteLine($"    {missing.Count} of {wet.Count}");

        foreach (string one in missing.Take(8)) Console.WriteLine($"      {one}");

        // And what those eight have instead. A city with a pond you can only fish in is
        // still a map with water on it, and declaring one byte the answer while leaving
        // eight maps of water unaccounted for is the kind of tidy conclusion this
        // project is supposed to refuse.
        Console.WriteLine();
        Console.WriteLine("    what they have instead, counting only bytes that are rare on dry land");

        var instead = new Dictionary<byte, int>();

        foreach (Core.World.MapData map in world.Maps)
        {
            if (!wet.Contains(map.Id) || map.Behaviours.Length == 0) continue;
            if (map.Behaviours.Contains(best)) continue;

            foreach (byte behaviour in map.Behaviours.Distinct())
            {
                if (onDry.GetValueOrDefault(behaviour) > dry.Count / 10) continue;

                instead[behaviour] = instead.GetValueOrDefault(behaviour) + 1;
            }
        }

        foreach ((byte behaviour, int count) in instead.OrderByDescending(e => e.Value).Take(6))
        {
            Console.WriteLine(
                $"      0x{behaviour:X2}  on {count} of those {missing.Count}, " +
                $"and on {onDry.GetValueOrDefault(behaviour)} of {dry.Count} dry maps");
        }
    }

    /// <summary>
    /// One map drawn as characters, with the water candidates marked.
    /// <para>
    /// Every behaviour byte this project has named was named by drawing it. A count is
    /// a number that any wrong answer can also produce; a shape is not. Tall grass at
    /// the wrong stride gave 52 squares scattered down two edges on alternating rows,
    /// and only the picture said so.
    /// </para>
    /// </summary>
    private static void WriteWaterMap(Rom rom, string mapId)
    {
        Core.World.WorldData world = WorldExporter.Export(rom);

        if (world.Find(mapId) is not { } map)
        {
            Console.WriteLine($"  no map {mapId} on this cartridge");
            return;
        }

        var grid = new Core.World.CollisionGrid(map.Width, map.Height, map.Collision);

        Console.WriteLine();
        Console.WriteLine($"{map.Id} {map.Name}, {map.Width}x{map.Height}");
        Console.WriteLine("  D = door, W = 0x15, w = 0x10, ~ = 0x21, . = walkable, # = solid");
        Console.WriteLine();

        var doors = map.Warps.Select(w => w.Square).ToHashSet();

        for (int y = 0; y < map.Height; y++)
        {
            var row = new System.Text.StringBuilder("  ");

            for (int x = 0; x < map.Width; x++)
            {
                int at = y * map.Width + x;
                byte behaviour = at < map.Behaviours.Length ? map.Behaviours[at] : (byte)0;
                var square = new Core.World.GridPosition(x, y);

                row.Append(
                    doors.Contains(square) ? 'D'
                    : behaviour switch
                    {
                        0x15 => 'W',
                        0x10 => 'w',
                        0x21 => '~',
                        _ => grid.IsWalkable(square) ? '.' : '#',
                    });
            }

            Console.WriteLine(row.ToString());
        }

        Console.WriteLine();
        Console.WriteLine($"  {map.Warps.Count} doors");

        foreach (Warp warp in map.Warps)
        {
            Console.WriteLine(
                $"    {warp.Square} -> {warp.TargetMapId} " +
                $"{(world.Find(warp.TargetMapId)?.Name ?? "?")}, warp {warp.TargetWarpId}");
        }
    }

    private static void WriteFifthMove(Rom rom)
    {
        Dictionary<int, Learnset> learnsets = LearnsetExtractor.Extract(rom);
        List<MoveData> moves = MoveExtractor.Extract(rom);

        var moveNames = moves.ToDictionary(m => m.Id, m => m.Name);
        var names = new Dictionary<int, string>();

        if (TableLocator.Locate(rom).SpeciesNames is { } table)
        {
            for (int i = 1; i <= 411; i++)
            {
                int at = table.Offset + i * GameText.SpeciesNameLength;
                if (at + GameText.SpeciesNameLength > rom.Length) break;

                names[i] = GameText.Decode(rom.Slice(at, GameText.SpeciesNameLength));
            }
        }

        var found = new List<(int Level, int Species, int MoveId)>();

        foreach ((int species, Learnset learnset) in learnsets)
        {
            foreach (LevelUpMove entry in learnset.Moves)
            {
                // Four already known the level before, and this one arriving with
                // nowhere to go. Counted distinctly, because a move learned twice
                // occupies one slot and would otherwise look like two.
                int known = learnset.Moves
                    .Where(m => m.Level < entry.Level)
                    .Select(m => m.MoveId)
                    .Distinct()
                    .TakeLast(5)
                    .Count();

                if (known < 4) continue;
                if (learnset.Moves.Any(m => m.Level < entry.Level && m.MoveId == entry.MoveId)) continue;

                found.Add((entry.Level, species, entry.MoveId));
                break;
            }
        }

        Console.WriteLine();
        Console.WriteLine("The first level at which each species has to turn a move away");
        Console.WriteLine();
        Console.WriteLine($"  {found.Count} of {learnsets.Count} species reach five moves at all");
        Console.WriteLine();
        Console.WriteLine("  The earliest, which are the ones a test can reach in one win");

        foreach ((int level, int species, int moveId) in found.OrderBy(f => f.Level).Take(20))
        {
            Console.WriteLine(
                $"    L{level,-3} {names.GetValueOrDefault(species, "?"),-12} species {species,3}   " +
                $"offered {moveNames.GetValueOrDefault(moveId, "?")}");
        }
    }

    /// <summary>
    /// Every move grouped by the effect byte in its own record.
    /// <para>
    /// The battle engine does nothing at all with a move whose power is zero — one line
    /// says so: <c>if (move.Category == DamageCategory.Status) return;</c>. That is a
    /// third of the moves in the game, and it is why a level 30 BULBASAUR can spend a
    /// whole fight saying "used POISONPOWDER!" at a level 9 PIDGEY without touching it.
    /// </para>
    /// <para>
    /// What each effect number means is read off the members rather than remembered: the
    /// group containing POISONPOWDER and POISON GAS is the one that poisons, the group
    /// containing GROWL and TAIL WHIP lowers something. That is the same method the
    /// script widths were derived by, applied to a table this project already extracts.
    /// </para>
    /// </summary>
    /// <summary>
    /// The silence that did not have to be silence.
    /// <para>
    /// Nothing in a record says what a group does. But this engine has already committed
    /// to a status for some groups, and where every one of a type's committed groups is
    /// that type the whole way through and they all agree, the type is settled — and a
    /// silent damaging group that is one type throughout and carries a secondary chance in
    /// every record is the same claim asked again.
    /// </para>
    /// <para>
    /// Both halves are printed because the refusals are the point. A type settled by a
    /// mixed-type group is not settled at all, and a group this engine already answers is
    /// not the rule's to speak about.
    /// </para>
    /// </summary>
    private static void WriteRidersByType(List<MoveData> moves)
    {
        List<TypeRider> settled = RidersByType.Settled(moves, MoveEffects.Of);
        List<RiderGroup> accounted = RidersByType.Accounted(moves, MoveEffects.Of);

        Console.WriteLine();
        Console.WriteLine(
            $"  What a type already means. {settled.Count} types are settled — every group this " +
            "engine gives");
        Console.WriteLine(
            "  a status to for that type is that type the whole way through, and they agree:");

        foreach (TypeRider rider in settled)
        {
            Console.WriteLine(
                $"    {rider.Type,-9} -> {rider.Status,-9} from " +
                $"{string.Join(", ", rider.From.Select(f => $"0x{f:X2}"))}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"  {accounted.Count} damaging groups follow from those: one type throughout, and a " +
            "secondary chance in");
        Console.WriteLine(
            $"  every record. This engine agrees with {accounted.Count(a => a.EngineAgrees)} of them.");

        Console.WriteLine();

        foreach (RiderGroup group in accounted)
        {
            Console.WriteLine(
                $"    0x{group.Effect:X2}  {group.Type,-9} -> {group.Status,-9} " +
                $"{(group.EngineAgrees ? "agreed " : "SILENT ")} {string.Join(", ", group.Moves)}");
        }
    }

    /// <summary>
    /// Where the six effort yields live in a species record, and which slice is which
    /// stat — both read off the table rather than remembered.
    /// <para>
    /// This is the instrument the effort-value work was built on, and it settles two
    /// separate questions. The <b>packing</b>: try all 27 byte pairs a 28-byte record
    /// has, read each as six two-bit fields, and count the records whose six add up to
    /// between one and three. One pair wins outright, and the only records where it
    /// fails are the placeholder run this cartridge never fields. The <b>order</b>: a
    /// species that yields in one slice only should be a species whose highest base
    /// stat is the stat that slice means, and the diagonal says whether it is.
    /// </para>
    /// </summary>
    private static void WriteEfforts(Rom rom)
    {
        RomTables tables = TableLocator.Locate(rom);

        if (tables.BaseStats is not { } table)
        {
            Console.WriteLine("Skipping effort yields: base-stat table not found.");
            return;
        }

        int count = table.EntryCount;

        var records = new byte[count][];

        for (int i = 0; i < count; i++)
            records[i] = rom.Slice(table.Offset + i * SpeciesData.SizeBytes, SpeciesData.SizeBytes).ToArray();

        // A record of all noughts is a row the table has and the game does not.
        List<byte[]> live = [.. records.Where(r => r[0] != 0)];

        Console.WriteLine();
        Console.WriteLine("Which two bytes hold six two-bit yields");
        Console.WriteLine();
        Console.WriteLine($"  Of {live.Count} records with base stats, how many read as six slices");
        Console.WriteLine("  totalling one to three — which is what an effort yield can be.");
        Console.WriteLine();

        static int TotalAt(byte[] record, int at)
        {
            int packed = record[at] | (record[at + 1] << 8);
            int total = 0;

            for (int slice = 0; slice < 6; slice++) total += (packed >> (2 * slice)) & 3;

            return total;
        }

        var scores = new List<(int At, int Ok, int None, int Over)>();

        for (int at = 0; at + 1 < SpeciesData.SizeBytes; at++)
        {
            int ok = live.Count(r => TotalAt(r, at) is >= 1 and <= 3);
            int none = live.Count(r => TotalAt(r, at) == 0);

            scores.Add((at, ok, none, live.Count - ok - none));
        }

        foreach ((int at, int ok, int none, int over) in scores.OrderByDescending(s => s.Ok).Take(5))
            Console.WriteLine($"  bytes {at,2} and {at + 1,-2}   one to three: {ok,4}   none: {none,4}   impossible: {over,4}");

        int best = scores.OrderByDescending(s => s.Ok).First().At;

        // The exceptions, named. A run of consecutive indices is a block the cartridge
        // keeps and never uses; anything scattered would mean the reading is wrong.
        List<int> impossible =
            [.. Enumerable.Range(0, count).Where(i => records[i][0] != 0 && TotalAt(records[i], best) > 3)];

        Console.WriteLine();

        if (impossible.Count == 0)
        {
            Console.WriteLine($"  Bytes {best} and {best + 1} hold them, with no exceptions at all.");
        }
        else
        {
            bool run = impossible[^1] - impossible[0] + 1 == impossible.Count;

            Console.WriteLine(
                $"  Bytes {best} and {best + 1} hold them. The {impossible.Count} records where that " +
                $"reading is impossible are {impossible[0]} to {impossible[^1]}, " +
                (run ? "one unbroken run — a block of the table, not a scatter of misreadings."
                     : "scattered, which is a reason to doubt this."));
        }

        // ---- and which slice is which stat --------------------------------------------
        string[] names = ["HP", "attack", "defence", "speed", "sp. attack", "sp. defence"];

        Console.WriteLine();
        Console.WriteLine("Which slice is which stat");
        Console.WriteLine();
        Console.WriteLine("  Each row is the species yielding in that slice and nothing else. The columns");
        Console.WriteLine("  are how often each base stat is that species' highest. A slice that means a");
        Console.WriteLine("  stat should agree with itself and with nothing else.");
        Console.WriteLine();
        Console.WriteLine("        " + string.Join("", names.Select(n => n.PadLeft(12))));

        for (int slice = 0; slice < 6; slice++)
        {
            var hits = new int[6];
            int n = 0;

            foreach (byte[] r in live)
            {
                int packed = r[best] | (r[best + 1] << 8);

                if (((packed >> (2 * slice)) & 3) == 0) continue;
                if (Enumerable.Range(0, 6).Any(o => o != slice && ((packed >> (2 * o)) & 3) != 0)) continue;

                n++;

                int[] bases = [r[0], r[1], r[2], r[3], r[4], r[5]];
                int highest = bases.Max();

                for (int stat = 0; stat < 6; stat++)
                    if (bases[stat] == highest) hits[stat]++;
            }

            Console.WriteLine(
                $"  {slice} n={n,3}" + string.Join("", hits.Select(h => $"{(n == 0 ? 0 : h * 100 / n),11}%")));
        }

        Console.WriteLine();
        Console.WriteLine("  The order that reads off the diagonal is the six-stat order this project");
        Console.WriteLine("  already uses everywhere: HP, attack, defence, speed, sp. attack, sp. defence.");
    }

    private static void WriteMoveEffects(Rom rom)
    {
        List<MoveData> moves = MoveExtractor.Extract(rom);

        Console.WriteLine();
        Console.WriteLine("Moves by the target byte in their record");

        foreach (var aim in moves.Where(m => m.Id > 0).GroupBy(m => m.Target).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine(
                $"  0x{aim.Key:X2}  {aim.Count(),4} moves   " +
                string.Join(", ", aim.Take(6).Select(m => m.Name)));
        }

        // The check that makes the byte mean something. Every move whose record aims it at
        // the user is one whose whole effect is on the user, and no move outside that
        // group is — so a modelled effect that lands on the other one while its record
        // aims at the user would be this engine disagreeing with the cartridge.
        List<MoveData> wrong =
        [
            .. moves.Where(m => m.Id > 0 && m.AimsAtSelf)
                .Where(m => MoveEffects.Of(m.Effect).Kind is not (EffectKind.None or EffectKind.Nothing))
                .Where(m => !MoveEffects.Of(m.Effect).OnUser),
        ];

        Console.WriteLine(
            wrong.Count == 0
                ? "  every move aimed at the user has its effect applied to the user"
                : $"  {wrong.Count} moves are aimed at the user and applied to the other one: " +
                  string.Join(", ", wrong.Select(m => m.Name)));

        Console.WriteLine();
        Console.WriteLine("Moves by the effect byte in their record");

        var byEffect = moves
            .Where(m => m.Id > 0)
            .GroupBy(m => m.Effect)
            .OrderByDescending(g => g.Count())
            .ToList();

        int status = moves.Count(m => m.Id > 0 && m.Category == DamageCategory.Status);

        Console.WriteLine();
        List<MoveData> real = [.. moves.Where(m => m.Id > 0)];

        Console.WriteLine(
            $"  {real.Count} moves, {status} of them status moves, across {byEffect.Count} different effects");

        // The one number that says how much of a battle is actually happening. Everything
        // outside it is a move that announces itself and does nothing, which is the
        // battle engine's own version of a script that finishes saying nothing.
        Console.WriteLine(
            $"  {MoveEffects.Known(real)} of {real.Count} have an effect this engine knows how to do, " +
            $"{MoveEffects.Known(real.Where(m => m.Category == DamageCategory.Status))} of the {status} status moves");

        // The list that decides what to build next: the groups this engine does not
        // know, heaviest first. Everything else in this report is context.
        Console.WriteLine();
        Console.WriteLine("  What is still silent, by how many moves it costs (effect 0 is not silent,");
        Console.WriteLine("  it is a move with nothing to do beyond hitting).");
        Console.WriteLine();
        Console.WriteLine("  Marked groups already have part of what they do in the record rather than");
        Console.WriteLine("  in the effect byte, and that part is applied where the field is read: a");
        Console.WriteLine("  damaging group whose records carry no accuracy never misses, and one whose");
        Console.WriteLine("  records carry a priority moves out of turn. For 0x11 and 0x67 that is the");
        Console.WriteLine("  whole of it and they are not silent at all, which this report used to deny.");

        // A group of damaging moves every one of which carries the field is a group that
        // field explains — at least in part. Damaging on purpose: every status move on
        // this cartridge carries no accuracy, because that is what "always hits" looks
        // like in a record, so without that clause the rule swallows sixty-three groups
        // and says nothing.
        var inTheRecord = byEffect
            .Where(g => g.Key != 0)
            .Where(g => g.All(m => m.Power > 0))
            .Where(g => g.All(m => m.Accuracy == 0) || g.All(m => m.Priority != 0))
            .Select(g => g.Key)
            .ToHashSet();

        foreach (var group in byEffect
                     .Where(g => g.Key != 0 && Core.Battle.MoveEffects.Of(g.Key).Kind == Core.Battle.EffectKind.None)
                     .OrderByDescending(g => g.Count())
                     .Take(200))
        {
            string mark = inTheRecord.Contains(group.Key)
                ? group.All(m => m.Accuracy == 0) ? "  <- never misses, off the record" : "  <- moves out of turn, off the record"
                : "";

            Console.WriteLine(
                $"    0x{group.Key:X2}  {group.Count(),3} moves   " +
                string.Join(", ", group.Select(m => m.Name).Take(6)) + mark);
        }

        // And the number that matters, which is the one this report never gave: how much
        // of the silence is real. A group whose own record carries the whole answer is
        // not silent, and counting it as silent is how a report says there is more left
        // to do than there is.
        List<IGrouping<byte, MoveData>> stillQuiet =
        [
            .. byEffect.Where(g =>
                g.Key != 0
                && Core.Battle.MoveEffects.Of(g.Key).Kind == Core.Battle.EffectKind.None
                && !inTheRecord.Contains(g.Key))
        ];

        Console.WriteLine();
        Console.WriteLine(
            $"  {stillQuiet.Count} groups covering {stillQuiet.Sum(g => g.Count())} moves are silent and should not be. " +
            $"{inTheRecord.Count} more are quiet on purpose,");
        Console.WriteLine(
            $"  and effect 0 — {byEffect.First(g => g.Key == 0).Count()} moves — has nothing to say beyond hitting.");

        WriteRidersByType(moves);

        // The other marker in a record, and the sharper of the two. Printed in full
        // because the claim is a strong one — no group mixes it — and a claim like that
        // is worth being able to check at a glance.
        var one = byEffect.Where(g => g.All(m => m.Power == 1)).ToList();

        Console.WriteLine();
        Console.WriteLine(
            $"  A power of one is not a power. {one.Sum(g => g.Count())} moves in {one.Count} groups carry it,");
        Console.WriteLine(
            "  and not one group mixes it with a real power — so it is the record saying");
        Console.WriteLine(
            "  the number is somewhere else. Answered where somewhere else is inside the");
        Console.WriteLine(
            "  fight; left alone where it is in the game's code, because a constant written");
        Console.WriteLine(
            "  here from memory of another game is the mistake this project rules out.");

        foreach (var group in one.OrderBy(g => g.Key))
        {
            bool answered = Core.Battle.MoveEffects.Of(group.Key).Kind != Core.Battle.EffectKind.None;

            Console.WriteLine(
                $"    0x{group.Key:X2}  {group.Count(),2}  {(answered ? "->" : "  ")} " +
                string.Join(", ", group.Select(m => m.Name)));
        }

        Console.WriteLine();
        Console.WriteLine("  The effects with the most status moves in them");

        foreach (var group in byEffect
                     .Where(g => g.Any(m => m.Category == DamageCategory.Status))
                     .OrderByDescending(g => g.Count(m => m.Category == DamageCategory.Status)))
        {
            List<MoveData> silent = [.. group.Where(m => m.Category == DamageCategory.Status)];

            Console.WriteLine(
                $"    0x{group.Key:X2}  {silent.Count,3} status of {group.Count(),3}   " +
                string.Join(", ", silent.Select(m => m.Name)));
        }

        // And the other half of the same question: an effect that also has damaging moves
        // in it is an effect that already fires, and whatever it does is being skipped
        // only for the powerless ones.
        // The two runs the numbering falls into, printed whole. A table with a shape
        // is worth more than a table of special cases, and the shape is only visible
        // when the empty-looking slots between the famous moves are printed too.
        Console.WriteLine();
        Console.WriteLine("  0x00-0x18 and 0x32-0x46, every member");

        foreach (var group in byEffect
                     .Where(g => g.Key is (<= 0x18) or (>= 0x32 and <= 0x46))
                     .OrderBy(g => g.Key))
        {
            Console.WriteLine($"    0x{group.Key:X2}  " + string.Join(", ", group.Select(m => m.Name)));
        }

        Console.WriteLine();
        Console.WriteLine("  The effects with no status move in them at all, by size");

        foreach (var group in byEffect
                     .Where(g => g.All(m => m.Category != DamageCategory.Status))
                     .Take(8))
        {
            Console.WriteLine(
                $"    0x{group.Key:X2}  {group.Count(),3} moves   " +
                string.Join(", ", group.Take(6).Select(m => m.Name)));
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
                    // Every command that can put a number in a variable, not just the
                    // obvious one. Asking this tool who writes 0x4055 and being told
                    // "nobody, ever" is only useful if it was looking at all of them.
                    string what = command.Arguments.Length < 4 || command.Word() != variable ? "" : command.Code switch
                    {
                        0x16 => $"sets it to {command.Word(2)}",
                        0x17 => $"adds {command.Word(2)} to it",
                        0x18 => $"takes {command.Word(2)} off it",
                        0x19 => $"copies 0x{command.Word(2):X4} into it",
                        0x1A => $"copies 0x{command.Word(2):X4} into it if that is not zero",
                        0x26 => $"puts the answer to routine 0x{command.Word(2):X4} in it",
                        0x21 => $"compares it with {command.Word(2)}",
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
    /// Everybody in the world who hands over a monster, and what.
    /// <para>
    /// The check on a width. 0x79 was adopted on four sites whose next byte was a real
    /// command at fifteen and at no other length; this is the other half — whether the
    /// arguments read as anything. A species out of range, a level of nought or a
    /// hundred and one, or a count that is not roughly the number of gift monsters a
    /// Pokémon game has, and the width is wrong however well it parses.
    /// </para>
    /// </summary>
    /// <summary>
    /// Runs one script exactly as the client would, and says what came out.
    /// <para>
    /// Not the same as running it with a default save, which is what every other report
    /// here does. The client runs with a save that has a gender on it and a party
    /// attached, and "the tool prints three pages and the game shows none" is a
    /// difference this had no way to see.
    /// </para>
    /// </summary>
    private static void RunAsTheClientWould(Rom rom, uint address)
    {
        foreach (bool girl in new[] { false, true })
        foreach (int inParty in new[] { 0, 1 })
        {
            var save = new ScriptState { IsGirl = girl };

            ScriptRun run = ScriptRunner.Run(
                rom, address, save.WithParty(Enumerable.Repeat<IReadOnlyList<int>>([1], inParty)));

            Console.WriteLine();
            Console.WriteLine($"  as a {(girl ? "girl" : "boy")} with {inParty} in the party:");

            if (run.StoppedAt is { } code) Console.WriteLine($"    stopped at 0x{code:X2}");

            if (run.Pages.Count == 0) Console.WriteLine("    says nothing");

            foreach (string page in run.Pages)
                Console.WriteLine($"    \"{GameText.ToAscii(page).Replace('\n', ' ')}\"");

            // The beats, in order, because a scene that never ends is a scene stuck on
            // one of these and there is no way to know which by looking at the pages.
            foreach (SceneBeat beat in run.Beats)
            {
                Console.WriteLine(beat switch
                {
                    SceneBeat.Say say => $"      say: \"{GameText.ToAscii(say.Page).Replace('\n', ' ')}\"",
                    SceneBeat.Walk walk =>
                        $"      walk {(walk.IsPlayer ? "the player" : $"person {walk.PersonId}")}: " +
                        $"{walk.Steps.Count} steps [{string.Join(" ", walk.Steps.Select(b => $"{b:X2}"))}]",
                    _ => "      ?",
                });
            }
        }
    }

    /// <summary>
    /// Which standard routine is a question, derived rather than remembered.
    /// <para>
    /// The routines are called by number and the table of them is code-referenced, so
    /// what any of them does has never been readable here. One of them is the yes/no box
    /// — every "would you like…" in the game — and it announces itself by what comes
    /// after it: a question is only worth asking if somebody then looks at the answer,
    /// and the answer lands in 0x800D.
    /// </para>
    /// <para>
    /// So: for each routine number, how often the very next command compares 0x800D.
    /// Whichever number is the question does it nearly always; the ones that merely put
    /// a page on the screen do it nearly never.
    /// </para>
    /// </summary>
    private static void WriteStandardRoutines(Rom rom)
    {
        MapLibrary library = MapLibrary.Open(rom);

        var calls = new Dictionary<int, int[]>();   // routine -> [total, asked about]

        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> scripts =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
                .. map.Signs.Where(s => s.HasScript).Select(s => s.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
            ];

            foreach (uint script in scripts.Distinct())
            {
                List<ScriptCommand> read = ScriptReader.ReadAll(rom, script);

                for (int i = 0; i < read.Count; i++)
                {
                    if (read[i].Code is not (ScriptCommands.CallStandard or 0x08)) continue;
                    if (read[i].Arguments.Length < 1) continue;

                    int routine = read[i].Arguments[0];

                    if (!calls.TryGetValue(routine, out int[]? tally)) calls[routine] = tally = new int[2];

                    tally[0]++;

                    // The very next command, and only that one. A compare three
                    // instructions later is about something else.
                    if (i + 1 < read.Count && read[i + 1].Code == 0x21 && read[i + 1].Word() == 0x800D)
                        tally[1]++;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("Standard routines, and whether anybody looks at the answer");
        Console.WriteLine();

        foreach ((int routine, int[] tally) in calls.OrderByDescending(e => e.Value[0]))
        {
            Console.WriteLine(
                $"  routine {routine,3}: {tally[0],5} calls, {tally[1],5} followed at once by a compare on 0x800D " +
                $"({(tally[0] == 0 ? 0 : (double)tally[1] / tally[0]):P0})");
        }
    }

    private static void WriteGiftMons(Rom rom)
    {
        MapLibrary library = MapLibrary.Open(rom);

        // Names, so the check is legible rather than a list of numbers. LAPRAS at 25 is
        // a fact anybody can weigh; species 131 at 25 is not.
        var names = new Dictionary<int, string>();

        if (TableLocator.Locate(rom).SpeciesNames is { } table)
        {
            for (int i = 1; i <= 411; i++)
            {
                int at = table.Offset + i * GameText.SpeciesNameLength;
                if (at + GameText.SpeciesNameLength > rom.Length) break;

                names[i] = GameText.Decode(rom.Slice(at, GameText.SpeciesNameLength));
            }
        }

        var found = new List<string>();
        int outOfRange = 0;

        foreach (LoadedMap map in library.All())
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            List<(string Who, uint Script)> scripts =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => ($"person {o.LocalId} at {o.Square}", o.ScriptAddress)),
                .. map.Triggers.Where(t => t.HasScript).Select(t => ($"square {t.Square}", t.ScriptAddress)),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => ("arriving here", e.ScriptAddress)),
            ];

            foreach ((string who, uint script) in scripts.DistinctBy(e => e.Script))
            {
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, script))
                {
                    if (command.Code != 0x79 || command.Arguments.Length < 4) continue;

                    int named = command.Word();
                    int level = command.Word(2);

                    string species = named >= 0x4000
                        ? $"whatever 0x{named:X4} holds"
                        : names.GetValueOrDefault(named, $"#{named}");

                    if (named < 0x4000 && (named is <= 0 or > 411 || level is <= 0 or > 100)) outOfRange++;

                    found.Add($"    {mapId} {map.Name}: {who} gives {species} at level {level}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("Who hands over a monster");
        Console.WriteLine();
        Console.WriteLine($"  {found.Count} places, {outOfRange} of them naming a species or level out of range");
        Console.WriteLine();

        foreach (string line in found.Take(40)) Console.WriteLine(line);
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
                (person.HiddenBy != 0 ? $", gone once flag 0x{person.HiddenBy:X4} is set" : "") +
                (person.IsTrainer ? $", trainer {person.TrainerId} facing {person.Facing} seeing {person.SightRange}" : "") +
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
                (trigger.CanBeFought ? $", fights trainer {string.Join(" or ", trigger.Fights)}" : ""));

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

            // Kept apart from the pages, because it is: the line belongs to the fight
            // and the fight has its own screen.
            foreach (string page in run.Challenge)
                Console.WriteLine($"      on the way in: \"{GameText.ToAscii(page).Replace("\n", " ")}\"");

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
    /// <summary>
    /// Which substitution codes this cartridge's dialogue uses, how often, and in what
    /// sentences.
    /// <para>
    /// Every line of the opening reads "{FD}{06}: I'll take this one, then!" because
    /// 0xFD is a marker meaning "put something here" and the byte after it says what.
    /// Which byte means which thing is not written down anywhere in the image, so it is
    /// derived the only way anything is derived here: count every site, print the
    /// sentences, and let the sentences say it. A code that only ever appears after
    /// "received the" and before "from PROF. OAK" is a species name, and no amount of
    /// remembering another game's table is worth as much as that one observation.
    /// </para>
    /// </summary>
    /// <summary>
    /// Whether the flag that takes an object off the map is the same number a script
    /// sets, or an offset one.
    /// <para>
    /// The three balls on the professor's table are hidden by flags 0x0028, 0x0029 and
    /// 0x002A, and the script that hands one over sets 0x0828. Both cannot be right, and
    /// which is right is not something to remember — so both are counted. If the object
    /// numbering and the script numbering are the same space, the raw values will be the
    /// ones scripts set; if they are separated by a constant, the shifted values will be.
    /// </para>
    /// </summary>
    /// <summary>
    /// Runs of short names in the image that are not any table this project already
    /// knows, which is how the rival's name menu gets found.
    /// <para>
    /// The game offers a list of names for him during an intro this project does not
    /// run, and until it is located the rival is called "RIVAL" — a placeholder, and
    /// deliberately one, because writing a name here from memory of the games is exactly
    /// the guess everything else refuses to make.
    /// </para>
    /// <para>
    /// The shape searched for is the shape every other name table in this cartridge has:
    /// a fixed stride, one terminator per record, zero fill after it. No expected names
    /// and no expected address go into the search, so what comes back is read rather
    /// than confirmed.
    /// </para>
    /// </summary>
    /// <summary>
    /// One map's walkability as text, which is the fastest way to answer "how do I get
    /// from here to there" without walking it.
    /// <para>
    /// Written after an hour of finding out that Route 1's ledges cannot be read off a
    /// rendered picture: a ledge and a path are the same colour at that size, and the
    /// only way to tell was to walk into them. The collision data has known the answer
    /// all along.
    /// </para>
    /// </summary>
    /// <summary>
    /// Whether the way trainers are facing is the way they are facing.
    /// <para>
    /// A bug catcher in Viridian Forest reads as facing Up with a sight range of one,
    /// and the row above him is solid across the whole map — a trainer who can never see
    /// anybody. One of those is a curiosity; a direction's worth of them is a mapping
    /// read the wrong way round.
    /// </para>
    /// <para>
    /// The test is the square in front. A trainer exists to catch somebody standing in
    /// their line of sight, so a facing that points at a wall on most of the trainers
    /// that carry it is a facing this project has misread.
    /// </para>
    /// </summary>
    /// <summary>
    /// What runs in front of a line of dialogue with a gap in it.
    /// <para>
    /// Two gaps in this cartridge's text are still gaps: <c>{FD}{02}</c> and
    /// <c>{FD}{03}</c>. One command is known to fill one — 0x7D writes a species name —
    /// and it accounts for a minority of the sites. The rest are filled by something
    /// this reader steps over, and the professor's "{FD}{02} POKéMON seen" says at least
    /// one of them puts a number there rather than a name.
    /// </para>
    /// <para>
    /// So: for every line with a gap, tally what ran in front of it, and for every line
    /// without one, tally the same. A command that fills gaps sits in front of gap lines
    /// and nowhere else; lock, faceplayer and the rest sit in front of everything and
    /// the second column is what says so. The instrument decides, not the argument.
    /// </para>
    /// </summary>
    private static void WriteGaps(Rom rom, string like)
    {
        // Far enough back to cover a run-up of several buffers, short enough that the
        // command belongs to this line rather than the conversation.
        const int Window = 12;

        MapLibrary library = MapLibrary.Open(rom);

        var beforeGaps = new Dictionary<byte, int>();
        var beforeRest = new Dictionary<byte, int>();
        var examples = new Dictionary<byte, List<string>>();
        var sentences = new SortedSet<string>(StringComparer.Ordinal);

        int gapLines = 0;
        int restLines = 0;

        Console.WriteLine();
        Console.WriteLine("What runs in front of a line with a gap in it");

        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> addresses =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
            ];

            foreach (uint address in addresses.Distinct())
            {
                List<ScriptCommand> read = ScriptReader.ReadAll(rom, address);

                for (int i = 0; i < read.Count; i++)
                {
                    // Both ways this cartridge says something: a message with the text
                    // in its own argument, and a pointer loaded for a standard routine
                    // to say a command or two later.
                    if (read[i].Code is not (ScriptCommands.Message or ScriptCommands.LoadPointer)) continue;

                    uint text = read[i].Code == ScriptCommands.Message ? read[i].Pointer() : read[i].Pointer(1);

                    if (rom.ToOffsetOrNull(text) is not { } at) continue;

                    ReadOnlySpan<byte> bytes = rom.Span[at..];

                    if (!GameText.LooksLikeDialogue(bytes)) continue;

                    string said = string.Join(" ", GameText.DecodeDialogue(bytes));
                    bool hasGap = said.Contains("{FD}{02}", StringComparison.Ordinal)
                        || said.Contains("{FD}{03}", StringComparison.Ordinal);

                    if (hasGap)
                    {
                        gapLines++;
                        sentences.Add(said.Replace("\n", " "));
                    }
                    else
                    {
                        restLines++;
                    }

                    // Once per line, not once per repetition — a loop that locks twice
                    // would otherwise vote twice.
                    var ran = new HashSet<byte>();

                    for (int j = Math.Max(0, i - Window); j < i; j++) ran.Add(read[j].Code);

                    foreach (byte code in ran)
                    {
                        if (hasGap) beforeGaps[code] = beforeGaps.GetValueOrDefault(code) + 1;
                        else beforeRest[code] = beforeRest.GetValueOrDefault(code) + 1;
                    }

                    if (!hasGap) continue;

                    foreach (byte code in ran)
                    {
                        List<string> seen = examples.TryGetValue(code, out List<string>? had) ? had : examples[code] = [];

                        if (seen.Count < 3) seen.Add($"0x{address:X8}  {said.Replace("\n", " ")}");
                    }
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  {gapLines} lines with a gap, {restLines} without, {sentences.Count} of them different");
        Console.WriteLine();
        Console.WriteLine("  Every different one, because what goes in a gap is legible from the sentence");

        foreach (string line in sentences) Console.WriteLine($"    \"{line}\"");
        Console.WriteLine();
        Console.WriteLine("  code   in front of a gap   in front of the rest   share of its sites");

        foreach ((byte code, int gaps) in beforeGaps.OrderByDescending(e => e.Value).Take(14))
        {
            int rest = beforeRest.GetValueOrDefault(code);

            Console.WriteLine(
                $"  0x{code:X2}   {gaps,5} / {gapLines}          {rest,5} / {restLines}" +
                $"           {100.0 * gaps / (gaps + rest),5:F1} %");
        }

        // No summary line, and that is a finding rather than a gap in the tool. The
        // three commands that fill gaps sit in front of a minority of them each; the
        // rest are filled by the game's own code, and a tally cannot see a command that
        // was never written down. What settles one of these is the run-up below.

        // The tally says which codes are near gaps; this says what the run-up actually
        // looks like, which is the part a person has to read.
        Console.WriteLine();
        Console.WriteLine("  The last few commands in front of some of them");

        int shown = 0;

        foreach (LoadedMap map in library.All())
        {
            if (shown >= 8) break;

            IEnumerable<uint> addresses =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
            ];

            foreach (uint address in addresses.Distinct())
            {
                if (shown >= 8) break;

                List<ScriptCommand> read = ScriptReader.ReadAll(rom, address);

                for (int i = 0; i < read.Count && shown < 8; i++)
                {
                    if (read[i].Code is not (ScriptCommands.Message or ScriptCommands.LoadPointer)) continue;

                    uint text = read[i].Code == ScriptCommands.Message ? read[i].Pointer() : read[i].Pointer(1);

                    if (rom.ToOffsetOrNull(text) is not { } at) continue;

                    ReadOnlySpan<byte> bytes = rom.Span[at..];

                    if (!GameText.LooksLikeDialogue(bytes)) continue;

                    string said = string.Join(" ", GameText.DecodeDialogue(bytes));

                    if (!said.Contains("{FD}{02}", StringComparison.Ordinal)
                        && !said.Contains("{FD}{03}", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (like.Length > 0 && !said.Contains(like, StringComparison.OrdinalIgnoreCase)) continue;

                    shown++;

                    Console.WriteLine();
                    Console.WriteLine($"    0x{address:X8}  \"{said.Replace("\n", " ")}\"");

                    for (int j = Math.Max(0, i - 6); j <= i; j++)
                    {
                        string args = string.Join(" ", read[j].Arguments.ToArray().Select(b => $"{b:X2}"));

                        Console.WriteLine($"      0x{read[j].Code:X2}  {args}");
                    }
                }
            }
        }

    }

    /// <summary>
    /// Every <c>trainerbattle</c> on the cartridge, by variant, and what its pointers are.
    /// <para>
    /// The command has a different length for each variant, and the longer ones carry a
    /// pointer past the two everybody has. What that pointer holds is the question BROCK
    /// raised: the badge, the TM and every flag a gym win sets live at the end of it, and
    /// nothing here followed it.
    /// </para>
    /// <para>
    /// Script or text is not asserted, it is measured — the same way every text pointer
    /// in this project is checked, by decoding what is there and asking whether it reads
    /// as speech.
    /// </para>
    /// </summary>
    private static void WriteFightKinds(Rom rom)
    {
        MapLibrary library = MapLibrary.Open(rom);

        var kinds = new Dictionary<byte, int>();
        var speech = new Dictionary<byte, int>();
        var code = new Dictionary<byte, int>();
        var examples = new Dictionary<byte, List<string>>();

        Console.WriteLine();
        Console.WriteLine("Every trainerbattle, by variant");

        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> addresses =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
            ];

            foreach (uint address in addresses.Distinct())
            {
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
                {
                    if (command.Code != ScriptCommands.TrainerBattle) continue;

                    byte kind = command.Arguments[0];

                    kinds[kind] = kinds.GetValueOrDefault(kind) + 1;

                    // The pointer past the two every variant has.
                    if (command.Arguments.Length < 17) continue;

                    uint third = command.Pointer(13);

                    if (rom.ToOffsetOrNull(third) is not { } at) continue;

                    bool reads = GameText.LooksLikeDialogue(rom.Span[at..]);

                    if (reads) speech[kind] = speech.GetValueOrDefault(kind) + 1;
                    else code[kind] = code.GetValueOrDefault(kind) + 1;

                    List<string> seen = examples.TryGetValue(kind, out List<string>? had) ? had : examples[kind] = [];

                    if (seen.Count < 3)
                    {
                        seen.Add(
                            $"0x{address:X8} trainer {command.Word(1)} -> 0x{third:X8}  " +
                            (reads
                                ? $"\"{string.Join(" ", ScriptReader.ReadDialogue(rom, third)).Replace("\n", " ")}\""
                                : string.Join(
                                    ", ",
                                    ScriptReader.Read(rom, third).Take(4).Select(c => ScriptCommands.NameOf(c.Code)))));
                    }
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("  kind   sites   third pointer reads as speech / as script");

        foreach ((byte kind, int count) in kinds.OrderByDescending(e => e.Value))
        {
            Console.WriteLine(
                $"  {kind,4}   {count,5}   {speech.GetValueOrDefault(kind),4} / {code.GetValueOrDefault(kind),4}" +
                (ScriptCommands.TrainerBattleLength(kind) is { } length ? $"   ({length + 1} bytes)" : "   (unknown)"));

            foreach (string one in examples.GetValueOrDefault(kind, [])) Console.WriteLine($"      {one}");
        }
    }

    /// <summary>
    /// What every trainer in the game says on the way into their fight.
    /// <para>
    /// The number is the point. This project opened four hundred and fifty fights with
    /// one sentence of its own making, and the cartridge had a different one for almost
    /// every trainer sitting three arguments into the command that starts the fight.
    /// </para>
    /// <para>
    /// Counted the same way everything else here is: by finding it from the trainer id
    /// through the same function the client uses, rather than by trusting that the
    /// pointer is where it ought to be. A line that does not decode as speech is not
    /// counted, because a wrong pointer that prints anyway is the failure this project
    /// is arranged against.
    /// </para>
    /// </summary>
    private static void WriteChallenges(Rom rom)
    {
        MapLibrary library = MapLibrary.Open(rom);

        int fields = 0;
        int found = 0;
        var silent = new List<int>();
        var shown = new List<string>();

        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> addresses =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
            ];

            foreach (uint address in addresses.Distinct())
            {
                foreach (int trainer in ScriptReader.FindTrainers(rom, address))
                {
                    fields++;

                    if (ScriptReader.BeforeTheFight(rom, address, trainer) is not { } said)
                    {
                        silent.Add(trainer);
                        continue;
                    }

                    found++;

                    List<string> pages = ScriptRunner.Speech(rom, said);

                    if (shown.Count < 12 && pages.Count > 0)
                        shown.Add($"trainer {trainer,4}  \"{pages[0].Replace('\n', ' ')}\"");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("What a trainer says on the way in");
        Console.WriteLine();
        Console.WriteLine($"  {fields} fights a script can start, {found} of them opening with words of their own");
        Console.WriteLine($"  {silent.Count} without: the variant that carries no intro text");
        Console.WriteLine();

        foreach (string one in shown) Console.WriteLine($"    {one}");
    }

    /// <summary>
    /// Where a door puts you, and whether there is anywhere to step from there.
    /// <para>
    /// Walking out of a building leaves you standing on the door — that is what the
    /// cartridge's warp says, and the games then walk you one square clear of it. Without
    /// that step the mat is a place you get stuck: the arrival that would take you back
    /// inside has already happened, so pressing towards the door does nothing and you
    /// have to step off and back on.
    /// </para>
    /// <para>
    /// Which way to step is the question, and it is asked of the cartridge rather than
    /// answered from memory of the games. A door is already defined here as a warp on a
    /// square the block data calls solid; this counts, for every one of them, which of the
    /// four neighbours you could actually stand on.
    /// </para>
    /// </summary>
    private static void WriteDoorSteps(Rom rom)
    {
        Core.World.WorldData world = WorldExporter.Export(rom);

        Console.WriteLine();
        Console.WriteLine("Doors, and where there is room to step");

        // A warp with a door at either end is a building, and a building is walked out
        // of rather than stood in. Asked of both ends because the two ends are one thing:
        // the outside of a shop is a door and the mat inside it is not, and the step
        // happens in both directions.
        var counts = new Dictionary<Direction, int>();
        var only = new Dictionary<Direction, int>();

        int through = 0;
        int stuck = 0;
        int elsewhere = 0;
        var nowhere = new List<string>();

        foreach (MapData map in world.Maps)
        {
            foreach (Warp warp in map.Warps)
            {
                if (world.Find(warp.TargetMapId) is not { } target) continue;

                GridPosition arrival = warp.TargetWarpId >= 0 && warp.TargetWarpId < target.Warps.Count
                    ? target.Warps[warp.TargetWarpId].Square
                    : new GridPosition(-1, -1);

                if (arrival.X < 0) continue;

                if (!map.IsDoor(warp.Square) && !target.IsDoor(arrival))
                {
                    elsewhere++;
                    continue;
                }

                through++;

                CollisionGrid grid = target.ToGrid();
                var open = new List<Direction>();

                foreach (Direction way in (Direction[])[Direction.Down, Direction.Up, Direction.Left, Direction.Right])
                {
                    GridPosition next = arrival.Step(way);

                    // A neighbour that is itself a warp is not a way out — shop fronts
                    // are three doors side by side, and stepping along one lands on
                    // another.
                    if (!grid.IsWalkable(next) || target.WarpAt(next) is not null) continue;

                    open.Add(way);
                    counts[way] = counts.GetValueOrDefault(way) + 1;
                }

                if (open.Count > 0) only[open[0]] = only.GetValueOrDefault(open[0]) + 1;
                else
                {
                    stuck++;
                    if (nowhere.Count < 6) nowhere.Add($"{target.Id} {target.Name} at {arrival}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"  {through} warps with a door at one end or the other, {stuck} of those " +
            "arriving somewhere with nowhere to step");

        Console.WriteLine($"  {elsewhere} others — stairs, cave mouths, ladders — left alone");
        Console.WriteLine();
        Console.WriteLine("  way      can step   taken first");

        foreach (Direction way in (Direction[])[Direction.Down, Direction.Up, Direction.Left, Direction.Right])
        {
            Console.WriteLine(
                $"  {way,-8} {counts.GetValueOrDefault(way),5} / {through}   {only.GetValueOrDefault(way),5}");
        }

        foreach (string one in nowhere) Console.WriteLine($"    nowhere to go: {one}");
    }

    private static void WriteSightLines(Rom rom)
    {
        MapLibrary library = MapLibrary.Open(rom);

        var open = new Dictionary<Direction, int>();
        var walled = new Dictionary<Direction, int>();

        Console.WriteLine();
        Console.WriteLine("Which way trainers are looking, and whether anything is there");

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.IsTrainer && o.SightRange > 0))
            {
                GridPosition front = new GridPosition(person.X, person.Y).Step(person.Facing);

                if (map.Collision.IsWalkable(front)) open[person.Facing] = open.GetValueOrDefault(person.Facing) + 1;
                else walled[person.Facing] = walled.GetValueOrDefault(person.Facing) + 1;
            }
        }

        // Down is also what an unrecognised movement type falls back to, so a healthy
        // figure for Down could be the fallback flattering itself. Counting the types
        // separates the two: a trainer whose type is one of the four this project knows
        // is a trainer whose facing was read, not guessed.
        var types = new Dictionary<int, int>();

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.IsTrainer && o.SightRange > 0))
                types[person.MovementType] = types.GetValueOrDefault(person.MovementType) + 1;
        }

        Console.WriteLine(
            "  movement types: " +
            string.Join(", ", types.OrderByDescending(t => t.Value).Take(8).Select(t => $"{t.Key}x{t.Value}")));

        // And the decisive split. A movement type this project recognises gives a facing
        // that was read; anything else gives Down because Down is the fallback. If the
        // fallback is a good guess the two rates will look alike, and if it is not, the
        // guessed ones will look like chance.
        int knownOpen = 0, knownAll = 0, guessedOpen = 0, guessedAll = 0;

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.IsTrainer && o.SightRange > 0))
            {
                bool read = person.MovementType is >= 3 and <= 10;
                bool clear = map.Collision.IsWalkable(new GridPosition(person.X, person.Y).Step(person.Facing));

                if (read)
                {
                    knownAll++;
                    if (clear) knownOpen++;
                }
                else
                {
                    guessedAll++;
                    if (clear) guessedOpen++;
                }
            }
        }

        Console.WriteLine(
            $"  facing read from the type: {knownOpen}/{knownAll} " +
            $"({(knownAll == 0 ? 0 : 100.0 * knownOpen / knownAll):F0} %)");

        Console.WriteLine(
            $"  facing guessed as Down:    {guessedOpen}/{guessedAll} " +
            $"({(guessedAll == 0 ? 0 : 100.0 * guessedOpen / guessedAll):F0} %)");

        Console.WriteLine();

        foreach (Direction facing in (Direction[])[Direction.Up, Direction.Down, Direction.Left, Direction.Right])
        {
            int clear = open.GetValueOrDefault(facing);
            int blocked = walled.GetValueOrDefault(facing);
            int all = clear + blocked;

            Console.WriteLine(
                $"  {facing,-6} {all,4} trainers, {clear,4} can see the square in front " +
                $"({(all == 0 ? 0 : 100.0 * clear / all):F0} %)");
        }
    }

    private static void WriteWalkable(Rom rom, string mapId)
    {
        MapLibrary library = MapLibrary.Open(rom);

        if (library.TryLoad(mapId) is not { } map)
        {
            Console.WriteLine($"No map {mapId}.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"{mapId} {map.Name} — {map.Collision.Width}x{map.Collision.Height}");
        Console.WriteLine("  . walkable   # solid   \" grass   o warp   P person   T trigger");
        Console.WriteLine();

        var warps = map.Warps.Select(w => w.Square).ToHashSet();
        var people = map.Objects.Select(o => new GridPosition(o.X, o.Y)).ToHashSet();
        var triggers = map.Triggers.Select(t => t.Square).ToHashSet();

        Console.Write("     ");
        for (int x = 0; x < map.Collision.Width; x++) Console.Write(x % 10);
        Console.WriteLine();

        for (int y = 0; y < map.Collision.Height; y++)
        {
            Console.Write($"  {y,3} ");

            for (int x = 0; x < map.Collision.Width; x++)
            {
                var square = new GridPosition(x, y);

                Console.Write(
                    warps.Contains(square) ? 'o'
                    : people.Contains(square) ? 'P'
                    : triggers.Contains(square) ? 'T'
                    : !map.Collision.IsWalkable(square) ? '#'
                    : '.');
            }

            Console.WriteLine();
        }
    }

    private static void WriteNameRuns(Rom rom, RomExtractor extractor)
    {
        Console.WriteLine();
        Console.WriteLine("Runs of short names outside the tables already located");

        // The list the rival is named from, which is not a fixed-width table and so is
        // found by its run rather than by its stride.
        IReadOnlyList<string> suggestions = NameSuggestions.Locate(rom, Console.WriteLine);

        if (suggestions.Count > 0)
        {
            Console.WriteLine($"    all of them: {string.Join(", ", suggestions)}");
            Console.WriteLine($"    the first that is a name and not a menu option: {NameSuggestions.FirstName(suggestions)}");
        }


        // Everything already accounted for, so a hit inside one of them is not reported
        // as a discovery. The species names alone are 412 records and would drown it.
        var known = new List<(int From, int To)>();

        foreach (TableLocation? table in (TableLocation?[])[extractor.Tables.SpeciesNames, extractor.Tables.BaseStats])
        {
            if (table is not null) known.Add((table.Offset, table.Offset + table.EntrySize * table.EntryCount));
        }

        var found = new List<(int At, int Stride, int Count, List<string> Names)>();

        for (int stride = 8; stride <= 8; stride++)
        {
            for (int at = 0; at + stride * 4 < rom.Length; at += 1)
            {
                if (known.Any(k => at >= k.From && at <= k.To)) continue;

                var names = new List<string>();

                for (int i = 0; at + (i + 1) * stride <= rom.Length; i++)
                {
                    if (Named(rom, at + i * stride, stride) is not { } name) break;

                    names.Add(name);
                }

                if (names.Count is < 3 or > 12) continue;

                found.Add((at, stride, names.Count, names));
                at += names.Count * stride;
            }
        }

        foreach ((int at, int stride, int count, List<string> names) in found
                     .Where(f => f.Names.All(n => n.Length is >= 3 and <= 7 && n.All(char.IsUpper)))

                     // The easy-chat word bank is thousands of short uppercase words in
                     // exactly this shape and would fill any list this prints.
                     .Where(f => f.At < 0x3E0000 || f.At > 0x420000)
                     .OrderByDescending(f => f.Count)
                     .Take(24))
        {
            Console.WriteLine();
            Console.WriteLine($"  0x{Rom.BaseAddress + (uint)at:X8}  {count} names, {stride} bytes each");
            Console.WriteLine($"    {string.Join(", ", names.Take(12))}");
        }
    }

    /// <summary>
    /// One fixed-width record read as a name, or nothing when it is not one.
    /// <para>
    /// Letters, then a terminator, then zeroes to the end of the record — which is what
    /// a compiler does with an array of string literals, and what makes these tables
    /// findable at all. Two letters is the shortest thing worth calling a name.
    /// </para>
    /// </summary>
    private static string? Named(Rom rom, int at, int stride)
    {
        var text = new System.Text.StringBuilder();

        for (int i = 0; i < stride; i++)
        {
            byte b = rom.ReadU8(at + i);

            if (b == GameText.Terminator)
            {
                if (text.Length < 2) return null;

                // The fill after the terminator is zero in the tables already located,
                // but a menu of a few names need not have been written as an array of
                // literals — so anything is allowed after it and the run length is what
                // has to carry the evidence instead.
                return text.ToString();
            }

            if (b >= 0xBB && b < 0xBB + 26) text.Append((char)('A' + (b - 0xBB)));
            else if (b >= 0xD5 && b < 0xD5 + 26) text.Append((char)('a' + (b - 0xD5)));
            else return null;
        }

        return null;
    }

    private static void WriteHideFlags(Rom rom)
    {
        MapLibrary library = MapLibrary.Open(rom);

        var hidden = new HashSet<int>();
        var set = new HashSet<int>();

        Console.WriteLine();
        Console.WriteLine("Flags that take an object off the map");

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects)
            {
                if (person.HiddenBy != 0) hidden.Add(person.HiddenBy);
            }

            IEnumerable<uint> addresses =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
            ];

            foreach (uint address in addresses)
            {
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
                {
                    if (command.Code is 0x29 or 0x2A) set.Add(command.Word());
                }
            }
        }

        Console.WriteLine($"  {hidden.Count} flags hide something, {set.Count} flags are set or cleared by a script");

        // The other hypothesis, and the one the balls point at. If an object goes away
        // because a command says so rather than because a flag moved, the command will
        // be in the scripts of the things that go away — the item balls, every one of
        // which is picked up and vanishes — and nowhere near the ones that do not.
        var withCommand = new Dictionary<byte, (int Gifts, int Others)>();

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
            {
                ScriptRun run = ScriptRunner.Run(rom, person.ScriptAddress);
                bool gift = run.GivesItem is not null;

                foreach (byte code in ScriptReader
                             .ReadAll(rom, person.ScriptAddress)
                             .Select(c => c.Code)
                             .Distinct())
                {
                    (int gifts, int others) = withCommand.GetValueOrDefault(code);

                    withCommand[code] = gift ? (gifts + 1, others) : (gifts, others + 1);
                }
            }
        }

        // Sharper: pair every object that carries a hide flag with its own script, and
        // ask what that script sets. A global count lets any script in the game explain
        // any object's flag; this one does not.
        var pairs = new Dictionary<int, int>();
        int paired = 0;

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript && o.HiddenBy != 0))
            {
                paired++;

                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, person.ScriptAddress))
                {
                    if (command.Code is not (0x29 or 0x2A)) continue;

                    int difference = command.Word() - person.HiddenBy;

                    pairs[difference] = pairs.GetValueOrDefault(difference) + 1;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  {paired} objects carry a hide flag and have a script. What their own scripts set:");

        foreach ((int difference, int count) in pairs.OrderByDescending(e => e.Value).Take(6))
        {
            Console.WriteLine(
                $"    their own flag {(difference < 0 ? "-" : "+")} 0x{Math.Abs(difference):X4}  {count} times");
        }

        // 0x53 turns up in the ball script holding 0x800F, in the rival's holding
        // 0x4004, and once holding a plain 8 — which is the rival's own object number,
        // in the script where he walks out of the lab. If it means "this one is not here
        // any more", its arguments will be object numbers and nothing else.
        var arguments = new Dictionary<byte, List<int>>();

        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> addresses =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
            ];

            foreach (uint address in addresses)
            {
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
                {
                    if (command.Code is not (0x53 or 0x54 or 0x55)) continue;

                    (arguments.TryGetValue(command.Code, out List<int>? had)
                        ? had
                        : arguments[command.Code] = []).Add(command.Word());
                }
            }
        }

        // If 0x800F is ever written by a script, it is a working variable and 0x53's
        // meaning depends on what put it there. If it is never written and only read,
        // something outside the script language fills it — and the only thing outside
        // the script language that a person's own script knows about is the person.
        int writes = 0;

        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> all =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
            ];

            foreach (uint address in all)
            {
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
                {
                    if (command.Code is (0x16 or 0x17 or 0x18 or 0x19 or 0x1A) && command.Word() == 0x800F) writes++;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  0x800F is written by a script {writes} times");

        foreach ((byte code, List<int> words) in arguments.OrderBy(e => e.Key))
        {
            int objectLike = words.Count(w => w is > 0 and < 64);
            int variables = words.Count(w => w >= 0x4000);

            Console.WriteLine();
            Console.WriteLine(
                $"  0x{code:X2}  {words.Count} sites: {objectLike} look like an object number, " +
                $"{variables} are a variable, {words.Count - objectLike - variables} are neither");

            Console.WriteLine($"    values: {string.Join(", ", words.Distinct().OrderBy(w => w).Take(16))}");
        }

        // 0x44 and 0x46 both carry a word and a word, and 0x46 was adopted as the one
        // that hands an item over. If 0x44 is the same thing, its first word will be an
        // item the script then names in 0x8000 for the "obtained" fanfare — the two are
        // written a few commands apart at every real handover.
        foreach (byte give in (byte[])[0x44, 0x46])
        {
            int sites = 0;
            int announced = 0;

            foreach (LoadedMap map in library.All())
            {
                IEnumerable<uint> all =
                [
                    .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                    .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
                    .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
                ];

                foreach (uint address in all)
                {
                    List<ScriptCommand> read = ScriptReader.ReadAll(rom, address);

                    for (int i = 0; i < read.Count; i++)
                    {
                        if (read[i].Code != give) continue;

                        sites++;

                        int item = read[i].Word();

                        for (int j = i + 1; j < Math.Min(read.Count, i + 8); j++)
                        {
                            if (read[j].Code == 0x1A && read[j].Word() == 0x8000 && read[j].Word(2) == item)
                            {
                                announced++;
                                break;
                            }
                        }
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                $"  0x{give:X2}  {sites} sites, {announced} of them name their own first word " +
                $"into 0x8000 within a few commands");
        }

        Console.WriteLine();
        Console.WriteLine("  Commands that appear in the scripts of things you pick up, and how exclusively");

        foreach ((byte code, (int gifts, int others)) in withCommand
                     .Where(e => e.Value.Gifts >= 20)
                     .OrderByDescending(e => (double)e.Value.Gifts / (e.Value.Gifts + e.Value.Others))
                     .Take(8))
        {
            Console.WriteLine(
                $"    0x{code:X2}  in {gifts} pickup scripts and {others} others " +
                $"({100.0 * gifts / (gifts + others):F0} % of its sites are pickups)");
        }

        // Every offset worth trying, reported side by side rather than one at a time.
        // A survey that only tests the answer somebody expected cannot disagree with them.
        foreach (int shift in (int[])[0, 0x100, 0x200, 0x400, 0x800, 0x1000])
        {
            int matched = hidden.Count(f => set.Contains(f + shift));

            Console.WriteLine(
                $"    +0x{shift:X4}  {matched} of {hidden.Count} hide-flags are touched by a script " +
                $"({100.0 * matched / Math.Max(1, hidden.Count):F0} %)");
        }
    }

    private static void WriteSubstitutions(Rom rom)
    {
        MapLibrary library = MapLibrary.Open(rom);
        var counts = new Dictionary<byte, int>();
        var examples = new Dictionary<byte, List<string>>();

        Console.WriteLine();
        Console.WriteLine("Substitution codes in dialogue");

        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> addresses =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
            ];

            foreach (uint address in addresses)
            {
                foreach (string page in ScriptRunner.Run(rom, address).Pages)
                {
                    for (int i = page.IndexOf("{FD}", StringComparison.Ordinal); i >= 0;
                         i = page.IndexOf("{FD}", i + 1, StringComparison.Ordinal))
                    {
                        if (i + 8 > page.Length || page[i + 4] != '{') continue;
                        if (!byte.TryParse(page.AsSpan(i + 5, 2), System.Globalization.NumberStyles.HexNumber,
                                null, out byte which))
                        {
                            continue;
                        }

                        counts[which] = counts.GetValueOrDefault(which) + 1;

                        List<string> seen = examples.TryGetValue(which, out List<string>? had)
                            ? had
                            : examples[which] = [];

                        string flat = $"0x{address:X8}  {page.Replace("\n", " ")}";
                        if (seen.Count < 4 && !seen.Any(e => e.EndsWith(page.Replace("\n", " "), StringComparison.Ordinal))) seen.Add(flat);
                    }
                }
            }
        }

        foreach ((byte which, int count) in counts.OrderByDescending(e => e.Value))
        {
            Console.WriteLine();
            Console.WriteLine($"  {{FD}}{{{which:X2}}}  {count} times");

            foreach (string page in examples[which]) Console.WriteLine($"    \"{page}\"");
        }
    }

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
        int returned = 0;
        var derailed = new SortedSet<string>(StringComparer.Ordinal);
        int truncated = 0;

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
                if (run.CodeCalled.Count > 0)
                {
                    returned++;

                    // Named rather than counted. A width being weighed is judged on how
                    // many of these it costs, and "five more" is only an argument if the
                    // five can be looked at.
                    foreach (uint derail in run.CodeCalled) derailed.Add($"0x{address:X8} at 0x{derail:X8}");
                }
                if (ScriptReader.ReadAllTruncated(rom, address)) truncated++;

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

        // Kept apart from the stops on purpose. These are runs that were called into
        // something unreadable and returned from it, which is what the console does and
        // what a stop is not. Counting them together would let a width we have not
        // adopted hide inside a routine we can never adopt.
        Console.WriteLine($"  {returned} of those finish by returning from code they cannot read");

        foreach (string one in derailed) Console.WriteLine($"    {one}");
        Console.WriteLine($"  {truncated} read more blocks than the traversal limit allows");

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
    /// <summary>
    /// The doors that are not on any map, and which of the unreachable maps they open.
    /// <para>
    /// The world file's warps are squares: stand on this one, arrive there. That is every
    /// door the walker has ever known about, and it leaves 179 maps of 425 with nothing
    /// leading in — a whole archipelago, five department store floors, and the caves and
    /// towers behind them.
    /// </para>
    /// <para>
    /// A <c>warp</c> is also a script command, and a script can run one from anywhere.
    /// This asks every script on every map for the ones it contains, and then asks the
    /// walker which of the maps they name it had given up on.
    /// </para>
    /// </summary>
    private static void WriteScriptedDoors(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("doors a script makes, which are on no square");

        WorldData world = WorldExporter.Export(rom);

        Dictionary<string, MapData> maps = world.Maps.ToDictionary(m => m.Id);

        List<(MapData From, ScriptedDoor Door)> doors =
        [
            .. world.Maps.OrderBy(m => m.Id).SelectMany(m => m.Doors.Select(d => (m, d))),
        ];

        // Which maps have a doorway leading in, by the only kind of door the world file
        // has ever carried. Everything else is somewhere no square anywhere reaches.
        HashSet<string> byASquare =
        [
            .. world.Maps.SelectMany(m => m.Warps.Where(w => !w.IsDynamic).Select(w => w.TargetMapId)),
            .. world.Maps.SelectMany(m => m.Connections.Select(c => c.MapId)),
            world.Maps.First().Id,
        ];

        Console.WriteLine(
            $"  {doors.Count} of them, on {doors.Select(d => d.From.Id).Distinct().Count()} maps, " +
            $"naming {doors.Select(d => d.Door.TargetMapId).Distinct().Count()} different places");

        List<(MapData From, ScriptedDoor Door)> only =
        [
            .. doors.Where(d => !byASquare.Contains(d.Door.TargetMapId)),
        ];

        Console.WriteLine(
            $"  {only.Count} of them lead somewhere no doorway and no map edge does, " +
            $"{only.Select(d => d.Door.TargetMapId).Distinct().Count()} different maps");

        Console.WriteLine();

        foreach ((MapData from, ScriptedDoor door) in doors)
        {
            bool known = maps.TryGetValue(door.TargetMapId, out MapData? target);

            string note =
                !known ? "   <- no such map"
                : !byASquare.Contains(door.TargetMapId) ? "   <- NO DOORWAY LEADS HERE"
                : "";

            Console.WriteLine(
                $"    {from.Id,-7} {from.Name,-18} {door.What,-16} -> " +
                $"{door.TargetMapId,-7} {target?.Name ?? "?",-18} warp {door.TargetWarpId} " +
                $"at ({door.X},{door.Y}){note}");
        }
    }

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
    /// <summary>
    /// What a new game already knows, and who that keeps off the map.
    /// <para>
    /// The instrument for the mistake that a fresh save is an empty save. It prints the
    /// candidate runs so the choice can be checked, then the flags, then — the part that
    /// matters — the people each one hides, because a flag number on its own cannot be
    /// wrong in any visible way and a name standing in the wrong room can.
    /// </para>
    /// </summary>
    private static void WriteNewGame(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("What a new game starts with");

        if (NewGameLocator.Locate(rom, Console.WriteLine) is not { } opening)
        {
            Console.WriteLine("  nothing found");
            return;
        }

        Console.WriteLine($"  the script is at 0x{opening.Address:X8}");
        Console.WriteLine();
        Console.WriteLine($"  {opening.Flags.Count} flags: {string.Join(", ", opening.Flags.Select(f => $"0x{f:X3}"))}");

        foreach ((int variable, int value) in opening.Variables)
            Console.WriteLine($"  variable 0x{variable:X4} = {value}");

        MapLibrary library = MapLibrary.Open(rom);

        var hides = new Dictionary<int, List<string>>();
        int behind = 0;

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HiddenBy != 0))
            {
                behind++;

                if (!opening.Flags.Contains(person.HiddenBy)) continue;

                if (!hides.TryGetValue(person.HiddenBy, out List<string>? who)) hides[person.HiddenBy] = who = [];

                who.Add($"{WorldExporter.MapId(map.Bank, map.Number)} person {person.LocalId} at ({person.X}, {person.Y})");
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"  {hides.Sum(p => p.Value.Count)} of the {behind} people who stand behind a flag are hidden " +
            $"from the first frame, across {hides.Count} of these {opening.Flags.Count} flags");

        foreach ((int flag, List<string> who) in hides.OrderBy(p => p.Key))
        {
            Console.WriteLine($"    0x{flag:X3}  {who.Count} {(who.Count == 1 ? "person" : "people")}");

            foreach (string one in who.Take(3)) Console.WriteLine($"          {one}");
        }

        List<int> spare = [.. opening.Flags.Where(f => !hides.ContainsKey(f))];

        if (spare.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  {spare.Count} of them hide nobody on any map — they are the ones scripts read " +
                $"rather than the ones objects stand behind: {string.Join(", ", spare.Select(f => $"0x{f:X3}"))}");
        }
    }

    /// <summary>
    /// What a script says once the fight it started is over, per outcome.
    /// <para>
    /// The instrument for words nobody ever heard. A script that starts a fight with
    /// nobody in it stops there and picks up afterwards on a number the fight leaves
    /// behind; until this beat there was no afterwards, so everything below was read past
    /// and thrown away in the same frame.
    /// </para>
    /// </summary>
    /// <summary>
    /// What turns into what, and how the reading was arrived at.
    /// <para>
    /// The two lines of evidence are the point of printing this at all: the table is
    /// picked out of four thousand candidates by the fact that almost every one of its
    /// entries points at something with a higher base-stat total, and the level method is
    /// picked out of fifteen by the fact that it is the only one that follows itself —
    /// and always at a bigger number.
    /// </para>
    /// </summary>
    private static void WriteEvolutions(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("What turns into what");

        RomExtractor extractor = RomExtractor.Open(rom);
        List<SpeciesData> species = extractor.ExtractSpecies();

        var routines = new Dictionary<int, uint>();

        if (ItemTable.Locate(rom) is { } where)
        {
            foreach (ItemRecord record in ItemTable.Read(rom, where)) routines[record.Id] = record.FieldUse;
        }

        if (EvolutionExtractor.Locate(rom, species, routines, Console.WriteLine) is not { } table)
        {
            Console.WriteLine("  nothing on this cartridge reads as an evolution table");
            return;
        }

        var named = species.ToDictionary(s => s.Index, s => GameText.ToAscii(s.Name));

        string Named(int index) => named.GetValueOrDefault(index) ?? $"species {index}";

        Console.WriteLine();

        foreach (IGrouping<int, Evolution> by in table.Evolutions.GroupBy(e => e.Method).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine(
                $"  method {by.Key}: {by.Count()} of them" +
                (by.Key == table.ByLevel ? "  <- the level one" : by.Key == table.ByItem ? "  <- the item one" : "") +
                $", parameters {by.Min(e => e.Parameter)}..{by.Max(e => e.Parameter)}");

            foreach (Evolution one in by.Take(4))
            {
                Console.WriteLine(
                    $"      {Named(one.Species),-12} -> {Named(one.Into),-12} " +
                    (by.Key == table.ByLevel ? $"at level {one.Parameter}" : $"parameter {one.Parameter}"));
            }
        }

        // The longest lines this cartridge has, which is the readable version of the
        // chain test the method number was derived from.
        Console.WriteLine();
        Console.WriteLine("  The longest lines, by the level method:");

        var byLevel = table.Evolutions.Where(e => e.Method == table.ByLevel).ToList();

        foreach (Evolution first in byLevel
                     .Where(e => byLevel.All(other => other.Into != e.Species))
                     .Where(e => byLevel.Any(next => next.Species == e.Into))
                     .Take(8))
        {
            var line = new List<string> { Named(first.Species) };
            Evolution? step = first;

            while (step is { } here)
            {
                line.Add($"-{here.Parameter}-> {Named(here.Into)}");
                step = byLevel.FirstOrDefault(next => next.Species == here.Into);
            }

            Console.WriteLine("      " + string.Join(" ", line));
        }
    }

    /// <summary>
    /// Who each machine works on, and how that table was told apart from the thousands
    /// that look exactly like it.
    /// <para>
    /// The shape — four hundred and twelve eight-byte words with a quiet top byte —
    /// matches seven thousand places in this image. What picks one of them is agreement
    /// with a table located separately: a machine teaches a move, and something that
    /// learns that move by growing up is something the machine can teach. Both scores
    /// are printed, because "ninety-nine per cent against sixty-six" is evidence and
    /// "found it" is not.
    /// </para>
    /// </summary>
    private static void WriteMachineCompatibility(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("Who a machine works on");

        RomExtractor extractor = RomExtractor.Open(rom);
        List<SpeciesData> species = extractor.ExtractSpecies();

        List<MoveData> moves;

        try
        {
            moves = MoveExtractor.Extract(rom);
        }
        catch (InvalidDataException)
        {
            moves = [];
        }

        Dictionary<int, Learnset> learnsets = LearnsetExtractor.Extract(rom);

        if (moves.Count == 0
            || MachineMoves.Locate(rom, moves.Count, ObstacleMoves.Find(rom)) is not { } listAt)
        {
            Console.WriteLine("  no machine move list, so there is nothing to be compatible with");
            return;
        }

        List<int> taught = MachineMoves.Read(rom, listAt);

        if (MachineCompatibility.Locate(rom, species.Count, taught, learnsets, Console.WriteLine)
            is not { } sets)
        {
            Console.WriteLine("  no table — every machine would work on everything");
            return;
        }

        Console.WriteLine();
        Console.WriteLine(
            $"  Table at 0x{Rom.BaseAddress + (uint)sets.Address:X8}, " +
            $"{sets.Masks.Count} words of eight bytes, {MachineMoves.Count} bits used of sixty-four.");

        string Named(int index) =>
            index >= 0 && index < species.Count && !string.IsNullOrWhiteSpace(species[index].Name)
                ? species[index].Name
                : $"species {index}";

        string MoveNamed(int id) => id >= 0 && id < moves.Count ? moves[id].Name : $"move {id}";

        string Machine(int index) => index < 50 ? $"TM{index + 1:00}" : $"HM{index - 49:00}";

        // How many species each machine reaches. A machine nobody can use, or one
        // everybody can, would be the sign of a column read from the wrong place.
        var reach = new int[MachineMoves.Count];

        for (int s = 0; s < sets.Masks.Count; s++)
            for (int m = 0; m < MachineMoves.Count; m++)
                if (sets.Allows(s, m)) reach[m]++;

        Console.WriteLine();
        Console.WriteLine("  The narrowest and the widest:");

        foreach (int m in Enumerable.Range(0, MachineMoves.Count).OrderBy(m => reach[m]).Take(4))
            Console.WriteLine($"    {Machine(m)} {MoveNamed(taught[m]),-14} {reach[m],4} species");

        foreach (int m in Enumerable.Range(0, MachineMoves.Count).OrderByDescending(m => reach[m]).Take(4))
            Console.WriteLine($"    {Machine(m)} {MoveNamed(taught[m]),-14} {reach[m],4} species");

        // The exceptions, in full. These are the three the score does not explain, and
        // they are all one thing: something that knows the move from birth and still
        // cannot be taught it. A table with no exceptions at all would be the more
        // suspicious result, so they are printed rather than rounded away.
        Console.WriteLine();
        Console.WriteLine("  Knows it already and still cannot be taught it:");

        var machineOf = new Dictionary<int, int>();

        for (int i = 0; i < taught.Count; i++) machineOf[taught[i]] = i;

        foreach ((int index, Learnset learnset) in learnsets.OrderBy(l => l.Key))
        {
            // Species zero is the empty slot every one of these tables begins with, and
            // it has a learnset like everything else. Skipped here for the same reason
            // the scoring skips it: it is not a creature and its word is not evidence.
            if (index <= 0) continue;

            foreach (LevelUpMove entry in learnset.Moves)
            {
                if (!machineOf.TryGetValue(entry.MoveId, out int machine)) continue;
                if (sets.Allows(index, machine)) continue;

                Console.WriteLine(
                    $"    {Named(index),-12} learns {MoveNamed(entry.MoveId),-14} at level {entry.Level,3} " +
                    $"but {Machine(machine)} says no");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  Spot check — compare these against the games:");

        foreach (int index in Spread(Enumerable.Range(0, sets.Masks.Count).ToList(), 6))
        {
            List<string> can =
            [
                .. Enumerable.Range(0, MachineMoves.Count)
                    .Where(m => sets.Allows(index, m))
                    .Select(m => $"{Machine(m)} {MoveNamed(taught[m])}")
            ];

            Console.WriteLine();
            Console.WriteLine($"    {Named(index)} — {can.Count}");

            for (int at = 0; at < can.Count; at += 3)
                Console.WriteLine("      " + string.Join(", ", can.Skip(at).Take(3)));
        }
    }

    /// <summary>
    /// Which behaviour byte is a storage machine, and why that one.
    /// <para>
    /// The same method the water was found by: lay the behaviour bytes against a
    /// structure that has nothing to do with them. Here it is the healer script, which
    /// was located for an unrelated reason years of milestones ago.
    /// </para>
    /// </summary>
    private static void WriteComputers(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("The machine in the corner");

        var library = MapLibrary.Open(rom);
        List<LoadedMap> maps = [.. library.All()];

        uint? healer = HealerLocator.Locate(
            maps.Select(m => ($"{m.Bank}.{m.Number}", (IReadOnlyList<MapObject>)m.Objects)), rom);

        if (healer is null)
        {
            Console.WriteLine("  no healer script, so there is nothing to cross this against");
            return;
        }

        var heals = new HashSet<string>(
            maps.Where(m => m.Objects.Any(o => HealerLocator.Heals(rom, o, healer)))
                .Select(m => $"{m.Bank}.{m.Number}"));

        Console.WriteLine($"  healer script 0x{healer:X8}, on {heals.Count} maps");
        Console.WriteLine();
        Console.WriteLine("  Every behaviour byte, by how well it separates those maps from the rest:");

        var onHealing = new Dictionary<byte, HashSet<string>>();
        var elsewhere = new Dictionary<byte, HashSet<string>>();

        foreach (LoadedMap map in maps)
        {
            string id = $"{map.Bank}.{map.Number}";

            foreach (byte behaviour in new HashSet<byte>(map.Behaviours))
            {
                Dictionary<byte, HashSet<string>> into = heals.Contains(id) ? onHealing : elsewhere;

                if (!into.TryGetValue(behaviour, out HashSet<string>? set)) into[behaviour] = set = [];

                set.Add(id);
            }
        }

        foreach (byte behaviour in onHealing.Keys
                     .OrderByDescending(b => onHealing[b].Count - (elsewhere.GetValueOrDefault(b)?.Count ?? 0))
                     .Take(6))
        {
            int other = elsewhere.GetValueOrDefault(behaviour)?.Count ?? 0;

            Console.WriteLine(
                $"    0x{behaviour:X2}   {onHealing[behaviour].Count,3} of {heals.Count} healing maps, " +
                $"{other,4} of {maps.Count - heals.Count} others" +
                (behaviour == MetatileBehaviour.StairsUp ? "   <- the stairs, taken for a machine until milestone 82" : ""));
        }

        // And where it sits, and — the question the first version of this never asked —
        // whether it is a warp. It is, on every one of them, and every one lands on a
        // square carrying 0x6B. Nineteen staircases, not nineteen machines.
        Console.WriteLine();
        Console.WriteLine("  map                    size     square    healer   apart   warp");

        foreach (LoadedMap map in maps)
        {
            int width = map.Collision.Width;

            for (int at = 0; at < map.Behaviours.Length; at++)
            {
                if (map.Behaviours[at] != MetatileBehaviour.StairsUp) continue;

                var square = new GridPosition(at % width, at / width);

                MapObject? nurse = map.Objects.FirstOrDefault(o => HealerLocator.Heals(rom, o, healer));

                bool warps = map.Warps.Any(w => w.X == square.X && w.Y == square.Y);

                Console.WriteLine(
                    $"    {map.Name,-22} {width}x{map.Collision.Height,-4} ({square.X},{square.Y})" +
                    (nurse is null
                        ? "     -        -    "
                        : $"     ({nurse.X},{nurse.Y})    " +
                          $"{Math.Abs(nurse.X - square.X) + Math.Abs(nurse.Y - square.Y),-6}") +
                    (warps ? "  yes" : "  no"));
            }
        }

        WriteMachinesThatAreNotTiles(rom, library, maps);
    }

    /// <summary>
    /// The other kind of machine, and why there is no byte to find for it.
    /// <para>
    /// Every machine above is a behaviour byte and nothing else — no sign, no person, no
    /// script anywhere on the map. That is what made a behaviour test the only way to
    /// find one, and it is what makes them the only machines this client can open.
    /// </para>
    /// <para>
    /// The one in the player's bedroom is the opposite in every respect: behaviour 0x00,
    /// a sign, and a script that says one line and hands the rest of itself to special
    /// routines. Nothing about it can be read except how alone those routines are.
    /// </para>
    /// </summary>
    private static void WriteMachinesThatAreNotTiles(Rom rom, MapLibrary library, List<LoadedMap> maps)
    {
        Console.WriteLine();
        Console.WriteLine("  And the machines that are not tiles.");

        List<OneOfAKind> alone = ScriptedMachines.Find(SpecialCalls.All(rom, library));

        Console.WriteLine(
            $"    {alone.Count} scripts in the world call a routine no other script calls; " +
            $"the ten most alone:");

        Console.WriteLine();

        foreach (OneOfAKind machine in alone.Take(10))
        {
            LoadedMap? on = maps.FirstOrDefault(m => WorldExporter.MapId(m.Bank, m.Number) == machine.MapId);

            // The byte on its own square, when the site names one — which is the whole
            // question for anything that looks like a machine.
            string behaviour = Square(machine.What) is { } square && on is not null
                && square.Y * on.Collision.Width + square.X is var at
                && at >= 0 && at < on.Behaviours.Length
                    ? $"0x{on.Behaviours[at]:X2}"
                    : "  -";

            Console.WriteLine(
                $"    {machine.MapId,-6} {on?.Name ?? "?",-16} {machine.What,-16} behaviour {behaviour}   " +
                $"only caller of {string.Join(", ", machine.Alone.Select(r => $"0x{r:X4}"))}");
        }

        Console.WriteLine();
        Console.WriteLine(
            "    A machine this client can open is a byte with no script. One with a script " +
            "and no byte is a line of text and then code, and there is nothing further to read.");
    }

    /// <summary>The square a site names, for the ones that name one at all.</summary>
    private static GridPosition? Square(string what)
    {
        int open = what.IndexOf('('), comma = what.IndexOf(','), close = what.IndexOf(')');

        if (open < 0 || comma < open || close < comma) return null;

        return int.TryParse(what[(open + 1)..comma], out int x)
            && int.TryParse(what[(comma + 1)..close], out int y)
                ? new GridPosition(x, y)
                : null;
    }

    /// <summary>
    /// Four ways of looking for the cartridge's lettering, and what each one rules out.
    /// <para>
    /// The expensive part of a search like this is not running it — it is discovering,
    /// again, that the four obvious ideas do not work. This exists so that the next
    /// attempt starts where this one stopped.
    /// </para>
    /// </summary>
    private static void WriteLetterHunt(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("Looking for the lettering");

        Console.WriteLine();
        Console.WriteLine($"  {LetterHunt.PrintableCodes()} of 256 character codes print something.");

        List<LetterHit> byCode = LetterHunt.IndexedByCharacterCode(rom);

        Console.WriteLine();
        Console.WriteLine("  By character code — blank where nothing prints, ink where something does:");

        Report(byCode);

        List<LetterHit> raw = LetterHunt.LooksLikeAnAlphabet(rom.Span, Rom.BaseAddress);

        Console.WriteLine();
        Console.WriteLine("  By the shape of an alphabet — eleven capitals read the same backwards,");
        Console.WriteLine("  in an order nothing else has:");

        Report(raw);

        var blocks = 0;
        var unpacked = new List<LetterHit>();

        for (int at = 0; at + 4 < rom.Length; at += 4)
        {
            if (rom.ReadU8(at) != 0x10) continue;

            int size = (int)(rom.ReadU32(at) >> 8);

            if (size is < 512 or > 0x8000) continue;

            byte[] data;

            try { data = Lz77.Decompress(rom.Span[at..]); }
            catch { continue; }

            if (data.Length < size) continue;

            blocks++;
            unpacked.AddRange(LetterHunt.LooksLikeAnAlphabet(data, Rom.BaseAddress + (uint)at));
        }

        Console.WriteLine();
        Console.WriteLine($"  The same, through {blocks} compressed blocks unpacked:");

        Report(unpacked);

        Console.WriteLine();
        Console.WriteLine("  Four: a person looking. The block at 0x08232800 is plain eight-by-eight and");
        Console.WriteLine("  reads perfectly — a clean Latin lowercase alphabet among the kana — which is");
        Console.WriteLine("  how we know the readers above work. It holds no capitals and is not indexed");
        Console.WriteLine("  by code either. Sheets across 0x08230000-0x08236000 show that block and then");
        Console.WriteLine("  compressed data; the Latin sheet is not beside it.");
        Console.WriteLine();
        Console.WriteLine("  So the lettering is in this image in a form none of these four recognises.");
        Console.WriteLine("  What is ruled out is a bitmap eight pixels wide, at one, two or four bits");
        Console.WriteLine("  deep, eight, twelve or sixteen tall, packed or compressed.");
    }

    private static void Report(List<LetterHit> hits)
    {
        if (hits.Count == 0)
        {
            Console.WriteLine("    nothing anywhere on the image");
            return;
        }

        foreach (LetterHit hit in hits.OrderByDescending(h => h.Share).Take(6))
        {
            Console.WriteLine(
                $"    0x{hit.Address:X8}  {hit.Depth}bpp {hit.Height} tall" +
                (hit.Offset > 0 ? $", glyph {hit.Offset}" : "") +
                $"   {hit.Score}/{hit.OutOf}");
        }
    }

    /// <summary>
    /// Who turns a flag on, and who turns it off.
    /// <para>
    /// The reach report says what is behind somebody who is not there yet. This says
    /// what would put them there, which is the next question in every case.
    /// </para>
    /// </summary>
    private static void WriteFlagClearers(Rom rom, int flag)
    {
        Console.WriteLine();
        Console.WriteLine($"Flag 0x{flag:X4}");

        List<FlagChange> changes = FlagClearers.Find(rom, flag);

        if (changes.Count == 0)
        {
            Console.WriteLine("  nothing on this image sets or clears it");
            return;
        }

        var library = MapLibrary.Open(rom);
        List<LoadedMap> maps = [.. library.All()];

        foreach (FlagChange change in changes)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  {(change.Sets ? "set" : "cleared")} at 0x{change.At:X8}" +
                (change.ScriptStart == 0
                    ? " — nothing points anywhere near it"
                    : $", in the script beginning 0x{change.ScriptStart:X8}"));

            if (change.ScriptStart == 0) continue;

            // Who runs it, if anybody does. A script nobody's record points at is
            // reached some other way, and which other way is the useful half.
            var owned = false;

            foreach (LoadedMap map in maps)
            {
                foreach (MapObject person in map.Objects)
                {
                    if (person.ScriptAddress != change.ScriptStart) continue;

                    owned = true;

                    Console.WriteLine(
                        $"    run by {map.Bank}.{map.Number} {map.Name} object {person.LocalId} " +
                        $"at ({person.X}, {person.Y})");
                }
            }

            if (!owned)
            {
                Console.WriteLine(
                    change.InsideAFight
                        ? "    nobody's record points at it, and a trainerbattle stands in front of the " +
                          "change — so it is in the script a won fight leads to, which this reader does " +
                          "not follow"
                        : "    nobody's record points at it");
            }
        }
    }

    private static void WriteAfterFights(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("What a script says once its own fight is over");

        BattleOutcomes? outcomes = BattleOutcomeLocator.Locate(rom, Console.WriteLine);

        if (outcomes is null) return;

        MapLibrary library = MapLibrary.Open(rom);

        int fights = 0;
        int speaking = 0;

        foreach (LoadedMap map in library.All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
            {
                // Every one of them, not only the one today's save would reach. The
                // sleepers and the one-of-a-kind creatures all keep their fight behind a
                // flag, so a run from a fresh save finds three of the nine.
                foreach (uint resumes in ScriptReader.AfterTheWildFights(rom, person.ScriptAddress))
                {
                fights++;

                Console.WriteLine();
                Console.WriteLine(
                    $"  {WorldExporter.MapId(map.Bank, map.Number)} person {person.LocalId} at " +
                    $"({person.X}, {person.Y}): picks up at 0x{resumes:X8}");

                bool said = false;

                foreach ((string label, int outcome) in new[]
                         {
                             ("won", outcomes.Won),
                             ("walked away", outcomes.Ran),
                             ("caught", outcomes.Caught),
                         })
                {
                    var state = new ScriptState();
                    state.Write(0x800D, outcome);

                    ScriptRun rest = ScriptRunner.Run(rom, resumes, state);

                    if (rest.Pages.Count == 0)
                    {
                        Console.WriteLine($"    {label,-12} ({outcome}): nothing");
                        continue;
                    }

                    said = true;

                    Console.WriteLine(
                        $"    {label,-12} ({outcome}): \"{GameText.ToAscii(rest.Pages[0]).Replace("\n", " ")}\"" +
                        (rest.Pages.Count > 1 ? $" (+{rest.Pages.Count - 1} more)" : ""));
                }

                if (said) speaking++;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"  {fights} scripts stop for a fight with nobody in it, and {speaking} of them " +
            "have something to say once it is over");
    }


    /// <summary>
    /// Everything the sound walk finds, printed.
    /// <para>
    /// The whole of the sound work — the recordings, the instruments, the songs, the table
    /// that names them and the table that says whose cry is whose — has never been run
    /// against a real cartridge. It was built against a fixture this machine wrote, which can
    /// only ever prove that the code agrees with the file it was written from.
    /// </para>
    /// <para>
    /// This is what makes it checkable. Nothing here decides anything; it prints what came
    /// back, including the numbers that would say something went wrong.
    /// </para>
    /// </summary>
    private static void WriteSound(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("SOUND");
        Console.WriteLine();

        SoundTreeResult tree = SoundLocator.Walk(rom, Console.WriteLine);

        Console.WriteLine();
        Console.WriteLine($"  {tree.Samples.Count} recordings");

        if (tree.Samples.Count > 0)
        {
            List<SampleRecord> packed = [.. tree.Samples.Where(s => s.Compressed)];

            Console.WriteLine($"    {packed.Count} of them packed, {tree.Samples.Count - packed.Count} plain");
            Console.WriteLine($"    {tree.Samples.Count(s => s.Loops)} loop");

            List<int> rates = [.. tree.Samples.Select(s => s.Rate).Order()];

            Console.WriteLine(
                $"    rates from {rates[0]} to {rates[^1]}, middle one {rates[rates.Count / 2]}");
        }

        // The cry table, which the walk above cannot see: its entries carry a type byte
        // outside the driver's kind enumeration, so every one of them is rejected as an
        // instrument.
        Console.WriteLine();

        CryTableResult? cries = CryTableLocator.Locate(rom, tree.Samples, Console.WriteLine);

        if (cries is not null)
        {
            int species = RomExtractor.Open(rom).ExtractSpecies().Count;

            Console.WriteLine($"    the species table names {species}");

            if (cries.Count != species)
            {
                Console.WriteLine(
                    $"    which is {Math.Abs(cries.Count - species)} "
                    + (cries.Count < species ? "fewer cries than creatures" : "more cries than creatures"));
            }

            Console.WriteLine($"    {cries.Samples.Distinct().Count()} different recordings between them");

            var library = new CryLibrary(rom, tree.Samples, cries);

            var decoded = 0;
            var samplesLong = 0;

            for (int at = 0; at < cries.Count; at++)
            {
                if (library.For(at) is not { } voice) continue;

                decoded++;
                samplesLong += voice.Audio.Length;
            }

            Console.WriteLine(
                decoded == 0
                    ? "    none of them unpacked, which is a finding"
                    : $"    {decoded} unpacked, {samplesLong / Math.Max(1, decoded)} samples long on average");
        }

        // And whether a song can actually be assembled, which is the question every layer
        // above was built to answer.
        Console.WriteLine();

        if (!tree.FoundATable)
        {
            Console.WriteLine("  no song table, so nothing can be assembled");

            return;
        }

        var mixer = new PokeMmo.Core.Sound.Mixer(32768);

        var loaded = 0;
        var tracks = 0;
        var missing = new List<int>();

        for (int song = 0; song < tree.Table.Count; song++)
        {
            if (SongLoader.Load(rom, tree, song, mixer) is not { } player)
            {
                missing.Add(song);

                continue;
            }

            loaded++;
            tracks += player.TrackCount;
        }

        Console.WriteLine($"  {loaded} of {tree.Table.Count} songs assemble, {tracks} tracks between them");

        if (missing.Count > 0)
        {
            Console.WriteLine(
                $"    {missing.Count} do not: " +
                string.Join(", ", missing.Take(20)) + (missing.Count > 20 ? ", ..." : ""));
        }
    }

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

        // Every script on every map, and everything reachable from each of them.
        //
        // This used to run each person's script on a fresh save and record where that
        // one run stopped, which is two mistakes at once. It saw only the people, so the
        // signs — added late, scenery for most of this project — were invisible; and it
        // saw only the path today's flags choose, so anything behind a condition was
        // invisible too. The command blocking the machine in BILL's cottage is both: a
        // sign, behind a flag. It had exactly one site to be scored on, and one site
        // decides nothing.
        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> starts =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.Signs.Where(s => s.HasScript).Select(s => s.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
            ];

            foreach (uint start in starts)
            {
                foreach (uint reachable in ScriptReader.Reachable(rom, start))
                {
                    if (ScriptReader.StoppedAt(rom, reachable) is not { } code) continue;
                    if (ScriptReader.StoppedAtOffset(rom, reachable) is not { } at) continue;

                    if (!sites.TryGetValue(code, out List<int>? where)) sites[code] = where = [];

                    if (where.Count < sitesPer && !where.Contains(at)) where.Add(at);
                }
            }
        }

        foreach ((byte code, List<int> where) in sites.OrderByDescending(e => e.Value.Count))
        {
            var scores = new List<(int Width, double Clean, double Pointers, double Depth, double Speech, bool Ruled)>();

            for (int width = 0; width <= maxWidth; width++)
            {
                int clean = 0;
                int pointers = 0;
                int landed = 0;

                int depth = 0;
                int carries = 0;

                int spoke = 0;
                int named = 0;

                // Ruled out before the sites are even walked: a width that resumes on the
                // same byte at nearly every site has resumed inside an argument. Nine in
                // ten rather than all ten, because a few of any scorer's sites are a
                // misaligned read that happened to land on this byte.
                double column = ResumesOnAColumn(rom, where, width);

                // And the same idea from the other side: a width that resumes on nothing
                // but nops and ends at nearly every site has landed in the padding at
                // the tail of an argument rather than on the next instruction.
                bool ruled = column >= 0.9 || ResumesOnWork(rom, where, width) <= 0.1;

                foreach (int at in where)
                {
                    // Ruled out rather than scored down. These two are not preferences
                    // between widths; they are a width caught taking something that
                    // belongs to the script, and one site is enough to catch it.
                    if (LosesAPage(rom, at + 1 + width)) ruled = true;
                    if (EatsInstructions(rom, at + 1, width)) ruled = true;

                    if (CarriesAPointer(rom, at + 1, width)) carries++;

                    (bool ended, int good, int total, int read, int speech, int said) =
                        ReadsOn(rom, at + 1 + width);

                    if (ended) clean++;

                    pointers += good;
                    landed += total;
                    depth += read;
                    spoke += speech;
                    named += said;
                }

                // No pointers at all is not evidence of anything, and scoring it as
                // perfect would hand the answer to whichever width happened to avoid
                // them. It counts as nothing either way.
                scores.Add((
                    width,
                    clean / (double)where.Count,
                    landed == 0 ? 0 : pointers / (double)landed,
                    carries / (double)where.Count,
                    named == 0 ? 0 : spoke / (double)named,
                    ruled));
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
            // Ruled out first, and separately from everything else. A width caught
            // eating instructions or losing a page is not a worse answer than the
            // others — it is not an answer, and letting it compete on the scores is how
            // a width that reads beautifully and prints nothing gets adopted.
            List<(int Width, double Clean, double Pointers, double Depth, double Speech, bool Ruled)> standing =
                [.. scores.Where(s => !s.Ruled)];

            if (standing.Count == 0) standing = scores;

            double top = standing.Max(s => s.Depth) >= 0.5 ? standing.Max(s => s.Depth) : 0;

            // The second-sharpest test, and the one that settles the commands whose
            // neighbours are speech rather than jumps. A width that resumed inside an
            // argument makes the next loadpointer or message name an address, and an
            // address that is not a page of text does not read as one — the same
            // recognisability the extractor uses to find seven thousand pages of
            // dialogue, aimed at one word. It only gets to speak when the pointer test
            // has said nothing, and only when it is unanimous across the sites.
            double spoken = top > 0 || standing.Max(s => s.Speech) < 1 ? 0 : standing.Max(s => s.Speech);

            // Everything close to the top, not just the top. A single arrow claims more
            // than this evidence supports, and claiming more than the evidence supports
            // is the specific mistake this whole method exists to avoid.
            int[] shortlist = top > 0
                ? [.. standing.Where(s => s.Depth >= top - 0.05).Select(s => s.Width)]
                : spoken > 0
                    ? [.. standing
                        .Where(s => s.Speech >= spoken)
                        // Tied on text, separated by whether the read gets anywhere. A
                        // continuation test cannot be trusted on its own — that is why
                        // it comes last — but between two widths that have already
                        // survived everything else, "reads to an end at every site" and
                        // "reads to an end at a third of them" is not a close call.
                        .Where(s => s.Clean >= standing.Where(x => x.Speech >= spoken).Max(x => x.Clean) - 0.05)
                        .Select(s => s.Width)]
                    : [.. standing.Where(s => s.Clean + s.Pointers >= standing.Max(x => x.Clean + x.Pointers) - 0.1).Select(s => s.Width)];

            Console.WriteLine();
            Console.WriteLine($"  0x{code:X2}  stops {where.Count} scripts");

            foreach ((int width, double cleanly, double pointing, double deep, double speech, bool ruled) in scores)
            {
                string mark = ruled
                    ? "  ruled out: it eats a page, an instruction, or resumes on a column"
                    : (top > 0 || spoken > 0) && shortlist.Contains(width) ? " <-" : "";

                Console.WriteLine(
                    $"      {width} bytes:  {deep,5:P0} carry a real pointer, " +
                    $"{cleanly,5:P0} read on to an end, {pointing,5:P0} of those pointers land, " +
                    $"{speech,5:P0} of the text they name reads as speech, " +
                    $"{ResumesOnAColumn(rom, where, width),5:P0} resume on the same byte, " +
                    $"{ResumesOnWork(rom, where, width),5:P0} resume on real work{mark}");
            }

            Console.WriteLine(
                top > 0
                    ? shortlist.Length == 1
                        ? $"      -> {shortlist[0]} bytes, ending on a pointer that lands on something real"
                        : $"      -> {string.Join(" or ", shortlist)} bytes, equally"
                    : spoken > 0
                        ? shortlist.Length == 1
                            ? $"      -> {shortlist[0]} bytes, the one left once the text it names and where it reads on are both counted"
                            : $"      -> {string.Join(" or ", shortlist)} bytes, all of whose text reads as text"
                        : "      -> undecided. No width ends on a real pointer, and the continuation " +
                          "test is not to be trusted alone: read the bytes");
        }
    }

    /// <summary>
    /// True when the byte a width would resume on never changes across the sites.
    /// <para>
    /// The oldest rule in this project, finally asked by a machine rather than by eye: a
    /// column that never changes is an argument. Turned round, it is sharper still —
    /// what a width <em>resumes on</em> is supposed to be an opcode, and opcodes vary
    /// from site to site. A resume byte that is the same at every one of twenty-one
    /// sites is not an opcode; it is the middle of an argument.
    /// </para>
    /// <para>
    /// This is what the continuation test cannot see. Reading 0xA1 as one byte wide
    /// leaves three nops in front of every loadpointer in the game and reads on to a
    /// proper end every time, so it scores perfectly — and it is wrong, and the column
    /// says so: the byte it resumes on is 0x00 at all twenty-one sites, while the right
    /// width resumes on 0x0F, 0x67, 0x28 and 0xC5.
    /// </para>
    /// </summary>
    /// <param name="sites">Where the command was found, by offset of the command itself.</param>
    /// <returns>
    /// How much of the sites agree on the resume byte, from nothing to one. Reported as a
    /// share rather than a yes, because a handful of the sites a scorer gathers are
    /// always a misaligned read that happened to land on this byte, and demanding
    /// unanimity hands the answer to them.
    /// </returns>
    private static double ResumesOnAColumn(Rom rom, IReadOnlyList<int> sites, int width)
    {
        if (sites.Count < 4) return 0;

        var counts = new Dictionary<byte, int>();

        foreach (int at in sites)
        {
            if (at + 1 + width >= rom.Length) continue;

            byte b = rom.ReadU8(at + 1 + width);

            counts[b] = counts.GetValueOrDefault(b) + 1;
        }

        return counts.Count == 0 ? 0 : counts.Values.Max() / (double)sites.Count;
    }

    /// <summary>
    /// How often a width resumes on a command that actually does something.
    /// <para>
    /// The other half of the column test, and the one that settles 0xA1. Reading it two
    /// bytes wide resumes on 0x02 at eight sites and 0x00 at the rest — <c>end</c> and
    /// <c>nop</c>, nothing else, ever. That scores a hundred per cent on "reads on to an
    /// end" because it ends immediately, which is the continuation test being shown
    /// exactly what it asks for and nothing at all.
    /// </para>
    /// <para>
    /// A correct width resumes on work: a loadpointer, a pause, a setflag, a message. A
    /// wrong one lands in the padding at the end of an argument.
    /// </para>
    /// </summary>
    private static double ResumesOnWork(Rom rom, IReadOnlyList<int> sites, int width)
    {
        if (sites.Count == 0) return 0;

        int work = 0;

        foreach (int at in sites)
        {
            if (at + 1 + width >= rom.Length) continue;

            byte code = rom.ReadU8(at + 1 + width);

            if (code is not (ScriptCommands.Nop or ScriptCommands.End or 0x01)) work++;
        }

        return work / (double)sites.Count;
    }

    /// <summary>
    /// True when the bytes a width swallows are themselves whole instructions.
    /// <para>
    /// A width that is too long does not merely take extra bytes: it takes the commands
    /// that were standing there. This asks the question directly — decode the swallowed
    /// bytes as a script and see whether they end exactly where the width does, with at
    /// least one command that carries arguments of its own.
    /// </para>
    /// <para>
    /// What counts as an instruction has to be narrow, and this is the second time of
    /// asking. It was "any command carrying arguments", and that ruled out the right
    /// answer for 0xA1: its four argument bytes are <c>28 00 00 00</c>, which decodes as
    /// a pause and a nop as readily as anything else does. The result read beautifully
    /// and ended the script that wakes the sleeper on ROUTE 12 immediately after the
    /// battle, three commands short of the flag that takes it off the map.
    /// </para>
    /// <para>
    /// So it now counts only a command that carries a pointer landing on something real.
    /// Argument bytes look like small commands all the time; they almost never look like
    /// a command holding an address that leads to a script or a page of text.
    /// </para>
    /// </summary>
    private static bool EatsInstructions(Rom rom, int from, int width)
    {
        if (width == 0) return false;

        int offset = from;
        bool carried = false;

        while (offset < from + width)
        {
            if (offset + 1 >= rom.Length) return false;

            byte code = rom.ReadU8(offset);

            if (ScriptCommands.ArgumentLength(code, rom.ReadU8(offset + 1)) is not { } length)
                return false;

            // Cut in half rather than swallowed whole, which is the same crime and
            // shows up differently. Only counted when the thing being cut is provably
            // an instruction and not a coincidence: a page pointer that lands on text.
            // Anything looser would reject correct widths, because the argument bytes
            // of a real command decode as commands as readily as anything else does.
            if (offset + 1 + length > from + width
                && code is ScriptCommands.LoadPointer or ScriptCommands.Message
                && offset + 1 + length <= rom.Length)
            {
                var straddling = new ScriptCommand(offset, code, rom.Slice(offset + 1, length).ToArray());

                uint page = code == ScriptCommands.LoadPointer ? straddling.Pointer(1) : straddling.Pointer();

                if (rom.ToOffsetOrNull(page) is { } at && GameText.LooksLikeDialogue(rom.Span[at..])) return true;
            }

            // A pointer that lands, which is the only kind of swallowed command worth
            // believing in. Anything looser and ordinary arguments read as code.
            if (length > 0 && offset + 1 + length <= from + width)
            {
                var swallowed = new ScriptCommand(offset, code, rom.Slice(offset + 1, length).ToArray());

                uint target = code switch
                {
                    ScriptCommands.Call or ScriptCommands.Goto => swallowed.Pointer(),
                    ScriptCommands.CallIf or ScriptCommands.GotoIf => swallowed.Pointer(1),
                    ScriptCommands.LoadPointer => swallowed.Pointer(1),
                    ScriptCommands.Message => swallowed.Pointer(),
                    _ => 0,
                };

                if (rom.ToOffsetOrNull(target) is { } lands &&
                    (GameText.LooksLikeDialogue(rom.Span[lands..]) || ScriptReader.Read(rom, target).Count >= 2))
                {
                    carried = true;
                }
            }

            offset += 1 + length;
        }

        return carried && offset == from + width;
    }

    /// <summary>
    /// True when reading on from here meets a standard routine that prints a page
    /// before anything has loaded one.
    /// <para>
    /// The sharpest single test in this whole method, and the only one with a count
    /// behind it rather than a judgement: of the 1202 calls to standard routine 4 in
    /// every script this cartridge's maps can reach, 1202 have a page loaded first.
    /// Zero do not. A width that leaves one standing alone has swallowed the loadpointer
    /// it needed, and what it swallowed is the words.
    /// </para>
    /// </summary>
    private static bool LosesAPage(Rom rom, int from, int maxCommands = 12)
    {
        int offset = from;

        for (int i = 0; i < maxCommands; i++)
        {
            if (offset < 0 || offset + 1 >= rom.Length) return false;

            byte code = rom.ReadU8(offset);
            byte first = rom.ReadU8(offset + 1);

            if (ScriptCommands.ArgumentLength(code, first) is not { } length) return false;

            if (code == ScriptCommands.LoadPointer) return false;

            // The routines that print. Standard 1 is not one of them — it is called
            // without a page 168 times out of 168, which is how it can be told apart
            // from the ones that are.
            if (code == ScriptCommands.CallStandard && first is 2 or 3 or 4 or 5 or 6 or 9) return true;

            offset += 1 + length;

            if (code is ScriptCommands.End or ScriptCommands.Return or ScriptCommands.Goto) return false;
        }

        return false;
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
    private static (bool Ended, int GoodPointers, int TotalPointers, int Read, int GoodText, int TotalText) ReadsOn(
        Rom rom, int from, int maxCommands = 12)
    {
        int offset = from;
        int good = 0;
        int total = 0;
        int read = 0;
        int speech = 0;
        int said = 0;

        for (int i = 0; i < maxCommands; i++)
        {
            if (offset < 0 || offset + 1 >= rom.Length) return (false, good, total, read, speech, said);

            byte code = rom.ReadU8(offset);
            byte first = rom.ReadU8(offset + 1);

            if (ScriptCommands.ArgumentLength(code, first) is not { } length) return (false, good, total, read, speech, said);
            if (offset + 1 + length > rom.Length) return (false, good, total, read, speech, said);

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

            // The other kind of pointer these scripts carry, and the one that decides
            // this case: text. A width that resumed inside an argument makes the next
            // loadpointer or message name an address that is not speech, and speech is
            // recognisable — that is the same test the extractor uses to find dialogue
            // in the first place, pointed at one address instead of sixteen megabytes.
            uint words = code switch
            {
                ScriptCommands.LoadPointer => new ScriptCommand(
                    offset, code, rom.Slice(offset + 1, length).ToArray()).Pointer(1),
                ScriptCommands.Message => new ScriptCommand(
                    offset, code, rom.Slice(offset + 1, length).ToArray()).Pointer(),
                _ => 0,
            };

            if (words != 0)
            {
                said++;

                if (rom.ToOffsetOrNull(words) is { } page && GameText.LooksLikeDialogue(rom.Span[page..]))
                    speech++;
            }

            offset += 1 + length;

            if (code is ScriptCommands.End or ScriptCommands.Return or ScriptCommands.Goto)
                return (true, good, total, read, speech, said);
        }

        return (false, good, total, read, speech, said);
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
    /// <summary>
    /// How often each byte value actually begins a command, counted over every script
    /// this cartridge's maps can reach.
    /// <para>
    /// Built because two commands tied on every other test. A candidate width is a claim
    /// about where the next command starts, and that claim can be checked against what
    /// commands actually start with — a width that resumes on a byte no script in the
    /// whole game ever starts a command with is resuming inside an argument, whatever
    /// else it scores.
    /// </para>
    /// <para>
    /// Counted at real boundaries, which is the same lesson as the warp command: the
    /// same byte counted anywhere it appears is mostly argument, and a histogram of
    /// arguments answers a different question than the one being asked.
    /// </para>
    /// </summary>
    private static int[] OpcodeCounts(Rom rom)
    {
        var counts = new int[256];

        MapLibrary library = MapLibrary.Open(rom);

        var seen = new HashSet<uint>();

        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> starts =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.Signs.Where(s => s.HasScript).Select(s => s.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
            ];

            foreach (uint start in starts)
            {
                foreach (uint reachable in ScriptReader.Reachable(rom, start))
                {
                    // Once each. A script every door on a route points at would
                    // otherwise vote as many times as it has doors.
                    if (!seen.Add(reachable)) continue;

                    foreach (ScriptCommand command in ScriptReader.Read(rom, reachable))
                        counts[command.Code]++;
                }
            }
        }

        return counts;
    }

    /// <summary>
    /// How often a call to a standard routine has a page loaded immediately before it.
    /// <para>
    /// Counted rather than assumed, because it is about to be used as evidence.
    /// </para>
    /// </summary>
    private static (int Loaded, int Alone) LoadedBeforeCallStandard(Rom rom)
    {
        int loaded = 0;
        int alone = 0;

        var byNumber = new SortedDictionary<byte, (int Loaded, int Alone)>();

        MapLibrary library = MapLibrary.Open(rom);

        var seen = new HashSet<uint>();

        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> starts =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.Signs.Where(s => s.HasScript).Select(s => s.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
            ];

            foreach (uint start in starts)
            {
                foreach (uint reachable in ScriptReader.Reachable(rom, start))
                {
                    if (!seen.Add(reachable)) continue;

                    List<ScriptCommand> commands = ScriptReader.Read(rom, reachable);

                    for (int i = 0; i < commands.Count; i++)
                    {
                        if (commands[i].Code != ScriptCommands.CallStandard) continue;

                        // Anywhere earlier in the same straight-line run, not merely
                        // immediately before: the commonest shape puts the gaps to fill
                        // in between, as BILL's does when he hands over the ticket.
                        bool has = commands.Take(i).Any(c => c.Code == ScriptCommands.LoadPointer);

                        byte which = commands[i].Arguments.Length > 0 ? commands[i].Arguments[0] : (byte)0xFF;

                        byNumber.TryGetValue(which, out (int Loaded, int Alone) tally);
                        byNumber[which] = has ? (tally.Loaded + 1, tally.Alone) : (tally.Loaded, tally.Alone + 1);

                        if (has) loaded++;
                        else alone++;
                    }
                }
            }
        }

        foreach ((byte which, (int with, int without)) in byNumber)
        {
            Console.WriteLine(
                $"    callstd {which,3}: {with,5} with a page, {without,5} without " +
                $"({with / (double)Math.Max(1, with + without),6:P1})");
        }

        return (loaded, alone);
    }

    /// <summary>
    /// Points the rejection tests at the widths this reader already believes.
    /// <para>
    /// Every width in the table was decided on the evidence available at the time, and
    /// two of them were decided twice. A test sharp enough to rule a width out is sharp
    /// enough to be aimed at the answers already given — and a width that was right on
    /// one script and wrong on the cartridge is exactly the failure this project cannot
    /// see from the inside, because a wrong width does not fail. It reads on, and the
    /// script quietly contains less.
    /// </para>
    /// <para>
    /// Reported, never applied. Nothing here changes a number.
    /// </para>
    /// </summary>
    /// <summary>
    /// Which way each ledge byte is hopped, asked of the map data rather than remembered.
    /// <para>
    /// Four behaviour bytes sit together in the table and this project named them south,
    /// north, west and east on the strength of two maps. The counts say that naming is at
    /// least incomplete — one of the four never appears at all — and every one of the
    /// 1042 ledge squares in the world is a wall in the collision data, so nobody has
    /// ever crossed one to find out.
    /// </para>
    /// <para>
    /// Three questions, each answerable from the bytes:
    /// </para>
    /// <para>
    /// Which axis. A ledge you hop southward runs east–west, because it is the edge of a
    /// step in the ground. So the axis a byte's squares run <i>along</i> is the axis it
    /// is <i>not</i> hopped along.
    /// </para>
    /// <para>
    /// Which side you stand on and which you land on. Both are walkable — that is what
    /// makes a hop possible — and they are told apart by height: a ledge is hopped down.
    /// The elevation nibble in each block says which side is higher, and it is read here
    /// straight off the cartridge rather than through the world file, which keeps only
    /// what a server needs.
    /// </para>
    /// </summary>
    private static void WriteLedges(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("Ledges, and which way each one is hopped");

        // Straight off the bank table rather than through the map library, because the
        // library renders every map it hands out and this question needs no pictures.
        MapBankTable banks = MapBankLocator.Locate(rom)
            ?? throw new InvalidDataException("no map bank table");

        var sides = new SortedDictionary<byte, (int Count, int AlongX, int AlongY, int[] Walkable, int[] Higher, int[] Lower)>();

        Direction[] ways = [Direction.Up, Direction.Down, Direction.Left, Direction.Right];

        foreach ((int bank, int number, MapHeaderRecord header) in banks.AllMaps)
        {
            byte[] behaviours = header.Layout.ReadBehaviours(rom);
            ushort[] blocks = header.Layout.ReadBlocks(rom);

            int width = header.Layout.Width;
            int height = header.Layout.Height;

            if (behaviours.Length < width * height) continue;

            CollisionGrid collision = header.Layout.ReadCollision(rom);

            byte At(int x, int y) => behaviours[y * width + x];
            int Up(int x, int y) => new MapBlock(blocks[y * width + x]).Elevation;

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    byte behaviour = At(x, y);

                    if (!MetatileBehaviour.IsLedge(behaviour)) continue;

                    sides.TryGetValue(behaviour, out var tally);

                    tally.Walkable ??= new int[4];
                    tally.Higher ??= new int[4];
                    tally.Lower ??= new int[4];

                    tally.Count++;

                    // The run this square is part of: the same byte to either side of it
                    // along one axis or the other.
                    if (At(x - 1, y) == behaviour || At(x + 1, y) == behaviour) tally.AlongX++;
                    if (At(x, y - 1) == behaviour || At(x, y + 1) == behaviour) tally.AlongY++;

                    for (int i = 0; i < ways.Length; i++)
                    {
                        GridPosition next = new GridPosition(x, y).Step(ways[i]);

                        if (collision.IsWalkable(next)) tally.Walkable[i]++;

                        // Against the square on the other side rather than against the
                        // ledge itself. A ledge block carries elevation zero — the value
                        // that means "whatever is around it" — so everything next to one
                        // reads as higher and nothing reads as lower, which answers
                        // nothing. What matters is which of the two sides is the step up.
                        GridPosition across = new GridPosition(x, y).Step(Opposite(ways[i]));

                        int there = Up(next.X, next.Y);
                        int beyond = Up(across.X, across.Y);

                        if (there > beyond) tally.Higher[i]++;
                        if (there < beyond) tally.Lower[i]++;
                    }

                    sides[behaviour] = tally;
                }
            }
        }

        foreach ((byte behaviour, var tally) in sides)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  0x{behaviour:X2}  {tally.Count} squares, " +
                $"{tally.AlongX} of them in an east–west run, {tally.AlongY} in a north–south one " +
                $"-> hopped {(tally.AlongX > tally.AlongY ? "north or south" : "east or west")}");

            for (int i = 0; i < ways.Length; i++)
            {
                Console.WriteLine(
                    $"      {ways[i],-5}: {tally.Walkable[i],5} walkable, " +
                    $"{tally.Higher[i],5} higher than the far side, {tally.Lower[i],5} lower");
            }
        }
    }

    private static Direction Opposite(Direction direction) => direction switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        _ => Direction.Left,
    };

    private static void WriteWidthAudit(Rom rom, int maxWidth = 8)
    {
        Console.WriteLine();
        Console.WriteLine("Widths already in the table, scored again at every site they are read at");

        MapLibrary library = MapLibrary.Open(rom);

        var sites = new Dictionary<byte, List<int>>();
        var seen = new HashSet<uint>();

        foreach (LoadedMap map in library.All())
        {
            IEnumerable<uint> starts =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => o.ScriptAddress),
                .. map.Signs.Where(s => s.HasScript).Select(s => s.ScriptAddress),
                .. map.Triggers.Where(t => t.HasScript).Select(t => t.ScriptAddress),
                .. map.OnEntry.Where(e => e.HasScript).Select(e => e.ScriptAddress),
            ];

            foreach (uint start in starts)
            {
                foreach (uint reachable in ScriptReader.Reachable(rom, start))
                {
                    if (!seen.Add(reachable)) continue;

                    foreach (ScriptCommand command in ScriptReader.Read(rom, reachable))
                    {
                        if (ScriptCommands.ArgumentLength(command.Code, 0) is not { } declared) continue;
                        if (declared == 0) continue;

                        if (!sites.TryGetValue(command.Code, out List<int>? where)) sites[command.Code] = where = [];

                        if (!where.Contains(command.Offset)) where.Add(command.Offset);
                    }
                }
            }
        }

        int clean = 0;

        foreach ((byte code, List<int> where) in sites.OrderByDescending(e => e.Value.Count))
        {
            if (ScriptCommands.ArgumentLength(code, 0) is not { } declared) continue;

            // Caught taking something, and left standing in the road for it. Either
            // test alone is far too eager to be worth printing: the argument bytes of a
            // perfectly ordinary loadpointer decode as instructions, so "eats
            // instructions" fires at 2519 of loadpointer's 2520 sites and means nothing
            // there. What means something is a width that eats an instruction AND then
            // cannot read on, while some other width can — that is not an argument
            // that happens to look like code, it is a boundary in the wrong place.
            bool Suspect(int at) =>
                (EatsInstructions(rom, at + 1, declared) || LosesAPage(rom, at + 1 + declared))
                && !ReadsOn(rom, at + 1 + declared).Ended;

            int caught = where.Count(Suspect);

            if (caught == 0)
            {
                clean++;
                continue;
            }

            int[] better =
            [
                .. Enumerable.Range(0, maxWidth + 1)
                    .Where(w => w != declared)
                    .Where(w => where.All(at =>
                        !EatsInstructions(rom, at + 1, w)
                        && !LosesAPage(rom, at + 1 + w)
                        && ReadsOn(rom, at + 1 + w).Ended)),
            ];

            if (better.Length == 0)
            {
                clean++;
                continue;
            }

            Console.WriteLine();
            Console.WriteLine(
                $"  0x{code:X2} is read as {declared} bytes and is caught at {caught} of {where.Count} sites");

            Console.WriteLine($"      clean at every site: {string.Join(", ", better)} bytes");

            foreach (int at in where.Where(Suspect).Take(3))
            {
                string hex = string.Join(
                    " ", Enumerable.Range(0, 12).Select(i => $"{rom.ReadU8(at + i):X2}"));

                Console.WriteLine($"        {Rom.BaseAddress + (uint)at:X8}  {hex}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  {clean} widths are clean at every site they are read at");
    }

    private static void WriteOpcodeCounts(Rom rom)
    {
        int[] counts = OpcodeCounts(rom);
        int total = counts.Sum();

        Console.WriteLine();
        Console.WriteLine($"What starts a command, over {total} commands in every script the maps reach");

        foreach ((byte code, int count) in counts
                     .Select((c, i) => ((byte)i, c))
                     .Where(e => e.Item2 > 0)
                     .OrderByDescending(e => e.Item2)
                     .Take(24))
        {
            Console.WriteLine($"    0x{code:X2}  {count,6}  {count / (double)total,7:P2}  {ScriptCommands.NameOf(code)}");
        }

        // And the other end of the list, which is the end that decides things. A width
        // is only ever wrong in the direction of resuming on a byte that starts almost
        // nothing, so the rare ones are the ones worth naming.
        Console.WriteLine();
        Console.WriteLine("  The rarest, which are what a wrong width tends to resume on:");

        foreach ((byte code, int count) in counts
                     .Select((c, i) => ((byte)i, c))
                     .Where(e => e.Item2 is > 0 and < 12)
                     .OrderBy(e => e.Item2))
        {
            Console.WriteLine($"    0x{code:X2}  {count,6}  {count / (double)total,7:P3}  {ScriptCommands.NameOf(code)}");
        }

        // The habit that turns out to decide things: a standard routine that prints a
        // page is always handed the page first. If that holds across the whole game
        // then a candidate width which swallows the loadpointer and leaves the callstd
        // standing alone is not reading a script, it is eating one — and what it eats
        // is the words.
        (int loaded, int alone) = LoadedBeforeCallStandard(rom);

        Console.WriteLine();
        Console.WriteLine(
            $"  callstd has a page loaded earlier in its run {loaded} times and none at all {alone} times " +
            $"({loaded / (double)Math.Max(1, loaded + alone):P1} of the time)");

        var never = counts
            .Select((c, i) => ((byte)i, c))
            .Where(e => e.Item2 == 0 && ScriptCommands.ArgumentLength(e.Item1, 0) is not null)
            .Select(e => e.Item1)
            .ToList();

        Console.WriteLine();
        Console.WriteLine(
            $"  {never.Count} commands this reader knows the width of never start one: " +
            string.Join(" ", never.Select(c => $"0x{c:X2}")));
    }

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

    /// <summary>
    /// Every item this cartridge says can be carried, grouped by the effect number it
    /// carries.
    /// <para>
    /// The field has been extracted since the item table was first read and has never been
    /// looked at. This is the same position abilities were in before they were modelled,
    /// and the same first step: find out what is actually there before writing a line of
    /// rules about it.
    /// </para>
    /// <para>
    /// Grouped by effect rather than listed by item, because the question is not "what does
    /// LEFTOVERS do" — it is "how many distinct things does this cartridge think an item can
    /// do, and which items share each one". A group with eight members is one rule worth
    /// eight items; a group with one member is a rule worth one, and the two deserve
    /// different amounts of attention.
    /// </para>
    /// <para>
    /// The parameter comes with it because the two fields are read together everywhere else
    /// on this cartridge — the same byte that says how much a POTION restores is the one
    /// beside the hold effect — so a group whose members all share a parameter and a group
    /// whose members each have their own are different shapes of rule.
    /// </para>
    /// </summary>
    /// <summary>
    /// The strength bands, computed from this cartridge and printed with their boundaries.
    /// <para>
    /// The whole point of publishing it. A curated tier list can only be argued with; one
    /// computed from the image can be re-run, and this prints the boundaries beside the
    /// members so anybody can check that the second follows from the first.
    /// </para>
    /// </summary>
    private static void WriteTiers(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("Tiers");

        List<SpeciesData> species =
        [
            .. RomExtractor.Open(rom).ExtractSpecies().Where(s => s.Index > 0 && s.BaseStatTotal > 0),
        ];

        if (species.Count == 0)
        {
            Console.WriteLine("  no base stats, so nothing to band");
            return;
        }

        IReadOnlyList<int> cuts = Tiers.Boundaries(species);

        Console.WriteLine(
            $"  {species.Count} species with stats, in {Tiers.Bands} bands at the quintiles of their totals");

        Console.WriteLine($"  boundaries: {string.Join(", ", cuts)}");
        Console.WriteLine();

        foreach (var band in species
                     .GroupBy(s => Tiers.Of(s.BaseStatTotal, cuts))
                     .OrderBy(g => g.Key))
        {
            int[] totals = [.. band.Select(s => s.BaseStatTotal).Order()];

            Console.WriteLine(
                $"  {Tiers.NameOf(band.Key),-12} {band.Count(),4} species, " +
                $"totals {totals[0]}..{totals[^1]}");

            // A handful from each end, because a band's edges are the only part anybody
            // argues with and a full list of eighty is a list nobody reads.
            foreach (SpeciesData one in band.OrderBy(s => s.BaseStatTotal).Take(3)
                         .Concat(band.OrderByDescending(s => s.BaseStatTotal).Take(3).Reverse()))
            {
                Console.WriteLine($"      {one.Index,4} {one.Name,-14} {one.BaseStatTotal,4}");
            }
        }
    }

    private static void WriteHolds(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("Held items");

        if (ItemTable.Locate(rom) is not { } table)
        {
            Console.WriteLine("  no item table, so nothing to say");
            return;
        }

        List<ItemRecord> items = [.. ItemTable.Read(rom, table)];
        List<ItemRecord> carried = [.. items.Where(i => i.HoldEffect != 0)];

        Console.WriteLine(
            $"  {carried.Count} of {items.Count} items carry an effect, " +
            $"across {carried.Select(i => i.HoldEffect).Distinct().Count()} distinct effect numbers");

        Console.WriteLine();

        foreach (var group in carried
                     .GroupBy(i => i.HoldEffect)
                     .OrderByDescending(g => g.Count())
                     .ThenBy(g => g.Key))
        {
            bool sameParam = group.Select(i => i.HoldEffectParam).Distinct().Count() == 1;

            Console.WriteLine(
                $"  effect {group.Key,3} — {group.Count(),2} item(s), " +
                (sameParam ? $"all with parameter {group.First().HoldEffectParam}" : "each with its own parameter"));

            foreach (ItemRecord item in group.OrderBy(i => i.Id))
            {
                Console.WriteLine(
                    $"      {item.Id,4} {item.Name,-14} param {item.HoldEffectParam,3}  {item.Pocket}");
            }
        }

        // And the other half of the same question: what is in the pockets things are
        // carried from that carries nothing. A berry with no effect number is either a
        // berry whose effect lives elsewhere or a berry that does nothing, and which of
        // those it is decides whether there is a second table to go and find.
        Console.WriteLine();
        Console.WriteLine("  In the same pockets, carrying nothing:");

        var pockets = carried.Select(i => i.Pocket).Distinct().ToHashSet();

        foreach (var pocket in items
                     .Where(i => pockets.Contains(i.Pocket) && i.HoldEffect == 0)
                     .GroupBy(i => i.Pocket))
        {
            Console.WriteLine($"      {pocket.Key,-9} {pocket.Count(),3}   e.g. {string.Join(", ", pocket.Take(4).Select(i => i.Name))}");
        }
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

            // And what each of them actually teaches, which is the only thing about a
            // machine a player cares about and the one thing its own record does not
            // say. Printed with the move's name and effect group beside it, because a
            // list of numbers cannot be checked against anything by looking at it: a
            // machine whose move is one along from its own would read perfectly here
            // and be wrong for all fifty-eight.
            List<MoveData> moveTable;

            try
            {
                moveTable = MoveExtractor.Extract(rom);
            }
            catch (InvalidDataException)
            {
                moveTable = [];
            }

            if (moveTable.Count > 0
                && MachineMoves.Locate(rom, moveTable.Count, ObstacleMoves.Find(rom)) is { } listAt)
            {
                List<int> taught = MachineMoves.Read(rom, listAt);

                Console.WriteLine();
                Console.WriteLine($"  What they teach, read at 0x{listAt:X6}:");

                foreach ((ItemRecord item, int move) in machines.Zip(taught))
                {
                    string name = move >= 0 && move < moveTable.Count ? moveTable[move].Name : "?";

                    Console.WriteLine(
                        $"    {item.Id,4} {item.Name,-6} {move,4}  {name,-14} " +
                        $"effect 0x{(move >= 0 && move < moveTable.Count ? moveTable[move].Effect : 0):X2}");
                }
            }
        }

        // What each one clears, which is in no field of its own record and not in the
        // routine either — all six of the named cures run the same one. Printed with
        // the bit each anchor claimed, because "ANTIDOTE is poison" is a claim and
        // "ANTIDOTE alone sets 0x10, and the one that clears everything sets 0x3F" is
        // evidence.
        if (ItemEffects.Locate(rom, items, Console.WriteLine) is { } cures)
        {
            Console.WriteLine();
            Console.WriteLine($"  What clears what, from items {cures.FirstItem}..{cures.LastItem}:");

            foreach ((int id, Ailments clears) in cures.Cures.OrderBy(c => c.Key))
            {
                string name = id < items.Count ? items[id].Name.Trim() : $"item {id}";

                Console.WriteLine($"    {id,4} {name,-14} {clears}");
            }
        }

        // Which pockets holding is for. No field says it, and the obvious reading —
        // anything with a hold effect — is wrong: most of what a player hands over has
        // no hold effect at all, and a Potion held does nothing and is still held. What
        // the cartridge does say, and says clearly, is which pockets ever use the field.
        Console.WriteLine();
        Console.WriteLine("  Where the hold effect field is used at all:");

        foreach (IGrouping<Pocket, ItemData> pocket in items
                     .Select(i => i.ToData())
                     .GroupBy(i => i.Pocket)
                     .OrderBy(g => (int)g.Key))
        {
            int holding = pocket.Count(i => i.HoldEffect != 0);

            Console.WriteLine(
                $"    {pocket.Key,-10} {pocket.Count(),4} items, {holding,3} with a hold effect" +
                (holding == 0 ? "   <- nothing here is carried" : ""));
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

    /// <summary>
    /// Writes one character's walking frames out, plus the silhouette and the outline that
    /// the cosmetic art is actually placed against.
    /// <para>
    /// This exists because somebody drawing clothes needs to know what they are drawing them
    /// on, and the answer is not in this repository and never will be: the figure comes off
    /// the player's own cartridge. So it is a command they run on their own machine against
    /// their own file, writing to their own directory. Nothing it produces is committed,
    /// shipped, or sent anywhere.
    /// </para>
    /// <para>
    /// Three things come out, and the third is the one worth having:
    /// </para>
    /// <list type="number">
    /// <item>every frame as it is, so the proportions can be seen exactly;</item>
    /// <item>the frames again at eight times, because sixteen by thirty-two is small;</item>
    /// <item>a <b>silhouette</b> — the exact shape with every pixel flattened to one colour.</item>
    /// </list>
    /// <para>
    /// The silhouette is the honest thing to hand an artist. It carries the whole of what
    /// they need — where the head ends, how wide the shoulders are, where the feet sit — and
    /// none of the pixels somebody else drew. Art made to fit a silhouette is art this
    /// project owns; art traced over the original is not, and the difference matters for
    /// exactly the thing the cosmetics are for.
    /// </para>
    /// </summary>
    private static void WriteOneCharacter(Rom rom, int graphicsId, string outputDirectory)
    {
        Console.WriteLine();
        Console.WriteLine($"Character {graphicsId}");

        if (OverworldSprites.LocateGraphicsTable(rom, Console.WriteLine) is not { } table)
        {
            Console.WriteLine("  no graphics table found");
            return;
        }

        if (OverworldSprites.LocatePaletteTable(rom, Console.WriteLine) is not { } palettes)
        {
            Console.WriteLine("  no palette table found");
            return;
        }

        List<ObjectGraphicsInfo?> records = OverworldSprites.ReadGraphics(rom, table, 256);

        if (records.ElementAtOrDefault(graphicsId) is not { } info)
        {
            Console.WriteLine($"  nothing at slot {graphicsId}");
            return;
        }

        Dictionary<int, int> boundaries = OverworldSprites.FrameListBoundaries(rom, records);

        List<IndexedImage> frames = OverworldSprites.ReadFrames(rom, info, boundaries);

        if (frames.Count == 0)
        {
            Console.WriteLine("  no frames");
            return;
        }

        if (OverworldSprites.PaletteForTag(rom, palettes, info.PaletteTag) is not { } palette)
        {
            Console.WriteLine("  no palette for this one's tag");
            return;
        }

        string directory = Path.Combine(outputDirectory, $"character-{graphicsId:D3}");
        Directory.CreateDirectory(directory);

        Console.WriteLine($"  {info.Width}x{info.Height}, {frames.Count} frames");

        // The nine, named for what they are rather than for where they sit. Somebody drawing
        // a hat should not have to know that frame seven is a left-facing stride.
        string[] named =
        [
            "0-down-still", "1-up-still", "2-left-still",
            "3-down-step-a", "4-down-step-b",
            "5-up-step-a", "6-up-step-b",
            "7-left-step-a", "8-left-step-b",
        ];

        for (int frame = 0; frame < frames.Count; frame++)
        {
            string name = frame < named.Length ? named[frame] : $"{frame}";

            byte[] rgba = frames[frame].ToRgba(palette);

            PngWriter.Write(
                Path.Combine(directory, $"frame-{name}.png"),
                frames[frame].Width, frames[frame].Height, rgba);

            PngWriter.Write(
                Path.Combine(directory, $"frame-{name}@8x.png"),
                frames[frame].Width * 8, frames[frame].Height * 8,
                Enlarge(rgba, frames[frame].Width, frames[frame].Height, 8));

            PngWriter.Write(
                Path.Combine(directory, $"silhouette-{name}.png"),
                frames[frame].Width, frames[frame].Height,
                Flatten(rgba, frames[frame].Width, frames[frame].Height));
        }

        Console.WriteLine($"  wrote {frames.Count * 3} files to {directory}");

        // And the rectangle everything is actually placed against, which is measured rather
        // than assumed — see CharacterSprite, where getting this wrong put hats in the air.
        (int x, int y, int w, int h) = Bounds(frames[0].ToRgba(palette), frames[0].Width, frames[0].Height);

        Console.WriteLine();
        Console.WriteLine("  The figure inside the frame, which is what cosmetic art is scaled onto:");
        Console.WriteLine($"    frame   {frames[0].Width} x {frames[0].Height}");
        Console.WriteLine($"    figure  {w} x {h} at ({x}, {y})");
        Console.WriteLine();
        Console.WriteLine("  In the sixteen-by-thirty-two box the art is drawn in, one box step is");
        Console.WriteLine($"    {w / 16.0:0.00} pixels across and {h / 32.0:0.00} pixels down.");
        Console.WriteLine();
        Console.WriteLine("  Hand an artist the silhouettes rather than the frames. They carry the");
        Console.WriteLine("  whole of the shape and none of somebody else's pixels, and art drawn to");
        Console.WriteLine("  fit one is art this project owns.");
    }

    /// <summary>Nearest-neighbour, because pixels have to stay pixels.</summary>
    private static byte[] Enlarge(byte[] rgba, int width, int height, int by)
    {
        var bigger = new byte[width * by * height * by * 4];

        for (int y = 0; y < height * by; y++)
        {
            for (int x = 0; x < width * by; x++)
            {
                int from = ((y / by) * width + x / by) * 4;
                int to = (y * width * by + x) * 4;

                Array.Copy(rgba, from, bigger, to, 4);
            }
        }

        return bigger;
    }

    /// <summary>
    /// Every pixel that is there at all, in one flat colour. The shape without the drawing.
    /// </summary>
    private static byte[] Flatten(byte[] rgba, int width, int height)
    {
        var flat = new byte[rgba.Length];

        for (int i = 0; i < width * height; i++)
        {
            if (rgba[i * 4 + 3] == 0) continue;

            flat[i * 4] = 90;
            flat[i * 4 + 1] = 90;
            flat[i * 4 + 2] = 100;
            flat[i * 4 + 3] = 255;
        }

        return flat;
    }

    /// <summary>The smallest rectangle holding everything that is not transparent.</summary>
    private static (int X, int Y, int Width, int Height) Bounds(byte[] rgba, int width, int height)
    {
        int left = width, top = height, right = -1, bottom = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (rgba[(y * width + x) * 4 + 3] == 0) continue;

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0 ? (0, 0, width, height) : (left, top, right - left + 1, bottom - top + 1);
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
              --character [id]       write one character's walking frames, the same
                                     frames at eight times, and a silhouette of each.
                                     Defaults to 0, the player. Writes to your own
                                     output directory and nowhere else.
              --overworld            report the overworld sprite tables and write a
                                     few of the walking figures as PNGs
              --sound                report the whole sound walk: recordings,
                                     instruments, songs, the song table, the cry
                                     table, and how many songs assemble. Prints
                                     what was found, including what was not.
              --trainers             report the trainer table: where it starts, what
                                     was rejected just before it, and a few parties
              --items                report the item table, its pockets and prices
              --scripts              report how far object scripts read, and which
                                     commands stop them
              --new-game             report the flags and variables a new save already
                                     holds, and who they keep off the map
              --after-fights         report what every script that starts a fight with
                                     nobody in it says once the fight is over
              --evolutions           report the evolution table: where it is, which
                                     method means a level, and what turns into what
              --machines             report which machines work on which species, and
                                     how the table was told apart from seven thousand
                                     runs of bytes with the same shape
              --computers            report the behaviour byte that means a storage
                                     machine, and the evidence separating it
              --letters              hunt for the cartridge's own lettering four ways,
                                     and report where it is not
              --clears <flag>        who turns a flag on and who turns it off, e.g.
                                     --clears 0x0035
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

        /// <summary>
        /// Write one character's walking frames out as PNGs, for somebody drawing clothes to
        /// put on them. Null unless asked for.
        /// </summary>
        public int? Character { get; private init; }

        public bool DumpTrainers { get; private init; }
        public bool DumpItems { get; private init; }

        public bool DumpHolds { get; private init; }

        public bool DumpTiers { get; private init; }
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

        /// <summary>Everybody who hands over a monster, and what.</summary>
        public bool Gifts { get; private init; }

        /// <summary>A script variable to trace: who writes it, and who is waiting on it.</summary>
        public int? Variable { get; private init; }

        /// <summary>Check the map's own script list — the fifth pointer in every header.</summary>
        public bool Probe { get; private init; }

        /// <summary>The map whose scripts to run, rather than read.</summary>
        public string ScriptRun { get; private init; } = "";

        /// <summary>Count what stops a run, across every script on the cartridge.</summary>
        public bool ScriptRuns { get; private init; }

        public bool Substitutions { get; private init; }

        public bool HideFlags { get; private init; }

        public bool NameRuns { get; private init; }

        public string Walkable { get; private init; } = "";

        public bool SightLines { get; private init; }

        /// <summary>Which commands run in front of a line of dialogue that has a gap in it.</summary>
        public bool Gaps { get; private init; }

        /// <summary>Narrows the run-ups printed to lines containing this.</summary>
        public string GapLike { get; private init; } = "";

        /// <summary>Every trainerbattle by variant, and what its extra pointer holds.</summary>
        public bool FightKinds { get; private init; }

        /// <summary>Where a door puts you, and which way there is room to step.</summary>
        public bool DoorSteps { get; private init; }

        /// <summary>Every move grouped by the effect byte in its record.</summary>
        public bool MoveEffects { get; private init; }

        /// <summary>Where the six effort yields are in a species record, and which is which.</summary>
        public bool Efforts { get; private init; }

        public bool FifthMove { get; private init; }

        public bool Challenges { get; private init; }

        public bool Water { get; private init; }

        public bool Doors2 { get; private init; }

        public bool WarpShape { get; private init; }

        public string WaterMap { get; private init; } = "";

        /// <summary>Which trainers are fought by a script that names the rival.</summary>
        public bool RivalFights { get; private init; }

        /// <summary>Count which special routines get called, and on which maps.</summary>
        public bool Specials { get; private init; }

        /// <summary>The doors scripts make, which are on no map.</summary>
        public bool ScriptedDoors { get; private init; }

        public int? Special { get; private init; }

        public byte? Answers { get; private init; }

        public bool AnswerSweep { get; private init; }

        public string SpecialsOn { get; private init; } = "";

        /// <summary>Count the scripts map objects hand their work to.</summary>
        public bool Shared { get; private init; }

        /// <summary>Split the people whose script finishes and says nothing by cause.</summary>
        public bool Silent { get; private init; }

        /// <summary>Everything the sound walk finds, so it can be checked against a real file.</summary>
        public bool Sound { get; private init; }

        /// <summary>Score every argument width for the commands that stop a run.</summary>
        public bool Derive { get; private init; }

        public bool Opcodes { get; private init; }

        public bool Audit { get; private init; }

        public bool Ledges { get; private init; }

        /// <summary>Report the flags and variables a brand new save already holds.</summary>
        public bool NewGame { get; private init; }

        /// <summary>Report what a script says once the fight it started is over.</summary>
        public bool AfterFights { get; private init; }

        /// <summary>Report the evolution table and what it says.</summary>
        public bool Evolutions { get; private init; }

        public bool Machines { get; private init; }

        public bool Computers { get; private init; }

        public bool Letters { get; private init; }

        public int? Clears { get; private init; }

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
            int? character = null;
            bool trainers = false;
            bool items = false;
            bool holds = false;
            bool tiers = false;
            bool scripts = false;
            string scriptMap = "";
            string at = "";
            string scriptRun = "";
            bool scriptRuns = false;
            bool substitutions = false;
            bool hideFlags = false;
            bool nameRuns = false;
            string walkable = "";
            bool sightLines = false;
            bool gaps = false;
            string gapLike = "";
            bool fightKinds = false;
            bool doorSteps = false;
            bool moveEffects = false;
            bool efforts = false;
            bool fifthMove = false;
            bool challenges = false;
            bool water = false;
            bool doors2 = false;
            bool warpShape = false;
            string waterMap = "";
            bool rivalFights = false;
            bool specials = false;
            bool scriptedDoors = false;
            int? special = null;
            byte? answers = null;
            bool answerSweep = false;
            string specialsOn = "";
            bool shared = false;
            bool silent = false;
            bool sound = false;
            bool derive = false;
            bool opcodes = false;
            bool audit = false;
            bool ledges = false;
            bool newGame = false;
            bool afterFights = false;
            bool evolutions = false;
            bool machines = false;
            bool computers = false;
            bool letters = false;
            int? clears = null;
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
            bool gifts = false;
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
                    case "--character":
                        // The graphics id, defaulting to the first player character, which is
                        // index zero. Given as a number so every other character in the game
                        // can be looked at with the same command.
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int who))
                        {
                            character = who;
                            i++;
                        }
                        else
                        {
                            character = 0;
                        }

                        break;
                    case "--trainers":
                        trainers = true;
                        break;
                    case "--items":
                        items = true;
                        break;
                    case "--holds":
                        holds = true;
                        break;
                    case "--tiers":
                        tiers = true;
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
                    case "--substitutions":
                        substitutions = true;
                        break;
                    case "--hide-flags":
                        hideFlags = true;
                        break;
                    case "--name-runs":
                        nameRuns = true;
                        break;
                    case "--walkable":
                        walkable = Next(args, ref i, "--walkable");
                        break;
                    case "--rival-fights":
                        rivalFights = true;
                        break;
                    case "--fifth-move":
                        fifthMove = true;
                        break;

                    case "--move-effects":
                        moveEffects = true;
                        break;
                    case "--evs":
                        efforts = true;
                        break;
                    case "--door-steps":
                        doorSteps = true;
                        break;
                    case "--water-map":
                        waterMap = Next(args, ref i, "--water-map");
                        break;

                    case "--warp-shape":
                        warpShape = true;
                        break;

                    case "--two-way":
                        doors2 = true;
                        break;

                    case "--water":
                        water = true;
                        break;

                    case "--challenges":
                        challenges = true;
                        break;

                    case "--fight-kinds":
                        fightKinds = true;
                        break;
                    case "--sight-lines":
                        sightLines = true;
                        break;
                    case "--gaps":
                        gaps = true;

                        // Optionally narrowed to one sentence, because the run-up in
                        // front of "{FD}{03} inches" is the one worth reading and it is
                        // the fiftieth of sixty-six.
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                            gapLike = args[++i];

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
                    case "--scripted-doors":
                        scriptedDoors = true;
                        break;
                    case "--shared":
                        shared = true;
                        break;
                    case "--silent":
                        silent = true;
                        break;
                    case "--sound":
                        sound = true;
                        break;
                    case "--derive":
                        derive = true;
                        break;
                    case "--opcodes":
                        opcodes = true;
                        break;
                    case "--audit":
                        audit = true;
                        break;
                    case "--ledges":
                        ledges = true;
                        break;
                    case "--new-game":
                        newGame = true;
                        break;
                    case "--after-fights":
                        afterFights = true;
                        break;
                    case "--evolutions":
                        evolutions = true;
                        break;
                    case "--machines":
                        machines = true;
                        break;
                    case "--computers":
                        computers = true;
                        break;
                    case "--letters":
                        letters = true;
                        break;
                    case "--clears":
                        string flagAsked = Next(args, ref i, "--clears");
                        clears = flagAsked.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? Convert.ToInt32(flagAsked[2..], 16)
                            : int.Parse(flagAsked);
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
                    case "--gifts":
                        gifts = true;
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
                Character = character,
                DumpTrainers = trainers,
                DumpItems = items,
                DumpHolds = holds,
                DumpTiers = tiers,
                DumpScripts = scripts,
                ScriptMap = scriptMap,
                At = at,
                Step = step,
                Doors = doors,
                PlayerWalks = playerWalks,
                Gifts = gifts,
                Variable = variable,
                Probe = probe,
                ScriptRun = scriptRun,
                ScriptRuns = scriptRuns,
                Substitutions = substitutions,
                HideFlags = hideFlags,
                NameRuns = nameRuns,
                Walkable = walkable,
                SightLines = sightLines,
                Gaps = gaps,
                GapLike = gapLike,
                FightKinds = fightKinds,
                DoorSteps = doorSteps,
                MoveEffects = moveEffects,
                Efforts = efforts,
                FifthMove = fifthMove,
                Challenges = challenges,
                Water = water,
                Doors2 = doors2,
                WarpShape = warpShape,
                WaterMap = waterMap,
                RivalFights = rivalFights,
                Specials = specials,
                ScriptedDoors = scriptedDoors,
                Special = special,
                Answers = answers,
                AnswerSweep = answerSweep,
                SpecialsOn = specialsOn,
                Shared = shared,
                Silent = silent,
                Sound = sound,
                Derive = derive,
                Opcodes = opcodes,
                Audit = audit,
                Ledges = ledges,
                NewGame = newGame,
                AfterFights = afterFights,
                Evolutions = evolutions,
                Machines = machines,
                Computers = computers,
                Letters = letters,
                Clears = clears,
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
