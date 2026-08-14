using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Items;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Which condition each medicine clears.
/// <para>
/// <see cref="ItemData"/> has carried a note about this since potions worked: an
/// Antidote and a Full Heal have zero in every field of their own record, so which
/// condition each one clears really does live somewhere else. They also run the same
/// field routine as one another, so even the routine that was enough to pick the stones
/// out of three hundred items cannot tell them apart.
/// </para>
/// <para>
/// It is a second table of short arrays, and a scan for runs of valid pointers finds
/// tens of thousands. What finds it is a pattern that can only be one thing: five items
/// each claiming a single distinct bit of one column, and the item that clears
/// everything holding all five at once. On a real FireRed that happens once.
/// </para>
/// </summary>
public class CureTests
{
    private const int Antidote = 14;
    private const int BurnHeal = 15;
    private const int IceHeal = 16;
    private const int Awakening = 17;
    private const int ParlyzHeal = 18;
    private const int FullHeal = 23;
    private const int Flute = 24;

    private const int Column = 3;
    private const int ArrayLength = 8;

    /// <summary>Where each item's short array is planted, so a pointer has somewhere to go.</summary>
    private static int Effect(int id) => 0x800 + id * ArrayLength;

    private static readonly (int Id, string Name)[] Named =
    [
        (Antidote, "ANTIDOTE"),
        (BurnHeal, "BURN HEAL"),
        (IceHeal, "ICE HEAL"),
        (Awakening, "AWAKENING"),
        (ParlyzHeal, "PARLYZ HEAL"),
        (FullHeal, "FULL HEAL"),
        (Flute, "YELLOW FLUTE"),
    ];

    private static List<ItemRecord> Items() =>
    [
        .. Enumerable.Range(0, 40).Select(id => new ItemRecord(
            id,
            0,
            Named.FirstOrDefault(n => n.Id == id).Name ?? $"THING {id}",
            100,
            Pocket.Items,
            0, 0, 0, 1, 0))
    ];

    /// <summary>
    /// An image with the table in it. The bits are deliberately not in the order the
    /// anchors are listed in, so a locator that assumed one would be caught.
    /// </summary>
    private static byte[] Image(int table = 0x400, int column = Column, int fullHeal = 0x3F, int flute = 0x01)
    {
        var image = new byte[0x2000];

        void Pointer(int id)
        {
            int at = table + 4 * (id - Antidote);
            uint address = Rom.BaseAddress + (uint)Effect(id);

            for (int b = 0; b < 4; b++) image[at + b] = (byte)(address >> (b * 8));
        }

        // Everything from the first anchor to the last gets an entry, so the run reads
        // as a run.
        for (int id = Antidote; id <= Flute; id++) Pointer(id);

        image[Effect(Antidote) + column] = 0x10;
        image[Effect(BurnHeal) + column] = 0x08;
        image[Effect(IceHeal) + column] = 0x04;
        image[Effect(Awakening) + column] = 0x20;
        image[Effect(ParlyzHeal) + column] = 0x02;
        image[Effect(FullHeal) + column] = (byte)fullHeal;
        image[Effect(Flute) + column] = (byte)flute;

        return image;
    }

    private static CureTable? Locate(byte[] image) =>
        ItemEffects.Locate(new Rom(image), Items());

    [Fact]
    public void TheColumnIsFoundByFiveNamesAndOneThatHasThemAll()
    {
        CureTable? found = Locate(Image());

        Assert.NotNull(found);
        Assert.Equal(Column, found.Column);
    }

    /// <summary>
    /// And each bit means what the item named for it means. This is the one derivation
    /// in the project that needs the cartridge's own words, and it is the same allowance
    /// the ball kinds have: names stop at the exporter and a number crosses the line.
    /// </summary>
    [Fact]
    public void EachBitMeansWhatItsItemIsNamedFor()
    {
        CureTable found = Locate(Image())!;

        Assert.Equal(Ailments.Poison, found.Cures[Antidote]);
        Assert.Equal(Ailments.Burn, found.Cures[BurnHeal]);
        Assert.Equal(Ailments.Freeze, found.Cures[IceHeal]);
        Assert.Equal(Ailments.Sleep, found.Cures[Awakening]);
        Assert.Equal(Ailments.Paralysis, found.Cures[ParlyzHeal]);
    }

    /// <summary>
    /// The sixth. Five bits are named, the union has six, and this project models six
    /// things that can be wrong with a creature — so one bit is left and one thing is
    /// left, and they are each other.
    /// </summary>
    [Fact]
    public void TheLeftoverBitIsTheLeftoverThing()
    {
        CureTable found = Locate(Image())!;

        Assert.Equal(Ailments.Everything, found.Cures[FullHeal]);
        Assert.Equal(Ailments.Confusion, found.Cures[Flute]);
    }

