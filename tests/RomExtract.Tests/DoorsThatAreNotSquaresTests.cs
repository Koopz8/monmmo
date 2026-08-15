using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Doors a script makes, which are on no square.
/// <para>
/// A warp in this project has always been a map record: a square, a target, a warp id on
/// the far side. Those are the only doors the walker has ever known about, and they leave
/// 179 of 425 maps with nothing leading in — a fact the startup log has stated for a dozen
/// milestones as though it were about geometry.
/// </para>
/// <para>
/// <c>warp</c> is also a script command, seven bytes wide, and its width was derived long
/// ago by the shape of its arguments. Nothing had ever read what it framed.
/// </para>
/// </summary>
public class DoorsThatAreNotSquaresTests
{
    /// <summary>
    /// The command's arguments, in the order the cartridge writes them. Taken from the
    /// derivation that settled its width:
    /// <code>39 | 01 57 | 00 | 1B 00 | 15 00   -> 1.87 SEAFOAM ISLANDS at (27, 21)</code>
    /// </summary>
    [Fact]
    public void AWarpCommandIsAMapAWarpIdAndASquare()
    {
        var command = new ScriptCommand(0, ScriptedDoors.Warp, [0x01, 0x57, 0x00, 0x1B, 0x00, 0x15, 0x00]);

        ScriptedDoor door = ScriptedDoors.Read(command, "person 1")!;

        Assert.Equal("1.87", door.TargetMapId);
        Assert.Equal(0, door.TargetWarpId);
        Assert.Equal(new GridPosition(27, 21), door.Square);
    }

    /// <summary>
    /// A bank and a map of 0xFF is the cartridge saying "wherever the last one was" —
    /// used by the scripts that put a player back where a scene interrupted them. It is a
    /// door to nowhere in particular and reporting it as a link to map 255.255, which no
    /// bank has, would be worse than leaving it out.
    /// </summary>
    [Fact]
    public void AndWhereverYouWereIsNotADoor()
    {
        var command = new ScriptCommand(0, ScriptedDoors.Warp, [0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00]);

        Assert.Null(ScriptedDoors.Read(command, "person 1"));
    }

    [Fact]
    public void AndNothingElseIsOne()
    {
        var command = new ScriptCommand(0, 0x29, [0x89, 0x01]);

        Assert.Null(ScriptedDoors.Read(command, "person 1"));
    }

    /// <summary>Two rooms with no doorway between them, and a script that joins them.</summary>
    private static WorldData TwoRooms(params ScriptedDoor[] doors)
    {
        MapData here = new("1.0", "PALLET TOWN", 4, 4, new byte[16]) { Doors = doors };
        MapData there = new("2.0", "ONE ISLAND", 4, 4, new byte[16]);

        return new WorldData([here, there]);
    }

    [Fact]
    public void AWalkDoesNotUseThemUnlessItIsAsked()
    {
        WorldData world = TwoRooms(new ScriptedDoor("person 1", "2.0", 0, 1, 1));

        Reach walked = WorldWalker.Walk(world, "1.0");

        Assert.DoesNotContain("2.0", walked.Maps);
    }

    /// <summary>
    /// And does when it is. This is the whole instrument: the difference between the two
    /// numbers is how much of the world is cut off by a story rather than by geometry.
    /// </summary>
    [Fact]
    public void AndDoesWhenItIs()
    {
        WorldData world = TwoRooms(new ScriptedDoor("person 1", "2.0", 0, 1, 1));

        Reach walked = WorldWalker.Walk(world, "1.0", throughScriptedDoors: true);

        Assert.Contains("2.0", walked.Maps);
    }

    /// <summary>A door to a map this world file does not have is reported, not followed.</summary>
    [Fact]
    public void ADoorToAMapThatIsNotHereIsCountedAsBeyond()
    {
        WorldData world = TwoRooms(new ScriptedDoor("person 1", "9.9", 0, 1, 1));

        Reach walked = WorldWalker.Walk(world, "1.0", throughScriptedDoors: true);

        Assert.Contains("9.9", walked.Beyond);
        Assert.DoesNotContain("9.9", walked.Maps);
    }

    [Fact]
    public void TheyTravelInTheWorldFile()
    {
        WorldData world = TwoRooms(
            new ScriptedDoor("person 1", "2.0", 3, 5, 6),
            new ScriptedDoor("trigger (1,2)", "2.0", 0, 1, 1));

        using var buffer = new MemoryStream();
        world.Save(buffer);

        buffer.Position = 0;

        WorldData reloaded = WorldData.Load(buffer);

        Assert.Equal(world.Find("1.0")!.Doors, reloaded.Find("1.0")!.Doors);
    }
}
