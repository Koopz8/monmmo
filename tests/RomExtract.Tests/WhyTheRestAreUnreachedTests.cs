using PokeMmo.Core.World;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Why the maps no run reaches are never reached (303).
/// <para>
/// The floor table has said "388 of 425" for milestones and nothing had asked what the other
/// thirty-seven are. <b>A count of unreached maps is not a count of reasons</b>: eleven have no way
/// in at all, eighteen are behind one another, and <b>eight</b> are named by ground the run stands
/// on. Three of the eight account for twenty-two of the twenty-six.
/// </para>
/// </summary>
public sealed class WhyTheRestAreUnreachedTests
{
    private static MapData Map(string id, IEnumerable<string>? warpsTo = null,
        IEnumerable<string>? bordersTo = null) =>
        new(id, id, 4, 4, new byte[16])
        {
            Warps =
            [
                .. (warpsTo ?? []).Select((t, i) => new Warp(0, i, 0, t)),
            ],
            Connections =
            [
                .. (bordersTo ?? []).Select(t => new MapConnection(ConnectionSide.Up, 0, t)),
            ],
        };

    private static WhyUnreached Why(IReadOnlyList<Unreached> sorted, string id) =>
        sorted.Single(u => u.MapId == id).Why;

    /// <summary>
    /// <b>THE THING.</b> The three buckets, with one map of each in the same world — a fixture with
    /// one bucket cannot tell a classification from a constant.
    /// </summary>
    [Fact]
    public void TheThreeBucketsAreToldApart()
    {
        List<MapData> maps =
        [
            Map("1.0", warpsTo: ["1.1", "1.3"]),   // reached, and it names two of the rest
            Map("1.1", warpsTo: ["1.2"]),          // named from reached ground — a ROOT
            Map("1.2"),                            // only named from 1.1, which is unreached
            Map("1.3"),                            // named from reached ground too
            Map("1.4"),                            // nothing names it at all
        ];

        IReadOnlyList<Unreached> sorted = WhyTheRestAreUnreached.In(maps, new HashSet<string> { "1.0" });

        Assert.Equal(WhyUnreached.Reached, Why(sorted, "1.0"));
        Assert.Equal(WhyUnreached.NamedFromReachedGround, Why(sorted, "1.1"));
        Assert.Equal(WhyUnreached.OnlyFromSomewhereUnreached, Why(sorted, "1.2"));
        Assert.Equal(WhyUnreached.NamedFromReachedGround, Why(sorted, "1.3"));
        Assert.Equal(WhyUnreached.NoWayInAtAll, Why(sorted, "1.4"));
    }

    /// <summary>
    /// <b>THE SENTINEL IS NOT A WAY IN — and this is a DECOY, because the file cannot hold the
    /// shape it guards.</b> A warp is dynamic exactly when its target is <c>127.127</c>, so
    /// dropping those can only ever change the answer for a map whose OWN id is <c>127.127</c>,
    /// and no bank in this cartridge has 128 maps in it.
    /// <para>
    /// The first version of this fixture pointed a sentinel warp at one map and asserted about
    /// ANOTHER, so a break removing the filter came back green — the filter had nothing to do with
    /// either. A rule the cartridge never exercises is a rule no break can be aimed at, so the
    /// fixture carries the case instead (57, and 300 did the same).
    /// </para>
    /// </summary>
    [Fact]
    public void TheSentinelIsNotAWayIn()
    {
        string sentinel = $"{Warp.Dynamic}.{Warp.Dynamic}";

        List<MapData> maps =
        [
            Map("1.0", warpsTo: [sentinel, "1.2"]),
            Map(sentinel),
            Map("1.2"),
        ];

        IReadOnlyList<Unreached> sorted = WhyTheRestAreUnreached.In(maps, new HashSet<string> { "1.0" });

        // The sentinel map is named by a warp and it is still NO WAY IN, because that warp names
        // the runtime's marker rather than a place.
        Assert.Equal(WhyUnreached.NoWayInAtAll, Why(sorted, sentinel));

        // And the ordinary warp from the very same map is still a way in, so the fixture is about
        // the sentinel and not about 1.0 being ignored.
        Assert.Equal(WhyUnreached.NamedFromReachedGround, Why(sorted, "1.2"));
    }

    /// <summary>
    /// A border is a way in as much as a warp is, and the two are counted apart — because a root
    /// named ONLY by borders is a crossing the walk refuses rather than a door it does not take,
    /// and on this cartridge exactly one of the eight is that (SAFFRON CITY, 0 warps and 4 borders).
    /// </summary>
    [Fact]
    public void ABorderIsAWayInAndIsCountedApart()
    {
        List<MapData> maps = [Map("1.0", bordersTo: ["1.1"]), Map("1.1")];

        Unreached one = WhyTheRestAreUnreached
            .In(maps, new HashSet<string> { "1.0" })
            .Single(u => u.MapId == "1.1");

        Assert.Equal(WhyUnreached.NamedFromReachedGround, one.Why);
        Assert.Empty(one.NamedByWarp);
        Assert.Equal(["1.0"], one.NamedByBorder);
    }

    /// <summary>
    /// <b>A COUNT IS NOT A RANKING</b> (trap 3). Eight roots is the count of reasons; what says
    /// which reason matters is how much of the world sits behind it, and on this cartridge three
    /// of the eight carry twenty-two of the twenty-six.
    /// <para>
    /// The fixture has a root with a chain behind it and a root with nothing, so a version
    /// returning a constant — one, or the whole unreached set — fails on one of the two.
    /// </para>
    /// </summary>
    [Fact]
    public void EachRootIsPricedByWhatSitsBehindIt()
    {
        List<MapData> maps =
        [
            Map("1.0", warpsTo: ["1.1", "1.4"]),
            Map("1.1", warpsTo: ["1.2"]),
            Map("1.2", warpsTo: ["1.3"]),
            Map("1.3"),
            Map("1.4"),
        ];

        IReadOnlyList<Unreached> sorted =
            WhyTheRestAreUnreached.In(maps, new HashSet<string> { "1.0" });

        IReadOnlyDictionary<string, int> cost =
            WhyTheRestAreUnreached.WhatEachRootCosts(maps, sorted);

        // The root itself plus the two behind it.
        Assert.Equal(3, cost["1.1"]);

        // And a root with nothing behind it is one, not nought — it is itself a map nothing reaches.
        Assert.Equal(1, cost["1.4"]);
    }

    /// <summary>
    /// And a chain that leads back out to reached ground does not carry the maps on the far side:
    /// only unreached maps are walked, so the closure cannot leak into the world the run already
    /// has.
    /// </summary>
    [Fact]
    public void TheClosureWalksOnlyUnreachedGround()
    {
        List<MapData> maps =
        [
            Map("1.0", warpsTo: ["1.1"]),
            Map("1.1", warpsTo: ["1.0", "1.2"]),
            Map("1.2"),
        ];

        IReadOnlyList<Unreached> sorted =
            WhyTheRestAreUnreached.In(maps, new HashSet<string> { "1.0" });

        Assert.Equal(2, WhyTheRestAreUnreached.WhatEachRootCosts(maps, sorted)["1.1"]);
    }
}
