using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Maps;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The sign kind byte, and which side of a sign it names (279).
/// <para>
/// This project read the kind as two values — the buried kind and everything else — and it takes
/// FIVE. All 519 of the four script kinds hold a ROM pointer and none of the 183 buried ones does,
/// so the byte separates the two record shapes perfectly. And three of the four script kinds name
/// a side: <c>0x01</c>'s south neighbour is walkable on 73 of 73, <c>0x03</c>'s west on 14 of 14,
/// <c>0x04</c>'s east on 10 of 10, where the commonest kind's own rates are 87%, 55% and 47%.
/// </para>
/// </summary>
public sealed class WhichWayASignIsReadTests
{
    private const int North = 0;
    private const int South = 1;
    private const int West = 2;
    private const int East = 3;

    private static bool[] Open(params int[] sides) =>
        [.. Enumerable.Range(0, 4).Select(sides.Contains)];

    /// <summary>The side every one of them has open is the side the kind names.</summary>
    [Fact]
    public void TheSideOpenOnEveryOneIsTheSideNamed()
    {
        Assert.Equal(
            South,
            WhichWayASignIsRead.TheSideAllOfThemHaveOpen(
                [Open(South), Open(South, North), Open(South, West, East)]));
    }

    /// <summary>
    /// <b>One side open on every one but ONE is no side at all.</b> The rule is about a whole
    /// kind: a single sign that does not have it open is a sign the rule would be wrong about, and
    /// 73-of-73 is the reading rather than 72-of-73.
    /// </summary>
    [Fact]
    public void OneExceptionIsEnoughToNameNoSide()
    {
        Assert.Null(
            WhichWayASignIsRead.TheSideAllOfThemHaveOpen(
                [Open(South), Open(South), Open(North)]));
    }

    /// <summary>
    /// <b>TWO sides open on every one names NEITHER.</b> Picking the first would be a verdict
    /// about the order the loop ran in, dressed as a verdict about the cartridge — and it is the
    /// break this refuses: a version that returned the first qualifying side passes every other
    /// fixture here.
    /// </summary>
    [Fact]
    public void TwoSidesOpenOnEveryOneNamesNeither()
    {
        Assert.Null(
            WhichWayASignIsRead.TheSideAllOfThemHaveOpen(
                [Open(South, East), Open(South, East), Open(South, East, North)]));
    }

    /// <summary>
    /// No signs is no side, rather than every side. A side that all of nothing has open is all
    /// four of them, which is the same ambiguity with a smaller denominator.
    /// </summary>
    [Fact]
    public void NoSignsNamesNoSide()
    {
        Assert.Null(WhichWayASignIsRead.TheSideAllOfThemHaveOpen([]));
    }

    /// <summary>And a kind with nothing open anywhere names nothing.</summary>
    [Fact]
    public void NothingOpenNamesNoSide()
    {
        Assert.Null(WhichWayASignIsRead.TheSideAllOfThemHaveOpen([Open(), Open()]));
    }

    /// <summary>Across is the opposite side, which is the control on any side reading.</summary>
    [Fact]
    public void AcrossIsTheOppositeSide()
    {
        Assert.Equal(South, WhichWayASignIsRead.Across(North));
        Assert.Equal(North, WhichWayASignIsRead.Across(South));
        Assert.Equal(East, WhichWayASignIsRead.Across(West));
        Assert.Equal(West, WhichWayASignIsRead.Across(East));
    }

    /// <summary>Every side has an opposite, and it is never itself.</summary>
    [Fact]
    public void EverySideHasAnOppositeAndItIsNotItself()
    {
        foreach (int side in new[] { North, South, West, East })
        {
            Assert.NotEqual(side, WhichWayASignIsRead.Across(side));
            Assert.Equal(side, WhichWayASignIsRead.Across(WhichWayASignIsRead.Across(side)));
        }
    }

    // ------------------------------------------------------------ the kind byte

    private const byte SignSize = 12;

    /// <summary>
    /// A map with one sign list: <paramref name="kinds"/> gives each sign's kind byte, and every
    /// sign whose kind is not the buried one is given a ROM pointer in its last four bytes.
    /// </summary>
    private static (Rom Rom, uint Events) Image(params int[] kinds)
    {
        var image = new byte[0x4000];

        const int Events = 0x100;
        const int Signs = 0x200;

        // The events record: counts of people, warps, triggers, signs then four pointers.
        image[Events + 3] = (byte)kinds.Length;

        void Pointer(int at, uint to)
        {
            image[at] = (byte)to;
            image[at + 1] = (byte)(to >> 8);
            image[at + 2] = (byte)(to >> 16);
            image[at + 3] = (byte)(to >> 24);
        }

        Pointer(Events + 4 + 12, 0x08000000 + Signs);

        for (var i = 0; i < kinds.Length; i++)
        {
            int at = Signs + (i * SignSize);

            image[at] = (byte)i;
            image[at + 5] = (byte)kinds[i];

            Pointer(at + 8, kinds[i] == 7 ? 0x00010000u + (uint)i : 0x08001234u);
        }

        return (new Rom(image), 0x08000000 + Events);
    }

    /// <summary>
    /// <b>The kinds are counted, not assumed.</b> Asserting "there are two" is what this project
    /// did for thirty milestones; the tally is what found five.
    /// </summary>
    [Fact]
    public void EveryKindByteIsTallied()
    {
        (Rom rom, uint events) = Image(0, 0, 1, 7, 7, 4);

        (IReadOnlyDictionary<int, int> kinds, IReadOnlyDictionary<int, int> pointers) =
            WhatIsBuried.KindsOfSign(rom, [Map(events)]);

        Assert.Equal(4, kinds.Count);
        Assert.Equal(2, kinds[0]);
        Assert.Equal(1, kinds[1]);
        Assert.Equal(2, kinds[7]);
        Assert.Equal(1, kinds[4]);

        // And which of them hold a pointer, which is the other half of the reading.
        Assert.Equal(2, pointers[0]);
        Assert.Equal(1, pointers[1]);
        Assert.Equal(1, pointers[4]);
        Assert.False(pointers.ContainsKey(7));
    }

    /// <summary>
    /// <c>EverySign</c> is every sign and not only the buried ones — the list the kind reading
    /// needs and the one <c>WhatIsBuried.In</c> deliberately does not give.
    /// </summary>
    [Fact]
    public void EverySignIsEverySignAndNotOnlyTheBuriedOnes()
    {
        (Rom rom, uint events) = Image(0, 1, 7, 3);

        List<WhatIsBuried.ASign> all = WhatIsBuried.EverySign(rom, [Map(events)]);

        Assert.Equal(4, all.Count);
        Assert.Equal([0, 1, 7, 3], all.Select(one => one.Kind));

        // One of them is buried, and the buried reader agrees about which.
        Assert.Single(WhatIsBuried.In(rom, [Map(events)]));
    }

    private static LoadedMap Map(uint events) =>
        new("fixture", 1, 0, 0, 0, [], new Core.World.CollisionGrid(1, 1, [0]))
        {
            EventsPointer = events,
        };
}
