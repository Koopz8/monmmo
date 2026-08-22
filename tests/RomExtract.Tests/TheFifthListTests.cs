using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The fifth list: read since 224, exported by nothing, run by nothing (307).
/// <para>
/// A map's own script list holds entries of several kinds. Two of them point at a table of
/// variable, value and script and became <see cref="MapData.OnEntry"/>; the rest point straight
/// at a script and were <b>read on the cartridge side and never exported</b>, so every walk over
/// the world record went over maps with no unconditional scripts at all. That is 239's fault one
/// list further on, and it cost nine maps: <c>2.1 TRAINER TOWER</c>'s own script sets flag
/// <c>0x0005</c> at <c>0x081C4F62</c>, nothing else the run reaches ever moves it, and 306 left
/// "what sets 0x0005" open because the only thing that does is in a list the run could not see.
/// </para>
/// <para>
/// <b>Carrying them is not running them.</b> When the cartridge runs one is inside compiled code,
/// so <c>--on-load</c> is a lever and is marked MODELLED. These tests guard the three rules that
/// makes: what the export keeps, that the walk runs it only when told, and that it runs FIRST.
/// </para>
/// </summary>
public sealed class TheFifthListTests
{
    private const int Kind = 3;

    private const int Conditional = 2;

    private const uint TheMapsOwnScript = 0x08005000;

    private const uint OnArrival = 0x08006000;

    private const uint APerson = 0x08007000;

    private const int Opened = 0x0005;

    private const int Counter = 0x4055;

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    // ------------------------------------------------------ what the export keeps

    /// <summary>
    /// The filter, asked of a list with <b>one of every shape</b> in it.
    /// </summary>
    /// <remarks>
    /// 35's rule. A fixture built out of whatever the code happens to handle is satisfied by
    /// whatever the code happens to handle; this one carries both conditional kinds, two
    /// unconditional ones, and a nought pointer, and names in the assertion exactly which survive.
    /// </remarks>
    [Fact]
    public void TheExportKeepsEveryUnconditionalEntryAndNoConditionalOne()
    {
        List<MapScriptOnLoad> kept = MapScripts.Unconditional(
        [
            new MapScriptEntry(1, 0x08001000),
            new MapScriptEntry(2, 0x08002000),
            new MapScriptEntry(3, 0x08003000),
            new MapScriptEntry(4, 0x08004000),
            new MapScriptEntry(5, 0),
        ]);

        Assert.Equal(
            [
                new MapScriptOnLoad(1, 0x08001000),
                new MapScriptOnLoad(3, 0x08003000),
            ],
            kept);
    }

    /// <summary>
    /// And the kind byte survives the crossing.
    /// <para>
    /// It is the only thing in the data that says anything at all about <em>when</em> a script
    /// runs, so an export that threw it away would make "ask this per kind" impossible without
    /// anything looking wrong. Asserted separately from the filter, because a version that kept
    /// the right entries and stamped them all kind 0 passes the test above.
    /// </para>
    /// </summary>
    [Fact]
    public void AndTheKindByteCrossesWithIt()
    {
        List<MapScriptOnLoad> kept = MapScripts.Unconditional(
            [new MapScriptEntry(7, 0x08009000), new MapScriptEntry(1, 0x0800A000)]);

        Assert.Equal([7, 1], kept.Select(e => e.Kind));
    }

    // ------------------------------------------------------------------ the lever

    /// <summary>One map: an unconditional script, an arrival script, and somebody to talk to.</summary>
    /// <remarks>
    /// The person is behind <see cref="Opened"/>, which is what the map's own script sets — the
    /// real shape on <c>2.1</c>, where setting a flag is what takes the person out of the doorway.
    /// Here it is the other way round so that a run which never opens it has strictly less to do,
    /// which is what makes the flag count the thing to assert.
    /// </remarks>
    private static Attempt Run(bool onLoad, out List<uint> ran)
    {
        var order = new List<uint>();
        ran = order;

        var remembered = new Dictionary<int, int>();

        MapData start = new MapData("1.0", "1.0", 4, 4, new byte[16])
        {
            OnLoad = [new MapScriptOnLoad(Kind, TheMapsOwnScript)],
            OnEntry = [new MapEntryScript(0, 0, OnArrival)],
            Objects =
            [
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = APerson },
            ],
        };

