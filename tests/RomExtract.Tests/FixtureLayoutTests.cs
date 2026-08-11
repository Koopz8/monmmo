using PokeMmo.RomExtract.Graphics;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// That the fixture's regions do not sit on top of each other.
/// <para>
/// There are two dozen of them now, laid out by hand at round addresses, and an
/// overlap does not fail — whichever write runs last simply wins. That is how a block
/// of scripts came to be full of sprite pixels, and how the sprite pixels came to be
/// full of palette entries, both silently, both while every test passed because they
/// happened to touch the parts that were not clobbered.
/// </para>
/// </summary>
public class FixtureLayoutTests
{
    private sealed record Region(string Name, int Start, int Length)
    {
        public int End => Start + Length;

        public bool Overlaps(Region other) => Start < other.End && other.Start < End;
    }

    private static IEnumerable<Region> Regions()
    {
        yield return new("species names", SyntheticRom.SpeciesNamesOffset, SyntheticRom.SpeciesCount * GameText.SpeciesNameLength);
        yield return new("base stats", SyntheticRom.BaseStatsOffset, SyntheticRom.SpeciesCount * 28);
        yield return new("front pics", SyntheticRom.FrontPicTableOffset, SyntheticRom.SpeciesCount * 8);
        yield return new("back pics", SyntheticRom.BackPicTableOffset, SyntheticRom.SpeciesCount * 8);
        yield return new("normal palettes", SyntheticRom.NormalPaletteTableOffset, SyntheticRom.SpeciesCount * 8);
        yield return new("shiny palettes", SyntheticRom.ShinyPaletteTableOffset, SyntheticRom.SpeciesCount * 8);
        yield return new("decoy palettes", SyntheticRom.DecoyPaletteTableOffset, SyntheticRom.DecoyPaletteEntryCount * 8);

        yield return new("map layout table", SyntheticRom.MapLayoutTableOffset, SyntheticRom.MapLayoutTableLength * 4);
        yield return new("tileset attributes", SyntheticRom.TilesetAttributesOffset, SyntheticRom.MetatileCount * SyntheticRom.AttributeStride);

        yield return new("map headers", SyntheticRom.MapHeadersOffset, SyntheticRom.MapCount * 28);
        yield return new("bank arrays", SyntheticRom.BankArraysOffset, SyntheticRom.MapCount * 4);
        yield return new("region names", SyntheticRom.RegionNameTextOffset, SyntheticRom.RegionLocationCount * 16);
        yield return new("region entries", SyntheticRom.RegionMapEntriesOffset, SyntheticRom.RegionLocationCount * 8);

        yield return new("learnset table", SyntheticRom.LearnsetTableOffset, SyntheticRom.SpeciesCount * 4);
        yield return new("learnset blobs", SyntheticRom.LearnsetBlobsOffset, SyntheticRom.SpeciesCount * SyntheticRom.LearnsetStride);

        yield return new("map events", SyntheticRom.MapEventsOffset, SyntheticRom.MapCount * 32);
        yield return new("warps", SyntheticRom.MapWarpsOffset, SyntheticRom.MapCount * 64);
        yield return new("objects", SyntheticRom.MapObjectsOffset, SyntheticRom.MapCount * 128);
        yield return new("connections", SyntheticRom.MapConnectionsOffset, SyntheticRom.MapCount * 64);

        yield return new("scripts", SyntheticRom.ScriptsOffset, SyntheticRom.MapCount * 4 * 64);
        yield return new("script text", SyntheticRom.ScriptTextOffset, SyntheticRom.MapCount * 4 * 256);

        yield return new("overworld table", SyntheticRom.OverworldTableOffset, SyntheticRom.OverworldCount * 4);
        yield return new("overworld records", SyntheticRom.OverworldRecordsOffset, SyntheticRom.OverworldCount * ObjectGraphicsInfo.RecordSizeBytes);
        yield return new("overworld frame lists", SyntheticRom.OverworldFrameListsOffset, SyntheticRom.OverworldCount * SyntheticRom.OverworldFrameCount * 8);
        yield return new(
            "overworld pixels",
            SyntheticRom.OverworldPixelsOffset,
            SyntheticRom.OverworldCount * SyntheticRom.OverworldFrameCount * SyntheticRom.OverworldWidth * SyntheticRom.OverworldHeight / 2);
        yield return new("overworld palette table", SyntheticRom.OverworldPaletteTableOffset, SyntheticRom.OverworldPaletteCount * 8);
        yield return new("overworld palette data", SyntheticRom.OverworldPaletteDataOffset, SyntheticRom.OverworldPaletteCount * GbaPalette.SizeBytes);
    }

    [Fact]
    public void RegionsDoNotOverlap()
    {
        List<Region> regions = Regions().OrderBy(r => r.Start).ToList();

        var clashes = new List<string>();

        for (int i = 0; i < regions.Count - 1; i++)
        {
            if (regions[i].Overlaps(regions[i + 1]))
            {
                clashes.Add(
                    $"{regions[i].Name} (0x{regions[i].Start:X6}-0x{regions[i].End:X6}) runs into " +
                    $"{regions[i + 1].Name} (0x{regions[i + 1].Start:X6})");
            }
        }

        Assert.Empty(clashes);
    }

    [Fact]
    public void EverythingFitsInsideTheImage()
    {
        foreach (Region region in Regions())
            Assert.True(region.End <= SyntheticRom.RomSize, $"{region.Name} ends past the end of the image");
    }
}
