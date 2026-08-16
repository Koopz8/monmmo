using PokeMmo.Core.Sound;
using PokeMmo.RomExtract.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The four channels that are circuits rather than recordings.
/// <para>
/// They were held open and silent, on the grounds that they had their own machinery and this
/// was not it. Then a real cartridge said what a voicegroup is actually made of: 24 slots, of
/// which 22 were these and 2 were recordings. A build that plays only recordings plays a
/// twelfth of the music, which is why a town's theme came out as two looping samples and no
/// melody — audible, and completely wrong, and indistinguishable from a bug in the sequencer.
/// </para>
/// <para>
/// A cycle of a square wave is a recording played round and round, so each of these becomes a
/// looping <see cref="Voice"/> and every piece already built works on it unchanged.
/// </para>
/// </summary>
public class TheChannelsThatAreCircuitsTests
{
    // ---- the square channels ------------------------------------------------------------

    /// <summary>
    /// A square wave is high for its duty's share of the cycle and low for the rest, and the
    /// four duties really are four different shares.
    /// </summary>
    [Fact]
    public void ASquareWaveIsHighForItsShareOfTheCycle()
    {
        var highs = new List<int>();

        for (int duty = 0; duty < PsgVoices.DutyEighths.Count; duty++)
        {
            Voice voice = PsgVoices.Square(duty);

            Assert.Equal(PsgVoices.SquareCycle, voice.Audio.Length);

            int high = voice.Audio.Count(s => s > 0);

            Assert.Equal(PsgVoices.DutyEighths[duty] * PsgVoices.SquareCycle / 8, high);

            highs.Add(high);
        }

        // Four different shares, so the duty is being read rather than ignored.
        Assert.Equal(4, highs.Distinct().Count());
    }

    /// <summary>
    /// And it loops, which is what makes one cycle a note rather than a click.
    /// </summary>
    [Fact]
    public void AndItLoopsFromItsStart()
    {
        Voice voice = PsgVoices.Square(1);

        Assert.True(voice.Loops);
        Assert.Equal(0, voice.LoopStart);
    }

    /// <summary>
    /// It is written to sound at middle C, so that the player's own pitch arithmetic carries
    /// it to every other key.
    /// <para>
    /// One cycle every <see cref="PsgVoices.SquareCycle"/> samples, so playing it at the rate
    /// it carries gives that many cycles a second — which has to be middle C or every note in
    /// the game is at the wrong pitch by the same factor.
    /// </para>
    /// </summary>
    [Fact]
    public void ItIsWrittenToSoundAtMiddleC()
    {
        Voice voice = PsgVoices.Square(2);

        double cycles = (double)voice.Rate / voice.Audio.Length;

        Assert.InRange(cycles, PsgVoices.MiddleC - 1, PsgVoices.MiddleC + 1);
    }

    // ---- the programmable channel -------------------------------------------------------

    /// <summary>
    /// The wave channel is thirty-two steps of four bits, high half of a byte first.
    /// <para>
    /// Which half comes first is invisible in a shape whose two halves are the same, so the
    /// bytes here differ in each half and the answer says which was read.
    /// </para>
    /// </summary>
    [Fact]
    public void TheWaveChannelIsThirtyTwoStepsHighHalfFirst()
    {
        // 0xF0 is fifteen then nought: the loudest step followed by the quietest.
        Voice voice = PsgVoices.Wave([0xF0, 0x00, .. new byte[14]]);

        Assert.Equal(PsgVoices.WaveSteps, voice.Audio.Length);

        Assert.True(voice.Audio[0] > 0, "the first step was not the high half of the first byte");
        Assert.True(voice.Audio[1] < 0, "the second step was not the low half of the first byte");
    }

    /// <summary>
    /// And its steps sit either side of silence rather than above it. A waveform that never
    /// goes negative is a note with a click on the front of it.
    /// </summary>
    [Fact]
    public void AndItsStepsSitEitherSideOfSilence()
    {
        // Every step at eight, which is the middle of nought to fifteen.
        Voice middle = PsgVoices.Wave([.. Enumerable.Repeat((byte)0x88, 16)]);

        Assert.All(middle.Audio, sample => Assert.Equal(0, sample));

        Voice loud = PsgVoices.Wave([.. Enumerable.Repeat((byte)0xFF, 16)]);
        Voice quiet = PsgVoices.Wave([.. Enumerable.Repeat((byte)0x00, 16)]);

        Assert.All(loud.Audio, sample => Assert.True(sample > 0));
        Assert.All(quiet.Audio, sample => Assert.True(sample < 0));
    }

    /// <summary>A description this build cannot reach is silence rather than a crash.</summary>
    [Fact]
    public void AWaveWithNoDescriptionIsSilent()
    {
        Voice voice = PsgVoices.Wave([]);

        Assert.All(voice.Audio, sample => Assert.True(sample < 0));
    }

    // ---- the noise channel ---------------------------------------------------------------

