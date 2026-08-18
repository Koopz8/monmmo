using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Whether the game's own code holds a variable's number, told apart from four bytes that
/// happen to equal it.
/// <para>
/// <b>A script names a variable in an operand; compiled code cannot.</b> A sixteen-bit constant
/// does not fit in a THUMB instruction, so the compiler puts it in a four-byte-aligned literal
/// pool and loads it PC-relative. That is the only shape available to a routine that wants to
/// read <c>0x4026</c>, and it is the only handle this project has on the question "is this
/// variable dead, or does the compiled game read it by address?"
/// </para>
/// <para>
/// <b>The aligned word ALONE is a weak filter and the cartridge says so.</b> Over the ninety
/// variables the map scan writes, forty-one have an aligned word somewhere and the REVERSED image
/// gives twenty-seven — the same order of number, which is the shape 245 threw an aggregate away
/// for. Requiring an instruction that reaches the word takes it to twenty-nine against four.
/// </para>
/// </summary>
public sealed class HeldByTheGamesOwnCodeTests
{
    private const int Variable = 0x4026;

    /// <summary>The four bytes a literal pool entry for <see cref="Variable"/> is made of.</summary>
    private static byte[] Word() => [Variable & 0xFF, (Variable >> 8) & 0xFF, 0, 0];

    /// <summary>
    /// <c>ldr rX, [pc, #imm]</c> — five fixed bits, three of register, eight of offset. The
    /// address it reaches is <c>align4(here + 4) + imm * 4</c>.
    /// </summary>
    private static byte[] Load(int register, int words)
    {
        int instruction = 0x4800 | (register << 8) | words;

        return [(byte)instruction, (byte)(instruction >> 8)];
    }

    /// <summary>An image of nothing, with pieces put where the test wants them.</summary>
    private static Rom Image(params (int At, byte[] Bytes)[] pieces)
    {
        var data = new byte[0x1000];

        foreach ((int at, byte[] bytes) in pieces) bytes.CopyTo(data, at);

        return new Rom(data);
    }

    /// <summary>The offset a load at <paramref name="at"/> with this offset reaches.</summary>
    private static int Reaches(int at, int words) => ((at + 4) & ~3) + (words * 4);

    // ------------------------------------------------------------------ what is held

    /// <summary>
    /// THE THING: an aligned word an instruction loads is the game's code holding that number.
    /// </summary>
    [Fact]
    public void AnAlignedWordAnInstructionLoadsIsHeldByCode()
    {
        const int at = 0x200;
        const int words = 10;

        int literal = Reaches(at, words);

        IReadOnlyList<WordSite> found = EverywhereInTheImage.HeldAsAWord(
            Image((at, Load(0, words)), (literal, Word())), Variable);

        WordSite one = Assert.Single(found);

        Assert.True(one.HeldByCode);
        Assert.Equal((literal, at), (one.Offset, one.LoadedFrom));
    }

    /// <summary>
    /// AND THE DISCRIMINATION THE WHOLE INSTRUMENT RESTS ON: four bytes nothing loads are not.
    /// </summary>
    /// <remarks>
    /// Without the instruction this test's image and the one above are the same measurement, and
    /// on the cartridge that difference is 41-against-27 becoming 29-against-4.
    /// </remarks>
    [Fact]
    public void AnAlignedWordNothingLoadsIsNotHeldByCode()
    {
        IReadOnlyList<WordSite> found = EverywhereInTheImage.HeldAsAWord(
            Image((0x240, Word())), Variable);

        WordSite one = Assert.Single(found);

        Assert.False(one.HeldByCode);
        Assert.Null(one.LoadedFrom);
    }

