namespace PokeMmo.RomExtract.Sound;

/// <summary>
/// What one animation command is for, above the level of its opcode.
/// <para>
/// Grouped rather than one-per-opcode because what a player of this game will ever need is
/// the group: something appeared, something was heard, time passed, the script went
/// somewhere else. The opcode is kept on every event as well, so nothing is lost by grouping.
/// </para>
/// </summary>
public enum AnimCommand
{
    /// <summary>A graphic loaded or let go, named by tag.</summary>
    Graphic,

    /// <summary>Something made — a sprite, a visual task, a sound task.</summary>
    Creates,

    /// <summary>Time passing, or waiting for something to finish.</summary>
    Waits,

    /// <summary>A sound.</summary>
    Sound,

    /// <summary>Somewhere else — a call, a return, a jump, a conditional jump.</summary>
    Flow,

    /// <summary>The end of the script.</summary>
    End,

    /// <summary>Backgrounds, blending, priority, visibility — the screen rather than a sprite.</summary>
    Screen,

    /// <summary>Nothing at all. The format has two of these and they are used.</summary>
    Nothing,

    /// <summary>An opcode this reader does not account for. Counted, never guessed at.</summary>
    Unknown,
}

/// <summary>One animation command, where it was, and what came with it.</summary>
public sealed record AnimEvent(
    int Offset,
    byte Opcode,
    AnimCommand Command,
    IReadOnlyList<byte> Arguments,
    int Target = -1)
{
    /// <summary>
    /// The four-byte pointer this command names, when it names one. A sprite template for
    /// <c>createsprite</c>, a function for the two task commands, a destination for the
    /// jumps — and it is the identity that matters rather than what is behind it, since
    /// what is behind three of those four is compiled code.
    /// </summary>
    public uint Names =>
        Arguments.Count >= 4
            ? (uint)(Arguments[0] | (Arguments[1] << 8) | (Arguments[2] << 16) | (Arguments[3] << 24))
            : 0;
}

/// <summary>
/// One move's animation, read.
/// <para>
/// <see cref="EndedProperly"/> carries the same weight it does for a music track: a script
/// that ran to its end command is one this reader understood, and one that stopped for any
/// other reason is a finding.
/// </para>
/// </summary>
public sealed record AnimScript(
    int Offset,
    IReadOnlyList<AnimEvent> Events,
    bool EndedProperly,
    int Unknown)
{
    /// <summary>How long this animation takes, in frames, following no jumps.</summary>
    public int Frames => Events.Where(e => e.Opcode == 0x04).Sum(e => e.Arguments.Count > 0 ? e.Arguments[0] : 0);

    /// <summary>The sprite templates this animation names, as identities.</summary>
    public IReadOnlyList<uint> Templates =>
        [.. Events.Where(e => e.Opcode == 0x02).Select(e => e.Names).Distinct()];

    /// <summary>The sounds it plays, by the game's own id.</summary>
    public IReadOnlyList<int> Sounds =>
        [.. Events.Where(e => e.Command == AnimCommand.Sound && e.Arguments.Count >= 2)
            .Select(e => e.Arguments[0] | (e.Arguments[1] << 8)).Distinct()];
}
