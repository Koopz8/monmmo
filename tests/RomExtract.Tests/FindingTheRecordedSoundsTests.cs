using PokeMmo.RomExtract.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The first layer of sound, found by what it looks like.
/// <para>
/// A recorded sound on this cartridge carries a sixteen-byte header: three zero bytes, a
/// loop flag with exactly two legal values, a pitch drawn from a known dozen, a loop point
/// and a length. That is a shape noise does not produce, which is why the sound work starts
/// here rather than at the song table — every other layer is found by pointing at something
/// this one has already confirmed.
/// </para>
/// <para>
/// Nothing in the locator is hardcoded to a cartridge. These tests therefore assert what it
/// found rather than where, and the fixture writes six sounds plus the two near-misses that
/// have to be rejected for the right reason.
/// </para>
/// </summary>
public class FindingTheRecordedSoundsTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static IReadOnlyList<SampleRecord> Found() => SampleLocator.All(Synthetic.ToRom());

    private static SampleRecord? At(int offset) => Found().FirstOrDefault(s => s.Offset == offset);

    /// <summary>All six, and each where it was written.</summary>
    [Fact]
    public void ItFindsEveryRecordedSound()
    {
        for (int i = 0; i < SyntheticRom.SampleCount; i++)
        {
            int at = SyntheticRom.SamplesOffset + i * SyntheticRom.SampleStride;

            Assert.NotNull(At(at));
        }
    }

    /// <summary>
    /// And reads each header rather than assuming it. Both fields that vary between the
    /// six vary in the answer.
    /// </summary>
    [Fact]
    public void AndReadsWhatEachHeaderActuallySaid()
    {
        for (int i = 0; i < SyntheticRom.SampleCount; i++)
        {
            SampleRecord sample = At(SyntheticRom.SamplesOffset + i * SyntheticRom.SampleStride)!;

            Assert.Equal(SyntheticRom.SampleLoopsAt(i), sample.Loops);
            Assert.Equal(SyntheticRom.SamplePitchFor(i), sample.Pitch);
            Assert.Equal(SyntheticRom.SampleBytes, sample.Length);
        }

        // And the two that vary really do vary, so the loop above cannot be passing by
        // comparing a constant with itself. Asked of the six the loop walked rather than of
        // everything found, because the fixture also carries a recording at a rate that is
        // deliberately not one of these.
        List<SampleRecord> six =
        [
            .. Enumerable.Range(0, SyntheticRom.SampleCount)
                .Select(i => At(SyntheticRom.SamplesOffset + i * SyntheticRom.SampleStride)!),
        ];

        Assert.Equal(2, six.Select(s => s.Pitch).Distinct().Count());
        Assert.Equal(2, six.Select(s => s.Loops).Distinct().Count());
    }

    /// <summary>
    /// The rate comes out of the pitch as a number of samples a second, which is division
    /// rather than a guess — and it is the one derived value on the record.
    /// </summary>
    [Fact]
    public void AndTheRateIsThePitchDividedRatherThanChosen()
    {
        foreach (SampleRecord sample in Found())
            Assert.Equal((int)(sample.Pitch / 1024), sample.Rate);

        // A real recorded sound lands somewhere a person could have recorded it.
        Assert.All(Found(), s => Assert.InRange(s.Rate, 5000, 40000));
    }

    /// <summary>
    /// A pitch that is not a whole number of samples a second is not a pitch.
    /// <para>
    /// This replaced a list of twelve permitted values. The list was not wrong about the
    /// format — every value in it is real — it was wrong about being a filter: a real
    /// cartridge carries 2367 recordings whose rates are not on it, and refusing them left
    /// sixteen song headers on a file that has hundreds. Everything above this layer is
    /// found by pointing at what this layer confirmed, so one line here starved all of it.
    /// </para>
    /// <para>
    /// What is left says something stronger about the format and weaker about the world:
    /// the pitch is 1024 times the rate, so ten bits of it are zero, and what remains is a
    /// number of samples a second somebody could have recorded at.
    /// </para>
    /// </summary>
    [Fact]
    public void APitchThatIsNotAWholeRateIsNotASound() =>
        Assert.Null(At(SyntheticRom.SampleWithOddRateOffset));

    /// <summary>And neither is a whole rate nobody records at, at either end.</summary>
    [Fact]
    public void AndNeitherIsARateNobodyRecordsAt()
    {
        Assert.Null(At(SyntheticRom.SampleTooSlowOffset));
        Assert.Null(At(SyntheticRom.SampleTooFastOffset));
    }

    /// <summary>
    /// But a rate the driver's usual twelve do not include <em>is</em> a sound.
    /// <para>
    /// The half of the change that matters. The other three tests here say what is still
    /// rejected; this one says what is no longer rejected, and it is the one that would fail
    /// if somebody put the whitelist back.
    /// </para>
    /// </summary>
    [Fact]
    public void ButARateOutsideTheDriversUsualTwelveIs()
    {
        SampleRecord? found = At(SyntheticRom.SampleAtAnUnusualRateOffset);

        Assert.NotNull(found);
        Assert.Equal((int)SyntheticRom.UnusualRate, found.Rate);

        // And it really is outside the list, or this proves nothing.
        Assert.DoesNotContain(SyntheticRom.UnusualRate * 1024, SampleLocator.KnownPitches);
    }

    /// <summary>
    /// And neither is one whose sound does not fit in the file. Every other check passes on
    /// this one, which is what makes it worth writing down.
    /// </summary>
    [Fact]
    public void AndNeitherIsOneThatRunsOffTheEnd() =>
        Assert.Null(At(SyntheticRom.SampleRunningOffTheEndOffset));

    /// <summary>
    /// And neither is one whose loop flag is not one of the two the format allows.
    /// <para>
    /// This test exists because deleting the check it guards broke nothing. Everything else
    /// in this file was written before the code; this one was written after, because the
    /// break-it pass proved that line had nobody watching it. A guard nothing can fail is a
    /// comment with a semicolon on the end.
    /// </para>
    /// </summary>
    [Fact]
    public void AndNeitherIsOneWhoseLoopFlagIsNeitherOfTheTwo() =>
        Assert.Null(At(SyntheticRom.SampleWithABadLoopFlagOffset));

    /// <summary>
    /// Nothing is found twice, and nothing is found inside somebody else's audio.
    /// <para>
    /// The failure this guards is specific: audio is bytes, and bytes can look like a
    /// header. The fixture fills every sound with values that are never zero for exactly
    /// this reason, but the overlap check is what would catch it on a real cartridge, where
    /// silence in a recording is a run of zeroes.
    /// </para>
    /// </summary>
    [Fact]
    public void AndNothingOverlapsAnythingElse()
    {
        List<SampleRecord> found = [.. Found().OrderBy(s => s.Offset)];

        for (int i = 1; i < found.Count; i++)
        {
            Assert.True(
                found[i].Offset >= found[i - 1].DataOffset + found[i - 1].Length,
                $"the sound at {found[i].Offset:X} starts inside the one at {found[i - 1].Offset:X}");
        }
    }

    /// <summary>
    /// It says what it found, including when what it found is nothing.
    /// <para>
    /// A count printed only when it is large is a count with no baseline, and this project
    /// has been caught by that before.
    /// </para>
    /// </summary>
    [Fact]
    public void AndItSaysWhatItFound()
    {
        var said = new List<string>();

        SampleLocator.All(Synthetic.ToRom(), said.Add);

        Assert.Contains(said, line => line.Contains("recorded sounds"));

        // The rates it found, listed rather than summarised — the number that says whether
        // this layer is starving every layer above it.
        Assert.Contains(said, line => line.Contains("rates:"));

        // And how many carry a rate outside the driver's usual twelve. Always printed, and
        // here it has something real to report.
        Assert.Contains(said, line => line.Contains("outside the driver's usual twelve"));
    }

    /// <summary>
    /// An empty file finds nothing and says so rather than throwing, which is the shape
    /// every locator in this project has.
    /// </summary>
    [Fact]
    public void AndAFileWithNothingInItFindsNothing()
    {
        var said = new List<string>();

        Assert.Empty(SampleLocator.All(new Rom(new byte[0x2000]), said.Add));

        Assert.Contains(said, line => line.Contains("0 recorded sounds"));
    }
}
