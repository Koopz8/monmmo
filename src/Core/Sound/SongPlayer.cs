namespace PokeMmo.Core.Sound;

/// <summary>One instrument, as far as playing a note needs to know.</summary>
/// <param name="Voice">The recording, already unpacked.</param>
/// <param name="Key">
/// The key the recording sounds at its own rate. Every other key is that rate scaled.
/// </param>
public sealed record Instrument(Voice Voice, int Key, byte Attack, byte Decay, byte Sustain, byte Release)
{
    /// <summary>A silent instrument, for a slot that names nothing playable.</summary>
    public static readonly Instrument Nothing = new(Voice.Silence(), 60, 255, 255, 255, 255);
}

/// <summary>One track's worth of commands, in the order they were read.</summary>
public sealed record Track(IReadOnlyList<SequenceEvent> Events);

/// <summary>
/// Performing a song: turning parsed commands into notes on a mixer, in time.
/// <para>
/// This is the piece that was missing. A reader turns bytes into commands and a mixer turns
/// notes into samples, and between them sat nothing at all — the two halves of the sound work
/// could each be right and the project would still be silent.
/// </para>
/// <para>
/// What it does is keep a cursor per track, each with a number of ticks left to wait, and
/// advance them all together. A tick is the sequencer's unit of time; how long one lasts is
/// the tempo, which a track can change mid-song and which therefore cannot be a constant
/// anywhere.
/// </para>
/// <para>
/// <b>Modelled.</b> The command bytes and their arguments are read; what a sequencer does with
/// them is the driver's code. The length table below is the format's own and is the one part
/// of this that would be an invention if it were wrong.
/// </para>
/// </summary>
public sealed class SongPlayer
{
    /// <summary>
    /// How long each wait and each note lasts, in ticks.
    /// <para>
    /// Forty-nine values, and the spacing is the interesting part: whole numbers up to
    /// twenty-four and then only the lengths a piece of music actually uses. That is why it
    /// is a table rather than arithmetic — the gaps are deliberate, and a formula fitted to
    /// the first half would be wrong for every value after it.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<int> Lengths =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
        24, 28, 30, 32, 36, 40, 42, 44, 48, 52, 54, 56, 60, 64, 66, 68, 72, 76, 78, 80, 84,
        88, 90, 92, 96,
    ];

    /// <summary>Ticks in one beat. The format's own figure, and it does not vary.</summary>
    public const int TicksPerBeat = 24;

    /// <summary>The tempo a song runs at until a track says otherwise. <b>Modelled.</b></summary>
    public const int DefaultBeatsPerMinute = 150;

    private const byte FirstWait = 0x80;
    private const byte LastWait = 0xB0;
    private const byte Tempo = 0xBB;
    private const byte SetInstrument = 0xBD;
    private const byte SetVolume = 0xBE;
    private const byte NoteOff = 0xCE;
    private const byte FirstNote = 0xCF;

    /// <summary>The key a recording is written to sound at. <b>Modelled.</b></summary>
    private const int MiddleC = 60;

    private sealed class Cursor(Track track)
    {
        public Track Track { get; } = track;

        /// <summary>
        /// Where each command sat on the cartridge, so a jump can be followed.
        /// <para>
        /// A jump names a place rather than a step number, and the reader kept the place
        /// every command came from. Built once per track rather than searched each time,
        /// because a song jumps back on every repeat and a track can be twenty thousand
        /// commands long.
        /// </para>
        /// </summary>
        public Dictionary<int, int> ByOffset { get; } = Build(track);

        private static Dictionary<int, int> Build(Track track)
        {
            var found = new Dictionary<int, int>();

            for (int i = 0; i < track.Events.Count; i++)
                found.TryAdd(track.Events[i].Offset, i);

            return found;
        }

        public int At { get; set; }

        public int Waiting { get; set; }

        public int Instrument { get; set; }

        public int Volume { get; set; } = 127;

        /// <summary>What this track is currently sounding, so it can be let go.</summary>
        public PlayingNote? Sounding { get; set; }

        public bool Finished { get; set; }
    }

    private readonly List<Cursor> _cursors;
    private readonly IReadOnlyList<Instrument> _voicegroup;
    private readonly Mixer _mixer;

    private int _beatsPerMinute = DefaultBeatsPerMinute;
    private int _untilNextTick;

    public SongPlayer(IEnumerable<Track> tracks, IReadOnlyList<Instrument> voicegroup, Mixer mixer)
    {
        _cursors = [.. tracks.Select(t => new Cursor(t))];
        _voicegroup = voicegroup;
        _mixer = mixer;

        _untilNextTick = SamplesPerTick;
    }

    /// <summary>How many output samples one tick lasts, at the tempo in force.</summary>
    private int SamplesPerTick =>
        Math.Max(1, _mixer.Rate * 60 / Math.Max(1, _beatsPerMinute * TicksPerBeat));

    /// <summary>
    /// How many tracks are being performed, which is not always how many the song has.
    /// <para>
    /// A track this reader could not follow to an end is dropped rather than performed, so
    /// this number is the answer to "how much of the song survived" — and a caller that
    /// wants to know had no way to ask.
    /// </para>
    /// </summary>
    public int TrackCount => _cursors.Count;

    /// <summary>
    /// How many instruments this song can name. Every slot counts, including the silent
    /// ones — a track asks by position, so a hole would shift every number after it.
    /// </summary>
    public int InstrumentCount => _voicegroup.Count;

    /// <summary>True once every track has run out.</summary>
    public bool IsFinished => _cursors.All(c => c.Finished);

    /// <summary>How many tracks have run out, which is not always all or none.</summary>
    public int Ran => _cursors.Count(c => c.Finished);

    /// <summary>
    /// How many ticks have been taken, how many commands run, and how many notes begun.
    /// <para>
    /// A song stuck on one note sounds from outside exactly like a song playing quietly,
    /// and these three numbers say which of the ways it is stuck. No ticks means the clock;
    /// ticks but no commands means every track is waiting; commands but no notes means the
    /// track is running settings and nothing else.
    /// </para>
    /// </summary>
    public int Ticks { get; private set; }

    public int Commands { get; private set; }

    public int Notes { get; private set; }

    /// <summary>The tempo in force, which a track may change part-way through.</summary>
    public int BeatsPerMinute => _beatsPerMinute;

    /// <summary>
    /// What the instruments this song draws on are made of, said out loud.
    /// <para>
    /// A sequencer running correctly and a song that sounds like one held note are the same
    /// thing when the notes never stop. The envelope decides when one stops, and its four
    /// bytes are the least confirmed numbers in the whole sound chain — read from four
    /// offsets nobody has ever checked against a sound. A recording that loops and an
    /// envelope that never decays is a drone, whatever the sequencer does above it.
    /// </para>
    /// </summary>
    public string Instruments()
    {
        var loops = 0;
        var sustained = 0;
        var silent = 0;

        foreach (Instrument instrument in _voicegroup)
        {
            if (instrument.Voice.Audio.Length == 0) { silent++; continue; }

            if (instrument.Voice.Loops) loops++;
            if (instrument.Sustain >= 250 && instrument.Release >= 250) sustained++;
        }

        return $"{_voicegroup.Count} instruments, {silent} silent, {loops} loop, "
               + $"{sustained} never fade — "
               + string.Join(
                   " ",
                   _voicegroup.Where(i => i.Voice.Audio.Length > 0).Take(4)
                       .Select(i => $"[{i.Voice.Rate}Hz key{i.Key} a{i.Attack} d{i.Decay} s{i.Sustain} r{i.Release}]"));
    }

    /// <summary>
    /// Fills a buffer, performing the song as it goes.
    /// <para>
    /// The tick clock is checked per sample rather than per buffer, so a buffer of any length
    /// gives the same performance. A sequencer that only advanced between buffers would play
    /// a song differently depending on how often somebody asked it for audio, which is the
    /// kind of bug that only appears on somebody else's machine.
    /// </para>
    /// </summary>
    public short[] Render(int samples)
    {
        var output = new short[Math.Max(0, samples)];

        for (int i = 0; i < output.Length; i++)
        {
            if (--_untilNextTick <= 0)
            {
                Tick();

                _untilNextTick = SamplesPerTick;
            }

            short[] one = _mixer.Render(1);

            output[i] = one.Length > 0 ? one[0] : (short)0;
        }

        return output;
    }

    /// <summary>One tick of the sequencer: every track that is not waiting takes its turn.</summary>
    private void Tick()
    {
        Ticks++;

        foreach (Cursor cursor in _cursors)
        {
            if (cursor.Finished) continue;

            if (cursor.Waiting > 0)
            {
                cursor.Waiting--;

                continue;
            }

            // A track runs commands until one of them asks for time. Bounded, because a
            // track of nothing but settings would otherwise spin for ever inside one tick —
            // and a song that hangs the client is worse than a song that stops.
            for (int steps = 0; steps < 64 && cursor.Waiting == 0 && !cursor.Finished; steps++)
                Step(cursor);
        }
    }

    private void Step(Cursor cursor)
    {
        if (cursor.At >= cursor.Track.Events.Count)
        {
            cursor.Finished = true;

            return;
        }

        SequenceEvent next = cursor.Track.Events[cursor.At++];

        Commands++;

        switch (next.Command)
        {
            case SequenceCommand.End:
                cursor.Finished = true;

                break;

            case SequenceCommand.Wait:
                cursor.Waiting = LengthOf(next.Opcode - FirstWait);

                break;

            case SequenceCommand.NoteOff:
                cursor.Sounding?.Release();
                cursor.Sounding = null;

                break;

            case SequenceCommand.NoteOn:
                Play(cursor, next);

                break;

            case SequenceCommand.Setting:
                Set(cursor, next);

                break;

            case SequenceCommand.Goto:
                Jump(cursor, next);

                break;

            // The calls and the returns were already followed by the reader, which flattened
            // the track into the order it actually runs in. There is nothing left here to
            // follow, and an unknown byte is one this reader did not account for rather than
            // one it should act on.
            default:
                break;
        }
    }

    /// <summary>
    /// A jump backwards, which is how a piece of music repeats.
    /// <para>
    /// This is the one command the reader could not flatten: following it would have meant
    /// reading the same bytes for ever, so the reader stopped there and left the place it
    /// was going. The place is a cartridge offset, and every command kept the offset it came
    /// from, so the two meet here.
    /// </para>
    /// <para>
    /// It matters more than it looks. A song that repeats from its own loop point plays the
    /// way it was written; one that starts again from the top plays its introduction every
    /// time round, which is wrong in a way anybody who has heard the tune will notice
    /// immediately. Read, rather than modelled — the loop point is the cartridge's.
    /// </para>
    /// </summary>
    private static void Jump(Cursor cursor, SequenceEvent jump)
    {
        // A jump this reader did not resolve, or one landing where no command was recorded,
        // ends the track. Guessing at where it meant would be performing an invention.
        if (jump.Target < 0 || !cursor.ByOffset.TryGetValue(jump.Target, out int index))
        {
            cursor.Finished = true;

            return;
        }

        cursor.At = index;
    }

    private void Set(Cursor cursor, SequenceEvent setting)
    {
        if (setting.Arguments.Count == 0) return;

        switch (setting.Opcode)
        {
            case Tempo:
                // The record carries half of it, which is how a byte holds three hundred.
                _beatsPerMinute = Math.Max(1, setting.Arguments[0] * 2);

                break;

            case SetInstrument:
                cursor.Instrument = setting.Arguments[0];

                break;

            case SetVolume:
                cursor.Volume = setting.Arguments[0];

                break;
        }
    }

    private void Play(Cursor cursor, SequenceEvent note)
    {
        if (note.Arguments.Count == 0) return;

        Notes++;

        // A track sounds one note at a time, so whatever was ringing is let go first. This
        // engine's mixer would happily hold both, and a track that never released would fill
        // every voice it had within a few bars.
        cursor.Sounding?.Release();

        Instrument instrument = cursor.Instrument >= 0 && cursor.Instrument < _voicegroup.Count
            ? _voicegroup[cursor.Instrument]
            : Instrument.Nothing;

        int key = note.Arguments[0];
        int loudness = note.Arguments.Count > 1 ? note.Arguments[1] : 127;

        var envelope = new Envelope(
            instrument.Attack, instrument.Decay, instrument.Sustain, instrument.Release);

        cursor.Sounding = _mixer.Play(
            instrument.Voice,
            RateFor(instrument, key),
            envelope,
            Math.Clamp(loudness * cursor.Volume / 127, 0, 255));

        // How long it is held. Nought means it rings until something stops it, which is what
        // the one note command below the rest is for.
        int held = LengthOf(note.Opcode - FirstNote);

        if (held > 0) cursor.Waiting = 0;
    }

    /// <summary>
    /// What rate a recording has to be played at to sound as a given key.
    /// <para>
    /// A semitone is the twelfth root of two, and twelve semitones double it. Computed rather
    /// than tabled because the arithmetic is exact and a table of a hundred and twenty-eight
    /// entries would be a hundred and twenty-eight chances to mistype one.
    /// </para>
    /// </summary>
    private static int RateFor(Instrument instrument, int key) =>
        Math.Max(1, (int)(instrument.Voice.Rate * Math.Pow(2, (key - instrument.Key) / 12.0)));

    /// <summary>How many ticks a length index comes to, or nought when it is off the table.</summary>
    public static int LengthOf(int index) =>
        index >= 0 && index < Lengths.Count ? Lengths[index] : 0;
}
