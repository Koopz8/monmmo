using PokeMmo.RomExtract.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Three layers, each confirmed by the one below it.
/// <para>
/// An instrument is twelve bytes containing a pointer to a recording already confirmed. A
/// voicegroup is a run of confirmed instruments. A song header points at a confirmed
/// voicegroup. The song table points at confirmed song headers. That chain is the entire
/// method, and it is why nothing in the locator needs to know where anything is on any
/// particular cartridge.
/// </para>
/// <para>
/// These tests assert the chain rather than the offsets — that each layer found what the
/// layer below it made findable, and that the disagreements are counted rather than
/// smoothed over.
/// </para>
/// </summary>
public class WalkingUpToTheSongsTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static SoundTreeResult Walked() => SoundLocator.Walk(Synthetic.ToRom());

    // ---- instruments and voicegroups -------------------------------------------------

    /// <summary>Every voicegroup written, and no extras invented.</summary>
    [Fact]
    public void ItFindsTheVoicegroups()
    {
        SoundTreeResult tree = Walked();

        for (int group = 0; group < SyntheticRom.VoicegroupCount; group++)
        {
            int at = SyntheticRom.VoicegroupsOffset + group * SyntheticRom.VoicegroupStride;

            Assert.Contains(tree.Voicegroups, v => v.Offset == at);
        }
    }

    /// <summary>
    /// And each is the length it was written, which is what says the run ends where the
    /// instruments stop rather than wherever the reader got bored.
    /// </summary>
    [Fact]
    public void AndEachIsAsLongAsItWasWritten()
    {
        foreach (VoicegroupRecord group in Written(Walked()))
            Assert.Equal(SyntheticRom.InstrumentsPerVoicegroup, group.Count);
    }

    /// <summary>
    /// The kinds are read rather than assumed: the fixture makes every fourth instrument a
    /// shape rather than a recording, and both kinds come back.
    /// </summary>
    [Fact]
    public void AndTheKindsAreReadRatherThanAssumed()
    {
        VoicegroupRecord group = Written(Walked()).First();

        Assert.Contains(group.Instruments, i => i.Kind == InstrumentKind.Sampled);
        Assert.Contains(group.Instruments, i => i.Kind == InstrumentKind.Square);

        Assert.True(group.Sampled < group.Count, "every instrument came back as a recording");
    }

    /// <summary>
    /// Every recorded instrument points at a recording this build actually found. This is
    /// the condition the whole walk rests on, so it is asserted directly rather than
    /// inferred from the counts.
    /// </summary>
    [Fact]
    public void AndEveryRecordedInstrumentNamesARecordingThatWasFound()
    {
        SoundTreeResult tree = Walked();

        var samples = tree.Samples.Select(s => s.Offset).ToHashSet();

        foreach (InstrumentRecord instrument in tree.Voicegroups.SelectMany(v => v.Instruments))
        {
            if (!instrument.IsSampled) continue;

            Assert.Contains((int)(instrument.Pointer - 0x0800_0000), samples);
        }
    }

    /// <summary>A run of two is instrument-shaped and is not a voicegroup.</summary>
    [Fact]
    public void AndARunTooShortToBeAnythingIsNotAVoicegroup() =>
        Assert.DoesNotContain(Walked().Voicegroups, v => v.Offset == SyntheticRom.ShortVoicegroupOffset);

    // ---- songs -------------------------------------------------------------------------

    [Fact]
    public void ItFindsTheSongs()
    {
        SoundTreeResult tree = Walked();

        for (int song = 0; song < SyntheticRom.SongCount; song++)
        {
            int at = SyntheticRom.SongHeadersOffset + song * SyntheticRom.SongStride;

            Assert.Contains(tree.Songs, s => s.Offset == at);
        }
    }

    /// <summary>
    /// And reads each header rather than assuming it — the track count varies between one
    /// and four across the fixture, and so does the voicegroup each song draws on.
    /// </summary>
    [Fact]
    public void AndReadsWhatEachSongHeaderSaid()
    {
        SoundTreeResult tree = Walked();

        for (int song = 0; song < SyntheticRom.SongCount; song++)
        {
            int at = SyntheticRom.SongHeadersOffset + song * SyntheticRom.SongStride;

            SongHeaderRecord found = tree.Songs.Single(s => s.Offset == at);

            Assert.Equal(SyntheticRom.TracksInSong(song), found.TrackCount);
            Assert.Equal(found.TrackCount, found.TrackOffsets.Count);

            Assert.Equal(
                SyntheticRom.VoicegroupsOffset
                    + SyntheticRom.VoicegroupForSong(song) * SyntheticRom.VoicegroupStride,
                found.VoicegroupOffset);
        }

        // And the counts really do differ, so the loop cannot be comparing a constant with
        // itself.
        Assert.True(tree.Songs.Select(s => s.TrackCount).Distinct().Count() > 1);
    }

    // ---- the table -----------------------------------------------------------------------

    [Fact]
    public void ItFindsTheSongTable()
    {
        SoundTreeResult tree = Walked();

        Assert.True(tree.FoundATable);
        Assert.Equal(SyntheticRom.SongTableOffset, tree.SongTableOffset);
        Assert.Equal(SyntheticRom.SongCount, tree.Table.Count);
    }

    /// <summary>
    /// And every entry names a song that was found by shape, which is the two halves of the
    /// walk agreeing with each other.
    /// </summary>
    [Fact]
    public void AndEveryEntryNamesASongThatWasFound()
    {
        SoundTreeResult tree = Walked();

        var songs = tree.Songs.Select(s => s.Offset).ToHashSet();

        Assert.All(tree.Table, entry => Assert.Contains(entry.HeaderOffset, songs));

        // And nothing was found by shape that the table cannot reach.
        Assert.Equal(0, tree.SongsNoTableNames);
    }

    /// <summary>
    /// Three things that look like tables and are not, each wrong in exactly one way.
    /// <para>
    /// These exist because the break-it pass deleted four of the rules that tell tables
    /// apart and no test noticed. The fixture had one table-shaped thing in it, so every
    /// rule about choosing between candidates was guarding a case that was not there. Each
    /// decoy below fails exactly one rule and passes the rest, so deleting that rule makes
    /// the decoy win and this test say so.
    /// </para>
    /// </summary>
    [Fact]
    public void AndTheThingsThatLookLikeTablesAndAreNotAreNotChosen()
    {
        SoundTreeResult tree = Walked();

        Assert.NotEqual(SyntheticRom.ShortDecoyTableOffset, tree.SongTableOffset);
        Assert.NotEqual(SyntheticRom.WrongGroupTableOffset, tree.SongTableOffset);
        Assert.NotEqual(SyntheticRom.NotSongsTableOffset, tree.SongTableOffset);

        // And the two long ones are longer than the real table, which is what makes them
        // dangerous rather than merely wrong.
        Assert.True(SyntheticRom.DecoyTableCount > SyntheticRom.SongCount);
    }

    /// <summary>
    /// A song header that names a voicegroup nobody found is not a song header.
    /// <para>
    /// The fixture writes one that is right in every other way, because deleting this rule
    /// in the source changed nothing until there was something for it to reject.
    /// </para>
    /// </summary>
    [Fact]
    public void AndASongNamingAVoicegroupNobodyFoundIsNotASong() =>
        Assert.DoesNotContain(Walked().Songs, s => s.Offset == SyntheticRom.SongWithNoVoicegroupOffset);

    /// <summary>
    /// The corroboration: something in the file holds the table's own address.
    /// <para>
    /// It is deliberately weaker evidence than decoding the sound driver's prologue would
    /// be, and deliberately chosen for that reason — decoding the prologue is reading
    /// compiled code, which is the line this project does not cross.
    /// </para>
    /// </summary>
    [Fact]
    public void AndSomethingInTheFilePointsAtIt() =>
        Assert.True(Walked().PointersToTheTable > 0);

    // ---- what it says --------------------------------------------------------------------

    /// <summary>It reports every layer, including the rejections.</summary>
    [Fact]
    public void AndItSaysWhatItFoundAtEveryLayer()
    {
        var said = new List<string>();

        SoundLocator.Walk(Synthetic.ToRom(), said.Add);

        Assert.Contains(said, l => l.Contains("recorded sounds"));
        Assert.Contains(said, l => l.Contains("voicegroups"));
        Assert.Contains(said, l => l.Contains("song headers"));
        Assert.Contains(said, l => l.Contains("song table at"));
        Assert.Contains(said, l => l.Contains("too short or named no recording"));
        Assert.Contains(said, l => l.Contains("appears") && l.Contains("elsewhere in the file"));
    }

    /// <summary>
    /// A file with nothing in it finds nothing at every layer and says so, rather than
    /// throwing or returning a table of noise.
    /// </summary>
    [Fact]
    public void AndAFileWithNothingInItFindsNothingAnywhere()
    {
        var said = new List<string>();

        SoundTreeResult tree = SoundLocator.Walk(new Rom(new byte[0x4000]), said.Add);

        Assert.Empty(tree.Samples);
        Assert.Empty(tree.Voicegroups);
        Assert.Empty(tree.Songs);
        Assert.False(tree.FoundATable);
        Assert.Equal(-1, tree.SongTableOffset);

        Assert.Contains(said, l => l.Contains("no song table"));
    }

    private static IReadOnlyList<VoicegroupRecord> Written(SoundTreeResult tree) =>
    [
        .. Enumerable.Range(0, SyntheticRom.VoicegroupCount).Select(group =>
            tree.Voicegroups.Single(v =>
                v.Offset == SyntheticRom.VoicegroupsOffset + group * SyntheticRom.VoicegroupStride)),
    ];
}
