using PokeMmo.Core.Scripts;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// What one arm of one conditional would do, as far as the world is concerned.
/// <para>
/// Only the part that is <em>this arm's own</em>. Almost every branch in this cartridge
/// rejoins — both arms end up at the same <c>release</c>, and a great many of them reach the
/// same shared block on the way — so an arm summarised on everything reachable from it would
/// credit each arm with the other one's work and report two identical halves. The difference
/// is the measurement; the union is noise.
/// </para>
/// </summary>
/// <param name="Sets">Flags this arm turns on.</param>
/// <param name="Clears">Flags this arm turns off.</param>
/// <param name="Walks">True when it walks somebody — the quiet way a doorway is cleared.</param>
/// <param name="Hides">True when it takes somebody off the map — the loud way.</param>
/// <param name="HandsSomethingOver">True when it gives an item or a creature.</param>
/// <param name="Routines">Routines it calls into the game's own code by number.</param>
/// <param name="Commands">How many commands are on this arm and not on the other.</param>
public sealed record WhatAnArmDoes(
    IReadOnlyList<int> Sets,
    IReadOnlyList<int> Clears,
    bool Walks,
    bool Hides,
    bool HandsSomethingOver,
    IReadOnlyList<int> Routines,
    int Commands)
{
    /// <summary>An arm with nothing on it that the other arm does not also have.</summary>
    public bool Nothing =>
        Sets.Count == 0
        && Clears.Count == 0
        && !Walks
        && !Hides
        && !HandsSomethingOver
        && Routines.Count == 0;

    /// <summary>What it does, in words, for anybody printing it.</summary>
    public override string ToString()
    {
        string[] did =
        [
            .. new[]
            {
                Walks ? "walks somebody" : null,
                Hides ? "hides somebody" : null,
                HandsSomethingOver ? "hands something over" : null,
                Sets.Count > 0 ? $"sets {string.Join(", ", Sets.Select(f => $"0x{f:X4}"))}" : null,
                Clears.Count > 0 ? $"clears {string.Join(", ", Clears.Select(f => $"0x{f:X4}"))}" : null,
                Routines.Count > 0
                    ? "asks routine(s) " + string.Join(", ", Routines.Select(r => $"0x{r:X3}"))
                    : null,
            }.OfType<string>(),
        ];

        return did.Length == 0 ? "nothing this build can see" : string.Join(", ", did);
    }
}

/// <summary>
/// One flag a script asks about, and what each answer is worth.
/// <para>
/// <b>Which arm is which is read, not assumed.</b> <c>checkflag</c> leaves a comparison of
/// one-or-nothing against one, and the conditional that follows carries the operator byte
/// that decides what it accepts — so <c>goto if less</c> is the "not yet" arm and <c>goto if
/// equal</c> is the "already done" arm, and the same two blocks swap places between them.
/// Assuming the branch is the set arm would have been right about half the time, which is the
/// worst kind of wrong: it reads clean.
/// </para>
/// </summary>
/// <param name="Flag">The flag number.</param>
/// <param name="At">Where the <c>checkflag</c> sits, so it can be looked at.</param>
public sealed record FlagAsked(int Flag, int At, WhatAnArmDoes IfSet, WhatAnArmDoes IfClear)
{
    /// <summary>
    /// True when both answers come to the same thing here — the two arms rejoin without
    /// either doing anything the other does not.
    /// <para>
    /// A real and common finding rather than a failure: plenty of flags in this cartridge
    /// only decide which line of text somebody says. A gate that changes nothing is not a
    /// gate, and saying so is the whole point of asking.
    /// </para>
    /// </summary>
    public bool NeitherAnswerChangesAnything => IfSet.Nothing && IfClear.Nothing;
}

