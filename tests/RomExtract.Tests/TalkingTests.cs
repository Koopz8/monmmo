using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Deciding who a player is talking to.
/// <para>
/// This is shared code rather than client code on purpose. The client decides whether
/// pressing the button opens a box; the server decides whether that person is close
/// enough to hold still. Two answers to the same question is how you get a
/// conversation with somebody who is not there.
/// </para>
/// </summary>
public class FacingTests
{
    private static MapObject Somebody(int localId, int x, int y) =>
        new(localId, 5, x, y, Direction.Down, 0, false);

    [Fact]
    public void TheSquareInFrontIsTheOneThatCounts()
    {
        MapObject[] people = [Somebody(1, 3, 2), Somebody(2, 5, 5)];

        Assert.Equal(1, Interaction.InFrontOf(new GridPosition(3, 3), Direction.Up, people)?.LocalId);
    }

    [Fact]
    public void FacingAwayFromSomebodyIsNotTalkingToThem()
    {
        MapObject[] people = [Somebody(1, 3, 2)];

        Assert.Null(Interaction.InFrontOf(new GridPosition(3, 3), Direction.Down, people));
    }

    [Fact]
    public void StandingOnSomebodyIsNotTalkingToThemEither()
    {
        // Not reachable in a world with collision, but the rule is "the square in
        // front", and a version written as "within one square" would answer differently.
        MapObject[] people = [Somebody(1, 3, 3)];

        Assert.Null(Interaction.InFrontOf(new GridPosition(3, 3), Direction.Up, people));
    }

    [Fact]
    public void WhereTheyAreNowBeatsWhereTheCartridgePutThem()
    {
        // The whole reason this takes a second argument. After a few seconds of
        // wandering, the placement in the image is a fact about a file rather than
        // about the world, and reading from it has players talking to empty squares.
        MapObject[] people = [Somebody(1, 3, 2)];

        var live = new Dictionary<int, GridPosition> { [1] = new(4, 3) };

        Assert.Null(Interaction.InFrontOf(new GridPosition(3, 3), Direction.Up, people, live));
        Assert.Equal(1, Interaction.InFrontOf(new GridPosition(3, 3), Direction.Right, people, live)?.LocalId);
    }

    [Fact]
    public void SomebodyNotYetHeardAboutIsWhereTheCartridgeSaid()
    {
        // A player can press the button in the moment between arriving on a map and the
        // server saying who is on it. Falling back to the placement is right, because
        // that is exactly where everybody is until the first tick moves them.
        MapObject[] people = [Somebody(1, 3, 2)];

        Assert.Equal(
            1,
            Interaction.InFrontOf(new GridPosition(3, 3), Direction.Up, people, new Dictionary<int, GridPosition>())
                ?.LocalId);
    }

    [Fact]
    public void EverybodyTurnsToFaceYou()
    {
        foreach (Direction facing in Enum.GetValues<Direction>())
        {
            Direction turned = Interaction.Opposite(facing);

            Assert.NotEqual(facing, turned);
            Assert.Equal(facing, Interaction.Opposite(turned));
        }
    }
}

/// <summary>
/// What the server does about a conversation, which is only ever one thing: hold the
/// person still. It has no cartridge and so has no idea what anybody says.
/// </summary>
public class TalkingTests
{
    private const string Town = "3.0";

