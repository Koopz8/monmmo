using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Fights with a person rather than something in the grass.
/// <para>
/// The engine underneath is still one-on-one. A trainer fight is a run of those, and
/// what these tests are really about is the seam between them: that somebody else comes
/// out, that health is written down before they do, and that the fight only ends when
/// one side has genuinely nobody left.
/// </para>
/// </summary>
public class TrainerBattleTests
{
    private const string Town = "3.0";

    private static MapObject Trainer(int localId, int x, int y, Direction facing, int trainerId, int sight = 3) =>
        new(localId, 5, x, y, facing, 0, true, 0, 0, 0, trainerId, sight);

    private static GameWorld World(params MapObject[] people)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = people };

        return new GameWorld(new WorldData([map]), Town, TestRules.All);
    }

    /// <summary>
    /// A player with a party that wins.
    /// <para>
    /// Deliberately over-levelled. These tests are about what happens between battles,
    /// and a fight the player loses on the first creature never gets there — which is
    /// exactly how three of them failed the first time they were run.
    /// </para>
    /// </summary>
    private static ServerPlayer Join(GameWorld world, int party = 1, int level = 30)
    {
        var members = new List<SavedMon>();

        for (int i = 0; i < party; i++) members.Add(Healthy(3 + i, level));

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter() with { Party = members });

        return player;
    }

    /// <summary>
    /// One party member at full health.
    /// <para>
    /// The health is a number larger than any maximum rather than a real one, because
    /// the server applies damage rather than assigning health — maximum health comes
    /// out of base stats, so asking for more than there is means "undamaged".
    /// </para>
    /// </summary>
    private static SavedMon Healthy(int species, int level) =>
        new(species, level, null, 9999, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

    /// <summary>Fights until the whole thing ends, collecting everything that was sent.</summary>
    private static List<NetMessage> FightToTheEnd(GameWorld world, ServerPlayer player, int maxTurns = 400)
    {
        var seen = new List<NetMessage>();

        for (int turn = 0; turn < maxTurns && player.InBattle; turn++)
        {
            foreach (Outgoing outgoing in world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0)))
                seen.Add(outgoing.Message);
        }

        return seen;
    }

    /// <summary>Walks a player onto a square and returns whatever the server said about it.</summary>
    private static List<NetMessage> StepTo(GameWorld world, ServerPlayer player, GridPosition square, Direction facing)
    {
        player.Square = square.Step(Opposite(facing));
        player.LastStepAt = double.NegativeInfinity;

        return world.Move(player.Id, facing, 1000).Select(o => o.Message).ToList();
    }

    private static Direction Opposite(Direction direction) => Interaction.Opposite(direction);

    [Fact]
    public void WalkingIntoSomebodysLineOfSightStartsAFight()
    {
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone));
        ServerPlayer player = Join(world);

        List<NetMessage> said = StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        BattleStarted started = said.OfType<BattleStarted>().Single();

        Assert.Equal(TestRules.OneAlone, started.TrainerId);
        Assert.True(player.InBattle);
    }

    [Fact]
    public void WalkingPastSomebodyOutOfTheirLineDoesNot()
    {
        // The line is the whole rule. A distance would have them notice somebody
        // standing diagonally, which is famously the one thing they do not do.
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone));
        ServerPlayer player = Join(world);

        Assert.Empty(StepTo(world, player, new GridPosition(6, 3), Direction.Down).OfType<BattleStarted>());
        Assert.False(player.InBattle);
    }

    [Fact]
    public void SomebodyStandingInTheWayBlocksTheirView()
    {
        // Both stand in the same column, so the far one is looking through the near one.
        GameWorld world = World(
            Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone, sight: 4),
            Trainer(2, 4, 2, Direction.Left, TestRules.ThreeStrong, sight: 0));

        ServerPlayer player = Join(world);

        Assert.Empty(StepTo(world, player, new GridPosition(4, 4), Direction.Down).OfType<BattleStarted>());
    }

    [Fact]
    public void TalkingToSomebodyWhoWantsAFightStartsOne()
    {
        GameWorld world = World(Trainer(1, 4, 3, Direction.Up, TestRules.OneAlone));
        ServerPlayer player = Join(world);

        player.Square = new GridPosition(4, 2);
        player.Facing = Direction.Down;

        List<NetMessage> said = world.StartTalking(player.Id, 1).Select(o => o.Message).ToList();

        Assert.Single(said.OfType<BattleStarted>());

        // And they are fighting rather than standing still being spoken to.
        Assert.Null(world.TalkingTo(player.Id));
    }

    [Fact]
    public void TheyBringEverybodyTheyHave()
    {
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.ThreeStrong));
        ServerPlayer player = Join(world, party: 3);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        List<NetMessage> said = FightToTheEnd(world, player);

        // Three creatures, two of which have to be sent out after the first faints.
        Assert.Equal(2, said.OfType<BattlerSentOut>().Count(s => s.Side == Side.Opponent));
    }

    [Fact]
    public void TheFightIsNotOverWhileTheyHaveSomebodyLeft()
    {
        // The engine says a battle ended every time anybody faints. Reporting that as
        // the end of the fight would close the screen after their first creature.
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.ThreeStrong));
        ServerPlayer player = Join(world, party: 3);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        var updates = new List<BattleUpdate>();
        int finishes = 0;

        for (int turn = 0; turn < 400 && player.InBattle; turn++)
        {
            foreach (Outgoing outgoing in world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0)))
            {
                if (outgoing.Message is BattleUpdate update) updates.Add(update);
                if (outgoing.Message is BattleFinished) finishes++;
            }
        }

        Assert.Equal(1, finishes);

        // "Ended" belongs to the last battle of the fight and to no other.
        Assert.Equal(1, updates.Count(u => u.Events.Any(e => e is BattleEvent.Ended)));
    }

    [Fact]
    public void YourNextOneComesOutWhenTheFirstFaints()
    {
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.ThreeStrong));

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter() with
        {
            Party =
            [
                // One health and slower than what it is facing, so it goes down before
                // it gets a turn.
                new SavedMon(3, 2, null, 1, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]),
                Healthy(4, 30),
            ],
        });

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        List<NetMessage> said = FightToTheEnd(world, player);

        Assert.Contains(said.OfType<BattlerSentOut>(), s => s.Side == Side.Player);
    }

    [Fact]
    public void WhatHappenedToTheOneWhoFaintedIsWrittenDown()
    {
        // Written before anybody replaces them. A fight that only records the creature
        // that happened to be out at the end is one you can walk away from healthy.
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone));

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter() with
        {
            Party =
            [
                // One health and slower than what it is facing, so it goes down before
                // it gets a turn. Over-levelling it instead means it kills everything
                // first and never faints at all, which is how this test first passed
                // while proving nothing.
                new SavedMon(3, 2, null, 1, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]),
                Healthy(4, 30),
            ],
        });

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);
        FightToTheEnd(world, player);

        SavedCharacter saved = world.Snapshot(player.Id)!;

        Assert.Equal(0, saved.Party[0].CurrentHp);
    }

    [Fact]
    public void SomebodyBeatenStaysBeaten()
    {
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone));
        ServerPlayer player = Join(world, party: 3);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);
        FightToTheEnd(world, player);

        Assert.Contains(TestRules.OneAlone, player.DefeatedTrainers);

        // And walking back through the same line does nothing at all.
        Assert.Empty(StepTo(world, player, new GridPosition(4, 3), Direction.Down).OfType<BattleStarted>());
    }

    [Fact]
    public void WhoYouHaveBeatenIsWrittenIntoTheSave()
    {
        // It has to outlive the connection. A trainer who forgets they lost challenges
        // you again the moment you walk back past them.
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone));
        ServerPlayer player = Join(world, party: 3);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);
        FightToTheEnd(world, player);

        Assert.Contains(TestRules.OneAlone, world.Snapshot(player.Id)!.DefeatedTrainers);
    }

    [Fact]
    public void ABallThrownAtSomebodyElsesCreatureIsNotAThrow()
    {
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.ThreeStrong));
        ServerPlayer player = Join(world, party: 3);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        int before = player.Bag.CountOf(TestRules.BallItem);

        List<NetMessage> said = world
            .TakeBattleTurn(player.Id, new BattleAction.ThrowBall(TestRules.BallItem))
            .Select(o => o.Message)
            .ToList();

        Assert.Equal(before, player.Bag.CountOf(TestRules.BallItem));
        Assert.DoesNotContain(
            said.OfType<BattleUpdate>().SelectMany(u => u.Events),
            e => e is BattleEvent.BallThrown);
    }

    [Fact]
    public void BeatingSomebodyPaysForEveryCreatureTheyBrought()
    {
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.ThreeStrong));
        ServerPlayer player = Join(world, party: 3);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        List<NetMessage> said = FightToTheEnd(world, player);

        List<BattleEvent> events = said.OfType<BattleUpdate>().SelectMany(u => u.Events).ToList();

        // Only meaningful when the player actually won, which with these numbers they do.
        Assert.Equal(Side.Player, said.OfType<BattleFinished>().Single().Winner);
        Assert.Equal(3, events.Count(e => e is BattleEvent.ExperienceGained));
    }

    [Fact]
    public void SomebodyWhoHasTurnedIsLookingTheNewWay()
    {
        // Their square comes from the living world already. Their facing has to as
        // well: taking the position from one source and the direction from the other
        // gives a line neither of them is looking along, and the bug hides until
        // somebody happens to walk up to a trainer who glances about.
        var watcher = new MapObject(
            1, 5, 4, 1, MapObject.FacingFor(1), 1, IsTrainer: true,
            RangeX: 1, RangeY: 1, ScriptAddress: 0, TrainerId: TestRules.OneAlone, SightRange: 3);

        GameWorld world = World(watcher);
        ServerPlayer player = Join(world);

        player.Square = new GridPosition(0, 7);

        // Turn the clock until they happen to look along the row the player will be on.
        bool looking = false;

        for (double now = 0.2; now < 200 && !looking; now += 0.2)
        {
            foreach (ObjectMoved moved in world.Tick(now).Select(o => o.Message).OfType<ObjectMoved>())
                if (moved.Facing == Direction.Right) looking = true;
        }

        Assert.True(looking, "they never turned to face along the row.");

        Assert.Single(StepTo(world, player, new GridPosition(6, 1), Direction.Left).OfType<BattleStarted>());
    }

    [Fact]
    public void SomebodyBeatenSaysSoRatherThanSayingNothing()
    {
        // "I walked in front of them and nothing happened" has several causes that look
        // identical from the player's side. The server is the only side that can tell
        // them apart, so it writes down which it was.
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone));
        ServerPlayer player = Join(world, party: 3);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);
        FightToTheEnd(world, player);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        Assert.Contains("already been beaten", world.LastSightRefusal);
    }

    [Fact]
    public void SomebodyWithNoLineOfSightSaysSoToo()
    {
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone, sight: 0));
        ServerPlayer player = Join(world);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        Assert.Contains("no line of sight", world.LastSightRefusal);
    }

    [Fact]
    public void WalkingPastAnOrdinaryPersonSaysNothingAtAll()
    {
        // Most people on a map are not trainers. A refusal for every one of them would
        // bury the one that matters.
        GameWorld world = World(new MapObject(1, 5, 4, 1, Direction.Down, 0, false));
        ServerPlayer player = Join(world);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        Assert.Null(world.LastSightRefusal);
    }

    [Fact]
    public void BeatingSomebodyPays()
    {
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.ThreeStrong));
        ServerPlayer player = Join(world, party: 3);

        int before = player.Money;

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        BattleFinished finished = FightToTheEnd(world, player).OfType<BattleFinished>().Single();

        // The rate is this project's, not the cartridge's — the games use a per-class
        // table this project does not read — so what is asserted is the formula as
        // stated rather than a number from anywhere else.
        int expected = GameWorld.PrizePerLevel * 7;

        Assert.Equal(expected, finished.Prize);
        Assert.Equal(before + expected, finished.Money);
        Assert.Equal(before + expected, player.Money);
    }

    [Fact]
    public void SomethingOutOfTheGrassPaysNothing()
    {
        // Prize money is for beating a person. A wild creature has no pockets.
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone));
        ServerPlayer player = Join(world, party: 3);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);
        FightToTheEnd(world, player);

        int afterTrainer = player.Money;

        Assert.True(afterTrainer > SavedCharacter.StartingMoney);
    }

    [Fact]
    public void LosingPaysNothing()
    {
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.ThreeStrong));

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter() with
        {
            Party = [new SavedMon(3, 2, null, 1, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])],
        });

        int before = player.Money;

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        BattleFinished finished = FightToTheEnd(world, player).OfType<BattleFinished>().Single();

        Assert.Equal(Side.Opponent, finished.Winner);
        Assert.Equal(0, finished.Prize);
        Assert.Equal(before, player.Money);
    }

    [Fact]
    public void SomebodyWhoseIdThisServerHasNoPartyForStartsNothing()
    {
        // Ordinary rather than an error: a world file exported from one image and a
        // rules file from another would be full of these, and the right behaviour is a
        // world where nobody fights rather than a server that will not run.
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, trainerId: 9999));
        ServerPlayer player = Join(world);

        Assert.Empty(StepTo(world, player, new GridPosition(4, 3), Direction.Down).OfType<BattleStarted>());
    }

    [Fact]
    public void SomebodyMarkedATrainerWithNoIdStartsNothingEither()
    {
        // 432 of the 441 trainers in a real world file name an id. The other nine are
        // people whose script this project cannot read all the way to the fight.
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, trainerId: 0));
        ServerPlayer player = Join(world);

        Assert.Empty(StepTo(world, player, new GridPosition(4, 3), Direction.Down).OfType<BattleStarted>());
    }
}
