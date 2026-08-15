using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Saying something, and who hears it.
/// <para>
/// This is the piece the instancing work made necessary rather than optional. A place can
/// have more than one copy of itself, and two people standing in the same town can be in
/// copies that cannot see each other — from inside, indistinguishable from the other person
/// not being there. <c>/with</c> was the answer to that, and it needs somebody to tell you
/// their name.
/// </para>
/// <para>
/// So the unit a room means here is a <em>copy</em>, not a map. Everything below is really
/// one claim asked several ways: words reach exactly the people who are in the room with
/// you, and nobody else.
/// </para>
/// </summary>
public class ChatTests
{
    private const string Town = "1.0";

    private static GameWorld World(int copies = 1)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        return world;
    }

    private static ServerPlayer Arrive(GameWorld world, long accountId, string name)
    {
        (ServerPlayer player, _) = world.Join(accountId, name, SavedCharacter.Fresh(Town, 3, 4));

        return player;
    }

    private static ChatSaid Only(List<Outgoing> from) =>
        Assert.Single(from.Select(o => o.Message).OfType<ChatSaid>());

    [Fact]
    public void SayingSomethingReachesTheRoom()
    {
        GameWorld world = World();
        ServerPlayer mason = Arrive(world, 1, "Mason");

        List<Outgoing> said = world.Say(mason.Id, "hello", null, 10);

        ChatSaid heard = Only(said);

        Assert.Equal("Mason", heard.Name);
        Assert.Equal("hello", heard.Text);
        Assert.False(heard.Private);

        // Aimed at the copy of the place, which is the unit a room means here.
        Assert.Equal(mason.Where, Assert.Single(said).OnMap);
    }

    /// <summary>
    /// And a whisper reaches two people, reading differently at each end. One of them needs
    /// to see who it went to and the other who it came from.
    /// </summary>
    [Fact]
    public void AWhisperReachesTwoPeopleAndReadsDifferently()
    {
        GameWorld world = World();

        ServerPlayer mason = Arrive(world, 1, "Mason");
        ServerPlayer koop = Arrive(world, 2, "Koop");

        List<Outgoing> said = world.Say(mason.Id, "psst", "Koop", 10);

        Assert.Equal(2, said.Count);

        ChatSaid mine = said.Select(o => o.Message).OfType<ChatSaid>().Single(c => c.Mine);
        ChatSaid theirs = said.Select(o => o.Message).OfType<ChatSaid>().Single(c => !c.Mine);

        // The sender's copy names who it went to; the receiver's names who it came from.
        Assert.Equal("Koop", mine.Name);
        Assert.Equal("Mason", theirs.Name);

        Assert.True(mine.Private);
        Assert.True(theirs.Private);

        Assert.Equal(mason.Id, said.Single(o => o.Message == mine).OnlyTo);
        Assert.Equal(koop.Id, said.Single(o => o.Message == theirs).OnlyTo);
    }

    /// <summary>And a whisper to nobody is refused rather than shouted.</summary>
    [Fact]
    public void AWhisperToNobodyIsRefused()
    {
        GameWorld world = World();
        ServerPlayer mason = Arrive(world, 1, "Mason");

        List<Outgoing> said = world.Say(mason.Id, "psst", "Nobody", 10);

        Assert.Empty(said.Select(o => o.Message).OfType<ChatSaid>());
        Assert.Single(said.Select(o => o.Message).OfType<Rejected>());
        Assert.Contains("nobody is called", world.LastChat ?? "");
    }

    /// <summary>Whispering to yourself sends one copy rather than two.</summary>
    [Fact]
    public void AndWhisperingToYourselfIsNotEchoedTwice()
    {
        GameWorld world = World();
        ServerPlayer mason = Arrive(world, 1, "Mason");

        Assert.Single(world.Say(mason.Id, "talking to myself", "Mason", 10));
    }

    /// <summary>
    /// The rule the whole feature waited for: two people in different copies of one place
    /// do not hear each other. A chat scoped to the map would put words in the mouths of
    /// people who are not there.
    /// </summary>
    [Fact]
    public void TwoCopiesOfAPlaceAreTwoRooms()
    {
        GameWorld world = World();

        ServerPlayer mason = Arrive(world, 1, "Mason");
        ServerPlayer koop = Arrive(world, 2, "Koop");

        // Put them in different copies of the same map, which is a state the world reaches
        // on its own once a room is busy.
        world.Locked(koop.Id, p => p.Copy = mason.Copy + 1);

        Outgoing said = Assert.Single(world.Say(mason.Id, "anybody there?", null, 10));

        Assert.Equal(mason.Where, said.OnMap);
        Assert.NotEqual(koop.Where, said.OnMap);
    }

    // ---- what is refused ---------------------------------------------------------------

    [Fact]
    public void NothingIsSaidTooOften()
    {
        GameWorld world = World();
        ServerPlayer mason = Arrive(world, 1, "Mason");

        Assert.NotEmpty(world.Say(mason.Id, "one", null, 10));

        List<Outgoing> again = world.Say(mason.Id, "two", null, 10 + (GameWorld.LeastChatSeconds / 2));

        Assert.Empty(again.Select(o => o.Message).OfType<ChatSaid>());
        Assert.Single(again.Select(o => o.Message).OfType<Rejected>());
        Assert.Contains("refused", world.LastChat ?? "");

        // And once the gap has passed, it goes.
        Assert.NotEmpty(
            world.Say(mason.Id, "three", null, 10 + GameWorld.LeastChatSeconds).Select(o => o.Message).OfType<ChatSaid>());
    }

    /// <summary>
    /// Cut rather than refused. Somebody who typed one character too many meant the first
    /// hundred and twenty of them.
    /// </summary>
    [Fact]
    public void ALongLineIsCutRatherThanRefused()
    {
        GameWorld world = World();
        ServerPlayer mason = Arrive(world, 1, "Mason");

        ChatSaid heard = Only(world.Say(mason.Id, new string('a', GameWorld.LongestLine * 3), null, 10));

        Assert.Equal(GameWorld.LongestLine, heard.Text.Length);
    }

    /// <summary>
    /// A newline in a chat line is a way to draw over somebody else's, so what is not a
    /// character goes before anybody sees it.
    /// </summary>
    [Fact]
    public void AndWhatIsNotACharacterGoes()
    {
        GameWorld world = World();
        ServerPlayer mason = Arrive(world, 1, "Mason");

        ChatSaid heard = Only(world.Say(mason.Id, "up\nhere\tand\rthere", null, 10));

        Assert.DoesNotContain('\n', heard.Text);
        Assert.DoesNotContain('\r', heard.Text);
        Assert.DoesNotContain('\t', heard.Text);
        Assert.Equal("uphereandthere", heard.Text);
    }

    [Fact]
    public void AndSayingNothingSaysNothing()
    {
        GameWorld world = World();
        ServerPlayer mason = Arrive(world, 1, "Mason");

        Assert.Empty(world.Say(mason.Id, "   ", null, 10));
        Assert.Empty(world.Say(mason.Id, "", null, 10));

        // And a line of nothing but control characters is a line of nothing.
        Assert.Empty(world.Say(mason.Id, "\n\t\r", null, 10));
    }

    /// <summary>And nobody who is not in the world says anything at all.</summary>
    [Fact]
    public void AndNobodyOutsideTheWorldSaysAnything()
    {
        GameWorld world = World();

        Assert.Empty(world.Say(404, "hello", null, 10));
    }
}
