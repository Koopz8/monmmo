using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Reaching somewhere and getting back from it are two facts, and this project has printed one
/// of them for two hundred milestones as though it were both.
/// <para>
/// The walk's edges have directions in them — a ledge is hopped one way and climbed none, a
/// door names a warp on the far map and nothing makes the far warp name one back, and nineteen
/// exits in this game name a map no bank has because the room decides at runtime. So the world
/// the walk explores is a DIRECTED graph, and "425 maps are reachable" says nothing at all about
/// whether a player who goes there is stuck.
/// </para>
/// <para>
/// Measured on this cartridge at the floor: <b>24029 of 35142 squares stood on cannot get back</b>,
/// and the way into all of them is eighteen ledge hops on ROUTE 4.
/// </para>
/// </summary>
public sealed class TheWayBackTests
{
    /// <summary>
    /// THE TARGET GETS BACK TO ITSELF. Standing where you meant to be is the whole of what the
    /// question asks, and leaving it out makes the starting square the one place in the world
    /// that is stranded — a wrong answer with a tidy explanation.
    /// </summary>
    [Fact]
    public void WhereTheWalkBeganIsNotStranded()
    {
        Assert.Empty(TheWayBack.Stranded([1], Array.Empty<(int, int)>(), 1));
    }

    /// <summary>
    /// A ONE-WAY EDGE STRANDS WHAT IS BEHIND IT, and the same pair of places with the edge back
    /// strands nothing. Both halves, because either alone is satisfied by an instrument that
    /// always says the same thing.
    /// </summary>
    [Fact]
    public void OneWayStrandsAndTwoWayDoesNot()
    {
        Assert.Equal([2], TheWayBack.Stranded([1, 2], [(1, 2)], 1));
        Assert.Empty(TheWayBack.Stranded([1, 2], [(1, 2), (2, 1)], 1));
    }

    /// <summary>
    /// IT IS REACHABILITY AND NOT ONE HOP. A place three steps out with a way round back to the
    /// start is not stranded, and a walk that only looked for an edge straight home would say it
    /// was — which would report every corridor in the game as a trap.
    /// </summary>
    [Fact]
    public void AWayRoundCountsHoweverLongItIs()
    {
        Assert.Empty(TheWayBack.Stranded([1, 2, 3, 4], [(1, 2), (2, 3), (3, 4), (4, 1)], 1));
    }

    /// <summary>
    /// AND A DEAD END IS STRANDED HOWEVER FAR ALONG IT SITS. The far side of a one-way step is
    /// stranded whole, not just the square the step lands on: the two squares past it have a way
    /// back to each other and none to the start.
    /// </summary>
    [Fact]
    public void EverythingBehindTheOneWayStepIsStrandedNotJustTheLanding()
    {
        IReadOnlyList<int> stranded = TheWayBack.Stranded(
            [1, 2, 3, 4], [(1, 2), (2, 3), (3, 2), (3, 4), (4, 3)], 1);

        Assert.Equal([2, 3, 4], stranded.Order());
    }

    /// <summary>
    /// THE ANSWER IS ABOUT THE PLACES STOOD ON, not about the edges' own endpoints. A step
    /// recorded into a square the walk had already seen is a real edge; a version that reported
    /// on whatever the edge list happened to mention would be reporting on the order the walk
    /// visited things in.
    /// </summary>
    [Fact]
    public void SomewhereStoodOnWithNoEdgeAtAllIsStranded()
    {
        Assert.Equal([9], TheWayBack.Stranded([1, 9], [(1, 1)], 1));
    }

    /// <summary>
    /// AND THE EDGES ARE READ FORWARDS. Handing it the same edges the other way round is the
    /// whole of the difference between "can get there" and "can get back", so an implementation
    /// that reversed them by accident would answer this backwards.
    /// </summary>
    [Fact]
    public void TheDirectionOfAnEdgeIsWhatIsBeingAsked()
    {
        Assert.Empty(TheWayBack.Reaching<int>([], 1).Where(n => n != 1));
        Assert.Equal([1, 2], TheWayBack.Reaching([(2, 1)], 1).Order());
        Assert.Equal([1], TheWayBack.Reaching([(1, 2)], 1));
    }

