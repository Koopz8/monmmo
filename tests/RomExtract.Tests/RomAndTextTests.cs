using PokeMmo.RomExtract;

namespace PokeMmo.RomExtract.Tests;

public class GameTextTests
{
    [Theory]
    [InlineData("BULBASAUR")]
    [InlineData("PIKACHU")]
    [InlineData("MR. MIME")]
    [InlineData("HO-OH")]
    [InlineData("PORYGON2")]
    [InlineData("FARFETCH’D")]
    [InlineData("NIDORAN♂")]
    [InlineData("NIDORAN♀")]
    public void RoundTripsEveryNameShapeInTheSpeciesTable(string name)
    {
        byte[] encoded = GameText.Encode(name, GameText.SpeciesNameLength);

        Assert.Equal(GameText.SpeciesNameLength, encoded.Length);
        Assert.Equal(name, GameText.Decode(encoded));
        Assert.True(GameText.LooksLikeName(name));
    }

    [Fact]
    public void StopsAtTheTerminator()
    {
        byte[] bytes = [0xBC, 0xCF, 0xC6, GameText.Terminator, 0xBB, 0xBB];
        Assert.Equal("BUL", GameText.Decode(bytes));
    }

    [Fact]
    public void UnmappedBytesDecodeToQuestionMarksRatherThanThrowing()
    {
        byte[] junk = [0x01, 0x02, 0x03, GameText.Terminator];
        string decoded = GameText.Decode(junk);

        Assert.Equal("???", decoded);
        Assert.False(GameText.LooksLikeName(decoded));
    }

    [Fact]
    public void GarbageIsRejectedByTheNamePlausibilityCheck()
    {
        Assert.False(GameText.LooksLikeName(""));
        Assert.False(GameText.LooksLikeName("   "));
        Assert.False(GameText.LooksLikeName("AB?CD"));
    }

    [Fact]
    public void EncodingZeroFillsAfterTheTerminator()
    {
        // Regression: these tables are fixed-width arrays initialised from string
        // literals, so the tail is zero fill. Padding with more terminators produced
        // a search key that matched nothing on a real cartridge.
        byte[] encoded = GameText.Encode("BULBASAUR", GameText.SpeciesNameLength);

        Assert.Equal(GameText.Terminator, encoded[9]);
        Assert.Equal(0x00, encoded[10]);
    }

    [Fact]
    public void AnchorEncodingStopsRightAfterTheTerminator()
    {
        byte[] anchor = GameText.EncodeAnchor("BULBASAUR");

        Assert.Equal(10, anchor.Length);
        Assert.Equal(GameText.Terminator, anchor[^1]);

        // The anchor must be a prefix of the real record regardless of how the
        // remaining field width is padded.
        byte[] record = GameText.Encode("BULBASAUR", GameText.SpeciesNameLength);
        Assert.Equal(anchor, record[..anchor.Length]);
    }

    [Fact]
    public void EncodingTruncatesRatherThanOverflowingTheField()
    {
        byte[] encoded = GameText.Encode("ABCDEFGHIJKLMNOP", GameText.SpeciesNameLength);

        Assert.Equal(GameText.SpeciesNameLength, encoded.Length);
        Assert.Equal(GameText.Terminator, encoded[^1]);
        Assert.Equal("ABCDEFGHIJ", GameText.Decode(encoded));
    }
}

public class RomTests
{
    [Fact]
    public void ParsesTheCartridgeHeader()
    {
        Rom rom = new SyntheticRom().ToRom();

        Assert.Equal("POKEMON FIRE", rom.Title);
        Assert.Equal("BPRE", rom.GameCode);
        Assert.Equal("01", rom.MakerCode);
        Assert.Equal(0, rom.Version);
    }

    [Fact]
    public void ComputesAStableSha1()
    {
        var synthetic = new SyntheticRom();
        Rom a = synthetic.ToRom();
        Rom b = synthetic.ToRom();

        Assert.Equal(40, a.Sha1.Length);
        Assert.Equal(a.Sha1, b.Sha1);
        Assert.Equal(a.Sha1, a.Sha1); // cached path returns the same value
    }

    [Fact]
    public void IdentifiesFireRedFromTheHeader()
    {
        RomIdentity identity = RomIdentity.Identify(new SyntheticRom().ToRom());

        Assert.Equal(RomGame.FireRed, identity.Game);
        Assert.True(identity.IsFireRed);
    }

    [Fact]
    public void FlagsAnImageWhoseHashIsNotKnownGood()
    {
        // The synthetic cartridge is obviously not a real dump, so it must not be
        // reported as one — this is the check that stops a modified or corrupt file
        // from silently passing as verified.
        RomIdentity identity = RomIdentity.Identify(new SyntheticRom().ToRom());

        Assert.False(identity.Sha1IsKnown);
        Assert.Contains("not in the known-good list", identity.Description);
    }

    [Fact]
    public void RecognisesRomAddressesAndRejectsEverythingElse()
    {
        Rom rom = new SyntheticRom().ToRom();

        Assert.True(rom.IsRomAddress(Rom.BaseAddress));
        Assert.True(rom.IsRomAddress(Rom.BaseAddress + 0x1000));
        Assert.False(rom.IsRomAddress(0));
        Assert.False(rom.IsRomAddress(0x0200_0000));                        // EWRAM, not cartridge
        Assert.False(rom.IsRomAddress(Rom.BaseAddress + SyntheticRom.RomSize));
    }

    [Fact]
    public void RefusesToDereferenceAPointerOutsideTheImage()
    {
        Rom rom = new SyntheticRom().ToRom();
        Assert.Throws<ArgumentOutOfRangeException>(() => rom.ToOffset(0x0900_0000));
    }

    [Fact]
    public void RefusesASliceThatEscapesTheImage()
    {
        Rom rom = new SyntheticRom().ToRom();
        Assert.Throws<ArgumentOutOfRangeException>(() => rom.Slice(SyntheticRom.RomSize - 4, 16));
    }

    [Fact]
    public void RejectsAFileTooSmallToBeACartridge()
    {
        Assert.Throws<ArgumentException>(() => new Rom(new byte[32]));
    }

    [Fact]
    public void ReadsLittleEndianScalars()
    {
        var bytes = new byte[0x200];
        bytes[0x100] = 0x78; bytes[0x101] = 0x56; bytes[0x102] = 0x34; bytes[0x103] = 0x12;

        var rom = new Rom(bytes);

        Assert.Equal(0x78, rom.ReadU8(0x100));
        Assert.Equal(0x5678, rom.ReadU16(0x100));
        Assert.Equal(0x12345678u, rom.ReadU32(0x100));
    }
}
