using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A <c>callstd</c> is a call whose address is not in the command — it is an entry in a table
/// this project has never located, and that is why twelve sites came back <b>not said</b> at 221.
/// <para>
/// <b>The table was hunted by shape and the hunt failed.</b> Runs of ten or more consecutive
/// pointers all landing on something that reads as a script: twenty-four in the image, nought in
/// the same file reversed. So the shape is real and it does not identify anything, because the
/// filter every sweep in this project uses accepts a pointer to <c>nop ; end</c> — and most of
/// the candidates are exactly that.
/// </para>
/// <para>
/// <b>The question was answered from the callers instead.</b> If a script says
/// <c>callstd N ; compare 0x800D ; if</c> and nothing before it could have put anything in that
/// variable, then the compare is reading what <c>N</c> left, whatever <c>N</c> is. On this
/// cartridge <c>callstd 0x05</c> has 152 such sites and <c>callstd 0x00</c> has two.
/// </para>
/// <para>
/// Not circular: the verdict is derived only from sites where nothing else could have answered,
/// and applied to sites where something else could.
/// </para>
/// </summary>
public sealed class TheRoutinesReachedByNumberTests
{
    private const byte Filler = 0x77;
    private const byte End = 0x02;
    private const byte Special = 0x25;
    private const byte CallStandard = 0x09;
    private const byte LoadPointer = 0x0F;
    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte Lock = 0x6A;
    private const byte Nop = 0x00;
    private const byte Return = 0x03;

    private static byte[] Blank()
    {
        var image = new byte[0x20000];

        Array.Fill(image, Filler);

        return image;
    }

    private static void Put(byte[] image, int at, params int[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++) image[at + i] = (byte)bytes[i];
    }

    private static void Address(byte[] image, int at, int offset)
    {
        uint address = Rom.BaseAddress + (uint)offset;

        for (var i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>
    /// THE VERDICT IS ONE SITE. A compare has to be reading something, and where nothing else
    /// could have written the variable there is only one candidate left.
    /// </summary>
    [Fact]
    public void OneSiteWithNothingBeforeItIsEnoughToSayAStandardRoutineAnswers()
    {
        var answers = new StandardRoutines.Answers(5, 400, 100, NothingBefore: 1, 200, 199);

        Assert.True(answers.MustAnswer);

        // And a routine every one of whose sites had something else in front of it says nothing
        // at all — however many sites there are.
        Assert.False(new StandardRoutines.Answers(6, 400, 100, 0, 200, 200).MustAnswer);
    }

    /// <summary>
    /// The walk back is 219's, and this is what it is being asked here: a <c>lock</c> and a
    /// <c>loadpointer</c> cannot have answered, so the compare after the <c>callstd</c> is the
    /// standard routine's.
    /// </summary>
    [Fact]
    public void ALockAndSomeTextInFrontOfItAreNotAnAnswer()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Lock);
        Put(image, 0x1001, LoadPointer, 0x00);
        Address(image, 0x1003, 0x1400);
        Put(image, 0x1007, CallStandard, 0x05);
        Put(image, 0x1009, Compare, 0x0D, 0x80, 0x01, 0x00);
        Put(image, 0x100E, GotoIf, 0x01);
        Address(image, 0x1010, 0x1000);
        Put(image, 0x1014, End);

        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        Assert.Equal(
            SpecialCalls.LeftBehind.Nothing,
            SpecialCalls.WhatAnsweredBefore(commands, 2).Left);

        Assert.True(StandardRoutines.ProvesItAnswers(commands, 2));
    }

