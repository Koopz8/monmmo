using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The bytes a beaten trainer carries on with, and the guard that is in them.
/// <para>
/// Every one of this cartridge's eight gym leaders is <c>trainerbattle</c> kind 1, and every
/// one of the eight has the same three commands immediately after it: <c>checkflag</c>, a
/// branch, and the line they say when there is nothing left to give. The branch leads to the
/// TM. <c>--fights</c> reads both exits of all 729 <c>trainerbattle</c> sites in the image and
/// finds 8 of 8 of kind 1 and 2 of 19 of kind 2 with a conditional in the fall-through that
/// the fight's own script never arrives at, and nothing anywhere else in the file naming those
/// addresses. A cartridge does not put a guard where nothing can reach it.
/// </para>
/// <para>
/// <b>This fixture is built on that shape and not on the harmless one.</b>
/// <see cref="AfterTheRocketsTests"/> puts a line and an end after the command, which is a
/// shape both readings agree about — so it passed under the reading that skipped the guard,
/// and the eight TMs were handed over once per pass for ever underneath it.
/// </para>
/// </summary>
public class TheGuardAfterTheFightTests
{
    private const byte TrainerBattle = 0x5C;
    private const byte LoadPointer = 0x0F;
    private const byte CallStandard = 0x09;
    private const byte CheckFlag = 0x2B;
    private const byte GotoIf = 0x06;
    private const byte SetFlag = 0x29;
    private const byte AddItem = 0x44;
    private const byte Release = 0x6C;
    private const byte End = 0x02;

    /// <summary>Zero is Less, which is what a clear flag compares as. "If they have not yet."</summary>
    private const byte IfClear = 0x00;

    private const int Trainer = 0x019E;
    private const int Reward = 0x0147;
    private const int TakenIt = 0x0254;
    private const int Badge = 0x002E;

