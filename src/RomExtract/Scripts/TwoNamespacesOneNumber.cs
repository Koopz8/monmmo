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
    /// <summary>
    /// The same variable numbers again, split by WHICH operand of which command named them.
    /// </summary>
    /// <remarks>
    /// A namespace is not a thing a sweep observes — it is a thing each operand of each command
    /// declares. Summed together they cannot say which operand is dragging a number into a band
    /// it does not belong in, and that is the only question worth asking of an out-of-band
    /// number.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, int>> ByOperand { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<int, int>>();

    /// <summary>
    /// Numbers something LOOKS AT that is not a script command at all, by what does the looking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A read is not always a command, and every sweep in this project was one.</b>
    /// <see cref="TwoNamespacesOneNumber.Of"/> walks a script stream and decides what a number is
    /// by which operand of which command named it. That is the right shape for a question about a
    /// script stream and it is silently the wrong shape for a question about the cartridge: a map
    /// runs a script on arrival <em>when a variable holds a value</em>, and that condition sits in
    /// the map's own header as two halfwords. It names a variable, it is a read, and no command
    /// anywhere in the file is involved.
    /// </para>
    /// <para>
    /// So 245 reported <c>0x407C</c> as looked at nowhere in sixteen megabytes while nineteen maps
    /// were consulting it on arrival. Trap 1 one level down — the scan enumerated commands and the
    /// sentence was about the world.
    /// </para>
    /// <para>
    /// A dictionary rather than a set because a reader that is not a command has to say what did
    /// the reading, or it is an unfalsifiable subtraction from somebody else's number.
    /// </para>
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyCollection<int>> LookedAtBySomethingElse
    {
        get;
        init;
    } = new Dictionary<string, IReadOnlyCollection<int>>();

    /// <summary>Every number a non-command reader looks at.</summary>
    public IReadOnlyCollection<int> LookedAtOutsideTheCommands =>
        [.. LookedAtBySomethingElse.SelectMany(o => o.Value).Distinct()];

    /// <summary>Operands that put something INTO a variable rather than looking at one.</summary>
    public static readonly string[] Writing = ["0x16 arg0", "0x17 arg0", "0x18 arg0", "0x1A arg0"];

    /// <summary>Every number any writing operand names.</summary>
    public IReadOnlyCollection<int> Written =>
        [.. ByOperand.Where(o => Writing.Contains(o.Key)).SelectMany(o => o.Value.Keys)];

    /// <summary>
    /// How much of what a reading operand names is ever written, per operand.
    /// </summary>
    /// <remarks>
    /// <b>The test that does not need to know what a band is.</b> A variable something looks at
    /// is a variable something writes; an operand naming a hundred and forty-nine numbers of
    /// which three are ever written is not naming variables at all. This asserts no boundary
    /// from outside the file — it asks the file about itself.
    /// </remarks>
    public IReadOnlyList<(string Operand, int Written, int Numbers)> WrittenPerOperand
    {
        get
        {
            IReadOnlyCollection<int> written = Written;

            return
            [
                .. ByOperand.Where(o => !Writing.Contains(o.Key))
                    .Select(o => (
                        Operand: o.Key,
                        Written: o.Value.Keys.Count(written.Contains),
                        Numbers: o.Value.Count))
                    .OrderBy(o => o.Operand, StringComparer.Ordinal),
            ];
        }
    }

    /// <summary>
    /// Reading operands whose numbers are almost never written — the ones naming VALUES.
    /// </summary>
    /// <remarks>
    /// Half is a round number and it is deliberately doing no work: on this cartridge the
    /// operands come in at 2% and 86% to 100%, with nothing between. The percentages are
    /// printed beside it so the gap can be seen rather than trusted.
    /// </remarks>
    public IReadOnlyList<string> NameValues =>
    [
        .. WrittenPerOperand.Where(o => o.Numbers > 0 && o.Written * 2 < o.Numbers)
            .Select(o => o.Operand),
    ];

    /// <summary>
    /// Numbers a writing operand names and no LOOKING operand ever does — written and never
    /// read, asked of a population where the question has an answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>184 built one half of this and 214 needed the other.</b> The whole-image version says
    /// 650 written and never read against 1070 in the reversed image — the same order of number,
    /// so the aggregate is what these bytes do by accident and only the per-variable answers
    /// mean anything. Over the map scan the population is one this project can stand behind.
    /// </para>
    /// <para>
    /// The value-naming operands are left out, or a variable nothing looks at is hidden the
    /// moment somebody hands its number to a routine as a literal.
    /// </para>
    /// </remarks>
    public IReadOnlyList<int> WrittenAndNeverLookedAt =>
    [
        .. WrittenAndNeverLookedAtByACommand.Where(
            n => !LookedAtOutsideTheCommands.Contains(n)),
    ];

    /// <summary>
    /// The same list before any reader that is not a script command is subtracted from it.
    /// </summary>
    /// <remarks>
    /// <b>The size of the correction, printed rather than reasoned about.</b> This is exactly
    /// what 245 reported, and the difference between it and
    /// <see cref="WrittenAndNeverLookedAt"/> is the count of variables this project called
    /// unconsulted while the cartridge was consulting them somewhere no command exists. Kept for
    /// the same reason <see cref="WrittenAndNeverReadRaw"/> is: a correction whose size nobody
    /// can see is a number that changed for reasons the reader has to take on trust.
    /// </remarks>
    public IReadOnlyList<int> WrittenAndNeverLookedAtByACommand
    {
        get
        {
            IReadOnlyList<string> values = NameValues;

            HashSet<int> looked =
            [
                .. ByOperand
                    .Where(o => !Writing.Contains(o.Key) && !values.Contains(o.Key))
                    .SelectMany(o => o.Value.Keys),
            ];

            return [.. Written.Where(n => !looked.Contains(n)).Order()];
        }
    }

    /// <summary>
    /// And the same with the value-naming operands counted as looks — how many the raw reading
    /// would have hidden.
    /// </summary>
    public IReadOnlyList<int> WrittenAndNeverReadRaw
    {
        get
        {
            HashSet<int> looked =
            [
                .. ByOperand.Where(o => !Writing.Contains(o.Key)).SelectMany(o => o.Value.Keys),
                .. LookedAtOutsideTheCommands,
            ];

            return [.. Written.Where(n => !looked.Contains(n)).Order()];
        }
    }

    /// <summary>The numbers used in both, most-named first — counting every operand.</summary>
    public IReadOnlyList<SharedNumber> Shared => SharedOf(Variables);

    /// <summary>
    /// And the same, once the value-naming operands are left out of the variable side.
    /// </summary>
    /// <remarks>
    /// This is the honest answer to "how many numbers does this game use both ways". The raw
    /// version counts a literal 5 handed to a routine as a use of variable 5, and on this
    /// cartridge that is 26 of the 27.
    /// </remarks>
    public IReadOnlyList<SharedNumber> SharedRealVariables
    {
        get
        {
            IReadOnlyList<string> values = NameValues;

            var real = new Dictionary<int, int>();

            foreach ((string operand, IReadOnlyDictionary<int, int> of) in ByOperand)
            {
                if (values.Contains(operand)) continue;

                foreach ((int number, int places) in of)
                    real[number] = real.GetValueOrDefault(number) + places;
            }

            return SharedOf(real);
        }
    }

    private IReadOnlyList<SharedNumber> SharedOf(IReadOnlyDictionary<int, int> variables) =>
    [
        .. Flags.Keys.Where(variables.ContainsKey)
            .Select(n => new SharedNumber(n, Flags[n], variables[n]))
            .OrderByDescending(n => n.AsAFlag + n.AsAVariable)
            .ThenBy(n => n.Number),
    ];

    /// <summary>
    /// How the numbers of one namespace spread across the number space, in bands of
    /// <paramref name="width"/> — the shape, so that a band can be READ rather than asserted.
    /// </summary>
    /// <remarks>
    /// <b>Hardcode nothing.</b> This game's variables and flags live in bands, and this project
    /// is not allowed to write those bands down from outside knowledge: it has to find them by
    /// what they look like. Places as well as numbers, because one number named four hundred
    /// times and four hundred numbers named once are the same count and not the same band.
    /// </remarks>
    public static IReadOnlyList<(int From, int Numbers, int Places)> Bands(
        IReadOnlyDictionary<int, int> of, int width = 0x1000) =>
    [
        .. of.GroupBy(n => n.Key / width * width)
            .Select(g => (From: g.Key, Numbers: g.Count(), Places: g.Sum(n => n.Value)))
            .OrderBy(g => g.From),
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
        var byOperand = new Dictionary<string, Dictionary<int, int>>();
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

                        string operand = $"0x{code:X2} arg{at}";

                        if (!byOperand.TryGetValue(operand, out Dictionary<int, int>? each))
                            byOperand[operand] = each = [];

                        each[variable] = each.GetValueOrDefault(variable) + 1;
                    }
                }
            }
        }

        return new BothNamespaces(flags, variables, commands)
        {
            ByOperand = byOperand.ToDictionary(
                o => o.Key, o => (IReadOnlyDictionary<int, int>)o.Value),
        };
    }
}
