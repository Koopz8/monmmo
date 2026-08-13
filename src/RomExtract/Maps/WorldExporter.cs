using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Maps;

/// <summary>
/// Produces the collision-only world file the server runs on.
/// <para>
/// This is the one bridge between the cartridge and the server, and it is deliberately
/// a <em>file</em> rather than a reference. An operator runs this against their own
/// image; the server then reads dimensions and walkability and nothing else. No
/// graphics, no text, no audio, and no extractor code in the server's dependency graph.
/// </para>
/// </summary>
public static class WorldExporter
{
    /// <summary>Builds world data for every map the cartridge holds.</summary>
    public static WorldData Export(Rom rom, Action<string>? log = null)
    {
        MapBankTable banks = MapBankLocator.Locate(rom, log)
            ?? throw new InvalidDataException("No map bank table found.");

        RegionNameTable? names = RegionNameLocator.Locate(rom, log);

        List<int> sectionIds = banks.AllMaps.Select(m => (int)m.Header.RegionSectionId).ToList();
        int indexBase = names?.InferIndexBase(sectionIds) ?? 0;

        Dictionary<string, MapEncounters> encounters = EncounterExtractor
            .Extract(rom, log)
            .GroupBy(e => e.MapId)
            .ToDictionary(g => g.Key, g => g.First());

        var maps = new List<MapData>();

        foreach ((int bank, int number, MapHeaderRecord header) in banks.AllMaps)
        {
            try
            {
                CollisionGrid grid = header.Layout.ReadCollision(rom);
                var collision = new byte[grid.Width * grid.Height];

                for (int y = 0; y < grid.Height; y++)
                {
                    for (int x = 0; x < grid.Width; x++)
                        collision[y * grid.Width + x] = grid.CollisionAt(new GridPosition(x, y));
                }

                string id = MapId(bank, number);

                maps.Add(new MapData(
                    id,
                    names?.Resolve(header.RegionSectionId, indexBase) ?? $"SECTION {header.RegionSectionId}",
                    grid.Width,
                    grid.Height,
                    collision)
                {
                    // Behaviours are what tell the server which squares are grass, and
                    // are a byte a square — still no graphics, text or audio.
                    Behaviours = header.Layout.ReadBehaviours(rom),
                    Encounters = encounters.GetValueOrDefault(id),
                    Connections = MapLinkExtractor.ReadConnections(rom, header, log),
                    Warps = MapLinkExtractor.ReadWarps(rom, header, grid.Width, grid.Height, log),
                    Objects = MapLinkExtractor.ReadObjects(rom, header, grid.Width, grid.Height, log),
                    Triggers = MapLinkExtractor.ReadTriggers(rom, header, grid.Width, grid.Height, log),

                    // The fifth list. Only the conditions travel — the addresses stay on
                    // the cartridge, exactly as they do for a trigger.
                    OnEntry = MapScripts.OnEntry(rom, header),
                });
            }
            catch (Exception ex)
            {
                // One unreadable map should not cost the operator the whole world.
                log?.Invoke($"  skipped {MapId(bank, number)}: {ex.Message}");
            }
        }

        // What each person hands over, which is a fact about their script rather than
        // about the record they stand on. Read here rather than at extraction because
        // running a script is a different job from reading an object event.
        for (int i = 0; i < maps.Count; i++)
        {
            maps[i] = maps[i] with
            {
                // And what arriving somewhere hands over, which nobody is talked to for.
                // Walking into the shop in Viridian is what hands over the parcel the
                // rest of the story turns on, and there is no person in that exchange
                // for the usual machinery to hang off.
                OnEntry =
                [
                    .. maps[i].OnEntry.Select(e =>
                    {
                        if (!e.HasScript) return e;

                        Scripts.ScriptRun arriving = Scripts.ScriptRunner.Run(rom, e.ScriptAddress);

                        return arriving.GivesItem is { } given
                            ? e with { GivesItemId = given, GivesCount = Math.Max(1, arriving.GivesCount) }
                            : e;
                    }),
                ],
                Objects =
                [
                    .. maps[i].Objects.Select(o =>
                    {
                        if (!o.HasScript) return o;

                        Scripts.ScriptRun run = Scripts.ScriptRunner.Run(rom, o.ScriptAddress);

                        // Read rather than run, and only for this one question. A run
                        // walks the path today's save chooses, and every rock-smash rock
                        // in the game sits behind a badge check — so running one with a
                        // fresh save jumps straight past the command that says what it
                        // is. What a thing *is* does not depend on whether you can get
                        // at it yet.
                        Scripts.ScriptCommand? handover = Scripts.ScriptReader
                            .ReadAll(rom, o.ScriptAddress)
                            .FirstOrDefault(c => c.Code == 0x79 && c.Arguments.Length >= 4);

                        MapObject read = o with
                        {
                            Talks = run.Pages.Count > 0,
                            ShiftedBy = Scripts.ScriptReader.ReadAll(rom, o.ScriptAddress)
                                .FirstOrDefault(c => c.Code == 0x7C)?.Word() ?? 0,

                            // Named rather than resolved. The species is sometimes a
                            // variable — the three starters are one script reading
                            // whichever ball was pressed — and which ball that was is a
                            // fact about a save, not about a cartridge.
                            GivesSpecies = handover?.Word() ?? 0,
                            GivesLevel = handover?.Word(2) ?? 0,
                        };

                        return run.GivesItem is { } item
                            ? read with { GivesItemId = item, GivesCount = Math.Max(1, run.GivesCount) }
                            : read;
                    }),
                ],
            };
        }

        // Located rather than known, and only once every map's people have been read:
        // the nurse is whoever hands their work to the script that one person on each of
        // the most maps hands theirs to, which is not a question a single map can answer.
        uint? healer = Scripts.HealerLocator.Locate(
            maps.Select(m => (m.Id, m.Objects)), rom, log);

        if (healer is not null)
        {
            for (int i = 0; i < maps.Count; i++)
            {
                maps[i] = maps[i] with
                {
                    Objects =
                    [
                        .. maps[i].Objects.Select(o => o with { Heals = Scripts.HealerLocator.Heals(rom, o, healer) }),
                    ],
                };
            }
        }

        int withEncounters = maps.Count(m => m.Encounters is not null);
        int warps = maps.Sum(m => m.Warps.Count);
        int connections = maps.Sum(m => m.Connections.Count);

        log?.Invoke($"  exported {maps.Count} maps, {withEncounters} with encounters");
        int objects = maps.Sum(m => m.Objects.Count);
        int trainers = maps.Sum(m => m.Objects.Count(o => o.IsTrainer));

        log?.Invoke($"  {warps} warps, {connections} edge connections");
        log?.Invoke($"  {objects} objects, {trainers} of them trainers");

        int lying = maps.Sum(m => m.Objects.Count(o => o.GivesItem));

        log?.Invoke(
            lying == 0
                ? "  nothing is lying around to be picked up"
                : $"  {lying} items lying on the ground across {maps.Count(m => m.Objects.Any(o => o.GivesItem))} maps");

        int chatty = maps.Sum(m => m.Objects.Count(o => o.GivesItem && o.Talks));

        if (chatty > 0)
            log?.Invoke($"  {chatty} of those are people who say something as they hand it over");

        var triggers = maps.SelectMany(m => m.Triggers).ToList();

        if (triggers.Count > 0)
        {
            log?.Invoke(
                $"  {triggers.Count} squares run a script when you walk onto them, across " +
                $"{maps.Count(m => m.Triggers.Count > 0)} maps, " +
                $"{triggers.Count(t => t.CanBeFought)} of them a fight");
        }

        var handing = maps.SelectMany(m => m.Objects).Where(o => o.GivesMon).ToList();

        if (handing.Count > 0)
        {
            log?.Invoke(
                $"  {handing.Count} people hand over a monster, " +
                $"{handing.Count(o => o.GivesSpecies >= MapObject.FirstVariable)} of them whichever one you pick");
        }

        var behindFlags = maps.SelectMany(m => m.Objects).Where(o => o.HiddenBy != 0).ToList();

        if (behindFlags.Count > 0)
        {
            log?.Invoke(
                $"  {behindFlags.Count} of {maps.Sum(m => m.Objects.Count)} people are only there while a flag " +
                $"says so, across {behindFlags.Select(o => o.HiddenBy).Distinct().Count()} flags");
        }

        var arrivals = maps.SelectMany(m => m.OnEntry).ToList();

        if (arrivals.Count > 0)
        {
            log?.Invoke(
                $"  {arrivals.Count} things run on arriving somewhere, across " +
                $"{maps.Count(m => m.OnEntry.Count > 0)} maps, " +
                $"gated on {arrivals.Select(e => e.Variable).Distinct().Count()} variables");
        }

        var obstacles = maps
            .SelectMany(m => m.Objects)
            .Where(o => o.ShiftedBy != 0)
            .GroupBy(o => o.ShiftedBy)
            .OrderByDescending(g => g.Count())
            .ToList();

        if (obstacles.Count > 0)
        {
            log?.Invoke(
                $"  {obstacles.Sum(g => g.Count())} things in the way across " +
                $"{maps.Count(m => m.Objects.Any(o => o.ShiftedBy != 0))} maps, " +
                $"needing {obstacles.Count} different moves: " +
                string.Join(", ", obstacles.Select(g => $"{g.Count()} x move {g.Key}")));
        }

        int healers = maps.Sum(m => m.Objects.Count(o => o.Heals));

        log?.Invoke(
            healers == 0
                ? "  nobody heals — losing is still the only way to put a party back on its feet"
                : $"  {healers} of them heal a party across {maps.Count(m => m.Objects.Any(o => o.Heals))} maps");

        ReportDanglingLinks(maps, log);

        return new WorldData(maps);
    }

    /// <summary>
    /// Says how many links point at maps that are not in the export.
    /// <para>
    /// Some are expected — a map that failed to read takes its neighbours' links with
    /// it. What this catches is the other case: a whole-file misread where the numbers
    /// look fine and every single link dangles, which a total on its own would hide.
    /// </para>
    /// </summary>
    private static void ReportDanglingLinks(List<MapData> maps, Action<string>? log)
    {
        if (log is null) return;

        var known = maps.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        int danglingWarps = maps.Sum(m => m.Warps.Count(w => !known.Contains(w.TargetMapId)));
        int danglingEdges = maps.Sum(m => m.Connections.Count(c => !known.Contains(c.MapId)));

        if (danglingWarps == 0 && danglingEdges == 0) return;

        log($"  {danglingWarps} warps and {danglingEdges} connections lead to maps that are not here");
    }

    /// <summary>The identifier both sides use for a map: the game's own bank and map numbers.</summary>
    public static string MapId(int bank, int number) => $"{bank}.{number}";
}
