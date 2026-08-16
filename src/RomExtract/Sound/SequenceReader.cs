using PokeMmo.Core.Sound;

namespace PokeMmo.RomExtract.Sound;

/// <summary>
/// One track, read.
/// <para>
/// <see cref="EndedProperly"/> is the field that matters. A track that ran to an end command
/// is a track this reader understood; one that stopped because it hit the edge of the file
/// or ran out of budget is a finding, and the difference is never smoothed over.
/// </para>
/// </summary>
public sealed record TrackRead(
    int Offset,
    IReadOnlyList<SequenceEvent> Events,
    bool EndedProperly,
    int Unknown)
{
    public int Notes => Events.Count(e => e.Command == SequenceCommand.NoteOn);

    /// <summary>
    /// The byte this read was looking at when it stopped, when it stopped badly.
    /// <para>
    /// A track that does not end is dropped, and a song all of whose tracks are dropped does
    /// not come back at all. On a real cartridge that is 142 songs out of 255, every one of
    /// them for this reason — and "the reader could not follow it" names no byte, no offset
    /// and nothing to fix. This does.
    /// </para>
    /// <para>
    /// The likeliest cause is an argument width: a command whose arguments this reader counts
    /// wrongly leaves the read one or two bytes out, and everything after it is nonsense. The
    /// byte it dies on is the evidence for which command that is.
    /// </para>
    /// </summary>
    public byte StoppedOn { get; init; }

    /// <summary>Where it stopped, so the bytes either side can be looked at.</summary>
    public int StoppedAt { get; init; } = -1;

    /// <summary>The last command it read before it stopped, which is often the culprit.</summary>
    public byte After { get; init; }

    /// <summary>
    /// True when this read stopped at a jump backwards into music it had already followed,
    /// which is a track that repeats rather than a track that finishes.
    /// <para>
    /// <see cref="EndedProperly"/> cannot tell those apart and should not try: both are reads
    /// this reader followed to somewhere it understands, and both are safe to perform. But
    /// they are different things to <em>be</em>, and only one of them is a track that stops.
    /// A song whose tracks neither run to an end command nor loop is a song being cut short
    /// somewhere, and until now there was no number that said so.
    /// </para>
    /// </summary>
    public bool Loops { get; init; }

    /// <summary>
    /// How many subsections were expanded into this track.
    /// <para>
    /// A phrase played four times is one subsection called four times, and this reader
    /// flattens each call where it is made. So this is the difference between how long a
    /// track is written and how long it is to perform, and it is the number that says whether
    /// the budget is being spent on repeats.
    /// </para>
    /// </summary>
    public int Calls { get; init; }
}

/// <summary>
/// Reading the byte language a track is written in.
/// <para>
/// This is the only part of the sound format that is not four-byte aligned and the only part
/// with control flow in it. The command bytes divide cleanly: below 0x80 is an argument or a
/// repeat of the last command, 0x80 to 0xB0 is time passing, 0xB1 to 0xCF are the structural
/// commands and the settings, and 0xD0 upwards is a note.
/// </para>
/// <para>
/// <b>Which command a bare byte repeats.</b> Only a command that has something after it can
/// be the running one. A wait takes nothing, so a byte below 0x80 after a wait repeats
/// whatever came before the wait — almost always a note. Reading it as a repeated wait
/// consumes no bytes at all and the read stays where it is for ever, which is what every
/// failing song on a real cartridge was doing: twenty thousand commands at one address.
/// </para>
/// <para>
/// <b>The one genuine ambiguity, named rather than hidden.</b> A note takes a key, then
/// optionally a loudness, then optionally a length — all of them bytes below 0x80, which is
/// also what a repeat of the previous command looks like. There is no marker separating "a
/// third argument to this note" from "another note using the running command". Every reader
/// of this format resolves it the same way, by taking up to three arguments greedily, and
/// that resolution is <b>modelled</b> rather than read: it is a decision about an ambiguity
/// in the data, and it can be wrong. Where it is wrong it produces a track with too few
/// notes rather than a crash, which is the failure direction to prefer and the reason the
/// note count is reported.
/// </para>
/// </summary>
public static class SequenceReader
{
    /// <summary>Nothing before this is a command.</summary>
    private const byte FirstCommand = 0x80;

