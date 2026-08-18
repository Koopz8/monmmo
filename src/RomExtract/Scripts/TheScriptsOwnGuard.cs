namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// What a conditional script does about the very variable its condition names.
/// </summary>
/// <remarks>
/// <para>
/// A map runs a script when a variable holds a value, and a square does the same. The record
/// carries the condition, so the script should not have to — and 111 of the 217 square scripts
/// whose condition names a value in <c>0..7</c> WRITE that variable (the disarm, which is how a
/// story beat stops happening twice) while <b>one</b> of them COMPARES it.
/// </para>
/// <para>
/// <b>The eleven that do are the whole of this cartridge's impossible bucket.</b> Every square
/// condition <c>--arrivals</c> reports as one nothing can produce wants <c>99</c>, and every one
/// of those eleven scripts opens <c>compare &lt;its own variable&gt;, 100</c> and ends
/// <c>setvar &lt;its own variable&gt;, 100</c>. Writing your own variable is ordinary; guarding
/// on it is not, and the two together mean the script is doing the record's job.
/// </para>
/// <para>
/// This says what the bytes do. What the ENGINE does with a condition value nothing can produce
/// is not readable from a script and this makes no claim about it.
/// </para>
/// </remarks>
public static class TheScriptsOwnGuard
{
    private const byte Compare = 0x21;

    private const byte SetVar = 0x16;

    /// <summary>
    /// The value the first <c>compare</c> of <paramref name="variable"/> tests it against, or
    /// null when the script never compares that variable.
    /// </summary>
    /// <remarks>
    /// The FIRST one, because a guard is at the top — a compare further down is the script
    /// branching on its own progress, which is a different thing and would make this answer yes
    /// for reasons that have nothing to do with the record.
    /// </remarks>
    public static int? Guard(IEnumerable<ScriptCommand> script, int variable)
    {
        foreach (ScriptCommand command in script)
        {
            if (command.Code != Compare || command.Arguments.Length < 4) continue;
            if (command.Word() != variable) continue;

            return command.Word(2);
        }

        return null;
    }

    /// <summary>
    /// The value the first <c>setvar</c> of <paramref name="variable"/> writes, or null when the
    /// script never writes it.
    /// </summary>
    public static int? Writes(IEnumerable<ScriptCommand> script, int variable)
    {
        foreach (ScriptCommand command in script)
        {
            if (command.Code != SetVar || command.Arguments.Length < 4) continue;
            if (command.Word() != variable) continue;

            return command.Word(2);
        }

        return null;
    }

    /// <summary>
    /// The variable the script's FIRST compare names, whatever it is — the control on
    /// <see cref="Guard"/>.
    /// </summary>
    /// <remarks>
    /// <b>Without this, "eleven scripts guard on their own variable" is a number with no floor.</b>
    /// Plenty of scripts open with a compare; the question is whether the variable it names is the
    /// one the record named, and that needs the share of scripts whose first compare names
    /// something else printed beside it.
    /// </remarks>
    public static int? FirstCompareNames(IEnumerable<ScriptCommand> script)
    {
        foreach (ScriptCommand command in script)
        {
            if (command.Code != Compare || command.Arguments.Length < 4) continue;

            return command.Word();
        }

        return null;
    }
}
