using PokeMmo.Core.World;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The flags nothing in the world can move, which is the general case of every wall.
/// <para>
/// One door in SAFFRON took ten measurements to place, and the answer was that nothing
/// readable sets the flag behind it. That sounded like a finding about SAFFRON until the
/// counts were put side by side: most flags that move a person are moved by nothing this
/// project can read, and the door was one ordinary member of a large set.
/// </para>
/// <para>
/// Two opposite failures, and they must not be added together. Somebody the story would move
/// and never does is <b>in the way</b> — that is a blocked doorway, and it gets noticed.
/// Somebody the story would bring in and never does is <b>invisible</b>, and nothing has ever
/// noticed one at all.
/// </para>
/// </summary>
public class WhatNothingMovesTests
{
    private const int Stuck = 0x003E;
    private const int Absent = 0x0055;
    private const int Movable = 0x0037;
    private const int OnlyCleared = 0x0036;

    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static MapObject Person(int id, int hiddenBy) =>
        new(id, 1, 1, 1, Direction.Down, 0, false) { HiddenBy = hiddenBy };

    /// <summary>
    /// Four flags, one of each kind, and two people behind the stuck one so that counting
    /// people and counting flags cannot be confused.
    /// </summary>
    private static WorldData World() =>
        new(
        [
            Room("3.10") with { Objects = [Person(1, Stuck), Person(2, Stuck), Person(3, Movable)] },
            Room("14.0") with { Objects = [Person(1, Stuck), Person(2, Absent), Person(3, OnlyCleared)] },
        ])
        {
            // A fresh save is not an empty save: this one hides somebody before the first frame.
            FlagsAtStart = [Absent],
        };

    private static IReadOnlyList<WhatMoves> Ranked() =>
        WhoMovesEachFlag.Rank(World(), [Movable], [OnlyCleared]);

    /// <summary>
    /// Somebody nothing will ever move is standing there for ever, and that is the wall list.
    /// </summary>
    [Fact]
    public void AFlagNothingSetsHoldsPeopleWhoNeverLeave()
    {
        WhatMoves flag = Assert.Single(Ranked(), f => f.Flag == Stuck);

        Assert.True(flag.StuckThere);
        Assert.False(flag.NeverArrive);
        Assert.Equal(3, flag.People);
        Assert.Equal(2, flag.Maps);
    }

    /// <summary>
    /// And the mirror, which is the half nothing has ever looked at: the flag is on before the
    /// first frame and nothing clears it, so the person is invisible rather than in the way.
    /// <b>The two are opposite failures and adding them together would hide the second.</b>
    /// </summary>
    [Fact]
    public void AndAFlagNothingClearsHoldsPeopleWhoNeverArrive()
    {
        WhatMoves flag = Assert.Single(Ranked(), f => f.Flag == Absent);

        Assert.True(flag.NeverArrive);
        Assert.False(flag.StuckThere);
    }

    /// <summary>
    /// A flag a script sets is not the code boundary, however many people are behind it. The
    /// whole list is worthless if the things that do work are on it.
    /// </summary>
    [Fact]
    public void AFlagAScriptSetsIsNotTheCodeBoundary()
    {
        WhatMoves flag = Assert.Single(Ranked(), f => f.Flag == Movable);

        Assert.False(flag.NothingCanMoveIt);
        Assert.False(flag.StuckThere);
    }

    /// <summary>
    /// And clearing counts as moving it just as setting does.
    /// <para>
    /// The decoy, and the reason the two sets are handed in separately rather than merged.
    /// Three flags in the middle of this game are opened by a <c>clearflag</c> and nothing
    /// else — milestone 73 was about finding them — so a scan that only counted
    /// <c>setflag</c> would put the whole middle of the story on the code-boundary list and
    /// send the next session hunting for a routine that does not exist.
    /// </para>
    /// </summary>
    [Fact]
    public void AndAFlagOnlyEverClearedIsMovableToo()
    {
        WhatMoves flag = Assert.Single(Ranked(), f => f.Flag == OnlyCleared);

        Assert.True(flag.ClearedByAScript);
        Assert.False(flag.SetByAScript);
        Assert.False(flag.NothingCanMoveIt);
    }

    /// <summary>
    /// Worst first, and the ones nothing can move before the ones something can. A ranked list
    /// in the wrong order is a list nobody reads past the top of — which is the fault already
    /// recorded against the shut-doors list, where thirty lines said one ruled-out thing and
    /// the doors the session was about were pushed off the end.
    /// </summary>
    [Fact]
    public void TheOnesNothingCanMoveComeFirstAndTheBiggestOfThoseFirstOfAll()
    {
        IReadOnlyList<WhatMoves> ranked = Ranked();

        Assert.Equal(Stuck, ranked[0].Flag);
        Assert.Equal(Absent, ranked[1].Flag);
        Assert.All(ranked.Take(2), f => Assert.True(f.NothingCanMoveIt));
        Assert.All(ranked.Skip(2), f => Assert.False(f.NothingCanMoveIt));
    }
}
