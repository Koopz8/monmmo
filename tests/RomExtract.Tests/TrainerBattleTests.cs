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

        List<NetMessage> said = [.. world.Move(player.Id, facing, 1000).Select(o => o.Message)];

        // Being seen is not the fight; it is the start of a walk, and the fight is at
        // the end of it. The clock has to run for that walk to happen, and every test
        // below that used to get a battle out of one step now gets one out of a step
        // and a few seconds.
        for (double now = 1000; now < 1010 && !player.InBattle; now += 0.1)
            said.AddRange(world.Tick(now).Select(o => o.Message));

        return said;
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

        // Talking holds them still. It does not fight them: a trainer who has to be
        // spoken to has words first, and the man at the top of NUGGET BRIDGE has a
        // whole scene of them.
        List<NetMessage> said = world.StartTalking(player.Id, 1).Select(o => o.Message).ToList();

        Assert.Empty(said.OfType<BattleStarted>());

        // The fight is what closing the box comes to.
        Assert.Single(world.StopTalking(player.Id).Select(o => o.Message).OfType<BattleStarted>());

        Assert.Null(world.TalkingTo(player.Id));
    }

    [Fact]
    public void SomebodyAlreadyBeatenIsJustSomebodyToTalkTo()
    {
        // The other half of waiting for the box to close. A trainer who is queued up on
        // being spoken to has to be un-queued by having been beaten, or every later
        // conversation with them would end in a fight they have already lost.
        GameWorld world = World(Trainer(1, 4, 3, Direction.Up, TestRules.OneAlone));
        ServerPlayer player = Join(world);

        player.Square = new GridPosition(4, 2);
        player.Facing = Direction.Down;

        player.DefeatedTrainers.Add(TestRules.OneAlone);

        world.StartTalking(player.Id, 1);

        Assert.Empty(world.StopTalking(player.Id).Select(o => o.Message).OfType<BattleStarted>());
        Assert.Null(player.Battle);
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
    public void SomebodyElseCanBeSentOutByChoice()
    {
        // The last thing a battle could not do. Everything else about a party was
        // already here — the next one comes out when somebody faints — but nobody could
        // choose, so a fight was whichever creature happened to be first.
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone));
        ServerPlayer player = Join(world, party: 2);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        List<NetMessage> said = [.. world.TakeBattleTurn(player.Id, new BattleAction.SwitchTo(1))
            .Select(o => o.Message)];

        Assert.Single(said.OfType<BattlerSentOut>().Where(s => s.Side == Side.Player));

        // And it cost the turn: the other side moved and this one did not.
        List<BattleEvent> events = said.OfType<BattleUpdate>().SelectMany(u => u.Events).ToList();

        Assert.Contains(events.OfType<BattleEvent.MoveUsed>(), e => e.Side == Side.Opponent);
        Assert.DoesNotContain(events.OfType<BattleEvent.MoveUsed>(), e => e.Side == Side.Player);
    }

    [Fact]
    public void SwitchingToNobodyIsRefused()
    {
        // Three things a client could ask for and a player could not do: a slot that is
        // not in the party, the one already out, and somebody who has fainted.
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone));
        ServerPlayer player = Join(world, party: 2);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        Assert.Single(world.TakeBattleTurn(player.Id, new BattleAction.SwitchTo(9))
            .Select(o => o.Message).OfType<Rejected>());

        Assert.Single(world.TakeBattleTurn(player.Id, new BattleAction.SwitchTo(0))
            .Select(o => o.Message).OfType<Rejected>());
    }

    [Fact]
    public void WhoeverLeavesTakesTheirDamageWithThem()
    {
        // The one going out has to be written back before the one coming in replaces
        // them, or a switch is a free heal.
        GameWorld world = World(Trainer(1, 4, 1, Direction.Down, TestRules.OneAlone));
        ServerPlayer player = Join(world, party: 2);

        StepTo(world, player, new GridPosition(4, 3), Direction.Down);

        // A turn of being hit, then out.
        world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0));

        int hurt = player.Party[0].CurrentHp;

        world.TakeBattleTurn(player.Id, new BattleAction.SwitchTo(1));

        Assert.Equal(hurt, player.Party[0].CurrentHp);
    }

    [Fact]
    public void BeatingAGymLeaderHandsOverWhatTheFightPaysOut()
    {
        // Eight fights in this cartridge pay out more than money, and every one is a
        // gym. The TM is inside the script the trainerbattle runs on being won, which no
        // conversation reaches — BROCK says he is PEWTER's gym leader before the fight
        // and muses about trainers everywhere after it, and neither line mentions TM39.
        //
        // Talked to rather than walked past, because that is the only way a leader is
        // fought: their record carries no sight range at all.
        var leader = new MapObject(
            1, 5, 4, 1, Direction.Down, 0, IsTrainer: false, TrainerId: TestRules.OneAlone, SightRange: 0)
        {
            WinsItemId = TestRules.PotionItem,
            WinsCount = 1,
        };

        GameWorld world = World(leader);
        ServerPlayer player = Join(world);

        player.Square = new GridPosition(4, 2);
        player.Facing = Direction.Up;

        world.StartTalking(player.Id, 1);

        Assert.Single(world.StopTalking(player.Id).Select(o => o.Message).OfType<BattleStarted>());

        List<NetMessage> said = FightToTheEnd(world, player);

        Assert.Equal(Side.Player, said.OfType<BattleFinished>().Single().Winner);
        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
        Assert.Contains("for beating trainer", world.LastPrize ?? "");
    }

    [Fact]
    public void AFightsPrizeAndItsScriptAreOneHandover()
    {
        // Two views of the same thing: the export reads the won-fight script for its
        // giveitem and the server pays it out, and the client runs that same script and
        // names what came out. Both are wanted — one covers a script this reader cannot
        // follow, the other covers a prize the export cannot see — but they share a
        // ledger entry, because MISTY handed over TM03 twice before they did.
        var leader = new MapObject(
            1, 5, 4, 1, Direction.Down, 0, IsTrainer: false, TrainerId: TestRules.OneAlone, SightRange: 0)
        {
            WinsItemId = TestRules.PotionItem,
            WinsCount = 1,
            CanGive = [TestRules.PotionItem],
        };

        GameWorld world = World(leader);
        ServerPlayer player = Join(world);

        player.Square = new GridPosition(4, 2);
        player.Facing = Direction.Up;

        world.StartTalking(player.Id, 1);
        FightToTheEnd(world, player);

        // And now the client, having played the same script out, says what it handed over.
        world.ScriptGave(player.Id, 1, TestRules.PotionItem);

        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
    }

    [Fact]
    public void AGymLeaderPaysOutOnce()
    {
        // The same ledger a ball on the ground uses. Without it, a client that could get
        // the fight to end twice could get the TM twice.
        var leader = new MapObject(
            1, 5, 4, 1, Direction.Down, 0, IsTrainer: false, TrainerId: TestRules.OneAlone, SightRange: 0)
        {
            WinsItemId = TestRules.PotionItem,
            WinsCount = 1,
        };

        GameWorld world = World(leader);
        ServerPlayer player = Join(world);

        player.Square = new GridPosition(4, 2);
        player.Facing = Direction.Up;

        world.StartTalking(player.Id, 1);
        world.StopTalking(player.Id);
        FightToTheEnd(world, player);

        // Beaten once, so the second conversation is a conversation and not a fight.
        player.DefeatedTrainers.Remove(TestRules.OneAlone);

        world.StartTalking(player.Id, 1);
        world.StopTalking(player.Id);
        FightToTheEnd(world, player);

        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
        Assert.Null(world.LastPrize);
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
    public void LosingPaysNothingAndCostsHalf()
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

        // Losing used to cost nothing at all, which made a centre a place with no
        // reason to exist. It costs half now, and the walk back from wherever you last
        // rested.
        Assert.Equal(before / GameWorld.LossShare, player.Money);
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

/// <summary>
/// The walk between being seen and being fought.
/// <para>
/// A trainer who spots you across a route does not fight you from there. They walk over,
/// and that walk is the part everybody remembers — it is why you learn to hug the far
/// wall of a route rather than stroll down the middle of it. Until this, being seen and
/// being fought were the same instant.
/// </para>
/// </summary>
public class ApproachTests
{
    private const string Route = "3.19";

    private static GameWorld World(params MapObject[] people)
    {
        MapData map = new(Route, "ROUTE 1", 8, 8, new byte[64]) { Objects = people };

        return new GameWorld(new WorldData([map]), Route, TestRules.All);
    }

    /// <summary>Somebody looking down a long line, four squares of it.</summary>
    private static MapObject Watcher(int localId, int x, int y) =>
        new(localId, 5, x, y, Direction.Down, 0, true, 0, 0, 0, TestRules.OneAlone, 4);

    private static ServerPlayer Join(GameWorld world) =>
        world.Join(1, "Koop", world.FreshCharacter() with
        {
            MapId = Route,
            Party = [new SavedMon(3, 30, null, 100, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])],
        }).Player;

    /// <summary>Walks into the line of sight and returns everything the server then said.</summary>
    private static (List<NetMessage> Said, ServerPlayer Player) Spotted(GameWorld world)
    {
        ServerPlayer player = Join(world);

        player.Square = new GridPosition(3, 5);
        player.LastStepAt = double.NegativeInfinity;

        return ([.. world.Move(player.Id, Direction.Right, 1000).Select(o => o.Message)], player);
    }

    private static List<NetMessage> Run(GameWorld world, double seconds, double from = 1000)
    {
        var said = new List<NetMessage>();

        for (double now = from; now < from + seconds; now += 0.1)
            said.AddRange(world.Tick(now).Select(o => o.Message));

        return said;
    }

    [Fact]
    public void BeingSeenIsNotYetBeingFought()
    {
        GameWorld world = World(Watcher(1, 4, 1));

        (List<NetMessage> said, ServerPlayer player) = Spotted(world);

        Assert.Equal(1, said.OfType<TrainerSpotted>().Single().LocalId);
        Assert.Empty(said.OfType<BattleStarted>());
        Assert.False(player.InBattle);
    }

    [Fact]
    public void TheyWalkOverAndThenTheFightStarts()
    {
        GameWorld world = World(Watcher(1, 4, 1));

        (_, ServerPlayer player) = Spotted(world);

        List<NetMessage> walked = Run(world, 10);

        // Three squares between (4, 1) and (4, 5), stopping one short of standing on
        // the player. Every one of them is sent, because the walk is the point.
        Assert.Equal(3, walked.OfType<ObjectMoved>().Count(m => m.LocalId == 1 && (m.X, m.Y) != (4, 1)));

        Assert.Single(walked.OfType<BattleStarted>());
        Assert.True(player.InBattle);
    }

    [Fact]
    public void ThePlayerStandsStillForIt()
    {
        // Enforced rather than asked for. A client that decided when it was allowed to
        // move again could decide it had never been seen.
        GameWorld world = World(Watcher(1, 4, 1));

        (_, ServerPlayer player) = Spotted(world);

        GridPosition before = player.Square;

        Assert.Contains(
            world.Move(player.Id, Direction.Down, 1001).Select(o => o.Message).OfType<MoveRejected>(),
            r => r.Reason.Contains("word"));

        Assert.Equal(before, player.Square);
    }

    /// <summary>Runs the clock and remembers when each thing was said.</summary>
    private static List<(double At, NetMessage Message)> Timed(GameWorld world, double seconds, double from = 1000)
    {
        var said = new List<(double, NetMessage)>();

        for (double now = from; now < from + seconds; now += 0.05)
        {
            foreach (Outgoing outgoing in world.Tick(now)) said.Add((now - from, outgoing.Message));
        }

        return said;
    }

    [Fact]
    public void NobodyMovesUntilTheMarkHasBeenSeen()
    {
        // Without a beat here the notice, the walk and the fight all land inside a
        // second, and a player sees a battle screen appear rather than somebody
        // deciding to challenge them. Asserted rather than tuned and forgotten: a pause
        // nothing tests is a pause somebody deletes.
        GameWorld world = World(Watcher(1, 4, 1));

        Spotted(world);

        Assert.Empty(Timed(world, 0.6).Where(m => m.Message is ObjectMoved));
        Assert.NotEmpty(Timed(world, 3, 1000.6).Where(m => m.Message is ObjectMoved));
    }

    [Fact]
    public void TheFightDoesNotStartTheInstantTheyArrive()
    {
        GameWorld world = World(Watcher(1, 4, 1));

        Spotted(world);

        List<(double At, NetMessage Message)> said = Timed(world, 10);

        double lastStep = said.Where(m => m.Message is ObjectMoved).Max(m => m.At);
        double fight = said.First(m => m.Message is BattleStarted).At;

        // They stand in front of you first. Arriving and attacking on the same tick is
        // the thing that made the whole encounter unreadable.
        Assert.True(fight > lastStep + 0.2, $"arrived at {lastStep:0.00}s and fought at {fight:0.00}s");
    }

    [Fact]
    public void WalkingThroughADoorMidApproachIsNotFollowed()
    {
        // The walk is towards a square, and the player is no longer standing on it.
        // Somebody left mid-stride would otherwise hold that player still forever.
        GameWorld world = World(Watcher(1, 4, 1));

        (_, ServerPlayer player) = Spotted(world);

        world.Leave(player.Id);

        Assert.Empty(Run(world, 10).OfType<BattleStarted>());
    }
}