    /// <summary>
    /// WHICH SITES CAN SAY ANYTHING AT ALL: a compare straight after, and something branching on
    /// it. A compare nothing branches on changes no path, and counting it would be counting a
    /// site that cannot speak as a site that said nothing.
    /// </summary>
    [Fact]
    public void ACompareNothingBranchesOnIsNotASiteThatSaysAnything()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Lock);
        Put(image, 0x1001, CallStandard, 0x05);
        Put(image, 0x1003, Compare, 0x0D, 0x80, 0x01, 0x00);
        Put(image, 0x1008, End);

        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        Assert.False(StandardRoutines.AsksTheQuestionHere(commands, 1));
        Assert.False(StandardRoutines.ProvesItAnswers(commands, 1));

        // And with a branch after it, the same bytes do say something.
        Put(image, 0x1008, GotoIf, 0x01);
        Address(image, 0x100A, 0x1000);
        Put(image, 0x100E, End);

        List<ScriptCommand> branching = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        Assert.True(StandardRoutines.AsksTheQuestionHere(branching, 1));
        Assert.True(StandardRoutines.ProvesItAnswers(branching, 1));
    }

    /// <summary>
    /// AND A ROUTINE IN FRONT OF IT is an answer, so that site says nothing about the standard
    /// routine — which is the case the cartridge's twelve unresolved sites are, and exactly the
    /// case the verdict must not be derived from.
    /// </summary>
    [Fact]
    public void ARoutineInFrontOfItMeansThatSiteProvesNothing()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x88, 0x01);
        Put(image, 0x1003, LoadPointer, 0x00);
        Address(image, 0x1005, 0x1400);
        Put(image, 0x1009, CallStandard, 0x05);
        Put(image, 0x100B, Compare, 0x0D, 0x80, 0x00, 0x00);
        Put(image, 0x1010, GotoIf, 0x01);
        Address(image, 0x1012, 0x1000);
        Put(image, 0x1016, End);

        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        Assert.Equal(
            SpecialCalls.LeftBehind.ARoutine,
            SpecialCalls.WhatAnsweredBefore(commands, 2).Left);

        // It asks the question and answers it the other way: this site proves nothing.
        Assert.True(StandardRoutines.AsksTheQuestionHere(commands, 2));
        Assert.False(StandardRoutines.ProvesItAnswers(commands, 2));
    }

    /// <summary>
    /// AND THE PAYOFF: at those sites, a standard routine KNOWN to answer takes the compare —
    /// and one nothing is known about does not.
    /// <para>
    /// This is the whole of 222 in one assertion. The same bytes, the same barrier, and the
    /// verdict turns on whether the callers pinned that number down elsewhere in the file.
    /// </para>
    /// </summary>
    [Fact]
    public void AStandardRoutineKnownToAnswerTakesTheCompareAndAnUnknownOneDoesNot()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x88, 0x01);
        Put(image, 0x1003, CallStandard, 0x05);
        Put(image, 0x1005, Compare, 0x0D, 0x80, 0x00, 0x00);
        Put(image, 0x100A, GotoIf, 0x01);
        Address(image, 0x100C, 0x1000);
        Put(image, 0x1010, End);

        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        (WhoTheCompareBelongsTo.InTheWay known, uint number) =
            Assert.NotNull(WhoTheCompareBelongsTo.WhatStoodInTheWay(
                new Rom(image), commands, 0, new HashSet<int> { 5 }));

        Assert.Equal(WhoTheCompareBelongsTo.InTheWay.AStandardRoutineThatAnswers, known);
        Assert.Equal(5u, number);
        Assert.Equal(WhoTheCompareBelongsTo.Whose.SomebodyElses, WhoTheCompareBelongsTo.Belongs(known));

        // The same site, with nothing known about number five.
        (WhoTheCompareBelongsTo.InTheWay unknown, uint _) =
            Assert.NotNull(WhoTheCompareBelongsTo.WhatStoodInTheWay(new Rom(image), commands, 0));

        Assert.Equal(WhoTheCompareBelongsTo.InTheWay.AStandardRoutine, unknown);
        Assert.Equal(WhoTheCompareBelongsTo.Whose.NotSaid, WhoTheCompareBelongsTo.Belongs(unknown));

        // And a DIFFERENT number is not covered by what was learned about five.
        Put(image, 0x1004, 0x06);

        List<ScriptCommand> six = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        (WhoTheCompareBelongsTo.InTheWay other, uint _) =
            Assert.NotNull(WhoTheCompareBelongsTo.WhatStoodInTheWay(
                new Rom(image), six, 0, new HashSet<int> { 5 }));

        Assert.Equal(WhoTheCompareBelongsTo.InTheWay.AStandardRoutine, other);
    }

    /// <summary>
    /// THE FILTER THE TABLE HUNT FOUNDERED ON, asserted rather than described: a pointer to
    /// <c>nop ; end</c> reads as a script.
    /// <para>
    /// Two bytes of nothing pass the test every sweep in this project uses, which is why
    /// twenty-four runs of ten such pointers exist in sixteen megabytes and none of them can be
    /// told from the table. The filter is weak on purpose and this is the shape of how weak.
    /// </para>
    /// </summary>
    [Fact]
    public void APointerToNothingReadsAsAScript()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Nop, End);

        Assert.True(ScriptReader.ReadsAsAScript(new Rom(image), Rom.BaseAddress + 0x1000));

        // And filler does not, which is what makes it a filter at all rather than a yes-machine.
        Assert.False(ScriptReader.ReadsAsAScript(new Rom(image), Rom.BaseAddress + 0x2000));
    }

    /// <summary>
    /// The table sweep reports MAXIMAL runs. A run of twenty holds eleven runs of ten, and
    /// counting those separately would turn one candidate into eleven — including in the
    /// reversed-image count, which is the only thing that says whether the shape means anything.
    /// </summary>
    [Fact]
    public void ARunOfPointersIsCountedOnceAndNotOncePerWindow()
    {
        byte[] image = Blank();

        Put(image, 0x1500, Nop, End);

        // Twelve pointers in a row, all to the same two harmless bytes.
        for (var i = 0; i < 12; i++) Address(image, 0x1000 + (i * 4), 0x1500);

        StandardRoutines.ATable table = Assert.Single(StandardRoutines.Tables(new Rom(image), 10));

        Assert.Equal(12, table.Entries);
        Assert.Equal(Rom.BaseAddress + 0x1000, table.At);

        // And a run one short of what is asked for is not a candidate at all.
        Assert.Empty(StandardRoutines.Tables(new Rom(image), 13));
    }

    /// <summary>
    /// AND THE FLOOR RUNS ON THE SAME SWEEP. A fixture with one run forwards has none backwards,
    /// which is what the real image reported — and if the floor could never come back empty the
    /// number beside it would mean nothing.
    /// </summary>
    [Fact]
    public void TheReversedImageIsTheSameSweepAndCanComeBackEmpty()
    {
        byte[] image = Blank();

        Put(image, 0x1500, Nop, End);

        for (var i = 0; i < 12; i++) Address(image, 0x1000 + (i * 4), 0x1500);

        Assert.Empty(StandardRoutines.NoiseFloor(new Rom(image), 10));
    }

    /// <summary>
    /// What an entry leaves is read with the same instrument that reads any other called block —
    /// so a standard routine, once its table is found, needs no new reading.
    /// </summary>
    [Fact]
    public void AnEntryIsReadWithTheSameInstrumentAsAnyOtherCalledBlock()
    {
        byte[] image = Blank();

        Address(image, 0x1000, 0x1200);

        Put(image, 0x1200, Special, 0x5D, 0x00);
        Put(image, 0x1203, Return);

        var table = new StandardRoutines.ATable(Rom.BaseAddress + 0x1000, 1, [Rom.BaseAddress + 0x1200]);

        (int index, uint at, SpecialCalls.LeftBehind left, int who) =
            Assert.Single(StandardRoutines.WhatTheyLeave(new Rom(image), table));

        Assert.Equal(0, index);
        Assert.Equal(Rom.BaseAddress + 0x1200, at);
        Assert.Equal(SpecialCalls.LeftBehind.ARoutine, left);
        Assert.Equal(0x5D, who);
    }
}