    private static GameWorld World(params MapObject[] people)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = people };

        return new GameWorld(new WorldData([map]), Town, TestRules.All);
    }

    private static ServerPlayer Join(GameWorld world, int id, int x, int y, Direction facing)
    {
        (ServerPlayer player, _) = world.Join(
            id, $"Player{id}", new SavedCharacter(Town, x, y, facing, []));

        return player;
    }

    private static MapObject Wanderer(int localId, int x, int y) =>
        new(localId, 5, x, y, Direction.Down, 2, false, 3, 3);

    /// <summary>Runs the clock forward and collects everything that moved.</summary>
    private static List<ObjectMoved> Run(GameWorld world, double seconds, double from = 0)
    {
        var moved = new List<ObjectMoved>();

        for (double now = from + 0.2; now <= from + seconds; now += 0.2)
            moved.AddRange(world.Tick(now).Select(o => o.Message).OfType<ObjectMoved>());

        return moved;
    }

    [Fact]
    public void SomebodyBeingTalkedToStandsStill()
    {
        // Without this they wander off every second or so, which over a conversation of
        // three pages means finishing a sentence from the other side of the street.
        GameWorld world = World(Wanderer(1, 3, 3));
        ServerPlayer player = Join(world, 1, 3, 4, Direction.Up);

        world.StartTalking(player.Id, 1);

        Assert.Empty(Run(world, 60).Where(m => (m.X, m.Y) != (3, 3)));
    }

    [Fact]
    public void TheyCarryOnOnceYouAreDone()
    {
        GameWorld world = World(Wanderer(1, 3, 3));
        ServerPlayer player = Join(world, 1, 3, 4, Direction.Up);

        world.StartTalking(player.Id, 1);
        world.StopTalking(player.Id);

        Assert.NotEmpty(Run(world, 60));
    }

    [Fact]
    public void TheyTurnToLookAtYou()
    {
        GameWorld world = World(new MapObject(1, 5, 3, 3, Direction.Down, 0, false));
        ServerPlayer player = Join(world, 1, 3, 2, Direction.Down);

        List<Outgoing> send = world.StartTalking(player.Id, 1);

        ObjectMoved turned = send.Select(o => o.Message).OfType<ObjectMoved>().Single();

        Assert.Equal(Direction.Up, turned.Facing);
        Assert.Equal((3, 3), (turned.X, turned.Y));
    }

    [Fact]
    public void TalkingToSomebodyAcrossTheMapDoesNothing()
    {
        // Not anti-cheat — there is nothing to cheat at yet. It is what stops the hold
        // from being a way to freeze anybody on the map from anywhere on it.
        GameWorld world = World(Wanderer(1, 6, 6));
        ServerPlayer player = Join(world, 1, 0, 0, Direction.Down);

        Assert.Empty(world.StartTalking(player.Id, 1));
        Assert.Null(world.TalkingTo(player.Id));
        Assert.NotEmpty(Run(world, 60));
    }

    [Fact]
    public void FacingTheWrongWayIsNotTalking()
    {
        GameWorld world = World(Wanderer(1, 3, 3));
        ServerPlayer player = Join(world, 1, 3, 4, Direction.Down);

        world.StartTalking(player.Id, 1);

        Assert.Null(world.TalkingTo(player.Id));
    }

    [Fact]
    public void HangingUpByDisconnectingReleasesThem()
    {
        // The client says when a box closes. It does not, and cannot, say anything when
        // the window is closed instead — so the release has to be able to happen without
        // being asked for, or somebody stands to attention forever.
        GameWorld world = World(Wanderer(1, 3, 3));
        ServerPlayer talker = Join(world, 1, 3, 4, Direction.Up);

        world.StartTalking(talker.Id, 1);

        // Somebody else has to stay, or the map stops being simulated at all and this
        // would pass whether or not the hold was released.
        Join(world, 2, 0, 0, Direction.Down);
        world.Leave(talker.Id);

        Assert.NotEmpty(Run(world, 60));
    }

    [Fact]
    public void OnlyTheOneTalkingCanEndIt()
    {
        GameWorld world = World(Wanderer(1, 3, 3));
        ServerPlayer talker = Join(world, 1, 3, 4, Direction.Up);
        ServerPlayer other = Join(world, 2, 3, 2, Direction.Down);

        world.StartTalking(talker.Id, 1);
        world.StopTalking(other.Id);

        Assert.Equal(1, world.TalkingTo(talker.Id));
        Assert.Empty(Run(world, 60).Where(m => (m.X, m.Y) != (3, 3)));
    }

    [Fact]
    public void NobodyCollectsFrozenPeopleBehindThem()
    {
        // A client that loses a "finished" message and starts a second conversation
        // would otherwise leave the first person held for the rest of the session.
        GameWorld world = World(
            new MapObject(1, 5, 3, 3, Direction.Down, 2, false, 3, 3),
            new MapObject(2, 5, 3, 5, Direction.Down, 2, false, 3, 3));

        ServerPlayer player = Join(world, 1, 3, 4, Direction.Up);

        world.StartTalking(player.Id, 1);

        player.Facing = Direction.Down;
        world.StartTalking(player.Id, 2);

        Assert.Equal(2, world.TalkingTo(player.Id));
    }
}

