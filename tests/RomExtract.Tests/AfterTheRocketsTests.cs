using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a beaten trainer's script actually does.
/// <para>
/// The runner has always stopped at a <c>trainerbattle</c> and handed the fight to the
/// server. What it did when the trainer was <em>already</em> beaten was carry straight on
/// through the bytes that follow the command — which are usually a line and an end, so it
/// read as working: the trainer said their second line and the script stopped.
/// </para>
/// <para>
/// What it stopped short of, in the ROCKET HIDEOUT, is the <c>clearflag</c> that puts the
/// LIFT KEY on the floor. The script that runs is not the bytes after the command; it is
/// the one the command points at, and the last pointer that reads as a script is the one.
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
        Pointer(image, 0x10E, 0x08000600);   // and the script it leads to

        // The bytes after the command: a line, and an end. This is the path the runner
        // used to take, and it does nothing.
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

    [Fact]
    public void ABeatenTrainerRunsTheScriptTheFightLeadsTo()
    {
        ScriptRun run = ScriptRunner.Run(new Rom(Image()), 0x08000100, Beaten());

        Assert.Contains(Hidden, run.FlagsCleared);
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
    /// A fight with nothing but text after it falls through as it always did. The change
    /// is which script runs when there is one, not that one is invented.
    /// </summary>
    [Fact]
    public void AFightThatLeadsNowhereCarriesOnAsBefore()
    {
        byte[] image = Image();

        // The third pointer at text as well, so nothing reads as a script.
        Pointer(image, 0x10E, 0x08000400);

        ScriptRun run = ScriptRunner.Run(new Rom(image), 0x08000100, Beaten());

        Assert.Empty(run.FlagsCleared);
    }
}
