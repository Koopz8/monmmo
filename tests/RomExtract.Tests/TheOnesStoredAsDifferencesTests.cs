using PokeMmo.RomExtract.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The recordings that were invisible.
/// <para>
/// A cry is not stored as audio. It is stored as the <em>difference</em> between one sample
/// and the next, four bits at a time, which halves it — and it carries its marker in the
/// first byte of its header rather than the fourth. The format's own documentation describes
/// a header as three zero bytes and then a loop flag, so a reader written from that
/// documentation — which is what this project had — rejects every packed recording on the
/// cartridge without ever mentioning them.
/// </para>
/// <para>
/// That is the finding this step was for. It was not that the decoder was hard; it was that
/// the locator was silently short, and nothing was going to say so.
/// </para>
/// <para>
/// The two fixtures are chosen so the tests do not have to reimplement the difference table.
/// A test that recomputed the sixteen values would be a copy of the code checking itself. One
/// recording is all noughts and must come back flat; one is all ones and must come back
/// climbing by exactly one. Both hold whatever the other fourteen entries turn out to be.
/// </para>
/// </summary>
public class TheOnesStoredAsDifferencesTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static SampleRecord Packed(int offset) =>
        SampleLocator.All(Synthetic.ToRom()).Single(s => s.Offset == offset);

    /// <summary>
    /// They are found at all, which they were not before. The blunt one, and the one that
    /// would fail if the locator went back to insisting on three leading zeroes.
    /// </summary>
    [Fact]
    public void TheyAreFoundAtAll()
    {
        Assert.NotNull(Packed(SyntheticRom.FlatCryOffset));
        Assert.NotNull(Packed(SyntheticRom.RampCryOffset));
    }

    /// <summary>And they are known to be packed rather than taken for ordinary audio.</summary>
    [Fact]
    public void AndAreKnownToBePackedRatherThanPlain()
    {
        Assert.True(Packed(SyntheticRom.FlatCryOffset).Compressed);
        Assert.False(Packed(SyntheticRom.FlatCryOffset).Loops);

        // And the ordinary ones are still not packed, which is the other half of the claim.
        Assert.False(
            SampleLocator.All(Synthetic.ToRom())
                .Single(s => s.Offset == SyntheticRom.SampleOffsetAt(0)).Compressed);
    }

    /// <summary>
    /// A packed recording takes about half the room its length claims, which is the entire
    /// point of packing it and the number the locator has to use when checking it fits.
    /// </summary>
    [Fact]
    public void AndTakeAboutHalfTheRoomTheirLengthClaims()
    {
        SampleRecord cry = Packed(SyntheticRom.FlatCryOffset);

        Assert.True(cry.DataBytes < cry.Length);
        Assert.Equal(cry.Length / 64 * 33, cry.DataBytes);
    }

    /// <summary>
    /// Differences of nought hold one value flat — and the value is the one stored outright
    /// at the head of each block, which says the first sample is read rather than assumed.
    /// </summary>
    [Fact]
    public void NoDifferenceAtAllHoldsOneValue()
    {
        sbyte[] audio = CryDecoder.Decode(Synthetic.ToRom(), Packed(SyntheticRom.FlatCryOffset));

        Assert.Equal(SyntheticRom.CrySamples, audio.Length);
        Assert.All(audio, sample => Assert.Equal(SyntheticRom.FlatCryValue, sample));
    }

    /// <summary>
    /// And differences of one climb by one a sample, across the whole of a block — sixty-four
    /// of them, which is what proves the lone padded nibble at the start is handled and the
    /// pairs after it are not off by one.
    /// </summary>
    [Fact]
    public void AndTheSmallestDifferenceClimbsByOne()
    {
        sbyte[] audio = CryDecoder.Decode(Synthetic.ToRom(), Packed(SyntheticRom.RampCryOffset));

        for (int block = 0; block < SyntheticRom.CrySamples / CryDecoder.SamplesPerBlock; block++)
        {
            for (int i = 0; i < CryDecoder.SamplesPerBlock; i++)
            {
                Assert.Equal(
                    (sbyte)(SyntheticRom.RampCryStart + i),
                    audio[block * CryDecoder.SamplesPerBlock + i]);
            }
        }
    }

    /// <summary>
    /// Every block starts from a sample stored outright rather than carrying on from the
    /// last one.
    /// <para>
    /// This is what stops one bad byte ruining everything after it — a block is at most
    /// sixty-four samples of damage — and it is visible here because the ramp restarts at
    /// its beginning value rather than continuing to climb.
    /// </para>
    /// </summary>
    [Fact]
    public void AndEveryBlockStartsAfresh()
    {
        sbyte[] audio = CryDecoder.Decode(Synthetic.ToRom(), Packed(SyntheticRom.RampCryOffset));

        Assert.Equal(SyntheticRom.RampCryStart, audio[0]);
        Assert.Equal(SyntheticRom.RampCryStart, audio[CryDecoder.SamplesPerBlock]);

        // Which is only a claim at all because the value it would otherwise have carried on
        // to is different.
        Assert.NotEqual(SyntheticRom.RampCryStart, audio[CryDecoder.SamplesPerBlock - 1]);
    }

    /// <summary>
    /// The table's steps are the squares of nought to seven going up, and their negatives
    /// coming down.
    /// <para>
    /// Stated as a property rather than as a list, because a test that listed the sixteen
    /// numbers would agree with a typo in the code as readily as with the format.
    /// </para>
    /// </summary>
    [Fact]
    public void TheDifferencesAreSquaresOneWayAndTheirNegativesTheOther()
    {
        for (int i = 0; i < 8; i++) Assert.Equal(i * i, CryDecoder.Deltas[i]);

        // And the second half mirrors the first, read backwards — which is what makes the
        // table symmetric about nought and a cry sound the same going up as coming down.
        for (int i = 1; i < 8; i++) Assert.Equal(-CryDecoder.Deltas[i], CryDecoder.Deltas[16 - i]);
    }

    /// <summary>
    /// An ordinary recording still decodes as itself. The decoder handles both kinds, and a
    /// change that packed everything would be caught here rather than by listening.
    /// </summary>
    [Fact]
    public void AndAnOrdinaryRecordingIsStillItself()
    {
        Rom rom = Synthetic.ToRom();

        SampleRecord plain = SampleLocator.All(rom)
            .Single(s => s.Offset == SyntheticRom.SampleOffsetAt(0));

        sbyte[] audio = CryDecoder.Decode(rom, plain);

        Assert.Equal(plain.Length, audio.Length);

        for (int i = 0; i < audio.Length; i++)
            Assert.Equal(unchecked((sbyte)rom.ReadU8(plain.DataOffset + i)), audio[i]);
    }

    /// <summary>
    /// And the report counts them, so a cartridge whose cries are all being missed is a
    /// number somebody can see rather than a silence.
    /// </summary>
    [Fact]
    public void AndTheReportCountsThem()
    {
        var said = new List<string>();

        SampleLocator.All(Synthetic.ToRom(), said.Add);

        Assert.Contains(said, line => line.Contains("are packed"));
    }
}
