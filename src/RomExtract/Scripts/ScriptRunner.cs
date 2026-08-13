using PokeMmo.Core.Scripts;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// One thing a scene does, in the order it does it.
/// <para>
/// A list of pages and a list of movements says what happened but not when, and a
/// cutscene is entirely about when: the professor's line lands while he is walking over,
/// not before he sets off and not after he arrives. The order is the content.
/// </para>
/// </summary>
public abstract record SceneBeat
{
    /// <summary>One page of text.</summary>
    public sealed record Say(string Page) : SceneBeat;

    /// <summary>Somebody walks. The steps are the cartridge's own bytes.</summary>
    public sealed record Walk(int PersonId, IReadOnlyList<byte> Steps) : SceneBeat
    {
        public bool IsPlayer => PersonId == MovementList.Player;
    }
}

/// <summary>What running a script actually came to.</summary>
public sealed record ScriptRun
{
    /// <summary>The pages that get said, in the order they get said.</summary>
    public IReadOnlyList<string> Pages { get; init; } = [];

    /// <summary>
    /// Everything the scene does, in order — the same pages, with the movements in
    /// between them where they belong.
    /// <para>
    /// <see cref="Pages"/> is kept beside this rather than derived away from it. Most of
    /// what runs a script wants only the words, and a shopkeeper's one line does not
    /// need a scene player.
    /// </para>
    /// </summary>
    public IReadOnlyList<SceneBeat> Beats { get; init; } = [];

    /// <summary>
    /// The routines this run asked the game for and did not get an answer from.
    /// <para>
    /// A special is a call into the game's own code by number, and this project cannot
    /// follow one. Stepping over it is the only option; recording that it happened is
    /// what stops the difference between "this person has nothing to say" and "this
    /// person asked something we cannot ask" from being invisible.
    /// </para>
    /// <para>
    /// It is not a harmless silence. The answer variable keeps its zero, and zero is an
    /// answer — at 174 branching sites in this cartridge the script reads that zero and
    /// skips what it was about to do.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> SpecialsCalled { get; init; } = [];

    /// <summary>True when this run is a scene rather than a conversation.</summary>
    public bool IsScene => Beats.OfType<SceneBeat.Walk>().Any();

    /// <summary>What a shop opened by this run sells, if it opened one.</summary>
    public IReadOnlyList<int> Stock { get; init; } = [];

    /// <summary>The fight this run picks, if it picks one.</summary>
    public int? TrainerId { get; init; }

    /// <summary>
    /// What this run hands over, if it hands over anything.
    /// <para>
    /// A ball lying on the ground is a script like any other: it puts an item id in one
    /// of the argument variables, a count in the next, and calls a standard routine to
    /// do the giving. This project has never followed a standard routine — the table of
    /// them is code-referenced and has never been located — so all of those people ran
    /// to a clean end and produced nothing at all.
    /// </para>
    /// <para>
    /// Following the routine is not needed. Both numbers are written down in front of
    /// the call, in plain sight, by the script that is about to make it.
    /// </para>
    /// </summary>
    public int? GivesItem { get; init; }

    public int GivesCount { get; init; }

    /// <summary>
    /// The monster this script hands over, if it hands one over.
    /// <para>
    /// Twenty-five of them in the game, and the first is the whole opening: the ball on
    /// the professor's table. The species is sometimes written down and sometimes comes
    /// out of a variable the script set a few commands earlier — the three starters are
    /// one script that reads whichever ball was chosen — so it is resolved here rather
    /// than left as a number that might be a species or might be 0x4002.
    /// </para>
    /// </summary>
    public (int Species, int Level)? GivesMon { get; init; }

    /// <summary>
    /// Where to carry on from once the player has answered, when the run stopped at a
    /// question.
    /// <para>
    /// Standard routine 5 is the yes/no box, derived rather than remembered: of the
    /// game's 219 calls to it, 213 are followed immediately by a compare on 0x800D, and
    /// every other routine with any volume is followed by one exactly never. A routine
    /// whose answer is looked at is a routine that asked something.
    /// </para>
    /// <para>
    /// A run cannot answer it. Everything else here can be decided from the save, but
    /// this needs a person — so the run stops, hands back where it got to, and whoever
    /// has the player carries on from there with 0x800D set. Running past it instead is
    /// what took the "no" arm of every question in the game: 0x800D holds nought, and
    /// nought is no.
    /// </para>
    /// </summary>
    public uint? Question { get; init; }

