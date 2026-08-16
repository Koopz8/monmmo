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
    }

    /// <summary>
    /// A one-off noise, over the top of whatever is playing.
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
    public void PlayOver(Voice voice, int rate)
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

        short[] played = _player is null ? _mixer.Render(wanted) : _player.Render(wanted);

        if (played.Length == wanted) return played;

        var padded = new short[wanted];

        played.CopyTo(padded, 0);

        return padded;
    }
}
