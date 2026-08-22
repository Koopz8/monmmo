using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// What the fifth list moves, and what nothing else moves (307).
/// <para>
/// <b>The map scan has read the fifth list since 224 and the RUN has never opened one.</b>
/// <see cref="MapScripts.OnEntry"/> takes the two CONDITIONAL kinds — a table of variable, value
/// and script — and hands them to the walk as things to run on arrival. The other kinds point
/// straight at a script with no condition on it, and <see cref="MapScripts"/> says in as many
/// words why they are left alone: <i>running one means knowing WHEN the cartridge runs it, which
/// is not written down anywhere in the data</i>. That reservation is about running them and it is
/// right. What nobody had printed is <b>what they move</b>.
/// </para>
/// <para>
/// This is 239's shape a second time. Signs were the fourth list and the exported map record
/// carried none of them, so the walk went over a world with 519 sign scripts it could not see.
/// The fifth list is exported as CONDITIONS ONLY, so the walk goes over a world whose
/// unconditional map scripts do not exist — and one of them is the answer to the question 306
/// left open.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Every count here has its denominator beside it and its control in the same table.</b> The
/// number that matters is not "the fifth list moves 61 flags" — it is how many of them no other
/// kind moves, which is what dropping the list actually costs, and the other four kinds' rows are
/// printed so that number can be read against something (68).
/// </para>
/// </remarks>
public static class TheFifthList
{
    /// <summary>One of the five kinds of script a map hangs, and what it alone moves.</summary>
    /// <param name="Kind">person, trigger, sign, on arrival, on load.</param>
    /// <param name="Scripts">Script entries of this kind in the whole world.</param>
    /// <param name="Addresses">Distinct addresses those entries point at (224: not the same number).</param>
    /// <param name="Maps">Maps carrying at least one.</param>
    /// <param name="TurnedOn">Flags a <c>setflag</c> in one of them names.</param>
    /// <param name="TurnedOff">Flags a <c>clearflag</c> in one of them names.</param>
    /// <param name="Only">
    /// Flags this kind moves that <b>no other kind moves either way</b> — the count that says
    /// what dropping this kind costs, and the only one of these columns that can come back empty.
    /// </param>
    public sealed record AKind(
        string Kind,
        int Scripts,
        int Addresses,
        int Maps,
        IReadOnlyList<int> TurnedOn,
        IReadOnlyList<int> TurnedOff,
        IReadOnlyList<int> Only);

    /// <summary>One entry of a map's own script list, by its kind byte.</summary>
    /// <param name="Kind">The byte. 2 and 4 are the conditional ones the walk already runs.</param>
    /// <param name="Entries">How many entries in the world carry it.</param>
    /// <param name="Maps">How many maps.</param>
    /// <param name="Addresses">Distinct pointers.</param>
    /// <param name="Resolves">How many of those pointers are ROM addresses.</param>
    /// <param name="ReadsAsScript">
    /// How many decode to a proper end. A kind whose pointer is not a script at all would show
    /// here, and reading a condition table as commands is a misread that parses (the conditional
    /// kinds are excluded from this column for exactly that reason).
    /// </param>
    public sealed record AKindByte(
        byte Kind,
        int Entries,
        int Maps,
        int Addresses,
        int Resolves,
        int ReadsAsScript);

    /// <summary>A flag only the fifth list moves, and what is standing behind it.</summary>
    /// <param name="Flag">The flag.</param>
    /// <param name="TurnedOn">Whether an unconditional map script sets it.</param>
    /// <param name="TurnedOff">Whether one clears it.</param>
    /// <param name="People">Objects in the world this flag hides.</param>
    /// <param name="Maps">Maps those objects stand on.</param>
    /// <param name="Where">Which maps' fifth-list scripts move it, with the kind byte.</param>
    public sealed record AFlagOnlyHere(
        int Flag,
        bool TurnedOn,
        bool TurnedOff,
        int People,
        int Maps,
        IReadOnlyList<string> Where)
    {
        /// <summary>Whether anything is hidden by it — a flag holding nobody costs the walk nothing.</summary>
        public bool Gates => People > 0;
    }