/// <summary>
/// Ending a walk that produced no fight.
/// <para>
/// A walk almost always ends in a battle, and a battle announces itself. These are the
/// times it does not — and without a word from the server the player goes on standing
/// still, refused every step, waiting for something that is not coming. Same class of
/// bug as a conversation nobody ever ends.
/// </para>
/// </summary>
public class ApproachEndingTests
{
    private const string Route = "3.19";
    private const string Town = "3.0";

    private static MapObject Watcher(int localId, int trainerId) =>
        new(localId, 5, 4, 1, Direction.Down, 0, true, 0, 0, 0, trainerId, 4);

    private static GameWorld World(MapObject watcher, PokeMmo.Core.Data.GameRules? rules) =>
        new(
            new WorldData([
                new MapData(Route, "ROUTE 1", 8, 8, new byte[64]) { Objects = [watcher] },
                new MapData(Town, "PALLET TOWN", 8, 8, new byte[64]),
            ]),
            Route,
            rules);

    private static ServerPlayer Spotted(GameWorld world)
    {
        (ServerPlayer player, _) = world.Join(1, "Koop", new SavedCharacter(Route, 3, 5, Direction.Down, [
            new SavedMon(3, 30, null, 100, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]),
        ]));

        player.LastStepAt = double.NegativeInfinity;
        world.Move(player.Id, Direction.Right, 1000);

