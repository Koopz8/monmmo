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
            found.AddRange(
                On(WorldExporter.MapId(map.Bank, map.Number), map.OnEntry, map.Triggers, written));
        }

        return found;
    }

    /// <summary>
    /// Both of one map's conditional lists, taking the records rather than a
    /// <see cref="MapLibrary"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same question, asked of the other list.</b> 247 found that a trigger's condition is
    /// a read nothing in this project counted; this is the half of <c>--arrivals</c> that was
    /// never pointed at it. A second private copy of this reading is how the shared script list
    /// came to be short for three milestones, so there is not one — both lists go through
    /// <see cref="For"/> and each condition carries which asked.
    /// </para>
    /// <para>
    /// Split out of the sweep so a fixture can reach it. The rule that a trigger's condition is
    /// marked as a square's lives here, and a rule inside a function that needs a whole cartridge
    /// is a rule no break can be aimed at.
    /// </para>
    /// </remarks>
    public static IEnumerable<Arrival> On(
        string mapId,
        IEnumerable<MapEntryScript> entries,
        IEnumerable<MapTrigger> triggers,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> written)
    {
        foreach (MapEntryScript entry in entries.Where(ReadsThatAreNotCommands.IsARead))
            yield return For(mapId, entry, written.GetValueOrDefault(entry.Variable));

        foreach (MapTrigger trigger in triggers.Where(ReadsThatAreNotCommands.IsARead))
        {
            yield return For(
                mapId,
                trigger.Variable,
                trigger.Value,
                trigger.ScriptAddress,
                written.GetValueOrDefault(trigger.Variable))
                with
                {
                    Asks = OnASquare,
                };
        }
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
    /// One list's conditions, counted by what this reading can say about each of them.
    /// </summary>
    /// <param name="Asks">Which list — <see cref="OnArrival"/> or <see cref="OnASquare"/>.</param>
    /// <param name="Conditions">How many conditions that list has.</param>
    /// <param name="Fire">How many get each verdict.</param>
    /// <param name="Middle">
    /// How many of that list's MIDDLE BUCKET get each of the four answers — the split 255 made of
    /// the two lists added together.
    /// </param>
    public sealed record Verdicts(
        string Asks,
        int Conditions,
        IReadOnlyDictionary<WhetherItCanFire, int> Fire,
        IReadOnlyDictionary<HowItIsReached, int> Middle);

    /// <summary>
    /// Every condition, split by WHICH LIST ASKED and then by what can be said about it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>250's rule, one level in.</b> 250 exists because <c>--arrivals</c> asked its
    /// "does anything write this variable at all" question of one of the two lists and reported
    /// an empty bucket; asked of the other, the same bucket held forty-three. 255 then split the
    /// middle bucket four ways and reported the split of the two lists ADDED TOGETHER, which is
    /// the same shape again: a total that mixes two populations cannot come back different for
    /// them. It does — the one-hop copy correction is worth everything on one list and nothing on
    /// the other.
    /// </para>
    /// <para>
    /// <b>Here rather than in the printer.</b> 255's four answers were lambdas inside a function
    /// that needs a whole cartridge, so no fixture could reach the rule and no break could be
    /// aimed at it — the fault this project fixed at 219, 221, 222 and 223 and walked back into.
    /// Grouping by <see cref="Arrival.Asks"/> is the rule this milestone is about, so it lives
    /// where a test can ask it directly.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Verdicts> ByList(
        IEnumerable<Arrival> conditions,
        IReadOnlyDictionary<int, WhatItCanHold> canHold,
        int ceiling)
    {
        var found = new List<Verdicts>();

        // The order is the order the lists are named in, not the order the cartridge happens to
        // produce them in, so two runs of this print the same table.
        foreach (string asks in new[] { OnArrival, OnASquare })
        {
            List<Arrival> of = [.. conditions.Where(a => a.Asks == asks)];

            if (of.Count == 0) continue;

            var fire = new Dictionary<WhetherItCanFire, int>();
            var middle = new Dictionary<HowItIsReached, int>();

            foreach (WhetherItCanFire which in Enum.GetValues<WhetherItCanFire>()) fire[which] = 0;

            foreach (HowItIsReached which in Enum.GetValues<HowItIsReached>()) middle[which] = 0;

            foreach (Arrival one in of)
            {
                fire[WhatAVariableCanHold.CanItFire(
                    canHold, one.Variable, one.Value, one.WrittenWithThis, ceiling)]++;

                // The middle bucket only — the four answers are about conditions a setvar cannot
                // satisfy, and counting the rest of the list into them would answer a different
                // question with the same words.
                if (one.NobodyWritesThisValue)
                    middle[WhatAVariableCanHold.HowReached(canHold, one.Variable, one.Value, ceiling)]++;
            }

            found.Add(new Verdicts(asks, of.Count, fire, middle));
        }

        return found;
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
