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
    /// Every variable an unresolved copy takes the value FROM — the sources of the copies that
    /// made <see cref="Copied"/> true.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A copy's source is in the bytes even when the value is not.</b> <c>copyvar</c>'s second
    /// operand names a variable, and this reading has always thrown that name away and reported
    /// "something copies into it" — which is true and is less than the cartridge says. Naming the
    /// source costs nothing and turns "this does not know" into "this does not know, and here is
    /// the variable to go and ask".
    /// </para>
    /// <para>
    /// <b>It is not a second hop and must not be quoted as one.</b> What the source held at the
    /// moment of the copy is a fact about the run, not about the file; 255 refused a barrier list
    /// for exactly this reason and the refusal stands. This says WHERE the value came from, not
    /// what it was.
    /// </para>
    /// </remarks>
    public IReadOnlyCollection<int> From { get; init; } = [];

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

    /// <summary>
    /// How many of the values in <c>0..ceiling</c> the counter walk reaches at all — the
    /// denominator on every answer <see cref="CanReach"/> gives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The number 258 exists for.</b> "A counter can reach it" reads as a finding and is one
    /// only if the counter cannot reach everything. `0x4001` is set to forty-five different values
    /// and stepped by one, two and four; its walk reaches <b>100 of the 100 values in 0..99</b>,
    /// so it answers yes to every question anybody will ever ask it. `0x4002` and `0x4003` do the
    /// same. Every variable this test has ever been given saturates, which means the three
    /// conditions 255 credited to a counter were credited by a test that could not say no.
    /// </para>
    /// <para>
    /// Saturation is an exact predicate and not a threshold: the walk either reaches every value
    /// in range or it does not. That is deliberate — this project has been caught by a band
    /// boundary before, and 244's written-ness rule works because it needs none.
    /// </para>
    /// </remarks>
    public int HowManyItReaches(int ceiling)
    {
        var reached = new HashSet<int>(Set.Where(v => v >= 0 && v <= ceiling));

        if (Steps.Count == 0) return reached.Count;

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

                    todo.Enqueue(next);
                }
            }
        }

        return reached.Count;
    }

    /// <summary>
    /// Whether the counter walk reaches EVERY value in <c>0..ceiling</c>, so that an answer of
    /// "a counter can reach it" carries no information at all.
    /// </summary>
    public bool ACounterReachesEverything(int ceiling) =>
        Steps.Count > 0 && HowManyItReaches(ceiling) == ceiling + 1;
}

/// <summary>
/// Which of the four answers a condition in the middle bucket has.
/// </summary>
/// <remarks>
/// <para>
/// The order the answers are decided in: a value the corrected write set already contains is
/// <see cref="Written"/> and the bucket was simply wrong about it; one a counter walks to is
/// <see cref="Counted"/>; one behind a copy from a source this cannot read is
/// <see cref="Copied"/>, which is the answer this reading is not able to give; and what is left
/// is <see cref="Neither"/>, the boundary the bucket was named for.
/// </para>
/// <para>
/// <b>Out here rather than inside the sweep that prints it.</b> 255 decided these four with
/// lambdas inside a function that needs a whole cartridge, and a rule inside a whole-world sweep
/// is a rule no fixture can reach — which is the fault this project has fixed at 219, 221, 222
/// and 223. A break aimed at those lambdas could only ever come back green.
/// </para>
/// </remarks>
public enum HowItIsReached
{
    /// <summary>Nothing writes the variable at all, or nothing that reaches this value.</summary>
    Neither,

    /// <summary>A <c>setvar</c>, or a copy whose source the command before it just set.</summary>
    Written,

    /// <summary>A counter walks to it, and <c>addvar</c>'s step is a literal.</summary>
    Counted,

    /// <summary>
    /// A counter "walks to it" and walks to every other value in range as well, so the answer
    /// carries no information — 258. Not a threshold: the walk either covers the whole range or
    /// it does not.
    /// </summary>
    CounterReachesEverything,

    /// <summary>A copy from a source this cannot read — the honest "does not know".</summary>
    Copied,
}

/// <summary>
/// Whether the condition on a script can ever be true, as far as this reading can see.
/// </summary>
/// <remarks>
/// <b>Three verdicts and an error bar.</b> The first two are readings, the third is an admission
/// and the fourth is the boundary. A count of the fourth means nothing without the third printed
/// beside it, which is why they are one enum and not two booleans.
/// </remarks>
public enum WhetherItCanFire
{
    /// <summary>Nothing this reading can see puts that value there.</summary>
    NothingCan,

    /// <summary>A <c>setvar</c>, a one-hop copy of a literal, or a counter reaches it.</summary>
    SomethingWritesIt,

    /// <summary>It wants nought, which every variable holds before anything writes it.</summary>
    ArmedFromTheStart,

    /// <summary>Behind a copy from a source this cannot read. The error bar.</summary>
    DoesNotKnow,
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

