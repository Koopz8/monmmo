using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Why a walk over this cartridge stops on a cycle rather than on a fixed point.
/// <para>
/// <b>239 found that the run stopped settling the moment signs went into it; 240 named
/// <c>0x026C</c> and <c>0x0807</c> as what makes it go round.</b> Neither said why, and "the value
/// at the end of a pass depends on which map the walk reached last" has stood as the explanation
/// since — which describes oscillation rather than causing it.
/// </para>
/// <para>
/// The cause is parity. Three signs on three maps share one block that reads <c>0x026C</c> and
/// writes the opposite, and three is odd, so every pass ends with it the other way round. And
/// <c>0x0807</c> is moved TWICE a pass at one address, so it ends every pass exactly as it began
/// — <b>240's second name is not a cause</b>, because moving both ways in a pass is necessary and
/// parity is the part that decides.
/// </para>
/// </summary>
public sealed class WhyItGoesRoundTests
{
    private static MovedAFlag Moved(int flag, int pass, uint address, bool cleared) =>
        new(flag, pass, "1.59", address, cleared, WhatRanIt.ASign);

    /// <summary>The three signs that share one toggling block, on one pass.</summary>
    private static IEnumerable<MovedAFlag> ThreeSigns(int pass) =>
    [
        Moved(0x026C, pass, 0x08162212, false),
        Moved(0x026C, pass, 0x08162264, true),
        Moved(0x026C, pass, 0x081622B1, false),
    ];

    /// <summary>A flag set and cleared once each in the same pass, at one address.</summary>
    private static IEnumerable<MovedAFlag> SetAndCleared(int flag, int pass) =>
    [
        Moved(flag, pass, 0x08165134, false),
        Moved(flag, pass, 0x08165134, true),
    ];

    // ------------------------------------------------------------------------- the parity

    /// <summary>
    /// THE THING: an odd number of moves on the pass the run stopped on means the flag ends every
    /// pass the other way round.
    /// </summary>
    [Fact]
    public void AnOddNumberOfMovesOnTheLastPassCannotSettle()
    {
        IReadOnlyList<ToggledInAPass> toggled =
            WhyItGoesRound.In([.. ThreeSigns(5), .. ThreeSigns(6)]);

        ToggledInAPass one = Assert.Single(WhyItGoesRound.CannotSettle(toggled, 6));

        Assert.Equal((0x026C, 6, 3, 3), (one.Flag, one.Pass, one.Moves, one.Addresses));
        Assert.True(one.Odd);
    }

    /// <summary>
    /// AND THE HALF 240 GOT WRONG: a flag moved BOTH WAYS in a pass but an EVEN number of times
    /// ends the pass exactly as it began, and is not why anything goes round.
    /// </summary>
    /// <remarks>
    /// <c>0x0807</c> is this shape on the cartridge — set and cleared at one address on `2.38`,
    /// seven times each over seven passes. 240 named it beside <c>0x026C</c> because its criterion
    /// was "moves both ways within one pass", which is necessary and not sufficient.
    /// </remarks>
    [Fact]
    public void AFlagMovedEvenlyEndsThePassAsItBegan()
    {
        IReadOnlyList<ToggledInAPass> toggled =
            WhyItGoesRound.In([.. SetAndCleared(0x0807, 5), .. SetAndCleared(0x0807, 6)]);

        Assert.All(toggled, t => Assert.False(t.Odd));
        Assert.Empty(WhyItGoesRound.CannotSettle(toggled, 6));
    }

    /// <summary>
    /// AND A FLAG THAT STOPPED MOVING IS NOT OSCILLATING, however odd its own last pass was.
    /// </summary>
    /// <remarks>
    /// <b>The first version of this reading asked each flag about the last pass IT moved in</b>
    /// and reported <c>0x002E</c>, which is set once on pass one, cleared once on pass two, and
    /// never touched again. Odd on the last pass it took part in, and settled by pass three —
    /// because it stopped. Asking about the pass the RUN stopped on is what tells them apart.
    /// </remarks>
    [Fact]
    public void AFlagThatStoppedMovingIsNotWhyTheRunGoesRound()
    {
        IReadOnlyList<ToggledInAPass> toggled = WhyItGoesRound.In(
        [
            Moved(0x002E, 1, 0x0816A5C5, false),
            Moved(0x002E, 2, 0x08165D8E, true),
        ]);

        // Odd on both of the passes it moved in, and absent from pass 6.
        Assert.All(toggled, t => Assert.True(t.Odd));
        Assert.Empty(WhyItGoesRound.CannotSettle(toggled, 6));
    }

    // ------------------------------------------------------------------ what is asked at all

    /// <summary>
    /// A flag moved only one way is not asked: something set once and never cleared is a thing
    /// that happened, and counting it would make every ordinary story flag a candidate.
    /// </summary>
    [Fact]
    public void AFlagMovedOnlyOneWayIsNotCounted()
    {
        Assert.Empty(
            WhyItGoesRound.In(
            [
                Moved(0x0100, 6, 0x08160000, false),
                Moved(0x0100, 6, 0x08160010, false),
                Moved(0x0100, 6, 0x08160020, false),
            ]));
    }

    /// <summary>
    /// And the addresses are counted as well as the moves, because three moves at one address and
    /// three at three are the same parity and not the same finding.
    /// </summary>
    [Fact]
    public void TheAddressesAreCountedBesideTheMoves()
    {
        ToggledInAPass three = Assert.Single(WhyItGoesRound.In(ThreeSigns(6)));

        Assert.Equal((3, 3), (three.Moves, three.Addresses));

        ToggledInAPass one = Assert.Single(WhyItGoesRound.In(SetAndCleared(0x0807, 6)));

        Assert.Equal((2, 1), (one.Moves, one.Addresses));
    }
}
