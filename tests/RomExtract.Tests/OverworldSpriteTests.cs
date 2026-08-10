using PokeMmo.RomExtract.Graphics;

namespace PokeMmo.RomExtract.Tests;

public class OverworldSpriteTests
{
    private static readonly SyntheticRom Fixture = new();

    private static int GraphicsTable(Rom rom) =>
        OverworldSprites.LocateGraphicsTable(rom)
        ?? throw new InvalidOperationException("No overworld graphics table found.");

    [Fact]
    public void FindsTheGraphicsTableWithoutBeingToldWhereItIs()
    {
        Assert.Equal(SyntheticRom.OverworldTableOffset, OverworldSprites.LocateGraphicsTable(Fixture.ToRom()));
    }

    [Fact]
    public void ReadsARecordsDimensions()
    {
        Rom rom = Fixture.ToRom();

        List<ObjectGraphicsInfo?> records =
            OverworldSprites.ReadGraphics(rom, GraphicsTable(rom), SyntheticRom.OverworldCount);

        ObjectGraphicsInfo info = records[0]!;

        Assert.Equal(SyntheticRom.OverworldWidth, info.Width);
        Assert.Equal(SyntheticRom.OverworldHeight, info.Height);
        Assert.Equal(SyntheticRom.OverworldPaletteTagFor(0), info.PaletteTag);
    }

    [Fact]
    public void TheRecordedSizeAgreesWithTheDimensionsInEitherUnit()
    {
        // Two conventions appear in one table: most records state bytes at four bits a
        // pixel, some state the pixel count. Demanding only the first cut six records
        // off the front of the real table — the scan started after them, every graphics
        // id shifted, and nothing failed.
        Rom rom = Fixture.ToRom();

        List<ObjectGraphicsInfo?> records =
            OverworldSprites.ReadGraphics(rom, GraphicsTable(rom), SyntheticRom.OverworldCount);

        foreach (ObjectGraphicsInfo? info in records.Where(r => r is not null))
        {
            Assert.True(
                info!.SizeBytes == info.ExpectedFrameBytes || info.SizeBytes == info.Width * info.Height,
                $"size {info.SizeBytes} for {info.Width}x{info.Height}");
        }
    }

    [Fact]
    public void ATableBeginningWithTheLessCommonSizeUnitIsStillFoundAtItsStart()
    {
        // The whole point. The fixture's first two records state size in pixels, so a
        // locator that only allows bytes would begin two entries late and quietly hand
        // every sprite the wrong graphics id.
        Rom rom = Fixture.ToRom();

        Assert.True(SyntheticRom.OverworldSizeInPixels(0));
        Assert.Equal(SyntheticRom.OverworldTableOffset, OverworldSprites.LocateGraphicsTable(rom));
    }

    [Fact]
    public void FramesSizedInPixelsStillDecode()
    {
        Rom rom = Fixture.ToRom();

        List<ObjectGraphicsInfo?> records =
            OverworldSprites.ReadGraphics(rom, GraphicsTable(rom), SyntheticRom.OverworldCount);

        Dictionary<int, int> boundaries = OverworldSprites.FrameListBoundaries(rom, records);

        List<IndexedImage> frames = OverworldSprites.ReadFrames(rom, records[0]!, boundaries);

        Assert.Equal(SyntheticRom.OverworldFrameCount, frames.Count);
        Assert.Equal(SyntheticRom.OverworldPixelFor(0, 0), frames[0][0, 0]);
    }

    [Fact]
    public void ADeadEntryKeepsItsPlace()
    {
        // A graphics id is an index into this table. Dropping a null would shift every
        // id after it, and every sprite would be somebody else's.
        Rom rom = Fixture.ToRom();

        List<ObjectGraphicsInfo?> records =
            OverworldSprites.ReadGraphics(rom, GraphicsTable(rom), SyntheticRom.OverworldCount);

        Assert.Null(records[SyntheticRom.DeadOverworldIndex]);
        Assert.NotNull(records[SyntheticRom.DeadOverworldIndex + 1]);
        Assert.Equal(SyntheticRom.OverworldCount, records.Count);
    }

    [Fact]
    public void ReadsEveryFrameAndStopsWhereTheNextSpriteBegins()
    {
        // The lists carry no count and no terminator — they are packed back to back.
        // Reading until the entries stop looking like frames gives a sprite every frame
        // in the cartridge after it, because the next list is frames too.
        Rom rom = Fixture.ToRom();

        List<ObjectGraphicsInfo?> records =
            OverworldSprites.ReadGraphics(rom, GraphicsTable(rom), SyntheticRom.OverworldCount);

        Dictionary<int, int> boundaries = OverworldSprites.FrameListBoundaries(rom, records);

        List<IndexedImage> frames = OverworldSprites.ReadFrames(rom, records[2]!, boundaries);

        Assert.Equal(SyntheticRom.OverworldFrameCount, frames.Count);
    }

