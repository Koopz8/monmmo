namespace PokeMmo.Core.Sound;

/// <summary>
/// A recording, as something that can be played.
/// <para>
/// Every field is <b>read</b> — this is the sample header off the cartridge, plus the bytes
/// after it. The audio is signed and eight bits, which is the hardware's own format.
/// </para>
/// </summary>
public sealed record Voice(sbyte[] Audio, int Rate, bool Loops, int LoopStart)
{
    /// <summary>A short flat tone, for tests and for anything with nothing to play.</summary>
    public static Voice Silence(int length = 64) => new(new sbyte[length], 8000, false, 0);
}

/// <summary>
/// One note being played: a recording, a pitch, and an envelope.
/// <para>
/// The position is kept as a fixed-point number rather than a float. That is not
/// fastidiousness — the hardware steps through a recording by adding a fixed-point
/// increment and truncating, and the truncation is audible. Doing it in floating point and
/// rounding would produce a different sound that is arguably nicer and definitely not this.
/// </para>
/// </summary>
public sealed class PlayingNote
{
    /// <summary>Bits of fraction in the position. <b>Modelled.</b></summary>
    public const int FractionBits = 12;

    private const int One = 1 << FractionBits;

    private readonly Voice _voice;
    private readonly int _step;

    private long _position;

    public PlayingNote(Voice voice, int playbackRate, int outputRate, Envelope envelope, int volume = 255)
    {
        _voice = voice;
        Envelope = envelope;
        Volume = Math.Clamp(volume, 0, 255);

        // How far through the recording one output sample moves. A recording played at its
        // own rate steps exactly one, which is the identity this whole class is built round.
        _step = (int)((long)playbackRate * One / Math.Max(1, outputRate));
    }

    public Envelope Envelope { get; }

    public int Volume { get; }

    public bool IsFinished { get; private set; }

    /// <summary>
    /// The next sample out of this note, at the mixer's rate, before the envelope.
    /// <para>
    /// No interpolation between samples. The hardware does not interpolate, and the
    /// roughness that comes of not doing it is a large part of why this generation of games
    /// sounds the way it does.
    /// </para>
    /// </summary>
    public int Next()
    {
        if (IsFinished || _voice.Audio.Length == 0) return 0;

        long index = _position >> FractionBits;

        if (index >= _voice.Audio.Length)
        {
            if (!_voice.Loops || _voice.LoopStart >= _voice.Audio.Length)
            {
                IsFinished = true;

                return 0;
            }

            // Back to the loop point, keeping the fraction — dropping it would put a tiny
            // click at every repeat, once per loop, for as long as the note is held.
            long into = index - _voice.LoopStart;
            long span = _voice.Audio.Length - _voice.LoopStart;

            index = _voice.LoopStart + into % span;

            _position = (index << FractionBits) | (_position & (One - 1));
        }

        int sample = _voice.Audio[index];

        _position += _step;

        return sample;
    }

    /// <summary>Let this note go.</summary>
    public void Release() => Envelope.Release();
}

/// <summary>
/// Adding the playing notes together.
/// <para>
/// <b>Modelled throughout, and named as such.</b> The hardware mixes a fixed number of
/// voices into a small buffer at a rate the cartridge chooses, and the specifics — how many
/// voices, how the sum is clipped, how often envelopes move — are properties of the sound
/// driver's compiled code rather than of any table on the file.
/// </para>
/// <para>
/// What this is not: an emulator. It does not aim to produce the same bytes the hardware
/// would. It aims to play the notes the sequences actually contain, using the recordings and
/// envelopes actually on the cartridge, at a quality that is honest about being a model.
/// </para>
/// </summary>
public sealed class Mixer
{
    /// <summary>
    /// How many notes may sound at once. <b>Modelled.</b>
    /// <para>
    /// The driver has a fixed number of voices and steals the oldest when it runs out, which
    /// is why a busy passage in these games drops notes rather than getting louder. Twelve is
    /// the usual figure; the behaviour that matters is the stealing, not the number.
    /// </para>
    /// </summary>
    public const int MostNotes = 12;

    /// <summary>
    /// Engine steps a second — how often envelopes move. <b>Modelled.</b>
    /// <para>
    /// The driver runs its envelopes off the hardware's frame timer rather than off the
    /// mixing rate. Sixty is that timer.
    /// </para>
    /// </summary>
    public const int StepsPerSecond = 60;

    private readonly List<PlayingNote> _notes = [];
    private readonly int _rate;

    private int _untilNextStep;

    public Mixer(int outputRate = 13379)
    {
        _rate = Math.Max(1, outputRate);
        _untilNextStep = _rate / StepsPerSecond;
    }

    /// <summary>The rate this mixer produces samples at.</summary>
    public int Rate => _rate;

    /// <summary>How many notes are sounding.</summary>
    public int Sounding => _notes.Count;

    /// <summary>
    /// Starts a note. When there is no room, the oldest goes — which is what the driver does
    /// and is audible as dropped notes rather than as distortion.
    /// </summary>
    public PlayingNote Play(Voice voice, int playbackRate, Envelope envelope, int volume = 255)
    {
        if (_notes.Count >= MostNotes) _notes.RemoveAt(0);

        var note = new PlayingNote(voice, playbackRate, _rate, envelope, volume);

        _notes.Add(note);

        return note;
    }

    /// <summary>Lets every sounding note go.</summary>
    public void ReleaseAll()
    {
        foreach (PlayingNote note in _notes) note.Release();
    }

    /// <summary>
    /// Fills a buffer, and returns it. Values are signed sixteen-bit, which is what every
    /// audio device this client will meet expects.
    /// </summary>
    public short[] Render(int samples)
    {
        var output = new short[Math.Max(0, samples)];

        for (int i = 0; i < output.Length; i++)
        {
            if (--_untilNextStep <= 0)
            {
                _untilNextStep = _rate / StepsPerSecond;

                foreach (PlayingNote note in _notes) note.Envelope.Step();
            }

            int total = 0;

            for (int n = 0; n < _notes.Count; n++)
            {
                PlayingNote note = _notes[n];

                // An eight-bit sample, times a loudness out of 255, times a volume out of
                // 255. The two multiplications and the one shift are the whole of it.
                total += note.Next() * note.Envelope.Level * note.Volume >> 12;
            }

            // Clipped rather than scaled. Scaling the sum by the number of voices would make
            // a chord quieter than a single note, which is not what the hardware does and is
            // not what these games sound like.
            output[i] = (short)Math.Clamp(total, short.MinValue, short.MaxValue);

            _notes.RemoveAll(note => note.IsFinished || note.Envelope.IsFinished);
        }

        return output;
    }
}
