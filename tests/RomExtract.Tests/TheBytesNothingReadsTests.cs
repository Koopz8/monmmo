using PokeMmo.RomExtract.Maps;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// 259 found the object table's kind byte by hand — <c>0xFF</c> on nine records where 1639 had
/// nought, in a byte no reader consumed — and it took a hexdump and a hunch. This asks the same
/// question of all four event lists at once.
/// <para>
/// <b>The reader says which bytes it reads.</b> The list of consumed offsets is not written down
/// anywhere and must not be: a hand-kept list goes stale the first time a field is added, which is
/// the fault this project fixed at 220, 224, 251 and 258. <see cref="Rom.WatchReads"/> records
/// what a reader actually touched.
/// </para>
/// <para>
/// <b>And the answer is an ELEVATION.</b> object +8, warp +4, trigger +4 and sign +4 hold the
/// elevation of the square the record stands on: 3860 of 3863 records either match it exactly or
/// have nought — the wildcard — on one side. Three genuinely disagree.
/// </para>
/// </summary>
public sealed class TheBytesNothingReadsTests
{
    private static readonly SyntheticRom Fixture = new();

    private static List<MapHeaderRecord> Headers()
    {
        Rom rom = Fixture.ToRom();

        MapBankTable banks = MapBankLocator.Locate(rom)
            ?? throw new InvalidOperationException("No bank table in the fixture.");

        return [.. banks.AllMaps.Select(m => m.Header)];
    }

    // ------------------------------------------------------------------ the watch

    /// <summary>
    /// THE MECHANISM: a read through the image is recorded, and a byte nobody asked for is not.
    /// Without this the sweep is a hand-kept list of offsets wearing a measurement's clothes.
    /// </summary>
    [Fact]
    public void EveryByteAReadTouchesIsRecordedAndNothingElseIs()
    {
        Rom rom = Fixture.ToRom();
        var touched = new HashSet<int>();

        using (rom.WatchReads(touched))
        {
            rom.ReadU8(100);
            rom.ReadU16(200);
            rom.ReadU32(300);
        }

        Assert.Equal([100, 200, 201, 300, 301, 302, 303], touched.Order());
    }

    /// <summary>
    /// AND IT STOPS. A watch left on would make every later reading count as a read of these
    /// records, and the sweep would report that nothing is unread anywhere.
    /// </summary>
    [Fact]
    public void TheWatchStopsWhenItIsDisposed()
    {
        Rom rom = Fixture.ToRom();
        var touched = new HashSet<int>();

        using (rom.WatchReads(touched)) rom.ReadU8(100);

        rom.ReadU8(101);

        Assert.Equal([100], touched.Order());
    }

    // ------------------------------------------------------------------ the sweep

    /// <summary>
    /// A BYTE THE READER CONSUMES IS NOT REPORTED. The whole instrument is the difference between
    /// a byte nothing looks at and a byte something does, so a sweep that listed both would be a
    /// list of every offset in the record.
    /// </summary>
    [Fact]
    public void AByteTheReaderConsumesIsNotCalledUnread()
    {
        List<UnreadByte> found = WhatNothingReads.In(
            Fixture.ToRom(),
            Headers(),
            (image, header) => MapLinkExtractor.ReadObjects(
                image, header, SyntheticRom.MapWidth, SyntheticRom.MapHeight));

        // The local id is the first byte of an object record and the reader takes it every time.
        Assert.DoesNotContain(found, u => u.List == DroppedEvent.Objects && u.Offset == 0);

        // …and the graphics id, and the square.
        Assert.DoesNotContain(found, u => u.List == DroppedEvent.Objects && u.Offset == 1);
        Assert.DoesNotContain(found, u => u.List == DroppedEvent.Objects && u.Offset == 4);
    }

    /// <summary>
    /// AND A BYTE IT DOES NOT CONSUME IS. The positive half — a sweep that has only ever come back
    /// empty has not been shown able to come back full (253), and here the reader is run alone so
    /// every byte the OTHER three readers would have taken is unread.
    /// </summary>
    [Fact]
    public void AByteNoReaderConsumesIsReported()
    {
        List<UnreadByte> found = WhatNothingReads.In(
            Fixture.ToRom(),
            Headers(),
            (image, header) => MapLinkExtractor.ReadObjects(
                image, header, SyntheticRom.MapWidth, SyntheticRom.MapHeight));

        // Nothing in that one reader looks at a warp at all, so every byte of one is unread.
        Assert.Contains(found, u => u.List == DroppedEvent.Warps);
    }

    /// <summary>
    /// AND THE TWO ANSWERS ARE DIFFERENT ANSWERS. "Nothing reads it" and "nothing reads it AND it
    /// varies" are the difference between a spare byte and a field, and folding them together
    /// would have reported every padding byte in the cartridge as a finding.
    /// </summary>
    [Fact]
    public void ASpareByteAndAFieldAreToldApart()
    {
        var spare = new UnreadByte("object", 3, new Dictionary<int, int> { [0] = 1648 });
        var field = new UnreadByte("object", 2, new Dictionary<int, int> { [0] = 1639, [0xFF] = 9 });

        Assert.True(spare.AlwaysNought);
        Assert.Equal(0, spare.Unusual);

        Assert.False(field.AlwaysNought);
        Assert.Equal(9, field.Unusual);
        Assert.Equal(1648, field.Records);
    }

    /// <summary>
    /// AND A BYTE THAT IS THE SAME EVERYWHERE BUT NOT NOUGHT IS NOT SPARE. A record layout can
    /// carry a constant that is not zero, and calling it spare because it never varies would be
    /// the same mistake as calling a field spare because it usually does not.
    /// </summary>
    [Fact]
    public void AConstantThatIsNotNoughtIsNotCalledSpare()
    {
        var constant = new UnreadByte("trigger", 5, new Dictionary<int, int> { [3] = 228 });

        Assert.False(constant.AlwaysNought);
        Assert.Equal(0, constant.Unusual);
    }
}
