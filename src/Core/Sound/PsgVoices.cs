namespace PokeMmo.Core.Sound;

/// <summary>
/// The four circuit channels, as recordings.
/// <para>
/// <b>Why this shape.</b> A square wave is a recording of one cycle played round and round —
/// so a channel that is a circuit on the hardware can be a looping <see cref="Voice"/> here,
/// and then every piece already built works on it unchanged: the mixer resamples it for
/// pitch, the envelope shapes it, the sequencer starts and stops it. Nothing needed a second
/// path through the player, which is what the alternative would have cost.
/// </para>
/// <para>
/// This matters more than it sounds. A real cartridge's voicegroup measured 24 slots of which
/// 22 were circuits and 2 were recordings — so a build that plays only recordings plays a
/// twelfth of the music, which is why a town's theme came out as two looping samples and no
/// melody at all.
/// </para>
/// <para>
/// <b>Modelled.</b> That one cycle at this length is indistinguishable from the hardware's own
/// generator is a claim about a circuit rather than about the file. The duty fractions and the
/// wave channel's thirty-two steps are the format's; the sample counts below are chosen.
/// </para>
/// </summary>
public static class PsgVoices
{
    /// <summary>
    /// How many samples one cycle of a square wave is written as. <b>Modelled.</b>
    /// <para>
    /// A power of two, and large enough that the four duty fractions are exact whole numbers
    /// of samples rather than rounded ones — an eighth of 64 is 8, and an eighth of 60 is not
    /// a number of samples at all.
    /// </para>
    /// </summary>
    public const int SquareCycle = 64;

    /// <summary>The wave channel's own figure: thirty-two four-bit steps, and it does not vary.</summary>
    public const int WaveSteps = 32;

    /// <summary>
    /// Middle C, in cycles a second. The key every voice here is written to sound at, so that
    /// the player's own pitch arithmetic carries it to every other key.
    /// </summary>
    public const double MiddleC = 261.6255653;

    /// <summary>The key that pitch belongs to, which is the one the player scales from.</summary>
    public const int MiddleCKey = 60;

    /// <summary>
    /// How loud a circuit channel is against a recording. <b>Modelled.</b>
    /// <para>
    /// Below full, because a square wave at full is very much louder to the ear than a
    /// recording at full — it spends all its time at the extremes and a recording does not.
    /// </para>
    /// </summary>
    public const int Amplitude = 96;

    /// <summary>
    /// The four duty cycles, as how many eighths of a cycle the wave is high for.
    /// <para>
    /// The format's own four, and the order is theirs: an eighth, a quarter, a half, and
    /// three quarters. The last one is the second one upside down and sounds the same, which
    /// is a fact about the circuit rather than an oversight here.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<int> DutyEighths = [1, 2, 4, 6];

    /// <summary>
    /// The loudest a circuit channel's envelope counts to. <b>Read.</b>
    /// <para>
    /// Four bits, not eight. A real cartridge's town theme has twenty-four circuit slots and
    /// every one of them reads a sustain of exactly fifteen — twenty-four coincidences is not
    /// a coincidence, it is a scale. Read as if it were the recorded channels' nought-to-255
    /// it means six per cent, which is why the melody was there and inaudible.
    /// </para>
    /// </summary>
    public const int EnvelopeFull = 15;

    /// <summary>
    /// A circuit channel's four envelope bytes, on the scale the rest of this build uses.
    /// <para>
    /// <b>Modelled, and the nought is the interesting part.</b> On these channels nought does
    /// not mean "none of this stage", it means "instantly" for attack and release and "never"
    /// for decay — the opposite ends of the same byte. Read as an ordinary number a decay of
    /// nought collapses a note to its sustain in one step, which is exactly what was
    /// happening.
    /// </para>
    /// </summary>
    public static (byte Attack, byte Decay, byte Sustain, byte Release) Shaping(
        byte attack, byte decay, byte sustain, byte release)
    {
        const int Scale = 255 / EnvelopeFull;

        return (
            // Nought is immediate, which is what the player already does with a nought here.
            attack == 0 ? (byte)255 : Up(attack),

            // Nought is no decay at all: the note holds where the attack left it. A multiplier
            // of the full scale is what "leave it alone" is in the player's arithmetic.
            decay == 0 ? (byte)255 : (byte)(255 - Up(decay) + 1),

            Up(sustain),

            // Nought is immediate again, and the player's own floor takes it from there.
            release == 0 ? (byte)0 : (byte)(255 - Up(release) + 1));

        static byte Up(byte value) => (byte)Math.Min(255, Math.Max(1, (int)value) * Scale);
    }

    /// <summary>One of the two square channels, at the given duty.</summary>
    public static Voice Square(int duty)
    {
        int high = DutyEighths[Math.Clamp(duty, 0, DutyEighths.Count - 1)] * SquareCycle / 8;

        var cycle = new sbyte[SquareCycle];

        for (int i = 0; i < SquareCycle; i++)
            cycle[i] = (sbyte)(i < high ? Amplitude : -Amplitude);

        return new Voice(cycle, (int)(SquareCycle * MiddleC), true, 0);
    }

    /// <summary>
    /// The programmable channel, from the sixteen bytes that describe it.
    /// <para>
    /// Thirty-two steps of four bits each, high nibble first — the same order the packed
    /// recordings use, and for the same reason: this is how the hardware reads a byte.
    /// </para>
    /// </summary>
    public static Voice Wave(ReadOnlySpan<byte> packed)
    {
        var steps = new sbyte[WaveSteps];

        for (int i = 0; i < WaveSteps; i++)
        {
            int at = i / 2;

            int nibble = at < packed.Length
                ? (i % 2 == 0 ? packed[at] >> 4 : packed[at] & 0x0F)
                : 0;

            // Nought to fifteen becomes minus to plus, which is what a waveform is. Without
            // the shift every wave would sit above silence and click on every note.
            steps[i] = (sbyte)((nibble - 8) * Amplitude / 8);
        }

        return new Voice(steps, (int)(WaveSteps * MiddleC), true, 0);
    }

    /// <summary>
    /// How long a run of noise is written before it repeats. <b>Modelled.</b>
    /// <para>
    /// Long enough that the repeat is not a pitch anybody hears. Noise that loops over a
    /// short buffer stops being noise and becomes a buzz at the loop's own frequency, which
    /// is a mistake that sounds like a different mistake.
    /// </para>
    /// </summary>
    public const int NoiseLength = 32768;

    /// <summary>
    /// The noise channel, as a long run that repeats.
    /// <para>
    /// The hardware's own shift register: fifteen bits, or seven for the narrow setting that
    /// makes a metallic rattle rather than a hiss. Written out rather than generated live,
    /// because a recording is what everything above this knows how to play.
    /// </para>
    /// </summary>
    public static Voice Noise(bool narrow = false)
    {
        var run = new sbyte[NoiseLength];

        int register = 0x7FFF;
        int width = narrow ? 7 : 15;

        for (int i = 0; i < NoiseLength; i++)
        {
            int bit = (register ^ (register >> 1)) & 1;

            register = (register >> 1) | (bit << (width - 1));

            run[i] = (sbyte)((register & 1) == 0 ? Amplitude : -Amplitude);
        }

        return new Voice(run, (int)(NoiseLength * MiddleC / 256), true, 0);
    }
}
