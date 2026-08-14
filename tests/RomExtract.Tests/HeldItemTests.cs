using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Something to carry.
/// <para>
/// Every trainer's party on the cartridge says what its members hold. This project has
/// been reading that number, writing it into the rules file and throwing it away at the
/// battle factory since trainers existed — eighty-seven of seventeen hundred and
/// fifty-four members, across seven different items.
/// </para>
/// <para>
/// What holding one <em>does</em> is another matter and mostly not answerable: an item's
/// record carries a hold effect as a number and what each number means is in the game's
/// code. So this is about the item, not its effect — which is exactly enough for the one
/// group in the move table that is about a held item rather than about holding it.
/// </para>
/// </summary>
public class HeldItemTests
{
    private const byte StealEffect = 0x69;

    private const int Thief = 1;
    private const int Plain = 2;
    private const int Trinket = 207;
    private const int SomethingElse = 142;

    /// <summary>
    /// A hundred for the secondary chance, because that is what THIEF's record says —
    /// and on a move that inflicts no condition, a hundred is the record saying "this
    /// always does its other thing". The same marker the trapping group carries.
    /// </summary>
    private static MoveData Move(int id, byte effect, byte power) =>
        new(id, "", effect, power, PokemonType.Normal, 100, 20, 100, 0, 0);

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var species = new SpeciesData
        {
            Index = 1,
            BaseHp = 200,
            BaseAttack = 20,
            BaseDefense = 200,
            BaseSpeed = (byte)speed,
            BaseSpAttack = 20,
            BaseSpDefense = 200,
            Type1 = PokemonType.Normal,
            Type2 = PokemonType.Normal,
            GrowthRate = GrowthRate.MediumFast,
        };

        var battler = new Battler(species, 50, Nature.Hardy);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Fact]
    public void TheStealingGroupIsRead()
    {
        Assert.Equal(EffectKind.Steal, MoveEffects.Of(StealEffect).Kind);
    }

    [Fact]
    public void TakingSomethingOffSomebodyWhoIsCarryingIt()
    {
        Battler you = Make(250, Move(Thief, StealEffect, 40));
        Battler them = Make(1, Move(Plain, 0, 10));

        them.Holding = Trinket;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.Stole { Side: Side.Player, ItemId: Trinket });
        Assert.Equal(Trinket, you.Holding);
        Assert.Equal(0, them.Holding);
    }

    /// <summary>
    /// Only with empty hands. The games' rule, and also the only one that does not need
    /// somewhere to put a second item.
    /// </summary>
    [Fact]
    public void SomebodyAlreadyCarryingSomethingTakesNothing()
    {
        Battler you = Make(250, Move(Thief, StealEffect, 40));
        Battler them = Make(1, Move(Plain, 0, 10));

        you.Holding = SomethingElse;
        them.Holding = Trinket;

        Assert.Contains(Turn(new Battle(you, them, 7)), e => e is BattleEvent.NothingHappened);

        Assert.Equal(SomethingElse, you.Holding);
        Assert.Equal(Trinket, them.Holding);
    }

    [Fact]
    public void ThereIsNothingToTakeFromEmptyHands()
    {
        Battler you = Make(250, Move(Thief, StealEffect, 40));
        Battler them = Make(1, Move(Plain, 0, 10));

        Assert.Contains(Turn(new Battle(you, them, 7)), e => e is BattleEvent.NothingHappened);
        Assert.Equal(0, you.Holding);
    }

    /// <summary>
    /// It still has to land. A move that steals on a miss would be a move that never
    /// misses, which is a different field entirely.
    /// </summary>
    [Fact]
    public void NothingIsTakenByAMoveThatMissed()
    {
        Battler you = Make(250, new MoveData(Thief, "", StealEffect, 40, PokemonType.Normal, 1, 20, 100, 0, 0));
        Battler them = Make(1, Move(Plain, 0, 10));

        them.Holding = Trinket;

        List<BattleEvent> events = Turn(new Battle(you, them, 3));

        Assert.Contains(events, e => e is BattleEvent.MoveMissed { Side: Side.Player });
        Assert.Equal(0, you.Holding);
        Assert.Equal(Trinket, them.Holding);
    }

    // ---- where it comes from, and where it goes ------------------------------------

    /// <summary>
    /// The number the rules file has always carried. A trainer's party member is built
    /// holding what the cartridge says it holds.
    /// </summary>
    [Fact]
    public void ATrainersPartyCarriesWhatTheRulesSay()
    {
        var factory = new BattleFactory(TestRules.All);

        List<Battler> party = factory.TrainerParty(TestRules.Carrying);

        Assert.NotEmpty(party);
        Assert.Equal(Trinket, party[0].Holding);
    }

    [Fact]
    public void WhatABattlerCarriesSurvivesBeingWrittenDown()
    {
        var battler = Make(50, Move(Plain, 0, 10));

        battler.Holding = Trinket;

        SavedMon saved = BattleFactory.Save(battler);

        Assert.Equal(Trinket, saved.HeldItem);

        Battler again = new BattleFactory(TestRules.All).Restore(saved)!;

        Assert.Equal(Trinket, again.Holding);
    }

    /// <summary>
    /// And across a sign-out, which is the half that makes THIEF worth using. The column
    /// is added to an existing database rather than only to a fresh one, because a schema
    /// that is right on a new machine and wrong on every machine that has been playing is
    /// the worst of both.
    /// </summary>
    [Fact]
    public async Task WhatWasTakenIsStillThereTomorrow()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        SavedCharacter character = SavedCharacter.Fresh("1.0", 1, 1) with
        {
            Party =
            [
                new SavedMon(1, 20, null, 30, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
                {
                    HeldItem = Trinket,
                },
            ],
        };

        Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync("Mason", "a-good-password", character));

        var back = (AuthOutcome.Success)await store.LoginAsync("Mason", "a-good-password");

        Assert.Equal(Trinket, back.Character.Party[0].HeldItem);
    }
}
