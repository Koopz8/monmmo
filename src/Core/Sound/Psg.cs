namespace PokeMmo.Core.Sound;

/// <summary>
/// The four channels that are shapes rather than recordings.
/// <para>
/// Two square waves, one programmable waveform, and one noise generator. They predate the
/// recorded voices by a decade — this is the Game Boy's sound hardware, still present and
/// still used, and a great deal of what a Game Boy Advance game sounds like comes out of it.
/// </para>
/// <para>
/// <b>Modelled.</b> These are circuits, not data: nothing about them is on the cartridge
/// except which one an instrument asks for and with what settings. What is <em>read</em> is
/// the instrument entry that selects them — the duty cycle, the waveform, the envelope.
/// </para>
/// </summary>
public static class Psg
{
    /// <summary>
    /// The four duty cycles a square channel can have, as eighths of the period spent high.
    /// <b>Read</b>, in the sense that the instrument entry names one of these four by number
    /// — the fractions themselves are the hardware's.
    /// </summary>
    public static readonly IReadOnlyList<int> Duties = [1, 2, 4, 6];

    /// <summary>How loud a shape channel is at full. <b>Modelled.</b></summary>
    public const int Amplitude = 96;
}

/// <summary>One of the two square-wave channels.</summary>
public sealed class SquareVoice
{
    private readonly int _highEighths;
    private readonly int _period;

    private int _at;

    /// <param name="duty">Which of the four duty cycles, nought to three.</param>
    /// <param name="frequency">In hertz.</param>
    /// <param name="rate">The mixer's rate.</param>
    public SquareVoice(int duty, int frequency, int rate)
    {
        _highEighths = Psg.Duties[Math.Clamp(duty, 0, Psg.Duties.Count - 1)];

        // In samples. One at the very least, because a period of nought is a division by
        // nought and a frequency above the mixing rate is a note nobody can hear anyway.
        _period = Math.Max(1, rate / Math.Max(1, frequency));
    }

    /// <summary>The next sample: high for part of the period, low for the rest.</summary>
    public int Next()
    {
        int eighth = _at * 8 / _period;

        _at = (_at + 1) % _period;

        return eighth < _highEighths ? Psg.Amplitude : -Psg.Amplitude;
    }
}

/// <summary>
/// The programmable waveform channel: thirty-two four-bit steps, played round and round.
/// <para>
/// The thirty-two steps are <b>read</b> — they are the sixteen bytes an instrument entry
/// points at. How they are played is the hardware's, and modelled.
/// </para>
/// </summary>
public sealed class WaveVoice
{
    /// <summary>Steps in one turn of the wave.</summary>
    public const int Steps = 32;

    private readonly int[] _shape;
    private readonly int _period;

    private int _at;

    public WaveVoice(ReadOnlySpan<byte> packed, int frequency, int rate)
    {
        _shape = new int[Steps];

        // Two steps to a byte, high nibble first. Centred on nought, because the hardware's
        // nought to fifteen is a level rather than a signed sample and mixing it unshifted
        // would put a large constant offset into everything.
        for (int i = 0; i < Steps; i++)
        {
            int at = i / 2;

            int nibble = at < packed.Length
                ? (i % 2 == 0 ? packed[at] >> 4 : packed[at] & 0x0F)
                : 0;

            _shape[i] = (nibble - 8) * Psg.Amplitude / 8;
        }

        _period = Math.Max(Steps, rate / Math.Max(1, frequency));
    }

    public int Next()
    {
        int step = _at * Steps / _period;

        _at = (_at + 1) % _period;

        return _shape[Math.Clamp(step, 0, Steps - 1)];
    }
}

/// <summary>
/// The noise channel: a shift register whose bottom bit is the sound.
/// <para>
/// This is the one channel whose output is genuinely determined rather than chosen — the
/// same register produces the same "random" sequence every time, which is why a drum sounds
/// like the same drum twice. Modelled, and deterministic on purpose: two players hearing the
/// same fight should hear the same cymbal.
/// </para>
/// </summary>
public sealed class NoiseVoice
{
    /// <summary>Where the register starts. Anything but nought, which would never change.</summary>
    private const int Seed = 0x7FFF;

    private readonly int _period;
    private readonly bool _short;

    private int _register = Seed;
    private int _at;
    private int _sample = Psg.Amplitude;

    /// <param name="narrow">
    /// The seven-bit register rather than the fifteen-bit one — a much shorter cycle, which
    /// is heard as a metallic buzz rather than as a hiss.
    /// </param>
    public NoiseVoice(int frequency, int rate, bool narrow = false)
    {
        _period = Math.Max(1, rate / Math.Max(1, frequency));
        _short = narrow;
    }

    public int Next()
    {
        if (++_at >= _period)
        {
            _at = 0;

            int feedback = (_register ^ (_register >> 1)) & 1;

            _register >>= 1;
            _register |= feedback << 14;

            if (_short)
            {
                _register &= ~(1 << 6);
                _register |= feedback << 6;
            }

            _sample = (_register & 1) == 0 ? Psg.Amplitude : -Psg.Amplitude;
        }

        return _sample;
    }
}