        return Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, _) =>
            {
                order.Add(address);

                if (address == TheMapsOwnScript) return Nothing with { FlagsSet = [Opened] };

                if (address == OnArrival)
                {
                    // WHAT THE ARRIVAL SCRIPT READS IS WHAT THE MAP'S OWN SCRIPT LEFT.
                    //
                    // The order test turns on this and not on the list `ran`: a list of
                    // addresses says which ran first and cannot say whether running first
                    // MATTERED. Here it does — the arrival script only advances the counter
                    // when the flag is already on, so a walk that runs the fifth list second
                    // leaves the counter at nought and the person hands over nothing.
                    int was = remembered.GetValueOrDefault(Counter);

                    if (order.Contains(TheMapsOwnScript)) remembered[Counter] = 1;

                    return Nothing with
                    {
                        Touched = [new VariableTouch(Counter, true, was, remembered.GetValueOrDefault(Counter))],
                    };
                }

                return Nothing;
            },
            null,
            false,
            0,
            null,
            false,
            remembered,
            false,
            null,
            runOnLoad: onLoad);
    }

    /// <summary>
    /// <b>Off by default.</b> The walk does not run a map's own unconditional scripts unless it
    /// is told to, because when the cartridge runs one is not in the data.
    /// </summary>
    [Fact]
    public void TheWalkDoesNotRunTheFifthListUnlessTheLeverIsOn()
    {
        Attempt played = Run(false, out List<uint> ran);

        Assert.DoesNotContain(TheMapsOwnScript, ran);
        Assert.DoesNotContain(Opened, played.Flags);
    }

    /// <summary>And with the lever it runs, and what it moves is in the run.</summary>
    [Fact]
    public void AndWithTheLeverItRunsAndWhatItSetsIsInTheRun()
    {
        Attempt played = Run(true, out List<uint> ran);

        Assert.Contains(TheMapsOwnScript, ran);
        Assert.Contains(Opened, played.Flags);
    }

    /// <summary>
    /// <b>And it runs FIRST</b> — before the map's arrival scripts, before the triggers, before
    /// anybody is talked to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 239's argument, one list over: a map's own script list is what the cartridge runs to bring
    /// the map up, and a conditional entry in the SAME list is checked against variables those
    /// scripts may have just written. Running them afterwards is not a stricter reading or a
    /// looser one, it is an order the cartridge cannot produce.
    /// </para>
    /// <para>
    /// Asserted on what the order DID and not only on the order itself. 119's costume: a fixture
    /// whose subject is an ordering has to make the second thing the one that matters, or a walk
    /// that runs them in either order passes it.
    /// </para>
    /// </remarks>
    [Fact]
    public void AndItRunsBeforeTheScriptsTheMapRunsOnArrival()
    {
        Attempt played = Run(true, out List<uint> ran);

        Assert.True(
            ran.IndexOf(TheMapsOwnScript) < ran.IndexOf(OnArrival),
            "the map's own script list ran after the scripts it runs on arrival");

        // And the arrival script saw it: it only writes the counter once the flag is on.
        Traced wrote = played.Trace.First(t => t.What.Wrote && t.What.Variable == Counter);

        Assert.Equal(1, wrote.What.Value);
    }

    // --------------------------------------------------------------- the bucketing

    /// <summary>
    /// Which of the five kinds a script came from, decided off the label the shared list already
    /// writes rather than off a second walk of the same tables.
    /// </summary>
    /// <remarks>
    /// Every count in <c>--the-fifth-list</c> hangs on this. A version that put "on arrival" in
    /// the "on load" bucket would report the walk as blind to a list it has run since 176, and
    /// nothing else in the output would look wrong — so all five are named here rather than
    /// counted.
    /// </remarks>
    [Theory]
    [InlineData("person 1", "person")]
    [InlineData("trigger (5,15)", "trigger")]
    [InlineData("sign (9,43)", "sign")]
    [InlineData("on arrival (0x4055 == 3)", "on arrival")]
    [InlineData("on load (kind 3)", "on load")]
    public void EachOfTheFiveKindsIsBucketedByItsOwnName(string what, string kind) =>
        Assert.Equal(kind, TheFifthList.KindOf(what));

    /// <summary>
    /// And the two that start with the same word are not confused: "on arrival" and "on load"
    /// share a prefix, and a prefix test on "on" would put every arrival script in the fifth list.
    /// </summary>
    [Fact]
    public void AndTheTwoKindsThatBothBeginWithOnAreToldApart() =>
        Assert.NotEqual(
            TheFifthList.KindOf("on arrival (0x4055 == 3)"),
            TheFifthList.KindOf("on load (kind 3)"));

    /// <summary>
    /// The conditional kinds are exactly 2 and 4 — named, not counted.
    /// <para>
    /// This one rule decides what the export drops and what the walk may run, and it is asked in
    /// two places. A version that answered true for a third kind would silently take a list of
    /// real scripts out of the world and nothing would report a shortfall.
    /// </para>
    /// </summary>
    [Fact]
    public void OnlyKindsTwoAndFourPointAtATableOfConditions()
    {
        Assert.True(MapScripts.IsConditional(2));
        Assert.True(MapScripts.IsConditional(4));

        foreach (byte kind in new byte[] { 0, 1, 3, 5, 6, 7 })
            Assert.False(MapScripts.IsConditional(kind), $"kind {kind} is not a table of conditions");
    }
}