/// <summary>What one script asks about, and what it could not be asked.</summary>
/// <param name="Flags">Every flag it asks about, with both answers priced.</param>
/// <param name="AskedWithoutABranch">
/// <c>checkflag</c>s this could not pair with a conditional, because something sits between
/// the two. Printed rather than swallowed: it is exactly how many gates this instrument is
/// blind to, and a blind spot with no number on it is a claim of completeness nobody checked.
/// </param>
/// <param name="Truncated">
/// True when the read ran into its own limit, so there may be more script than this saw.
/// </param>
public sealed record WaitingOn(
    IReadOnlyList<FlagAsked> Flags,
    int AskedWithoutABranch,
    bool Truncated)
{
    /// <summary>
    /// Every other kind of question this script branches on, by the command that asked it.
    /// <para>
    /// <b>The instrument being able to say the question was wrong.</b> "Waiting on a flag" is
    /// a guess until something reads the bytes, and a script gated on a variable, on what is
    /// in the bag, or on a routine's answer would come back from a flag-shaped instrument as
    /// <em>asks about no flag at all</em> — which reads like a person with no part in the
    /// story and is nothing of the kind.
    /// </para>
    /// <para>
    /// So every conditional that is not a <c>checkflag</c>'s is counted under whatever
    /// command left the comparison it reads. A door gated on <c>0x47</c> is a shopping list;
    /// one gated on a <c>compare</c> after a <c>special</c> is behind the code boundary after
    /// all. They are different jobs and they must not arrive looking the same.
    /// </para>
    /// </summary>
    public IReadOnlyList<(byte Code, int Times)> OtherQuestions { get; init; } = [];
}

/// <summary>
/// One script that turns a flag on, and enough of where it is to go and look.
/// <para>
/// The address travels because the name does not answer the next question. <c>1.57 trigger
/// (5,15)</c> is a place; whether the run <em>stood on that square and ran it</em> is the
/// difference between a map to reach, a square the walk never touched, and a script that ran
/// and stopped short of its own <c>setflag</c> — three different jobs behind one line.
/// </para>
/// </summary>
public sealed record SetsAFlag(string MapId, string What, uint Address)
{
    public override string ToString() => $"{MapId} {What}";
}

/// <summary>
/// One condition standing between the start of a script and something inside it.
/// <para>
/// Not "which flags does this script mention" — <em>which answers had to go a particular way
/// for this command to run at all</em>. A script that ran and did not set its flag has the
/// setting behind one of these, and the chain of them is the list of things that have to be
/// true before that door opens.
/// </para>
/// </summary>
/// <param name="AskedBy">The command that left the comparison — <c>checkflag</c>, <c>compare</c>, or something unnamed.</param>
/// <param name="Word">Its first word: a flag number, or the variable being compared.</param>
/// <param name="Against">What it was compared against, for a <c>compare</c>.</param>
/// <param name="Condition">The operator byte on the branch.</param>
/// <param name="TookTheBranch">Whether getting here meant jumping rather than falling through.</param>
public sealed record OnTheWay(byte AskedBy, int Word, int Against, byte Condition, bool TookTheBranch)
{
    /// <summary>
    /// True when this script put the number there itself, earlier on this very path.
    /// <para>
    /// <b>The difference between a precondition and a switch</b>, and the fault this record
    /// was corrected to carry. A comparison on a variable the script wrote two lines above is
    /// not something that must be true before the door opens — it is the script deciding
    /// something and then reading its own answer back. Reported as a gate, it sends the next
    /// session hunting for whoever sets a number nobody outside this script ever sets.
    /// </para>
    /// </summary>
    public bool DecidedHere { get; init; }

    /// <summary>Which command put it there — <c>setvar</c>, or a routine, which is the boundary again.</summary>
    public byte DecidedBy { get; init; }

    /// <summary>What it put there.</summary>
    public int Became { get; init; }

    /// <summary>What had to be true, in words.</summary>
    public override string ToString()
    {
        if (DecidedHere)
        {
            string how = DecidedBy == SpecialCalls.SpecialVar
                ? $"routine 0x{Became:X3} put it there"
                : $"this script {ScriptCommands.NameOf(DecidedBy)} {Became} first";

            return $"0x{Word:X4} {Operator()} {Against} — NOT A GATE, {how}";
        }

        if (AskedBy == 0x2B)
        {
            bool jumpsWhenSet = ScriptState.Accepts(Condition, ScriptState.Compare(1, 1));
            bool jumpsWhenClear = ScriptState.Accepts(Condition, ScriptState.Compare(0, 1));

            return jumpsWhenSet == jumpsWhenClear
                ? $"flag 0x{Word:X4} either way"
                : $"flag 0x{Word:X4} {(TookTheBranch == jumpsWhenSet ? "SET" : "CLEAR")}";
        }

        if (AskedBy == 0x21) return $"0x{Word:X4} {Operator()} {Against}";

        return $"something this could not name (0x{AskedBy:X2}) {Operator()} {Against}";
    }

