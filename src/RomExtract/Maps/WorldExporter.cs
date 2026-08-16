using PokeMmo.RomExtract.Scripts;
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

                    // Which song plays here. Read off the header since the map work and
                    // carried nowhere until now: a number, which is all a world file is
                    // allowed to know about music and all either side needs to agree on.
                    Music = header.Music,

                    Encounters = encounters.GetValueOrDefault(id),
                    Connections = MapLinkExtractor.ReadConnections(rom, header, log),
                    Warps = MapLinkExtractor.ReadWarps(rom, header, grid.Width, grid.Height, log),
                    Objects = MapLinkExtractor.ReadObjects(rom, header, grid.Width, grid.Height, log),
                    Triggers = MapLinkExtractor.ReadTriggers(rom, header, grid.Width, grid.Height, log),

                    // The fifth list. Only the conditions travel — the addresses stay on
                    // the cartridge, exactly as they do for a trigger.
                    OnEntry = MapScripts.OnEntry(rom, header),

                    // And the doors that are on no square, read out of the same scripts.
                    // A map's warp records say where its doorways go; these say where its
                    // boats and lifts do, and until now nothing on the server's side of
                    // the split had ever been told they existed.
                    Doors = ScriptedDoors.On(rom, header, grid.Width, grid.Height, log),

                    // And the boat, if this map is a stop on it. The one door in this
                    // game that is neither a square nor a script — see Ferries, where the
                    // reason it can be read at all is written down.
                    Ferry = Ferries.DockOn(
                        rom, header, id, grid.Width, grid.Height,
                        MapLinkExtractor.ReadObjects(rom, header, grid.Width, grid.Height, log), log),
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

                            // What they take, on the same terms and for the same reason:
                            // read rather than run, because the branch Oak takes the
                            // parcel on is one a fresh save never reaches.
                            // Everything this script could ever hand over, read rather
                            // than run. Not the answer — which item, if any, depends on a
                            // save this has never seen — but the list of answers a client
                            // is allowed to give, which is exactly what a trigger's
                            // trainer ids already are.
                            //
                            // Both ways of handing something over, because there are two
                            // and this list knew one. `giveitem` names the item in its
                            // own arguments; the other writes the item and the count into
                            // 0x8000 and 0x8001 and calls a standard routine to do the
                            // rest. LOSTELLE in the BERRY FOREST hands over her father's
                            // parcel the second way, and the server refused it — "object
                            // 1 never hands over item 147" — with the item sitting in
                            // plain sight two commands above the call.
                            CanGive = [.. Scripts.ScriptReader.EverythingItCouldGive(rom, o.ScriptAddress)],

                            // Every fight this script could ever set up. Ten scripts in
                            // the game do, and two of them are the sleepers across the
                            // roads out of LAVENDER and CELADON — 33 maps each.
                            //
                            // Species, level, and a held item that is zero at all ten
                            // sites: `B6 8F 00 1E 00 00` is SNORLAX at 30.
                            CanFight =
                            [
                                .. Scripts.ScriptReader.Reachable(rom, o.ScriptAddress)
                                    .SelectMany(a => Scripts.ScriptReader.Read(rom, a))
                                    .Where(c => c.Code == 0xB6 && c.Arguments.Length >= 3)
                                    .Select(c => new WildFight(c.Word(), c.Arguments[2]))
                                    .Where(f => f.Species > 0 && f.Level is > 0 and <= 100)
                                    .Distinct(),
                            ],

                            TakesItemId = Scripts.ScriptReader.ReadAll(rom, o.ScriptAddress)
                                .FirstOrDefault(c => c.Code == 0x45)?.Word() ?? 0,
                            TakesCount = Math.Max(1, Scripts.ScriptReader.ReadAll(rom, o.ScriptAddress)
                                .FirstOrDefault(c => c.Code == 0x45)?.Word(2) ?? 1),
                        };

                        read = run.GivesItem is { } item
                            ? read with { GivesItemId = item, GivesCount = Math.Max(1, run.GivesCount) }
                            : read;

                        // And what winning a fight with them pays out, which is somewhere
                        // else entirely: the script a trainerbattle runs on being won.
                        // Nothing above reaches it — a run walks the path a fresh save
                        // takes and that path stops at the fight, and reading the object's
                        // own script does not follow a pointer the command carries rather
                        // than a goto. BROCK's TM39 sat there unread.
                        if (read.CanBeFought
                            && Scripts.ScriptReader.AfterTheFight(rom, o.ScriptAddress, read.TrainerId) is { } won)
                        {
                            Scripts.ScriptRun after = Scripts.ScriptRunner.Run(rom, won);

                            if (after.GivesItem is { } prize)
                                read = read with { WinsItemId = prize, WinsCount = Math.Max(1, after.GivesCount) };
                        }

                        return read;
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

        // And the daycare, which is the same kind of question and cannot be asked of one
        // map either: the attendants share no script, only the routines they call, so the
        // set that names them is only visible once every map's people have been read.
        Scripts.DaycareFound? daycare = Scripts.DaycareLocator.Locate(
            maps.Select(m => (m.Id, m.Objects)), rom, log);

        if (daycare is not null)
        {
            for (int i = 0; i < maps.Count; i++)
            {
                string mapId = maps[i].Id;

                maps[i] = maps[i] with
                {
                    Objects =
                    [
                        .. maps[i].Objects.Select(o => o with
                        {
                            MindsCreatures = Scripts.DaycareLocator.Minds(mapId, o, daycare),
                        }),
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

        // Two different questions, and conflating them cost BROCK his fight. The mark on
        // the record says somebody watches a corridor and walks over; the id in the
        // script says who they field. A gym leader has the second and not the first, so
        // the ones counted apart here are the ones you have to talk to.
        int fightable = maps.Sum(m => m.Objects.Count(o => o.CanBeFought));
        int spoken = maps.Sum(m => m.Objects.Count(o => o.CanBeFought && !o.IsTrainer));

        int prizes = maps.Sum(m => m.Objects.Count(o => o.WinsItem));

        log?.Invoke(
            $"  {fightable} of them will fight, {spoken} of those only when talked to, " +
            $"{prizes} handing something over for winning");

        int lying = maps.Sum(m => m.Objects.Count(o => o.GivesItem));

        log?.Invoke(
            lying == 0
                ? "  nothing is lying around to be picked up"
                : $"  {lying} items lying on the ground across {maps.Count(m => m.Objects.Any(o => o.GivesItem))} maps");

        int chatty = maps.Sum(m => m.Objects.Count(o => o.GivesItem && o.Talks));

        // A ball on the ground hands something over and says nothing, and every one of
        // them carries a flag in its own record — which is the only way it can be gone
        // when you come back, because its script does not set one. Four commands: two
        // arguments and a call into a standard routine.
        int balls = maps.Sum(m => m.Objects.Count(o => o.GivesItem && !o.Talks));
        int remembered = maps.Sum(m => m.Objects.Count(o => o.GivesItem && !o.Talks && o.HiddenBy > 0));

        log?.Invoke($"  {remembered} of those {balls} carry a flag to be gone by");

        // And the ones a fresh run never reaches. An item behind a yes/no or behind a
        // flag is written in the script in plain sight and handed over on a branch this
        // export does not walk — the two fossils in MT. MOON are the first of them, and
        // "Obtained the DOME FOSSIL!" on screen with nothing in the bag is what that
        // looks like from the player's side.
        int hidden = maps.Sum(m => m.Objects.Count(o =>
            o.HasScript && !o.GivesItem && o.CanGive.Count > 0));

        log?.Invoke($"  {hidden} more hand something over on a branch a fresh save does not take");

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

        int minders = maps.Sum(m => m.Objects.Count(o => o.MindsCreatures));

        log?.Invoke(
            minders == 0
                ? "  nobody minds creatures — there is nowhere to leave two together"
                : $"  {minders} of them will mind two across " +
                  $"{maps.Count(m => m.Objects.Any(o => o.MindsCreatures))} maps");

        ReportDanglingLinks(maps, log);

        NewGameState? opening = NewGameLocator.Locate(rom, log);

        if (opening is not null)
        {
            // Said out loud because it is the answer to a question this export used to
            // get wrong silently: how many of those people a brand new character can see.
            int hiddenAtStart = behindFlags.Count(o => opening.Flags.Contains(o.HiddenBy));

            log?.Invoke(
                $"  {hiddenAtStart} of those {behindFlags.Count} are hidden from the first frame " +
                $"by the {opening.Flags.Count} flags a new game sets");
        }

        // The ferry's ticket, read off the one dock that checks for one.
        var passes = new List<FerryPass>();

        foreach ((int bank, int number, MapHeaderRecord header) in banks.AllMaps)
        {
            if (maps.FirstOrDefault(m => m.Id == MapId(bank, number)) is not { Ferry: not null } dock) continue;

            foreach (FerryPass pass in Ferries.Passes(rom, header, dock.Width, dock.Height, log))
                if (!passes.Contains(pass)) passes.Add(pass);
        }

        return new WorldData(maps)
        {
            FlagsAtStart = opening?.Flags ?? [],

            // What the boat asks for. Read from whichever dock asks — only one of the
            // ten does, and it asks on behalf of all of them.
            FerryPasses = passes,
            VariablesAtStart = [.. (opening?.Variables ?? []).Select(v => new StartingVariable(v.Variable, v.Value))],
        };
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
