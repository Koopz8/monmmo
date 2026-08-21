using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Which side of a sign the walk may read it from (279, 280).
/// <para>
/// 242 had this project read a sign from its own square or any of the four around it. 279 counted
/// the sign record's KIND byte — which this project had treated as two values and which takes five
/// — and found three of the four script kinds name a side: <c>0x01</c>'s south neighbour is
/// walkable on 73 of 73, <c>0x03</c>'s west on 14 of 14, <c>0x04</c>'s east on 10 of 10, against
/// the commonest kind's own 87.2%, 54.7% and 46.9%.
/// </para>
/// <para>
/// So 97 of the 519 sign scripts are readable from ONE square and the walk was reading them from
/// four. What it costs is measured in one process rather than across two builds (241):
/// <c>obeySignSides</c> is the control, and the answer is 0 maps, 0 flags and 2 signs at the floor
/// and at the widest.
/// </para>
/// </summary>
public sealed class WhichSideTheSignIsReadFromTests
{
    private const uint TheSign = 0x3000;
    private const int Moved = 0x0500;

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// A five-by-five room, walkable everywhere except the squares named — a sign's own square is
    /// solid, which is what a sign is (242).
    /// </summary>
    private static MapData Room(params (int X, int Y)[] solid)
    {
        var collision = new byte[25];

        foreach ((int x, int y) in solid) collision[(y * 5) + x] = 1;

        return new MapData("1.0", "room", 5, 5, collision);
    }

    private static Attempt Run(MapData map, bool obeySignSides = true) =>
        Autoplayer.Play(
            new WorldData([map]),
            map.Id,
            TestRules.All,
            (address, _, _) => Nothing with { FlagsSet = [Moved + (int)(address >> 12)] },
            obeySignSides: obeySignSides);

    private static bool ReadIt(Attempt played) =>
        played.FlagMoves.Any(m => m.Address == TheSign && m.From == WhatRanIt.ASign);

    // ------------------------------------------------------------ the record

    /// <summary>
    /// The three kinds that name a side name the square on that side, and the two that do not
    /// name nothing.
    /// </summary>
    [Fact]
    public void OnlyThreeKindsNameASquare()
    {
        Assert.Equal(new GridPosition(2, 3), new MapSign(2, 2, MapSign.FromTheSouth, 1).MustBeReadFrom);
        Assert.Equal(new GridPosition(1, 2), new MapSign(2, 2, MapSign.FromTheWest, 1).MustBeReadFrom);
        Assert.Equal(new GridPosition(3, 2), new MapSign(2, 2, MapSign.FromTheEast, 1).MustBeReadFrom);

        Assert.Null(new MapSign(2, 2, 0, 1).MustBeReadFrom);
        Assert.Null(new MapSign(2, 2, MapSign.HiddenItem, 0).MustBeReadFrom);
    }

    /// <summary>
    /// <b>And 0x02 names nothing.</b> By elimination it would be north, and this cartridge has
    /// none of them — so naming it would be an inference with no record behind it, and the
    /// project's rule is that an inference does not go in a READ table (67).
    /// </summary>
    [Fact]
    public void TheKindThisCartridgeNeverUsesNamesNothing()
    {
        Assert.Null(new MapSign(2, 2, 2, 1).MustBeReadFrom);
    }

    // ------------------------------------------------------------ the walk

    /// <summary>
    /// A sign whose kind names no side is read from a neighbour, which is what 242 established
    /// and what the other 422 signs still do.
    /// </summary>
    [Fact]
    public void ASignThatNamesNoSideIsStillReadFromAnyNeighbour()
    {
        MapData room = Room((2, 2), (2, 3)) with
        {
            Signs = [new MapSign(2, 2, Kind: 0, TheSign)],
        };

        Assert.True(ReadIt(Run(room)));
    }

    /// <summary>
    /// <b>THE THING.</b> The same sign, the same room, the same walk — with a kind that names the
    /// SOUTH, and the south square walled off. Three of its four neighbours are open and the walk
    /// stands on all of them, and the sign is not read.
    /// </summary>
    [Fact]
    public void ASignThatNamesASideIsNotReadFromAnyOther()
    {
        MapData room = Room((2, 2), (2, 3)) with
        {
            Signs = [new MapSign(2, 2, MapSign.FromTheSouth, TheSign)],
        };

        Assert.False(ReadIt(Run(room)));
    }

    /// <summary>
    /// And with the side ignored it IS read — which is the control, in the same process, and the
    /// reason the difference is a subtraction rather than a memory of an earlier build (241).
    /// </summary>
    [Fact]
    public void AndTheControlReadsItFromWhereverItCan()
    {
        MapData room = Room((2, 2), (2, 3)) with
        {
            Signs = [new MapSign(2, 2, MapSign.FromTheSouth, TheSign)],
        };

        Assert.False(ReadIt(Run(room)));
        Assert.True(ReadIt(Run(room, obeySignSides: false)));
    }

    /// <summary>
    /// The side it names is the side it is read from — open that square and the same sign runs.
    /// Without this the rule would be satisfied by never reading a sign of that kind at all.
    /// </summary>
    [Fact]
    public void OpenTheSideItNamesAndItIsReadAgain()
    {
        MapData room = Room((2, 2)) with
        {
            Signs = [new MapSign(2, 2, MapSign.FromTheSouth, TheSign)],
        };

        Assert.True(ReadIt(Run(room)));
    }

    /// <summary>
    /// <b>And the OTHER two kinds, each with only the wrong side open.</b> One kind passing is a
    /// rule that might be about the south; three is a rule about the record (7).
    /// </summary>
    [Theory]
    [InlineData(MapSign.FromTheWest, 1, 2)]
    [InlineData(MapSign.FromTheEast, 3, 2)]
    public void EveryKindThatNamesASideIsReadFromThatSideAlone(int kind, int x, int y)
    {
        MapData shut = Room((2, 2), (x, y)) with { Signs = [new MapSign(2, 2, kind, TheSign)] };
        MapData open = Room((2, 2)) with { Signs = [new MapSign(2, 2, kind, TheSign)] };

        Assert.False(ReadIt(Run(shut)));
        Assert.True(ReadIt(Run(open)));

        // And the loose run reads the walled-off one, so the fixture is not simply unreachable.
        Assert.True(ReadIt(Run(shut, obeySignSides: false)));
    }
}
