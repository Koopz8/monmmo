namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// A script that hands itself to code nothing else in the world calls.
/// </summary>
/// <param name="MapId">Which map it is on.</param>
/// <param name="What">Which person, sign or trigger it is.</param>
/// <param name="Alone">The routines it calls that no other script calls.</param>
public sealed record OneOfAKind(string MapId, string What, IReadOnlyList<int> Alone);

/// <summary>
/// The machines that are not tiles.
/// <para>
/// A storage machine in a healing centre is a behaviour byte and nothing else — no sign,
/// no person, no script. That is why it took a behaviour test to find it (milestone 68)
/// and why it is the only one this client can open: there is nothing else about it to
/// read.
/// </para>
/// <para>
/// The one in the player's bedroom is the opposite. Its square carries behaviour 0x00, it
/// is a sign, and its script says one line and then hands the whole of itself to special
/// routines — code, which this project has never followed and has said so from the start.
/// There is no byte to find for it. Looking for one is the mistake this exists to stop
/// somebody making twice.
/// </para>
/// <para>
/// What can be said about such a script without following anything is how alone it is. A
/// routine called from three hundred scripts is a common service; a routine called from
/// exactly one is that script's own behaviour, sitting in code where nothing can reach it.
/// So the measure is the count of routines a site is the only caller of, and the sites
/// this ranks highest are the ones that cannot be reproduced from data at all.
/// </para>
/// </summary>
public static class ScriptedMachines
{
    /// <summary>
    /// How many callers a routine may have and still count as one script's own. One,
    /// because two callers is already a shared service rather than a private one.
    /// </summary>
    public const int Alone = 1;

    /// <summary>
    /// The sites that call routines nobody else calls, the most alone first.
    /// </summary>
    public static List<OneOfAKind> Find(IEnumerable<SpecialCall> calls)
    {
        List<SpecialCall> all = [.. calls];

        // Sites per routine, counted once each — a script calling the same routine twice
        // is still one caller, and PC scripts do exactly that.
        var callers = new Dictionary<int, HashSet<string>>();

        foreach (SpecialCall call in all)
        {
            if (!callers.TryGetValue(call.Routine, out HashSet<string>? who)) callers[call.Routine] = who = [];

            who.Add(Site(call));
        }

        var mine = new Dictionary<string, (SpecialCall Any, SortedSet<int> Routines)>();

        foreach (SpecialCall call in all)
        {
            if (callers[call.Routine].Count > Alone) continue;

            string site = Site(call);

            if (!mine.TryGetValue(site, out var found)) mine[site] = found = (call, []);

            found.Routines.Add(call.Routine);
        }

        return
        [
            .. mine.Values
                .Select(m => new OneOfAKind(m.Any.MapId, m.Any.What, [.. m.Routines]))
                .OrderByDescending(m => m.Alone.Count)
                .ThenBy(m => m.MapId)
                .ThenBy(m => m.What),
        ];
    }

    private static string Site(SpecialCall call) => $"{call.MapId} {call.What}";
}
