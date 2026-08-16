using PokeMmo.Core.Sound;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Sound;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The sound card, and the only part of the sound work that cannot be tested.
/// <para>
/// Everything else — finding the tables, reading a track, unpacking a recording, performing a
/// song, deciding what should be on — is a library with tests. This is a buffer, a poll and a
/// device handle, and it is kept this small deliberately: the rules live in
/// <see cref="Jukebox"/> where they can be broken on purpose.
/// </para>
/// <para>
/// It reads the player's own cartridge, on the player's own machine, and writes nothing down.
/// The recordings exist only while a song is playing.
/// </para>
/// </summary>
public sealed class Speakers : IDisposable
{
    /// <summary>
    /// How many samples a second. <b>Modelled.</b>
    /// <para>
    /// The cartridge's own driver runs at a rate its header names and the recordings carry
    /// their own rates, which the mixer resamples from. This is the rate the sound card is
    /// asked for, and it is a decision about this machine rather than a fact about that file.
    /// </para>
    /// </summary>
    public const int Rate = 32768;

    /// <summary>
    /// How many samples are handed over at a time. Small enough that a song changing on a
    /// doorstep is not audibly late, large enough that a frame's worth of work is not spent
    /// filling it.
    /// </summary>
    private const int BufferSamples = 2048;

    private readonly Jukebox _box;
    private readonly Mixer _mixer;
    private readonly CryLibrary _cries;

    /// <summary>
    /// Which performer each song belongs to, off its own table entry.
    /// <para>
    /// The number the eight-byte entry writes twice. It decides which one-off songs can
    /// overlap and which replace each other, and it is read rather than decided here — a
    /// song this cartridge does not list falls back to being its own performer, which is the
    /// permissive answer and the one that cannot silence something real.
    /// </para>
    /// </summary>
    private readonly IReadOnlyList<SongTableEntry> _table;

    /// <summary>
    /// The song a fight is playing, or nothing. Held here rather than pushed into the jukebox
    /// because the map's own song arrives every frame — see <see cref="Play"/>.
    /// </summary>
    private int _fight = Jukebox.Nothing;
    private readonly short[] _buffer = new short[BufferSamples];
    private readonly bool _ready;

    private AudioStream _stream;
    private bool _closed;

    /// <summary>
    /// Opens the sound card and prepares to play out of the given cartridge.
    /// <para>
    /// A machine with no sound card is a machine that still plays this game. Every method
    /// below does nothing at all in that case, rather than the client refusing to start
    /// because nobody plugged in speakers.
    /// </para>
    /// </summary>
    /// <param name="entryFor">
    /// Which entry of the cry table each species uses. Not the species number: a cartridge
    /// names more species than it has cries, because a block in the middle of the numbering
    /// carries no creature. See <see cref="CryIndex"/>.
    /// </param>
    public Speakers(
        Rom rom, SoundTreeResult tree, CryTableResult? cries, IReadOnlyDictionary<int, int>? entryFor)
    {
        var mixer = new Mixer(Rate);

        _mixer = mixer;

        _box = new Jukebox(song => SongLoader.Load(rom, tree, song, mixer), mixer);
        _cries = new CryLibrary(rom, tree.Samples, cries, entryFor);
        _table = tree.Table;

        Raylib.InitAudioDevice();

        _ready = Raylib.IsAudioDeviceReady();

        if (!_ready) return;

        // How big the card's own buffer is, said before the stream is made rather than
        // after. Without this raylib picks a size of its own and refuses every write that is
        // not exactly that — once a frame, for as long as the game is running, which is what
        // a console full of "attempting to write too many frames to buffer" is.
        Raylib.SetAudioStreamBufferSizeDefault(BufferSamples);

        // One channel, sixteen bits a sample, which is what the mixer gives back.
        _stream = Raylib.LoadAudioStream(Rate, 16, 1);

        Raylib.PlayAudioStream(_stream);
    }

    /// <summary>Whether there is anything to play out of.</summary>
    public bool IsReady => _ready;

    /// <summary>What is on, for anybody who wants to say so on a screen.</summary>
    public int Playing => _box.Playing;

    /// <summary>
    /// Puts a map's music on, or carries on if it is already the one playing.
    /// <para>
    /// Called every frame rather than on the frames a map changes, because the rule about a
    /// song already playing lives in the jukebox and is tested there. A caller that had to
    /// remember whether the map had changed would be a second copy of that rule.
    /// </para>
    /// </summary>
    public void Play(int song)
    {
        if (!_ready) return;

        // A fight's music outranks the map's. The map's number arrives every frame, so this
        // has to be decided here rather than by whoever starts the fight — a caller that
        // pushed a song in would have it overwritten before the next buffer.
        _box.Play(_fight == Jukebox.Nothing ? song : _fight);
    }

    /// <summary>
    /// A one-off song over the top of whatever is playing: a door, a faint, a healing
    /// machine, a menu beep.
    /// <para>
    /// All of those are song numbers in the same table as the town themes, which is why this
    /// takes a number and not a recording. Which performer it goes to is read off the song's
    /// own table entry.
    /// </para>
    /// </summary>
    /// <returns>Whether this cartridge had the song.</returns>
    public bool Effect(int song)
    {
        if (!_ready || song == Jukebox.Nothing) return false;

        return _box.PlayOver(song, GroupOf(song));
    }

