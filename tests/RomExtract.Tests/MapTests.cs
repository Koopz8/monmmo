using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Graphics;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Tests;

public class MapLocatorTests
{
    private static readonly SyntheticRom Synthetic = new();

    [Fact]
    public void FindsTheLayoutPointerTable()
    {
        MapLayoutTable? table = MapLocator.Locate(Synthetic.ToRom());

        Assert.NotNull(table);
        Assert.Equal(SyntheticRom.MapLayoutTableOffset, table!.Offset);
        Assert.Equal(SyntheticRom.MapLayoutTableLength, table.EntryCount);
    }

    [Fact]
    public void ResolvesEveryLivePointerToTheLayoutItTargets()
    {
        MapLayoutTable table = MapLocator.Locate(Synthetic.ToRom())!;

        Assert.Equal(SyntheticRom.MapLayoutTableLength - 1, table.Valid.Count());
        Assert.All(table.Valid, x => Assert.Equal(SyntheticRom.MapLayoutOffset, x.Layout.Offset));
    }

    [Fact]
    public void StepsOverADeadSlotInsteadOfEndingTheTableOnIt()
    {
        // Regression: a single null entry used to terminate the run. On a real image
        // that truncated the table at the dead slot and left the remainder to be found
        // as a separate run — with every index in it shifted.
        MapLayoutTable table = MapLocator.Locate(Synthetic.ToRom())!;

        Assert.Equal(SyntheticRom.MapLayoutTableLength, table.EntryCount);
        Assert.Null(table.Layouts[SyntheticRom.DeadLayoutTableIndex]);

        // Entries on both sides of the dead slot are present, at their own indices.
        Assert.NotNull(table.Layouts[SyntheticRom.DeadLayoutTableIndex - 1]);
        Assert.NotNull(table.Layouts[SyntheticRom.DeadLayoutTableIndex + 1]);
        Assert.NotNull(table.Layouts[SyntheticRom.MapLayoutTableLength - 1]);
    }

    [Fact]
    public void StillEndsTheTableAtASustainedRunOfDeadEntries()
    {
        // Tolerance has to be bounded, or a table would run on into whatever follows it.
        var data = new byte[0x20000];
        MapLayoutTable? table = MapLocator.Locate(new Rom(data));

        Assert.Null(table);
    }

    [Fact]
    public void ReadsTheLayoutDimensionsAndPointers()
    {
        MapLayoutRecord layout = MapLocator.Locate(Synthetic.ToRom())!.Valid.First().Layout;

        Assert.Equal(SyntheticRom.MapWidth, layout.Width);
        Assert.Equal(SyntheticRom.MapHeight, layout.Height);
        Assert.Equal(SyntheticRom.MapWidth * SyntheticRom.MapHeight, layout.BlockCount);
        Assert.Equal(Rom.BaseAddress + SyntheticRom.TilesetRecordOffset, layout.PrimaryTilesetPointer);
        Assert.Equal(0u, layout.SecondaryTilesetPointer);
        Assert.Equal(2, layout.BorderWidth);
    }

    [Fact]
    public void RejectsRecordsWithImplausibleDimensions()
    {
        var data = new byte[0x10000];

        // A pointer-shaped record whose dimensions are nonsense must not parse.
        void WriteU32(int at, uint v)
        {
            data[at] = (byte)v; data[at + 1] = (byte)(v >> 8);
            data[at + 2] = (byte)(v >> 16); data[at + 3] = (byte)(v >> 24);
        }

        WriteU32(0x100, 0xFFFFFFFF);              // width
        WriteU32(0x104, 4);                       // height
        WriteU32(0x108, Rom.BaseAddress + 0x200);
        WriteU32(0x10C, Rom.BaseAddress + 0x200);
        WriteU32(0x110, Rom.BaseAddress + 0x200);

        Assert.Null(MapLayoutRecord.TryParse(new Rom(data), 0x100));
    }