    /// <summary>
    /// The move that shifts this one out of the way, if it is something in the way.
    /// <para>
    /// Two hundred objects across forty-seven maps, and they announce themselves: the
    /// script's first act is to name a move and ask who in the party knows it. CUT for
    /// the trees, STRENGTH for the boulders, ROCK SMASH for the rubble — three ids, and
    /// nothing else in the game asks this question.
    /// </para>
    /// </summary>
    public int? ShiftedBy { get; init; }

    /// <summary>Flags this run set, in order, and the ones it cleared.</summary>
    public IReadOnlyList<int> FlagsSet { get; init; } = [];

    public IReadOnlyList<int> FlagsCleared { get; init; } = [];

    /// <summary>Variables this run wrote, and what it left in them.</summary>
    public IReadOnlyDictionary<int, int> VariablesWritten { get; init; } =
        new Dictionary<int, int>();

    /// <summary>
    /// The command that stopped the run, or nothing when it ended properly.
    /// <para>
    /// Same instrument as <c>StoppedAt</c>, kept per run because a script can now stop
    /// somewhere it only reaches on one branch — which means the same person can read
    /// perfectly today and stop tomorrow, and the difference is a flag.
    /// </para>
    /// </summary>
    public byte? StoppedAt { get; init; }

    /// <summary>
    /// Where in the image that happened, so the bytes around it can be printed.
    /// <para>
    /// The reader has the same thing and it is not enough here. A run follows jumps,
    /// so where it gives up is almost never inside the script it started in — three
    /// people out of four in FireRed say nothing themselves and call somebody who
    /// does, and printing the bytes at the address on the map shows a handoff.
    /// </para>
    /// </summary>
    public int? StoppedAtOffset { get; init; }

    /// <summary>
    /// Addresses this run was <c>call</c>ed into and could not read as script.
    /// <para>
    /// The naming screen is the one that found this. "Do you want to give a nickname
    /// to BULBASAUR?" answers yes into <c>call 0x081A74EB</c>, and that address is not
    /// script at all — it is ARM code, the same kind of thing a <c>special</c> is, and
    /// no amount of adopting widths will ever decode it. The script that called it
    /// expects to carry on: the <c>goto</c> that leads to the rival taking his own is
    /// the very next command after the call returns.
    /// </para>
    /// <para>
    /// So these are not stops, and they are kept apart from <see cref="StoppedAt"/> for
    /// the reason the mid-scene release diagnostic was eventually deleted: a check that
    /// fires on something normal stops meaning anything. A width we have not adopted
    /// yet and a routine we can never adopt are different findings, and lumping them
    /// together would make the first invisible.
    /// </para>
    /// </summary>
    public IReadOnlyList<uint> CodeCalled { get; init; } = [];

    /// <summary>
    /// Objects this run took off the map, by their number on it.
    /// <para>
    /// Command 0x53, derived from its arguments: 224 sites and every single one holds
    /// either a number between 1 and 10 or a variable — never anything else. Numbers
    /// that small, in that range, on a command that appears where things stop being
    /// there, are object numbers. Its partner 0x55 has 34 sites and every one is a plain
    /// number, which is the right proportion for a game that hides far more than it
    /// reveals.
    /// </para>
    /// <para>
    /// The clincher is a literal: the rival leaves the professor's lab through
    /// <c>0x53 08</c>, and person 8 on that map is the rival.
    /// </para>
    /// <para>
    /// The item balls are not in here and do not need to be. They vanish inside the
    /// standard routine that hands the item over, which is code — and it is why 575
    /// objects in this cartridge carry a flag that takes them off the map and only 7 of
    /// them have a script that sets it.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Hides { get; init; } = [];

    public bool IsEmpty =>
        Pages.Count == 0 && Stock.Count == 0 && TrainerId is null && GivesItem is null &&
        FlagsSet.Count == 0 && FlagsCleared.Count == 0 && VariablesWritten.Count == 0;
}

