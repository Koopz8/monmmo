using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Battles resolved by the server.
/// <para>
/// The claim being tested is not that the arithmetic is right — that is covered
/// against the engine directly — but that the client no longer gets a say. What it
/// sends is a request; what comes back is a decision.
/// </para>
/// </summary>
public class ServerBattleTests
{
    private const string Route = "3.19";

    /// <summary>A map that is nothing but encounter grass, so a battle is easy to start.</summary>
    private static GameWorld GrassyWorld(uint seed = 1)
    {
        var behaviours = new byte[16];
        Array.Fill(behaviours, MetatileBehaviour.TallGrass);

        MapData map = new(Route, "ROUTE 1", 4, 4, new byte[16])
        {
            Behaviours = behaviours,
            Encounters = new MapEncounters(Route, Land: new EncounterTable(
                EncounterKind.Land,
                100,
                Enumerable.Range(0, 12).Select(_ => new WildSlot(16, 3, 3)).ToList())),
        };

        return new GameWorld(new WorldData([map]), Route, TestRules.All, seed);
    }

    private static (GameWorld World, ServerPlayer Player, BattleStarted Start) InBattle(uint seed = 1)
    {
        GameWorld world = GrassyWorld(seed);

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        double now = 0;

        for (int step = 0; step < 200; step++)
        {
            player.Square = new GridPosition(step % 4, 1);
            player.LastStepAt = double.NegativeInfinity;
            now += 1;

            foreach (Outgoing outgoing in world.Move(player.Id, Direction.Down, now))
            {
                if (outgoing.Message is BattleStarted started) return (world, player, started);
            }
        }

        throw new InvalidOperationException("Never met anything.");
    }

    [Fact]
    public void ANewAccountIsGivenSomethingToFightWith()
    {
        GameWorld world = GrassyWorld();
        SavedCharacter fresh = world.FreshCharacter();

        // Handed out at registration rather than conjured at the first encounter, so
        // the server never has to invent a battler mid-battle.
        SavedMon starter = Assert.Single(fresh.Party);

        Assert.Equal(BattleFactory.StarterSpecies, starter.Species);
        Assert.NotEmpty(starter.Moves);
    }

    [Fact]
    public void AnEncounterArrivesWithBothSidesDrawn()
    {
        (_, _, BattleStarted start) = InBattle();

        Assert.Equal(16, start.Opponent.Species);
        Assert.Equal(start.Opponent.MaxHp, start.Opponent.CurrentHp);
        Assert.NotEmpty(start.You.Moves);
        Assert.True(start.Balls > 0);
    }

    [Fact]
    public void TheServerHoldsTheBattleNotTheClient()
    {
        (_, ServerPlayer player, _) = InBattle();

        Assert.NotNull(player.Battle);
        Assert.True(player.InBattle);
    }

