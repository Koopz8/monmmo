namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// The number <c>0x9C</c> takes, against the move the block asked about first.
/// </summary>
/// <remarks>
/// <para>
/// <b>This command already had a name in this repository and nothing outside one file knew it.</b>
/// 191 wrote <c>private const byte DoFieldEffect = 0x9C</c> inside the sweep that reads who knows
/// a move, and used it to say what a block offers to do. Everywhere else — the width table,
/// <see cref="ScriptCommands.NameOf"/>, every dump — it stayed a number, and 232 measured it as an
/// unnamed argument column forty-one milestones later. One command, two files, and neither of them
/// asking the other.
/// </para>
/// <para>
/// So this asks the question the name implies and had never been checked: <b>is the number a
/// function of the move?</b> It is derivable because the same block says both — a
/// <c>findmove</c> and then, past a yes-or-no, a <c>0x9C</c>.
/// </para>
/// </remarks>
public static class FieldEffectNumbers
{
    /// <param name="Move">The move the block asked about.</param>
    /// <param name="Effect">The number <c>0x9C</c> takes in the same block.</param>
    /// <param name="At">Where the <c>findmove</c> is.</param>
    public sealed record Offer(int Move, int Effect, int At);

    /// <summary>
    /// Whether each move has ONE number, and what the repeats do.
    /// </summary>
    /// <param name="Offers">How many blocks pair a move with a number.</param>
    /// <param name="Moves">How many distinct moves.</param>
    /// <param name="Effects">How many distinct numbers.</param>
    /// <param name="WithTwoNumbers">Any move that got more than one number — the failure.</param>
    /// <param name="Repeated">How many moves appear in more than one block.</param>
    /// <param name="RepeatedAgreeing">And how many of those got the same number every time.</param>
    public sealed record OneEach(
        int Offers,
        int Moves,
        int Effects,
        IReadOnlyList<int> WithTwoNumbers,
        int Repeated,
        int RepeatedAgreeing)
    {
        /// <summary>Nothing contradicts "one number per move" — which is not the same as evidence for it.</summary>
        public bool Holds => WithTwoNumbers.Count == 0;
    }

    /// <summary>
    /// One move, one number — asked of the offers rather than assumed.
    /// </summary>
    /// <remarks>
    /// <b>Counting distinct moves against distinct numbers is not this question.</b> Two moves and
    /// two numbers reads the same whether each move has its own or one move has both, and those
    /// are opposite findings. The move that got two is named.
    /// </remarks>
    public static OneEach PerMove(IEnumerable<Offer> offers)
    {
        var byMove = new Dictionary<int, HashSet<int>>();
        var seen = new Dictionary<int, int>();
        var all = 0;

        foreach (Offer offer in offers)
        {
            all++;

            if (!byMove.TryGetValue(offer.Move, out HashSet<int>? numbers))
                byMove[offer.Move] = numbers = [];

            numbers.Add(offer.Effect);

            seen[offer.Move] = seen.GetValueOrDefault(offer.Move) + 1;
        }

        List<int> repeated = [.. seen.Where(m => m.Value > 1).Select(m => m.Key).Order()];

        return new OneEach(
            all,
            byMove.Count,
            byMove.Values.SelectMany(n => n).Distinct().Count(),
            [.. byMove.Where(m => m.Value.Count > 1).Select(m => m.Key).Order()],
            repeated.Count,
            repeated.Count(m => byMove[m].Count == 1));
    }

    /// <summary>
    /// Whether one set of numbers is exactly the LOWEST of the two put together, and how often
    /// that would happen by chance.
    /// </summary>
    /// <param name="Cleanly">Every one of these below every one of those.</param>
    /// <param name="Of">How many distinct numbers there are altogether.</param>
    /// <param name="Taken">How many of them are in the first set.</param>
    /// <param name="OneIn">
    /// One in this many arrangements of which numbers land in the first set would be this clean.
    /// </param>
    public sealed record TheSplit(bool Cleanly, int Of, int Taken, double OneIn);

    /// <summary>
    /// The floor on "the move-driven numbers are all smaller than the others".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Six numbers against four sounds like nothing until the question is asked properly: if which
    /// six of the ten were the move-driven ones were down to chance, the odds of them being
    /// exactly the six smallest are one in <c>C(10, 6)</c>.
    /// </para>
    /// <para>
    /// With either side empty there is no split to be surprised by, and this says so rather than
    /// returning a large number about nothing.
    /// </para>
    /// </remarks>
    public static TheSplit AreTheLowest(IEnumerable<int> these, IEnumerable<int> those)
    {
        int[] mine = [.. these.Distinct().Order()];
        int[] theirs = [.. those.Distinct().Order()];

        int of = mine.Concat(theirs).Distinct().Count();

        if (mine.Length == 0 || theirs.Length == 0) return new TheSplit(false, of, mine.Length, 1);

        bool cleanly = mine[^1] < theirs[0];

        return new TheSplit(cleanly, of, mine.Length, Ways(of, mine.Length));
    }

