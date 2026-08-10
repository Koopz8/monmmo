using PokeMmo.Core.Net;
using PokeMmo.Core.World;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

public class GameWorldTests
{
    /// <summary>A 4x3 map, open except for a wall at (2, 0).</summary>
    private static GameWorld NewWorld()
    {
        var collision = new byte[12];
        collision[2] = 1;

        return new GameWorld(
            new WorldData([new MapData("3.0", "PALLET TOWN", 4, 3, collision)]),
            "3.0");
    }

    private static IEnumerable<T> MessagesOf<T>(IEnumerable<Outgoing> outgoing) where T : NetMessage =>
        outgoing.Select(o => o.Message).OfType<T>();

    [Fact]
    public void FindsItsMapByIdOrByName()
    {
        Assert.Equal("3.0", NewWorld().Map.Id);

        var byName = new GameWorld(
            new WorldData([new MapData("3.0", "PALLET TOWN", 1, 1, [0])]),
            "pallet");

        Assert.Equal("3.0", byName.Map.Id);
    }

    [Fact]
    public void RefusesToStartOnAMapItDoesNotHave()
    {
        var world = new WorldData([new MapData("3.0", "PALLET TOWN", 1, 1, [0])]);
        Assert.Throws<ArgumentException>(() => new GameWorld(world, "cinnabar"));
    }

    [Fact]
    public void WelcomesAJoiningPlayerWithTheirOwnPosition()
    {
        GameWorld world = NewWorld();
        (ServerPlayer player, List<Outgoing> send) = world.Join("Mason");

        Welcome welcome = MessagesOf<Welcome>(send).Single();

        Assert.Equal(player.Id, welcome.PlayerId);
        Assert.Equal("3.0", welcome.MapId);
        Assert.Equal(player.Square.X, welcome.X);
        Assert.Equal(1, world.PlayerCount);
    }

    [Fact]
    public void TellsANewcomerAboutEveryoneAlreadyThere()
    {
        GameWorld world = NewWorld();
        world.Join("First");
        world.Join("Second");

        (_, List<Outgoing> send) = world.Join("Third");

        // Two existing players, addressed only to the newcomer.
        List<PlayerAppeared> toNewcomer = send
            .Where(o => o.OnlyTo is not null)
            .Select(o => o.Message)
            .OfType<PlayerAppeared>()
            .ToList();

        Assert.Equal(2, toNewcomer.Count);
        Assert.Contains(toNewcomer, p => p.Name == "First");
        Assert.Contains(toNewcomer, p => p.Name == "Second");
    }

    [Fact]
    public void AnnouncesTheNewcomerToEveryoneElseButNotToThemselves()
    {
        GameWorld world = NewWorld();
        world.Join("First");

        (ServerPlayer player, List<Outgoing> send) = world.Join("Second");

        Outgoing announcement = send.Single(o =>
            o.Message is PlayerAppeared appeared && appeared.PlayerId == player.Id);

        Assert.Equal(player.Id, announcement.Except);
        Assert.Null(announcement.OnlyTo);
    }

    [Fact]
    public void PlayersJoiningTogetherDoNotShareASquare()
    {
        GameWorld world = NewWorld();

        (ServerPlayer first, _) = world.Join("First");
        (ServerPlayer second, _) = world.Join("Second");

        Assert.NotEqual(first.Square, second.Square);
    }

    [Fact]
    public void LeavingRemovesThePlayerAndTellsEveryone()
    {
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join("Mason");

        List<Outgoing> send = world.Leave(player.Id);

        Assert.Equal(player.Id, MessagesOf<PlayerLeft>(send).Single().PlayerId);
        Assert.Equal(0, world.PlayerCount);
        Assert.Null(world.Find(player.Id));
    }

    [Fact]
    public void LeavingTwiceSaysNothingTheSecondTime()
    {
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join("Mason");

        world.Leave(player.Id);
        Assert.Empty(world.Leave(player.Id));
    }

    [Fact]
    public void AcceptsAStepIntoAnOpenSquare()
    {
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join("Mason");
        player.Square = new GridPosition(0, 0);

        List<Outgoing> send = world.Move(player.Id, Direction.Right, 10);
        PlayerMoved moved = MessagesOf<PlayerMoved>(send).Single();

        Assert.Equal(1, moved.X);
        Assert.Equal(0, moved.Y);
        Assert.Equal(new GridPosition(1, 0), world.Find(player.Id)!.Square);
    }

    [Fact]
    public void BroadcastsMovementToEveryoneIncludingTheMover()
    {
        // The mover predicted this step, but everyone else has to be told, and a
        // message addressed to nobody in particular reaches all of them.
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join("Mason");

        Outgoing broadcast = world.Move(player.Id, Direction.Down, 10).Single();

        Assert.Null(broadcast.OnlyTo);
        Assert.Null(broadcast.Except);
    }