/// <summary>
/// Whether the server ends up looking the same way the player does.
/// <para>
/// The client predicts its own movement and tells the server what it did. It told the
/// server about steps. It did not tell it about turns — and a turn on the spot is what
/// walking into a wall does, and a person is a wall as far as the grid is concerned. So
/// the last thing anybody does before speaking to somebody was the one thing the server
/// never heard about, and it went on answering "who is in front of this player" from
/// whichever way they last walked.
/// </para>
/// <para>
/// Fifth time this project has enforced a rule on one side of the split and left the
/// other side unaware of it. The first four were map edges, NPC collision, doors and
/// counters. This one runs the client's own character against a real world and checks
/// the two agree, rather than checking each side against itself.
/// </para>
/// </summary>
public class TurningTests
{
    private const string Town = "3.0";

    /// <summary>An 8x8 town with a wall at (2, 2), shared by both sides.</summary>
    private static byte[] Walls()
    {
        var collision = new byte[64];
        collision[2 * 8 + 2] = 1;
        return collision;
    }

    private static GameWorld World(byte[] collision) =>
        new(new WorldData([new MapData(Town, "PALLET TOWN", 8, 8, collision)]), Town, TestRules.All);

    /// <summary>
    /// One press, played through the client's character and forwarded to the server
    /// exactly the way the render loop forwards it — from <c>ToReport</c>, and from
    /// nothing else.
    /// </summary>
    private static void Press(
        GameWorld world, int playerId, WalkingCharacter character, Direction press, ref double now)
    {
        for (int frame = 0; frame < 30; frame++)
        {
            character.Update(1f / 60f, frame == 0 ? press : null);

            if (character.ToReport is { } report)
            {
                now += 1.0;
                world.Move(playerId, report, now);
            }

            if (!character.IsStepping) return;
        }
    }

    [Fact]
    public void TheServerLooksTheSameWayThePlayerDoes()
    {
        byte[] collision = Walls();
        GameWorld world = World(collision);

        (ServerPlayer player, _) = world.Join(
            1, "Koop", new SavedCharacter(Town, 1, 1, Direction.Down, []));

        var character = new WalkingCharacter();
        character.Place(new CollisionGrid(8, 8, collision), new GridPosition(1, 1));

        double now = 0;

        // Walk one square right, then press into the wall — which turns without moving.
        Press(world, player.Id, character, Direction.Right, ref now);
        Press(world, player.Id, character, Direction.Down, ref now);

        Assert.Equal(new GridPosition(2, 1), character.Square);
        Assert.Equal(character.Square, player.Square);
        Assert.Equal(Direction.Down, character.Facing);
        Assert.Equal(character.Facing, player.Facing);
    }

    [Fact]
    public void TurningTowardsSomebodyIsEnoughToTalkToThem()
    {
        // The bug as it was actually met: a player standing beside somebody, turned to
        // face them, pressed the button, and the server refused a conversation with a
        // person it agreed was standing right there.
        //
        // Pressing towards them is the whole input. A person is solid, so the press
        // turns without stepping — and a turn was the one thing the client kept to
        // itself, which left the server sure this player was still looking down.
        byte[] collision = Walls();

        MapData map = new(Town, "PALLET TOWN", 8, 8, collision)
        {
            Objects = [new MapObject(1, 5, 4, 5, Direction.Down, 0, false)],
        };

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer player, _) = world.Join(
            1, "Koop", new SavedCharacter(Town, 3, 5, Direction.Down, []));

