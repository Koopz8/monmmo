using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A fresh save is not an empty save.
/// <para>
/// The bug these are written against: MR. FUJI stood in his own front room on turn one,
/// holding the POKé FLUTE, while the tower he is supposed to be trapped at the top of
/// stood empty behind him. He is hidden by a flag, the flag was not set, and nothing had
/// ever set it — because the cartridge sets it, along with forty-eight others, before
/// the player has taken a single step.
/// </para>
/// </summary>
public class NewGameTests
{
    private const byte SetFlag = 0x29;
    private const byte SetVar = 0x16;
    private const byte End = 0x02;

    /// <summary>
    /// An image with one opening script in it and two things that look like one.
    /// <para>
    /// The decoys are the point. On a real cartridge the three longest runs of 0x29
    /// bytes are all stretches of graphics, and a locator that only counted length would
    /// pick a picture and be sure about it.
    /// </para>
    /// </summary>
    private static Rom Image(int flags, params int[] decoyLengths)
    {
        var data = new byte[0x2000];

        // A run of the same flag over and over, pointed at by nothing: what a picture
        // full of 0x29 bytes looks like to a script reader.
        int at = 0x100;

        foreach (int length in decoyLengths)
        {
            for (int i = 0; i < length; i++)
            {
                data[at++] = SetFlag;
                data[at++] = 0x29;
                data[at++] = 0x29;
            }
        }

        int opening = 0x1000;
        int write = opening;

        for (int i = 0; i < flags; i++)
        {
            data[write++] = SetFlag;
            data[write++] = (byte)(0x30 + i);
            data[write++] = 0x00;
        }

        data[write++] = SetVar;
        data[write++] = 0x25;
        data[write++] = 0x40;
        data[write++] = 0xF4;
        data[write++] = 0x01;
        data[write] = End;

        // Something in the image points at it, which is the other half of being a script.
        uint address = Rom.BaseAddress + (uint)opening;

        data[0x40] = (byte)address;
        data[0x41] = (byte)(address >> 8);
        data[0x42] = (byte)(address >> 16);
        data[0x43] = (byte)(address >> 24);

        return new Rom(data);
    }

    [Fact]
    public void TheOpeningIsTheLongestRunOfSetFlagsAnythingPointsAt()
    {
        NewGameState opening = NewGameLocator.Locate(Image(flags: 20))!;

        Assert.Equal(Rom.BaseAddress + 0x1000, opening.Address);
        Assert.Equal(20, opening.Flags.Count);
        Assert.Equal(0x30, opening.Flags[0]);
        Assert.Equal(0x43, opening.Flags[^1]);
    }

    [Fact]
    public void TheVariableTheSameScriptWritesComesWithIt()
    {
        NewGameState opening = NewGameLocator.Locate(Image(flags: 20))!;

        Assert.Equal([(0x4025, 500)], opening.Variables);
    }

    /// <summary>
    /// The test that would have failed on the cartridge: three of its runs of 0x29 are
    /// longer than the real one and every one of them is a picture.
    /// </summary>
    [Fact]
    public void ALongerRunOfTheSameByteIsAPictureRatherThanAScript()
    {
        NewGameState opening = NewGameLocator.Locate(Image(flags: 20, decoyLengths: [60, 40]))!;

        Assert.Equal(Rom.BaseAddress + 0x1000, opening.Address);
        Assert.Equal(20, opening.Flags.Count);
    }

    [Fact]
    public void AnImageWithNoOpeningInItSaysSoRatherThanGuessing()
    {
        // Four flags is what an ordinary script sets on its way out. Nothing on an image
        // like this should be mistaken for the beginning of a game.
        Assert.Null(NewGameLocator.Locate(Image(flags: 4)));
    }

    [Fact]
    public void WhatANewGameStartsWithSurvivesTheWorldFile()
    {
        var world = new WorldData([new MapData("1.0", "PALLET TOWN", 4, 4, new byte[16])])
        {
            FlagsAtStart = [0x35, 0x9D, 0x2C],
            VariablesAtStart = [new StartingVariable(0x4025, 500)],
        };

        using var file = new MemoryStream();

        world.Save(file);
        file.Position = 0;

        WorldData read = WorldData.Load(file);

        Assert.Equal([0x35, 0x9D, 0x2C], read.FlagsAtStart);
        Assert.Equal([new StartingVariable(0x4025, 500)], read.VariablesAtStart);
    }