    private const uint TheFightsOwnScript = 0x08000600;
    private const uint TheGiveArm = 0x08000700;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static void Pointer(byte[] image, int at, uint address)
    {
        for (int i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>
    /// A gym leader, as this cartridge writes one: kind 1, three pointers, and a guarded
    /// hand-over in the bytes after the command.
    /// </summary>
    private static byte[] Image()
    {
        var image = new byte[0x2000];

        // 18 bytes: kind, id, one more word, and three pointers.
        Put(image, 0x100, TrainerBattle, 1, Trainer & 0xFF, Trainer >> 8, 0x00, 0x00);
        Pointer(image, 0x106, 0x08000400);   // what they say on sight
        Pointer(image, 0x10A, 0x08000400);   // what they say when beaten
        Pointer(image, 0x10E, TheFightsOwnScript);

        // THE FALL-THROUGH, which is the whole question: have you taken it yet?
        Put(image, 0x112, CheckFlag, TakenIt & 0xFF, TakenIt >> 8);
        Put(image, 0x115, GotoIf, IfClear);
        Pointer(image, 0x117, TheGiveArm);
        Put(image, 0x11B, LoadPointer, 0x00);
        Pointer(image, 0x11D, 0x08000400);
        Put(image, 0x121, CallStandard, 0x04, Release, End);

        Put(image, 0x400, 0xFE, 0xFE, 0xFE, 0xFE);

        // The battle's own continuation: the badge. It belongs to winning.
        Put(image, 0x600, SetFlag, Badge & 0xFF, Badge >> 8, Release, End);

        // And the arm behind the guard: the reward, and the flag that says it is gone.
        Put(image, 0x700, AddItem, Reward & 0xFF, Reward >> 8, 0x01, 0x00);
        Put(image, 0x705, SetFlag, TakenIt & 0xFF, TakenIt >> 8, Release, End);

        return image;
    }

    private static ScriptRun Run(params int[] flags)
    {
        var save = new ScriptState(flags);

        save.MarkBeaten(Trainer);

        return ScriptRunner.Run(new Rom(Image()), 0x08000100, save);
    }

    /// <summary>
    /// Beaten, and the reward not yet taken: the guard is reached, its arm is taken, and the
    /// reward is handed over once — with the flag that will stop it happening again.
    /// </summary>
    [Fact]
    public void ABeatenTrainerReachesTheGuardAndHandsTheRewardOver()
    {
        ScriptRun run = Run();

        Assert.Equal(Reward, run.GivesItem);
        Assert.Contains(TakenIt, run.FlagsSet);
    }

    /// <summary>
    /// AND THE DISCRIMINATION THIS FILE EXISTS FOR. Beaten, and the reward already taken:
    /// nothing is handed over. Under the reading that jumped into the fight's own script
    /// these bytes were never reached, so this answer was the same as the one above — and
    /// every gym leader in the game handed their TM over again on every pass.
    /// </summary>
    [Fact]
    public void AndDoesNotHandItOverASecondTime()
    {
        ScriptRun run = Run(TakenIt);

        Assert.Null(run.GivesItem);
    }

    /// <summary>
    /// The badge belongs to the battle and not to having fought one. A beaten trainer's
    /// script does not set it again — which is the same claim as above, made about the arm
    /// that has no guard on it at all.
    /// </summary>
    [Fact]
    public void TheVictoryItselfIsNotRunAgainByABeatenTrainer()
    {
        Assert.DoesNotContain(Badge, Run().FlagsSet);
        Assert.DoesNotContain(Badge, Run(TakenIt).FlagsSet);
    }

    /// <summary>
    /// And before the fight, neither arm runs: the fight is reported, the continuation is
    /// reported beside it, and nothing has been handed over.
    /// </summary>
    [Fact]
    public void BeforeTheFightNeitherHappens()
    {
        ScriptRun run = ScriptRunner.Run(new Rom(Image()), 0x08000100, new ScriptState([]));

        Assert.Equal(Trainer, run.TrainerId);
        Assert.Equal(TheFightsOwnScript, run.AfterTheFight);
        Assert.Null(run.GivesItem);
        Assert.Empty(run.FlagsSet);
    }

    // AND THE OTHER HALF OF THE PATH.
    //
    // Everything above is the reader. Whether the victory ever runs at all is the walk's
    // business now, and a fixture that only exercises the reader would report a working
    // half of a broken whole — which is the fault this project found four times in one
    // session and is the reason these two live in the same file.

    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static MapObject Person(int localId, uint script) =>
        new(localId, 1, localId, 1, Direction.Down, 0, false) { ScriptAddress = script };

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// A walk with one person who picks a fight and hands back a continuation, and a run
    /// long enough that a continuation running once per pass would be visible.
    /// </summary>
    private static (Attempt Played, int TimesTheVictoryRan) Walk(int level)
    {
        MapData start = Room("1.0") with { Objects = [Person(1, 0x1000), Person(2, 0x2000)] };

        const uint Victory = 0x9000;

        var opened = 0x100;
        var asked = 0;
        var victories = 0;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, _) =>
            {
                if (address == Victory)
                {
                    victories++;

                    return new PlayedScript([Badge], [], [], [], null, null);
                }

                return address == 0x1000
                    ? new PlayedScript(asked++ < 4 ? [opened++] : [], [], [], [], (1, level), null)
                    : Nothing with { Fights = TestRules.OneAlone, AfterTheFight = Victory };
            });

        return (played, victories);
    }

    /// <summary>
    /// Winning runs what the victory was for, on the pass that won it. It used to run on
    /// the pass after — and on every pass after that.
    /// </summary>
    [Fact]
    public void WinningRunsWhatTheVictoryWasFor()
    {
        (Attempt played, int victories) = Walk(level: 50);

        Assert.Equal(1, played.FightsWon);
        Assert.Contains(Badge, played.Flags);
        Assert.Equal(1, victories);
    }

    /// <summary>And losing does not: there is nothing to run yet.</summary>
    [Fact]
    public void LosingRunsNothing()
    {
        (Attempt played, int victories) = Walk(level: 2);

        Assert.Equal(0, played.FightsWon);
        Assert.Equal(0, victories);
        Assert.DoesNotContain(Badge, played.Flags);
    }
}