    [Fact]
    public void RefusesToWalkThroughAWallWithoutMovingThePlayer()
    {
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join("Mason");
        player.Square = new GridPosition(1, 0);

        world.Move(player.Id, Direction.Right, 10);   // (2,0) is a wall

        Assert.Equal(new GridPosition(1, 0), world.Find(player.Id)!.Square);
        Assert.Equal(Direction.Right, world.Find(player.Id)!.Facing);
    }

    [Fact]
    public void RefusesToWalkOffTheMap()
    {
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join("Mason");
        player.Square = new GridPosition(0, 0);

        world.Move(player.Id, Direction.Up, 10);

        Assert.Equal(new GridPosition(0, 0), world.Find(player.Id)!.Square);
    }

    [Fact]
    public void TurningOnTheSpotStillUpdatesFacing()
    {
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join("Mason");

        world.Move(player.Id, Direction.Right, 10);
        Assert.Equal(Direction.Right, world.Find(player.Id)!.Facing);
    }

    [Fact]
    public void RejectsStepsSentFasterThanAPlayerCouldWalk()
    {
        // The rate limit is what stops a modified client from sprinting: the honest
        // one is bounded by its own step animation.
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join("Mason");
        player.Square = new GridPosition(0, 0);

        world.Move(player.Id, Direction.Right, 10);
        List<Outgoing> tooSoon = world.Move(player.Id, Direction.Right, 10.01);

        MoveRejected rejected = MessagesOf<MoveRejected>(tooSoon).Single();

        Assert.Equal(1, rejected.X);
        Assert.Equal(new GridPosition(1, 0), world.Find(player.Id)!.Square);
    }

    [Fact]
    public void TellsOnlyTheOffenderWhenAMoveIsRejected()
    {
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join("Mason");

        world.Move(player.Id, Direction.Right, 10);
        Outgoing rejection = world.Move(player.Id, Direction.Right, 10.01).Single();

        Assert.Equal(player.Id, rejection.OnlyTo);
    }

    [Fact]
    public void AcceptsStepsAtTheNormalWalkingPace()
    {
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join("Mason");
        player.Square = new GridPosition(0, 0);
        player.LastStepAt = double.NegativeInfinity;

        double now = 10;

        for (int i = 0; i < 3; i++)
        {
            world.Move(player.Id, Direction.Right, now);
            now += WalkingCharacter.StepSeconds;
        }

        // Three steps from x=0, with a wall at x=2, leaves the player against it.
        Assert.Equal(new GridPosition(1, 0), world.Find(player.Id)!.Square);
    }

    [Fact]
    public void IgnoresMovesFromSomebodyWhoIsNotInTheWorld()
    {
        GameWorld world = NewWorld();
        List<Outgoing> send = world.Move(999, Direction.Up, 10);

        Assert.Single(MessagesOf<Rejected>(send));
    }

    [Theory]
    [InlineData("", "Player")]
    [InlineData("   ", "Player")]
    [InlineData("Mason", "Mason")]
    [InlineData("a really quite long name indeed", "a really quite l")]
    public void CleansUpNamesThatCameFromAClient(string given, string expected)
    {
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join(given);

        Assert.Equal(expected, player.Name);
    }

    [Fact]
    public void StripsControlCharactersFromNames()
    {
        GameWorld world = NewWorld();
        (ServerPlayer player, _) = world.Join("Ma so\nn");

        Assert.Equal("Mason", player.Name);
    }
}

/// <summary>
/// The rule the whole project is built around, enforced rather than remembered.
/// </summary>
public class ServerBoundaryTests
{
    [Fact]
    public void TheServerDoesNotReferenceTheCartridgeExtractor()
    {
        // The server learns the world from a collision-only file an operator exports.
        // If it ever links the extractor, cartridge reading has moved server-side and
        // the posture the project depends on is gone. A comment would not have caught
        // that; this does.
        IEnumerable<string?> referenced = typeof(GameWorld).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name);

        Assert.DoesNotContain("RomExtract", referenced);
    }

    [Fact]
    public void TheServerDoesReferenceTheSharedCore()
    {
        // The other half of the rule: server and client must share movement and
        // protocol code, or their answers can drift apart.
        IEnumerable<string?> referenced = typeof(GameWorld).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name);

        Assert.Contains("Core", referenced);
    }
}

/// <summary>Encounters as the server decides them.</summary>
public class ServerEncounterTests
{
    /// <summary>A 4x3 map, entirely open, with grass along the bottom row.</summary>
    private static GameWorld GrassyWorld(int rate = 100, uint seed = 1)
    {
        var behaviours = new byte[12];
        for (int x = 0; x < 4; x++) behaviours[2 * 4 + x] = MetatileBehaviour.TallGrass;

        var map = new MapData("3.19", "ROUTE 1", 4, 3, new byte[12])
        {
            Behaviours = behaviours,
            Encounters = new MapEncounters("3.19", Land: new EncounterTable(
                EncounterKind.Land,
                rate,
                Enumerable.Range(0, 12).Select(i => new WildSlot(16, 2, 5)).ToList())),
        };

        return new GameWorld(new WorldData([map]), "3.19", seed);
    }

