using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// 260 found an elevation in all four event records and that 423 of 425 maps carry more than one
/// of them, while this project's walk is two-dimensional. That is a worry, not a number.
/// <para>
/// <b>The flat fill and the layered fill are ONE fill with one predicate swapped.</b> A
/// before-and-after built from two separately-written fills is a measurement with no instrument
/// (241), and this project has been caught by that once already.
/// </para>
/// <para>
/// The rule is MODELLED and small: two squares are on one layer when their elevations are equal,
/// or when either is nought — the value a walker may step onto from anywhere. Nothing here has
/// read the engine's own rule.
/// </para>
/// </summary>
public sealed class WhatTheLayersCostTests
{
    /// <summary>A three-by-one strip: two squares on one layer and one on another.</summary>
    private static CollisionGrid Strip(int width = 3) =>
        new(width, 1, new byte[width]);

    // ------------------------------------------------------------------------ the rule

    /// <summary>
    /// THE RULE, all three ways it can answer, named. It is a list, so the fixture carries one of
    /// everything (251's lesson).
    /// </summary>
    [Fact]
    public void TwoSquaresAreOnOneLayerWhenTheyMatchOrEitherIsNought()
    {
        Assert.True(WhatTheLayersCost.Connects(3, 3));
        Assert.True(WhatTheLayersCost.Connects(0, 4));
        Assert.True(WhatTheLayersCost.Connects(4, 0));

        // …and the answer that has to be possible, or the rule is a machine for saying yes.
        Assert.False(WhatTheLayersCost.Connects(3, 4));
    }

    // ------------------------------------------------------------------------ the fill

    /// <summary>
    /// A LAYER STOPS A FILL. The middle square is on another layer, so the far one is behind it
    /// even though every square is walkable.
    /// </summary>
    [Fact]
    public void AStepOntoAnotherLayerIsNotTaken()
    {
        HashSet<GridPosition> reached = WhatTheLayersCost.Fill(
            Strip(),
            [3, 4, 3],
            [new GridPosition(0, 0)],
            WhatTheLayersCost.Connects);

        Assert.Equal([new GridPosition(0, 0)], reached);
    }

    /// <summary>
    /// AND NOUGHT LETS IT THROUGH, which is the whole reason the wildcard is in the rule: a
    /// transition square between two layers joins them.
    /// </summary>
    [Fact]
    public void ANoughtSquareJoinsTwoLayers()
    {
        HashSet<GridPosition> reached = WhatTheLayersCost.Fill(
            Strip(),
            [3, 0, 4],
            [new GridPosition(0, 0)],
            WhatTheLayersCost.Connects);

        Assert.Equal(3, reached.Count);
    }

    /// <summary>
    /// <b>THE CONTROL.</b> The flat answer is this same fill with a rule that always says yes, so
    /// the two cannot differ for any reason but the rule. Without this the difference the command
    /// prints could be about two separately-written fills rather than about the cartridge.
    /// </summary>
    [Fact]
    public void TheFlatFillIsTheLayeredFillWithARuleThatAlwaysSaysYes()
    {
        HashSet<GridPosition> flat = WhatTheLayersCost.Fill(
            Strip(), [3, 4, 3], [new GridPosition(0, 0)], (_, _) => true);

        Assert.Equal(3, flat.Count);
    }

    /// <summary>
    /// A FILL STILL STOPS AT A WALL. The layer rule is added to walkability, not instead of it —
    /// a fill that walked through walls whenever the elevations matched would report the whole
    /// map reachable and the difference as nought.
    /// </summary>
    [Fact]
    public void AWallStopsTheFillWhateverTheElevations()
    {
        var blocked = new CollisionGrid(3, 1, [0, 1, 0]);

        HashSet<GridPosition> reached = WhatTheLayersCost.Fill(
            blocked, [3, 3, 3], [new GridPosition(0, 0)], WhatTheLayersCost.Connects);

        Assert.Equal([new GridPosition(0, 0)], reached);
    }

