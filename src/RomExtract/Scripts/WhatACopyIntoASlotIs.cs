using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// Where the value written into an argument slot came from — READ off the two namespaces this
/// project measured, not asserted (264).
/// </summary>
/// <remarks>
/// A variable id in this cartridge is <c>0x4000</c> upwards (the save's own) or <c>0x8000</c>
/// upwards (the argument slots). A copy's second word below <c>0x4000</c> is therefore not a
/// variable at all and can only be a literal, which is 244's finding in the one place it decides
/// something here: <c>0x1A</c> is <c>setorcopyvar</c>, and with a literal second word it is a
/// <c>setvar</c> wearing another opcode.
/// </remarks>
public enum WhereTheValueCameFrom
{
    /// <summary>A plain <c>setvar</c> — the thing 296 already counts, and the calibration row.</summary>
    ASetVar,

    /// <summary>A copy whose second word is below the variable bands, so it is a number.</summary>
    ALiteral,

    /// <summary>A copy out of the save's own band, whose value nothing here can read.</summary>
    TheSave,

    /// <summary>A copy out of another argument slot, which is where an answer lives.</summary>
    AnotherSlot,
}

/// <summary>
/// One write into an argument slot in the run of commands beside a <c>special</c> call.
/// </summary>
/// <param name="At">
/// The CALL's byte position, not the write's — 223's rule. A block hanging off nineteen maps is
/// nineteen records at one address, and every number here is counted in byte positions.
/// </param>
/// <param name="Before">
/// Whether the write is in front of the call or behind it. <b>Nothing behind a call can be an
/// argument to it</b>, which is what makes the same sweep run forward this reading's floor.
/// </param>
public sealed record AWriteIntoASlot(
    int At,
    string MapId,
    int Routine,
    byte Code,
    int Slot,
    int Source,
    bool Before)
{
    /// <summary>Which of the four kinds this write is.</summary>
    public WhereTheValueCameFrom From => Code == WhatACopyIntoASlotIs.SetVar
        ? WhereTheValueCameFrom.ASetVar
        : Source switch
        {
            >= SpecialCalls.FirstArgument => WhereTheValueCameFrom.AnotherSlot,
            >= WhatACopyIntoASlotIs.FirstSaved => WhereTheValueCameFrom.TheSave,
            _ => WhereTheValueCameFrom.ALiteral,
        };

    /// <summary>Whether the value being moved is the variable a routine answers into.</summary>
    public bool TheAnswer => Source == WhatACopyIntoASlotIs.TheAnswer;
}

/// <summary>One kind of write, counted on both sides of a call.</summary>
/// <param name="Before">Byte positions of calls with one of these in front of them.</param>
/// <param name="After">The same, behind them — the floor.</param>
/// <param name="Routines">Routines this kind would hand a value to.</param>
/// <param name="New">Of those, the ones 296 does not already read as handed one.</param>
public sealed record HowOftenBesideACall(
    WhereTheValueCameFrom From,
    int Before,
    int After,
    int Routines,
    int New)
{
    /// <summary>
    /// How much likelier this kind is in front of a call than behind it. A real argument
    /// mechanism must be above one; the <see cref="WhereTheValueCameFrom.ASetVar"/> row is what
    /// one looks like on this cartridge.
    /// </summary>
    public double Ratio => After == 0 ? double.PositiveInfinity : (double)Before / After;
}

/// <summary>
/// What a copy into an argument slot is worth (297) — 296's own stated limitation, measured.
/// <para>
/// 296 records a <c>setvar</c> and nothing else, and said so in its leftovers: <em>a value copied
/// into <c>0x8004</c> is invisible as an argument, and both halves of that are wrong in opposite
/// directions and neither is measured.</em> <b>A caveat you can state you can usually measure</b>
/// (42), and until you do you do not know whether it is a footnote or a fifth of the answer.
/// </para>
/// <para>
/// The floor is the same walk run FORWARD. Nothing behind a call can be an argument to it, so a
/// kind of write that is an argument must be commoner in front of a call than behind it — and the
/// plain <c>setvar</c>, which this project already reads as an argument, is in the table as the
/// row that says what that looks like (68). It shares <see cref="SpecialCalls.Around"/> with the
/// reading it is testing rather than walking its own copy of it (53).
/// </para>
/// </summary>
public static class WhatACopyIntoASlotIs
{
    /// <summary>Puts a literal in a variable.</summary>
    public const byte SetVar = 0x16;

