namespace PokeMmo.RomExtract.Sound;

/// <summary>
/// Walking up from the recordings to the song table, one confirmed layer at a time.
/// <para>
/// The shape of this is the whole argument for it. <see cref="SampleLocator"/> finds
/// recordings by a header noise cannot produce, and needs to know nothing about anything
/// else. Every layer after that is found by <em>pointing at the layer below it</em>: an
/// instrument is twelve bytes containing a pointer to a recording already confirmed, a
/// voicegroup is a run of confirmed instruments, a song header is a thing whose fifth to
/// eighth bytes point at a confirmed voicegroup, and the song table is a run of eight-byte
/// entries pointing at confirmed song headers.
/// </para>
/// <para>
/// Nothing here is hardcoded to a cartridge, and nothing here reads code. Every offset is
/// arrived at from the file's own contents, and what was found is the return value.
/// </para>
/// <para>
/// <b>What this deliberately does not do.</b> The usual way to find a song table is to scan
/// for the sound driver's <c>SelectSong</c> function by its thirty-byte prologue and read
/// the table's address back out of the instruction stream. That works, and every existing
/// tool does it, and it is reading compiled code — which is the line this project does not
/// cross. So the cross-check here is a different one that reaches the same question from
/// the data side: a real song table's address is loaded from somewhere, so its address
/// ought to appear as a plain 32-bit pointer somewhere in the file. That number is counted
/// and reported. It is weaker evidence than decoding the prologue would be, and it is
/// evidence that does not require pretending a disassembler is a file reader.
/// </para>
/// </summary>
public static class SoundLocator
{
    /// <summary>
    /// The fewest instruments a run has to have before it is a voicegroup. <b>Modelled.</b>
    /// <para>
    /// Two consecutive twelve-byte shapes happen by accident; a dozen do not. This is the
    /// single number in this file most likely to be wrong, which is why the count of runs
    /// rejected by it is printed rather than swallowed.
    /// </para>
    /// </summary>
    private const int ShortestVoicegroup = 4;

    /// <summary>The most tracks one song can have. Sixteen is what the hardware mixes.</summary>
    private const int MostTracks = 16;

    /// <summary>The fewest entries a run has to have before it is the song table.</summary>
    private const int ShortestSongTable = 8;

    /// <summary>The whole walk, from the recordings up.</summary>
    public static SoundTreeResult Walk(Rom rom, Action<string>? log = null)
    {
        IReadOnlyList<SampleRecord> samples = SampleLocator.All(rom, log);

        var sampleAt = samples.Select(s => s.Offset).ToHashSet();

        IReadOnlyList<VoicegroupRecord> voicegroups = Voicegroups(rom, sampleAt, log);

        // Every instrument boundary, not only the first of each run. Voicegroups have no
        // delimiter between them, so one run covers several and a song header names the
        // middle of it — which used to be rejected as pointing at nothing confirmed. On a
        // real cartridge that left sixteen song headers on a file with hundreds.
        var voicegroupAt = voicegroups
            .SelectMany(v => v.Instruments.Select(i => i.Offset))
            .ToHashSet();

        IReadOnlyList<SongHeaderRecord> songs = Songs(rom, voicegroupAt, log);

        var songAt = songs.Select(s => s.Offset).ToHashSet();

        (int tableOffset, IReadOnlyList<SongTableEntry> table) = SongTable(rom, songAt, log);

        int pointers = tableOffset < 0 ? 0 : PointersTo(rom, tableOffset);

        if (tableOffset >= 0)
        {
            log?.Invoke(
                pointers > 0
                    ? $"    the table's own address appears {pointers} time(s) elsewhere in the file"
                    : "    nothing in the file points at this table, which is worth a second look");
        }

        var result = new SoundTreeResult(samples, voicegroups, songs, tableOffset, table, pointers);

        // The disagreement, said out loud. A song found by shape that no table names is
        // either a song the table does not reach or a false positive, and which of those it
        // is cannot be decided from here — so it is reported rather than resolved.
        if (result.SongsNoTableNames > 0)
        {
            log?.Invoke(
                $"    {result.SongsNoTableNames} song header(s) look right but no table entry names them");
        }

        return result;
    }

    // ---- instruments and voicegroups ---------------------------------------------------

