using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// People a scene walked to a square that is not on the map.
/// <para>
/// A scene that walks somebody aside is applied as a displacement from wherever they already
/// are, and the run is a fixpoint: it plays the scene again on every pass. Six passes, six
/// walks, and the sixth is over the edge. On a real cartridge that is 21 people standing at
/// coordinates that do not exist — one of them at <c>x = -29</c> on a map 48 wide — and the
/// blocked-doorway report is computed against exactly those positions.
/// </para>
/// <para>
/// <b>Reported and not repaired.</b> What stops the scene running twice on the cartridge is a
/// flag this project has not read; clamping the number would turn a wrong position into a
/// plausible one, which is the harder fault to find. So the run says it instead, and the
/// saying is what this guards.
/// </para>
/// </summary>
public class WalkedOffTheMapTests
{
    private static MapData Room() => new("1.0", "1.0", 4, 4, new byte[16]);

    private static MapObject Person(int localId, uint script) =>
        new(localId, 1, localId, 1, Direction.Down, 0, false) { ScriptAddress = script };

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// One person who keeps something opening so the loop runs a few passes, and one whose
    /// script walks them the same way every time it is run.
    /// </summary>
    private static Attempt Walk(bool everyPass)
    {
        MapData start = Room() with { Objects = [Person(1, 0x1000), Person(2, 0x2000)] };

        var opened = 0x100;
        var asked = 0;
        var walked = 0;

        return Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, _) => address == 0x1000
                ? new PlayedScript(asked++ < 4 ? [opened++] : [], [], [], [], null, null)
                : everyPass || walked++ == 0
                    ? Nothing with { Walked = [(2, 1, 0)] }
                    : Nothing);
    }

    /// <summary>
    /// Walked the same way on every pass, they leave the map, and the run says so with the
    /// square and the size of the map it is not on.
    /// </summary>
    [Fact]
    public void SomebodyWalkedEveryPassEndsUpOffTheMap()
    {
        Attempt played = Walk(everyPass: true);

        WalkedOffTheMap lost = Assert.Single(played.OffTheMap);

        Assert.Equal("1.0", lost.MapId);
        Assert.Equal(2, lost.LocalId);
        Assert.True(lost.To.X >= lost.Width, $"they ended at x={lost.To.X} on a map {lost.Width} wide");
    }

    /// <summary>
    /// AND THE ANSWER THAT MEANS THERE IS NOTHING WRONG. The same person, walked one square by
    /// a scene that only happens once, is still on the map — so the list is empty while the
    /// walk itself is not. An instrument that cannot come back empty is not measuring, and
    /// "nobody was walked" and "nobody was walked off" have to be different answers.
    /// </summary>
    [Fact]
    public void SomebodyWalkedOnceStaysOnTheMap()
    {
        Attempt played = Walk(everyPass: false);

        Assert.NotEmpty(played.Moved);
        Assert.Empty(played.OffTheMap);
    }
}
