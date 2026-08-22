using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// For a compare that sits past something which may have answered instead: what that something
/// was, and whether it can have answered.
/// </summary>
/// <remarks>
/// <para>
/// 220 gave <see cref="SpecialContracts"/> the barrier <see cref="SpecialCalls"/> has had since
/// 214, and 145 sites moved into "the compare is past something that may have answered". <b>May
/// have.</b> That is a statement about this reading, not about the cartridge, and leaving it
/// there would be the fourth time in this project that "nothing found" stood in for "did not
/// look".
/// </para>
/// <para>
/// 219 already did one by hand: the thing between <c>special 0x001C</c> and its compare is
/// <c>call 0x081A6675</c>, and that block is <c>copyvar 0x8012, 0x8013 ; return</c> — four bytes
/// that cannot have touched the answer variable. The compare is <c>0x001C</c>'s after all. This
/// is that reading, for all of them, with a verdict that is allowed to be <b>unknown</b>.
/// </para>
/// </remarks>
public static class WhoTheCompareBelongsTo
{
    /// <summary>What stood between the routine and the compare.</summary>
    public enum InTheWay
    {
        /// <summary>Another <c>special</c> or <c>specialvar</c> — it answered, and this is its compare.</summary>
        AnotherRoutine,

        /// <summary>
        /// A command that answers into the same variable on its own account — <c>0xA0</c>,
        /// <c>giveitem</c>, <c>findmove</c>.
        /// </summary>
        ACommandThatAnswers,

        /// <summary>
        /// A <c>callstd</c> whose number the callers show <b>does</b> answer — so it clobbered
        /// whatever was in the variable, and the compare is its.
        /// </summary>
        AStandardRoutineThatAnswers,

        /// <summary>
        /// A <c>callstd</c> or <c>gotostd</c> whose number nothing in the file pins down. The
        /// table it indexes has never been found, and 222's reading of the callers only speaks
        /// for the numbers it saw a compare after.
        /// </summary>
        AStandardRoutine,

        /// <summary>A <c>call</c> whose block leaves an answer of its own.</summary>
        ACallThatAnswers,

        /// <summary>
        /// A <c>call</c> whose block provably puts nothing in the answer variable. <b>The
        /// compare is the routine's after all.</b>
        /// </summary>
        ACallThatTouchesNothing,

        /// <summary>A <c>call</c> whose block ends by jumping somewhere this reading does not follow.</summary>
        ACallThatJumpsAway,
    }

    /// <summary>Whose answer the compare is reading, as far as this can say.</summary>
    public enum Whose
    {
        /// <summary>The thing in the way answered; the compare is not this routine's.</summary>
        SomebodyElses,

        /// <summary>The thing in the way cannot have answered; the compare is this routine's.</summary>
        StillThisRoutines,

        /// <summary>
        /// The thing in the way goes somewhere this reading does not. <b>Its own answer</b> —
        /// not folded into either of the others, because a reading that stopped and a fact about
        /// the cartridge are different things.
        /// </summary>
        NotSaid,
    }

    /// <param name="Routine">The routine whose answer was being claimed.</param>
    /// <param name="At">The byte position of the <c>special</c>.</param>
    /// <param name="Called">
    /// The block a <c>call</c> went to, or — for a standard routine — the NUMBER it asked for.
    /// Which of those it is is decided by <see cref="Was"/>, and the two are printed
    /// differently because one of them is not an address.
    /// </param>
    public sealed record ACompareAcross(
        string MapId,
        string What,
        int Routine,
        uint At,
        InTheWay Was,
        uint Called,
        Whose Belongs,
        IReadOnlyList<int> Compared);