    /// <summary>
    /// Which performer a song names, or the song itself when this cartridge does not list it.
    /// <para>
    /// Falling back to the song number makes an unlisted song its own performer, so it
    /// overlaps everything instead of replacing something real. The permissive direction is
    /// the right one: an effect too many is a noise, an effect cut off is a bug.
    /// </para>
    /// </summary>
    private int GroupOf(int song) =>
        song >= 0 && song < _table.Count ? _table[song].Group : song;

    /// <summary>
    /// Puts a fight's music on, and holds it against the map's until the fight is over.
    /// <para>
    /// <see cref="Jukebox.Nothing"/> means this build has no song for that sort of fight, and
    /// the map's music carries on — which is what happened before any of this and is wrong in
    /// a way a player can hear. It is left wrong and counted rather than filled in with a
    /// number nobody can trace to a byte. See <see cref="BattleMusic"/>.
    /// </para>
    /// </summary>
    public void Fight(int song)
    {
        if (!_ready) return;

        _fight = song;

        if (song != Jukebox.Nothing) _box.Play(song);
    }

    /// <summary>The fight is over; the next frame's map song takes over again.</summary>
    public void FightOver()
    {
        if (!_ready) return;

        _fight = Jukebox.Nothing;
    }

    /// <summary>Whether a fight's music is currently outranking the map's.</summary>
    public bool InAFight => _fight != Jukebox.Nothing;

    public void Stop()
    {
        if (_ready) _box.Stop();
    }

    /// <summary>
    /// The noise one creature makes, over the top of whatever is playing.
    /// <para>
    /// A creature with no noise on this cartridge makes none, rather than a click. The whole
    /// decision about which recording belongs to which creature was made before the window
    /// opened; this is the part that puts it on.
    /// </para>
    /// </summary>
    public void Cry(int species)
    {
        if (!_ready) return;

        if (_cries.For(species) is not { } voice) return;

        _box.PlayRecording(voice, voice.Rate);
    }

    /// <summary>How many creatures this cartridge has a noise for.</summary>
    public int CryCount => _cries.Count;

    private int _filled;
    private int _reported;
    private int _described = int.MinValue;

    /// <summary>
    /// A line about what the sequencer is doing, about once a second, or nothing.
    /// <para>
    /// A song that plays one note and never moves on sounds, from outside, exactly like a
    /// song playing quietly — and looks from the log exactly like a song playing correctly.
    /// The four numbers say which it is: no ticks means the clock is not running, ticks with
    /// no commands means every track is waiting for ever, commands with no notes means the
    /// tracks are running settings, and tracks that have all run out means it is over.
    /// </para>
    /// </summary>
    public string? TakeReport()
    {
        if (!_ready) return null;

        // Buffers rather than seconds, because this is the one clock the sound side has.
        // At 32768 samples a second and 2048 to a buffer, sixteen of them is a second.
        if (_filled - _reported < 16) return null;

        _reported = _filled;

        if (_box.Performing is not { } player)
            return $"sound: song {_box.Playing} is not playing — nothing was assembled for it";

        // Once per song rather than once a second: what it is made of does not change.
        if (_described != _box.Playing)
        {
            _described = _box.Playing;

            return $"sound: song {_box.Playing} — {player.Instruments()}";
        }

        // The tracks that have run out, against the tracks that were written to repeat. Those
        // two numbers used to be one, and a song whose tracks stop where the music loops
        // looked exactly like a song that had finished.
        string stopped = player.LoopedAndStopped > 0
            ? $"{player.Ran}/{player.TrackCount} run out INCLUDING {player.LoopedAndStopped} that loop"
            : $"{player.Ran}/{player.TrackCount} run out, {player.Looping} loop";

        return $"sound: song {_box.Playing}, {player.BeatsPerMinute}bpm, {player.Ticks} ticks, "
               + $"{player.Commands} commands, {player.Notes} notes, "
               + $"{stopped}, {_mixer.Sounding} sounding"
               + (_box.Effects > 0 ? $", {_box.Effects} over the top" : "");
    }

    /// <summary>
    /// Fills whatever the sound card has finished with. Called once a frame.
    /// <para>
    /// Nothing here decides anything. If the card is not hungry this returns without asking
    /// the jukebox for a sample, which is what keeps the song's clock and the card's clock
    /// the same clock.
    /// </para>
    /// </summary>
    public void Pump()
    {
        if (!_ready || _closed) return;

        // More than one, because a frame that took too long leaves the card with several
        // buffers to fill and filling one a frame would never catch up.
        for (int fills = 0; fills < 4 && Raylib.IsAudioStreamProcessed(_stream); fills++)
        {
            short[] samples = _box.Render(BufferSamples);

            samples.CopyTo(_buffer, 0);

            _filled++;

            unsafe
            {
                fixed (short* data = _buffer)
                {
                    Raylib.UpdateAudioStream(_stream, data, BufferSamples);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_closed) return;

        _closed = true;

        if (_ready)
        {
            Raylib.StopAudioStream(_stream);
            Raylib.UnloadAudioStream(_stream);
        }

        Raylib.CloseAudioDevice();
    }
}