        Assert.NotNull(player.WatchedBy);

        return player;
    }

    private static List<NetMessage> Run(GameWorld world, double seconds = 10, double from = 1000)
    {
        var said = new List<NetMessage>();

        for (double now = from; now < from + seconds; now += 0.1)
            said.AddRange(world.Tick(now).Select(o => o.Message));

        return said;
    }

    [Fact]
    public void SomebodyWhoCannotFightSaysSoRatherThanStandingThere()
    {
        // A trainer the server has no party for. They still walk over — the sight line
        // is real — and then there is nothing to start.
        GameWorld world = World(Watcher(1, 9999), TestRules.All);

        ServerPlayer player = Spotted(world);

        List<NetMessage> said = Run(world);

        Assert.Empty(said.OfType<BattleStarted>());
        Assert.Single(said.OfType<ApproachEnded>());
        Assert.Null(player.WatchedBy);
    }

    [Fact]
    public void WalkingThroughADoorMidApproachReleasesThePlayer()
    {
        // The walk is abandoned on the route, and the player is on another map being
        // refused every step by somebody who is no longer following them.
        GameWorld world = World(Watcher(1, TestRules.OneAlone), TestRules.All);

        ServerPlayer player = Spotted(world);

        player.MapId = Town;
        player.Square = new GridPosition(1, 1);

        List<NetMessage> said = Run(world);

        Assert.Single(said.OfType<ApproachEnded>());
        Assert.Null(player.WatchedBy);
    }
}
