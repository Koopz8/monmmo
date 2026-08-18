using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// What the scripts expect of one routine, gathered from every place it is called.
/// </summary>
/// <param name="Routine">The routine number, which is the only name it has.</param>
/// <param name="Sites">How many times it is called.</param>
/// <param name="TakesArguments">
/// How many argument slots are written immediately before it, at the site that writes most.
/// A routine that is never handed anything asks about the save rather than about a thing.
/// </param>
/// <param name="Compared">
/// Every value the answer is compared against, and how often. This is the shape of the
/// routine's range as its callers understand it — a routine compared only against nought and
/// one answers yes or no; one compared against one through eight counts something with eight
/// of it.
/// </param>
/// <param name="Branches">
/// How many sites branch on the answer at all, with nothing in between that could have
/// answered instead.
/// </param>
/// <param name="AcrossABarrier">
/// How many sites branch on the answer <b>only past something that may have answered
/// instead</b> — a <c>call</c>, another <c>special</c>, a <c>callstd</c>. Until 220 these were
/// counted in <see cref="Branches"/> and their values in <see cref="Compared"/>, and nothing
/// said so. This is the error bar on every routine sentence in this project, and it comes back
/// nought for a routine nobody reads across anything.
/// </param>
/// <param name="ComparedAcross">
/// The values compared past the barrier, kept apart from <see cref="Compared"/> for the same
/// reason. They may still belong to this routine — 219 proved nineteen of them do — but proving
/// it takes reading what is in the way, which is <c>--through-a-call</c>'s job and not this
/// one's.
/// </param>
/// <param name="Where">A few places it is called, so the bytes can be looked at.</param>
public sealed record SpecialContract(
    int Routine,
    int Sites,
    int TakesArguments,
    IReadOnlyDictionary<int, int> Compared,
    int Branches,
    int AcrossABarrier,
    IReadOnlyDictionary<int, int> ComparedAcross,
    IReadOnlyList<string> Where)
{
    /// <summary>
    /// The largest value anybody compares the answer against, which is the closest thing to a
    /// range this can honestly report.
    /// </summary>
    public int Highest => Compared.Count == 0 ? 0 : Compared.Keys.Max();

    /// <summary>
    /// True when the compared values are a run from one upwards with nothing missing — the
    /// shape of a count rather than of a yes-or-no or a set of unrelated ids.
    /// </summary>
    public bool LooksLikeACount =>
        Compared.Count >= 3
        && Compared.Keys.Order().SequenceEqual(Enumerable.Range(1, Compared.Count));
}

/// <summary>
/// What every routine is asked, derived from the scripts that ask it.
/// <para>
/// <b>The boundary this project cannot read across, measured from the outside.</b> A
/// <c>special</c> is a call into the game's ARM code by number. What the routine does is in no
/// table and no amount of looking at data will say — so the runner steps over it, the answer
/// variable keeps its zero, and every script that branches on the answer takes the zero arm.
/// Every badge check in this game is one of those, which is why the boundary lands squarely on
/// the endgame.
/// </para>
/// <para>
/// What <em>is</em> readable is the shape of the expectation, and it is readable precisely.
/// A script writes its arguments into 0x8000 and upwards, calls the routine, compares the
/// answer against a number, and branches. Argument count, the values compared, and how many
/// sites branch at all are all in the bytes.
/// </para>
/// <para>
/// That is not what a routine does. It is <b>the specification any stand-in has to satisfy</b>
/// — and unlike a guess, it is checkable: supply an answer, walk the story again, and see how
/// much of the world opens. A number that opens nothing was wrong or irrelevant, and either
/// way that is a result.
/// </para>
/// <para>
/// <see cref="SpecialCalls"/> asks this question of an <em>opcode</em>, which is what it
/// needed for deriving argument widths. This asks it of a <em>routine</em>, which is what a
/// stand-in needs, and the two are different questions that happen to read the same bytes.
/// </para>
/// </summary>
public static class SpecialContracts
{
    /// <summary>Where a script puts an answer when it is not told where to put it.</summary>
    public const int AnswerVariable = 0x800D;

