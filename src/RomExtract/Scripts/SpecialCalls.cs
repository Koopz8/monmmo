using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// One call into the game's own code, and what the script around it expects back.
/// </summary>
public sealed record SpecialCall(
    string MapId,
    string What,
    int Routine,
    int? AnswersInto,
    IReadOnlyList<(int Variable, int Value)> Arguments,
    IReadOnlyList<(int Value, byte Condition)> Compared,
    IReadOnlyList<Branch> Branches);

/// <summary>
/// One fork in the road after a special, and where each arm goes.
/// <para>
/// The two addresses are the whole point. What a routine does cannot be read, but what
/// the script does about each answer can be — and a script's own words are evidence in a
/// way that a recollection of another game is not. This project named <c>giveitem</c>
/// from the shape of what surrounded it and the obstacle family from move ids looked up
/// in the cartridge's own table; reading a routine from what its two arms say is the same
/// move.
/// </para>
/// </summary>
public sealed record Branch(int Value, byte Condition, uint Taken, uint NotTaken);

/// <summary>
/// The <c>special</c> calls, and the shape of what is asked of each routine.
/// <para>
/// This is the boundary this project cannot read across. A special is a call into the
/// game's ARM code by number; what the routine does is not in any table, and no amount of
/// looking at data will say. Everything else in this project was somewhere in the image
/// waiting to be found — this is not.
/// </para>
/// <para>
/// What <em>is</em> readable is the shape of the expectation. A script writes its
/// arguments into 0x8000 and upwards, calls the routine, and then compares the answer
/// against a number and branches. Argument count, answer range, and how many different
/// values the script distinguishes are all right there in the bytes. That is not what a
/// routine does, but it is the specification a hand-written stand-in has to satisfy — and
/// it is checkable, which a guess is not.
/// </para>
/// </summary>
public static class SpecialCalls
{
    /// <summary>Calls a routine by number, taking no answer.</summary>
    public const byte Special = 0x25;

    /// <summary>Calls one and puts the answer in a variable.</summary>
    public const byte SpecialVar = 0x26;

    private const byte SetVar = 0x16;
    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte CallIf = 0x07;

    /// <summary>
    /// The argument variables, which is what makes an argument tellable from a variable.
    /// <para>
    /// 0x8000 upwards are the slots a script passes values in; 0x4000 upwards are the
    /// save's own. A setvar to the first kind in front of a call is an argument to it; a
    /// setvar to the second happens to be nearby.
    /// </para>
    /// </summary>
    private const int FirstArgument = 0x8000;

    private const int LastArgument = 0x800F;

    /// <summary>How many commands either side count as "around" the call.</summary>
    private const int Window = 4;

    /// <summary>
    /// Commands that put their own answer in the result variable.
    /// <para>
    /// Scanning forward for "the compare that reads this routine's answer" has to stop at
    /// the first of these, and getting that wrong is not a small error — it credits one
    /// routine with another's reply. On a real image the call to 0x0174 in BILL's house
    /// is followed immediately by 0xA0 and only then by a compare, so the whole reading
    /// of what 0x0174 answers came from a routine that is not 0x0174.
    /// </para>
    /// </summary>
    private static readonly byte[] Answering =
    [
        Special, SpecialVar,
        0xA0,           // answers into the result variable and takes nothing
        0x46, 0x47,     // giveitem, and the one shaped like it
        0x7C,           // findmove
        0x09, 0x08,     // callstd, gotostd — a standard routine answers too

        // AND A PLAIN CALL, which is the same fault one step out (214).
        //
        // callstd was already here because a standard routine answers. An ordinary call is
        // a jump into a block this scan is not reading, and that block can do anything at
        // all — including calling a special of its own. On this cartridge it does: SEVEN
        // ISLAND's `special 0x0028 ; call 0x081A4EAF ; compare 0x800D 0` credited the
        // compare to 0x0028, and 0x081A4EAF is three commands long and the first of them is
        // `special 0x005D`. The answer being read belongs to a routine two levels away.
        //
        // Stopping here loses attributions rather than inventing them, which is the only
        // direction this can safely be wrong in.
        Call,           // call
    ];