/// <summary>
/// Runs a script rather than reading it.
/// <para>
/// <see cref="ScriptReader.ReadAll"/> answers "what could this person possibly say",
/// because it follows both arms of every conditional — it has to, since choosing needs
/// the flags of a save. That is the right answer for a dump and the wrong one for a
/// conversation: it is why a trainer greeted you, gloated about losing, and thanked you
/// for the rematch, all in one breath, before anybody had fought anything.
/// </para>
/// <para>
/// This one takes the save's flags and walks a single path. Jumps are taken or not
/// taken; the arm that does not run is not read. What comes back is a transcript.
/// </para>
/// <para>
/// The state it is given is copied, not written through. A run has to be repeatable —
/// the client runs one to find out whether to open a box at all — and a run that set
/// flags as it went would only be right the first time.
/// </para>
/// </summary>
public static class ScriptRunner
{
    /// <summary>
    /// Commands executed before giving up.
    /// <para>
    /// Higher than the reader's limit and for a different reason. The reader walks
    /// forwards and stops; this follows jumps, and a script that loops back on itself is
    /// not an error — it is how a "which one do you want?" prompt waits for an answer
    /// this has no way to give.
    /// </para>
    /// </summary>
    private const int MaxCommands = 4096;

    /// <summary>
    /// The standard routine that asks a yes-or-no question.
    /// <para>
    /// Derived, not remembered. Of this game's 219 calls to routine 5, 213 are followed
    /// immediately by a compare on 0x800D — and of the 1967 calls to routine 4, the 667
    /// to routine 6 and the 303 to routine 2, exactly none are. A routine whose answer
    /// somebody looks at is a routine that asked something.
    /// </para>
    /// </summary>
    private const byte Question = 5;

