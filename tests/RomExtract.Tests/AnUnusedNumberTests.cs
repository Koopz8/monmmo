using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The nudge for a three-byte sweep (272): the same sweep, asked for numbers the cartridge does
/// not use, with the same high byte.
/// </summary>
public sealed class AnUnusedNumberTests
{
    private const byte SetFlag = 0x29;
    private const byte SetVar = 0x16;
    private const byte End = 0x02;

    [Fact]
    public void NeighboursKeepTheHighByteAndSkipWhatIsUsed()
    {
        IReadOnlyList<int> found = AnUnusedNumber.Neighbours(0x0102, v => v is 0x0101 or 0x0103, 4);

        // 0x0101 and 0x0103 are used; the next out each way are 0x0100 and 0x0104, then nothing
        // below (0x0100 is the band's floor) and 0x0105, 0x0106 above.
        Assert.Equal([0x0100, 0x0104, 0x0105, 0x0106], found);
        Assert.All(found, n => Assert.Equal(0x0100, n & ~0xFF));
    }

    [Fact]
    public void NeighboursStopAtTheBandsTopAndNeverCrossIntoTheNextHighByte()
    {
        IReadOnlyList<int> found = AnUnusedNumber.Neighbours(0x01FE, _ => false, 4);

        Assert.Equal([0x01FB, 0x01FC, 0x01FD, 0x01FF], found);
        Assert.DoesNotContain(0x0200, found);
        Assert.All(found, n => Assert.Equal(0x0100, n & ~0xFF));
    }

    [Fact]
    public void NeighboursNeverIncludeTheNumberItself()
    {
        Assert.DoesNotContain(0x0042, AnUnusedNumber.Neighbours(0x0042, _ => false, 16));
    }

    /// <summary>
    /// The floor is the sweep: a neighbour with three <c>setflag</c>s planted for it, one of which
    /// reads on to an end, comes back as 3 and 1.
    /// </summary>
    [Fact]
    public void TheFlagFloorIsTheSameSweepAskedOfTheNeighbours()
    {
        var image = new byte[0x400];

        Array.Fill(image, (byte)0xFF);

        // Two bare setflag 0x0211s in junk, and one followed by an end.
        image[0x10] = SetFlag; image[0x11] = 0x11; image[0x12] = 0x02;
        image[0x40] = SetFlag; image[0x41] = 0x11; image[0x42] = 0x02;
        image[0x80] = SetFlag; image[0x81] = 0x11; image[0x82] = 0x02; image[0x83] = End;

        // And one for the number being asked about, which must not count toward its own floor.
        image[0xC0] = SetFlag; image[0xC1] = 0x10; image[0xC2] = 0x02; image[0xC3] = End;

        var rom = new Rom(image);

        AnUnusedNumber.Floor floor = AnUnusedNumber.ForAFlag(rom, 0x0210, v => v != 0x0211, 4);

        AnUnusedNumber.Found only = Assert.Single(floor.Neighbours);

        Assert.Equal(0x0211, only.Number);
        Assert.Equal(3, only.Sites);
        Assert.Equal(1, only.ReadsAsScript);
        Assert.Equal(3, floor.MaxSites);
        Assert.Equal(0x0211, floor.MaxSitesAt);
    }

    [Fact]
    public void TheVariableFloorCountsEveryWayANumberGetsIntoAVariable()
    {
        var image = new byte[0x400];

        Array.Fill(image, (byte)0xFF);

        image[0x10] = SetVar; image[0x11] = 0x21; image[0x12] = 0x40; image[0x13] = 0x05; image[0x14] = 0x00; image[0x15] = End;

        var rom = new Rom(image);

        AnUnusedNumber.Floor floor = AnUnusedNumber.ForAVariable(rom, 0x4020, v => v != 0x4021, 2);

        AnUnusedNumber.Found only = Assert.Single(floor.Neighbours);

        Assert.Equal(1, only.Sites);
        Assert.Equal(1, only.ReadsAsScript);
    }

    [Fact]
    public void TheMedianIsTheMiddleNeighbourAndAnEmptyFloorIsNought()
    {
        var floor = new AnUnusedNumber.Floor(1,
        [
            new AnUnusedNumber.Found(2, 1, 0),
            new AnUnusedNumber.Found(3, 40, 9),
            new AnUnusedNumber.Found(4, 3, 1),
        ]);

        Assert.Equal(3, floor.MedianSites);
        Assert.Equal(1, floor.MedianReads);
        Assert.Equal(40, floor.MaxSites);
        Assert.Equal(3, floor.MaxSitesAt);
        Assert.Equal(3, floor.Over);

        var empty = new AnUnusedNumber.Floor(1, []);

        Assert.Equal(0, empty.MedianSites);
        Assert.Equal(0, empty.MaxSites);
    }
}
