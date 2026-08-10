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
    public void TheRecordedSizeIsTheDimensionsAtFourBitsAPixel()
    {
        // This relationship is what identifies the table. Nearly anything can look like
        // a pointer; very little accidentally satisfies arithmetic across three fields.
        Rom rom = Fixture.ToRom();

        List<ObjectGraphicsInfo?> records =
            OverworldSprites.ReadGraphics(rom, GraphicsTable(rom), SyntheticRom.OverworldCount);

        foreach (ObjectGraphicsInfo? info in records.Where(r => r is not null))
            Assert.Equal(info!.ExpectedFrameBytes, info.SizeBytes);
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
