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
    private readonly CryLibrary _cries;
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

        _box = new Jukebox(song => SongLoader.Load(rom, tree, song, mixer), mixer);
        _cries = new CryLibrary(rom, tree.Samples, cries, entryFor);

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
        if (_ready) _box.Play(song);
    }

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

        _box.PlayOver(voice, voice.Rate);
    }

    /// <summary>How many creatures this cartridge has a noise for.</summary>
    public int CryCount => _cries.Count;

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
