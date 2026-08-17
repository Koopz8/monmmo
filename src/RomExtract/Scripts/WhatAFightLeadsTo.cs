using PokeMmo.RomExtract;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// One <c>trainerbattle</c> in the image, and the two places a beaten trainer could resume.
/// </summary>
/// <param name="MapId">Which map the script it sits in belongs to.</param>
/// <param name="Who">Which person, trigger, sign or arrival script that is.</param>
/// <param name="At">Where the command itself is.</param>
/// <param name="Variant">Its first argument, which chooses how many pointers follow.</param>
/// <param name="TrainerId">Which trainer.</param>
/// <param name="After">The address of the byte after the command — the fall-through.</param>
/// <param name="Jump">
/// The last of its pointers that reads like a script, which is where the runner goes instead.
/// </param>
public sealed record AFight(
    string MapId,
    string Who,
    uint At,
    byte Variant,
    int TrainerId,
    uint After,
    uint Jump)
{
    /// <summary>What the fall-through turned out to be.</summary>
    public required WhatFollows Follows { get; init; }

    /// <summary>How many places in the whole image hold the fall-through address as a pointer.</summary>
    public required int NamedBy { get; init; }

    /// <summary>Whether the jump's own block comes back to the fall-through.</summary>
    public required bool JumpRejoins { get; init; }

    public override string ToString() =>
        $"{MapId,-8} {Who,-22} 0x{At + Rom.BaseAddress:X8} kind {Variant} trainer 0x{TrainerId:X3}";
}

/// <summary>What the bytes immediately after a <c>trainerbattle</c> read as.</summary>
public enum WhatFollows
{
    /// <summary>They do not read as commands at all — so nothing is being lost by not reading them.</summary>
    NotCommands,

    /// <summary>The block stops at once: an <c>end</c> or a <c>return</c> and nothing before it.</summary>
    NothingAtAll,

    /// <summary>Something is said, and then the block ends. The second line, and no more.</summary>
    JustALine,

    /// <summary>A conditional branch is reached before the block ends. A GUARD.</summary>
    AGuard,
}

/// <summary>
/// Where a beaten trainer carries on, asked of the cartridge instead of assumed.
/// <para>
/// <b>A <c>trainerbattle</c> has two exits and only one of them can be right.</b> The command
/// carries between one and four pointers; the runner, when the trainer is already beaten,
/// takes the last of them that reads like a script — which was derived from the ROCKET
/// HIDEOUT, where the <c>clearflag</c> that puts the LIFT KEY on the floor is inside one and
/// sixty-six maps sat behind it. The other exit is the byte after the command, which the same
/// reading never reaches.
/// </para>
/// <para>
/// The two are distinguishable by reading. A cartridge does not put a <c>checkflag</c> and a
/// branch somewhere nothing can arrive: if the bytes after the command are a guard, and
/// nothing else in the image names that address, then falling through is the only reading
/// under which those bytes mean anything. If they are a line and an end, either exit is
/// harmless and the question does not arise.
/// </para>
/// <para>
/// So this counts the shapes rather than arguing about the variants. It is a scan and it can
/// come back empty.
/// </para>
/// </summary>
public static class WhatAFightLeadsTo
{
    /// <summary>How far to read the fall-through before giving up on finding its end.</summary>
    private const int Far = 64;

    /// <summary>
    /// Every <c>trainerbattle</c> reachable from these scripts, with both of its exits read.
    /// </summary>
    public static IReadOnlyList<AFight> In(
        Rom rom, IEnumerable<SetsAFlag> scripts, IReadOnlyDictionary<uint, IReadOnlyList<int>>? names = null)
    {
        var found = new List<AFight>();
        var seen = new HashSet<uint>();

        foreach (SetsAFlag script in scripts)
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, script.Address))
            {
                if (command.Code != ScriptCommands.TrainerBattle) continue;

                var at = (uint)command.Offset;

                if (!seen.Add(at)) continue;

                var after = (uint)(Rom.BaseAddress + command.Offset + 1 + command.Arguments.Length);

                uint jump = ScriptReader.ScriptsAfterAFight(rom, command).LastOrDefault();

                found.Add(new AFight(
                    script.MapId,
                    script.What,
                    at,
                    command.Arguments.Length > 0 ? command.Arguments[0] : (byte)0xFF,
                    command.Word(1),
                    after,
                    jump)
                {
                    Follows = Reads(rom, after),
                    NamedBy = names?.GetValueOrDefault(after)?.Count ?? -1,
                    JumpRejoins = jump != 0 && Reaches(rom, jump, after),
                });
            }
        }

        return found;
    }

    /// <summary>
    /// What one straight line of bytes reads as: nothing, a line, or a guard.
    /// <para>
    /// Only the straight line. A conditional is the thing being looked for, so following one
    /// would be deciding the answer in the middle of asking the question.
    /// </para>
    /// </summary>
    public static WhatFollows Reads(Rom rom, uint address)
    {
        if (rom.ToOffsetOrNull(address) is not { } offset) return WhatFollows.NotCommands;

        var said = false;

        for (int i = 0; i < Far; i++)
        {
            if (offset >= rom.Length) return WhatFollows.NotCommands;

            byte code = rom.ReadU8(offset);
            byte first = offset + 1 < rom.Length ? rom.ReadU8(offset + 1) : (byte)0;

            if (ScriptCommands.ArgumentLength(code, first) is not { } length) return WhatFollows.NotCommands;

            switch (code)
            {
                case ScriptCommands.GotoIf:
                case ScriptCommands.CallIf:
                    return WhatFollows.AGuard;

                case ScriptCommands.LoadPointer:
                case ScriptCommands.Message:
                case ScriptCommands.CallStandard:
                    said = true;
                    break;

                case ScriptCommands.End:
                case ScriptCommands.Return:
                case ScriptCommands.Goto:
                    return said ? WhatFollows.JustALine : WhatFollows.NothingAtAll;
            }

            offset += 1 + length;
        }

        return WhatFollows.NotCommands;
    }

    /// <summary>Whether the block at one address ever arrives at another, down any arm.</summary>
    private static bool Reaches(Rom rom, uint from, uint target) =>
        ScriptReader.ReadAll(rom, from)
            .Any(c => Rom.BaseAddress + (uint)c.Offset == target);
}