    /// <summary>
    /// What rules out the leftover being a marker rather than an ailment is that one
    /// item sets it and nothing else. A "clears everything" flag would never appear
    /// alone, so without such an item the bit is left unnamed rather than guessed at.
    /// </summary>
    [Fact]
    public void ALeftoverBitThatNeverStandsAloneIsNotNamed()
    {
        CureTable found = Locate(Image(flute: 0x21))!;

        Assert.Equal(Ailments.Everything & ~Ailments.Confusion, found.Cures[FullHeal]);
        Assert.Equal(Ailments.Sleep, found.Cures[Flute]);
    }

    /// <summary>
    /// An item claiming two bits is not an anchor. The pattern is five <em>single</em>
    /// bits, because an item that clears two things cannot say which bit is which.
    /// </summary>
    [Fact]
    public void AnAnchorThatClearsTwoThingsIsNotAnAnchor()
    {
        var image = Image();
        image[Effect(Antidote) + Column] = 0x18;

        Assert.Null(Locate(image));
    }

    /// <summary>And the one that clears everything really has to.</summary>
    [Fact]
    public void TheOneThatClearsEverythingHasToClearEverything()
    {
        Assert.Null(Locate(Image(fullHeal: 0x1F)));
    }

    [Fact]
    public void TwoAnchorsSharingABitIsNoPattern()
    {
        var image = Image();
        image[Effect(BurnHeal) + Column] = 0x10;

        Assert.Null(Locate(image));
    }

    /// <summary>
    /// A cartridge that does not name the six is a cartridge this cannot be read off,
    /// and it says so rather than finding something.
    /// </summary>
    [Fact]
    public void WithoutTheNamesThereIsNoAnchor()
    {
        List<ItemRecord> nameless =
            [.. Items().Select(i => i with { Name = $"THING {i.Id}" })];

        Assert.Null(ItemEffects.Locate(new Rom(Image()), nameless));
    }

    // ---- what it does out of a fight -------------------------------------------------

