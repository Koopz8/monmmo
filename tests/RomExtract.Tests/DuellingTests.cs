using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Two people fighting each other — the third verb this game's multiplayer has.
/// <para>
/// The first was seeing each other, the second was swapping one thing each, and this is
/// the one everything else was built for. The engine underneath is the engine that has
/// fought every wild encounter and every trainer in this project, unchanged: what is new
/// is that neither of its two sides is "you".
/// </para>
/// <para>
/// The rules here are decisions rather than readings, because the cartridge has no rule
/// for this — it has no second player. They are all in one direction: <em>a duel costs
/// nothing.</em> No experience, no money, no black-out, no catching somebody else's
/// creature, and nothing written back to either party. A fight that could cost you your
/// afternoon's work is a fight nobody would agree to twice, and a multiplayer verb nobody
/// uses twice is not a verb.
/// </para>
/// </summary>
public class DuellingTests
{
    private const string Town = "1.0";

    private static SavedMon Member(int species, int level = 20) =>
        new(species, level, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

    /// <summary>Two players facing each other, a square apart, each with a party.</summary>
    private static (GameWorld World, ServerPlayer One, ServerPlayer Two) Facing(int each = 2)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]);
        MapData elsewhere = new("1.1", "VIRIDIAN CITY", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map, elsewhere]), Town, TestRules.All);

        world.Operators.Add("Mason");

        (ServerPlayer one, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));
        (ServerPlayer two, _) = world.Join(2, "Koop", SavedCharacter.Fresh(Town, 3, 3));

        one.Square = new GridPosition(3, 4);
        two.Square = new GridPosition(3, 3);
        one.Facing = Direction.Up;
        two.Facing = Direction.Down;

        one.Party = [.. Enumerable.Range(1, each).Select(s => Member(s))];
        two.Party = [.. Enumerable.Range(30, each).Select(s => Member(s))];

        return (world, one, two);
    }

    private static List<Outgoing> Fighting(GameWorld world, ServerPlayer one, ServerPlayer two)
    {
        world.AskToDuel(one.Id, two.Id);
        return world.AskToDuel(two.Id, one.Id);
    }

    /// <summary>Two requests pointing at each other, and nothing else.</summary>
    [Fact]
    public void AskingBackIsAgreeing()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Assert.Equal(0, world.OpenDuels);

        world.AskToDuel(one.Id, two.Id);

        Assert.Equal(0, world.OpenDuels);

        world.AskToDuel(two.Id, one.Id);

        Assert.Equal(1, world.OpenDuels);
    }

    [Fact]
    public void AndTheOtherOneIsToldTheyWereAsked()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        List<Outgoing> said = world.AskToDuel(one.Id, two.Id);

        Assert.Contains(
            said.Where(o => o.OnlyTo == two.Id).Select(o => o.Message).OfType<DuelAsked>(),
            a => a.FromPlayerId == one.Id && a.FromName == "Mason");
    }

    /// <summary>Both are told a battle started, each seeing their own side as theirs.</summary>
    [Fact]
    public void BothSidesAreSentIntoTheSameFight()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        List<Outgoing> said = Fighting(world, one, two);

        BattleStarted? mine = said.Where(o => o.OnlyTo == one.Id)
            .Select(o => o.Message).OfType<BattleStarted>().FirstOrDefault();

        BattleStarted? theirs = said.Where(o => o.OnlyTo == two.Id)
            .Select(o => o.Message).OfType<BattleStarted>().FirstOrDefault();

        Assert.NotNull(mine);
        Assert.NotNull(theirs);

        Assert.Equal(mine.You.Species, theirs.Opponent.Species);
        Assert.Equal(mine.Opponent.Species, theirs.You.Species);
    }

    /// <summary>And no balls and no medicine, because neither is offered in a duel.</summary>
    [Fact]
    public void WithNothingToThrowAndNothingToDrink()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        List<Outgoing> said = Fighting(world, one, two);

        Assert.All(
            said.Select(o => o.Message).OfType<BattleStarted>(),
            started =>
            {
                Assert.Empty(started.Balls);
                Assert.Empty(started.Medicine);
            });
    }

    /// <summary>Nothing happens until both have decided.</summary>
    [Fact]
    public void ATurnWaitsForBothOfThem()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Fighting(world, one, two);

        Assert.Empty(world.TakeBattleTurn(one.Id, new BattleAction.UseMove(0)));

        Assert.NotEmpty(world.TakeBattleTurn(two.Id, new BattleAction.UseMove(0)));
    }

    /// <summary>And both are told what happened, each from their own chair.</summary>
    [Fact]
    public void AndBothAreToldAboutItFromTheirOwnChair()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Fighting(world, one, two);

        world.TakeBattleTurn(one.Id, new BattleAction.UseMove(0));

        List<Outgoing> said = world.TakeBattleTurn(two.Id, new BattleAction.UseMove(0));

        BattleUpdate mine = said.Where(o => o.OnlyTo == one.Id)
            .Select(o => o.Message).OfType<BattleUpdate>().Single();

        BattleUpdate theirs = said.Where(o => o.OnlyTo == two.Id)
            .Select(o => o.Message).OfType<BattleUpdate>().Single();

        Assert.Equal(mine.YourHp, theirs.OpponentHp);
        Assert.Equal(mine.OpponentHp, theirs.YourHp);

        // The same events, side for side. A move one of them used is a move the other one
        // was hit by, and it is one turn.
        Assert.Equal(mine.Events.Count, theirs.Events.Count);

        Assert.Equal(
            mine.Events.OfType<BattleEvent.MoveUsed>().Select(m => m.Side),
            theirs.Events.OfType<BattleEvent.MoveUsed>().Select(m => m.Side.Other()));
    }

    /// <summary>Fought to the end, somebody wins and the other is told they lost.</summary>
    [Fact]
    public void SomebodyWinsAndBothAreTold()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing(each: 1);

        // A hopeless mismatch, so it finishes: one side at level 100, the other at 2.
        one.Party = [Member(1, 100)];
        two.Party = [Member(30, 2)];

        Fighting(world, one, two);

        BattleFinished? finish = null;

        for (int turn = 0; turn < 40 && finish is null; turn++)
        {
            world.TakeBattleTurn(one.Id, new BattleAction.UseMove(0));

            finish = world.TakeBattleTurn(two.Id, new BattleAction.UseMove(0))
                .Where(o => o.OnlyTo == one.Id)
                .Select(o => o.Message)
                .OfType<BattleFinished>()
                .FirstOrDefault();
        }

        Assert.NotNull(finish);
        Assert.Equal(Side.Player, finish.Winner);
        Assert.Equal(0, world.OpenDuels);
    }

    /// <summary>
    /// And it cost nothing. The decision, stated as a test: both parties come out of a
    /// duel exactly as they went in, whoever won.
    /// </summary>
    [Fact]
    public void AndNeitherPartyIsAnyTheWorseForIt()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing(each: 1);

        one.Party = [Member(1, 100)];
        two.Party = [Member(30, 2)];

        List<SavedMon> before = [.. two.Party];
        int money = two.Money;
        string where = two.MapId;

        Fighting(world, one, two);

        for (int turn = 0; turn < 40 && world.OpenDuels > 0; turn++)
        {
            world.TakeBattleTurn(one.Id, new BattleAction.UseMove(0));
            world.TakeBattleTurn(two.Id, new BattleAction.UseMove(0));
        }

        Assert.Equal(0, world.OpenDuels);
        Assert.Equal(before, two.Party);
        Assert.Equal(money, two.Money);
        Assert.Equal(where, two.MapId);
    }

    /// <summary>Nobody fights across a room, by the same reach a trade uses.</summary>
    [Fact]
    public void NobodyFightsAcrossARoom()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        two.Square = new GridPosition(7, 7);

        world.AskToDuel(one.Id, two.Id);

        Assert.Contains("within reach", world.LastDuel);
    }

    [Fact]
    public void AndNobodyFightsThemselves()
    {
        (GameWorld world, ServerPlayer one, _) = Facing();

        world.AskToDuel(one.Id, one.Id);

        Assert.Contains("themselves", world.LastDuel);
        Assert.Equal(0, world.OpenDuels);
    }

    [Fact]
    public void AndNobodyWithNothingToSendOut()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        two.Party = [];

        world.AskToDuel(one.Id, two.Id);

        Assert.Contains("nobody to send out", world.LastDuel);
    }

    /// <summary>
    /// A fainted party can still duel. The copies come out on their feet, which follows
    /// from the fight being fought with copies at all — and means the answer to "shall we"
    /// is never "let me walk to a bed first".
    /// </summary>
    [Fact]
    public void ButAFaintedPartyStillCan()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        two.Party = [Member(30) with { CurrentHp = 0 }];

        List<Outgoing> said = Fighting(world, one, two);

        Assert.Equal(1, world.OpenDuels);

        // And the copy is on its feet, which is what "fought with copies" is worth.
        BattleStarted started = said.Where(o => o.OnlyTo == two.Id)
            .Select(o => o.Message).OfType<BattleStarted>().Single();

        Assert.Equal(started.You.MaxHp, started.You.CurrentHp);
    }

    /// <summary>Walking off the map ends it, and both are told.</summary>
    [Fact]
    public void WalkingOffEndsIt()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Fighting(world, one, two);

        List<Outgoing> said = world.RunConsole(one.Id, "/tp 1.1 3 3");

        Assert.Equal(0, world.OpenDuels);
        Assert.Contains(said.Where(o => o.OnlyTo == two.Id).Select(o => o.Message), m => m is BattleFinished);
    }

    [Fact]
    public void AndSoDoesLeaving()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Fighting(world, one, two);

        world.Leave(two.Id);

        Assert.Equal(0, world.OpenDuels);
    }

    /// <summary>
    /// Switching costs the turn and nothing else: the one who comes out arrives to
    /// whatever the other side had already decided to do.
    /// </summary>
    [Fact]
    public void SwitchingSendsSomebodyElseOut()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        List<Outgoing> opened = Fighting(world, one, two);

        int before = opened.Where(o => o.OnlyTo == one.Id)
            .Select(o => o.Message).OfType<BattleStarted>().Single().You.Species;

        world.TakeBattleTurn(one.Id, new BattleAction.SwitchTo(1));

        List<Outgoing> said = world.TakeBattleTurn(two.Id, new BattleAction.UseMove(0));

        BattlerSentOut sent = said.Where(o => o.OnlyTo == one.Id)
            .Select(o => o.Message).OfType<BattlerSentOut>().Single();

        Assert.Equal(Side.Player, sent.Side);
        Assert.NotEqual(before, sent.Battler.Species);
    }

    /// <summary>And the other side is told who came out, on their side of the screen.</summary>
    [Fact]
    public void AndTheOtherSideIsToldWhoCameOut()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Fighting(world, one, two);

        world.TakeBattleTurn(one.Id, new BattleAction.SwitchTo(1));

        List<Outgoing> said = world.TakeBattleTurn(two.Id, new BattleAction.UseMove(0));

        BattlerSentOut seen = said.Where(o => o.OnlyTo == two.Id)
            .Select(o => o.Message).OfType<BattlerSentOut>().Single();

        Assert.Equal(Side.Opponent, seen.Side);
    }

    /// <summary>
    /// A switch nobody could make becomes a move rather than a refusal, because a turn
    /// left waiting on a decision the player believes they have made is a duel that hangs.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    public void ASwitchNobodyCouldMakeIsJustATurn(int slot)
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Fighting(world, one, two);

        world.TakeBattleTurn(one.Id, new BattleAction.SwitchTo(slot));

        List<Outgoing> said = world.TakeBattleTurn(two.Id, new BattleAction.UseMove(0));

        Assert.Empty(said.Select(o => o.Message).OfType<BattlerSentOut>());
        Assert.NotEmpty(said.Select(o => o.Message).OfType<BattleUpdate>());
    }

    /// <summary>One fight at a time, so a challenge cannot interrupt one.</summary>
    [Fact]
    public void OneFightAtATime()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Facing();

        Fighting(world, one, two);

        world.AskToDuel(one.Id, two.Id);

        Assert.Contains("one fight at a time", world.LastDuel);
    }
}
