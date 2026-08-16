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
        // comparing a constant with itself.
        Assert.Equal(2, Found().Select(s => s.Pitch).Distinct().Count());
        Assert.Equal(2, Found().Select(s => s.Loops).Distinct().Count());
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
    /// A header that is right in every way except the rate is not a sound. This is the
    /// check that would be quietly dropped by anybody who found the pitch list annoying.
    /// </summary>
    [Fact]
    public void AndARateTheFormatDoesNotUseIsNotASound() =>
        Assert.Null(At(SyntheticRom.SampleWithOddRateOffset));

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

        // The near-miss line is always printed, and here it has something real to report.
        Assert.Contains(said, line => line.Contains("this build does not know"));
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
