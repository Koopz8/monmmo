using PokeMmo.Core.Data;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Graphics;

namespace PokeMmo.RomExtract.Tests;

public class TableLocatorTests
{
    private static RomTables Locate() => TableLocator.Locate(new SyntheticRom().ToRom());

    [Fact]
    public void FindsEveryTableAtTheOffsetItWasWrittenTo()
    {
        RomTables tables = Locate();

        Assert.Equal(SyntheticRom.SpeciesNamesOffset, tables.SpeciesNames?.Offset);
        Assert.Equal(SyntheticRom.BaseStatsOffset, tables.BaseStats?.Offset);
        Assert.Equal(SyntheticRom.FrontPicTableOffset, tables.FrontPics?.Offset);
        Assert.Equal(SyntheticRom.BackPicTableOffset, tables.BackPics?.Offset);
        Assert.Equal(SyntheticRom.NormalPaletteTableOffset, tables.NormalPalettes?.Offset);
        Assert.Equal(SyntheticRom.ShinyPaletteTableOffset, tables.ShinyPalettes?.Offset);
    }

    [Fact]
    public void ReportsCartridgeAddressesNotJustFileOffsets()
    {
        RomTables tables = Locate();
        Assert.Equal(Rom.BaseAddress + SyntheticRom.BaseStatsOffset, tables.BaseStats!.Address);
    }

    [Fact]
    public void CountsEverySpriteTableEntry()
    {
        RomTables tables = Locate();

        Assert.Equal(SyntheticRom.SpeciesCount, tables.FrontPics!.EntryCount);
        Assert.Equal(SyntheticRom.SpeciesCount, tables.BackPics!.EntryCount);
        Assert.Equal(SyntheticRom.SpeciesCount, tables.NormalPalettes!.EntryCount);
    }

    [Fact]
    public void FindsNothingInAnImageThatHasNoTables()
    {
        // A cartridge-sized buffer of noise must not yield confident false positives.
        var noise = new byte[1024 * 1024];
        new Random(99).NextBytes(noise);

        RomTables tables = TableLocator.Locate(new Rom(noise));

        Assert.Null(tables.SpeciesNames);
        Assert.Null(tables.BaseStats);
        Assert.Empty(tables.All);
    }

    [Fact]
    public void RejectsAnAnchorThatIsNotFollowedByARealTable()
    {
        // Plant only the anchor bytes, with nothing valid after them. A locator that
        // trusted the anchor alone would report a table here.
        var data = new byte[512 * 1024];
        byte[] nameAnchor = GameText.Encode("BULBASAUR", GameText.SpeciesNameLength);
        nameAnchor.CopyTo(data, 0x2000);

        byte[] statAnchor = [45, 49, 49, 45, 65, 65, 12, 3, 45, 64];
        statAnchor.CopyTo(data, 0x4000);

        RomTables tables = TableLocator.Locate(new Rom(data));

        Assert.Null(tables.SpeciesNames);
        Assert.Null(tables.BaseStats);
    }

    [Fact]
    public void EmitsDiagnosticsExplainingWhatItFound()
    {
        var messages = new List<string>();
        TableLocator.Locate(new SyntheticRom().ToRom(), messages.Add);

        Assert.Contains(messages, m => m.Contains("species names"));
        Assert.Contains(messages, m => m.Contains("base stats"));
        Assert.Contains(messages, m => m.Contains("pic table"));
        Assert.Contains(messages, m => m.Contains("palette table"));
    }
}

