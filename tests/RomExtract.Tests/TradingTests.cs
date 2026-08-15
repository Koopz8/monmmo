using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Two people swapping one thing each.
/// <para>
/// The second verb this game's multiplayer has. The first is seeing each other, and until
/// now that was all of it.
/// </para>
/// <para>
/// Almost every rule here exists because of something that has already gone wrong in this
/// project. The swap is one operation with nothing between its halves, because every
/// duplication bug so far came from one fact written down in two interruptible steps. The
/// last one that can fight cannot be traded away, because the box already learned that. And
/// changing an offer un-agrees both sides, which is not a bug this project had — it is the
/// oldest confidence trick there is, and a trade where the table can change after you say
/// yes is a trade nobody should ever say yes to.
/// </para>
/// </summary>
public class TradingTests
{
    private const string Town = "1.0";

    private static SavedMon Member(int species) =>
        new(species, 20, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

    /// <summary>Two players facing each other, a square apart, each with a party of two.</summary>
    private static (GameWorld World, ServerPlayer One, ServerPlayer Two) Facing(int each = 2)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer one, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));
        (ServerPlayer two, _) = world.Join(2, "Koop", SavedCharacter.Fresh(Town, 3, 3));

        one.Square = new GridPosition(3, 4);
        two.Square = new GridPosition(3, 3);
        one.Facing = Direction.Up;
        two.Facing = Direction.Down;

        one.Party = [.. Enumerable.Range(1, each).Select(Member)];
        two.Party = [.. Enumerable.Range(100, each).Select(Member)];

        return (world, one, two);
    }

    private static void Trading(GameWorld world, ServerPlayer one, ServerPlayer two)
    {
        world.AskToTrade(one.Id, two.Id);
        world.AskToTrade(two.Id, one.Id);
    }

    // ---- getting into one ---------------------------------------------------------

    /// <summary>Two requests pointing at each other is the whole handshake.</summary>
    [Fact]
    public void TwoRequestsPointingAtEachOtherOpenATrade()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Assert.Empty(world.AskToTrade(one.Id, two.Id).Select(o => o.Message).OfType<TradeUpdated>());

        List<Outgoing> agreed = world.AskToTrade(two.Id, one.Id);

        Assert.Equal(1, world.OpenTrades);
        Assert.Equal(2, agreed.Select(o => o.Message).OfType<TradeUpdated>().Count());
    }

    /// <summary>And the first one only asks, which the other is told about.</summary>
    [Fact]
    public void AskingTellsTheOtherAndNothingElse()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        List<Outgoing> asked = world.AskToTrade(one.Id, two.Id);

        TradeAsked said = Assert.Single(asked.Select(o => o.Message).OfType<TradeAsked>());

        Assert.Equal(one.Id, said.FromPlayerId);
        Assert.Equal(0, world.OpenTrades);
        Assert.All(asked, o => Assert.Equal(two.Id, o.OnlyTo));
    }

    [Fact]
    public void NobodyTradesWithThemselves()
    {
        (GameWorld world, ServerPlayer one, _) = Facing();

        Assert.Empty(world.AskToTrade(one.Id, one.Id));
        Assert.Contains("themselves", world.LastTrade ?? "");
    }

    /// <summary>
    /// Out of reach is refused, by the same rule that decides whether somebody can be
    /// spoken to. A trade across a room is one nobody can see happening.
    /// </summary>
    [Fact]
    public void SomebodyAcrossTheRoomIsRefused()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        two.Square = new GridPosition(7, 7);

        Assert.Empty(world.AskToTrade(one.Id, two.Id));
        Assert.Contains("within reach", world.LastTrade ?? "");
    }

    // ---- what is on the table -----------------------------------------------------

    [Fact]
    public void BothSidesSeeWhatIsOffered()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Trading(world, one, two);

        List<Outgoing> said = world.OfferInTrade(one.Id, 0);

        TradeUpdated mine = said.Where(o => o.OnlyTo == one.Id).Select(o => o.Message).OfType<TradeUpdated>().Single();
        TradeUpdated theirs = said.Where(o => o.OnlyTo == two.Id).Select(o => o.Message).OfType<TradeUpdated>().Single();

        Assert.Equal(one.Party[0].Species, mine.Yours?.Species);
        Assert.Equal(one.Party[0].Species, theirs.Theirs?.Species);
        Assert.Null(theirs.Yours);
    }

    /// <summary>
    /// The rule that makes a trade safe to agree to. Changing what is on the table after
    /// the other side has said yes un-says it, every time.
    /// </summary>
    [Fact]
    public void ChangingAnOfferUnagreesBothSides()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Trading(world, one, two);
        world.OfferInTrade(one.Id, 0);
        world.OfferInTrade(two.Id, 0);
        world.ConfirmTrade(one.Id, true);
        world.ConfirmTrade(two.Id, true);

        // Which would have completed it — so the same sequence with a change in the middle
        // is the test, and it must not.
        Assert.Equal(0, world.OpenTrades);

        (GameWorld again, ServerPlayer three, ServerPlayer four) = Facing();

        Trading(again, three, four);
        again.OfferInTrade(three.Id, 0);
        again.OfferInTrade(four.Id, 0);
        again.ConfirmTrade(four.Id, true);

        List<Outgoing> changed = again.OfferInTrade(three.Id, 1);

        TradeUpdated after = changed
            .Where(o => o.OnlyTo == four.Id).Select(o => o.Message).OfType<TradeUpdated>().Single();

        Assert.False(after.YouAgreed);
        Assert.False(after.TheyAgreed);
        Assert.Equal(1, again.OpenTrades);
    }

    /// <summary>
    /// The same rule the box keeps. Somebody who trades away the last thing they can fight
    /// with cannot walk out of the room they are standing in.
    /// </summary>
    [Fact]
    public void TheLastOneThatCanFightStaysWhereItIs()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing(each: 1);

        Trading(world, one, two);

        Assert.Empty(world.OfferInTrade(one.Id, 0));
        Assert.Contains("last one that can fight", world.LastTrade ?? "");
    }

    [Fact]
    public void ASlotNobodyHasIsRefused()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Trading(world, one, two);

        Assert.Empty(world.OfferInTrade(one.Id, 9));
        Assert.Contains("no slot", world.LastTrade ?? "");
    }

    // ---- the swap -----------------------------------------------------------------

    /// <summary>
    /// The whole point. One each, both ways, in one operation with nothing between its
    /// halves — which is where every duplication this project has had came from.
    /// </summary>
    [Fact]
    public void BothAgreeingSwapsOneEach()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        int mine = one.Party[0].Species;
        int theirs = two.Party[0].Species;

        Trading(world, one, two);
        world.OfferInTrade(one.Id, 0);
        world.OfferInTrade(two.Id, 0);
        world.ConfirmTrade(one.Id, true);

        List<Outgoing> done = world.ConfirmTrade(two.Id, true);

        Assert.Equal(theirs, one.Party[0].Species);
        Assert.Equal(mine, two.Party[0].Species);

        // And nothing was made or lost on either side.
        Assert.Equal(2, one.Party.Count);
        Assert.Equal(2, two.Party.Count);

        Assert.Equal(2, done.Select(o => o.Message).OfType<TradeEnded>().Count());
        Assert.Equal(0, world.OpenTrades);
    }

    /// <summary>One side agreeing is not a trade.</summary>
    [Fact]
    public void OneSideAgreeingChangesNothing()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        int mine = one.Party[0].Species;

        Trading(world, one, two);
        world.OfferInTrade(one.Id, 0);
        world.OfferInTrade(two.Id, 0);
        world.ConfirmTrade(one.Id, true);

        Assert.Equal(mine, one.Party[0].Species);
        Assert.Equal(1, world.OpenTrades);
    }

    /// <summary>And agreeing with an empty table is not a trade either.</summary>
    [Fact]
    public void AgreeingWithNothingOnTheTableChangesNothing()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Trading(world, one, two);
        world.ConfirmTrade(one.Id, true);
        world.ConfirmTrade(two.Id, true);

        Assert.Equal(1, world.OpenTrades);
    }

    // ---- getting out of one -------------------------------------------------------

    [Fact]
    public void WalkingAwayEndsItForBoth()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Trading(world, one, two);

        List<Outgoing> stopped = world.CancelTrade(one.Id);

        Assert.Equal(0, world.OpenTrades);
        Assert.Equal(2, stopped.Select(o => o.Message).OfType<TradeEnded>().Count());
    }

    /// <summary>
    /// And leaving the world does. A trade that outlived one of the two people in it would
    /// be a trade the other could never get out of.
    /// </summary>
    [Fact]
    public void LeavingEndsItForTheOther()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Trading(world, one, two);

        List<Outgoing> left = world.Leave(one.Id);

        Assert.Equal(0, world.OpenTrades);
        Assert.Contains(left.Select(o => o.Message).OfType<TradeEnded>(), e => e.Reason == "They left.");
    }

    /// <summary>Nobody is in two at once.</summary>
    [Fact]
    public void OneTradeAtATime()
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer one, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));
        (ServerPlayer two, _) = world.Join(2, "Koop", SavedCharacter.Fresh(Town, 3, 3));
        (ServerPlayer three, _) = world.Join(3, "Ada", SavedCharacter.Fresh(Town, 3, 5));

        one.Square = new GridPosition(3, 4);
        two.Square = new GridPosition(3, 3);
        three.Square = new GridPosition(3, 5);
        one.Facing = Direction.Up;

        world.AskToTrade(one.Id, two.Id);
        world.AskToTrade(two.Id, one.Id);

        one.Facing = Direction.Down;

        Assert.Empty(world.AskToTrade(three.Id, one.Id));
        Assert.Equal(1, world.OpenTrades);
    }
}
