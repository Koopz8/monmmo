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
using PokeMmo.Core.Sound;
using PokeMmo.RomExtract.Trainers;
using PokeMmo.Core.Save;
using PokeMmo.Server;

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
            WriteScriptRuns(rom, options.ScriptRun, options.Variables, options.RoutineAnswers, options.SayYes);

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
        if (options.FlagGates) WriteFlagGates(rom);
        if (options.SpecialContracts) WriteSpecialContracts(rom);
        if (options.Standard) WriteRoutinesReachedByNumber(rom);
        if (options.TheScan) WriteWhatTheScanOpens(rom);
        if (options.PersonCommands) WriteTwoCommands(rom);
        if (options.Arrivals) WriteArrivals(rom);
        if (options.TheFloor) WriteTheFloorTable(rom, options.StartAt);
        if (options.FieldEffects) WriteFieldEffects(rom);
        if (options.ReadFrom.Count > 0) WriteBlocks(rom, options.ReadFrom);
        if (options.Closure) WriteClosure(rom, options.RoutineAnswers, options.StartAt);
        if (options.Play)
            WritePlaythrough(
                rom, options.RoutineAnswers, options.StartAt, options.Boat, options.Money, options.SayYes,
                options.Variables, options.Surf, options.InOrder, options.Watch);
        if (options.WhereFrom.Count > 0) WriteWhereFrom(rom, options.WhereFrom);
        if (options.InTheImage.Count > 0) WriteInTheImage(rom, options.InTheImage);
        if (options.ClimbFrom.Count > 0) WriteClimb(rom, options.ClimbFrom);
        if (options.WhoWrites.Count > 0) WriteWhoWrites(rom, options.WhoWrites);
        if (options.WhoReads.Count > 0) WriteWhoReads(rom, options.WhoReads);
        if (options.ThroughACall) WriteThroughACall(rom);
        if (options.Stops.Count > 0) WriteStops(rom, options.Stops);
        if (options.Fights) WriteFights(rom);
        if (options.WhoKnows) WriteWhoKnows(rom);
        if (options.Coins) WriteTheCoinCase(rom);
        if (options.Entries) WriteEntries(rom);
        if (options.Counters) WriteCounters(rom);

        if (options.SequenceWidths) WriteSequenceWidths(rom);

        if (options.OneSong is { } whichSong) WriteOneSong(rom, whichSong);

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

        // Byte two, between the class and the picture, read past since trainers were first
        // read. Printed rather than named: what its low seven bits select is not in any table
        // on this file, so calling it anything here would be importing a fact from elsewhere
        // and printing it as though it had been found.
        //
        // What the numbers should say if the split is right: a handful of distinct low values
        // across the whole table, and a top bit that is set on roughly the share of trainers
        // you would expect to differ in some one way. A low half taking dozens of values, or
        // a top bit that is never set, would mean the byte is one field rather than two.
        var lowHalves = trainers.GroupBy(t => t.PackedIndex).OrderByDescending(g => g.Count()).ToList();

        Console.WriteLine();
        Console.WriteLine(
            $"    byte 2 of the record: {lowHalves.Count} distinct value(s) in its low seven bits, "
            + $"top bit set on {trainers.Count(t => t.PackedFlag)} of {trainers.Count}");

        foreach (IGrouping<int, TrainerRecord> half in lowHalves.Take(10))
        {
            Console.WriteLine(
                $"      {half.Key,3}: {half.Count(),4} trainers — classes "
                + string.Join(", ", half.Select(t => t.Class).Distinct().OrderBy(c => c).Take(6)));
        }

        if (lowHalves.Count > 10) Console.WriteLine($"      ... and {lowHalves.Count - 10} more");

        Console.WriteLine(
            lowHalves.Count <= 16
                ? "      a few values across the whole table is what a small index looks like"
                : "      that many values is not a small index, and the byte is probably one field");

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
    /// <summary>
    /// Where reads stop, across every kind of script and every block they reach.
    /// <para>
    /// <b>This asked two smaller questions and read as though it had asked this one.</b> It
    /// walked <em>people</em>, and only the <em>first block</em> of each — so a command that
    /// stops a read had to be in a person's opening straight line to be counted at all. It
    /// reported eight stopped reads on a cartridge where the true figure is a different number
    /// entirely, and eight is small enough to look like a solved problem.
    /// </para>
    /// <para>
    /// What it was blind to, exactly: 1.80's on-arrival script runs through a goto into a
    /// shared block, and stops on <c>0x9E</c> eleven bytes before a <c>call</c> that clears
    /// the flag keeping nineteen people off eleven maps. Neither a person nor a first block,
    /// so neither half of the old reading could see it, and the output was identical to a
    /// reading that had looked.
    /// </para>
    /// </summary>
    private static void WriteScripts(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("Scripts");

        MapLibrary library = MapLibrary.Open(rom);

        var stoppers = new Dictionary<byte, int>();
        var byKind = new Dictionary<string, int>();
        var opened = new Dictionary<string, int>();
        var examples = new Dictionary<byte, (int Start, int Stop)>();
        var seen = new HashSet<uint>();

        var scripts = 0;
        var blocks = 0;
        var stopped = 0;
        var withMart = 0;
        var withTrainer = 0;

        foreach (LoadedMap map in library.All())
        {
            foreach (SetsAFlag script in EveryScriptOn(map))
            {
                scripts++;

                string kind = KindOf(script);

                opened[kind] = opened.GetValueOrDefault(kind) + 1;

                if (script.What.StartsWith("person", StringComparison.Ordinal))
                {
                    if (ScriptReader.FindMart(rom, script.Address).Count > 0) withMart++;
                    if (ScriptReader.FindTrainer(rom, script.Address) is not null) withTrainer++;
                }

                // Every block it reaches, not just the one it starts in. A read stops per
                // block, and most of the work in this game is at the other end of a call.
                foreach (uint block in ScriptReader.Reachable(rom, script.Address))
                {
                    if (!seen.Add(block)) continue;

                    blocks++;

                    if (ScriptReader.StoppedAt(rom, block) is not { } code) continue;

                    stopped++;
                    stoppers[code] = stoppers.GetValueOrDefault(code) + 1;
                    byKind[kind] = byKind.GetValueOrDefault(kind) + 1;

                    if (!examples.ContainsKey(code)
                        && rom.ToOffsetOrNull(block) is { } from
                        && ScriptReader.StoppedAtOffset(rom, block) is { } at)
                    {
                        examples[code] = (from, at);
                    }
                }
            }
        }

        Console.WriteLine(
            $"  {scripts} script(s) on {library.All().Count()} map(s), reaching {blocks} block(s)");
        Console.WriteLine(
            "    by kind: " + string.Join(", ", opened.OrderByDescending(k => k.Value).Select(k => $"{k.Value} {k.Key}")));
        Console.WriteLine(
            $"  {blocks - stopped} of those blocks read to a proper end, {stopped} stopped at a command "
            + "this project does not have a width for");

        if (byKind.Count > 0)
        {
            Console.WriteLine(
                "    the stops, by what the script is attached to: "
                + string.Join(", ", byKind.OrderByDescending(k => k.Value).Select(k => $"{k.Value} {k.Key}")));
        }

        Console.WriteLine($"  {withTrainer} people name a trainer, {withMart} open a shop");
        // AND WHAT IS BEHIND EACH ONE, WHICH IS THE RANKING.
        //
        // How often a command stops a read is a count, and this project has written down once
        // already that a count is not a ranking. 0x73 stops three hundred and seventy-eight
        // runs of the playthrough — more than every other unknown command put together — and at
        // every one of its sites what follows is a release and an end. Nothing is behind it.
        // 0x9E stopped three blocks and one of the three was eleven bytes from the call that
        // puts nineteen people on eleven maps.
        //
        // The width is unknown, so this does not pick one: it tries them all, keeps the ones
        // that read to a proper end, and reports what they find between them. "Every width that
        // parses finds nothing" needs no guess to stand on.
        Console.WriteLine();
        Console.WriteLine("  The commands stopping the most reads, and what is behind each:");

        var behind = new Dictionary<byte, Behind>();

        foreach ((byte code, int _) in stoppers)
        {
            if (examples.TryGetValue(code, out (int Start, int Stop) where))
                behind[code] = WhatIsBehindAStop.Of(rom, where.Stop);
        }

        foreach ((byte code, int count) in stoppers.OrderByDescending(s => s.Value).Take(20))
        {
            Console.WriteLine(
                $"    0x{code:X2}  stops {count,3}  — "
                + (behind.TryGetValue(code, out Behind? what) ? what.ToString() : "not sampled"));
        }

        List<byte> costly =
        [
            .. stoppers.Keys
                .Where(code => behind.TryGetValue(code, out Behind? what) && !what.NothingBehindIt
                    && what.WidthsThatParse > 0)
                .OrderByDescending(code => stoppers[code]),
        ];

        Console.WriteLine();
        Console.WriteLine(
            $"  {costly.Count} of those {stoppers.Count} have something behind them at every width "
            + "that reads on. THAT is the list:");
        Console.WriteLine(
            "    " + (costly.Count == 0
                ? "none — every stop on this cartridge is two bytes from the end of its block"
                : string.Join(", ", costly.Take(12).Select(code => $"0x{code:X2} ({stoppers[code]})"))));

        // A count says which command is in the way; it does not say how long that
        // command is, and guessing a length is worse than not knowing one — a wrong
        // length resumes inside an argument and invents every instruction after it.
        // The bytes are what settle it: a pointer is recognisable on sight.
        Console.WriteLine();
        Console.WriteLine("  One block for each, from its start, with ^ under where it stopped:");

        foreach (byte code in stoppers.OrderByDescending(s => s.Value).Take(8).Select(s => s.Key))
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

        static string KindOf(SetsAFlag script) =>
            script.What.StartsWith("on ", StringComparison.Ordinal)
                ? string.Join(" ", script.What.Split(' ').Take(2))
                : script.What.Split(' ')[0];
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
    /// <summary>
    /// Every script on one map, run, with the levers the playthrough has.
    /// <para>
    /// <b>They were not wired in, and that is how a question stayed unanswerable.</b> The
    /// starter is behind <c>0x4055 == 2</c> and then a yes-or-no, and this is the one tool that
    /// shows a script line by line — so the only way to see what happens on the other side of
    /// either was to run the whole playthrough and infer it from a party list. A lever that
    /// exists on one instrument and not on the one that shows the detail is a lever nobody can
    /// aim.
    /// </para>
    /// </summary>
    private static void WriteScriptRuns(
        Rom rom,
        string mapId,
        IReadOnlyDictionary<int, int>? variables = null,
        IReadOnlyDictionary<int, int>? answers = null,
        bool sayYes = false)
    {
        Console.WriteLine();
        Console.WriteLine($"Running the scripts on {mapId}");

        if (variables is { Count: > 0 })
        {
            Console.WriteLine(
                "  MODELLED: starting with "
                + string.Join(", ", variables.Select(v => $"0x{v.Key:X4} = {v.Value}"))
                + " — nothing on the cartridge says a run holds these");
        }

        if (sayYes) Console.WriteLine("  MODELLED: answering yes to every offer");

        MapLibrary library = MapLibrary.Open(rom);

        if (library.TryLoad(mapId) is not { } map)
        {
            Console.WriteLine($"  no map {mapId} on this cartridge");
            return;
        }

        Console.WriteLine($"  {map.Name}, {map.Objects.Count(o => o.HasScript)} of {map.Objects.Count} with a script");

        foreach (MapObject person in map.Objects.Where(o => o.HasScript))
        {
            var start = new ScriptState(variables: variables?.Select(v => new KeyValuePair<int, int>(v.Key, v.Value)));

            ScriptRun fresh = ScriptRunner.Run(rom, person.ScriptAddress, start, answers: answers);

            // And the offers taken, the same way the playthrough takes them: run again from
            // where it stopped, up to a handful of times, so a scene behind two questions is
            // not a scene behind one.
            for (var answered = 0; sayYes && fresh.Question is { } carryOn && answered < 8; answered++)
            {
                foreach ((int variable, int value) in fresh.VariablesWritten) start.Write(variable, value);

                start.Write(SpecialContracts.AnswerVariable, 1);

                ScriptRun next = ScriptRunner.Run(rom, carryOn, start, answers: answers);

                fresh = fresh with
                {
                    Pages = [.. fresh.Pages, .. next.Pages],
                    FlagsSet = [.. fresh.FlagsSet, .. next.FlagsSet],
                    FlagsCleared = [.. fresh.FlagsCleared, .. next.FlagsCleared],
                    GivesMon = fresh.GivesMon ?? next.GivesMon,
                    GivesItem = fresh.GivesItem ?? next.GivesItem,
                    Question = next.Question,
                };
            }

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
            $"  {calls.Count} calls at {profiles.Sum(p => p.Places)} byte position(s) to " +
            $"{profiles.Count} different routines; " +
            $"{profiles.Count(p => p.Answers)} of them are asked a question, " +
            $"{profiles.Count(p => p.ArgumentSlots.Count > 0)} are given arguments");

        // The number that matters. Nothing calls these routines, so the answer variable
        // keeps its zero — and a zero is an answer, not an absence. Every site where the
        // script says "if the answer is zero, skip this" is a piece of the game being
        // skipped right now, quietly, on a technicality.
        Console.WriteLine(
            $"  {profiles.Count(p => p.ZeroIsMisleading)} routines branch away on the zero they " +
            $"are getting by default, at " +
            $"{profiles.Sum(p => p.BranchesTakenByZero)} of {profiles.Sum(p => p.Branches)} branching sites "
            + $"— which are {profiles.Sum(p => p.PlacesTakenByZero)} of {profiles.Sum(p => p.BranchPlaces)} "
            + "byte position(s), and that is the number about the cartridge");

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
    /// <summary>
    /// What every flag actually gates, which is the question co-op could not answer.
    /// <para>
    /// Two people playing together want a door one of them opened to be open for both, and
    /// do not want a badge one of them earned to appear on the other. The cartridge does not
    /// distinguish them — there is no bit anywhere saying "this flag is about the world" — so
    /// the classification cannot be read. It is derived instead, from what the world file
    /// itself uses each flag for, and this prints the derivation so it can be looked at
    /// rather than trusted.
    /// </para>
    /// <para>
    /// The number to read first is the split. A clean one means the rule writes itself; a
    /// mess is the finding.
    /// </para>
    /// </summary>
    /// <summary>
    /// How far a player can actually get by playing, walked the whole way.
    /// <para>
    /// Every other reach figure this project has printed is one photograph: given these
    /// flags and these moves, where can somebody stand. Playing is not one photograph — you
    /// walk as far as you can, talk to whoever is there, and what they do opens more world.
    /// This is that loop run until it stops opening anything.
    /// </para>
    /// <para>
    /// <b>It is a floor, not a ceiling.</b> A <c>special</c> is a call into the cartridge's
    /// own code, the runner steps over it, and the answer variable keeps its zero — so a
    /// script that asks a question and branches on the answer takes the zero arm. Every badge
    /// check in this game is one of those. Where this walk says a door is shut, the door may
    /// simply have asked something nobody could answer, and the count of those is printed
    /// beside the result rather than left out of it.
    /// </para>
    /// </summary>
    /// <summary>
    /// What every routine this project cannot execute is actually asked.
    /// <para>
    /// The boundary measured from the outside. What a routine <em>does</em> is compiled code
    /// and unreadable; what its callers <em>expect</em> is in the bytes — how many arguments
    /// they hand it, what they compare the answer against, and whether they branch at all.
    /// </para>
    /// <para>
    /// That is the specification a stand-in would have to satisfy, and it is checkable in a
    /// way a guess is not: supply an answer with <c>--answer</c>, walk the story again, and
    /// see how much of the world opens.
    /// </para>
    /// </summary>
    /// <summary>
    /// A number written as decimal or as hex, because a routine number is always quoted in
    /// hex and a badge count never is.
    /// </summary>
    private static bool TryNumber(string text, out int value)
    {
        text = text.Trim();

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(
                text[2..], System.Globalization.NumberStyles.HexNumber, null, out value);
        }

        return int.TryParse(text, out value);
    }

    /// <summary>
    /// Plays the game from a fresh save and says how far it got.
    /// <para>
    /// The instrument the question "can I finish it" actually wants. The closure walk answers
    /// where somebody could <em>stand</em>; this one talks to people, takes what they hand
    /// over, and fights what fights back — so it can tell a door that will not open from a
    /// fight that cannot be won, which are two very different things to have to fix.
    /// </para>
    /// <para>
    /// It is a floor twice over: a routine this project cannot execute answers zero and its
    /// callers take the zero arm, and this plays badly on purpose — best move by raw power,
    /// never switches, never buys anything, never heals between fights. A fight it loses is
    /// not proof a person would. A fight it <em>wins</em> is proof the fight works.
    /// </para>
    /// </summary>
    /// <summary>
    /// Every place in the image a script names one of these items.
    /// <para>
    /// The world file's answer to "where does a FRESH WATER come from" was one shop counter,
    /// two hops and a boat from anywhere the story reaches, and that could not be the whole
    /// truth: the world file records what it can attribute to an object, and a vending
    /// machine that offers a menu and hands the drink over inside a routine is attributable
    /// to nobody. So this asks the image instead.
    /// </para>
    /// <para>
    /// <b>Every arm, not the one that runs.</b> This reads rather than runs — a script that
    /// only hands something over on a branch today's save cannot take still hands it over,
    /// and that is exactly what is being looked for.
    /// </para>
    /// </summary>
    private static void WriteWhereFrom(Rom rom, IReadOnlyList<int> items)
    {
        Console.WriteLine();
        Console.WriteLine("WHERE THESE COME FROM");
        Console.WriteLine();

        Dictionary<int, string> names = ItemTable.Locate(rom) is { } itemsAt
            ? ItemTable.Read(rom, itemsAt).ToDictionary(item => item.Id, item => item.Name)
            : [];

        string NameOf(int itemId) =>
            names.GetValueOrDefault(itemId) is { Length: > 0 } name
                ? $"{name} (0x{itemId:X3})"
                : $"item 0x{itemId:X3}";

        MapLibrary library = MapLibrary.Open(rom);

        List<ItemSite> sites = ItemMentions.Of(rom, library, items);

        Console.WriteLine(
            $"  {sites.Count} mention(s) of {items.Count} item(s) across every script the maps reach");

        foreach (int itemId in items)
        {
            List<ItemSite> mine = [.. sites.Where(s => s.ItemId == itemId)];

            Console.WriteLine();
            Console.WriteLine($"  {NameOf(itemId)} — {mine.Count} mention(s)");

            if (mine.Count == 0)
            {
                Console.WriteLine(
                    "    NOTHING IN ANY SCRIPT NAMES IT. Whatever produces one is inside a routine,");
                Console.WriteLine(
                    "    and no amount of reading scripts will ever find it.");

                continue;
            }

            foreach (IGrouping<string, ItemSite> how in mine.GroupBy(s => s.How).OrderBy(g => g.Key))
            {
                Console.WriteLine($"    {how.Key}: {how.Count()} site(s)");

                foreach (ItemSite site in how.Take(12))
                {
                    Console.WriteLine(
                        $"      {site.MapId,-8} {site.What,-18} script 0x{site.Address:X8}"
                        + $" at 0x{site.Offset:X6}"
                        + (site.Count > 1 ? $"  x{site.Count}" : ""));
                }

                if (how.Count() > 12) Console.WriteLine($"      ... and {how.Count() - 12} more");
            }
        }
    }

    /// <summary>
    /// Why a door did not open, in the order the answers rule each other out.
    /// <para>
    /// An export fault first, because a door square that cannot be stood on is this project's
    /// mistake rather than the game's and must never be reported as a story gate.
    /// </para>
    /// </summary>
    private static string WhyShut(ShutDoor door) =>
        door.CouldStandOnIt ? "stood on it and did not go through"
        : !door.SquareIsWalkable ? "THE DOOR SQUARE IS NOT WALKABLE — an export fault, not a gate"
        : door.SomebodyIsInTheWay ? "somebody is standing in the way"
        : door.ArrivedOnAnIsland ? "ARRIVED ON AN ISLAND — it never walked this map at all"
        : "never reached the door";

    /// <summary>
    /// One variable's whole life in the run, in order.
    /// <para>
    /// <b>What the run held when somebody looked</b>, which is a different question from what
    /// it ended up holding and the only one a counter ever raises. The three balls in the lab
    /// hand something over at <c>0x4055 == 2</c> and say "you already have one" from three
    /// upwards; a run that ends with five in it may have been read at two and moved on, or may
    /// never have been two at the moment it mattered, and every instrument this project has
    /// prints the same five either way.
    /// </para>
    /// <para>
    /// It says what was in the variable and what it was held against, and nothing about which
    /// arm the script then took. That is the runner's business, and a trace that decided what
    /// a comparison meant would be a second reader quietly disagreeing with the first.
    /// </para>
    /// </summary>
    private static void WriteTheTrace(Attempt played, int? watch)
    {
        if (watch is not { } variable) return;

        Console.WriteLine();
        Console.WriteLine($"  EVERY LOOK AT AND CHANGE TO 0x{variable:X4}, IN ORDER");

        if (played.Trace.Count == 0)
        {
            Console.WriteLine(
                $"    nothing the run executed touched 0x{variable:X4} at all — which is a"
                + " different finding from it holding the wrong number, and the two have looked"
                + " identical until now");
            return;
        }

        // Reads and writes counted apart, because the interesting one has always been the one
        // nothing recorded.
        int reads = played.Trace.Count(t => !t.What.Wrote);

        Console.WriteLine(
            $"    {played.Trace.Count} touch(es): {played.Trace.Count - reads} write(s), {reads} read(s)"
            + (played.TraceDropped > 0
                ? $" — AND {played.TraceDropped} MORE DROPPED, the trace filled up"
                : ""));

        foreach (Traced touch in played.Trace) Console.WriteLine($"    {touch}");
    }

    /// <summary>
    /// The floor table: one run at each of the six lever settings, printed with the differences
    /// between them worked out from those same six rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Six runs, on purpose.</b> The block at the top of every session's prompt was stale in
    /// five of its six rows for thirteen milestones and every sentence written about it stayed
    /// true, because each milestone re-ran the pair it cared about and pasted the delta onto a
    /// base nobody re-ran. The only thing that catches that is running the whole block, so this
    /// runs the whole block.
    /// </para>
    /// <para>
    /// What the rows say and what the differences say both live in <see cref="TheFloorTable"/>,
    /// where a test can ask them without sixteen megabytes. This function is the plumbing: it
    /// reads the cartridge facts once, plays six times, and prints what it was handed.
    /// </para>
    /// </remarks>
    /// <summary>
    /// One or more addresses, decoded: the bytes and what they read as, side by side, plus every
    /// block each one reaches and where any read stopped.
    /// </summary>
    /// <remarks>
    /// The command this project has needed since 190 and kept doing by hand. See
    /// <see cref="ABlockRead"/> for why the bytes and the decode come off the same command.
    /// </remarks>
    /// <summary>
    /// What number <c>dofieldeffect</c> takes, against the move the same block asked about — and
    /// the sites that take one with no move anywhere near them.
    /// </summary>
    private static void WriteFieldEffects(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("WHAT DOFIELDEFFECT TAKES");
        Console.WriteLine();

        List<MoveData> moves = MoveExtractor.Extract(rom);

        MapLibrary library = MapLibrary.Open(rom);

        var opened = new HashSet<int>();

        foreach ((string _, string _, uint address) in library.EveryScript())
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
                opened.Add(command.Offset);

        IReadOnlyList<MoveSite> sites = EverywhereInTheImage.AsksWhoKnows(rom, moves.Count, [.. opened]);

        List<FieldEffectNumbers.Offer> offers =
        [
            .. sites.Where(s => s.Offers)
                .Select(s => new FieldEffectNumbers.Offer(s.Move, s.FieldEffect, s.Offset))
                .OrderBy(o => o.Effect),
        ];

        Console.WriteLine($"  {offers.Count} block(s) in the image pair a move with a number:");

        foreach (FieldEffectNumbers.Offer offer in offers)
        {
            string name = offer.Move < moves.Count ? moves[offer.Move].Name : "(past the table)";

            Console.WriteLine(
                $"    move {offer.Move,3} {name,-12} -> {offer.Effect,3}   at 0x{Rom.BaseAddress + (uint)offer.At:X8}");
        }

        FieldEffectNumbers.OneEach each = FieldEffectNumbers.PerMove(offers);

        Console.WriteLine();
        Console.WriteLine(
            $"  {each.Moves} move(s), {each.Effects} number(s), and "
            + (each.Holds
                ? "no move has two"
                : "SOME MOVE HAS TWO: " + string.Join(", ", each.WithTwoNumbers)));
        Console.WriteLine(
            $"    {each.Repeated} move(s) appear in more than one block and {each.RepeatedAgreeing} of those"
            + " got the same number every time — which is ALL the direct evidence there is that the"
            + " number follows the move, and it is one agreement, not six");

        // AND THE SITES WITH NO MOVE. The map scan opens seven of these commands; the ones above
        // account for some of them, and what is left is the interesting half.
        List<ScriptCommand> onMaps =
        [
            .. opened.Order()
                .Where(o => rom.ReadU8(o) == ScriptCommands.DoFieldEffect)
                .Select(o => new ScriptCommand(o, ScriptCommands.DoFieldEffect, rom.Slice(o + 1, 2).ToArray())),
        ];

        var driven = offers.Select(o => o.Effect).ToHashSet();

        List<ScriptCommand> others = [.. onMaps.Where(c => !driven.Contains(c.Word()))];

        Console.WriteLine();
        Console.WriteLine(
            $"  the map scan opens {onMaps.Count} of them; {onMaps.Count - others.Count} take a number a move"
            + $" drives and {others.Count} do not:");

        foreach (ScriptCommand command in others)
            Console.WriteLine($"    0x{Rom.BaseAddress + (uint)command.Offset:X8}  number {command.Word(),3}   no move is asked about in that block");

        FieldEffectNumbers.TheSplit split =
            FieldEffectNumbers.AreTheLowest(driven, others.Select(o => o.Word()));

        Console.WriteLine();
        Console.WriteLine(
            split.Cleanly
                ? $"  EVERY move-driven number is below every other one — {split.Taken} of {split.Of},"
                  + $" which chance would do one time in {split.OneIn:0}"
                : "  the two sets interleave, so the numbers are not two bands");

        // AND THE RAW SWEEP, which is here to be thrown away.
        (int sites, int readsOn, int words) real = FieldEffectNumbers.Sweep(rom);
        (int sites, int readsOn, int words) floor = FieldEffectNumbers.NoiseFloor(rom);

        Console.WriteLine();
        Console.WriteLine(
            $"  the raw whole-image sweep: {real.sites} site(s), {real.readsOn} reading to a proper end,"
            + $" {real.words} distinct number(s)");
        Console.WriteLine(
            $"    the same sweep REVERSED:  {floor.sites} site(s), {floor.readsOn} reading on,"
            + $" {floor.words} distinct number(s)");
        Console.WriteLine(
            floor.readsOn >= real.readsOn
                ? "    THE REVERSAL IS AHEAD. The raw sweep is not a finding and the only sites worth"
                  + " reading are the ones a map or a jump opens."
                : "    the real image is ahead, which is worth looking at rather than assuming");
    }

    private static void WriteBlocks(Rom rom, IReadOnlyList<uint> addresses)
    {
        Console.WriteLine();
        Console.WriteLine("READ FROM");
        Console.WriteLine();

        foreach (uint address in addresses)
        {
            if (!rom.IsRomAddress(address))
            {
                Console.WriteLine($"  0x{address:X8} is not an address in this image");
                continue;
            }

            IReadOnlyList<ABlockRead.Block> blocks = ABlockRead.From(rom, address);

            Console.WriteLine();
            Console.WriteLine(
                $"  0x{address:X8} — {blocks.Count} block(s), "
                + $"{blocks.Sum(b => b.Lines.Count)} command(s), "
                + $"{blocks.Count(b => b.Stopped)} of them stopped");

            foreach (ABlockRead.Block block in blocks)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"    0x{block.Address:X8}"
                    + (block.Address == address ? "   <- asked for" : "")
                    + (block.Reaches.Count > 0
                        ? "   hands over to " + string.Join(", ", block.Reaches.Select(r => $"0x{r:X8}"))
                        : ""));

                foreach (ABlockRead.Line line in block.Lines)
                {
                    Console.WriteLine(
                        $"      0x{line.Offset:X6}  "
                        + string.Join(" ", line.Bytes.Select(b => $"{b:X2}")).PadRight(26)
                        + line.Name);
                }

                if (block.StoppedOn is { } code)
                {
                    Console.WriteLine(
                        $"      0x{block.StoppedAt:X6}  {code:X2}"
                        + new string(' ', 24)
                        + "<- STOPPED: this project has no width for it");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "  the bytes and the decode come off the same command, so they cannot disagree — and"
            + " what a block hands over to is the four pointer forms only, never a fall-through");
    }

    private static void WriteTheFloorTable(Rom rom, string startAt)
    {
        Console.WriteLine();
        Console.WriteLine("THE FLOOR TABLE, RE-READ");
        Console.WriteLine();

        WorldData world = WorldExporter.Export(rom);
        GameRules rules = RulesExporter.Export(rom);
        MapData first = world.Find(startAt) ?? world.Maps.First();
        Dictionary<int, int> teaches = TeachingMachines(rom);

        // Read once. Which script addresses are doors into a scene rather than scenes is a fact
        // about the cartridge, not about a lever, and six identical readings of it would be six
        // identical readings of it.
        Dictionary<uint, uint> doorsTo = EntriesToAScene
            .In(rom, MapLibrary.Open(rom).All().SelectMany(EveryScriptOn), HowAScriptRuns.FirstRemembered)
            .GroupBy(d => d.Where.Address)
            .ToDictionary(g => g.Key, g => g.First().Leads);

        Console.WriteLine(
            $"  {TheFloorTable.Settings.Count} run(s) over {world.Maps.Count} maps, from"
            + $" {first.Id} ({first.Name}) — this is slow, and that is the price of a table"
            + " nobody has to keep up to date");
        Console.WriteLine();

        List<TheFloorTable.Row> rows = [];

        foreach (TheFloorTable.Setting at in TheFloorTable.Settings)
        {
            // A FRESH SAVE EACH TIME. Both of these are written into as a run goes, and a run
            // handed the previous run's beaten trainers and story memory is not the setting it
            // says it is — it is that setting continued from somewhere else.
            var beaten = new HashSet<int>();
            var remembered = new Dictionary<int, int>();

            var reader = new HowAScriptRuns(
                rom, teaches, null, null, at.SayYes, beaten, remembered);

            Attempt played = Autoplayer.Play(
                world, first.Id, rules, reader.Read, null, at.Boat, 0, beaten, at.Surf,
                remembered, at.InOrder, doorsTo);

            rows.Add(TheFloorTable.Read(at, played, world.Maps.Count));

            Console.WriteLine($"    ran {at.Command}");
        }

        Console.WriteLine();
        Console.WriteLine("  THE ROWS");
        Console.WriteLine();

        foreach (string line in TheFloorTable.Render(rows)) Console.WriteLine("    " + line);

        Console.WriteLine();
        Console.WriteLine("  and what each run did about the sea, which is READ and not a lever");

        foreach (TheFloorTable.Row row in rows)
            Console.WriteLine($"    {row.At.Command,-42} {row.Water}");

        Console.WriteLine();
        Console.WriteLine(
            "  AND THE DIFFERENCES, SUBTRACTED FROM THE ROWS ABOVE rather than remembered");
        Console.WriteLine(
            "    every pair of rows exactly ONE lever apart. A pair two levers apart also"
            + " produces a number and that number is not about either lever, so it is not here.");
        Console.WriteLine();

        foreach (TheFloorTable.Difference difference in TheFloorTable.Differences(rows))
            Console.WriteLine("    " + difference.Said);

        Console.WriteLine();
        Console.WriteLine(
            "  Paste the rows above into the prompt whole. Do not apply a delta to them — the"
            + " deltas are printed from the same six runs, so a table kept by hand from these"
            + " lines is exactly the drift this command exists to end.");
    }

    private static void WritePlaythrough(
        Rom rom, IReadOnlyDictionary<int, int> answers, string startAt, bool boat = false, int money = 0,
        bool sayYes = false, IReadOnlyDictionary<int, int>? variables = null, bool surf = false,
        bool inOrder = false, int? watch = null)
    {
        Console.WriteLine();
        Console.WriteLine("A PLAYTHROUGH");
        Console.WriteLine();

        WorldData world = WorldExporter.Export(rom);
        GameRules rules = RulesExporter.Export(rom);

        MapData first = world.Find(startAt) ?? world.Maps.First();

        Dictionary<int, int> teaches = TeachingMachines(rom);

        // Shared with the walk below: it fills this in as it wins, and the reader above reads
        // it before every script.
        var beatenTrainers = new HashSet<int>();

        // The story's own memory, shared between the reader that writes it and the walk that
        // decides which scripts fire on it.
        var remembered = new Dictionary<int, int>();

        Console.WriteLine(
            $"  {world.Maps.Count} maps, {rules.TrainerCount} trainers, {teaches.Count} machines; "
            + $"starting at {first.Id} ({first.Name})");
        Console.WriteLine(
            $"  a new game sets {world.FlagsAtStart.Count} flags before the first frame, and this "
            + "starts with them — a fresh save is not an empty save");

        if (answers.Count > 0)
        {
            Console.WriteLine(
                "  standing in for " + string.Join(
                    ", ", answers.Select(a => $"0x{a.Key:X3}={a.Value}")) + " (modelled)");
        }

        if (variables is { Count: > 0 })
        {
            Console.WriteLine(
                "  putting " + string.Join(
                    ", ", variables.Select(v => $"0x{v.Key:X4}={v.Value}"))
                + " in the story's own variables before every script (modelled — this is a ceiling)");
        }

        Console.WriteLine();

        // The reader, which used to be a hundred and forty lines of local function right here.
        // Running a script is deciding what a scene does given what the run holds; that is not
        // printing, and it does not belong in a file nothing can test. Two live faults were
        // sitting in it when it moved.
        var reader = new HowAScriptRuns(
            rom, teaches, answers, variables, sayYes, beatenTrainers, remembered, watch);

        // Which scripts are doors into a scene rather than scenes. Read here because this is
        // where the cartridge is; the walk is handed the answer.
        // Grouped, because one script address is attached to more than one thing: a person and
        // a trigger can share one, and nineteen Pokémon Centres share a nurse. They all read
        // the same bytes and so lead the same place, which is why taking the first is safe and
        // building the dictionary without grouping is not — it threw.
        Dictionary<uint, uint> doorsTo = EntriesToAScene
            .In(rom, MapLibrary.Open(rom).All().SelectMany(EveryScriptOn), HowAScriptRuns.FirstRemembered)
            .GroupBy(d => d.Where.Address)
            .ToDictionary(g => g.Key, g => g.First().Leads);

        Attempt played = Autoplayer.Play(
            world, first.Id, rules, reader.Read, Console.WriteLine, boat, money, beatenTrainers, surf,
            remembered, inOrder, doorsTo);

        int hanging = played.Questions.Values.Sum();

        // AND HOW MUCH OF THE ABOVE WAS THE SAME SCENE TWICE.
        //
        // 193 and 194 established that one scene is written as several entry stubs and that
        // this walk takes every door. The prediction was that every count below is inflated by
        // the door count. It is not: the walking was, because a walk accumulates, and a count
        // of how many times is not. Printed rather than removed, so it cannot quietly grow.
        Console.WriteLine(
            $"    the four counts below are PLACES and not times: the run asked"
            + $" {played.AskedSpecials} / {played.AskedUnread} / {played.AskedQuestions} /"
            + $" {played.AskedRefusals} times, and a fixpoint asks again on every pass."
            + $" {played.FoldedByDoor} of the folding was a scene arriving by another DOOR rather"
            + " than on another pass.");

        Console.WriteLine();
        Console.WriteLine(
            $"  it got to {played.Reached.Count} of {world.Maps.Count} maps in {played.Passes} pass(es), "
            + $"and stopped because {Why(played.Stopped)}");
        Console.WriteLine(
            $"    {played.Flags.Count} flags, {played.Moves.Count} field moves, "
            + $"{played.Party.Count} in the party, highest level {played.HighestLevel}");

        // AND WHICH FLAGS, WITH A DENOMINATOR — the same fix 209 made one line below this.
        //
        // "153 flags" cannot come back surprising: a run that set a hundred and fifty marks on
        // a character and a run that opened a hundred and fifty doors print the same line, and
        // diffing two runs means reading a number rather than a list. 207 needed the list to
        // find which three flags a milestone had added and had to hand-patch a print into two
        // worktrees to get it.
        //
        // The rule that decides which flags count is FlagGates', not this printer's — the
        // seventh time a line like it has moved out of here.
        var gates = new FlagGates(world);

        IReadOnlyList<int> missing = gates.NotIn(played.Flags);

        Console.WriteLine(
            $"      of those, {gates.HowManyOf(played.Flags)} gate something in this world file"
            + $" (of {gates.Count} that do), so {missing.Count} gating flag(s) it never set");

        Console.WriteLine(
            "      set: "
            + string.Join(", ", played.Flags.Order().Take(14).Select(f => $"0x{f:X4}"))
            + (played.Flags.Count > 14 ? $", +{played.Flags.Count - 14} more" : ""));

        Console.WriteLine(
            missing.Count == 0
                ? "      and it set EVERY flag that gates anything, which is the answer that"
                  + " would mean there is nothing left to reach"
                : "      never set: "
                  + string.Join(", ", missing.Take(14).Select(f => $"0x{f:X4}"))
                  + (missing.Count > 14 ? $", +{missing.Count - 14} more" : ""));

        // AND WHY EACH OF THEM IS SHUT, which the line above cannot say.
        //
        // "110 gating flags it never set" reads the same whether the run is one door short of
        // everything or a hundred and ten scripts short, and those are opposite findings. This
        // is the join --flags has never made: --flags takes only the ROM and has never seen an
        // Attempt, and the run knows what it set and nothing about what else could have.
        WriteWhyTheGatesAreShut(rom, world, gates, played.Flags);

        // WHY IT COULD CROSS WATER, WHICH WAS A COMMAND-LINE FLAG UNTIL NOW.
        //
        // The sea is 1245 squares across 35 maps and the walk has always been told whether to
        // swim. The cartridge decides it the other way round: the one block in the image that
        // offers to cross water asks who knows the move first — so this run asks the same
        // question of its own party, and --surf is what is left when the answer is no.
        Console.WriteLine(
            $"    crossing water: {(
                played.SurfMove == 0
                    ? "this cartridge has no move by that name, so there is none — READ"
                    : played.LearnedToCrossOnPass > 0
                        ? $"READ — the party knew move {played.SurfMove} from pass {played.LearnedToCrossOnPass}, so it swam"
                        : played.SwamAnyway
                            ? $"MODELLED — nobody ever knew move {played.SurfMove}; --surf swam anyway, and this is a ceiling"
                            : $"nobody ever knew move {played.SurfMove}, so every sea was a wall")}");
        Console.WriteLine(
            $"    {played.FightsWon} fights won, {played.FightsLost} lost to"
            + (played.FightAttemptsLost > played.FightsLost
                ? $" ({played.FightAttemptsLost} attempts — it goes back every pass)"
                : "")
            + $", {played.FightsSkipped} never fought at all"
            + $" (healed {played.PartiesHealed} times)");

        // WHAT THE STORY IS HOLDING AT THE END, WHICH IS THE OTHER HALF OF "FLAGS".
        //
        // A run prints how many flags it set and has never printed a single one of the numbers.
        // They are the same kind of fact — PALLET TOWN's whole opening is a counter — and the
        // difference between "the run never reached the scene" and "the run reached it and the
        // counter was on the wrong number" is invisible without this.
        if (remembered.Count > 0)
        {
            Console.WriteLine(
                $"    the story's memory: "
                + string.Join(", ", remembered.OrderBy(v => v.Key).Take(12)
                    .Select(v => $"0x{v.Key:X4}={v.Value}"))
                + (remembered.Count > 12 ? $", +{remembered.Count - 12} more" : ""));
        }
        else
        {
            Console.WriteLine("    the story's memory: empty — nothing it ran left a number behind");
        }

        WriteTheTrace(played, watch);

        // THE PARTY, ONE LINE PER CREATURE.
        //
        // "highest level 25" is a summary, and a summary is where a fact goes to hide. It read
        // the same on every pass of a run that won ninety-four fights, which is either a party
        // that does not grow or a maximum pinned by a gift — and one of those is a fault in
        // this project and the other is not. One line each says which without anybody guessing.
        Console.WriteLine(
            "    the party: "
            + (played.Party.Count == 0
                ? "empty"
                : string.Join(", ", played.Party.Select(m => $"#{m.Species} at {m.Level}"))));

        // AND WHETHER IT TOOK THE SAME GIFT TWICE, WHICH MAKES THIS A CEILING.
        //
        // Every pass runs every script again. A gift the cartridge hands over once is handed
        // over once per pass here unless something stops it, and five copies of the same
        // creature is a party no player could assemble. This run's numbers are a floor in every
        // other respect and this one line is where they stop being one — so it is printed
        // rather than left to be noticed in the list above.
        int copies = played.Party.Count - played.Party.Select(m => m.Species).Distinct().Count();

        if (copies > 0)
        {
            Console.WriteLine(
                $"    {copies} of those are a second copy of something already in it — a gift taken");
            Console.WriteLine(
                "    again on a later pass. THIS RUN IS NOT A FLOOR IN THAT RESPECT: no player");
            Console.WriteLine(
                "    assembles this party, and whatever it wins with it is a ceiling.");
        }

        // AND THE SAME QUESTION ABOUT EVERYTHING ELSE THAT CHANGES HANDS.
        //
        // The party has said this for a while and the bag never has. An item off the floor is
        // kept from refilling by the flag on the object's own record; an item somebody hands
        // over is kept from refilling by a guard inside their script, and until the fight's
        // two exits were told apart the run jumped over eight of those guards — one per gym,
        // once per pass, for ever. Printed with its denominator, because "none of them twice"
        // and "nothing hands anything over" are different findings.
        IReadOnlyList<HandedOver> twice = played.HandedOverTwice;

        Console.WriteLine(
            $"    {played.Handovers.Count} place(s) handed something over; {twice.Count} of them"
            + " did it on more than one pass");

        foreach (HandedOver again in twice.Take(8)) Console.WriteLine($"      {again}");

        if (twice.Count > 8) Console.WriteLine($"      ... and {twice.Count - 8} more");

        if (played.FightsSkipped > 0)
        {
            Console.WriteLine(
                "    a fight never had is a trainer whose party this build could not assemble,");
            Console.WriteLine(
                "    or one reached before anything had been handed over to fight with");
        }

        // The bag, which is new and is the whole of this milestone. Both halves are worth
        // printing: what it managed to pick up says whether collecting works at all, and
        // what it was asked for and did not have is the shopping list for whatever is
        // still shut.
        // Names, off the cartridge, purely so the shopping list below reads as something
        // rather than as a column of numbers. The rules file carries no names by design,
        // so this is the only side of the project that can do it.
        Dictionary<int, string> itemNames = ItemTable.Locate(rom) is { } itemsAt
            ? ItemTable.Read(rom, itemsAt).ToDictionary(i => i.Id, i => i.Name)
            : [];

        string NameOf(int itemId) =>
            itemNames.GetValueOrDefault(itemId) is { Length: > 0 } name
                ? $"{name} (0x{itemId:X3})"
                : $"item 0x{itemId:X3}";

        Console.WriteLine();
        Console.WriteLine(
            $"  it ended up carrying {played.Carried.Count} different things, "
            + $"{played.Carried.Sum(e => e.Count)} in total");

        foreach (BagEntry entry in played.Carried.OrderByDescending(e => e.Count).Take(12))
            Console.WriteLine($"    {entry.Count,3} x {NameOf(entry.ItemId)}");

        if (played.Carried.Count > 12)
            Console.WriteLine($"    ... and {played.Carried.Count - 12} more");

        // ALWAYS, and this is the whole point of it.
        //
        // This section used to sit behind `money > 0 || played.Bought.Count > 0`, so the
        // default run — which is every run nobody passed --money to — printed nothing here at
        // all. Not "it bought nothing": nothing. And a report that says nothing is
        // indistinguishable from a run with nothing to say, which is the trap this project has
        // written down four times and has been walking past in its own output ever since there
        // was a bag to fill.
        //
        // What it was hiding: four of the six things on the shopping list are asked for on
        // ground where that thing is SOLD, and the run stood at the counter and could not
        // afford them. That is a different job from reaching the map, and nothing said so.
        Console.WriteLine();
        Console.WriteLine(
            money > 0
                ? $"  it was handed {money} to spend (MODELLED — nothing in this game gives it"
                    + $" any) and spent {money - played.MoneyLeft} of it"
                : "  it was handed NOTHING to spend, which is the default and is why it buys"
                    + " nothing. `--money N` is the lever and it is MODELLED — the payout table"
                    + " has never been located, so there is no read number to hand it.");

        Console.WriteLine(
            $"  {played.CountersOnReachedGround} shop counter(s) stand on ground it reached;"
            + $" it stood in front of {played.CountersStoodAt} of them"
            + $" — bought {played.Bought.Count}, could not buy {played.CouldNotBuy.Count}");

        // And which of the two ways the gap between those numbers is made. A thing "sold on
        // ground it reached" is sold on a MAP it reached; being beside the person selling it
        // is a second thing, and until this line the two were one word. Four entries on the
        // shopping list read as a money problem and one of them is this instead.
        if (played.CountersOnReachedGround > played.CountersStoodAt)
        {
            Console.WriteLine(
                $"    the other {played.CountersOnReachedGround - played.CountersStoodAt}:"
                + $" {played.CountersHiddenByAFlag} hidden behind a flag on their own record,"
                + $" {played.CountersNeverStoodBeside} it walked the map and never stood beside"
                + " — a WALK finding, not a money one");

            // And which kind of walk finding, which is the only part that is actionable.
            // A clerk stands BEHIND a counter in this game and the player talks across it, so
            // a distance of two is not a room it failed to enter — it is one tile of reach
            // this walk does not have. Sorted nearest first, so the top of this list is the
            // cheapest thing in it.
            //
            // The split first, because a list of eight out of nineteen cannot say whether the
            // ninth is the same shape, and a filter that keeps output readable must never
            // decide which question gets asked.
            int acrossACounter = played.CountersOutOfReach.Count(c => c.NearestStood == 2);
            int neverOnTheMap = played.CountersOutOfReach.Count(c => c.NearestStood < 0);

            // THE SAME FACT READ THE OTHER WAY, AND IT KILLED THE OBVIOUS EXPLANATION.
            //
            // The guess was that a clerk is walled in — that no square beside them can be
            // stood on, so talking across the counter is the only way the shop works and
            // adjacency is the wrong rule rather than a missing one. Measured off the map's
            // own collision, EVERY clerk has two or three walkable squares beside them, and
            // the run stood on none of them.
            //
            // Walkable is not reachable. Those squares are behind the counter, on the clerk's
            // side of it, and nothing joins them to the shop floor. So the conclusion survives
            // and the proof of it does not: the number that says so is the distance, which
            // follows the walk, and not the collision byte, which follows one edge and answers
            // a different question. Two readings disagreeing, and the one that looks stricter
            // is the one that is wrong about the question.
            int walledIn = played.CountersOutOfReach.Count(c => c.SquaresBesideThatAreWalkable == 0);

            Console.WriteLine(
                $"      of those {played.CountersOutOfReach.Count}: {acrossACounter} are exactly"
                + " 2 away — ACROSS A COUNTER, which is how this game sells things and is not a"
                + $" reach problem at all; {neverOnTheMap} stood on no square of that map;"
                + $" {played.CountersOutOfReach.Count - acrossACounter - neverOnTheMap} are"
                + " some other distance");

            Console.WriteLine(
                $"      and only {walledIn} of them are WALLED IN — the rest have 2 or 3 walkable"
                + " squares beside them that this run never stood on. Walkable is not reachable:"
                + " those squares are the clerk's side of the counter and nothing joins them to"
                + " the shop floor. The distance is the reading here; the collision byte answers"
                + " a different question and says the opposite.");

            foreach (CounterOutOfReach far in played.CountersOutOfReach.Take(8))
            {
                Console.WriteLine(
                    $"      {far.MapId} object {far.LocalId} at {far.Square}: "
                    + (far.NearestStood < 0
                        ? "it stood on NO square of this map — reached by a door it never took"
                        : $"nearest square it stood on is {far.NearestStood} away, and"
                            + $" {far.SquaresBesideThatAreWalkable} of the 4 squares beside them"
                            + " can be stood on at all"));
            }

            if (played.CountersOutOfReach.Count > 8)
                Console.WriteLine($"      ... and {played.CountersOutOfReach.Count - 8} more");
        }

        // THE THIRD CEILING, AND THE ONLY ONE WITHOUT A LEVER.
        //
        // --say-yes and --boat are named, printed and switchable. This one arrived at 200 by
        // reading two command widths CORRECTLY: the reader now steps cleanly over the command
        // that asks about money, so the run takes the arm where the thing is handed over —
        // every time, with a purse of nought. The first thing that fell out of it was a fifth
        // party member.
        //
        // Both halves, because they are different claims. The count is how WIDE the gap is;
        // the list under it is what the gap is currently WORTH, and only the list says the
        // party number is above the floor. Either can be nought and they mean different things.
        Console.WriteLine(
            $"  {played.WalkedPastAMoneyCheck} place(s) asked it for money and it answered"
            + " neither way — a CEILING, and the only one with no lever");

        // AND WHICH PLACES, which this printed a count of and never a list.
        //
        // "8 places ask the run for money" reads the same whether those eight are eight
        // shopkeepers or one shopkeeper and seven counters nobody has looked at. 208 read a
        // coin counter off the cartridge and could not say whether the run stands in front of
        // it, because this line was a number with no list — the same shape as the flags that
        // look moved and are not.
        Dictionary<string, MapData> named = world.Maps.ToDictionary(m => m.Id);

        foreach (AskedForMoney asked in played.MoneyChecks.Take(12))
        {
            Console.WriteLine(
                $"    {asked.MapId,-8} {(named.TryGetValue(asked.MapId, out MapData? on) ? on.Name : ""),-16}"
                + $" 0x{asked.Address:X8}  wants "
                + string.Join(" or ", asked.Prices));
        }

        if (played.MoneyChecks.Count > 12)
            Console.WriteLine($"    ... and {played.MoneyChecks.Count - 12} more");

        if (played.TookSomethingAnyway.Count == 0)
        {
            Console.WriteLine(
                "    and nothing changed hands on the far side of one, so nothing it is"
                + " carrying is unpaid for");
        }

        foreach (PaidForNothing free in played.TookSomethingAnyway)
        {
            Console.WriteLine(
                $"    {free.MapId} 0x{free.Address:X8} wanted {free.Price} and handed over"
                + $" {free.What} ANYWAY — this is above the floor");
        }

        foreach (Bought buy in played.Bought)
            Console.WriteLine($"    bought {NameOf(buy.ItemId)} for {buy.Price} at {buy.MapId}");

        // Why not, when it did not. "It bought nothing" has four causes and they are not
        // remotely alike; the first run of this hit the one nobody would have guessed.
        foreach (NotBought missed in played.CouldNotBuy.Take(12))
            Console.WriteLine($"    did NOT buy {NameOf(missed.ItemId)} at {missed.MapId}: {missed.Why}");

        if (played.CouldNotBuy.Count > 12)
            Console.WriteLine($"    ... and {played.CouldNotBuy.Count - 12} more it could not buy");

        // The two silences, told apart. Neither of these is "it bought nothing" — that is the
        // headline above and it is now always printed.
        if (played.CountersStoodAt == 0)
        {
            Console.WriteLine(
                "    it never stood in front of a counter selling anything it had been refused —"
                + " so this is a REACH finding, not a money one");
        }
        else if (played.Bought.Count == 0 && played.CouldNotBuy.Count == 0)
        {
            Console.WriteLine(
                "    it stood at counters and neither bought nor was stopped, which should be"
                + " impossible — every stock line takes one of the two arms");
        }

        Console.WriteLine();

        if (played.Refused.Count == 0)
        {
            Console.WriteLine(
                "  nothing asked it for anything it did not have — either the bag covers every");
            Console.WriteLine(
                "  check in the reachable world, or nothing reachable checks (which is a finding)");
        }
        else
        {
            Console.WriteLine(
                $"  {played.Refused.Count} places asked for something it was not carrying — "
                + "this is the shopping list");

            foreach (Wanted want in played.Refused.Take(20))
            {
                Console.WriteLine(
                    $"    {want.MapId,-8} wants {want.Count} x {NameOf(want.ItemId)}"
                    + (want.Times > 1 ? $"  (asked {want.Times} times)" : ""));

                // And where one comes from, which is the half that says what to build. An
                // empty list is the sharper answer: nothing on any map in the game hands
                // one over, so whatever produces it is behind a routine and no amount of
                // walking will ever reach it.
                if (want.Sources.Count == 0)
                {
                    Console.WriteLine(
                        "             NOTHING ON ANY MAP HANDS ONE OVER — it comes from a routine");

                    continue;
                }

                foreach (IGrouping<string, FoundAt> how in want.Sources.GroupBy(s => s.How))
                {
                    // A reached one for the example where there is one, because "it is on
                    // a map you have been to" and "it is behind everything else that is
                    // shut" are the two answers and the example has to say which.
                    FoundAt one = how.FirstOrDefault(s => s.Reached) ?? how.First();

                    Console.WriteLine(
                        $"             {how.Key} at {how.Count()} place(s), "
                        + $"{how.Count(s => s.Reached)} of them on ground it reached"
                        + $" — e.g. {one.MapId} (object {one.LocalId})");

                    if (one.Reached) continue;

                    // And the way in, which is the door to go and open. The first hop is
                    // the only one that matters — everything after it is behind that.
                    if (one.WayIn.Count >= 2)
                    {
                        Console.WriteLine(
                            "               the way in: "
                            + string.Join(
                                " -> ",
                                one.WayIn.Select(h => h.How == Hop.Start ? h.MapId : $"{h.MapId} by {h.How}"))
                            + $"   (the shut step is {one.WayIn[0].MapId} -> {one.WayIn[1].MapId})");

                        continue;
                    }

                    // Three answers, and the first version of this printed the wrong one of
                    // them for the map that matters. "Nothing leads here" and "everything
                    // that leads here is itself unreached" are not the same finding, and the
                    // second one is a signpost to somewhere else entirely.
                    Console.WriteLine(one.Behind switch
                    {
                        [] =>
                            $"               every way in to {one.MapId} is unreached, and they lead"
                            + " only to each other — a closed ring",

                        [string only] when only == one.MapId =>
                            $"               NOTHING ANYWHERE LEADS TO {one.MapId} — no door, no map"
                            + " edge, no scripted door, no boat",

                        var ends =>
                            $"               every way in to {one.MapId} is itself unreached; it"
                            + $" bottoms out at {string.Join(", ", ends)},"
                            + " which nothing anywhere leads into",
                    });
                }
            }

            if (played.Refused.Count > 20)
                Console.WriteLine($"    ... and {played.Refused.Count - 20} more");
        }

        // The yes-or-nos, which nothing in this project had ever answered or counted. A script
        // that stops at one has not declined the offer — declining is a branch and would at
        // least run — it has stopped mid-sentence, and from the outside that is identical to
        // somebody having nothing more to say.
        Console.WriteLine();

        if (hanging == 0)
        {
            Console.WriteLine(sayYes
                ? "  no reachable script was left hanging at a yes-or-no"
                : "  no reachable script stopped at a yes-or-no");
        }
        else
        {
            Console.WriteLine(
                $"  {hanging} reachable script(s) across {played.Questions.Count} map(s) stopped at a"
                + " yes-or-no and were never answered");

            // AND THIS IS NOT ONLY LOST GROUND. IT IS THE SAME GROUND TAKEN AGAIN.
            //
            // A script that stops at a question never reaches its own setflag, so the next pass
            // finds the flag clear and runs the whole thing from the top. SILPH CO.'s LAPRAS is
            // handed over, the run is asked whether to name it, and the `setflag` that would
            // stop it happening twice sits on the far side of that question. Five LAPRAS.
            //
            // So a hanging question is a floor in one direction and a CEILING in the other, and
            // this line was missing for as long as the number has been printed. The roadmap has
            // carried "--say-yes costs party members: 6 on the floor, 2 with it on" for
            // milestones, as though answering were the expensive choice. It is backwards: six
            // was the fault and two is the game.
            Console.WriteLine(
                "    every one of those runs again from the top on the next pass, because the");
            Console.WriteLine(
                "    flag that would stop it is past the question — so whatever they hand over is");
            Console.WriteLine(
                "    handed over once per pass. THAT IS A CEILING INSIDE THIS FLOOR.");

            foreach ((string mapId, int times) in played.Questions.OrderByDescending(q => q.Value).Take(10))
                Console.WriteLine($"    {mapId,-8} {times} time(s)");

            if (played.Questions.Count > 10)
                Console.WriteLine($"    ... and {played.Questions.Count - 10} more maps");

            if (!sayYes)
            {
                Console.WriteLine(
                    "    try --say-yes: everything past one of these is unreached in a way that looks");
                Console.WriteLine(
                    "    exactly like a person with nothing more to say");
            }
        }

        // The boat, said out loud either way. "It never got to the islands" and "it was
        // holding a ticket the whole time and nobody asked" are the same output otherwise.
        Console.WriteLine();

        Console.WriteLine(played.RodeTheBoat
            ? "  the boat: RIDDEN — every dock joined to every other, asking for nothing"
            : "  the boat: not taken (this run is a floor). Try --boat and see what opens");

        if (played.RodeTheBoat)
        {
            Console.WriteLine(
                "    both halves are MODELLED: where the boat goes is inside the routine that draws");
            Console.WriteLine(
                "    the menu, and so is what it asks for. This reach is a ceiling, not a floor.");
        }

        // What the boat actually asks for, named. Without this the output can say the run
        // held no ticket and cannot say what a ticket is, which is the shortest possible
        // distance between an answer and being no further forward.
        foreach (FerryTicket ticket in played.Tickets)
        {
            // Reported, never enforced. See FerryTicket: these are worth a destination
            // rather than the boat, and this run found out the hard way.
            Console.WriteLine(
                $"    a pass in the scripts: flag 0x{ticket.Flag:X4} or {NameOf(ticket.ItemId)} — "
                + (ticket.Opens
                    ? ticket.FlagSet ? "has the flag" : "has one"
                    : "has neither"));

            if (ticket.Opens) continue;

            if (ticket.Sources.Count == 0)
            {
                Console.WriteLine(
                    "      NOTHING ON ANY MAP HANDS ONE OVER — it comes from a routine");

                continue;
            }

            foreach (IGrouping<string, FoundAt> how in ticket.Sources.GroupBy(s => s.How))
            {
                FoundAt one = how.FirstOrDefault(s => s.Reached) ?? how.First();

                Console.WriteLine(
                    $"      {how.Key} at {how.Count()} place(s), "
                    + $"{how.Count(s => s.Reached)} of them on ground it reached"
                    + $" — e.g. {one.MapId} (object {one.LocalId})");

                if (one.Reached) continue;

                Console.WriteLine(one.WayIn.Count < 2
                    ? $"        no way in to {one.MapId} from anywhere it reached"
                    : "        the way in: "
                      + string.Join(
                          " -> ",
                          one.WayIn.Select(h => h.How == Hop.Start ? h.MapId : $"{h.MapId} by {h.How}")));
            }
        }

        if (played.Removed.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  {played.Removed.Count} people were taken off a map by a script it ran — a person "
                + "removed is a person not in a doorway");
        }

        if (played.Moved.Count > 0)
        {
            Console.WriteLine(
                $"  {played.Moved.Count} people were walked out of where they stood by a script it ran"
                + " — the other way a doorway opens");

            Console.WriteLine(
                "    one square at a time, stopping at a wall — a scene's steps applied as one"
                + " jump put 364 of 426 of these off the edge of the map");
            Console.WriteLine(
                $"    {played.WalkSites} applymovement command(s) ON A MAP, asked for"
                + $" {played.WalksAsked} time(s) — a scene is commonly several entry stubs into one"
                + " block, and every entry runs the same commands. Each applies once per map,"
                + " because nineteen Centres share one nurse and that is nineteen scenes.");
        }

        // WHAT RAN, AS PLACES AND AS BLOCKS.
        //
        // Two numbers, for the reason 195 gave: a count with no denominator cannot come back
        // empty. "N scripts ran" and "N places ran a script" read identically and only one of
        // them is about this cartridge's shape. The second line is the size of the fault this
        // milestone fixed, measured rather than argued — if no shared block ever ran on more
        // than one map, it says nought and the key never mattered.
        int onSeveralMaps = played.Ran.Keys.GroupBy(k => k.Address).Count(g => g.Count() > 1);

        Console.WriteLine();
        Console.WriteLine(
            $"  {played.Ran.Count} (map, script) place(s) ran, which is {played.RanAnywhere.Count}"
            + " distinct block(s) — one nurse's script hangs off nineteen Pokémon Centres, and"
            + " running it in one town is not running it in the other eighteen");
        Console.WriteLine(
            onSeveralMaps == 0
                ? "    and none of them ran on more than one map, so the map in the key cost nothing here"
                : $"    {onSeveralMaps} block(s) ran on more than one map — each was ONE entry"
                    + " until now, carrying one merged reason it stopped for all of them");

        // AND WHETHER ANYBODY IN THIS WORLD IS STANDING SOMEWHERE THAT IS NOT ON IT.
        //
        // Asked of every person the cartridge places, not only the ones a scene walked. Once
        // the walk stops at a wall nothing the run does can put somebody off the map, and a
        // check nothing can fail is not a check — this half is about the export, and
        // "somebody is standing in the way" is computed against exactly these squares.
        Console.WriteLine(
            played.OffTheMap.Count == 0
                ? "  nobody in this world stands on a square that is not on their own map"
                : $"  {played.OffTheMap.Count} PEOPLE STAND ON A SQUARE THAT IS NOT ON THEIR OWN MAP");

        foreach (WalkedOffTheMap lost in played.OffTheMap.Take(6))
            Console.WriteLine($"    {lost}");

        if (played.OffTheMap.Count > 6)
            Console.WriteLine($"    ... and {played.OffTheMap.Count - 6} more");

        // A fact about the world file rather than about the run, printed here because this is
        // where it turned up. The mirror of "19 warps lead to maps that are not here", asked
        // from the other end: a warp pointing at nothing may be an unused room, but a room
        // nothing points at cannot be entered by anybody.
        Console.WriteLine();

        if (played.NoWayIn.Count == 0)
        {
            Console.WriteLine("  every map in this world has something leading into it");
        }
        else
        {
            Console.WriteLine(
                $"  {played.NoWayIn.Count} of {world.Maps.Count} maps have NO way in at all — no door,"
                + " no map edge, no scripted door");
            Console.WriteLine(
                "    either a hole in the export or a doorway the cartridge makes some way this has"
                + " never read");

            foreach (string adrift in played.NoWayIn.Take(20))
                Console.WriteLine($"    {adrift,-8} {world.Find(adrift)?.Name ?? ""}");

            if (played.NoWayIn.Count > 20)
                Console.WriteLine($"    ... and {played.NoWayIn.Count - 20} more");
        }

        // What actually stopped it, which is a much shorter list than what it never reached.
        Console.WriteLine();
        Console.WriteLine(
            $"  {played.ShutDoors.Count} doors lead out of somewhere it reached into somewhere it did not");

        // The sentinel warps first, out of the way: a script fills those in when it uses
        // them, so an unopened one is a feature rather than a wall.
        List<ShutDoor> real = [.. played.ShutDoors.Where(d => !d.IsDynamic)];

        int dynamic = played.ShutDoors.Count - real.Count;

        if (dynamic > 0)
        {
            Console.WriteLine(
                $"    ({dynamic} of them are 127.127 sentinels, filled in by a script when used — not walls)");
        }

        // WHY, COUNTED, BEFORE THE LIST.
        //
        // The list below stops at twenty and this number is sixty-four, so for as long as it
        // has existed the shape of the frontier has been whatever the first twenty happened to
        // be. They are not one kind of problem: a door somebody is standing in front of is a
        // flag to find, a door never reached is a walk that stopped short, and an island the
        // run landed on and could not cross is this project's own collision export. Three
        // different jobs, and reading twenty lines was the only way to tell how much of each.
        Console.WriteLine();

        foreach ((string why, int count) in real
                     .GroupBy(WhyShut)
                     .Select(g => (g.Key, g.Count()))
                     .OrderByDescending(p => p.Item2))
        {
            Console.WriteLine($"    {count,4}  {why}");
        }

        Console.WriteLine();

        // Grouped by where they lead and why, rather than one line per door.
        //
        // Ungrouped this list was useless and had been for a while: seventeen Pokémon Centres
        // each have a door into the same link room, so thirty-four lines of a thirty-line
        // budget said one thing that was ruled out milestones ago, and the doors the whole
        // session was actually about were pushed off the end where nobody could see them.
        // Thirty *destinations* is a different instrument from thirty doors.
        var byTarget = real
            .GroupBy(d => (d.ToMapId, Why: WhyShut(d)))
            .OrderByDescending(g => g.Any(d => d.CouldStandOnIt))
            .ThenByDescending(g => g.Count())
            .ThenBy(g => g.Key.ToMapId)
            .ToList();

        // Who turns each flag on, across every script on every map. Read once and only if
        // something asks — it is the same whole-world scan `--flags` does, and most runs of
        // this report have nobody standing in a doorway with a flag behind them.
        var setters = new Lazy<IReadOnlyDictionary<int, IReadOnlyList<SetsAFlag>>>(
            () => WhatItIsWaitingFor.SetBy(
                rom,
                MapLibrary.Open(rom).All().SelectMany(EveryScriptOn)));

        // And who writes each variable, read the same way and only if something asks.
        var writers = new Lazy<IReadOnlyDictionary<int, IReadOnlyList<WritesAVariable>>>(
            () => WhatItIsWaitingFor.WritesTo(
                rom,
                MapLibrary.Open(rom).All().SelectMany(EveryScriptOn)));

        // Which of the four answers the wall list actually gave, tallied so the fourth one has
        // a denominator. "A block reached from more than one map is a different scene" is a
        // claim about this cartridge; whether any of those blocks is ever named as the setter
        // of a flag holding a door shut is a claim about this REPORT, and the two read exactly
        // alike until both are printed. If the fourth answer is nought, the fix moved nothing
        // here and that is the finding rather than an absence of one.
        var standings = new Dictionary<WhereItStands, int>();

        foreach (IGrouping<(string ToMapId, string Why), ShutDoor> shut in byTarget.Take(30))
        {
            Console.WriteLine(
                $"    -> {shut.Key.ToMapId,-8} {shut.First().ToName,-16} {shut.Key.Why}");
            Console.WriteLine(
                $"       from {shut.Count()} door(s): "
                + string.Join(", ", shut.Take(3).Select(d => $"{d.FromMapId} {d.Square}"))
                + (shut.Count() > 3 ? $", and {shut.Count() - 3} more" : ""));

            // And who, when somebody is. "Somebody is standing in the way" was true for eight
            // measurements running and named nobody; which person and what talking to them
            // came to are the two things that make it a job rather than an observation.
            //
            // Which map they are on comes with them now. It was dropped here — the doors are
            // grouped by where they *lead*, and one group can gather doors out of several
            // maps — and without it the person's own script cannot be found again, which is
            // the one thing left to ask about the four who do nothing.
            foreach ((string fromMapId, Blocker who) in shut
                         .SelectMany(d => d.Who.Select(w => (d.FromMapId, Who: w)))
                         .DistinctBy(p => (p.FromMapId, p.Who.LocalId))
                         .Take(4))
            {
                string[] did =
                [
                    .. new[]
                    {
                        who.AskedFor.Count > 0
                            ? "asks for " + string.Join(" or ", who.AskedFor.Select(NameOf))
                            : null,
                        who.Walked ? "walks somebody" : null,
                        who.Hid ? "hides somebody" : null,
                        who.FlagsSet > 0 ? $"sets {who.FlagsSet} flag(s)" : null,

                        // The number to hand to --answer. "Talking to him does nothing" and
                        // "talking to him asks the game something this cannot ask" are a
                        // person with no part in the story and a wall with a number on it.
                        who.Routines.Count > 0
                            ? "asks routine(s) " + string.Join(", ", who.Routines.Select(r => $"0x{r:X3}"))
                            : null,
                    }.OfType<string>(),
                ];

                Console.WriteLine(
                    $"       object {who.LocalId} at {who.Square}, movement {who.MovementType}: "
                    + (!who.Talked
                        ? "NEVER TALKED TO — it could not stand beside them"
                        : did.Length == 0
                            ? "talked to, and nothing it did opens anything"
                            : "talked to — " + string.Join(", ", did)));

                // And, for the ones that did nothing, the arm the run could not take.
                //
                // A run reports what it ran. A script whose whole part in the story is behind
                // a flag it has not got reports as a person with nothing to say — which is
                // the same output as a person with nothing to say, and four of those are
                // standing in the last four doorways. Reading both arms is the one thing
                // `ReadAll` has always been able to do and nothing has ever asked it for.
                if (did.Length == 0 && who.Talked)
                    WriteWhatItIsWaitingFor(rom, world, fromMapId, who, played, setters, writers, standings);
            }
        }

        if (byTarget.Count > 30) Console.WriteLine($"    ... and {byTarget.Count - 30} more destinations");

        if (played.ShutDoors.Count > 30)
            Console.WriteLine($"    ... and {played.ShutDoors.Count - 30} more");

        // What the wall list above actually said, by kind, with its own total beside it.
        //
        // The fourth kind is the one this milestone added, and it is here so that "the run
        // ran that block in another town" cannot be quietly nought and read as "the fix
        // mattered". Printed whenever anything was asked at all — an empty tally means the
        // wall list asked nothing, which is a different fact again from asking and finding
        // none, and the two look identical from outside.
        if (standings.Count > 0)
        {
            int asked = standings.Values.Sum();

            Console.WriteLine();
            Console.WriteLine($"    of {asked} setter(s) the list above asked about:");

            foreach (WhereItStands stands in Enum.GetValues<WhereItStands>())
            {
                Console.WriteLine(
                    $"      {standings.GetValueOrDefault(stands),4}  {stands}"
                    + (stands == WhereItStands.ItRanTheSameBlockOnAnotherMap
                        && standings.GetValueOrDefault(stands) == 0
                        ? "   <- nought, so no verdict here moved: the shared blocks this run"
                            + " reached from two maps are not the ones holding a door shut"
                        : string.Empty));
            }
        }

        int standable = played.ShutDoors.Count(d => d.CouldStandOnIt);

        Console.WriteLine();
        Console.WriteLine(
            $"    {standable} of those it could stand on — a door it stood on and did not go");
        Console.WriteLine(
            "    through is a warp that leads nowhere this build exported, or one it did not follow.");
        Console.WriteLine(
            $"    The other {played.ShutDoors.Count - standable} are behind something on this side.");

        if (played.Blocked.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  and the frontier: squares wanting a move nothing in the party has");

            foreach (IGrouping<int, Frontier> wanting in played.Blocked
                         .GroupBy(b => b.ShiftedBy)
                         .OrderByDescending(g => g.Count()))
            {
                Console.WriteLine(
                    $"    move {wanting.Key,3}: {wanting.Count(),4} squares — "
                    + string.Join(", ", wanting.Take(3).Select(b => $"{b.MapId} {b.Square}")));
            }
        }

        // AND THE SEA, WHICH THIS WALK HAS NEVER MENTIONED.
        //
        // A frontier of squares wanting a move reads as the whole of what is in the way. It is
        // not: this walk has no notion of water at all, so every water square is dropped as
        // solid alongside every wall, and "there is nothing there" and "there is a sea there
        // and this cannot swim" have been the same silence for as long as the walk has existed.
        //
        // Not crossed — counted. Which move crosses water is something to READ off the
        // cartridge, and a walk that started swimming on a guess would open half the Sevii
        // islands and be unable to say why.
        if (played.Shore.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  and the shore: {played.Shore.Values.Sum()} water square(s) it turned back from, "
                + $"across {played.Shore.Count} map(s)");

            foreach ((string mapId, int squares) in played.Shore.OrderByDescending(w => w.Value).Take(8))
                Console.WriteLine($"    {mapId,-8} {squares,5} — {world.Find(mapId)?.Name ?? ""}");

            Console.WriteLine();
            Console.WriteLine(
                "    this walk cannot swim, and every one of those was dropped as though it were a");
            Console.WriteLine(
                "    wall. Which move crosses water is the next thing to read.");
        }


        Console.WriteLine();
        Console.WriteLine($"  {played.Unreached.Count} maps it never got to");

        foreach (string mapId in played.Unreached.Take(40))
            Console.WriteLine($"    {mapId,-8} {world.Find(mapId)?.Name ?? string.Empty}");

        if (played.Unreached.Count > 40)
            Console.WriteLine($"    ... and {played.Unreached.Count - 40} more");

        Console.WriteLine();
        Console.WriteLine(
            $"  {played.Specials.Values.Sum()} place(s) call {played.Specials.Count} routines it could "
            + "not answer — and the zero it answered instead is not one thing");

        // AND WHAT THE ZERO AMOUNTED TO, which "every one took the zero arm" cannot say.
        //
        // A routine whose answer is only ever compared against 2 does the same thing for nought
        // as for 3, 4 or 9 — the silence costs nothing a wrong answer would not. A routine
        // compared against nought takes its branch BECAUSE the run said nothing. Those are
        // opposite findings and this line has been printing them as one number.
        //
        // --routines knows the shape and has never seen a run; the run knows what it asked and
        // nothing about the shape. The join lives in SpecialCalls, not here.
        IReadOnlyList<SpecialCalls.WhatZeroDid> silence = SpecialCalls.ZeroAt(
            SpecialCalls.Profiles(SpecialCalls.All(rom, MapLibrary.Open(rom))), played.Specials);

        Console.WriteLine(
            "    ranked by how many branches nought takes, not by how often it was asked — the"
            + " two are nearly opposite lists here");

        foreach (SpecialCalls.WhatZeroDid what in silence.Take(8))
        {
            Console.WriteLine(
                $"    routine 0x{what.Routine:X3} asked {what.Asked,4} time(s)"
                + $"  — {Silence(what)}");
        }

        foreach (SpecialCalls.ZeroWas was in new[]
                 {
                     SpecialCalls.ZeroWas.AnAssertion, SpecialCalls.ZeroWas.Both,
                     SpecialCalls.ZeroWas.ARefusal, SpecialCalls.ZeroWas.NeverTested,
                 })
        {
            List<SpecialCalls.WhatZeroDid> these = [.. silence.Where(z => z.Was == was)];

            if (these.Count == 0) continue;

            Console.WriteLine(
                $"      {these.Sum(z => z.Asked),4} of the places, across {these.Count} routine(s):"
                + $" {Meaning(was)}"
                + (was is SpecialCalls.ZeroWas.NeverTested
                    ? ""
                    : $" — and {these.Sum(z => z.TakenByZero)} of their"
                      + $" {these.Sum(z => z.Branches)} branching site(s) in the whole file are"
                      + " taken by nought, which is"
                      + $" {these.Sum(z => z.PlacesTakenByZero)} of {these.Sum(z => z.BranchPlaces)}"
                      + " byte position(s)"));

            // AND THE ONES THAT MATTER, NAMED, WHATEVER THE RANKING SAYS.
            //
            // The list above is the eight the run asked most often, and the routines whose
            // silence actually decides something are asked twice between them — so the ranking
            // hides the only part of this that is a ceiling. A filter that keeps output
            // readable must never decide which question gets asked.
            if (was is not (SpecialCalls.ZeroWas.AnAssertion or SpecialCalls.ZeroWas.Both)) continue;

            Console.WriteLine(
                "            "
                + string.Join(
                    ", ",
                    these.Select(z => $"0x{z.Routine:X3} asked {z.Asked}x, {z.TakenByZero} of its"
                                      + $" {z.Branches} branching site(s) taken by nought"
                                      + $" — {z.PlacesTakenByZero} of {z.BranchPlaces} place(s)"
                                      + $" (tested against {string.Join("/", z.Tested.Order())})")));
        }

        Console.WriteLine();
        Console.WriteLine("    try --routines to see what each of those is asked, then --answer");
        Console.WriteLine("    one of them and run this again. What opens is the measurement.");

        // AND THE OTHER ERROR BAR, WHICH WAS NOT HERE.
        //
        // The routines are the game's own code and nothing in this project will ever follow
        // one. A command with no width is not that: it is a gap in a table in this repository,
        // and a script that stops at one comes back short with no error anywhere. One byte with
        // no entry hid nineteen people on eleven maps, and every reading of this cartridge
        // reported a smaller world, cleanly, for as long as it was missing.
        //
        // The two must not be printed as one number and must not be left as one. A routine is
        // the boundary; a missing width is a job.
        Console.WriteLine();

        if (played.UnreadCommands.Count == 0)
        {
            Console.WriteLine(
                "  and not one script it ran stopped at a command with no width — the reading is");
            Console.WriteLine(
                "  not what is holding this run back");
        }
        else
        {
            Console.WriteLine(
                $"  {played.UnreadCommands.Values.Sum()} place(s) stopped at "
                + $"{played.UnreadCommands.Count} command(s) this project has no width for");

            foreach ((byte code, int times) in played.UnreadCommands.OrderByDescending(p => p.Value).Take(8))
                Console.WriteLine($"    0x{code:X2} stopped {times} place(s)");

            Console.WriteLine();
            Console.WriteLine(
                "    unlike the routines above, these are not the game's code — they are a gap in");
            Console.WriteLine(
                "    a table in this repository. Everything past one of them is unreached in a way");
            Console.WriteLine(
                "    that looks exactly like a person having nothing more to say. --scripts ranks");
            Console.WriteLine(
                "    them across the whole cartridge and --derive scores the widths.");
        }

        static string Why(StoppedBecause stopped) => stopped switch
        {
            StoppedBecause.NothingMoreOpened => "a pass opened nothing new",
            StoppedBecause.ItNeverSettled => "it hit the pass backstop, so something never settles",
            _ => stopped.ToString(),
        };
    }

    /// <summary>Which item teaches which move, so a script handing one over counts as a move.</summary>
    private static Dictionary<int, int> TeachingMachines(Rom rom)
    {
        var teaches = new Dictionary<int, int>();

        List<ItemData> allItems = ItemTable.Locate(rom) is { } itemsAt
            ? [.. ItemTable.Read(rom, itemsAt).Select(i => i.ToData())]
            : [];

        List<int> machineItems =
            [.. allItems.Where(i => i.Pocket == Pocket.Machines).OrderBy(i => i.Id).Select(i => i.Id)];

        List<MoveData> allMoves = MoveExtractor.Extract(rom);

        if (machineItems.Count == MachineMoves.Count
            && MachineMoves.Locate(rom, allMoves.Count, ObstacleMoves.Find(rom)) is { } at)
        {
            List<int> taughtBy = MachineMoves.Read(rom, at);

            for (int i = 0; i < Math.Min(machineItems.Count, taughtBy.Count); i++)
                teaches[machineItems[i]] = taughtBy[i];
        }

        return teaches;
    }

    /// <summary>
    /// What every map runs on arrival, and whether anything in the file can satisfy the condition.
    /// </summary>
    private static void WriteArrivals(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("WHAT A MAP RUNS ON ARRIVAL, AND WHETHER IT CAN");
        Console.WriteLine();

        MapLibrary library = MapLibrary.Open(rom);

        List<WhenAMapRunsSomething.Arrival> arrivals =
            WhenAMapRunsSomething.In(library, WhenAMapRunsSomething.WhatIsWritten(rom, library));

        if (arrivals.Count == 0)
        {
            Console.WriteLine("  no map runs anything on arrival");

            return;
        }

        // Conditions and PLACES, because the same script address is hung off many maps and the
        // condition tables repeat with it. Counting the conditions is counting reads again.
        int distinct = arrivals.Select(a => (a.Variable, a.Value, a.Address)).Distinct().Count();

        Console.WriteLine(
            $"  {arrivals.Count} condition(s) — {distinct} distinct (variable, value, script) — on "
            + $"{arrivals.Select(a => a.Address).Distinct().Count()} script(s), across "
            + $"{arrivals.Select(a => a.MapId).Distinct().Count()} map(s), naming "
            + $"{arrivals.Select(a => a.Variable).Distinct().Count()} variable(s)");

        // Both numbers, because the same condition table hangs off many maps. The first is how
        // often the cartridge asks; the second is how many different things it is asking.
        int NotedAsPlaces(Func<WhenAMapRunsSomething.Arrival, bool> which) =>
            arrivals.Where(which).Select(a => (a.Variable, a.Value, a.Address)).Distinct().Count();

        Console.WriteLine();
        Console.WriteLine(
            $"    {arrivals.Count(a => a.NothingWritesIt),4} condition(s), "
            + $"{NotedAsPlaces(a => a.NothingWritesIt),3} distinct — a variable NOTHING in the scan writes at all");
        Console.WriteLine(
            $"    {arrivals.Count(a => a.NobodyWritesThisValue),4} condition(s), "
            + $"{NotedAsPlaces(a => a.NobodyWritesThisValue),3} distinct — a variable something writes, but nobody"
            + " writes THAT VALUE");
        Console.WriteLine(
            $"    {arrivals.Count(a => a.WrittenWithThis > 0),4} condition(s), "
            + $"{NotedAsPlaces(a => a.WrittenWithThis > 0),3} distinct — a setvar in the scan can satisfy it");

        Console.WriteLine();
        Console.WriteLine("  by variable, worst first:");

        foreach (IGrouping<int, WhenAMapRunsSomething.Arrival> variable in arrivals
                     .GroupBy(a => a.Variable)
                     .OrderByDescending(g => g.Count(a => a.WrittenWithThis == 0))
                     .ThenByDescending(g => g.Count()))
        {
            WhenAMapRunsSomething.Arrival first = variable.First();

            Console.WriteLine(
                $"    0x{variable.Key:X4} — {variable.Count(),4} condition(s), "
                + $"{variable.Select(a => (a.Value, a.Address)).Distinct().Count(),3} distinct, on "
                + $"{variable.Select(a => a.MapId).Distinct().Count(),3} map(s)");
            Console.WriteLine(
                $"             wanted {string.Join("/", variable.Select(a => a.Value).Distinct().Order())}"
                + $"; written "
                + (first.Values.Count == 0
                    ? "NOWHERE"
                    : string.Join(
                        ", ", first.Values.OrderBy(v => v.Key).Select(v => $"{v.Key} at {v.Value} place(s)")))
                + $"; {variable.Count(a => a.WrittenWithThis == 0)} condition(s) nobody writes");
        }
    }

    /// <summary>
    /// The two commands with widths and no names, measured against the maps they are on.
    /// </summary>
    private static void WriteTwoCommands(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("TWO COMMANDS WITH WIDTHS AND NO NAMES");
        Console.WriteLine();

        MapLibrary library = MapLibrary.Open(rom);

        List<PersonCommands.Site> sites = PersonCommands.In(rom, library);

        IReadOnlyDictionary<string, IReadOnlyList<(int LocalId, int Movement)>> everybody =
            PersonCommands.Everybody(library);

        if (sites.Count == 0)
        {
            Console.WriteLine("  the map scan opens neither of them anywhere");

            return;
        }

        foreach (byte code in new[] { PersonCommands.Three, PersonCommands.Two })
        {
            List<PersonCommands.Site> these = [.. sites.Where(s => s.Code == code)];

            if (these.Count == 0) continue;

            Console.WriteLine(
                $"  0x{code:X2} — {these.Count} read(s) at {these.Select(s => s.At).Distinct().Count()} "
                + $"place(s) on {these.Select(s => s.MapId).Distinct().Count()} map(s)");

            Console.WriteLine(
                $"      {PersonCommands.NamesSomebody(these),4} of them name a person who is really on that map");
            Console.WriteLine(
                $"      {PersonCommands.TheOtherWordWould(these, everybody),4} would if the SECOND word were read as the person"
                + "   <- the control");

            if (code == PersonCommands.Three)
            {
                Console.WriteLine(
                    $"      {these.Count(s => s.InsideTheMap),4} have their other two words inside that map's bounds");

                List<int> away = [.. these.Where(s => s.Away is not null).Select(s => s.Away!.Value).Order()];

                if (away.Count > 0)
                {
                    Console.WriteLine(
                        $"      how far those words are from where the cartridge put that person:"
                        + $" {away.Count(a => a == 0)} exactly there,"
                        + $" {away.Count(a => a is > 0 and <= 3)} within three squares,"
                        + $" {away.Count(a => a > 3)} further");
                    Console.WriteLine(
                        $"      {PersonCommands.ExactlyThereByChance(these):0.00} exactly-there would be expected"
                        + " by chance, if the words were any square on that map   <- the floor");
                }
            }
            else
            {
                Console.WriteLine(
                    "      the byte after the person, and how often it is that person's own movement type:");

                foreach (IGrouping<int, PersonCommands.Site> value in these
                             .GroupBy(s => s.A)
                             .OrderByDescending(g => g.Count()))
                {
                    Console.WriteLine(
                        $"        {value.Key,3} at {value.Count(),4} site(s)"
                        + $" — {value.Count(s => s.Movement == value.Key)} of them the person's own");
                }

                Console.WriteLine(
                    $"      {these.Count(s => s.Movement == s.A)} are the named person's own movement type;"
                    + $" {PersonCommands.SomebodyElsesMovement(these, everybody):0.0} would be"
                    + " somebody else's on the same map   <- the floor");
            }

            foreach (PersonCommands.Site site in these.Take(4))
            {
                Console.WriteLine(
                    $"        {site.MapId,-8} {site.What,-22} 0x{site.At:X6}  person {site.Person}"
                    + (site.Square is { } at ? $" at ({at.X},{at.Y}) move {site.Movement}" : " — NO SUCH PERSON")
                    + (site.Code == PersonCommands.Three ? $"  words ({site.A},{site.B})" : $"  byte {site.A}"));
            }

            Console.WriteLine();
        }
    }

    /// <summary>
    /// The error bar on every map-scan number in this project, in one table: how many times each
    /// command is decoded against how many byte positions those reads are.
    /// </summary>
    private static void WriteWhatTheScanOpens(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("WHAT THE MAP SCAN OPENS, IN READS AND IN PLACES");
        Console.WriteLine();

        (WhatTheScanOpens.Overall whole, List<WhatTheScanOpens.ACode> byCode) =
            WhatTheScanOpens.Of(rom, MapLibrary.Open(rom));

        Console.WriteLine(
            $"  {whole.Entries} script entry(ies) at {whole.Addresses} distinct address(es)");
        Console.WriteLine(
            $"  {whole.Reads} command read(s) at {whole.Places} byte position(s)"
            + $" — {(whole.Places == 0 ? 0 : (double)whole.Reads / whole.Places):0.0} reads per byte");
        Console.WriteLine();
        Console.WriteLine(
            "  by command, worst first. A code whose two numbers are EQUAL has nothing to correct");
        Console.WriteLine(
            "  in any instrument that counts it; anything above one has it waiting in all of them.");
        Console.WriteLine();

        // EVERY code, not the worst two dozen. A filter that keeps output readable must never
        // decide which question gets asked, and the code somebody wants to look up is as likely
        // to be near one as near sixty-seven.
        foreach (WhatTheScanOpens.ACode code in byCode)
        {
            Console.WriteLine(
                $"    0x{code.Code:X2} {ScriptCommands.NameOf(code.Code),-16} {code.Reads,5} read(s)"
                + $" at {code.Places,5} place(s)  x{code.Over:0.0}"
                + $"   on {code.Maps,3} map(s)");
        }

        // AND BY KIND, with the number that would have caught 224 at 221: what each kind opens
        // that nothing else does.
        Console.WriteLine();
        Console.WriteLine("  by which of the five kinds hangs the script:");
        Console.WriteLine();

        foreach (WhatTheScanOpens.AKind kind in WhatTheScanOpens.ByKind(rom, MapLibrary.Open(rom)))
        {
            Console.WriteLine(
                $"    {kind.Kind,-12} {kind.Entries,5} entry(ies) at {kind.Addresses,5} address(es), "
                + $"{kind.Reads,6} read(s) at {kind.Places,6} place(s)"
                + $"  — {kind.Only,5} of those places NO OTHER KIND opens");
            Console.WriteLine(
                $"                 asks {kind.Routines,3} routine(s), {kind.RoutinesOnly.Count} of them"
                + " asked by no other kind"
                + (kind.RoutinesOnly.Count == 0
                    ? ""
                    : ": " + string.Join(", ", kind.RoutinesOnly.Take(12).Select(r => $"0x{r:X3}"))
                      + (kind.RoutinesOnly.Count > 12 ? $" and {kind.RoutinesOnly.Count - 12} more" : "")));
            Console.WriteLine(
                $"                moves {kind.Flags,3} flag(s), {kind.FlagsOnly.Count} of them moved"
                + " by no other kind"
                + (kind.FlagsOnly.Count == 0
                    ? ""
                    : ": " + string.Join(", ", kind.FlagsOnly.Take(12).Select(f => $"0x{f:X4}"))
                      + (kind.FlagsOnly.Count > 12 ? $" and {kind.FlagsOnly.Count - 12} more" : "")));
        }

        int clean = byCode.Count(c => c.Reads == c.Places);

        Console.WriteLine();
        Console.WriteLine(
            $"  {clean} of {byCode.Count} code(s) are read once per byte position, so a count of"
            + " reads and a count of places are the same number for them");
    }

    /// <summary>
    /// The standard routines, which are blocks a script reaches by number. Nothing here knows
    /// where the table is; the shape is hunted and the reversed image says whether the shape
    /// means anything.
    /// </summary>
    private static void WriteRoutinesReachedByNumber(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("THE ROUTINES REACHED BY NUMBER");
        Console.WriteLine();

        List<StandardRoutines.Asked> asked = StandardRoutines.WhatIsAsked(rom, MapLibrary.Open(rom));

        if (asked.Count == 0)
        {
            Console.WriteLine("  nothing on any map asks for one");

            return;
        }

        int widest = asked.Max(a => a.Index);

        Console.WriteLine(
            $"  {asked.Sum(a => a.Sites)} asking(s) at {asked.Sum(a => a.Places)} place(s), "
            + $"of {asked.Select(a => a.Index).Distinct().Count()} number(s), highest 0x{widest:X2}");

        foreach (StandardRoutines.Asked one in asked)
        {
            Console.WriteLine(
                $"    {(one.Returns ? "callstd" : "gotostd")} 0x{one.Index:X2} — "
                + $"{one.Sites,4} time(s) at {one.Places,4} place(s)");
        }

        // WHO ANSWERS, without the table. If a script says `callstd N ; compare 0x800D` and
        // nothing before it could have put anything there, the compare is reading what N left.
        List<StandardRoutines.Answers> answers = StandardRoutines.WhoAnswers(rom, MapLibrary.Open(rom));

        Console.WriteLine();
        Console.WriteLine("  and which of them ANSWER, read from the callers rather than from the table:");

        if (answers.Count == 0)
        {
            Console.WriteLine("    nowhere in the maps is one of them followed by a compare it branches on");
        }

        foreach (StandardRoutines.Answers one in answers)
        {
            Console.WriteLine(
                $"    callstd 0x{one.Index:X2} — {one.Sites,3} site(s) at {one.Places,3} place(s) put a compare "
                + "on the answer variable straight after it");
            Console.WriteLine(
                $"        {one.NothingBefore,3} with NOTHING before that could have answered"
                + (one.MustAnswer ? "   <- so this one answers, whatever it is" : "")
                + $"; {one.SomebodyBefore} with somebody; {one.NotSaid} not said");
        }

        // The table has to have room for the highest number anybody asks for. Anything shorter
        // than that cannot be it, whatever else it looks like.
        int atLeast = widest + 1;

        List<StandardRoutines.ATable> tables = StandardRoutines.Tables(rom, atLeast);
        List<StandardRoutines.ATable> floor = StandardRoutines.NoiseFloor(rom, atLeast);

        Console.WriteLine();
        Console.WriteLine(
            $"  runs of {atLeast}+ consecutive pointers that all land on something reading as a script:");
        Console.WriteLine($"    {tables.Count} in this file");
        Console.WriteLine($"    {floor.Count} in the same file REVERSED  <- the floor");

        if (floor.Count >= tables.Count)
        {
            Console.WriteLine();
            Console.WriteLine(
                "  THE REVERSAL FINDS AS MANY, so this shape is what these bytes do by accident");
            Console.WriteLine("  and no candidate here is worth reading. The table is not found this way.");

            return;
        }

        foreach (StandardRoutines.ATable table in tables.OrderByDescending(t => t.Entries))
        {
            List<(int Index, uint At, SpecialCalls.LeftBehind Left, int Who)> leaves =
                [.. StandardRoutines.WhatTheyLeave(rom, table).Take(atLeast)];

            Console.WriteLine(
                $"    0x{table.At:X8} — {table.Entries,4} pointer(s), "
                + $"{leaves.Select(l => l.At).Distinct().Count()} distinct in the first {atLeast}, "
                + $"[5] leaves {leaves[5].Left}");
        }
    }

    private static void WriteSpecialContracts(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("WHAT THE ROUTINES ARE ASKED");
        Console.WriteLine();

        MapLibrary library = MapLibrary.Open(rom);

        List<SpecialContract> contracts = SpecialContracts.Derive(rom, library, Console.WriteLine);

        List<WhoTheCompareBelongsTo.ACompareAcross> across = WhoTheCompareBelongsTo.In(rom, library);

        // Branched-on first: a routine nobody branches on cannot be shutting a door, whatever
        // else it does, so it is not what a story walk is looking for.
        Console.WriteLine();
        Console.WriteLine("  the ones a script branches on, which are the ones that gate anything");

        foreach (SpecialContract contract in contracts
                     .Where(c => c.Branches > 0)
                     .OrderByDescending(c => c.Branches))
        {
            Console.WriteLine(
                $"    0x{contract.Routine:X3}  {contract.Sites,4} call(s) at {contract.CallPlaces,4} place(s)"
                + (contract.CallInflation > 1 ? $" x{contract.CallInflation:0.0}" : "     ")
                + $", {contract.Branches,3} branch at {contract.Places,3} place(s)"
                + $", {contract.TakesArguments} argument(s)"
                + (contract.LooksLikeACount ? "   <- compared against a run from 1, so it counts something" : ""));

            if (contract.Compared.Count > 0)
            {
                Console.WriteLine(
                    "        compared against " + string.Join(
                        ", ", contract.Compared.OrderBy(p => p.Key).Select(p => $"{p.Key}x{p.Value}")));
            }

            WriteAcrossABarrier(contract);

            Console.WriteLine($"        {string.Join("; ", contract.Where)}");
        }

        // The ones this reading called branched-on until 220 and no longer does. They are not
        // "nothing branches on it" — they are "the branch is past something that may have
        // answered instead", and folding them in with the quiet ones would bury exactly the
        // sites worth reading next.
        List<SpecialContract> onlyAcross =
        [
            .. contracts
                .Where(c => c.Branches == 0 && c.AcrossABarrier > 0)
                .OrderByDescending(c => c.AcrossABarrier),
        ];

        if (onlyAcross.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "  and the ones whose ONLY branch is past something that may have answered instead");
            Console.WriteLine(
                "  — counted as branched-on here until 220, when this reading was given the barrier");
            Console.WriteLine(
                "  SpecialCalls has had since 214. --through-a-call is what says whether the thing");
            Console.WriteLine("  in the way answered, and for 0x01C and 0x01D it says it did not");

            foreach (SpecialContract contract in onlyAcross)
            {
                Console.WriteLine(
                    $"    0x{contract.Routine:X3}  {contract.Sites,4} call(s) at {contract.CallPlaces,4} place(s), "
                    + $"{contract.AcrossABarrier,3} across a barrier at {contract.PlacesAcross,3} place(s), "
                    + $"{contract.TakesArguments} argument(s)");

                WriteAcrossABarrier(contract);

                WriteWhatWasInTheWay(across.Where(a => a.Routine == contract.Routine));

                Console.WriteLine($"        {string.Join("; ", contract.Where)}");
            }
        }

        // And the whole of it, so the three verdicts can be read as a shape rather than one
        // routine at a time. NotSaid is printed as its own number on purpose: a reading that
        // stopped and a fact about the cartridge are different things.
        if (across.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  {across.Count} site(s) whose compare is only past a barrier, by what was in the way:");

            foreach (IGrouping<WhoTheCompareBelongsTo.Whose, WhoTheCompareBelongsTo.ACompareAcross> whose
                     in across.GroupBy(a => a.Belongs).OrderByDescending(g => g.Count()))
            {
                Console.WriteLine($"    {whose.Count(),4}  {Verdict(whose.Key)}");

                foreach (IGrouping<WhoTheCompareBelongsTo.InTheWay, WhoTheCompareBelongsTo.ACompareAcross> was
                         in whose.GroupBy(a => a.Was).OrderByDescending(g => g.Count()))
                {
                    WhoTheCompareBelongsTo.ACompareAcross first = was.First();

                    Console.WriteLine(
                        $"          {was.Count(),4} — {InTheWay(was.Key)}"
                        + $"   e.g. 0x{first.Routine:X3} at {first.MapId} {first.What} 0x{first.At:X8}"
                        + Past(first));
                }
            }
        }

        int quiet = contracts.Count(c => c.Branches == 0 && c.AcrossABarrier == 0);

        Console.WriteLine();
        Console.WriteLine(
            $"  and {quiet} routines nothing branches on — called for their effect rather than");
        Console.WriteLine(
            "  their answer, so a stand-in for one of those buys no ground and is not worth writing");
    }

    /// <summary>
    /// Where the thing in the way was — an address for a call, a NUMBER for a standard routine.
    /// The two are not the same kind of thing and printing a number as an address reads as a
    /// pointer to the bottom of the image.
    /// </summary>
    private static string Past(WhoTheCompareBelongsTo.ACompareAcross across) => across.Was switch
    {
        WhoTheCompareBelongsTo.InTheWay.AStandardRoutineThatAnswers or
            WhoTheCompareBelongsTo.InTheWay.AStandardRoutine => $" past standard 0x{across.Called:X2}",
        _ => across.Called > 0 ? $" past 0x{across.Called:X8}" : "",
    };

    private static string Verdict(WhoTheCompareBelongsTo.Whose whose) => whose switch
    {
        WhoTheCompareBelongsTo.Whose.StillThisRoutines =>
            "the thing in the way CANNOT have answered — the compare is the routine's after all",
        WhoTheCompareBelongsTo.Whose.SomebodyElses =>
            "somebody else answered, and the compare was never this routine's",
        _ => "not said — the thing in the way goes somewhere this reading does not follow",
    };

    private static string InTheWay(WhoTheCompareBelongsTo.InTheWay was) => was switch
    {
        WhoTheCompareBelongsTo.InTheWay.AnotherRoutine => "another routine",
        WhoTheCompareBelongsTo.InTheWay.ACommandThatAnswers => "a command that answers on its own account",
        WhoTheCompareBelongsTo.InTheWay.AStandardRoutineThatAnswers =>
            "a standard routine the callers show DOES answer",
        WhoTheCompareBelongsTo.InTheWay.AStandardRoutine => "a standard routine, never read here",
        WhoTheCompareBelongsTo.InTheWay.ACallThatAnswers => "a call whose block leaves an answer",
        WhoTheCompareBelongsTo.InTheWay.ACallThatTouchesNothing => "a call whose block touches nothing",
        _ => "a call whose block jumps away",
    };

    /// <summary>The verdicts for one routine, when there are any.</summary>
    private static void WriteWhatWasInTheWay(IEnumerable<WhoTheCompareBelongsTo.ACompareAcross> across)
    {
        foreach (IGrouping<WhoTheCompareBelongsTo.InTheWay, WhoTheCompareBelongsTo.ACompareAcross> was
                 in across.GroupBy(a => a.Was).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine(
                $"        {was.Count(),3} of them past {InTheWay(was.Key)} — {Verdict(WhoTheCompareBelongsTo.Belongs(was.Key))}");
        }
    }

    /// <summary>
    /// What a routine's answer is compared against only on the far side of a barrier, printed
    /// separately from what it is compared against directly — because they are two different
    /// claims and one of them is not about this routine yet.
    /// </summary>
    private static void WriteAcrossABarrier(SpecialContract contract)
    {
        if (contract.AcrossABarrier == 0) return;

        Console.WriteLine(
            $"        and at {contract.AcrossABarrier} site(s) compared against "
            + string.Join(
                ", ", contract.ComparedAcross.OrderBy(p => p.Key).Select(p => $"{p.Key}x{p.Value}"))
            + " ONLY past something that may have answered instead");
    }



    private static void WriteClosure(Rom rom, IReadOnlyDictionary<int, int> answers, string startAt)
    {
        Console.WriteLine();
        Console.WriteLine("CAN IT BE FINISHED");
        Console.WriteLine();

        WorldData world = WorldExporter.Export(rom);

        MapData first = world.Find(startAt) ?? world.Maps.First();

        Console.WriteLine(
            $"  {world.Maps.Count} maps exported; walking from {first.Id} ({first.Name}), "
            + $"with {world.FlagsAtStart.Count} flags a new game already sets");
        Console.WriteLine();

        // Which item teaches which move, so that a script handing over HM01 counts as a
        // script that opened thirty-eight maps. Empty when the tables were not found, and
        // said out loud rather than silently costing the walk every field move in the game.
        var teaches = new Dictionary<int, int>();

        List<ItemData> allItems = ItemTable.Locate(rom) is { } itemsAt
            ? [.. ItemTable.Read(rom, itemsAt).Select(i => i.ToData())]
            : [];

        List<int> machineItems =
            [.. allItems.Where(i => i.Pocket == Pocket.Machines).OrderBy(i => i.Id).Select(i => i.Id)];

        List<MoveData> allMoves = MoveExtractor.Extract(rom);

        if (machineItems.Count == MachineMoves.Count
            && MachineMoves.Locate(rom, allMoves.Count, ObstacleMoves.Find(rom)) is { } machinesAt)
        {
            List<int> taughtBy = MachineMoves.Read(rom, machinesAt);

            for (int i = 0; i < Math.Min(machineItems.Count, taughtBy.Count); i++)
                teaches[machineItems[i]] = taughtBy[i];
        }

        Console.WriteLine(
            teaches.Count > 0
                ? $"  {teaches.Count} teaching machines, so a script handing one over counts as a move"
                : "  no machine table found, so no script can teach anything — every field move"
                  + " will read as unobtainable and the answer below is far too small");

        // The cartridge half, handed in. Running a script needs the image; the walk does not
        // and has never had one.
        ScriptOutcome Run(uint address, IReadOnlyCollection<int> flags)
        {
            var state = new ScriptState();

            foreach (int flag in flags) state.Set(flag);

            ScriptRun run = ScriptRunner.Run(rom, address, state, answers: answers);

            return new ScriptOutcome(
                run.FlagsSet,
                run.FlagsCleared,

                // What a script hands over, translated to a move when the thing it hands
                // over is a teaching machine. This is how CUT opens thirty-eight maps, and it
                // is the one part of the loop that needs a table rather than a script: an
                // item's own record does not say what it teaches.
                run.GivesItem is { } item && teaches.TryGetValue(item, out int move) ? [move] : [],
                run.SpecialsCalled);
        }

        Closure closed = StoryClosure.Walk(world, first.Id, Run, Console.WriteLine);

        Console.WriteLine();
        Console.WriteLine(
            $"  a player can reach {closed.Reached.Count} of {world.Maps.Count} maps by playing");
        Console.WriteLine(
            $"    {closed.Flags.Count} flags set, {closed.Moves.Count} field moves obtained "
            + $"({string.Join(", ", closed.Moves.Order())})");
        Console.WriteLine($"    it stopped opening after {closed.Rounds.Count} pass(es)");

        if (closed.Rounds.Count >= StoryClosure.MostRounds)
        {
            Console.WriteLine(
                "    which is the backstop rather than a fixpoint — something is alternating");
        }

        // The maps nobody can get to, which is the answer to the question.
        Console.WriteLine();
        Console.WriteLine($"  {closed.Unreached.Count} maps nobody can get to");

        foreach (string mapId in closed.Unreached.Take(40))
        {
            string name = world.Find(mapId)?.Name ?? "";

            Console.WriteLine($"    {mapId,-8} {name}");
        }

        if (closed.Unreached.Count > 40)
            Console.WriteLine($"    ... and {closed.Unreached.Count - 40} more");

        // What is standing in the way at the edge, which is where to look first.
        if (closed.Blocked.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  the frontier: squares wanting a move nobody has");

            foreach (IGrouping<int, Frontier> wanting in closed.Blocked
                         .GroupBy(b => b.ShiftedBy)
                         .OrderByDescending(g => g.Count()))
            {
                Console.WriteLine(
                    $"    move {wanting.Key,3}: {wanting.Count(),4} squares — "
                    + string.Join(", ", wanting.Take(3).Select(b => $"{b.MapId} {b.Square}")));
            }
        }

        // And the error bar, which is the number that decides how much of the above to
        // believe.
        Console.WriteLine();
        Console.WriteLine(
            $"  {closed.Specials.Values.Sum()} calls to {closed.Specials.Count} routines this "
            + "walk could not answer");

        foreach ((int routine, int times) in closed.Specials.OrderByDescending(p => p.Value).Take(10))
            Console.WriteLine($"    routine 0x{routine:X3} asked {times} time(s)");

        Console.WriteLine();
        Console.WriteLine(
            "    every one of those took the zero arm. A door this walk calls shut may have");
        Console.WriteLine(
            "    asked a question nobody could answer — so this figure is a floor, and the");
        Console.WriteLine(
            "    real world is never smaller than it.");
    }

    /// <summary>
    /// Every place in the whole file that moves these flags, and what names each one.
    /// <para>
    /// <b>The instrument that does not start at a map.</b> Everything else here gathers the
    /// scripts the maps point at and follows the jumps out of them — which is the right shape
    /// for almost every question and is silently the wrong shape for "is there anything here
    /// the maps do not point at?". Asked that, a map-first scan comes back identical to one
    /// that looked everywhere and found nothing.
    /// </para>
    /// <para>
    /// So this scans the file for the three bytes that move a flag, and asks of every hit
    /// whether the map scan ever decoded that byte. Then it climbs: what names this address,
    /// what names that, until it reaches something the map scan opens — a way in — or reaches
    /// a literal, which is the code boundary with an address on it.
    /// </para>
    /// </summary>
    private static void WriteInTheImage(Rom rom, IReadOnlyList<int> flags)
    {
        Console.WriteLine();
        Console.WriteLine("EVERYWHERE IN THE IMAGE");
        Console.WriteLine();

        MapLibrary library = MapLibrary.Open(rom);

        List<SetsAFlag> scripts = [.. library.All().SelectMany(EveryScriptOn)];

        int[] covered = EverywhereInTheImage.Opened(rom, scripts);

        int decoded = covered.Count(b => b != EverywhereInTheImage.Nobody);

        Console.WriteLine(
            $"  the map scan opens {scripts.Count} script(s), and what they decode to is "
            + $"{decoded} of the {rom.Length} bytes in this file — {100.0 * decoded / rom.Length:0.0}%");
        Console.WriteLine(
            "    so every \"nothing in the world does X\" this project has ever printed was a");
        Console.WriteLine(
            $"    sentence about that {100.0 * decoded / rom.Length:0.0}%. Everything below reads the whole file.");
        Console.WriteLine();
        Console.WriteLine(
            $"  a three-byte pattern turns up by accident about {EverywhereInTheImage.ByChance(rom, 3):0.0} "
            + "time(s) in an image this size — which is the error bar on every count below");

        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        var sites = new Dictionary<int, IReadOnlyList<FlagSite>>();

        foreach (int flag in flags)
        {
            IReadOnlyList<FlagSite> found = EverywhereInTheImage.Moves(rom, flag, covered);

            sites[flag] = found;

            Console.WriteLine();
            Console.WriteLine(
                $"  0x{flag:X4} — {found.Count} site(s) in the file, "
                + $"{found.Count(s => s.ReadsAsAScript)} of which read as script, "
                + $"{found.Count(s => s.Opened)} of which the map scan opened");

            // AND HOW MANY OF THEM ARE THE SAME PLACE TWICE.
            //
            // The error bar above is a whole-image average computed as though every byte
            // were independent, and this image is nothing of the sort. 0x0089 turns up nine
            // times against a floor of one, which reads as signal — and SEVEN of the nine
            // are inside 788 bytes of a low-entropy table with names in it, where the same
            // record repeats and the pattern is a field inside it rather than a command.
            //
            // A uniform floor cannot model that, so the clustering is printed beside it.
            // Sites that arrive in a clump are one fact about the file, however many of them
            // there are; sites spread across it are as many facts as there are sites.
            WriteHowClustered(rom, found.Select(f => f.Offset));

            if (found.Count == 0)
            {
                Console.WriteLine(
                    "    NOT ONE SETFLAG OR CLEARFLAG OF IT EXISTS IN THE FILE. Whatever moves it");
                Console.WriteLine(
                    "    is compiled code writing the flag array directly, and no reading of scripts");
                Console.WriteLine(
                    "    anywhere will ever find it.");

                continue;
            }

            foreach (FlagSite site in found.Take(40))
            {
                Console.WriteLine(
                    $"    0x{site.Offset:X6}  {(site.Sets ? "setflag  " : "clearflag")}  "
                    + (site.ReadsAsAScript ? "reads as script" : "DOES NOT READ AS SCRIPT — probably noise")
                    + "  "
                    + (site.Opened ? "the map scan opened this" : "NEVER OPENED BY THE MAP SCAN"));
            }

            if (found.Count > 40) Console.WriteLine($"    ... and {found.Count - 40} more");
        }

        // The two halves of one scene. Two lists of sites cannot say "one piece of script does
        // both"; sites a few dozen bytes apart can, and that is the whole question about
        // SAFFRON — a flag holding people in place and a flag keeping people off, failing in
        // opposite directions with only one direction ever visible.
        for (int a = 0; a < flags.Count; a++)
        {
            for (int b = a + 1; b < flags.Count; b++)
            {
                IReadOnlyList<(FlagSite First, FlagSite Second)> pairs =
                    EverywhereInTheImage.Together(sites[flags[a]], sites[flags[b]]);

                Console.WriteLine();
                Console.WriteLine(
                    $"  0x{flags[a]:X4} AND 0x{flags[b]:X4} within 128 bytes of each other: "
                    + $"{pairs.Count} place(s)");

                if (pairs.Count == 0)
                {
                    Console.WriteLine(
                        "    NOWHERE IN THE FILE DOES ONE PIECE OF SCRIPT MOVE BOTH — so they are two");
                    Console.WriteLine(
                        "    scenes after all, or the one that does it is not a script at all.");

                    continue;
                }

                foreach ((FlagSite first, FlagSite second) in pairs.Take(8))
                {
                    Console.WriteLine(
                        $"    {first} and {second} — {Math.Abs(first.Offset - second.Offset)} bytes apart");
                }
            }
        }

        // And the climb, on every site that reads as script.
        //
        // THE MISTAKE THIS WAS WRITTEN TO FIX. It climbed only sites the map scan had never
        // opened, reasoning that an opened site is already answered by --flags and that mixing
        // the two buries a new finding in a list of old ones. Both halves of that were true and
        // the conclusion was still wrong: the first thing this instrument was ever pointed at
        // came back with ONE site for 0x003E in the whole image, opened, and the climb — the
        // only part that could say what enters it — was the part that did not run.
        //
        // "Already answered" was doing the work there, and it was not answered. A filter whose
        // job is to keep the output readable must never be the thing that decides which
        // question gets asked.
        List<FlagSite> climbing =
        [
            .. sites.Values.SelectMany(s => s).Where(s => s.ReadsAsAScript).DistinctBy(s => s.Offset),
        ];

        Console.WriteLine();
        Console.WriteLine(
            $"  {climbing.Count} site(s) read as script — climbing each one, opened or not");

        foreach (FlagSite site in climbing.Take(12)) Climb(rom, index, covered, scripts, site);

        if (climbing.Count > 12)
            Console.WriteLine($"    ... and {climbing.Count - 12} more not climbed");
    }

    /// <summary>
    /// Every stopped read of one command, with the bytes around it.
    /// <para>
    /// <b>A width is settled by a column, and nothing here could show one.</b> <c>--scripts</c>
    /// prints one example per command — enough to know a command is in the way, and not enough
    /// to tell which width is right, because that is a question about what all its sites have
    /// in common. Every width adopted in this project was read off a column pasted together by
    /// hand from a script somewhere else.
    /// </para>
    /// </summary>
    private static void WriteStops(Rom rom, IReadOnlyList<byte> codes)
    {
        Console.WriteLine();
        Console.WriteLine("WHERE READS STOP");

        MapLibrary library = MapLibrary.Open(rom);

        var sites = new Dictionary<byte, List<int>>();
        var from = new Dictionary<int, uint>();
        var seen = new HashSet<uint>();

        foreach (LoadedMap map in library.All())
        {
            foreach (SetsAFlag script in EveryScriptOn(map))
            {
                foreach (uint block in ScriptReader.Reachable(rom, script.Address))
                {
                    if (!seen.Add(block)) continue;
                    if (ScriptReader.StoppedAt(rom, block) is not { } code) continue;
                    if (!codes.Contains(code)) continue;
                    if (ScriptReader.StoppedAtOffset(rom, block) is not { } at) continue;

                    if (!sites.TryGetValue(code, out List<int>? where)) sites[code] = where = [];

                    if (!where.Contains(at)) where.Add(at);

                    // AND WHERE THE READ STARTED, WHICH IS THE HALF THAT MATTERS.
                    //
                    // A stop is reported at the byte the reader could not step over, and that
                    // byte is only a command if the reader was in step to begin with. 0xE6's two
                    // sites are both inside a `gotoif`'s pointer — the block they belong to
                    // decodes perfectly from its own start — so what is wrong is the ADDRESS the
                    // read began at, not the command it ended on. Without the start printed
                    // beside the stop there is no way to tell those apart, and one of them is a
                    // width to derive while the other is a bogus pointer to find.
                    from[at] = block;
                }
            }
        }

        foreach (byte code in codes)
        {
            List<int> where = sites.GetValueOrDefault(code, []);

            Console.WriteLine();
            Console.WriteLine(
                $"  0x{code:X2} — {where.Count} stopped read(s)"
                + (where.Count > 1
                    ? $", {WhatIsBehindAStop.AreOneIdiom(rom, where):P0} of them sharing their run-up"
                    : ""));

            if (where.Count == 0)
            {
                Console.WriteLine("    nothing stops at it — it is not in the way of anything");
                continue;
            }

            Console.WriteLine();
            Console.WriteLine("    the run-up, the command, and what follows — the column is the answer:");

            foreach (int at in where.Take(16))
            {
                string before = string.Join(" ", Enumerable.Range(0, 5)
                    .Select(i => at - 5 + i >= 0 ? $"{rom.ReadU8(at - 5 + i):X2}" : ".."));

                string after = string.Join(" ", Enumerable.Range(1, 10)
                    .Select(i => at + i < rom.Length ? $"{rom.ReadU8(at + i):X2}" : ".."));

                uint start = from.GetValueOrDefault(at);

                Console.WriteLine(
                    $"      0x{at:X6}  {before} | {rom.ReadU8(at):X2} | {after}"
                    + $"   read from 0x{start:X8}"
                    + (start != 0 && at - (int)(start - Rom.BaseAddress) is var into and > 0
                        ? $" (+{into})"
                        : ""));
            }

            if (where.Count > 16) Console.WriteLine($"      ... and {where.Count - 16} more");

            // And what each width would resume on, across all of them at once. A width that
            // lands on padding at every site has landed in the tail of an argument; one that
            // lands on a real command at every site is the answer in plain sight.
            Console.WriteLine();
            Console.WriteLine("    what each width resumes on, across every site:");

            for (var width = 0; width <= 8; width++)
            {
                var landing = new Dictionary<byte, int>();

                foreach (int at in where)
                {
                    if (at + 1 + width >= rom.Length) continue;

                    byte next = rom.ReadU8(at + 1 + width);

                    landing[next] = landing.GetValueOrDefault(next) + 1;
                }

                if (landing.Count == 0) continue;

                Console.WriteLine(
                    $"      {width} bytes: "
                    + string.Join(", ", landing.OrderByDescending(l => l.Value).Take(4)
                        .Select(l => $"0x{l.Key:X2} ({ScriptCommands.NameOf(l.Key)}) x{l.Value}")));
            }
        }
    }

    /// <summary>
    /// Everywhere in the file a variable is written, and what opens it.
    /// <para>
    /// The mirror of <see cref="WriteInTheImage"/>, and it was missing for as long as that
    /// existed. The starter is behind <c>0x4055 == 2</c> — a variable, not a flag — and the
    /// only way to ask who puts a two in it was to read bytes by eye.
    /// </para>
    /// </summary>
    /// <summary>
    /// The scenes written as several doors into one room, and what that costs every count.
    /// </summary>

    /// <summary>
    /// What the cartridge puts between a shopkeeper and the floor a player stands on.
    /// <para>
    /// 197 measured that the playthrough stands in front of at most ONE shop counter in the
    /// whole game, and that every counter it misses is <b>exactly two squares</b> from the
    /// nearest floor it stood on — 11 of 11, 14 of 14, 19 of 19, at every lever setting. The
    /// explanation is obvious and this is the instrument that refuses to take it on trust: if
    /// talking across a counter is a thing this cartridge does, then the square in between is
    /// not an ordinary wall and its behaviour byte will say so, the same way water and ledges
    /// do.
    /// </para>
    /// <para>
    /// Asked of EVERY shopkeeper in the file rather than of the ones a run reached, because
    /// "the squares the playthrough happened to miss" is a fact about the playthrough. The
    /// distribution is what decides it: one value on most of them is a behaviour, and a spread
    /// across many values is ordinary scenery and the guess was wrong.
    /// </para>
    /// <para>
    /// It can come back empty. If no shopkeeper in the file has an unwalkable square beside
    /// them, the whole idea is wrong and this prints that.
    /// </para>
    /// </summary>
    private static void WriteCounters(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("WHAT IS BETWEEN A SHOPKEEPER AND THE FLOOR");
        Console.WriteLine();

        Core.World.WorldData world = WorldExporter.Export(rom);

        var shops = 0;
        var withNoWalkableNeighbour = 0;

        // The behaviour byte of every unwalkable square orthogonally beside a shopkeeper, and
        // the same for every unwalkable square beside ANYBODY, which is the control. A value
        // that is common around shopkeepers and equally common around everybody is scenery.
        var besideAShop = new Dictionary<byte, int>();
        var besideAnybody = new Dictionary<byte, int>();

        foreach (MapData map in world.Maps)
        {
            CollisionGrid grid = map.ToGrid();

            foreach (MapObject person in map.Objects)
            {
                var walkable = 0;

                foreach (GridPosition square in Around(person.Square))
                {
                    if (!grid.Contains(square)) continue;

                    if (grid.IsWalkable(square))
                    {
                        walkable++;

                        continue;
                    }

                    byte behaviour = map.BehaviourAt(square);

                    besideAnybody[behaviour] = besideAnybody.GetValueOrDefault(behaviour) + 1;

                    if (person.IsShopkeeper)
                        besideAShop[behaviour] = besideAShop.GetValueOrDefault(behaviour) + 1;
                }

                if (!person.IsShopkeeper) continue;

                shops++;

                if (walkable == 0) withNoWalkableNeighbour++;
            }
        }

        Console.WriteLine($"  {shops} shopkeeper(s) in the file, on {world.Maps.Count} map(s)");
        Console.WriteLine(
            $"  {withNoWalkableNeighbour} of them have NO walkable square beside them at all");

        if (besideAShop.Count == 0)
        {
            Console.WriteLine(
                "  and NOT ONE has an unwalkable square beside them — so there is nothing between"
                + " a shopkeeper and the floor, and the counter idea is wrong.");

            return;
        }

        int total = besideAShop.Values.Sum();
        int control = besideAnybody.Values.Sum();

        Console.WriteLine();
        Console.WriteLine(
            $"  the behaviour byte of the {total} unwalkable square(s) beside a shopkeeper,"
            + $" against the same byte beside ANY of the file's people ({control} square(s)):");

        foreach ((byte behaviour, int count) in besideAShop.OrderByDescending(p => p.Value).Take(8))
        {
            int anybody = besideAnybody.GetValueOrDefault(behaviour);

            // The share is the discriminator, not the count. A behaviour that is 90% of what
            // is beside a shopkeeper and 3% of what is beside everybody is a counter. One that
            // is 90% of both is a wall, and walls are beside everybody.
            Console.WriteLine(
                $"    0x{behaviour:X2}  {count,4} beside a shop ({100.0 * count / total,5:F1}%)"
                + $"   {anybody,5} beside anybody ({100.0 * anybody / control,5:F1}%)"
                + (behaviour == MetatileBehaviour.Normal ? "   ordinary ground" : string.Empty));
        }

        Console.WriteLine();
        Console.WriteLine(
            "  a value that is most of the first column and little of the second is a counter."
            + " One that is most of both is a wall, and walls stand beside everybody.");

        // AND THE SAME THING READ BY SHAPE, WHICH IS WHAT THIS PROJECT TRUSTS.
        //
        // A distribution can be enriched by accident. A counter has a shape and the shape is
        // the claim: somebody standing on one side of the square and floor a player can stand
        // on directly OPPOSITE. Nothing else in a building looks like that — a wall has wall
        // behind it, and a person against a wall has no floor on the far side.
        //
        // Asked of every square in the file carrying the top value, not of the ones beside a
        // shop, so the number has the whole file as its denominator. Printed with its own
        // control: the same shape counted for ordinary unwalkable ground.
        byte top = besideAShop.OrderByDescending(pair => pair.Value).First().Key;

        Console.WriteLine();
        Console.WriteLine($"  and the SHAPE of 0x{top:X2}, asked of every square in the file:");

        foreach (byte value in new[] { top, MetatileBehaviour.Normal })
        {
            var squares = 0;
            var sandwiched = 0;

            foreach (MapData map in world.Maps)
            {
                CollisionGrid grid = map.ToGrid();

                var people = map.Objects.Select(o => o.Square).ToHashSet();

                for (var y = 0; y < map.Height; y++)
                {
                    for (var x = 0; x < map.Width; x++)
                    {
                        var here = new GridPosition(x, y);

                        if (grid.IsWalkable(here)) continue;
                        if (map.BehaviourAt(here) != value) continue;

                        squares++;

                        // Somebody on one side, floor directly opposite. Both axes.
                        bool acrossY =
                            (people.Contains(here with { Y = y - 1 })
                                && grid.Contains(here with { Y = y + 1 })
                                && grid.IsWalkable(here with { Y = y + 1 }))
                            || (people.Contains(here with { Y = y + 1 })
                                && grid.Contains(here with { Y = y - 1 })
                                && grid.IsWalkable(here with { Y = y - 1 }));

                        bool acrossX =
                            (people.Contains(here with { X = x - 1 })
                                && grid.Contains(here with { X = x + 1 })
                                && grid.IsWalkable(here with { X = x + 1 }))
                            || (people.Contains(here with { X = x + 1 })
                                && grid.Contains(here with { X = x - 1 })
                                && grid.IsWalkable(here with { X = x - 1 }));

                        if (acrossY || acrossX) sandwiched++;
                    }
                }
            }

            Console.WriteLine(
                $"    0x{value:X2}  {squares,6} unwalkable square(s) in the world,"
                + $" {sandwiched,5} with somebody on one side and floor directly opposite"
                + $"  ({(squares == 0 ? 0 : 100.0 * sandwiched / squares),5:F1}%)"
                + (value == MetatileBehaviour.Normal ? "   <- the control" : string.Empty));
        }
    }

    /// <summary>The four squares orthogonally beside one.</summary>
    private static IEnumerable<GridPosition> Around(GridPosition at) =>
    [
        at with { Y = at.Y - 1 },
        at with { Y = at.Y + 1 },
        at with { X = at.X - 1 },
        at with { X = at.X + 1 },
    ];

    /// <summary>
    /// Prints how much of a count is one place rather than many. The rule is on
    /// <see cref="HowClustered"/>; only the wording is this file's business.
    /// </summary>
    private static void WriteHowClustered(Rom rom, IEnumerable<int> offsets)
    {
        List<int> at = [.. offsets];

        if (at.Count < 2) return;

        IReadOnlyList<Clump> clumps = HowClustered.In(rom, at);

        if (clumps.Count == 0)
        {
            Console.WriteLine(
                "    and no two of them are within a kilobyte of each other — so the count above"
                + " is that many separate facts about this file");

            return;
        }

        Console.WriteLine(
            $"    {clumps.Sum(c => c.Sites)} of them sit within a kilobyte of another — that is"
            + $" {clumps.Count} place(s), not {clumps.Sum(c => c.Sites)}. The whole-image error"
            + " bar assumes independent bytes and cannot model a clump; a run of table data"
            + " makes them all by itself.");

        foreach (Clump clump in clumps)
        {
            Console.WriteLine(
                $"      0x{clump.From:X6}..0x{clump.To:X6}  {clump.Sites} site(s) in"
                + $" {clump.To - clump.From + 3} byte(s), entropy {clump.Entropy:0.00} bits/byte"
                + (clump.LooksLikeATable ? "   <- table-like, not script" : string.Empty));
        }
    }

    private static void WriteEntries(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("DOORS INTO ONE ROOM");
        Console.WriteLine();

        MapLibrary library = MapLibrary.Open(rom);

        List<SetsAFlag> scripts = [.. library.All().SelectMany(EveryScriptOn)];

        IReadOnlyList<AnEntry> doors =
            EntriesToAScene.In(rom, scripts, HowAScriptRuns.FirstRemembered);

        IReadOnlyList<IGrouping<(string MapId, uint Leads), AnEntry>> rooms =
            EntriesToAScene.Rooms(doors);

        IReadOnlyList<IGrouping<uint, AnEntry>> shared = EntriesToAScene.SharedAcrossMaps(doors);

        Console.WriteLine(
            $"  {scripts.Count} script(s) the map scan opens, {doors.Count} of which do nothing but"
            + " hand over to another block");

        if (rooms.Count == 0)
        {
            Console.WriteLine(
                "  NO BLOCK IS ENTERED TWICE. Every handover in this cartridge goes somewhere of"
                + " its own, so nothing here is one scene written more than once.");

            return;
        }

        List<IGrouping<(string MapId, uint Leads), AnEntry>> oneScene =
            [.. rooms.Where(EntriesToAScene.IsOneSceneEnteredSeveralWays)];

        List<IGrouping<(string MapId, uint Leads), AnEntry>> aCrowd =
            [.. rooms.Where(r => !EntriesToAScene.IsOneSceneEnteredSeveralWays(r))];

        int extra = oneScene.Sum(r => r.Count() - 1);

        Console.WriteLine(
            $"  {rooms.Count} block(s) are entered by more than one door ON THE SAME MAP, by"
            + $" {rooms.Sum(r => r.Count())} doors between them");
        Console.WriteLine(
            $"    {oneScene.Count} of those are ONE SCENE ENTERED SEVERAL WAYS — every door says a"
            + $" different number, so it is announcing which one it is. {extra} of those runs are a"
            + " scene already played.");
        Console.WriteLine(
            $"    {aCrowd.Count} are several scripts that happen to share a block — the doors all say"
            + " the same thing, or say nothing, which is not announcing anything. A player takes"
            + " all of those.");
        Console.WriteLine(
            $"  the scratch variables they announce themselves in: "
            + string.Join(", ", doors.Where(d => d.Says >= 0).GroupBy(d => d.Into)
                .OrderByDescending(g => g.Count()).Take(4)
                .Select(g => $"0x{g.Key:X4} x{g.Count()}")));
        Console.WriteLine();

        foreach (IGrouping<(string MapId, uint Leads), AnEntry> room in oneScene.Take(10))
        {
            Console.WriteLine(
                $"    {room.Key.MapId} -> 0x{room.Key.Leads:X8} — {room.Count()} door(s), saying "
                + string.Join(", ", room.Select(d => d.Says).Order()));

            foreach (AnEntry door in room.OrderBy(d => d.Where.Address)) Console.WriteLine($"      {door}");
        }

        if (oneScene.Count > 10) Console.WriteLine($"    ... and {oneScene.Count - 10} more");

        // AND THE SHAPE THAT LOOKS IDENTICAL AND IS NOT.
        //
        // One nurse's script is attached to person 1 on nineteen Pokémon Centres. Grouped by
        // address alone the biggest room in this cartridge has twenty doors, and it is twenty
        // different people in twenty different towns that a player talks to one by one.
        // Printed here because anything keyed on a script address alone is wrong about it —
        // milestone 193's first version was, and it dropped seven walks in every eight.
        Console.WriteLine();
        Console.WriteLine(
            $"  and {shared.Count} block(s) are reached from more than one MAP — shared routines,"
            + " not repeated scenes, and a different thing entirely");

        foreach (IGrouping<uint, AnEntry> one in shared.Take(4))
        {
            Console.WriteLine(
                $"    0x{one.Key:X8} on {one.Select(d => d.Where.MapId).Distinct().Count()} map(s)"
                + $": {string.Join(", ", one.Select(d => d.Where.MapId).Distinct().Take(6))}, ...");
        }

        Console.WriteLine();
        Console.WriteLine(
            "  A player takes ONE door. Every walk here is a fixpoint that stands on every square");
        Console.WriteLine(
            "  and talks to everybody, so it takes all of them — and every number it reports per");
        Console.WriteLine(
            "  script is multiplied by however many doors that scene has. 193 found this in the");
        Console.WriteLine(
            "  walking, because people ended up in the wrong place. Nothing else had been asked.");
    }

    /// <summary>What a routine's answer is compared against, in one phrase.</summary>
    private static string Silence(SpecialCalls.WhatZeroDid what) =>
        what.Branches == 0
            ? "nothing branches on its answer"
            : $"compared against {string.Join(", ", what.Tested.Order())};"
              + $" nought takes {what.TakenByZero} of its {what.Branches} branch(es)"
              + $" at {what.PlacesTakenByZero} of {what.BranchPlaces} place(s) — {Meaning(what.Was)}";

    private static string Meaning(SpecialCalls.ZeroWas was) => was switch
    {
        SpecialCalls.ZeroWas.NeverTested => "nothing branches on the answer",
        SpecialCalls.ZeroWas.AnAssertion =>
            "NOUGHT TAKES EVERY BRANCH, so the run's silence decides it — it said yes",
        SpecialCalls.ZeroWas.ARefusal =>
            "nought takes no branch, so it falls through like any other wrong answer",
        _ => "nought takes some of the branches and not others",
    };

    /// <summary>
    /// The gates a run never opened, sorted by whether anything in the file could have opened
    /// them.
    /// <para>
    /// The rule is in <see cref="WhyTheGatesAreShut"/> rather than here, for the eighth time: a
    /// rule about the world inside this file is a rule nothing can fail.
    /// </para>
    /// </summary>
    private static void WriteWhyTheGatesAreShut(
        Rom rom, WorldData world, FlagGates gates, IReadOnlyCollection<int> set)
    {
        List<SetsAFlag> scripts = [.. MapLibrary.Open(rom).All().SelectMany(EveryScriptOn)];

        int[] covered = EverywhereInTheImage.Opened(rom, scripts);

        // What picking a thing up sets, read off the objects' own records. The cartridge does
        // it inside the standard routine that hands the thing over, which is compiled code —
        // only 7 of the 575 objects carrying a hide flag have a script that sets it.
        HashSet<int> onTheFloor =
        [
            .. world.Maps.SelectMany(m => m.Objects).Where(o => o.CanBeTakenAway).Select(o => o.HiddenBy),
        ];

        IReadOnlyList<AnObstacleGate> asked = GatesThatAreObstacles.In(rom, world);

        List<AnObstacleGate> obstacles = [.. asked.Where(g => g.Removed)];
        List<AnObstacleGate> staying = [.. asked.Where(g => !g.Removed)];

        IReadOnlyList<ShutGate> shut = WhyTheGatesAreShut.Of(
            gates,
            set,
            EverywhereInTheImage.EveryFlagMoved(rom, covered),
            onTheFloor,
            [.. obstacles.Select(g => g.Flag)]);

        if (shut.Count == 0) return;

        Console.WriteLine("      and why each of those is shut, asked of the WHOLE file:");

        if (asked.Count > 0)
        {
            List<MoveData> named = MoveExtractor.Extract(rom);

            string Move(int id) => id > 0 && id < named.Count ? named[id].Name : $"move {id}";

            string Moves(IEnumerable<AnObstacleGate> which) =>
                string.Join(
                    ", ",
                    which.SelectMany(g => g.Moves).Distinct().Order().Select(m => $"{Move(m)} ({m})"));

            Console.WriteLine(
                $"        {obstacles.Count} gating flag(s) in the world hold nothing but things"
                + $" asked about a move AND then taken off the map — {Moves(obstacles)} — between"
                + $" them running {obstacles.SelectMany(g => g.Scripts).Distinct().Count()} script(s)");
            Console.WriteLine(
                $"        and {staying.Count} hold something asked about a move and NEVER taken off"
                + $" it — {Moves(staying)} — which is a different mechanism and is left where it"
                + " fell rather than folded in");

            foreach (AnObstacleGate gate in staying)
            {
                Console.WriteLine(
                    $"          0x{gate.Flag:X4}  {Moves([gate]),-24}"
                    + $" {gates.Behind(gate.Flag).Count} object(s) —"
                    + $" {string.Join(", ", gates.Behind(gate.Flag).Take(4).Select(h => $"{h.MapId} p{h.LocalId}"))}");
            }
        }

        foreach ((ShutBecause why, int count) in WhyTheGatesAreShut.Counted(shut))
        {
            Console.WriteLine($"        {count,4}  {Because(why)}");
        }

        // The two that can never be walked to, named. The third bucket is the reach problem and
        // it is as long as the walk is short, so it is counted and not listed.
        foreach (ShutBecause why in new[] { ShutBecause.NothingSetsIt, ShutBecause.OnlyPastTheBoundary })
        {
            List<ShutGate> these = [.. shut.Where(g => g.Why == why)];

            if (these.Count == 0) continue;

            Console.WriteLine(
                $"        {Because(why)}: "
                + string.Join(
                    ", ",
                    these.Take(12).Select(g => $"0x{g.Flag:X4}" + (g.Sites > 0 ? $" ({g.Sites} setter(s))" : "")))
                + (these.Count > 12 ? $", +{these.Count - 12} more" : ""));
        }
    }

    private static string Because(ShutBecause why) => why switch
    {
        ShutBecause.NothingSetsIt => "no setflag names it and nothing on the floor hides behind it — the boundary",
        ShutBecause.OnlyPastTheBoundary => "set only where the map scan cannot see — past the code boundary",
        ShutBecause.TakenOffTheFloor => "set by PICKING SOMETHING UP — the object's record says so, no script does",
        ShutBecause.AnObstacle => "holds a TREE, A ROCK OR A BOULDER — cleared by knowing the move, not by a script",
        _ => "set by a script on a map, and the run never ran it — a REACH problem",
    };

    /// <summary>
    /// What the three coin commands count, how much of it fits, and what it buys.
    /// <para>
    /// <b>199 and 200 settled five widths and claimed nothing about any of them</b> — "the pair
    /// the GAME CORNER is built out of: the one that asks and the one that takes. What each
    /// does is NOT claimed here; only how wide it is." This prints the claim, and the number it
    /// turns on is written nowhere in the file: it is a bound plus a gift, at sites that agree
    /// on neither.
    /// </para>
    /// </summary>
    private static void WriteTheCoinCase(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("WHAT THE COIN COMMANDS COUNT, ASKED OF THE WHOLE FILE");
        Console.WriteLine();

        MapLibrary library = MapLibrary.Open(rom);

        List<SetsAFlag> scripts = [.. library.All().SelectMany(EveryScriptOn)];

        int[] covered = EverywhereInTheImage.Opened(rom, scripts);

        IReadOnlyList<TheCoinCase.Site> sites = TheCoinCase.Everywhere(rom, covered);

        List<TheCoinCase.Site> real = [.. sites.Where(s => s.ReadsAsScript)];

        (int floorSites, int floorReads, int floorPlaces) = TheCoinCase.NoiseFloor(rom);

        List<int> places = [.. real.Select(s => s.Offset)];

        int realPlaces = places.Count
                         - HowClustered.Clumped(rom, places)
                         + HowClustered.In(rom, places).Count;

        Console.WriteLine(
            $"  {sites.Count} site(s) in the file carry one of the three coin commands;"
            + $" {real.Count} of them read on to a proper end, {real.Count(s => s.Opened)} of those"
            + " the map scan opened");
        Console.WriteLine(
            $"  the same sweep on this file REVERSED finds {floorSites} site(s), {floorReads}"
            + $" reading on — {floorPlaces} place(s) against this file's {realPlaces}");
        Console.WriteLine(
            "    place(s) and not site(s) on both sides, because reversing a file preserves"
            + " clumping as well as frequency (206)");
        Console.WriteLine(
            realPlaces > floorPlaces && real.Count < floorReads
                ? "    THE TWO COMPARISONS DISAGREE ABOUT THE SIGN — behind the floor by site,"
                  + " ahead by place. Both are ways of saying the RAW SWEEP IS NOT A FINDING;"
                  + " the chains below are."
                : "    the raw sweep is a weak filter either way — what is worth reading below is"
                  + " the chain, not this count.");
        Console.WriteLine();

        foreach ((byte code, string what) in new[]
                 {
                     (TheCoinCase.HowMany, "reads the count into a variable"),
                     (TheCoinCase.HandOver, "adds to it"),
                     (TheCoinCase.TakeAway, "takes from it"),
                 })
        {
            Console.WriteLine(
                $"    0x{code:X2} {what,-32} {real.Count(s => s.Code == code),4} site(s) reading on");
        }

        // THE CEILING, WHICH IS THE WHOLE POINT.
        IReadOnlyList<TheCoinCase.Ceiling> ceilings = TheCoinCase.Ceilings(rom);

        Console.WriteLine();

        if (ceilings.Count == 0)
        {
            Console.WriteLine(
                "  NOTHING IN THE FILE READS THE COUNT, COMPARES IT AND HANDS SOME OVER — so there"
                + " is no capacity to derive, and this instrument has nothing to say about one.");
        }
        else
        {
            Console.WriteLine(
                $"  {ceilings.Count} place(s) read the count, compare it against a bound, branch,"
                + " and hand some over on the fall-through:");

            foreach (TheCoinCase.Ceiling c in ceilings.OrderBy(c => c.Offset))
            {
                Console.WriteLine(
                    $"    0x{Rom.BaseAddress + (uint)c.Offset:X8}  variable 0x{c.Variable:X4}"
                    + $"  bound {c.Bound,6}  gift {c.Gift,5}  ->  {c.Sum,6}"
                    + $"   {(c.Offset < covered.Length && covered[c.Offset] != EverywhereInTheImage.Nobody
                        ? "the map scan opened this"
                        : "PAST THE CODE BOUNDARY")}");
            }

            IReadOnlyList<(int Sum, int Sites, int DistinctPairs)> capacity =
                TheCoinCase.Capacity(ceilings);

            Console.WriteLine();

            if (capacity.Count == 1)
            {
                Console.WriteLine(
                    $"  EVERY ONE OF THEM SUMS TO {capacity[0].Sum} — from"
                    + $" {capacity[0].DistinctPairs} distinct (bound, gift) pair(s) at"
                    + $" {capacity[0].Sites} site(s).");
                Console.WriteLine(
                    "    a guard that refuses at the bound before adding the gift is a guard"
                    + " against passing bound + gift, so that number is the capacity — READ,"
                    + " and written nowhere in the file.");
            }
            else
            {
                Console.WriteLine(
                    $"  THE SUMS DISAGREE — {capacity.Count} different ones, so these are that many"
                    + " unrelated guards and there is no capacity here:");

                foreach ((int sum, int at, int pairs) in capacity)
                {
                    Console.WriteLine($"    {sum,8} at {at} site(s), {pairs} distinct pair(s)");
                }
            }

            // THE CONTROL, and it is the reversed image rather than a shuffle of the bounds
            // and gifts. A shuffle CANNOT come back agreeing — see TheCoinCase.CeilingFloor —
            // and a control with one outcome is not one.
            (int floorChains, int floorSums) = TheCoinCase.CeilingFloor(rom);

            Console.WriteLine();
            Console.WriteLine(
                $"  CONTROL — the same chain hunt on this file REVERSED finds {floorChains}"
                + $" chain(s)"
                + (floorChains == 0
                    ? ", so nothing with these byte statistics makes this shape by accident."
                    : $" summing to {floorSums} different number(s)"
                      + (floorSums == 1
                          ? " — WHICH IS THE SAME KIND OF AGREEMENT, so the one above is worth"
                            + " nothing. Read further before believing it."
                          : ", which scatter, so the agreement above is not what these bytes do"
                            + " by accident.")));
        }

        // MONEY IN, COINS OUT.
        IReadOnlyList<TheCoinCase.Exchange> exchanges = TheCoinCase.Exchanges(rom);

        Console.WriteLine();

        if (exchanges.Count == 0)
        {
            Console.WriteLine(
                "  NOTHING IN THE FILE ASKS AFTER MONEY, HANDS SOME OF THIS OVER AND THEN TAKES"
                + " THE MONEY — so nothing here says what one of these costs.");
        }
        else
        {
            Console.WriteLine($"  {exchanges.Count} place(s) sell them for money:");

            foreach (TheCoinCase.Exchange e in exchanges.OrderBy(e => e.Offset))
            {
                Console.WriteLine(
                    $"    0x{Rom.BaseAddress + (uint)e.Offset:X8}  asked {e.Asked,7}"
                    + $"  gave {e.Given,5}  took {e.Paid,7}"
                    + (e.Given > 0 && e.Paid % e.Given == 0
                        ? $"  ->  {e.Paid / e.Given} each"
                        : "  ->  not a whole number each"));
            }

            long[] rates =
            [
                .. exchanges.Where(e => e.Given > 0 && e.Paid % e.Given == 0)
                    .Select(e => e.Paid / e.Given).Distinct(),
            ];

            Console.WriteLine(
                rates.Length == 1
                    ? $"    one price at every place that sells them: {rates[0]} — READ"
                    : $"    {rates.Length} different prices, so there is no single one: "
                      + string.Join(", ", rates));
        }

        // WHAT IT BUYS.
        IReadOnlyList<TheCoinCase.PriceList> lists = TheCoinCase.PriceLists(rom);

        Console.WriteLine();

        if (lists.Count == 0)
        {
            Console.WriteLine(
                "  NO PRICE LIST IN THE FILE IS WRITTEN THIS WAY — two setvars and a shared door,"
                + " with the second variable one something subtracts from the count.");
        }
        else
        {
            List<ItemRecord> items = ItemTable.Locate(rom) is { } at ? ItemTable.Read(rom, at) : [];
            List<SpeciesData> species = RomExtractor.Open(rom).ExtractSpecies();

            Console.WriteLine(
                $"  {lists.Count} price list(s) written as script — rows of two setvars leaving by"
                + " one door, priced in the variable something subtracts:");

            foreach (TheCoinCase.PriceList list in lists)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"    0x{Rom.BaseAddress + (uint)list.Offset:X8}  thing in"
                    + $" 0x{list.ThingVariable:X4}, price in 0x{list.PriceVariable:X4}, all"
                    + $" leaving by 0x{list.SharedExit:X8} — {list.Rows.Count} row(s)");

                Console.WriteLine(
                    "      its door hands over: "
                    + (list.HandsOverItems, list.HandsOverCreatures) switch
                    {
                        (true, false) => "an ITEM — so the first column is read against the item table",
                        (false, true) =>
                            "a CREATURE — so the first column is read against the species table",
                        (true, true) =>
                            "BOTH an item and a creature — AMBIGUOUS, so both readings are printed",
                        _ => "NEITHER — nothing names these, so both readings are printed unclaimed",
                    });

                bool decided = list.HandsOverItems ^ list.HandsOverCreatures;

                foreach (TheCoinCase.PriceRow row in list.Rows)
                {
                    string asItem = items.FirstOrDefault(i => i.Id == row.Thing)?.Name ?? "-";
                    string asCreature = species.FirstOrDefault(s => s.Index == row.Thing)?.Name ?? "-";

                    Console.WriteLine(
                        $"      {row.Thing,5}  {row.Price,6}  "
                        + (decided
                            ? list.HandsOverItems ? asItem : asCreature
                            : $"as an item: {asItem,-14} as a creature: {asCreature}"));
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                "    WHICH TABLE A ROW IS READ AGAINST COMES OFF THE DOOR AND NOT OFF THE NUMBER.");
            Console.WriteLine(
                "    Every id in every list above is inside the item table AND inside the species");
            Console.WriteLine(
                "    table, so a reading that tries one and falls back to the other answers with");
            Console.WriteLine(
                "    whichever was tried first and never says it did. The first version of this");
            Console.WriteLine(
                "    printed five creatures as berries and mail, and looked exactly like this one.");
        }

        IReadOnlyList<(int Offset, int Held, int Price)> spends = TheCoinCase.Spends(rom);

        Console.WriteLine();
        Console.WriteLine(
            spends.Count == 0
                ? "  and nothing compares the count against a price and then subtracts it — the"
                  + " spending side is not in this file in that shape."
                : $"  {spends.Count} place(s) compare the count against a price and then subtract"
                  + $" it: {string.Join(", ", spends.Take(6)
                      .Select(s => $"0x{Rom.BaseAddress + (uint)s.Offset:X8} (0x{s.Held:X4} against 0x{s.Price:X4})"))}"
                  + (spends.Count > 6 ? ", ..." : ""));
    }

    /// <summary>
    /// Every place in the file that asks who knows a move, with the floor under it.
    /// <para>
    /// <b>The obstacle list is a fact about the maps, and it has been read as a fact about the
    /// game.</b> CUT, STRENGTH and ROCK SMASH were found because two hundred map objects open
    /// by naming them. Nothing on any map opens by naming the move that crosses water, and a
    /// scan that only opens maps says exactly what it would say if the move did not exist —
    /// which is trap one, and this is the instrument that stops it applying here.
    /// </para>
    /// </summary>
    private static void WriteWhoKnows(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("WHO KNOWS A MOVE, ASKED OF THE WHOLE FILE");
        Console.WriteLine();

        List<MoveData> moves = MoveExtractor.Extract(rom);

        MapLibrary library = MapLibrary.Open(rom);

        List<SetsAFlag> scripts = [.. library.All().SelectMany(EveryScriptOn)];

        int[] covered = EverywhereInTheImage.Opened(rom, scripts);

        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        IReadOnlyList<MoveSite> sites = EverywhereInTheImage.AsksWhoKnows(rom, moves.Count, covered);

        (int floor, int floorReads, int floorJumped, int floorPlaces) =
            EverywhereInTheImage.MoveNoiseFloor(rom, moves.Count);

        List<MoveSite> real = [.. sites.Where(s => s.ReadsAsAScript)];

        List<MoveSite> jumped =
        [
            .. real.Where(s => EverywhereInTheImage.WhoNames(rom, index, s.Address, 192).Any(n => n.AJump)),
        ];

        Console.WriteLine(
            $"  the move table this cartridge holds has {moves.Count} entries, which is the range"
            + " a plausible move id is read against");
        Console.WriteLine(
            $"  a three-byte pattern turns up by accident about {EverywhereInTheImage.ByChance(rom, 3):0.0}"
            + " time(s) in an image this size");
        Console.WriteLine();
        Console.WriteLine(
            $"  {sites.Count} site(s) read as \"who knows this move\", {real.Count} of them read on to a"
            + $" proper end, {jumped.Count} of those are jumped into");
        Console.WriteLine(
            $"  the same sweep on this file REVERSED finds {floor}, {floorReads} reading on,"
            + $" {floorJumped} jumped into — same bytes, same frequencies, NO COMMANDS");
        Console.WriteLine(
            "    if those two are the same number the list below is noise and the only honest"
            + " thing to do with it is throw it away");

        // AND HOW MUCH OF EITHER NUMBER IS ONE PLACE.
        //
        // 205 found this instrument's floor is a whole-image average computed as though every
        // byte were independent, and that a run of table data makes clumps all by itself —
        // 0x0089 showed nine sites against a floor of one and seven of them were inside 791
        // bytes of a table. The reversed-image control catches noise that has the same
        // FREQUENCIES; it cannot catch noise that has the same SHAPE, because reversing the
        // file leaves a table looking exactly as clumped as it was.
        //
        // So both halves get the same question asked of them, and the comparison that matters
        // is between the two clump counts rather than between the two totals.
        WriteHowClustered(rom, sites.Select(s => s.Offset));

        int clumped = HowClustered.Clumped(rom, sites.Select(s => s.Offset));
        int places = sites.Count - clumped + HowClustered.In(rom, sites.Select(s => s.Offset)).Count;

        Console.WriteLine(
            $"    SO THE COMPARISON IS {places} place(s) against the reversed image's"
            + $" {floorPlaces} — not {sites.Count} against {floor}."
            + (places > floorPlaces
                ? " Above the floor, and by less than the raw counts said."
                : " AT OR BELOW THE FLOOR: the raw counts said otherwise and they were counting"
                    + " clumps twice."));

        Console.WriteLine();

        Console.WriteLine($"  {real.Count(s => s.Opened)} of the {real.Count} the map scan opened; the rest it never did");
        Console.WriteLine();

        foreach (IGrouping<int, MoveSite> move in jumped
                     .GroupBy(s => s.Move)
                     .OrderByDescending(g => g.Count())
                     .ThenBy(g => g.Key))
        {
            string name = move.Key < moves.Count ? moves[move.Key].Name : "(past the table)";

            int opened = move.Count(s => s.Opened);

            Console.WriteLine(
                $"    move {move.Key,3} {name,-14} {move.Count(),2} site(s) jumped into, {opened} of them"
                + $" opened by the map scan{(opened == 0 ? "  <- NOTHING ON ANY MAP ASKS THIS" : "")}");

            foreach (MoveSite site in move.OrderBy(s => s.Offset).Take(4))
            {
                Console.WriteLine($"      0x{site.Address:X8}  {Where(site, scripts, covered)}");

                // AND WHETHER IT OFFERS ANYTHING. Three bytes turn up by accident; a block
                // that asks who knows a move, puts a yes-or-no on the screen and then does a
                // field effect is a scene, and the cartridge says in its own words what for.
                if (!site.Offers) continue;

                Console.WriteLine(
                    $"        offers it: field effect {site.FieldEffect}, and it says"
                    + $"  \"{OneLine(string.Join(
                        " ",
                        GameText.DecodeDialogue(rom.Span[(int)(site.Question - Rom.BaseAddress)..])))}\"");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  and the ones nothing opens, climbed:");

        foreach (MoveSite site in jumped.Where(s => !s.Opened).Take(8))
            Climb(rom, index, covered, scripts, new FlagSite(site.Offset, 0, true, true, false));

        static string OneLine(string said) =>
            string.Join(" ", said.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)).Trim();

        static string Where(MoveSite site, IReadOnlyList<SetsAFlag> scripts, int[] covered) =>
            site.Offset < covered.Length && covered[site.Offset] is var owner and not EverywhereInTheImage.Nobody
                ? $"{scripts[owner]}"
                : "nothing opens it";
    }

    /// <summary>
    /// Both exits of every <c>trainerbattle</c> in the image, and which of them holds a guard.
    /// <para>
    /// <b>Ranked by nothing.</b> There is no ranking here on purpose: the question is whether
    /// a shape exists at all, and a count of one kind against another is the whole answer. A
    /// column of fall-throughs that hold a conditional nothing else names is evidence that
    /// the runner's jump skips a guard the cartridge wrote; an empty column is evidence that
    /// it does not, and the reason for printing both.
    /// </para>
    /// </summary>
    private static void WriteFights(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("WHERE A BEATEN TRAINER CARRIES ON");
        Console.WriteLine();

        MapLibrary library = MapLibrary.Open(rom);

        IReadOnlyDictionary<uint, IReadOnlyList<int>> names = EverywhereInTheImage.PointerIndex(rom);

        IReadOnlyList<AFight> fights = WhatAFightLeadsTo.In(
            rom, library.All().SelectMany(EveryScriptOn), names);

        Console.WriteLine(
            $"  {fights.Count} trainerbattle(s) the map scan opens, across "
            + $"{fights.Select(f => f.MapId).Distinct().Count()} map(s)");
        Console.WriteLine(
            "  a beaten trainer resumes at ONE of two places, and this project has only ever read one:");
        Console.WriteLine(
            "    the JUMP    — the last pointer inside the command that reads like a script");
        Console.WriteLine(
            "    the AFTER   — the byte immediately following the command, which nothing reaches today");
        Console.WriteLine();

        foreach (IGrouping<byte, AFight> kind in fights.GroupBy(f => f.Variant).OrderBy(g => g.Key))
        {
            Console.WriteLine(
                $"  kind {kind.Key} — {kind.Count()} site(s), "
                + $"{kind.Count(f => f.Jump != 0)} of them with a script pointer to jump to");

            foreach (IGrouping<WhatFollows, AFight> shape in kind
                         .GroupBy(f => f.Follows)
                         .OrderByDescending(g => g.Count()))
            {
                int unnamed = shape.Count(f => f.NamedBy == 0);

                Console.WriteLine(
                    $"    the after reads as {Shape(shape.Key),-14} {shape.Count(),3} site(s)"
                    + $" — {unnamed} of them named by nothing else in the file");
            }

            // The ones that matter, if any: a guard on the fall-through, a jump that never
            // comes back to it, and no other way in. Printed with the addresses so the next
            // person reads the bytes rather than this sentence.
            List<AFight> skipped =
            [
                .. kind.Where(f => f.Follows == WhatFollows.AGuard && f.Jump != 0 && !f.JumpRejoins),
            ];

            if (skipped.Count == 0)
            {
                Console.WriteLine("    NOTHING OF THIS KIND SKIPS A GUARD — the two exits agree here");

                continue;
            }

            Console.WriteLine(
                $"    {skipped.Count} of them SKIP A GUARD: the after is a conditional, and the jump");
            Console.WriteLine(
                "    never arrives at it. Under the jump reading those bytes are unreachable.");

            foreach (AFight one in skipped.Take(12))
            {
                Console.WriteLine(
                    $"      {one.MapId,-8} {one.Who,-22} at 0x{one.At + Rom.BaseAddress:X8}"
                    + $"  after 0x{one.After:X8} (named by {one.NamedBy})  jump 0x{one.Jump:X8}");
            }

            if (skipped.Count > 12) Console.WriteLine($"      ... and {skipped.Count - 12} more");
        }

        static string Shape(WhatFollows what) => what switch
        {
            WhatFollows.AGuard => "A GUARD",
            WhatFollows.JustALine => "just a line",
            WhatFollows.NothingAtAll => "nothing at all",
            _ => "not commands",
        };
    }

    /// <summary>
    /// The attributions 214 stopped making, made one level in.
    /// <para>
    /// Adding <c>call</c> to the answer scan's barrier list lost 42 of 1097 attributions and
    /// that was the right way to be wrong. The answers are still there and they belong to
    /// somebody; this says who, and how often nothing inside the call answers at all — which is
    /// the case where the compare is reading something older still and neither reading is
    /// right.
    /// </para>
    /// </summary>
    private static void WriteThroughACall(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("WHAT ANSWERED, ONE LEVEL IN");
        Console.WriteLine();

        List<SpecialCalls.AnsweredThroughACall> found =
            SpecialCalls.ThroughACall(rom, MapLibrary.Open(rom));

        List<SpecialCalls.AnsweredThroughACall> answered =
            [.. found.Where(a => a.Left == SpecialCalls.LeftBehind.ARoutine)];

        Console.WriteLine(
            $"  {found.Count} place(s) call a block and compare the answer variable straight after");

        foreach (SpecialCalls.LeftBehind left in Enum.GetValues<SpecialCalls.LeftBehind>())
        {
            int howMany = found.Count(a => a.Left == left);

            if (howMany == 0) continue;

            Console.WriteLine($"    {howMany,4} leave {Left(left)}");
        }

        if (found.Count == 0)
        {
            Console.WriteLine(
                "    nothing in this file reads an answer through a call, so the barrier the"
                + " scan added costs it nothing.");

            return;
        }

        Console.WriteLine();

        foreach (IGrouping<int, SpecialCalls.AnsweredThroughACall> byRoutine in answered
                     .GroupBy(a => a.Answerer)
                     .OrderByDescending(g => g.Count()))
        {
            Console.WriteLine(
                $"    routine 0x{byRoutine.Key:X3} answers at {byRoutine.Count()} place(s) through"
                + $" {byRoutine.Select(a => a.Called).Distinct().Count()} block(s)"
                + $" — e.g. {byRoutine.First().MapId} {byRoutine.First().What}"
                + $" calls 0x{byRoutine.First().Called:X8} and compares against"
                + $" {byRoutine.First().Value}");
        }

        // AND THE ONES WHOSE CALL TOUCHES NOTHING, walked back to whatever answered before it.
        //
        // A call that leaves the answer variable as it found it means the compare after it is
        // reading something older, so the older answer is the right attribution — the barrier
        // 214 added stops the scan guessing, and this is the case where it does not have to.
        List<SpecialCalls.AnsweredThroughACall> untouched =
            [.. found.Where(a => a.Left == SpecialCalls.LeftBehind.Nothing)];

        if (untouched.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  {untouched.Count} place(s) call a block that leaves the answer variable"
                + " alone, so the compare is reading something older. Walking back in the caller:");

            foreach (IGrouping<(SpecialCalls.LeftBehind, int), SpecialCalls.AnsweredThroughACall> what in
                     untouched.GroupBy(a => (a.Before, a.Older)).OrderByDescending(g => g.Count()))
            {
                Console.WriteLine(
                    $"      {what.Count(),4} — {Outcome(what.Key)}"
                    + $"   e.g. {what.First().MapId} {what.First().What}"
                    + $" at 0x{what.First().Through:X8}");
            }
        }

        List<SpecialCalls.AnsweredThroughACall> onTheLine =
            [.. found.Where(a => a.Left == SpecialCalls.LeftBehind.ANumberOnTheStraightLine)];

        if (onTheLine.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  {onTheLine.Count} place(s) call a block whose STRAIGHT LINE ends by saying the"
                + $" answer out loud — {string.Join(", ", onTheLine.Select(a => a.Answerer).Distinct().Order())}"
                + " — but an arm of the same block asks a routine, so they are NOT constants.");
            Console.WriteLine("    Read one level of arms, those blocks return:");

            foreach (IGrouping<uint, SpecialCalls.AnsweredThroughACall> block in onTheLine
                         .GroupBy(a => a.Called)
                         .OrderByDescending(g => g.Count()))
            {
                SpecialCalls.WhatItCanReturn can = SpecialCalls.Returns(rom, block.Key);

                Console.WriteLine(
                    $"      0x{block.Key:X8} at {block.Count(),3} place(s) — leaves"
                    + $" {string.Join(" or ", can.Answers.Select(Outcome))}"
                    + (can.Deciders.Count == 0
                        ? "; nothing on its straight line chooses"
                        : $"; the choice turns on {string.Join(", ", can.Deciders.Select(d => $"0x{d:X3}"))}"));
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"  {found.Select(a => a.Called).Distinct().Count()} distinct block(s) are called this"
            + $" way, from {found.Select(a => a.MapId).Distinct().Count()} map(s)");
    }

    private static string Outcome((SpecialCalls.LeftBehind Left, int Who) answer) =>
        answer.Left switch
        {
            SpecialCalls.LeftBehind.ARoutine => $"routine 0x{answer.Who:X3}'s answer",
            SpecialCalls.LeftBehind.ANumber => $"{answer.Who}",
            SpecialCalls.LeftBehind.ANumberOnTheStraightLine => $"{answer.Who}",
            SpecialCalls.LeftBehind.AnotherVariable => $"variable 0x{answer.Who:X4}",
            SpecialCalls.LeftBehind.WentSomewhereElse => "whatever is left by a jump not followed here",
            _ => "whatever was already there",
        };

    private static string Left(SpecialCalls.LeftBehind left) => left switch
    {
        SpecialCalls.LeftBehind.ARoutine => "a routine's answer",
        SpecialCalls.LeftBehind.ANumber => "a number, and nothing in the block asks anything — a constant",
        SpecialCalls.LeftBehind.ANumberOnTheStraightLine =>
            "a number on the straight line, but an arm of the block asks a routine",
        SpecialCalls.LeftBehind.AnotherVariable => "another variable's contents, not followed here",
        SpecialCalls.LeftBehind.WentSomewhereElse =>
            "nothing, and the block ends by jumping somewhere this reading does not follow",
        _ => "NOTHING — the compare reads whatever was there before the call",
    };

    /// <summary>
    /// Every place in the whole image that LOOKS at a variable — <c>--who-writes</c>'s mirror.
    /// <para>
    /// <b>This project has had one side of this since 184.</b> "Nothing sets this" has been
    /// askable for eleven milestones and "nothing reads this" has not, so a variable written
    /// once and never looked at has read exactly like a variable that gates something. 214's
    /// last piece of ceiling turned out to be one of the first kind and it took a hand-grep to
    /// say so.
    /// </para>
    /// </summary>
    private static void WriteWhoReads(Rom rom, IReadOnlyList<int> variables)
    {
        Console.WriteLine();
        Console.WriteLine("WHO READS THESE");
        Console.WriteLine();

        MapLibrary library = MapLibrary.Open(rom);

        List<SetsAFlag> scripts = [.. library.All().SelectMany(EveryScriptOn)];

        int[] covered = EverywhereInTheImage.Opened(rom, scripts);

        Console.WriteLine(
            $"  a three-byte pattern turns up by accident about {EverywhereInTheImage.ByChance(rom, 3):0.0}"
            + " time(s) in an image this size, per command — which is the error bar below");

        // THE PAIR, WHICH IS THE FINDING — a variable a hundred places write and nobody reads
        // is not a story counter however busy it looks.
        IReadOnlyDictionary<int, int> written = EverywhereInTheImage.EveryVariableWritten(rom);
        IReadOnlyDictionary<int, int> looked = EverywhereInTheImage.EveryVariableRead(rom);

        // THE BAND, BECAUSE THE RAW NUMBERS ARE NOISE.
        //
        // Swept across all sixteen megabytes these come back 5039 written and 9997 read, and
        // almost all of that is compiled code that happens to decode. "Reads as script" is a
        // weak filter and this project has thrown away a whole-file count for exactly this
        // reason before. The save's own variables are 0x4000 upwards; the raw figure is printed
        // beside the banded one rather than instead of it, and it is not a finding.
        bool TheSaves(int v) => v is >= 0x4000 and < 0x8000;

        List<int> deaf = [.. written.Keys.Where(v => TheSaves(v) && !looked.ContainsKey(v)).Order()];

        (int floorWritten, int floorRead, int floorNever) =
            EverywhereInTheImage.NeverReadFloor(rom, TheSaves);

        Console.WriteLine(
            $"  raw, across the whole image: {written.Count} variable(s) written and"
            + $" {looked.Count} read — which is mostly compiled code that happens to decode");
        Console.WriteLine(
            $"  in the save's own band (0x4000-0x7FFF): {written.Count(v => TheSaves(v.Key))}"
            + $" written, {looked.Count(v => TheSaves(v.Key))} read, {deaf.Count} written and"
            + " never read");
        Console.WriteLine(
            $"    the same band on this file REVERSED: {floorWritten} written, {floorRead} read,"
            + $" {floorNever} never read");
        Console.WriteLine(
            floorNever * 2 > deaf.Count
                ? "    WHICH IS THE SAME ORDER OF NUMBER, so the aggregate is what these bytes do"
                  + " by accident. Only the per-variable answers below are worth anything."
                : "    which is far short of it, so the aggregate is above the floor");

        if (deaf.Count > 0)
        {
            Console.WriteLine(
                "    written and never read: "
                + string.Join(", ", deaf.Take(16).Select(v => $"0x{v:X4} x{written[v]}"))
                + (deaf.Count > 16 ? $", +{deaf.Count - 16} more" : ""));
        }

        foreach (int which in variables)
        {
            IReadOnlyList<VariableSite> sites = EverywhereInTheImage.Reads(rom, which, covered);
            IReadOnlyList<VariableSite> writes = EverywhereInTheImage.Writes(rom, which, covered);

            List<VariableSite> real = [.. sites.Where(s => s.ReadsAsAScript)];

            (int floor, int floorReads, int floorPlaces) =
                EverywhereInTheImage.ReadNoiseFloor(rom, which);

            Console.WriteLine();
            Console.WriteLine(
                $"  0x{which:X4} — {sites.Count} site(s) look at it, {real.Count} of them read as"
                + $" script, {real.Count(s => s.Opened)} of those the map scan opened");
            Console.WriteLine(
                $"    the same sweep on this file REVERSED finds {floor} site(s), {floorReads}"
                + $" reading as script, {floorPlaces} place(s)");
            Console.WriteLine(
                $"    and {writes.Count(s => s.ReadsAsAScript)} place(s) write it"
                + $" ({writes.Count} raw)");

            if (real.Count == 0)
            {
                Console.WriteLine(
                    "    NOTHING IN THE FILE LOOKS AT IT. Whatever is put in it is put there and"
                    + " never asked about — by any script, anywhere in sixteen megabytes.");
            }

            foreach (VariableSite site in real.Take(16))
            {
                Console.WriteLine(
                    $"      0x{site.Offset:X6}  {ScriptCommands.NameOf(site.How)}"
                    + $"  other operand 0x{site.Value:X4}"
                    + (site.Opened ? "  the map scan opened this" : "  NEVER OPENED BY THE MAP SCAN"));
            }

            if (real.Count > 16) Console.WriteLine($"      ... and {real.Count - 16} more");
        }
    }

    private static void WriteWhoWrites(Rom rom, IReadOnlyList<int> variables)
    {
        Console.WriteLine();
        Console.WriteLine("WHO WRITES THESE");
        Console.WriteLine();

        MapLibrary library = MapLibrary.Open(rom);

        List<SetsAFlag> scripts = [.. library.All().SelectMany(EveryScriptOn)];

        int[] covered = EverywhereInTheImage.Opened(rom, scripts);

        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        Console.WriteLine(
            $"  a three-byte pattern turns up by accident about {EverywhereInTheImage.ByChance(rom, 3):0.0} "
            + "time(s) in an image this size, per command — which is the error bar below");

        // THE SHAPE OF THE WHOLE SET, BEFORE ANY ONE OF THEM.
        //
        // A variable written by nine places holding nine different numbers is a story counter.
        // One written by three hundred is a scratch pad — milestone 173 established that about
        // 0x4001 by counting, and this is the same count taken across every variable at once,
        // so the line between the two kinds can be looked at instead of assumed.
        IReadOnlyDictionary<int, int> everything = EverywhereInTheImage.EveryVariableWritten(rom);

        Console.WriteLine(
            $"  {everything.Count} variable(s) are written somewhere in the file, "
            + $"{everything.Values.Sum()} site(s) between them");
        Console.WriteLine(
            "    the busiest, which are the scratch pads: "
            + string.Join(", ", everything.OrderByDescending(v => v.Value).Take(6)
                .Select(v => $"0x{v.Key:X4} x{v.Value}")));

        foreach (IGrouping<string, KeyValuePair<int, int>> band in everything
                     .Where(v => v.Key is >= 0x4000 and < 0x8000)
                     .GroupBy(v => $"0x{v.Key & 0xFFF0:X4}")
                     .OrderBy(g => g.Key)
                     .Take(8))
        {
            Console.WriteLine(
                $"    {band.Key}s: {band.Count()} variable(s), busiest "
                + $"x{band.Max(v => v.Value)}, quietest x{band.Min(v => v.Value)}");
        }

        foreach (int which in variables)
        {
            IReadOnlyList<VariableSite> sites = EverywhereInTheImage.Writes(rom, which, covered);

            List<VariableSite> real = [.. sites.Where(s => s.ReadsAsAScript)];

            Console.WriteLine();
            Console.WriteLine(
                $"  0x{which:X4} — {sites.Count} site(s) in the file, {real.Count} of which read as"
                + $" script, {real.Count(s => s.Opened)} of which the map scan opened");

            if (real.Count == 0)
            {
                Console.WriteLine(
                    "    NOTHING IN THE FILE PUTS A NUMBER IN IT. Whatever moves this variable is");
                Console.WriteLine(
                    "    compiled code, and no reading of scripts will ever find it.");

                continue;
            }

            foreach (IGrouping<int, VariableSite> value in real
                         .GroupBy(s => s.Copies ? -1 : s.Value)
                         .OrderBy(g => g.Key))
            {
                Console.WriteLine(
                    $"    = {(value.Key < 0 ? "copied" : value.Key.ToString())}: {value.Count()} site(s)"
                    + $", {value.Count(s => s.Opened)} opened — "
                    + string.Join(", ", value.Take(3).Select(s => Where(s, scripts, covered))));
            }
        }

        Console.WriteLine();
        Console.WriteLine("  and the ones nothing opens, climbed:");

        foreach (int which in variables)
        {
            foreach (VariableSite site in EverywhereInTheImage.Writes(rom, which, covered)
                         .Where(s => s.ReadsAsAScript && !s.Opened)
                         .Take(6))
            {
                Climb(rom, index, covered, scripts, new FlagSite(site.Offset, 0, true, true, false));
            }
        }

        static string Where(VariableSite site, IReadOnlyList<SetsAFlag> scripts, int[] covered) =>
            site.Offset < covered.Length && covered[site.Offset] is var owner and not EverywhereInTheImage.Nobody
                ? $"0x{site.Offset:X6} ({scripts[owner]})"
                : $"0x{site.Offset:X6} (nothing opens it)";
    }

    /// <summary>
    /// The climb, on any address at all.
    /// <para>
    /// It was reachable only by naming a flag, and the question it answers is not about flags:
    /// <em>who runs this block?</em> is worth asking of a block whatever happens to be inside
    /// it. Written the third time it was wanted and improvised around.
    /// </para>
    /// </summary>
    private static void WriteClimb(Rom rom, IReadOnlyList<uint> addresses)
    {
        Console.WriteLine();
        Console.WriteLine("CLIMBING");
        Console.WriteLine();

        MapLibrary library = MapLibrary.Open(rom);

        List<SetsAFlag> scripts = [.. library.All().SelectMany(EveryScriptOn)];

        int[] covered = EverywhereInTheImage.Opened(rom, scripts);

        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        foreach (uint address in addresses)
        {
            int offset = rom.ToOffsetOrNull(address) ?? 0;

            Climb(
                rom,
                index,
                covered,
                scripts,
                new FlagSite(
                    offset, 0, true, true, offset < covered.Length && covered[offset] != EverywhereInTheImage.Nobody));
        }
    }

    /// <summary>
    /// What names this address, and what names that, until it reaches the world or nothing.
    /// <para>
    /// A block is jumped into at its first command, and the flag being hunted is usually some
    /// way inside it — so the search is for anything naming an address in the bytes just above,
    /// not for the address itself. Asking for the exact address answers "nothing" correctly and
    /// uselessly.
    /// </para>
    /// </summary>
    private static void Climb(
        Rom rom,
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index,
        int[] covered,
        IReadOnlyList<SetsAFlag> scripts,
        FlagSite site,
        int slack = 192,
        int maxSteps = 24,
        int mostPerStep = 24)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"    climbing from {(site.Flag == 0 ? $"0x{site.Offset:X6}" : site.ToString())} at 0x{site.Address:X8}"
            + (site.Opened ? $" — opened by {Owner(site.Offset)}" : " — opened by nothing"));

        var seen = new HashSet<uint>();
        var queue = new Queue<(uint Address, int Depth)>();

        queue.Enqueue((site.Address, 0));

        var steps = 0;
        var waysIn = 0;
        var literals = 0;

        while (queue.Count > 0 && steps++ < maxSteps)
        {
            (uint address, int depth) = queue.Dequeue();

            if (!seen.Add(address)) continue;

            IReadOnlyList<NamesIt> names = EverywhereInTheImage.WhoNames(rom, index, address, slack);

            string pad = new(' ', 6 + (depth * 2));

            if (names.Count == 0)
            {
                Console.WriteLine(
                    $"{pad}NOTHING IN THE FILE NAMES 0x{address:X8} or the {slack} bytes above it");

                continue;
            }

            foreach (NamesIt named in names.Take(mostPerStep))
            {
                bool opened = named.Offset < covered.Length
                    && covered[named.Offset] != EverywhereInTheImage.Nobody;

                Console.WriteLine(
                    $"{pad}{named}"
                    + (named.AJump
                        ? opened
                            ? $"  <- A WAY IN: {Owner(named.Offset)}"
                            : "  <- a jump nothing opens"
                        : named.ALiteral ? "  <- a literal: only code reads one" : "  <- loose bytes, probably noise"));

                if (named.AJump && opened) waysIn++;

                // And what a literal sits among. One address in compiled code is a call site;
                // a run of them on four-byte boundaries is a TABLE, and which entry this is
                // says what selects it. The difference matters and costs one line to print.
                if (named.ALiteral)
                {
                    literals++;

                    Console.WriteLine($"{pad}  {Neighbours(named.Offset)}");
                }

                // Climb on from the command that carries the pointer, not from the pointer:
                // what names this block is what names the command that jumps out of it.
                if (named.AJump && !opened && depth + 1 < 6)
                {
                    uint command = Rom.BaseAddress + (uint)named.Offset;

                    queue.Enqueue((command, depth + 1));
                }
            }

            if (names.Count > mostPerStep)
                Console.WriteLine($"{pad}... and {names.Count - mostPerStep} more naming it");
        }

        Console.WriteLine(
            waysIn > 0
                ? $"      {waysIn} way(s) in, all named above. If they are all disqualified, whatever"
                    + " enters this block with a third answer is not a script jump."
                : literals > 0
                    ? "      NOTHING JUMPS HERE. What names it is a literal, which only compiled code "
                        + "reads — so this is run from the far side of the code boundary, and the "
                        + "offsets above are where to look."
                    : "      NOTHING JUMPS HERE, and nothing names it at all.");

        // Which script first decoded a byte, in the words the rest of this tool uses.
        string Owner(int offset) =>
            offset < covered.Length && covered[offset] is var which and not EverywhereInTheImage.Nobody
                ? scripts[which].ToString()
                : "nothing";

        // The aligned words either side of a literal, so a table reads as a table.
        string Neighbours(int offset)
        {
            var around = new List<string>();

            for (int at = offset - 12; at <= offset + 12; at += 4)
            {
                if (at < 0 || at + 4 > rom.Length) continue;

                uint word = rom.ReadU32(at);

                around.Add(
                    at == offset ? $"[0x{word:X8}]"
                    : rom.IsRomAddress(word) ? $"0x{word:X8}"
                    : "--------");
            }

            return "in context: " + string.Join(" ", around);
        }
    }

    /// <summary>
    /// The code boundary, asked of the file instead of of the world.
    /// <para>
    /// <b>The boundary has always been a sentence about the scripts the maps reach.</b> "Two
    /// hundred and forty-eight flags gate somebody and no script moves them" is measured by
    /// gathering every script a map points at, following the jumps, and finding no
    /// <c>setflag</c>. That reading cannot tell a flag moved by nothing from a flag moved by a
    /// piece of script nothing leads to, and it does not fail when asked — it prints the same
    /// line either way, which is how this thread lost three rounds in one session.
    /// </para>
    /// <para>
    /// So the same question, put to all sixteen megabytes: is there a <c>setflag</c> or a
    /// <c>clearflag</c> for this flag <em>anywhere</em>, in bytes any map opens or not? The
    /// split it produces is two different jobs. A flag moved in unopened script is an entry
    /// point to find. A flag moved nowhere in the file is compiled code writing the array, and
    /// no amount of reading scripts will ever reach it.
    /// </para>
    /// </summary>
    private static void WriteTheBoundaryAgainstTheFile(
        Rom rom,
        MapLibrary library,
        FlagGates gates,
        IReadOnlyCollection<int> turnedOn,
        IReadOnlyCollection<int> turnedOff)
    {
        int[] covered = EverywhereInTheImage.Opened(rom, [.. library.All().SelectMany(EveryScriptOn)]);

        IReadOnlyDictionary<int, IReadOnlyList<FlagSite>> moved =
            EverywhereInTheImage.EveryFlagMoved(rom, covered);

        var world = new HashSet<int>(turnedOn.Concat(turnedOff));

        List<int> boundary = [.. gates.All.Select(g => g.Flag).Where(f => !world.Contains(f))];

        int decoded = covered.Count(b => b != EverywhereInTheImage.Nobody);

        int sites = moved.Values.Sum(s => s.Count);
        (int noiseSites, int noiseJumped, int noisePlaces) = EverywhereInTheImage.NoiseFloor(rom);

        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        IReadOnlyList<EverywhereInTheImage.OutsideTheWorld> outside =
            EverywhereInTheImage.PastTheBoundary(rom, index, boundary, moved);

        int jumpedInto = outside.Count(f => f.JumpedInto.Count > 0);

        Console.WriteLine();
        Console.WriteLine("  AND THE SAME QUESTION PUT TO THE WHOLE FILE");
        Console.WriteLine();
        Console.WriteLine(
            $"    the scripts above decode {decoded} of this file's {rom.Length} bytes "
            + $"({100.0 * decoded / rom.Length:0.0}%) — every line before this one is about that much of it");
        Console.WriteLine(
            $"    {sites} site(s) in the file read as a setflag or clearflag, moving {moved.Count} flag(s)");
        Console.WriteLine(
            $"    the same sweep on this file REVERSED finds {noiseSites}, {noiseJumped} of them jumped into —");
        Console.WriteLine(
            "    same bytes, same frequencies, no commands. That is what these two filters find when");
        Console.WriteLine(
            "    there is nothing there, and it is what the counts below have to be read against.");

        // AND BOTH SIDES ASKED HOW CLUMPED THEY ARE.
        //
        // Reversing the file preserves frequencies AND shape: a table reversed is still a
        // table and still clumps exactly as hard. So this control has never been able to see
        // the failure mode 205 found, and the raw comparison counts clumps twice on both
        // sides. The place-level comparison is the one that means anything.
        List<int> allAt = [.. moved.Values.SelectMany(v => v).Select(f => f.Offset)];

        WriteHowClustered(rom, allAt);

        int clumpedHere = HowClustered.Clumped(rom, allAt);

        int placesHere =
            allAt.Count - clumpedHere + HowClustered.In(rom, allAt).Count;

        Console.WriteLine(
            $"    SO THE COMPARISON IS {placesHere} place(s) against the reversed image's"
            + $" {noisePlaces} — not {sites} against {noiseSites}."
            + (placesHere > noisePlaces
                ? $" The real image is ahead by {100.0 * (placesHere - noisePlaces) / noisePlaces:0.0}%."
                : $" The real image is BEHIND by {100.0 * (noisePlaces - placesHere) / noisePlaces:0.0}%."));

        Console.WriteLine(
            "    NOTE THAT THE TWO COMPARISONS DISAGREE ABOUT THE SIGN, and neither margin is"
            + " large. Counting sites, this file is behind its own reversal; counting places, it"
            + " is ahead. Both are ways of saying the same thing — the raw sweep is not a"
            + " finding — and the jumped-into rates below are still the only part clearly above"
            + " anything.");

        int unopenedSites = outside.Sum(f => f.Unopened.Count);
        int jumpedSites = outside.Sum(f => f.JumpedInto.Count);

        // AND IT HAS TO BE THE SAME UNIT ON BOTH SIDES.
        //
        // This printed a count of flags beside a count of sites and invited them to be compared.
        // They cannot be: one flag can hold a dozen sites, and the first run of this put "20"
        // next to "47" as though 20 were the smaller number.
        Console.WriteLine();
        Console.WriteLine(
            $"    a site something jumps into is {Rate(jumpedSites, unopenedSites)} of the unopened sites "
            + $"here, against {Rate(noiseJumped, noiseSites)} in the reversal");
        Console.WriteLine(
            "    — and if those two are the same number, the jumped-into list below is noise and");
        Console.WriteLine(
            "    the only honest thing to do with it is throw it away.");
        Console.WriteLine();
        Console.WriteLine(
            $"    of the {boundary.Count} gating flags nothing in the world moves:");
        Console.WriteLine(
            $"      {outside.Count} are moved by something reading as script that the maps never open "
            + $"({unopenedSites} site(s))");
        Console.WriteLine(
            $"        {jumpedInto} of those are jumped into by a script ({jumpedSites} site(s)) — an entry point to find");
        Console.WriteLine(
            $"      {boundary.Count - outside.Count} are moved by no script anywhere in the file — compiled code, and");
        Console.WriteLine(
            "        unreachable by reading scripts however many are opened");

        foreach (EverywhereInTheImage.OutsideTheWorld flag in outside.Take(20))
        {
            IReadOnlyList<FlagSite> worst = flag.JumpedInto.Count > 0 ? flag.JumpedInto : flag.Unopened;

            Console.WriteLine(
                $"      0x{flag.Flag:X4}  {flag.Unopened.Count} site(s) nothing opens, "
                + $"{flag.JumpedInto.Count} of them jumped into — "
                + string.Join(", ", worst.Take(3).Select(s => $"0x{s.Offset:X6} {(s.Sets ? "set" : "clear")}")));
        }

        if (outside.Count > 20) Console.WriteLine($"      ... and {outside.Count - 20} more");

        static string Rate(int some, int all) => all == 0 ? "none" : $"{100.0 * some / all:0.0}%";

        Console.WriteLine();
        Console.WriteLine(
            "    --in-the-image 0xNNNN[,0xNNNN] climbs any of them: what names the site, what");
        Console.WriteLine(
            "    names that, until it reaches something a map opens or reaches a literal.");
    }

    private static void WriteFlagGates(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("WHAT EACH FLAG GATES");
        Console.WriteLine();

        MapLibrary library = MapLibrary.Open(rom);
        WorldData world = WorldExporter.Export(rom);

        var gates = new FlagGates(world);

        var hiding = new Dictionary<int, List<string>>();

        foreach (MapData map in world.Maps)
        {
            foreach (MapObject person in map.Objects.Where(o => o.HiddenBy != 0))
            {
                if (!hiding.TryGetValue(person.HiddenBy, out List<string>? where))
                    hiding[person.HiddenBy] = where = [];

                where.Add($"{map.Id} person {person.LocalId}");
            }
        }

        Console.WriteLine($"  {gates.Count} flags gate something in this world file");

        foreach ((FlagGate kind, int count) in gates.Counted.OrderByDescending(p => p.Value))
            Console.WriteLine($"    {count,4} gate {Describe(kind)}");

        Console.WriteLine();
        Console.WriteLine("  the first 40 of them, and what each moves");

        foreach ((int flag, FlagGate kind) in gates.All.Take(40))
        {
            string what = hiding.TryGetValue(flag, out List<string>? where)
                ? $"{where.Count} person(s) — {string.Join(", ", where.Take(3))}"
                    + (where.Count > 3 ? $", +{where.Count - 3} more" : "")
                : Describe(kind);

            Console.WriteLine($"    0x{flag:X4}  {what}");
        }

        if (gates.All.Count > 40)
        {
            Console.WriteLine(
                $"    ... and {gates.All.Count - 40} more — the ranked list below is the useful half");
        }

        // And the other half, which is the half that decides the rule: how many flags the
        // scripts touch that gate nothing here. Those are the marks on a character — a badge,
        // which starter was taken — and they are the ones that must NOT travel.
        // Read twice on purpose. The plain reading walks both arms of every branch, including
        // ones the script has already decided against; the second honours them, which is what
        // running means. The difference between the two is a list of flags that look moved and
        // are not — and it is exactly where SAFFRON was hiding.
        (IReadOnlyCollection<int> looksMoved, IReadOnlyCollection<int> looksCleared) =
            WhatItIsWaitingFor.Touches(rom, library.All().SelectMany(EveryScriptOn));

        (IReadOnlyCollection<int> turnedOn, IReadOnlyCollection<int> turnedOff) =
            WhatItIsWaitingFor.ReallyTouches(rom, library.All().SelectMany(EveryScriptOn), out int ranOut);

        var touched = new HashSet<int>(turnedOn.Concat(turnedOff));
        var looksTouched = new HashSet<int>(looksMoved.Concat(looksCleared));

        // What this scan actually opened, by kind.
        //
        // A null result is only worth as much as the reading behind it, and three times this
        // session "nothing in the world does X" turned out to be a sentence about a scan that
        // had never looked at some whole class of script. A count per kind is the cheapest
        // possible way to tell "there are none" from "I did not look" — and the two have been
        // indistinguishable in this output for as long as it has existed.
        var kinds = new Dictionary<string, int>();

        foreach (LoadedMap map in library.All())
        {
            foreach (SetsAFlag script in EveryScriptOn(map))
            {
                string kind = script.What.Split(' ')[0] + (script.What.StartsWith("on ") ? " " + script.What.Split(' ')[1] : "");

                kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
            }

        }

        int marks = touched.Count(f => !gates.IsAboutTheWorld(f));

        Console.WriteLine();
        Console.WriteLine(
            $"  {kinds.Values.Sum()} scripts were opened to work that out: "
            + string.Join(", ", kinds.OrderByDescending(k => k.Value).Select(k => $"{k.Value} {k.Key}")));
        Console.WriteLine(
            "    a kind with no line here is a kind nothing looked at, which reads exactly like");
        Console.WriteLine(
            "    a kind with nothing in it — and that has been true of this output all along");
        Console.WriteLine();
        Console.WriteLine(
            $"  {looksTouched.Count} flags are set or cleared by a script somewhere, reading every branch");
        Console.WriteLine(
            $"  {touched.Count} of them on an arm a run could actually take — the other "
            + $"{looksTouched.Count - touched.Count} are behind a switch the script itself decides");

        if (ranOut > 0)
        {
            Console.WriteLine(
                $"    ({ranOut} script(s) hit the step limit before finishing — a short read "
                + "invents walls, so it is counted)");
        }

        Console.WriteLine($"    {touched.Count - marks} of those gate something — they travel between people playing together");
        Console.WriteLine($"    {marks} gate nothing this build can see — they stay with whoever earned them");
        Console.WriteLine();
        Console.WriteLine(
            "    a flag gating something this project has not extracted yet reads as gating");
        Console.WriteLine(
            "    nothing, so it stays personal — a door that fails to open for a friend, which");
        Console.WriteLine(
            "    somebody notices. The opposite error hands over a badge, which nobody notices.");

        WriteWhatNothingMoves(world, turnedOn, turnedOff);

        WriteTheBoundaryAgainstTheFile(rom, library, gates, turnedOn, turnedOff);

        static string Describe(FlagGate kind) => kind switch
        {
            FlagGate.APerson => "somebody standing there",
            FlagGate.TheBoat => "the boat",
            _ => "nothing this build can see",
        };
    }

    /// <summary>
    /// What a person who does nothing is waiting for, and who could give it to them.
    /// <para>
    /// The last thing left to ask about a blocker. The playthrough already says this script
    /// sets no flag, asks for nothing, walks nobody and calls no routine — and every one of
    /// those is a statement about <em>the arm the run took</em>. The other arm is right there
    /// in the image and has been readable the whole time.
    /// </para>
    /// <para>
    /// Three flags at most, worst first: a flag the run has not got, whose other arm actually
    /// does something, is a job. A flag the run already has is not a wall, and saying so is
    /// how this instrument gets to be wrong out loud.
    /// </para>
    /// </summary>
    private static void WriteWhatItIsWaitingFor(
        Rom rom,
        WorldData world,
        string mapId,
        Blocker who,
        Attempt played,
        Lazy<IReadOnlyDictionary<int, IReadOnlyList<SetsAFlag>>> setters,
        Lazy<IReadOnlyDictionary<int, IReadOnlyList<WritesAVariable>>> writers,
        Dictionary<WhereItStands, int> standings)
    {
        // The record first, because that is where this game keeps it. Only 7 of the 575
        // objects carrying a hide flag have a script that sets it — the flag that takes
        // somebody off a map is written on the map's own object record and set by something
        // else entirely, so reading the script for it is reading the wrong half of the file.
        if (who.HiddenBy == 0)
        {
            Console.WriteLine(
                "         NO FLAG TAKES IT OFF THE MAP — its record is not hidden by anything, "
                + "so nothing removes it");
        }
        else
        {
            IReadOnlyList<SetsAFlag> removes = setters.Value.GetValueOrDefault(who.HiddenBy, []);

            Console.WriteLine(
                $"         its record is hidden by flag 0x{who.HiddenBy:X4} — "
                + (played.Flags.Contains(who.HiddenBy) ? "which the run HAS set" : "which the run never set"));

            if (removes.Count == 0)
            {
                Console.WriteLine(
                    "           NOTHING IN THE WORLD SETS IT — it comes out of a routine, not a script");
            }

            // How many of them a run could actually reach the setflag from. A plain read
            // follows every branch including ones the script has already ruled out, so being
            // named here is not the same as being a way through — and this thread spent two
            // rounds in SILPH CO. on the difference.
            var canReallySet = 0;

            foreach (SetsAFlag sets in removes)
            {
                if (WhatItIsWaitingFor.PathTo(rom, sets.Address, who.HiddenBy) is not null) canReallySet++;
            }

            foreach (SetsAFlag sets in removes.Take(3))
            {
                Console.WriteLine(
                    $"           set by {sets.MapId} {world.Find(sets.MapId)?.Name ?? "(not exported)"} "
                    + $"{sets.What} — {Standing(sets)}");

                // And, when the run got into the script ON THIS MAP and still did not set it,
                // what it would have had to be true to get to the setflag. On this map: the
                // same block hangs off up to nineteen of them, and having run it in one town
                // says nothing whatever about the square in this one.
                if (played.Ran.ContainsKey((sets.MapId, sets.Address)))
                    WriteTheWayIn(rom, world, sets, who.HiddenBy, writers);
            }

            if (removes.Count > 3) Console.WriteLine($"           ... and {removes.Count - 3} more that set it");

            // The conclusion across all of them, which no single line above can draw.
            if (canReallySet == 0)
            {
                Console.WriteLine(
                    $"           SO NOTHING THAT CAN ACTUALLY RUN SETS 0x{who.HiddenBy:X4} — all "
                    + $"{removes.Count} were named by a read that walks arms no run could take.");
                Console.WriteLine(
                    "           This door is behind the code boundary, not behind a walk.");
            }
            else if (canReallySet < removes.Count)
            {
                Console.WriteLine(
                    $"           {canReallySet} of those {removes.Count} can really reach the setflag; "
                    + "the rest are arms no run could take.");
            }
        }

        if (world.Find(mapId)?.Objects.FirstOrDefault(o => o.LocalId == who.LocalId) is not { } person
            || person.ScriptAddress == 0)
        {
            Console.WriteLine("         and has no script at all — nobody is standing here on purpose");
            return;
        }

        WaitingOn waiting = WhatItIsWaitingFor.Asks(rom, person.ScriptAddress);

        if (waiting.Flags.Count == 0)
        {
            // The finding that kills the whole idea, and the reason this was built rather
            // than written down. A script with no conditional in it is not waiting; the run
            // read all of it, and the door is shut for some other reason entirely.
            Console.WriteLine(
                "         its script asks about no flag at all — it is not waiting on one"
                + (waiting.AskedWithoutABranch > 0
                    ? $" ({waiting.AskedWithoutABranch} checkflag(s) this could not pair with a branch)"
                    : string.Empty)
                + (waiting.Truncated ? " — and the read hit its own limit, so there may be more" : string.Empty));

            WriteWhatElseItAsks(waiting);

            return;
        }

        var has = played.Flags.ToHashSet();

        List<FlagAsked> worst =
        [
            .. waiting.Flags
                .OrderBy(f => has.Contains(f.Flag))
                .ThenBy(f => f.IfSet.Nothing && f.IfClear.Nothing)
                .ThenByDescending(f => Math.Max(f.IfSet.Commands, f.IfClear.Commands)),
        ];

        foreach (FlagAsked asked in worst.Take(3))
        {
            Console.WriteLine(
                $"         waiting on flag 0x{asked.Flag:X4} — "
                + (has.Contains(asked.Flag) ? "which the run HAS set" : "which the run never set"));

            if (asked.NeitherAnswerChangesAnything)
            {
                Console.WriteLine("           and neither answer changes anything — it only picks a line of text");
            }
            else
            {
                Console.WriteLine($"           if set:   {asked.IfSet}");
                Console.WriteLine($"           if clear: {asked.IfClear}");
            }

            IReadOnlyList<SetsAFlag> from = setters.Value.GetValueOrDefault(asked.Flag, []);

            Console.WriteLine(
                from.Count == 0
                    ? "           NOTHING IN THE WORLD SETS IT — it comes out of a routine, not a script"
                    : $"           set by {from.Count}: " + string.Join(", ", from.Take(3))
                        + (from.Count > 3 ? $", +{from.Count - 3} more" : string.Empty));
        }

        if (worst.Count > 3) Console.WriteLine($"         ... and {worst.Count - 3} more flag(s) it asks about");

        // Which of the four this is, is a rule about the world and lives on the run. Only the
        // wording is this file's business — a conditional here is a conditional no test can
        // reach, which is the fault this project has now moved out of this file six times.
        string Standing(SetsAFlag sets) => Say(
            played.HowItStands(sets.MapId, sets.Address),
            played.Ran.GetValueOrDefault((sets.MapId, sets.Address)));

        string Say(WhereItStands stands, WhatRan? here)
        {
            standings[stands] = standings.GetValueOrDefault(stands) + 1;

            return stands switch
            {
                WhereItStands.OnAMapItNeverReached =>
                    "ON A MAP IT NEVER REACHED — that map is the job",

                WhereItStands.ItRanTheScriptHere =>
                    "IT RAN THIS SCRIPT AND THE FLAG IS STILL UNSET — " + WhyItStopped(here!),

                // The fourth answer, which had been collapsed into the first. Saying "IT RAN
                // THIS SCRIPT" here — with a reason merged in from another town — is a
                // fallback that names a cause, which is worse than one that says nothing.
                WhereItStands.ItRanTheSameBlockOnAnotherMap =>
                    "on a map it reached, and it never ran this script HERE — the same block "
                        + "hangs off another map and the run ran it there, a different scene",

                _ => "on a map it reached, and it never ran this script — it never stood on the "
                    + "square",
            };
        }

        if (waiting.AskedWithoutABranch > 0)
        {
            Console.WriteLine(
                $"         ({waiting.AskedWithoutABranch} checkflag(s) this could not pair with a branch — "
                + "the size of what it cannot see)");
        }

        WriteWhatElseItAsks(waiting);
    }

    /// <summary>
    /// Why a script that ran did not get as far as the flag it sets.
    /// <para>
    /// Three ways, and two of them already have a lever. A yes-or-no nobody answered is
    /// <c>--say-yes</c>; a routine into code this cannot execute is <c>--answer</c>; an
    /// ordinary branch is the one that needs the bytes read, and <see cref="WriteTheWayIn"/>
    /// reads them.
    /// </para>
    /// </summary>
    private static string WhyItStopped(WhatRan did) =>
        did.StoppedAtAQuestion
            ? "it stopped at a yes-or-no nobody answered — try --say-yes"
            : did.Fought.Count > 0
                ? $"IT STOPPED AT A FIGHT it did not win (trainer(s) "
                    + string.Join(", ", did.Fought.Take(3)) + ") — everything after the fight is "
                    + "unreached, and the setflag may be sitting there unconditionally"
                : did.Routines.Count > 0
                    ? $"it asked routine(s) {string.Join(", ", did.Routines.Take(3).Select(r => $"0x{r:X3}"))} "
                        + "and took the zero arm — try --answer"
                    : "it ran to the end, so the setflag is on an ordinary branch it had no reason to take";

    /// <summary>
    /// The answers that had to go a particular way to reach a <c>setflag</c>, in order.
    /// <para>
    /// The last question in the chain. Three SAFFRON doors are one flag; that flag is set by a
    /// trigger in SILPH CO.; the run stood on the trigger and ran it and the flag is still
    /// unset. So the setting is behind a branch <em>inside</em> that script, and this is the
    /// list of what has to be true first.
    /// </para>
    /// </summary>
    private static void WriteTheWayIn(
        Rom rom,
        WorldData world,
        SetsAFlag sets,
        int flag,
        Lazy<IReadOnlyDictionary<int, IReadOnlyList<WritesAVariable>>> writers)
    {
        // The trigger's own record, which is where the last answer was hiding too.
        //
        // A trigger in this cartridge fires when a variable equals a value, and both are
        // written down on the square rather than in the script. The playthrough runs every
        // trigger it stands on regardless — an upper bound on what runs, which is the right
        // default for a floor — so a scene meant for later runs now, takes the not-yet arm of
        // its own counter, and reports as a script that does nothing.
        if (world.Find(sets.MapId)?.Triggers.FirstOrDefault(t => t.ScriptAddress == sets.Address)
            is { Variable: not 0 } fires)
        {
            Console.WriteLine(
                $"             its own record fires it when 0x{fires.Variable:X4} == {fires.Value} "
                + "— and the run stood on it and ran it whatever was in there");
        }

        IReadOnlyList<OnTheWay>? way = WhatItIsWaitingFor.PathTo(rom, sets.Address, flag);

        if (way is null)
        {
            // TWO READINGS DISAGREEING, AND THE STRICTER ONE IS RIGHT.
            //
            // This script was named because a plain read found a setflag somewhere inside it.
            // Walking it with the script's own decisions honoured says no run can ever get
            // there. Both cannot be true, and the one that models what running means wins:
            // the script is not a way to set this flag, and naming it sent this thread to
            // SILPH CO. for two rounds.
            //
            // Said loudly rather than printed as nothing, because "no line here" is what a
            // script with no gates looks like too.
            Console.WriteLine(
                "             BUT NO RUN CAN REACH THAT setflag FROM HERE — the script decides"
                + " its own switch and the flag is behind a value it never sets.");
            Console.WriteLine(
                "             So this script is NOT a way to set it, and something else enters"
                + " the shared block it lives in. That is the job.");

            return;
        }

        Console.WriteLine(
            way.Count == 0
                ? "             nothing gates it on the way found — it sets the flag unconditionally"
                : $"             to get there: {string.Join(" AND ", way.Take(6))}"
                    + (way.Count > 6 ? $", and {way.Count - 6} more" : string.Empty)
                    + "  (one path of possibly several)");

        // And who could put the right number in. A variable nothing writes is behind the code
        // boundary exactly as a flag nothing sets is — the same finding, and until now only
        // half of it could be reached, because "what gates what" only ever asked about flags.
        // Only the ones that are actually gates. A variable this script wrote itself is a
        // switch it computes and reads back, and asking who else writes it is asking the
        // wrong question about the wrong number.
        foreach (int variable in way
                     .Where(w => w.AskedBy != 0x2B && !w.DecidedHere)
                     .Select(w => w.Word)
                     .Distinct()
                     .Take(3))
        {
            IReadOnlyList<WritesAVariable> puts = writers.Value.GetValueOrDefault(variable, []);

            if (puts.Count == 0)
            {
                Console.WriteLine(
                    $"             NOTHING IN THE WORLD WRITES 0x{variable:X4} — it comes out of a routine");

                continue;
            }

            // How many maps, not just how many scripts. A story counter is written in one
            // place; a scratch variable is written everywhere, and the two are the same line
            // of output until the spread is on it. 285 scripts sounds like thoroughness and
            // means the opposite.
            int maps = puts.Select(w => w.Where.MapId).Distinct().Count();

            Console.WriteLine(
                $"             0x{variable:X4} is written by {puts.Count} script(s) across {maps} map(s)"
                + (maps > 1 ? " — written in many places, so read it as scratch before story" : string.Empty));

            Console.WriteLine("               e.g. " + string.Join("; ", puts.Take(3)));
        }
    }

    /// <summary>
    /// What else the script branches on, which is how this instrument gets to say the question
    /// was wrong.
    /// <para>
    /// A door gated on <c>0x47</c> is a shopping list; one gated on a <c>compare</c> after a
    /// <c>special</c> is behind the code boundary; one gated on a plain variable is a story
    /// counter. Three different jobs, and a flag-shaped instrument reports all three as "asks
    /// about no flag at all" unless it is made to count them.
    /// </para>
    /// </summary>
    private static void WriteWhatElseItAsks(WaitingOn waiting)
    {
        if (waiting.OtherQuestions.Count == 0) return;

        Console.WriteLine(
            "         it also branches on "
            + string.Join(
                ", ",
                waiting.OtherQuestions
                    .Take(4)
                    .Select(q => $"{q.Times} x {(q.Code == 0 ? "nothing this could see" : ScriptCommands.NameOf(q.Code))}")));
    }

    /// <summary>
    /// The flags nothing in the world can move, and how many people each one holds.
    /// <para>
    /// <b>The general case of every wall this project has chased.</b> One door in SAFFRON took
    /// ten measurements to place and the answer was that nothing readable sets the flag behind
    /// it — which sounded like a finding about SAFFRON right up until these two counts were
    /// put side by side. It is the ordinary condition of this cartridge.
    /// </para>
    /// <para>
    /// Split two ways, because they are opposite failures. Somebody the story would move and
    /// never does is <b>in the way</b>, and gets chased for ten measurements. Somebody the
    /// story would bring in and never does is <b>invisible</b>, and nothing has ever noticed
    /// them at all — which makes the second list the more interesting of the two.
    /// </para>
    /// </summary>
    private static void WriteWhatNothingMoves(
        WorldData world, IReadOnlyCollection<int> turnedOn, IReadOnlyCollection<int> turnedOff)
    {
        IReadOnlyList<WhatMoves> ranked = WhoMovesEachFlag.Rank(world, turnedOn, turnedOff);

        List<WhatMoves> stuck = [.. ranked.Where(f => f.StuckThere)];
        List<WhatMoves> never = [.. ranked.Where(f => f.NeverArrive)];
        List<WhatMoves> moved = [.. ranked.Where(f => !f.NothingCanMoveIt)];

        Console.WriteLine();
        Console.WriteLine(
            $"  {ranked.Count(f => f.NothingCanMoveIt)} of those {ranked.Count} gating flags are set "
            + "and cleared by nothing at all — this is the code boundary, drawn");
        Console.WriteLine(
            $"    {moved.Count} a script can move, {stuck.Count} hold somebody who will never leave, "
            + $"{never.Count} hold somebody who will never arrive");

        Console.WriteLine();
        Console.WriteLine(
            $"  {stuck.Sum(f => f.People)} people stand somewhere for ever, behind {stuck.Count} flag(s) "
            + "nothing sets");
        Console.WriteLine(
            $"    of them {stuck.Sum(f => f.InDoorways)} are standing on or beside a door, behind "
            + $"{stuck.Count(f => f.InDoorways > 0)} flag(s) — THIS is the wall list");
        Console.WriteLine(
            "    the rest are villagers on squares nobody needs. Every blocked doorway is one of");
        Console.WriteLine(
            "    these flags; almost none of these flags is a blocked doorway, and ranking by how");
        Console.WriteLine(
            "    many people one holds reads as a wall list while being nothing of the kind");

        foreach (WhatMoves flag in stuck.Where(f => f.InDoorways > 0).Take(12))
        {
            Console.WriteLine(
                $"    0x{flag.Flag:X4}  {flag.InDoorways,3} in doorways of {flag.People,3} people "
                + $"across {flag.Maps} map(s)");
        }

        int walls = stuck.Count(f => f.InDoorways > 0);

        if (walls > 12) Console.WriteLine($"    ... and {walls - 12} more with somebody in a doorway");

        Console.WriteLine();
        Console.WriteLine(
            $"  {never.Sum(f => f.People)} people never arrive at all, behind {never.Count} flag(s) "
            + "nothing clears — nothing has ever noticed these");

        foreach (WhatMoves flag in never.Take(12))
        {
            Console.WriteLine(
                $"    0x{flag.Flag:X4}  {flag.People,3} people across {flag.Maps} map(s)"
                + (flag.InDoorways > 0 ? $" ({flag.InDoorways} of them in a doorway)" : string.Empty));
        }

        if (never.Count > 12) Console.WriteLine($"    ... and {never.Count - 12} more");
    }

    /// <summary>setflag and clearflag, both two bytes wide and both long since derived.</summary>
    private const byte SetFlagCode = 0x29;

    private const byte ClearFlagCode = 0x2A;

    /// <summary>Every script on one map, with where it came from.</summary>
    private static IEnumerable<(string MapId, string What, uint Address)> ScriptsOf(LoadedMap map) =>
        EveryScriptOn(map).Select(s => (s.MapId, s.What, s.Address));

    /// <summary>
    /// Every script on one map, including what it runs on arrival.
    /// <para>
    /// On-entry scripts were missing from this list for as long as it has existed, and this
    /// session very nearly concluded "nothing in the world sets flag 0x003E" from a scan of
    /// three of the four kinds. A list of what counts as "every script" is exactly the sort of
    /// thing that stays quietly incomplete, so it lives somewhere it can be tested now.
    /// </para>
    /// </summary>
    private static IEnumerable<SetsAFlag> EveryScriptOn(LoadedMap map) =>
        WhatItIsWaitingFor.EveryScriptOn(
            WorldExporter.MapId(map.Bank, map.Number),
            map.Objects,
            map.Triggers,
            map.Signs,
            map.OnEntry,
            map.OnLoad);

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
            List<SpeciesData> species = RomExtractor.Open(rom).ExtractSpecies();

            Console.WriteLine($"    the species table names {species.Count}");
            Console.WriteLine($"    {cries.Samples.Distinct().Count()} different recordings between them");

            // Which entry each species uses, which is not its number. The two counts cannot
            // line up, and the difference is a block of slots in the middle of the numbering
            // that carry no creature — found here by their repeated name.
            (Dictionary<int, int> entryFor, CryIndexResult index) =
                CryIndex.Derive([.. species.Select(s => s.Name)], cries.Count, Console.WriteLine);

            if (!index.NoGap)
            {
                Console.WriteLine(
                    $"    which leaves {cries.Count - index.Mapped} entries over — "
                    + (cries.Count == index.Mapped
                        ? "none, and the arithmetic closes exactly"
                        : "worth a look if that is not a small number"));
            }

            var library = new CryLibrary(rom, tree.Samples, cries, entryFor);

            var decoded = 0;
            var samplesLong = 0;

            foreach (int at in entryFor.Keys)
            {
                if (library.For(at) is not { } voice) continue;

                decoded++;
                samplesLong += voice.Audio.Length;
            }

            Console.WriteLine(
                decoded == 0
                    ? "    none of them unpacked, which is a finding"
                    : $"    {decoded} creatures have a noise, {samplesLong / Math.Max(1, decoded)} samples long on average");
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
        var dropped = 0;
        var trouble = new Dictionary<SongTrouble, int>();

        for (int song = 0; song < tree.Table.Count; song++)
        {
            if (SongLoader.Load(rom, tree, song, mixer, out SongTrouble why) is not { } player)
            {
                trouble[why] = trouble.GetValueOrDefault(why) + 1;

                continue;
            }

            loaded++;
            tracks += player.TrackCount;

            // A song that comes back with fewer tracks than its header claims is a song
            // partly read, which is a different and quieter failure than one that does not
            // come back at all.
            dropped += tree.Songs.First(s => s.Offset == tree.Table[song].HeaderOffset).TrackCount
                       - player.TrackCount;
        }

        // Where the reads that failed actually died. A command whose argument count this
        // reader has wrong leaves every read after it a byte or two out, so the byte it dies
        // on — counted across a whole cartridge — names the command that is wrong.
        var died = new Dictionary<byte, int>();
        var after = new Dictionary<byte, int>();
        var overBudget = 0;

        var tableNames = tree.Table.Select(e => e.HeaderOffset).ToHashSet();

        foreach (SongHeaderRecord header in tree.Songs.Where(s => tableNames.Contains(s.Offset)))
        {
            foreach (int track in header.TrackOffsets)
            {
                TrackRead read = SequenceReader.Read(rom, track);

                if (read.EndedProperly) continue;

                if (read.StoppedAt < 0)
                {
                    overBudget++;

                    continue;
                }

                died[read.StoppedOn] = died.GetValueOrDefault(read.StoppedOn) + 1;
                after[read.After] = after.GetValueOrDefault(read.After) + 1;
            }
        }

        if (died.Count > 0 || overBudget > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  where the reads that failed died");

            Console.WriteLine(
                "    on byte: " + string.Join(
                    ", ",
                    died.OrderByDescending(p => p.Value).Take(10).Select(p => $"0x{p.Key:X2} x{p.Value}")));

            Console.WriteLine(
                "    after command: " + string.Join(
                    ", ",
                    after.OrderByDescending(p => p.Value).Take(10).Select(p => $"0x{p.Key:X2} x{p.Value}")));

            if (overBudget > 0)
                Console.WriteLine($"    and {overBudget} ran past the command budget, which is a different thing");
        }

        Console.WriteLine();
        Console.WriteLine($"  {loaded} of {tree.Table.Count} songs assemble, {tracks} tracks between them");

        if (dropped > 0)
            Console.WriteLine($"    and {dropped} more tracks were dropped from songs that did assemble");

        // Which of the three places the trouble is in, rather than one word for all of them.
        foreach ((SongTrouble why, int count) in trouble.OrderByDescending(p => p.Value))
        {
            Console.WriteLine($"    {count} did not, because: {Explain(why)}");
        }

        static string Explain(SongTrouble why) => why switch
        {
            SongTrouble.NoHeader => "the table names a header this walk did not confirm",
            SongTrouble.NoVoicegroup => "the header names a voicegroup this walk did not confirm",
            SongTrouble.EveryTrackDropped => "every track ran somewhere the reader could not follow",
            _ => why.ToString(),
        };

        WriteRejectedHeaders(rom, tree);
        WriteTrackEndings(rom, tree);
        WritePerformers(tree);
        WriteScriptedSongs(rom);
    }

    /// <summary>
    /// Every song the table names whose header this walk did not confirm, with the rule that
    /// turned it down and the bytes that caused it.
    /// <para>
    /// Thirty-one of these on a real cartridge, and one of them is the professor's laboratory
    /// — so they are not obscure songs in a corner of the file. "Not confirmed" was one word
    /// covering six faults sitting in three different layers, and which one it is says
    /// whether to look at the table, at the voicegroup walk, or at the recordings under it.
    /// </para>
    /// </summary>
    private static void WriteRejectedHeaders(Rom rom, SoundTreeResult tree)
    {
        var confirmed = tree.Songs.Select(s => s.Offset).ToHashSet();

        List<SongTableEntry> rejected = [.. tree.Table.Where(e => !confirmed.Contains(e.HeaderOffset))];

        if (rejected.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"  the {rejected.Count} headers the table names and this walk did not confirm");

        var byReason = new Dictionary<SongRejection, List<SongTableEntry>>();

        foreach (SongTableEntry entry in rejected)
        {
            SongRejection why = SoundLocator.WhyNot(rom, tree, entry.HeaderOffset);

            if (!byReason.TryGetValue(why, out List<SongTableEntry>? had)) byReason[why] = had = [];

            had.Add(entry);
        }

        foreach ((SongRejection why, List<SongTableEntry> entries) in byReason.OrderByDescending(p => p.Value.Count))
        {
            Console.WriteLine();
            Console.WriteLine($"    {entries.Count}: {Because(why)}");

            // The songs themselves, so a number somebody recognises can be looked up. Ten
            // rather than all of them, and it says when it has stopped.
            foreach (SongTableEntry entry in entries.Take(10))
            {
                Console.WriteLine(
                    $"      song {entry.Index,3}  header 0x{entry.HeaderOffset:X6}  "
                    + $"group {entry.Group,3}  bytes {SoundLocator.BytesAt(rom, entry.HeaderOffset)}");
            }

            if (entries.Count > 10) Console.WriteLine($"      ... and {entries.Count - 10} more");
        }

        static string Because(SongRejection why) => why switch
        {
            SongRejection.PastTheEnd => "the header or its pointers run off the end of the file",
            SongRejection.TrackCount => "the first byte is not a track count anything could have",
            SongRejection.VoicegroupNotAPointer => "where the voicegroup should be is not an address",
            SongRejection.VoicegroupNotConfirmed =>
                "it names a voicegroup that resolves but the walk below did not confirm — "
                + "so the fault is in the recordings or the instruments, not here",
            SongRejection.TrackNotAPointer => "one of its track pointers is not an address",
            SongRejection.None =>
                "nothing rejects it, which means it was confirmed and something else is wrong",
            _ => why.ToString(),
        };
    }

    /// <summary>
    /// How each track of each song in the table stops: at an end command, at a jump backwards,
    /// or not at all.
    /// <para>
    /// The number that was missing. A track that has run out and a track that was written to
    /// repeat were the same answer, so a song whose tracks stop where the music loops looked
    /// exactly like a song that had finished — which is what <c>song 291</c> is.
    /// </para>
    /// </summary>
    private static void WriteTrackEndings(Rom rom, SoundTreeResult tree)
    {
        var tableNames = tree.Table.Select(e => e.HeaderOffset).ToHashSet();

        var ends = 0;
        var loops = 0;
        var neither = 0;
        var calls = 0;

        var songsAllLooping = 0;
        var songsMixed = 0;

        foreach (SongHeaderRecord header in tree.Songs.Where(s => tableNames.Contains(s.Offset)))
        {
            var songLoops = 0;
            var songEnds = 0;

            foreach (int track in header.TrackOffsets)
            {
                TrackRead read = SequenceReader.Read(rom, track);

                calls += read.Calls;

                if (!read.EndedProperly) { neither++; continue; }

                if (read.Loops) { loops++; songLoops++; }
                else { ends++; songEnds++; }
            }

            if (songLoops > 0 && songEnds == 0) songsAllLooping++;
            else if (songLoops > 0) songsMixed++;
        }

        Console.WriteLine();
        Console.WriteLine("  how the tracks of the songs in the table stop");
        Console.WriteLine($"    {loops} jump backwards, which is a track written to repeat");
        Console.WriteLine($"    {ends} run to an end command, which is a track written to stop");
        Console.WriteLine($"    {neither} do neither, and are dropped");
        Console.WriteLine($"    {calls} subsections were expanded, which is what a repeated phrase is");
        Console.WriteLine();
        Console.WriteLine(
            $"    {songsAllLooping} songs have every track repeating; {songsMixed} have some of each");
        Console.WriteLine(
            "    a song of mostly-repeating tracks with a few that stop is the shape to look at: "
            + "before this build, a phrase called twice cut the track off at the second call");
    }

    /// <summary>
    /// The group number each table entry writes twice, counted.
    /// <para>
    /// This is the field that decides which one-off songs can overlap. The reading of it —
    /// that it names one of the driver's several performers — is <b>modelled</b>, and this is
    /// what would say so: a handful of distinct values, one of them holding most of the table,
    /// is a performer index. A different number for nearly every song is not, and would mean
    /// the whole overlapping arrangement is built on a misreading.
    /// </para>
    /// </summary>
    private static void WritePerformers(SoundTreeResult tree)
    {
        if (tree.Table.Count == 0) return;

        var byGroup = tree.Table
            .GroupBy(e => e.Group)
            .OrderByDescending(g => g.Count())
            .ToList();

        Console.WriteLine();
        Console.WriteLine(
            $"  the group number, written twice in every entry: {byGroup.Count} distinct value(s) "
            + $"across {tree.Table.Count} songs");

        foreach (IGrouping<byte, SongTableEntry> group in byGroup.Take(12))
        {
            Console.WriteLine(
                $"    group {group.Key,3}: {group.Count(),4} songs — "
                + string.Join(", ", group.Take(8).Select(e => e.Index)));
        }

        if (byGroup.Count > 12) Console.WriteLine($"    ... and {byGroup.Count - 12} more");

        Console.WriteLine();
        Console.WriteLine(
            byGroup.Count < tree.Table.Count / 4
                ? "    a few values covering many songs is what a performer index looks like"
                : "    nearly one value per song is NOT a performer index, and the reading is wrong");
    }

    /// <summary>
    /// Which song numbers the scripts actually fire, and how the family of commands that name
    /// them holds up against the whole cartridge.
    /// </summary>
    private static void WriteScriptedSongs(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("  which songs the scripts name");

        MapLibrary library = MapLibrary.Open(rom);

        Dictionary<byte, int> sites = SoundCues.Sites(rom, library);

        if (sites.Count == 0)
        {
            Console.WriteLine("    none, which would mean the command family is read wrong");

            return;
        }

        foreach ((byte code, int count) in sites.OrderByDescending(p => p.Value))
            Console.WriteLine($"    0x{code:X2}: {count} sites");

        // The corroboration. A command that names something and a command that waits for that
        // same something should sit next to each other nearly every time.
        foreach ((byte play, byte wait) in new[]
                 {
                     (SoundCues.PlayEffect, SoundCues.WaitEffect),
                     (SoundCues.PlayOver, SoundCues.WaitFor),
                 })
        {
            (int plays, int paired) = SoundCues.Pairing(rom, library, play, wait);

            if (plays == 0) continue;

            Console.WriteLine(
                $"    0x{play:X2} is followed immediately by 0x{wait:X2} at {paired} of {plays} sites");
        }

        List<SoundCue> cues = SoundCues.All(rom, library);

        Console.WriteLine();
        Console.WriteLine($"    {cues.Count} song numbers named, {cues.Select(c => c.Song).Distinct().Count()} of them different");

        foreach (IGrouping<byte, SoundCue> family in cues.GroupBy(c => c.Code).OrderBy(g => g.Key))
        {
            Console.WriteLine();
            Console.WriteLine($"    0x{family.Key:X2} names:");

            foreach (IGrouping<int, SoundCue> song in family.GroupBy(c => c.Song).OrderByDescending(g => g.Count()))
            {
                SoundCue first = song.First();

                Console.WriteLine(
                    $"      song {song.Key,4}  x{song.Count(),-4} first at {first.MapId} "
                    + $"{first.What} 0x{first.Offset:X6}");
            }
        }

        // And the only battle music on this cartridge that is read rather than decided.
        Console.WriteLine();
        Console.WriteLine("  battle music");

        BattleMusic music = BattleMusicLocator.Themes(rom, library, Console.WriteLine);

        foreach (BattleTheme theme in music.All)
        {
            Console.WriteLine(
                $"    {theme.Kind}: song {theme.Song} — {(theme.Read ? "read" : "modelled")}, {theme.Where}");
        }

        Console.WriteLine(
            $"    {music.Silent.Count} kinds of fight have no song and keep the map's music: "
            + string.Join(", ", music.Silent));

        Console.WriteLine(
            "    an ordinary wild encounter and an ordinary trainer have no script, so there is "
            + "nothing on the file to read for them — that gap is counted rather than filled in");
    }

    /// <summary>
    /// Which commands take arguments a byte at a time and which take a fixed run of them,
    /// measured against this cartridge rather than decided in advance.
    /// <para>
    /// The greedy rule this reader uses is self-correcting for a command taking several
    /// ordinary arguments — the leftovers come back round as a repeat of the running command
    /// and are consumed. It is <b>not</b> self-correcting for a command whose argument can
    /// look like a command: a four-byte address has bytes above 0x80 in it, so the read walks
    /// into the middle of one and never recovers. And because almost no byte in this encoding
    /// is invalid, it does not fail either — it runs until the budget stops it.
    /// </para>
    /// <para>
    /// So the question is asked of the file. For each command, each width is tried and the
    /// tracks that reach an end are counted. A width that reads more tracks to an end than
    /// the others is not a proof, and it is a great deal better than a table copied out of
    /// somebody's notes — the number is printed either way.
    /// </para>
    /// </summary>
    private static void WriteSequenceWidths(Rom rom)
    {
        Console.WriteLine();
        Console.WriteLine("SEQUENCE ARGUMENT WIDTHS");
        Console.WriteLine();

        SoundTreeResult tree = SoundLocator.Walk(rom);

        // Only the songs the table names.
        //
        // A song header found by shape alone is much easier to believe in than it was: a
        // voicegroup pointer may now land on any instrument boundary in a run, and the
        // longest run on this cartridge is 854 instruments. So there are false headers, and
        // their track pointers name arbitrary addresses — reads that were never going to end
        // and that say nothing about argument widths. The table is the filter that removes
        // them, and measuring without it was measuring mostly noise.
        var named = tree.Table.Select(e => e.HeaderOffset).ToHashSet();

        List<int> tracks =
        [
            .. tree.Songs.Where(s => named.Contains(s.Offset))
                .SelectMany(s => s.TrackOffsets)
                .Take(SweepTracks),
        ];

        Console.WriteLine(
            $"  {tracks.Count} tracks sampled from songs the table names, {SweepBudget} commands each");

        // Two numbers, because one of them can be got at by cheating. Making a command eat
        // more bytes makes the read advance faster, and a faster read stumbles onto an end
        // byte more often — so "reaches an end" alone rewards any width that is simply
        // larger. A track read correctly ends in tens of commands, not thousands, so how many
        // end *promptly* is the number that cannot be had that way.
        (int Ends, int Promptly) Score(IReadOnlyDictionary<byte, int>? widths)
        {
            var ends = 0;
            var promptly = 0;

            foreach (int track in tracks)
            {
                TrackRead read = SequenceReader.Read(rom, track, widths, SweepBudget);

                if (!read.EndedProperly) continue;

                ends++;

                if (read.Events.Count <= PromptlyCommands) promptly++;
            }

            return (ends, promptly);
        }

        (int Ends, int Promptly) plain = Score(null);

        Console.WriteLine(
            $"  {plain.Ends} reach an end as things stand, {plain.Promptly} of them within "
            + $"{PromptlyCommands} commands");

        Console.WriteLine();

        var better = new Dictionary<byte, int>();

        for (byte opcode = 0xB5; opcode <= 0xCD; opcode++)
        {
            var scores = new List<(int Width, int Ends, int Promptly)>();

            for (int width = 0; width <= 5; width++)
            {
                (int ends, int promptly) = Score(new Dictionary<byte, int> { [opcode] = width });

                scores.Add((width, ends, promptly));
            }

            (int Width, int Ends, int Promptly) best =
                scores.OrderByDescending(s => s.Promptly).ThenByDescending(s => s.Ends).First();

            // Judged on the number that cannot be had by eating bytes. Most of these will
            // not beat leaving the command alone, and saying nothing about them is the point.
            if (best.Promptly <= plain.Promptly) continue;

            better[opcode] = best.Width;

            Console.WriteLine(
                $"  0x{opcode:X2}  {best.Width} bytes -> {best.Promptly} prompt ends "
                + $"(was {plain.Promptly}), {best.Ends} in all   "
                + string.Join(" ", scores.Select(x => $"{x.Width}:{x.Promptly}/{x.Ends}")));
        }

        if (better.Count == 0)
        {
            Console.WriteLine(
                "  no width reads more tracks promptly to an end than the greedy rule does, so");
            Console.WriteLine(
                "  the trouble is not argument widths and looking harder here would be wasted");

            return;
        }

        // And all of them together, which is not the same as each of them separately: two
        // commands can each derail the same tracks, so fixing one alone shows nothing.
        (int Ends, int Promptly) all = Score(better);

        Console.WriteLine();
        Console.WriteLine(
            $"  all of those together: {all.Promptly} of {tracks.Count} end promptly, "
            + $"{all.Ends} end at all");
    }

    /// <summary>How many tracks the width sweep looks at. <b>Modelled</b>, for speed only.</summary>
    private const int SweepTracks = 300;

    private const int SweepBudget = 3000;

    /// <summary>
    /// How few commands make an ending believable. <b>Modelled.</b>
    /// <para>
    /// A track read correctly ends in tens of commands. One that ends after two thousand did
    /// not find its own end command; it wandered onto a byte that happened to be one.
    /// </para>
    /// </summary>
    private const int PromptlyCommands = 400;

    /// <summary>
    /// One song's tracks, byte by byte, with what this reader made of each.
    /// <para>
    /// Two rounds of inference have not settled where the reads derail, and the sweep's
    /// answer does not describe any format — four commands wanting four bytes and three
    /// wanting two is not what a real encoding looks like. So this stops inferring. It prints
    /// the bytes and what was made of them, and the place where the two stop agreeing will be
    /// visible to anybody who reads it.
    /// </para>
    /// </summary>
    private static void WriteOneSong(Rom rom, int song)
    {
        Console.WriteLine();
        Console.WriteLine($"SONG {song}");
        Console.WriteLine();

        SoundTreeResult tree = SoundLocator.Walk(rom);

        // A negative number means "find me one that fails", which saves guessing at numbers
        // until one of them is broken. The first song all of whose tracks the reader cannot
        // follow — the failure that accounts for every one of the 649.
        if (song < 0)
        {
            song = FirstBadSong(rom, tree);

            if (song < 0)
            {
                Console.WriteLine("  every song the table names reads to an end, so there is nothing to look at");

                return;
            }

            Console.WriteLine($"  the first song whose tracks do not read is {song}");
            Console.WriteLine();
        }

        if (song >= tree.Table.Count)
        {
            Console.WriteLine($"  the table has {tree.Table.Count} songs, so there is no song {song}");

            return;
        }

        int at = tree.Table[song].HeaderOffset;

        if (tree.Songs.FirstOrDefault(s => s.Offset == at) is not { } header)
        {
            Console.WriteLine($"  the table names 0x{at:X6}, which this walk did not confirm as a header");

            return;
        }

        Console.WriteLine(
            $"  header 0x{header.Offset:X6}: {header.TrackCount} tracks, "
            + $"voicegroup 0x{header.VoicegroupOffset:X6}, priority {header.Priority}");

        for (int track = 0; track < header.TrackOffsets.Count; track++)
        {
            int begins = header.TrackOffsets[track];

            TrackRead read = SequenceReader.Read(rom, begins);

            Console.WriteLine();
            Console.WriteLine(
                $"  track {track} at 0x{begins:X6}: {read.Events.Count} commands, "
                + (read.EndedProperly ? "ended" : "DID NOT END")
                + $", {read.Notes} notes, {read.Unknown} unaccounted");

            // The raw bytes, so the reading below can be checked against them.
            Console.Write("    bytes:");

            for (int i = 0; i < BytesToShow && begins + i < rom.Length; i++)
            {
                if (i % 16 == 0) Console.Write($"{Environment.NewLine}      {begins + i:X6}  ");

                Console.Write($"{rom.ReadU8(begins + i):X2} ");
            }

            Console.WriteLine();

            // And what was made of them. Where these two stop agreeing is the answer.
            foreach (SequenceEvent e in read.Events.Take(CommandsToShow))
            {
                Console.WriteLine(
                    $"      {e.Offset:X6}  {e.Opcode:X2}  {e.Command,-8} "
                    + (e.Arguments.Count > 0 ? string.Join(" ", e.Arguments.Select(b => $"{b:X2}")) : "")
                    + (e.Target >= 0 ? $" -> {e.Target:X6}" : ""));
            }

            if (read.Events.Count > CommandsToShow)
                Console.WriteLine($"      ... and {read.Events.Count - CommandsToShow} more");
        }
    }

    /// <summary>The first song the table names whose tracks this reader cannot follow.</summary>
    private static int FirstBadSong(Rom rom, SoundTreeResult tree)
    {
        for (int song = 0; song < tree.Table.Count; song++)
        {
            if (tree.Songs.FirstOrDefault(s => s.Offset == tree.Table[song].HeaderOffset)
                is not { } header) continue;

            if (header.TrackOffsets.All(t => !SequenceReader.Read(rom, t).EndedProperly)) return song;
        }

        return -1;
    }

    private const int BytesToShow = 96;

    private const int CommandsToShow = 40;

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

        // And what was opened, by kind — the project's standing answer to the fault that keeps
        // coming back. This report rolled its own four-kind list and was blind to the fifth for
        // as long as it existed; nothing failed, because nothing can fail in here. A line per
        // kind cannot stop that happening, but it makes it visible in the output the moment it
        // does: a kind with no line is a kind nothing looked at.
        var kinds = new Dictionary<string, int>();

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
            // AND THE FIFTH KIND, WHICH THIS ROLLED ITS OWN LIST TO LEAVE OUT.
            //
            // Four kinds were written out by hand here — people, signs, triggers, on-entry —
            // long after `EveryScriptOn` was created so that "what counts as every script"
            // would live in one place and be wrong in one place. It was not called, so the
            // map's own script list was invisible, and 0xD0 — which stops fifty-one blocks,
            // more than the next three commands together — did not appear in this report at
            // all. Not scored low. Absent.
            //
            // A width nobody derives is a read that stops, and a read that stops does not
            // fail: it comes back clean and quietly contains less.
            foreach (SetsAFlag what in EveryScriptOn(map))
            {
                string kind = what.What.StartsWith("on ", StringComparison.Ordinal)
                    ? string.Join(" ", what.What.Split(' ').Take(2))
                    : what.What.Split(' ')[0];

                kinds[kind] = kinds.GetValueOrDefault(kind) + 1;

                uint start = what.Address;

                foreach (uint reachable in ScriptReader.Reachable(rom, start))
                {
                    if (ScriptReader.StoppedAt(rom, reachable) is not { } code) continue;
                    if (ScriptReader.StoppedAtOffset(rom, reachable) is not { } at) continue;

                    if (!sites.TryGetValue(code, out List<int>? where)) sites[code] = where = [];

                    if (where.Count < sitesPer && !where.Contains(at)) where.Add(at);
                }
            }
        }

        Console.WriteLine(
            "  opened, by kind: "
            + string.Join(", ", kinds.OrderByDescending(k => k.Value).Select(k => $"{k.Value} {k.Key}")));
        Console.WriteLine(
            "    a kind with no line here is a kind nothing looked at, which reads exactly like");
        Console.WriteLine(
            "    a kind with nothing in it. This report was blind to the fifth for a year.");

        // Every pointer in the image, once. What it is for is below: a width that carries the
        // read on into an address something else names has swallowed a block boundary.
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        foreach ((byte code, List<int> where) in sites.OrderByDescending(e => e.Value.Count))
        {
            var scores = new List<(int Width, double Clean, double Pointers, double Depth, double Speech, bool Ruled)>();
            var reasons = new Dictionary<int, string?>();

            string? Ruled(int width) => reasons.GetValueOrDefault(width);

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
                // AND THE ONE THAT SETTLED 0xD0, WHICH EVERY TEST ABOVE GOT WRONG.
                //
                // You do not fall into a block that has its own pointer. If a width carries
                // the read on into an address something else in the image names, that width
                // has eaten a block boundary — usually an `end` — and is now reading the
                // neighbouring script as though it were this one.
                //
                // 0xD0 stops fifty-one blocks, more than the next three commands together. At
                // three bytes it reads on into a textbox and scores beautifully; at two it
                // stops at an `end` and scores worse on every continuation test there was.
                // Eleven of its sixteen sites have something pointing at that textbox, so the
                // textbox is a script in its own right and the byte three bytes along is an
                // `end`. Two is right and every other test here preferred three.
                //
                // This is the trap the note on 0x4F describes, from the other side: the
                // continuation tests reward the width that swallows whatever the reader cannot
                // yet handle. A block boundary is the one thing they cannot see, and it is the
                // only thing in the file that says "the script stops HERE" out loud.
                double intoSomebodyElses = EverywhereInTheImage.ReadsOnIntoSomebodyElses(index, where, width);

                // WHICH rule, not THAT one did. This printed "it eats a page, an instruction,
                // or resumes on a column" for three different rules, so a width thrown out
                // could not be argued with — and one of them was throwing out the right answer.
                string? why =
                    intoSomebodyElses >= 0.5 ? "it reads on into a block something else points at"
                    : ResumesOnWork(rom, where, width) <= 0.1 ? "it resumes on nothing but nops and ends"
                    : column >= 0.9 ? "it resumes on the same byte at nearly every site"
                    : null;

                bool ruled = why is not null;

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
                reasons[width] = why;

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

            // HOW MUCH THESE SITES ARE ONE PIECE OF SCRIPT WRITTEN OUT AGAIN AND AGAIN.
            //
            // Printed, not acted on. "Resumes on the same byte at nearly every site" means a
            // width landed inside an argument — unless the sites are duplicates of one idiom,
            // in which case the RIGHT width resumes on a column too and the test is worth
            // nothing here. 0x3F is twenty sites of one shape.
            //
            // Deliberately not wired into the verdict: suppressing a rule until it agrees with
            // an answer already obtained by reading the bytes is not evidence, it is
            // decoration. The verdict already says "read the bytes"; this says how loudly.
            Console.WriteLine();
            Console.WriteLine(
                $"  0x{code:X2}  stops {where.Count} scripts"
                + $" — {WhatIsBehindAStop.AreOneIdiom(rom, where):P0} of them share their run-up,"
                + " so the column test is worth that much less here");

            foreach ((int width, double cleanly, double pointing, double deep, double speech, bool ruled) in scores)
            {
                string mark = ruled
                    ? "  ruled out: " + (Ruled(width) ?? "it eats a page or an instruction")
                    : (top > 0 || spoken > 0) && shortlist.Contains(width) ? " <-" : "";

                Console.WriteLine(
                    $"      {width} bytes:  {deep,5:P0} carry a real pointer, " +
                    $"{cleanly,5:P0} read on to an end, {pointing,5:P0} of those pointers land, " +
                    $"{speech,5:P0} of the text they name reads as speech, " +
                    $"{ResumesOnAColumn(rom, where, width),5:P0} resume on the same byte, " +
                    $"{ResumesOnWork(rom, where, width),5:P0} resume on real work, " +
                    $"{EverywhereInTheImage.ReadsOnIntoSomebodyElses(index, where, width),5:P0} read on into somebody else's block{mark}");
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
              --song <n>             print one song's tracks byte by byte, with what
                                     this reader made of each — the raw bytes and the
                                     reading side by side. A negative number finds the
                                     first song whose tracks do not read.
              --sequence-widths      measure how many bytes each sequence command's
                                     arguments take, by trying every width against
                                     this cartridge and counting how many tracks
                                     reach an end
              --from <map id>       where a new character wakes up (default 4.1, the same
                                    one the server uses).
              --play                play the game from a fresh save, as far as it can get:
                                    walk, talk to everybody reachable, fight whoever picks
                                    a fight, take what is given, walk again. Says where it
                                    stopped and what it never got to. Takes --answer too.
              --money N             give the playthrough N to spend. MODELLED — nothing in this
                                    game gives it any, and the payout table has never been
                                    located. Prices are read. It buys only what it has been
                                    refused, off shelves it can stand in front of.
              --say-yes             answer every yes-or-no with yes and carry on from where the
                                    script stopped. MODELLED: choosing needs a person, and yes
                                    is the answer that opens the most world — a ceiling, like
                                    --boat. Without it every offer in the game hangs mid-sentence.
              --boat                let the playthrough take the ferry. Whether the boat will
                                    carry it is READ off the scripts — a flag or an item; where
                                    the boat goes is MODELLED as every dock, so this makes the
                                    reach a ceiling rather than the floor --play alone reports.
              --where-from ID[,ID]  every place in the image a script names these items, by
                                    what it does with them: handed over, asked for, taken
                                    away, sold, or loaded for a routine. Reads rather than
                                    runs, so a gift on a branch nobody can take still shows.
              --field-effects       what number dofieldeffect takes, against the move the same
                                    block asked about, with the four sites that take one with no
                                    move anywhere near them
              --read-from A[,A]     decode these addresses: the bytes and what they read as,
                                    side by side, every block each one reaches, and where any
                                    read stopped. The command this project kept doing by hand.
              --in-the-image F[,F]  every place in the WHOLE FILE that turns these flags on or
                                    off, and whether the map scan ever decoded that byte.
                                    Every other reading here starts at a map and follows the
                                    jumps, which cannot answer "is there anything the maps do
                                    not point at" and does not fail when asked. Two flags at
                                    once looks for one piece of script moving both — a scene.
                                    Then it climbs: what names this, what names that, until it
                                    reaches something a map opens, or a literal, which is the
                                    code boundary with an address on it.
              --stops 0xNN[,0xNN]   every place a read stopped at this command, with the bytes
                                    around each and what each candidate width would resume on.
                                    --scripts prints ONE example per command, which is enough to
                                    know a command is in the way and not enough to see a column
                                    — and a column across the sites is what settles a width.
              --who-writes 0xNNNN   every place in the WHOLE FILE that puts a number in one of
                                    the story's own variables, and whether the map scan ever
                                    decoded that byte. The other half of --in-the-image: a gate
                                    is a flag or it is a variable, and only one of them could be
                                    hunted through the image until now.
              --climb 0xNNNNNNNN    who names this address, who names them, until it reaches
                                    something a map opens or reaches a literal. The same walk
                                    --in-the-image does, off its leash: half the time the thing
                                    worth asking about is a block rather than a flag.
              --in-order            run a trigger or an arrival script only when its own
                                    condition is met. Without it the run takes arms of the story
                                    no single playthrough could take in one pass — PALLET TOWN's
                                    counter goes to nine before the three balls read it, and the
                                    balls answer "you already have one". A ceiling without it and
                                    a floor with it; neither is the truth alone.
              --trace 0xNNNN        follow one of the story's variables through the whole run,
                                    in order: every write and EVERY READ, with what it held at
                                    the moment somebody looked. Not a lever — it changes nothing
                                    the run does. --who-writes answers the same question of the
                                    image, statically, following every arm of every branch; a run
                                    takes one arm, so the two lists differ and only this one says
                                    what happened.
              --surf                swim WHETHER OR NOT the party knows how. MODELLED, and now
                                    only the override: the walk crosses water on its own when
                                    the party knows the move the cartridge crosses water with,
                                    which is READ — see --who-knows. This is what is left when
                                    the answer to the cartridge's own question is no.
              --fights              every trainerbattle a map opens, with BOTH of the places a
                                    beaten trainer could carry on from: the byte after the
                                    command, and the last of its own pointers that reads like
                                    a script. The runner takes the second. This says what the
                                    first one holds, and how many of them nothing else names.
              --who-knows           every place in the WHOLE FILE that asks which party slot
                                    knows a move, by move, with the reversed-image floor beside
                                    it. The obstacle scan asks this of the maps, and the maps
                                    are 0.6% of the file — so the move that crosses water has
                                    been invisible in exactly the way a move nothing asks about
                                    is invisible.
              --entries             the scenes this cartridge writes as several doors into one
                                    room: a script whose whole content is a handover, grouped by
                                    where it leads. A player takes one door; every walk this
                                    project has takes all of them, so every per-script number it
                                    reports is multiplied by however many doors a scene has.
              --routines            what every routine this project cannot execute is
                                    asked: how many arguments, what its answer is compared
                                    against, how many sites branch on it.
              --answer <r>=<v>      stand in for one routine while walking. Modelled, and
                                    an experiment: supply it, walk again, see what opens.
              --can-it-be-finished  walk the story to a fixpoint from a fresh save: walk,
                                    run every script a player can stand in front of, take
                                    what it opens, walk again. Says how far a player can
                                    actually get and what stops them.
              --flags               classify every flag by what turning it on changes:
                                    somebody appearing, the boat, or nothing at all. The
                                    split co-op's propagation rule is derived from.
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

        public bool FlagGates { get; private init; }

        public bool Closure { get; private init; }

        public bool SpecialContracts { get; private init; }

        public bool Standard { get; private init; }

        public bool TheScan { get; private init; }

        public bool PersonCommands { get; private init; }

        public bool Arrivals { get; private init; }

        public bool TheFloor { get; private init; }

        public bool FieldEffects { get; private init; }

        /// <summary>Addresses to decode and print, or nothing.</summary>
        public IReadOnlyList<uint> ReadFrom { get; private init; } = [];

        public bool Play { get; private init; }

        /// <summary>Items to hunt through every script in the image, or nothing.</summary>
        public IReadOnlyList<int> WhereFrom { get; private init; } = [];

        /// <summary>
        /// Flags to hunt through the whole file rather than through the scripts maps reach.
        /// </summary>
        public IReadOnlyList<int> InTheImage { get; private init; } = [];

        /// <summary>Addresses to climb from, for when the question is not about a flag.</summary>
        public IReadOnlyList<uint> ClimbFrom { get; private init; } = [];

        /// <summary>Variables to hunt through the whole file, the way flags already are.</summary>
        public IReadOnlyList<int> WhoWrites { get; private init; } = [];

        /// <summary>Variables to ask the whole image who LOOKS at.</summary>
        public IReadOnlyList<int> WhoReads { get; private init; } = [];

        /// <summary>Whether to attribute the answers that arrive through a call.</summary>
        public bool ThroughACall { get; private init; }

        /// <summary>Commands to show every stopped read of, with the bytes around each.</summary>
        public IReadOnlyList<byte> Stops { get; private init; } = [];

        /// <summary>Whether to read both exits of every trainerbattle in the image.</summary>
        public bool Fights { get; private init; }

        /// <summary>Whether to hunt every place in the file that asks who knows a move.</summary>
        public bool WhoKnows { get; private init; }

        /// <summary>Whether to read the three coin commands and what they add up to.</summary>
        public bool Coins { get; private init; }

        /// <summary>Whether to count the scenes written as several doors into one room.</summary>
        public bool Entries { get; private init; }

        /// <summary>Read what sits between a shopkeeper and the floor. See WriteCounters.</summary>
        public bool Counters { get; private init; }

        /// <summary>Whether the playthrough may take the ferry, which makes its reach a ceiling.</summary>
        public bool Boat { get; private init; }

        /// <summary>
        /// Whether the walk may cross water. MODELLED, and a ceiling in the same way the boat
        /// is: the walker has always been able to swim and nothing has ever told it to, so the
        /// sea has been indistinguishable from a wall in every number this project prints.
        /// </summary>
        public bool Surf { get; private init; }

        /// <summary>
        /// Whether a trigger or an arrival script only runs when its own condition is met.
        /// <para>
        /// Off, the run is a ceiling: it takes arms of the story no single playthrough could
        /// take in one pass. On, it is a floor. Neither is the truth on its own.
        /// </para>
        /// </summary>
        public bool InOrder { get; private init; }

        /// <summary>
        /// One of the story's variables to follow through the run, in order.
        /// <para>
        /// Not a lever: it changes nothing the run does. It is the ordered half of the story's
        /// memory, and the half a dictionary of final values throws away.
        /// </para>
        /// </summary>
        public int? Watch { get; private init; }

        /// <summary>What the playthrough has to spend. Modelled, and nothing supplies it.</summary>
        public int Money { get; private init; }

        /// <summary>Whether the playthrough says yes to every offer. Modelled, and a ceiling.</summary>
        public bool SayYes { get; private init; }

        public string StartAt { get; private init; } = Beginning.MapId;

        public IReadOnlyDictionary<int, int> RoutineAnswers { get; private init; } = new Dictionary<int, int>();

        /// <summary>
        /// Numbers to put in the story's own variables before each script runs. <b>Modelled</b>,
        /// exactly as <c>--answer</c> is.
        /// <para>
        /// What stands in front of SAFFRON is <c>0x4001 != 0 AND 0x4001 != 1</c> — a counter,
        /// not a flag. A run cannot reach a counter's later values by walking, because the
        /// thing that advances it is a scene it has already run at the wrong moment. So it is
        /// handed in from outside and the difference is the measurement, which is the same
        /// bargain this project has made with the boat, the money and the routines.
        /// </para>
        /// </summary>
        public IReadOnlyDictionary<int, int> Variables { get; private init; } = new Dictionary<int, int>();

        /// <summary>Measure how many bytes each sequence command's arguments take.</summary>
        public bool SequenceWidths { get; private init; }

        /// <summary>Print one song's tracks byte by byte, with what was made of each.</summary>
        public int? OneSong { get; private init; }

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
            bool flagGates = false;
            bool closure = false;
            bool specialContracts = false;
            bool standard = false;
            bool theScan = false;
            bool personCommands = false;
            bool arrivals = false;
            bool theFloor = false;
            bool fieldEffects = false;
            var readFrom = new List<uint>();
            bool fights = false;
            bool whoKnows = false;
            var coins = false;
            bool entries = false;
            var counters = false;
            bool play = false;
            var whereFrom = new List<int>();
            var inTheImage = new List<int>();
            var climbFrom = new List<uint>();
            var whoWrites = new List<int>();
            var whoReads = new List<int>();
            var throughACall = false;
            var stops = new List<byte>();
            bool boat = false;
            var surf = false;
            var inOrder = false;
            int? watch = null;
            var money = 0;
            bool sayYes = false;
            string startAt = Beginning.MapId;
            var routineAnswers = new Dictionary<int, int>();
            var variables = new Dictionary<int, int>();
            bool sequenceWidths = false;
            int? oneSong = null;
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
                    case "--flags":
                        flagGates = true;
                        break;
                    case "--can-it-be-finished":
                        closure = true;
                        break;
                    case "--routines":
                        specialContracts = true;
                        break;
                    case "--standard":
                        standard = true;
                        break;
                    case "--the-scan":
                        theScan = true;
                        break;
                    case "--two-commands":
                        personCommands = true;
                        break;
                    case "--arrivals":
                        arrivals = true;
                        break;
                    case "--the-floor":
                        theFloor = true;
                        break;
                    case "--field-effects":
                        fieldEffects = true;
                        break;
                    case "--read-from":
                    {
                        foreach (string named in Next(args, ref i, "--read-from").Split(','))
                        {
                            if (TryNumber(named, out int blockAt)) readFrom.Add((uint)blockAt);
                        }

                        break;
                    }
                    case "--fights":
                        fights = true;
                        break;
                    case "--who-knows":
                        whoKnows = true;
                        break;
                    case "--coins":
                        coins = true;
                        break;
                    case "--entries":
                        entries = true;
                        break;
                    case "--counters":
                        counters = true;
                        break;
                    case "--play":
                        play = true;
                        break;
                    case "--in-order":
                        inOrder = true;
                        break;
                    case "--trace":
                        if (TryNumber(Next(args, ref i, "--trace"), out int followed)) watch = followed;

                        break;
                    case "--surf":
                        surf = true;
                        break;
                    case "--boat":
                        boat = true;
                        break;
                    case "--say-yes":
                        sayYes = true;
                        break;
                    case "--money":
                        if (TryNumber(Next(args, ref i, "--money"), out int purse)) money = purse;

                        break;
                    case "--where-from":
                    {
                        // One or more item ids, decimal or hex, so the three drinks can be
                        // asked about in one pass over four hundred maps rather than three.
                        foreach (string named in Next(args, ref i, "--where-from").Split(','))
                        {
                            if (TryNumber(named, out int itemId)) whereFrom.Add(itemId);
                        }

                        break;
                    }
                    case "--stops":
                    {
                        foreach (string named in Next(args, ref i, "--stops").Split(','))
                        {
                            if (TryNumber(named, out int code) && code is >= 0 and <= 0xFF)
                                stops.Add((byte)code);
                        }

                        break;
                    }
                    case "--through-a-call":
                        throughACall = true;

                        break;
                    case "--who-reads":
                    {
                        foreach (string named in Next(args, ref i, "--who-reads").Split(','))
                        {
                            if (TryNumber(named, out int looked)) whoReads.Add(looked);
                        }

                        break;
                    }
                    case "--who-writes":
                    {
                        foreach (string named in Next(args, ref i, "--who-writes").Split(','))
                        {
                            if (TryNumber(named, out int hunted)) whoWrites.Add(hunted);
                        }

                        break;
                    }
                    case "--climb":
                    {
                        // Any address at all. The climb was reachable only through a flag,
                        // and half the time the thing worth asking about is a block — "who
                        // runs this?" is the same question whatever is inside it.
                        foreach (string named in Next(args, ref i, "--climb").Split(','))
                        {
                            if (TryNumber(named, out int address)) climbFrom.Add((uint)address);
                        }

                        break;
                    }
                    case "--in-the-image":
                    {
                        // One or more flag numbers. Two of them is the interesting case: a
                        // scene that sets one and clears another is one piece of script, and
                        // nothing here has ever been able to look for that.
                        foreach (string named in Next(args, ref i, "--in-the-image").Split(','))
                        {
                            if (TryNumber(named, out int flag)) inTheImage.Add(flag);
                        }

                        break;
                    }
                    case "--from":
                        startAt = Next(args, ref i, "--from");
                        break;
                    case "--answer":
                    {
                        // routine=value, so an experiment can be run without a rebuild.
                        string[] halves = Next(args, ref i, "--answer").Split('=', 2);

                        if (halves.Length == 2
                            && TryNumber(halves[0], out int routine)
                            && TryNumber(halves[1], out int answerValue))
                        {
                            routineAnswers[routine] = answerValue;
                        }

                        break;
                    }
                    case "--var":
                    {
                        // variable=value, the same shape as --answer and just as modelled.
                        string[] halves = Next(args, ref i, "--var").Split('=', 2);

                        if (halves.Length == 2
                            && TryNumber(halves[0], out int whichVariable)
                            && TryNumber(halves[1], out int put))
                        {
                            variables[whichVariable] = put;
                        }

                        break;
                    }
                    case "--sequence-widths":
                        sequenceWidths = true;
                        break;
                    case "--song":
                        oneSong = int.Parse(Next(args, ref i, "--song"));
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
                RoutineAnswers = routineAnswers,
                Variables = variables,
                AnswerSweep = answerSweep,
                SpecialsOn = specialsOn,
                Shared = shared,
                Silent = silent,
                Sound = sound,
                FlagGates = flagGates,
                Closure = closure,
                SpecialContracts = specialContracts,
                Standard = standard,
                TheScan = theScan,
                PersonCommands = personCommands,
                Arrivals = arrivals,
                TheFloor = theFloor,
                FieldEffects = fieldEffects,
                ReadFrom = readFrom,
                Play = play,
                WhereFrom = whereFrom,
                InTheImage = inTheImage,
                ClimbFrom = climbFrom,
                WhoWrites = whoWrites,
                WhoReads = whoReads,
                ThroughACall = throughACall,
                Stops = stops,
                Fights = fights,
                WhoKnows = whoKnows,
                Coins = coins,
                Entries = entries,
                Counters = counters,
                Boat = boat,
                Surf = surf,
                InOrder = inOrder,
                Watch = watch,
                Money = money,
                SayYes = sayYes,
                StartAt = startAt,
                Answers = answers,
                SequenceWidths = sequenceWidths,
                OneSong = oneSong,
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
