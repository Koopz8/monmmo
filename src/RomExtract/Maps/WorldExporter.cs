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
                });
            }
            catch (Exception ex)
            {
                // One unreadable map should not cost the operator the whole world.
                log?.Invoke($"  skipped {MapId(bank, number)}: {ex.Message}");
            }
        }

        int withEncounters = maps.Count(m => m.Encounters is not null);
        int warps = maps.Sum(m => m.Warps.Count);
        int connections = maps.Sum(m => m.Connections.Count);

        log?.Invoke($"  exported {maps.Count} maps, {withEncounters} with encounters");
        log?.Invoke($"  {warps} warps, {connections} edge connections");

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
