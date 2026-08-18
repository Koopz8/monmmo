using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// What one variable can be made to hold, from the writes whose value is in the bytes.
/// </summary>
/// <param name="Variable">The variable.</param>
/// <param name="Set">Every value a <c>setvar</c> in the scan puts in it.</param>
/// <param name="Steps">Every amount an <c>addvar</c> or <c>subvar</c> moves it by.</param>
/// <param name="Copied">
/// True when something copies into it. What a copy leaves is another variable's contents and is
/// not in the bytes, so a copied-into variable can hold things this cannot enumerate — which is a
/// THIRD answer and not a reachable value.
/// </param>
public sealed record WhatItCanHold(
    int Variable,
    IReadOnlyCollection<int> Set,
    IReadOnlyCollection<int> Steps,
    bool Copied)
{
    /// <summary>
    /// Whether some sequence of the writes this cartridge contains can leave
    /// <paramref name="value"/> in it.
    /// </summary>
    /// <param name="value">The value a condition wants.</param>
    /// <param name="ceiling">
    /// How far to count. A counter with a step of one reaches every number eventually, and the
    /// question is only ever asked about values a condition names, so counting past the largest
    /// of those is work with no answer at the end of it.
    /// </param>
    public bool CanReach(int value, int ceiling)
    {
        if (Set.Contains(value)) return true;
        if (Steps.Count == 0) return false;
        if (value < 0 || value > ceiling) return false;

        // A bounded walk from every starting value. Bounded because a step of one reaches
        // everything and an unbounded closure would answer yes to every question ever asked.
        var reached = new HashSet<int>(Set.Where(v => v >= 0 && v <= ceiling));
        var todo = new Queue<int>(reached);

        while (todo.Count > 0)
        {
            int at = todo.Dequeue();

            foreach (int step in Steps)
            {
                foreach (int next in new[] { at + step, at - step })
                {
                    if (next < 0 || next > ceiling) continue;
                    if (!reached.Add(next)) continue;
                    if (next == value) return true;

                    todo.Enqueue(next);
                }
            }
        }

        return false;
    }
}

/// <summary>
/// What every variable the map scan writes can be made to hold.
/// <para>
/// <b>A limitation this project has declared since 229 and never measured.</b>
/// <c>--arrivals</c> asks whether anything writes the value a condition wants and answers off
/// <c>setvar</c> alone, saying out loud that a <c>copyvar</c> or an <c>addvar</c> writes something
/// the bytes do not carry — so a condition satisfiable only through one of those reads as
/// satisfiable by nothing. That is the safe direction and it has been quoted as a caveat for
/// twenty-five milestones with no number on it.
/// </para>
/// <para>
/// Half of it is readable. <b><c>addvar</c>'s second word is a literal</b>: a variable a script
/// sets to nought and another script adds one to can hold one, two, three and so on, and that is
/// in the bytes as plainly as a <c>setvar</c> is. The other half is not — what a <c>copyvar</c>
/// leaves is another variable's contents — and this reports that as a third answer rather than
/// folding it into either.
/// </para>
/// </summary>
public static class WhatAVariableCanHold
{
    private const byte SetVar = 0x16;
    private const byte AddVar = 0x17;
    private const byte SubVar = 0x18;
    private const byte CopyVar = 0x19;
    private const byte CopyVarIfNotZero = 0x1A;

    /// <summary>What one command does to a variable, if it does anything readable.</summary>
    /// <remarks>
    /// Split out so the rule can be asked of one command. The whole sweep needs a cartridge and
    /// a rule inside one is a rule no fixture can reach.
    /// </remarks>
    public static (int Variable, int? Value, int? Step, bool Copies)? WhatItDoes(ScriptCommand command)
    {
        if (command.Arguments.Length < 4) return null;

        return command.Code switch
        {
            SetVar => (command.Word(), command.Word(2), null, false),
            AddVar => (command.Word(), null, command.Word(2), false),

            // A subtraction is a step of the same size. Which direction it goes is not a fact
            // about what the variable can HOLD — a counter that can go up by one and down by one
            // reaches the same set either way, and a walk that only added would answer no to a
            // value below where it started.
            SubVar => (command.Word(), null, command.Word(2), false),
            CopyVar or CopyVarIfNotZero => (command.Word(), null, null, true),
            _ => null,
        };
    }