    private const byte LastWait = 0xB0;
    private const byte EndOfTrack = 0xB1;
    private const byte GotoOpcode = 0xB2;
    private const byte CallOpcode = 0xB3;
    private const byte ReturnOpcode = 0xB4;
    private const byte NoteOffOpcode = 0xCE;
    private const byte FirstNote = 0xCF;

    /// <summary>The most arguments a note takes: key, loudness, length.</summary>
    private const int MostNoteArguments = 3;

    /// <summary>
    /// How many commands one track may be before this reader gives up. <b>Modelled.</b>
    /// <para>
    /// A track is allowed to jump backwards, which means a track is allowed to loop for ever
    /// — and looping for ever is what a piece of music does. Following the jumps without a
    /// budget would not be a bug in the data, it would be the data working as intended, so
    /// the budget is a property of the reader rather than a rejection of the track.
    /// </para>
    /// </summary>
    public const int MostCommands = 20_000;

    /// <summary>
    /// The single-argument settings. Everything from 0xB5 to 0xCD that is not one of the
    /// structural commands takes exactly one byte after it.
    /// </summary>
    private static bool IsSetting(byte opcode) => opcode is >= 0xB5 and <= 0xCD;

    /// <summary>
    /// Whether a command has anything after it, which is what decides whether it can be the
    /// running command.
    /// <para>
    /// The settings, the note-off and the notes do; the waits and the four structural
    /// commands do not. A command with nothing after it cannot be repeated by a bare
    /// argument byte, because there is no argument for that byte to be.
    /// </para>
    /// </summary>
    private static bool TakesOperands(byte opcode) => opcode >= FirstSetting;

    /// <summary>Where the commands that carry something after them begin.</summary>
    private const byte FirstSetting = 0xB5;

    /// <summary>
    /// A read that did not reach an end, with what it was looking at when it gave up.
    /// <para>
    /// The byte and the offset are what turn "the reader could not follow it" into something
    /// somebody can act on. A command whose argument count this reader has wrong leaves the
    /// read a byte or two out and everything after it is nonsense — so the byte it dies on,
    /// counted across a whole cartridge, names the command that is wrong.
    /// </para>
    /// </summary>
    private static TrackRead Stopped(
        int offset, List<SequenceEvent> events, int unknown, byte on, int at, byte after) =>
        new(offset, events, false, unknown) { StoppedOn = on, StoppedAt = at, After = after };

