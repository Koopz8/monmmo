using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Four event-list readers drop a record whose square is off the map, before anything else sees
/// it, and none of them said how many. 247, 250, 257 and 258 all rest on "228 triggers".
/// <para>
/// <b>The answer is nought for three of the four lists</b> — warps, triggers and signs lose
/// nothing at all, so every reading built on them is complete. The object table loses nine, and
/// the nine are not off-map people: they are a SECOND KIND OF RECORD, marked by <c>0xFF</c> in
/// the byte after the graphics id, whose every field after the square means something else.
/// </para>
/// <para>
/// <b>All nine of this cartridge's sit outside their own map</b>, so the off-map test caught
/// every one of them and the kind byte was never needed — the right answer for the wrong reason.
/// The fixture carries a clone whose square is ON the map, because a rule the cartridge never
/// exercises is a rule no break can be aimed at.
/// </para>
/// </summary>
public sealed class WhatTheReadersThrowAwayTests
{
    private static readonly SyntheticRom Fixture = new();

    private static MapHeaderRecord HeaderFor(int index)
    {
        Rom rom = Fixture.ToRom();

        MapBankTable banks = MapBankLocator.Locate(rom)
            ?? throw new InvalidOperationException("No bank table in the fixture.");

        (int bank, int map) = (index / SyntheticRom.MapsPerBank, index % SyntheticRom.MapsPerBank);

        return banks.AllMaps.Single(m => m.Bank == bank && m.Map == map).Header;
    }

    private static (List<MapObject> Kept, List<DroppedEvent> Dropped) Read(int index)
    {
        var dropped = new List<DroppedEvent>();

        List<MapObject> kept = MapLinkExtractor.ReadObjects(
            Fixture.ToRom(),
            HeaderFor(index),
            SyntheticRom.MapWidth,
            SyntheticRom.MapHeight,
            dropped: dropped);

        return (kept, dropped);
    }

    // ---------------------------------------------------------------- the kind byte decides

    /// <summary>
    /// <b>THE DECOY.</b> A clone whose square is ON the map is still not a person, and only the
    /// kind byte can say so. Every clone in the cartridge is outside its own map, so without this
    /// fixture the off-map test alone passes every test there is.
    /// </summary>
    [Fact]
    public void ACloneIsTakenOutOnTheKindByteEvenWhenItsSquareIsOnTheMap()
    {
        (List<MapObject> kept, List<DroppedEvent> dropped) = Read(0);

        Assert.DoesNotContain(kept, o => o.GraphicsId == SyntheticRom.CloneGraphicsId);
        Assert.DoesNotContain(kept, o => o.LocalId == SyntheticRom.CloneLocalId);

        DroppedEvent clone = Assert.Single(dropped, d => d.List == DroppedEvent.Clones);

        Assert.InRange(clone.X, 0, SyntheticRom.MapWidth - 1);
        Assert.InRange(clone.Y, 0, SyntheticRom.MapHeight - 1);
    }

    /// <summary>
    /// AND THE FIELDS ARE READ WITH THE OTHER LAYOUT. The byte the ordinary reading calls an
    /// elevation is the local id of the object being cloned, and the two halfwords it calls a
    /// trainer type and a sight range are a map number and a bank. Read as a person, this record
    /// is somebody standing at elevation four with a trainer type of eleven.
    /// </summary>
    [Fact]
    public void ACloneNamesAMapAndAnObjectOnIt()
    {
        DroppedEvent clone = Assert.Single(Read(0).Dropped, d => d.List == DroppedEvent.Clones);

        Assert.Equal(SyntheticRom.ClonedMapNumber, clone.Variable);
        Assert.Equal(SyntheticRom.ClonedMapBank, clone.Value);
    }

    /// <summary>
    /// AND AN ORDINARY OBJECT OFF THE MAP IS STILL DROPPED, AS AN OBJECT. Two rules, two
    /// answers — folding them together is how a clone would read as an off-map person and an
    /// off-map person would read as a clone, and the count of each is the whole finding.
    /// </summary>
    [Fact]
    public void AnObjectOutsideTheMapIsDroppedAndIsNotCalledAClone()
    {
        List<DroppedEvent> dropped = Read(0).Dropped;

        DroppedEvent stray = Assert.Single(dropped, d => d.List == DroppedEvent.Objects);

        Assert.True(
            stray.X < 0 || stray.X >= SyntheticRom.MapWidth
            || stray.Y < 0 || stray.Y >= SyntheticRom.MapHeight,
            "the object dropped as off-map has to be off the map");
    }

    /// <summary>
    /// AND THE SURVIVORS ARE UNTOUCHED. A reader that takes two records out of a table has to
    /// leave the rest exactly as they were — the fault this whole milestone is about is a filter
    /// nobody counted, and a filter that also removed a person would be worse.
    /// </summary>
    [Fact]
    public void TakingTwoRecordsOutLeavesEveryOtherObjectAlone()
    {
        Assert.Equal(SyntheticRom.ObjectsFor(0), Read(0).Kept);
    }

    /// <summary>
    /// AND A READER ASKED FOR NOTHING COLLECTS NOTHING. The collector is optional and every
    /// existing caller passes none, so a reader that needed one would have taken the whole
    /// project down rather than quietly under-reporting.
    /// </summary>
    [Fact]
    public void TheCollectorIsOptional()
    {
        Assert.Equal(
            SyntheticRom.ObjectsFor(0),
            MapLinkExtractor.ReadObjects(
                Fixture.ToRom(), HeaderFor(0), SyntheticRom.MapWidth, SyntheticRom.MapHeight));
    }

    // ------------------------------------------------------------------------ the controls

    /// <summary>
    /// THE CONTROL THAT DECIDED IT. A record's own id is one more than where it sits, and bytes
    /// past the end of a table cannot manage that — 1576 of 1576 kept records do it and so do
    /// nine of the nine clones, which is what said they were real.
    /// </summary>
    [Fact]
    public void ARecordWhoseIdMatchesWhereItSitsIsSaidToBeReal()
    {
        Assert.True(new DroppedEvent("object", 9, 12, 0, 0, 48, 40, LocalId: 10).ItsIdMatchesWhereItSits);
        Assert.False(new DroppedEvent("object", 9, 12, 0, 0, 48, 40, LocalId: 3).ItsIdMatchesWhereItSits);
    }

    /// <summary>
    /// AND THE OTHER CONTROL: a table one entry too long can only ever over-read the LAST record,
    /// so a record dropped from the middle is one the cartridge meant to put there.
    /// </summary>
    [Fact]
    public void TheLastRecordInATableIsToldFromAnyOther()
    {
        Assert.True(new DroppedEvent("object", 11, 12, 0, 0, 48, 40).WasTheLastInItsTable);
        Assert.False(new DroppedEvent("object", 9, 12, 0, 0, 48, 40).WasTheLastInItsTable);
        Assert.True(new DroppedEvent("object", 0, 1, 0, 0, 48, 40).WasTheLastInItsTable);
    }
}
