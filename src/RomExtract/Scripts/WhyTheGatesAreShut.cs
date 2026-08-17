using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>Why a gate a run never opened is shut.</summary>
public enum ShutBecause
{
    /// <summary>
    /// No <c>setflag</c> anywhere in the sixteen megabytes names it, and it is not the hide flag
    /// of anything on the floor. <b>The true boundary</b>, and the name is narrower than it
    /// looks: whatever turns this on is not a script and not a pickup.
    /// </summary>
    NothingSetsIt,

    /// <summary>
    /// Something sets it, and the map scan never opened any of those places. The setter is real
    /// and it is past the code boundary, which is where the wall flags live.
    /// </summary>
    OnlyPastTheBoundary,

    /// <summary>
    /// A script the map scan DID open sets it, and the run never got there. <b>A reach problem
    /// rather than a reading one</b> — closed by walking further.
    /// </summary>
    NeverRan,

    /// <summary>
    /// No script sets it, and it is the hide flag of an object that hands something over — so
    /// what sets it is picking that thing up.
    /// <para>
    /// <b>The bucket that was missing, and the one the numbers caught.</b> The cartridge sets
    /// this kind of flag inside the standard routine that does the handing over, which is
    /// compiled code; only 7 of the 575 objects carrying a hide flag have a script that sets
    /// it. The run already knows this — it reads the flag off the object's own record when it
    /// picks something up — so calling these the boundary is wrong twice over: the file cannot
    /// see the setter and the run opens them anyway.
    /// </para>
    /// </summary>
    TakenOffTheFloor,

    /// <summary>
    /// The people it holds are not people: a tree, a rock, a boulder. Their script asks who
    /// knows a move and takes the object off the map, and the flag that keeps it off is set by
    /// the routine rather than by any <c>setflag</c>.
    /// <para>
    /// <b>The same mechanism as <see cref="TakenOffTheFloor"/>, one class further out</b>, and
    /// the first version of this classifier filed all fifteen of them under the boundary.
    /// </para>
    /// </summary>
    AnObstacle,
}

/// <summary>One gate the run never opened, and why.</summary>
/// <param name="Gates">What it holds up, per the world file.</param>
/// <param name="Sites">How many places in the whole image set it.</param>
/// <param name="Opened">How many of those the map scan decoded.</param>
public sealed record ShutGate(int Flag, FlagGate Gates, ShutBecause Why, int Sites, int Opened);

/// <summary>
/// The gating flags a run never set, sorted by whether anything could have set them.
/// <para>
/// <b>210 printed "110 gating flags it never set" and nothing about any of them.</b> That number
/// reads the same whether the run is one door short of everything or a hundred and ten scripts
/// short, and those are opposite findings: a gate nothing in the file sets is the code boundary
/// and will never open, a gate set only where the map scan cannot see is the wall list's shape,
/// and a gate set by a script on a map is a walk that has not gone far enough.
/// </para>
/// <para>
/// This is the join `--flags` has never made. <c>--flags</c> takes only the ROM and has never
/// seen an <c>Attempt</c>; the playthrough knows what it set and nothing about what else could
/// have. Neither half can answer this on its own, and joining them is not the same as diffing
/// <c>--flags</c> across a run change — which is a scan that did not look.
/// </para>
/// <para>
/// <b>The first version of this had three buckets and the numbers caught it.</b> "Nothing in
/// the file sets it" fell from 134 at the floor to 56 with the levers on — which is impossible
/// for a property of the FILE, and the impossibility was the finding: the run sets sixty-five
/// flags that no <c>setflag</c> in the cartridge names, because picking a thing up sets the
/// object's own hide flag inside compiled code. A bucket named for a cause was wrong again.
/// </para>
/// <para>
/// <b>It has now shrunk twice.</b> Three buckets became four when the numbers showed that
/// picking a thing up sets its hide flag; four became five when fifteen of the remaining
/// gates turned out to hold trees and rocks rather than people. Both times the
/// mechanism was the same — a standard routine setting a flag no <c>setflag</c> names — and both
/// times a bucket called "the boundary" was holding things that open.
/// </para>
/// <para>
/// Every bucket can be empty, including all five at once.
/// </para>
/// </summary>
public static class WhyTheGatesAreShut
{
    /// <summary>
    /// Every gate not in <paramref name="setByTheRun"/>, with the reason it is still shut.
    /// </summary>
    /// <param name="gates">What the world file says each flag holds up.</param>
    /// <param name="setByTheRun">The flags the run ended with.</param>
    /// <param name="movedInTheImage">
    /// Every place in the whole file that moves a flag, from
    /// <see cref="EverywhereInTheImage.EveryFlagMoved"/>. <b>Setters only are counted.</b> A
    /// place that CLEARS a flag is not a place that could have opened this gate, and counting
    /// it would put a gate in the wrong bucket for a script that turns it off.
    /// </param>
    /// <param name="onTheFloor">
    /// The hide flags of objects that hand something over — the ones picking a thing up sets.
    /// <b>Read off the world file rather than off any script</b>, because the routine that does
    /// it is compiled code.
    /// </param>
    /// <param name="obstacles">
    /// The hide flags whose objects are a tree, a rock or a boulder, from
    /// <see cref="GatesThatAreObstacles"/>. The same kind of thing as
    /// <paramref name="onTheFloor"/> and found the same way — off what the object is, not off
    /// any <c>setflag</c>.
    /// </param>
    public static IReadOnlyList<ShutGate> Of(
        FlagGates gates,
        IEnumerable<int> setByTheRun,
        IReadOnlyDictionary<int, IReadOnlyList<FlagSite>> movedInTheImage,
        IReadOnlyCollection<int> onTheFloor,
        IReadOnlyCollection<int> obstacles)
    {
        var shut = new List<ShutGate>();

        foreach (int flag in gates.NotIn(setByTheRun))
        {
            IReadOnlyList<FlagSite> sets =
                movedInTheImage.TryGetValue(flag, out IReadOnlyList<FlagSite>? all)
                    ? [.. all.Where(s => s.Sets)]
                    : [];

            int opened = sets.Count(s => s.Opened);

            // THE ORDER IS A DECISION AND IT IS SAID OUT LOUD.
            //
            // A flag can be several of these at once. An opened setter comes first because it
            // is the one a walk can reach and prove. Being an obstacle or a thing on the floor
            // comes next, ahead of an unopened setter, because both are opened by a routine
            // rather than by a script and calling either "past the boundary" would be false
            // about a gate whose opener is simply not written as script.
            shut.Add(new ShutGate(
                flag,
                gates.Of(flag),
                opened > 0 ? ShutBecause.NeverRan
                : obstacles.Contains(flag) ? ShutBecause.AnObstacle
                : onTheFloor.Contains(flag) ? ShutBecause.TakenOffTheFloor
                : sets.Count > 0 ? ShutBecause.OnlyPastTheBoundary
                : ShutBecause.NothingSetsIt,
                sets.Count,
                opened));
        }

        return shut;
    }

    /// <summary>How many gates are shut for each reason, biggest first.</summary>
    public static IReadOnlyList<(ShutBecause Why, int Gates)> Counted(IEnumerable<ShutGate> shut) =>
    [
        .. shut.GroupBy(g => g.Why)
            .Select(g => (Why: g.Key, Gates: g.Count()))
            .OrderByDescending(g => g.Gates)
            .ThenBy(g => g.Why),
    ];
}