        var character = new WalkingCharacter();

        // The grid the client predicts against has people in it, which is what makes
        // walking towards one a turn rather than a step.
        character.Place(
            new CollisionGrid(8, 8, collision).With([new GridPosition(4, 5)]),
            new GridPosition(3, 5));

        double now = 0;

        Press(world, player.Id, character, Direction.Right, ref now);

        Assert.Equal(new GridPosition(3, 5), character.Square);
        Assert.Equal(Direction.Right, character.Facing);

        world.StartTalking(player.Id, 1);

        Assert.Equal(1, world.TalkingTo(player.Id));
    }

    [Fact]
    public void ATurnIsNotRefusedForBeingTooQuick()
    {
        // The interval exists to stop somebody walking faster than the game allows. A
        // turn moves nobody, and the turn it would refuse is the one immediately after
        // arriving — which is every turn a player makes on the way to speaking to
        // somebody.
        GameWorld world = World(Walls());

        (ServerPlayer player, _) = world.Join(
            1, "Koop", new SavedCharacter(Town, 1, 1, Direction.Down, []));

        world.Move(player.Id, Direction.Right, 10);

        List<Outgoing> send = world.Move(player.Id, Direction.Down, 10.001);

        Assert.Empty(send.Select(o => o.Message).OfType<MoveRejected>());
        Assert.Equal(Direction.Down, player.Facing);
    }

    [Fact]
    public void FacingTheSameWayTwiceSaysNothingToAnybody()
    {
        // A client only reports changes, but a server that rebroadcast every repeat
        // would hand anyone who does not one megaphone per frame.
        GameWorld world = World(Walls());

        (ServerPlayer player, _) = world.Join(
            1, "Koop", new SavedCharacter(Town, 2, 1, Direction.Down, []));

        Assert.Empty(world.Move(player.Id, Direction.Down, 10));
    }
}

/// <summary>
/// The flags a save carries, and the one the server sets on its own.
/// <para>
/// The server cannot run a script — the bytes are on a cartridge it has never seen — so
/// what it knows about flags is what it was told and what it stores. The exception is
/// the flag that says a trainer has been beaten, because beating somebody is decided
/// here and nowhere else.
/// </para>
/// </summary>
public class ScriptFlagTests
{
    private const string Town = "3.0";

    private static GameWorld World(params MapObject[] people)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = people };

        return new GameWorld(new WorldData([map]), Town, TestRules.All);
    }

    private static MapObject Trainer(int localId, int trainerId) =>
        new(localId, 5, 3, 3, Direction.Down, 0, true, 0, 0, 0, trainerId, 1);

    private static Welcome WelcomeFor(GameWorld world, SavedCharacter saved)
    {
        (_, List<Outgoing> send) = world.Join(1, "Koop", saved);

        return send.Select(o => o.Message).OfType<Welcome>().Single();
    }

    [Fact]
    public void TheSaveHandsBackWhatItWasGiven()
    {
        Welcome welcome = WelcomeFor(
            World(),
            SavedCharacter.Fresh(Town, 1, 1) with
            {
                Flags = [0x2A5],
                Variables = [new SavedVariable(0x4001, 3)],
            });

        Assert.Equal([0x2A5], welcome.Flags);
        Assert.Equal(3, welcome.Variables.Single(v => v.Id == 0x4001).Value);
    }

    [Fact]
    public void TheClientIsToldWhoThisCharacterHasBeaten()
    {
        // Not a flag. Running a trainer's script needs to know whether the fight has
        // happened, and the id is the only name for it either side has — the word in the
        // command that looked like a flag is zero for every trainer on the cartridge.
        Welcome welcome = WelcomeFor(
            World(Trainer(1, 41)),
            SavedCharacter.Fresh(Town, 1, 1) with { DefeatedTrainers = [41] });

        Assert.Equal([41], welcome.Beaten);
        Assert.Empty(welcome.Flags);
    }

    [Fact]
    public void WhatAClientSaysAScriptDidIsKept()
    {
        GameWorld world = World();

        (ServerPlayer player, _) = world.Join(1, "Koop", SavedCharacter.Fresh(Town, 1, 1) with { Flags = [0x828] });

        world.RunScript(player.Id, new ScriptRan([0x2A5], [0x828], [new SavedVariable(0x4001, 3)]));

        SavedCharacter saved = world.Snapshot(player.Id)!;

        Assert.Equal([0x2A5], saved.Flags);
        Assert.Equal(3, saved.Variables.Single(v => v.Id == 0x4001).Value);
    }
}

