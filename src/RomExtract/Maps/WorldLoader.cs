using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Maps;

/// <summary>One loaded map: its picture, its walkability, and where it came from.</summary>
public sealed record LoadedMap(
    string Name,
    int Bank,
    int Number,
    int PixelWidth,
    int PixelHeight,
    byte[] Rgba,
    CollisionGrid Collision)
{
    /// <summary>People and things standing on this map.</summary>
    public IReadOnlyList<MapObject> Objects { get; init; } = [];

    /// <summary>The doors and stairways on this map.</summary>
    public IReadOnlyList<Warp> Warps { get; init; } = [];

    /// <summary>Squares that run a script when somebody walks onto them.</summary>
    public IReadOnlyList<MapTrigger> Triggers { get; init; } = [];

    /// <summary>
    /// What this map runs on arrival, when one of its variables says so.
    /// <para>
    /// With the script addresses, unlike the server's copy — this side has the cartridge,
    /// which is the whole arrangement collision and triggers already use.
    /// </para>
    /// </summary>
    public IReadOnlyList<MapEntryScript> OnEntry { get; init; } = [];

    /// <summary>
    /// The signs, notice boards and bookshelves on this map.
    /// <para>
    /// Client-side only, and deliberately so. A sign has nothing the server needs to
    /// arbitrate: nobody stands still to be read, nothing changes hands, and the words
    /// are on an image the server has never seen. It is read exactly where the map is.
    /// </para>
    /// </summary>
    public IReadOnlyList<MapSign> Signs { get; init; } = [];

    /// <summary>The map as a PNG, which is the simplest thing for a renderer to consume.</summary>
    public byte[] ToPng() => Graphics.PngWriter.ToArray(PixelWidth, PixelHeight, Rgba);
}

/// <summary>
/// Turns a cartridge on disk into something a client can draw and walk around.
/// <para>
/// Deliberately free of any engine type, and living here rather than in the client,
/// so the whole load path can be exercised by the normal test suite. The engine layer
/// above it only has to turn a byte array into a texture.
/// </para>
/// </summary>
public static class WorldLoader
{
    /// <summary>
    /// Opens a cartridge and loads one map, chosen by <c>bank.map</c> address when
    /// given and by name otherwise.
    /// </summary>
    public static LoadedMap Load(string romPath, string? mapName, string? mapAddress) =>
        Load(Rom.Load(romPath), mapName, mapAddress);

    /// <summary>
    /// Loads a map from an already-open cartridge. Locating the tables scans the whole
    /// image several times, so the client opens it once and reuses it.
    /// </summary>
    public static LoadedMap Load(Rom rom, string? mapName, string? mapAddress)
    {

        MapBankTable banks = MapBankLocator.Locate(rom)
            ?? throw new InvalidDataException("No map bank table found. Is this a Generation III cartridge?");

        RegionNameTable? names = RegionNameLocator.Locate(rom);

        List<int> sectionIds = banks.AllMaps.Select(m => (int)m.Header.RegionSectionId).ToList();
        int indexBase = names?.InferIndexBase(sectionIds) ?? 0;

        (int Bank, int Map, MapHeaderRecord Header) chosen = Choose(banks, names, indexBase, mapName, mapAddress);

        RenderedMap picture = MapRenderer
            .Create(rom, chosen.Header.Layout)
            .Render(chosen.Header.Layout);

        return new LoadedMap(
            names?.Resolve(chosen.Header.RegionSectionId, indexBase) ?? $"SECTION {chosen.Header.RegionSectionId}",
            chosen.Bank,
            chosen.Map,
            picture.Width,
            picture.Height,
            picture.Rgba,
            chosen.Header.Layout.ReadCollision(rom));
    }

    private static (int Bank, int Map, MapHeaderRecord Header) Choose(
        MapBankTable banks,
        RegionNameTable? names,
        int indexBase,
        string? mapName,
        string? mapAddress)
    {
        List<(int Bank, int Map, MapHeaderRecord Header)> all = banks.AllMaps.ToList();

        if (!string.IsNullOrWhiteSpace(mapAddress))
        {
            string[] parts = mapAddress.Split('.');

            if (parts.Length == 2 && int.TryParse(parts[0], out int bank) && int.TryParse(parts[1], out int number))
            {
                foreach (var candidate in all)
                {
                    if (candidate.Bank == bank && candidate.Map == number) return candidate;
                }
            }

            throw new InvalidDataException($"No map at address '{mapAddress}'.");
        }

        if (!string.IsNullOrWhiteSpace(mapName) && names is not null)
        {
            // Exact names win, then the largest — the outdoor map rather than one of
            // the interiors that share its name.
            var matches = Core.World.MapNameMatch.Rank(
                all,
                m => names.Resolve(m.Header.RegionSectionId, indexBase),
                mapName,
                m => m.Header.Layout.BlockCount).ToList();

            if (matches.Count > 0) return matches[0];

            throw new InvalidDataException($"No map name contains '{mapName}'.");
        }

        if (all.Count == 0) throw new InvalidDataException("The cartridge holds no maps.");

        return all[0];
    }
}
