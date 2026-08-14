using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Water, and getting onto it.
/// <para>
/// The claim being tested is not "surfing works" but the pair of rules underneath it,
/// which point in opposite directions. Water is a wall to somebody on foot — and it was
/// never a wall before, because the block data does not make it one; two thirds of the
/// water in this game is passable and the cartridge keeps people out of it with a rule
/// about the behaviour byte. And water is floor to somebody on it, while the land stays
/// exactly as it was, which is what makes stepping ashore the way off.
/// </para>
/// </summary>
public class SurfTests
{
    private const string Pond = "3.30";

    /// <summary>
    /// A map with a lake down the middle of it, passable in the block data.
    /// <para>
    /// Passable on purpose. A fixture whose water was already solid would pass this
    /// whole file with the feature deleted.
    /// </para>
    /// </summary>
    private static MapData Lake(bool withFish = true)
    {
        const int width = 8;
        const int height = 6;

        var behaviours = new byte[width * height];
        var collision = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 3; x < 6; x++) behaviours[y * width + x] = MetatileBehaviour.Water;
        }

        MapData map = new(Pond, "THE LAKE", width, height, collision)
        {
            Behaviours = behaviours,
        };

        return withFish
            ? map with
            {
                Encounters = new MapEncounters(
                    Pond,
                    Water: new EncounterTable(
                        EncounterKind.Water,
                        100,
                        Enumerable.Range(0, 5).Select(_ => new WildSlot(16, 10, 10)).ToList())),
            }
            : map;
    }

    private static GameWorld World(bool withFish = true) =>
        new(new WorldData([Lake(withFish)]), Pond, TestRules.All, 1);

    private static SavedMon Swimmer() =>
        new(1, 20, null, 60, StatusCondition.None, Nature.Hardy, [TestRules.SurfMove], 0);

    private static ServerPlayer Standing(GameWorld world, bool canSurf = true)
    {
        (ServerPlayer player, _) = world.Join(
            1, "Mason", world.FreshCharacter() with { Party = canSurf ? [Swimmer()] : [] });

        player.Square = new GridPosition(2, 2);
        player.Facing = Direction.Right;
        player.LastStepAt = double.NegativeInfinity;

        return player;
    }

    [Fact]
    public void TheSeaIsNotAWallInTheBlockDataAndHasToBeMadeOne()
    {
        // The whole reason this exists. Nothing in the collision data stops anybody
        // walking out onto the water, and on 47 maps of this cartridge nothing did.
        MapData lake = Lake();

        Assert.True(new CollisionGrid(lake.Width, lake.Height, lake.Collision)
            .IsWalkable(new GridPosition(4, 2)));

        Assert.False(lake.ToGrid(surfing: false).IsWalkable(new GridPosition(4, 2)));
        Assert.True(lake.ToGrid(surfing: true).IsWalkable(new GridPosition(4, 2)));
    }

    [Fact]
    public void TheLandIsStillThereWhileYouAreOnTheWater()
    {
        // Which is what makes getting off possible at all. A grid that opened the water
        // and closed the land would leave somebody afloat for good.
        Assert.True(Lake().ToGrid(surfing: true).IsWalkable(new GridPosition(2, 2)));
    }

    [Fact]
    public void WalkingIntoWaterIsRefused()
    {
        GameWorld world = World();
        ServerPlayer player = Standing(world);

        world.Move(player.Id, Direction.Right, 1);

        Assert.Equal(new GridPosition(2, 2), player.Square);
        Assert.False(player.Surfing);
    }

    [Fact]
    public void AskingToSurfWithNobodyWhoCanIsRefused()
    {
        // The check that matters, and it is on this side. The client knows the party
        // perfectly well — it is the client that offers the swim — but a client is a
        // thing a player can rewrite.
        GameWorld world = World();
        ServerPlayer player = Standing(world, canSurf: false);

        Assert.Empty(world.Surf(player.Id));

        Assert.Equal("refused: nobody in the party knows SURF", world.LastSurf);
        Assert.False(player.Surfing);
    }

    [Fact]
    public void AskingToSurfAtDryLandIsRefused()
    {
        GameWorld world = World();
        ServerPlayer player = Standing(world);

        player.Facing = Direction.Left;

        Assert.Empty(world.Surf(player.Id));

        Assert.Contains("is not water", world.LastSurf ?? "");
        Assert.False(player.Surfing);
    }

    [Fact]
    public void AskingToSurfAtWaterPutsYouOnIt()
    {
        GameWorld world = World();
        ServerPlayer player = Standing(world);

        List<NetMessage> said = world.Surf(player.Id).Select(o => o.Message).ToList();

        Assert.True(player.Surfing);
        Assert.Equal(new GridPosition(3, 2), player.Square);

        SurfingChanged afloat = said.OfType<SurfingChanged>().Single();

        Assert.True(afloat.Surfing);
        Assert.Equal(3, afloat.X);
        Assert.Equal(2, afloat.Y);

        // And everybody else sees the step, because from outside it is a step.
        Assert.Single(said.OfType<PlayerMoved>());
    }

    [Fact]
    public void OnTheWaterTheWaterIsWalkable()
    {
        GameWorld world = World();
        ServerPlayer player = Standing(world);

        world.Surf(player.Id);

        player.LastStepAt = double.NegativeInfinity;
        world.Move(player.Id, Direction.Right, 10);

        Assert.Equal(new GridPosition(4, 2), player.Square);
        Assert.True(player.Surfing);
    }

    [Fact]
    public void SteppingAshoreIsHowItEnds()
    {
        // Not offered as a choice, because it is not one: the step was onto land and
        // there is nowhere to be but on it.
        GameWorld world = World();
        ServerPlayer player = Standing(world);

        world.Surf(player.Id);

        player.LastStepAt = double.NegativeInfinity;
        List<NetMessage> said = world.Move(player.Id, Direction.Left, 10).Select(o => o.Message).ToList();

        Assert.Equal(new GridPosition(2, 2), player.Square);
        Assert.False(player.Surfing);

        Assert.False(said.OfType<SurfingChanged>().Single().Surfing);
    }

    [Fact]
    public void AsecondAskWhileAlreadyAfloatDoesNothing()
    {
        GameWorld world = World();
        ServerPlayer player = Standing(world);

        world.Surf(player.Id);

        GridPosition where = player.Square;

        Assert.Empty(world.Surf(player.Id));
        Assert.Equal(where, player.Square);
    }

    [Fact]
    public void WhatLivesInTheWaterIsNotWhatLivesInTheGrass()
    {
        // The table is chosen by what is underfoot rather than by what map this is. The
        // same route has grass down one side and sea down the other.
        GameWorld world = World();
        ServerPlayer player = Standing(world);

        world.Surf(player.Id);

        BattleStarted? met = null;

        for (int step = 0; step < 60 && met is null; step++)
        {
            player.LastStepAt = double.NegativeInfinity;

            foreach (Outgoing outgoing in world.Move(
                         player.Id, step % 2 == 0 ? Direction.Right : Direction.Left, step + 20))
            {
                if (outgoing.Message is BattleStarted started) met = started;
            }
        }

        Assert.NotNull(met);
        Assert.Equal(10, met!.Opponent.Level);
    }

    [Fact]
    public void AMapWithNoWaterTableHasNothingInItsWater()
    {
        GameWorld world = World(withFish: false);
        ServerPlayer player = Standing(world);

        world.Surf(player.Id);

        for (int step = 0; step < 60; step++)
        {
            player.LastStepAt = double.NegativeInfinity;

            foreach (Outgoing outgoing in world.Move(
                         player.Id, step % 2 == 0 ? Direction.Right : Direction.Left, step + 20))
            {
                Assert.IsNotType<BattleStarted>(outgoing.Message);
            }
        }

        Assert.Null(player.Battle);
    }

    [Fact]
    public void TheMoveIsFoundByNameRatherThanRemembered()
    {
        // A number written here because SURF is move 57 in some other game is the one
        // mistake this project keeps a standing rule against. It is read off the rules
        // file's own text, and a rules file without it simply has no surfing.
        Assert.Equal(TestRules.SurfMove, World().SurfMove);
    }
}

