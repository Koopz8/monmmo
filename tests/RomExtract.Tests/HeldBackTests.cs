using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Who a map is holding back, and on which flag.
/// <para>
/// GIOVANNI was missing from the ROCKET HIDEOUT. The save said the flag that hides him was
/// clear. A <c>/flag 0x38 off</c> that should have been a no-op put him back on the couch.
/// Working out which of those two records was lying took most of a session and did not
/// finish, because from the outside there was no way to ask the server what it thought.
/// </para>
/// <para>
/// Two facts explain an absence and they are different faults. Either the flag is set — the
/// world is behaving and the flag arrived from somewhere — or the flag is clear and the
/// player was simply never told, which is not about flags at all. This puts both on one
/// line, and marks the rows where they disagree.
/// </para>
/// </summary>
public class HeldBackTests
{
    private const string Room = "1.45";

    private static MapObject Carrying(int localId, int hiddenBy) =>
        new(localId, 5, localId, 2, Direction.Down, 0, false) { HiddenBy = hiddenBy, Talks = true };

    private static (GameWorld World, ServerPlayer Player) Standing(params MapObject[] people)
    {
        MapData map = new(Room, "ROCKET HIDEOUT", 8, 8, new byte[64]) { Objects = people };

        var world = new GameWorld(new WorldData([map]), Room, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Room, 3, 4));

        return (world, player);
    }

    [Fact]
    public void ARoomWithNobodyCarryingAFlagSaysSo()
    {
        (GameWorld world, ServerPlayer player) = Standing(
            new MapObject(1, 5, 1, 2, Direction.Down, 0, false) { Talks = true });

        Assert.Contains("nobody here carries a flag", Assert.Single(world.WhoIsBeingHeldBack(player)));
    }

    /// <summary>
    /// The ordinary case: a flag is clear, so the person is there and is being drawn. The
    /// two records agree and the line does not complain.
    /// </summary>
    [Fact]
    public void SomebodyWhoseFlagIsClearIsDrawnAndAgrees()
    {
        (GameWorld world, ServerPlayer player) = Standing(Carrying(1, 0x0038));

        List<string> said = world.WhoIsBeingHeldBack(player);

        Assert.Contains("0x0038", said[1]);
        Assert.Contains("clear", said[1]);
        Assert.Contains("drawn", said[1]);
        Assert.DoesNotContain("disagrees", said[1]);
    }

    /// <summary>And the other ordinary case: set, and gone, which is the world working.</summary>
    [Fact]
    public void SomebodyWhoseFlagIsSetIsNotDrawnAndAgrees()
    {
        (GameWorld world, ServerPlayer player) = Standing(Carrying(1, 0x0038));

        world.RunScript(player.Id, new Core.Net.ScriptRan([0x0038], [], []));

        List<string> said = world.WhoIsBeingHeldBack(player);

        Assert.Contains("set", said[1]);
        Assert.Contains("not drawn", said[1]);
        Assert.DoesNotContain("disagrees", said[1]);
    }

    /// <summary>
    /// The hour this was written for. The flag is clear — so the person should be on the
    /// map — and the player is not being told about them. Nothing about flags is wrong and
    /// nothing about flags will fix it, which is exactly what an hour of looking at flags
    /// failed to establish.
    /// </summary>
    [Fact]
    public void AClearFlagAndNobodyDrawnIsMarkedAsADisagreement()
    {
        (GameWorld world, ServerPlayer player) = Standing(Carrying(1, 0x0038));

        // What a lost ObjectsPlaced leaves behind: the world says they are here, and this
        // player has never been told.
        player.Seeing.Remove(1);

        Assert.Contains("disagrees", world.WhoIsBeingHeldBack(player)[1]);
    }

    /// <summary>
    /// And the mirror of it — set, and drawn anyway — because a person who should have gone
    /// and did not is the same fault pointing the other way.
    /// </summary>
    [Fact]
    public void ASetFlagAndSomebodyStillDrawnIsMarkedToo()
    {
        (GameWorld world, ServerPlayer player) = Standing(Carrying(1, 0x0038));

        player.Script.Set(0x0038);

        Assert.Contains("disagrees", world.WhoIsBeingHeldBack(player)[1]);
    }

    /// <summary>Everybody who carries one is listed, not just the first.</summary>
    [Fact]
    public void EveryoneCarryingAFlagIsListed()
    {
        (GameWorld world, ServerPlayer player) = Standing(
            Carrying(1, 0x0038), Carrying(2, 0x0037), Carrying(3, 0x00AD));

        List<string> said = world.WhoIsBeingHeldBack(player);

        Assert.Contains("3 carry a flag", said[0]);
        Assert.Equal(4, said.Count);
    }
}
