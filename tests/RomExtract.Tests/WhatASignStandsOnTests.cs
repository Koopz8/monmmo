using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a sign stands on, and the direction that can name a byte (281).
/// <para>
/// <b>"179 signs stand on <c>0x84</c>" names nothing.</b> It reads the same whether that byte is a
/// sign board or every wall in the game, and those are opposite findings. The direction that names
/// it is the other one: <b>189 squares of it exist in this cartridge and 179 hold a sign</b> —
/// 94.7% against the world's own 0.300%, which is three hundred and fifteen-fold.
/// </para>
/// <para>
/// 242 said a sign's own square is SOLID, which is what a sign is. True of every one of the 97 that
/// name a side (0 of 73, 0 of 14, 0 of 10 walkable) and false of 85 of the 422 that name none.
/// </para>
/// </summary>
public sealed class WhatASignStandsOnTests
{
    private static IEnumerable<(byte, bool)> Squares(byte behaviour, int howMany, int marked) =>
        Enumerable.Range(0, howMany).Select(i => (behaviour, i < marked));

    /// <summary>Both halves are counted: how many squares, and how many of them are marked.</summary>
    [Fact]
    public void BothTheSquaresAndTheMarkedOnesAreCounted()
    {
        IReadOnlyDictionary<byte, (int Squares, int Marked)> tally =
            WhichWayASignIsRead.HowOften([.. Squares(0x84, 10, 9), .. Squares(0x00, 100, 1)]);

        Assert.Equal((10, 9), tally[0x84]);
        Assert.Equal((100, 1), tally[0x00]);
    }

    /// <summary>A behaviour nothing is marked on still gets counted, or the floor is wrong.</summary>
    [Fact]
    public void ABehaviourWithNothingOnItIsStillCounted()
    {
        IReadOnlyDictionary<byte, (int Squares, int Marked)> tally =
            WhichWayASignIsRead.HowOften([.. Squares(0x84, 4, 4), .. Squares(0x11, 96, 0)]);

        Assert.Equal((96, 0), tally[0x11]);
        Assert.Equal(0.04, WhichWayASignIsRead.Everywhere(tally), 6);
    }

    /// <summary>
    /// <b>THE THING: the floor is over EVERY square, not over the ones that scored.</b> A floor
    /// drawn from the rows that did well is a floor the answer chose (79) — here it would be 4/8
    /// instead of 4/100, and a twelve-fold enrichment would read as nothing at all.
    /// </summary>
    [Fact]
    public void TheFloorIsEverySquareAndNotOnlyTheOnesThatScored()
    {
        IReadOnlyDictionary<byte, (int Squares, int Marked)> tally =
            WhichWayASignIsRead.HowOften([.. Squares(0x84, 8, 4), .. Squares(0x11, 92, 0)]);

        double everywhere = WhichWayASignIsRead.Everywhere(tally);

        Assert.Equal(0.04, everywhere, 6);

        // 0x84 is half marked, which is twelve and a half times the world's own rate.
        Assert.Equal(12.5, 0.5 / everywhere, 6);
    }

    /// <summary>
    /// <b>And the direction it is asked in decides the answer.</b> Two behaviours hold the same
    /// number of marks; one is nearly all marks and the other is a rounding error on a huge
    /// population, and counting the MARKS alone cannot tell them apart.
    /// </summary>
    [Fact]
    public void TheSameCountOfMarksMeansOppositeThingsOnTwoPopulations()
    {
        IReadOnlyDictionary<byte, (int Squares, int Marked)> tally =
            WhichWayASignIsRead.HowOften([.. Squares(0x84, 20, 19), .. Squares(0x00, 20000, 19)]);

        Assert.Equal(tally[0x84].Marked, tally[0x00].Marked);

        double board = (double)tally[0x84].Marked / tally[0x84].Squares;
        double ground = (double)tally[0x00].Marked / tally[0x00].Squares;

        Assert.True(board > ground * 500, $"{board:P1} against {ground:P4}");
    }

    /// <summary>Nothing at all is a floor of nought rather than a division by nought.</summary>
    [Fact]
    public void NoSquaresIsNoFloor()
    {
        Assert.Equal(0, WhichWayASignIsRead.Everywhere(WhichWayASignIsRead.HowOften([])), 6);
    }

    /// <summary>
    /// The byte this cartridge uses for it is named, with the evidence on the constant rather than
    /// in a milestone document nobody re-runs (231).
    /// </summary>
    [Fact]
    public void TheSignBoardIsNamed()
    {
        Assert.Equal(0x84, MetatileBehaviour.SignBoard);
        Assert.NotEqual(MetatileBehaviour.Normal, MetatileBehaviour.SignBoard);
        Assert.NotEqual(MetatileBehaviour.Counter, MetatileBehaviour.SignBoard);
    }
}