/// <summary>
/// Doors that lead back the way you came.
/// <para>
/// Nineteen warps on this cartridge name map 127.127, which no bank has. Every one is
/// the exit of a room reached from many places — the CABLE CLUB above every POKeMON
/// CENTER, the lifts in SILPH CO. and the ROCKET HIDEOUT — and a room reached from
/// twelve places cannot write down which one it came from.
/// </para>
/// </summary>
public class ReturnDoorTests
{
    [Fact]
    public void TheSentinelIsADestinationRatherThanAMissingMap()
    {
        Assert.True(new Warp(5, 8, Warp.Dynamic, $"{Warp.Dynamic}.{Warp.Dynamic}").IsDynamic);
        Assert.False(new Warp(5, 8, 0, "3.19").IsDynamic);
    }

    [Fact]
    public void AWalkIsNotReportedAsHavingFoundAHole()
    {
        // The whole reason this exists. A walker counting these as maps the world file
        // does not have is a walker reporting a hole where the cartridge has a door.
        MapData room = new("1.1", "THE CABLE CLUB", 4, 4, new byte[16])
        {
            Warps = [new Warp(1, 1, Warp.Dynamic, $"{Warp.Dynamic}.{Warp.Dynamic}")],
        };

        Reach reach = WorldWalker.Walk(new WorldData([room]), "1.1");

        Assert.Contains("1.1", reach.Maps);
        Assert.Empty(reach.Beyond);
    }
}
