using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Whether anything was handed over twice, which the run has never said.
/// <para>
/// The party has said it for a while — <em>a second copy of something already in it</em> —
/// and the bag never has. An item off the floor is kept from refilling by the flag on the
/// object's own record; an item somebody hands over is kept from refilling by a guard inside
/// their own script, and one of those two was read and the other was not. Eight gym leaders
/// handed their TM over once per pass for ever underneath a run that reported nothing unusual.
/// </para>
/// <para>
/// Both halves are asserted here: that a repeat is seen, and that a run with no repeat says
/// so with its denominator. "None of them twice" and "nothing hands anything over" printed
/// the same as each other before this, which is the shape this project keeps finding.
/// </para>
/// </summary>
public class TakingTheSameGiftTwiceTests
{
    private const int Present = 0x0147;

    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static MapObject Person(int localId, uint script) =>
        new(localId, 1, localId, 1, Direction.Down, 0, false) { ScriptAddress = script };

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// One person who opens something for four passes so the walk keeps going, and one who
    /// hands a present over — either every time, or only the first time.
    /// </summary>
    private static Attempt Walk(bool everyPass)
    {
        MapData start = Room("1.0") with { Objects = [Person(1, 0x1000), Person(2, 0x2000)] };

        var opened = 0x100;
        var asked = 0;
        var handed = 0;

        return Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, _) => address == 0x1000
                ? new PlayedScript(asked++ < 4 ? [opened++] : [], [], [], [], null, null)
                : everyPass || handed++ == 0
                    ? Nothing with { Gets = (Present, 1) }
                    : Nothing);
    }

    /// <summary>A script that hands the same thing over on every pass is reported as one.</summary>
    [Fact]
    public void SomethingHandedOverOnEveryPassIsSaidSo()
    {
        HandedOver again = Assert.Single(Walk(everyPass: true).Handovers);

        Assert.True(
            again.Passes.Count > 1,
            $"it was handed over on pass(es) {string.Join(",", again.Passes)}, so nothing repeated");
    }

    /// <summary>
    /// And a script that hands it over once is still counted — as a place that handed
    /// something over, on one pass. The denominator is the half of this that can come back
    /// empty, and a run with nothing to report has to be distinguishable from a run that
    /// never looked.
    /// </summary>
    [Fact]
    public void SomethingHandedOverOnceIsCountedAndNotRepeated()
    {
        HandedOver once = Assert.Single(Walk(everyPass: false).Handovers);

        Assert.Single(once.Passes);
        Assert.Contains($"0x{Present:X3}", once.What);
    }
}
