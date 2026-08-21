using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The boundary sorted by what names the script that moves each flag (271). Four buckets in order
/// of strength, and a flag goes in the first it satisfies.
/// </summary>
public sealed class WhatTheBoundaryIsTests
{
    private const byte SetFlag = 0x29;
    private const byte Goto = 0x05;
    private const byte End = 0x02;

    private const int Opening = 0x200;   // setflag 0x0A ; setflag 0x0D ; end
    private const int Jumped = 0x300;    // setflag 0x0B ; end   <- goto at 0x100
    private const int Literal = 0x400;   // setflag 0x0C ; end   <- aligned word at 0x500
    private const int Nobody = 0x600;    // setflag 0x0D ; end   <- nothing

    private static Rom Image()
    {
        var image = new byte[0x1000];

        Array.Fill(image, (byte)0xFF);

        int at = Opening;
        image[at++] = SetFlag; image[at++] = 0x0A; image[at++] = 0x00;
        image[at++] = SetFlag; image[at++] = 0x0D; image[at++] = 0x00;
        image[at] = End;

        at = Jumped;
        image[at++] = SetFlag; image[at++] = 0x0B; image[at++] = 0x00;
        image[at] = End;

        at = Literal;
        image[at++] = SetFlag; image[at++] = 0x0C; image[at++] = 0x00;
        image[at] = End;

        at = Nobody;
        image[at++] = SetFlag; image[at++] = 0x0D; image[at++] = 0x00;
        image[at] = End;

        image[0x100] = Goto;
        Put(image, 0x101, 0x08000000 + Jumped);
        image[0x105] = End;

        Put(image, 0x500, 0x08000000 + Literal);

        return new Rom(image);
    }

    private static void Put(byte[] image, int at, uint value)
    {
        image[at] = (byte)value;
        image[at + 1] = (byte)(value >> 8);
        image[at + 2] = (byte)(value >> 16);
        image[at + 3] = (byte)(value >> 24);
    }

    private static FlagSite Site(int offset, int flag) => new(offset, flag, true, true, false);

    private static EverywhereInTheImage.OutsideTheWorld Flag(int flag, params FlagSite[] sites) =>
        new(flag, sites, []);

    [Fact]
    public void EachFlagGoesInTheStrongestBucketAnyOfItsSitesSupports()
    {
        Rom rom = Image();
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        EverywhereInTheImage.OutsideTheWorld[] boundary =
        [
            Flag(0x0A, Site(Opening, 0x0A)),
            Flag(0x0B, Site(Jumped, 0x0B)),
            Flag(0x0C, Site(Literal, 0x0C)),
            // 0x0D is moved by the opening AND by a site nothing names: the opening wins.
            Flag(0x0D, Site(Nobody, 0x0D), Site(Opening + 3, 0x0D)),
        ];

        IReadOnlyList<WhatTheBoundaryIs.Sorted> sorted =
            WhatTheBoundaryIs.Sort(rom, index, boundary, 0x08000000 + Opening);

        Assert.Equal(
            [WhatTheBoundaryIs.Named.TheOpening, WhatTheBoundaryIs.Named.AJumpsBlock,
             WhatTheBoundaryIs.Named.ALiteralsBlock, WhatTheBoundaryIs.Named.TheOpening],
            sorted.Select(s => s.By));

        Assert.Equal(0x101, sorted[1].What!.Offset);
        Assert.True(sorted[1].What!.AJump);
        Assert.Equal(0x500, sorted[2].What!.Offset);
        Assert.True(sorted[2].What!.ALiteral);
    }

    [Fact]
    public void WithoutAnOpeningTheOpeningBucketIsEmptyAndNothingIsNothing()
    {
        Rom rom = Image();
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        IReadOnlyList<WhatTheBoundaryIs.Sorted> sorted = WhatTheBoundaryIs.Sort(
            rom, index, [Flag(0x0A, Site(Opening, 0x0A)), Flag(0x0D, Site(Nobody, 0x0D))], null);

        Assert.Equal([WhatTheBoundaryIs.Named.Nothing, WhatTheBoundaryIs.Named.Nothing], sorted.Select(s => s.By));
        Assert.All(sorted, s => Assert.Null(s.What));
    }

    /// <summary>
    /// 270's finding, as a fixture: a site WITHIN 192 BYTES of a jump-named block but not on it is
    /// named by nothing. A window would have promoted it.
    /// </summary>
    [Fact]
    public void ASiteJustAfterAJumpedBlockIsNamedByNothing()
    {
        Rom rom = Image();
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = EverywhereInTheImage.PointerIndex(rom);

        // Jumped's block is four bytes; this setflag sits right after its end.
        byte[] bytes = rom.Span.ToArray();
        bytes[Jumped + 4] = SetFlag; bytes[Jumped + 5] = 0x0E; bytes[Jumped + 6] = 0x00; bytes[Jumped + 7] = End;
        rom = new Rom(bytes);
        index = EverywhereInTheImage.PointerIndex(rom);

        IReadOnlyList<WhatTheBoundaryIs.Sorted> sorted =
            WhatTheBoundaryIs.Sort(rom, index, [Flag(0x0E, Site(Jumped + 4, 0x0E))], null);

        Assert.Equal(WhatTheBoundaryIs.Named.Nothing, Assert.Single(sorted).By);
    }
}
