using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Every script block in the file, rather than every script block a map leads to.
/// <para>
/// The map scan opens <b>3888</b> blocks. Reachable from an aligned pointer somewhere in the image
/// there are <b>10240</b>, against a reversed-image floor of 456 — so <b>6621 blocks no map leads
/// to</b>, and every sweep this project has ever run over "the scripts" has run over about a
/// third of them.
/// </para>
/// <para>
/// What it cannot do is answer 252's question. <c>compare</c>'s variable operand — the row this
/// whole method is calibrated on — scores 92% over the entries the maps DO lead to and <b>27%</b>
/// over the ones they do not, in the same run of the same instrument. That is the code boundary
/// seen from a new side, and it is a population this test cannot be run on.
/// </para>
/// </summary>
public sealed class EveryScriptInTheImageTests
{
    private const byte SetVar = 0x16;
    private const byte End = 0x02;
    private const byte Goto = 0x05;

    private const int Aligned = 0x40;
    private const int Unaligned = 0x51;
    private const int ToTheShortOne = 0x48;
    private const int ToNothing = 0x4C;

    private const uint Short = 0x08000300;
    private const uint Long = 0x08000200;
    private const uint Nowhere = 0x08000700;

    /// <summary>
    /// An image with two scripts, a one-command block, and four pointers at them.
    /// </summary>
    /// <remarks>
    /// Zero-filled around the edges on purpose and safely: a run of zeros decodes as no-ops
    /// forever and never reaches an end, so it does not read as a script — which is the one thing
    /// trap 1 warns about and the one thing this fixture needs to be true.
    /// </remarks>
    private static Rom Image()
    {
        var image = new byte[0x1000];

        void Pointer(int at, uint address)
        {
            image[at] = (byte)address;
            image[at + 1] = (byte)(address >> 8);
            image[at + 2] = (byte)(address >> 16);
            image[at + 3] = (byte)(address >> 24);
        }

        // The long one: four setvars and an end, then a goto into a block nothing points at.
        var to = 0x200;

        for (var i = 0; i < 4; i++)
        {
            image[to++] = SetVar;
            image[to++] = (byte)(0x01 + i);
            image[to++] = 0x40;
            image[to++] = 0x05;
            image[to++] = 0x00;
        }

        image[to++] = Goto;

        Pointer(to, 0x08000600);

        // The block only the goto leads to.
        image[0x600] = SetVar;
        image[0x601] = 0x0A;
        image[0x602] = 0x40;
        image[0x603] = 0x01;
        image[0x604] = 0x00;
        image[0x605] = End;

        // The short one: an end and nothing else.
        image[0x300] = End;

        Pointer(Aligned, Long);
        Pointer(ToTheShortOne, Short);
        Pointer(ToNothing, Nowhere);
        Pointer(Unaligned, Long);

        return new Rom(image);
    }

    /// <summary>
    /// AN ADDRESS SOMETHING POINTS AT, WHICH DECODES TO A PROPER END, IS A WAY IN — and one that
    /// does not decode is not, however many things point at it. Both, because the pointer test on
    /// its own finds tens of thousands of accidents in sixteen megabytes.
    /// </summary>
    [Fact]
    public void APointedAddressThatReadsAsAScriptIsAWayInAndOneThatDoesNotIsNot()
    {
        TheImagesScripts found = EveryScriptInTheImage.In(Image());

        Assert.Contains(Long, found.Entries);
        Assert.Contains(Short, found.Entries);
        Assert.DoesNotContain(Nowhere, found.Entries);
    }

    /// <summary>
    /// ALIGNMENT IS THE FILTER AND IT IS A PARAMETER. A pointer the game's own code holds sits in
    /// a literal pool or a table and is aligned; a pointer a script holds is a <c>call</c>'s
    /// argument and is not, and its block arrives through the caller's reach anyway. Measured on
    /// the cartridge the floor falls 2616 to 456 while the count falls 13270 to 10240 — which is
    /// the argument for it, and it is an argument only if the loose answer is also available.
    /// </summary>
    [Fact]
    public void AnUnalignedPointerIsOnlyAWayInWhenTheFilterIsOff()
    {
        var image = new byte[0x1000];

        image[0x300] = End;
        image[Unaligned] = 0x00;
        image[Unaligned + 1] = 0x03;
        image[Unaligned + 2] = 0x00;
        image[Unaligned + 3] = 0x08;

        var rom = new Rom(image);

        Assert.DoesNotContain(Short, EveryScriptInTheImage.In(rom).Entries);
        Assert.Contains(Short, EveryScriptInTheImage.In(rom, aligned: false).Entries);
    }

    /// <summary>
    /// AND WHAT AN ENTRY REACHES IS A BLOCK EVEN WHEN NOTHING POINTS AT IT. A script's own
    /// <c>goto</c> is how most of this cartridge's blocks are named, and a population of entries
    /// alone would be a population of front doors.
    /// </summary>
    [Fact]
    public void ABlockOnlyAGotoNamesIsStillInThePopulation()
    {
        TheImagesScripts found = EveryScriptInTheImage.In(Image());

        Assert.DoesNotContain(0x08000600u, found.Entries);
        Assert.Contains(0x08000600u, found.Blocks);
    }

    /// <summary>
    /// LENGTH IS THE OTHER HALF OF THE LUCK. Three bytes that decode and hit an end is a common
    /// accident and a real script is several commands — so the threshold drops the one-command
    /// block and keeps the five-command one.
    /// </summary>
    [Fact]
    public void TheLengthThresholdDropsTheShortBlockAndKeepsTheLongOne()
    {
        TheImagesScripts found = EveryScriptInTheImage.In(Image(), leastCommands: 4);

        Assert.Contains(Long, found.Entries);
        Assert.DoesNotContain(Short, found.Entries);

        // And at a length of one it is there, so the assertion above is about the threshold.
        Assert.Contains(Short, EveryScriptInTheImage.In(Image(), leastCommands: 1).Entries);
    }

    /// <summary>
    /// THE ADDRESSES CONSIDERED ARE COUNTED THE SAME WAY THEY ARE FILTERED. `Pointed` is the
    /// denominator for "how many decoded", and a denominator counting addresses the filter threw
    /// away makes the hit rate look worse the tighter the filter gets.
    /// </summary>
    [Fact]
    public void ThePointedCountIsOfTheAddressesTheFilterKept()
    {
        Rom rom = Image();

        Assert.Equal(3, EveryScriptInTheImage.In(rom).Pointed);
        Assert.Equal(4, EveryScriptInTheImage.In(rom, aligned: false).Pointed);
    }

    /// <summary>
    /// AND THE FLOOR IS THE SAME HUNT ON THE IMAGE BACKWARDS. Reversing keeps every byte and
    /// every byte's frequency and destroys every command boundary — so what it finds is what this
    /// hunt would find in a file with these statistics and no scripts in it. On this fixture that
    /// is nothing, and on the cartridge it is 456 blocks against 10240.
    /// </summary>
    [Fact]
    public void TheFloorIsTheHuntOnTheReversedImage()
    {
        Assert.Empty(EveryScriptInTheImage.Floor(Image()).Entries);
        Assert.NotEmpty(EveryScriptInTheImage.In(Image()).Entries);
    }
}
