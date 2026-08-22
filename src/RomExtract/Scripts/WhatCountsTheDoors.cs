using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// One save variable on one map: every value a <c>setvar</c> there gives it, beside the number of
/// ways into that map.
/// </summary>
/// <param name="Doors">
/// Distinct MAPS whose warps name this one. The raw warp count is <paramref name="Warps"/> and
/// both are printed, because choosing between them after seeing the answer is how a filter gets
/// chosen by its result (79).
/// </param>
public sealed record AVariableOnAMap(
    int Variable,
    string MapId,
    string Name,
    IReadOnlyList<int> Values,
    int Doors,
    int Warps)
{
    /// <summary>Whether the variable takes exactly one value per way in.</summary>
    public bool Counts => Values.Count == Doors;
}

/// <summary>
/// Which save variable counts a map's doors (297).
/// <para>
/// This came out of 297's own control. The one kind of copy-into-a-slot that scores above its
/// floor — a copy out of the SAVE — adds no routine at all to what 296 already reads, and what it
/// found instead is <b><c>0x403A</c></b>: a variable named nowhere in sixteen megabytes but on
/// four maps, handed to <c>special 0x0132</c>, and taking exactly as many values on three of
/// those four as there are maps that can warp to them.
/// </para>
/// <para>
/// The floor is the same question asked of every variable the map scan writes on every map it
/// writes it on, <b>and it has to have the one-door pairs counted out</b>: a map with one way in
/// is matched by any variable written once, and that is what most of the raw 37.6% is (71, 264).
/// </para>
/// </summary>
public static class WhatCountsTheDoors
{
    /// <summary>The variable this reading is about.</summary>
    public const int TheLift = 0x403A;

    /// <summary>The routine it is handed to.</summary>
    public const int TheRoutine = 0x0132;

    /// <summary>Every (variable, map) pair a <c>setvar</c> in the map scan writes.</summary>
    public static IReadOnlyList<AVariableOnAMap> All(Rom rom, MapLibrary library)
    {
        var wrote = new Dictionary<(int Variable, string MapId), SortedSet<int>>();

        foreach ((string mapId, string _, uint address) in library.EveryScript())
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (command.Code != WhatACopyIntoASlotIs.SetVar) continue;
                if (command.Word() < WhatACopyIntoASlotIs.FirstSaved) continue;
                if (command.Word() >= SpecialCalls.FirstArgument) continue;

                if (!wrote.TryGetValue((command.Word(), mapId), out SortedSet<int>? values))
                    wrote[(command.Word(), mapId)] = values = [];

                values.Add(command.Word(2));
            }
        }

        var into = new Dictionary<string, List<string>>();
        var names = new Dictionary<string, string>();

        foreach (LoadedMap map in library.All())
        {
            string id = $"{map.Bank}.{map.Number}";
            names[id] = map.Name;

            foreach (Warp warp in map.Warps)
            {
                if (!into.TryGetValue(warp.TargetMapId, out List<string>? from))
                    into[warp.TargetMapId] = from = [];

                from.Add(id);
            }
        }

        return
        [
            .. wrote.Select(w => new AVariableOnAMap(
                w.Key.Variable,
                w.Key.MapId,
                names.GetValueOrDefault(w.Key.MapId, "?"),
                [.. w.Value],
                into.GetValueOrDefault(w.Key.MapId, []).Distinct().Count(),
                into.GetValueOrDefault(w.Key.MapId, []).Count)),
        ];
    }

    /// <summary>
    /// The base rate, on maps with at least <paramref name="leastDoors"/> ways in.
    /// </summary>
    /// <remarks>
    /// <b>A hit on a one-door map is not a hit.</b> It is the blank entry of 264's item table in
    /// another shape: any variable written once matches it, so counting those makes a test that
    /// cannot fail. The command prints the whole ladder rather than one cut, because the cut
    /// would otherwise be chosen by the answer (79).
    /// </remarks>
    public static (int Pairs, int Match) Floor(
        IReadOnlyList<AVariableOnAMap> all, int leastDoors)
    {
        List<AVariableOnAMap> live = [.. all.Where(v => v.Doors >= leastDoors)];

        return (live.Count, live.Count(v => v.Counts));
    }
}