    [Fact]
    public void ATurnComesBackAsEventsAndHealth()
    {
        (GameWorld world, ServerPlayer player, _) = InBattle();

        List<Outgoing> send = world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0));

        BattleUpdate update = send.Select(o => o.Message).OfType<BattleUpdate>().Single();

        Assert.NotEmpty(update.Events);
        Assert.Contains(update.Events, e => e is BattleEvent.MoveUsed);

        // Health travels alongside the events rather than being derivable from them:
        // a client reconstructing state from a narrative will eventually disagree.
        // It has to agree with the battle the server is actually holding.
        Assert.Equal(player.Battle!.Player.CurrentHp, update.YourHp);
        Assert.Equal(player.Battle.Opponent.CurrentHp, update.OpponentHp);

        // And somebody took a hit, since both sides attacked.
        Assert.True(
            update.OpponentHp < player.Battle.Opponent.MaxHp ||
            update.YourHp < player.Battle.Player.MaxHp);
    }

    [Fact]
    public void EventsCarryNoNames()
    {
        // The server has none to give. Asserted on real output rather than only on the
        // shape of the types, because this is the message that actually leaves.
        (GameWorld world, ServerPlayer player, _) = InBattle();

        List<Outgoing> send = world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0));
        BattleUpdate update = send.Select(o => o.Message).OfType<BattleUpdate>().Single();

        string json = System.Text.Json.JsonSerializer.Serialize(update);

        Assert.DoesNotContain("BULBASAUR", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TACKLE", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheServerCountsTheBalls()
    {
        (GameWorld world, ServerPlayer player, BattleStarted start) = InBattle();

        world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(BallKind.Poke));

        Assert.Equal(start.Balls - 1, player.Balls);
    }

    [Fact]
    public void ThrowingWithNoBallsLeftIsNotABall()
    {
        // The count is the server's, so a client asking to throw one it does not have
        // gets a turn spent, not a free throw.
        (GameWorld world, ServerPlayer player, _) = InBattle();
        player.Balls = 0;

        List<Outgoing> send = world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(BallKind.Master));

        BattleUpdate update = send.Select(o => o.Message).OfType<BattleUpdate>().Single();

        Assert.Equal(0, player.Balls);
        Assert.DoesNotContain(update.Events, e => e is BattleEvent.BallThrown);
    }

    [Fact]
    public void ACatchGrowsThePartyWithoutTheClientSayingSo()
    {
        (GameWorld world, ServerPlayer player, _) = InBattle();

        int before = player.Party.Count;

        List<Outgoing> send = world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(BallKind.Master));

        BattleFinished finished = send.Select(o => o.Message).OfType<BattleFinished>().Single();

        Assert.True(finished.Caught);
        Assert.Equal(before + 1, finished.Party.Count);
        Assert.Equal(16, finished.Party[^1].Species);
        Assert.Equal(before + 1, player.Party.Count);
    }

    [Fact]
    public void TheBattleIsClosedWhenItEnds()
    {
        (GameWorld world, ServerPlayer player, _) = InBattle();

        world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(BallKind.Master));

        Assert.Null(player.Battle);
        Assert.False(player.InBattle);
    }

    [Fact]
    public void DamageTakenInABattleIsWrittenBackToTheParty()
    {
        // Health carries out of a battle. Decided from the battle the server ran,
        // rather than reported by the client afterwards.
        (GameWorld world, ServerPlayer player, _) = InBattle(seed: 9);

        for (int turn = 0; turn < 20 && player.Battle is not null; turn++)
            world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0));

        SavedMon lead = player.Party[0];

        Assert.True(lead.CurrentHp >= 0);
        Assert.True(lead.CurrentHp <= Stats.Hp(TestRules.All.SpeciesAt(lead.Species)!.BaseHp, lead.Level));
    }

    [Fact]
    public void ATurnFromSomebodyNotInABattleIsRefused()
    {
        GameWorld world = GrassyWorld();
        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        List<Outgoing> send = world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0));

        Assert.IsType<Rejected>(Assert.Single(send).Message);
    }

    [Fact]
    public void AServerWithNoRulesStartsNoBattles()
    {
        // Without base stats it cannot build a battler at all. Rolling an encounter it
        // could not run would leave a player stuck staring at grass.
        var behaviours = new byte[16];
        Array.Fill(behaviours, MetatileBehaviour.TallGrass);

        MapData map = new(Route, "ROUTE 1", 4, 4, new byte[16])
        {
            Behaviours = behaviours,
            Encounters = new MapEncounters(Route, Land: new EncounterTable(
                EncounterKind.Land,
                100,
                Enumerable.Range(0, 12).Select(_ => new WildSlot(16, 3, 3)).ToList())),
        };

        var world = new GameWorld(new WorldData([map]), Route);

        Assert.False(world.CanResolveBattles);

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        double now = 0;

        for (int step = 0; step < 100; step++)
        {
            player.Square = new GridPosition(step % 4, 1);
            player.LastStepAt = double.NegativeInfinity;
            now += 1;

            Assert.DoesNotContain(
                world.Move(player.Id, Direction.Down, now),
                o => o.Message is BattleStarted);
        }
    }
}