    private static ServerPlayer PlayerAt(GameWorld world, int x, int y)
    {
        (ServerPlayer player, _) = world.Join("Mason");
        player.Square = new GridPosition(x, y);
        player.LastStepAt = double.NegativeInfinity;
        return player;
    }

    /// <summary>
    /// Steps into the grass repeatedly until something appears. Even at the highest
    /// rate an encounter is a coin flip per step, so a single-step assertion would be
    /// flaky by construction.
    /// </summary>
    private static (WildEncounterStarted? Encounter, Outgoing? Message) WalkUntilEncounter(
        GameWorld world, ServerPlayer player, int maxSteps = 40)
    {
        double now = 0;

        for (int step = 0; step < maxSteps; step++)
        {
            player.Square = new GridPosition(step % 4, 1);
            player.LastStepAt = double.NegativeInfinity;
            now += 1;

            foreach (Outgoing outgoing in world.Move(player.Id, Direction.Down, now))
                if (outgoing.Message is WildEncounterStarted encounter) return (encounter, outgoing);
        }

        return (null, null);
    }

    [Fact]
    public void SteppingIntoGrassCanStartAnEncounter()
    {
        GameWorld world = GrassyWorld();
        ServerPlayer player = PlayerAt(world, 0, 1);

        (WildEncounterStarted? encounter, _) = WalkUntilEncounter(world, player);

        Assert.NotNull(encounter);
        Assert.Equal(16, encounter!.Species);
        Assert.InRange(encounter.Level, 2, 5);
    }

    [Fact]
    public void AnEncounterIsToldOnlyToThePlayerWhoWalkedIntoIt()
    {
        GameWorld world = GrassyWorld();
        ServerPlayer player = PlayerAt(world, 0, 1);

        (_, Outgoing? message) = WalkUntilEncounter(world, player);

        Assert.NotNull(message);
        Assert.Equal(player.Id, message!.OnlyTo);
    }

    [Fact]
    public void WalkingOnOrdinaryGroundMeetsNothing()
    {
        GameWorld world = GrassyWorld();
        ServerPlayer player = PlayerAt(world, 0, 0);

        // Moving along the top row, which carries no grass, however many steps.
        double now = 0;

        for (int step = 0; step < 200; step++)
        {
            player.Square = new GridPosition(0, 0);
            player.LastStepAt = double.NegativeInfinity;
            now += 1;

            Assert.DoesNotContain(world.Move(player.Id, Direction.Right, now),
                o => o.Message is WildEncounterStarted);
        }
    }

    [Fact]
    public void AMapWithoutEncounterTablesNeverStartsOne()
    {
        var behaviours = new byte[4];
        behaviours[0] = MetatileBehaviour.TallGrass;

        var map = new MapData("3.0", "PALLET TOWN", 2, 2, new byte[4]) { Behaviours = behaviours };
        var world = new GameWorld(new WorldData([map]), "3.0");

        (ServerPlayer player, _) = world.Join("Mason");
        player.Square = new GridPosition(1, 0);
        player.LastStepAt = double.NegativeInfinity;

        Assert.DoesNotContain(world.Move(player.Id, Direction.Left, 10),
            o => o.Message is WildEncounterStarted);
    }

    [Fact]
    public void ARateOfZeroMeansGrassIsJustGrass()
    {
        GameWorld world = GrassyWorld(rate: 0);
        ServerPlayer player = PlayerAt(world, 0, 1);

        (WildEncounterStarted? encounter, _) = WalkUntilEncounter(world, player, maxSteps: 200);

        Assert.Null(encounter);
    }

    [Fact]
    public void TheEncounterSeedTravelsWithTheMessage()
    {
        // The client resolves the battle itself from this seed, so it has to arrive.
        GameWorld world = GrassyWorld();
        ServerPlayer player = PlayerAt(world, 0, 1);

        (WildEncounterStarted? encounter, _) = WalkUntilEncounter(world, player);

        Assert.NotNull(encounter);
        Assert.NotEqual(0u, encounter!.Seed);
    }

    [Fact]
    public void TheSameServerSeedProducesTheSameEncounters()
    {
        static List<string> Walk(uint seed)
        {
            GameWorld world = GrassyWorld(rate: 30, seed: seed);
            (ServerPlayer player, _) = world.Join("Mason");

            var met = new List<string>();
            double now = 0;

            for (int step = 0; step < 60; step++)
            {
                player.Square = new GridPosition(step % 4, 1);
                player.LastStepAt = double.NegativeInfinity;
                now += 1;

                foreach (Outgoing outgoing in world.Move(player.Id, Direction.Down, now))
                    if (outgoing.Message is WildEncounterStarted e) met.Add($"{e.Species}@{e.Level}");
            }

            return met;
        }

        Assert.Equal(Walk(1234), Walk(1234));
    }

    [Fact]
    public void APlayerIsMarkedAsBattlingWhenSomethingAppears()
    {
        GameWorld world = GrassyWorld();
        ServerPlayer player = PlayerAt(world, 0, 1);

        WalkUntilEncounter(world, player);

        Assert.True(world.Find(player.Id)!.InBattle);
    }
}
