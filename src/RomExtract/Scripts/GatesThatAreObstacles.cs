using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// The gating flags whose people are not people: a tree, a rock, a boulder.
/// <para>
/// <b>Fifteen gating flags in this cartridge hold nothing but things asked about a move and
/// then taken off the map</b>, and between them they run TWO scripts — one asking about move
/// 15 and one about move 249. They are one family, <c>0x0011</c> to <c>0x001F</c>, holding one
/// obstacle per map across thirty-odd maps: a hundred and forty-six objects behind fifteen
/// flags. A further twelve gates hold something asked about a move and never removed.
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
/// <summary>One gating flag whose objects are all the same kind of obstacle.</summary>
/// <param name="Moves">
/// The move ids their scripts ask about. <b>Printed rather than assumed</b> — "asked about a
/// move" was all the first version of this said, and which move is the difference between a
/// tree and a boulder.
/// </param>
/// <param name="Removed">
/// Whether the objects are taken off the map afterwards. False is the interesting one: the
/// thing is asked about and stays exactly where it is, and whatever clears it is not this.
/// </param>
public sealed record AnObstacleGate(
    int Flag, IReadOnlyList<int> Moves, IReadOnlyList<uint> Scripts, bool Removed);

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
    /// Every gate whose objects are ALL asked about a move, with which move and whether they
    /// are then taken off the map.
    /// <para>
    /// The two kinds are one list with a flag on them rather than two lists, because they are
    /// the same question answered two ways — and the removed half is not allowed to swallow the
    /// other. Twelve of this cartridge's gates hold something asked about a move and never
    /// removed; whatever clears those is a different mechanism from the one that clears a tree,
    /// and widening the rule to catch them would be picking a shape to fit an answer.
    /// </para>
    /// </returns>
    public static IReadOnlyList<AnObstacleGate> In(Rom rom, WorldData world)
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

        var known = new Dictionary<uint, (IReadOnlyList<int> Moves, bool Takes)>();

        (IReadOnlyList<int> Moves, bool Takes) What(uint address)
        {
            if (address == 0) return ([], false);

            if (known.TryGetValue(address, out (IReadOnlyList<int>, bool) already)) return already;

            var moves = new SortedSet<int>();
            var takes = false;

            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (command.Code == ObstacleMoves.FindMove) moves.Add(command.Word());
                if (command.Code == TakeOffTheMap) takes = true;
            }

            return known[address] = ([.. moves], takes);
        }

        var found = new List<AnObstacleGate>();

        foreach ((int flag, List<uint> theirs) in behind)
        {
            List<(IReadOnlyList<int> Moves, bool Takes)> what = [.. theirs.Select(What)];

            // Every one of them has to be asked about something, or this is a flag that holds a
            // person as well and calling it an obstacle's would hide the person inside a bucket
            // named for scenery.
            if (what.Any(w => w.Moves.Count == 0)) continue;

            // And they have to agree about whether they are taken away. A flag holding a tree
            // and a boulder is neither kind: the two things behind it are cleared by different
            // mechanisms and no one answer is true of both.
            if (what.Select(w => w.Takes).Distinct().Count() != 1) continue;

            found.Add(new AnObstacleGate(
                flag,
                [.. what.SelectMany(w => w.Moves).Distinct().Order()],
                [.. theirs.Distinct().Order()],
                what[0].Takes));
        }

        return [.. found.OrderBy(g => g.Flag)];
    }
}
