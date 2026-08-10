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

    private static RegionNameTable Locate() => RegionNameLocator.Locate(Synthetic.ToRom())!;

    [Fact]
    public void FindsTheLocationTable()
    {
        RegionNameTable names = Locate();

        Assert.Equal(SyntheticRom.RegionMapEntriesOffset, names.Offset);
        Assert.Equal(SyntheticRom.RegionLocationCount, names.Count);
    }

    [Fact]
    public void DecodesEveryLocationName()
    {
        RegionNameTable names = Locate();

        for (int i = 0; i < SyntheticRom.RegionLocationCount; i++)
        {
            if (i == SyntheticRom.DeadRegionNameIndex) continue;
            Assert.Equal(SyntheticRom.RegionNameFor(i), names[i]);
        }
    }

    [Fact]
    public void PrefersThePlaceNameTableOverAnotherRunOfTextPointers()
    {
        // Regression: the image also holds a run of text pointers whose contents are
        // not places. Picking by length or by address found that one instead.
        RegionNameTable names = Locate();

        Assert.NotEqual(SyntheticRom.DecoyNamePointersOffset, names.Offset);
        Assert.DoesNotContain(names.Names, n => n.StartsWith("EXIT"));
        Assert.Contains("PALLET TOWN", names.Names);
    }

    [Fact]
    public void ScansBothTableShapes()
    {
        List<RegionNameTable> candidates = RegionNameLocator.ScanCandidates(Synthetic.ToRom());

        Assert.Contains(candidates, c => c.Shape == "pointer array");
        Assert.Contains(candidates, c => c.Shape == "x,y,w,h + name");
    }

    [Fact]
    public void FallsBackToASectionIdWhenAnIndexIsOutOfRange()
    {
        Assert.Equal("SECTION 999", Locate()[999]);
    }

    [Fact]
    public void MapsResolveToTheirLocationName()
    {
        Rom rom = Synthetic.ToRom();
        MapBankTable table = MapBankLocator.Locate(rom)!;
        RegionNameTable names = RegionNameLocator.Locate(rom)!;

        foreach ((int bank, int map, MapHeaderRecord header) in table.AllMaps)
        {
            string expected = SyntheticRom.RegionNameFor(SyntheticRom.RegionSectionFor(bank, map));
            Assert.Equal(expected, names[header.RegionSectionId]);
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
        Assert.Equal(expected, RegionNameLocator.LooksLikeLocationName(candidate));
    }

    [Theory]
    [InlineData("PALLET TOWN", true)]
    [InlineData("VIRIDIAN FOREST", true)]
    [InlineData("ROUTE 22", true)]
    [InlineData("EXIT", false)]
    [InlineData("CANCEL", false)]
    public void JudgesWhetherANameReadsLikeAPlace(string candidate, bool expected)
    {
        Assert.Equal(expected, RegionNameLocator.ReadsLikeAPlace(candidate));
    }

    [Fact]
    public void StepsOverDeadSlotsInsteadOfSplittingTheTable()
    {
        // Regression: a gap in the name table used to end the run, leaving the table
        // split in two with only the first fragment kept — and every section id past
        // the gap unnamed.
        RegionNameTable names = Locate();

        Assert.Equal(SyntheticRom.RegionLocationCount, names.Count);
        Assert.Equal(string.Empty, names.Names[SyntheticRom.DeadRegionNameIndex]);
        Assert.Equal(
            SyntheticRom.RegionNameFor(SyntheticRom.DeadRegionNameIndex + 1),
            names[SyntheticRom.DeadRegionNameIndex + 1]);
    }

    [Fact]
    public void UnnamedSlotsFallBackToTheirSectionId()
    {
        Assert.StartsWith("SECTION", Locate()[SyntheticRom.DeadRegionNameIndex]);
    }

    [Fact]
    public void AlignsTheFirstNameWithTheLowestSectionIdInUse()
    {
        // A game sharing a codebase with another region carries that region's section
        // ids first, so the local ones start partway up and would otherwise all fall
        // off the end of the table.
        var table = new RegionNameTable(0, "test", ["PALLET TOWN", "VIRIDIAN CITY", "PEWTER CITY"]);

        int indexBase = table.InferIndexBase([88, 89, 90]);

        Assert.Equal(88, indexBase);
        Assert.Equal("PALLET TOWN", table.Resolve(88, indexBase));
        Assert.Equal("PEWTER CITY", table.Resolve(90, indexBase));
    }

    [Fact]
    public void LeavesTheBaseAtZeroWhenEverySectionIdAlreadyFits()
    {
        var table = new RegionNameTable(0, "test", ["A TOWN", "B TOWN", "C TOWN"]);

        Assert.Equal(0, table.InferIndexBase([0, 1, 2]));
        Assert.Equal("A TOWN", table.Resolve(0, 0));
    }
}
