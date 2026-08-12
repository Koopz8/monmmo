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

        (ServerPlayer player, _) = world.Join(1, "Mason", TestRules.Equipped(world));

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
    public void ANewAccountStartsWithNothingToFightWith()
    {
        // There used to be a party handed out here, from before this game had an
        // opening. It has one now — the professor takes you to his lab and there are
        // three balls on the table — and starting with the thing the first hour is about
        // is worse than starting with nothing.
        GameWorld world = GrassyWorld();
        SavedCharacter fresh = world.FreshCharacter();

        Assert.Empty(fresh.Party);

        // Balls, though. Those are the bag, not the party, and the opening does not
        // hand them over.
        Assert.NotEmpty(fresh.Items);
    }

    [Fact]
    public void NothingHappensInGrassWithNobodyToSendOut()
    {
        // What the free starter was really protecting against. An empty party has to be
        // survivable, because it is now the first thing every character has.
        GameWorld world = GrassyWorld();

        (ServerPlayer player, _) = world.Join(1, "Koop", world.FreshCharacter());

        for (int i = 0; i < 200; i++) world.Move(player.Id, i % 2 == 0 ? Direction.Right : Direction.Left, i);

        Assert.Null(player.Battle);
    }

    [Fact]
    public void AnEncounterArrivesWithBothSidesDrawn()
    {
        (_, _, BattleStarted start) = InBattle();

        Assert.Equal(16, start.Opponent.Species);
        Assert.Equal(start.Opponent.MaxHp, start.Opponent.CurrentHp);
        Assert.NotEmpty(start.You.Moves);
        Assert.True(start.Balls.Sum(b => b.Count) > 0);
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

        int before = start.Balls.Single(b => b.ItemId == TestRules.BallItem).Count;

        world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(TestRules.BallItem));

        Assert.Equal(before - 1, player.Bag.CountOf(TestRules.BallItem));
    }

    [Fact]
    public void ThrowingWithNoBallsLeftIsNotABall()
    {
        // The count is the server's, so a client asking to throw one it does not have
        // gets a turn spent, not a free throw.
        (GameWorld world, ServerPlayer player, _) = InBattle();
        player.Bag = new Bag();

        List<Outgoing> send = world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(TestRules.BallItem));

        BattleUpdate update = send.Select(o => o.Message).OfType<BattleUpdate>().Single();

        Assert.Equal(0, player.Bag.CountOf(TestRules.BallItem));
        Assert.DoesNotContain(update.Events, e => e is BattleEvent.BallThrown);
    }

    [Fact]
    public void ACatchGrowsThePartyWithoutTheClientSayingSo()
    {
        (GameWorld world, ServerPlayer player, _) = InBattle();

        int before = player.Party.Count;

        List<Outgoing> send = world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(TestRules.BallItem));

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

        world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(TestRules.BallItem));

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
    public void WinningLeavesTheExperienceOnTheParty()
    {
        // The writeback at the end of a battle rebuilds the lead's health from the
        // battler that fought — and that battler was built before the battle and never
        // grew. Rebuilding it wholesale would silently undo the level-up that just
        // happened, and only a test that looks after the battle would notice.
        (GameWorld world, ServerPlayer player, _) = InBattle(seed: 11);

        BattleFinished? finished = null;

        for (int turn = 0; turn < 40 && finished is null; turn++)
        {
            finished = world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0))
                .Select(o => o.Message)
                .OfType<BattleFinished>()
                .FirstOrDefault();
        }

        Assert.NotNull(finished);
        Assert.Equal(Side.Player, finished!.Winner);

        Assert.True(player.Party[0].Experience > 0, "the payout was lost when the battle closed");
        Assert.Equal(player.Party[0].Experience, finished.Party[0].Experience);
    }

    [Fact]
    public void WinningIsNarratedAsAPayout()
    {
        (GameWorld world, ServerPlayer player, _) = InBattle(seed: 11);

        var events = new List<BattleEvent>();

        for (int turn = 0; turn < 40 && player.Battle is not null; turn++)
        {
            events.AddRange(world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0))
                .Select(o => o.Message)
                .OfType<BattleUpdate>()
                .SelectMany(u => u.Events));
        }

        // Sent with the turn that earned it, not after the battle, so a level-up reads
        // as part of the fight.
        Assert.Contains(events, e => e is BattleEvent.ExperienceGained);

        // And before the battle closes. Appended after "You won the battle!" it reads
        // backwards, and that is the easiest line in a battle to press past unread.
        int gained = events.FindIndex(e => e is BattleEvent.ExperienceGained);
        int ended = events.FindIndex(e => e is BattleEvent.Ended);

        Assert.True(gained >= 0 && ended >= 0);
        Assert.True(gained < ended, "the payout was announced after the battle ended");
    }

    [Fact]
    public void CatchingSomethingPaysNoExperience()
    {
        // The games award none for a capture, and it matters here for a duller reason:
        // the opponent is about to join the party, so paying out for beating it would
        // be paying for something that never fainted.
        (GameWorld world, ServerPlayer player, _) = InBattle();

        List<Outgoing> send = world.TakeBattleTurn(player.Id, new BattleAction.ThrowBall(TestRules.BallItem));

        BattleUpdate update = send.Select(o => o.Message).OfType<BattleUpdate>().Single();

        Assert.DoesNotContain(update.Events, e => e is BattleEvent.ExperienceGained);
        Assert.Equal(0, player.Party[0].Experience);
    }

    [Fact]
    public void ATurnFromSomebodyNotInABattleIsRefused()
    {
        GameWorld world = GrassyWorld();
        (ServerPlayer player, _) = world.Join(1, "Mason", TestRules.Equipped(world));

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

        (ServerPlayer player, _) = world.Join(1, "Mason", TestRules.Equipped(world));

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

/// <summary>
/// What happens after a loss.
/// <para>
/// This is the case that froze a real game: a party with nothing left standing has no
/// healthy lead, so every encounter after it started a battle that was already over.
/// </para>
/// </summary>
public class LosingTests
{
    private const string Route = "3.19";

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

    private static SavedMon Fainted(int species = 16) =>
        new(species, 3, null, 0, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

    private static bool WalkIntoGrass(GameWorld world, ServerPlayer player, int steps = 200)
    {
        double now = 0;

        for (int step = 0; step < steps; step++)
        {
            player.Square = new GridPosition(step % 4, 1);
            player.LastStepAt = double.NegativeInfinity;
            now += 1;

            if (world.Move(player.Id, Direction.Down, now).Any(o => o.Message is BattleStarted)) return true;
        }

        return false;
    }

    [Fact]
    public void AWipedPartyIsHealedOnLogin()
    {
        // Without this a save written mid-wipe is an account that can never battle
        // again — no healthy lead, no encounters, and so no way back.
        GameWorld world = GrassyWorld();

        (ServerPlayer player, _) = world.Join(
            1, "Mason", new SavedCharacter(Route, 0, 0, Direction.Down, [Fainted()]));

        Assert.True(player.Party[0].CurrentHp > 0);
    }

    [Fact]
    public void HealingOnLoginLeavesAHealthyPartyAlone()
    {
        GameWorld world = GrassyWorld();

        var hurt = new SavedMon(16, 3, null, 1, StatusCondition.Burn, Nature.Hardy, [TestRules.FirstMove]);

        (ServerPlayer player, _) = world.Join(
            1, "Mason", new SavedCharacter(Route, 0, 0, Direction.Down, [hurt]));

        // One battler on its last point is not a wipe, and healing it would make every
        // reconnect a free rest stop.
        Assert.Equal(hurt, player.Party[0]);
    }

    [Fact]
    public void NoEncounterStartsWhenNothingCanFight()
    {
        // The freeze: the battle was over before the first turn, so the server had
        // nothing to answer with and the screen waited forever.
        GameWorld world = GrassyWorld();

        (ServerPlayer player, _) = world.Join(
            1, "Mason", new SavedCharacter(Route, 0, 0, Direction.Down, [Fainted()]));

        // Faint it again, past the login heal, to reach the state directly.
        player.Party[0] = Fainted();

        Assert.False(WalkIntoGrass(world, player));
        Assert.Null(player.Battle);
    }

    [Fact]
    public void LosingHealsThePartyRatherThanEndingTheAccount()
    {
        GameWorld world = GrassyWorld(seed: 3);

        (ServerPlayer player, _) = world.Join(1, "Mason", TestRules.Equipped(world));

        // A single point of health, so the loss arrives quickly.
        player.Party[0] = player.Party[0] with { CurrentHp = 1 };

        Assert.True(WalkIntoGrass(world, player));

        BattleFinished? finished = null;

        for (int turn = 0; turn < 40 && finished is null; turn++)
        {
            finished = world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0))
                .Select(o => o.Message)
                .OfType<BattleFinished>()
                .FirstOrDefault();
        }

        Assert.NotNull(finished);

        if (finished!.Winner != Side.Opponent) return;   // it won; nothing to heal

        Assert.All(finished.Party, m => Assert.True(m.CurrentHp > 0));
        Assert.All(player.Party, m => Assert.True(m.CurrentHp > 0));

        // And the proof it is recoverable: another encounter can start.
        Assert.True(WalkIntoGrass(world, player));
    }
}
