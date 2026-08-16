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
                song == SyntheticRom.SongNamingAMergedVoicegroup
                    ? SyntheticRom.SecondAdjacentVoicegroupOffset
                    : SyntheticRom.VoicegroupsOffset
                      + SyntheticRom.VoicegroupForSong(song) * SyntheticRom.VoicegroupStride,
                found.VoicegroupOffset);
        }

        // And the counts really do differ, so the loop cannot be comparing a constant with
        // itself.
        Assert.True(tree.Songs.Select(s => s.TrackCount).Distinct().Count() > 1);
    }

    /// <summary>
    /// A song naming the middle of a run of instruments is a song, not a rejection.
    /// <para>
    /// There is no delimiter between voicegroups: they sit back to back, so one unbroken run
    /// covers several and nothing in the file says where one ends. A walk that only offered
    /// run starts rejected every song but the first of each run — on a real cartridge that
    /// left sixteen song headers on a file that has hundreds.
    /// </para>
    /// </summary>
    [Fact]
    public void ASongNamingTheMiddleOfARunIsStillASong()
    {
        SoundTreeResult tree = Walked();

        int at = SyntheticRom.SongHeadersOffset
                 + SyntheticRom.SongNamingAMergedVoicegroup * SyntheticRom.SongStride;

        SongHeaderRecord song = tree.Songs.Single(s => s.Offset == at);

        Assert.Equal(SyntheticRom.SecondAdjacentVoicegroupOffset, song.VoicegroupOffset);

        // And it really is the middle of one: the run it falls in starts earlier.
        VoicegroupRecord run = tree.Voicegroups.Single(v => v.Holds(song.VoicegroupOffset));

        Assert.True(
            run.Offset < song.VoicegroupOffset,
            "the run starts where the song points, so this proves nothing about the middle");
    }

    /// <summary>
    /// And a pointer landing part way through an instrument is not one. Twelve bytes is the
    /// stride, and four bytes into one of them is not the start of anything.
    /// </summary>
    [Fact]
    public void ButAPointerIntoTheMiddleOfAnInstrumentIsNot()
    {
        SoundTreeResult tree = Walked();

        VoicegroupRecord run = tree.Voicegroups.Single(
            v => v.Holds(SyntheticRom.SecondAdjacentVoicegroupOffset));

        Assert.False(run.Holds(SyntheticRom.SecondAdjacentVoicegroupOffset + 4));
        Assert.Empty(run.From(SyntheticRom.SecondAdjacentVoicegroupOffset + 4));
    }

    /// <summary>
    /// And what it draws on begins where it pointed rather than where the run does.
    /// <para>
    /// The half that would still be wrong if only the confirming were fixed: a song naming
    /// the second group would load, and every instrument number in it would be off by the
    /// length of the first.
    /// </para>
    /// </summary>
    [Fact]
    public void AndWhatItDrawsOnBeginsWhereItPointed()
    {
        SoundTreeResult tree = Walked();

        VoicegroupRecord run = tree.Voicegroups.Single(
            v => v.Holds(SyntheticRom.SecondAdjacentVoicegroupOffset));

        IReadOnlyList<InstrumentRecord> from = run.From(SyntheticRom.SecondAdjacentVoicegroupOffset);

        Assert.NotEmpty(from);
        Assert.Equal(SyntheticRom.SecondAdjacentVoicegroupOffset, from[0].Offset);
        Assert.True(from.Count < run.Count, "the run and the voicegroup are the same length");
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

    // ---- why a header was not confirmed ------------------------------------------------

    /// <summary>
    /// A header the walk confirmed is rejected by nothing.
    /// <para>
    /// The other half of every rejection test, and the one that says the reasons are being
    /// asked of the same rules the walk itself used rather than of a second, looser copy.
    /// </para>
    /// </summary>
    [Fact]
    public void AConfirmedHeaderIsRejectedByNothing()
    {
        Rom rom = Synthetic.ToRom();
        SoundTreeResult tree = SoundLocator.Walk(rom);

        Assert.NotEmpty(tree.Songs);

        Assert.Equal(SongRejection.None, SoundLocator.WhyNot(rom, tree, tree.Songs[0].Offset));
    }

    /// <summary>
    /// And one that is not says which rule turned it down.
    /// <para>
    /// Thirty-one songs the table names come back unconfirmed on a real cartridge, and one of
    /// them is the professor's laboratory. "Not confirmed" was one word covering six faults
    /// across three layers — the table, the voicegroup walk and the recordings under it — and
    /// which one it is decides where to look.
    /// </para>
    /// </summary>
    [Fact]
    public void AndOneThatIsNotSaysWhichRuleTurnedItDown()
    {
        Rom rom = Synthetic.ToRom();
        SoundTreeResult tree = SoundLocator.Walk(rom);

        // A song header whose voicegroup pointer leads nowhere this walk confirmed. The
        // fixture has one on purpose; it is the reason the walk's own count is short.
        SongRejection why = SoundLocator.WhyNot(rom, tree, SyntheticRom.SongWithNoVoicegroupOffset);

        Assert.NotEqual(SongRejection.None, why);
    }

    /// <summary>
    /// And a place with no header at all is turned down by the track count rather than by
    /// something further in.
    /// <para>
    /// A rejection that names the wrong rule is worse than one that names none: it sends
    /// somebody to the voicegroup walk when the offset was never a header to begin with.
    /// </para>
    /// </summary>
    [Fact]
    public void AndSomewhereWithNoHeaderIsTurnedDownByTheTrackCount()
    {
        Rom rom = Synthetic.ToRom();
        SoundTreeResult tree = SoundLocator.Walk(rom);

        // Nought is not a track count anything could have, and the fixture leaves the front
        // of the file empty.
        Assert.Equal(SongRejection.TrackCount, SoundLocator.WhyNot(rom, tree, 0x200));
    }

    /// <summary>And the bytes behind a rejection can be looked at rather than believed.</summary>
    [Fact]
    public void AndTheBytesBehindItCanBeLookedAt()
    {
        Rom rom = Synthetic.ToRom();

        string bytes = SoundLocator.BytesAt(rom, SyntheticRom.SongHeadersOffset);

        Assert.Equal(8, bytes.Split(' ').Length);

        // Past the end of the file says so rather than throwing or coming back empty.
        Assert.Contains("past the end", SoundLocator.BytesAt(rom, rom.Length + 16));
    }
}