    private const int FirstArgument = 0x8000;

    private const int LastArgument = 0x800F;

    private const byte SetVar = 0x16;
    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte CallIf = 0x07;

    /// <summary>How many commands either side count as belonging to the call.</summary>
    public const int Window = 4;

    public static List<SpecialContract> Derive(Rom rom, MapLibrary library, Action<string>? log = null)
    {
        var sites = new Dictionary<int, int>();
        var arguments = new Dictionary<int, int>();
        var compared = new Dictionary<int, Dictionary<int, int>>();
        var branches = new Dictionary<int, int>();
        var across = new Dictionary<int, int>();
        var comparedAcross = new Dictionary<int, Dictionary<int, int>>();
        var where = new Dictionary<int, List<string>>();

        foreach ((string mapId, string what, uint address) in Scripts(library))
        {
            List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

            for (int i = 0; i < commands.Count; i++)
            {
                ScriptCommand command = commands[i];

                // The routine number sits in a different place for the two opcodes: the one
                // that takes an answer names the variable first.
                int routine = command.Code switch
                {
                    SpecialCalls.Special when command.Arguments.Length >= 2 => command.Word(),
                    SpecialCalls.SpecialVar when command.Arguments.Length >= 4 => command.Word(2),
                    _ => -1,
                };

                if (routine < 0) continue;

                sites[routine] = sites.GetValueOrDefault(routine) + 1;

                int handed = Arguments(commands, i);

                arguments[routine] = Math.Max(arguments.GetValueOrDefault(routine), handed);

                int answer = command.Code == SpecialCalls.SpecialVar ? command.Word() : AnswerVariable;

                (List<int> values, List<int> beyond) = ComparedAfter(commands, i, answer);

                if (values.Count > 0) branches[routine] = branches.GetValueOrDefault(routine) + 1;

                if (beyond.Count > 0) across[routine] = across.GetValueOrDefault(routine) + 1;

                if (!compared.TryGetValue(routine, out Dictionary<int, int>? seen))
                    compared[routine] = seen = [];

                foreach (int value in values) seen[value] = seen.GetValueOrDefault(value) + 1;

                if (!comparedAcross.TryGetValue(routine, out Dictionary<int, int>? far))
                    comparedAcross[routine] = far = [];

                foreach (int value in beyond) far[value] = far.GetValueOrDefault(value) + 1;

                if (!where.TryGetValue(routine, out List<string>? places)) where[routine] = places = [];

                if (places.Count < 3) places.Add($"{mapId} {what} at 0x{command.Offset:X6}");
            }
        }

        List<SpecialContract> derived =
        [
            .. sites.Keys.Order().Select(routine => new SpecialContract(
                routine,
                sites[routine],
                arguments.GetValueOrDefault(routine),
                compared.GetValueOrDefault(routine, []),
                branches.GetValueOrDefault(routine),
                across.GetValueOrDefault(routine),
                comparedAcross.GetValueOrDefault(routine, []),
                where.GetValueOrDefault(routine, []))),
        ];

        log?.Invoke($"  {derived.Count} routines called, {derived.Sum(d => d.Sites)} times between them");
        log?.Invoke($"    {derived.Count(d => d.Branches > 0)} of them are branched on, which is what makes an answer matter");
        log?.Invoke($"    {derived.Count(d => d.LooksLikeACount)} are compared against a run from one upwards");

        // And the error bar, which is the number this reading did not have for six milestones.
        log?.Invoke(
            $"    {derived.Sum(d => d.AcrossABarrier)} site(s) across {derived.Count(d => d.AcrossABarrier > 0)} routine(s) "
            + "branch on the answer only PAST something that may have answered instead, and are not counted above");
        log?.Invoke(
            $"    {derived.Count(d => d.Branches == 0 && d.AcrossABarrier > 0)} routine(s) are branched on ONLY that way — "
            + "every branch this project credited them with was read across a call");

        return derived;
    }

