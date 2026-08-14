using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Somewhere to put a seventh.
/// <para>
/// A catch used to disappear. The fight said "Gotcha!", the party was already six, and
/// the line that adds it checked for room and quietly did nothing — a loss a player
/// could watch happen and could not prove. The check was there; the else was not.
/// </para>
/// <para>
/// So there had to be somewhere to put it, and somewhere has a size. That size is in no
/// table — box storage lives in the save file, whose layout this project does not read —
/// but the game says it out loud to anybody who asks the man in the Pokémon Center, and
/// so it is read out of the sentence. How many boxes there are is said nowhere at all,
/// so there is one, and that is stated rather than guessed.
/// </para>
/// </summary>
public class BoxTests
{
    private const string Town = "1.0";

    /// <summary>
    /// A room with a machine on one square, and the player standing in front of it.
    /// <para>
    /// The machine matters: every one of these operations is refused anywhere else, so a
    /// fixture on bare ground would test the refusal and nothing else.
    /// </para>
    /// </summary>
    private static (GameWorld World, ServerPlayer Player) Standing(
        int party = 1, int stored = 0, bool atAMachine = true)
    {
        var behaviours = new byte[64];

        // (3,3), which is the square the player at (3,4) faces when looking up.
        if (atAMachine) behaviours[3 * 8 + 3] = MetatileBehaviour.Computer;

        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]) { Behaviours = behaviours };

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4) with
        {
            Facing = Direction.Up,
        });

        player.Party = [.. Enumerable.Range(0, party).Select(Member)];
        player.Box = [.. Enumerable.Range(100, stored).Select(Member)];

        return (world, player);
    }

    /// <summary>Levels chosen so a test can say which one moved.</summary>
    private static SavedMon Member(int which) =>
        new(3, which + 1, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

    private static BoxUpdated? Said(List<Outgoing> from) =>
        from.Select(o => o.Message).OfType<BoxUpdated>().FirstOrDefault();

    // ---- the size, read out of a sentence --------------------------------------------

    /// <summary>
    /// The phrase and a number after it. Written as a locator rather than a constant
    /// because thirty is a thing this project would otherwise be remembering, and the
    /// standing rule against remembering is why half of this is right.
    /// </summary>
    [Fact]
    public void TheSizeIsReadOutOfWhatTheGameSays()
    {
        Assert.Equal(30, BoxCapacity.Locate(new Rom(Saying("Each BOX can hold up to 30 POKeMON."))));
    }

    [Fact]
    public void ADifferentCartridgeCouldSaySomethingElse()
    {
        Assert.Equal(7, BoxCapacity.Locate(new Rom(Saying("Each BOX can hold up to 7 POKeMON."))));
    }

    /// <summary>
    /// A cartridge that never says it has no box, rather than a box of a size somebody
    /// made up. The same answer this project gives whenever a reading does not come out.
    /// </summary>
    [Fact]
    public void ACartridgeThatNeverSaysHasNoBox()
    {
        Assert.Null(BoxCapacity.Locate(new Rom(Saying("Each BOX is a nice place to be."))));
    }

    [Fact]
    public void AnAbsurdNumberIsNotAnAnswer()
    {
        Assert.Null(BoxCapacity.Locate(new Rom(Saying("Each BOX can hold up to 0 POKeMON."))));
    }

    /// <summary>Two sentences disagreeing is the phrase having been matched wrongly.</summary>
    [Fact]
    public void TwoSentencesDisagreeingAreNotBelieved()
    {
        var image = new byte[0x2000];

        Plant(image, 0x100, "Each BOX can hold up to 30 POKeMON.");
        Plant(image, 0x800, "Each BOX can hold up to 12 POKeMON.");

        Assert.Null(BoxCapacity.Locate(new Rom(image)));
    }

    private static byte[] Saying(string text)
    {
        var image = new byte[0x2000];
        Plant(image, 0x100, text);
        return image;
    }

    /// <summary>
    /// The sentence, in the cartridge's own encoding.
    /// <para>
    /// On one line, where the real image puts a break between "to" and the number. The
    /// locator joins the decoded lines before looking for a number precisely so that it
    /// does not care, and the real image is what exercises the other arrangement.
    /// </para>
    /// </summary>
    private static void Plant(byte[] image, int at, string text)
    {
        byte[] encoded = GameText.EncodeAnchor(text);

        for (int i = 0; i < encoded.Length; i++) image[at + i] = encoded[i];
    }

    // ---- and what the server does with it --------------------------------------------

    [Fact]
    public void TheSizeReachesTheServer()
    {
        (GameWorld world, _) = Standing();

        Assert.Equal(TestRules.BoxSize, world.BoxSize);
    }

    [Fact]
    public void SomebodyGoesIntoTheBoxAndComesBackOut()
    {
        (GameWorld world, ServerPlayer player) = Standing(party: 2);

        Assert.Equal("Into the box.", Said(world.Deposit(player.Id, 1))?.Message);
        Assert.Single(player.Party);
        Assert.Single(player.Box);
        Assert.Equal(2, player.Box[0].Level);

        Assert.Equal("Out of the box.", Said(world.Withdraw(player.Id, 0))?.Message);
        Assert.Equal(2, player.Party.Count);
        Assert.Empty(player.Box);
    }

    /// <summary>
    /// The last one able to fight never goes. A player with nothing standing cannot
    /// start a battle, cannot be challenged out of it, and has no way back except a
    /// centre they may be a very long walk from.
    /// </summary>
    [Fact]
    public void TheLastOneStandingStays()
    {
        (GameWorld world, ServerPlayer player) = Standing(party: 1);

        Assert.Equal("That's the last one that can fight.", Said(world.Deposit(player.Id, 0))?.Message);
        Assert.Single(player.Party);
        Assert.Empty(player.Box);
    }

    /// <summary>
    /// And somebody fainted may go, because they were never the one holding the party
    /// up. A rule written as "the last member" rather than "the last one that can
    /// fight" would trap a player with five fainted creatures and one healthy one.
    /// </summary>
    [Fact]
    public void SomebodyFaintedIsNotTheLastOneStanding()
    {
        (GameWorld world, ServerPlayer player) = Standing(party: 2);

        player.Party[0] = player.Party[0] with { CurrentHp = 0 };

        Assert.Equal("Into the box.", Said(world.Deposit(player.Id, 0))?.Message);
        Assert.Single(player.Box);
    }

    [Fact]
    public void NothingComesOutIntoAFullParty()
    {
        (GameWorld world, ServerPlayer player) = Standing(party: 6, stored: 1);

        Assert.Equal("The party is full.", Said(world.Withdraw(player.Id, 0))?.Message);
        Assert.Single(player.Box);
    }

    [Fact]
    public void NothingGoesIntoAFullBox()
    {
        (GameWorld world, ServerPlayer player) = Standing(party: 2, stored: TestRules.BoxSize);

        Assert.Equal("The box is full.", Said(world.Deposit(player.Id, 1))?.Message);
        Assert.Equal(2, player.Party.Count);
    }

    // ---- which is what it was all for ------------------------------------------------

    /// <summary>
    /// A seventh catch. This is the whole milestone: before it, this creature stopped
    /// existing between the ball closing and the screen saying "Gotcha!".
    /// </summary>
    [Fact]
    public void ASeventhCatchGoesToTheBox()
    {
        (GameWorld world, ServerPlayer player) = Standing(party: 6);

        Assert.Equal(GameWorld.Kept.InTheBox, world.Catch(player.Id, Member(200)));

        Assert.Equal(6, player.Party.Count);
        Assert.Single(player.Box);
        Assert.Equal(201, player.Box[0].Level);
    }

    [Fact]
    public void AnythingWithRoomForItStillJoinsTheParty()
    {
        (GameWorld world, ServerPlayer player) = Standing(party: 2);

        Assert.Equal(GameWorld.Kept.InTheParty, world.Catch(player.Id, Member(200)));

        Assert.Equal(3, player.Party.Count);
        Assert.Empty(player.Box);
    }

    /// <summary>
    /// Both full is a refusal rather than a loss. It cannot be reached by anything the
    /// server currently allows, and it returns an answer anyway — a branch that silently
    /// drops a creature is the thing this milestone exists to remove.
    /// </summary>
    [Fact]
    public void BothFullIsAnAnswerRatherThanSilence()
    {
        (GameWorld world, ServerPlayer player) = Standing(party: 6, stored: TestRules.BoxSize);

        Assert.Equal(GameWorld.Kept.Nowhere, world.Catch(player.Id, Member(200)));

        Assert.Equal(6, player.Party.Count);
        Assert.Equal(TestRules.BoxSize, player.Box.Count);
    }

    // ---- and it is still there tomorrow ----------------------------------------------

    [Fact]
    public async Task WhatIsInTheBoxSurvivesASignOut()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        SavedCharacter character = SavedCharacter.Fresh(Town, 1, 1) with
        {
            Party = [Member(0), Member(1)],
            Box = [Member(50), Member(51), Member(52)],
        };

        Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync("Mason", "a-good-password", character));

        var back = (AuthOutcome.Success)await store.LoginAsync("Mason", "a-good-password");

        Assert.Equal(2, back.Character.Party.Count);
        Assert.Equal(3, back.Character.Box.Count);

        // In order, and not muddled with the party. Both lists live in one table, told
        // apart by a column, and the failure this pins is the one where they come back
        // as one list of five.
        Assert.Equal([1, 2], back.Character.Party.Select(m => m.Level));
        Assert.Equal([51, 52, 53], back.Character.Box.Select(m => m.Level));
    }

    /// <summary>
    /// And the message that ends a fight carries it.
    /// <para>
    /// Found by playing. Only a catch into a full party can change the box mid-fight,
    /// which is rare enough that the first version left it out — and then the screen
    /// opened on a box that was accurate at login and did not contain the creature the
    /// player had just been told went into it.
    /// </para>
    /// </summary>
    [Fact]
    public void TheEndOfAFightSaysWhatIsInTheBox()
    {
        var finished = new BattleFinished(Side.Player, true, 0, 0, [], [Member(0)])
        {
            ToTheBox = true,
            Box = [Member(9)],
        };

        Assert.True(finished.ToTheBox);
        Assert.Single(finished.Box);
    }

    /// <summary>
    /// And none of it happens anywhere else. The games put the box behind a machine and
    /// so does this — it is the one interaction in the game a player could otherwise do
    /// from the middle of a route.
    /// </summary>
    [Fact]
    public void NoneOfItWorksAwayFromAMachine()
    {
        (GameWorld world, ServerPlayer player) = Standing(party: 2, stored: 1, atAMachine: false);

        Assert.Equal("There is no machine here.", Said(world.Deposit(player.Id, 1))?.Message);
        Assert.Equal("There is no machine here.", Said(world.Withdraw(player.Id, 0))?.Message);

        Assert.Equal(2, player.Party.Count);
        Assert.Single(player.Box);
    }

    /// <summary>
    /// A catch still reaches the box from wherever it happened. What the machine gates
    /// is moving things about on purpose, not a creature having nowhere else to go.
    /// </summary>
    [Fact]
    public void ACatchStillReachesTheBoxOutOnARoute()
    {
        (GameWorld world, ServerPlayer player) = Standing(party: 6, atAMachine: false);

        Assert.Equal(GameWorld.Kept.InTheBox, world.Catch(player.Id, Member(200)));
        Assert.Single(player.Box);
    }

    /// <summary>What is written down goes back where the player left it.</summary>
    [Fact]
    public void ASnapshotCarriesTheBox()
    {
        (GameWorld world, ServerPlayer player) = Standing(party: 2, stored: 2);

        SavedCharacter? saved = world.Snapshot(player.Id);

        Assert.NotNull(saved);
        Assert.Equal(2, saved.Box.Count);
    }
}