    /// <summary>
    /// AND A START ON A WALL STARTS NOTHING. The fill is seeded from every door and every person,
    /// and a door whose square is solid would otherwise seed a fill from inside a building.
    /// </summary>
    [Fact]
    public void AStartOnASolidSquareIsNotAStart()
    {
        var blocked = new CollisionGrid(3, 1, [1, 0, 0]);

        HashSet<GridPosition> reached = WhatTheLayersCost.Fill(
            blocked, [3, 3, 3], [new GridPosition(0, 0)], WhatTheLayersCost.Connects);

        Assert.Empty(reached);
    }

    // ------------------------------------------------------------------------ the hop

    /// <summary>
    /// A LEDGE IS CROSSED, NOT STOOD ON — and a fill that cannot do it is weaker than the walk it
    /// claims to measure. 261 built one and reported 751 squares of ROUTE 17, the most ledge-dense
    /// map in the game, as behind a layer change.
    /// </summary>
    [Fact]
    public void AFillThatCanHopCrossesALedgeAndOneThatCannotDoesNot()
    {
        // Three squares: standing, a solid ledge, and the landing.
        var grid = new CollisionGrid(3, 1, [0, 1, 0]);

        GridPosition? Hop(GridPosition square, Direction facing) =>
            square == new GridPosition(1, 0) && facing == Direction.Right
                ? new GridPosition(2, 0)
                : null;

        Assert.Equal(
            2,
            WhatTheLayersCost.Fill(
                grid, [3, 0, 3], [new GridPosition(0, 0)], WhatTheLayersCost.Connects, Hop).Count);

        // …and without the hop the landing is behind a wall, which is what a fill with no ledge
        // rule sees. Both answers have to be possible or the hop is not being tested.
        Assert.Single(
            WhatTheLayersCost.Fill(
                grid, [3, 0, 3], [new GridPosition(0, 0)], WhatTheLayersCost.Connects));
    }

    /// <summary>
    /// AND THE HOP GOES THROUGH THE LEDGE, NOT OVER IT. A ledge carries elevation nought — the
    /// wildcard — and hopping one is how a walker changes layer, so asking whether the START
    /// connects to the LANDING refuses exactly the move the ledge exists to allow.
    /// </summary>
    [Fact]
    public void AHopMayChangeLayerBecauseTheLedgeIsTheWildcard()
    {
        var grid = new CollisionGrid(3, 1, [0, 1, 0]);

        GridPosition? Hop(GridPosition square, Direction facing) =>
            square == new GridPosition(1, 0) && facing == Direction.Right
                ? new GridPosition(2, 0)
                : null;

        // Elevation 3, over a nought ledge, onto elevation 1 — start and landing do not connect.
        Assert.Equal(
            2,
            WhatTheLayersCost.Fill(
                grid, [3, 0, 1], [new GridPosition(0, 0)], WhatTheLayersCost.Connects, Hop).Count);
    }

    /// <summary>
    /// AND A LEDGE ONTO ANOTHER LAYER WITH NO WILDCARD IN BETWEEN IS STILL REFUSED. Without this
    /// the hop would be a way round the rule rather than a move through it, and every layered
    /// answer would collapse to the flat one.
    /// </summary>
    [Fact]
    public void AHopFromOneLayerStraightOntoAnotherIsRefused()
    {
        var grid = new CollisionGrid(3, 1, [0, 1, 0]);

        GridPosition? Hop(GridPosition square, Direction facing) =>
            square == new GridPosition(1, 0) && facing == Direction.Right
                ? new GridPosition(2, 0)
                : null;

        // The ledge itself is at elevation 4 rather than nought, so neither half connects.
        Assert.Single(
            WhatTheLayersCost.Fill(
                grid, [3, 4, 3], [new GridPosition(0, 0)], WhatTheLayersCost.Connects, Hop));
    }

    // ------------------------------------------------------------------ the elevations

    /// <summary>
    /// THE NIBBLE, off the same <see cref="MapBlock"/> reading the rest of the project uses —
    /// bits 12 to 15, above the metatile id and the collision bits.
    /// </summary>
    [Fact]
    public void TheElevationComesOffTheTopOfTheBlock()
    {
        // Metatile 0x123, collision 2, elevation 4.
        ushort block = 0x123 | (2 << 10) | (4 << 12);

        Assert.Equal([4], WhatTheLayersCost.Elevations([block]));

        // …and it is not the collision bits, which a shift wrong by two would give.
        Assert.NotEqual([2], WhatTheLayersCost.Elevations([block]));
    }
}
