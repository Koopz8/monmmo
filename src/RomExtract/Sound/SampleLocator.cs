namespace PokeMmo.RomExtract.Sound;

/// <summary>
/// One recorded sound on the cartridge: a sixteen-byte header and the bytes after it.
/// <para>
/// Every field here is <b>read</b>. Nothing about a sample is modelled — it is a
/// recording, and a recording is the least ambiguous thing on the whole file.
/// </para>
/// </summary>
public sealed record SampleRecord(
    int Offset,
    bool Loops,
    uint Pitch,
    int LoopStart,
    int Length,
    bool Compressed = false)
{
    public const int HeaderBytes = 16;

    /// <summary>
    /// How many bytes of the file this recording actually occupies.
    /// <para>
    /// The same as its length when it is plain audio, and a good deal less when it is
    /// packed — which is the whole point of packing it.
    /// </para>
    /// </summary>
    public int DataBytes => Compressed ? CryDecoder.PackedBytesFor(Length) : Length;

    /// <summary>Where the audio itself starts.</summary>
    public int DataOffset => Offset + HeaderBytes;

    /// <summary>
    /// What this was recorded at, in samples a second.
    /// <para>
    /// The header stores 1024 times the rate that plays middle C at the right pitch, which
    /// is a fixed-point number rather than a frequency — dividing it back out is arithmetic
    /// rather than a guess, so this stays on the read side of the line.
    /// </para>
    /// </summary>
    public int Rate => (int)(Pitch / SampleLocator.PitchScale);
}

/// <summary>
/// Finding the recorded sounds by what they look like.
/// <para>
/// The GBA's standard sound driver — MusicPlayer2000, which everybody calls Sappy — puts a
/// sixteen-byte header in front of every recorded sound, and that header has one of the
/// most distinctive shapes on the cartridge:
/// </para>
/// <code>
/// 00 00 00        three zero bytes
/// 00 or 40 or 01  whether it loops, or is packed, and nothing else is allowed here
/// pitch           four bytes, 1024 times the rate
/// loop start      four bytes, an index into the sound
/// length - 1      four bytes
/// </code>
/// <para>
/// Four bytes with three legal values and a pitch that is a whole number of samples a
/// second is enough that noise practically cannot produce one. That is what makes this the
/// right place to start on sound: it is the one layer that can be found without knowing
/// where anything else is, and every other layer — instruments, voicegroups, song headers —
/// is found by pointing at something this class has already confirmed.
/// </para>
/// <para>
/// Which is exactly why the rule here has to be right. This layer used to insist a pitch be
/// one of twelve listed values, and on a real cartridge that threw away 2367 recordings and
/// left sixteen song headers on a file that has hundreds. Everything above it was starved by
/// one line.
/// </para>
/// <para>
/// Hardcodes nothing. It scans, it checks, and what it found is its return value; no
/// offset for any particular cartridge appears anywhere in this file.
/// </para>
/// </summary>
public static class SampleLocator
{
    /// <summary>
    /// The pitches the driver's own tooling emits, as the fixed-point values that appear in
    /// the header.
    /// <para>
    /// <b>No longer a filter.</b> It was one, and it was wrong: a real cartridge carries 2367
    /// headers that pass every other test and name a rate outside this list, and throwing
    /// them away left sixteen song headers on a file with hundreds. The list is kept because
    /// how many recordings use one of these rates is worth knowing — but it is now something
    /// counted rather than something enforced, which is the difference between a fact and a
    /// decision.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<uint> KnownPitches =
    [
        0x0059_9800, 0x007B_3000, 0x00A4_4000, 0x00D1_0C00,
        0x00F6_6000, 0x011B_B400, 0x0148_8000, 0x01A2_1800,
        0x01EC_C000, 0x0237_6800, 0x0273_2400, 0x0291_0000,
    ];

    /// <summary>
    /// What the pitch field is a fixed-point number of. <b>Read.</b>
    /// <para>
    /// The header stores 1024 times the rate. That is the format's own arrangement, and it
    /// is what replaces the whitelist: a pitch that is not a whole multiple of this is not a
    /// rate, whatever else it might be. Ten bits forced to zero is weaker evidence than a
    /// list of twelve values and it is evidence about the <em>format</em> rather than about
    /// which cartridges somebody happened to look at.
    /// </para>
    /// </summary>
    public const uint PitchScale = 1024;

    /// <summary>
    /// The slowest and fastest a recording on this hardware plausibly is. <b>Modelled.</b>
    /// <para>
    /// Wide enough to hold every rate the list above uses, at both ends, with room either
    /// side. These reject a pitch field that was never a pitch; they are not an opinion about
    /// which rates a game may use.
    /// </para>
    /// </summary>
    public const int SlowestBelievable = 4000;

    public const int FastestBelievable = 48000;

