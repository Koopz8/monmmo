using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Tests;

public class MapLinkExtractionTests
{
    private static readonly SyntheticRom Fixture = new();

    private static MapHeaderRecord HeaderFor(int index)
    {
        Rom rom = Fixture.ToRom();

        MapBankTable banks = MapBankLocator.Locate(rom)
            ?? throw new InvalidOperationException("No bank table in the fixture.");

        (int bank, int map) = (index / SyntheticRom.MapsPerBank, index % SyntheticRom.MapsPerBank);

        return banks.AllMaps.Single(m => m.Bank == bank && m.Map == map).Header;
    }

    [Fact]
    public void ReadsWarpsExactly()
    {
        Rom rom = Fixture.ToRom();

        foreach (int index in new[] { 0, 1, 12, SyntheticRom.MapCount - 1 })
        {
            List<Warp> warps = MapLinkExtractor.ReadWarps(
                rom, HeaderFor(index), SyntheticRom.MapWidth, SyntheticRom.MapHeight);

            Assert.Equal(SyntheticRom.WarpsFor(index), warps);
        }
    }

    [Fact]
    public void ReadsTheWarpTableAndNotTheObjectTable()
    {
        // The events record holds four counts and four pointers. Taking the first of
        // each — the object events — would give three warps from the wrong table, and
        // they would look entirely plausible.
        Rom rom = Fixture.ToRom();

        List<Warp> warps = MapLinkExtractor.ReadWarps(
            rom, HeaderFor(0), SyntheticRom.MapWidth, SyntheticRom.MapHeight);

        Assert.Equal(2, warps.Count);
    }

    [Fact]
    public void AMapWithNoEventsHasNoWarps()
    {
        Rom rom = Fixture.ToRom();

        List<Warp> warps = MapLinkExtractor.ReadWarps(
            rom, HeaderFor(SyntheticRom.MapWithoutEvents), SyntheticRom.MapWidth, SyntheticRom.MapHeight);

        Assert.Empty(warps);
    }

    [Fact]
    public void AWarpOutsideTheMapIsDropped()
    {
        Rom rom = Fixture.ToRom();

        List<Warp> warps = MapLinkExtractor.ReadWarps(
            rom, HeaderFor(SyntheticRom.MapWithAStrayWarp), SyntheticRom.MapWidth, SyntheticRom.MapHeight);

        Assert.Equal(SyntheticRom.WarpsFor(SyntheticRom.MapWithAStrayWarp), warps);
        Assert.All(warps, w => Assert.InRange(w.X, 0, SyntheticRom.MapWidth - 1));
    }

    [Fact]
    public void ReadsConnectionsExactly()
    {
        Rom rom = Fixture.ToRom();

        foreach (int index in new[] { 0, 5, SyntheticRom.MapCount - 1 })
        {
            List<MapConnection> connections = MapLinkExtractor.ReadConnections(rom, HeaderFor(index));

            Assert.Equal(SyntheticRom.ConnectionsFor(index), connections);
        }
    }

    [Fact]
    public void ADiveConnectionIsNotAWalkableEdge()
    {
        // Directions five and six join a surface map to an underwater one. Reading
        // them as a side would give a map an edge that leads somewhere unreachable.
        Rom rom = Fixture.ToRom();

        List<MapConnection> connections = MapLinkExtractor.ReadConnections(rom, HeaderFor(0));

        Assert.Equal(2, connections.Count);
        Assert.All(connections, c => Assert.True(Enum.IsDefined(c.Side)));
    }

    [Fact]
    public void NegativeConnectionOffsetsSurvive()
    {
        // The offset slides a neighbour along the shared edge and is signed. Reading
        // it unsigned would turn a small negative into four billion, and the map would
        // join at a column that does not exist.
        Rom rom = Fixture.ToRom();

        List<MapConnection> connections = MapLinkExtractor.ReadConnections(rom, HeaderFor(0));

        Assert.Contains(connections, c => c.Offset < 0);
        Assert.Equal(-2, connections.Single(c => c.Side == ConnectionSide.Down).Offset);
    }
}

public class ExportedWorldLinkTests
{
    private static readonly WorldData Exported = WorldExporter.Export(new SyntheticRom().ToRom());

    [Fact]
    public void EveryMapCarriesItsWarpsAndConnections()
    {
        MapData map = Exported.Find(SyntheticRom.MapIdAt(1))!;

        Assert.Equal(SyntheticRom.WarpsFor(1), map.Warps);
        Assert.Equal(SyntheticRom.ConnectionsFor(1), map.Connections);
    }

    [Fact]
    public void LinksSurviveASaveAndLoad()
    {
        using var buffer = new MemoryStream();
        Exported.Save(buffer);

        buffer.Position = 0;
        WorldData reloaded = WorldData.Load(buffer);

        MapData before = Exported.Find(SyntheticRom.MapIdAt(9))!;
        MapData after = reloaded.Find(SyntheticRom.MapIdAt(9))!;

        Assert.Equal(before.Warps, after.Warps);
        Assert.Equal(before.Connections, after.Connections);
    }

    [Fact]
    public void EveryLinkLeadsSomewhereReal()
    {
        // The check the export report makes on a real cartridge, asserted here: a
        // whole-file misread would leave every link pointing at a map that is not
        // there, and the totals alone would look fine.
        var known = Exported.Maps.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (MapData map in Exported.Maps)
        {
            Assert.All(map.Warps, w => Assert.Contains(w.TargetMapId, known));
            Assert.All(map.Connections, c => Assert.Contains(c.MapId, known));
        }
    }

    [Fact]
    public void AWarpIsFoundBySquare()
    {
        MapData map = Exported.Find(SyntheticRom.MapIdAt(1))!;

        Warp expected = SyntheticRom.WarpsFor(1)[0];

        Assert.Equal(expected, map.WarpAt(new GridPosition(expected.X, expected.Y)));
        Assert.Null(map.WarpAt(new GridPosition(9, 7)));
    }
}
