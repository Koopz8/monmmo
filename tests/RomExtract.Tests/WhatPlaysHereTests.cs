using PokeMmo.Core.World;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Maps;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// One number, carried four layers further than it used to go.
/// <para>
/// A map header has said which song plays there since the very first milestone that read
/// a header at all — it is two bytes at offset sixteen of the twenty-eight, and
/// <see cref="MapHeaderRecord.Music"/> has been reading it the whole time. It then went
/// nowhere. The dump tool printed it; the world file did not carry it; the server had
/// never heard of it; no client could ask.
/// </para>
/// <para>
/// So this is the first thing written for sound and it contains no sound at all. It is a
/// number moving, which is the right first step precisely because it can be proved
/// completely on a machine with no cartridge on it and nothing that makes a noise.
/// </para>
/// <para>
/// Read, all of it. Nothing here is modelled — the number is the cartridge's own, and this
/// file's whole claim is that it arrives unchanged.
/// </para>
/// </summary>
public class WhatPlaysHereTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static WorldData Exported() => WorldExporter.Export(Synthetic.ToRom());

    /// <summary>
    /// It comes out of the header at all. The blunt one, and the one that fails if the
    /// field is ever dropped from the record.
    /// </summary>
    [Fact]
    public void EveryMapSaysWhatPlaysThere()
    {
        WorldData world = Exported();

        Assert.NotEmpty(world.Maps);

        // Not that any particular map is any particular song — that would be a test of the
        // fixture. That the numbers are not all the same is what says a real per-map field
        // is being read rather than a constant being handed out.
        Assert.True(
            world.Maps.Select(m => m.Music).Distinct().Count() > 1,
            "every map claims the same song, which is what a hardcoded number looks like");
    }

    /// <summary>
    /// And it is the header's own number rather than one this side invented, checked map
    /// by map against the record it was read from.
    /// </summary>
    [Fact]
    public void AndItIsTheNumberTheHeaderActuallyGave()
    {
        Rom rom = Synthetic.ToRom();

        MapBankTable banks = MapBankLocator.Locate(rom)!;
        WorldData world = WorldExporter.Export(rom);

        var byId = world.Maps.ToDictionary(m => m.Id);

        int checkedMaps = 0;

        foreach ((int bank, int number, MapHeaderRecord header) in banks.AllMaps)
        {
            if (!byId.TryGetValue($"{bank}.{number}", out MapData? map)) continue;

            Assert.Equal(header.Music, map.Music);

            checkedMaps++;
        }

        // Said out loud, because every assertion above is inside a loop that an empty
        // dictionary would skip entirely — and a test that checks nothing passes.
        Assert.True(checkedMaps > 1, $"only {checkedMaps} maps were actually compared");
    }

    /// <summary>
    /// And it survives the world file, which is the layer this number did not previously
    /// reach and the only one that matters for two people standing in the same room.
    /// </summary>
    [Fact]
    public void AndItSurvivesBeingWrittenDownAndReadBack()
    {
        WorldData before = Exported();

        using var file = new MemoryStream();

        before.Save(file);

        file.Position = 0;

        WorldData after = WorldData.Load(file);

        Assert.Equal(before.Maps.Count, after.Maps.Count);

        foreach (MapData was in before.Maps)
        {
            MapData now = after.Maps.Single(m => m.Id == was.Id);

            Assert.Equal(was.Music, now.Music);
        }
    }

    /// <summary>
    /// Zero is a value rather than a gap.
    /// <para>
    /// The cartridge uses it for "carry on playing whatever was already playing", which is
    /// how a doorway into a house does not restart the town's theme. A nullable field would
    /// have made that indistinguishable from a map nobody read the header of, and the two
    /// want opposite behaviour.
    /// </para>
    /// </summary>
    [Fact]
    public void AndNoughtIsSomethingRatherThanNothing()
    {
        var quiet = new MapData("9.9", "SOMEWHERE", 2, 2, new byte[4]) { Music = 0 };

        var world = new WorldData([quiet]);

        using var file = new MemoryStream();

        world.Save(file);

        file.Position = 0;

        Assert.Equal(0, WorldData.Load(file).Maps.Single().Music);
    }
}
