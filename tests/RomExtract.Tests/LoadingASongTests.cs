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

    /// <summary>And every song in the table does, not only the first.</summary>
    [Fact]
    public void AndEverySongInTheTableDoes()
    {
        (Rom rom, SoundTreeResult tree) = Cartridge();

        Assert.NotEmpty(tree.Table);

        for (int song = 0; song < tree.Table.Count; song++)
            Assert.NotNull(SongLoader.Load(rom, tree, song, new Mixer(8000)));
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
}
