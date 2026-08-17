namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// What sits behind a command this project has no width for.
/// </summary>
/// <param name="WidthsThatParse">
/// How many candidate widths decode from here to a proper end. Zero means nothing does, and
/// the stop is probably not a command at all — a misread that landed on a data byte.
/// </param>
/// <param name="Commands">The most commands any of those widths reads before the block ends.</param>
/// <param name="Consequences">
/// Everything world-changing found behind the stop, at any width that parses: flags moved,
/// blocks jumped to, things handed over, people hidden or walked, fights, routines.
/// </param>
public sealed record Behind(int WidthsThatParse, int Commands, IReadOnlyList<byte> Consequences)
{
    /// <summary>
    /// True when every width that parses finds nothing but the block ending.
    /// <para>
    /// <b>The difference between a blocker and a nuisance.</b> A stop two bytes from a
    /// <c>release</c> costs nothing however many runs hit it; a stop eleven bytes from a
    /// <c>call</c> cost nineteen people on eleven maps. Counting how often a command stops a
    /// read ranks the first above the second, and that is a count being read as a ranking
    /// again.
    /// </para>
    /// </summary>
    public bool NothingBehindIt => WidthsThatParse > 0 && Consequences.Count == 0;

    /// <summary>What is behind it, in words, for anybody printing it.</summary>
    public override string ToString() =>
        WidthsThatParse == 0 ? "no width reads on from here at all — probably not a command"
        : Consequences.Count == 0 ? $"nothing but the block ending ({Commands} command(s))"
        : $"{Commands} command(s), including "
            + string.Join(", ", Consequences.Take(4).Select(ScriptCommands.NameOf));
}

/// <summary>
/// How much a stopped read is actually costing.
/// <para>
/// <b>Written because the biggest number was the wrong number.</b> The playthrough's own
/// reading stops at <c>0x73</c> three hundred and seventy-eight times — more than every other
/// unknown command on the cartridge put together — and at every one of its four sites what
/// follows is <c>releaseall</c> and <c>end</c>. Nothing is behind it. Meanwhile <c>0x9E</c>
/// stopped three blocks and one of those three was eleven bytes from the <c>call</c> that puts
/// nineteen people on eleven maps.
/// </para>
/// <para>
/// Ranking unknown commands by how often they stop a read puts the harmless one at the top and
/// buries the expensive one. Milestone 174 made this mistake with people in doorways and wrote
/// it down as a rule: <em>a count is not a ranking</em>. This is the same rule, applied to the
/// other list.
/// </para>
/// <para>
/// <b>The width is unknown — that is the whole problem — so this does not pick one.</b> It
/// tries every plausible width, keeps the ones that decode to a proper end, and reports what
/// they find between them. If they all find nothing, nothing is behind the stop whichever
/// width turns out to be right, and that conclusion needs no guess to stand on.
/// </para>
/// </summary>
public static class WhatIsBehindAStop
{
    /// <summary>
    /// Commands that change the world, as opposed to saying something or waiting.
    /// <para>
    /// Deliberately not "everything that is not text". A stop that hides a message is a stop
    /// worth fixing eventually; a stop that hides a <c>setflag</c> is a stop that has been
    /// making this project report a smaller world. Only the second kind should be able to move
    /// something to the top of a list.
    /// </para>
    /// </summary>
    private static readonly byte[] Consequential =
    [
        0x29,                       // setflag
        0x2A,                       // clearflag
        ScriptCommands.Call,
        ScriptCommands.Goto,
        ScriptCommands.GotoIf,
        ScriptCommands.CallIf,
        0x44, 0x46,                 // hands an item over
        0x79,                       // hands a creature over
        0x53,                       // takes somebody off the map
        MovementLists.ApplyMovement,
        ScriptCommands.TrainerBattle,
        SpecialCalls.Special,
        SpecialCalls.SpecialVar,
        0x16, 0x17, 0x18, 0x19, 0x1A, // writes a variable
    ];

    /// <summary>
    /// What lies behind a stop, across every width that reads on from it cleanly.
    /// </summary>
    /// <param name="at">Where the command this project cannot read begins.</param>
    /// <param name="maxWidth">The widest argument list worth trying.</param>
    /// <param name="maxCommands">How far past the stop to read before giving up.</param>
    public static Behind Of(Rom rom, int at, int maxWidth = 8, int maxCommands = 64)
    {
        var consequences = new List<byte>();

        var parsed = 0;
        var most = 0;

        for (int width = 0; width <= maxWidth; width++)
        {
            if (at + 1 + width >= rom.Length) continue;

            List<ScriptCommand> read = ScriptReader.Read(
                rom, Rom.BaseAddress + (uint)(at + 1 + width), maxCommands);

            // Only a width that reaches a proper end is evidence of anything. One that stops
            // at the next unknown command has not read what is behind this stop — it has moved
            // the same question along by a few bytes, and counting it would let a width that
            // reads two commands and dies claim there is nothing there.
            if (read.Count == 0) continue;
            if (read[^1].Code is not (ScriptCommands.End or ScriptCommands.Return or ScriptCommands.Goto))
                continue;

            parsed++;
            most = Math.Max(most, read.Count);

            foreach (ScriptCommand command in read)
            {
                if (!Consequential.Contains(command.Code)) continue;
                if (!consequences.Contains(command.Code)) consequences.Add(command.Code);
            }
        }

        return new Behind(parsed, most, consequences);
    }
}