    /// <summary>
    /// And the load has to reach THIS word — an instruction of the right shape pointing four
    /// bytes past it is not a load of it.
    /// </summary>
    /// <remarks>
    /// The arithmetic is the filter. Five fixed bits recur constantly in sixteen megabytes; five
    /// fixed bits whose eight-bit offset lands on exactly this address are 2.4% of aligned words.
    /// A rule that accepted any nearby <c>ldr</c> would be back at the weak version with more
    /// steps.
    /// </remarks>
    [Fact]
    public void ALoadThatReachesTheNextWordIsNotALoadOfThisOne()
    {
        const int at = 0x200;
        const int words = 10;

        IReadOnlyList<WordSite> found = EverywhereInTheImage.HeldAsAWord(
            Image((at, Load(0, words + 1)), (Reaches(at, words), Word())), Variable);

        Assert.False(Assert.Single(found).HeldByCode);
    }

    // ----------------------------------------------------------- and what is not a word

    /// <summary>
    /// An occurrence off a four-byte boundary is not a literal pool entry, and is not found.
    /// </summary>
    /// <remarks>
    /// <c>setvar 0x4026, 0</c> is the five bytes <c>16 26 40 00 00</c>, so the word
    /// <c>0x00004026</c> falls out of it whenever the command lands one byte before a boundary —
    /// which is what happens at <c>0x165220</c> on this cartridge. An unaligned scan finds that
    /// every time a script writes the variable, which is every time, and a pool entry is the one
    /// thing guaranteed aligned.
    /// </remarks>
    [Fact]
    public void AnOccurrenceOffAFourByteBoundaryIsNotAWord()
    {
        Assert.Empty(EverywhereInTheImage.HeldAsAWord(Image((0x241, Word())), Variable));
    }

    /// <summary>
    /// And the shape that actually produces one: a <c>setvar</c> of this very variable, whose own
    /// five bytes contain the word one byte off the boundary.
    /// </summary>
    /// <remarks>
    /// The fixture is the cartridge's, not an invented offset. A scan that dropped the alignment
    /// would find the number in every script that writes it, which is every script this list is
    /// about — the instrument would report each of the twelve as held by code, by reading the
    /// evidence that it is written.
    /// </remarks>
    [Fact]
    public void AScriptWritingTheVariableDoesNotMakeItHeldByCode()
    {
        // setvar 0x4026, 0 — the word 0x00004026 falls at 0x201, which is not a boundary.
        byte[] setvar = [0x16, Variable & 0xFF, (Variable >> 8) & 0xFF, 0, 0];

        Assert.Empty(EverywhereInTheImage.HeldAsAWord(Image((0x200, setvar)), Variable));
    }

    /// <summary>
    /// And a word the map scan decoded is somebody's operand, not compiled code — even with an
    /// instruction reaching it.
    /// </summary>
    [Fact]
    public void AWordInsideAScriptsOwnOperandIsNotHeldByCode()
    {
        const int at = 0x200;
        const int words = 10;

        int literal = Reaches(at, words);

        Rom rom = Image((at, Load(0, words)), (literal, Word()));

        var covered = new int[rom.Length];

        Array.Fill(covered, EverywhereInTheImage.Nobody);

        covered[literal] = 0;

        WordSite one = Assert.Single(EverywhereInTheImage.HeldAsAWord(rom, Variable, covered));

        Assert.True(one.Opened);
        Assert.False(one.HeldByCode);
        Assert.NotNull(one.LoadedFrom);
    }

    /// <summary>
    /// And asking for many numbers at once answers each of them separately — the denominator is
    /// ninety variables and ninety passes of sixteen megabytes is ninety times the work.
    /// </summary>
    [Fact]
    public void ManyNumbersAtOnceAnswerSeparately()
    {
        Rom rom = Image((0x240, Word()), (0x250, [0x50, 0x40, 0, 0]));

        IReadOnlyDictionary<int, IReadOnlyList<WordSite>> found =
            EverywhereInTheImage.HeldAsAWord(rom, [Variable, 0x4050, 0x4051]);

        Assert.Equal(0x240, Assert.Single(found[Variable]).Offset);
        Assert.Equal(0x250, Assert.Single(found[0x4050]).Offset);
        Assert.Empty(found[0x4051]);
    }
}