    /// <summary>
    /// The whole point, said in the terms the player sees it in: somebody who is meant
    /// to be somewhere else is not standing in the room.
    /// </summary>
    [Fact]
    public void SomebodyHiddenAtTheStartIsNotThereForABrandNewCharacter()
    {
        MapData house = new("8.2", "LAVENDER TOWN", 8, 8, new byte[64])
        {
            Objects =
            [
                // The old man who is supposed to be up the tower.
                new MapObject(1, 78, 3, 3, Direction.Down, 0, false) { HiddenBy = 0x35, CanGive = [350] },
                new MapObject(2, 124, 1, 4, Direction.Down, 0, false),
            ],
        };

        var world = new GameWorld(
            new WorldData([house]) { FlagsAtStart = [0x35] },
            "8.2",
            TestRules.All);

        SavedCharacter fresh = world.FreshCharacter();

        Assert.Contains(0x35, fresh.Flags);

        (_, List<Outgoing> welcome) = world.Join(1, "Koop", fresh);

        List<int> here = welcome
            .Select(o => o.Message)
            .OfType<ObjectsPlaced>()
            .SelectMany(p => p.Objects)
            .Select(o => o.LocalId)
            .ToList();

        Assert.Equal([2], here);
    }

    /// <summary>
    /// The instrument's counterpart. The walker measures what a character can reach, and
    /// it was measuring it for a character nobody has ever played: one with no flags, and
    /// so with every latecomer in the game already standing in the corridors.
    /// </summary>
    [Fact]
    public void TheWalkerCountsOnlyThePeopleAFreshCharacterWouldMeet()
    {
        const string cave = "1.90";
        const int width = 5;
        const int height = 7;

        var collision = new byte[width * height];

        for (int y = 2; y <= 4; y++)
        {
            collision[y * width + 0] = 1;
            collision[y * width + 1] = 1;
            collision[y * width + 3] = 1;
            collision[y * width + 4] = 1;
        }

        // Rooted to the spot in the one-square corridor between the two rooms, and only
        // there once flag 0x2C has been cleared by whatever clears it.
        MapObject guard = new(1, 5, 2, 3, Direction.Down, 8, IsTrainer: false) { HiddenBy = 0x2C };

        var world = new WorldData([new MapData(cave, "THE CORRIDOR", width, height, collision) { Objects = [guard] }])
        {
            FlagsAtStart = [0x2C],
        };

        // He is not there yet, so the corridor is a corridor.
        Assert.Equal(23, WorldWalker.Walk(world, cave).Stood.Count);
        Assert.Empty(WorldWalker.Walk(world, cave).People);

        // And walked as the walker used to walk it — as though no flag had ever been set
        // — he is a wall, which is the reading that was wrong.
        Assert.True(WorldWalker.Walk(world, cave, flagsSet: []).Stood.Count < 23);
    }

    /// <summary>
    /// Forgetting the story means going back to the beginning, not back to nothing.
    /// </summary>
    [Fact]
    public async Task ForgettingTheStoryLeavesACharacterWhereANewOneWouldBe()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        SavedCharacter start = SavedCharacter.Fresh("1.0", 1, 1) with
        {
            Flags = [0x35, 0x9D],
            Variables = [new SavedVariable(0x4025, 500)],
        };

        Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync("Mason", "a-good-password", start with
            {
                // Somebody who has played: the old man rescued, the tower behind them.
                Flags = [0x9D, 0x34],
                Variables = [new SavedVariable(0x4025, 500), new SavedVariable(0x4001, 3)],
            }));

        Assert.True(await store.ForgetStoryAsync("Mason", start) >= 0);

        var back = (AuthOutcome.Success)await store.LoginAsync("Mason", "a-good-password");

        Assert.Equal([0x35, 0x9D], back.Character.Flags.Order());
        Assert.Equal([new SavedVariable(0x4025, 500)], back.Character.Variables);
    }

    /// <summary>
    /// And the counterpart: a world file that says nothing about the opening still works,
    /// because a stripped test image has no opening script to find.
    /// </summary>
    [Fact]
    public void AWorldWithNoOpeningHandsOutNoFlags()
    {
        var world = new GameWorld(
            new WorldData([new MapData("1.0", "PALLET TOWN", 8, 8, new byte[64])]),
            "1.0",
            TestRules.All);

        Assert.Empty(world.FreshCharacter().Flags);
    }
}
