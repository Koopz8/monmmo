using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>One place the boat calls at, with the script that proves it.</summary>
public sealed record FerryStop(string MapId, string What, int Number, int Routine)
{
    public bool IsAPerson => What.StartsWith("person ", StringComparison.Ordinal);

    public int Attendant =>
        IsAPerson && int.TryParse(What["person ".Length..], out int id) ? id : 0;
}

/// <summary>
/// The boat, found by what the scripts around it say rather than by what its routine does.
/// <para>
/// The archipelago is a second world: 246 maps reachable from PALLET TOWN and 179 that are
/// not, in 30 pieces, with no warp and no map edge and — milestone 98 — no scripted door
/// between them. What crosses is a routine, a call into the game's own ARM code by number,
/// and this project has never been able to follow one.
/// </para>
/// <para>
/// It did not need to. A routine is unreadable; the script around it is not, and the script
/// says everything. This one has a shape nothing else in the cartridge has:
/// </para>
/// <code>
///   setvar 0x8004, 1       ; where I am standing
///   ...
///   special 0x017B         ; sail
///   waitstate
///   end
/// </code>
/// <para>
/// A conversation whose last act is to hand the screen over, having written down where it
/// is. Sixteen scripts on this cartridge do that, on eleven maps, and every one of them
/// calls the same routine — and no two maps write the same number. That is a table of ten
/// destinations, written in the open by the scripts that use it.
/// </para>
/// <para>
/// The numbers check themselves: 0 is VERMILION CITY, 1 through 7 are ONE ISLAND through
/// SEVEN ISLAND in order, and 9 and 10 are NAVEL ROCK and BIRTH ISLAND. Nothing was told
/// that ordering; it fell out of ten scripts that have never met.
/// </para>
/// </summary>
public static class Ferries
{
    private const byte Special = 0x25;
    private const byte WaitState = 0x27;
    private const byte End = 0x02;
    private const byte SetVar = 0x16;

    /// <summary>
    /// The argument slot a script writes its own dock number into.
    /// <para>
    /// One of the sixteen slots a script passes values to routines in. On its own it says
    /// nothing — a hundred and forty maps write a constant into this one — so it is the
    /// pairing with the hand-over that identifies a dock, never the slot by itself.
    /// </para>
    /// </summary>
    private const int Standing = 0x8004;

    /// <summary>Every script whose last act is to hand over, having written where it stands.</summary>
    public static List<FerryStop> Stops(
        Rom rom, MapHeaderRecord header, string mapId, int width, int height, Action<string>? log = null)
    {
        var found = new List<FerryStop>();

        foreach ((string what, uint address) in ScriptsOn(rom, header, width, height, log))
        {
            List<ScriptCommand> commands = [.. ScriptReader.ReadAll(rom, address).OrderBy(c => c.Offset)];

            var written = new HashSet<int>();

            foreach (ScriptCommand c in commands.Where(c => c.Code == SetVar && c.Word(0) == Standing))
                written.Add(c.Word(2));

            // Exactly one. A script that writes two different numbers into the slot is
            // using it for something else, which most of the hundred and forty are.
            if (written.Count != 1) continue;

            for (int i = 0; i + 2 < commands.Count; i++)
            {
                if (commands[i].Code != Special) continue;
                if (commands[i + 1].Code != WaitState || commands[i + 2].Code != End) continue;

                // Adjacent in the image as well as in the list, so a jump target that
                // happens to be read next does not count as "and then".
                if (commands[i + 1].Offset != commands[i].Offset + 3) continue;

                found.Add(new FerryStop(mapId, what, written.Single(), commands[i].Word(0)));
            }
        }

        return found;
    }

    /// <summary>
    /// The dock a map is, if it is one.
    /// <para>
    /// Where a map carries both — ONE ISLAND's harbour has four squares that begin the
    /// crossing and a hut with a sailor in it — the person wins. A square you walk onto
    /// is where a boat lands; a person is who you ask for a boat, and only the second can
    /// be talked to.
    /// </para>
    /// </summary>
    public static FerryDock? DockOn(
        Rom rom, MapHeaderRecord header, string mapId, int width, int height,
        IReadOnlyList<MapObject> people, Action<string>? log = null)
    {
        List<FerryStop> stops = Stops(rom, header, mapId, width, height, log);

        if (stops.FirstOrDefault(s => s.IsAPerson) is not { } stop) return null;
        if (people.FirstOrDefault(o => o.LocalId == stop.Attendant) is not { } sailor) return null;

        // Where somebody arriving is put down: the square the sailor is facing. Nine of
        // the ten huts are the same room to the byte — a sailor at (8,6) facing up, with
        // (8,5) the only square on the map anybody could stand on beside him — so this is
        // not a choice about where it would be nice to arrive. It is the only square
        // there is.
        GridPosition arrival = sailor.Square.Step(sailor.Facing);

        return new FerryDock(stop.Number, sailor.LocalId, arrival.X, arrival.Y);
    }

    /// <summary>
    /// What the boat asks for, read off the dock that asks.
    /// <para>
    /// Only one of the ten does. VERMILION's sailor runs two sub-routines of the same
    /// shape — <c>checkflag F</c>, and within a few commands <c>checkitem I</c> — and
    /// either answer opens the boat. Both questions are in plain sight; which places
    /// each ticket is worth is inside the routine that draws the menu, and is not.
    /// </para>
    /// <para>
    /// Paired by proximity rather than by a table, because the pairing is what the
    /// script does: the flag is asked first and the item immediately after it, on the
    /// branch that the flag being set leads to. A flag with no item near it is not a
    /// ticket and is not reported as one.
    /// </para>
    /// </summary>
    public static List<FerryPass> Passes(
        Rom rom, MapHeaderRecord header, int width, int height, Action<string>? log = null)
    {
        var found = new List<FerryPass>();

        foreach ((string _, uint address) in ScriptsOn(rom, header, width, height, log))
        {
            List<ScriptCommand> commands = [.. ScriptReader.ReadAll(rom, address).OrderBy(c => c.Offset)];

            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].Code != CheckFlag) continue;

                for (int j = i + 1; j < commands.Count && j <= i + Nearby; j++)
                {
                    if (commands[j].Code != CheckItem) continue;

                    var pass = new FerryPass(commands[i].Word(0), commands[j].Word(0));

                    if (!found.Contains(pass)) found.Add(pass);

                    break;
                }
            }
        }

        return found;
    }

    private const byte CheckFlag = 0x2B;
    private const byte CheckItem = 0x47;

    /// <summary>How many commands apart the two halves of one question may be.</summary>
    private const int Nearby = 4;

    private static IEnumerable<(string What, uint Address)> ScriptsOn(
        Rom rom, MapHeaderRecord header, int width, int height, Action<string>? log)
    {
        foreach (MapObject person in MapLinkExtractor.ReadObjects(rom, header, width, height, log)
                     .Where(o => o.HasScript))
        {
            yield return ($"person {person.LocalId}", person.ScriptAddress);
        }

        foreach (MapTrigger trigger in MapLinkExtractor.ReadTriggers(rom, header, width, height, log)
                     .Where(t => t.HasScript))
        {
            yield return ($"trigger ({trigger.X},{trigger.Y})", trigger.ScriptAddress);
        }
    }
}
