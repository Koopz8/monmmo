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
public sealed class Jukebox(Func<int, SongPlayer?> load)
{
    private readonly Func<int, SongPlayer?> _load = load;

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
    /// Samples, however many were asked for.
    /// <para>
    /// Always exactly that many, even with nothing playing. A sound card asks for a fixed
    /// buffer and does not care that the game has nothing to say; handing back a short one
    /// would be handing back a click.
    /// </para>
    /// </summary>
    public short[] Render(int samples)
    {
        int wanted = Math.Max(0, samples);

        if (_player is null) return new short[wanted];

        short[] played = _player.Render(wanted);

        if (played.Length == wanted) return played;

        var padded = new short[wanted];

        played.CopyTo(padded, 0);

        return padded;
    }
}
