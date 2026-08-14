using PokeMmo.Core.Scripts;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a script does once the fight it started is over.
/// <para>
/// The sleeper on ROUTE 12 wakes, is fought, and vanishes — and the two lines the
/// cartridge has for that moment, "SNORLAX calmed down. It gave a huge yawn..." and
/// "And returned to the mountains.", were never said. The script read straight past its
/// own <c>dowildbattle</c> with the outcome still unasked, found nothing that matched,
/// and stopped.
/// </para>
/// </summary>
public class AfterTheWildFightTests
{
    private const byte SpecialVar = 0x26;
    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte SetFlag = 0x29;
    private const byte SetWild = 0xB6;
    private const byte DoWild = 0xB7;
    private const byte Return = 0x03;
    private const byte End = 0x02;
    private const byte LoadPointer = 0x0F;
    private const byte CallStd = 0x09;

    private const int Result = 0x800D;

    private static void Ask(byte[] data, ref int at, byte routine)
    {
        data[at++] = SpecialVar;
        data[at++] = unchecked((byte)Result);
        data[at++] = Result >> 8;
        data[at++] = routine;
        data[at++] = 0;
    }

    private static void Then(byte[] data, ref int at, int value, byte condition, uint target)
    {
        data[at++] = Compare;
        data[at++] = unchecked((byte)Result);
        data[at++] = Result >> 8;
        data[at++] = (byte)value;
        data[at++] = (byte)(value >> 8);

        data[at++] = GotoIf;
        data[at++] = condition;
        data[at++] = (byte)target;
        data[at++] = (byte)(target >> 8);
        data[at++] = (byte)(target >> 16);
        data[at++] = (byte)(target >> 24);
    }

    /// <summary>
    /// An image with the two shapes on it: creatures whose script only carries on when
    /// they have been caught, and creatures whose script asks three ways and only writes
    /// something down for one of them.
    /// </summary>
    private static Rom Image(int won = 1, int ran = 4, int walkedOff = 5, int caught = 7)
    {
        var data = new byte[0x4000];

        // Where the answers land. 0x2000 begins with a setflag — this is the one that
        // happened. 0x2100 does not. 0x2200 is a bare return.
        data[0x2000] = SetFlag;
        data[0x2001] = 0x50;
        data[0x2003] = End;

        data[0x2100] = End;
        data[0x2200] = Return;

        int at = 0x1000;

        // Nine of the "three ways" shape, because one site proves nothing.
        for (int i = 0; i < 9; i++)
        {
            Ask(data, ref at, routine: 0xB4);
            Then(data, ref at, won, condition: 1, target: Rom.BaseAddress + 0x2000);
            Then(data, ref at, ran, condition: 1, target: Rom.BaseAddress + 0x2100);
            Then(data, ref at, walkedOff, condition: 1, target: Rom.BaseAddress + 0x2100);
            data[at++] = End;
        }

        // And nine of the "only if it is yours" shape.
        for (int i = 0; i < 9; i++)
        {
            Ask(data, ref at, routine: 0xB4);
            Then(data, ref at, caught, condition: 5, target: Rom.BaseAddress + 0x2200);
            data[at++] = End;
        }

        // A yes-or-no, asked of a different routine and compared once. This is what makes
        // counting askers useless and counting the shape of the asking work.
        for (int i = 0; i < 200; i++)
        {
            Ask(data, ref at, routine: 0x39);
            Then(data, ref at, 1, condition: 1, target: Rom.BaseAddress + 0x2100);
            data[at++] = End;
        }

        return new Rom(data);
    }

    [Fact]
    public void TheOutcomeThatWritesSomethingDownIsTheOneThatWasWon()
    {
        BattleOutcomes outcomes = BattleOutcomeLocator.Locate(Image())!;

        Assert.Equal(1, outcomes.Won);
        Assert.Equal(7, outcomes.Caught);
        Assert.Equal(4, outcomes.Ran);
        Assert.Equal(18, outcomes.Sites);
    }