/// <summary>
/// The counter that heals, and how the server comes to know which one it is.
/// <para>
/// It does not work it out. What heals a party is a routine in the game's own code,
/// which is not data and cannot be read from an image — three rounds went into
/// establishing that, and it is a boundary rather than a gap. The world file carries a
/// flag instead, put there at export by counting who calls what.
/// </para>
/// </summary>
public class HealingTests
{
    private const string Town = "3.0";

    private static MapObject Nurse(int localId) =>
        new(localId, 5, 3, 3, Direction.Down, 0, false) { Heals = true };

    private static GameWorld World(params MapObject[] people)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = people };

        return new GameWorld(new WorldData([map]), Town, TestRules.All);
    }

    private static (GameWorld World, ServerPlayer Player) AtTheCounter(SavedMon party)
    {
        GameWorld world = World(Nurse(1));

        (ServerPlayer player, _) = world.Join(
            1, "Koop", SavedCharacter.Fresh(Town, 3, 4) with { Party = [party] });

        player.Facing = Direction.Up;

        return (world, player);
    }

    private static SavedMon Wounded =>
        new(3, 30, null, 1, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

    [Fact]
    public void TalkingToOneStandsThePartyBackUp()
    {
        (GameWorld world, ServerPlayer player) = AtTheCounter(Wounded);

        PartyHealed healed = world.StartTalking(player.Id, 1)
            .Select(o => o.Message)
            .OfType<PartyHealed>()
            .Single();

        Assert.True(healed.Needed);
        Assert.True(healed.Party[0].CurrentHp > 1);
        Assert.Equal(healed.Party[0].CurrentHp, player.Party[0].CurrentHp);
    }

    [Fact]
    public void WalkingInHealthyIsNotReportedAsAMiracle()
    {
        // A centre that announces a recovery to somebody who did not need one is the
        // kind of small lie that makes a player stop believing the rest of it.
        (GameWorld world, ServerPlayer player) = AtTheCounter(Wounded);

        world.StartTalking(player.Id, 1);
        world.StopTalking(player.Id);

        PartyHealed again = world.StartTalking(player.Id, 1)
            .Select(o => o.Message)
            .OfType<PartyHealed>()
            .Single();

        Assert.False(again.Needed);
    }

    [Fact]
    public void SomebodyPoisonedNeededItEvenAtFullHealth()
    {
        // Whether a party is well is not whether it can fight. Something on full health
        // with a burn can fight and very much wants a centre.
        GameWorld world = World(Nurse(1));

        (ServerPlayer player, _) = world.Join(
            1,
            "Koop",
            SavedCharacter.Fresh(Town, 3, 4) with
            {
                Party = [new SavedMon(3, 5, null, 999, StatusCondition.Poison, Nature.Hardy, [TestRules.FirstMove])],
            });

        player.Facing = Direction.Up;

        PartyHealed healed = world.StartTalking(player.Id, 1)
            .Select(o => o.Message)
            .OfType<PartyHealed>()
            .Single();

        Assert.True(healed.Needed);
        Assert.Equal(StatusCondition.None, player.Party[0].Status);
    }

    [Fact]
    public void SomebodyWhoIsNotANurseHealsNobody()
    {
        GameWorld world = World(new MapObject(1, 5, 3, 3, Direction.Down, 0, false));

        (ServerPlayer player, _) = world.Join(
            1, "Koop", SavedCharacter.Fresh(Town, 3, 4) with { Party = [Wounded] });

        player.Facing = Direction.Up;

        Assert.Empty(world.StartTalking(player.Id, 1).Select(o => o.Message).OfType<PartyHealed>());
        Assert.Equal(1, player.Party[0].CurrentHp);
    }
}