    /// <summary>
    /// What kind an instrument's first byte says it is, or nothing when it says nothing.
    /// <para>
    /// The type byte is a small enumeration with gaps, and the gaps are what make this a
    /// filter rather than a cast. Everything outside the listed values is not an instrument.
    /// </para>
    /// </summary>
    public static InstrumentKind? KindOf(byte type) => type switch
    {
        0x00 or 0x08 => InstrumentKind.Sampled,

        // Read off a cartridge rather than off a document. The cry table's 388 entries all
        // carry 0x20 and every one of them points at a confirmed recording, which is as
        // direct a demonstration that it names a recording as this project can get.
        0x20 or 0x28 => InstrumentKind.Sampled,
        0x01 or 0x02 or 0x09 or 0x0A => InstrumentKind.Square,
        0x03 or 0x0B => InstrumentKind.Wave,
        0x04 or 0x0C => InstrumentKind.Noise,
        0x40 => InstrumentKind.KeySplit,
        0x80 => InstrumentKind.Percussion,
        _ => null,
    };

    /// <summary>
    /// One instrument entry, if the twelve bytes at this offset are one.
    /// <para>
    /// A recorded instrument has to point at a recording this build has already confirmed.
    /// That is the load-bearing condition in the whole file: it is what turns "twelve bytes
    /// that could be anything" into "twelve bytes that name something real".
    /// </para>
    /// </summary>
    private static InstrumentRecord? Instrument(Rom rom, int offset, HashSet<int> sampleAt)
    {
        if (offset + InstrumentRecord.SizeBytes > rom.Length) return null;

        byte type = rom.ReadU8(offset);

        if (KindOf(type) is not { } kind) return null;

        uint pointer = rom.ReadU32(offset + 4);

        if (kind == InstrumentKind.Sampled)
        {
            if (rom.ToOffsetOrNull(pointer) is not { } at || !sampleAt.Contains(at)) return null;
        }
        else if (kind is InstrumentKind.KeySplit or InstrumentKind.Percussion)
        {
            // The two composite kinds point at a table rather than at a recording. The table
            // is not walked here — what matters at this layer is that the pointer resolves,
            // which is enough to tell an instrument from twelve arbitrary bytes.
            if (rom.ToOffsetOrNull(pointer) is null) return null;
        }

        return new InstrumentRecord(offset, type, kind, pointer);
    }

    private static IReadOnlyList<VoicegroupRecord> Voicegroups(
        Rom rom, HashSet<int> sampleAt, Action<string>? log)
    {
        var found = new List<VoicegroupRecord>();

        int tooShort = 0;

        // Type bytes this build rejects on twelve-byte entries that otherwise name a
        // confirmed recording. Every one of them breaks a run in half, so this is the list
        // of what to look at next — counted rather than guessed at, which is how 0x20 got
        // into the enumeration above.
        var unknownTypes = new Dictionary<byte, int>();

        for (int offset = 0; offset + InstrumentRecord.SizeBytes <= rom.Length;)
        {
            if (Instrument(rom, offset, sampleAt) is null)
            {
                if (KindOf(rom.ReadU8(offset)) is null
                    && rom.ToOffsetOrNull(rom.ReadU32(offset + 4)) is { } names
                    && sampleAt.Contains(names))
                {
                    byte type = rom.ReadU8(offset);

                    unknownTypes[type] = unknownTypes.GetValueOrDefault(type) + 1;
                }

                offset += 4;
                continue;
            }

            var run = new List<InstrumentRecord>();

            int at = offset;

            while (Instrument(rom, at, sampleAt) is { } instrument)
            {
                run.Add(instrument);
                at += InstrumentRecord.SizeBytes;
            }

            // A run of nothing but shapes and empty tables is not a voicegroup anybody could
            // play. At least one recording is what says these twelve-byte blocks are the
            // thing they look like rather than a coincidence of small numbers.
            if (run.Count >= ShortestVoicegroup && run.Any(i => i.IsSampled))
                found.Add(new VoicegroupRecord(offset, run));
            else if (run.Count > 0)
                tooShort++;

            // Past the whole run. Stepping four bytes into it would find the same run again
            // starting one instrument later, over and over.
            offset = Math.Max(offset + 4, at);
        }

        log?.Invoke($"  {found.Count} voicegroups");

        if (found.Count > 0)
        {
            log?.Invoke(
                $"    {found.Sum(v => v.Count)} instruments in them, " +
                $"{found.Sum(v => v.Sampled)} of which name a recording");
        }

        log?.Invoke($"    {tooShort} run(s) of instrument-shaped bytes were too short or named no recording");

        if (found.Count > 0)
        {
            log?.Invoke(
                $"    longest run {found.Max(v => v.Count)} instruments — a run covers as many "
                + "voicegroups as sit next to each other, which nothing in the file marks");
        }

        if (unknownTypes.Count > 0)
        {
            log?.Invoke(
                "    type bytes this build rejects on entries that do name a recording: "
                + string.Join(
                    ", ",
                    unknownTypes.OrderByDescending(p => p.Value).Take(8)
                        .Select(p => $"0x{p.Key:X2} x{p.Value}")));
        }

        return found;
    }

