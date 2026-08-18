using PokeMmo.RomExtract;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>One script that does nothing but hand over to another one.</summary>
/// <param name="Where">Which map and which person, trigger or sign it is attached to.</param>
/// <param name="Leads">The block it hands over to.</param>
/// <param name="Says">What it put in the scratch variable first, or -1 if it put nothing.</param>
/// <param name="Into">Which scratch variable that was.</param>
public sealed record AnEntry(SetsAFlag Where, uint Leads, int Says, int Into)
{
    public override string ToString() =>
        $"{Where.MapId,-8} {Where.What,-22} -> 0x{Leads:X8}"
        + (Says < 0 ? "" : $"  saying 0x{Into:X4} = {Says}");
}

/// <summary>
/// Scenes written as several doors into one room.
/// <para>
/// <b>PEWTER CITY writes one cutscene four times.</b> Four consecutive twelve-byte blocks, one
/// per square you can cross to start it:
/// </para>
/// <code>
/// 08165D8E  69  16 01 40 00 00  05 [08165DBE]  02     lockall; 0x4001 &lt;- 0; goto the scene
/// 08165D9A  69  16 01 40 01 00  05 [08165DBE]  02     ...              1
/// 08165DA6  69  16 01 40 02 00  05 [08165DBE]  02     ...              2
/// 08165DB2  69  16 01 40 03 00  05 [08165DBE]  02     ...              3
/// </code>
/// <para>
/// The number is which door you came in by, written into a scratch variable for the scene to
/// read back. It is not a precondition — milestone 173 established that <c>0x4001</c> is
/// scratch by counting the 285 scripts that write it, and this is what they write it for.
/// </para>
/// <para>
/// <b>Why this is worth counting.</b> A player takes one door. Everything this project does is
/// a fixpoint that stands on every square and talks to everybody, so it takes all of them — and
/// every number the run reports per script is multiplied by however many doors that scene has.
/// Milestone 193 found it in the walking, where it was visible because people ended up in the
/// wrong place. Nothing else it counts had ever been asked.
/// </para>
/// <para>
/// Found by shape and not by the variable: a block whose whole content is a handover. The
/// <c>setvar</c> is reported when there is one and not required, because a scene entered two
/// ways that does not care which way is the same shape with one command missing.
/// </para>
/// </summary>
public static class EntriesToAScene
{
    /// <summary>Where the band a script hands ARGUMENTS to a routine in begins.</summary>
    public const int FirstArgument = 0x8000;

    /// <summary>And where it ends.</summary>
    public const int LastArgument = 0x800F;

    /// <summary>
    /// Whether a variable is one a door can announce itself in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two bands and not one. The scratch pads below <paramref name="scratchBelow"/> are where
    /// 173 found the door numbers and where this instrument has looked since 194 — but a door
    /// can announce itself in an ARGUMENT variable just as well, and this cartridge has a
    /// twenty-two-door scene that does: `10.14`'s slot machines say
    /// <c>0x8004 = 0</c> through <c>21</c>. Cut at the scratch cliff alone, every one of them
    /// reads as a block that does something of its own, and the scene is invisible.
    /// </para>
    /// <para>
    /// <b>What stays out is the story's own memory</b>, and that is the whole point of a cut. A
    /// block that writes <c>0x4055</c> before handing over is not saying which door you came in
    /// by; it is moving the story on, and folding those together would fold two scenes into one
    /// because they happen to share an exit.
    /// </para>
    /// </remarks>
    public static bool AnnouncesItself(int variable, int scratchBelow) =>
        variable < scratchBelow || variable is >= FirstArgument and <= LastArgument;

    /// <summary>The commands a block may contain and still be nothing but a handover.</summary>
    private static readonly byte[] Housekeeping =
    [
        0x69,   // lockall
        0x6A,   // lock
        0x6B,   // releaseall
        0x6C,   // release
        0x5A,   // faceplayer2
        0x5B,   // and the other one
        ScriptCommands.End,
        ScriptCommands.Return,
        ScriptCommands.Nop,
    ];

    /// <summary>Longest a block can be and still be doing nothing but pointing somewhere.</summary>
    private const int ShortEnough = 8;

