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

    /// <summary>Reads one track from where its song header said it begins.</summary>
    public static TrackRead Read(Rom rom, int offset)
    {
        var events = new List<SequenceEvent>();
        var returns = new Stack<int>();

        int unknown = 0;
        int at = offset;
        byte running = 0;

        // Where this read has already been. A track that jumps back to a place it has
        // already been is looping, which is what music does — so it is stopped rather than
        // followed, and stopping there is not a failure.
        var seen = new HashSet<int>();

        while (events.Count < MostCommands)
        {
            if (at < 0 || at >= rom.Length) return new TrackRead(offset, events, false, unknown);

            byte opcode = rom.ReadU8(at);

            // Below 0x80 is an argument sitting where a command should be, which means the
            // last command again. A track that begins with one has no last command, and that
            // is a track this reader cannot follow rather than one it should guess at.
            if (opcode < FirstCommand)
            {
                if (running == 0) return new TrackRead(offset, events, false, unknown);

                opcode = running;
            }
            else
            {
                at++;
                running = opcode;
            }

            int start = at;

            switch (opcode)
            {
                case EndOfTrack:
                    events.Add(new SequenceEvent(start, opcode, SequenceCommand.End, []));

                    return new TrackRead(offset, events, true, unknown);

                case GotoOpcode or CallOpcode:
                {
                    if (at + 4 > rom.Length) return new TrackRead(offset, events, false, unknown);

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

                    if (opcode == CallOpcode) returns.Push(at);

                    if (!seen.Add(target)) return new TrackRead(offset, events, true, unknown);

                    at = target;

                    // A jump lands on a command, never on an argument, so the running
                    // command does not survive it.
                    running = 0;

                    break;
                }

                case ReturnOpcode:
                    events.Add(new SequenceEvent(start, opcode, SequenceCommand.Return, []));

                    if (returns.Count == 0) return new TrackRead(offset, events, true, unknown);

                    at = returns.Pop();
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
                        events.Add(new SequenceEvent(
                            start, opcode, SequenceCommand.Setting, Take(rom, ref at, 1)));

                        break;
                    }

                    unknown++;

                    events.Add(new SequenceEvent(start, opcode, SequenceCommand.Unknown, []));

                    break;
            }
        }

        // Out of budget rather than out of track. Reported as not having ended, because it
        // did not.
        return new TrackRead(offset, events, false, unknown);
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
