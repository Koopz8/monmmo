namespace PokeMmo.Core.Sound;

/// <summary>
/// What is playing, and what should be.
/// <para>
/// The sound work had a reader, a decoder, a mixer and a performer, and every one of them was
/// tested and none of them was ever asked for a sample. This is the part that decides — a map
/// names a song by number, and this is what turns a number arriving into a song starting, a
/// song continuing, or nothing happening at all.
/// </para>
/// <para>
/// It knows nothing about a cartridge and nothing about a sound card. What it takes is a way
/// of turning a song number into something that performs, and what it gives back is samples.
/// That is what lets it be tested at all: the two ends of it are the two things that cannot
/// be, and everything between them is here.
/// </para>
/// </summary>
public sealed class Jukebox(Func<int, SongPlayer?> load, Mixer mixer)
{
    private readonly Func<int, SongPlayer?> _load = load;
    private readonly Mixer _mixer = mixer;

    private SongPlayer? _player;

    /// <summary>
    /// The one-off songs, by the group their table entry names.
    /// <para>
    /// <b>Why a group and not a list.</b> A sound effect in this engine is a song in the same
    /// table as the music, and the eight-byte table entry carries a small number written
    /// twice — <b>read</b>, and the strongest thing in the entry after the pointer itself.
    /// The driver has several performers rather than one, and that number is understood to
    /// say which of them a song is for. <b>That reading is modelled</b>: nothing in the data
    /// says what the number means, only that it is there and that it repeats.
    /// </para>
    /// <para>
    /// What follows from it is the useful part. Two songs naming the same performer cannot
    /// both be on, so a second one arriving replaces the first — which is why a door opening
    /// twice in quick succession is one noise rather than two, and why a menu beep does not
    /// pile up into a drone when somebody holds a direction. How many may overlap is
    /// therefore not a number invented here; it is however many performers the cartridge
    /// names, counted from the table.
    /// </para>
    /// </summary>
    private readonly Dictionary<int, SongPlayer> _effects = [];

    /// <summary>
    /// Which song was last asked for, whether or not it turned out to exist.
    /// <para>
    /// Nought is a real song number on this cartridge, so "nothing asked for yet" has to be
    /// something else.
    /// </para>
    /// </summary>
    private int _asked = Nothing;

    /// <summary>What a song number is not.</summary>
    public const int Nothing = -1;

    /// <summary>The song currently asked for, which is not always one that is sounding.</summary>
    public int Playing => _asked;

    /// <summary>True when nothing is being performed, whether or not something was asked for.</summary>
    public bool IsSilent => _player is null;

    /// <summary>What is being performed, for anybody who wants to ask it how it is going.</summary>
    public SongPlayer? Performing => _player;

    /// <summary>
    /// How many times a song has actually been fetched, which is the number that says whether
    /// asking for the same one twice did anything.
    /// </summary>
    public int Fetches { get; private set; }

    /// <summary>
    /// Put a song on, if it is not already on.
    /// <para>
    /// The same song asked for twice carries on rather than starting again. That is not an
    /// optimisation: a building's rooms are separate maps naming the same music, so a player
    /// walking through a door would hear the tune restart from the beginning every few steps.
    /// </para>
    /// <para>
    /// A song this cartridge does not have leaves silence, and is remembered as asked for so
    /// that it is not looked for again on every frame the player stands there.
    /// </para>
    /// </summary>
    public void Play(int song)
    {
        if (song == _asked) return;

        _asked = song;

        if (song == Nothing)
        {
            _player = null;

            return;
        }

        Fetches++;

        _player = _load(song);
    }

    /// <summary>Silence, and a clean slate — the next request starts again whatever it is.</summary>
    public void Stop()
    {
        _player = null;
        _asked = Nothing;

        foreach (SongPlayer effect in _effects.Values) effect.Silence();

        _effects.Clear();
    }

    /// <summary>
    /// How many one-off songs are being performed over the music.
    /// <para>
    /// Nought most of the time, which is what makes it worth reporting: an effect that starts
    /// and never finishes is a performer that stays here for ever, and this is the number
    /// that says so.
    /// </para>
    /// </summary>
    public int Effects => _effects.Count;

    /// <summary>
    /// How many one-off songs have been fetched, which says whether firing one did anything.
    /// </summary>
    public int EffectFetches { get; private set; }