    /// <summary>
    /// Every script the map scan opens that is nothing but a handover, with where it leads.
    /// </summary>
    public static IReadOnlyList<AnEntry> In(Rom rom, IEnumerable<SetsAFlag> scripts, int scratchBelow)
    {
        var found = new List<AnEntry>();

        foreach (SetsAFlag script in scripts)
        {
            List<ScriptCommand> read = ScriptReader.Read(rom, script.Address);

            if (read.Count == 0 || read.Count > ShortEnough) continue;

            List<ScriptCommand> handovers =
            [
                .. read.Where(c => c.Code is ScriptCommands.Goto or ScriptCommands.Call),
            ];

            if (handovers.Count != 1) continue;

            List<ScriptCommand> said = [.. read.Where(c => c.Code == 0x16)];

            // Anything that is not the handover, a scratch write, or housekeeping means this
            // block does something of its own and is a scene rather than a door into one.
            bool bare = read.All(c =>
                c == handovers[0]
                || Housekeeping.Contains(c.Code)
                || (c.Code == 0x16 && said.Count == 1 && AnnouncesItself(c.Word(), scratchBelow)));

            if (!bare) continue;

            uint leads = handovers[0].Pointer();

            if (leads == 0 || !rom.IsRomAddress(leads)) continue;

            found.Add(new AnEntry(
                script,
                leads,
                said.Count == 1 ? said[0].Word(2) : -1,
                said.Count == 1 ? said[0].Word() : 0));
        }

        return found;
    }

    /// <summary>
    /// The doors gathered by the room they lead into, <b>on one map</b>.
    /// <para>
    /// <b>The map is half the key and leaving it out is a fault this cost.</b> One nurse's
    /// script is attached to person 1 on nineteen Pokémon Centres, and one shopkeeper's to
    /// nineteen marts; the biggest group by address alone is twenty doors, and it is twenty
    /// different people in twenty different towns. A player talks to all twenty. Grouped that
    /// way this instrument reports the shared routines of the game as scenes played over and
    /// over, which is the opposite of a finding.
    /// </para>
    /// <para>
    /// Two doors into one block on the SAME map is one scene entered two ways, which is the
    /// thing. <see cref="SharedAcrossMaps"/> is the other list, kept apart rather than mixed in.
    /// </para>
    /// </summary>
    public static IReadOnlyList<IGrouping<(string MapId, uint Leads), AnEntry>> Rooms(
        IEnumerable<AnEntry> doors) =>
    [
        .. doors
            .GroupBy(d => (d.Where.MapId, d.Leads))
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.Leads),
    ];

    /// <summary>
    /// Whether a room's doors are doors — <b>told apart by the number they say</b>.
    /// <para>
    /// A stub that announces which door it came in by says a DIFFERENT number per door: the
    /// five triggers on <c>3.14</c> say 0, 1, 2, 3, 4 and they are five squares of one line you
    /// cross once. Six people on the same map handing over to the same block while all saying
    /// <c>2</c> are not announcing anything — they are six people who share a script, and a
    /// player talks to all six.
    /// </para>
    /// <para>
    /// Same map, same block, and the two are opposite findings. The number is the only thing
    /// that separates them and it is right there in the bytes.
    /// </para>
    /// </summary>
    public static bool IsOneSceneEnteredSeveralWays(IEnumerable<AnEntry> room)
    {
        List<AnEntry> doors = [.. room];

        return doors.Count > 1
               && doors.All(d => d.Says >= 0)
               && doors.Select(d => d.Says).Distinct().Count() == doors.Count;
    }

    /// <summary>
    /// Blocks reached from more than one map: shared routines rather than repeated scenes.
    /// <para>
    /// Worth its own number because it is the shape that makes the one above wrong if the two
    /// are mixed, and because anything keyed on a script address alone is wrong about it.
    /// </para>
    /// </summary>
    public static IReadOnlyList<IGrouping<uint, AnEntry>> SharedAcrossMaps(
        IEnumerable<AnEntry> doors) =>
    [
        .. doors
            .GroupBy(d => d.Leads)
            .Where(g => g.Select(d => d.Where.MapId).Distinct().Count() > 1)
            .OrderByDescending(g => g.Select(d => d.Where.MapId).Distinct().Count()),
    ];
}
