using PokeMmo.Core.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The piece that was missing between a reader and a mixer.
/// <para>
/// A reader turns bytes into commands and a mixer turns notes into samples. Between them sat
/// nothing at all, which meant both halves of the sound work could be entirely right and the
/// project would still be silent. This is what performs a song: a cursor per track, each with
/// a number of ticks left to wait, all advanced together.
/// </para>
/// <para>
/// Tested against its own properties rather than against a recording, for the same reason the
/// mixer was: there is no recording of the real thing on this machine, and inventing one to
/// compare against would be worse than having none.
/// </para>
/// </summary>
public class PerformingASongTests
{
    private static Instrument Tone() =>
        new(new Voice([.. Enumerable.Range(0, 64).Select(i => (sbyte)(i < 32 ? 100 : -100))], 8000, true, 0),
            60, 255, 255, 255, 200);

    private static SequenceEvent Event(byte opcode, SequenceCommand command, params byte[] arguments) =>
        new(0, opcode, command, arguments);

    /// <summary>A wait of the given length, by its opcode.</summary>
    private static SequenceEvent Wait(int ticks) =>
        Event((byte)(0x80 + SongPlayer.Lengths.ToList().IndexOf(ticks)), SequenceCommand.Wait);

    private static SequenceEvent Note(int key) =>
        Event(0xD4, SequenceCommand.NoteOn, (byte)key, 127);

    private static SequenceEvent End() => Event(0xB1, SequenceCommand.End);

    private static SongPlayer Playing(params SequenceEvent[] events) =>
        new([new Track(events)], [Tone()], new Mixer(8000));

    // ---- the length table -------------------------------------------------------------

    /// <summary>
    /// Forty-nine lengths, and the spacing is the point: whole numbers up to twenty-four and
    /// then only the lengths music uses. A formula fitted to the first half would be wrong
    /// for every value after it, which is why it is a table.
    /// </summary>
    [Fact]
    public void TheLengthsAreATableRatherThanArithmetic()
    {
        Assert.Equal(49, SongPlayer.Lengths.Count);

        // Whole numbers to begin with.
        for (int i = 0; i <= 24; i++) Assert.Equal(i, SongPlayer.Lengths[i]);

        // And gaps afterwards, which is the half a formula would get wrong.
        Assert.True(
            SongPlayer.Lengths.Skip(25).Zip(SongPlayer.Lengths.Skip(24)).Any(p => p.First - p.Second > 1),
            "nothing after twenty-four skips a value, so this could have been arithmetic");

        // It only goes up.
        Assert.Equal(SongPlayer.Lengths.OrderBy(x => x), SongPlayer.Lengths);
    }

    /// <summary>And a length off the end of it is nought rather than a crash.</summary>
    [Fact]
    public void AndALengthOffTheEndIsNought()
    {
        Assert.Equal(0, SongPlayer.LengthOf(-1));
        Assert.Equal(0, SongPlayer.LengthOf(SongPlayer.Lengths.Count));
    }

    // ---- performing -------------------------------------------------------------------

    /// <summary>A song with a note in it makes a noise.</summary>
    [Fact]
    public void ASongWithANoteInItMakesANoise()
    {
        SongPlayer player = Playing(Note(60), Wait(24), End());

        Assert.Contains(player.Render(4000), sample => sample != 0);
    }

    /// <summary>And a song of nothing but waiting is silent.</summary>
    [Fact]
    public void AndASongOfNothingButWaitingIsSilent()
    {
        SongPlayer player = Playing(Wait(24), Wait(24), End());

        Assert.All(player.Render(4000), sample => Assert.Equal(0, sample));
    }

    /// <summary>A track runs to its end and says so.</summary>
    [Fact]
    public void ATrackRunsToItsEnd()
    {
        SongPlayer player = Playing(Note(60), Wait(4), End());

        Assert.False(player.IsFinished);

        player.Render(8000);

        Assert.True(player.IsFinished);
    }

    /// <summary>
    /// A track of nothing but settings does not spin for ever. A song that hangs the client
    /// is worse than a song that stops.
    /// </summary>
    [Fact]
    public void ATrackOfNothingButSettingsDoesNotSpinForEver()
    {
        var settings = new List<SequenceEvent>();

        for (int i = 0; i < 500; i++)
            settings.Add(Event(0xBE, SequenceCommand.Setting, 100));

        settings.Add(End());

        SongPlayer player = new([new Track(settings)], [Tone()], new Mixer(8000));

        // The point is that this returns at all.
        player.Render(2000);

        Assert.True(player.IsFinished);
    }