    /// <summary>
    /// The comparison as it had to come out. Falling through a branch is the operator
    /// inverted, which is a thing worth writing down once rather than in every caller.
    /// </summary>
    private string Operator()
    {
        string taken = Condition switch
        {
            0 => "<", 1 => "==", 2 => ">", 3 => "<=", 4 => ">=", 5 => "!=", _ => "?",
        };

        string missed = Condition switch
        {
            0 => ">=", 1 => "!=", 2 => "<=", 3 => ">", 4 => "<", 5 => "==", _ => "?",
        };

        return TookTheBranch ? taken : missed;
    }
}

/// <summary>One script writing a variable, and what it puts in it.</summary>
/// <param name="How">The command — <c>setvar</c>, <c>addvar</c>, <c>subvar</c>, <c>copyvar</c>.</param>
/// <param name="Value">
/// The second word. A number for everything except <c>copyvar</c>, where it is another
/// variable and what is in it cannot be known from here — said out loud rather than printed as
/// though it were a value.
/// </param>
public sealed record WritesAVariable(SetsAFlag Where, byte How, int Value)
{
    public override string ToString() =>
        How == 0x19
            ? $"{Where} copies 0x{Value:X4} into it"
            : $"{Where} {ScriptCommands.NameOf(How)} {Value}";
}

/// <summary>
/// What a person standing in a doorway is waiting for.
/// <para>
/// <b>The question a playthrough cannot ask.</b> A run takes one arm of each conditional —
/// that is what running is — so a script whose whole part in the story is on the arm the run
/// did not take reports as a person who does nothing. Four people in the last four doorways
/// read exactly that way: talked to, set no flag, asked for nothing, walked nobody, called no
/// routine at all. Not behind the code boundary, then. Behind a flag, with the run reading
/// the "not yet" arm and stopping.
/// </para>
/// <para>
/// <see cref="ScriptReader.ReadAll"/> has followed both arms for milestones. Nothing has ever
/// asked it <em>which</em> arm, or what the difference between them was worth. That is all
/// this is.
/// </para>
/// <para>
/// <b>It has to be able to come back empty.</b> If these four scripts ask about no flag, the
/// sentence above is wrong and this prints that instead — which is the only reason to build
/// an instrument rather than write the conclusion down.
/// </para>
/// </summary>
public static class WhatItIsWaitingFor
{
    private const byte CheckFlag = 0x2B;
    private const byte SetFlag = 0x29;
    private const byte ClearFlag = 0x2A;
    private const byte HideObject = 0x53;
    private const byte GiveItem = 0x46;
    private const byte GiveItemToo = 0x44;
    private const byte GiveMon = 0x79;

    /// <summary>
    /// Every flag this script asks about, and what each answer would have done.
    /// </summary>
    public static WaitingOn Asks(Rom rom, uint address, int maxScripts = 96)
    {
        var flags = new List<FlagAsked>();
        var loose = 0;
        var otherwise = new Dictionary<byte, int>();

        List<uint> blocks = ScriptReader.Reachable(rom, address, maxScripts);

        foreach (uint block in blocks)
        {
            List<ScriptCommand> commands = ScriptReader.Read(rom, block);

            // Every conditional in here that is not a checkflag's, under whatever command
            // left the comparison it reads. Counted first and separately, so that "it asks
            // about no flag" can never be printed over the top of "it asks about something
            // else entirely".
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].Code is not (ScriptCommands.GotoIf or ScriptCommands.CallIf)) continue;
                if (i > 0 && commands[i - 1].Code == CheckFlag) continue;

                byte asked = i > 0 ? commands[i - 1].Code : (byte)0;

