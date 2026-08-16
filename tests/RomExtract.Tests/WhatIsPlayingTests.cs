using PokeMmo.Core.Sound;
using PokeMmo.RomExtract.Maps;
using PokeMmo.RomExtract.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The part that decides what is playing.
/// <para>
/// The sound work had a reader, a decoder, a mixer and a performer, all tested, and not one of
/// them was ever asked for a sample. A map names a song by number; this is what turns a number
/// arriving into a song starting, a song carrying on, or nothing happening at all.
/// </para>
/// </summary>
public class WhatIsPlayingTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static SongPlayer Anything() =>
        new(
            [new Track([new SequenceEvent(0, 0xD4, SequenceCommand.NoteOn, [60, 127])])],
            [Instrument.Nothing],
            new Mixer(8000));

    // ---- asking for the same thing twice ------------------------------------------------

    /// <summary>
    /// A song asked for twice is not started twice.
    /// <para>
    /// Not an optimisation. A building's rooms are separate maps naming the same music, so a
    /// player walking through a door would otherwise hear the tune restart from its beginning
    /// every few steps.
    /// </para>
    /// </summary>
    [Fact]
    public void TheSameSongAskedForTwiceCarriesOn()
    {
        var box = new Jukebox(_ => Anything());

        box.Play(3);
        box.Play(3);
        box.Play(3);

        Assert.Equal(1, box.Fetches);
        Assert.Equal(3, box.Playing);
    }

    /// <summary>And a different one does change what is on.</summary>
    [Fact]
    public void AndADifferentOneChangesIt()
    {
        var box = new Jukebox(_ => Anything());

        box.Play(3);
        box.Play(4);

        Assert.Equal(2, box.Fetches);
        Assert.Equal(4, box.Playing);
    }

    /// <summary>Stopping clears it, so the same song can be put on again afterwards.</summary>
    [Fact]
    public void StoppingClearsIt()
    {
        var box = new Jukebox(_ => Anything());

        box.Play(3);
        box.Stop();

        Assert.True(box.IsSilent);
        Assert.Equal(Jukebox.Nothing, box.Playing);

        box.Play(3);

        Assert.Equal(2, box.Fetches);
        Assert.False(box.IsSilent);
    }

    // ---- a song that is not there ----------------------------------------------------------

    /// <summary>
    /// A song this cartridge does not have leaves silence, and is not looked for again.
    /// <para>
    /// Looking for it costs a walk of the whole table, and a player standing on that map would
    /// pay it sixty times a second. The map is a finding either way, and a finding is not a
    /// thing to keep rediscovering.
    /// </para>
    /// </summary>
    [Fact]
    public void ASongThatIsNotThereIsNotLookedForTwice()
    {
        var box = new Jukebox(_ => null);

        box.Play(9);
        box.Play(9);
        box.Play(9);

        Assert.True(box.IsSilent);
        Assert.Equal(1, box.Fetches);

        // And it is still what was asked for, which is what stops it being looked for again.
        Assert.Equal(9, box.Playing);
    }

    /// <summary>Nought is a real song, so "nothing" cannot be nought.</summary>
    [Fact]
    public void NoughtIsARealSong()
    {
        var box = new Jukebox(_ => Anything());

        box.Play(0);

        Assert.False(box.IsSilent);
        Assert.Equal(1, box.Fetches);
    }

    /// <summary>And asking for nothing is asking for silence rather than for song nothing.</summary>
    [Fact]
    public void AndAskingForNothingIsAskingForSilence()
    {
        var fetched = new List<int>();

        var box = new Jukebox(song =>
        {
            fetched.Add(song);

            return Anything();
        });

        box.Play(2);
        box.Play(Jukebox.Nothing);

        Assert.True(box.IsSilent);
        Assert.Equal([2], fetched);
    }

    // ---- samples ------------------------------------------------------------------------------

    /// <summary>
    /// It gives back exactly as many samples as were asked for, playing or not. A sound card
    /// asks for a fixed buffer and does not care that the game has nothing to say — a short
    /// one would be a click.
    /// </summary>
    [Fact]
    public void ItAlwaysGivesBackAsManySamplesAsWereAskedFor()
    {
        var box = new Jukebox(_ => Anything());

        Assert.Equal(512, box.Render(512).Length);

        box.Play(1);

        Assert.Equal(512, box.Render(512).Length);

        box.Stop();

        Assert.Equal(512, box.Render(512).Length);
    }

    /// <summary>And with nothing on, those samples are silence rather than whatever was there.</summary>
    [Fact]
    public void AndWithNothingOnTheyAreSilence()
    {
        var box = new Jukebox(_ => Anything());

        Assert.All(box.Render(256), sample => Assert.Equal(0, sample));
    }

    // ---- which song a map names ------------------------------------------------------------------

    /// <summary>
    /// A map carries the song its own header names.
    /// <para>
    /// The number was in the header record from the day map headers were read and went
    /// nowhere. A map that could not say what it plays is a map that cannot be played, and
    /// this is the two ends of the sound work finally meeting: the file says which song, and
    /// something else knows how to make a song out of it.
    /// </para>
    /// </summary>
    [Fact]
    public void AMapCarriesTheSongItsHeaderNames()
    {
        MapLibrary library = MapLibrary.Open(Synthetic.ToRom());

        List<int> music = [.. library.All().Select(m => m.Music)];

        Assert.NotEmpty(music);

        // Read rather than assumed: two maps name two different songs, so a constant would
        // not have passed.
        Assert.True(music.Distinct().Count() > 1, "every map names the same song, so this proves nothing");
        Assert.All(music, song => Assert.True(song >= 0));
    }

    // ---- against a cartridge --------------------------------------------------------------------

    /// <summary>
    /// And the whole of it against a file: a song number in, a noise out.
    /// <para>
    /// Every joint at once — the locator, the reader, the decoder, the performer and this. The
    /// separate tests each hold one of them; nothing until now held them together.
    /// </para>
    /// </summary>
    [Fact]
    public void ASongNumberGoesInAndANoiseComesOut()
    {
        Rom rom = Synthetic.ToRom();
        SoundTreeResult tree = SoundLocator.Walk(rom);

        var mixer = new Mixer(8000);
        var box = new Jukebox(song => SongLoader.Load(rom, tree, song, mixer));

        box.Play(0);

        Assert.False(box.IsSilent);

        var heard = new List<short>();

        for (int piece = 0; piece < 40; piece++) heard.AddRange(box.Render(400));

        Assert.Contains(heard, sample => sample != 0);
    }
}
