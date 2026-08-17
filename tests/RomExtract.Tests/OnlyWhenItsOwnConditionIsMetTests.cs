using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Whether a script's own condition is honoured, which is what the floor means.
/// <para>
/// A trigger and an arrival script each carry a variable and a value. This walk has always run
/// them regardless, which makes the run a <b>ceiling</b> in that respect: it takes arms of the
/// story no single playthrough could take in one pass. Honoured, the same walk is a floor.
/// </para>
/// <para>
/// Neither is the truth on its own, so it is a lever rather than a decision — the same shape as
/// <c>--boat</c> and <c>--surf</c>, and for the same reason.
/// </para>
/// </summary>
public class OnlyWhenItsOwnConditionIsMetTests
{
    private const int Counter = 0x4055;
    private const int Arrived = 0x0321;

    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>A map whose arrival script only fires when the counter holds one.</summary>
    private static WorldData World() =>
        new([Room("1.0") with { OnEntry = [new MapEntryScript(Counter, 1, 0x1000)] }]);

    private static Attempt Run(bool inOrder, IReadOnlyDictionary<int, int>? remembered = null) =>
        Autoplayer.Play(
            World(),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing with { FlagsSet = [Arrived] },
            remembered: remembered,
            inOrder: inOrder);

    /// <summary>
    /// Not honouring it runs the scene whatever the counter holds, which is the ceiling this
    /// project has been quoting as a floor.
    /// </summary>
    [Fact]
    public void WithoutTheLeverAnArrivalScriptRunsWhateverTheCounterHolds() =>
        Assert.Contains(Arrived, Run(inOrder: false).Flags);

    /// <summary>And honouring it, the scene waits for its own number.</summary>
    [Fact]
    public void WithTheLeverItWaitsForItsOwnNumber() =>
        Assert.DoesNotContain(Arrived, Run(inOrder: true).Flags);

    /// <summary>
    /// And runs once the number is there — or the lever is not a condition, it is an off switch,
    /// and every scene in the game behind a counter would be unreachable for ever.
    /// </summary>
    [Fact]
    public void AndRunsOnceTheNumberIsThere() =>
        Assert.Contains(Arrived, Run(inOrder: true, new Dictionary<int, int> { [Counter] = 1 }).Flags);
}