    /// <summary>
    /// A song laid over the music rather than instead of it.
    /// <para>
    /// This is the thing the sound work did not have. A faint, a door, a healing machine, a
    /// menu beep — all of them are song numbers in the same table as the town themes, and
    /// this jukebox played one song at a time, so there was no way to sound one without
    /// stopping the music. Cries only worked because they are not songs at all: they go onto
    /// the mixer as a raw recording and bypass the whole performer.
    /// </para>
    /// <para>
    /// Unlike <see cref="Play"/>, the same song asked for twice starts again. That is not an
    /// inconsistency — it is the same rule applied to a different thing. Music asked for
    /// twice carries on because a building's rooms are separate maps naming one theme; an
    /// effect asked for twice is somebody pressing the button twice, and hearing nothing the
    /// second time is the bug.
    /// </para>
    /// </summary>
    /// <param name="song">The song number, from the same table the music comes from.</param>
    /// <param name="group">
    /// Which performer it is for, off its table entry. Anything already on that performer
    /// stops. A caller with no group to give may pass the song number itself, which makes
    /// every effect its own performer and lets them all overlap.
    /// </param>
    /// <returns>Whether a song was found and started.</returns>
    public bool PlayOver(int song, int group)
    {
        if (song == Nothing) return false;

        EffectFetches++;

        SongPlayer? started = _load(song);

        // A song this cartridge does not have leaves whatever was on that performer alone.
        // Silencing it would mean a missing effect could cut short a real one, which is a
        // worse failure than a missing effect.
        if (started is null) return false;

        Replace(group, started);

        return true;
    }

    private void Replace(int group, SongPlayer started)
    {
        if (_effects.TryGetValue(group, out SongPlayer? was)) was.Silence();

        _effects[group] = started;
    }

    /// <summary>
    /// A one-off recording, over the top of whatever is playing.
    /// <para>
    /// Over the top rather than instead of. A creature's cry is the sound the game makes when
    /// it comes out, and the music does not stop for it — a jukebox that swapped one for the
    /// other would silence a town every time somebody sent something out.
    /// </para>
    /// <para>
    /// It goes straight onto the mixer, which is the same mixer the song is being performed
    /// onto. The mixer has always been able to hold more than one thing at once; this is the
    /// first caller that wanted it to.
    /// </para>
    /// </summary>
    public void PlayRecording(Voice voice, int rate)
    {
        if (voice.Audio.Length == 0) return;

        // Held at full and let go at once, which is what a recording of a whole noise wants:
        // the shaping is already in the recording, and putting an envelope over it would be
        // shaping it twice.
        _mixer.Play(voice, Math.Max(1, rate), new Envelope(255, 255, 255, 255));
    }

    /// <summary>
    /// Samples, however many were asked for.
    /// <para>
    /// Always exactly that many, even with nothing playing. A sound card asks for a fixed
    /// buffer and does not care that the game has nothing to say; handing back a short one
    /// would be handing back a click.
    /// </para>
    /// <para>
    /// With no song on, the mixer is still turned over. Otherwise a cry on a map with no
    /// music would be put onto a mixer nobody was asking for samples from, and would be
    /// silent — which is exactly the sort of thing that is only ever found in the one place
    /// it happens.
    /// </para>
    /// </summary>
    public short[] Render(int samples)
    {
        int wanted = Math.Max(0, samples);

        var output = new short[wanted];

        for (int i = 0; i < wanted; i++)
        {
            _player?.Advance();

            // Taken as a list because a finished effect is dropped from the dictionary in
            // this same pass, and a sample is a bad moment to be enumerating something that
            // is changing.
            if (_effects.Count > 0) AdvanceEffects();

            // Once, however many performers there are. This is the whole of what the split
            // between advancing and mixing bought.
            output[i] = _mixer.Next();
        }

        return output;
    }

    /// <summary>
    /// Every one-off song forward by one sample, and the ones that have finished let go.
    /// <para>
    /// Let go rather than cut off. The performer stops putting notes on the moment its tracks
    /// run out, but the last note it started is still sounding, and a recording has a tail —
    /// so releasing it lets it fade at the rate its own instrument says. Removing it without
    /// releasing would leave a note on the mixer with nothing to stop it, and on a looping
    /// recording with a sustain that never falls that is a drone nothing can silence.
    /// </para>
    /// </summary>
    private void AdvanceEffects()
    {
        List<int>? done = null;

        foreach ((int group, SongPlayer effect) in _effects)
        {
            effect.Advance();

            if (!effect.IsFinished) continue;

            effect.Silence();

            (done ??= []).Add(group);
        }

        if (done is null) return;

        foreach (int group in done) _effects.Remove(group);
    }
}
