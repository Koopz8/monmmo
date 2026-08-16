namespace PokeMmo.RomExtract.Sound;

/// <summary>Which of the driver's four kinds of voice an instrument entry is.</summary>
public enum InstrumentKind
{
    /// <summary>A recording, played back at a pitch. Points at a sample.</summary>
    Sampled,

    /// <summary>One of the two square-wave channels.</summary>
    Square,

    /// <summary>The programmable waveform channel.</summary>
    Wave,

    /// <summary>The noise channel.</summary>
    Noise,

    /// <summary>A range of keys handed off to other instruments.</summary>
    KeySplit,

    /// <summary>A table of one instrument per key — how a drum kit is expressed.</summary>
    Percussion,
}

/// <summary>
/// One twelve-byte instrument entry.
/// <para>
/// The first byte says which kind it is and the rest means different things depending. This
/// record keeps only what the walk upwards needs: the kind, and the pointer in bytes four to
/// seven, which is a sample for a recorded instrument and a table for the other two
/// composite kinds. Everything else — envelopes, panning, duty cycles — is left on the
/// cartridge until there is something that plays it.
/// </para>
/// </summary>
public sealed record InstrumentRecord(int Offset, byte Type, InstrumentKind Kind, uint Pointer)
{
    public const int SizeBytes = 12;

    /// <summary>True when this one names a recording rather than a shape or a table.</summary>
    public bool IsSampled => Kind == InstrumentKind.Sampled;
}

/// <summary>
/// A run of instruments used together — what a song picks its sounds from.
/// <para>
/// <b>A run is not a voicegroup.</b> There is no delimiter between voicegroups on the file:
/// they sit back to back, so one unbroken run of instrument-shaped entries usually covers
/// several of them, and nothing in the data says where one ends. That is a fact about the
/// format rather than a shortcoming of this walk — the boundaries are not written down, and
/// the only thing that ever names one is a song header pointing at it.
/// </para>
/// <para>
/// Which is why <see cref="Holds"/> exists. A song naming the middle of a run is naming a
/// voicegroup, and a walk that only offered run starts rejected every song but the first.
/// </para>
/// </summary>
public sealed record VoicegroupRecord(int Offset, IReadOnlyList<InstrumentRecord> Instruments)
{
    public int Count => Instruments.Count;

    /// <summary>How many of them are recordings this build has already confirmed.</summary>
    public int Sampled => Instruments.Count(i => i.IsSampled);

    /// <summary>Whether an instrument of this run begins exactly here.</summary>
    public bool Holds(int offset) =>
        offset >= Offset
        && offset < Offset + Count * InstrumentRecord.SizeBytes
        && (offset - Offset) % InstrumentRecord.SizeBytes == 0;

    /// <summary>
    /// The instruments from a given point to the end of the run, which is a voicegroup as
    /// far as anything can tell — where the next one starts is not written down anywhere.
    /// </summary>
    public IReadOnlyList<InstrumentRecord> From(int offset) =>
        Holds(offset)
            ? [.. Instruments.Skip((offset - Offset) / InstrumentRecord.SizeBytes)]
            : [];
}

/// <summary>
/// One song's header: how many tracks, which instruments, and where each track's sequence
/// begins.
/// </summary>
public sealed record SongHeaderRecord(
    int Offset,
    int TrackCount,
    byte Priority,
    byte Reverb,
    int VoicegroupOffset,
    IReadOnlyList<int> TrackOffsets)
{
    /// <summary>Four bytes, then the voicegroup pointer, then one pointer a track.</summary>
    public static int SizeOf(int trackCount) => 8 + trackCount * 4;
}

/// <summary>
/// Why a thing at an offset is not a song header.
/// <para>
/// The table names headers this walk did not confirm, and until now that was one word
/// covering six faults across three layers. Which one it is says whether to look at the
/// table, the voicegroup walk or the file itself.
/// </para>
/// </summary>
public enum SongRejection
{
    /// <summary>It is one. Nothing was rejected.</summary>
    None,

    /// <summary>The header, or the pointers it claims, run off the end of the file.</summary>
    PastTheEnd,

    /// <summary>
    /// The first byte is not a track count anything could have. Nought, or more than the
    /// hardware mixes — which usually means the offset is not a header at all.
    /// </summary>
    TrackCount,

    /// <summary>Where the voicegroup should be is not an address into this cartridge.</summary>
    VoicegroupNotAPointer,

    /// <summary>
    /// It names a voicegroup that resolves but that the walk below did not confirm.
    /// <para>
    /// The interesting one, and the one that means the fault is a layer down rather than
    /// here. A song naming a real voicegroup this build could not find is a recording or an
    /// instrument that was rejected, not a bad header.
    /// </para>
    /// </summary>
    VoicegroupNotConfirmed,

    /// <summary>One of its track pointers is not an address into this cartridge.</summary>
    TrackNotAPointer,
}

/// <summary>One entry of the song table: a song, and which group it belongs to.</summary>
public sealed record SongTableEntry(int Index, int HeaderOffset, byte Group)
{
    public const int SizeBytes = 8;
}

/// <summary>
/// Everything the walk found, and what it could not account for.
/// <para>
/// The unaccounted counts are as much the point as the found ones. A walk that reports only
/// its successes cannot be wrong, and this project has been caught by exactly that shape
/// before.
/// </para>
/// </summary>
public sealed record SoundTreeResult(
    IReadOnlyList<SampleRecord> Samples,
    IReadOnlyList<VoicegroupRecord> Voicegroups,
    IReadOnlyList<SongHeaderRecord> Songs,
    int SongTableOffset,
    IReadOnlyList<SongTableEntry> Table,
    int PointersToTheTable)
{
    public bool FoundATable => SongTableOffset >= 0;

    /// <summary>Songs that no table entry names — found by shape and reachable by nobody.</summary>
    public int SongsNoTableNames =>
        Songs.Count(s => !Table.Any(e => e.HeaderOffset == s.Offset));
}