    public static ScriptRun Run(Rom rom, uint address, ScriptState? state = null, int maxPages = 32)
    {
        ScriptState save = (state ?? new ScriptState()).Copy();

        var pages = new List<string>();
        var beats = new List<SceneBeat>();
        var specials = new List<int>();
        var stock = new List<int>();
        var set = new List<int>();
        var cleared = new List<int>();
        var written = new Dictionary<int, int>();
        var stack = new Stack<uint>();
        var codeCalled = new List<uint>();

        // What a script has put where its dialogue leaves a gap. Two are ever written
        // to in this cartridge and only the first by anything read so far; the array is
        // sized for the codes the text actually uses.
        var buffers = new string?[4];
        var hides = new List<int>();

        int? trainerId = null;
        int? gives = null;
        int givesCount = 0;
        (int Species, int Level)? givesMon = null;
        int? shifts = null;
        uint? question = null;
        byte? stoppedAt = null;
        int? stoppedAtOffset = null;

        Comparison result = Comparison.Equal;
        uint pending = 0;

        if (rom.ToOffsetOrNull(address) is not { } offset) return new ScriptRun();

        for (int executed = 0; executed < MaxCommands; executed++)
        {
            if (offset >= rom.Length) break;

            byte code = rom.ReadU8(offset);
            byte first = offset + 1 < rom.Length ? rom.ReadU8(offset + 1) : (byte)0;

            if (ScriptCommands.ArgumentLength(code, first) is not { } length)
            {
                // Inside a call, an unreadable byte is not the end of the story. The
                // cartridge calls out to its own code all the time — the naming screen,
                // the trade screen, the slot machines — and every one of those calls has
                // a return address sitting on this stack because the script means to
                // carry on afterwards. Reading them is impossible; returning from them
                // is exactly right, and it is what the console does.
                //
                // Only inside a call. A run that derails with nothing on the stack has
                // genuinely stopped, and saying otherwise would quietly hide every width
                // still missing — the one thing this reader must never do.
                if (stack.Count > 0 && rom.ToOffsetOrNull(stack.Pop()) is { } back)
                {
                    codeCalled.Add(Rom.BaseAddress + (uint)offset);
                    offset = back;
                    continue;
                }

                stoppedAt = code;
                stoppedAtOffset = offset;
                break;
            }

            if (offset + 1 + length > rom.Length) break;

            var command = new ScriptCommand(offset, code, rom.Slice(offset + 1, length).ToArray());

            offset += 1 + length;

            // Where the next command comes from, when it is not simply the next one.
            uint jump = 0;
            bool push = false;
            bool stop = false;

            switch (code)
            {
                case ScriptCommands.End:
                case 0x0D:                              // killscript
                    stop = true;
                    break;

                case ScriptCommands.Return:
                    if (stack.Count == 0) stop = true;
                    else jump = stack.Pop();
                    break;

                case ScriptCommands.Goto:
                    jump = command.Pointer();
                    break;

                case ScriptCommands.Call:
                    jump = command.Pointer();
                    push = true;
                    break;

                case ScriptCommands.GotoIf:
                    if (ScriptState.Accepts(command.Arguments[0], result)) jump = command.Pointer(1);
                    break;

                case ScriptCommands.CallIf:
                    if (ScriptState.Accepts(command.Arguments[0], result))
                    {
                        jump = command.Pointer(1);
                        push = true;
                    }

                    break;

                case 0x2B:                              // checkflag
                    // A flag is a number that is one or nothing, compared against one.
                    // Set reads as equal and clear as less, which is what makes the
                    // commonest pair in the whole cartridge — checkflag then "goto if
                    // less" — mean "if they have not done this yet".
                    result = ScriptState.Compare(save.Has(command.Word()) ? 1 : 0, 1);
                    break;

                case 0x29:                              // setflag
                    if (save.Set(command.Word())) set.Add(command.Word());
                    break;

                case 0x2A:                              // clearflag
                    if (save.Clear(command.Word())) cleared.Add(command.Word());
                    break;

                case 0x21:                              // compare
                    result = ScriptState.Compare(save.Read(command.Word()), command.Word(2));
                    break;

                case 0x22:                              // comparevars
                    result = ScriptState.Compare(save.Read(command.Word()), save.Read(command.Word(2)));
                    break;

                case 0x16:                              // setvar
                    save.Write(command.Word(), command.Word(2));
                    written[command.Word()] = command.Word(2);
                    break;

                case 0x1A:                              // copyvarifnotzero
                    // The argument slots a standard routine reads from. Written here
                    // rather than treated as ordinary variables because that is what
                    // they are: 0x8000 and 0x8001 are how a script passes two numbers
                    // to a routine, and an item on the ground is exactly two numbers.
                    save.Write(command.Word(), command.Word(2));
                    break;

                case 0x17:                              // addvar
                    save.Write(command.Word(), save.Read(command.Word()) + command.Word(2));
                    written[command.Word()] = save.Read(command.Word());
                    break;

                case 0x18:                              // subvar
                    save.Write(command.Word(), save.Read(command.Word()) - command.Word(2));
                    written[command.Word()] = save.Read(command.Word());
                    break;

                case ScriptCommands.LoadPointer:
                    pending = command.Pointer(1);
                    break;

                case ScriptCommands.Message:
                    Say(rom, command.Pointer(), pages, beats, maxPages, save, buffers);
                    pending = 0;
                    break;

                case 0xA0:
                    // Which of the two sets of words this character reads. Named from the
                    // cartridge rather than recalled: the fork after this command is
                    // "Waiter"/"Waitress", "little brother"/"little sister", "All boys
                    // leave home someday"/"All girls dream of traveling" — seven scripts
                    // on six maps, and the zero arm says "boy" at every one of them.
                    save.Write(0x800D, save.IsGirl ? 1 : 0);
                    break;

                case SpecialCalls.Special:
                case SpecialCalls.SpecialVar:
                    // Stepped over, because it is a call into code on the cartridge that
                    // this cannot execute. Recorded, because the alternative is a script
                    // that quietly does less and looks like a script that does less.
                    specials.Add(code == SpecialCalls.Special ? command.Word() : command.Word(2));
                    break;

                case MovementLists.ApplyMovement:
                    // Whose movement and which list. Both are written down in front of
                    // the command; what the individual step bytes mean was derived by
                    // walking them across every map and asking who ends up inside a wall.
                    if (MovementLists.Read(rom, command.Pointer(2)) is { Length: > 0 } steps)
                        beats.Add(new SceneBeat.Walk(command.Word(), steps));

                    break;

                case ScriptCommands.CallStandard when save.Read(0x8000) is not 0 && pending == 0:
                case 0x08 when save.Read(0x8000) is not 0 && pending == 0:
                    // Something is being handed over. Which routine does the handing is
                    // a number this project cannot resolve, and does not need to: the
                    // item and the count were written down immediately before the call.
                    gives ??= save.Read(0x8000);
                    givesCount = Math.Max(1, save.Read(0x8001));

                    save.Write(0x8000, 0);
                    save.Write(0x8001, 0);

                    // Same reason giveitem does it. A routine that hands something over
                    // answers into the result variable, and a script that then asks and
                    // is told nothing reads its own failure line.
                    save.Write(0x800D, 1);

                    break;

                case ScriptCommands.CallStandard:
                case 0x08:                              // gotostd
                    // The standard routines are what actually put a loaded pointer on
                    // the screen. Which number does which differs between games; what
                    // does not differ is that the text was loaded first, so the loaded
                    // pointer is the thing to say and the routine number is not read.
                    if (pending != 0)
                    {
                        Say(rom, pending, pages, beats, maxPages, save, buffers);
                        pending = 0;
                    }

                    // Except for one of them. Routine 5 asks, and a run cannot answer —
                    // so it stops here and says where to carry on from.
                    if (command.Arguments.Length > 0 && command.Arguments[0] == Question)
                    {
                        question = Rom.BaseAddress + (uint)(command.Offset + 1 + command.Arguments.Length);
                        stop = true;
                    }

                    break;

                case 0x44:                              // hands an item over, as 0x46 does
                case 0x46:                              // giveitem
                    // The command itself, now that its width is known. What follows it
                    // is always `compare 0x800D, 0` and a branch, and the arm that
                    // branch takes when the variable is zero says "Too bad! The BAG is
                    // full..." — so zero is the failure and this has to say otherwise.
                    //
                    // Leaving it unwritten is not neutral. Every script that asks
                    // whether something worked was hearing no, and four people in this
                    // game were reported as saying the bag-full line as though it were
                    // their only one.
                    // Two commands, not one, and the second was found by walking into
                    // the Viridian shop and being handed nothing. Both carry a word and
                    // a word, and both are followed within a few commands by their own
                    // first word being written into 0x8000 for the "obtained" fanfare:
                    // 39 of 0x44's 42 sites and 27 of 0x46's 32. Whatever separates
                    // them, it is not whether they hand something over — and 0x44 is the
                    // commoner of the two, so ignoring it lost forty-two handovers
                    // including the parcel the whole story turns on.
                    //
                    // Item zero is not an item. A script that reaches this with nothing
                    // loaded is doing something else with the command, and reporting a
                    // handover of nothing would put a person who says "Mew!" on the list
                    // of people who give you things.
                    if (command.Word() != 0)
                    {
                        gives ??= command.Word();
                        givesCount = Math.Max(1, command.Word(2));
                    }

                    save.Write(0x800D, 1);
                    break;

                case 0x79:                              // gives a monster
                    // The species is a number or a variable holding one. Both turn up:
                    // Lapras and Eevee are written into the script, and the starter is
                    // whichever of the three balls was pressed, which the same script
                    // read into 0x4002 four commands earlier.
                    {
                        int named = command.Word();
                        int species = named >= 0x4000 ? save.Read(named) : named;

                        if (species > 0) givesMon ??= (species, Math.Max(1, command.Word(2)));
                    }

                    break;

                case 0x53:                              // takes an object off the map
                    // A number or a variable holding one, exactly as givemon's species
                    // is. The bound is the object list's own: a map's people are
                    // numbered from one and the largest in this cartridge is well inside
                    // this, so a variable that happens to hold a large number is a
                    // variable this run has not understood rather than a person.
                    {
                        int named = command.Word();
                        int who = named >= 0x4000 ? save.Read(named) : named;

                        if (who is > 0 and < 64 && !hides.Contains(who)) hides.Add(who);
                    }

                    break;

                case 0x7D:                              // names a species for the text
                    // Adopted at width 3 last time on the evidence that it sits between
                    // a handover and a text box at every gift site — "the game about to
                    // say which one you got". This is that sentence finished: the first
                    // argument picks which gap in the dialogue to fill and the word
                    // after it is a species, or a variable holding one, exactly as
                    // givemon's is.
                    //
                    // The pairing is off by two, and the cartridge says so rather than
                    // any table: the ball script writes buffer 0 and the very next thing
                    // it says is "Do you want to give a nickname to this {FD}{02}?".
                    {
                        int which = command.Arguments[0] + 2;
                        int named = command.Word(1);
                        int species = named >= 0x4000 ? save.Read(named) : named;

                        if (which >= 0 && which < buffers.Length && species > 0)
                            buffers[which] = save.NameOfSpecies?.Invoke(species);
                    }

                    break;

                case 0x7C:                              // findmove
                    // The command every cut tree, boulder and heap of rubble opens with.
                    // It names a move and answers with the party slot that knows it, or
                    // six for nobody, and the next two commands are always `compare
                    // 0x800D, 6` and a branch.
                    //
                    // Left unwritten this reads as slot zero — "the first one in your
                    // party can do it" — for every party, including an empty one. Every
                    // obstacle in the game would offer to move itself.
                    shifts ??= command.Word();

                    save.Write(0x800D, save.SlotKnowing(command.Word()));
                    break;

                case ScriptCommands.PokeMart:
                    stock.AddRange(Mart(rom, command.Pointer()));
                    break;

                case ScriptCommands.TrainerBattle:
                    // The command is its own conditional, and this is the whole reason a
                    // beaten trainer used to read their opening line. Having beaten them
                    // does not skip a branch — it makes the fight itself do nothing, and
                    // the script carries straight on to whatever they say afterwards.
                    // Confirmed on a real image: all fifteen people on Route 8 have a
                    // different second line, and it is the line they say once beaten.
                    //
                    // Which trainer, and not which flag. The word after the id is not a
                    // flag number — it is zero for every one of those fifteen — so the
                    // games remember a beaten trainer somewhere the script does not say.
                    int id = command.Word(1);

                    if (save.HasBeaten(id)) break;

                    trainerId = id;

                    // Every variant but one opens with the line they say on sight, and
                    // that line belongs to the fight rather than to what comes after it.
                    if (command.Arguments[0] != 3)
                        Say(rom, command.Pointer(5), pages, beats, maxPages, save, buffers);

                    stop = true;
                    break;
            }

            if (stop) break;

            if (jump != 0)
            {
                if (rom.ToOffsetOrNull(jump) is not { } destination) break;

                if (push) stack.Push((uint)(Rom.BaseAddress + offset));

                offset = destination;
            }
        }

        return new ScriptRun
        {
            Pages = pages,
            Beats = beats,
            SpecialsCalled = specials,
            CodeCalled = codeCalled,
            Hides = hides,
            Stock = stock,
            TrainerId = trainerId,
            GivesItem = gives,
            GivesCount = givesCount,
            GivesMon = givesMon,
            ShiftedBy = shifts,
            Question = question,
            FlagsSet = set,
            FlagsCleared = cleared,
            VariablesWritten = written,
            StoppedAt = stoppedAt,
            StoppedAtOffset = stoppedAtOffset,
        };
    }