/// <summary>
/// Losing, which until now cost nothing at all.
/// <para>
/// Healing on the spot was a stand-in from before there was anywhere to wake up, and it
/// outlived its usefulness the moment there was: a loss that costs nothing makes a
/// centre a place with no reason to exist and every potion in the bag a souvenir.
/// </para>
/// </summary>
public class BlackingOutTests
{
    private const string Town = "3.0";
    private const string Route = "3.19";

    private static GameWorld World()
    {
        MapData town = new(Town, "PALLET TOWN", 8, 8, new byte[64])
        {
            Objects = [new MapObject(1, 5, 3, 3, Direction.Down, 0, false) { Heals = true }],
        };

        // Grass everywhere and something waiting in all of it, so a fight can be picked
        // on demand rather than walked into and hoped for.
        var behaviours = new byte[64];
        Array.Fill(behaviours, MetatileBehaviour.TallGrass);

        MapData route = new(Route, "ROUTE 1", 8, 8, new byte[64])
        {
            Behaviours = behaviours,
            Encounters = new MapEncounters(Route, Land: new EncounterTable(
                EncounterKind.Land, 100,
                [.. Enumerable.Range(0, 12).Select(_ => new WildSlot(16, 40, 40))])),
        };

        return new GameWorld(new WorldData([town, route]), Town, TestRules.All);
    }

    private static SavedMon Wounded =>
        new(3, 30, null, 1, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

    private static (GameWorld World, ServerPlayer Player) Playing()
    {
        GameWorld world = World();

        (ServerPlayer player, _) = world.Join(
            1, "Koop", SavedCharacter.Fresh(Town, 3, 4) with { Party = [Wounded], Money = 3000 });

        player.Facing = Direction.Up;

        return (world, player);
    }

    /// <summary>
    /// Walks into a fight on the route and loses it, by going in on one health against
    /// something forty levels above.
    /// </summary>
    private static List<Outgoing> LoseAFight(GameWorld world, ServerPlayer player)
    {
        player.MapId = Route;
        player.Party[0] = player.Party[0] with { CurrentHp = 1 };

        double now = 0;

        for (int step = 0; step < 200; step++)
        {
            player.Square = new GridPosition(step % 4, 1);
            player.LastStepAt = double.NegativeInfinity;
            now += 1;

            if (world.Move(player.Id, Direction.Down, now).Any(o => o.Message is BattleStarted)) break;
        }

        Assert.True(player.InBattle, "never met anything");

        List<Outgoing> last = [];

        for (int turn = 0; turn < 40 && player.InBattle; turn++)
            last = world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0));

