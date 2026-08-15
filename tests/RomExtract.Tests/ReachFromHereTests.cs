using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// How much of the world one save can get to.
/// <para>
/// The server prints a reach figure at startup and it answers a different question: how much
/// of this game is built, walked from the first square with a fresh character's flags. What
/// nobody could ask until now is how much of it a particular save has <em>opened</em> — and
/// the two are only the same number on the first morning.
/// </para>
/// <para>
/// Three walks and a difference: on the flags alone, with the moves this party happens to
/// know, and with water crossed. Which of the three the number jumps at is the answer; the
/// numbers on their own say nothing.
/// </para>
/// </summary>
public class ReachFromHereTests
{
    private const string Here = "1.0";
    private const string Beyond = "1.1";

    /// <summary>
    /// A corridor one square wide, with a door at the end and a boulder in it. One wide on
    /// purpose: in an open room a walk goes round anything, and a fixture where the
    /// obstacle can be walked around is a fixture that tests nothing.
    /// </summary>
    private static WorldData TwoRooms(int shiftedBy, int hiddenBy = 0)
    {
        MapObject boulder = new(1, 5, 0, 1, Direction.Down, 0, false)
        {
            ShiftedBy = shiftedBy,
            HiddenBy = hiddenBy,
        };

        MapData first = new(Here, "PALLET TOWN", 1, 4, new byte[4])
        {
            Objects = shiftedBy == 0 && hiddenBy == 0 ? [] : [boulder],
            Warps = [new Warp(0, 0, 0, Beyond)],
        };

        MapData second = new(Beyond, "VIRIDIAN CITY", 1, 4, new byte[4])
        {
            Warps = [new Warp(0, 0, 0, Here)],
        };

        return new WorldData([first, second]);
    }

    private static (GameWorld World, ServerPlayer Player) Standing(WorldData world, params int[] moves)
    {
        var game = new GameWorld(world, Here, TestRules.All);

        (ServerPlayer player, _) = game.Join(1, "Mason", SavedCharacter.Fresh(Here, 0, 3));

        if (moves.Length > 0)
        {
            player.Party =
            [
                new SavedMon(3, 20, null, 20, StatusCondition.None, Nature.Hardy, moves),
            ];
        }

        return (game, player);
    }

    [Fact]
    public void ItCountsTheMapsThisSaveCanGetTo()
    {
        (GameWorld game, ServerPlayer player) = Standing(TwoRooms(shiftedBy: 0));

        Assert.Contains("2 of 2 maps from here", game.WhereThisSaveCanGet(player)[0]);
    }

    /// <summary>
    /// A boulder nobody can shift closes the second room, and the second line says the
    /// party has nothing that would open it.
    /// </summary>
    [Fact]
    public void SomethingInTheWayClosesWhatIsBehindIt()
    {
        (GameWorld game, ServerPlayer player) = Standing(TwoRooms(shiftedBy: TestRules.FirstMove));

        List<string> said = game.WhereThisSaveCanGet(player);

        Assert.Contains("1 of 2 maps", said[0]);
        Assert.Contains("nothing", said[1]);
    }

    /// <summary>And a party that knows the move opens it, which is the second line's point.</summary>
    [Fact]
    public void APartyThatKnowsTheMoveOpensIt()
    {
        (GameWorld game, ServerPlayer player) =
            Standing(TwoRooms(shiftedBy: TestRules.FirstMove), TestRules.FirstMove);

        List<string> said = game.WhereThisSaveCanGet(player);

        Assert.Contains("1 of 2 maps", said[0]);
        Assert.Contains("2 with what this party knows", said[1]);
    }

    /// <summary>
    /// The one that matters for a story. A boulder hidden by a flag this save holds is not
    /// in the way at all — which is the difference between the startup figure and this one,
    /// and the whole reason a played save needs its own answer.
    /// </summary>
    [Fact]
    public void SomebodyHiddenByAFlagThisSaveHoldsIsNotInTheWay()
    {
        const int Gone = 0x0036;

        (GameWorld game, ServerPlayer player) =
            Standing(TwoRooms(shiftedBy: TestRules.FirstMove, hiddenBy: Gone));

        Assert.Contains("1 of 2 maps", game.WhereThisSaveCanGet(player)[0]);

        player.Script.Set(Gone);

        Assert.Contains("2 of 2 maps", game.WhereThisSaveCanGet(player)[0]);
    }

    /// <summary>And what is in the way is named rather than counted.</summary>
    [Fact]
    public void WhatIsInTheWayIsNamed()
    {
        (GameWorld game, ServerPlayer player) = Standing(TwoRooms(shiftedBy: TestRules.FirstMove));

        Assert.Contains(
            game.WhereThisSaveCanGet(player),
            said => said.Contains("in the way") && said.Contains($"needs move {TestRules.FirstMove}"));
    }
}