                otherwise[asked] = otherwise.GetValueOrDefault(asked) + 1;
            }

            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].Code != CheckFlag || commands[i].Arguments.Length < 2) continue;

                // The pair this cartridge writes everywhere: checkflag, then a conditional
                // on the comparison it left behind. Anything in between and the comparison
                // may not be this flag's any more, so it is counted and left alone rather
                // than read as though it were.
                if (i + 1 >= commands.Count
                    || commands[i + 1].Code is not (ScriptCommands.GotoIf or ScriptCommands.CallIf)
                    || commands[i + 1].Arguments.Length < 5)
                {
                    loose++;
                    continue;
                }

                ScriptCommand branch = commands[i + 1];

                uint jumpsTo = branch.Pointer(1);
                uint carriesOn = Rom.BaseAddress + (uint)(branch.Offset + 1 + branch.Arguments.Length);

                if (!rom.IsRomAddress(jumpsTo) || !rom.IsRomAddress(carriesOn))
                {
                    loose++;
                    continue;
                }

                // Which of those two the flag being set actually leads to. The operator
                // byte says; a checkflag leaves set as equal and clear as less.
                byte condition = branch.Arguments[0];

                bool jumpsWhenSet = ScriptState.Accepts(condition, ScriptState.Compare(1, 1));
                bool jumpsWhenClear = ScriptState.Accepts(condition, ScriptState.Compare(0, 1));

                (WhatAnArmDoes onlyOverThere, WhatAnArmDoes onlyHere) =
                    Difference(rom, jumpsTo, carriesOn, maxScripts);

                var nothing = new WhatAnArmDoes([], [], false, false, false, [], 0);

                // A `call` comes back. So what follows a conditional *call* is not the other
                // arm — it is the shared remainder, run whichever way the answer went, and
                // pricing it as the cost of the answer would credit both arms with work that
                // is not either one's. Only the called block is the difference.
                //
                // A conditional `goto` never comes back and keeps both halves.
                if (branch.Code == ScriptCommands.CallIf) onlyHere = nothing;

                flags.Add(new FlagAsked(
                    commands[i].Word(),
                    commands[i].Offset,
                    jumpsWhenSet ? onlyOverThere : jumpsWhenClear ? onlyHere : nothing,
                    jumpsWhenClear ? onlyOverThere : jumpsWhenSet ? onlyHere : nothing));
            }
        }

        return new WaitingOn(
            [.. flags.DistinctBy(f => (f.Flag, f.At))],
            loose,
            ScriptReader.ReadAllTruncated(rom, address, maxScripts))
        {
            OtherQuestions = [.. otherwise.OrderByDescending(p => p.Value).Select(p => (p.Key, p.Value))],
        };
    }

    /// <summary>
    /// What stands between the start of a script and its turning a particular flag on.
    /// <para>
    /// <b>The question after "it ran this script and the flag is still unset".</b> Three
    /// SAFFRON doors are one flag, that flag is set by a trigger in SILPH CO., and the run
    /// stood on the trigger and ran it. So the <c>setflag</c> is behind a branch inside it,
    /// and this is the list of answers that had to go a particular way to reach it.
    /// </para>
    /// <para>
    /// <b>One path, not every path.</b> The first route found is returned, and a
    /// <c>setflag</c> reachable two ways has a second chain this does not show. That is a
    /// real limit and the reason this returns a chain rather than a verdict: an empty chain
    /// means unconditional <em>on the way found</em>, and null means nothing reachable sets
    /// the flag at all.
    /// </para>
    /// </summary>
    public static IReadOnlyList<OnTheWay>? PathTo(Rom rom, uint address, int flag, int maxSteps = 8192)
    {
        var seen = new HashSet<(uint Block, int At)>();
        var queue = new Queue<(uint Block, int At, List<OnTheWay> Chain, Dictionary<int, (byte How, int Value)> Put)>();

        queue.Enqueue((address, 0, [], []));

        var steps = 0;

        while (queue.Count > 0 && steps++ < maxSteps)
        {
            (uint block, int at, List<OnTheWay> chain, Dictionary<int, (byte How, int Value)> put) =
                queue.Dequeue();

            if (!seen.Add((block, at))) continue;
            if (rom.ToOffsetOrNull(block) is null) continue;

            List<ScriptCommand> commands = ScriptReader.Read(rom, block);

            // What left the comparison the next conditional will read. Carried across
            // commands rather than assumed adjacent, because a script that compares and then
            // does something harmless before branching is still branching on the compare.
            ScriptCommand? asked = null;

            for (int i = at; i < commands.Count; i++)
            {
                ScriptCommand command = commands[i];

                if (command.Code == SetFlag && command.Arguments.Length >= 2 && command.Word() == flag)
                    return chain;

                if (command.Code is CheckFlag or 0x21 or 0x22 or 0x47) asked = command;

                // What this script has already put in a variable before reading it back.
                //
                // THE FAULT THIS WAS WRITTEN TO FIX. A comparison on a variable the script
                // itself wrote two lines earlier is not a precondition — it is a switch the
                // script computes and then reads, and reporting it as something that must be
                // true before the door opens sends the next session hunting for whoever sets
                // a number that nobody outside this script ever sets. `0x4001 != 0 AND
                // 0x4001 != 1` read exactly like a story gate and 285 scripts write 0x4001.
                if (command.Code is SetVar or AddVar or SubVar or CopyVar or CopyVarIfNotZero
                    && command.Arguments.Length >= 4)
                {
                    put[command.Word()] = (command.Code, command.Word(2));
                }

                // And a routine writing into one, which is the same thing with the answer on
                // the far side of the code boundary.
                if (command.Code == SpecialCalls.SpecialVar && command.Arguments.Length >= 4)
                    put[command.Word()] = (SpecialCalls.SpecialVar, command.Word(2));

                if (command.Code is ScriptCommands.GotoIf or ScriptCommands.CallIf
                    && command.Arguments.Length >= 5)
                {
                    int word = asked is { Arguments.Length: >= 2 } ? asked.Word() : 0;

                    var step = new OnTheWay(
                        asked?.Code ?? 0,
                        word,
                        asked is { Arguments.Length: >= 4 } ? asked.Word(2) : 0,
                        command.Arguments[0],
                        true)
                    {
                        DecidedHere = asked?.Code != CheckFlag && put.ContainsKey(word),
                        DecidedBy = put.GetValueOrDefault(word).How,
                        Became = put.GetValueOrDefault(word).Value,
                    };

                    if (rom.IsRomAddress(command.Pointer(1)))
                        queue.Enqueue((command.Pointer(1), 0, [.. chain, step], new(put)));

                    // And carrying on past it is the other answer, priced the same way.
                    chain = [.. chain, step with { TookTheBranch = false }];

                    asked = null;

                    continue;
                }

                if (command.Code is ScriptCommands.Goto or ScriptCommands.Call
                    && rom.IsRomAddress(command.Pointer()))
                {
                    queue.Enqueue((command.Pointer(), 0, chain, new(put)));

                    if (command.Code == ScriptCommands.Goto) break;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Everywhere a script writes a variable, by variable.
    /// <para>
    /// <b>The mirror of <see cref="SetBy"/>, and the thing the chain to SAFFRON turned out to
    /// need.</b> What stands in front of that door is not a flag at all — it is
    /// <c>0x4001 != 0 AND 0x4001 != 1</c>, a story counter, and "which flags gate what" cannot
    /// see a counter. A variable nothing writes is behind the code boundary exactly as a flag
    /// nothing sets is, and the two questions had one answer between them.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<WritesAVariable>> WritesTo(
        Rom rom, IEnumerable<SetsAFlag> scripts)
    {
        var found = new Dictionary<int, List<WritesAVariable>>();

        foreach (SetsAFlag script in scripts)
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, script.Address))
            {
                // setvar, addvar, subvar and copyvarifnotzero all take the variable first and
                // a number second. copyvar takes a variable second, and what is in it is not
                // knowable from here — which is worth saying rather than reporting as a value.
                if (command.Code is not (SetVar or AddVar or SubVar or CopyVar or CopyVarIfNotZero)) continue;
                if (command.Arguments.Length < 4) continue;

                if (!found.TryGetValue(command.Word(), out List<WritesAVariable>? where))
                    found[command.Word()] = where = [];

                var wrote = new WritesAVariable(script, command.Code, command.Word(2));

                if (!where.Contains(wrote)) where.Add(wrote);
            }
        }

        return found.ToDictionary(p => p.Key, p => (IReadOnlyList<WritesAVariable>)p.Value);
    }

    private const byte SetVar = 0x16;
    private const byte AddVar = 0x17;
    private const byte SubVar = 0x18;
    private const byte CopyVar = 0x19;
    private const byte CopyVarIfNotZero = 0x1A;

    /// <summary>
    /// Everywhere a script turns a flag on, by flag.
    /// <para>
    /// The second half of the question, and the one that turns a flag number into a job:
    /// a door waiting on a flag nothing in the world sets is a door behind the code
    /// boundary, and a door waiting on a flag a person two maps away sets is a walk.
    /// </para>
    /// <para>
    /// Handed the scripts to look at rather than opening a cartridge itself, because what
    /// counts as "every script in the world" is the caller's question — and because a
    /// measurement that needs a whole map library to test is a measurement nobody tests.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<SetsAFlag>> SetBy(
        Rom rom, IEnumerable<SetsAFlag> scripts)
    {
        var found = new Dictionary<int, List<SetsAFlag>>();

        foreach (SetsAFlag script in scripts)
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, script.Address))
            {
                if (command.Code != SetFlag || command.Arguments.Length < 2) continue;

                if (!found.TryGetValue(command.Word(), out List<SetsAFlag>? where))
                    found[command.Word()] = where = [];

                if (!where.Contains(script)) where.Add(script);
            }
        }

        return found.ToDictionary(p => p.Key, p => (IReadOnlyList<SetsAFlag>)p.Value);
    }

    /// <summary>
    /// What each of two arms does that the other does not.
    /// <para>
    /// Both are read the way <see cref="ScriptReader.ReadAll"/> reads anything — following
    /// calls and gotos and both halves of any conditional inside them — and then each is
    /// stripped of every command the other one also reaches. What survives is the price of
    /// the answer.
    /// </para>
    /// </summary>
    private static (WhatAnArmDoes Jumped, WhatAnArmDoes CarriedOn) Difference(
        Rom rom, uint jumpsTo, uint carriesOn, int maxScripts)
    {
        List<ScriptCommand> over = Arm(rom, jumpsTo, maxScripts);
        List<ScriptCommand> here = Arm(rom, carriesOn, maxScripts);

        var inOver = over.Select(c => c.Offset).ToHashSet();
        var inHere = here.Select(c => c.Offset).ToHashSet();

        return (
            Summarise([.. over.Where(c => !inHere.Contains(c.Offset))]),
            Summarise([.. here.Where(c => !inOver.Contains(c.Offset))]));
    }

    /// <summary>Everything one arm runs, deduplicated by where each command sits.</summary>
    private static List<ScriptCommand> Arm(Rom rom, uint address, int maxScripts) =>
    [
        .. ScriptReader
            .Reachable(rom, address, maxScripts)
            .SelectMany(block => ScriptReader.Read(rom, block))
            .DistinctBy(command => command.Offset),
    ];

    private static WhatAnArmDoes Summarise(IReadOnlyList<ScriptCommand> commands)
    {
        var sets = new List<int>();
        var clears = new List<int>();
        var routines = new List<int>();

        var walks = false;
        var hides = false;
        var hands = false;

        foreach (ScriptCommand command in commands)
        {
            switch (command.Code)
            {
                case SetFlag when command.Arguments.Length >= 2:
                    if (!sets.Contains(command.Word())) sets.Add(command.Word());
                    break;

                case ClearFlag when command.Arguments.Length >= 2:
                    if (!clears.Contains(command.Word())) clears.Add(command.Word());
                    break;

                case MovementLists.ApplyMovement:
                    walks = true;
                    break;

                case HideObject:
                    hides = true;
                    break;

                case GiveItem or GiveItemToo or GiveMon:
                    hands = true;
                    break;

                case SpecialCalls.Special when command.Arguments.Length >= 2:
                    if (!routines.Contains(command.Word())) routines.Add(command.Word());
                    break;

                case SpecialCalls.SpecialVar when command.Arguments.Length >= 4:
                    if (!routines.Contains(command.Word(2))) routines.Add(command.Word(2));
                    break;
            }
        }

        return new WhatAnArmDoes(sets, clears, walks, hides, hands, routines, commands.Count);
    }
}
