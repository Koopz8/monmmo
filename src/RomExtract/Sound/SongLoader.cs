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
/// <summary>
/// Why a song did not come back, when one did not.
/// <para>
/// "142 of 255 songs do not assemble" is a number nobody can act on. Which of these it is
/// says whether the trouble is in the table, the voicegroup walk or the track reader — three
/// entirely different places, and until now they were one word.
/// </para>
/// </summary>
public enum SongTrouble
{
    /// <summary>It came back.</summary>
    None,

    /// <summary>No such song number in the table.</summary>
    NoSuchSong,

    /// <summary>The table names a header this walk did not confirm.</summary>
    NoHeader,

    /// <summary>The header names a voicegroup this walk did not confirm.</summary>
    NoVoicegroup,

    /// <summary>Every one of its tracks ran off somewhere this reader could not follow.</summary>
    EveryTrackDropped,
}

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
    public static SongPlayer? Load(Rom rom, SoundTreeResult tree, int song, Mixer mixer) =>
        Load(rom, tree, song, mixer, out _);

    /// <summary>
    /// The same, and why not when not. Every caller that reports rather than plays wants the
    /// reason, and a caller that merely plays can go on ignoring it.
    /// </summary>
    public static SongPlayer? Load(
        Rom rom, SoundTreeResult tree, int song, Mixer mixer, out SongTrouble why)
    {
        why = SongTrouble.None;

        if (song < 0 || song >= tree.Table.Count)
        {
            why = SongTrouble.NoSuchSong;

            return null;
        }

        int at = tree.Table[song].HeaderOffset;

        if (tree.Songs.FirstOrDefault(s => s.Offset == at) is not { } header)
        {
            why = SongTrouble.NoHeader;

            return null;
        }

        List<Instrument> voicegroup = Voicegroup(rom, tree, header.VoicegroupOffset);

        if (voicegroup.Count == 0)
        {
            why = SongTrouble.NoVoicegroup;

            return null;
        }

        List<Track> tracks =
        [
            .. header.TrackOffsets
                .Select(track => SequenceReader.Read(rom, track))

                // A track that never reached an end command is one this reader did not
                // follow to its end, and performing it would be performing a guess. Dropped
                // rather than played, and a song whose every track is dropped comes back as
                // nothing at all rather than as a song of no tracks.
                .Where(read => read.EndedProperly)

                // Whether the track repeats travels with it. Without it a performer can
                // count the tracks that have stopped but not the ones that were never
                // supposed to, and those are the same number until they are not.
                .Select(read => new Track(read.Events, read.Loops)),
        ];

        if (tracks.Count == 0)
        {
            why = SongTrouble.EveryTrackDropped;

            return null;
        }

        return new SongPlayer(tracks, voicegroup, mixer);
    }

    /// <summary>
    /// One of the circuit channels, as something that can be played.
    /// <para>
    /// <b>Where the numbers come from.</b> A square instrument keeps its duty in the same
    /// four bytes a recorded one keeps its pointer — there is no recording to point at, so
    /// the field holds a small number instead. The wave channel does point at something: the
    /// sixteen bytes that describe its shape. Both of those are modelled readings of a field
    /// whose meaning depends on the kind, and both are wrong in an audible rather than a
    /// destructive way.
    /// </para>
    /// </summary>
    private static Instrument Circuit(Rom rom, InstrumentRecord instrument)
    {
        Voice voice = instrument.Kind switch
        {
            InstrumentKind.Square => PsgVoices.Square((int)(instrument.Pointer & 3)),

            InstrumentKind.Wave => PsgVoices.Wave(
                rom.ToOffsetOrNull(instrument.Pointer) is { } at && at + 16 <= rom.Length
                    ? rom.Slice(at, 16)
                    : []),

            InstrumentKind.Noise => PsgVoices.Noise((instrument.Pointer & 1) != 0),

            // The two composite kinds hand a range of keys to other instruments, which is a
            // table this build does not walk. Silent rather than guessed at.
            _ => Instrument.Nothing.Voice,
        };

        if (voice.Audio.Length == 0) return Instrument.Nothing;

        // The same four bytes as a recorded instrument, on a different scale. These channels
        // count their envelope in four bits rather than eight, so a sustain of fifteen is
        // full rather than six per cent — which is the difference between a melody and a
        // melody nobody can hear.
        (byte attack, byte decay, byte sustain, byte release) = PsgVoices.Shaping(
            rom.ReadU8(instrument.Offset + 8),
            rom.ReadU8(instrument.Offset + 9),
            rom.ReadU8(instrument.Offset + 10),
            rom.ReadU8(instrument.Offset + 11));

        return new Instrument(voice, PsgVoices.MiddleCKey, attack, decay, sustain, release);
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
                // The four channels that are circuits rather than recordings — and on a real
                // cartridge they are most of a voicegroup, not an afterthought. Twenty-two of
                // twenty-four slots in the town theme's group are these, so a build that held
                // them open and silent played a twelfth of the music.
                //
                // A cycle of a square wave is a recording played round and round, so each one
                // becomes a looping Voice and everything above this works on it unchanged.
                built.Add(Circuit(rom, instrument));

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
