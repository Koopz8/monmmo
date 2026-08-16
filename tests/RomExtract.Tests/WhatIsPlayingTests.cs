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
        var box = new Jukebox(_ => Anything(), new Mixer(8000));

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
        var box = new Jukebox(_ => Anything(), new Mixer(8000));

        box.Play(3);
        box.Play(4);

        Assert.Equal(2, box.Fetches);
        Assert.Equal(4, box.Playing);
    }

    /// <summary>Stopping clears it, so the same song can be put on again afterwards.</summary>
    [Fact]
    public void StoppingClearsIt()
    {
        var box = new Jukebox(_ => Anything(), new Mixer(8000));

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
        var box = new Jukebox(_ => null, new Mixer(8000));

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
        var box = new Jukebox(_ => Anything(), new Mixer(8000));

        box.Play(0);

        Assert.False(box.IsSilent);
        Assert.Equal(1, box.Fetches);
    }

    /// <summary>And asking for nothing is asking for silence rather than for song nothing.</summary>
    [Fact]
    public void AndAskingForNothingIsAskingForSilence()
    {
        var fetched = new List<int>();

        var box = new Jukebox(
            song =>
            {
                fetched.Add(song);

                return Anything();
            },
            new Mixer(8000));

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
        var box = new Jukebox(_ => Anything(), new Mixer(8000));

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
        var box = new Jukebox(_ => Anything(), new Mixer(8000));

        Assert.All(box.Render(256), sample => Assert.Equal(0, sample));
    }

    // ---- a noise over the top --------------------------------------------------------------------

    private static Voice Noise() =>
        new([.. Enumerable.Range(0, 400).Select(i => (sbyte)(i % 2 == 0 ? 100 : -100))], 8000, false, 0);

    /// <summary>
    /// A cry sounds on a map with no music.
    /// <para>
    /// The case that would have been found only by standing in the one building where it
    /// happens. With nothing playing there was nothing turning the mixer over, so a cry went
    /// onto it and was never asked for.
    /// </para>
    /// </summary>
    [Fact]
    public void ACrySoundsWithNoMusicOn()
    {
        var box = new Jukebox(_ => Anything(), new Mixer(8000));

        Assert.True(box.IsSilent);

        box.PlayRecording(Noise(), 8000);

        Assert.Contains(box.Render(400), sample => sample != 0);
    }

    /// <summary>
    /// And over the music rather than instead of it. A jukebox that swapped one for the other
    /// would silence a town every time somebody sent something out.
    /// </summary>
    [Fact]
    public void AndOverTheMusicRatherThanInsteadOfIt()
    {
        var mixer = new Mixer(8000);

        var box = new Jukebox(
            _ => new SongPlayer(
                [
                    new Track(
                    [
                        new SequenceEvent(0, 0xD4, SequenceCommand.NoteOn, [60, 127]),
                        new SequenceEvent(3, 0x90, SequenceCommand.Wait, []),
                    ]),
                ],
                [new Instrument(Noise(), 60, 255, 255, 255, 255)],
                mixer),
            mixer);

        box.Play(1);

        int music = box.Render(400).Max(s => Math.Abs((int)s));

        box.Play(2);
        box.PlayRecording(Noise(), 8000);

        int both = box.Render(400).Max(s => Math.Abs((int)s));

        Assert.True(music > 0, "the music was silent, so nothing could be over it");
        Assert.True(both > music, $"{both} was no louder than {music}, so the cry replaced the song");
    }

    /// <summary>And a recording with nothing in it is nothing rather than a crash.</summary>
    [Fact]
    public void AndAnEmptyRecordingIsNothing()
    {
        var box = new Jukebox(_ => Anything(), new Mixer(8000));

        box.PlayRecording(new Voice([], 8000, false, 0), 8000);

        Assert.All(box.Render(256), sample => Assert.Equal(0, sample));
    }

    // ---- one-off songs over the music ------------------------------------------------------

    /// <summary>
    /// A song built to sound for a while and then stop, which is the shape of every effect:
    /// one note, a wait, and an end.
    /// </summary>
    private static SongPlayer Effect(Mixer mixer) =>
        new(
            [
                new Track(
                [
                    new SequenceEvent(0, 0xD4, SequenceCommand.NoteOn, [60, 127]),
                    new SequenceEvent(3, 0x82, SequenceCommand.Wait, []),
                    new SequenceEvent(4, 0xB1, SequenceCommand.End, []),
                ]),
            ],
            [new Instrument(Noise(), 60, 255, 255, 255, 255)],
            mixer);

    /// <summary>
    /// Music that goes on for ever, so that anything laid over it has something to be over.
    /// </summary>
    private static SongPlayer Music(Mixer mixer) =>
        new(
            [
                new Track(
                [
                    new SequenceEvent(0, 0xD4, SequenceCommand.NoteOn, [60, 127]),
                    new SequenceEvent(3, 0x90, SequenceCommand.Wait, []),
                    new SequenceEvent(4, 0xB2, SequenceCommand.Goto, [], 0),
                ]),
            ],
            [new Instrument(Noise(), 60, 255, 255, 255, 255)],
            mixer);

    /// <summary>
    /// A one-off song sounds over the music instead of replacing it.
    /// <para>
    /// The thing the sound work did not have. A faint, a door, a healing machine and a menu
    /// beep are all song numbers in the same table as the town themes, and a jukebox that
    /// performs one song at a time cannot sound one without stopping the music.
    /// </para>
    /// </summary>
    [Fact]
    public void AOneOffSongSoundsOverTheMusic()
    {
        var mixer = new Mixer(8000);

        var box = new Jukebox(song => song == 1 ? Music(mixer) : Effect(mixer), mixer);

        box.Play(1);

        int music = box.Render(200).Max(s => Math.Abs((int)s));

        Assert.True(box.PlayOver(2, group: 0));

        int both = box.Render(200).Max(s => Math.Abs((int)s));

        Assert.True(music > 0, "the music was silent, so nothing could be over it");
        Assert.True(both > music, $"{both} was no louder than {music}, so the effect replaced the song");

        // And the music is still the song that is on, which is what "over" means.
        Assert.Equal(1, box.Playing);
    }

    /// <summary>
    /// And the music is still playing after the effect has finished, rather than having been
    /// stopped by it.
    /// </summary>
    [Fact]
    public void AndTheMusicIsStillThereAfterwards()
    {
        var mixer = new Mixer(8000);

        var box = new Jukebox(song => song == 1 ? Music(mixer) : Effect(mixer), mixer);

        box.Play(1);
        box.PlayOver(2, group: 0);

        // Long enough for the effect's one note and its end to have gone by.
        box.Render(20_000);

        Assert.Equal(0, box.Effects);

        // A window wider than one turn of the music's own loop, because its recording is
        // shorter than the wait after it — so a narrow window can land in the gap between
        // one note ending and the next beginning and prove nothing.
        Assert.Contains(box.Render(8000), sample => sample != 0);
    }

    /// <summary>
    /// An effect that has finished is let go of rather than kept.
    /// <para>
    /// A performer left in place is one more thing advanced every sample for the rest of the
    /// session, and its last note is one of twelve voices the music cannot have.
    /// </para>
    /// </summary>
    [Fact]
    public void AnEffectThatHasFinishedIsLetGoOf()
    {
        var mixer = new Mixer(8000);

        var box = new Jukebox(_ => Effect(mixer), mixer);

        box.PlayOver(7, group: 0);

        Assert.Equal(1, box.Effects);

        box.Render(20_000);

        Assert.Equal(0, box.Effects);
    }

    /// <summary>
    /// The same effect asked for twice starts again, unlike music.
    /// <para>
    /// Not an inconsistency — the same rule applied to a different thing. Music asked for
    /// twice carries on because a building's rooms are separate maps naming one theme; an
    /// effect asked for twice is somebody pressing the button twice, and hearing nothing the
    /// second time is the bug.
    /// </para>
    /// </summary>
    [Fact]
    public void TheSameEffectAskedForTwiceStartsAgain()
    {
        var mixer = new Mixer(8000);

        var box = new Jukebox(_ => Effect(mixer), mixer);

        box.PlayOver(7, group: 0);
        box.PlayOver(7, group: 0);

        Assert.Equal(2, box.EffectFetches);
    }

    /// <summary>
    /// And it replaces the one that was on that performer rather than piling up on it.
    /// <para>
    /// The group is the number the song's own table entry carries, written twice. Two songs
    /// naming one performer cannot both be on, which is why a door opened twice quickly is
    /// one noise and why holding a direction does not turn a menu beep into a drone.
    /// </para>
    /// </summary>
    [Fact]
    public void AndItReplacesTheOneOnThatPerformer()
    {
        var mixer = new Mixer(8000);

        var box = new Jukebox(_ => Effect(mixer), mixer);

        box.PlayOver(7, group: 3);
        box.PlayOver(8, group: 3);

        Assert.Equal(1, box.Effects);
    }

    /// <summary>
    /// And two effects naming different performers do both sound.
    /// <para>
    /// The other half of the same rule, and the one a fixture with a single group could not
    /// tell apart from replacing everything.
    /// </para>
    /// </summary>
    [Fact]
    public void AndTwoDifferentPerformersBothSound()
    {
        var mixer = new Mixer(8000);

        var box = new Jukebox(_ => Effect(mixer), mixer);

        box.PlayOver(7, group: 3);
        box.PlayOver(8, group: 4);

        Assert.Equal(2, box.Effects);
    }

    /// <summary>
    /// A song this cartridge does not have leaves whatever was on that performer alone.
    /// <para>
    /// A missing effect silencing a real one would be a worse failure than a missing effect.
    /// </para>
    /// </summary>
    [Fact]
    public void AnEffectThatIsNotThereLeavesTheOneThatIsAlone()
    {
        var mixer = new Mixer(8000);

        var box = new Jukebox(song => song == 9 ? null : Effect(mixer), mixer);

        box.PlayOver(7, group: 2);

        Assert.False(box.PlayOver(9, group: 2));
        Assert.Equal(1, box.Effects);
    }

    /// <summary>
    /// Stopping stops the effects too, and lets their notes go.
    /// <para>
    /// Otherwise a performer outlives the jukebox that was holding it, advancing for ever
    /// with nobody able to reach it.
    /// </para>
    /// </summary>
    [Fact]
    public void StoppingStopsTheEffectsToo()
    {
        var mixer = new Mixer(8000);

        var box = new Jukebox(_ => Effect(mixer), mixer);

        box.PlayOver(7, group: 1);
        box.Stop();

        Assert.Equal(0, box.Effects);
    }

    /// <summary>
    /// The mixer is turned exactly once a sample, however many things are being performed.
    /// <para>
    /// The failure this whole split exists to prevent, and one nothing would have said a word
    /// about. Two performers each turning the mixer for themselves would step every envelope
    /// twice per sample and read every recording at twice its rate — so the music's notes
    /// would decay faster for as long as an effect was sounding, and the effect would be
    /// played at the wrong pitch.
    /// </para>
    /// <para>
    /// Measured rather than asserted about: a recording is put on the mixer and rendered
    /// alone, then the same recording is rendered with an effect running alongside. A mixer
    /// turned twice per sample runs out of the recording in half the samples.
    /// </para>
    /// </summary>
    [Fact]
    public void TheMixerIsTurnedOnceASampleHoweverManyArePerforming()
    {
        static int Lasts(bool alongside)
        {
            var mixer = new Mixer(8000);

            var box = new Jukebox(_ => Effect(mixer), mixer);

            // A recording that runs out rather than loops, so how long it lasts is a
            // measurement of how fast the mixer is being turned.
            box.PlayRecording(Noise(), 8000);

            if (alongside) box.PlayOver(7, group: 0);

            short[] rendered = box.Render(2000);

            int last = 0;

            for (int i = 0; i < rendered.Length; i++)
                if (rendered[i] != 0) last = i;

            return last;
        }

        int alone = Lasts(false);
        int with = Lasts(true);

        Assert.True(alone > 0, "the recording was silent, so nothing was measured");

        // The effect's own note sounds too, so "with" may be longer. What it must not be is
        // half, which is what a mixer turned twice a sample gives.
        Assert.True(
            with >= alone,
            $"the recording ran out after {with} samples alongside an effect and {alone} alone — "
            + "the mixer is being turned more than once a sample");
    }

    /// <summary>
    /// An effect ending lets go of its own notes and nothing else's.
    /// <para>
    /// <b>This is the one nothing could fail.</b> The first version of this file tested that
    /// the music survived an effect — and it did, however the letting-go was done, because
    /// every fixture instrument here had a release of 255 and its track looped. A release of
    /// 255 never fades and a looping track starts the note again a moment later, so a music
    /// note wrongly released came straight back and the test could not see it.
    /// </para>
    /// <para>
    /// So this one is built the other way round: the music is one long note that is never
    /// started again, on an instrument whose release actually falls. Release the wrong note
    /// and the music is gone and stays gone.
    /// </para>
    /// </summary>
    [Fact]
    public void AnEffectEndingLetsGoOfItsOwnNotesAndNoOthers()
    {
        var mixer = new Mixer(8000);

        // A recording that goes round and round, so the note sounds for as long as it is
        // held rather than running out on its own.
        var held = new Voice([.. Enumerable.Range(0, 64).Select(i => (sbyte)(i % 2 == 0 ? 100 : -100))], 8000, true, 0);

        // Sustain full, release 32 — held for ever until let go, and gone within a few steps
        // once it is.
        var instrument = new Instrument(held, 60, 255, 255, 255, 32);

        SongPlayer LongNote() =>
            new(
                [
                    new Track(
                    [
                        new SequenceEvent(0, 0xD4, SequenceCommand.NoteOn, [60, 127]),
                        new SequenceEvent(3, 0xB1, SequenceCommand.End, []),
                    ]),
                ],
                [instrument],
                mixer);

        var box = new Jukebox(song => song == 1 ? LongNote() : Effect(mixer), mixer);

        box.Play(1);
        box.Render(2000);

        Assert.Contains(box.Render(400), sample => sample != 0);

        box.PlayOver(2, group: 0);

        // Long enough for the effect to finish and be let go of, and for a wrongly released
        // music note to have faded to nothing several times over.
        box.Render(40_000);

        Assert.Equal(0, box.Effects);

        Assert.Contains(
            box.Render(400),
            sample => sample != 0);
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
        var box = new Jukebox(song => SongLoader.Load(rom, tree, song, mixer), mixer);

        box.Play(0);

        Assert.False(box.IsSilent);

        var heard = new List<short>();

        for (int piece = 0; piece < 40; piece++) heard.AddRange(box.Render(400));

        Assert.Contains(heard, sample => sample != 0);
    }
}