    /// <param name="Kinds">All five, so this table controls itself.</param>
    /// <param name="ByKind">The map script list by kind byte, conditional and not.</param>
    /// <param name="OnlyHere">
    /// Every flag the unconditional entries move that no other kind of script moves, with what it
    /// hides. Sorted by how many people, because a count is not a ranking (3).
    /// </param>
    public sealed record Reading(
        IReadOnlyList<AKind> Kinds,
        IReadOnlyList<AKindByte> ByKind,
        IReadOnlyList<AFlagOnlyHere> OnlyHere)
    {
        /// <summary>The fifth list's row, which is the one this reading is about.</summary>
        public AKind FifthList => Kinds.Single(k => k.Kind == OnLoad);

        /// <summary>How many of the only-here flags hide somebody.</summary>
        public int Gating => OnlyHere.Count(f => f.Gates);

        /// <summary>How many objects in the world those flags hide between them.</summary>
        public int PeopleHeld => OnlyHere.Sum(f => f.People);
    }

    /// <summary>The name this project gives the unconditional entries in a map's own script list.</summary>
    public const string OnLoad = "on load";

    /// <summary>
    /// Read the whole world's five lists and score each one by what it alone moves.
    /// </summary>
    /// <remarks>
    /// The four kinds the walk already runs are gathered from the same
    /// <see cref="WhatItIsWaitingFor.EveryScriptOn"/> the rest of this project uses, rather than
    /// from a private copy of "every script on a map" — 224 is the milestone about what a private
    /// copy costs, and it has been re-fixed four times since.
    /// </remarks>
    public static Reading Read(Rom rom, IReadOnlyCollection<LoadedMap> maps)
    {
        // Flags by the kind of script that moves them. Two sets per kind and never one
        // classification, because a flag set in one place and cleared in another is the
        // commonest shape there is.
        var on = new Dictionary<string, HashSet<int>>();
        var off = new Dictionary<string, HashSet<int>>();
        var scripts = new Dictionary<string, int>();
        var addresses = new Dictionary<string, HashSet<uint>>();
        var mapsWith = new Dictionary<string, HashSet<string>>();

        // Which maps' fifth-list scripts move each flag, for the sorted list at the end.
        var whereOnlyHere = new Dictionary<int, List<string>>();

        var byKind = new Dictionary<byte, (HashSet<string> Maps, HashSet<uint> At, int Entries, int Resolves, int Script)>();

        foreach (LoadedMap map in maps)
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            foreach (SetsAFlag script in WhatItIsWaitingFor.EveryScriptOn(
                         mapId, map.Objects, map.Triggers, map.Signs, map.OnEntry, map.OnLoad))
            {
                string kind = KindOf(script.What);

                scripts[kind] = scripts.GetValueOrDefault(kind) + 1;

                if (!addresses.TryGetValue(kind, out HashSet<uint>? at)) addresses[kind] = at = [];
                at.Add(script.Address);

                if (!mapsWith.TryGetValue(kind, out HashSet<string>? seen)) mapsWith[kind] = seen = [];
                seen.Add(mapId);

                (IReadOnlyCollection<int> turnedOn, IReadOnlyCollection<int> turnedOff) =
                    WhatItIsWaitingFor.Touches(rom, [script]);

                if (!on.TryGetValue(kind, out HashSet<int>? sets)) on[kind] = sets = [];
                if (!off.TryGetValue(kind, out HashSet<int>? clears)) off[kind] = clears = [];

                foreach (int flag in turnedOn) sets.Add(flag);
                foreach (int flag in turnedOff) clears.Add(flag);

                if (kind != OnLoad) continue;

                foreach (int flag in turnedOn.Concat(turnedOff).Distinct())
                {
                    if (!whereOnlyHere.TryGetValue(flag, out List<string>? where))
                        whereOnlyHere[flag] = where = [];

                    string said = $"{mapId} {script.What}";
                    if (!where.Contains(said)) where.Add(said);
                }
            }

            // And the list itself, by kind byte — including the two conditional kinds, so the
            // reader can see how much of the list the walk already runs.
            foreach (MapScriptEntry entry in map.OnLoad)
            {
                (HashSet<string> Maps, HashSet<uint> At, int Entries, int Resolves, int Script) row =
                    byKind.GetValueOrDefault(entry.Kind, ([], [], 0, 0, 0));

                row.Maps.Add(mapId);
                row.At.Add(entry.Pointer);

                bool resolves = rom.ToOffsetOrNull(entry.Pointer) is not null;

                // A conditional kind's pointer is a table of conditions and is NOT a script, so
                // asking whether it decodes is asking the wrong question of it. Left out rather
                // than counted as a failure — a misread that parses is worse than a blank.
                bool script = !MapScripts.IsConditional(entry.Kind)
                              && resolves
                              && ScriptReader.ReadsAsAScript(rom, entry.Pointer);

                byKind[entry.Kind] = (
                    row.Maps,
                    row.At,
                    row.Entries + 1,
                    row.Resolves + (resolves ? 1 : 0),
                    row.Script + (script ? 1 : 0));
            }
        }

