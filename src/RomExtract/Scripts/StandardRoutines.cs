using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// The standard routines: blocks a script reaches by number rather than by address.
/// </summary>
/// <remarks>
/// <para>
/// <c>callstd 6</c> is a call, and the address is not in the command — it is the sixth entry of
/// a table somewhere in the image. This project has never read one, and that is why twelve sites
/// came back <b>not said</b> at 221: seven of <c>0x0188</c>'s and five whose whole claim on a
/// routine sits behind a <c>callstd</c>. They are in the barrier list because a standard routine
/// answers, and nothing here could say what.
/// </para>
/// <para>
/// <b>Found by shape, with a floor beside it.</b> Nothing here knows where the table is. What is
/// known is what the scripts ask for — the indices they actually use — and what a table of
/// script pointers looks like: consecutive words, every one an address in this image, every one
/// landing on something that reads as a script. The same sweep runs on the file reversed, and
/// if the reversal finds as many the shape is what these bytes do by accident.
/// </para>
/// </remarks>
public static class StandardRoutines
{
    /// <summary>Call a standard routine and come back.</summary>
    public const byte CallStandard = 0x09;

    /// <summary>Jump to one and do not.</summary>
    public const byte GotoStandard = 0x08;

    /// <summary>Pointer tables in this image are word-aligned.</summary>
    private const int Alignment = 4;

    private const byte Compare = 0x21;

    /// <param name="Index">The number the scripts use.</param>
    /// <param name="Sites">How many times it is asked for.</param>
    /// <param name="Places">How many byte positions those are — a shared block is read once per entry.</param>
    public sealed record Asked(int Index, int Sites, int Places, bool Returns);

    /// <param name="At">Where the run of pointers starts.</param>
    /// <param name="Entries">How many consecutive pointers it holds.</param>
    public sealed record ATable(uint At, int Entries, IReadOnlyList<uint> Pointers);

