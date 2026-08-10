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

        return new LoadedMap(
            NameOf(entry.Header),
            entry.Bank,
            entry.Map,
            picture.Width,
            picture.Height,
            picture.Rgba,
            entry.Header.Layout.ReadCollision(_rom));
    }

    private string NameOf(MapHeaderRecord header) =>
        _names?.Resolve(header.RegionSectionId, _indexBase) ?? $"SECTION {header.RegionSectionId}";
}
