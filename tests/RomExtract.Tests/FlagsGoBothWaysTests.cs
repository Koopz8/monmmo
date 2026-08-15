using System.Text.RegularExpressions;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A flag the server sets is a flag the client is told about.
/// <para>
/// Flags travelled one way for the whole life of this project. The client runs the scripts,
/// so the client is what sets flags, and it reports them with <see cref="ScriptRan"/>; the
/// server writes them down and saves them. That is most of the traffic and it always worked.
/// </para>
/// <para>
/// The other direction had a message — <see cref="FlagsChanged"/>, "flags the server set
/// without being asked" — and a handler on the client, and an entry in the wire guardrail's
/// sample list. It had no sender. Not one line of the server ever built one, so every flag
/// the server set of its own accord was a fact only one side of the split knew, and there
/// was nothing anywhere to say so: the message compiled, round-tripped, and was covered.
/// </para>
/// <para>
/// What it cost was a whole errand. The server repairs a ball's flag whenever somebody walks
/// up to a ball it has already handed over — deliberately, so a lost report heals itself —
/// and it repaired the GOLD TEETH. The client was never told. Its own copy of the flag stayed
/// clear, and its own run of the WARDEN's script therefore took the branch before the teeth
/// were found: he asked for them, over and over, while they sat in the bag, and HM04 with
/// STRENGTH behind it stayed on his side of a conversation neither half could finish.
/// </para>
/// </summary>
public class FlagsGoBothWaysTests
{
    private const string Room = "1.0";
    private const int TheFlag = 0x0189;
    private const int Teeth = 353;

    /// <summary>A room with a ball on the floor, one square north of the player.</summary>
    private static (GameWorld World, ServerPlayer Player) WithABall()
    {
        MapObject ball = new(1, 5, 3, 3, Direction.Down, 0, false)
        {
            GivesItemId = Teeth,
            GivesCount = 1,
            HiddenBy = TheFlag,
        };

        MapData map = new(Room, "SAFARI ZONE", 8, 8, new byte[64]) { Objects = [ball] };

        var world = new GameWorld(new WorldData([map]), Room, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Room, 3, 4));

        world.Operators.Add("Mason");

        player.Square = new GridPosition(3, 4);
        player.Facing = Direction.Up;

