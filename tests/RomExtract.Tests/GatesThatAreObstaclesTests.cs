using PokeMmo.Core.World;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Twenty-two of the sixty-two gates no walk can open turned out to hold trees and rocks.
/// <para>
/// They read as the code boundary because nothing sets their flags, and that is true and
/// misleading in exactly the way the pickups were: the script asks who knows the move, takes the
/// object off the map, and the flag that keeps it off is set by the routine rather than by any
/// <c>setflag</c>. Same mechanism, one class further out, and the classifier filed all of them
/// under "the true boundary" until this.
/// </para>
/// <para>
/// <b>Found by shape and not by address.</b> The three addresses this cartridge happens to use
/// are printed by the instrument, not written down here — and the fixture below is built out of
/// the shape rather than out of them.
/// </para>
/// </summary>
public sealed class GatesThatAreObstaclesTests
{
    private const byte Filler = 0x77;
    private const byte End = 0x02;
    private const byte FindMove = 0x7C;
    private const byte TakeOffTheMap = 0x53;

    private const int AsksAndTakes = 0x1000;
    private const int AsksOnly = 0x1100;
    private const int TakesOnly = 0x1200;
    private const int NeitherOne = 0x1300;

    private const int ATree = 0x0011;
    private const int ABoulderThatStays = 0x0012;
    private const int SomethingRemovedWithNoQuestion = 0x0013;
    private const int APerson = 0x0014;
    private const int ATreeAndAPerson = 0x0015;

    private static Rom Image()
    {
        var image = new byte[0x20000];

        Array.Fill(image, Filler);

        // asks who knows a move, then takes an object off the map, then ends.
        Put(image, AsksAndTakes, FindMove, 0x0F, 0x00, TakeOffTheMap, 0x01, 0x00, End);

        // asks, and never takes anything off anything.
        Put(image, AsksOnly, FindMove, 0x46, 0x00, End);

        // takes, and never asks.
        Put(image, TakesOnly, TakeOffTheMap, 0x01, 0x00, End);

        Put(image, NeitherOne, End);

        return new Rom(image);
    }

    private static void Put(byte[] image, int at, params int[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++) image[at + i] = (byte)bytes[i];
    }

    private static MapObject Hidden(int id, int flag, int script) =>
        new(id, 1, id, 1, Direction.Down, 0, false)
        {
            HiddenBy = flag,
            ScriptAddress = Rom.BaseAddress + (uint)script,
        };

    /// <summary>
    /// Five gates, one of each kind, and one deliberately mixed.
    /// <para>
    /// The mixed one is the point of the "all rather than any" rule: a flag that holds a tree
    /// AND a person is not an obstacle's flag, and calling it one hides the person inside a
    /// bucket named for scenery.
    /// </para>
    /// </summary>
    private static WorldData World() =>
        new(
        [
            new MapData("1.0", "1.0", 8, 8, new byte[64])
            {
                Objects =
                [
                    Hidden(1, ATree, AsksAndTakes),
                    Hidden(2, ABoulderThatStays, AsksOnly),
                    Hidden(3, SomethingRemovedWithNoQuestion, TakesOnly),
                    Hidden(4, APerson, NeitherOne),
                    Hidden(5, ATreeAndAPerson, AsksAndTakes),
                    Hidden(6, ATreeAndAPerson, NeitherOne),
                ],
            },
        ]);

    /// <summary>Asked about a move and then taken off the map is an obstacle.</summary>
    [Fact]
    public void SomethingAskedAboutAMoveAndThenRemovedIsAnObstacle()
    {
        (IReadOnlyList<int> flags, IReadOnlyList<uint> scripts, _) =
            GatesThatAreObstacles.In(Image(), World());

        Assert.Equal(new[] { ATree }, flags);
        Assert.Equal(new[] { Rom.BaseAddress + AsksAndTakes }, scripts);
    }

    /// <summary>
    /// AND THE ONE THAT IS ASKED AND STAYS IS KEPT APART, not folded in.
    /// <para>
    /// Seven of this cartridge's gates hold something whose script asks who knows move 70 and
    /// never removes anything. Whatever clears those is a different mechanism from the one that
    /// clears a tree, and widening the rule to catch them would be picking a shape to fit an
    /// answer.
    /// </para>
    /// </summary>
    [Fact]
    public void SomethingAskedAboutAMoveAndNeverRemovedIsADifferentThing()
    {
        (IReadOnlyList<int> flags, _, IReadOnlyList<int> staying) =
            GatesThatAreObstacles.In(Image(), World());

        Assert.Equal(new[] { ABoulderThatStays }, staying);
        Assert.DoesNotContain(ABoulderThatStays, flags);
    }

    /// <summary>
    /// Taken off the map with nobody asked anything is neither — the two halves of the shape are
    /// both load-bearing.
    /// </summary>
    [Fact]
    public void SomethingRemovedWithoutBeingAskedIsNeither()
    {
        (IReadOnlyList<int> flags, _, IReadOnlyList<int> staying) =
            GatesThatAreObstacles.In(Image(), World());

        Assert.DoesNotContain(SomethingRemovedWithNoQuestion, flags);
        Assert.DoesNotContain(SomethingRemovedWithNoQuestion, staying);
    }

    /// <summary>
    /// ALL RATHER THAN ANY: a flag holding a tree and a person is not an obstacle's flag.
    /// </summary>
    [Fact]
    public void AFlagHoldingATreeAndAPersonIsNotAnObstaclesFlag()
    {
        (IReadOnlyList<int> flags, _, IReadOnlyList<int> staying) =
            GatesThatAreObstacles.In(Image(), World());

        Assert.DoesNotContain(ATreeAndAPerson, flags);
        Assert.DoesNotContain(ATreeAndAPerson, staying);
    }

    /// <summary>
    /// And a world with nothing of the kind comes back empty, which is what makes the count in
    /// the instrument a count of something.
    /// </summary>
    [Fact]
    public void AWorldWithNoObstaclesComesBackEmpty()
    {
        WorldData plain =
            new([
                new MapData("1.0", "1.0", 8, 8, new byte[64])
                {
                    Objects = [Hidden(1, APerson, NeitherOne)],
                },
            ]);

        (IReadOnlyList<int> flags, IReadOnlyList<uint> scripts, IReadOnlyList<int> staying) =
            GatesThatAreObstacles.In(Image(), plain);

        Assert.Empty(flags);
        Assert.Empty(scripts);
        Assert.Empty(staying);
    }
}