    private static (GameWorld World, ServerPlayer Player) Standing(StatusCondition wrong, int hp = 20)
    {
        const string town = "1.0";

        MapData map = new(town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map]), town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(town, 3, 4));

        player.Party = [new SavedMon(3, 10, null, hp, wrong, Nature.Hardy, [TestRules.FirstMove])];

        return (world, player);
    }

    [Fact]
    public void SomethingPoisonedStopsBeingPoisoned()
    {
        (GameWorld world, ServerPlayer player) = Standing(StatusCondition.Poison);

        player.Bag.Add(TestRules.AntidoteItem, 1);

        List<Outgoing> said = world.UseItem(player.Id, TestRules.AntidoteItem, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: "The poison is gone." });
        Assert.Equal(StatusCondition.None, player.Party[0].Status);
        Assert.Equal(0, player.Bag.CountOf(TestRules.AntidoteItem));
    }

    /// <summary>
    /// And nothing else. An Antidote used on something asleep is a wasted Antidote in
    /// every one of these games, and this refuses it rather than spending it.
    /// </summary>
    [Fact]
    public void AnAntidoteDoesNothingForSleep()
    {
        (GameWorld world, ServerPlayer player) = Standing(StatusCondition.Sleep);

        player.Bag.Add(TestRules.AntidoteItem, 1);

        List<Outgoing> said = world.UseItem(player.Id, TestRules.AntidoteItem, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: "It won't have any effect." });
        Assert.Equal(StatusCondition.Sleep, player.Party[0].Status);
        Assert.Equal(1, player.Bag.CountOf(TestRules.AntidoteItem));
    }

    [Fact]
    public void TheOneThatClearsEverythingClearsWhicheverItIs()
    {
        foreach (StatusCondition wrong in new[]
        {
            StatusCondition.Poison, StatusCondition.Burn, StatusCondition.Paralysis,
            StatusCondition.Sleep, StatusCondition.Freeze,
        })
        {
            (GameWorld world, ServerPlayer player) = Standing(wrong);

            player.Bag.Add(TestRules.FullHealItem, 1);
            world.UseItem(player.Id, TestRules.FullHealItem, 0);

            Assert.Equal(StatusCondition.None, player.Party[0].Status);
        }
    }

    /// <summary>
    /// One item does both, and neither half may be the gate on the other. Written with
    /// the restore as the gate, a Full Restore on somebody at full health and poisoned
    /// says "it won't have any effect" and leaves them poisoned.
    /// </summary>
    [Fact]
    public void SomethingAtFullHealthIsStillCured()
    {
        (GameWorld world, ServerPlayer player) = Standing(StatusCondition.Burn, hp: 999);

        player.Bag.Add(TestRules.FullRestoreItem, 1);

        List<Outgoing> said = world.UseItem(player.Id, TestRules.FullRestoreItem, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: "The burn is gone." });
        Assert.Equal(StatusCondition.None, player.Party[0].Status);
    }

    [Fact]
    public void SomethingHurtAndBurnedGetsBoth()
    {
        (GameWorld world, ServerPlayer player) = Standing(StatusCondition.Burn, hp: 1);

        player.Bag.Add(TestRules.FullRestoreItem, 1);

        List<Outgoing> said = world.UseItem(player.Id, TestRules.FullRestoreItem, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: not null } bag
            && bag.Message.Contains("Restored") && bag.Message.Contains("the burn is gone"));

        Assert.Equal(StatusCondition.None, player.Party[0].Status);
    }

    // ---- and inside one --------------------------------------------------------------

    private static Battler Make(StatusCondition wrong)
    {
        var species = new SpeciesData
        {
            Index = 1,
            BaseHp = 120, BaseAttack = 50, BaseDefense = 50,
            BaseSpeed = 50, BaseSpAttack = 50, BaseSpDefense = 50,
            Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
            GrowthRate = GrowthRate.MediumFast,
        };

        var battler = new Battler(species, 50, Nature.Hardy);

        battler.Moves.Add(new MoveData(1, "", 0, 40, PokemonType.Normal, 100, 20, 0, 0, 0));
        battler.Status = wrong;

        return battler;
    }

    [Fact]
    public void ACureWorksInAFightToo()
    {
        Battler you = Make(StatusCondition.Poison);
        Battler them = Make(StatusCondition.None);

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseItem(TestRules.AntidoteItem) { Cures = Ailments.Poison },
            new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.PutRight { Cleared: Ailments.Poison });
        Assert.Equal(StatusCondition.None, you.Status);
    }

    /// <summary>
    /// Confusion is the sixth thing and is not a condition — it runs alongside one — so
    /// it is cleared separately and only inside a fight, which is the only place it
    /// exists.
    /// </summary>
    [Fact]
    public void TheSixthThingIsClearedToo()
    {
        Battler you = Make(StatusCondition.None);
        Battler them = Make(StatusCondition.None);

        you.ConfusedTurns = 3;

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseItem(TestRules.FullHealItem) { Cures = Ailments.Everything },
            new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.PutRight { Cleared: Ailments.Confusion });
        Assert.False(you.IsConfused);
    }

    /// <summary>
    /// Reaching into a bag is not the creature moving, so sleep cannot stop it.
    /// <para>
    /// Found by writing the test above. The turn used to begin by asking whether this
    /// one could act at all, which made a Full Heal useless on the only thing it is for:
    /// the check ran first, the creature slept through its own cure, and the item was
    /// spent on nothing. The same was true of a ball and of the door.
    /// </para>
    /// </summary>
    [Fact]
    public void SleepDoesNotStopSomebodyReachingIntoABag()
    {
        Battler you = Make(StatusCondition.Sleep);
        Battler them = Make(StatusCondition.None);

        you.SleepTurns = 3;

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(
            new BattleAction.UseItem(TestRules.FullHealItem) { Cures = Ailments.Everything },
            new BattleAction.UseMove(0));

        Assert.Equal(StatusCondition.None, you.Status);

        // And the counter did not come down, because in these games it comes down when
        // the creature tries to move and this turn it never tried.
        Assert.Equal(3, you.SleepTurns);
    }

    /// <summary>
    /// A cure that restores nothing is not a wasted turn, and used to look like one:
    /// the health event says "restored zero", which reads as "it would have no effect".
    /// The two are separate events for exactly that reason.
    /// </summary>
    [Fact]
    public void ACureThatRestoresNothingStillSaysSomething()
    {
        Battler you = Make(StatusCondition.Sleep);
        Battler them = Make(StatusCondition.None);

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseItem(TestRules.FullHealItem) { Cures = Ailments.Everything, Restores = 0 },
            new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.PutRight);
        Assert.Equal(StatusCondition.None, you.Status);
    }

    // ---- and across the file ---------------------------------------------------------

    [Fact]
    public void WhatItClearsSurvivesBeingWrittenDown()
    {
        using var buffer = new MemoryStream();

        TestRules.All.Save(buffer);
        buffer.Position = 0;

        GameRules again = GameRules.Load(buffer);

        Assert.Equal(Ailments.Poison, again.ItemAt(TestRules.AntidoteItem)!.Cures);
        Assert.Equal(Ailments.Everything, again.ItemAt(TestRules.FullHealItem)!.Cures);
        Assert.Equal(Ailments.None, again.ItemAt(TestRules.PotionItem)!.Cures);
    }
}
