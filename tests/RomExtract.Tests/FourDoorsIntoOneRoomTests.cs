using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The same command is the same movement.
/// <para>
/// A scene in this cartridge is commonly written as several tiny entry stubs, one per square
/// you can cross to start it, each announcing which door it came in by and then jumping to the
/// one block that is the scene. PEWTER CITY has two of these side by side:
/// </para>
/// <code>
/// 08165D8E  69  16 01 40 00 00  05 [08165DBE]  02     lockall; 0x4001 &lt;- 0; goto the scene
/// 08165D9A  69  16 01 40 01 00  05 [08165DBE]  02     ... and again, saying 1
/// 08165DA6  69  16 01 40 02 00  05 [08165DBE]  02     ... 2
/// 08165DB2  69  16 01 40 03 00  05 [08165DBE]  02     ... 3
/// </code>
/// <para>
/// A player crosses one square. A fixpoint stands on every square, so it runs the scene four
/// times — and all four run <b>the same</b> <c>applymovement</c> commands at the same
/// addresses. On a real cartridge the floor run reached <b>61</b> distinct movement commands
/// and asked for one <b>416</b> times.
/// </para>
/// <para>
/// So this is identity rather than a decision, and that is the whole point: what stops the
/// scene happening twice is not a flag to be hunted, it is that there was only ever one scene.
/// </para>
/// </summary>
public class FourDoorsIntoOneRoomTests
{
    /// <summary>
    /// A room with two doors one above the other, and ground on every side of them.
    /// <para>
    /// Two doors and not one, because the question is <em>how far</em> somebody was walked and
    /// one door cannot tell one step from two. Somebody standing on a door blocks it: walked
    /// down once he clears the first and stands on the second, walked twice he clears both.
    /// The counter this milestone adds cannot make that distinction and neither can a test
    /// that only reads it — which is how the first version of this file passed with the rule
    /// broken and the break came back green.
    /// </para>
    /// <para>
    /// He steps out of the line of the doors rather than along it, so a walk never cuts the
    /// route to the door it just cleared.
    /// </para>
    /// </summary>
    private static MapData Corridor() =>
        new MapData("1.0", "1.0", 4, 3, new byte[12])
        {
            Warps = [new Warp(2, 0, 0, "1.1"), new Warp(2, 1, 0, "1.2")],
        };

    private static MapObject Person(int localId, int x, int y, uint script) =>
        new(localId, 1, x, y, Direction.Down, 0, false) { ScriptAddress = script };

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// Two people whose scripts are two entry stubs into one scene: different script
    /// addresses, and a movement command inside that may or may not be the same one.
    /// <para>
    /// Person 3 starts on the upper door. Walked once he clears it and stands on the lower
    /// one; walked twice he clears both.
    /// </para>
    /// </summary>
    private static Attempt TwoDoors(uint firstSite, uint secondSite)
    {
        var world = new WorldData(
        [
            Corridor() with
            {
                Objects = [Person(1, 0, 2, 0x1000), Person(2, 1, 2, 0x2000), Person(3, 2, 0, 0)],
            },
            new MapData("1.1", "1.1", 2, 2, new byte[4]) { Warps = [new Warp(0, 0, 0, "1.0")] },
            new MapData("1.2", "1.2", 2, 2, new byte[4]) { Warps = [new Warp(0, 0, 0, "1.0")] },
        ]);

        return Autoplayer.Play(
            world,
            "1.0",
            TestRules.All,
            (address, _, _) => address switch
            {
                0x1000 => Nothing with { Walked = [(3, (IReadOnlyList<Direction>)[Direction.Down], firstSite)] },
                0x2000 => Nothing with { Walked = [(3, (IReadOnlyList<Direction>)[Direction.Down], secondSite)] },
                _ => Nothing,
            });
    }

    /// <summary>
    /// Two doors into one scene move somebody ONE square: the upper door opens and the lower
    /// one does not, because he is standing on it. Both entries execute the command at the same
    /// address; there is one movement in the cartridge and there is one here.
    /// </summary>
    [Fact]
    public void TwoEntriesIntoOneSceneMoveSomebodyOnce()
    {
        Attempt played = TwoDoors(0x08165DC0, 0x08165DC0);

        Assert.Equal(1, played.WalkSites);
        Assert.True(
            played.WalksAsked > played.WalkSites,
            $"asked {played.WalksAsked} time(s) for {played.WalkSites} command(s) — nothing was doubled up");

        Assert.Contains("1.1", played.Reached);
        Assert.DoesNotContain("1.2", played.Reached);
    }

    /// <summary>
    /// AND THE DISCRIMINATION. Two different commands are two movements and both happen, so he
    /// clears both doors — otherwise this rule would quietly delete the second half of every
    /// scene that walks somebody twice, and the test above would still pass.
    /// </summary>
    [Fact]
    public void TwoDifferentCommandsAreTwoMovements()
    {
        Attempt played = TwoDoors(0x08165DC0, 0x08165DC7);

        Assert.Equal(2, played.WalkSites);
        Assert.Contains("1.1", played.Reached);
        Assert.Contains("1.2", played.Reached);
    }

    /// <summary>
    /// AND THE OTHER DIRECTION, which the first version of this rule got wrong. One script is
    /// attached to nineteen different Pokémon Centres and one to eight gym guides — the same
    /// address, reached from eight maps, is EIGHT scenes and not one. Keyed on the address
    /// alone, seven of every eight were silently dropped.
    /// <para>
    /// Two maps here, the same script address on both, and somebody on each who has to move.
    /// </para>
    /// </summary>
    [Fact]
    public void OneScriptSharedByTwoMapsIsTwoScenes()
    {
        MapData Shared(string id) => new MapData(id, id, 4, 3, new byte[12])
        {
            Objects = [Person(1, 0, 2, 0x1000), Person(3, 2, 0, 0)],
        };

        var world = new WorldData([Shared("1.0") with { Warps = [new Warp(3, 2, 0, "1.1")] },
                                   Shared("1.1") with { Warps = [new Warp(3, 2, 0, "1.0")] }]);

        Attempt played = Autoplayer.Play(
            world,
            "1.0",
            TestRules.All,
            (address, _, _) => address == 0x1000
                ? Nothing with { Walked = [(3, (IReadOnlyList<Direction>)[Direction.Down], 0x08165DC0u)] }
                : Nothing);

        Assert.Equal(2, played.WalkSites);
        Assert.Contains(("1.0", 3), played.Moved);
        Assert.Contains(("1.1", 3), played.Moved);
    }

    /// <summary>
    /// And the count of asks is kept beside the count of commands rather than instead of it.
    /// The two being far apart is the finding; one number cannot say it.
    /// </summary>
    [Fact]
    public void ARunThatWalksNobodyReportsNeitherRatherThanZeroOfNothing()
    {
        Attempt played = Autoplayer.Play(
            new WorldData([Corridor() with { Objects = [Person(1, 0, 2, 0x1000)] }]),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing);

        Assert.Equal(0, played.WalkSites);
        Assert.Equal(0, played.WalksAsked);
        Assert.Empty(played.Moved);
    }
}