    /// <summary>
    /// Every standard routine the maps ask for, with how often and — the half that matters —
    /// whether the asking is a <c>callstd</c> that comes back or a <c>gotostd</c> that does not.
    /// </summary>
    public static List<Asked> WhatIsAsked(Rom rom, MapLibrary library)
    {
        var sites = new Dictionary<(int Index, bool Returns), List<int>>();

        foreach ((string _, string _, uint address) in library.EveryScript())
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (command.Code is not (CallStandard or GotoStandard)) continue;
                if (command.Arguments.Length < 1) continue;

                var key = (command.Arguments[0], command.Code == CallStandard);

                if (!sites.TryGetValue(key, out List<int>? at)) sites[key] = at = [];

                at.Add(command.Offset);
            }
        }

        return
        [
            .. sites
                .Select(entry => new Asked(
                    entry.Key.Index,
                    SpecialContracts.SitesAndPlaces(entry.Value).Sites,
                    SpecialContracts.SitesAndPlaces(entry.Value).Places,
                    entry.Key.Returns))
                .OrderByDescending(a => a.Places)
                .ThenBy(a => a.Index),
        ];
    }

    /// <param name="Index">The standard routine's number.</param>
    /// <param name="Sites">Sites where a compare on the answer variable follows it immediately.</param>
    /// <param name="NothingBefore">
    /// Of those, how many have <b>nothing before them that could have answered</b>. Each of these
    /// is a place where the compare has no other possible source, so the standard routine itself
    /// must be what answers.
    /// </param>
    /// <param name="SomebodyBefore">How many have something before them that could have.</param>
    /// <param name="NotSaid">How many the walk back could not account for.</param>
    public sealed record Answers(
        int Index, int Sites, int Places, int NothingBefore, int SomebodyBefore, int NotSaid)
    {
        /// <summary>
        /// <b>The standard routine answers, and the file says so without the table being found.</b>
        /// One site with nothing before it is enough: the compare has to be reading something,
        /// and there is nothing else it can be reading.
        /// </summary>
        public bool MustAnswer => NothingBefore > 0;
    }

    /// <summary>
    /// Which standard routines answer into the answer variable, derived from where the scripts
    /// put a compare after one and what — if anything — could have answered instead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This does not need the table.</b> Hunting the table by shape came back with two dozen
    /// candidates and no way to choose, because the filter every sweep in this project uses
    /// accepts a pointer to <c>nop ; end</c>. The question the table was wanted for can be
    /// answered from the callers instead: if a script says <c>callstd N ; compare 0x800D</c> and
    /// nothing before it could have put anything in that variable, then the compare is reading
    /// what <c>N</c> left, whatever <c>N</c> turns out to be.
    /// </para>
    /// <para>
    /// The walk back is 219's, and this is the first thing outside its own milestone to use it.
    /// </para>
    /// </remarks>
    public static List<Answers> WhoAnswers(Rom rom, MapLibrary library, int answer = 0x800D)
    {
        var sites = new Dictionary<int, List<int>>();
        var nothing = new Dictionary<int, int>();
        var somebody = new Dictionary<int, int>();
        var unsaid = new Dictionary<int, int>();

        foreach ((string _, string _, uint address) in library.EveryScript())
        {
            List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

            for (int i = 0; i < commands.Count; i++)
            {
                if (!AsksTheQuestionHere(commands, i, answer)) continue;

                int index = commands[i].Arguments[0];

                if (!sites.TryGetValue(index, out List<int>? at)) sites[index] = at = [];

                at.Add(commands[i].Offset);

                if (ProvesItAnswers(commands, i, answer))
                {
                    nothing[index] = nothing.GetValueOrDefault(index) + 1;
                }
                else if (SpecialCalls.WhatAnsweredBefore(commands, i, answer).Left
                         == SpecialCalls.LeftBehind.WentSomewhereElse)
                {
                    unsaid[index] = unsaid.GetValueOrDefault(index) + 1;
                }
                else
                {
                    somebody[index] = somebody.GetValueOrDefault(index) + 1;
                }
            }
        }

        return
        [
            .. sites.Select(entry => new Answers(
                    entry.Key,
                    SpecialContracts.SitesAndPlaces(entry.Value).Sites,
                    SpecialContracts.SitesAndPlaces(entry.Value).Places,
                    nothing.GetValueOrDefault(entry.Key),
                    somebody.GetValueOrDefault(entry.Key),
                    unsaid.GetValueOrDefault(entry.Key)))
                .OrderByDescending(a => a.NothingBefore)
                .ThenByDescending(a => a.Places),
        ];
    }

    /// <summary>
    /// Whether this site is one that can say anything at all: a <c>callstd</c> with a compare on
    /// the answer variable immediately after it, <b>branched on</b>.
    /// <para>
    /// A compare with nothing after it changes no path and says nothing about what anybody
    /// answered — the same rule the routine table has used since it was written, here because a
    /// site that cannot say anything must not be counted as saying nothing.
    /// </para>
    /// </summary>
    public static bool AsksTheQuestionHere(List<ScriptCommand> commands, int at, int answer = 0x800D) =>
        at >= 0
        && at + 2 < commands.Count
        && commands[at].Code == ScriptCommands.CallStandard
        && commands[at].Arguments.Length >= 1
        && commands[at + 1].Code == Compare
        && commands[at + 1].Word() == answer
        && commands[at + 2].Code is ScriptCommands.GotoIf or ScriptCommands.CallIf;

    /// <summary>
    /// Whether this site <b>proves</b> the standard routine answers: it asks the question, and
    /// the walk back finds nothing in front of it that could have answered instead.
    /// <para>
    /// <b>Both halves, and the second is the whole argument.</b> A compare has to be reading
    /// something; where nothing else wrote the variable, the <c>callstd</c> is the only candidate
    /// left. A site with a <c>special</c> in front of it proves nothing either way and must not
    /// be counted — those are precisely the sites this verdict is then applied to.
    /// </para>
    /// </summary>
    public static bool ProvesItAnswers(List<ScriptCommand> commands, int at, int answer = 0x800D) =>
        AsksTheQuestionHere(commands, at, answer)
        && SpecialCalls.WhatAnsweredBefore(commands, at, answer).Left == SpecialCalls.LeftBehind.Nothing;

    /// <summary>
    /// Every run of at least <paramref name="atLeast"/> consecutive word-aligned pointers into
    /// this image, each landing on something that reads as a script.
    /// </summary>
    /// <remarks>
    /// <b>Maximal runs only.</b> A run of twenty contains eleven runs of ten, and reporting all
    /// of them would turn one candidate into eleven and make the count meaningless — including
    /// the count on the reversed image, which is the only thing that says whether any of this is
    /// a shape at all.
    /// </remarks>
    public static List<ATable> Tables(Rom rom, int atLeast)
    {
        var found = new List<ATable>();
        var run = new List<uint>();
        var start = 0;

        for (var offset = 0; offset + 4 <= rom.Length; offset += Alignment)
        {
            uint word = rom.ReadU32(offset);

            if (Points(rom, word))
            {
                if (run.Count == 0) start = offset;

                run.Add(word);

                continue;
            }

            if (run.Count >= atLeast) found.Add(new ATable(Rom.BaseAddress + (uint)start, run.Count, [.. run]));

            run.Clear();
        }

        if (run.Count >= atLeast) found.Add(new ATable(Rom.BaseAddress + (uint)start, run.Count, [.. run]));

        return found;
    }

    /// <summary>An address in this image, landing on something that reads as a script.</summary>
    private static bool Points(Rom rom, uint word) =>
        rom.ToOffsetOrNull(word) is not null && ScriptReader.ReadsAsAScript(rom, word);

    /// <summary>
    /// The same sweep on the file reversed, which is what says whether the shape means anything.
    /// </summary>
    public static List<ATable> NoiseFloor(Rom rom, int atLeast)
    {
        byte[] backwards = rom.Span.ToArray();

        Array.Reverse(backwards);

        return Tables(new Rom(backwards), atLeast);
    }

    /// <summary>
    /// What each entry of a candidate table leaves in the answer variable — the question the
    /// twelve unanswered sites are waiting on.
    /// </summary>
    public static List<(int Index, uint At, SpecialCalls.LeftBehind Left, int Who)> WhatTheyLeave(
        Rom rom, ATable table) =>
    [
        .. table.Pointers.Select((at, index) =>
        {
            (SpecialCalls.LeftBehind left, int who) = SpecialCalls.WhatACallLeaves(rom, at);

            return (index, at, left, who);
        }),
    ];
}
