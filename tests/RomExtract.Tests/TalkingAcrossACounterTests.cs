using System.Linq;
using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Who the walk may speak to, which is the most load-bearing rule in this project and was one
/// square too strict for its whole life.
/// <para>
/// Every shop clerk in this game stands behind a <see cref="MetatileBehaviour.Counter"/>
/// square. A walk requiring orthogonal adjacency therefore stood in front of at most ONE
/// counter in the entire cartridge, and 11 of 11, 14 of 14 and 19 of 19 of the ones it missed
/// were exactly two squares from the nearest floor it stood on — every lever setting, no
/// exceptions and no tail.
/// </para>
/// <para>
/// <c>0x80</c> is READ, not chosen: 91.9% of the unwalkable squares beside a shopkeeper against
/// an 8.9% control, and 22.5% of its own squares have somebody on one side and floor directly
/// opposite against a 0.3% control. Both readings are written out on the constant.
/// </para>
/// <para>
/// <b>The fixture has a counter AND a plain wall</b>, one square apart in every other respect,
/// because a rule that reaches two squares through anything is a different rule and would pass
/// every test that only had the counter in it.
/// </para>
/// </summary>
public sealed class TalkingAcrossACounterTests
{
    private const uint BehindTheCounter = 0x1000;

    private const uint BehindTheWall = 0x2000;

    private const uint OnTheFloor = 0x3000;

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// An 8x8 room whose top three rows are solid, so the only way to anybody standing in row
    /// 1 is through row 2 — and row 2 is a counter in one column and a wall in another.
    /// <para>
    /// The third person stands on open floor in row 4 and is the control: a rule that broke
    /// ordinary adjacency while adding counters would otherwise pass.
    /// </para>
    /// </summary>
    private static WorldData Shop()
    {
        var collision = new byte[64];
        var behaviours = new byte[64];

        // Rows 0, 1 and 2 solid. Rows 3 to 7 are floor.
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 8; x++) collision[(y * 8) + x] = 1;
        }

        // (1,2) is a counter. (5,2) is an ordinary solid square. Both are equally impassable
        // and that is the point — the difference is the behaviour byte and nothing else.
        behaviours[(2 * 8) + 1] = MetatileBehaviour.Counter;
        behaviours[(2 * 8) + 5] = MetatileBehaviour.Normal;

        return new WorldData(
        [
            new MapData("1.0", "1.0", 8, 8, collision)
            {
                Behaviours = behaviours,
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false)
                    {
                        ScriptAddress = BehindTheCounter,
                    },
                    new MapObject(2, 1, 5, 1, Direction.Down, 0, false)
                    {
                        ScriptAddress = BehindTheWall,
                    },
                    new MapObject(3, 1, 3, 4, Direction.Down, 0, false)
                    {
                        ScriptAddress = OnTheFloor,
                    },
                ],
            },
        ]);
    }

    private static Attempt Play() =>
        Autoplayer.Play(Shop(), "1.0", TestRules.All, (_, _, _) => Nothing);

    /// <summary>
    /// The finding: somebody behind a counter is spoken to from the far side of it.
    /// </summary>
    [Fact]
    public void SomebodyBehindACounterIsSpokenToAcrossIt()
    {
        Assert.Contains(("1.0", BehindTheCounter), Play().Ran.Keys);
    }

    /// <summary>
    /// And the discrimination the whole fixture exists for: the same geometry with an ordinary
    /// solid square in the middle is still a wall.
    /// <para>
    /// Without this, "anybody two squares away can be spoken to" passes — and that rule would
    /// have the walk talking through the walls of every building in the game.
    /// </para>
    /// </summary>
    [Fact]
    public void SomebodyBehindAnOrdinaryWallIsStillOutOfReach()
    {
        Assert.DoesNotContain(("1.0", BehindTheWall), Play().Ran.Keys);
    }

    /// <summary>
    /// The ordinary case, asserted: somebody you can simply walk up to is still spoken to.
    /// <para>
    /// 195's lesson applied rather than discovered. A change to this rule that dropped plain
    /// adjacency while adding counters would pass both tests above.
    /// </para>
    /// </summary>
    [Fact]
    public void SomebodyStandingOnOpenFloorIsStillSpokenToNormally()
    {
        Assert.Contains(("1.0", OnTheFloor), Play().Ran.Keys);
    }

    /// <summary>
    /// One square of counter and not a run of them.
    /// <para>
    /// A counter is several squares long — 728 in this world, of which 164 have somebody
    /// behind them — so "reach through counter squares" and "reach across one counter square"
    /// are different rules that agree on every clerk in the game. They disagree here, and the
    /// narrower one is what was measured: the run stood two away, not three.
    /// </para>
    /// </summary>
    [Fact]
    public void TwoCountersDeepIsNotReachable()
    {
        var collision = new byte[64];
        var behaviours = new byte[64];

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 8; x++) collision[(y * 8) + x] = 1;
        }

        // A person in row 1 with counter at (1,2) and counter at (1,3) — two deep, floor at
        // (1,4). Reachable only by a rule that walks along counters.
        behaviours[(2 * 8) + 1] = MetatileBehaviour.Counter;
        behaviours[(3 * 8) + 1] = MetatileBehaviour.Counter;

        var world = new WorldData(
        [
            new MapData("1.0", "1.0", 8, 8, collision)
            {
                Behaviours = behaviours,
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false)
                    {
                        ScriptAddress = BehindTheCounter,
                    },
                ],
            },
        ]);

        Attempt played = Autoplayer.Play(
            world, "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.DoesNotContain(("1.0", BehindTheCounter), played.Ran.Keys);
    }
}
