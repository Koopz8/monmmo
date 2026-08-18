using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A map's arrival script is not a script the map runs — it is a script the map runs <b>when a
/// variable holds a particular value</b>. 227 and 228 found these scripts asking eleven routines
/// nothing else asks and moving eleven flags nothing else moves; this asks which of them can run.
/// <para>
/// <b>The condition is a variable AND a value, and the second half is the whole instrument.</b>
/// Every one of this cartridge's 69 distinct arrival conditions names a variable something
/// writes — nought name one nothing writes. Ask the fuller question and 28 of the 69 are waiting
/// on a value no <c>setvar</c> in the scan ever produces.
/// </para>
/// <para>
/// On this cartridge that is mostly one variable: <c>0x406F</c> is wanted at 1, 2, 3, 5, 6, 7 and
/// 8 by twenty maps, and the only thing in the scan that writes it writes <b>nought</b>, at three
/// places.
/// </para>
/// </summary>
public sealed class WaitingOnANumberNobodyWritesTests
{
    private static WhenAMapRunsSomething.Arrival For(
        int variable, int wanted, params (int Value, int Places)[] written) =>
        WhenAMapRunsSomething.For(
            "3.10",
            new MapEntryScript(variable, wanted, 0x08160000),
            written.ToDictionary(w => w.Value, w => w.Places));

    /// <summary>
    /// THE DISCRIMINATION: the same variable, written, and this value not among what is written.
    /// A reading that asked only whether the variable is written calls this satisfiable.
    /// </summary>
    [Fact]
    public void AVariableSomethingWritesIsNotAVariableSomethingWritesThisValueTo()
    {
        WhenAMapRunsSomething.Arrival waiting = For(0x406F, 5, (0, 3));

        Assert.False(waiting.NothingWritesIt);
        Assert.True(waiting.NobodyWritesThisValue);
        Assert.Equal(3, waiting.Written);
        Assert.Equal(0, waiting.WrittenWithThis);
    }

    /// <summary>
    /// And the same variable with the wanted value among what is written is satisfiable — the
    /// answer that has to be possible, or the instrument is a machine for saying no.
    /// </summary>
    [Fact]
    public void AConditionWhoseValueSomebodyWritesIsSatisfiable()
    {
        WhenAMapRunsSomething.Arrival can = For(0x4001, 1, (0, 90), (1, 70));

        Assert.False(can.NothingWritesIt);
        Assert.False(can.NobodyWritesThisValue);
        Assert.Equal(70, can.WrittenWithThis);
    }

    /// <summary>
    /// A VARIABLE NOTHING WRITES IS ITS OWN ANSWER, not a third spelling of the one above. On
    /// this cartridge the bucket is empty, which is a fact about it and only says anything
    /// because the bucket exists.
    /// </summary>
    [Fact]
    public void AVariableNothingWritesAtAllIsADifferentAnswer()
    {
        WhenAMapRunsSomething.Arrival nothing = For(0x40FF, 1);

        Assert.True(nothing.NothingWritesIt);
        Assert.False(nothing.NobodyWritesThisValue);

        // Three answers, and the two "no" ones are not the same answer.
        Assert.NotEqual(nothing.NothingWritesIt, For(0x406F, 5, (0, 3)).NothingWritesIt);
    }

    /// <summary>
    /// The values are carried, because "written NOWHERE" and "written, but only ever nought" are
    /// what a reader needs to tell a code boundary from a story counter that has not got there.
    /// </summary>
    [Fact]
    public void WhatIsActuallyWrittenIsCarriedBesideTheVerdict()
    {
        Assert.Equal(
            new Dictionary<int, int> { [0] = 3 },
            For(0x406F, 5, (0, 3)).Values);

        Assert.Empty(For(0x40FF, 1).Values);
    }

    /// <summary>
    /// AND THE WRITERS ARE COUNTED IN PLACES. One address that nineteen maps' scripts run through
    /// is one place that writes, not nineteen — the fault 220 and 223 spent two milestones on,
    /// which this instrument would have walked into for the seventh time.
    /// </summary>
    [Fact]
    public void OneAddressWrittenThroughNineteenTimesIsOnePlace()
    {
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> tally = WhenAMapRunsSomething.Tally(
            [(0x406F, 0, 0x1BB1CA), (0x406F, 0, 0x1BB1CA), (0x406F, 0, 0x162526)]);

        Assert.Equal(2, tally[0x406F][0]);

        // And the values are kept apart, because the whole question is which value.
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> both = WhenAMapRunsSomething.Tally(
            [(0x4055, 1, 0x1000), (0x4055, 2, 0x2000)]);

        Assert.Equal(new[] { 1, 2 }, both[0x4055].Keys.Order());
        Assert.Equal(1, both[0x4055][1]);
    }

    /// <summary>
    /// ONLY THE WRITE WHOSE VALUE IS IN THE COMMAND. A <c>copyvar</c> or an <c>addvar</c> puts
    /// something in a variable too and what it puts there is not in the bytes, so a condition
    /// satisfied only by one of those reads as satisfied by nothing.
    /// <para>
    /// That is the direction this is allowed to be wrong in — it overstates how much is behind
    /// the boundary rather than understating it — and it is asserted rather than left implied.
    /// </para>
    /// </summary>
    [Fact]
    public void OnlyASetVarSaysWhatValueItWrites()
    {
        Assert.Equal(
            (0x4055, 2),
            WhenAMapRunsSomething.WhatIsSet(new ScriptCommand(0, 0x16, [0x55, 0x40, 0x02, 0x00])));

        // copyvar, addvar, and something that is not a write at all.
        Assert.Null(WhenAMapRunsSomething.WhatIsSet(new ScriptCommand(0, 0x19, [0x55, 0x40, 0x56, 0x40])));
        Assert.Null(WhenAMapRunsSomething.WhatIsSet(new ScriptCommand(0, 0x17, [0x55, 0x40, 0x01, 0x00])));
        Assert.Null(WhenAMapRunsSomething.WhatIsSet(new ScriptCommand(0, 0x29, [0x70, 0x00])));

        // And a setvar too short to hold both halves says nothing.
        Assert.Null(WhenAMapRunsSomething.WhatIsSet(new ScriptCommand(0, 0x16, [0x55, 0x40])));
    }
}
