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
        Assert.All(tracks, t => Assert.True(t.EndedProperly));

        Assert.Contains(said, l => l.Contains("ran to an end"));
        Assert.Contains(said, l => l.Contains("does not account for"));

        // And the tracks really are the songs' tracks rather than a list of the same one.
        Assert.Equal(tree.Songs.Sum(s => s.TrackCount), tracks.Count);
    }
}
