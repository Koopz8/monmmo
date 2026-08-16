using PokeMmo.Core.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The first modelled half of the sound work, and the first thing here that makes a noise.
/// <para>
/// Everything before this was read: sample headers, instrument entries, song headers, track
/// bytes. This is the other kind. The four envelope numbers are read off the cartridge and
/// what the hardware's driver does with them is compiled code, so the arithmetic below is a
/// model of a behaviour rather than a reading of a table — and it is tested as a model:
/// against its own properties, not against a recording of the real thing, because there is
/// no recording of the real thing on this machine and inventing one would be worse than
/// having none.
/// </para>
/// <para>
/// The properties that actually matter are the ones whose absence would be a bug you could
/// hear or a bug that would hang the client: every note must end, a chord must be louder
/// than one note, a recording played at its own rate must come back unchanged, and a loop
/// must not click.
/// </para>
/// </summary>
public class WhereSoundFirstHappensTests
{
    // ---- envelopes -------------------------------------------------------------------

    /// <summary>It gets louder, then settles where it was told to.</summary>
    [Fact]
    public void ANoteRisesAndThenHolds()
    {
        var envelope = new Envelope(attack: 64, decay: 200, sustain: 100, release: 200);

        int first = envelope.Step();

        Assert.True(first > 0);
        Assert.Equal(EnvelopeStage.Attack, envelope.Stage);

        for (int i = 0; i < 200; i++) envelope.Step();

        Assert.Equal(EnvelopeStage.Sustain, envelope.Stage);
        Assert.Equal(100, envelope.Level);
    }

    /// <summary>
    /// And it climbs on the way up rather than jumping, which is the difference between an
    /// envelope and a switch.
    /// </summary>
    [Fact]
    public void AndItClimbsRatherThanJumping()
    {
        var envelope = new Envelope(attack: 32, decay: 255, sustain: 255, release: 200);

        var seen = new List<int>();

        for (int i = 0; i < 4; i++) seen.Add(envelope.Step());

        Assert.Equal(seen.OrderBy(x => x), seen);
        Assert.True(seen.Distinct().Count() > 1, "it went straight to full");
    }

    /// <summary>
    /// Every note ends. This is the property that matters most, because the failure is not a
    /// wrong sound — it is a channel held for ever by something nobody can hear.
    /// </summary>
    [Theory]
    [InlineData(255, 255, 255, 255)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(255, 0, 0, 0)]
    [InlineData(8, 250, 200, 254)]
    [InlineData(0, 0, 0, 0)]
    public void EveryNoteEnds(int attack, int decay, int sustain, int release)
    {
        var envelope = new Envelope((byte)attack, (byte)decay, (byte)sustain, (byte)release);

        for (int i = 0; i < 2000 && !envelope.IsFinished; i++)
        {
            envelope.Step();

            // Let it go early, because a held note is meant to hold — what has to end is a
            // note that has been let go.
            if (i == 50) envelope.Release();
        }

        Assert.True(envelope.IsFinished, "a released note never finished");
        Assert.Equal(0, envelope.Level);
    }

    /// <summary>
    /// A release of 255 is the case that would hang: multiplying a level by one leaves it
    /// where it was for ever. It is worth its own test because it is the only value that
    /// does it.
    /// </summary>
    [Fact]
    public void AndAReleaseThatWouldNeverFadeStillEnds()
    {
        var envelope = new Envelope(255, 255, 255, 255);

        envelope.Step();
        envelope.Release();

        for (int i = 0; i < 1000 && !envelope.IsFinished; i++) envelope.Step();

        Assert.True(envelope.IsFinished);
    }

    // ---- playing a recording ----------------------------------------------------------

    /// <summary>
    /// A recording played at its own rate comes back sample for sample. This is the identity
    /// the whole resampler is built round, and every other rate is a stretch of it.
    /// </summary>
    [Fact]
    public void ARecordingPlayedAtItsOwnRateComesBackUnchanged()
    {
        sbyte[] audio = [.. Enumerable.Range(0, 32).Select(i => (sbyte)(i * 4 - 64))];

        var voice = new Voice(audio, 8000, Loops: false, LoopStart: 0);

        var note = new PlayingNote(voice, 8000, 8000, new Envelope(255, 255, 255, 255));

        foreach (sbyte expected in audio) Assert.Equal(expected, note.Next());
    }

    /// <summary>
    /// And played at twice its rate it comes back at half the length, taking every second
    /// sample — which is what no interpolation means.
    /// </summary>
    [Fact]
    public void AndPlayedTwiceAsFastItTakesEverySecondSample()
    {
        sbyte[] audio = [.. Enumerable.Range(0, 32).Select(i => (sbyte)(i * 4 - 64))];

        var note = new PlayingNote(
            new Voice(audio, 16000, false, 0), 16000, 8000, new Envelope(255, 255, 255, 255));

        for (int i = 0; i < 16; i++) Assert.Equal(audio[i * 2], note.Next());
    }

    /// <summary>A recording that does not loop finishes when it runs out.</summary>
    [Fact]
    public void ARecordingThatDoesNotLoopFinishes()
    {
        var note = new PlayingNote(
            new Voice(new sbyte[8], 8000, false, 0), 8000, 8000, new Envelope(255, 255, 255, 255));

        for (int i = 0; i < 8; i++) note.Next();

        note.Next();

        Assert.True(note.IsFinished);
    }

