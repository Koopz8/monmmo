using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Which way each ledge byte is hopped — a rule the walk applies to 1042 squares, whose entire
/// justification was <b>seven numbers in a doc comment that no instrument printed</b>.
/// <para>
/// All seven reproduce to the digit. What does not survive is the criterion the comment names:
/// it says the right assignment is the one leaving the geography CONNECTED and what it measured
/// is maps REACHED, and on this cartridge those point opposite ways — <c>0x3B</c> south reaches
/// 211 maps and strands 35328 of the 46433 squares it stands on, the worst of the four.
/// </para>
/// <para>
/// And <c>0x38</c>, written down as an inference because "no direction changes the reach", is
/// decided once the other two bytes are on: <b>west, by 222 squares</b>. One byte at a time could
/// not have decided it, because with everything else a wall the walk stands beside 9 of its 39.
/// </para>
/// </summary>
public sealed class WhichWayALedgeIsHoppedTests
{
    private const string Route = "3.90";

    /// <summary>
    /// A map with two ledges, one behind the other: south across the middle, then west below it.
    /// </summary>
    /// <remarks>
    /// <b>The shape the original derivation could not see.</b> The western ledge is only ever
    /// approached after the southern one has been hopped, so a sweep that tries one byte at a
    /// time with everything else a wall never reaches it and reports every direction alike.
    /// </remarks>
    private static MapData TwoLedges()
    {
        const int width = 5;
        const int height = 6;

        var behaviours = new byte[width * height];
        var collision = new byte[width * height];

        // A solid row across y=2, its right-hand three squares a south ledge: the only way down.
        for (var x = 0; x < width; x++) collision[(2 * width) + x] = 1;

        for (var x = 2; x < width; x++) behaviours[(2 * width) + x] = MetatileBehaviour.HopSouth;

        // And below it, a west ledge down x=1 with one column of floor past it. That column has
        // no other way in — the squares above it are the solid part of the row — so it is behind
        // the south ledge and behind the west one, in that order.
        for (var y = 3; y < height; y++)
        {
            behaviours[(y * width) + 1] = MetatileBehaviour.HopWest;
            collision[(y * width) + 1] = 1;
        }

        return new MapData(Route, "TWO LEDGES", width, height, collision) { Behaviours = behaviours };
    }

    private static WorldData World() => new([TwoLedges()]);

    private static AnAssignment Row(
        byte behaviour, Direction? way, IReadOnlyDictionary<byte, Direction>? alongside = null) =>
        WhichWayALedgeIsHopped.Try(
            World(), Route, behaviour, way, alongside);

    /// <summary>
    /// THE DIRECTION THAT OPENS THE WORLD IS THE ONE THE LEDGE IS HOPPED, and the three that do
    /// not come out level with leaving it a wall. Both halves: a sweep whose wrong directions
    /// beat the wall row would be measuring something other than the ledge.
    /// </summary>
    [Fact]
    public void OnlyTheRightDirectionOpensAnythingAndTheRestMatchTheWall()
    {
        int wall = Row(MetatileBehaviour.HopSouth, null).Stood;

        Assert.True(Row(MetatileBehaviour.HopSouth, Direction.Down).Stood > wall);

        Assert.Equal(wall, Row(MetatileBehaviour.HopSouth, Direction.Up).Stood);
        Assert.Equal(wall, Row(MetatileBehaviour.HopSouth, Direction.Left).Stood);
        Assert.Equal(wall, Row(MetatileBehaviour.HopSouth, Direction.Right).Stood);
    }

    /// <summary>
    /// A BYTE BEHIND ANOTHER LEDGE CANNOT BE DECIDED ON ITS OWN — every direction scores the same
    /// as the wall, which is four measurements agreeing about nothing. This is the fault in the
    /// original derivation and the reason <c>0x38</c> spent seventy milestones as an inference.
    /// </summary>
    [Fact]
    public void ABytePastAnotherLedgeIsUndecidableOneAtATime()
    {
        Assert.All(
            WhichWayALedgeIsHopped.Ways,
            way => Assert.Equal(
                Row(MetatileBehaviour.HopWest, null).Stood,
                Row(MetatileBehaviour.HopWest, way).Stood));
    }

    /// <summary>
    /// AND IS DECIDED ONCE THE OTHER IS ON. The same byte, the same map, the same instrument —
    /// only the company changed, which is the experiment one-byte-at-a-time cannot run.
    /// </summary>
    [Fact]
    public void TheSameByteIsDecidedWithTheOtherLedgeAtItsMeasuredValue()
    {
        IReadOnlyDictionary<byte, Direction> alongside =
            new Dictionary<byte, Direction> { [MetatileBehaviour.HopSouth] = Direction.Down };

        int wall = Row(MetatileBehaviour.HopWest, null, alongside).Stood;

        Assert.True(Row(MetatileBehaviour.HopWest, Direction.Left, alongside).Stood > wall);
        Assert.Equal(wall, Row(MetatileBehaviour.HopWest, Direction.Right, alongside).Stood);
    }

