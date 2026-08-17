using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Telling one scene written four times from four scripts that share a block.
/// <para>
/// Same map, same target, and the two are opposite findings. `3.14` has five triggers on five
/// adjacent squares handing over to one block and saying 0, 1, 2, 3, 4 — five squares of one
/// line, crossed once. It also has six people handing over to a different block and all saying
/// <c>2</c> — six people who share a script, and a player talks to all six.
/// </para>
/// <para>
/// The number is the whole difference and it is in the bytes: a stub that announces which door
/// it came in by says a different number per door.
/// </para>
/// </summary>
public class EntriesToASceneTests
{
    private const byte LockAll = 0x69;
    private const byte SetVar = 0x16;
    private const byte Goto = 0x05;
    private const byte SetFlag = 0x29;
    private const byte End = 0x02;

    private const int Scratch = 0x4001;
    private const int Remembered = 0x4055;

    private const uint TheScene = 0x08000600;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static void Pointer(byte[] image, int at, uint address)
    {
        for (int i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>One twelve-byte stub: lockall, say which door, go to the scene.</summary>
    private static void Stub(byte[] image, int at, int says, int variable = Scratch, uint leads = TheScene)
    {
        Put(image, at, LockAll, SetVar, (byte)(variable & 0xFF), (byte)(variable >> 8),
            (byte)says, 0x00, Goto);
        Pointer(image, at + 7, leads);
        Put(image, at + 11, End);
    }

    private static byte[] Image()
    {
        var image = new byte[0x1000];

        Stub(image, 0x100, 0);
        Stub(image, 0x110, 1);
        Stub(image, 0x120, 2);

        Put(image, 0x600, SetFlag, 0x2E, 0x00, End);

        return image;
    }

    private static SetsAFlag At(string mapId, string what, uint address) => new(mapId, what, address);

    private static IReadOnlyList<AnEntry> Doors(byte[] image, params SetsAFlag[] scripts) =>
        EntriesToAScene.In(new Rom(image), scripts, 0x4010);

    [Fact]
    public void ABlockThatOnlyHandsOverIsADoor()
    {
        AnEntry door = Assert.Single(Doors(Image(), At("3.2", "trigger (1,1)", 0x08000100)));

        Assert.Equal(TheScene, door.Leads);
        Assert.Equal(0, door.Says);
        Assert.Equal(Scratch, door.Into);
    }

    /// <summary>
    /// A block that does something of its own is a scene, not a door into one. Without this
    /// every script in the game that ends in a <c>goto</c> would be counted.
    /// </summary>
    [Fact]
    public void ABlockThatDoesSomethingOfItsOwnIsNot()
    {
        byte[] image = Image();

        // A setflag before the handover: this block is doing something.
        Put(image, 0x100, LockAll, SetFlag, 0x30, 0x00, Goto);
        Pointer(image, 0x105, TheScene);
        Put(image, 0x109, End);

        Assert.Empty(Doors(image, At("3.2", "trigger (1,1)", 0x08000100)));
    }

    /// <summary>
    /// And a block that writes something the story REMEMBERS is not announcing a door either.
    /// The scratch line is read off the cartridge's own write-count cliff, not written here.
    /// </summary>
    [Fact]
    public void AHandoverThatWritesTheStorysOwnMemoryIsNotADoor()
    {
        byte[] image = Image();

        Stub(image, 0x100, 1, variable: Remembered);

        Assert.Empty(Doors(image, At("3.2", "trigger (1,1)", 0x08000100)));
    }

    [Fact]
    public void DoorsSayingDifferentNumbersOnOneMapAreOneScene()
    {
        IReadOnlyList<AnEntry> doors = Doors(
            Image(),
            At("3.2", "trigger (1,1)", 0x08000100),
            At("3.2", "trigger (1,2)", 0x08000110));

        IGrouping<(string, uint), AnEntry> room = Assert.Single(EntriesToAScene.Rooms(doors));

        Assert.True(EntriesToAScene.IsOneSceneEnteredSeveralWays(room));
    }

    /// <summary>
    /// AND THE OPPOSITE FINDING WITH THE SAME SHAPE. Two on one map saying the same number are
    /// two people who share a script, and a player talks to both. Getting this wrong turns the
    /// shared routines of the game into a list of scenes played over and over.
    /// </summary>
    [Fact]
    public void DoorsSayingTheSameNumberAreACrowd()
    {
        byte[] image = Image();

        Stub(image, 0x110, 0);

        IReadOnlyList<AnEntry> doors = Doors(
            image,
            At("3.14", "person 3", 0x08000100),
            At("3.14", "person 4", 0x08000110));

        IGrouping<(string, uint), AnEntry> room = Assert.Single(EntriesToAScene.Rooms(doors));

        Assert.False(EntriesToAScene.IsOneSceneEnteredSeveralWays(room));
    }

    /// <summary>
    /// And two on DIFFERENT maps are not a room at all. Nineteen Pokémon Centres share one
    /// nurse's script; grouped without the map they are the biggest room in the cartridge and
    /// the answer is meaningless. Milestone 193's first version made exactly this mistake and
    /// dropped seven walks in every eight.
    /// </summary>
    [Fact]
    public void TheSameBlockOnTwoMapsIsASharedRoutineAndNotARoom()
    {
        IReadOnlyList<AnEntry> doors = Doors(
            Image(),
            At("5.5", "person 1", 0x08000100),
            At("6.6", "person 1", 0x08000110));

        Assert.Empty(EntriesToAScene.Rooms(doors));
        Assert.Single(EntriesToAScene.SharedAcrossMaps(doors));
    }

    /// <summary>And one door is not a room. The instrument has to be able to say nothing.</summary>
    [Fact]
    public void OneDoorIsNotARoom()
    {
        Assert.Empty(EntriesToAScene.Rooms(Doors(Image(), At("3.2", "trigger (1,1)", 0x08000100))));
    }
}