    /// <summary>Reads one track from where its song header said it begins.</summary>
    /// <param name="widths">
    /// How many bytes named commands take, where that is known. A command not in here takes
    /// arguments greedily — up to one byte, stopping at anything that is itself a command.
    /// <para>
    /// This exists because the greedy rule is self-correcting for a command that takes more
    /// than one argument and <b>is not</b> for one whose arguments can look like commands. A
    /// four-byte address has bytes above 0x80 in it, so a command taking one that this reader
    /// reads as taking one byte walks into the middle of the address and never recovers —
    /// and because almost no byte in this encoding is invalid, it does not fail either. It
    /// runs until the budget stops it, which is what 649 tracks on a real cartridge do.
    /// </para>
    /// <para>
    /// Which commands those are is not guessed at here. It is measured: see the sweep in
    /// romdump, which tries each width against a whole cartridge and counts how many tracks
    /// reach an end.
    /// </para>
    /// </param>
    /// <param name="budget">How many commands to follow before giving up.</param>
    public static TrackRead Read(
        Rom rom,
        int offset,
        IReadOnlyDictionary<byte, int>? widths = null,
        int budget = MostCommands)
    {
        var events = new List<SequenceEvent>();

        // Where to carry on after a call, and which subsection that call went into. The
        // second half is what the recursion guard below pops.
        var returns = new Stack<(int At, int Target)>();

        int unknown = 0;
        int calls = 0;
        int at = offset;
        byte running = 0;

        // Every place this read has already read a command. A jump *backwards* into music
        // already followed is the loop point, and following it would mean reading the same
        // bytes for ever.
        //
        // This used to be a set of jump targets, and the track's own beginning was never in
        // it — so a goto back to the top was followed once and the entire track was read a
        // second time before the check caught it. Four notes came back as eight, and the
        // budget for a long track was spent twice as fast as it should be.
        var read = new HashSet<int>();

        // Which subsections are currently being expanded — the call chain, not the places
        // that have ever been called.
        //
        // These two sets were one set, and that is the bug this whole comment exists for. A
        // call comes back and a goto does not, so a subsection called twice — which is how
        // this format writes a repeated bar, and therefore how most music on the cartridge is
        // written — looked exactly like a loop. The read stopped dead at the second call and
        // reported that it had ended properly, with a call as its last command. Everything
        // after it, including the goto that would have made the track repeat, was never read,
        // and the performer walked off the end of the list and called the track finished.
        //
        // What genuinely cannot be followed is a subsection that calls itself, directly or
        // round a chain, and that is what this set is for.
        var inside = new HashSet<int>();

        while (events.Count < Math.Max(1, budget))
        {
            if (at < 0 || at >= rom.Length)
                return Stopped(offset, events, unknown, 0, at, running) with { Calls = calls };

            byte opcode = rom.ReadU8(at);

            // Where this command begins, taken before the opcode byte is stepped over. It
            // has to be the command's own position rather than its arguments', because a
            // jump names the place it lands on and the only way to find what is there is to
            // compare the two — an event recorded one byte along would match nothing.
            int start = at;

            read.Add(start);

            // Below 0x80 is an argument sitting where a command should be, which means the
            // last command again. A track that begins with one has no last command, and that
            // is a track this reader cannot follow rather than one it should guess at.
            if (opcode < FirstCommand)
            {
                if (running == 0)
                    return Stopped(offset, events, unknown, opcode, start, running) with { Calls = calls };

                opcode = running;
            }
            else
            {
                at++;

                // A command that takes no operands does not become the running command.
                //
                // This is read off a cartridge, and it is the single rule that had every
                // failing song failing. A wait takes nothing after it, so repeating a wait
                // consumes no bytes at all — and a read that made a wait the running command
                // sat on one offset for twenty thousand commands and then gave up. It is
                // also what the bytes plainly say: after a note and a wait, a bare key byte
                // is another note, and reading it as a repeated wait turns a bar of music
                // into an infinite loop.
                if (TakesOperands(opcode)) running = opcode;
            }

            // Nothing below may leave the read where it found it. With the rule above there
            // is no way for that to happen, which is exactly why it is worth saying: the last
            // thing that could spin for ever here did so silently for four rounds of looking.
            int wasAt = at;

            switch (opcode)
            {
                case EndOfTrack:
                    events.Add(new SequenceEvent(start, opcode, SequenceCommand.End, []));

                    return new TrackRead(offset, events, true, unknown) { Calls = calls };

                case GotoOpcode or CallOpcode:
                {
                    if (at + 4 > rom.Length)
                        return Stopped(offset, events, unknown, opcode, start, running) with { Calls = calls };

                    uint address = rom.ReadU32(at);

                    at += 4;

                    if (rom.ToOffsetOrNull(address) is not { } target)
                    {
                        unknown++;

                        events.Add(new SequenceEvent(start, opcode, SequenceCommand.Unknown, []));

                        break;
                    }

                    events.Add(new SequenceEvent(
                        start,
                        opcode,
                        opcode == GotoOpcode ? SequenceCommand.Goto : SequenceCommand.Call,
                        [],
                        target));

                    if (opcode == GotoOpcode)
                    {
                        // Backwards into music already followed: the loop point, and the end
                        // of what there is to read. The jump itself is kept, and the
                        // performer resolves it against the offsets every command carries —
                        // which is what makes a song repeat from where it was written to
                        // repeat rather than from its own first bar.
                        if (read.Contains(target))
                            return new TrackRead(offset, events, true, unknown)
                            {
                                Loops = true, Calls = calls,
                            };

                        at = target;

                        // A jump lands on a command, never on an argument, so the running
                        // command does not survive it.
                        running = 0;

                        break;
                    }

                    // A subsection already being expanded further up this same chain. That
                    // is recursion rather than repetition, it has no bottom, and it is a read
                    // this reader cannot follow rather than one it should guess at.
                    if (!inside.Add(target))
                        return Stopped(offset, events, unknown, opcode, start, running)
                            with { Calls = calls };

                    calls++;

                    returns.Push((at, target));

                    at = target;
                    running = 0;

                    break;
                }

                case ReturnOpcode:
                    events.Add(new SequenceEvent(start, opcode, SequenceCommand.Return, []));

                    // A return with nothing to return to. This used to count as ending
                    // properly, which put a track in front of the performer whose last
                    // command does nothing — so it ran off the end of the list and reported
                    // itself as a track that had run out. The same wrong answer as the
                    // repeated call, arrived at from the other side.
                    if (returns.Count == 0)
                        return Stopped(offset, events, unknown, opcode, start, running)
                            with { Calls = calls };

                    (at, int came) = returns.Pop();

                    inside.Remove(came);

                    running = 0;

                    break;

                case NoteOffOpcode:
                    events.Add(new SequenceEvent(
                        start, opcode, SequenceCommand.NoteOff, Take(rom, ref at, 1)));

                    break;

                case >= FirstNote:
                    events.Add(new SequenceEvent(
                        start, opcode, SequenceCommand.NoteOn, Take(rom, ref at, MostNoteArguments)));

                    break;

                case >= FirstCommand and <= LastWait:
                    events.Add(new SequenceEvent(start, opcode, SequenceCommand.Wait, []));

                    break;

                default:
                    if (IsSetting(opcode))
                    {
                        // A stated width is taken whatever the bytes look like; without one,
                        // greedily and never past something that is itself a command.
                        IReadOnlyList<byte> arguments =
                            widths is not null && widths.TryGetValue(opcode, out int wide)
                                ? Exactly(rom, ref at, wide)
                                : Take(rom, ref at, 1);

                        events.Add(new SequenceEvent(start, opcode, SequenceCommand.Setting, arguments));

                        break;
                    }

                    unknown++;

                    events.Add(new SequenceEvent(start, opcode, SequenceCommand.Unknown, []));

                    break;
            }
        }

        // Out of budget rather than out of track. Reported as not having ended, because it
        // did not — and distinguishable from every other failure by the offset, which is the
        // one place the read stopped somewhere it was still making sense.
        return new TrackRead(offset, events, false, unknown) { StoppedAt = -1, Calls = calls };
    }

