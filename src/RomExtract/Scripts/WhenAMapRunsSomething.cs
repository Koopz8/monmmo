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

        /// <summary>What asked — a map's header on arrival, or a square somebody walks onto.</summary>
        /// <remarks>
        /// <b>Two lists, one reading.</b> A trigger is a square that runs a script when a
        /// variable holds a value; an arrival condition is a map that runs a script when a
        /// variable holds a value. They are the same question and 247 found that out the hard
        /// way, so this reads both rather than growing a second copy of itself — which is 224's
        /// fault and the reason this project has a rule about unifying onto the one that knows
        /// the most.
        /// </remarks>
        public string Asks { get; init; } = OnArrival;
    }

    /// <summary>A map's header, when the player arrives.</summary>
    public const string OnArrival = "on arrival";

    /// <summary>A square, when the player walks onto it.</summary>
    public const string OnASquare = "on a square";

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

            foreach (MapEntryScript entry in map.OnEntry.Where(ReadsThatAreNotCommands.IsARead))
                found.Add(For(mapId, entry, written.GetValueOrDefault(entry.Variable)));

            // THE SAME QUESTION, ASKED OF THE OTHER LIST. 247 found that a trigger's condition
            // is a read nothing in this project counted; this is the half of --arrivals that
            // was never pointed at it. A second private copy of this reading is how the shared
            // script list came to be short for three milestones, so there is not one.
            foreach (MapTrigger trigger in map.Triggers.Where(ReadsThatAreNotCommands.IsARead))
            {
                found.Add(
                    For(
                        mapId,
                        trigger.Variable,
                        trigger.Value,
                        trigger.ScriptAddress,
                        written.GetValueOrDefault(trigger.Variable))
                        with
                        {
                            Asks = OnASquare,
                        });
            }
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
        string mapId, MapEntryScript entry, IReadOnlyDictionary<int, int>? written) =>
        For(mapId, entry.Variable, entry.Value, entry.ScriptAddress, written);

    /// <summary>The same, taking the three fields both records carry.</summary>
    public static Arrival For(
        string mapId,
        int variable,
        int value,
        uint address,
        IReadOnlyDictionary<int, int>? written)
    {
        IReadOnlyDictionary<int, int> values = written ?? new Dictionary<int, int>();

        return new Arrival(
            mapId,
            variable,
            value,
            address,
            values.Values.Sum(),
            values.GetValueOrDefault(value),
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