        var kinds = new List<AKind>();

        foreach (string kind in scripts.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            HashSet<int> mine = [.. on.GetValueOrDefault(kind, []), .. off.GetValueOrDefault(kind, [])];

            HashSet<int> others =
            [
                .. scripts.Keys.Where(k => k != kind)
                    .SelectMany(k => on.GetValueOrDefault(k, []).Concat(off.GetValueOrDefault(k, []))),
            ];

            kinds.Add(new AKind(
                kind,
                scripts[kind],
                addresses[kind].Count,
                mapsWith[kind].Count,
                [.. on.GetValueOrDefault(kind, []).Order()],
                [.. off.GetValueOrDefault(kind, []).Order()],
                [.. mine.Where(f => !others.Contains(f)).Order()]));
        }

        // What each only-here flag hides. Counted off the object records, which is a different
        // structure from the scripts entirely — a flag nothing hides is a flag whose cost to the
        // walk is nought however many scripts move it.
        var held = new Dictionary<int, (int People, HashSet<string> Maps)>();

        foreach (LoadedMap map in maps)
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            foreach (MapObject person in map.Objects.Where(o => o.HiddenBy != 0))
            {
                (int People, HashSet<string> Maps) row = held.GetValueOrDefault(person.HiddenBy, (0, []));
                row.Maps.Add(mapId);
                held[person.HiddenBy] = (row.People + 1, row.Maps);
            }
        }

        AKind fifth = kinds.Single(k => k.Kind == OnLoad);

        var onlyHere = new List<AFlagOnlyHere>();

        foreach (int flag in fifth.Only)
        {
            (int People, HashSet<string> Maps) behind = held.GetValueOrDefault(flag, (0, []));

            onlyHere.Add(new AFlagOnlyHere(
                flag,
                fifth.TurnedOn.Contains(flag),
                fifth.TurnedOff.Contains(flag),
                behind.People,
                behind.Maps.Count,
                whereOnlyHere.GetValueOrDefault(flag, [])));
        }

        return new Reading(
            kinds,
            [.. byKind.Select(p => new AKindByte(
                    p.Key, p.Value.Entries, p.Value.Maps.Count, p.Value.At.Count, p.Value.Resolves, p.Value.Script))
                .OrderBy(k => k.Kind)],
            [.. onlyHere.OrderByDescending(f => f.People).ThenBy(f => f.Flag)]);
    }

    /// <summary>
    /// Which of the five kinds a <see cref="SetsAFlag"/> came from.
    /// <para>
    /// Off the label that list already writes, rather than off a second enumeration of the same
    /// maps. Two passes over one set of tables is how 251 lost <c>copyvar</c> and 258 lost half a
    /// walk; this way the kinds cannot come to disagree about what a script is.
    /// </para>
    /// </summary>
    public static string KindOf(string what) =>
        what.StartsWith("person", StringComparison.Ordinal) ? "person"
        : what.StartsWith("trigger", StringComparison.Ordinal) ? "trigger"
        : what.StartsWith("sign", StringComparison.Ordinal) ? "sign"
        : what.StartsWith("on arrival", StringComparison.Ordinal) ? "on arrival"
        : OnLoad;
}
