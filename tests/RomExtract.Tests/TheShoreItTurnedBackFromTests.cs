using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The sea, which this walk has never mentioned.
/// <para>
/// A frontier of "squares wanting a move nothing in the party has" reads as the whole of what
/// is in the way. It is not. This walk has no notion of water at all — the word does not appear
/// in it — so every water square was dropped as solid alongside every wall, and <em>there is
/// nothing there</em> and <em>there is a sea there and this cannot swim</em> have been the same
/// silence since the walker existed.
/// </para>
/// <para>
/// On the cartridge that is <b>1245 squares across 35 maps</b>, against a frontier naming
/// twenty squares and one move. Seventeen of the doors the run calls shut are on maps it landed
/// on and could not cross.
/// </para>
/// <para>
/// <b>Counted, not crossed.</b> Which move crosses water is something to read off the image,
/// and a walk that started swimming on a guess would open half the Sevii islands and be unable
/// to say why. A number that says how much is behind a guess is worth more than the guess.
/// </para>
/// </summary>
public class TheShoreItTurnedBackFromTests
{
    private const byte Open = 0x00;
    private const byte Solid = 0x01;

    /// <summary>Three squares in a row: where it starts, then the sea or a wall.</summary>
    private static MapData Strip(byte behaviour)
    {
        var collision = new byte[3];

        collision[1] = Solid;
        collision[2] = Solid;

        return new MapData("1.0", "1.0", 3, 1, collision)
        {
            Behaviours = [MetatileBehaviour.Normal, behaviour, behaviour],
        };
    }

    private static Reach Walk(MapData map, bool surfing = false) =>
        WorldWalker.Walk(new WorldData([map]), "1.0", [], surfing: surfing, startSquare: new GridPosition(0, 0));

    /// <summary>A square it turned back from because it is water says so.</summary>
    [Fact]
    public void WaterItTurnedBackFromIsCounted()
    {
        Reach reach = Walk(Strip(MetatileBehaviour.Water));

        Assert.Contains(reach.Shore, w => w.Square == new GridPosition(1, 0));
    }

    /// <summary>
    /// And a wall is not the sea. Without this half the number is "squares it could not enter",
    /// which is the count it already had and which says nothing new.
    /// </summary>
    [Fact]
    public void AnOrdinaryWallIsNotShore()
    {
        Reach reach = Walk(Strip(MetatileBehaviour.Normal));

        Assert.Empty(reach.Shore);
    }

    /// <summary>
    /// And it is the same question whether or not the walk is swimming: a run told to cross
    /// water has no shore left to turn back from, which is what makes the count on a run that
    /// is not swimming a measure of what the lever is worth.
    /// </summary>
    [Fact]
    public void AWalkThatCrossesWaterHasNoShore()
    {
        Assert.Empty(Walk(Strip(MetatileBehaviour.Water), surfing: true).Shore);
    }

    /// <summary>
    /// And nothing is invented on a map with no sea in it, which is most of them.
    /// </summary>
    [Fact]
    public void AMapWithNoWaterHasNoShore()
    {
        var open = new MapData("1.0", "1.0", 3, 1, new byte[3])
        {
            Behaviours = [MetatileBehaviour.Normal, MetatileBehaviour.Normal, MetatileBehaviour.Normal],
        };

        Assert.Empty(Walk(open).Shore);
    }

    /// <summary>
    /// And the playthrough passes the lever on, or <c>--surf</c> is a flag that does nothing
    /// and every number printed under it is the number without it.
    /// </summary>
    [Fact]
    public void ThePlaythroughHandsTheLeverToTheWalk()
    {
        // A door on the far side of the water, so this asks what the run REACHES and not only
        // what it counted. Two calls into the walk take this lever and the shore is filled in
        // by one of them; a test that watched the shore alone left the other unguarded.
        MapData shore = Strip(MetatileBehaviour.Water) with
        {
            Warps = [new Warp(2, 0, 0, "1.1")],
        };

        MapData across = new MapData("1.1", "1.1", 2, 1, new byte[2])
        {
            Behaviours = [MetatileBehaviour.Normal, MetatileBehaviour.Normal],
            Warps = [new Warp(0, 0, 0, "1.0")],
        };

        var world = new WorldData([shore, across]);

        PlayedScript nothing = new([], [], [], [], null, null);

        Attempt ashore = Autoplayer.Play(world, "1.0", TestRules.All, (_, _, _) => nothing);
        Attempt afloat = Autoplayer.Play(world, "1.0", TestRules.All, (_, _, _) => nothing, surfing: true);

        Assert.NotEmpty(ashore.Shore);
        Assert.DoesNotContain("1.1", ashore.Reached);

        Assert.Empty(afloat.Shore);
        Assert.Contains("1.1", afloat.Reached);
    }
}