    /// <summary>
    /// A verdict for every site whose compare is only past a barrier, over every script the
    /// maps hang off anything.
    /// </summary>
    public static List<ACompareAcross> In(
        Rom rom, MapLibrary library, int forward = SpecialContracts.Window)
    {
        var found = new List<ACompareAcross>();

        // Which standard routines answer, read off the callers — 222. Derived only from sites
        // where nothing else could have answered, and applied here to sites where something
        // could, which is the opposite direction and not circular.
        HashSet<int> answering =
        [
            .. StandardRoutines.WhoAnswers(rom, library).Where(a => a.MustAnswer).Select(a => a.Index),
        ];

        foreach ((string mapId, string what, uint address) in library.EveryScript())
        {
            List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

            for (int i = 0; i < commands.Count; i++)
            {
                int routine = commands[i].Code switch
                {
                    SpecialCalls.Special when commands[i].Arguments.Length >= 2 => commands[i].Word(),
                    SpecialCalls.SpecialVar when commands[i].Arguments.Length >= 4 => commands[i].Word(2),
                    _ => -1,
                };

                if (routine < 0) continue;

                int answer = commands[i].Code == SpecialCalls.SpecialVar
                    ? commands[i].Word()
                    : SpecialContracts.AnswerVariable;

                (IReadOnlyList<int> direct, IReadOnlyList<int> beyond) =
                    SpecialContracts.WhatIsComparedAfter(commands, i, answer, forward);

                // Only the sites the table gave up on. One with a clean compare as well has an
                // owner already, and reporting it here would count it twice — and the rule for
                // which is which is asked of SpecialContracts rather than repeated here.
                if (!SpecialContracts.NothingCleanHere(direct, beyond)) continue;

                (InTheWay Was, uint Called)? stood =
                    WhatStoodInTheWay(rom, commands, i, answering, forward);

                if (stood is null) continue;

                found.Add(new ACompareAcross(
                    mapId,
                    what,
                    routine,
                    Rom.BaseAddress + (uint)commands[i].Offset,
                    stood.Value.Was,
                    stood.Value.Called,
                    Belongs(stood.Value.Was),
                    beyond));
            }
        }

        return found;
    }

    /// <summary>
    /// The first thing after the routine that could have answered, and what it turns out to be
    /// — or null when nothing in the window could have.
    /// <para>
    /// Exposed against a handful of bytes rather than a whole world, for the same reason the
    /// other two readings are: a rule a test can only reach through a cartridge is a rule no
    /// test reaches.
    /// </para>
    /// </summary>
    public static (InTheWay Was, uint Called)? WhatStoodInTheWay(
        Rom rom, List<ScriptCommand> commands, int at, IReadOnlySet<int>? answering = null,
        int forward = SpecialContracts.Window)
    {
        for (int i = at + 1; i < commands.Count && i - at <= forward; i++)
        {
            if (!SpecialCalls.AnswersItself(commands[i].Code)) continue;

            if (commands[i].Code == ScriptCommands.Call)
            {
                uint called = commands[i].Pointer(0);

                return (SpecialCalls.WhatACallLeaves(rom, called).Left switch
                {
                    SpecialCalls.LeftBehind.Nothing => InTheWay.ACallThatTouchesNothing,
                    SpecialCalls.LeftBehind.WentSomewhereElse => InTheWay.ACallThatJumpsAway,
                    _ => InTheWay.ACallThatAnswers,
                }, called);
            }

            if (commands[i].Code is SpecialCalls.Special or SpecialCalls.SpecialVar)
                return (InTheWay.AnotherRoutine, 0);

            if (commands[i].Code is ScriptCommands.CallStandard or 0x08)
            {
                int index = commands[i].Arguments.Length > 0 ? commands[i].Arguments[0] : -1;

                return (answering?.Contains(index) == true
                    ? InTheWay.AStandardRoutineThatAnswers
                    : InTheWay.AStandardRoutine, (uint)Math.Max(index, 0));
            }

            return (InTheWay.ACommandThatAnswers, 0);
        }

        return null;
    }

    /// <summary>
    /// The verdict, from what stood in the way.
    /// <para>
    /// <b>A standard routine is not a no.</b> <c>callstd</c> is in the barrier list because a
    /// standard routine answers, and this project has never read one — so a site behind one is
    /// unknown rather than somebody else's, and saying "somebody else's" would be inventing a
    /// fact to fill a hole. The same for a block that jumps away.
    /// </para>
    /// </summary>
    public static Whose Belongs(InTheWay was) => was switch
    {
        InTheWay.ACallThatTouchesNothing => Whose.StillThisRoutines,
        InTheWay.AStandardRoutine or InTheWay.ACallThatJumpsAway => Whose.NotSaid,
        InTheWay.AStandardRoutineThatAnswers => Whose.SomebodyElses,
        _ => Whose.SomebodyElses,
    };
}
