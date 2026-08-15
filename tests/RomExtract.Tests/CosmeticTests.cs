using PokeMmo.Core.Cosmetics;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a character looks like, and who decides.
/// <para>
/// The first invented content in this project. Every other number in this codebase is either
/// read off a cartridge and evidenced, or modelled with the argument written beside it —
/// there is a whole milestone about the difference. A hat is neither, and the namespace it
/// lives in says so, because the first made-up constant that is not clearly labelled is the
/// one that teaches everybody that made-up constants are fine.
/// </para>
/// <para>
/// The rules worth testing are the ones a wardrobe screen would otherwise own privately: what
/// a garment takes the place of, and whether you are allowed to wear it at all.
/// </para>
/// </summary>
public class CosmeticTests
{
    private const string Town = "1.0";

    private const int Cap = 301;
    private const int Shirt = 601;
    private const int Jeans = 701;
    private const int Skirt = 801;
    private const int Dress = 901;

    private static (GameWorld World, ServerPlayer Player) Standing()
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));

        return (world, player);
    }

    // ---- what goes where ----------------------------------------------------------

    [Fact]
    public void EverythingInTheWardrobeKnowsItsOwnSlot()
    {
        Assert.All(Wardrobe.All, c => Assert.Equal(c, Wardrobe.At(c.Id)));
        Assert.Null(Wardrobe.At(0));
    }

    /// <summary>
    /// A dress is not a shirt and a pair of trousers at once, it is instead of them. The
    /// rule lives on the appearance rather than in whatever screen happens to be open.
    /// </summary>
    [Fact]
    public void ADressTakesOffTheShirtTheTrousersAndTheSkirt()
    {
        Appearance dressed = Appearance.Bare
            .Wearing(Wardrobe.At(Shirt)!)
            .Wearing(Wardrobe.At(Jeans)!)
            .Wearing(Wardrobe.At(Dress)!);

        Assert.Equal(Dress, dressed.In(CosmeticSlot.Dress));
        Assert.Equal(0, dressed.In(CosmeticSlot.Shirt));
        Assert.Equal(0, dressed.In(CosmeticSlot.Pants));
        Assert.Equal(0, dressed.In(CosmeticSlot.Skirt));
    }

    /// <summary>And trousers and a skirt are alternatives to each other, both ways round.</summary>
    [Theory]
    [InlineData(Jeans, Skirt)]
    [InlineData(Skirt, Jeans)]
    public void TrousersAndASkirtReplaceEachOther(int first, int second)
    {
        Appearance worn = Appearance.Bare.Wearing(Wardrobe.At(first)!).Wearing(Wardrobe.At(second)!);

        Assert.Equal(1, worn.Worn.Count);
        Assert.Equal(second, worn.In(Wardrobe.At(second)!.Slot));
    }

    /// <summary>A hat has nothing to do with any of it.</summary>
    [Fact]
    public void AHatDoesNotTakeAnythingElseOff()
    {
        Appearance worn = Appearance.Bare.Wearing(Wardrobe.At(Shirt)!).Wearing(Wardrobe.At(Cap)!);

        Assert.Equal(Shirt, worn.In(CosmeticSlot.Shirt));
        Assert.Equal(Cap, worn.In(CosmeticSlot.Hat));
    }

    /// <summary>
    /// Drawing order is fixed rather than whatever a dictionary offers, because a renderer
    /// that iterated the worn set would put a hat under a shirt one run in ten.
    /// </summary>
    [Fact]
    public void TheDrawingOrderPutsAHatOverAShirt()
    {
        Appearance worn = Appearance.Bare.Wearing(Wardrobe.At(Cap)!).Wearing(Wardrobe.At(Shirt)!);

        List<CosmeticSlot> order = [.. worn.InDrawingOrder().Select(w => w.Slot)];

        Assert.True(order.IndexOf(CosmeticSlot.Shirt) < order.IndexOf(CosmeticSlot.Hat));
    }

    // ---- who decides --------------------------------------------------------------

    /// <summary>
    /// The whole of the commercial half. A client that decided what it was wearing would be
    /// a client that wears whatever it has been edited to wear, and the point of a thing
    /// being sold is that not everybody has it.
    /// </summary>
    [Fact]
    public void SomethingNobodyOwnsIsRefused()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        Assert.Empty(world.Wear(player.Id, Cap, CosmeticSlot.Hat));
        Assert.Contains("does not own", world.LastWorn ?? "");
        Assert.Equal(0, player.Looks.In(CosmeticSlot.Hat));
    }

    [Fact]
    public void SomethingOwnedGoesOnAndIsToldToTheMap()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        player.Owns.Add(Cap);

        List<Outgoing> shown = world.Wear(player.Id, Cap, CosmeticSlot.Hat);

        Assert.Equal(Cap, player.Looks.In(CosmeticSlot.Hat));
        Assert.Contains(shown.Select(o => o.Message).OfType<AppearanceChanged>(), a => a.PlayerId == player.Id);
        Assert.All(shown, o => Assert.Equal(Town, o.OnMap));
    }

    /// <summary>
    /// Taking something off needs no permission. There is no version of this game where a
    /// player cannot remove their own hat, whatever they did or did not pay for it.
    /// </summary>
    [Fact]
    public void TakingSomethingOffNeedsNothing()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        player.Owns.Add(Cap);
        world.Wear(player.Id, Cap, CosmeticSlot.Hat);

        world.Wear(player.Id, 0, CosmeticSlot.Hat);

        Assert.Equal(0, player.Looks.In(CosmeticSlot.Hat));
    }

    /// <summary>A number this game has never heard of is refused rather than worn.</summary>
    [Fact]
    public void ANumberThatIsNotACosmeticIsRefused()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        Assert.Empty(world.Wear(player.Id, 99999, CosmeticSlot.Hat));
        Assert.Contains("no cosmetic", world.LastWorn ?? "");
    }

    /// <summary>
    /// Everybody owns the plain end of each slot without buying anything. A character with
    /// no shirt is not a character, and a shop that accidentally charged for the only shirt
    /// would be a shop nobody could get dressed in.
    /// </summary>
    [Fact]
    public void ThePlainEndOfEachSlotIsFree()
    {
        (GameWorld _, ServerPlayer player) = Standing();

        Assert.All(Wardrobe.FreeToEverybody, id => Assert.Contains(id, player.Owns));
        Assert.All(Wardrobe.FreeToEverybody, id => Assert.NotNull(Wardrobe.At(id)));
    }

    // ---- and it lasts -------------------------------------------------------------

    /// <summary>
    /// What is owned and what is on both survive a sign-out, which is the difference between
    /// a cosmetic and a party trick.
    /// </summary>
    [Fact]
    public async Task WhatIsOwnedAndWornOutlivesTheConnection()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        SavedCharacter fresh = SavedCharacter.Fresh(Town, 1, 1);

        var registered = (AuthOutcome.Success)await store.RegisterAsync("Mason", "a-good-password", fresh);

        await store.SaveAsync(registered.Account.Id, fresh with
        {
            Cosmetics = [Cap, Dress],
            Looks = Appearance.Bare.Wearing(Wardrobe.At(Dress)!).Wearing(Wardrobe.At(Cap)!),
        });

        var back = (AuthOutcome.Success)await store.LoginAsync("Mason", "a-good-password");

        Assert.Equal([Cap, Dress], back.Character.Cosmetics.Order());
        Assert.Equal(Dress, back.Character.Looks.In(CosmeticSlot.Dress));
        Assert.Equal(Cap, back.Character.Looks.In(CosmeticSlot.Hat));
    }

    /// <summary>And a player arrives wearing it, which is what everyone else is told.</summary>
    [Fact]
    public void SomebodyArrivesWearingWhatTheySavedIn()
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4) with
        {
            Cosmetics = [Cap],
            Looks = Appearance.Bare.Wearing(Wardrobe.At(Cap)!),
        });

        Assert.Equal(Cap, player.ToAppeared().Looks.In(CosmeticSlot.Hat));
    }
}
