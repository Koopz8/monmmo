using PokeMmo.Core.Save;
using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// One script, run the way the playthrough runs it — with what the walk already knows.
/// <para>
/// <b>This was a local function in <c>Program.cs</c>, and that is the whole reason it is here.</b>
/// Running a script is not printing: it is deciding what a scene does given the flags the run
/// holds, the bag it carries, the trainers it has beaten and the offers it takes. All of that
/// lived in a file with no tests, that no fixture can reach, and which nothing can break on
/// purpose.
/// </para>
/// <para>
/// Two live faults were sitting in it when it moved, both found by measuring rather than by
/// reading, and both un-guardable where they were:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Nobody told it who had been beaten.</b> A <c>trainerbattle</c> is its own conditional —
/// beaten, the fight does nothing and the script carries on into whatever the victory was for —
/// and with <c>HasBeaten</c> false at every site, every script containing a fight stopped at the
/// fight on every pass, forever. That is SILPH CO.'s <c>setflag 0x003E</c>, eleven commands past
/// GIOVANNI, and two sessions were spent on it.
/// </description></item>
/// <item><description>
/// <b>The continuation after an unanswered question carried the flags and not the variables.</b>
/// PALLET TOWN's three balls each write their species into <c>0x4002</c> and then ask; the
/// <c>givemon</c> on the far side reads it back. Continuing with a state that had never heard of
/// it made the species nought, and <c>givemon</c> of nought hands over nothing. No run this
/// project has ever printed had a starter.
/// </description></item>
/// </list>
/// <para>
/// The same structural fault — a rule about the world living where no test can reach it — had
/// been found five times in six milestones before this moved.
/// </para>
/// </summary>
/// <param name="rom">The player's own cartridge.</param>
/// <param name="teaches">Which item teaches which field move, so a gift can be counted as one.</param>
/// <param name="answers">Stand-ins for routines this project cannot execute. MODELLED.</param>
/// <param name="variables">Numbers put into the story's own variables before every script. MODELLED.</param>
/// <param name="sayYes">Whether to take every offer. MODELLED, and a ceiling.</param>
/// <param name="beaten">
/// Who the run has beaten. Shared with the walk rather than handed over, so a win is visible to
/// the very next script — which is what the cartridge does.
/// </param>
public sealed class HowAScriptRuns(
    Rom rom,
    IReadOnlyDictionary<int, int> teaches,
    IReadOnlyDictionary<int, int>? answers = null,
    IReadOnlyDictionary<int, int>? variables = null,
    bool sayYes = false,
    IReadOnlyCollection<int>? beaten = null,
    IDictionary<int, int>? remembered = null,
    int? watch = null)
{
    /// <summary>
    /// Where the scratch pads stop and the story's own memory begins.
    /// <para>
    /// <b>Read off the cartridge rather than assumed.</b> <c>--who-writes</c> counts how many
    /// places in the whole image put a number in each variable, and the distribution has a
    /// cliff in it: the twelve variables below this are written up to a hundred and sixty-eight
    /// times each, and every band above tops out at twenty-one and mostly under ten. A pad
    /// three hundred scripts scribble on is not something the story remembers.
    /// </para>
    /// <para>
    /// Where exactly to cut is a decision — MODELLED — but that there is somewhere to cut is
    /// a measurement, and the number is printed by the instrument that found it.
    /// </para>
    /// </summary>
    public const int FirstRemembered = 0x4010;

    /// <summary>
    /// What the story is holding, across scripts.
    /// <para>
    /// <b>Flags crossed from one script to the next and numbers did not.</b> The run carried its
    /// flags, its bag, and latterly the trainers it had beaten; every variable was rebuilt from
    /// nothing at every script, so a counter set by one scene was zero by the time the next
    /// scene read it.
    /// </para>
    /// <para>
    /// PALLET TOWN is the whole opening of the game and it is a counter: the trigger north of
    /// the town puts one in <c>0x4055</c>, the lab's arrival script reads that one and puts in
    /// two, and two is what makes the three balls give you something. With no memory between
    /// scripts the first step of that was undone before the second ran, and no run this project
    /// has printed has ever held a starter.
    /// </para>
    /// </summary>
    /// <para>
    /// Shared with the walk when the walk is honouring conditions, because a trigger that fires
    /// at <c>0x4055 == 1</c> and a script that reads <c>0x4055</c> have to be looking at the
    /// same number or the run is two runs.
    /// </para>
    private readonly IDictionary<int, int> _remembered = remembered ?? new Dictionary<int, int>();
    /// <summary>
    /// Run one script with everything the walk has learned so far, and say what it did.
    /// </summary>
    /// <param name="flags">What the run has turned on, which decides which arm every branch takes.</param>
    /// <param name="carrying">Its bag, asked rather than copied — it changes between one person and the next.</param>
    public PlayedScript Read(uint address, IReadOnlyCollection<int> flags, Bag carrying)
    {
        var state = new ScriptState
        {
            // What the playthrough is holding, asked rather than copied — the bag
            // changes underneath this between one person and the next.
            CountOfItem = carrying.CountOf,
        };

        foreach (int flag in flags) state.Set(flag);

        // And who it has beaten, which is half of what a trainerbattle asks. Without this
        // the fight is always in front of the script and everything the victory unlocks is
        // behind it, on every pass, however many the run wins.
        foreach (int trainer in (beaten ?? [])) state.MarkBeaten(trainer);

        // What earlier scenes left in the story's own variables. Before the injected ones, so
        // a lever on the command line still wins.
        foreach ((int variable, int held) in _remembered) state.Write(variable, held);

        // Modelled, and put in before the script rather than after: a counter is read on
        // the first line of the scene it gates.
        foreach ((int variable, int put) in variables ?? new Dictionary<int, int>())
            state.Write(variable, put);

        ScriptRun run = ScriptRunner.Run(rom, address, state, answers: answers, watch: watch);

        var wrote = new Dictionary<int, int>(run.VariablesWritten);
        var touched = new List<VariableTouch>(run.Touched);
        var flagsSet = new List<int>(run.FlagsSet);
        var flagsCleared = new List<int>(run.FlagsCleared);
        var specials = new List<int>(run.SpecialsCalled);
        var hides = new List<int>(run.Hides);
        var walked = new List<(int PersonId, IReadOnlyList<Direction> Steps)>();

        // How far a scene walks somebody, as a displacement. The steps are the cartridge's
        // own bytes and what they mean was derived by walking every list across every map
        // and counting who ended up inside a wall; a step this project does not model is
        // stood still through, which is DirectionOf's honest reading rather than a guess.
        //
        // The player is skipped. Where somebody is standing when they talk to you is not a
        // fact about an image, and the player was never in anybody's way.
        void Follow(ScriptRun one)
        {
            foreach (SceneBeat.Walk step in one.Beats.OfType<SceneBeat.Walk>())
            {
                if (step.IsPlayer) continue;

                // The steps, in order, and not their sum. A sum is applied in one jump and
                // lands wherever the arithmetic says — which on a real cartridge was off the
                // map five times out of six. A step this project does not model is stood
                // still through, which is DirectionOf's own honest reading.
                List<Direction> going = [.. step.Steps.Select(MovementLists.DirectionOf).OfType<Direction>()];

                if (going.Count > 0) walked.Add((step.PersonId, going));
            }
        }

        Follow(run);
        var asked = new List<(int, int, bool)>([.. run.ItemsAsked.Select(a => (a.ItemId, a.Count, a.Carried))]);
        var stoppedAt = new List<byte>();

        if (run.StoppedAt is { } firstUnread) stoppedAt.Add(firstUnread);

        int? gives = run.GivesItem;
        int givesCount = run.GivesCount;
        int? takes = run.TakesItem;
        int takesCount = run.TakesCount;
        (int Species, int Level)? mon = run.GivesMon;
        int? fights = run.TrainerId;
        uint afterTheFight = run.AfterTheFight;

        // And the yes-or-no, which nothing in this project has ever answered.
        //
        // <b>Modelled, and it is a policy rather than a reading.</b> The runner stops at
        // one and hands back where to carry on from, because choosing needs a person.
        // Saying yes is the choice that opens the most world, which makes this a ceiling
        // exactly as --boat and --answer are: an offer accepted is not proof anybody
        // would accept it, and an offer never reached is not proof of anything at all.
        //
        // Bounded, because a script can loop back to its own question — that is how a
        // "which one do you want" prompt waits for an answer, and running it forever is
        // not an answer either.
        for (var answered = 0; sayYes && run.Question is { } carryOn && answered < 8; answered++)
        {
            foreach (int flag in flagsSet) state.Set(flag);
            foreach (int flag in flagsCleared) state.Clear(flag);

            // AND THE VARIABLES, WHICH WERE NOT CARRIED AND ARE HALF OF WHAT A SCENE IS.
            //
            // The flags crossed this line and the numbers did not. PALLET TOWN's three
            // balls each write which species they are into 0x4002 and then ask whether you
            // want it; the `givemon` on the far side of that question reads 0x4002 back.
            // Continuing with a state that had never heard of it made the species nought,
            // and `givemon` of nought hands over nothing — so the run answered yes to the
            // professor and walked out of the lab with an empty party, for every one of
            // the six passes, in every run this project has ever printed.
            //
            // The starter is the only creature in this game a player chooses.
            foreach ((int variable, int value) in run.VariablesWritten) state.Write(variable, value);

            // Yes. The variable the box answers into is the one everything reads.
            state.Write(SpecialContracts.AnswerVariable, 1);

            run = ScriptRunner.Run(rom, carryOn, state, answers: answers, watch: watch);

            foreach ((int variable, int value) in run.VariablesWritten) wrote[variable] = value;

            // The far side of a question is the same scene continuing, and the balls in the
            // lab do their asking before their giving — so a trace that stopped at the
            // question would stop exactly where the interesting part starts.
            touched.AddRange(run.Touched);

            flagsSet.AddRange(run.FlagsSet);
            flagsCleared.AddRange(run.FlagsCleared);
            specials.AddRange(run.SpecialsCalled);
            hides.AddRange(run.Hides);
            asked.AddRange(run.ItemsAsked.Select(a => (a.ItemId, a.Count, a.Carried)));
            Follow(run);

            if (run.StoppedAt is { } unread) stoppedAt.Add(unread);

            gives ??= run.GivesItem;
            if (run.GivesItem is not null) givesCount = run.GivesCount;

            takes ??= run.TakesItem;
            if (run.TakesItem is not null) takesCount = run.TakesCount;

            mon ??= run.GivesMon;

            if (fights is null && run.TrainerId is not null) afterTheFight = run.AfterTheFight;

            fights ??= run.TrainerId;
        }

        // And what this scene left behind for the next one, minus the scratch pads.
        foreach ((int variable, int value) in wrote)
        {
            if (variable >= FirstRemembered) _remembered[variable] = value;
        }

        return new PlayedScript(
            flagsSet,
            flagsCleared,
            [.. gives is { } item && teaches.TryGetValue(item, out int move) ? new[] { move } : []],
            specials,
            mon,
            fights)
        {
            Gets = gives is { } got ? (got, Math.Max(1, givesCount)) : null,
            AfterTheFight = afterTheFight,
            Takes = takes is { } gave ? (gave, Math.Max(1, takesCount)) : null,
            Hides = hides,
            Walked = walked,
            Asked = asked,
            StoppedAtAQuestion = run.Question is not null,
            StoppedAt = stoppedAt,
            Touched = touched,
        };
    }

}