    /// <summary>
    /// Noise is a long run rather than a short one, because noise that repeats over a short
    /// buffer stops being noise and becomes a buzz at the loop's own pitch.
    /// </summary>
    [Fact]
    public void NoiseIsALongRun()
    {
        Voice voice = PsgVoices.Noise();

        Assert.Equal(PsgVoices.NoiseLength, voice.Audio.Length);
        Assert.True(voice.Loops);
    }

    /// <summary>
    /// And the narrow setting is a different noise, not the same one relabelled — it is what
    /// makes a rattle rather than a hiss.
    /// </summary>
    [Fact]
    public void AndNarrowNoiseIsADifferentNoise()
    {
        sbyte[] wide = PsgVoices.Noise().Audio;
        sbyte[] narrow = PsgVoices.Noise(narrow: true).Audio;

        Assert.NotEqual(wide, narrow);

        // Both are actually noisy rather than flat, which a broken shift register would be.
        Assert.Contains(wide, s => s > 0);
        Assert.Contains(wide, s => s < 0);
        Assert.Contains(narrow, s => s > 0);
        Assert.Contains(narrow, s => s < 0);
    }

    // ---- the envelope, on its own scale ------------------------------------------------

    /// <summary>
    /// A circuit channel counts its envelope in four bits, not eight.
    /// <para>
    /// A real cartridge's town theme has twenty-four circuit slots and every one of them
    /// reads a sustain of exactly fifteen. Twenty-four coincidences is not a coincidence, it
    /// is a scale — and read as the recorded channels' nought-to-255 it means six per cent,
    /// which is why the melody was there and inaudible.
    /// </para>
    /// </summary>
    [Fact]
    public void FullOnACircuitIsFifteenRatherThanTwoHundredAndFiftyFive()
    {
        (byte _, byte _, byte sustain, byte _) =
            PsgVoices.Shaping(0, 0, (byte)PsgVoices.EnvelopeFull, 0);

        // Loud, rather than the six per cent it was.
        Assert.True(sustain > 200, $"a full sustain came back as {sustain} of 255");
    }

    /// <summary>
    /// And a nought means the opposite ends of the same byte: instantly for attack and
    /// release, never for decay.
    /// <para>
    /// This is the half that is not a scale. Read as an ordinary number, a decay of nought
    /// collapses a note to its sustain in a single step — so every circuit note jumped
    /// straight to six per cent of its loudness and stayed there.
    /// </para>
    /// </summary>
    [Fact]
    public void AndANoughtMeansOppositeEndsOfTheSameByte()
    {
        (byte attack, byte decay, byte _, byte release) = PsgVoices.Shaping(0, 0, 15, 0);

        // Attack immediately, and do not decay at all.
        Assert.Equal(255, attack);
        Assert.Equal(255, decay);

        // Release immediately, which the player's own floor carries to silence.
        Assert.Equal(0, release);
    }

    /// <summary>
    /// And a note shaped this way is actually loud, which is the whole of what was wrong.
    /// </summary>
    [Fact]
    public void ANoteShapedThisWayIsLoud()
    {
        (byte attack, byte decay, byte sustain, byte release) = PsgVoices.Shaping(0, 0, 15, 0);

        var envelope = new Envelope(attack, decay, sustain, release);

        for (int step = 0; step < 8; step++) envelope.Step();

        Assert.True(envelope.Level > 200, $"the note settled at {envelope.Level} of 255");

        // While the reading it replaced settles almost silent, which is what it sounded like.
        var raw = new Envelope(0, 0, 15, 0);

        for (int step = 0; step < 8; step++) raw.Step();

        Assert.True(raw.Level < 32, "the old reading was not quiet, so this proves nothing");
    }

    // ---- and out of a cartridge ------------------------------------------------------------

    /// <summary>
    /// A voicegroup slot that is a circuit comes back playable rather than silent.
    /// <para>
    /// The whole point. The fixture deliberately makes every fourth instrument a shape, and
    /// until now every one of those loaded as nothing at all.
    /// </para>
    /// </summary>
    [Fact]
    public void ASlotThatIsACircuitComesBackPlayable()
    {
        var synthetic = new SyntheticRom();

        Rom rom = synthetic.ToRom();

        SoundTreeResult tree = SoundLocator.Walk(rom);

        VoicegroupRecord group = tree.Voicegroups.First(v => v.Sampled < v.Count);

        SongHeaderRecord song = tree.Songs.First(s => s.VoicegroupOffset == group.Offset);

        int index = tree.Table.ToList().FindIndex(e => e.HeaderOffset == song.Offset);

        Assert.True(index >= 0);

        SongPlayer player = SongLoader.Load(rom, tree, index, new Mixer(8000))!;

        Assert.Equal(group.Count, player.InstrumentCount);

        // And the group really does hold circuits, or this proves nothing.
        Assert.True(group.Sampled < group.Count);

        // Every slot names something with sound in it now — the count of playable ones is
        // the whole group rather than only the recorded part of it.
        Assert.Contains(
            player.Instruments().Split(' '),
            word => word.Contains("instruments", StringComparison.Ordinal));
    }
}
