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
    /// <summary>Four squares of ground, so a walk has somewhere to go.</summary>
    private static MapData Room() => new("1.0", "1.0", 4, 4, new byte[16]);

    private static MapObject Person(int localId, int x, int y, uint script) =>
        new(localId, 1, x, y, Direction.Down, 0, false) { ScriptAddress = script };

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// Two people whose scripts are two entry stubs into one scene: different script
    /// addresses, and the same <c>applymovement</c> command inside.
    /// </summary>
    private static Attempt TwoDoors(uint firstSite, uint secondSite)
    {
        MapData start = Room() with
        {
            Objects = [Person(1, 0, 0, 0x1000), Person(2, 1, 0, 0x2000), Person(3, 1, 1, 0)],
        };

        return Autoplayer.Play(
            new WorldData([start]),
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
    /// Two doors into one scene move somebody once. Both entries execute the command at
    /// <c>0x08165DC0</c>; there is one movement in the cartridge and there is one here.
    /// </summary>
    [Fact]
    public void TwoEntriesIntoOneSceneMoveSomebodyOnce()
    {
        Attempt played = TwoDoors(0x08165DC0, 0x08165DC0);

        Assert.Equal(1, played.WalkSites);
        Assert.True(
            played.WalksAsked > played.WalkSites,
            $"it was asked {played.WalksAsked} time(s) for {played.WalkSites} command(s), so nothing was doubled up");
    }

    /// <summary>
    /// AND THE DISCRIMINATION. Two different commands are two movements, and both happen —
    /// otherwise this rule would quietly delete the second half of every scene that walks
    /// somebody twice, and every test above would still pass.
    /// </summary>
    [Fact]
    public void TwoDifferentCommandsAreTwoMovements()
    {
        Attempt played = TwoDoors(0x08165DC0, 0x08165DC7);

        Assert.Equal(2, played.WalkSites);
    }

    /// <summary>
    /// And the count of asks is kept beside the count of commands rather than instead of it.
    /// The two being far apart is the finding; one number cannot say it.
    /// </summary>
    [Fact]
    public void ARunThatWalksNobodyReportsNeitherRatherThanZeroOfNothing()
    {
        Attempt played = Autoplayer.Play(
            new WorldData([Room() with { Objects = [Person(1, 0, 0, 0x1000)] }]),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing);

        Assert.Equal(0, played.WalkSites);
        Assert.Equal(0, played.WalksAsked);
        Assert.Empty(played.Moved);
    }
}
