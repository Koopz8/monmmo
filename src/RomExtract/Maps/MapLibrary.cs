using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Maps;

/// <summary>
/// Every map on a cartridge, loadable by address without scanning again.
/// <para>
/// Locating the bank table means walking sixteen megabytes, which is fine once at
/// startup and not fine when a player opens a door. This holds the located tables so
/// a map change costs only decompressing and drawing the map itself.
/// </para>
/// </summary>
public sealed class MapLibrary
{
    private readonly Rom _rom;
    private readonly MapBankTable _banks;
    private readonly RegionNameTable? _names;
    private readonly int _indexBase;
    private readonly Dictionary<string, (int Bank, int Map, MapHeaderRecord Header)> _byId;

    private MapLibrary(
        Rom rom,
        MapBankTable banks,
        RegionNameTable? names,
        int indexBase)
    {
        _rom = rom;
        _banks = banks;
        _names = names;
        _indexBase = indexBase;

        _byId = banks.AllMaps.ToDictionary(
            m => WorldExporter.MapId(m.Bank, m.Map),
            m => m,
            StringComparer.OrdinalIgnoreCase);
    }

    public static MapLibrary Open(Rom rom, Action<string>? log = null)
    {
        MapBankTable banks = MapBankLocator.Locate(rom, log)
            ?? throw new InvalidDataException("No map bank table found. Is this a Generation III cartridge?");

        RegionNameTable? names = RegionNameLocator.Locate(rom, log);

        List<int> sectionIds = banks.AllMaps.Select(m => (int)m.Header.RegionSectionId).ToList();

        return new MapLibrary(rom, banks, names, names?.InferIndexBase(sectionIds) ?? 0);
    }

    public int Count => _byId.Count;

    public bool Contains(string mapId) => _byId.ContainsKey(mapId);

    /// <summary>Every map, loaded. Only for reports — this decompresses the lot.</summary>
    public IEnumerable<LoadedMap> All() => _byId.Values.Select(Load);

    /// <summary>
    /// Every script the maps hang off anything, with the map and a name for where it hangs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>All FIVE kinds.</b> People, triggers and signs are the three everybody thinks of; the
    /// other two are the scripts a map runs on arrival, and the entries in the map's own script
    /// list. Milestone 176 added the fifth to <c>WhatItIsWaitingFor.EveryScriptOn</c> after
    /// "nothing in the world sets this flag" turned out three times running to be a sentence
    /// about a scan that had never opened one.
    /// </para>
    /// <para>
    /// <b>And this list had three of them until 224.</b> It was created at 221 to end exactly
    /// this fault — five scans had rolled their own copy — and it unified them onto the SHORT
    /// version, so every instrument that uses it read 2331 entries where <c>--scripts</c> reads
    /// 2915. A shared wrong list is worse than five private ones: it agrees with itself.
    /// </para>
    /// <para>
    /// The conditional kinds of the on-load list are left out on purpose: their pointer is not a
    /// script, it is a table of variable, value and script, and those scripts arrive through the
    /// arrival list already. Reading a condition table as commands is a misread that would parse.
    /// </para>
    /// </remarks>
    public IEnumerable<(string MapId, string What, uint Address)> EveryScript() =>
        All().SelectMany(ScriptsOn);

    /// <summary>
    /// One map's scripts, all five kinds — split out so a test can hand it a map rather than a
    /// cartridge.
    /// <para>
    /// The list is asked of <see cref="Scripts.WhatItIsWaitingFor.EveryScriptOn"/> rather than
    /// written again here. <b>A sixth copy is how this list came to be short in the first
    /// place</b>: 221 unified five private copies onto a new one that had three kinds, and every
    /// instrument built on it read 2331 script entries where <c>--scripts</c> read 2915.
    /// </para>
    /// </summary>
    public static IEnumerable<(string MapId, string What, uint Address)> ScriptsOn(LoadedMap map) =>
        Scripts.WhatItIsWaitingFor
            .EveryScriptOn(
                WorldExporter.MapId(map.Bank, map.Number),
                map.Objects,
                map.Triggers,
                map.Signs,
                map.OnEntry,
                map.OnLoad)
            .Select(script => (script.MapId, script.What, script.Address));

    /// <summary>Loads a map by its <c>bank.map</c> address, or null when there is none.</summary>
    public LoadedMap? TryLoad(string mapId) =>
        _byId.TryGetValue(mapId, out (int Bank, int Map, MapHeaderRecord Header) found) ? Load(found) : null;

    /// <summary>Loads by name, preferring an exact match and then the largest map.</summary>
    public LoadedMap? TryLoadByName(string name)
    {
        var ranked = MapNameMatch.Rank(
            _banks.AllMaps,
            m => NameOf(m.Header),
            name,
            m => m.Header.Layout.BlockCount).ToList();

        return ranked.Count > 0 ? Load(ranked[0]) : null;
    }

    private LoadedMap Load((int Bank, int Map, MapHeaderRecord Header) entry)
    {
        RenderedMap picture = MapRenderer
            .Create(_rom, entry.Header.Layout)
            .Render(entry.Header.Layout);

        CollisionGrid raw = entry.Header.Layout.ReadCollision(_rom);

        List<Warp> warps = MapLinkExtractor.ReadWarps(_rom, entry.Header, raw.Width, raw.Height);

        // Doors are solid in the block data. The client has to open them for the same
        // reason the server does — it predicts every step against this grid, and a step
        // it predicts as blocked is a step it never sends.
        CollisionGrid collision = raw.WithOpen(warps.Select(w => w.Square));

        return new LoadedMap(
            NameOf(entry.Header),
            entry.Bank,
            entry.Map,
            picture.Width,
            picture.Height,
            picture.Rgba,
            collision)
        {
            // Read here rather than sent by the server. The client has the cartridge,
            // and both sides deriving them from the same image is the arrangement
            // collision already uses.
            Behaviours = entry.Header.Layout.ReadBehaviours(_rom),
            EventsPointer = entry.Header.EventsPointer,
            Music = entry.Header.Music,
            Objects = MapLinkExtractor.ReadObjects(_rom, entry.Header, collision.Width, collision.Height),
            Signs = MapLinkExtractor.ReadSigns(_rom, entry.Header, collision.Width, collision.Height),
            Triggers = MapLinkExtractor.ReadTriggers(_rom, entry.Header, collision.Width, collision.Height),
            Warps = warps,
            OnEntry = MapScripts.OnEntry(_rom, entry.Header),
            OnLoad = MapScripts.Read(_rom, entry.Header),
        };
    }

    private string NameOf(MapHeaderRecord header) =>
        _names?.Resolve(header.RegionSectionId, _indexBase) ?? $"SECTION {header.RegionSectionId}";
}
