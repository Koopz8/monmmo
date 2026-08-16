using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Travelling together — the rule instancing has owed since copies existed.
/// <para>
/// Copies are what make a busy place affordable and they are the one thing in this server
/// that can put two people who want to be together in rooms that cannot see each other. A
/// doorway half-solved it: walking into a new place prefers the copy you were already in, so
/// two people who go through the same door stay together. That stops the moment one of them
/// warps, takes another route, or is sent somewhere by a script.
/// </para>
/// <para>
/// And the half that matters for playing a story together: what you can <em>see</em> of the
/// world is your own flags, so two friends on one square already look at different rooms.
/// While travelling, that becomes the union of the company's — <b>borrowed, never written</b>.
/// Somebody three gyms behind can walk everywhere their friend can, immediately, and keeps
/// exactly what they earned when they go.
/// </para>
/// </summary>
public class TravellingTogetherTests
{
    private const string Town = "1.0";

    private static (GameWorld World, ServerPlayer One, ServerPlayer Two) Together()
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer one, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));
        (ServerPlayer two, _) = world.Join(2, "Koop", SavedCharacter.Fresh(Town, 3, 3));

        return (world, one, two);
    }

    private static void Agree(GameWorld world, ServerPlayer one, ServerPlayer two)
    {
        world.AskToTravelWith(one.Id, two.Id);
        world.AskToTravelWith(two.Id, one.Id);
    }

    // ---- the handshake -------------------------------------------------------------------

    /// <summary>
    /// Two requests pointing at each other, and nothing else — the same handshake a trade
    /// and a duel use, because a player should only have to learn it once.
    /// </summary>
    [Fact]
    public void AskingBackIsAgreeing()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        world.AskToTravelWith(one.Id, two.Id);

        Assert.Empty(world.CompanyOf(one.Id));

        world.AskToTravelWith(two.Id, one.Id);

        Assert.Equal(2, world.CompanyOf(one.Id).Count);
        Assert.Contains(two.Id, world.CompanyOf(one.Id));
    }

    /// <summary>Nobody travels with themselves, and it says so rather than doing nothing.</summary>
    [Fact]
    public void NobodyTravelsWithThemselves()
    {
        (GameWorld world, ServerPlayer one, _) = Together();

        world.AskToTravelWith(one.Id, one.Id);

        Assert.Empty(world.CompanyOf(one.Id));
        Assert.Contains("themselves", world.LastCompany);
    }

    /// <summary>
    /// And somebody who is not within reach is refused, for the reason milestone 118 gave:
    /// being carried across the world by typing a name would make every locked door optional.
    /// </summary>
    [Fact]
    public void SomebodyOutOfReachIsRefused()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        two.Square = new GridPosition(7, 7);

        world.AskToTravelWith(one.Id, two.Id);

        Assert.Empty(world.CompanyOf(one.Id));
        Assert.Contains("reach", world.LastCompany);
    }

    /// <summary>Leaving is immediate, and both sides are told.</summary>
    [Fact]
    public void LeavingIsImmediate()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        Agree(world, one, two);

        List<Outgoing> said = world.TravelAlone(one.Id);

        Assert.Empty(world.CompanyOf(one.Id));
        Assert.Empty(world.CompanyOf(two.Id));

        // A company of one is not a company, so the other one is travelling alone too — and
        // is told so with the same message, an empty list.
        Assert.Contains(
            said,
            o => o.Message is TravellingWith { PlayerIds.Count: 0 } && o.OnlyTo == two.Id);
    }

    /// <summary>
    /// And somebody leaving the world takes their company membership with them, rather than
    /// leaving the others travelling with a number.
    /// </summary>
    [Fact]
    public void LeavingTheWorldLeavesTheCompany()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        Agree(world, one, two);

        world.Leave(two.Id);

        Assert.Empty(world.CompanyOf(one.Id));
    }

    // ---- what it is for ------------------------------------------------------------------

    /// <summary>
    /// A member arriving somewhere lands in the copy the rest are in.
    /// <para>
    /// This is the whole point. Preferring the copy you came from works while two people
    /// walk through one door and fails the moment they arrive by different routes — which
    /// is what a warp, a ferry and a script all are.
    /// </para>
    /// </summary>
    [Fact]
    public void AMemberArrivesInTheCopyTheRestAreIn()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        Agree(world, one, two);

        two.Copy = 3;

        Assert.Equal(3, world.CopyForTest(one.Id, Town));
    }

    /// <summary>
    /// And somebody travelling alone gets the ordinary answer, which is what makes the one
    /// above mean anything.
    /// </summary>
    [Fact]
    public void AndSomebodyAloneGetsTheOrdinaryAnswer()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        two.Copy = 3;

        Assert.Equal(0, world.CopyForTest(one.Id, Town));
    }

    // ---- borrowed, never written ---------------------------------------------------------

    /// <summary>
    /// While travelling, you see the world the company has opened between them.
    /// <para>
    /// The seam is <c>VisibleTo</c>, which asks a player's own flags whether somebody is
    /// standing there. Two friends on one square already look at different rooms; this is
    /// what makes them look at the same one.
    /// </para>
    /// </summary>
    [Fact]
    public void TravellingShowsYouTheWorldTheCompanyHasOpened()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        const int Gate = 0x0037;

        two.Script.Set(Gate);

        Assert.False(world.SeesForTest(one.Id, Gate));

        Agree(world, one, two);

        Assert.True(world.SeesForTest(one.Id, Gate));
    }

    /// <summary>
    /// <b>And nothing was written to their save.</b>
    /// <para>
    /// The rule the whole arrangement turns on, and the one that makes dropping in and out
    /// work. Copying the flags across on joining would do the same thing irreversibly, and
    /// would put a save into a state its own inventory cannot justify — you have "delivered
    /// the parcel" set and no parcel, and a script an hour away asks.
    /// </para>
    /// </summary>
    [Fact]
    public void AndNothingWasWrittenToTheirSave()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        const int Gate = 0x0037;

        two.Script.Set(Gate);

        Agree(world, one, two);

        Assert.False(one.Script.Has(Gate));
    }

    /// <summary>
    /// And it is handed back the moment they stop travelling.
    /// <para>
    /// The friend logs off after an hour; you keep what you earned and lose only what you
    /// were looking through somebody else at. There is nothing to undo, because nothing was
    /// done.
    /// </para>
    /// </summary>
    [Fact]
    public void AndItIsHandedBackWhenTheyStopTravelling()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        const int Gate = 0x0037;

        two.Script.Set(Gate);

        Agree(world, one, two);

        Assert.True(world.SeesForTest(one.Id, Gate));

        world.TravelAlone(two.Id);

        Assert.False(world.SeesForTest(one.Id, Gate));
    }

    /// <summary>
    /// And what they earned while travelling is theirs and stays theirs.
    /// <para>
    /// The other half. Borrowing is how you travel together; a flag written by a script this
    /// save actually ran is how you progress, and leaving does not take it back.
    /// </para>
    /// </summary>
    [Fact]
    public void ButWhatTheyEarnedIsTheirsAndStays()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        const int Earned = 0x0036;

        Agree(world, one, two);

        one.Script.Set(Earned);

        world.TravelAlone(one.Id);

        Assert.True(one.Script.Has(Earned));
        Assert.True(world.SeesForTest(one.Id, Earned));
    }
}