    [Fact]
    public void RejectsALayoutWhoseBlockDataWouldNotFit()
    {
        var data = new byte[0x10000];

        void WriteU32(int at, uint v)
        {
            data[at] = (byte)v; data[at + 1] = (byte)(v >> 8);
            data[at + 2] = (byte)(v >> 16); data[at + 3] = (byte)(v >> 24);
        }

        WriteU32(0x100, 1000);                    // 1000 x 1000 blocks is 2 MB of data
        WriteU32(0x104, 1000);                    // in a 64 KB image
        WriteU32(0x108, Rom.BaseAddress + 0x200);
        WriteU32(0x10C, Rom.BaseAddress + 0x200);
        WriteU32(0x110, Rom.BaseAddress + 0x200);

        Assert.Null(MapLayoutRecord.TryParse(new Rom(data), 0x100));
    }

    [Fact]
    public void FindsNoLayoutTableInNoise()
    {
        var noise = new byte[512 * 1024];
        new Random(7).NextBytes(noise);

        Assert.Null(MapLocator.Locate(new Rom(noise)));
    }
}

public class MapRenderingTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static (Rom Rom, MapLayoutRecord Layout) Open()
    {
        Rom rom = Synthetic.ToRom();
        return (rom, MapLocator.Locate(rom)!.Valid.First().Layout);
    }

    [Fact]
    public void RendersAtSixteenPixelsPerBlock()
    {
        (Rom rom, MapLayoutRecord layout) = Open();
        RenderedMap map = MapRenderer.Create(rom, layout).Render(layout);

        Assert.Equal(SyntheticRom.MapWidth * 16, map.Width);
        Assert.Equal(SyntheticRom.MapHeight * 16, map.Height);
        Assert.Equal(map.Width * map.Height * 4, map.Rgba.Length);
    }

    [Fact]
    public void PaintsEverySquareWithTheColourItsMetatileSelects()
    {
        (Rom rom, MapLayoutRecord layout) = Open();
        RenderedMap map = MapRenderer.Create(rom, layout).Render(layout);

        for (int by = 0; by < SyntheticRom.MapHeight; by++)
        {
            for (int bx = 0; bx < SyntheticRom.MapWidth; bx++)
            {
                Rgba32 expected = Synthetic.ExpectedTilesetPalette[SyntheticRom.MetatileAt(bx, by)];

                // Sample the middle of the square, away from any edge effects.
                int at = ((by * 16 + 8) * map.Width + (bx * 16 + 8)) * 4;

                Assert.Equal(expected.R, map.Rgba[at + 0]);
                Assert.Equal(expected.G, map.Rgba[at + 1]);
                Assert.Equal(expected.B, map.Rgba[at + 2]);
                Assert.Equal(255, map.Rgba[at + 3]);
            }
        }
    }

    [Fact]
    public void FillsEachSquareCompletelyRatherThanOnlyItsFirstTile()
    {
        // A metatile is four 8x8 tiles. Drawing only the first would leave three
        // quarters of every square unpainted, and the centre sample above would
        // still pass — so check all four corners of one square.
        (Rom rom, MapLayoutRecord layout) = Open();
        RenderedMap map = MapRenderer.Create(rom, layout).Render(layout);

        Rgba32 expected = Synthetic.ExpectedTilesetPalette[SyntheticRom.MetatileAt(2, 3)];

        foreach ((int dx, int dy) in new[] { (0, 0), (15, 0), (0, 15), (15, 15) })
        {
            int at = ((3 * 16 + dy) * map.Width + (2 * 16 + dx)) * 4;
            Assert.Equal(expected.R, map.Rgba[at + 0]);
            Assert.Equal(expected.G, map.Rgba[at + 1]);
            Assert.Equal(expected.B, map.Rgba[at + 2]);
        }
    }

    [Fact]
    public void ProducesAValidPng()
    {
        (Rom rom, MapLayoutRecord layout) = Open();
        byte[] png = MapRenderer.Create(rom, layout).Render(layout).ToPng();

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
        PngProbe.ReadChunks(png, verifyCrc: true);
    }

    [Fact]
    public void DecodesBlockFields()
    {
        // metatile 0x123, collision 2, elevation 5
        var block = new MapBlock(0x5923);

        Assert.Equal(0x123, block.MetatileId);
        Assert.Equal(2, block.Collision);
        Assert.Equal(5, block.Elevation);
    }

    [Fact]
    public void DecodesMetatileEntryFields()
    {
        // tile 0x0A5, horizontal flip, palette 3
        var entry = new MetatileEntry(0x34A5);

        Assert.Equal(0x0A5, entry.TileId);
        Assert.True(entry.FlipHorizontal);
        Assert.False(entry.FlipVertical);
        Assert.Equal(3, entry.PaletteIndex);
    }

    [Fact]
    public void RefusesToRenderWhenThePrimaryTilesetIsNotOne()
    {
        (Rom rom, MapLayoutRecord layout) = Open();

        // Point the layout's primary tileset at the block data, which is not a
        // tileset record.
        MapLayoutRecord broken = layout with { PrimaryTilesetPointer = layout.BlocksPointer };

        Assert.Throws<InvalidDataException>(() => MapRenderer.Create(rom, broken));
    }
}

