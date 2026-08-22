using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The three ways ground the walk never stood on is shut off from it (288).
/// <para>
/// 287 counted 4019 squares of reached-but-never-stood-on ground and said in as many words that a
/// pocket is not proof of a fence: a square behind a one-way ledge looks exactly like one behind
/// a wall. On the real cartridge the answer is <b>0 on the same ground, 0 behind a ledge, 4019
/// sealed</b> — every pocket in the game is opened by a door or by nothing.
/// </para>
/// <para>
/// <b>The nought that matters is the first one.</b> A walk's steps are symmetric over walkable
/// ground, so a square joined to a stood-on one by ordinary steps and never visited would be a
/// walk that stopped early — a count that must be nought, which is the best check an instrument
/// can carry (240).
/// </para>
/// </summary>
public sealed class HowAPocketIsShutTests
{
    private const byte HopSouth = MetatileBehaviour.HopSouth;

    /// <summary>A one-wide column: 1 is solid, and behaviours are given square by square.</summary>
    private static MapData Column(byte[] collision, byte[]? behaviours = null) =>
        new("1.0", "1.0", 1, collision.Length, collision)
        {
            Behaviours = behaviours ?? new byte[collision.Length],
        };

    private static GridPosition At(int y) => new(0, y);

    private static HowShut Shut(MapData map, params int[] stood) =>
        HowAPocketIsShut.On(map, [.. stood.Select(At)], surfing: false);

    /// <summary>
    /// <b>Ground behind a WALL is sealed.</b> Nothing but a door opens it, and that is what all
    /// 4019 of this cartridge's fenced squares turn out to be.
    /// </summary>
    [Fact]
    public void GroundBehindAWallIsSealed()
    {
        HowShut shut = Shut(Column([0, 1, 0, 0]), 2, 3);

        Assert.Equal(1, shut.Sealed);
        Assert.Equal(0, shut.BehindALedge);
        Assert.Equal(0, shut.SameGround);
        Assert.Equal(1, shut.Fenced);
    }

    /// <summary>
    /// <b>THE THING, and the direction that matters.</b> A ledge hopped SOUTH sits between the
    /// pocket and the ground the walk took, so the only way across is a hop out of the pocket
    /// towards the walk. Reading the hop the other way finds nothing — and finds nothing on the
    /// real cartridge too, which is why this fixture exists rather than the measurement standing
    /// on its own.
    /// </summary>
    [Fact]
    public void GroundACanOnlyHopOutOfIsBehindALedgeAndNotSealed()
    {
        MapData map = Column([0, 1, 0, 0], [0, HopSouth, 0, 0]);

        HowShut shut = Shut(map, 2, 3);

        Assert.Equal(1, shut.BehindALedge);
        Assert.Equal(0, shut.Sealed);
    }

    /// <summary>
    /// And the same map with the ledge byte taken away is sealed — so the finding is the LEDGE
    /// and not the shape of the column. Without this the fixture above passes on any instrument
    /// that calls a one-square pocket a ledge.
    /// </summary>
    [Fact]
    public void AndWithoutTheLedgeByteTheSameShapeIsSealed()
    {
        HowShut shut = Shut(Column([0, 1, 0, 0], [0, 0, 0, 0]), 2, 3);

        Assert.Equal(0, shut.BehindALedge);
        Assert.Equal(1, shut.Sealed);
    }

    /// <summary>
    /// A hop INTO a pocket counts too — the walk itself would take it, so this case never arises
    /// on the cartridge, and an instrument that only reads one direction is only half an answer.
    /// </summary>
    [Fact]
    public void GroundOnlyAHopIntoIsBehindALedgeAsWell()
    {
        // Standing at 1, stepping south onto the ledge at 2, landing on 3.
        MapData map = Column([0, 0, 1, 0], [0, 0, HopSouth, 0]);

        HowShut shut = Shut(map, 0, 1);

        Assert.Equal(1, shut.BehindALedge);
        Assert.Equal(0, shut.Sealed);
    }

    /// <summary>
    /// <b>THE NOUGHT THAT MUST BE NOUGHT.</b> Ground joined to the walk's own by ordinary steps
    /// and never stood on is a walk that stopped early. The instrument has to be able to SAY it,
    /// or a nought on the cartridge means nothing at all.
    /// </summary>
    [Fact]
    public void GroundJoinedByPlainStepsIsTheOneThatMustBeNought()
    {
        HowShut shut = Shut(Column([0, 0, 0, 0]), 0);

        Assert.Equal(3, shut.SameGround);
        Assert.Equal(0, shut.BehindALedge);
        Assert.Equal(0, shut.Sealed);
    }

    /// <summary>A map the walk stood on all of is fenced nowhere.</summary>
    [Fact]
    public void AMapItStoodOnAllOfIsFencedNowhere()
    {
        HowShut shut = Shut(Column([0, 0]), 0, 1);

        Assert.Equal(0, shut.Fenced);
        Assert.Equal(2, shut.StoodOn);
    }

    /// <summary>
    /// A wall is not fenced ground. The three buckets are a partition of the WALKABLE squares the
    /// walk did not stand on, and nothing else.
    /// </summary>
    [Fact]
    public void TheThreeBucketsPartitionTheWalkableGroundItMissed()
    {
        MapData map = Column([0, 1, 0, 1, 0], [0, HopSouth, 0, 0, 0]);

        HowShut shut = Shut(map, 2);

        // Walkable: 0, 2, 4. Stood on: 2. So two squares are unaccounted for, and every one of
        // them is in exactly one bucket.
        Assert.Equal(2, shut.Fenced);
        Assert.Equal(1, shut.StoodOn);
    }

    /// <summary>
    /// A square the walk claims to have stood on that is not walkable is not counted as ground —
    /// otherwise a caller handing in a stale list would make the map look smaller than it is.
    /// </summary>
    [Fact]
    public void ASquareTheWalkCouldNotHaveStoodOnIsNotCounted()
    {
        HowShut shut = Shut(Column([0, 1, 0]), 0, 1);

        Assert.Equal(1, shut.StoodOn);
        Assert.Equal(1, shut.Sealed);
    }
}
