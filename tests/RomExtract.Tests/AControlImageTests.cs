using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The floor, which every reading in this project has next to it and which 268 found blind.
/// <para>
/// Reversing keeps every byte, every byte's frequency and every TABLE, and destroys every command
/// boundary. This cartridge's accidents come from its tables, so the reversal under-counted them
/// sixteen-fold — 451 entries where the region-preserving floor says about 7300.
/// </para>
/// <para>
/// Two more controls are here. <b>Rotation</b> keeps the tables, the alignment and the direction
/// and breaks the correspondence between a pointer and its target — and is itself a bad floor,
/// which its own variance says: 289, 2301 and 2449 entries at three offsets. <b>The nudge</b>
/// keeps everything including the region, and is stable from four bytes to four thousand:
/// 14.9% to 16.4%, against 19.2% as named.
/// </para>
/// </summary>
public sealed class AControlImageTests
{
    private static Rom Image()
    {
        var bytes = new byte[0x400];

        for (var i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i * 7);

        return new Rom(bytes);
    }

    /// <summary>
    /// BACKWARDS IS BACKWARDS, which is worth one assertion because everything this project has
    /// ever called a floor rests on it doing exactly that and nothing else.
    /// </summary>
    [Fact]
    public void BackwardsIsTheImageReversed()
    {
        Rom rom = Image();
        Rom back = AControlImage.Backwards(rom);

        Assert.Equal(rom.Length, back.Length);
        Assert.All(
            Enumerable.Range(0, rom.Length),
            i => Assert.Equal(rom.ReadU8(i), back.ReadU8(rom.Length - 1 - i)));
    }

    /// <summary>
    /// A ROTATION IS A CYCLIC SHIFT AND KEEPS EVERY BYTE. Not a truncation and not a fill: a
    /// control that lost bytes off one end would have different frequencies from the file, which
    /// is the one thing every floor in this project promises it does not.
    /// </summary>
    [Fact]
    public void RotationMovesEveryByteAndLosesNone()
    {
        Rom rom = Image();
        Rom moved = AControlImage.Rotated(rom, 8);

        Assert.All(
            Enumerable.Range(0, rom.Length),
            i => Assert.Equal(rom.ReadU8((i + 8) % rom.Length), moved.ReadU8(i)));

        Assert.Equal(
            Enumerable.Range(0, rom.Length).Select(rom.ReadU8).Order(),
            Enumerable.Range(0, rom.Length).Select(moved.ReadU8).Order());
    }

    /// <summary>
    /// AND IT IS ALWAYS A MULTIPLE OF FOUR. A rotation of one byte moves every table off its
    /// alignment, so a sweep that filters on alignment would be measuring the rotation rather than
    /// the file — and the whole point of this control is that the structure survives it.
    /// </summary>
    [Fact]
    public void ARotationIsRoundedDownToAMultipleOfFour()
    {
        Rom rom = Image();

        Assert.All(
            Enumerable.Range(0, rom.Length),
            i => Assert.Equal(
                AControlImage.Rotated(rom, 4).ReadU8(i),
                AControlImage.Rotated(rom, 7).ReadU8(i)));
    }

    /// <summary>
    /// AND NOUGHT, AND THE WHOLE LENGTH, ARE THE FILE ITSELF. Both ends, because a rotation that
    /// quietly did something at the edges would put a difference in the floor that is about the
    /// arithmetic rather than about the cartridge.
    /// </summary>
    [Fact]
    public void RotatingByNoughtOrByEverythingIsTheImage()
    {
        Rom rom = Image();

        foreach (int by in new[] { 0, rom.Length })
        {
            Rom same = AControlImage.Rotated(rom, by);

            Assert.All(
                Enumerable.Range(0, rom.Length),
                i => Assert.Equal(rom.ReadU8(i), same.ReadU8(i)));
        }
    }

    /// <summary>
    /// THE OFFSETS ARE SPREAD, ALIGNED AND REPRODUCIBLE. Fractions of the file rather than
    /// anything drawn at random — a control nobody can reproduce from the file alone is a control
    /// nobody can check.
    /// </summary>
    [Fact]
    public void TheOffsetsAreDistinctMultiplesOfFourInsideTheFile()
    {
        Rom rom = Image();

        IReadOnlyList<int> offsets = AControlImage.Offsets(rom, 3);

        Assert.Equal(3, offsets.Count);
        Assert.Equal(3, offsets.Distinct().Count());
        Assert.All(offsets, o => Assert.Equal(0, o % 4));
        Assert.All(offsets, o => Assert.InRange(o, 1, rom.Length - 1));
    }

    private const byte SetVar = 0x16;
    private const byte End = 0x02;

    /// <summary>
    /// THE NUDGE ASKS THE SAME POINTERS ABOUT AN ADDRESS A LITTLE OFF. As named it counts what
    /// decodes; nudged past the end of the block it does not — which is the entire mechanism, and
    /// on this cartridge the maps' own targets fall from 99.6% to 51% and everything else moves
    /// by two points.
    /// </summary>
    [Fact]
    public void TheNudgeCountsWhatDecodesAtTheAddressItIsAimedAt()
    {
        var image = new byte[0x400];

        var at = 0x100;

        for (var i = 0; i < 3; i++)
        {
            image[at++] = SetVar;
            image[at++] = (byte)(0x01 + i);
            image[at++] = 0x40;
            image[at++] = 0x05;
            image[at++] = 0x00;
        }

        image[at] = End;

        var rom = new Rom(image);

        Assert.Equal(1, EveryScriptInTheImage.NudgedFloor(rom, [0x08000100], 0));

        // FOUR BYTES ON STILL DECODES, and that is not a fault in the fixture — it is the
        // phenomenon. Landing in the middle of a block, the reader resynchronises and reaches the
        // same end, which is exactly why "reads as a script" turns out to be worth two points on
        // this cartridge. The maps' own targets fall only from 99.6% to 68% at this nudge.
        Assert.Equal(1, EveryScriptInTheImage.NudgedFloor(rom, [0x08000100], 4));

        // Aimed clean off the block, into bytes that decode as no-ops and never end, it does not.
        Assert.Equal(0, EveryScriptInTheImage.NudgedFloor(rom, [0x08000100], 0x100));
    }

    /// <summary>
    /// AND THE NUDGE'S LIST IS THE ALIGNED TARGETS, decoding or not. It has to include the ones
    /// that do not decode, or the denominator is the answer: asking only the addresses that
    /// already decode what share of them decode is a hundred per cent by construction.
    /// </summary>
    [Fact]
    public void TheAlignedListHoldsTargetsThatDoNotDecodeToo()
    {
        var image = new byte[0x400];

        image[0x300] = End;

        void Pointer(int at, uint address)
        {
            image[at] = (byte)address;
            image[at + 1] = (byte)(address >> 8);
            image[at + 2] = (byte)(address >> 16);
            image[at + 3] = (byte)(address >> 24);
        }

        Pointer(0x100, 0x08000300);
        Pointer(0x104, 0x08000200);

        IReadOnlyList<uint> aligned = EveryScriptInTheImage.Aligned(new Rom(image));

        Assert.Contains(0x08000300u, aligned);
        Assert.Contains(0x08000200u, aligned);
    }
}
