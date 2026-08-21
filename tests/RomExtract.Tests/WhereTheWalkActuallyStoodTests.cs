using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Reaching a map and standing on a square are two facts, and the run only carried the first (282).
/// <para>
/// 249 asked how much of the buried list the walk goes over and answered it with <c>Reached</c>,
/// which is the MAP — so "the widest walk stands on 182 of 183" was the count of buried items whose
/// map it got to. 281 then found <b>41 of the 183 sit on squares nothing can stand on at all</b>,
/// and a map-level answer cannot see one of them. Asked properly the widest run is underfoot on
/// <b>137</b>, out of the <b>142</b> it could be.
/// </para>
/// </summary>
public sealed class WhereTheWalkActuallyStoodTests
{
    private const int Moved = 0x0500;

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>A five-by-five room, walkable everywhere except the squares named.</summary>
    private static MapData Room(string id, params (int X, int Y)[] solid)
    {
        var collision = new byte[25];

        foreach ((int x, int y) in solid) collision[(y * 5) + x] = 1;

        return new MapData(id, id, 5, 5, collision);
    }

    private static Attempt Run(params MapData[] maps) =>
        Autoplayer.Play(
            new WorldData([.. maps]),
            maps[0].Id,
            TestRules.All,
            (address, _, _) => Nothing with { FlagsSet = [Moved + (int)(address >> 12)] });

    /// <summary>The walk records the squares it stood on, and there are some.</summary>
    [Fact]
    public void TheRunSaysWhichSquaresItStoodOn()
    {
        Attempt played = Run(Room("1.0"));

        Assert.NotEmpty(played.StoodOn);
        Assert.All(played.StoodOn, one => Assert.Equal("1.0", one.MapId));
    }

    /// <summary>
    /// <b>THE THING: a solid square in a room the walk reaches is reached and not stood on.</b>
    /// This is the difference 249 could not see, and the whole of it.
    /// </summary>
    [Fact]
    public void AMapIsReachedWhereASolidSquareInItIsNot()
    {
        Attempt played = Run(Room("1.0", (2, 2)));

        Assert.Contains("1.0", played.Reached);
        Assert.DoesNotContain(("1.0", new GridPosition(2, 2)), played.StoodOn);

        // And its neighbours are stood on, so the square is missing because it is solid rather
        // than because the walk never went near it.
        Assert.Contains(("1.0", new GridPosition(2, 1)), played.StoodOn);
        Assert.Contains(("1.0", new GridPosition(1, 2)), played.StoodOn);
    }

    /// <summary>
    /// Every walkable square of a room with one way in is stood on, so the set is the walk's own
    /// reach and not a sample of it.
    /// </summary>
    [Fact]
    public void EveryWalkableSquareOfAReachedRoomIsInIt()
    {
        Attempt played = Run(Room("1.0", (2, 2)));

        Assert.Equal(24, played.StoodOn.Count);
    }

    /// <summary>
    /// And a map the walk never reaches contributes nothing — the set is squares stood on, not
    /// squares that exist.
    /// </summary>
    [Fact]
    public void AMapItNeverReachesIsNotInIt()
    {
        Attempt played = Run(Room("1.0"), Room("1.1"));

        Assert.DoesNotContain("1.1", played.Reached);
        Assert.DoesNotContain(played.StoodOn, one => one.MapId == "1.1");
    }
}
