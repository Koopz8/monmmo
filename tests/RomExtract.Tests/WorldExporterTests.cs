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
}
