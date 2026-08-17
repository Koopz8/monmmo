using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// The gating flags whose people are not people: a tree, a rock, a boulder.
/// <para>
/// <b>Twenty-two of the sixty-two gates no walk can open hold a hundred and fifty-three of
/// the two hundred and forty people behind them, and every one of those is an obstacle.</b>
/// Fifteen flags in one family — <c>0x0011</c> to <c>0x001F</c> — hold one obstacle per map
/// across thirty-odd maps, and the whole family runs two scripts between them. Seven more
/// hold one boulder each.
/// </para>
/// <para>
/// They read as the code boundary because nothing sets their flags, and that is true and
/// misleading in exactly the way <c>TakenOffTheFloor</c> was: the script asks who knows the
/// move, takes the object off the map, and the flag that keeps it off is set by the routine
/// rather than by any <c>setflag</c>. Same mechanism, one class further out — and the first
/// version of this classifier filed all of them under "the true boundary".
/// </para>
/// <para>
/// Found by shape rather than by address. An obstacle's script asks who knows a move AND
/// takes an object off the map; either alone is not enough, and the three addresses this
/// cartridge happens to use are printed rather than written down.
/// </para>
/// </summary>
public static class GatesThatAreObstacles
{
    /// <summary>Takes an object off the map. Claimed already — 224 sites, all object numbers.</summary>
    private const byte TakeOffTheMap = 0x53;

    /// <summary>
    /// Every gating flag whose hidden objects ALL run an obstacle's script, and the scripts
    /// they run.
    /// <para>
    /// <b>All rather than any</b>, because a flag that holds a rock and a shopkeeper is not an
    /// obstacle's flag and calling it one would hide the shopkeeper inside a bucket named for
    /// scenery.
    /// </para>
    /// </summary>
    /// <returns>
    /// The gates whose objects are asked about a move and then taken off the map, the scripts
    /// they run, and — separately — the gates whose objects are asked about a move and are
    /// <b>never</b> taken off it.
    /// <para>
    /// The second list is not folded into the first. Seven of this cartridge's gates hold
    /// something whose script asks who knows move 70 and never removes anything, and whatever
    /// clears those is a different mechanism from the one that clears a tree. Widening the rule
    /// to catch them would be picking a shape to fit an answer.
    /// </para>
    /// </returns>
    public static (IReadOnlyList<int> Flags, IReadOnlyList<uint> Scripts, IReadOnlyList<int> AskedButNotRemoved)
        In(Rom rom, WorldData world)
    {
        var behind = new Dictionary<int, List<uint>>();

        foreach (MapData map in world.Maps)
        {
            foreach (MapObject person in map.Objects)
            {
                if (person.HiddenBy == 0) continue;

                if (!behind.TryGetValue(person.HiddenBy, out List<uint>? theirs))
                {
                    behind[person.HiddenBy] = theirs = [];
                }

                theirs.Add(person.ScriptAddress);
            }
        }

        var known = new Dictionary<uint, (bool Asks, bool Takes)>();

        (bool Asks, bool Takes) What(uint address)
        {
            if (address == 0) return (false, false);

            if (known.TryGetValue(address, out (bool, bool) already)) return already;

            var asks = false;
            var takes = false;

            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (command.Code == ObstacleMoves.FindMove) asks = true;
                if (command.Code == TakeOffTheMap) takes = true;
            }

            return known[address] = (asks, takes);
        }

        var flags = new List<int>();
        var staying = new List<int>();
        var scripts = new SortedSet<uint>();

        foreach ((int flag, List<uint> theirs) in behind)
        {
            if (theirs.All(a => What(a) is { Asks: true, Takes: true }))
            {
                flags.Add(flag);

                foreach (uint address in theirs) scripts.Add(address);

                continue;
            }

            // Asked about a move and never taken off the map. A different mechanism, kept
            // apart rather than folded in.
            if (theirs.All(a => What(a) is { Asks: true, Takes: false })) staying.Add(flag);
        }

        return ([.. flags.Order()], [.. scripts], [.. staying.Order()]);
    }
}