    /// <summary>
    /// Which of the four answers a condition wanting <paramref name="value"/> of
    /// <paramref name="variable"/> has.
    /// </summary>
    /// <param name="canHold">What every variable the scan writes can be made to hold.</param>
    /// <param name="ceiling">
    /// How far a counter is walked. The question is only ever asked about values a condition
    /// names, so counting past the largest of those is work with no answer at the end of it.
    /// </param>
    /// <remarks>
    /// <b>One function, so the four answers cannot come apart.</b> They are decided in order and
    /// every condition gets exactly one, which is what makes the four counts add up to the bucket
    /// they were split out of. Asking the four questions separately at each call site is how a
    /// condition ends up in two buckets or in none.
    /// </remarks>
    public static HowItIsReached HowReached(
        IReadOnlyDictionary<int, WhatItCanHold> canHold, int variable, int value, int ceiling)
    {
        if (!canHold.TryGetValue(variable, out WhatItCanHold? hold)) return HowItIsReached.Neither;

        if (hold.Set.Contains(value)) return HowItIsReached.Written;

        if (hold.Steps.Count > 0 && hold.CanReach(value, ceiling))
        {
            // AND THE DENOMINATOR ON THE ANSWER (258). A walk that reaches every value in range
            // has said yes before it was asked, and every variable this has ever been given —
            // 0x4001, 0x4002, 0x4003 — reaches 100 of 100. Reported as its own answer rather than
            // folded into either neighbour, because "a counter reaches it" and "the test cannot
            // say no" are different facts and only one of them is about the cartridge.
            return hold.ACounterReachesEverything(ceiling)
                ? HowItIsReached.CounterReachesEverything
                : HowItIsReached.Counted;
        }

        return hold.Copied ? HowItIsReached.Copied : HowItIsReached.Neither;
    }

    /// <summary>
    /// Whether anything this reading can see is able to put <paramref name="value"/> in
    /// <paramref name="variable"/>, so that the condition naming them could ever be true.
    /// </summary>
    /// <param name="writtenWithThis">
    /// How many places write that value with a plain <c>setvar</c> — the reading
    /// <c>--arrivals</c> has always had.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Four answers, and the third one is the error bar.</b> A verdict of "this condition can
    /// never fire" is worth exactly what the DOES NOT KNOW column beside it is small: a list
    /// where most of the middle bucket is behind an unreadable copy has no business quoting the
    /// remainder as a boundary. Printing the two together is the whole point — 249's rule, that
    /// a set difference needs the base rate of the thing being differenced, in the shape this
    /// reading takes.
    /// </para>
    /// <para>
    /// <b>Nought is its own answer.</b> Every variable holds nought before anything writes it, so
    /// a condition wanting nought is armed from the start whatever the writers do — which is a
    /// different fact from "something can set it to that" and is not folded into it. 250 said
    /// this out loud about the variables nothing writes; it is true of the ones something writes
    /// too, and saying so was the correction.
    /// </para>
    /// </remarks>
    public static WhetherItCanFire CanItFire(
        IReadOnlyDictionary<int, WhatItCanHold> canHold,
        int variable,
        int value,
        int writtenWithThis,
        int ceiling)
    {
        if (writtenWithThis > 0) return WhetherItCanFire.SomethingWritesIt;

        switch (HowReached(canHold, variable, value, ceiling))
        {
            case HowItIsReached.Written:
            case HowItIsReached.Counted:
                return WhetherItCanFire.SomethingWritesIt;

            // AND NOT CounterReachesEverything, which falls through on purpose (258). A walk that
            // reaches every value in range is not evidence that anything writes this one, and
            // 255 and 257 both quoted three conditions it had credited.

            // Before the copy, because holding nought is a positive fact about the start of the
            // game and "something copies into it from a source this cannot read" is an admission
            // of ignorance. An admission cannot outrank a reading.
            default:
                if (value == 0) return WhetherItCanFire.ArmedFromTheStart;

                return HowReached(canHold, variable, value, ceiling) == HowItIsReached.Copied
                    ? WhetherItCanFire.DoesNotKnow
                    : WhetherItCanFire.NothingCan;
        }
    }

    /// <summary>Every variable the given scripts write, and what they can leave in it.</summary>
    /// <remarks>
    /// The reading itself is <see cref="From"/>; this only decides which scripts to read. A rule
    /// inside a function that needs a whole cartridge is a rule no fixture can reach, and this
    /// project has spent four milestones on that (219, 221, 222, 223).
    /// </remarks>
    public static IReadOnlyDictionary<int, WhatItCanHold> In(Rom rom, MapLibrary library) =>
        From(library.EveryScript().Select(s => ScriptReader.ReadAll(rom, s.Address)));

    /// <summary>
    /// The same reading, over script command sequences rather than over a cartridge.
    /// </summary>
    /// <param name="scripts">Each script's commands, in the order they are read.</param>
    public static IReadOnlyDictionary<int, WhatItCanHold> From(
        IEnumerable<IEnumerable<ScriptCommand>> scripts)
    {
        var set = new Dictionary<int, HashSet<int>>();
        var steps = new Dictionary<int, HashSet<int>>();
        var copied = new HashSet<int>();
        var from = new Dictionary<int, HashSet<int>>();

        foreach (IEnumerable<ScriptCommand> script in scripts)
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

            foreach (ScriptCommand command in script)
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

                        // AND WHERE IT CAME FROM. The value is not in the bytes; the SOURCE is,
                        // and throwing it away is how 0x405F came to be reported as written by
                        // nothing while four copies on one map were filling it from 0x4001.
                        if (!from.TryGetValue(does.Variable, out HashSet<int>? sources))
                            from[does.Variable] = sources = [];

                        sources.Add(command.Word(2));
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
                    copied.Contains(v))
                {
                    From = from.GetValueOrDefault(v) ?? [],
                });
    }
}
