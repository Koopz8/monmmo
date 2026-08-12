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

    /// <summary>Every script on every map, with where it came from.</summary>
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

    /// <summary>Where each arm of the branch after a call actually goes.</summary>
    private static List<Branch> Forks(List<ScriptCommand> commands, int at, int answer)
    {
        var forks = new List<Branch>();

        for (int i = at + 1; i < commands.Count - 1 && i <= at + Window; i++)
        {
            if (!Adjacent(commands[i - 1], commands[i])) break;

            if (Answering.Contains(commands[i].Code)) break;
            if (commands[i].Code == SetVar && commands[i].Word() == answer) break;

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