    /// <summary>How many ways to choose <paramref name="k"/> of <paramref name="n"/>.</summary>
    private static double Ways(int n, int k)
    {
        double ways = 1;

        for (var i = 0; i < k; i++) ways = ways * (n - i) / (i + 1);

        return Math.Round(ways);
    }

    /// <summary>What comes after a <c>dofieldeffect</c> and looks like waiting for it.</summary>
    public enum Waiting
    {
        /// <summary>Nothing in the next few commands does.</summary>
        Nothing,

        /// <summary>A <c>0x27</c>, which takes no argument and so names nothing.</summary>
        Unnamed,

        /// <summary>A <c>0x9E</c> holding the SAME number the effect was started with.</summary>
        ByNumber,

        /// <summary>A <c>0x9E</c> holding a different one, which would be the interesting answer.</summary>
        ByADifferentNumber,
    }

    /// <param name="At">Where the <c>dofieldeffect</c> is.</param>
    /// <param name="Number">The number it takes.</param>
    /// <param name="How">What waits for it.</param>
    /// <param name="Waited">The number the waiter names, or nought when it names none.</param>
    public sealed record AfterIt(int At, int Number, Waiting How, int Waited);

    /// <summary>The command that waits without naming anything.</summary>
    public const byte WaitUnnamed = 0x27;

    /// <summary>The command that waits and names a number.</summary>
    public const byte WaitByNumber = 0x9E;

    /// <summary>
    /// What waits for the <c>dofieldeffect</c> at <paramref name="index"/>, within the next few
    /// commands of the same block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not just the next command.</b> One of this cartridge's four sites puts a <c>0x33</c>
    /// between the two, so a reading that looked only at the byte after would call it nothing —
    /// and <b>not arbitrarily far either</b>, or every block with a <c>0x27</c> anywhere in it
    /// would count. A second <c>dofieldeffect</c> ends the window whatever is left of it, because
    /// past that point a waiter is the next effect's, not this one's.
    /// </para>
    /// </remarks>
    public static AfterIt WhatWaits(IReadOnlyList<ScriptCommand> block, int index, int within = 4)
    {
        ScriptCommand started = block[index];

        int number = started.Arguments.Length >= 2 ? started.Word() : 0;

        for (int i = index + 1; i < block.Count && i <= index + within; i++)
        {
            ScriptCommand command = block[i];

            if (command.Code == ScriptCommands.DoFieldEffect) break;

            if (command.Code == WaitUnnamed)
                return new AfterIt(started.Offset, number, Waiting.Unnamed, 0);

            if (command.Code != WaitByNumber || command.Arguments.Length < 2) continue;

            return new AfterIt(
                started.Offset,
                number,
                command.Word() == number ? Waiting.ByNumber : Waiting.ByADifferentNumber,
                command.Word());
        }

        return new AfterIt(started.Offset, number, Waiting.Nothing, 0);
    }

    /// <summary>
    /// One in how many times a run of matches this long would happen by drawing from an alphabet
    /// this big.
    /// </summary>
    /// <remarks>
    /// The alphabet is a MODELLED choice and the answer moves a lot with it, so whoever prints
    /// this has to say which one they used. Nothing here picks it.
    /// </remarks>
    public static double Coincidence(int matches, int alphabet) =>
        matches <= 0 || alphabet <= 1 ? 1 : Math.Round(Math.Pow(alphabet, matches));

    /// <summary>
    /// Every place in the WHOLE IMAGE that reads as this command, and how many of them read on to
    /// a proper end.
    /// </summary>
    /// <remarks>
    /// One byte and a word is a three-byte pattern in sixteen megabytes, so this is quoted only
    /// against <see cref="NoiseFloor"/> — and on this cartridge the two are the same number, which
    /// is why the sites worth reading are the ones a map or a jump opens rather than these.
    /// </remarks>
    public static (int Sites, int ReadsOn, int Words) Sweep(Rom rom)
    {
        ReadOnlySpan<byte> image = rom.Span;

        var sites = 0;
        var readsOn = 0;
        var words = new HashSet<int>();

        for (var i = 0; i + 3 <= image.Length; i++)
        {
            if (image[i] != ScriptCommands.DoFieldEffect) continue;

            sites++;

            words.Add(image[i + 1] | (image[i + 2] << 8));

            if (ScriptReader.StoppedAt(rom, Rom.BaseAddress + (uint)i) is null) readsOn++;
        }

        return (sites, readsOn, words.Count);
    }

    /// <summary>The same sweep on this file reversed — same bytes, same frequencies, no commands.</summary>
    public static (int Sites, int ReadsOn, int Words) NoiseFloor(Rom rom)
    {
        byte[] backwards = rom.Span.ToArray();

        Array.Reverse(backwards);

        return Sweep(new Rom(backwards));
    }
}