        return last;
    }

    [Fact]
    public void RestingAtACounterIsWhatMakesItYours()
    {
        // Walking through a centre on the way somewhere does not count. What makes one
        // yours is having stood at the counter.
        (GameWorld world, ServerPlayer player) = Playing();

        Assert.Null(player.RestingAt);

        world.StartTalking(player.Id, 1);

        Assert.Equal(Town, player.RestingAt);
        Assert.Equal(new GridPosition(3, 4), player.RestingSquare);
    }

    [Fact]
    public void SomebodyWhoHasNeverRestedWakesWhereEverybodyStarts()
    {
        // Not a fallback for an error. It is where they started, and it is the only
        // place the server knows is safe.
        (GameWorld world, ServerPlayer player) = Playing();

        LoseAFight(world, player);

        Assert.Null(player.RestingAt);
        Assert.Equal(Town, player.MapId);
    }

    [Fact]
    public void LosingCostsHalfTheMoney()
    {
        (GameWorld world, ServerPlayer player) = Playing();

        world.StartTalking(player.Id, 1);
        world.StopTalking(player.Id);

        List<Outgoing> end = LoseAFight(world, player);

        BlackedOut fainted = end.Select(o => o.Message).OfType<BlackedOut>().Single();

        Assert.Equal(1500, fainted.Money);
        Assert.Equal(1500, player.Money);
    }

    [Fact]
    public void LosingPutsYouBackAtTheCounterYouRestedAt()
    {
        (GameWorld world, ServerPlayer player) = Playing();

        world.StartTalking(player.Id, 1);
        world.StopTalking(player.Id);

        LoseAFight(world, player);

        Assert.Equal(Town, player.MapId);
        Assert.Equal(new GridPosition(3, 4), player.Square);
        Assert.All(player.Party, m => Assert.True(m.CurrentHp > 1));
    }

    [Fact]
    public void WhereYouRestedOutlivesTheConnection()
    {
        (GameWorld world, ServerPlayer player) = Playing();

        world.StartTalking(player.Id, 1);

        SavedCharacter saved = world.Snapshot(player.Id)!;

        Assert.Equal(Town, saved.RestingAt);
        Assert.Equal((3, 4), (saved.RestingX, saved.RestingY));
    }
}

/// <summary>
/// Picking things up off the ground.
/// <para>
/// A ball lying on a route is a person with a script, and that script writes an item id
/// and a count into two argument variables before calling a standard routine to do the
/// giving. This project has never followed one of those routines — the table is
/// code-referenced and was never located — so a hundred and seventy-three of them ran to
/// a clean end and produced nothing at all. Following the routine was never needed: both
/// numbers are written down in front of the call.
/// </para>
/// </summary>
public class PickingThingsUpTests
{
    private const string Town = "3.0";

    private static MapObject Ball(int localId, int itemId, int count = 1) =>
        new(localId, 5, 3, 3, Direction.Down, 0, false) { GivesItemId = itemId, GivesCount = count };

    private static (GameWorld World, ServerPlayer Player) Standing(params MapObject[] people)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Objects = people };

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Koop", SavedCharacter.Fresh(Town, 3, 4));

        player.Facing = Direction.Up;

        return (world, player);
    }

    [Fact]
    public void WhatIsOnTheGroundGoesInTheBag()
    {
        (GameWorld world, ServerPlayer player) = Standing(Ball(1, TestRules.PotionItem, 2));

        ItemFound found = world.StartTalking(player.Id, 1).Select(o => o.Message).OfType<ItemFound>().Single();

        Assert.Equal(TestRules.PotionItem, found.ItemId);
        Assert.Equal(2, found.Count);
        Assert.Equal(2, player.Bag.CountOf(TestRules.PotionItem));
    }

    [Fact]
    public void APickedUpBallIsNotThereTwice()
    {
        // The one thing that must not happen. A ball whose script runs every time it is
        // spoken to is an unlimited supply of whatever is in it.
        (GameWorld world, ServerPlayer player) = Standing(Ball(1, TestRules.PotionItem));

        world.StartTalking(player.Id, 1);
        world.StopTalking(player.Id);

        Assert.Empty(world.StartTalking(player.Id, 1));
        Assert.Equal(1, player.Bag.CountOf(TestRules.PotionItem));
    }

    [Fact]
    public void WhatHasBeenPickedUpOutlivesTheConnection()
    {
        (GameWorld world, ServerPlayer player) = Standing(Ball(1, TestRules.PotionItem));

        world.StartTalking(player.Id, 1);

        Assert.Equal([$"{Town}:1"], world.Snapshot(player.Id)!.ItemsTaken);
    }

    [Fact]
    public void ABallIsNotHeldStillToBeSpokenTo()
    {
        // There is nobody there. Holding it would be holding a ball to attention, and
        // the release only ever comes from a text box that was never opened.
        (GameWorld world, ServerPlayer player) = Standing(Ball(1, TestRules.PotionItem));

        world.StartTalking(player.Id, 1);

        Assert.Null(world.TalkingTo(player.Id));
    }
}