    // ---- tempo -----------------------------------------------------------------------------

    /// <summary>
    /// A track can change the tempo part-way through, which is why how long a tick lasts
    /// cannot be a constant anywhere.
    /// </summary>
    [Fact]
    public void ATrackCanChangeTheTempo()
    {
        SongPlayer player = Playing(Event(0xBB, SequenceCommand.Setting, 60), Wait(24), End());

        Assert.Equal(SongPlayer.DefaultBeatsPerMinute, player.BeatsPerMinute);

        player.Render(200);

        // The record carries half of it, which is how a byte holds three hundred.
        Assert.Equal(120, player.BeatsPerMinute);
    }

    /// <summary>
    /// And the tempo decides how long a song takes. Twice the tempo, about half the time —
    /// measured as an ordering rather than an exact ratio, because ticks are whole samples
    /// and the rounding is real.
    /// </summary>
    [Fact]
    public void AndTheTempoDecidesHowLongItTakes()
    {
        Assert.True(SamplesToFinish(30) > SamplesToFinish(120), "a slower tempo did not take longer");

        static int SamplesToFinish(int halfTempo)
        {
            SongPlayer player = new(
                [new Track([Event(0xBB, SequenceCommand.Setting, (byte)halfTempo), Wait(24), End()])],
                [Tone()],
                new Mixer(8000));

            var spent = 0;

            while (!player.IsFinished && spent < 400_000)
            {
                player.Render(100);
                spent += 100;
            }

            return spent;
        }
    }

    // ---- several tracks at once ---------------------------------------------------------------

    /// <summary>
    /// Tracks advance together rather than one after another, which is the whole of what
    /// makes a song rather than a queue.
    /// </summary>
    [Fact]
    public void TracksAdvanceTogether()
    {
        SongPlayer player = new(
            [
                new Track([Note(60), Wait(96), End()]),
                new Track([Note(67), Wait(96), End()]),
            ],
            [Tone()],
            new Mixer(8000));

        short[] both = player.Render(2000);

        SongPlayer alone = new(
            [new Track([Note(60), Wait(96), End()])], [Tone()], new Mixer(8000));

        short[] one = alone.Render(2000);

        // Two notes at once are louder than one — the same claim the mixer makes, asked here
        // of the thing that decides when they start.
        Assert.True(
            both.Max(s => Math.Abs((int)s)) > one.Max(s => Math.Abs((int)s)),
            "two tracks together were no louder than one, so they did not sound together");
    }

    /// <summary>
    /// The same song rendered in one buffer and in many comes out the same. A sequencer that
    /// only advanced between buffers would play differently depending on how often somebody
    /// asked it for audio, which is the kind of bug that only appears on somebody else's
    /// machine.
    /// </summary>
    [Fact]
    public void HowOftenItIsAskedDoesNotChangeWhatItPlays()
    {
        short[] whole = Playing(Note(60), Wait(24), Note(64), Wait(24), End()).Render(3000);

        SongPlayer piecemeal = Playing(Note(60), Wait(24), Note(64), Wait(24), End());

        var gathered = new List<short>();

        while (gathered.Count < 3000) gathered.AddRange(piecemeal.Render(37));

        Assert.Equal(whole, gathered.Take(3000));
    }

    // ---- what a note sounds as ------------------------------------------------------------------

    /// <summary>
    /// A higher key plays the recording faster, and twelve of them double it. Computed rather
    /// than tabled, because the arithmetic is exact and a table of a hundred and twenty-eight
    /// entries is a hundred and twenty-eight chances to mistype one.
    /// </summary>
    [Fact]
    public void AHigherKeyPlaysTheRecordingFaster()
    {
        // The same song at two keys an octave apart, and the higher one runs out of recording
        // sooner — which is the audible consequence of the rate and the only one that can be
        // seen from outside.
        Assert.True(Silent(72) < Silent(60), "an octave up did not play any faster");

        static int Silent(int key)
        {
            SongPlayer player = new(
                [new Track([Note(key), Wait(96), End()])],
                [new Instrument(
                    new Voice([.. Enumerable.Repeat((sbyte)90, 400)], 8000, false, 0),
                    60, 255, 255, 255, 255)],
                new Mixer(8000));

            short[] played = player.Render(4000);

            // Where it goes quiet and stays quiet.
            for (int i = played.Length - 1; i > 0; i--)
            {
                if (played[i] != 0) return i;
            }

            return 0;
        }
    }
}