    private static void Say(
        Rom rom, uint address, List<string> pages, List<SceneBeat> beats, int maxPages,
        ScriptState save, string?[] buffers)
    {
        if (address == 0) return;
        if (rom.ToOffsetOrNull(address) is not { } at) return;

        ReadOnlySpan<byte> bytes = rom.Span[at..];

        if (!GameText.LooksLikeDialogue(bytes)) return;

        foreach (string raw in GameText.DecodeDialogue(bytes))
        {
            if (pages.Count >= maxPages) return;

            string page = Fill(raw, save, buffers);

            pages.Add(page);
            beats.Add(new SceneBeat.Say(page));
        }
    }

    /// <summary>
    /// Puts the player, the rival and whatever a script has named into the gaps the
    /// cartridge's dialogue leaves.
    /// <para>
    /// Four codes, all derived by counting sites and reading sentences rather than by
    /// remembering a table: 0x01 is the player at 109 sites, 0x06 is the rival at 33,
    /// and 0x02 and 0x03 are species at 19 each. Only 0x02 is ever filled by anything
    /// this project has read — 0x03 belongs to the in-game trades, which are a special
    /// routine and so out of reach.
    /// </para>
    /// <para>
    /// A code with nothing behind it is left exactly as it was found. Substituting an
    /// empty string there would turn "Want to trade it for my {FD}{03}?" into "Want to
    /// trade it for my ?" — a sentence that looks like the cartridge's own and is not,
    /// which is the one failure this whole project is arranged against.
    /// </para>
    /// </summary>
    private static string Fill(string page, ScriptState save, string?[] buffers)
    {
        if (!page.Contains("{FD}", StringComparison.Ordinal)) return page;

        page = Replace(page, 0x01, save.PlayerName);
        page = Replace(page, 0x06, save.RivalName);

        for (int i = 0; i < buffers.Length; i++) page = Replace(page, i, buffers[i]);

        return page;

        static string Replace(string text, int code, string? with) =>
            string.IsNullOrEmpty(with) ? text : text.Replace($"{{FD}}{{{code:X2}}}", with, StringComparison.Ordinal);
    }

    private static List<int> Mart(Rom rom, uint address, int maxItems = 64)
    {
        var stock = new List<int>();

        if (rom.ToOffsetOrNull(address) is not { } list) return stock;

        for (int i = 0; i < maxItems; i++)
        {
            int at = list + i * 2;
            if (at + 2 > rom.Length) break;

            int itemId = rom.ReadU16(at);
            if (itemId == 0) break;

            stock.Add(itemId);
        }

        return stock;
    }
}
