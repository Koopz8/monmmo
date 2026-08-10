using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Tests;

public class MapBankTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static MapBankTable Locate() => MapBankLocator.Locate(Synthetic.ToRom())!;

    [Fact]
    public void FindsTheBankTable()
    {
        MapBankTable table = Locate();

        Assert.Equal(SyntheticRom.MapGroupsOffset, table.Offset);
        Assert.Equal(SyntheticRom.BankCount, table.Banks.Count);
    }

    [Fact]
    public void ReadsEveryMapInEveryBank()
    {
        MapBankTable table = Locate();

        Assert.Equal(SyntheticRom.BankCount * SyntheticRom.MapsPerBank, table.MapCount);
        Assert.All(table.Banks, bank => Assert.Equal(SyntheticRom.MapsPerBank, bank.Maps.Count));
    }

    [Fact]
    public void GivesEveryMapTheGamesOwnBankAndMapNumbers()
    {
        List<(int Bank, int Map, MapHeaderRecord Header)> maps = Locate().AllMaps.ToList();

        Assert.Equal(SyntheticRom.BankCount * SyntheticRom.MapsPerBank, maps.Count);

        foreach ((int bank, int map, MapHeaderRecord header) in maps)
        {
            Assert.InRange(bank, 0, SyntheticRom.BankCount - 1);
            Assert.InRange(map, 0, SyntheticRom.MapsPerBank - 1);
            Assert.Equal(SyntheticRom.RegionSectionFor(bank, map), header.RegionSectionId);
        }
    }

    [Fact]
    public void ResolvesEachHeaderToItsLayout()
    {
        MapHeaderRecord header = Locate().AllMaps.First().Header;

        Assert.Equal(SyntheticRom.MapLayoutOffset, header.Layout.Offset);
        Assert.Equal(SyntheticRom.MapWidth, header.Layout.Width);
        Assert.Equal(SyntheticRom.MapHeight, header.Layout.Height);
    }

    [Fact]
    public void ReadsHeaderMetadata()
    {
        MapHeaderRecord header = Locate().AllMaps.First().Header;

        Assert.Equal(100, header.Music);
        Assert.Equal(1, header.LayoutId);
        Assert.Equal(1, header.MapType);
        Assert.Equal(0u, header.EventsPointer);
    }

    [Fact]
    public void RejectsAHeaderWhoseLayoutPointerIsNotALayout()
    {
        var data = new byte[0x10000];

        void WriteU32(int at, uint v)
        {
            data[at] = (byte)v; data[at + 1] = (byte)(v >> 8);
            data[at + 2] = (byte)(v >> 16); data[at + 3] = (byte)(v >> 24);
        }

        // Points at zeroed memory, which cannot pass the layout invariants.
        WriteU32(0x100, Rom.BaseAddress + 0x800);

        Assert.Null(MapHeaderRecord.TryParse(new Rom(data), 0x100));
    }

    [Fact]
    public void FindsNoBankTableInNoise()
    {
        var noise = new byte[512 * 1024];
        new Random(11).NextBytes(noise);

        Assert.Null(MapBankLocator.Locate(new Rom(noise)));
    }
}

public class RegionNameTests
{
    private static readonly SyntheticRom Synthetic = new();

    [Fact]
    public void FindsTheRegionMapTable()
    {
        List<RegionMapLocation> names = MapBankLocator.LocateRegionNames(Synthetic.ToRom());

        Assert.Equal(SyntheticRom.RegionLocationCount, names.Count);
    }

    [Fact]
    public void DecodesEveryLocationName()
    {
        List<RegionMapLocation> names = MapBankLocator.LocateRegionNames(Synthetic.ToRom());

        for (int i = 0; i < SyntheticRom.RegionLocationCount; i++)
            Assert.Equal(SyntheticRom.RegionNameFor(i), names[i].Name);
    }

    [Fact]
    public void MapsResolveToTheirLocationName()
    {
        Rom rom = Synthetic.ToRom();
        MapBankTable table = MapBankLocator.Locate(rom)!;
        List<RegionMapLocation> names = MapBankLocator.LocateRegionNames(rom);

        foreach ((int bank, int map, MapHeaderRecord header) in table.AllMaps)
        {
            string expected = SyntheticRom.RegionNameFor(SyntheticRom.RegionSectionFor(bank, map));
            Assert.Equal(expected, names[header.RegionSectionId].Name);
        }
    }

    [Theory]
    [InlineData("PALLET TOWN", true)]
    [InlineData("ROUTE 1", true)]
    [InlineData("MT. MOON", true)]
    [InlineData("", false)]
    [InlineData("AB", false)]
    [InlineData("???????", false)]
    [InlineData("1234", false)]
    public void JudgesWhetherTextLooksLikeALocationName(string candidate, bool expected)
    {
        Assert.Equal(expected, RegionMapLocation.LooksLikeLocationName(candidate));
    }
}
