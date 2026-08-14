using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Handing something over, which is the half of carrying that was missing.
/// <para>
/// THIEF could take a held item off somebody and nothing could give one. That made
/// stealing the only way a player's own party ever carried anything, and the eighty-seven
/// trainers whose parties hold something the only source in the world.
/// </para>
/// <para>
/// Which things can be carried is not a field. The obvious reading — anything with a
/// hold effect — is wrong in a way that would have been hard to notice, because most of
/// what a player hands over has no hold effect at all: a Potion held does nothing and is
/// still held. The pocket is what says it. Across three hundred and eight items the
/// field is non-zero in exactly two pockets, ordinary items and berries, and never once
/// among the twelve balls, the fifty-eight machines or the fifty-five key items.
/// </para>
/// </summary>
public class HandingOverTests
{
    private static (GameWorld World, ServerPlayer Player) Standing(int held = 0)
    {
        const string town = "1.0";

        MapData map = new(town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map]), town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(town, 3, 4));

        player.Party =
        [
            new SavedMon(3, 10, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
            {
                HeldItem = held,
            },
        ];

        return (world, player);
    }

    // ---- which pockets holding is for ------------------------------------------------

    [Fact]
    public void APocketWithHoldEffectsInItIsAPocketThingsAreCarriedFrom()
    {
        Assert.True(TestRules.All.CanBeHeld(TestRules.TrinketItem));

        // And its neighbours in the same pocket, which carry no hold effect of their
        // own. This is the whole reason the pocket is asked rather than the item.
        Assert.True(TestRules.All.CanBeHeld(TestRules.PotionItem));
        Assert.True(TestRules.All.CanBeHeld(TestRules.StoneItem));
    }

    [Fact]
    public void NothingIsCarriedOutOfAPocketThatNeverUsesTheField()
    {
        Assert.False(TestRules.All.CanBeHeld(TestRules.BallItem));
        Assert.False(TestRules.All.CanBeHeld(TestRules.DiscItem));
        Assert.False(TestRules.All.CanBeHeld(TestRules.HiddenMachineItem));
    }

    /// <summary>
    /// And a key item is refused on top of the pocket rule. A thing the player is never
    /// allowed to lose is not a thing to hand to something that can be stolen from.
    /// </summary>
    [Fact]
    public void AKeyItemIsNeverHandedOver()
    {
        Assert.False(TestRules.All.CanBeHeld(TestRules.BicycleItem));
    }

    [Fact]
    public void SomethingThatIsNotAnItemAtAllIsNotCarried()
    {
        Assert.False(TestRules.All.CanBeHeld(9999));
    }

    /// <summary>
    /// The reading is made rather than stored. Nothing new goes into the file — the
    /// answer was always in the item records and nobody had asked them this question.
    /// </summary>
    [Fact]
    public void TheAnswerSurvivesBeingWrittenDown()
    {
        using var buffer = new MemoryStream();

        TestRules.All.Save(buffer);
        buffer.Position = 0;

        GameRules again = GameRules.Load(buffer);

        Assert.True(again.CanBeHeld(TestRules.TrinketItem));
        Assert.False(again.CanBeHeld(TestRules.BallItem));
        Assert.Equal([Pocket.Items], again.HoldingPockets);
    }

    // ---- giving ----------------------------------------------------------------------

    [Fact]
    public void SomebodyEmptyHandedTakesWhatTheyAreGiven()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        player.Bag.Add(TestRules.TrinketItem, 1);

        List<Outgoing> said = world.GiveItem(player.Id, TestRules.TrinketItem, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: "Handed it over." });
        Assert.Equal(TestRules.TrinketItem, player.Party[0].HeldItem);
        Assert.Equal(0, player.Bag.CountOf(TestRules.TrinketItem));
    }

    /// <summary>
    /// Somebody already carrying something swaps rather than refuses, and the old one
    /// goes back in the same breath. The alternative quietly destroys an item, which is
    /// the sort of thing a player notices an hour later and cannot prove.
    /// </summary>
    [Fact]
    public void GivingToFullHandsSwapsRatherThanLosingOne()
    {
        (GameWorld world, ServerPlayer player) = Standing(TestRules.PotionItem);

        player.Bag.Add(TestRules.TrinketItem, 1);

        List<Outgoing> said = world.GiveItem(player.Id, TestRules.TrinketItem, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: "Swapped what it was carrying." });
        Assert.Equal(TestRules.TrinketItem, player.Party[0].HeldItem);
        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
        Assert.Equal(0, player.Bag.CountOf(TestRules.TrinketItem));
    }

    /// <summary>
    /// Handing over what they already hold is a swap with itself, and it used to be
    /// worth spelling out: written as a plain swap it takes one out of the bag, puts one
    /// back, and looks like it worked while nothing has happened.
    /// </summary>
    [Fact]
    public void GivingWhatTheyAlreadyHoldChangesNothing()
    {
        (GameWorld world, ServerPlayer player) = Standing(TestRules.TrinketItem);

        player.Bag.Add(TestRules.TrinketItem, 2);

        List<Outgoing> said = world.GiveItem(player.Id, TestRules.TrinketItem, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: "It's already carrying that." });
        Assert.Equal(2, player.Bag.CountOf(TestRules.TrinketItem));
        Assert.Equal(TestRules.TrinketItem, player.Party[0].HeldItem);
    }

    [Fact]
    public void ABicycleIsNotHandedToAnybody()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        player.Bag.Add(TestRules.BicycleItem, 1);

        List<Outgoing> said = world.GiveItem(player.Id, TestRules.BicycleItem, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: "That can't be carried." });
        Assert.Equal(0, player.Party[0].HeldItem);
        Assert.Equal(1, player.Bag.CountOf(TestRules.BicycleItem));
    }

    /// <summary>Nothing is handed over out of a bag that has none of it.</summary>
    [Fact]
    public void NothingIsGivenOutOfAnEmptyBag()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        Assert.Empty(world.GiveItem(player.Id, TestRules.TrinketItem, 0));
        Assert.Equal(0, player.Party[0].HeldItem);
    }

    [Fact]
    public void NothingIsGivenToASlotWithNobodyInIt()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        player.Bag.Add(TestRules.TrinketItem, 1);

        Assert.Empty(world.GiveItem(player.Id, TestRules.TrinketItem, 4));
        Assert.Equal(1, player.Bag.CountOf(TestRules.TrinketItem));
    }

    // ---- taking back -----------------------------------------------------------------

    [Fact]
    public void WhatWasGivenCanBeTakenBack()
    {
        (GameWorld world, ServerPlayer player) = Standing(TestRules.TrinketItem);

        List<Outgoing> said = world.TakeItem(player.Id, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: "Took it back." });
        Assert.Equal(0, player.Party[0].HeldItem);
        Assert.Equal(1, player.Bag.CountOf(TestRules.TrinketItem));
    }

    [Fact]
    public void NothingIsTakenFromEmptyHands()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        List<Outgoing> said = world.TakeItem(player.Id, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: "It isn't carrying anything." });
    }

    /// <summary>
    /// The request carries a slot and no item id, so there is nothing in it to get wrong
    /// — or to lie about. A client cannot be handed something nobody was carrying.
    /// </summary>
    [Fact]
    public void TakingBackWhatWasStolenGivesTheStolenThing()
    {
        (GameWorld world, ServerPlayer player) = Standing(TestRules.PotionItem);

        world.TakeItem(player.Id, 0);

        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
        Assert.Equal(0, player.Bag.CountOf(TestRules.TrinketItem));
    }

    // ---- and the round trip ----------------------------------------------------------

    /// <summary>
    /// Both requests are on the wire. A message without its own line on the base type
    /// serialises and cannot be read back, which is what caught RunAway missing one the
    /// day running away was written.
    /// </summary>
    [Fact]
    public void BothRequestsSurviveTheWire()
    {
        foreach (NetMessage message in new NetMessage[]
        {
            new GiveItemRequest(TestRules.TrinketItem, 2),
            new TakeItemRequest(2),
        })
        {
            string json = System.Text.Json.JsonSerializer.Serialize(message);

            Assert.Equal(message, System.Text.Json.JsonSerializer.Deserialize<NetMessage>(json));
        }
    }

    /// <summary>And what was handed over is still being carried tomorrow.</summary>
    [Fact]
    public async Task WhatWasHandedOverIsStillThereTomorrow()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        SavedCharacter character = SavedCharacter.Fresh("1.0", 1, 1) with
        {
            Party =
            [
                new SavedMon(1, 20, null, 30, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
                {
                    HeldItem = TestRules.TrinketItem,
                },
            ],
        };

        Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync("Mason", "a-good-password", character));

        var back = (AuthOutcome.Success)await store.LoginAsync("Mason", "a-good-password");

        Assert.Equal(TestRules.TrinketItem, back.Character.Party[0].HeldItem);
    }
}
