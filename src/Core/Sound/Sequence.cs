namespace PokeMmo.Core.Sound;

// The shape of a piece of music, kept apart from the reader that finds one.
//
// A command in a track is a fact about music rather than about a cartridge — the same shape
// would come out of a file, a network message or a test — so it lives beside the mixer that
// plays it rather than beside the reader that extracts it. That is the same split MoveData
// has had since the beginning: the record is core, the finding of it is not.

/// <summary>What one command in a track turned out to be.</summary>
public enum SequenceCommand
{
    /// <summary>Time passing, and the only command most tracks are mostly made of.</summary>
    Wait,

    /// <summary>A note beginning, with a key, a loudness, and sometimes a length.</summary>
    NoteOn,

    /// <summary>A note ending.</summary>
    NoteOff,

    /// <summary>The end of the track. Nothing after this is part of it.</summary>
    End,

    /// <summary>Carry on somewhere else, and do not come back.</summary>
    Goto,

    /// <summary>Carry on somewhere else, and do come back.</summary>
    Call,

    /// <summary>Come back.</summary>
    Return,

    /// <summary>One of the settings — tempo, volume, panning, instrument, and the rest.</summary>
    Setting,

    /// <summary>A byte this reader does not account for. Counted, never guessed at.</summary>
    Unknown,
}

/// <summary>One command, where it was, and what came with it.</summary>
public sealed record SequenceEvent(
    int Offset,
    byte Opcode,
    SequenceCommand Command,
    IReadOnlyList<byte> Arguments,
    int Target = -1);