    /// <summary>Copies one variable into another.</summary>
    public const byte CopyVar = 0x19;

    /// <summary>
    /// <c>setorcopyvar</c> — a copy when its second word is a variable id and a <c>setvar</c>
    /// when it is not, which is 244's operand read one command over.
    /// </summary>
    public const byte CopyVarIfNotZero = 0x1A;

    /// <summary>The bottom of the save's own variable band.</summary>
    public const int FirstSaved = 0x4000;

    /// <summary>The variable a routine leaves its answer in.</summary>
    public const int TheAnswer = 0x800D;

    /// <summary>Every write into an argument slot beside a call, in one script.</summary>
    /// <remarks>
    /// Split out so a fixture can hand it a handful of bytes: a rule only reachable through a
    /// whole cartridge is a rule no break can be aimed at, which is this project's most repeated
    /// structural fault (219, 221, 222, 223).
    /// </remarks>
    public static List<AWriteIntoASlot> In(Rom rom, string mapId, uint address)
    {
        var found = new List<AWriteIntoASlot>();

        List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

        for (var i = 0; i < commands.Count; i++)
        {
            int routine = commands[i].Code switch
            {
                SpecialCalls.Special => commands[i].Word(),
                SpecialCalls.SpecialVar => commands[i].Word(2),
                _ => -1,
            };

            if (routine < 0) continue;

            foreach (int step in new[] { SpecialCalls.Backwards, SpecialCalls.Forwards })
            {
                foreach ((ScriptCommand command, IReadOnlySet<int> taken) in
                         SpecialCalls.Around(commands, i, step))
                {
                    if (command.Code is not (SetVar or CopyVar or CopyVarIfNotZero)) continue;

                    int slot = command.Word();

                    if (slot is < SpecialCalls.FirstArgument or > SpecialCalls.LastArgument) continue;

                    // A slot something nearer the call has already spent is not this call's,
                    // whichever command wrote it — which is 296's rule, asked of the other
                    // commands that write one.
                    if (taken.Contains(slot)) continue;

                    found.Add(new AWriteIntoASlot(
                        commands[i].Offset,
                        mapId,
                        routine,
                        command.Code,
                        slot,
                        command.Word(2),
                        step == SpecialCalls.Backwards));
                }
            }
        }

        return found;
    }

    /// <summary>The same over every script the map scan opens.</summary>
    public static List<AWriteIntoASlot> All(Rom rom, MapLibrary library)
    {
        var found = new List<AWriteIntoASlot>();

        foreach ((string mapId, string _, uint address) in library.EveryScript())
            found.AddRange(In(rom, mapId, address));

        return found;
    }

    /// <summary>
    /// The four kinds, counted on both sides of a call — the whole reading in one table.
    /// </summary>
    /// <param name="handed">
    /// The routines 296 already reads as handed a value, so the NEW column is what adopting a
    /// kind would actually cost. Trap 9: a count of how wrong something could be is not a count
    /// of how wrong it is.
    /// </param>
    public static IReadOnlyList<HowOftenBesideACall> Read(
        IReadOnlyList<AWriteIntoASlot> all, IReadOnlySet<int> handed, bool countTheAnswer = true)
    {
        var rows = new List<HowOftenBesideACall>();

        foreach (WhereTheValueCameFrom kind in Enum.GetValues<WhereTheValueCameFrom>())
        {
            List<AWriteIntoASlot> mine =
                [.. all.Where(w => w.From == kind && (countTheAnswer || !w.TheAnswer))];

            List<AWriteIntoASlot> before = [.. mine.Where(w => w.Before)];

            rows.Add(new HowOftenBesideACall(
                kind,
                before.Select(w => w.At).Distinct().Count(),
                mine.Where(w => !w.Before).Select(w => w.At).Distinct().Count(),
                before.Select(w => w.Routine).Distinct().Count(),
                before.Select(w => w.Routine).Distinct().Count(r => !handed.Contains(r))));
        }

        return rows;
    }
}