    /// <summary>
    /// The same reading, pointed at any command rather than at a special.
    /// <para>
    /// Several ordinary commands answer into the result variable too, and one of them —
    /// 0xA0 — is the reason a special was misidentified this milestone. Being able to ask
    /// the same question of a command as of a routine is what turns that from an
    /// embarrassment into an instrument.
    /// </para>
    /// <para>
    /// The answer variable is 0x800D, which is where everything that answers without
    /// being told where to put it, puts it.
    /// </para>
    /// </summary>
    public static List<SpecialCall> AllOf(Rom rom, MapLibrary library, byte code)
    {
        var found = new List<SpecialCall>();

        foreach ((string mapId, string what, uint address) in Scripts(library))
        {
            List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].Code != code) continue;

                found.Add(new SpecialCall(
                    mapId,
                    what,
                    code,
                    0x800D,
                    Before(commands, i),
                    After(commands, i, 0x800D),
                    Forks(commands, i, 0x800D)));
            }
        }

        return found;
    }

    /// <summary>
    /// Every command that is branched on, in one pass over the world.
    /// <para>
    /// The reason this exists rather than calling <see cref="AllOf"/> two hundred and
    /// fifty-six times: opening a map decompresses and renders it, and asking the library
    /// for all of them once per opcode does that work two hundred and fifty-six times
    /// over. Reading four hundred maps is a few seconds; reading them a hundred thousand
    /// times is an afternoon, and the first anybody knows about it is a tool that has
    /// printed its heading and stopped.
    /// </para>
    /// </summary>
    public static Dictionary<byte, List<SpecialCall>> Sweep(Rom rom, MapLibrary library)
    {
        var found = new Dictionary<byte, List<SpecialCall>>();

        foreach ((string mapId, string what, uint address) in Scripts(library))
        {
            List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

            for (int i = 0; i < commands.Count; i++)
            {
                List<Branch> forks = Forks(commands, i, 0x800D);
                if (forks.Count == 0) continue;

                byte code = commands[i].Code;

                if (!found.TryGetValue(code, out List<SpecialCall>? calls))
                    found[code] = calls = [];

                calls.Add(new SpecialCall(
                    mapId, what, code, 0x800D, Before(commands, i), After(commands, i, 0x800D), forks));
            }
        }

        return found;
    }

    /// <summary>Every script on every map, with where it came from.</summary>
    /// <summary>
    /// A compare whose answer was left behind by something inside a <c>call</c>.
    /// </summary>
    /// <summary>What a called block leaves in the answer variable, on its straight line.</summary>
    public enum LeftBehind
    {
        /// <summary>Nothing on the straight line puts anything in it.</summary>
        Nothing,

        /// <summary>A routine's answer — the one worth crediting.</summary>
        ARoutine,

        /// <summary>
        /// A number the block puts there itself, and nothing anywhere in it asks a routine.
        /// <b>No ceiling at all</b> — the answer is a constant however the block is entered.
        /// </summary>
        ANumber,

        /// <summary>
        /// The straight line ends by saying a number out loud, but some ARM of the block asks a
        /// routine.
        /// <para>
        /// <b>Which is not the same thing and the first version of this said it was.</b>
        /// <c>0x081BBB1E</c> ends <c>setvar 0x800D, 1 ; return</c> and its LESS arm ends
        /// <c>setvar 0x800D, 0 ; return</c> — so the block returns one or nought depending on a
        /// routine, and calling that "no ceiling" is a bucket named for a cause with the cause
        /// false. Trap 5, for the fourth time in this project.
        /// </para>
        /// </summary>
        ANumberOnTheStraightLine,

        /// <summary>Another variable's contents, which this reading does not follow.</summary>
        AnotherVariable,

        /// <summary>
        /// The reading STOPPED rather than finding nothing — the block ends by jumping somewhere
        /// else, or the walk back met something that ran and could not be accounted for.
        /// <para>
        /// <b>Those are different and conflating them is the same fault a third time.</b>
        /// "The call left the variable alone" and "the call went somewhere this reading does not
        /// follow" print identically as <see cref="Nothing"/>, and only one of them is a fact
        /// about the cartridge.
        /// </para>
        /// </summary>
        WentSomewhereElse,
    }

    /// <param name="Left">What the called block leaves in the answer variable.</param>
    /// <param name="Answerer">
    /// The routine, when <see cref="Left"/> is <see cref="LeftBehind.ARoutine"/>; the number,
    /// when it is <see cref="LeftBehind.ANumber"/>; nought otherwise.
    /// </param>
    /// <param name="Through">Where the call is, so the two levels can be read against each other.</param>
    /// <param name="Before">
    /// What answered in the CALLER before the call, when the call itself leaves the variable
    /// alone.
    /// <para>
    /// <b>Only asked when the call provably touches nothing.</b> A call that leaves the answer
    /// variable as it found it means the compare after it is reading whatever was there before —
    /// so the older answer is the right attribution and walking back to it is not a guess. Where
    /// the call DOES answer, or where the reading stopped at a jump, this is
    /// <see cref="LeftBehind.Nothing"/> and nothing is claimed.
    /// </para>
    /// </param>
    /// <param name="Older">The routine that answered before the call, or nought.</param>
    public sealed record AnsweredThroughACall(
        string MapId,
        string What,
        uint Through,
        uint Called,
        LeftBehind Left,
        int Answerer,
        int Value,
        byte Condition,
        LeftBehind Before,
        int Older);

    /// <summary>
    /// The attributions milestone 214 stopped making, made properly — one level in.
    /// <para>
    /// <b>214 added <c>call</c> to the barrier list and lost 42 of 1097 attributions.</b> That
    /// was the right way to be wrong: a missed reading is a reading nobody makes and a false one
    /// goes in a doc as a fact. But the answers are still there and they belong to somebody, and
    /// SEVEN ISLAND showed what that looks like — <c>special 0x0028 ; call 0x081A4EAF ; compare</c>,
    /// where the called block is three commands long and the first of them is
    /// <c>special 0x005D</c>.
    /// </para>
    /// <para>
    /// <b>The rule is: the answer a call leaves behind is whatever answered LAST on its straight
    /// line.</b> Not the first — a block that asks two things leaves the second one's answer —
    /// and not down any branch, because a run takes one arm and this is a question about the
    /// file.
    /// </para>
    /// <para>
    /// One level, and it stops there rather than recursing. A call inside a call is reported as
    /// answering nothing rather than being chased, because each level is another place the
    /// reading could be wrong and this project has been caught by exactly that twice.
    /// </para>
    /// </summary>
    public static List<AnsweredThroughACall> ThroughACall(Rom rom, MapLibrary library, int answer = 0x800D)
    {
        var found = new List<AnsweredThroughACall>();

        foreach ((string mapId, string what, uint address) in Scripts(library))
        {
            List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

            for (var i = 0; i + 1 < commands.Count; i++)
            {
                if (commands[i].Code != Call) continue;
                if (!Adjacent(commands[i], commands[i + 1])) continue;
                if (commands[i + 1].Code != Compare) continue;
                if (commands[i + 1].Word() != answer) continue;

                byte condition = i + 2 < commands.Count &&
                                 Adjacent(commands[i + 1], commands[i + 2]) &&
                                 commands[i + 2].Code is GotoIf or CallIf
                    ? commands[i + 2].Arguments[0]
                    : (byte)0xFF;

                (LeftBehind left, int who) = WhatIsLeftInside(rom, commands[i].Pointer(), answer);

                // Only when the call provably leaves the variable alone. Then, and only then,
                // the compare is reading something older and walking back to it is a reading
                // rather than a guess.
                (LeftBehind before, int older) = OlderAnswer(left, commands, i, answer);

                found.Add(new AnsweredThroughACall(
                    mapId,
                    what,
                    Rom.BaseAddress + (uint)commands[i].Offset,
                    commands[i].Pointer(),
                    left,
                    who,
                    commands[i + 1].Word(2),
                    condition,
                    before,
                    older));
            }
        }

        return found;
    }

    /// <summary>Calls a block and comes back. The barrier 214 added.</summary>
    private const byte Call = 0x04;

    /// <summary>
    /// What a called block leaves in the answer variable on its straight line, and who left it.
    /// <para>
    /// <b>The LAST thing that puts something there, of any kind.</b> The first version of this
    /// looked only for routines and credited <c>0x153</c> at fifty-seven places where the block
    /// ends <c>setvar 0x800D, 1 ; return</c> — the routines inside it were asked and their
    /// answers were thrown away, and the straight line says the answer out loud. Crediting a
    /// routine there is the same fault the barrier was added for, one level down.
    /// </para>
    /// <para>
    /// The straight line only, and one level: a <c>call</c> inside it leaves
    /// <see cref="LeftBehind.Nothing"/> rather than being followed. Each level is another place
    /// to be wrong.
    /// </para>
    /// </summary>
    /// <summary>
    /// What answered in the caller before a call that leaves the answer variable alone.
    /// <para>
    /// Walks back over commands that cannot have answered, and stops at the first that could.
    /// A second call is one of those and it is <b>not</b> followed — that would be walking back
    /// through a level this reading does not go into, and each level is another place to be
    /// wrong.
    /// </para>
    /// </summary>
    /// <summary>
    /// The older answer, but ONLY when the call left the variable alone.
    /// <para>
    /// <b>The condition is the whole licence.</b> Walking back is a reading when the call
    /// provably touched nothing and a guess otherwise, and the difference is a rule — so it
    /// lives here where a test can reach it rather than inside the sweep, which needs a whole
    /// world to run.
    /// </para>
    /// </summary>
    public static (LeftBehind Before, int Older) OlderAnswer(
        LeftBehind leftByTheCall, List<ScriptCommand> commands, int at, int answer = 0x800D) =>
        leftByTheCall == LeftBehind.Nothing
            ? WhatAnsweredBefore(commands, at, answer)
            : (LeftBehind.Nothing, 0);

    public static (LeftBehind Left, int Who) WhatAnsweredBefore(
        List<ScriptCommand> commands, int at, int answer = 0x800D)
    {
        for (int i = at - 1; i >= 0; i--)
        {
            if (!Adjacent(commands[i], commands[i + 1])) return (LeftBehind.Nothing, 0);

            switch (commands[i].Code)
            {
                case Special:
                    return (LeftBehind.ARoutine, commands[i].Word());

                case SpecialVar when commands[i].Word() == answer:
                    return (LeftBehind.ARoutine, commands[i].Word(2));

                case SetVar when commands[i].Word() == answer:
                    return (LeftBehind.ANumber, commands[i].Word(2));

                // Another call, which could have answered and which this does not follow.
                case Call:
                    return (LeftBehind.WentSomewhereElse, 0);
            }

            if (Answering.Contains(commands[i].Code)) return (LeftBehind.WentSomewhereElse, 0);
        }

        return (LeftBehind.Nothing, 0);
    }

    public static (LeftBehind Left, int Who) WhatACallLeaves(Rom rom, uint address, int answer = 0x800D) =>
        WhatIsLeftInside(rom, address, answer);

    /// <summary>
    /// Everything a called block can leave in the answer variable, and what chooses between them.
    /// </summary>
    /// <param name="Answers">
    /// Every distinct outcome, from the straight line and from each arm the straight line
    /// branches to. A block with one entry always leaves the same thing; a block with two is a
    /// yes-or-no.
    /// </param>
    /// <param name="Deciders">
    /// The routines asked on the straight line before a branch. <b>These are what the answer
    /// turns on</b>, and the reason a literal at the end of a straight line is not a constant.
    /// </param>
    public sealed record WhatItCanReturn(
        IReadOnlyList<(LeftBehind Left, int Who)> Answers, IReadOnlyList<int> Deciders);

    /// <summary>
    /// The arms, one level, for the blocks whose straight line ends in a literal and whose arms
    /// ask something.
    /// <para>
    /// 217 could say that fifty-seven places call a block that is not a constant and could not
    /// say what it returns instead. This says: the outcomes, and the routines the choice
    /// between them turns on.
    /// </para>
    /// <para>
    /// <b>One level of arms and no further.</b> An arm that branches again is read for what its
    /// own straight line leaves and its arms are not followed — each level is another place to
    /// be wrong, and this project has been caught by exactly that at 214, 216 and 217.
    /// </para>
    /// </summary>
    public static WhatItCanReturn Returns(Rom rom, uint address, int answer = 0x800D)
    {
        if (address < Rom.BaseAddress || address - Rom.BaseAddress >= (uint)rom.Length)
        {
            return new WhatItCanReturn([], []);
        }

        var answers = new List<(LeftBehind, int)> { WhatIsLeftInside(rom, address, answer) };
        var deciders = new List<int>();

        var asked = 0;

        foreach (ScriptCommand command in ScriptReader.Read(rom, address))
        {
            switch (command.Code)
            {
                case Special:
                    asked = command.Word();
                    break;

                case SpecialVar when command.Word() == answer:
                    asked = command.Word(2);
                    break;

                // A branch. Whatever was asked last is what this choice turns on, and where it
                // goes is another thing the block can leave behind.
                case GotoIf:
                case CallIf:
                    if (asked != 0) deciders.Add(asked);

                    answers.Add(WhatIsLeftInside(rom, command.Pointer(1), answer));
                    break;
            }
        }

        return new WhatItCanReturn(
            [.. answers.Distinct().OrderBy(a => a.Item1).ThenBy(a => a.Item2)],
            [.. deciders.Distinct().Order()]);
    }

    private static (LeftBehind Left, int Who) WhatIsLeftInside(Rom rom, uint address, int answer)
    {
        if (address < Rom.BaseAddress || address - Rom.BaseAddress >= (uint)rom.Length)
        {
            return (LeftBehind.Nothing, 0);
        }

        var left = LeftBehind.Nothing;
        var who = 0;

        // Whether any ARM of it asks a routine, which is a different question from what the
        // straight line ends with and the one that says whether a literal is really a constant.
        bool armsAsk = ScriptReader.ReadAll(rom, address)
            .Any(c => c.Code == Special || (c.Code == SpecialVar && c.Word() == answer));

        List<ScriptCommand> block = ScriptReader.Read(rom, address);

        foreach (ScriptCommand command in block)
        {
            switch (command.Code)
            {
                case Special:
                    (left, who) = (LeftBehind.ARoutine, command.Word());
                    break;

                case SpecialVar when command.Word() == answer:
                    (left, who) = (LeftBehind.ARoutine, command.Word(2));
                    break;

                case SetVar when command.Word() == answer:
                    (left, who) = (
                        armsAsk ? LeftBehind.ANumberOnTheStraightLine : LeftBehind.ANumber,
                        command.Word(2));
                    break;

                case 0x19 when command.Word() == answer:
                case 0x1A when command.Word() == answer:
                    (left, who) = (LeftBehind.AnotherVariable, command.Word(2));
                    break;
            }
        }

        // A block that put nothing there and ended by jumping somewhere else did not leave the
        // variable alone — the reading stopped. Saying so is the difference between a fact and
        // a place this instrument does not go.
        if (left == LeftBehind.Nothing && block.Count > 0 && block[^1].Code == ScriptCommands.Goto)
        {
            return (LeftBehind.WentSomewhereElse, 0);
        }

        return (left, who);
    }

    private static IEnumerable<(string MapId, string What, uint Address)> Scripts(MapLibrary library)
    {
        foreach (LoadedMap map in library.All())
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
                yield return (mapId, $"person {person.LocalId}", person.ScriptAddress);

            foreach (MapTrigger trigger in map.Triggers.Where(t => t.HasScript))
                yield return (mapId, $"trigger ({trigger.X},{trigger.Y})", trigger.ScriptAddress);

            foreach (MapSign sign in map.Signs.Where(s => s.HasScript))
                yield return (mapId, $"sign ({sign.X},{sign.Y})", sign.ScriptAddress);
        }
    }

    public static List<SpecialCall> All(Rom rom, MapLibrary library)
    {
        var found = new List<SpecialCall>();

        foreach (LoadedMap map in library.All())
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            List<(string What, uint Address)> scripts =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => ($"person {o.LocalId}", o.ScriptAddress)),
                .. map.Triggers.Where(t => t.HasScript).Select(t => ($"trigger ({t.X},{t.Y})", t.ScriptAddress)),
                .. map.Signs.Where(s => s.HasScript).Select(s => ($"sign ({s.X},{s.Y})", s.ScriptAddress)),
            ];

            foreach ((string what, uint address) in scripts)
            {
                List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

                for (int i = 0; i < commands.Count; i++)
                {
                    ScriptCommand command = commands[i];

                    int routine = command.Code switch
                    {
                        Special => command.Word(),
                        SpecialVar => command.Word(2),
                        _ => -1,
                    };

                    if (routine < 0) continue;

                    found.Add(new SpecialCall(
                        mapId,
                        what,
                        routine,
                        command.Code == SpecialVar ? command.Word() : null,
                        Before(commands, i),
                        After(commands, i, command.Code == SpecialVar ? command.Word() : 0x800D),
                        Forks(commands, i, command.Code == SpecialVar ? command.Word() : 0x800D)));
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Whether two commands sit next to each other in the image.
    /// <para>
    /// The reader follows calls, so its output is several scripts end to end and two
    /// neighbours in that list are not always neighbours in the cartridge. Without this,
    /// the last command of one script reads as an argument to the first of another.
    /// </para>
    /// </summary>
    private static bool Adjacent(ScriptCommand first, ScriptCommand second) =>
        second.Offset == first.Offset + 1 + first.Arguments.Length;

    /// <summary>
    /// The argument slots written immediately in front of the call.
    /// <para>
    /// Candidates rather than certainties, and the report says so. A setvar contiguous
    /// with a call is where an argument would be written, but nothing in the bytes
    /// distinguishes that from two neighbours that happen to be adjacent — and on a real
    /// image the pair in front of 0x0174 look far more like arguments to the routine that
    /// hands over the S.S. TICKET a few lines later.
    /// </para>
    /// </summary>
    private static List<(int, int)> Before(List<ScriptCommand> commands, int at)
    {
        var arguments = new List<(int, int)>();

        for (int i = at - 1; i >= 0 && i >= at - Window; i--)
        {
            if (!Adjacent(commands[i], commands[i + 1])) break;
            if (commands[i].Code != SetVar) continue;
            if (commands[i].Word() is < FirstArgument or > LastArgument) continue;

            arguments.Insert(0, (commands[i].Word(), commands[i].Word(2)));
        }

        return arguments;
    }

    /// <summary>What the script then compares the answer against, and how it branches.</summary>
    /// <summary>
    /// What the script compares a call's answer against, in the few commands after it.
    /// <para>
    /// Exposed so the barrier list can be tested against a handful of bytes rather than against
    /// a whole world. The rule it guards is not a small one: <b>getting it wrong credits one
    /// routine with another's reply</b>, and it has now done that twice — once through
    /// <c>0xA0</c> and once, at 214, through an ordinary <c>call</c>.
    /// </para>
    /// </summary>
    public static IReadOnlyList<(int Value, byte Condition)> WhatIsComparedAfter(
        List<ScriptCommand> commands, int at, int answer = 0x800D) =>
        [.. After(commands, at, answer)];

    private static List<(int, byte)> After(List<ScriptCommand> commands, int at, int answer)
    {
        var compared = new List<(int, byte)>();

        for (int i = at + 1; i < commands.Count && i <= at + Window; i++)
        {
            if (!Adjacent(commands[i - 1], commands[i])) break;

            // Somebody else has answered; anything after this is about them.
            if (Answering.Contains(commands[i].Code)) break;
            if (commands[i].Code == SetVar && commands[i].Word() == answer) break;

            if (commands[i].Code != Compare) continue;
            if (commands[i].Word() != answer) continue;

            byte condition = i + 1 < commands.Count &&
                             Adjacent(commands[i], commands[i + 1]) &&
                             commands[i + 1].Code is GotoIf or CallIf
                ? commands[i + 1].Arguments[0]
                : (byte)0xFF;

            compared.Add((commands[i].Word(2), condition));
        }

        return compared;
    }

    /// <summary>
    /// Where each arm of the branch after a call actually goes.
    /// <para>
    /// The compare has to be the <em>very next</em> command, not merely a nearby one. A
    /// looser rule finds forks near a command rather than forks about it, and ranks
    /// <c>compare</c> and <c>goto_if</c> themselves among the best-evidenced answerers in
    /// the game — which is retraction 2 again, wearing a different hat.
    /// </para>
    /// <para>
    /// It costs recall: a call whose answer is compared two commands later is not seen.
    /// That is the right way to be wrong here. A missed reading is a reading nobody makes;
    /// a false one goes into a doc as a fact.
    /// </para>
    /// <para>
    /// All three commands read this way so far — 0x46, 0x7C and 0xA0 — put the compare
    /// immediately after themselves at every site. The shape is not a compromise.
    /// </para>
    /// </summary>
    private static List<Branch> Forks(List<ScriptCommand> commands, int at, int answer)
    {
        var forks = new List<Branch>();

        for (int i = at + 1; i < commands.Count - 1 && i <= at + 1; i++)
        {
            if (!Adjacent(commands[i - 1], commands[i])) break;

            if (commands[i].Code != Compare) continue;
            if (commands[i].Word() != answer) continue;
            if (!Adjacent(commands[i], commands[i + 1])) continue;
            if (commands[i + 1].Code is not (GotoIf or CallIf)) continue;

            ScriptCommand jump = commands[i + 1];

            forks.Add(new Branch(
                commands[i].Word(2),
                jump.Arguments[0],
                jump.Pointer(1),

                // The address the read would carry on from, which is the arm taken when
                // the condition does not hold.
                (uint)(Rom.BaseAddress + jump.Offset + 1 + jump.Arguments.Length)));
        }

        return forks;
    }

    /// <summary>
    /// What a routine is asked for, summed over every call to it.
    /// <para>
    /// The specification a stand-in has to meet: how many arguments, which slots, and
    /// which answers the scripts actually distinguish. A routine nobody ever compares the
    /// answer of does something rather than answering something, and one compared against
    /// 0 and 1 alone answers yes or no.
    /// </para>
    /// </summary>
    public sealed record Profile(
        int Routine,
        int Calls,
        int Maps,
        bool Answers,
        IReadOnlyList<int> ArgumentSlots,
        IReadOnlyList<int> AnswersSeen,
        int Branches,
        int BranchesTakenByZero)
    {
        /// <summary>
        /// Where a routine nobody has written stands in for one nobody can read.
        /// <para>
        /// Nothing calls these, so the answer variable keeps whatever it had, which for a
        /// fresh save is zero. That is not neutral: at every site where the script says
        /// "if the answer is zero, skip this", a silent zero skips it. Counting those is
        /// the difference between knowing a routine is unmodelled and knowing what the
        /// game does about it in the meantime.
        /// </para>
        /// </summary>
        public bool ZeroIsMisleading => BranchesTakenByZero > 0;

        public override string ToString() =>
            $"0x{Routine:X4}  {Calls,4} calls on {Maps,3} maps  " +
            (ArgumentSlots.Count == 0
                ? "no arguments".PadRight(24)
                : $"args {string.Join(",", ArgumentSlots.Select(a => $"0x{a:X4}"))}".PadRight(24)) +
            (Answers
                ? $"answer tested against {string.Join(",", AnswersSeen)}".PadRight(30) +
                  (Branches == 0
                      ? ""
                      : $"zero branches away at {BranchesTakenByZero}/{Branches}")
                : "answer never looked at");
    }

    /// <summary>What answering nought amounted to at the places a run actually asked.</summary>
    public enum ZeroWas
    {
        /// <summary>Nothing ever branches on this routine's answer, so nought decides nothing.</summary>
        NeverTested,

        /// <summary>
        /// Nought takes none of the branches. <b>The run declined to answer and the decline cost
        /// nothing that any other wrong answer would not have cost.</b>
        /// </summary>
        ARefusal,

        /// <summary>
        /// Nought takes every branch. <b>The run did not decline; it said yes, everywhere.</b>
        /// </summary>
        AnAssertion,

        /// <summary>Nought takes some of the branches and not others.</summary>
        Both,
    }

    /// <summary>
    /// One routine a run could not answer, and what its silence did.
    /// </summary>
    /// <param name="Asked">How many places the RUN asked it — not how many exist in the file.</param>
    /// <param name="Tested">Every value the file compares its answer against.</param>
    /// <param name="Branches">
    /// How many of this routine's sites in the whole file branch on the answer at all.
    /// <b>The denominator the asked-count does not have.</b> A routine asked eighty-eight times
    /// whose answer is branched on at two sites is a routine whose silence can matter twice —
    /// counting the eighty-eight as places where the silence took a branch is the same mistake
    /// as counting sites where a bucket wants places.
    /// </param>
    /// <param name="TakenByZero">How many of those branches nought takes.</param>
    public sealed record WhatZeroDid(
        int Routine, int Asked, ZeroWas Was, IReadOnlyList<int> Tested, int Branches, int TakenByZero);

    /// <summary>
    /// The join nobody has made: which routines a run could not answer, against what the file
    /// does with the answer.
    /// <para>
    /// <b>"Every one took the zero arm" is three different things.</b> A run reports how many
    /// places called a routine it could not answer and calls the whole number a ceiling. But a
    /// routine whose answer is only ever compared against 2 does the same thing for nought as
    /// for 3, 4 or 9 — the silence costs nothing a wrong answer would not — while a routine
    /// compared against nought takes its branch <em>because</em> the run said nothing. Those are
    /// opposite findings and they have been printing as one number.
    /// </para>
    /// <para>
    /// <c>--routines</c> knows the shape and has never seen a run; the run knows what it asked
    /// and nothing about the shape. Neither half can say this on its own.
    /// </para>
    /// </summary>
    public static IReadOnlyList<WhatZeroDid> ZeroAt(
        IEnumerable<Profile> profiles, IReadOnlyDictionary<int, int> asked)
    {
        Dictionary<int, Profile> byRoutine = profiles.ToDictionary(p => p.Routine);

        var found = new List<WhatZeroDid>();

        foreach ((int routine, int times) in asked)
        {
            byRoutine.TryGetValue(routine, out Profile? profile);

            IReadOnlyList<int> tested = profile?.AnswersSeen ?? [];

            int branches = profile?.Branches ?? 0;
            int taken = profile?.BranchesTakenByZero ?? 0;

            // WHAT NOUGHT DOES, NOT WHAT IT IS COMPARED AGAINST.
            //
            // The first version of this classified on the values alone — nought is an
            // assertion where the file tests against nought, a refusal otherwise. That is
            // wrong and the instrument caught it by printing both numbers side by side: the
            // "nought is never the value tested" bucket reported thirty-nine of its six hundred
            // and ninety branches taken by nought. `compare 0x800D, 1 ; if LESS` is taken by
            // nought and does not test nought. The condition is half the question and the
            // values are the other half; Profile has already done it properly.
            found.Add(new WhatZeroDid(
                routine,
                times,
                branches == 0 ? ZeroWas.NeverTested
                : taken == 0 ? ZeroWas.ARefusal
                : taken == branches ? ZeroWas.AnAssertion
                : ZeroWas.Both,
                tested,
                branches,
                taken));
        }

        // RANKED BY WHAT NOUGHT DECIDES, NOT BY HOW OFTEN IT WAS ASKED.
        //
        // The two are not the same list and on this cartridge they are nearly opposite. 0x194
        // is asked fifty-four times by the widest run and nought takes ONE of its eighteen
        // branches; 0x083 and 0x084 are asked once and twice, and between them nought takes
        // THIRTY-NINE of the mixed bucket's forty-four. A count is not a ranking (trap 3), and
        // the question this block exists to answer is where the silence could matter.
        return
        [
            .. found
                .OrderByDescending(z => z.TakenByZero)
                .ThenByDescending(z => z.Asked)
                .ThenBy(z => z.Routine),
        ];
    }

    public static List<Profile> Profiles(IEnumerable<SpecialCall> calls) =>
    [
        .. calls
            .GroupBy(c => c.Routine)
            .Select(g => new Profile(
                g.Key,
                g.Count(),
                g.Select(c => c.MapId).Distinct().Count(),
                g.Any(c => c.Compared.Count > 0),
                [.. g.SelectMany(c => c.Arguments).Select(a => a.Variable).Distinct().Order()],
                [.. g.SelectMany(c => c.Compared).Select(c => c.Value).Distinct().Order()],
                g.SelectMany(c => c.Compared).Count(c => c.Condition != 0xFF),
                g.SelectMany(c => c.Compared)
                    .Count(c => c.Condition != 0xFF &&
                                ScriptState.Accepts(c.Condition, ScriptState.Compare(0, c.Value)))))
            .OrderByDescending(p => p.Calls),
    ];
}
