using PokeMmo.Core.Sound;
using PokeMmo.RomExtract.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The last joint in the sound work: a cartridge in, something that plays out.
/// <para>
/// The locator finds the tables, the reader turns a track into commands, the decoder unpacks a
/// recording and the player performs. Each knows nothing about the others, which is what made
/// them all testable — and it left one piece that has to know about all four. This is it, and
/// these are the tests that a song survives the whole chain rather than each link separately.
/// </para>
/// </summary>
public class LoadingASongTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static (Rom Rom, SoundTreeResult Tree) Cartridge()
    {
        Rom rom = Synthetic.ToRom();

        return (rom, SoundLocator.Walk(rom));
    }

    /// <summary>A song comes back, assembled out of four separate readings.</summary>
    [Fact]
    public void ASongComesBack()
    {
        (Rom rom, SoundTreeResult tree) = Cartridge();

        Assert.NotNull(SongLoader.Load(rom, tree, 0, new Mixer(8000)));
    }

    /// <summary>
    /// And every song in the table does, bar the one deliberately broken — which is the
    /// only one allowed to fail, and has to fail for the stated reason.
    /// </summary>
    [Fact]
    public void AndEverySongInTheTableDoes()
    {
        (Rom rom, SoundTreeResult tree) = Cartridge();

        Assert.NotEmpty(tree.Table);

        int broken = tree.Table.ToList().FindIndex(
            e => e.HeaderOffset
                 == SyntheticRom.SongHeadersOffset
                    + SyntheticRom.SongWhoseEveryTrackIsBroken * SyntheticRom.SongStride);

        Assert.True(broken >= 0, "the broken song is not in the table, so it proves nothing");

        for (int song = 0; song < tree.Table.Count; song++)
        {
            SongPlayer? player = SongLoader.Load(rom, tree, song, new Mixer(8000), out SongTrouble why);

            if (song == broken)
            {
                Assert.Null(player);
                Assert.Equal(SongTrouble.EveryTrackDropped, why);

                continue;
            }

            Assert.NotNull(player);
            Assert.Equal(SongTrouble.None, why);
        }
    }

    /// <summary>
    /// And a song number the table does not have says which of the four troubles it is.
    /// <para>
    /// "It did not come back" is one word for the table, the voicegroup walk and the track
    /// reader, and those are three entirely different places to look.
    /// </para>
    /// </summary>
    [Fact]
    public void AndItSaysWhichTroubleItWas()
    {
        (Rom rom, SoundTreeResult tree) = Cartridge();

        SongLoader.Load(rom, tree, tree.Table.Count, new Mixer(8000), out SongTrouble past);
        SongLoader.Load(rom, tree, -1, new Mixer(8000), out SongTrouble before);

        Assert.Equal(SongTrouble.NoSuchSong, past);
        Assert.Equal(SongTrouble.NoSuchSong, before);
    }

    /// <summary>
    /// A track this reader could not follow to an end is dropped rather than performed.
    /// <para>
    /// Performing it would be performing a guess. The fixture's last song carries one that
    /// runs off the end of the file, and the song still loads — with one fewer track than
    /// its own header claims, which is the number that says the drop happened.
    /// </para>
    /// </summary>
    [Fact]
    public void ATrackThatCouldNotBeFollowedIsDropped()
    {
        (Rom rom, SoundTreeResult tree) = Cartridge();

        int at = SyntheticRom.SongHeadersOffset + SyntheticRom.SongWithAnUnfinishedTrack * SyntheticRom.SongStride;

        SongHeaderRecord header = tree.Songs.Single(s => s.Offset == at);

        int index = tree.Table.ToList().FindIndex(e => e.HeaderOffset == at);

        SongPlayer player = SongLoader.Load(rom, tree, index, new Mixer(8000))!;

        Assert.Equal(header.TrackCount - 1, player.TrackCount);
        Assert.True(header.TrackCount > 1, "the broken song has one track, so dropping it proves nothing");
    }

    /// <summary>
    /// A song this cartridge does not have comes back as nothing, rather than as silence.
    /// <para>
    /// A caller handed an empty song would play nothing and believe it played something. A
    /// map naming a song that is not there is a finding, and a finding is not a thing to
    /// paper over with a rest.
    /// </para>
    /// </summary>
    [Fact]
    public void ASongThatIsNotThereComesBackAsNothing()
    {
        (Rom rom, SoundTreeResult tree) = Cartridge();

        Assert.Null(SongLoader.Load(rom, tree, -1, new Mixer(8000)));
        Assert.Null(SongLoader.Load(rom, tree, tree.Table.Count, new Mixer(8000)));
    }

    /// <summary>And it performs — the whole chain, ending in samples that are not silence.</summary>
    [Fact]
    public void AndItPerforms()
    {
        (Rom rom, SoundTreeResult tree) = Cartridge();

        SongPlayer player = SongLoader.Load(rom, tree, 0, new Mixer(8000))!;

        var heard = new List<short>();

        for (int piece = 0; piece < 40 && !player.IsFinished; piece++)
            heard.AddRange(player.Render(400));

        Assert.NotEmpty(heard);
        Assert.Contains(heard, sample => sample != 0);
    }

    /// <summary>
    /// A track that jumps backwards on the cartridge repeats when it is performed.
    /// <para>
    /// The reader and the player have to agree about what an offset means, and nothing made
    /// them. The reader stops at a backward jump — following it would be reading the same
    /// bytes for ever — and leaves the place it was going; the player finds that place by
    /// comparing it against where each command came from. One byte out in either and every
    /// song in the game would stop at the end of its first time through, silently, on
    /// somebody else's machine.
    /// </para>
    /// </summary>
    [Fact]
    public void ATrackThatJumpsBackwardsRepeats()
    {
        (Rom rom, SoundTreeResult _) = Cartridge();

        TrackRead read = SequenceReader.Read(rom, SyntheticRom.LoopingTrackOffset);

        // Two of them, because the reader went round once before it recognised where it had
        // been. Both name the same place, which is the place the track repeats from.
        List<SequenceEvent> jumps = [.. read.Events.Where(e => e.Command == SequenceCommand.Goto)];

        Assert.NotEmpty(jumps);

        // The reader resolved where they go, and they go somewhere the reader itself recorded
        // a command — which is the agreement, stated as a number.
        Assert.All(jumps, jump => Assert.Contains(read.Events, e => e.Offset == jump.Target));

        var player = new SongPlayer(
            [new Track(read.Events)],
            [Instrument.Nothing],
            new Mixer(8000));

        for (int piece = 0; piece < 200; piece++) player.Render(100);

        Assert.False(player.IsFinished, "a track that jumps back to itself stopped anyway");
    }

    /// <summary>
    /// The instruments come off the cartridge rather than being invented — the recordings are
    /// the ones the locator found, at the rates their own headers gave.
    /// </summary>
    [Fact]
    public void TheInstrumentsAreTheCartridgesOwn()
    {
        (Rom rom, SoundTreeResult tree) = Cartridge();

        Assert.NotEmpty(tree.Samples);

        // Every rate a sample record carries is one a real recording could have been made at,
        // and those are the rates the instruments are built from.
        Assert.All(tree.Samples, s => Assert.InRange(s.Rate, 5000, 40000));
    }

    /// <summary>
    /// Every slot of a voicegroup is filled, including the ones this build cannot play.
    /// <para>
    /// A track asks for an instrument by position, so a voicegroup with holes in it would
    /// make instrument seven mean different things in two songs. The shapes come back silent
    /// rather than absent.
    /// </para>
    /// </summary>
    [Fact]
    public void EverySlotOfAVoicegroupIsFilled()
    {
        (Rom rom, SoundTreeResult tree) = Cartridge();

        VoicegroupRecord group = tree.Voicegroups.First();

        // The fixture deliberately makes some of them shapes rather than recordings.
        Assert.True(group.Sampled < group.Count, "every instrument is a recording, so this proves nothing");

        // The song that draws on it still loads, which it could not if a shape had been
        // dropped and every number after it shifted.
        SongHeaderRecord song = tree.Songs.First(s => s.VoicegroupOffset == group.Offset);

        int index = tree.Table.ToList().FindIndex(e => e.HeaderOffset == song.Offset);

        Assert.True(index >= 0);

        SongPlayer player = SongLoader.Load(rom, tree, index, new Mixer(8000))!;

        // Every slot, shapes included. A hole would shift every instrument number after it.
        Assert.Equal(group.Count, player.InstrumentCount);
    }

    /// <summary>
    /// A file with no sound in it loads no songs, and says so by returning nothing rather
    /// than by throwing.
    /// </summary>
    [Fact]
    public void AFileWithNoSoundInItLoadsNothing()
    {
        var empty = new Rom(new byte[0x4000]);

        SoundTreeResult tree = SoundLocator.Walk(empty);

        Assert.Null(SongLoader.Load(empty, tree, 0, new Mixer(8000)));
    }

    /// <summary>
    /// Whether a track repeats survives the trip from the reader into the performer.
    /// <para>
    /// The number that says a song is being cut short. A track that has run out and a track
    /// that was written to repeat were one answer, so a song whose tracks stop where the
    /// music loops looked exactly like a song that had finished — and the count of tracks
    /// that had run out was the only thing anybody was watching.
    /// </para>
    /// </summary>
    [Fact]
    public void WhetherATrackRepeatsReachesThePerformer()
    {
        (Rom rom, SoundTreeResult tree) = Cartridge();

        int song = tree.Table.ToList().FindIndex(
            e => e.HeaderOffset
                 == SyntheticRom.SongHeadersOffset
                    + SyntheticRom.SongWithALoopingTrack * SyntheticRom.SongStride);

        Assert.True(song >= 0, "the song with a looping track is not in the table, so it proves nothing");

        SongPlayer player = Assert.IsType<SongPlayer>(SongLoader.Load(rom, tree, song, new Mixer(8000)));

        Assert.Equal(1, player.Looping);

        // And nothing has stopped that was written to repeat, which is the only right answer
        // before a single sample has been asked for.
        Assert.Equal(0, player.LoopedAndStopped);
    }

    /// <summary>
    /// And a song of tracks that all run to an end reports none of them as repeating, which
    /// is what makes the number above mean anything.
    /// </summary>
    [Fact]
    public void AndASongThatStopsReportsNoneRepeating()
    {
        (Rom rom, SoundTreeResult tree) = Cartridge();

        int song = tree.Table.ToList().FindIndex(
            e => e.HeaderOffset == SyntheticRom.SongHeadersOffset);

        Assert.True(song >= 0);

        SongPlayer player = Assert.IsType<SongPlayer>(SongLoader.Load(rom, tree, song, new Mixer(8000)));

        Assert.Equal(0, player.Looping);
    }
}
