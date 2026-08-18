namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// One question asked of any command that takes a byte and then a word: <b>is the byte an index?</b>
/// </summary>
/// <remarks>
/// <para>
/// This cartridge has several three-byte commands nobody has named, and they look alike from
/// outside: a byte, then a word. `0x9D` on `10.14` runs three times in a row saying
/// <c>0, 255</c> / <c>1, 10</c> / <c>2, 14</c>, which reads like filling three slots. `0x7F`
/// takes <c>0, 0x800D</c> at all three of its places. `0x82` takes a byte and a word too.
/// </para>
/// <para>
/// <b>They do not all mean the same thing, and that is what this is for.</b> Asked properly,
/// `0x9D`'s byte counts 0, 1, 2 from nought in every run and `0x82`'s is <b>1 at all seven of its
/// places</b> — a constant, not an index. A shape that matters somewhere does not matter
/// everywhere (trap 11), and the way to find out is to ask each one separately with a floor
/// beside it.
/// </para>
/// </remarks>
public static class AByteThenAWord
{
    /// <summary>Where variable ids start, so a word can be told from a literal.</summary>
    public const int FirstVariable = 0x4000;

    /// <param name="At">Where the run starts.</param>
    /// <param name="Bytes">The first argument of each command in the run, in order.</param>
    /// <param name="Words">And the word each took.</param>
    public sealed record Run(int At, IReadOnlyList<int> Bytes, IReadOnlyList<int> Words)
    {
        /// <summary>
        /// The bytes are 0, 1, 2 … in order — what an index into a list of slots looks like.
        /// </summary>
        /// <remarks>
        /// EVERY element, not most: a run whose bytes are 0, 1, 3 is not counting and saying it
        /// is would make the answer out of the two that happened to line up. A run of one counts
        /// only if its byte is nought, which is the discrimination that separates <c>0x7F</c>
        /// (always 0) from <c>0x82</c> (always 1).
        /// </remarks>
        public bool CountsFromNought => Bytes.Select((b, i) => b == i).All(m => m);

        /// <summary>How many of the words are in the variable band rather than literals.</summary>
        public int Variables => Words.Count(w => w >= FirstVariable);
    }

    /// <summary>What one command's arguments look like across the whole scan.</summary>
    /// <param name="Code">The command.</param>
    /// <param name="Places">How many BYTE POSITIONS take this shape.</param>
    /// <param name="Runs">Consecutive stretches of it, each counted once however often it is read.</param>
    /// <param name="Counting">How many of those runs count from nought.</param>
    /// <param name="Alphabet">How many distinct values the first byte takes anywhere.</param>
    /// <param name="Variables">How many of the words are variable ids.</param>
    /// <param name="Words">How many DISTINCT words it takes — the ordinary argument-column test.</param>
    public sealed record Reading(
        byte Code, int Places, int Runs, int Counting, int Alphabet, int Variables, int Words)
    {
        /// <summary>Every run counts, which is what an index would do.</summary>
        public bool AlwaysCounts => Runs > 0 && Counting == Runs;

        /// <summary>
        /// Whether counting from nought can say anything here at all.
        /// </summary>
        /// <remarks>
        /// <b>A byte that only ever takes one value counts from nought whenever that value is
        /// nought, and that is not a finding.</b> <c>0x7F</c> is 0 at all three of its places:
        /// every run "counts", the floor is one in one, and the honest report is that the
        /// question cannot be answered here rather than that the answer is yes.
        /// </remarks>
        public bool CanSayAnything => Alphabet > 1;

        /// <summary>
        /// One in how many times chance would put every byte where it is, drawing each from the
        /// values this command actually uses.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The alphabet is the command's OWN distinct first bytes, which is the conservative
        /// choice: a byte that only ever takes three values is much likelier to land on 0, 1, 2
        /// by accident than one drawn from 256. Quoting the 256 version would be flattering the
        /// finding by a factor of millions.
        /// </para>
        /// <para>
        /// It is one in <c>A^places</c> and not <c>A^runs</c>, because every position has to be
        /// right, not every run.
        /// </para>
        /// </remarks>
        public double OneIn =>
            !AlwaysCounts || !CanSayAnything ? 1 : Math.Round(Math.Pow(Alphabet, Places));
    }

    /// <summary>
    /// The runs of one command inside one block, each keyed by where it starts.
    /// </summary>
    /// <remarks>
    /// Keyed by byte position because a block hanging off twenty doors is read twenty times, and
    /// twenty reads of one run is one run. 0x9D's five runs read as twenty-three without this.
    /// </remarks>
    public static void Gather(
        IReadOnlyList<ScriptCommand> block, byte code, IDictionary<int, Run> into)
    {
        for (var i = 0; i < block.Count; i++)
        {
            if (block[i].Code != code) continue;
            if (i > 0 && block[i - 1].Code == code) continue;

            List<int> bytes = [];
            List<int> words = [];

            for (int j = i; j < block.Count && block[j].Code == code; j++)
            {
                if (block[j].Arguments.Length < 3) break;

                bytes.Add(block[j].Arguments[0]);
                words.Add(block[j].Arguments[1] | (block[j].Arguments[2] << 8));
            }

            if (bytes.Count > 0) into[block[i].Offset] = new Run(block[i].Offset, bytes, words);
        }
    }

    /// <summary>The whole reading for one command, off its runs.</summary>
    public static Reading Of(byte code, IEnumerable<Run> runs)
    {
        Run[] all = [.. runs];

        return new Reading(
            code,
            all.Sum(r => r.Bytes.Count),
            all.Length,
            all.Count(r => r.CountsFromNought),
            all.SelectMany(r => r.Bytes).Distinct().Count(),
            all.Sum(r => r.Variables),
            all.SelectMany(r => r.Words).Distinct().Count());
    }
}
