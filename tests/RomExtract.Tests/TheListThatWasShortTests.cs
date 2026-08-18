using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// 221 replaced five private copies of "every script on a map" with one shared list — and the
/// shared one had three of the five kinds.
/// <para>
/// People, triggers and signs were in it. The scripts a map runs <b>on arrival</b> and the
/// entries in the map's <b>own script list</b> were not, and those two were added to
/// <see cref="WhatItIsWaitingFor.EveryScriptOn"/> at 176 and 179 precisely because "nothing in
/// the world sets this flag" had turned out three times running to be a sentence about a scan
/// that never opened one.
/// </para>
/// <para>
/// So every instrument moved onto the shared list read 2331 script entries where
/// <c>--scripts</c> read 2915. <b>A shared wrong list is worse than five private ones</b>: five
/// disagree with each other and can be caught by comparison; one agrees with itself everywhere.
/// </para>
/// <para>
/// What it hid, once the two kinds were put back: twenty routines nobody had ever seen called,
/// and the ceiling headline 223 published four hours earlier — "25 of 411 places" — was
/// <b>45 of 437</b>.
/// </para>
/// </summary>
public sealed class TheListThatWasShortTests
{
    private static LoadedMap Map(
        IReadOnlyList<MapEntryScript>? onEntry = null,
        IReadOnlyList<MapScriptEntry>? onLoad = null) =>
        new("SECTION 52", 1, 93, 16, 16, new byte[16 * 16 * 4], new CollisionGrid(4, 4, new byte[16]))
        {
            Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x08001000 }],
            Triggers = [new MapTrigger(5, 15, 0x4060, 0, 0x08002000)],
            Signs = [new MapSign(9, 43, 0, 0x08003000)],
            OnEntry = onEntry ?? [],
            OnLoad = onLoad ?? [],
        };

    /// <summary>
    /// ALL FIVE KINDS, from one map. The three that were there and the two that were not.
    /// </summary>
    [Fact]
    public void OneMapsScriptsAreAllFiveKinds()
    {
        LoadedMap map = Map(
            onEntry: [new MapEntryScript(0x4050, 1, 0x08004000)],
            onLoad: [new MapScriptEntry(3, 0x08005000)]);

        Assert.Equal(
            [
                ("1.93", "person 1", 0x08001000u),
                ("1.93", "trigger (5,15)", 0x08002000u),
                ("1.93", "sign (9,43)", 0x08003000u),
                ("1.93", "on arrival (0x4050 == 1)", 0x08004000u),
                ("1.93", "on load (kind 3)", 0x08005000u),
            ],
            MapLibrary.ScriptsOn(map));
    }

    /// <summary>
    /// THE ARRIVAL SCRIPT ON ITS OWN, so a rule that dropped it is caught by a test that is not
    /// also about the other four.
    /// </summary>
    [Fact]
    public void TheScriptAMapRunsOnArrivalIsInTheList()
    {
        LoadedMap map = Map(onEntry: [new MapEntryScript(0x4055, 3, 0x0800A000)]);

        Assert.Contains(("1.93", "on arrival (0x4055 == 3)", 0x0800A000u), MapLibrary.ScriptsOn(map));

        // And a map with none has four, not a phantom fifth.
        Assert.Equal(3, MapLibrary.ScriptsOn(Map()).Count());
    }

    /// <summary>
    /// AND THE MAP'S OWN SCRIPT LIST, which is the one added last and dropped first.
    /// </summary>
    [Fact]
    public void TheMapsOwnScriptListIsInTheListToo()
    {
        LoadedMap map = Map(onLoad: [new MapScriptEntry(1, 0x0800B000)]);

        Assert.Contains(("1.93", "on load (kind 1)", 0x0800B000u), MapLibrary.ScriptsOn(map));
    }

    /// <summary>
    /// A CONDITIONAL ENTRY IS NOT A SCRIPT and stays out — its pointer is a table of variable,
    /// value and script, and reading a condition table as commands is a misread that would parse.
    /// <para>
    /// This is the decoy: a rule that put every on-load entry in would pass the test above and
    /// fail here, and a scan that reads a table as commands comes back with plausible nonsense
    /// rather than an error.
    /// </para>
    /// </summary>
    [Fact]
    public void AConditionalEntryIsATableAndNotAScript()
    {
        MapScriptEntry conditional = Enumerable.Range(1, 8)
            .Select(k => new MapScriptEntry((byte)k, 0x0800C000))
            .First(e => MapScripts.IsConditional(e.Kind));

        Assert.DoesNotContain(
            MapLibrary.ScriptsOn(Map(onLoad: [conditional])),
            script => script.Address == 0x0800C000u);
    }

    /// <summary>
    /// And a pointer of nought is not a script either — the shape most of the on-load slots have.
    /// </summary>
    [Fact]
    public void AnEmptySlotIsNotAScript()
    {
        Assert.Equal(3, MapLibrary.ScriptsOn(Map(onLoad: [new MapScriptEntry(3, 0)])).Count());
        Assert.Equal(3, MapLibrary.ScriptsOn(Map(onEntry: [new MapEntryScript(0x4050, 1, 0)])).Count());
    }
}