    /// <summary>
    /// THE DENOMINATOR IS WHAT TELLS THOSE TWO CASES APART. "Every direction scored the same"
    /// means one of two opposite things — the world does not care, or the walk never got near the
    /// squares — and only the count of its own squares the walk stood beside says which.
    /// </summary>
    [Fact]
    public void StoodBesideGrowsWhenTheOtherLedgeOpensTheWayToIt()
    {
        IReadOnlyDictionary<byte, Direction> alongside =
            new Dictionary<byte, Direction> { [MetatileBehaviour.HopSouth] = Direction.Down };

        Assert.Equal(0, Row(MetatileBehaviour.HopWest, Direction.Left).Beside);
        Assert.True(Row(MetatileBehaviour.HopWest, Direction.Left, alongside).Beside > 0);
    }

    /// <summary>
    /// IT IS BESIDE AND NOT ON. Nobody stands on a ledge — every ledge square in the game is
    /// solid, which is what makes it one — so a version counting the ones stood ON would answer
    /// nought for every byte and every direction alike.
    /// </summary>
    [Fact]
    public void TheSquaresCountedAreTheOnesNobodyCanStandOn()
    {
        MapData map = TwoLedges();

        Assert.All(
            Enumerable.Range(2, 3),
            x => Assert.False(map.ToGrid().IsWalkable(new GridPosition(x, 2))));

        Assert.True(Row(MetatileBehaviour.HopSouth, Direction.Down).Beside > 0);
    }

    /// <summary>
    /// THE WALL IS ONE OF THE ROWS. A sweep that only printed the four directions could not say
    /// that three of them changed nothing — it could only say they agreed with each other.
    /// </summary>
    [Fact]
    public void TheSweepIncludesLeavingItAWall()
    {
        IReadOnlyList<AnAssignment> sweep =
            WhichWayALedgeIsHopped.Sweep(World(), Route, MetatileBehaviour.HopSouth);

        Assert.Equal(5, sweep.Count);
        Assert.Null(sweep[0].Way);
        Assert.Equal("a wall", sweep[0].Name);
    }

    /// <summary>
    /// THE BYTE BEING TRIED OVERRIDES WHAT THE COMPANY SAYS ABOUT IT. The second table hands the
    /// whole measured assignment in as company — which contains the byte under test — so a row
    /// that let the company win would print the measured direction five times and label one of
    /// them "a wall".
    /// </summary>
    [Fact]
    public void TheCompanyDoesNotDecideTheByteUnderTest()
    {
        IReadOnlyDictionary<byte, Direction> everything = MetatileBehaviour.Hops;

        Assert.Equal(
            Row(MetatileBehaviour.HopSouth, null).Stood,
            Row(MetatileBehaviour.HopSouth, null, everything).Stood);
    }

    /// <summary>
    /// AND THE SECOND COLUMN IS THE WALK'S, not a number the sweep makes up. A ledge opens ground
    /// it cannot climb back out of, so hopping one strands what is past it — and leaving the same
    /// byte a wall strands nothing, because a world with no one-way edges in it has nowhere you
    /// cannot get back from.
    /// </summary>
    [Fact]
    public void HoppingStrandsWhatIsPastTheLedgeAndAWallStrandsNothing()
    {
        Assert.Equal(0, Row(MetatileBehaviour.HopSouth, null).Stranded);
        Assert.True(Row(MetatileBehaviour.HopSouth, Direction.Down).Stranded > 0);

        // And it is the far side rather than everything: what is above the ledge still gets home.
        AnAssignment hopped = Row(MetatileBehaviour.HopSouth, Direction.Down);

        Assert.True(hopped.GetsBack > 0);
        Assert.Equal(hopped.Stood, hopped.GetsBack + hopped.Stranded);
    }

    /// <summary>
    /// THE CENSUS COUNTS THE OUTER RING SEPARATELY, which is the whole of the difference between
    /// 954 and 962. `--ledges` examines the interior because its columns need four neighbours,
    /// and its totals have been quoted as totals since they were taken.
    /// </summary>
    [Fact]
    public void TheCensusSeparatesTheRingFromTheInterior()
    {
        // A three-by-three with the byte in the middle and one corner.
        byte[] behaviours = [7, 0, 0, 0, 7, 0, 0, 0, 0];

        (int all, int ring) = WhichWayALedgeIsHopped.Census(behaviours, 3, 3, 7);

        Assert.Equal(2, all);
        Assert.Equal(1, ring);

        // And a byte that is not there at all is nought both ways rather than a throw.
        Assert.Equal((0, 0), WhichWayALedgeIsHopped.Census(behaviours, 3, 3, 9));
    }

    /// <summary>
    /// AND EVERY EDGE SQUARE OF A MAP IS ON THE RING — all four sides, not just two. Counting
    /// only the first row and column is how "8 on the ring" would come out 4 and still look like
    /// a measurement.
    /// </summary>
    [Fact]
    public void EverySideOfTheRectangleCountsAsTheRing()
    {
        Assert.All(
            new[] { 0, 1, 2, 3, 5, 6, 7, 8 },
            i =>
            {
                var behaviours = new byte[9];
                behaviours[i] = 7;

                Assert.Equal((1, 1), WhichWayALedgeIsHopped.Census(behaviours, 3, 3, 7));
            });
    }
}
