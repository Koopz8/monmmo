using PokeMmo.Core.World;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Why the doors into the unreached maps are not taken (304).
/// <para>
/// 303 left seven warp-named roots as "doors the run reaches and does not take". It is none of the
/// obvious guesses: <b>43 of 43 doors into an unreached map were NEVER GOT NEAR</b>, and every one
/// has one or two walkable neighbours, so none is walled in. They are inside 287's pockets.
/// </para>
/// <para>
/// <b>The calibration row is what makes that a reading</b> rather than a broken instrument: asked
/// of every warp into a map the run DOES reach, it answers stood-on for <b>1165 of 1182</b>.
/// </para>
/// </summary>
public sealed class WhyTheDoorIsNotTakenTests
{
    /// <summary>
    /// A four-by-four map: a wall of solid down the middle column so the two halves cannot see
    /// each other, and a warp on each side.
    /// </summary>
    private static MapData Map(string id, params Warp[] warps)
    {
        var collision = new byte[16];

        for (var y = 0; y < 4; y++) collision[(y * 4) + 2] = 1;

        return new MapData(id, id, 4, 4, collision) { Warps = warps };
    }

    private static ADoorNotTaken Only(
        MapData map, IEnumerable<(string, GridPosition)> stood, string root) =>
        Assert.Single(
            WhyTheDoorIsNotTaken.Into(
                [map], new HashSet<string> { map.Id }, [.. stood], new HashSet<string> { root }));

    /// <summary>
    /// <b>THE THING.</b> Standing ON the door's square and never getting near it are different
    /// answers, and the fixture has one of each in the same map — a fixture with one cannot tell a
    /// classification from a constant.
    /// </summary>
    [Fact]
    public void StandingOnTheDoorAndNeverGettingNearAreDifferentAnswers()
    {
        MapData map = Map("1.0", new Warp(0, 0, 0, "9.9"), new Warp(3, 3, 0, "9.9"));

        List<(string, GridPosition)> stood = [("1.0", new GridPosition(0, 0))];

        IReadOnlyList<ADoorNotTaken> read = WhyTheDoorIsNotTaken.Into(
            [map], new HashSet<string> { "1.0" }, stood, new HashSet<string> { "9.9" });

        Assert.Equal(WhyNotTaken.StoodOnIt, read.Single(d => d.Square.X == 0).Why);
        Assert.Equal(WhyNotTaken.NeverGotNear, read.Single(d => d.Square.X == 3).Why);
    }

    /// <summary>
    /// <b>WALLED IN IS ABOUT THE FILE and is asked FIRST.</b> A door no square beside is walkable
    /// is one nothing could ever reach, and filing it as a run that did not get there would put a
    /// fact about the cartridge in a bucket about the walk (242, 281, and 211's rule).
    /// <para>
    /// The fixture's walled-in door sits in the solid column with solid on both sides, and the
    /// ordinary one is a step away — so a version that asked "never got near" first gets the first
    /// wrong and keeps the second right.
    /// </para>
    /// </summary>
    [Fact]
    public void WalledInIsAskedBeforeNeverGotNear()
    {
        // (2,0) is in the solid column; (2,1) below it is solid too, and (1,0)/(3,0) are open.
        var collision = new byte[16];

        for (var y = 0; y < 4; y++)
        {
            collision[(y * 4) + 1] = 1;
            collision[(y * 4) + 2] = 1;
            collision[(y * 4) + 3] = 1;
        }

        var map = new MapData("1.0", "1.0", 4, 4, collision)
        {
            Warps = [new Warp(2, 1, 0, "9.9"), new Warp(0, 1, 0, "9.9")],
        };

        IReadOnlyList<ADoorNotTaken> read = WhyTheDoorIsNotTaken.Into(
            [map], new HashSet<string> { "1.0" }, [], new HashSet<string> { "9.9" });

        Assert.Equal(WhyNotTaken.WalledIn, read.Single(d => d.Square.X == 2).Why);
        Assert.Equal(0, read.Single(d => d.Square.X == 2).WalkableNeighbours);

        // The one with open ground above and below it is not walled in, it is unvisited.
        Assert.Equal(WhyNotTaken.NeverGotNear, read.Single(d => d.Square.X == 0).Why);
    }

    /// <summary>
    /// Standing BESIDE a door is its own answer and not the same as standing on it. On this
    /// cartridge it never happens — 0 of 1182 — which is why it has to be a bucket rather than
    /// being folded into either neighbour: an empty bucket is a fact about the population it was
    /// asked of (31).
    /// </summary>
    [Fact]
    public void StandingBesideIsItsOwnAnswer()
    {
        MapData map = Map("1.0", new Warp(0, 1, 0, "9.9"));

        Assert.Equal(
            WhyNotTaken.StoodBeside,
            Only(map, [("1.0", new GridPosition(0, 0))], "9.9").Why);
    }

    /// <summary>
    /// <b>THE ROW WHOSE ANSWER IS KNOWN.</b> The same question asked of warps into maps the run
    /// DOES reach — without it, "the run never got to any of them" is what a broken instrument
    /// says too (68, 78).
    /// <para>
    /// The two are separate entry points on purpose: the known row must not be able to pick up a
    /// door into an unreached map, and vice versa, or the calibration would be scoring itself.
    /// </para>
    /// </summary>
    [Fact]
    public void TheKnownRowAsksOnlyAboutDoorsIntoReachedMaps()
    {
        MapData map = Map("1.0", new Warp(0, 0, 0, "1.1"), new Warp(3, 3, 0, "9.9"));

        HashSet<string> reached = ["1.0", "1.1"];
        List<(string, GridPosition)> stood = [("1.0", new GridPosition(0, 0))];

        ADoorNotTaken known = Assert.Single(
            WhyTheDoorIsNotTaken.TheKnownRow([map], reached, stood));

        Assert.Equal("1.1", known.To);
        Assert.Equal(WhyNotTaken.StoodOnIt, known.Why);

        ADoorNotTaken root = Assert.Single(
            WhyTheDoorIsNotTaken.Into([map], reached, stood, new HashSet<string> { "9.9" }));

        Assert.Equal("9.9", root.To);
    }

    /// <summary>
    /// And a sentinel warp is not a door into anywhere — the runtime's marker names no map, so
    /// counting one would put a door in every lift cabin (265, 287, 303).
    /// </summary>
    [Fact]
    public void ASentinelWarpIsNotADoor()
    {
        string sentinel = $"{Warp.Dynamic}.{Warp.Dynamic}";

        MapData map = Map("1.0", new Warp(0, 0, 0, sentinel));

        Assert.Empty(
            WhyTheDoorIsNotTaken.Into(
                [map], new HashSet<string> { "1.0" }, [], new HashSet<string> { sentinel }));

        Assert.Empty(
            WhyTheDoorIsNotTaken.TheKnownRow(
                [map], new HashSet<string> { "1.0", sentinel }, []));
    }
}