    /// <summary>
    /// Whether these four bytes are a rate at all: a whole number of samples a second, and a
    /// number of them somebody could have recorded at.
    /// </summary>
    public static bool IsARate(uint pitch) =>
        pitch % PitchScale == 0
        && pitch / PitchScale is >= SlowestBelievable and <= FastestBelievable;

    /// <summary>
    /// The three legal values of the header's first four bytes, read as one number.
    /// <para>
    /// Reading four bytes rather than checking three zeroes and a flag is not tidiness — the
    /// third value below has its marker in the <em>first</em> byte rather than the fourth,
    /// and a reader that insisted on three leading zeroes could never see it. That is exactly
    /// what this one did until the cries were looked at, and it is why every packed recording
    /// on the cartridge was invisible.
    /// </para>
    /// </summary>
    private const uint Straight = 0x0000_0000;

    private const uint Looped = 0x4000_0000;

    /// <summary>
    /// Packed rather than plain: the marker a cry carries. Never looped — nothing in this
    /// format is both.
    /// </summary>
    private const uint Packed = 0x0000_0001;

    /// <summary>
    /// The shortest run of audio worth believing in. <b>Modelled.</b>
    /// <para>
    /// Nothing says a sample cannot be four bytes long; what a four-byte sample means is
    /// that four zero bytes followed by a plausible number found each other by accident. A
    /// floor here trades a theoretical sample nobody would hear for a great deal of noise.
    /// </para>
    /// </summary>
    private const int ShortestBelievable = 32;

    /// <summary>
    /// Where a run of audio stops being audio and starts being a bad read. <b>Modelled.</b>
    /// <para>
    /// Sixteen megabytes is larger than any GBA cartridge, so this only ever rejects a
    /// length field that was never a length.
    /// </para>
    /// </summary>
    private const int LongestBelievable = 16 * 1024 * 1024;

    /// <summary>
    /// Every recorded sound on the cartridge, in the order they sit in the file.
    /// <para>
    /// Four-byte aligned, because everything in this format is except the sequences — which
    /// makes the scan four times cheaper and rejects a class of false positive for free.
    /// </para>
    /// </summary>
    public static IReadOnlyList<SampleRecord> All(Rom rom, Action<string>? log = null)
    {
        var found = new List<SampleRecord>();

        int unusual = 0;

        for (int offset = 0; offset + SampleRecord.HeaderBytes <= rom.Length; offset += 4)
        {
            uint kind = rom.ReadU32(offset);

            if (kind is not (Straight or Looped or Packed)) continue;

            uint pitch = rom.ReadU32(offset + 4);

            if (!IsARate(pitch)) continue;

            // Counted rather than enforced. A cartridge full of these is not a cartridge
            // this build should refuse to read; it is a number worth printing.
            if (!KnownPitches.Contains(pitch)) unusual++;

            long loopStart = rom.ReadU32(offset + 8);
            long length = (long)rom.ReadU32(offset + 12) + 1;

            if (length is < ShortestBelievable or > LongestBelievable) continue;

            bool packed = kind == Packed;

            // It has to actually fit. This is the check that does most of the work at the
            // end of the file, where a header can pass every other test and still describe
            // a sound that runs off the edge. A packed recording occupies about half what
            // its length says, which is the whole reason it is packed.
            long bytes = packed ? CryDecoder.PackedBytesFor((int)length) : length;

            if (offset + SampleRecord.HeaderBytes + bytes > rom.Length) continue;

            // And a loop point inside the sound it loops. A looped sample whose loop starts
            // past its own end is not a sample.
            if (kind == Looped && loopStart >= length) continue;
            if (kind != Looped && loopStart != 0) continue;

            found.Add(new SampleRecord(
                offset, kind == Looped, pitch, (int)loopStart, (int)length, packed));
        }

        log?.Invoke($"  {found.Count} recorded sounds");

        if (found.Count > 0)
        {
            List<int> rates = [.. found.Select(s => s.Rate).Distinct().Order()];

            log?.Invoke(
                $"    {found.Count(s => s.Loops)} loop, " +
                $"{found.Count(s => s.Compressed)} are packed; " +
                $"{rates.Count} distinct rates from {rates[0]} to {rates[^1]}");

            log?.Invoke($"    rates: {string.Join(", ", rates.Take(24))}{(rates.Count > 24 ? ", ..." : "")}");
        }

        // Said out loud whether it is zero or not, because a number that only appears when
        // it is inconvenient is a number nobody has a baseline for. This one used to be the
        // count of what was thrown away; it is now the count of what would have been.
        // Counted at the moment the rate was read, so it includes headers that were thrown
        // out afterwards for some other reason. "Of them" would be wrong and was.
        log?.Invoke(
            $"    {unusual} headers got past the rate check carrying one outside the driver's "
            + "usual twelve, which used to be grounds for refusing them");

        return found;
    }

}