public class SpeciesExtractionTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static RomExtractor Open() => RomExtractor.Open(Synthetic.ToRom());

    [Fact]
    public void DecodesTheAnchorSpeciesExactly()
    {
        SpeciesData species = Open().ExtractSpecies()[SyntheticRom.TestSpecies];

        Assert.Equal("BULBASAUR", species.Name);
        Assert.Equal(45, species.BaseHp);
        Assert.Equal(49, species.BaseAttack);
        Assert.Equal(49, species.BaseDefense);
        Assert.Equal(45, species.BaseSpeed);
        Assert.Equal(65, species.BaseSpAttack);
        Assert.Equal(65, species.BaseSpDefense);
        Assert.Equal(PokemonType.Grass, species.Type1);
        Assert.Equal(PokemonType.Poison, species.Type2);
        Assert.Equal(45, species.CatchRate);
        Assert.Equal(64, species.ExpYield);
        Assert.Equal(GrowthRate.MediumSlow, species.GrowthRate);
        Assert.Equal(EggGroup.Monster, species.EggGroup1);
        Assert.Equal(EggGroup.Grass, species.EggGroup2);
        Assert.Equal(318, species.BaseStatTotal);
    }

    [Fact]
    public void ReadsTheWholeTable()
    {
        List<SpeciesData> species = Open().ExtractSpecies();
        Assert.Equal(TableLocator.DefaultSpeciesCount, species.Count);
    }

    [Fact]
    public void PairsEveryRecordWithItsName()
    {
        List<SpeciesData> species = Open().ExtractSpecies();

        for (int i = 1; i < 200; i++)
            Assert.Equal(SyntheticRom.NameFor(i), species[i].Name);
    }

    [Fact]
    public void UnpacksTheSixPackedEffortValueFields()
    {
        // Bits 0-1 hold the HP yield; the record under test sets exactly that field.
        SpeciesData species = Open().ExtractSpecies()[SyntheticRom.TestSpecies];

        Assert.Equal(1, species.EvHp);
        Assert.Equal(0, species.EvAttack);
        Assert.Equal(0, species.EvSpDefense);
    }

    [Fact]
    public void ParsingRejectsAShortRecord()
    {
        Assert.Throws<ArgumentException>(() => SpeciesData.Parse(new byte[10], 0));
    }

    [Fact]
    public void SeparatesBodyColourFromTheNoFlipFlag()
    {
        var record = new byte[SpeciesData.SizeBytes];
        record[0] = 50;
        record[26] = 0x80 | 5;

        SpeciesData species = SpeciesData.Parse(record, 1);

        Assert.Equal(5, species.BodyColor);
        Assert.True(species.NoFlip);
    }
}

public class SpriteExtractionTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static RomExtractor Open() => RomExtractor.Open(Synthetic.ToRom());

    [Fact]
    public void RecoversTheSpritePixelsByteForByte()
    {
        ExtractedSprite sprite = Open().ExtractSprite(SyntheticRom.TestSpecies);

        Assert.Equal(64, sprite.Image.Width);
        Assert.Equal(64, sprite.Image.Height);
        Assert.Equal(Synthetic.ExpectedFrontImage.Pixels, sprite.Image.Pixels);
    }

    [Fact]
    public void RecoversThePaletteExactly()
    {
        ExtractedSprite sprite = Open().ExtractSprite(SyntheticRom.TestSpecies);

        for (int i = 0; i < GbaPalette.ColorCount; i++)
            Assert.Equal(Synthetic.ExpectedPalette[i], sprite.Palette[i]);
    }

    [Fact]
    public void ReadsTheBackSpriteFromTheSecondTable()
    {
        ExtractedSprite sprite = Open().ExtractSprite(SyntheticRom.TestSpecies, back: true);
        Assert.Equal(Synthetic.ExpectedBackImage.Pixels, sprite.Image.Pixels);
    }

    [Fact]
    public void ReadsTheShinyPaletteFromTheSecondPaletteTable()
    {
        ExtractedSprite sprite = Open().ExtractSprite(SyntheticRom.TestSpecies, shiny: true);

        for (int i = 0; i < GbaPalette.ColorCount; i++)
            Assert.Equal(Synthetic.ExpectedShinyPalette[i], sprite.Palette[i]);

        // Same pixels, different colours — which is exactly what shininess is.
        Assert.Equal(Synthetic.ExpectedFrontImage.Pixels, sprite.Image.Pixels);
    }

    [Fact]
    public void ProducesAPngWithATransparentBackgroundIndex()
    {
        ExtractedSprite sprite = Open().ExtractSprite(SyntheticRom.TestSpecies);
        byte[] rgba = sprite.Image.ToRgba(sprite.Palette);

        for (int i = 0; i < sprite.Image.Pixels.Length; i++)
        {
            byte expectedAlpha = (byte)(sprite.Image.Pixels[i] == 0 ? 0 : 255);
            Assert.Equal(expectedAlpha, rgba[i * 4 + 3]);
        }

        Assert.NotEmpty(sprite.ToPng());
    }

    [Fact]
    public void RejectsASpeciesIndexOutsideTheTable()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Open().ExtractSprite(99999));
        Assert.Throws<ArgumentOutOfRangeException>(() => Open().ExtractSprite(-1));
    }

    [Fact]
    public void FailsLoudlyWhenATableIsMissing()
    {
        var noise = new byte[1024 * 1024];
        new Random(5).NextBytes(noise);

        RomExtractor extractor = RomExtractor.Open(new Rom(noise));

        Assert.Throws<InvalidOperationException>(() => extractor.ExtractSprite(1));
        Assert.Throws<InvalidOperationException>(() => extractor.ExtractSpecies());
    }
}
