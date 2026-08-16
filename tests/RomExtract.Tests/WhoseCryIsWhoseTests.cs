using PokeMmo.Core.Sound;
using PokeMmo.RomExtract.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Which noise belongs to which creature.
/// <para>
/// The recordings were all found a while ago — several hundred of them — and nothing could say
/// which creature made any of them. That is a table, and it was invisible to the walk that
/// found everything else: the sound tree rejects a twelve-byte entry whose first byte is
/// outside the driver's kind enumeration, and these entries are not in it.
/// </para>
/// <para>
/// So it is found by a different shape, and the shape is the argument. One table was written
/// by one macro, so every entry carries the same type byte — which is what stops a run from
/// carrying on into whatever follows it, and is the only thing here that could be wrong.
/// </para>
/// </summary>
public class WhoseCryIsWhoseTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static (Rom Rom, CryTableResult Table) Cartridge()
    {
        Rom rom = Synthetic.ToRom();

        CryTableResult? found = CryTableLocator.Locate(rom, SampleLocator.All(rom));

        Assert.NotNull(found);

        return (rom, found);
    }

    /// <summary>The table is found, where it is, at the length it is.</summary>
    [Fact]
    public void TheTableIsFound()
    {
        (Rom _, CryTableResult table) = Cartridge();

        Assert.Equal(SyntheticRom.CryTableOffset, table.Offset);
        Assert.Equal(SyntheticRom.CryTableCount, table.Count);
    }

    /// <summary>
    /// And the type byte comes back rather than being asserted.
    /// <para>
    /// This project does not know what number a cry entry carries and does not need to. What
    /// it needs is that every entry carries the same one — so the number is reported, to be
    /// compared against a real cartridge by somebody who has one.
    /// </para>
    /// </summary>
    [Fact]
    public void AndTheTypeByteIsReportedRatherThanAssumed()
    {
        (Rom _, CryTableResult table) = Cartridge();

        Assert.Equal(SyntheticRom.CryTableType, table.Type);
    }

    /// <summary>
    /// A shorter run of the same shape does not win, and is counted rather than ignored.
    /// </summary>
    [Fact]
    public void AShorterRunOfTheSameShapeDoesNotWin()
    {
        (Rom _, CryTableResult table) = Cartridge();

        Assert.NotEqual(SyntheticRom.CryDecoyTableOffset, table.Offset);

        // The decoy was long enough to be considered, which is what makes losing meaningful.
        Assert.True(table.Runs > 1, "nothing else was even considered, so the longest winning proves nothing");
    }

    /// <summary>
    /// And a run that changes its type byte half way is two runs, not one.
    /// <para>
    /// The fixture puts two of them end to end, longer together than the real table and each
    /// shorter alone. A walk that let the type byte change would prefer it, and would be
    /// reading a cry table that ran on into whatever the cartridge happened to put next.
    /// </para>
    /// </summary>
    [Fact]
    public void AndARunThatChangesItsTypeByteIsTwoRuns()
    {
        (Rom _, CryTableResult table) = Cartridge();

        Assert.Equal(SyntheticRom.CryTableOffset, table.Offset);

        // Stated as the arithmetic that makes it a trap, so this cannot quietly stop being one.
        Assert.True(
            (SyntheticRom.CryTableCount - 1) * 2 > SyntheticRom.CryTableCount,
            "the changing-type run is not longer than the real table, so it is not a trap");
    }

    // ---- and out the other end ---------------------------------------------------------

    /// <summary>
    /// A species number goes in and that species' recording comes out, decoded.
    /// <para>
    /// The fixture's entries name three recordings in turn, so a number arriving at the wrong
    /// entry shows up as the wrong noise rather than as no noise. The first is packed with
    /// nothing but noughts, which decodes to one value held flat — checkable without
    /// reimplementing the difference table, which would make this a copy of the code rather
    /// than a check on it.
    /// </para>
    /// </summary>
    [Fact]
    public void ASpeciesNumberGoesInAndItsRecordingComesOut()
    {
        (Rom rom, CryTableResult table) = Cartridge();

        IReadOnlyList<SampleRecord> samples = SampleLocator.All(rom);

        Assert.Equal(SyntheticRom.FlatCryOffset, table.SampleFor(0));
        Assert.Equal(SyntheticRom.RampCryOffset, table.SampleFor(1));
        Assert.Equal(SyntheticRom.ZigZagCryOffset, table.SampleFor(2));

        SampleRecord flat = samples.Single(s => s.Offset == table.SampleFor(0));

        sbyte[] audio = CryDecoder.Decode(rom, flat);

        Assert.NotEmpty(audio);
        Assert.All(audio, sample => Assert.Equal(SyntheticRom.FlatCryValue, sample));
    }

    /// <summary>And a species this table does not reach is nothing rather than the wrong noise.</summary>
    [Fact]
    public void AndASpeciesItDoesNotReachIsNothing()
    {
        (Rom _, CryTableResult table) = Cartridge();

        Assert.Null(table.SampleFor(-1));
        Assert.Null(table.SampleFor(table.Count));

        // The fixture names fewer creatures than the cartridge has, which is the case that
        // makes this a real question rather than a defensive check.
        Assert.True(table.Count < SyntheticRom.SpeciesCount);
    }

    // ---- kept once decoded ---------------------------------------------------------------

    /// <summary>
    /// A creature's noise is unpacked the first time it is asked for and not again.
    /// <para>
    /// Unpacking one is a difference table walked a nibble at a time. Once when a creature
    /// first comes out is fine; every time one comes out is a hitch in a fight, and a fight is
    /// where every one of these is played.
    /// </para>
    /// </summary>
    [Fact]
    public void ANoiseIsUnpackedOnceAndKept()
    {
        (Rom rom, CryTableResult table) = Cartridge();

        var library = new CryLibrary(rom, SampleLocator.All(rom), table);

        Assert.Equal(0, library.Decoded);

        Voice? first = library.For(0);

        Assert.NotNull(first);
        Assert.Equal(1, library.Decoded);

        // The same object rather than an equal one, which is the difference between kept and
        // made again.
        Assert.Same(first, library.For(0));
        Assert.Equal(1, library.Decoded);

        library.For(1);

        Assert.Equal(2, library.Decoded);
    }

    /// <summary>
    /// And it carries the recording's own rate rather than a rate somebody decided on.
    /// </summary>
    [Fact]
    public void AndItCarriesTheRecordingsOwnRate()
    {
        (Rom rom, CryTableResult table) = Cartridge();

        IReadOnlyList<SampleRecord> samples = SampleLocator.All(rom);

        var library = new CryLibrary(rom, samples, table);

        SampleRecord record = samples.Single(s => s.Offset == SyntheticRom.FlatCryOffset);

        Assert.Equal(record.Rate, library.For(0)!.Rate);

        // And it does not loop. A looping cry would ring for as long as the fight lasted.
        Assert.False(library.For(0)!.Loops);
    }

    /// <summary>
    /// A creature this cartridge has no noise for is nothing, and so is every creature when
    /// there is no table at all.
    /// </summary>
    [Fact]
    public void ACreatureWithNoNoiseIsNothing()
    {
        (Rom rom, CryTableResult table) = Cartridge();

        var library = new CryLibrary(rom, SampleLocator.All(rom), table);

        Assert.Null(library.For(table.Count));
        Assert.Null(library.For(-1));

        var none = new CryLibrary(rom, SampleLocator.All(rom), null);

        Assert.Equal(0, none.Count);
        Assert.Null(none.For(0));
    }

    /// <summary>A file with no recordings in it has no cry table, and says so.</summary>
    [Fact]
    public void AFileWithNoRecordingsHasNoCryTable()
    {
        var empty = new Rom(new byte[0x4000]);

        Assert.Null(CryTableLocator.Locate(empty, SampleLocator.All(empty)));
    }
}
