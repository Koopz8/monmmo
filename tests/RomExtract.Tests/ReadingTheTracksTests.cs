using PokeMmo.Core.Sound;
using PokeMmo.RomExtract.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The byte language a track is written in.
/// <para>
/// It is the only part of this format that is not four-byte aligned and the only part with
/// control flow in it, which makes it the only part that can fail to terminate. So the thing
/// these tests care most about is not what the reader parses but what it says when it cannot:
/// a track that ran to an end and a track that stopped because the file did are two different
/// answers, and a reader that returns the same shape for both is a reader nobody can tell is
/// failing.
/// </para>
/// </summary>
public class ReadingTheTracksTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static TrackRead Read(int offset) => SequenceReader.Read(Synthetic.ToRom(), offset);

    private static TrackRead FirstOrdinaryTrack() => Read(SyntheticRom.SequencesOffset);

    /// <summary>An ordinary track runs to its end command.</summary>
    [Fact]
    public void AnOrdinaryTrackRunsToItsEnd()
    {
        TrackRead track = FirstOrdinaryTrack();

        Assert.True(track.EndedProperly);
        Assert.Equal(SequenceCommand.End, track.Events[^1].Command);
    }

    /// <summary>
    /// And every note in it was found, which is the count the greedy-argument decision could
    /// quietly cost.
    /// </summary>
    [Fact]
    public void AndEveryNoteInItWasFound() =>
        Assert.Equal(SyntheticRom.NotesPerTrack, FirstOrdinaryTrack().Notes);

    /// <summary>
    /// And the settings before the notes came out as settings with their argument, rather
    /// than as notes or as bytes nobody accounted for.
    /// </summary>
    [Fact]
    public void AndTheSettingsCameOutAsSettings()
    {
        TrackRead track = FirstOrdinaryTrack();

        List<SequenceEvent> settings = [.. track.Events.Where(e => e.Command == SequenceCommand.Setting)];

        Assert.Equal(4, settings.Count);
        Assert.All(settings, s => Assert.Single(s.Arguments));

        // Tempo is the first of them, and it carries the number that was written.
        Assert.Equal(0xBB, settings[0].Opcode);
        Assert.Equal(75, settings[0].Arguments[0]);
    }

    /// <summary>Nothing in an ordinary track is a byte the reader cannot account for.</summary>
    [Fact]
    public void AndNothingInItWentUnaccountedFor()
    {
        TrackRead track = FirstOrdinaryTrack();

        Assert.Equal(0, track.Unknown);
        Assert.DoesNotContain(track.Events, e => e.Command == SequenceCommand.Unknown);
    }

    /// <summary>
    /// A call goes somewhere else and a return comes back, and the track ends after the
    /// coming back rather than inside the subsection.
    /// </summary>
    [Fact]
    public void ACallGoesSomewhereElseAndComesBack()
    {
        TrackRead track = Read(SyntheticRom.CallingTrackOffset);

        Assert.True(track.EndedProperly);

        SequenceEvent call = track.Events.Single(e => e.Command == SequenceCommand.Call);

        Assert.Equal(SyntheticRom.CalledSubsectionOffset, call.Target);

        Assert.Contains(track.Events, e => e.Command == SequenceCommand.Return);

        // The note is inside the subsection, so finding it proves the call was followed
        // rather than stepped over.
        Assert.Equal(1, track.Notes);

        // And the end is the track's own, which proves the return came back.
        Assert.Equal(SequenceCommand.End, track.Events[^1].Command);
    }

    /// <summary>
    /// A track that jumps back to itself is a piece of music that loops, and the reader
    /// stops rather than following it for ever — and calls that an ending, because it is.
    /// </summary>
    [Fact]
    public void ATrackThatLoopsIsStoppedRatherThanFollowedForEver()
    {
        TrackRead track = Read(SyntheticRom.LoopingTrackOffset);

        Assert.True(track.EndedProperly);
        Assert.True(track.Events.Count < SequenceReader.MostCommands, "it ran to the budget instead of noticing the loop");

        Assert.Contains(track.Events, e => e.Command == SequenceCommand.Goto);

        // And it says so, which "ended properly" cannot: a track that repeats and a track
        // that finishes are both reads this reader followed to somewhere it understands, and
        // they are not the same thing to be.
        Assert.True(track.Loops);
    }

    /// <summary>
    /// And it is read once rather than twice.
    /// <para>
    /// The set that noticed the loop held jump targets only, and a track's own beginning is
    /// not a jump target — so the first jump back was followed, the whole track was read a
    /// second time, and only then did the check catch it. Every looping track on a cartridge
    /// came back with twice the notes it has and spent twice the budget it needed.
    /// </para>
    /// </summary>
    [Fact]
    public void AndItIsReadOnceRatherThanTwice()
    {
        TrackRead track = Read(SyntheticRom.LoopingTrackOffset);

        Assert.Equal(SyntheticRom.NotesInLoopingTrack, track.Notes);

        // One note, one jump. Anything more is the body read again.
        Assert.Single(track.Events.Where(e => e.Command == SequenceCommand.Goto));
    }

    /// <summary>
    /// A phrase called twice is played twice.
    /// <para>
    /// This is the one that was wrong, and it was wrong because a call and a jump backwards
    /// shared one set of places-already-been. A call comes back and a jump does not, so a
    /// subsection called a second time looked exactly like a loop: the read stopped dead
    /// there and reported that it had ended properly, with a call as its last command.
    /// </para>
    /// <para>
    /// The performer then walked off the end of that list and reported the track as having
    /// run out — which is a track ending where the music repeats, on every song written the
    /// way most music is written.
    /// </para>
    /// </summary>
    [Fact]
    public void APhraseCalledTwiceIsPlayedTwice()
    {
        TrackRead track = Read(SyntheticRom.RepeatedCallTrackOffset);

        Assert.True(track.EndedProperly);

        // Two calls to one place, both followed.
        Assert.Equal(2, track.Calls);
        Assert.Equal(2, track.Events.Count(e => e.Command == SequenceCommand.Call));

        // The subsection's notes, twice. A read that stopped at the second call finds half.
        Assert.Equal(
            SyntheticRom.NotesPerRepeatedSubsection * 2,
            track.Notes);

        // And it got past both of them to the jump that makes the track repeat, which is the
        // command the truncated read never reached.
        Assert.True(track.Loops);
        Assert.Equal(SequenceCommand.Goto, track.Events[^1].Command);
    }

    /// <summary>
    /// A subsection that calls itself is the thing that genuinely has no bottom, and it is
    /// reported rather than followed — and reported as a read that did not end, because it
    /// did not.
    /// </summary>
    [Fact]
    public void ASubsectionThatCallsItselfIsStopped()
    {
        TrackRead track = Read(SyntheticRom.SelfCallingTrackOffset);

        Assert.False(track.EndedProperly);
        Assert.False(track.Loops);
        Assert.True(track.Events.Count < SequenceReader.MostCommands, "it ran to the budget instead of noticing the recursion");
    }

    /// <summary>
    /// A return with nothing to return to is a read that has lost its place, not a track
    /// that has finished.
    /// <para>
    /// It used to count as ending properly, which handed the performer a track whose last
    /// command does nothing — so it ran off the end of the event list and called itself a
    /// track that had run out. The same wrong answer as the repeated call, from the other
    /// side.
    /// </para>
    /// </summary>
    [Fact]
    public void AReturnWithNothingToReturnToIsNotAnEnding()
    {
        // The subsection on its own, entered without the call that leads to it.
        TrackRead track = Read(SyntheticRom.CalledSubsectionOffset);

        Assert.False(track.EndedProperly);
        Assert.Equal(SequenceCommand.Return, track.Events[^1].Command);
    }

    /// <summary>
    /// A track that runs off the end of the file says so. It does not throw, and it does not
    /// report an ending it never reached.
    /// </summary>
    [Fact]
    public void ATrackThatRunsOffTheEndSaysSo()
    {
        TrackRead track = Read(SyntheticRom.UnendedTrackOffset);

        Assert.False(track.EndedProperly);
        Assert.DoesNotContain(track.Events, e => e.Command == SequenceCommand.End);
    }

    /// <summary>
    /// A track beginning with an argument byte has no command to repeat, and that is
    /// reported rather than guessed at.
    /// </summary>
    [Fact]
    public void AndATrackBeginningWithAnArgumentIsNotFollowed()
    {
        // The middle of a note's arguments — a real byte in the file, and not a place a
        // track begins.
        TrackRead track = Read(SyntheticRom.SequencesOffset + 1);

        Assert.False(track.EndedProperly);
        Assert.Empty(track.Events);
    }

    /// <summary>
    /// A read that fails says where it died and on what.
    /// <para>
    /// "The reader could not follow it" names no byte, no offset and nothing to fix, and on
    /// a real cartridge it was the reason 142 songs of 255 did not come back — every one of
    /// them. The likeliest cause is an argument width: a command whose arguments this reader
    /// counts wrongly leaves the read a byte or two out and everything after it is nonsense,
    /// so the byte it dies on is the evidence for which command that is.
    /// </para>
    /// </summary>
    [Fact]
    public void AFailedReadSaysWhereItDied()
    {
        TrackRead ran = Read(SyntheticRom.UnendedTrackOffset);

        Assert.False(ran.EndedProperly);

        // It ran off the end of the file, so where it stopped is past the end of it.
        Assert.True(ran.StoppedAt >= 0, "a read that failed did not say where");
        Assert.True(ran.StoppedAt >= SyntheticRom.RomSize, $"it stopped at {ran.StoppedAt:X}, inside the file");
    }

    /// <summary>
    /// And a read that begins on an argument says which byte that was, rather than only that
    /// it was one.
    /// </summary>
    [Fact]
    public void AndOneThatBeginsOnAnArgumentSaysWhichByte()
    {
        var rom = new SyntheticRom().ToRom();

        int at = SyntheticRom.SequencesOffset + 1;

        TrackRead track = SequenceReader.Read(rom, at);

        Assert.False(track.EndedProperly);
        Assert.Equal(at, track.StoppedAt);
        Assert.Equal(rom.ReadU8(at), track.StoppedOn);
    }

    /// <summary>
    /// A command with a stated width takes exactly that, whatever the bytes look like.
    /// <para>
    /// This is the whole point of stating one. The greedy rule stops at anything that is
    /// itself a command, which is right for ordinary arguments and wrong for an address —
    /// four bytes of an address have values above 0x80 in them, so a greedy read walks into
    /// the middle of one and, because almost no byte in this encoding is invalid, never
    /// fails. It runs until the budget stops it.
    /// </para>
    /// </summary>
    [Fact]
    public void AStatedWidthIsTakenWhateverTheBytesLookLike()
    {
        var rom = new SyntheticRom().ToRom();

        int at = SyntheticRom.SettingWithAnAddressOffset;

        // Greedily, the four bytes of the address are not arguments: the read takes the one
        // below 0x80 and then tries to run the rest as commands.
        TrackRead greedy = SequenceReader.Read(rom, at);

        SequenceEvent guessed = greedy.Events.First(e => e.Command == SequenceCommand.Setting);

        Assert.True(guessed.Arguments.Count < 4, "the greedy rule already took the whole address");

        // Told how wide it is, it takes all four and the track reads to its end.
        TrackRead stated = SequenceReader.Read(
            rom, at, new Dictionary<byte, int> { [SyntheticRom.SettingWithAnAddressOpcode] = 4 });

        Assert.True(stated.EndedProperly, "stating the width did not let the track reach its end");
        Assert.Equal(4, stated.Events.First(e => e.Command == SequenceCommand.Setting).Arguments.Count);
    }

    /// <summary>And a read may be given a smaller budget, which is what makes a sweep possible.</summary>
    [Fact]
    public void AndAReadMayBeGivenASmallerBudget()
    {
        var rom = new SyntheticRom().ToRom();

        TrackRead brief = SequenceReader.Read(rom, SyntheticRom.UnendedTrackOffset, null, budget: 4);

        Assert.False(brief.EndedProperly);
        Assert.True(brief.Events.Count <= 4);
    }

    /// <summary>
    /// A bare byte after a wait is another note, not a repeated wait.
    /// <para>
    /// The one rule that had every failing song on a real cartridge failing. A wait takes
    /// nothing after it, so repeating a wait consumes no bytes — a reader that let a wait
    /// become the running command sat on one offset for twenty thousand commands and then
    /// gave up. Only a command with something after it can be the running one.
    /// </para>
    /// <para>
    /// Read correctly the same bytes are music: a note, a wait, another note at a different
    /// key, a wait, an end.
    /// </para>
    /// </summary>
    [Fact]
    public void ABareByteAfterAWaitIsAnotherNote()
    {
        TrackRead track = Read(SyntheticRom.BareByteAfterAWaitOffset);

        Assert.True(track.EndedProperly, "the track did not reach its end");

        List<SequenceEvent> notes = [.. track.Events.Where(e => e.Command == SequenceCommand.NoteOn)];

        // Two of them, and the second is the byte that used to be a repeated wait.
        Assert.Equal(2, notes.Count);
        Assert.Equal(0x3C, notes[0].Arguments[0]);
        Assert.Equal(0x30, notes[1].Arguments[0]);

        // And the waits really are there, so this is not passing by ignoring them.
        Assert.Equal(2, track.Events.Count(e => e.Command == SequenceCommand.Wait));
    }

    /// <summary>
    /// And no command may leave the read where it found it.
    /// <para>
    /// The guard on the class of bug above rather than on that one instance. A reader that
    /// can spin is a client that hangs, and this one could for as long as its budget allowed
    /// while reporting nothing useful at the end of it.
    /// </para>
    /// </summary>
    [Fact]
    public void AndNoCommandLeavesTheReadWhereItFoundIt()
    {
        var rom = new SyntheticRom().ToRom();

        // Every track the walk finds, read to its end or to its failure — and none of them
        // spending the whole budget standing still.
        SoundTreeResult tree = SoundLocator.Walk(rom);

        foreach (TrackRead read in SequenceReader.AllTracks(rom, tree))
        {
            Assert.True(
                read.Events.Count < SequenceReader.MostCommands,
                $"the track at {read.Offset:X} used the whole budget, which is what spinning looks like");
        }
    }

    /// <summary>
    /// Every track of every song the walk found reads to an end, and the report says how
    /// many did — including when that number is smaller than the total.
    /// </summary>
    [Fact]
    public void EveryTrackOfEverySongReads()
    {
        Rom rom = Synthetic.ToRom();

        SoundTreeResult tree = SoundLocator.Walk(rom);

        var said = new List<string>();

        IReadOnlyList<TrackRead> tracks = SequenceReader.AllTracks(rom, tree, said.Add);

        Assert.NotEmpty(tracks);

        // All but two. One song carries a last track that runs off the end of the file, and
        // one song's only track does — so everything downstream has both an unfinished track
        // to drop and a song with nothing left to assemble.
        Assert.Equal(tracks.Count - 2, tracks.Count(t => t.EndedProperly));

        Assert.Contains(said, l => l.Contains("ran to an end"));
        Assert.Contains(said, l => l.Contains("does not account for"));

        // And the tracks really are the songs' tracks rather than a list of the same one.
        Assert.Equal(tree.Songs.Sum(s => s.TrackCount), tracks.Count);
    }
}
