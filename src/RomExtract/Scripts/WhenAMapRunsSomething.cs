using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// The condition on every script a map runs on arrival, and whether anything in the file can
/// ever satisfy it.
/// </summary>
/// <remarks>
/// <para>
/// An arrival script is not a script the map runs — it is a script the map runs <b>when a
/// variable holds a particular value</b>. 227 and 228 found these scripts asking eleven routines
/// nothing else asks and moving eleven flags nothing else moves, and the natural next question is
/// which of them can run at all.
/// </para>
/// <para>
/// <b>The condition is a variable and a value, and both halves matter.</b> A variable something
/// writes is not the same as a variable something writes THAT VALUE to; a story counter written
/// with 1, 2 and 3 leaves an arrival script waiting on 4 forever. Anything that checked only the
/// variable would report every one of these as reachable.
/// </para>
/// </remarks>
public static class WhenAMapRunsSomething
{
    /// <param name="Variable">The variable the condition names.</param>
    /// <param name="Value">The value it has to hold.</param>
    /// <param name="Written">How many places in the file write that variable at all.</param>
    /// <param name="WrittenWithThis">How many of those write this value.</param>
    /// <param name="Values">Every value anything writes to that variable, with how many places.</param>
    public sealed record Arrival(
        string MapId,
        int Variable,
        int Value,
        uint Address,
        int Written,
        int WrittenWithThis,
        IReadOnlyDictionary<int, int> Values)
    {
        /// <summary>
        /// Nothing in the file writes this variable at all — the condition is a code boundary
        /// with an address on it.
        /// </summary>
        public bool NothingWritesIt => Written == 0;

        /// <summary>
        /// Something writes the variable, but nothing ever writes THIS value. The script is
        /// waiting on a number no script produces.
        /// </summary>
        public bool NobodyWritesThisValue => Written > 0 && WrittenWithThis == 0;
    }

    /// <summary>
    /// The variables these arrival conditions LOOK AT.
    /// <para>
    /// <b>An arrival condition is a read of a variable and it is not a command.</b> Every sweep in
    /// this project walks a script stream and decides what a number is by which operand of which
    /// command named it, so none of them has ever counted one of these — and 245 reported
    /// <c>0x407C</c> as looked at nowhere in sixteen megabytes while nineteen maps consulted it on
    /// arrival.
    /// </para>
    /// <para>
    /// <b>An entry with no script is not a read.</b> The list in a map's header ends with an
    /// all-zero record, and its variable field is a nought that names nothing — counting it would
    /// rescue variable 0 from every deaf list for free, on every map in the game.
    /// </para>
    /// <para>
    /// Split out of the sweep, and taking the entries rather than a <see cref="MapLibrary"/>,
    /// because a rule that needs a whole cartridge is a rule no fixture can reach — which is the
    /// fault four milestones running found by breaking a guard and getting green back.
    /// </para>
    /// </summary>
    public static IReadOnlyCollection<int> LookedAt(IEnumerable<MapEntryScript> entries) =>
        [.. entries.Where(IsARead).Select(e => e.Variable).Distinct()];

    /// <summary>
    /// Whether one header entry is a read at all — it is, unless it runs nothing.
    /// </summary>
    /// <remarks>
    /// Where the rule above actually lives, so that the caller counting conditions and the caller
    /// counting variables cannot come apart. Two readings of one shape is how they always do.
    /// </remarks>
    public static bool IsARead(MapEntryScript entry) => entry.ScriptAddress != 0;

    /// <summary>
    /// Every arrival condition on every map, with what the file does about the variable it names.
    /// </summary>
    /// <param name="written">
    /// For each variable, the values written to it anywhere in the map scan, with how many places
    /// write each.
    /// </param>
    public static List<Arrival> In(
        MapLibrary library, IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> written)
    {
        var found = new List<Arrival>();

        foreach (LoadedMap map in library.All())
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            foreach (MapEntryScript entry in map.OnEntry.Where(IsARead))
                found.Add(For(mapId, entry, written.GetValueOrDefault(entry.Variable)));
        }

        return found;
    }

    /// <summary>
    /// One condition, against what the file writes to the variable it names.
    /// <para>
    /// Split out of the sweep because the sweep needs a whole cartridge, and the rule this
    /// milestone rests on — <b>a variable something writes is not a variable something writes
    /// THAT VALUE to</b> — is three lines of it.
    /// </para>
    /// </summary>
    public static Arrival For(
        string mapId, MapEntryScript entry, IReadOnlyDictionary<int, int>? written)
    {
        IReadOnlyDictionary<int, int> values = written ?? new Dictionary<int, int>();

        return new Arrival(
            mapId,
            entry.Variable,
            entry.Value,
            entry.ScriptAddress,
            values.Values.Sum(),
            values.GetValueOrDefault(entry.Value),
            values);
    }

    /// <summary>
    /// Every value written to every variable by the scripts the maps open, and how many BYTE
    /// POSITIONS write each.
    /// </summary>
    /// <remarks>
    /// Places, not reads: a block hanging off nineteen maps writes the same value at one address
    /// nineteen times, and counting those as nineteen writers would make a variable written in
    /// one place look well covered.
    /// </remarks>
    public static IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> WhatIsWritten(
        Rom rom, MapLibrary library) =>
        Tally(Writes(rom, library));

    private static IEnumerable<(int Variable, int Value, int At)> Writes(Rom rom, MapLibrary library)
    {
        foreach ((string _, string _, uint address) in library.EveryScript())
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (WhatIsSet(command) is { } set) yield return (set.Variable, set.Value, command.Offset);
            }
        }
    }

    /// <summary>
    /// The writes gathered by variable and value, counted in BYTE POSITIONS.
    /// <para>
    /// Places, not reads. A block hanging off nineteen maps writes the same value at one address
    /// nineteen times, and counting those as nineteen writers would make a variable written in
    /// one place look well covered — which is the fault 220 and 223 spent two milestones on, in a
    /// new instrument, the seventh time this project has walked into it.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> Tally(
        IEnumerable<(int Variable, int Value, int At)> writes)
    {
        var at = new Dictionary<int, Dictionary<int, HashSet<int>>>();

        foreach ((int variable, int value, int offset) in writes)
        {
            if (!at.TryGetValue(variable, out Dictionary<int, HashSet<int>>? values))
                at[variable] = values = [];

            if (!values.TryGetValue(value, out HashSet<int>? places)) values[value] = places = [];

            places.Add(offset);
        }

        return at.ToDictionary(
            e => e.Key,
            e => (IReadOnlyDictionary<int, int>)e.Value.ToDictionary(v => v.Key, v => v.Value.Count));
    }

    /// <summary>
    /// The variable and value a command writes, or null.
    /// <para>
    /// <b>Only the write whose value is in the command.</b> A <c>copyvar</c> or an <c>addvar</c>
    /// puts something in a variable too, and what it puts there is not readable from the bytes —
    /// so a condition satisfied only by one of those reads here as satisfied by nothing, which is
    /// the direction this is allowed to be wrong in. Said out loud rather than left implied.
    /// </para>
    /// </summary>
    public static (int Variable, int Value)? WhatIsSet(ScriptCommand command) =>
        command.Code == SetVar && command.Arguments.Length >= 4
            ? (command.Word(), command.Word(2))
            : null;

    private const byte SetVar = 0x16;
}
