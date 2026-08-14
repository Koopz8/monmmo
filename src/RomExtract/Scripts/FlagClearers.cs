namespace PokeMmo.RomExtract.Scripts;

/// <summary>One place a flag is turned on or off, and the script it sits in.</summary>
public sealed record FlagChange(uint At, uint ScriptStart, bool Sets, uint PointedAtFrom, bool InsideAFight);

/// <summary>
/// Who turns a flag on, and who turns it off.
/// <para>
/// The reach report can now say what is behind somebody who is not there yet — the POKé
/// FLUTE behind flag 0x0035, the LIFT KEY behind 0x0036, the SILPH SCOPE behind 0x0037,
/// each of them a person standing on ground a player can already reach. What it cannot
/// say is <em>what would clear them</em>, and that is the next question in every case.
/// </para>
/// <para>
/// So: every <c>setflag</c> and <c>clearflag</c> on the image carrying that number,
/// walked back to the nearest address anything points at — which is where its script
/// begins — and decoded from there. It is a small tool and it answered a question two
/// roadmaps had been circling.
/// </para>
/// <para>
/// <b>What it found.</b> 0x0035 is cleared by a plain script, and the map walk finds the
/// person who runs it. 0x0036 and 0x0037 are cleared inside a <c>trainerbattle</c>'s
/// continuation — the script that runs once the fight is won — and no walk over people,
/// signs, triggers or entry scripts reaches them, because this project's reader does not
/// follow that pointer. Two thirds of the game's middle is behind one unfollowed
/// argument.
/// </para>
/// </summary>
public static class FlagClearers
{
    private const byte SetFlag = 0x29;

    private const byte ClearFlag = 0x2A;

    /// <summary>The command that starts a fight and carries scripts to run afterwards.</summary>
    private const byte TrainerBattle = 0x5C;

    /// <summary>
    /// How far back to look for the start of the script a change sits in.
    /// <para>
    /// Generous. A script that sets a flag has usually said something first, and the
    /// walk stops at the first address anything points at rather than at this bound.
    /// </para>
    /// </summary>
    private const int Backwards = 0x120;

    /// <summary>Every place this flag is set or cleared, with the script around it.</summary>
    public static List<FlagChange> Find(Rom rom, int flag)
    {
        var found = new List<FlagChange>();

        for (int at = 0; at + 3 <= rom.Length; at++)
        {
            byte code = rom.ReadU8(at);

            if (code != SetFlag && code != ClearFlag) continue;
            if (rom.ReadU16(at + 1) != flag) continue;

            uint address = Rom.BaseAddress + (uint)at;

            (uint start, uint from) = Begins(rom, address);

            found.Add(new FlagChange(
                address,
                start,
                code == SetFlag,
                from,
                start != 0 && InsideAFight(rom, start, address)));
        }

        return found;
    }

    /// <summary>
    /// Where the script holding this address begins, and what points at it.
    /// <para>
    /// Walked back a byte at a time to the first address anything in the image points
    /// at. Scripts are reached by pointer and nothing else, so the first address with a
    /// pointer to it is the entry — and an entry is what a person's record holds.
    /// </para>
    /// </summary>
    private static (uint Start, uint From) Begins(Rom rom, uint address)
    {
        for (uint back = address; back > address - Backwards; back--)
        {
            for (int at = 0; at + 4 <= rom.Length; at += 4)
            {
                if (rom.ReadU32(at) != back) continue;

                return (back, Rom.BaseAddress + (uint)at);
            }
        }

        return (0, 0);
    }

    /// <summary>
    /// True when the change is somewhere only a won fight leads to.
    /// <para>
    /// Decided by reading the script from its start: if the reader reaches the change
    /// then anybody walking that script reaches it too, and if it does not — while a
    /// <c>trainerbattle</c> stands between — then the change is in the fight's
    /// continuation, which is a pointer this project has never followed.
    /// </para>
    /// </summary>
    private static bool InsideAFight(Rom rom, uint start, uint change)
    {
        List<ScriptCommand> commands;

        try { commands = ScriptReader.ReadAll(rom, start); }
        catch { return false; }

        var fights = false;

        foreach (ScriptCommand command in commands)
        {
            if (Rom.BaseAddress + (uint)command.Offset == change) return false;
            if (command.Code == TrainerBattle) fights = true;
        }

        return fights;
    }
}
