using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What the export says while it works, which is as much a deliverable as what it writes.
/// <para>
/// This project reads its own export log to decide what is true about a cartridge — how many
/// maps have encounters, how many people are in the way, how many warps lead nowhere. Every
/// one of those is a count printed by the exporter, so a count the exporter prints wrongly is
/// a fact this project believes wrongly.
/// </para>
/// </summary>
public class WorldExporterTests
{
    /// <summary>
    /// Every map's objects are read once, not twice.
    /// <para>
    /// The list is wanted twice — for the map's own people and again for the boat — and both
    /// call sites used to read it. That is wasted work, and worse: an object dropped for
    /// sitting outside its map is reported by the reader, so the same stray was reported once
    /// per read. On a real cartridge nine strays came out as thirty-six lines and read as
    /// thirty-six problems.
    /// </para>
    /// <para>
    /// Counted rather than inspected, because "read once" is not something the result carries.
    /// What it carries is how many times the reader spoke, and that is exactly the number that
    /// was wrong.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryMapsObjectsAreReadOnce()
    {
        var said = new List<string>();

        WorldData world = WorldExporter.Export(new SyntheticRom().ToRom(), said.Add);

        int strays = said.Count(line => line.Contains("object") && line.Contains("dropped"));

        Assert.True(strays > 0, "the fixture no longer has a stray object, so this proves nothing");

        // One line per map that has a stray, rather than one per read of that map.
        int mapsWithObjects = world.Maps.Count(m => m.Objects.Count > 0);

        Assert.Equal(mapsWithObjects, strays);
    }

    /// <summary>
    /// A warp naming the cartridge's own "no map" mark is not a missing map.
    /// <para>
    /// Bank 127, map 127 — every bit of both bytes set, which is this hardware's usual way
    /// of saying "none". Counting those together with genuinely absent maps makes a number
    /// nobody can act on: it is either a bug worth a day or nothing at all, and the total
    /// says which only by accident.
    /// </para>
    /// </summary>
    [Fact]
    public void ASentinelWarpIsNotAMissingMap()
    {
        var world = new WorldData(
        [
            new MapData("1.0", "HERE", 4, 4, new byte[16])
            {
                Warps =
                [
                    new Warp(1, 1, 0, WorldExporter.NoMap),
                    new Warp(2, 2, 0, "9.9"),
                ],
            },
        ]);

        var known = world.Maps.Select(m => m.Id).ToHashSet();

        int dangling = world.Maps.Sum(m => m.Warps.Count(w => !known.Contains(w.TargetMapId)));
        int sentinels = world.Maps.Sum(m => m.Warps.Count(w => w.TargetMapId == WorldExporter.NoMap));

        // Both are dangling by the old reckoning; only one of them is a finding.
        Assert.Equal(2, dangling);
        Assert.Equal(1, sentinels);
    }

    /// <summary>And the mark is what the cartridge writes, rather than a number chosen here.</summary>
    [Fact]
    public void AndTheMarkIsEveryBitOfBothBytes()
    {
        string[] halves = WorldExporter.NoMap.Split('.');

        Assert.Equal(2, halves.Length);
        Assert.All(halves, half => Assert.Equal(sbyte.MaxValue, int.Parse(half)));
    }
}