    private const string Route = "3.90";

    /// <summary>A map with a ledge across it, hopped southward — the shape of ROUTE 4's.</summary>
    private static MapData Terrace()
    {
        const int width = 5;
        const int height = 5;

        var behaviours = new byte[width * height];
        var collision = new byte[width * height];

        for (var x = 0; x < width; x++)
        {
            behaviours[2 * width + x] = MetatileBehaviour.HopSouth;
            collision[2 * width + x] = 1;
        }

        return new MapData(Route, "THE TERRACE", width, height, collision) { Behaviours = behaviours };
    }

    /// <summary>
    /// THE WALK'S OWN LEDGE IS ONE WAY AND THE RECORD SHOWS IT. The whole measurement rests on
    /// the edges being the walk's rather than a second author's, so this asks the real walker
    /// over a map whose only interesting feature is a ledge.
    /// </summary>
    [Fact]
    public void TheWalkerHopsDownAndCannotGetBackUp()
    {
        var steps = new List<AStepTaken>();

        Reach reach = WorldWalker.Walk(
            new WorldData([Terrace()]), Route, startSquare: new GridPosition(0, 0), steps: steps);

        var stood = reach.Stood.Select(s => new Somewhere(s.MapId, s.Square)).ToList();

        IReadOnlyList<Somewhere> stranded =
            TheWayBack.Stranded(stood, steps.Select(s => (s.From, s.To)), reach.Start);

        // Two rows above the ledge, two below it, and the ledge row itself is nobody's square.
        Assert.Equal(20, stood.Count);
        Assert.Equal(10, stranded.Count);
        Assert.All(stranded, s => Assert.True(s.Square.Y > 2));
    }

    /// <summary>
    /// EVERY ENQUEUE IS RECORDED, and that is the invariant the record has to hold rather than a
    /// count somebody wrote down. Every square the walk stood on except the one it began at is
    /// the far end of some recorded step — so a walk that grew a new way of moving and forgot to
    /// record it fails here, which is the only way this measurement can quietly go wrong.
    /// </summary>
    [Fact]
    public void EverySquareStoodOnExceptTheFirstArrivedBySomeRecordedStep()
    {
        var steps = new List<AStepTaken>();

        Reach reach = WorldWalker.Walk(
            new WorldData([Terrace()]), Route, startSquare: new GridPosition(0, 0), steps: steps);

        HashSet<Somewhere> arrived = [.. steps.Select(s => s.To)];

        Assert.All(
            reach.Stood.Select(s => new Somewhere(s.MapId, s.Square)).Where(s => s != reach.Start),
            s => Assert.Contains(s, arrived));

        // And nothing was recorded arriving anywhere the walk did not stand.
        HashSet<Somewhere> stood = [.. reach.Stood.Select(s => new Somewhere(s.MapId, s.Square))];

        Assert.All(steps, s => Assert.Contains(s.To, stood));
    }

    /// <summary>
    /// AND THE WALK SAYS WHERE IT BEGAN. It has always chosen that square itself when no caller
    /// named one, and never reported it — which is fine for every question asked FROM the start
    /// and impossible for one asked ABOUT it.
    /// </summary>
    [Fact]
    public void TheWalkReportsTheSquareItStartedOn()
    {
        Assert.Equal(
            new Somewhere(Route, new GridPosition(3, 1)),
            WorldWalker.Walk(
                new WorldData([Terrace()]), Route, startSquare: new GridPosition(3, 1)).Start);

        // And when nobody says, it is the square the walk actually picked rather than nothing.
        Reach chosen = WorldWalker.Walk(new WorldData([Terrace()]), Route);

        Assert.Equal(Route, chosen.Start.MapId);
        Assert.Contains((Route, chosen.Start.Square), chosen.Stood);
    }
}
