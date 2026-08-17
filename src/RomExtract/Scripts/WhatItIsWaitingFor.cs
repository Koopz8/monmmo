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
    public static IReadOnlyDictionary<int, IReadOnlyList<string>> SetBy(
        Rom rom, IEnumerable<(string What, uint Address)> scripts)
    {
        var found = new Dictionary<int, List<string>>();

        foreach ((string what, uint address) in scripts)
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (command.Code != SetFlag || command.Arguments.Length < 2) continue;

                if (!found.TryGetValue(command.Word(), out List<string>? where))
                    found[command.Word()] = where = [];

                if (!where.Contains(what)) where.Add(what);
            }
        }

        return found.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value);
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
