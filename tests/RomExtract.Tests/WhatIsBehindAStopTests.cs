using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// How much a stopped read is actually costing.
/// <para>
/// <b>The biggest number was the wrong number.</b> The playthrough's reading stops at
/// <c>0x73</c> three hundred and seventy-eight times, more than every other unknown command on
/// the cartridge put together — and at every one of its sites what follows is a release and an
/// end. Nothing is behind it. <c>0x9E</c> stopped three blocks, and one of the three sat eleven
/// bytes from the <c>call</c> that puts nineteen people on eleven maps.
/// </para>
/// <para>
/// Milestone 174 wrote the rule down after ranking people in doorways by how many a flag held:
/// <em>a count is not a ranking</em>. This is the same rule applied to the other list, and the
/// list had been ordered by count since it existed.
/// </para>
/// </summary>
public class WhatIsBehindAStopTests
{
    /// <summary>A byte this project has no width for, which is what a stop is.</summary>
    private const byte NoWidth = 0xEE;

    private const byte SetFlag = 0x29;
    private const byte Release = 0x6C;
    private const byte Message = 0x67;
    private const byte End = 0x02;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static Rom Image(int at, params byte[] behind)
    {
        var image = new byte[0x1000];

        // Fill with the unknown byte so that every width which is not deliberately made to
        // parse runs into something the reader cannot step over — which is what the rest of a
        // real cartridge looks like from the middle of a misread.
        Array.Fill(image, NoWidth);

        image[at] = NoWidth;

        Put(image, at + 1, behind);

        return new Rom(image);
    }

    /// <summary>The command with no width really has none, or this whole fixture is about nothing.</summary>
    [Fact]
    public void TheStandInForAnUnknownCommandIsActuallyUnknown() =>
        Assert.Null(ScriptCommands.ArgumentLength(NoWidth));

    /// <summary>
    /// A stop two bytes from the end of its block costs nothing, however many runs hit it.
    /// </summary>
    [Fact]
    public void AStopWithNothingButTheBlockEndingBehindItIsFree()
    {
        Behind behind = WhatIsBehindAStop.Of(Image(0x100, 0x00, 0x00, 0x00, 0x00, Release, End), 0x100);

        Assert.True(behind.NothingBehindIt);
        Assert.Empty(behind.Consequences);
    }

    /// <summary>
    /// And a stop with a <c>setflag</c> behind it is not free, whichever width turns out to be
    /// right — which is the point of trying them all rather than picking one.
    /// </summary>
    [Fact]
    public void AStopWithAFlagBehindItIsNotFree()
    {
        Behind behind = WhatIsBehindAStop.Of(
            Image(0x100, 0x00, 0x00, 0x00, 0x00, SetFlag, 0x55, 0x00, End), 0x100);

        Assert.False(behind.NothingBehindIt);
        Assert.Contains(SetFlag, behind.Consequences);
    }

    /// <summary>
    /// <b>Text is not a consequence.</b> Every stop hides something; only the ones hiding a
    /// change to the world have been making this project report a smaller one, and if a message
    /// counts then every stop is at the top of the list and the list says nothing.
    /// </summary>
    [Fact]
    public void SomethingSaidIsNotSomethingChanged()
    {
        Behind behind = WhatIsBehindAStop.Of(
            Image(0x100, 0x00, 0x00, 0x00, 0x00, Message, 0x00, 0x00, 0x00, 0x00, Release, End), 0x100);

        Assert.True(behind.NothingBehindIt);
    }

    /// <summary>
    /// <b>A width that dies is not a width that found nothing.</b> Counting one would let a
    /// width that reads two commands and runs into the next unknown byte report an empty
    /// verdict for the whole stop — which is the same "I did not look" dressed as "there is
    /// none" that this project keeps meeting.
    /// </summary>
    [Fact]
    public void OnlyAWidthThatReachesAProperEndCounts()
    {
        // At no width this reads a setflag and then runs into the unknown byte — some commands,
        // no end. At four it reads a single end and stops properly. Only the second is
        // evidence, and the setflag on the first must not reach the verdict: a width that died
        // mid-block has not seen what is behind this stop, it has moved the same question along
        // by a few bytes.
        Behind behind = WhatIsBehindAStop.Of(
            Image(0x100, SetFlag, 0x56, 0x00, NoWidth, End), 0x100);

        Assert.Equal(1, behind.WidthsThatParse);
        Assert.DoesNotContain(SetFlag, behind.Consequences);
        Assert.True(behind.NothingBehindIt);
    }

    /// <summary>
    /// And a stop no width reads on from is its own answer — probably not a command at all, but
    /// a misread that landed on a data byte. Reported apart from "nothing is behind it",
    /// because one of those is a stop worth ignoring and the other is a read already lost.
    /// </summary>
    [Fact]
    public void AStopNoWidthReadsOnFromIsNotAStopWithNothingBehindIt()
    {
        Behind behind = WhatIsBehindAStop.Of(Image(0x100), 0x100);

        Assert.Equal(0, behind.WidthsThatParse);
        Assert.False(behind.NothingBehindIt);
    }
}
