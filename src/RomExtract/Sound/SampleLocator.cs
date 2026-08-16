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
    int Length)
{
    public const int HeaderBytes = 16;

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
    public int Rate => (int)(Pitch / 1024);
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
/// 00 or 40        whether it loops, and nothing else is allowed here
/// pitch           four bytes, and one of a known dozen values
/// loop start      four bytes, an index into the sound
/// length - 1      four bytes
/// </code>
/// <para>
/// Three zeroes, a byte with two legal values, and a pitch drawn from a fixed set is
/// enough that noise practically cannot produce one. That is what makes this the right
/// place to start on sound: it is the one layer that can be found without knowing where
/// anything else is, and every other layer — instruments, voicegroups, song headers — is
/// found by pointing at something this class has already confirmed.
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
    /// This list is <b>read</b> in the sense that matters — every value in it comes from the
    /// format's documentation rather than from measuring one cartridge — but it is worth
    /// being honest that it is a whitelist, and a whitelist is a decision. A cartridge using
    /// a rate outside it would have its samples missed rather than mangled, which is the
    /// failure direction to prefer, and <see cref="Unusual"/> exists so that such a thing is
    /// reported rather than silently absent.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<uint> KnownPitches =
    [
        0x0059_9800, 0x007B_3000, 0x00A4_4000, 0x00D1_0C00,
        0x00F6_6000, 0x011B_B400, 0x0148_8000, 0x01A2_1800,
        0x01EC_C000, 0x0237_6800, 0x0273_2400, 0x0291_0000,
    ];

    /// <summary>Unlooped, and the only other legal value.</summary>
    private const byte Straight = 0x00;

    private const byte Looped = 0x40;

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
            if (rom.ReadU8(offset) != 0 || rom.ReadU8(offset + 1) != 0 || rom.ReadU8(offset + 2) != 0)
                continue;

            byte looping = rom.ReadU8(offset + 3);

            if (looping is not (Straight or Looped)) continue;

            uint pitch = rom.ReadU32(offset + 4);

            if (!KnownPitches.Contains(pitch))
            {
                // Counted rather than dropped in silence. A cartridge full of these is a
                // cartridge this list is wrong about, and that is a finding.
                if (Unusual(pitch)) unusual++;

                continue;
            }

            long loopStart = rom.ReadU32(offset + 8);
            long length = (long)rom.ReadU32(offset + 12) + 1;

            if (length is < ShortestBelievable or > LongestBelievable) continue;

            // It has to actually fit. This is the check that does most of the work at the
            // end of the file, where a header can pass every other test and still describe
            // a sound that runs off the edge.
            if (offset + SampleRecord.HeaderBytes + length > rom.Length) continue;

            // And a loop point inside the sound it loops. A looped sample whose loop starts
            // past its own end is not a sample.
            if (looping == Looped && loopStart >= length) continue;
            if (looping == Straight && loopStart != 0) continue;

            found.Add(new SampleRecord(offset, looping == Looped, pitch, (int)loopStart, (int)length));
        }

        log?.Invoke($"  {found.Count} recorded sounds");

        if (found.Count > 0)
        {
            log?.Invoke(
                $"    {found.Count(s => s.Loops)} loop, " +
                $"{found.Count(s => !s.Loops)} do not; " +
                $"rates {string.Join(", ", found.Select(s => s.Rate).Distinct().Order())}");
        }

        // Said out loud whether it is zero or not, because a number that only appears when
        // it is inconvenient is a number nobody has a baseline for.
        log?.Invoke($"    {unusual} headers looked right but carried a rate this build does not know");

        return found;
    }

    /// <summary>
    /// Whether a pitch is plausible enough to be worth reporting as unrecognised.
    /// <para>
    /// Without this every run of zeroes in the file counts as a near miss and the number
    /// above is meaningless. A real rate lands between about five and forty thousand samples
    /// a second; anything else was never a pitch.
    /// </para>
    /// </summary>
    private static bool Unusual(uint pitch) => pitch / 1024 is > 5000 and < 40000;
}
