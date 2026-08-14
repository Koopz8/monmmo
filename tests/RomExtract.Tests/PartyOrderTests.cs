using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Who goes first.
/// <para>
/// The lead is whoever sits in slot nought, and until now that was whoever had been
/// there since the day they were caught. The only ways to change it were to win — the
/// server moves past anybody who has fainted — or to faint. A player who wanted their
/// SQUIRTLE out in front of their MAGIKARP had no way to say so.
/// </para>
/// <para>
/// No machine is needed. Moving somebody to and from the box is storage and belongs at a
/// storage machine; rearranging what is already in your hands is not, and these games
/// have never asked anybody to walk to a Pokémon Center to do it.
/// </para>
/// </summary>
public class PartyOrderTests
{
    private const string Town = "1.0";

    private static (GameWorld World, ServerPlayer Player) Standing(int count = 3)
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map]), Town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(Town, 3, 4));

        player.Party = [.. Enumerable.Range(0, count).Select(Member)];

        return (world, player);
    }

    /// <summary>Levels chosen so a test can say which one moved.</summary>
    private static SavedMon Member(int which) =>
        new(3, which + 1, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

    private static PartyOrdered? Said(List<Outgoing> from) =>
        from.Select(o => o.Message).OfType<PartyOrdered>().FirstOrDefault();

    [Fact]
    public void TwoOfThemChangePlaces()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        Assert.Equal("Swapped.", Said(world.SwapParty(player.Id, 1, 2))?.Message);
        Assert.Equal([1, 3, 2], player.Party.Select(m => m.Level));
    }

    /// <summary>
    /// Named for the consequence rather than for the mechanism. "Swapped slots two and
    /// nought" is what happened; "leads now" is what it means.
    /// </summary>
    [Fact]
    public void ASwapWithTheFrontSaysWhatItMeans()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        Assert.Equal("Leading now.", Said(world.SwapParty(player.Id, 0, 2))?.Message);
        Assert.Equal([3, 2, 1], player.Party.Select(m => m.Level));
    }

    /// <summary>The party comes back with it, because the client's copy just went stale.</summary>
    [Fact]
    public void TheNewOrderComesBack()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        PartyOrdered? said = Said(world.SwapParty(player.Id, 0, 1));

        Assert.NotNull(said);
        Assert.Equal([2, 1, 3], said.Party.Select(m => m.Level));
    }

    [Fact]
    public void SwappingSomebodyWithThemselvesDoesNothing()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        Assert.Empty(world.SwapParty(player.Id, 1, 1));
        Assert.Equal([1, 2, 3], player.Party.Select(m => m.Level));
    }

    /// <summary>
    /// A slot nobody is in is refused rather than clamped. This arrives over a socket,
    /// and clamping would turn a client's mistake into a swap it never asked for.
    /// </summary>
    [Theory]
    [InlineData(0, 9)]
    [InlineData(-1, 1)]
    [InlineData(4, 5)]
    public void ASlotNobodyIsInIsRefused(int a, int b)
    {
        (GameWorld world, ServerPlayer player) = Standing();

        Assert.Empty(world.SwapParty(player.Id, a, b));
        Assert.Equal([1, 2, 3], player.Party.Select(m => m.Level));
    }

    /// <summary>
    /// Not mid-fight. The battle is holding its own copy of who is out, and the order
    /// underneath it changing is how a fight ends up with the wrong creature on screen.
    /// </summary>
    [Fact]
    public void NotInTheMiddleOfAFight()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        var factory = new BattleFactory(TestRules.All);

        player.Battle = new Encounter(0, factory.Wild(3, 5)!, [factory.Wild(3, 5)!], seed: 7);

        Assert.Contains(
            world.SwapParty(player.Id, 0, 1),
            o => o.Message is Rejected { Reason: "Not in the middle of a battle." });

        Assert.Equal([1, 2, 3], player.Party.Select(m => m.Level));
    }

    /// <summary>
    /// And what was rearranged is what gets written down. The order is the whole point of
    /// the feature, and a snapshot that carried the old one would undo it on the next
    /// sign-in.
    /// </summary>
    [Fact]
    public void TheNewOrderIsWhatIsSaved()
    {
        (GameWorld world, ServerPlayer player) = Standing();

        world.SwapParty(player.Id, 0, 2);

        SavedCharacter? saved = world.Snapshot(player.Id);

        Assert.NotNull(saved);
        Assert.Equal([3, 2, 1], saved.Party.Select(m => m.Level));
    }
}