    /// <summary>
    /// The numbers are the cartridge's, not this project's. An image that numbered them
    /// differently has to read differently, or the reading was a memory dressed up.
    /// </summary>
    [Fact]
    public void AnImageThatNumbersThemDifferentlyReadsDifferently()
    {
        BattleOutcomes outcomes = BattleOutcomeLocator.Locate(Image(won: 6, ran: 2, walkedOff: 3, caught: 9))!;

        Assert.Equal(6, outcomes.Won);
        Assert.Equal(9, outcomes.Caught);
        Assert.Equal(2, outcomes.Ran);
    }

    [Fact]
    public void AnImageWithNoFightsOnItSaysNothing()
    {
        Assert.Null(BattleOutcomeLocator.Locate(new Rom(new byte[0x4000])));
    }

    /// <summary>
    /// A sleeper in miniature: set a creature up, take it off the map, fight it, and then
    /// say something about how it went.
    /// </summary>
    private static Rom Sleeper()
    {
        var data = new byte[0x4000];

        int at = 0x1000;

        data[at++] = SetWild;
        data[at++] = 0x8F;              // SNORLAX
        data[at++] = 0x00;
        data[at++] = 30;
        data[at++] = 0x00;
        data[at++] = 0x00;

        data[at++] = SetFlag;           // off the map, before the fight, as the games do
        data[at++] = 0x54;
        data[at++] = 0x00;

        data[at++] = DoWild;

        int after = at;

        Ask(data, ref at, routine: 0xB4);
        Then(data, ref at, 1, condition: 1, target: Rom.BaseAddress + 0x2000);
        data[at++] = End;

        // "It returned to the mountains."
        data[0x2000] = LoadPointer;
        data[0x2001] = 0x00;
        data[0x2002] = 0x00;
        data[0x2003] = 0x30;
        data[0x2004] = 0x00;
        data[0x2005] = 0x08;
        data[0x2006] = CallStd;
        data[0x2007] = 0x04;
        data[0x2008] = End;

        // The words themselves. Long enough to read as a sentence rather than as two
        // stray bytes, because the reader will not take a pointer at its word — a
        // "message" of two letters is how a wrong width looks, and it says so.
        for (int i = 0; i < 12; i++) data[0x3000 + i] = (byte)(0xBB + i);

        data[0x300C] = 0xFF;

        Assert.Equal(0x100A, after);

        return new Rom(data);
    }

    [Fact]
    public void AScriptStopsForItsOwnFightAndSaysWhereItStopped()
    {
        ScriptRun run = ScriptRunner.Run(Sleeper(), 0x08001000);

        Assert.Equal((0x8F, 30), run.WildBattle);

        // Everything before the fight has happened — the creature is off the map — and
        // nothing after it has.
        Assert.Equal([0x54], run.FlagsSet);
        Assert.Empty(run.Pages);

        Assert.Equal(Rom.BaseAddress + 0x100A, run.ResumesAfterTheFight);
    }

    [Fact]
    public void PickedUpWithTheOutcomeWrittenDownItSaysTheWords()
    {
        Rom rom = Sleeper();

        ScriptRun run = ScriptRunner.Run(rom, 0x08001000);

        var state = new ScriptState();
        state.Write(Result, BattleOutcomeLocator.Locate(Image())!.Won);

        ScriptRun rest = ScriptRunner.Run(rom, run.ResumesAfterTheFight!.Value, state);

        Assert.Single(rest.Pages);
    }

    /// <summary>
    /// And the counterpart in the other direction: a fight walked away from lands on the
    /// same words here, and a fight nobody answered for lands on none.
    /// </summary>
    [Fact]
    public void WithNothingWrittenDownItSaysNothing()
    {
        Rom rom = Sleeper();

        ScriptRun run = ScriptRunner.Run(rom, 0x08001000);
        ScriptRun rest = ScriptRunner.Run(rom, run.ResumesAfterTheFight!.Value);

        Assert.Empty(rest.Pages);
    }
}