    // ---- song headers --------------------------------------------------------------------

    private static SongHeaderRecord? Song(Rom rom, int offset, HashSet<int> voicegroupAt)
    {
        if (offset + 8 > rom.Length) return null;

        int tracks = rom.ReadU8(offset);

        if (tracks is < 1 or > MostTracks) return null;

        if (offset + SongHeaderRecord.SizeOf(tracks) > rom.Length) return null;

        if (rom.ToOffsetOrNull(rom.ReadU32(offset + 4)) is not { } voicegroup) return null;

        if (!voicegroupAt.Contains(voicegroup)) return null;

        var trackOffsets = new List<int>(tracks);

        for (int track = 0; track < tracks; track++)
        {
            if (rom.ToOffsetOrNull(rom.ReadU32(offset + 8 + track * 4)) is not { } at) return null;

            trackOffsets.Add(at);
        }

        return new SongHeaderRecord(
            offset, tracks, rom.ReadU8(offset + 2), rom.ReadU8(offset + 3), voicegroup, trackOffsets);
    }

    private static IReadOnlyList<SongHeaderRecord> Songs(
        Rom rom, HashSet<int> voicegroupAt, Action<string>? log)
    {
        var found = new List<SongHeaderRecord>();

        for (int offset = 0; offset + 8 <= rom.Length; offset += 4)
        {
            if (Song(rom, offset, voicegroupAt) is { } song) found.Add(song);
        }

        log?.Invoke($"  {found.Count} song headers");

        if (found.Count > 0)
        {
            log?.Invoke(
                $"    {found.Sum(s => s.TrackCount)} tracks between them, " +
                $"most in one song {found.Max(s => s.TrackCount)}");
        }

        return found;
    }

    // ---- the song table --------------------------------------------------------------------

    /// <summary>
    /// Eight bytes an entry: a pointer, a group number, a zero, the group number again, and
    /// another zero. The doubled number and the two zeroes are as much of the signature as
    /// the pointer is.
    /// </summary>
    private static SongTableEntry? Entry(Rom rom, int offset, int index, HashSet<int> songAt)
    {
        if (offset + SongTableEntry.SizeBytes > rom.Length) return null;

        if (rom.ToOffsetOrNull(rom.ReadU32(offset)) is not { } header) return null;

        if (!songAt.Contains(header)) return null;

        byte group = rom.ReadU8(offset + 4);

        if (rom.ReadU8(offset + 5) != 0 || rom.ReadU8(offset + 7) != 0) return null;

        if (rom.ReadU8(offset + 6) != group) return null;

        return new SongTableEntry(index, header, group);
    }

    private static (int Offset, IReadOnlyList<SongTableEntry> Entries) SongTable(
        Rom rom, HashSet<int> songAt, Action<string>? log)
    {
        (int offset, List<SongTableEntry> entries) best = (-1, []);

        for (int offset = 0; offset + SongTableEntry.SizeBytes <= rom.Length; offset += 4)
        {
            var run = new List<SongTableEntry>();

            int at = offset;

            while (Entry(rom, at, run.Count, songAt) is { } entry)
            {
                run.Add(entry);
                at += SongTableEntry.SizeBytes;
            }

            // The longest one wins. A cartridge has one song table and a great many things
            // that look like the first entry of one.
            if (run.Count > best.entries.Count) best = (offset, run);

            if (run.Count > 0) offset = Math.Max(offset, at - 4);
        }

        if (best.entries.Count < ShortestSongTable)
        {
            log?.Invoke(
                best.entries.Count == 0
                    ? "  no song table"
                    : $"  no song table — the longest run was {best.entries.Count} entries, under the {ShortestSongTable} needed");

            return (-1, []);
        }

        log?.Invoke($"  song table at 0x{best.offset:X6} with {best.entries.Count} songs");

        return (best.offset, best.entries);
    }

    /// <summary>
    /// How many times the table's own address appears as a plain pointer in the file.
    /// <para>
    /// The corroborating witness described at the top of this class. Something has to load
    /// this address for the table to be reachable at all, so zero occurrences is a reason to
    /// distrust the answer — not proof against it, since the address may be built rather
    /// than loaded, which is exactly why this is reported rather than enforced.
    /// </para>
    /// </summary>
    private static int PointersTo(Rom rom, int offset)
    {
        uint address = Rom.BaseAddress + (uint)offset;

        int seen = 0;

        for (int at = 0; at + 4 <= rom.Length; at += 4)
        {
            if (rom.ReadU32(at) == address && at != offset) seen++;
        }

        return seen;
    }
}