    /// <summary>
    /// Exactly this many argument bytes, whatever they look like.
    /// <para>
    /// The counterpart of <see cref="Take"/>, and the whole point of it: a command carrying a
    /// four-byte address has bytes above 0x80 in it, and stopping at those is what derails
    /// the read.
    /// </para>
    /// </summary>
    private static IReadOnlyList<byte> Exactly(Rom rom, ref int at, int count)
    {
        var taken = new List<byte>(Math.Max(0, count));

        for (int i = 0; i < count && at < rom.Length; i++) taken.Add(rom.ReadU8(at++));

        return taken;
    }

    /// <summary>
    /// Up to this many argument bytes, stopping at the first thing that is a command.
    /// <para>
    /// This is where the ambiguity described at the top of the class lives. Greedy, and
    /// modelled.
    /// </para>
    /// </summary>
    private static IReadOnlyList<byte> Take(Rom rom, ref int at, int most)
    {
        var arguments = new List<byte>(most);

        while (arguments.Count < most && at < rom.Length && rom.ReadU8(at) < FirstCommand)
        {
            arguments.Add(rom.ReadU8(at));
            at++;
        }

        return arguments;
    }

    /// <summary>
    /// Every track of every song the walk found, and what the reading came to.
    /// <para>
    /// The report is the point. A parser that only says what it managed is a parser nobody
    /// can tell is failing, and this project counts what it stepped over everywhere else it
    /// reads something it does not fully understand.
    /// </para>
    /// </summary>
    public static IReadOnlyList<TrackRead> AllTracks(
        Rom rom, SoundTreeResult tree, Action<string>? log = null)
    {
        var read = new List<TrackRead>();

        foreach (SongHeaderRecord song in tree.Songs)
        {
            foreach (int track in song.TrackOffsets) read.Add(Read(rom, track));
        }

        int ended = read.Count(t => t.EndedProperly);

        log?.Invoke($"  {read.Count} tracks read, {ended} of which ran to an end");

        log?.Invoke(
            $"    {read.Sum(t => t.Notes)} notes, " +
            $"{read.Sum(t => t.Unknown)} bytes this reader does not account for");

        if (ended < read.Count)
            log?.Invoke($"    {read.Count - ended} stopped without ending, which is a finding rather than a track");

        return read;
    }
}