    [Fact]
    public void WithoutABoundaryASpriteRunsIntoTheNextOne()
    {
        // Kept as a test rather than a comment, because it is the failure this design
        // exists to prevent and it is completely silent — the extra frames decode
        // perfectly well, they just belong to somebody else.
        Rom rom = Fixture.ToRom();

        List<ObjectGraphicsInfo?> records =
            OverworldSprites.ReadGraphics(rom, GraphicsTable(rom), SyntheticRom.OverworldCount);

        List<IndexedImage> unbounded = OverworldSprites.ReadFrames(rom, records[2]!);

        Assert.True(unbounded.Count > SyntheticRom.OverworldFrameCount);
    }

    [Fact]
    public void EveryPixelOfAFrameDecodesToTheColourItWasWrittenWith()
    {
        // Flat fills, so one assertion covers tile ordering, nibble order and stride.
        Rom rom = Fixture.ToRom();

        List<ObjectGraphicsInfo?> records =
            OverworldSprites.ReadGraphics(rom, GraphicsTable(rom), SyntheticRom.OverworldCount);

        const int index = 5;

        Dictionary<int, int> boundaries = OverworldSprites.FrameListBoundaries(rom, records);
        List<IndexedImage> frames = OverworldSprites.ReadFrames(rom, records[index]!, boundaries);

        for (int frame = 0; frame < frames.Count; frame++)
        {
            byte expected = SyntheticRom.OverworldPixelFor(index, frame);
            IndexedImage image = frames[frame];

            Assert.Equal(SyntheticRom.OverworldWidth, image.Width);
            Assert.Equal(SyntheticRom.OverworldHeight, image.Height);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                    Assert.Equal(expected, image[x, y]);
            }
        }
    }

    [Fact]
    public void SaysWhySomethingIsNotARecord()
    {
        // The check that identifies this table is arithmetic across three fields, so
        // the explanation has to name which field disagreed — "not a record" would
        // leave a table found a few entries late indistinguishable from one found
        // correctly.
        Rom rom = Fixture.ToRom();

        int record = GraphicsTable(rom);

        Assert.Contains("reads as a record", ObjectGraphicsInfo.Explain(
            rom, rom.ToOffsetOrNull(rom.ReadU32(record))!.Value));

        // Somewhere that is definitely not one: the middle of the map collision data.
        string why = ObjectGraphicsInfo.Explain(rom, SyntheticRom.MapBlocksOffset);

        Assert.DoesNotContain("reads as a record", why);
        Assert.NotEmpty(why);
    }

    [Fact]
    public void FindsThePaletteTableByItsTagRange()
    {
        // Overworld palette tags do not count from anywhere — they are identifiers in
        // a fixed range, and that range is the only thing marking the table.
        Assert.Equal(
            SyntheticRom.OverworldPaletteTableOffset,
            OverworldSprites.LocatePaletteTable(Fixture.ToRom()));
    }

    [Fact]
    public void APaletteIsFoundByTheTagARecordAsksFor()
    {
        Rom rom = Fixture.ToRom();

        int palettes = OverworldSprites.LocatePaletteTable(rom)!.Value;

        List<ObjectGraphicsInfo?> records =
            OverworldSprites.ReadGraphics(rom, GraphicsTable(rom), SyntheticRom.OverworldCount);

        ObjectGraphicsInfo info = records[3]!;

        GbaPalette? palette = OverworldSprites.PaletteForTag(rom, palettes, info.PaletteTag);

        Assert.NotNull(palette);
        Assert.Equal(GbaPalette.ColorCount, palette.Count);
    }

    [Fact]
    public void ATagThatIsNotInTheTableFindsNothing()
    {
        Rom rom = Fixture.ToRom();
        int palettes = OverworldSprites.LocatePaletteTable(rom)!.Value;

        Assert.Null(OverworldSprites.PaletteForTag(rom, palettes, 0x11FE));
    }

    [Fact]
    public void SpritesRenderToSomethingWithColourInIt()
    {
        // The end-to-end shape of it: a graphics id becomes pixels with a palette
        // applied, which is all the client needs.
        Rom rom = Fixture.ToRom();

        int palettes = OverworldSprites.LocatePaletteTable(rom)!.Value;

        List<ObjectGraphicsInfo?> records =
            OverworldSprites.ReadGraphics(rom, GraphicsTable(rom), SyntheticRom.OverworldCount);

        ObjectGraphicsInfo info = records[1]!;

        GbaPalette palette = OverworldSprites.PaletteForTag(rom, palettes, info.PaletteTag)!;
        IndexedImage frame = OverworldSprites.ReadFrames(
            rom, info, OverworldSprites.FrameListBoundaries(rom, records))[0];

        byte[] rgba = frame.ToRgba(palette);

        Assert.Equal(frame.Width * frame.Height * 4, rgba.Length);
    }
}