    /// <summary>
    /// The literal a copy leaves, when the command immediately before it put one in the very
    /// variable being copied from — and null otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rule 255 is about.</b> <c>setvar 0x8004, 3 ; copyvar 0x406F, 0x8004</c> writes three
    /// into <c>0x406F</c>, and that is in the bytes as plainly as a <c>setvar</c> is. 229 reported
    /// twenty maps wanting 1/2/3/5/6/7/8 of <c>0x406F</c> against a single writer that writes
    /// nought, and said in its own documentation that a value written through a copy would read as
    /// written by nothing. It does, and this is how much.
    /// </para>
    /// <para>
    /// <b>Immediately before, and nothing further back.</b> Anything between the two can write the
    /// source without saying so: the third of <c>0x406F</c>'s copies has <c>special 0x014B</c>
    /// before it, whose reply this project cannot read, and a reading that carried an earlier
    /// literal past it would invent a value. The alternative is a barrier list of every command
    /// that might write a variable, which this project has had to fix twice (214, 220). Adjacency
    /// needs no list and cannot go stale.
    /// </para>
    /// </remarks>
    public static int? CopiedLiteral(ScriptCommand copy, ScriptCommand? before)
    {
        if (copy.Code is not (CopyVar or CopyVarIfNotZero)) return null;
        if (copy.Arguments.Length < 4) return null;
        if (before is not { } previous) return null;
        if (WhatItDoes(previous) is not { Value: { } literal } put) return null;

        return put.Variable == copy.Word(2) ? literal : null;
    }

    /// <summary>Every variable the given scripts write, and what they can leave in it.</summary>
    public static IReadOnlyDictionary<int, WhatItCanHold> In(Rom rom, MapLibrary library)
    {
        var set = new Dictionary<int, HashSet<int>>();
        var steps = new Dictionary<int, HashSet<int>>();
        var copied = new HashSet<int>();

        foreach ((string _, string _, uint address) in library.EveryScript())
        {
            // ONE HOP, AND ONLY FROM THE COMMAND IMMEDIATELY BEFORE.
            //
            // `setvar 0x8004, 3 ; copyvar 0x406F, 0x8004` writes THREE into 0x406F, and that is
            // in the bytes as plainly as a setvar is — the reading just never followed it. 229
            // reported that twenty maps want 1/2/3/5/6/7/8 of 0x406F and the only writer writes
            // nought; three and six are written by this idiom, on one map, twice.
            //
            // Immediately before, and not "the last literal seen", because anything between the
            // two can write the source without saying so: at the third of 0x406F's copies the
            // command before is `special 0x014B`, whose reply this project cannot read, and a
            // reading that carried an earlier literal past it would invent a value. The
            // alternative is a barrier list of every command that might write a variable, which
            // is a thing this project has had to fix twice (214, 220). Adjacency needs no list.
            ScriptCommand? before = null;

            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (WhatItDoes(command) is not { } does)
                {
                    before = command;

                    continue;
                }

                if (does.Value is { } value)
                {
                    if (!set.TryGetValue(does.Variable, out HashSet<int>? values))
                        set[does.Variable] = values = [];

                    values.Add(value);
                }

                if (does.Step is { } step and > 0)
                {
                    if (!steps.TryGetValue(does.Variable, out HashSet<int>? by))
                        steps[does.Variable] = by = [];

                    by.Add(step);
                }

                if (does.Copies)
                {
                    if (CopiedLiteral(command, before) is { } literal)
                    {
                        if (!set.TryGetValue(does.Variable, out HashSet<int>? values))
                            set[does.Variable] = values = [];

                        values.Add(literal);
                    }
                    else
                    {
                        copied.Add(does.Variable);
                    }
                }

                before = command;
            }
        }

        return set.Keys.Union(steps.Keys).Union(copied)
            .ToDictionary(
                v => v,
                v => new WhatItCanHold(
                    v,
                    set.GetValueOrDefault(v) ?? [],
                    steps.GetValueOrDefault(v) ?? [],
                    copied.Contains(v)));
    }
}
