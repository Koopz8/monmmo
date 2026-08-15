using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// What one daycare turned out to be, and which of them holds two.
/// </summary>
public sealed record DaycareFound(
    IReadOnlyList<int> Routines,
    IReadOnlyList<(string MapId, int LocalId)> Attendants,
    IReadOnlyList<(string MapId, int LocalId)> HoldTwo);

/// <summary>
/// Finds the people who will mind a creature for you, and works out which of them will
/// mind two.
/// <para>
/// Same move as <see cref="HealerLocator"/>, one notch harder. A nurse is found by a
/// shared script — every one of them hands off to the same address, so there is a single
/// number to look for. Nobody shares a script here: the two daycare attendants in this
/// cartridge have entirely separate scripts and nothing in common except <em>who they
/// call into the game's own code</em>.
/// </para>
/// <para>
/// So the signature is a set of <c>special</c> routines rather than an address. A routine
/// called by exactly one person on each of a handful of maps is a counter of some kind;
/// several such routines naming the <em>same</em> people is a subsystem. On a real image
/// five routines agree on one pair of people and the runner-up pair has two, which is the
/// margin this rule needs and does not have to be told about.
/// </para>
/// <para>
/// What a routine does is still unreadable — that boundary is described at length on
/// <see cref="SpecialCalls"/> and none of this crosses it. What is read is who calls what,
/// and what the script does about the answer.
/// </para>
/// </summary>
public static class DaycareLocator
{
    /// <summary>
    /// The most maps one place's own routines may appear on.
    /// <para>
    /// A daycare is a building, not a service counter in every town. Past this, a routine
    /// is the game's general machinery — the one that answers how many are in the party is
    /// called on twenty-two maps, and it is called by these attendants too.
    /// </para>
    /// </summary>
    private const int MostMaps = 6;

    /// <summary>
    /// How many routines have to name the same people before they are a subsystem.
    /// <para>
    /// Two is a coincidence this cartridge contains twice over — a pair of department
    /// store clerks, and a pair of people either end of an errand. Three is the first
    /// number that is not, and the real answer here is five.
    /// </para>
    /// </summary>
    private const int LeastRoutines = 3;

    /// <summary>How many are being minded, when a script bothers to tell one from two.</summary>
    private const int Two = 2;

    /// <summary>How far past a call to look for the compare that reads its answer.</summary>
    private const int Window = 4;

    private const byte Compare = 0x21;

    /// <summary>Where a routine that was not told where to answer, answers.</summary>
    private const int TheAnswer = 0x800D;