        return (world, player);
    }

    private static IEnumerable<FlagsChanged> FlagsIn(IEnumerable<Outgoing> said) =>
        said.Select(o => o.Message).OfType<FlagsChanged>();

    /// <summary>Picking a ball up says which flag went with it.</summary>
    [Fact]
    public void PickingUpABallTellsTheClientWhichFlagItSet()
    {
        (GameWorld world, ServerPlayer player) = WithABall();

        List<Outgoing> said = world.StartTalking(player.Id, 1);

        Assert.Contains(FlagsIn(said), f => f.Flags.Contains(TheFlag));
    }

    /// <summary>
    /// And only to the player it happened to. A flag is one save's business; a ball picked
    /// up in front of a stranger is not the stranger's ball.
    /// </summary>
    [Fact]
    public void AndOnlyToThePlayerItHappenedTo()
    {
        (GameWorld world, ServerPlayer player) = WithABall();

        List<Outgoing> said = world.StartTalking(player.Id, 1);

        Assert.All(
            said.Where(o => o.Message is FlagsChanged),
            o => Assert.Equal(player.Id, o.OnlyTo));
    }

    /// <summary>
    /// The repair is told about too, which is the case that broke. The second walk up to a
    /// ball this save has already emptied sets nothing new on the server — but a client that
    /// lost the flag is exactly the client standing there, and it is the one that has to hear.
    /// </summary>
    [Fact]
    public void ASaveThatLostTheFlagIsToldWhenItIsRepaired()
    {
        (GameWorld world, ServerPlayer player) = WithABall();

        // The item is taken, the ledger remembers it, and then the flag goes missing —
        // which is the shape of every save this bug ever produced.
        world.StartTalking(player.Id, 1);
        player.Script.Clear(TheFlag);

        List<Outgoing> again = world.StartTalking(player.Id, 1);

        Assert.Contains(FlagsIn(again), f => f.Flags.Contains(TheFlag));
    }

    /// <summary>Nothing is said when there was nothing to set.</summary>
    [Fact]
    public void NothingIsSaidWhenTheFlagWasAlreadySet()
    {
        (GameWorld world, ServerPlayer player) = WithABall();

        world.StartTalking(player.Id, 1);

        List<Outgoing> again = world.StartTalking(player.Id, 1);

        Assert.Empty(FlagsIn(again));
    }

    /// <summary>
    /// The console's flags already landed, by a different road: it sends the whole save
    /// back, which the client takes as the truth and replaces its own copy with. Written
    /// down because it is why this bug was invisible for so long — every flag anybody ever
    /// set by hand while looking for it worked perfectly.
    /// </summary>
    [Fact]
    public void TheConsoleTellsTheClientByResendingTheWholeSave()
    {
        (GameWorld world, ServerPlayer player) = WithABall();

        List<Outgoing> said = world.RunConsole(player.Id, "/flag 0x2A5");

        Assert.Contains(
            said.Select(o => o.Message).OfType<Welcome>(),
            w => w.Flags.Contains(0x2A5));
    }

    /// <summary>And clearing one is the same road, which is why nothing here clears.</summary>
    [Fact]
    public void AndAClearedFlagGoesTheSameWay()
    {
        (GameWorld world, ServerPlayer player) = WithABall();

        world.RunConsole(player.Id, "/flag 0x2A5");

        List<Outgoing> said = world.RunConsole(player.Id, "/flag 0x2A5 off");

        Assert.DoesNotContain(
            said.Select(o => o.Message).OfType<Welcome>(),
            w => w.Flags.Contains(0x2A5));
    }

    /// <summary>
    /// The general form of the same mistake: a message with one end.
    /// <para>
    /// The wire guardrail asks that every message can be written down and read back, which
    /// <see cref="FlagsChanged"/> could. What nothing asked was whether both ends of the
    /// split had ever heard of it. A message named on one side only is not a feature that
    /// is switched off — it is a feature that reads, to anybody looking at that side, as if
    /// it were there, and it was there for months.
    /// </para>
    /// <para>
    /// Named rather than built, because most of this game's messages are made with a
    /// target-typed <c>new(...)</c> and the type is nowhere in the line that makes one. What
    /// a side cannot do is take part in a message without ever writing its name down: to
    /// send it it must name it, and to receive it it must match on it.
    /// </para>
    /// <para>
    /// This reads the sources rather than the assemblies, so a run that cannot find them
    /// fails rather than passing quietly — passing quietly is the whole bug.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryMessageIsNamedByBothSidesOfTheSplit()
    {
        DirectoryInfo root = Repository();

        string server = Sources(root, "Server");
        string client = Sources(root, "Client");

        var lonely = new List<string>();

        foreach (Type kind in typeof(NetMessage).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(NetMessage)) && !t.IsAbstract))
        {
            var word = new Regex($@"\b{Regex.Escape(kind.Name)}\b");

            bool onServer = word.IsMatch(server);
            bool onClient = word.IsMatch(client);

            if (!onServer || !onClient)
                lonely.Add($"{kind.Name} ({(onServer ? "server only" : "client only")})");
        }

        Assert.Empty(lonely);
    }

    private static string Sources(DirectoryInfo root, string side) =>
        string.Join(
            "\n",
            Directory
                .EnumerateFiles(Path.Combine(root.FullName, "src", side), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .Select(File.ReadAllText));

    /// <summary>
    /// The checkout this test is running out of, found by looking for the file that defines
    /// what it is checking.
    /// </summary>
    private static DirectoryInfo Repository()
    {
        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            if (File.Exists(Path.Combine(at.FullName, "src", "Core", "Net", "Messages.cs"))) return at;
        }

        throw new InvalidOperationException(
            $"no checkout above {AppContext.BaseDirectory} — this guardrail reads the sources and " +
            "must not pass without them");
    }
}
