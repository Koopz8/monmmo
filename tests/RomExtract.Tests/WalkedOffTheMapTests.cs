using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A person a scene walks stops at a wall, like everybody else.
/// <para>
/// The steps of a movement list used to be summed and the total applied in one jump, from
/// wherever the person already was. The run is a fixpoint, so a scene it can reach is played
/// on every pass and the jump is applied again — and on a real cartridge <b>364 of 426</b> of
/// those landed off the edge of the map. One person ended at <c>x = -29</c> on a map 48 wide.
/// </para>
/// <para>
/// It was not a rounding error at the edges: <em>somebody is standing in the way</em> and
/// <em>a person removed is a person not in a doorway</em> are both computed against these
/// squares, so five walks in six were being answered against a square that does not exist.
/// </para>
/// <para>
/// The collision grid is the same oracle the step bytes were derived against in the first
/// place — a direction mapping that is wrong sends somebody through a wall — so the steps
/// travel and the walk does the walking.
/// </para>
/// </summary>
public class WalkedOffTheMapTests
{
    /// <summary>A four-wide room. Whether anything is in the way is the parameter.</summary>
    private static MapData Room(byte[]? collision = null) =>
        new("1.0", "1.0", 4, 4, collision ?? new byte[16]);

    private static MapObject Person(int localId, int x, int y, uint script) =>
        new(localId, 1, x, y, Direction.Down, 0, false) { ScriptAddress = script };

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    private static IReadOnlyList<Direction> Steps(int howMany, Direction way) =>
        [.. Enumerable.Repeat(way, howMany)];

    /// <summary>
    /// One person who keeps something opening so the loop runs several passes, and one whose
    /// script walks them — on every pass, or only once.
    /// </summary>
    private static Attempt Walk(
        IReadOnlyList<Direction> going, bool everyPass = true, byte[]? collision = null)
    {
        MapData start = Room(collision) with
        {
            Objects = [Person(1, 0, 0, 0x1000), Person(2, 1, 1, 0x2000)],
        };

        var opened = 0x100;
        var asked = 0;
        var walked = 0;

        return Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, _) => address == 0x1000
                ? new PlayedScript(asked++ < 4 ? [opened++] : [], [], [], [], null, null)
                : everyPass || walked++ == 0
                    ? Nothing with { Walked = [(2, going)] }
                    : Nothing);
    }

    /// <summary>
    /// Walked the same way on every pass of a fixpoint, they stop at the edge and stay on the
    /// map. This is the whole finding: the loop cannot be stopped from replaying the scene, so
    /// the walk has to be the thing that is bounded.
    /// </summary>
    [Fact]
    public void NobodyIsWalkedOffTheEdgeHoweverManyTimesTheSceneIsPlayed()
    {
        Attempt played = Walk(Steps(3, Direction.Right));

        Assert.NotEmpty(played.Moved);
        Assert.Empty(played.OffTheMap);
    }

    /// <summary>And the same going the other way, so it is not a one-sided bound.</summary>
    [Fact]
    public void NorOffTheOtherEdge()
    {
        Attempt played = Walk(Steps(3, Direction.Left));

        Assert.Empty(played.OffTheMap);
    }

    /// <summary>
    /// A wall stops them, and the steps after it do not happen either — a walker stopped at a
    /// wall does not carry on past it. Without this the bound would be the map's edge only,
    /// and every interior wall would still be walked through.
    /// </summary>
    [Fact]
    public void AWallStopsThemBeforeTheEdge()
    {
        // A blocked square at (2,1), with the walker starting at (1,1) and going right.
        var collision = new byte[16];

        collision[(1 * 4) + 2] = 1;

        Attempt played = Walk(Steps(2, Direction.Right), collision: collision);

        Assert.Empty(played.Moved);
        Assert.Empty(played.OffTheMap);
    }

    /// <summary>
    /// AND THE DECOY. Once the walk stops at a wall, nothing the run does can put anybody off
    /// the map — so the check would be one nothing can fail, which this project treats as no
    /// check at all. It asks the other half too: does every person the CARTRIDGE places stand
    /// on the map it places them on? On a real image the answer is yes, everywhere, and an
    /// answer of yes is only worth having from something that could have said no.
    /// </summary>
    [Fact]
    public void SomebodyTheWorldItselfPlacesOffTheMapIsReported()
    {
        MapData start = Room() with
        {
            Objects = [Person(1, 0, 0, 0x1000), Person(2, 9, 9, 0x2000)],
        };

        Attempt played = Autoplayer.Play(
            new WorldData([start]), "1.0", TestRules.All, (_, _, _) => Nothing);

        WalkedOffTheMap lost = Assert.Single(played.OffTheMap);

        Assert.Equal(2, lost.LocalId);
        Assert.Equal(9, lost.To.X);
        Assert.Equal(4, lost.Width);
    }

    /// <summary>
    /// AND THE ANSWER THAT MEANS THE WALK HAPPENED. Somebody walked one square onto ground
    /// they can stand on has moved, and the run says so — a bound that stopped everybody
    /// would pass every test above and quietly delete the thing this models.
    /// </summary>
    [Fact]
    public void SomebodyWalkedOntoOpenGroundActuallyMoves()
    {
        Attempt played = Walk(Steps(1, Direction.Right), everyPass: false);

        Assert.NotEmpty(played.Moved);
        Assert.Empty(played.OffTheMap);
    }
}
