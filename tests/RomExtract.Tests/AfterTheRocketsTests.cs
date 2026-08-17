using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a beaten trainer's script actually does.
/// <para>
/// The runner has always stopped at a <c>trainerbattle</c> and handed the fight to the
/// server. What it did when the trainer was <em>already</em> beaten went two ways, and both
/// of them were derived from this fixture.
/// </para>
/// <para>
/// First it carried straight on through the bytes after the command, and what it stopped
/// short of, in the ROCKET HIDEOUT, is the <c>clearflag</c> that puts the LIFT KEY on the
/// floor. So it was changed to jump to the last pointer inside the command that reads as a
/// script — and that was right about the LIFT KEY and wrong about everything else, because
/// that pointer is the <b>battle's</b> continuation. It runs when the fight is won, once.
/// Jumping there on every later pass ran the whole victory again every pass, and skipped the
/// bytes after the command for ever — where all eight gym leaders keep a <c>checkflag</c>
/// asking whether you already took the TM.
/// </para>
/// <para>
/// <b>This fixture could not tell those two apart, and that is why it agreed with the wrong
/// one.</b> Its bytes after the command are a line and an end, which is the harmless shape —
/// <c>--fights</c> counts 17 of 19 of this kind that way, and 8 of 8 of the gym leaders' kind
/// the other. <see cref="TheGuardAfterTheFightTests"/> is the same question asked with the
/// shape that discriminates. What is left here is the half this fixture does settle: where
/// the fight's own script is reported, and that it is not run before the fight.
/// </para>
/// </summary>
public class AfterTheRocketsTests
{
    private const byte TrainerBattle = 0x5C;
    private const byte LoadPointer = 0x0F;
    private const byte CallStandard = 0x09;
    private const byte ClearFlag = 0x2A;
    private const byte Release = 0x6C;
    private const byte End = 0x02;

    private const int Trainer = 0x0170;
    private const int Hidden = 0x0036;

    private const uint TheFightsOwnScript = 0x08000600;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static void Pointer(byte[] image, int at, uint address)
    {
        for (int i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>
    /// The hideout's shape: a fight, then a line and an end, and separately the script
    /// the fight points at — which is where the flag is cleared.
    /// </summary>
    private static byte[] Image()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, TrainerBattle, 2, Trainer & 0xFF, Trainer >> 8, 0x00, 0x00);
        Pointer(image, 0x106, 0x08000400);   // what they say on sight
        Pointer(image, 0x10A, 0x08000400);   // what they say when beaten
        Pointer(image, 0x10E, TheFightsOwnScript);

        // The bytes after the command: a line, and an end. THE HARMLESS SHAPE — nothing
        // here can tell a reading that runs it from one that does not, and saying so is
        // the whole reason this file still exists.
        Put(image, 0x112, LoadPointer, 0x00);
        Pointer(image, 0x114, 0x08000400);
        Put(image, 0x118, CallStandard, 0x04, Release, End);

        Put(image, 0x400, 0xFE, 0xFE, 0xFE, 0xFE);

        // The script the fight leads to, which is the one that matters.
        Put(image, 0x600, ClearFlag, Hidden & 0xFF, Hidden >> 8, Release, End);

        return image;
    }

    private static ScriptState Beaten()
    {
        // A save with the flag set, because that is what a new game does — and clearing
        // a flag nobody set is a no-op that reports nothing, which is how this looked
        // broken for an hour after it started working.
        var save = new ScriptState([Hidden]);

        save.MarkBeaten(Trainer);

        return save;
    }

    /// <summary>
    /// The fight's own script is reported rather than run. Whoever resolves the battle runs
    /// it, because only they know whether it was won — and the LIFT KEY is behind winning
    /// rather than behind having won at some point in the past.
    /// </summary>
    [Fact]
    public void TheScriptTheFightLeadsToIsHandedBack()
    {
        ScriptRun run = ScriptRunner.Run(new Rom(Image()), 0x08000100, new ScriptState([Hidden]));

        Assert.Equal(Trainer, run.TrainerId);
        Assert.Equal(TheFightsOwnScript, run.AfterTheFight);
    }

    /// <summary>
    /// And one who has not been beaten still stops at the fight. Running the aftermath
    /// before the fight would hand over the LIFT KEY for walking into the room.
    /// </summary>
    [Fact]
    public void OneWhoHasNotBeenBeatenStillStopsAtTheFight()
    {
        ScriptRun run = ScriptRunner.Run(new Rom(Image()), 0x08000100, new ScriptState([Hidden]));

        Assert.Empty(run.FlagsCleared);
        Assert.Equal(Trainer, run.TrainerId);
    }

    /// <summary>
    /// A beaten trainer does not run the victory again. The fight does nothing and the
    /// script carries on; the clearflag belongs to the win and has already happened.
    /// </summary>
    [Fact]
    public void ABeatenTrainerDoesNotRunTheVictoryAgain()
    {
        ScriptRun run = ScriptRunner.Run(new Rom(Image()), 0x08000100, Beaten());

        Assert.Empty(run.FlagsCleared);
        Assert.Null(run.TrainerId);
    }

    /// <summary>
    /// A fight with nothing but text after it reports no continuation at all, rather than
    /// reporting a page of text as one.
    /// </summary>
    [Fact]
    public void AFightThatLeadsNowhereReportsNoContinuation()
    {
        byte[] image = Image();

        // The third pointer at text as well, so nothing reads as a script.
        Pointer(image, 0x10E, 0x08000400);

        ScriptRun run = ScriptRunner.Run(new Rom(image), 0x08000100, new ScriptState([Hidden]));

        Assert.Equal(0u, run.AfterTheFight);
    }
}