    /// <summary>
    /// The daycare, or nothing when no set of routines stands out.
    /// <para>
    /// Two questions, in order. Who minds creatures — the people named by the largest
    /// agreeing set of routines. And which of them minds <em>two</em> — the ones whose own
    /// routines have their answers told apart at two.
    /// </para>
    /// <para>
    /// The second question is the one this project actually needs answered, and it is why
    /// this does not stop at the first. FireRed has two of these places and they are not
    /// the same service: one holds a single creature and hands it back stronger, and the
    /// other holds a pair and is where an egg comes from. A rule that found "a daycare"
    /// and stopped would let somebody leave two creatures at a counter this cartridge
    /// only ever puts one behind.
    /// </para>
    /// </summary>
    public static DaycareFound? Locate(
        IEnumerable<(string MapId, IReadOnlyList<MapObject> Objects)> world,
        Rom rom,
        Action<string>? log = null)
    {
        var callers = new Dictionary<int, List<(string MapId, int LocalId)>>();
        var maps = new Dictionary<int, HashSet<string>>();
        var scripts = new Dictionary<(string MapId, int LocalId), uint>();

        foreach ((string mapId, IReadOnlyList<MapObject> objects) in world)
        {
            foreach (MapObject person in objects.Where(o => o.HasScript))
            {
                scripts[(mapId, person.LocalId)] = person.ScriptAddress;

                foreach (int routine in RoutinesIn(rom, person.ScriptAddress).Distinct())
                {
                    if (!callers.TryGetValue(routine, out List<(string, int)>? who)) callers[routine] = who = [];
                    if (!maps.TryGetValue(routine, out HashSet<string>? on)) maps[routine] = on = [];

                    who.Add((mapId, person.LocalId));
                    on.Add(mapId);
                }
            }
        }

        // One caller per map, and few enough maps to be a place rather than a service.
        var tight = callers
            .Where(entry => entry.Value.Count == maps[entry.Key].Count)
            .Where(entry => entry.Value.Count is >= 2 and <= MostMaps)
            .ToList();

        // Routines that name exactly the same people are describing the same thing.
        var agreed = tight
            .GroupBy(entry => string.Join("+", entry.Value.Select(c => $"{c.MapId}:{c.LocalId}").Order()))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .FirstOrDefault();

        if (agreed is null || agreed.Count() < LeastRoutines)
        {
            log?.Invoke(
                $"  no daycare: no {LeastRoutines} routines are called by one person each on the same 2-{MostMaps} maps");

            return null;
        }

        int[] routines = [.. agreed.Select(g => g.Key).Order()];
        (string MapId, int LocalId)[] attendants = [.. agreed.First().Value.Order()];

        (string MapId, int LocalId)[] holdTwo =
            [.. attendants.Where(a => TellsTwoFromOne(rom, scripts[a], maps))];

        log?.Invoke(
            $"  daycare routines: {string.Join(", ", routines.Select(r => $"0x{r:X4}"))} — " +
            $"one caller each on {attendants.Length} maps");

        foreach ((string mapId, int localId) in attendants)
        {
            log?.Invoke(
                $"    {mapId} person {localId}   " +
                (holdTwo.Contains((mapId, localId))
                    ? "minds two: its own routines are told apart at two"
                    : "minds one: its own routines are never told apart at two, so it is not a daycare this models"));
        }

        return new DaycareFound(routines, attendants, holdTwo);
    }

    /// <summary>True when this person is somewhere two can be left together.</summary>
    public static bool Minds(string mapId, MapObject person, DaycareFound? found) =>
        found is not null && found.HoldTwo.Contains((mapId, person.LocalId));

    /// <summary>
    /// Whether this script ever tells two from one.
    /// <para>
    /// A counter is a routine whose answer gets compared against a number. What the
    /// numbers are is the whole reading: a place that only ever asks "is there one?"
    /// answers nought or one, and a place that asks "are there two?" is a place that can
    /// hold two. The Route 5 attendant's own routines are compared against nought and one
    /// and never anything else; the Four Island one's are compared against two twice over.
    /// </para>
    /// <para>
    /// Only the place's own routines count. Both attendants call the game's general
    /// machinery — one routine that answers the party size is compared against six by
    /// twenty-two different people — and letting those in would have every counter in the
    /// game telling two from one.
    /// </para>
    /// </summary>
    private static bool TellsTwoFromOne(Rom rom, uint address, Dictionary<int, HashSet<string>> maps)
    {
        List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

        for (int i = 0; i < commands.Count; i++)
        {
            if (commands[i].Code is not (SpecialCalls.Special or SpecialCalls.SpecialVar)) continue;

            bool told = commands[i].Code == SpecialCalls.SpecialVar;

            int routine = told ? commands[i].Word(2) : commands[i].Word();
            int answer = told ? commands[i].Word() : TheAnswer;

            if (maps.GetValueOrDefault(routine, []).Count > MostMaps) continue;

            for (int j = i + 1; j < commands.Count && j <= i + Window; j++)
            {
                // Somebody else has answered; anything after this is about them.
                if (commands[j].Code is SpecialCalls.Special or SpecialCalls.SpecialVar) break;

                if (commands[j].Code != Compare) continue;
                if (commands[j].Word() != answer) continue;
                if (commands[j].Word(2) == Two) return true;
            }
        }

        return false;
    }

    /// <summary>Every routine this script calls into the game's own code.</summary>
    private static IEnumerable<int> RoutinesIn(Rom rom, uint address) =>
        ScriptReader.ReadAll(rom, address)
            .Where(c => c.Code is SpecialCalls.Special or SpecialCalls.SpecialVar)
            .Select(c => c.Code == SpecialCalls.Special ? c.Word() : c.Word(2));
}