    /// <summary>
    /// How many argument slots are written immediately before the call.
    /// <para>
    /// Only the run touching the call. A setvar four commands earlier with a message in
    /// between is a variable that happens to be nearby.
    /// </para>
    /// </summary>
    private static int Arguments(List<ScriptCommand> commands, int at)
    {
        var handed = 0;

        for (int i = at - 1; i >= 0 && i >= at - Window; i--)
        {
            if (!Adjacent(commands[i], commands[i + 1])) break;
            if (commands[i].Code != SetVar) continue;
            if (commands[i].Word() is < FirstArgument or > LastArgument) continue;

            handed++;
        }

        return handed;
    }

    /// <summary>
    /// What the script compares the answer against in the commands right after — and what it
    /// compares the answer against only on the far side of somebody else's answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The barrier this had none of until 220.</b> Between the <c>special</c> and the
    /// <c>compare</c> there can be a <c>call</c>, a <c>specialvar</c>, a <c>callstd</c>, a
    /// <c>0xA0</c> — anything that puts an answer of its own in the same variable. Reading
    /// past one of those credits this routine with somebody else's reply, which is the fault
    /// <see cref="SpecialCalls"/> was given its barrier for at 214. This reading kept walking
    /// for six more milestones, and the two arms disagreed out loud: <c>--routines</c> gave
    /// <c>0x01C</c> nineteen branches while <c>--special 0x1C</c> said it was never branched
    /// on, about the same nineteen sites.
    /// </para>
    /// <para>
    /// The compares past the barrier are <b>returned rather than dropped</b>, because "this
    /// routine is not branched on" and "this routine's answer is read across something that
    /// may have answered instead" are different facts, and the second is the interesting one.
    /// 219 walked one of them back and found the thing in the way was
    /// <c>copyvar 0x8012, 0x8013 ; return</c>, which cannot have answered — so past the
    /// barrier is where to look next, not where to stop looking.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The same reading, against a handful of bytes rather than against a whole world.
    /// <para>
    /// <see cref="Derive"/> needs a <see cref="MapLibrary"/> and every script on every map. The
    /// rule this milestone is about is four lines inside it, and a rule a test can only reach
    /// through a cartridge is a rule no test reaches.
    /// </para>
    /// </summary>
    public static (IReadOnlyList<int> Direct, IReadOnlyList<int> Beyond) WhatIsComparedAfter(
        List<ScriptCommand> commands, int at, int answer = AnswerVariable)
    {
        (List<int> direct, List<int> beyond) = ComparedAfter(commands, at, answer);

        return (direct, beyond);
    }

    private static (List<int> Direct, List<int> Beyond) ComparedAfter(
        List<ScriptCommand> commands, int at, int answer)
    {
        var values = new List<int>();
        var beyond = new List<int>();
        var past = false;

        for (int i = at + 1; i < commands.Count && i <= at + Window; i++)
        {
            if (!Adjacent(commands[i - 1], commands[i])) break;

            // Somebody has written the answer variable themselves; anything after that is
            // about their number rather than this routine's.
            if (commands[i].Code == SetVar && commands[i].Word() == answer) break;

            // And somebody else may have ANSWERED. Everything from here on is theirs as far
            // as this reading can tell, so it is counted apart rather than credited here.
            if (SpecialCalls.AnswersItself(commands[i].Code)) past = true;

            if (commands[i].Code != Compare || commands[i].Word() != answer) continue;

            // Only a comparison something actually branches on. A compare with nothing after
            // it changes no path and says nothing about what the routine answers.
            bool forks = i + 1 < commands.Count
                         && Adjacent(commands[i], commands[i + 1])
                         && commands[i + 1].Code is GotoIf or CallIf;

            if (!forks) continue;

            (past ? beyond : values).Add(commands[i].Word(2));
        }

        return (values, beyond);
    }

    /// <summary>Whether one command sits immediately after another in the file.</summary>
    private static bool Adjacent(ScriptCommand first, ScriptCommand second) =>
        second.Offset > first.Offset && second.Offset - first.Offset <= 16;

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
}