    /// <summary>
    /// And one that loops goes back to its loop point rather than to its beginning, and
    /// keeps going for as long as anybody asks.
    /// </summary>
    [Fact]
    public void AndOneThatLoopsGoesBackToItsLoopPoint()
    {
        sbyte[] audio = [1, 2, 3, 4, 5, 6, 7, 8];

        var note = new PlayingNote(
            new Voice(audio, 8000, Loops: true, LoopStart: 4), 8000, 8000,
            new Envelope(255, 255, 255, 255));

        var heard = new List<int>();

        for (int i = 0; i < 16; i++) heard.Add(note.Next());

        Assert.False(note.IsFinished);

        // The first eight are the recording; after that it is the second half, twice.
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 5, 6, 7, 8, 5, 6, 7, 8], heard);
    }

    // ---- the mixer ---------------------------------------------------------------------

    /// <summary>Silence in, silence out, and no exceptions on the way.</summary>
    [Fact]
    public void AMixerWithNothingInItProducesSilence() =>
        Assert.All(new Mixer().Render(256), sample => Assert.Equal(0, sample));

    /// <summary>Something playing produces something to hear.</summary>
    [Fact]
    public void AndSomethingPlayingProducesSomethingToHear()
    {
        var mixer = new Mixer(8000);

        mixer.Play(Tone(), 8000, new Envelope(255, 255, 255, 200));

        Assert.Contains(mixer.Render(512), sample => sample != 0);
    }

    /// <summary>
    /// Two notes are louder than one. The failure this guards is the tempting one — dividing
    /// the sum by the number of voices, which makes a chord quieter than a single note and
    /// is not what this hardware does.
    /// </summary>
    [Fact]
    public void AndTwoNotesAreLouderThanOne()
    {
        Assert.True(Loudest(1) < Loudest(3), "adding notes did not add loudness");
    }

    /// <summary>
    /// It will not sound more than it has channels for, and the oldest is what goes — which
    /// is heard as dropped notes rather than as distortion.
    /// </summary>
    [Fact]
    public void AndItWillNotSoundMoreThanItHasChannelsFor()
    {
        var mixer = new Mixer(8000);

        for (int i = 0; i < Mixer.MostNotes * 3; i++)
            mixer.Play(Tone(), 8000, new Envelope(255, 255, 255, 255));

        Assert.Equal(Mixer.MostNotes, mixer.Sounding);
    }

    /// <summary>
    /// And a note that has finished gives its channel back, which is what stops a long piece
    /// of music from being permanently full.
    /// </summary>
    [Fact]
    public void AndAFinishedNoteGivesItsChannelBack()
    {
        var mixer = new Mixer(8000);

        mixer.Play(Tone(), 8000, new Envelope(255, 255, 0, 1));

        mixer.ReleaseAll();

        mixer.Render(8000);

        Assert.Equal(0, mixer.Sounding);
    }

    // ---- the shapes ----------------------------------------------------------------------

    /// <summary>A square wave is high for part of its period and low for the rest.</summary>
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ASquareIsHighForPartOfItsPeriodAndLowForTheRest(int duty)
    {
        var square = new SquareVoice(duty, 100, 8000);

        int[] period = [.. Enumerable.Range(0, 80).Select(_ => square.Next())];

        Assert.Contains(period, s => s > 0);
        Assert.Contains(period, s => s < 0);

        // And the fraction high is the duty it was asked for, near enough that a wrong duty
        // table would show.
        Assert.Equal(Psg.Duties[duty], period.Count(s => s > 0) * 8 / period.Length);
    }

    /// <summary>
    /// The waveform channel is centred on nothing. A wave read as nought-to-fifteen rather
    /// than as signed puts a constant offset into everything mixed with it, which is not
    /// heard as a wrong note — it is heard as a click at the start and end of every piece.
    /// </summary>
    [Fact]
    public void TheWaveformChannelIsCentredOnNothing()
    {
        // A full sweep, so a correct reading averages to about nought.
        byte[] packed = [.. Enumerable.Range(0, 16).Select(i => (byte)((i % 16) << 4 | (15 - i % 16)))];

        var wave = new WaveVoice(packed, 100, 8000);

        int total = 0;

        for (int i = 0; i < 800; i++) total += wave.Next();

        Assert.InRange(total / 800, -12, 12);
    }

    /// <summary>
    /// Noise is the same noise every time. Two people in the same fight have to hear the
    /// same cymbal, which is a networking property rather than a musical one.
    /// </summary>
    [Fact]
    public void NoiseIsTheSameNoiseEveryTime()
    {
        int[] once = Heard(new NoiseVoice(2000, 8000));
        int[] again = Heard(new NoiseVoice(2000, 8000));

        Assert.Equal(once, again);

        // And it is actually noise rather than a constant, which is the other half of the
        // claim and the half a broken shift register would still pass without.
        Assert.True(once.Distinct().Count() > 1, "the noise channel produced one value for ever");

        // A narrower register is a different sound rather than the same one, which is what
        // says the bit it feeds back is reaching anything.
        Assert.NotEqual(once, Heard(new NoiseVoice(2000, 8000, narrow: true)));
    }

    private static int[] Heard(NoiseVoice noise) =>
        [.. Enumerable.Range(0, 512).Select(_ => noise.Next())];

    private static Voice Tone() =>
        new([.. Enumerable.Range(0, 64).Select(i => (sbyte)(i < 32 ? 100 : -100))], 8000, true, 0);

    private static int Loudest(int notes)
    {
        var mixer = new Mixer(8000);

        for (int i = 0; i < notes; i++)
            mixer.Play(Tone(), 8000, new Envelope(255, 255, 255, 255));

        return mixer.Render(512).Max(s => Math.Abs((int)s));
    }
}
