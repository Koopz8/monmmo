using PokeMmo.Core.Sound;

namespace PokeMmo.RomExtract.Sound;

/// <summary>
/// Assembling one song out of a cartridge, ready to be performed.
/// <para>
/// The last joint in the sound work. The locator finds the tables, the reader turns a track
/// into commands, the decoder unpacks a recording and the player performs — and each of those
/// knows nothing about the others. This is what hands one to the next.
/// </para>
/// <para>
/// It runs on the player's own machine against the player's own file, like everything else
/// here. Nothing it produces is written down, and the recordings it decodes live only as long
/// as the song is playing.
/// </para>
/// </summary>
public static class SongLoader
{
    /// <summary>
    /// Everything for one song, or nothing when this cartridge has no such song.
    /// <para>
    /// Nothing rather than an empty song, so a caller cannot play silence and believe it
    /// played something. A map naming a song this file does not have is a finding, and a
    /// finding is not a thing to paper over with a rest.
    /// </para>
    /// </summary>
    public static SongPlayer? Load(Rom rom, SoundTreeResult tree, int song, Mixer mixer)
    {
        if (song < 0 || song >= tree.Table.Count) return null;

        int at = tree.Table[song].HeaderOffset;

        if (tree.Songs.FirstOrDefault(s => s.Offset == at) is not { } header) return null;

        List<Instrument> voicegroup = Voicegroup(rom, tree, header.VoicegroupOffset);

        if (voicegroup.Count == 0) return null;

        List<Track> tracks =
        [
            .. header.TrackOffsets
                .Select(track => SequenceReader.Read(rom, track))

                // A track that never reached an end command is one this reader did not
                // follow to its end, and performing it would be performing a guess. Dropped
                // rather than played, and a song whose every track is dropped comes back as
                // nothing at all rather than as a song of no tracks.
                .Where(read => read.EndedProperly)
                .Select(read => new Track(read.Events)),
        ];

        return tracks.Count == 0 ? null : new SongPlayer(tracks, voicegroup, mixer);
    }

    /// <summary>
    /// The instruments a song draws on, in the order the song asks for them by number.
    /// <para>
    /// Every slot is filled, including the ones this build cannot play. A voicegroup with
    /// holes in it would mean instrument seven meaning different things in two songs, because
    /// the number a track uses is a position rather than a name — so the shapes and the
    /// composite kinds come back silent rather than absent.
    /// </para>
    /// </summary>
    private static List<Instrument> Voicegroup(Rom rom, SoundTreeResult tree, int at)
    {
        // The run this offset falls inside, and the instruments from that offset on. A song
        // names a voicegroup by pointing at its first instrument, and where the voicegroup
        // ends is not written down anywhere — so it runs to the end of what was confirmed.
        if (tree.Voicegroups.FirstOrDefault(v => v.Holds(at)) is not { } group) return [];

        IReadOnlyList<InstrumentRecord> instruments = group.From(at);

        var built = new List<Instrument>(instruments.Count);

        foreach (InstrumentRecord instrument in instruments)
        {
            if (!instrument.IsSampled)
            {
                // The shapes are the four channels that are circuits rather than recordings.
                // They have their own machinery and this is not it, so the slot is held open
                // and silent — see PokeMmo.Core.Sound.Psg, which plays them.
                built.Add(Instrument.Nothing);

                continue;
            }

            if (rom.ToOffsetOrNull(instrument.Pointer) is not { } sampleAt
                || tree.Samples.FirstOrDefault(s => s.Offset == sampleAt) is not { } record)
            {
                built.Add(Instrument.Nothing);

                continue;
            }

            sbyte[] audio = CryDecoder.Decode(rom, record);

            built.Add(new Instrument(
                new Voice(audio, record.Rate, record.Loops, record.LoopStart),
                rom.ReadU8(instrument.Offset + 1),
                rom.ReadU8(instrument.Offset + 8),
                rom.ReadU8(instrument.Offset + 9),
                rom.ReadU8(instrument.Offset + 10),
                rom.ReadU8(instrument.Offset + 11)));
        }

        return built;
    }
}
