using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// Finds the shared script every Pokémon Centre nurse hands her work to.
/// <para>
/// Three rounds went into asking what a <c>special</c> routine does, and the answer is
/// that it is code — the routine lives in the game's own instructions, not in any table,
/// and this project reads data. That is a boundary rather than a gap, and no amount of
/// dumping crosses it.
/// </para>
/// <para>
/// What is data is who calls what. Every nurse in FireRed is nine bytes — lock,
/// faceplayer, <c>call</c>, release, end — and they all call the same address. So the
/// shared script called by <em>exactly one</em> person on each of the most maps is the
/// nurse, whatever happens inside it. On a real image that is twenty maps and twenty
/// callers, and all twenty of them say "Welcome to our POKéMON CENTER!" — which this
/// does not check, because a rule that reads English is a rule that only works in
/// English.
/// </para>
/// <para>
/// Same shape as the table locators in milestone 0: find it by what it looks like, print
/// what was found, hardcode nothing.
/// </para>
/// </summary>
public static class HealerLocator
{
    /// <summary>
    /// How many maps a shared script must appear on before it can be the nurse.
    /// <para>
    /// A world with two towns in it has no business naming one of them the centre. The
    /// number is low enough not to matter on a real cartridge and high enough that a
    /// stripped test image locates nothing rather than something arbitrary.
    /// </para>
    /// </summary>
    private const int MinimumMaps = 5;

    /// <summary>
    /// The address the nurses call, or nothing when no candidate stands out.
    /// <para>
    /// Candidates are shared scripts with one caller per map. That alone leaves the
    /// wireless club attendant a close second, because there is one of her per centre
    /// too — so the tie-break is that a nurse hands off once and to nowhere else, while
    /// the attendant's script calls three different places.
    /// </para>
    /// </summary>
    public static uint? Locate(
        IEnumerable<(string MapId, IReadOnlyList<MapObject> Objects)> world,
        Rom rom,
        Action<string>? log = null)
    {
        var callers = new Dictionary<uint, int>();
        var maps = new Dictionary<uint, HashSet<string>>();
        var soleHandoff = new Dictionary<uint, int>();

        foreach ((string mapId, IReadOnlyList<MapObject> objects) in world)
        {
            foreach (MapObject person in objects.Where(o => o.HasScript))
            {
                uint[] handoffs = [.. HandoffsIn(rom, person.ScriptAddress)];

                foreach (uint target in handoffs.Distinct())
                {
                    callers[target] = callers.GetValueOrDefault(target) + 1;

                    if (!maps.TryGetValue(target, out HashSet<string>? on)) maps[target] = on = [];

                    on.Add(mapId);

                    // Somebody whose whole script is this one handoff. That is what a
                    // nurse is, and it is what tells her apart from everyone else who
                    // happens to be one-per-town.
                    if (handoffs.Length == 1) soleHandoff[target] = soleHandoff.GetValueOrDefault(target) + 1;
                }
            }
        }

        (uint Address, int Maps, int Callers, int Sole)? best = null;

        foreach ((uint target, HashSet<string> on) in maps)
        {
            int calling = callers[target];
            int sole = soleHandoff.GetValueOrDefault(target);

            // One person per map, every one of them doing nothing else. Anything with
            // two callers on a map is a pair of cut trees rather than a counter.
            if (on.Count < MinimumMaps || calling != on.Count || sole != calling) continue;

            // Most maps wins, and the lower address breaks a tie so that the same
            // cartridge always exports the same answer.
            if (best is { } found && (found.Maps > on.Count || (found.Maps == on.Count && found.Address < target)))
                continue;

            best = (target, on.Count, calling, sole);
        }

        if (best is not { } nurse)
        {
            log?.Invoke("  no healer: no shared script is called by exactly one person on five or more maps");
            return null;
        }

        log?.Invoke(
            $"  healer script: 0x{nurse.Address:X8}   {nurse.Maps} maps, {nurse.Callers} callers, " +
            "each handing off nowhere else");

        return nurse.Address;
    }

    /// <summary>Where a person's own script hands off to, before anybody else's runs.</summary>
    private static IEnumerable<uint> HandoffsIn(Rom rom, uint address)
    {
        foreach (ScriptCommand command in ScriptReader.Read(rom, address))
        {
            uint target = command.Code switch
            {
                ScriptCommands.Call or ScriptCommands.Goto => command.Pointer(),
                ScriptCommands.CallIf or ScriptCommands.GotoIf => command.Pointer(1),
                _ => 0,
            };

            if (target != 0 && rom.IsRomAddress(target)) yield return target;
        }
    }

    /// <summary>True when this person's script is the one the nurses share.</summary>
    public static bool Heals(Rom rom, MapObject person, uint? healerScript) =>
        healerScript is { } shared && person.HasScript && HandoffsIn(rom, person.ScriptAddress).Contains(shared);
}
