namespace PokeMmo.RomExtract.Scripts;

/// <summary>One number a script stream uses as a flag and as a variable.</summary>
/// <param name="Number">The number.</param>
/// <param name="AsAFlag">Places a <c>setflag</c> or <c>clearflag</c> names it.</param>
/// <param name="AsAVariable">Places a variable command names it.</param>
public sealed record SharedNumber(int Number, int AsAFlag, int AsAVariable)
{
    public override string ToString() =>
        $"0x{Number:X4}  {AsAFlag} as a flag, {AsAVariable} as a variable";
}

/// <summary>What one sweep of a script stream found in each namespace.</summary>
/// <param name="Flags">Numbers named by <c>setflag</c> or <c>clearflag</c>.</param>
/// <param name="Variables">Numbers named by a command that writes or reads a variable.</param>
/// <param name="Commands">How many commands were read, so the two above have a denominator.</param>
public sealed record BothNamespaces(
    IReadOnlyDictionary<int, int> Flags,
    IReadOnlyDictionary<int, int> Variables,
    int Commands)
{
    /// <summary>The numbers used in both, most-named first.</summary>
    public IReadOnlyList<SharedNumber> Shared =>
    [
        .. Flags.Keys.Where(Variables.ContainsKey)
            .Select(n => new SharedNumber(n, Flags[n], Variables[n]))
            .OrderByDescending(n => n.AsAFlag + n.AsAVariable)
            .ThenBy(n => n.Number),
    ];

    /// <summary>
    /// What the overlap would be if the two sets landed independently in the span they occupy.
    /// </summary>
    /// <remarks>
    /// Not because chance is a plausible explanation, but because of SIZE: two sets of a few
    /// hundred numbers taken from a wide space overlap by about none, and an overlap far above
    /// that is the namespaces genuinely sharing numbers. Over the span the two sets between them
    /// occupy rather than over all 65536, which is the conservative direction — a narrower span
    /// makes a chance collision MORE likely, so anything above this floor clears a harder one.
    /// </remarks>
    public double Floor
    {
        get
        {
            if (Flags.Count == 0 || Variables.Count == 0) return 0;

            double span =
                Math.Max(Flags.Keys.Max(), Variables.Keys.Max())
                - Math.Min(Flags.Keys.Min(), Variables.Keys.Min())
                + 1;

            return Flags.Count * (Variables.Count / span);
        }
    }
}

/// <summary>
/// The numbers a script stream uses in both namespaces at once.
/// <para>
/// <b>A flag 0x4001 and a variable 0x4001 are different things, and this project's command line
/// cannot tell them apart.</b> <c>--trace 0x003F</c> watches a VARIABLE, answers "nothing the run
/// executed touched it", and is about something else entirely — which is how 240 printed that
/// line about a flag three scripts had just cleared. Every reading that matters decides by the
/// COMMAND and is safe; the argument on the command line is a bare number and is not.
/// </para>
/// <para>
/// <b>Asked of the map scan and not of the image.</b> The whole-image version of this question
/// answers 2117 flags, 12659 variables and 1182 shared, which is not a finding about anything:
/// sixteen megabytes of graphics contain every three-byte pattern many times over, and 233 threw
/// away a raw sweep for exactly this reason. Both are printed, and the image one is the noise.
/// </para>
/// </summary>
public static class TwoNamespacesOneNumber
{
    private const byte SetFlag = 0x29;
    private const byte ClearFlag = 0x2A;

    /// <summary>Commands that put something INTO a variable, and where the id sits.</summary>
    private static readonly (byte Code, int At)[] Writers =
        [(0x16, 0), (0x17, 0), (0x18, 0), (0x1A, 0)];

    /// <summary>
    /// Commands that LOOK at one. Both operands of <c>comparevars</c>, and the source of the
    /// copying pair rather than the destination — a destination is a write and counting it here
    /// would make every write a read as well.
    /// </summary>
    private static readonly (byte Code, int At)[] Readers =
        [(0x21, 0), (0x22, 0), (0x22, 2), (0x19, 2), (0x1A, 2)];

    /// <summary>
    /// Every number the given scripts name in each namespace, following calls and jumps.
    /// </summary>
    /// <param name="rom">The cartridge.</param>
    /// <param name="from">Script addresses to start from — the map scan's own list.</param>
    public static BothNamespaces Of(Rom rom, IEnumerable<uint> from)
    {
        var flags = new Dictionary<int, int>();
        var variables = new Dictionary<int, int>();
        var seen = new HashSet<uint>();

        var commands = 0;

        foreach (uint start in from)
        {
            foreach (uint block in ScriptReader.Reachable(rom, start))
            {
                // Each block once. A block reached from two maps is one block, and counting it
                // twice would inflate both namespaces by the same factor and the FLOOR by the
                // square of it — which is the direction that turns nothing into a finding.
                if (!seen.Add(block)) continue;

                // ONE BLOCK, and not ReadAll — that one walks everything reachable from where
                // it is pointed and has its own seen-set, so calling it once per block read the
                // target of every goto again for every block that jumps there. It counted this
                // fixture's one setflag twice from one starting address.
                foreach (ScriptCommand command in ScriptReader.Read(rom, block))
                {
                    commands++;

                    if (command.Code is SetFlag or ClearFlag)
                    {
                        int flag = command.Word();

                        flags[flag] = flags.GetValueOrDefault(flag) + 1;

                        continue;
                    }

                    foreach ((byte code, int at) in Writers.Concat(Readers))
                    {
                        if (command.Code != code) continue;
                        if (command.Arguments.Length < at + 2) continue;

                        int variable = command.Word(at);

                        variables[variable] = variables.GetValueOrDefault(variable) + 1;
                    }
                }
            }
        }

        return new BothNamespaces(flags, variables, commands);
    }
}
