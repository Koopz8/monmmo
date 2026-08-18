using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// How many times the map scan reads each command, and how many BYTE POSITIONS those reads are.
/// </summary>
/// <remarks>
/// <para>
/// <b>The error bar on every map-scan number in this project, in one table.</b> The scan walks
/// every script the maps hang off anything and follows calls, so a block shared by nineteen
/// Pokémon Centres is decoded nineteen times. Every sweep built on it counts reads unless it was
/// written to do otherwise, and 220 and 223 found that two of them were not.
/// </para>
/// <para>
/// Those two were corrected one at a time by hand. This asks the question of all of them at once:
/// for each command code, how far apart the two numbers are. A code whose reads and places are
/// equal has nothing to correct anywhere; a code read twenty-nine times per address has it
/// waiting in every instrument that counts it.
/// </para>
/// </remarks>
public static class WhatTheScanOpens
{
    /// <param name="Code">The command's opcode.</param>
    /// <param name="Reads">How many times the scan decodes it.</param>
    /// <param name="Places">How many distinct byte positions those reads are.</param>
    /// <param name="Scripts">How many script entries reach it.</param>
    /// <param name="Maps">How many maps those entries are on.</param>
    public sealed record ACode(byte Code, int Reads, int Places, int Scripts, int Maps)
    {
        /// <summary>
        /// Reads per byte position. <b>One means nothing anywhere counted this code wrongly;
        /// anything above it is how wrong.</b>
        /// </summary>
        public double Over => Places == 0 ? 0 : (double)Reads / Places;
    }

    /// <param name="Kind">
    /// Which of the five kinds hangs the script: <c>person</c>, <c>trigger</c>, <c>sign</c>,
    /// <c>on arrival</c>, <c>on load</c>.
    /// </param>
    /// <param name="Only">
    /// <b>Byte positions no other kind opens.</b> The number that says what dropping this kind
    /// would cost, and the number that would have caught 224 at 221 — a list missing
    /// <c>on load</c> was missing every position only <c>on load</c> reaches, and nothing
    /// printed that.
    /// </param>
    /// <param name="Routines">Routines asked by scripts of this kind.</param>
    /// <param name="RoutinesOnly">
    /// Routines <b>no other kind</b> asks. 224 recovered two kinds and twenty routines appeared;
    /// this is the number that says so directly rather than by comparing two runs.
    /// </param>
    public sealed record AKind(
        string Kind,
        int Entries,
        int Addresses,
        int Reads,
        int Places,
        int Only,
        int Routines,
        IReadOnlyList<int> RoutinesOnly);

    /// <param name="Entries">Script entries the maps hang off people, triggers and signs.</param>
    /// <param name="Addresses">How many distinct addresses those entries point at.</param>
    /// <param name="Reads">Commands decoded, counting a shared block once per entry that reaches it.</param>
    /// <param name="Places">How many distinct byte positions the scan decodes at all.</param>
    public sealed record Overall(int Entries, int Addresses, int Reads, int Places);

    /// <param name="Places">Byte positions this kind's scripts decode.</param>
    /// <param name="Routines">Routine numbers this kind's scripts ask.</param>
    public sealed record Gathered(
        int Entries,
        int Addresses,
        int Reads,
        IReadOnlyCollection<int> Places,
        IReadOnlyCollection<int> Routines);

    /// <summary>
    /// The per-kind rows, from what was gathered — <b>where the ALONE columns are decided</b>.
    /// <para>
    /// Split out of the sweep because the sweep needs a whole cartridge. A break that made the
    /// routines column the kind's own set rather than what only it asks came back green against
    /// every test in this file while the rule lived inside <see cref="ByKind"/>, which is the
    /// fifth time in nine milestones.
    /// </para>
    /// </summary>
    public static List<AKind> Assemble(IReadOnlyDictionary<string, Gathered> byKind)
    {
        Dictionary<string, IReadOnlyCollection<int>> places =
            byKind.ToDictionary(e => e.Key, e => e.Value.Places);

        Dictionary<string, IReadOnlyCollection<int>> routines =
            byKind.ToDictionary(e => e.Key, e => e.Value.Routines);

        return
        [
            .. byKind
                .Select(e => new AKind(
                    e.Key,
                    e.Value.Entries,
                    e.Value.Addresses,
                    e.Value.Reads,
                    e.Value.Places.Count,
                    OnlyHere(places, e.Key),
                    e.Value.Routines.Count,
                    OnlyIn(routines, e.Key)))
                .OrderByDescending(k => k.Only)
                .ThenByDescending(k => k.Places),
        ];
    }

    /// <summary>Which of the five kinds a script's name says it is.</summary>
    public static string KindOf(string what) =>
        what.StartsWith("on ", StringComparison.Ordinal)
            ? string.Join(" ", what.Split(' ').Take(2))
            : what.Split(' ')[0];

