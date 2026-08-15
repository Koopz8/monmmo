using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The boat, and the fact that nothing here can read what it does.
/// <para>
/// The archipelago is a second world — 246 maps reachable from PALLET TOWN, 179 that are
/// not, in 30 separate pieces, with no warp, no map edge and no scripted door between them.
/// What crosses is a <c>special</c>: a call into the cartridge's own ARM code by number,
/// which is the one boundary this project has never been able to read across.
/// </para>
/// <para>
/// It did not have to. Ten scripts in this game write a number into an argument slot and
/// then hand the screen to the same routine as the last thing they ever do, and no two of
/// them write the same number. The routine stays unreadable; the table of places it can put
/// you down is written in the open by the scripts that use it.
/// </para>
/// <para>
/// These tests are the rules on this side of that table: who may sail, from where, to what.
/// </para>
/// </summary>
public class TheBoatTests
{
    private const string Harbour = "1.0";
    private const string Island = "2.0";

    /// <summary>Two docks, each with a sailor, and one open square in front of each.</summary>
    private static (GameWorld World, ServerPlayer Player) TwoDocks()
    {
        MapObject sailorHere = new(2, 5, 3, 4, Direction.Up, 0, false) { Talks = true };
        MapObject sailorThere = new(2, 5, 5, 5, Direction.Down, 0, false) { Talks = true };

        MapData here = new(Harbour, "VERMILION CITY", 8, 8, new byte[64])
        {
            Objects = [sailorHere],
            Ferry = new FerryDock(0, 2, 3, 3),
        };

        MapData there = new(Island, "ONE ISLAND", 8, 8, new byte[64])
        {
            Objects = [sailorThere],
            Ferry = new FerryDock(1, 2, 5, 6),
        };

        var world = new GameWorld(new WorldData([here, there]), Harbour, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Harbour, 3, 5));

        world.Operators.Add("Mason");

        player.Square = new GridPosition(3, 5);
        player.Facing = Direction.Up;

        return (world, player);
    }

    private static FerryOpened? AskedOf(GameWorld world, ServerPlayer player) =>
        world.StartTalking(player.Id, 2).Select(o => o.Message).OfType<FerryOpened>().FirstOrDefault();

    [Fact]
    public void AskingTheSailorOffersEverywhereTheBoatCallsAt()
    {
        (GameWorld world, ServerPlayer player) = TwoDocks();

        FerryOpened? asked = AskedOf(world, player);

        Assert.NotNull(asked);
        Assert.Equal(0, asked.From);
        Assert.Equal([0, 1], asked.Ports.Select(p => p.Number));
    }

    /// <summary>
    /// And nobody else does. A sailor is the map's, not the person's — the cartridge names
    /// the dock — so anybody else standing on a dock is still just somebody standing there.
    /// </summary>
    [Fact]
    public void AndNobodyElseOnTheMapDoes()
    {
        MapObject sailor = new(2, 5, 3, 4, Direction.Up, 0, false) { Talks = true };
        MapObject bystander = new(3, 5, 4, 5, Direction.Left, 0, false) { Talks = true };

        MapData here = new(Harbour, "VERMILION CITY", 8, 8, new byte[64])
        {
            Objects = [sailor, bystander],
            Ferry = new FerryDock(0, 2, 3, 3),
        };

        var world = new GameWorld(new WorldData([here]), Harbour, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Harbour, 5, 5));

        player.Square = new GridPosition(5, 5);
        player.Facing = Direction.Left;

        Assert.Empty(world.StartTalking(player.Id, 3).Select(o => o.Message).OfType<FerryOpened>());
    }

    [Fact]
    public void SailingPutsYouDownAtTheOtherDock()
    {
        (GameWorld world, ServerPlayer player) = TwoDocks();

        AskedOf(world, player);

        world.Sail(player.Id, 1);

        Assert.Equal(Island, player.MapId);
        Assert.Equal(new GridPosition(5, 6), player.Square);
    }

    /// <summary>
    /// Nobody sails without asking. The list of places is this side's and so is the
    /// crossing; a client that sent the message on its own would be choosing a destination
    /// the server never offered.
    /// </summary>
    [Fact]
    public void NobodySailsWithoutAskingASailorFirst()
    {
        (GameWorld world, ServerPlayer player) = TwoDocks();

        world.Sail(player.Id, 1);

        Assert.Equal(Harbour, player.MapId);
        Assert.Contains("nobody asked", world.LastSail);
    }

    [Fact]
    public void AndNotToADockThatIsNotThere()
    {
        (GameWorld world, ServerPlayer player) = TwoDocks();

        AskedOf(world, player);

        world.Sail(player.Id, 7);

        Assert.Equal(Harbour, player.MapId);
        Assert.Contains("no dock", world.LastSail);
    }

    /// <summary>And a boat to where you already are is not a journey.</summary>
    [Fact]
    public void AndNotToWhereYouAlreadyAre()
    {
        (GameWorld world, ServerPlayer player) = TwoDocks();

        AskedOf(world, player);

        world.Sail(player.Id, 0);

        Assert.Equal(Harbour, player.MapId);
        Assert.Contains("already", world.LastSail);
    }

    /// <summary>One crossing per asking, so a stale screen cannot sail twice.</summary>
    [Fact]
    public void AndOnlyOnceForEachAsking()
    {
        (GameWorld world, ServerPlayer player) = TwoDocks();

        AskedOf(world, player);

        world.Sail(player.Id, 1);
        world.Sail(player.Id, 0);

        Assert.Equal(Island, player.MapId);
    }

    /// <summary>Closing the text box closes the boat with it.</summary>
    [Fact]
    public void WalkingAwayFromTheSailorEndsIt()
    {
        (GameWorld world, ServerPlayer player) = TwoDocks();

        AskedOf(world, player);

        world.StopTalking(player.Id);
        world.Sail(player.Id, 1);

        Assert.Equal(Harbour, player.MapId);
    }

    [Fact]
    public void TheConsoleCanSailWithoutASailor()
    {
        (GameWorld world, ServerPlayer player) = TwoDocks();

        world.RunConsole(player.Id, "/sail 1");

        Assert.Equal(Island, player.MapId);
    }

    [Fact]
    public void TheDockTravelsInTheWorldFile()
    {
        MapData dock = new(Island, "ONE ISLAND", 8, 8, new byte[64]) { Ferry = new FerryDock(1, 2, 5, 6) };
        MapData inland = new(Harbour, "PALLET TOWN", 8, 8, new byte[64]);

        using var buffer = new MemoryStream();
        new WorldData([dock, inland]).Save(buffer);

        buffer.Position = 0;

        WorldData reloaded = WorldData.Load(buffer);

        Assert.Equal(new FerryDock(1, 2, 5, 6), reloaded.Find(Island)!.Ferry);

        // And a map that is not a dock reloads as one that is not, rather than as dock
        // zero — which is a real place.
        Assert.Null(reloaded.Find(Harbour)!.Ferry);
    }
}