public class MetatileBehaviourTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static (Rom Rom, MapLayoutRecord Layout) Open()
    {
        Rom rom = Synthetic.ToRom();
        return (rom, MapLocator.Locate(rom)!.Valid.First().Layout);
    }

    [Fact]
    public void TellsTheAttributesApartFromTheCallbackByTheirLowBit()
    {
        // The two trailing fields differ in order between games, so they are
        // identified by what they point at: a function pointer has bit 0 set to
        // select the instruction set, a data pointer does not.
        (Rom rom, MapLayoutRecord layout) = Open();
        TilesetRecord tileset = TilesetRecord.TryParse(rom, layout.PrimaryTilesetPointer)!;

        Assert.Equal(
            Rom.BaseAddress + SyntheticRom.TilesetAttributesOffset,
            tileset.MetatileAttributesPointer);

        Assert.Equal(0u, tileset.MetatileAttributesPointer & 1);
    }

    [Fact]
    public void ReadsEverySquaresBehaviour()
    {
        (Rom rom, MapLayoutRecord layout) = Open();
        byte[] behaviours = layout.ReadBehaviours(rom);

        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                byte expected = SyntheticRom.BehaviourOfMetatile(SyntheticRom.MetatileAt(x, y));
                Assert.Equal(expected, behaviours[y * layout.Width + x]);
            }
        }
    }

    [Fact]
    public void GrassSquaresAreFoundWhereTheyWereWritten()
    {
        (Rom rom, MapLayoutRecord layout) = Open();
        byte[] behaviours = layout.ReadBehaviours(rom);

        int grass = behaviours.Count(MetatileBehaviour.IsEncounterGrass);

        Assert.True(grass > 0);
        Assert.Equal(
            behaviours.Where((_, i) =>
                MetatileBehaviour.IsEncounterGrass(
                    SyntheticRom.BehaviourOfMetatile(
                        SyntheticRom.MetatileAt(i % layout.Width, i / layout.Width)))).Count(),
            grass);
    }

    [Fact]
    public void TheWrongStrideSilentlyReturnsSomethingElse()
    {
        // Regression: this is the failure mode that cost the most time. Reading at
        // the wrong stride throws nothing and produces plausible-looking counts — it
        // just answers about a different metatile.
        (Rom rom, MapLayoutRecord layout) = Open();

        byte[] correct = layout.ReadBehaviours(rom);
        byte[] halfStride = layout.ReadBehaviours(rom, attributeStride: 2);

        Assert.NotEqual(correct, halfStride);
    }

    [Fact]
    public void TheStrideComesFromTheGameNotAGuess()
    {
        Assert.Equal(4, TilesetSplit.FireRed.AttributeStride);
        Assert.Equal(2, TilesetSplit.Emerald.AttributeStride);
    }
}