    /// <summary>
    /// The same reading, split by which kind of thing hangs the script — and for each kind, the
    /// byte positions <b>no other kind opens</b>.
    /// </summary>
    public static List<AKind> ByKind(Rom rom, MapLibrary library)
    {
        var entries = new Dictionary<string, int>();
        var addresses = new Dictionary<string, HashSet<uint>>();
        var reads = new Dictionary<string, int>();
        var places = new Dictionary<string, HashSet<int>>();
        var routines = new Dictionary<string, HashSet<int>>();

        foreach ((string _, string what, uint address) in library.EveryScript())
        {
            string kind = KindOf(what);

            entries[kind] = entries.GetValueOrDefault(kind) + 1;

            if (!addresses.TryGetValue(kind, out HashSet<uint>? at)) addresses[kind] = at = [];

            at.Add(address);

            if (!places.TryGetValue(kind, out HashSet<int>? opened)) places[kind] = opened = [];

            if (!routines.TryGetValue(kind, out HashSet<int>? asked)) routines[kind] = asked = [];

            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                reads[kind] = reads.GetValueOrDefault(kind) + 1;
                opened.Add(command.Offset);

                if (command.Code == SpecialCalls.Special && command.Arguments.Length >= 2)
                    asked.Add(command.Word());

                if (command.Code == SpecialCalls.SpecialVar && command.Arguments.Length >= 4)
                    asked.Add(command.Word(2));
            }
        }

        return Assemble(entries.Keys.ToDictionary(
            kind => kind,
            kind => new Gathered(
                entries[kind],
                addresses[kind].Count,
                reads.GetValueOrDefault(kind),
                places[kind],
                routines[kind])));
    }

    /// <summary>
    /// Byte positions this kind opens that no other kind does — what dropping it would cost.
    /// <para>
    /// <b>The number that would have caught 224 at 221.</b> A shared list missing <c>on load</c>
    /// was missing every position only <c>on load</c> reaches, and nothing anywhere printed that
    /// figure — so the loss showed up three milestones later as twenty routines appearing out of
    /// nowhere.
    /// </para>
    /// <para>
    /// Public and taking plain collections, because the sweep it is used in needs a whole
    /// cartridge and a rule only reachable through one is a rule no test reaches.
    /// </para>
    /// </summary>
    public static int OnlyHere(IReadOnlyDictionary<string, IReadOnlyCollection<int>> places, string kind) =>
        OnlyIn(places, kind).Count;

    /// <summary>
    /// The items one kind has that no other kind has — the same rule as <see cref="OnlyHere"/>
    /// and the thing it counts.
    /// <para>
    /// Asked twice: of byte positions, where it says what dropping a kind would cost, and of
    /// routine numbers, where it says which routines only that kind ever asks. <b>One rule.</b>
    /// The nine of <c>on load</c> and the eleven of <c>on arrival</c> are the twenty routines
    /// 224 found by comparing two runs of the whole instrument, arrived at here directly.
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> OnlyIn(
        IReadOnlyDictionary<string, IReadOnlyCollection<int>> byKind, string kind)
    {
        var elsewhere = new HashSet<int>();

        foreach ((string other, IReadOnlyCollection<int> mine) in byKind)
        {
            if (other != kind) elsewhere.UnionWith(mine);
        }

        return [.. byKind[kind].Where(at => !elsewhere.Contains(at)).Order()];
    }

    /// <summary>Every command the scan decodes, by code, in reads and in places.</summary>
    public static (Overall Whole, List<ACode> ByCode) Of(Rom rom, MapLibrary library)
    {
        var reads = new Dictionary<byte, int>();
        var places = new Dictionary<byte, HashSet<int>>();
        var scripts = new Dictionary<byte, int>();
        var maps = new Dictionary<byte, HashSet<string>>();
        var entries = 0;
        var addresses = new HashSet<uint>();
        var everywhere = new HashSet<int>();
        var read = 0;

        foreach ((string mapId, string _, uint address) in library.EveryScript())
        {
            entries++;
            addresses.Add(address);

            var here = new HashSet<byte>();

            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                read++;
                everywhere.Add(command.Offset);

                reads[command.Code] = reads.GetValueOrDefault(command.Code) + 1;

                if (!places.TryGetValue(command.Code, out HashSet<int>? at)) places[command.Code] = at = [];

                at.Add(command.Offset);

                if (!maps.TryGetValue(command.Code, out HashSet<string>? on)) maps[command.Code] = on = [];

                on.Add(mapId);

                // Once per entry, however many times the entry reads this code — the entry count
                // is about how many doors lead here, not about how long the block is.
                if (here.Add(command.Code))
                    scripts[command.Code] = scripts.GetValueOrDefault(command.Code) + 1;
            }
        }

        List<ACode> byCode =
        [
            .. reads.Keys
                .Select(code => new ACode(
                    code,
                    reads[code],
                    places[code].Count,
                    scripts.GetValueOrDefault(code),
                    maps[code].Count))
                .OrderByDescending(c => c.Over)
                .ThenByDescending(c => c.Reads),
        ];

        return (new Overall(entries, addresses.Count, read, everywhere.Count), byCode);
    }
}
