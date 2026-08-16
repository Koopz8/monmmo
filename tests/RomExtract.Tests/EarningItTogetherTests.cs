using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a story event does to the people standing next to whoever caused it.
/// <para>
/// The one place in co-op that writes to somebody else's save, and the rule it turns on could
/// not be read: the cartridge has no bit saying "this flag is about the world". It is derived
/// instead — a flag that puts somebody on a map or takes them off one is a fact about the
/// world; a flag that gates nothing anywhere in the world file is a mark on a character.
/// </para>
/// <para>
/// Which is the difference between a friend walking through a door you opened and a friend
/// being handed a badge you earned.
/// </para>
/// </summary>
public class EarningItTogetherTests
{
    private const string Town = "1.0";

    /// <summary>
    /// A flag that gates somebody, and a flag that gates nothing. Both real flag numbers as
    /// far as anything can tell; the only difference is whether the world file uses one.
    /// </summary>
    private const int OpensADoor = 0x0037;

    private const int JustAMark = 0x0201;

    private static (GameWorld World, ServerPlayer One, ServerPlayer Two) Together()
    {
        // Somebody hidden by a flag, which is how this game opens most of its story.
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64])
        {
            Objects =
            [
                new MapObject(1, 1, 2, 2, Direction.Down, 0, false) { HiddenBy = OpensADoor },
            ],
        };

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer one, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));
        (ServerPlayer two, _) = world.Join(2, "Koop", SavedCharacter.Fresh(Town, 3, 3));

        world.AskToTravelWith(one.Id, two.Id);
        world.AskToTravelWith(two.Id, one.Id);

        return (world, one, two);
    }

    // ---- the classification --------------------------------------------------------------

    /// <summary>
    /// A flag that puts somebody on a map is a fact about the world, and one the world file
    /// never mentions is not.
    /// </summary>
    [Fact]
    public void AFlagThatMovesSomebodyIsAboutTheWorld()
    {
        (GameWorld world, _, _) = Together();

        Assert.True(world.FlagGates.IsAboutTheWorld(OpensADoor));
        Assert.Equal(FlagGate.APerson, world.FlagGates.Of(OpensADoor));

        Assert.False(world.FlagGates.IsAboutTheWorld(JustAMark));
        Assert.Equal(FlagGate.Nothing, world.FlagGates.Of(JustAMark));
    }

    /// <summary>
    /// And the boat, which is the one gate written as a flag and an item together. The flag
    /// half is the world's; the item half is not, and is left alone.
    /// </summary>
    [Fact]
    public void AndTheBoatIsAboutTheWorldToo()
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(
            new WorldData([map]) { FerryPasses = [new FerryPass(0x0099, 42)] }, Town, TestRules.All);

        Assert.Equal(FlagGate.TheBoat, world.FlagGates.Of(0x0099));
    }

    // ---- what travels --------------------------------------------------------------------

    /// <summary>
    /// A flag about the world, set by one of them, is set for the other.
    /// <para>
    /// Which is the whole of what playing a story together means. A friend who had to re-open
    /// every door separately is not playing with you.
    /// </para>
    /// </summary>
    [Fact]
    public void AWorldFlagReachesWhoeverWasStandingThere()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        world.RunScript(one.Id, new ScriptRan([OpensADoor], [], []));

        Assert.True(one.Script.Has(OpensADoor));
        Assert.True(two.Script.Has(OpensADoor));
    }

    /// <summary>
    /// <b>And a mark on a character does not.</b>
    /// <para>
    /// The half that makes the other half safe. Propagating everything would mean your friend
    /// beats Brock and you are handed the badge — and unlike a door that fails to open, that
    /// one is invisible until much later.
    /// </para>
    /// </summary>
    [Fact]
    public void ButAMarkOnACharacterDoesNot()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        world.RunScript(one.Id, new ScriptRan([JustAMark], [], []));

        Assert.True(one.Script.Has(JustAMark));
        Assert.False(two.Script.Has(JustAMark));
    }

    /// <summary>
    /// A flag the story turns off travels as well.
    /// <para>
    /// The whole middle of this game is flags being cleared — a person the story removes is
    /// as much a fact about the world as one it adds. Propagating only the setting half would
    /// leave a friend looking at somebody who is no longer there.
    /// </para>
    /// </summary>
    [Fact]
    public void AndAFlagTheStoryTurnsOffTravelsToo()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        one.Script.Set(OpensADoor);
        two.Script.Set(OpensADoor);

        world.RunScript(one.Id, new ScriptRan([], [OpensADoor], []));

        Assert.False(one.Script.Has(OpensADoor));
        Assert.False(two.Script.Has(OpensADoor));
    }

    /// <summary>
    /// Variables do not travel.
    /// <para>
    /// A variable holds which starter was taken and which trainer the rival fielded. Every one
    /// of them answers "what did <em>you</em> do", and copying them across is how one person's
    /// rival becomes somebody else's.
    /// </para>
    /// </summary>
    [Fact]
    public void AndVariablesDoNotTravel()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        world.RunScript(one.Id, new ScriptRan([], [], [new SavedVariable(0x4001, 7)]));

        Assert.Equal(7, one.Script.Read(0x4001));
        Assert.NotEqual(7, two.Script.Read(0x4001));
    }

    // ---- who counts as standing there ------------------------------------------------------

    /// <summary>
    /// Somebody who is not travelling with you gets nothing, however close they are standing.
    /// <para>
    /// Writing to a stranger's save because they walked past is not a feature. Being in a
    /// company is the opting-in.
    /// </para>
    /// </summary>
    [Fact]
    public void SomebodyNotTravellingWithYouGetsNothing()
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64])
        {
            Objects = [new MapObject(1, 1, 2, 2, Direction.Down, 0, false) { HiddenBy = OpensADoor }],
        };

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer one, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));
        (ServerPlayer two, _) = world.Join(2, "Koop", SavedCharacter.Fresh(Town, 3, 3));

        world.RunScript(one.Id, new ScriptRan([OpensADoor], [], []));

        Assert.False(two.Script.Has(OpensADoor));
    }

    /// <summary>
    /// And somebody in the company who is somewhere else gets nothing either.
    /// <para>
    /// They did not see it happen. A story that reaches across the world because two people
    /// are nominally travelling together is a story nobody can follow — you would arrive in a
    /// town to find its events already over.
    /// </para>
    /// </summary>
    [Fact]
    public void AndSomebodySomewhereElseGetsNothingEither()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        // Same map, a different copy of it — which is exactly the case copies exist to make
        // possible, and from inside is indistinguishable from being on another map.
        two.Copy = 4;

        world.RunScript(one.Id, new ScriptRan([OpensADoor], [], []));

        Assert.False(two.Script.Has(OpensADoor));
    }

    /// <summary>
    /// And the one who was there is told about it, rather than being left looking at a room
    /// the story has changed under them.
    /// </summary>
    [Fact]
    public void AndTheOneWhoWasThereIsToldAboutIt()
    {
        (GameWorld world, ServerPlayer one, ServerPlayer two) = Together();

        // Both were sent this map's people when they arrived, and the flag hides one of
        // them. So setting it has something visible to change for the one who did not set
        // it — which is the only way to tell being told from being quietly written to.
        List<Outgoing> said = world.RunScript(one.Id, new ScriptRan([OpensADoor], [], []));

        Assert.Contains(said, o => o.OnlyTo == two.Id);
    }
}
