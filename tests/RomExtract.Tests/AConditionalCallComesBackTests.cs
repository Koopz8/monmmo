using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A conditional call comes back, so there is no other arm.
/// <para>
/// <b>The fault that invented a wall, and it was in this project rather than in the game.</b>
/// What follows a conditional <c>goto</c> is the condition inverted, and a <c>goto</c> the
/// script has already decided to take never returns — so a walk that stops there is right.
/// Neither of those is true of a conditional <c>call</c>: it goes, it comes back, and the rest
/// of the block runs whichever way the answer went.
/// </para>
/// <para>
/// SILPH CO.'s GIOVANNI trigger is <c>compare 0x4001, 0 / callif</c>, twice — two little
/// walking animations picked by which square you stepped on — and then, unconditionally, the
/// fight, three <c>hideobject</c>s, <c>setflag 0x003E</c> and <c>clearflag 0x003F</c>. Stopping
/// at the first taken call threw all of that away and reported that nothing in the world could
/// set the flag holding eight people in place on SAFFRON, four of them in doorways.
/// </para>
/// <para>
/// <see cref="WhatItIsWaitingFor.Asks"/> was corrected for exactly this in milestone 173 and
/// this walk was not, so the two halves of one tool disagreed a second time — and a second time
/// the stricter one was believed, because strictness sounds like rigour.
/// </para>
/// </summary>
public class AConditionalCallComesBackTests
{
    private const byte SetVar = 0x16;
    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte CallIf = 0x07;
    private const byte Goto = 0x05;
    private const byte SetFlag = 0x29;
    private const byte Return = 0x03;
    private const byte End = 0x02;

    /// <summary>Set after a conditional call the script has already decided to take.</summary>
    private const int AfterTheCall = 0x0055;

    /// <summary>Set after a conditional goto the script has already decided to take.</summary>
    private const int AfterTheGoto = 0x0056;

    /// <summary>Set inside the called block, which really is behind the condition.</summary>
    private const int InsideTheCall = 0x0057;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static void Pointer(byte[] image, int at, uint address)
    {
        for (int i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>
    /// Two scripts of the same shape, differing only in whether the branch comes back.
    /// <para>
    /// The decoy is the whole fixture. A walk fixed by simply never stopping at a taken branch
    /// would pass every test about the call and quietly start reporting flags behind a taken
    /// <c>goto</c> that no run can ever reach — which is the fault milestone 173 built the
    /// pruning for, in the opposite direction.
    /// </para>
    /// </summary>
    private static byte[] Image()
    {
        var image = new byte[0x2000];

        // The call. The script decides the answer itself, takes the branch, and carries on.
        Put(image, 0x100, SetVar, 0x01, 0x40, 0x00, 0x00);
        Put(image, 0x105, Goto);
        Pointer(image, 0x106, 0x08000200);

        Put(image, 0x200, Compare, 0x01, 0x40, 0x00, 0x00);
        Put(image, 0x205, CallIf, 0x01);
        Pointer(image, 0x207, 0x08000300);
        Put(image, 0x20B, SetFlag, AfterTheCall & 0xFF, AfterTheCall >> 8);
        Put(image, 0x20E, End);

        Put(image, 0x300, SetFlag, InsideTheCall & 0xFF, InsideTheCall >> 8, Return);

        // The goto. Same shape, and what follows it is unreachable.
        Put(image, 0x400, SetVar, 0x02, 0x40, 0x00, 0x00);
        Put(image, 0x405, Goto);
        Pointer(image, 0x406, 0x08000500);

        Put(image, 0x500, Compare, 0x02, 0x40, 0x00, 0x00);
        Put(image, 0x505, GotoIf, 0x01);
        Pointer(image, 0x507, 0x08000600);
        Put(image, 0x50B, SetFlag, AfterTheGoto & 0xFF, AfterTheGoto >> 8);
        Put(image, 0x50E, End);

        Put(image, 0x600, End);

        return image;
    }

    private static Rom Rom() => new(Image());

    /// <summary>
    /// The finding. What follows a taken conditional call is reached, because the call returns.
    /// </summary>
    [Fact]
    public void WhatFollowsATakenConditionalCallIsStillReached()
    {
        (IReadOnlyCollection<int> on, IReadOnlyCollection<int> _) = WhatItIsWaitingFor.ReallyTouches(
            Rom(), [new SetsAFlag("1.1", "trigger (0,0)", 0x08000100)], out int _);

        Assert.Contains(AfterTheCall, on);
    }

    /// <summary>
    /// And the decoy: what follows a taken conditional <em>goto</em> is not reached, because a
    /// goto does not come back. A walk that stopped distinguishing the two would be wrong in
    /// the other direction and every test above would still pass.
    /// </summary>
    [Fact]
    public void WhatFollowsATakenConditionalGotoIsStillNotReached()
    {
        (IReadOnlyCollection<int> on, IReadOnlyCollection<int> _) = WhatItIsWaitingFor.ReallyTouches(
            Rom(), [new SetsAFlag("1.1", "trigger (1,1)", 0x08000400)], out int _);

        Assert.DoesNotContain(AfterTheGoto, on);
    }

    /// <summary>
    /// And the chain says so: nothing had to be true to reach it. A flag after a conditional
    /// call is not behind that condition, and reporting it as gated sends the next session
    /// hunting for whoever supplies an answer that changes nothing.
    /// </summary>
    [Fact]
    public void NothingHadToBeTrueToReachWhatFollowsTheCall()
    {
        IReadOnlyList<OnTheWay>? chain = WhatItIsWaitingFor.PathTo(Rom(), 0x08000100, AfterTheCall);

        Assert.NotNull(chain);
        Assert.Empty(chain);
    }

    /// <summary>
    /// While what is <em>inside</em> the called block really is behind the condition — so the
    /// fix is not "conditional calls gate nothing", it is "they gate their own contents".
    /// </summary>
    [Fact]
    public void WhatIsInsideTheCalledBlockIsStillBehindTheCondition()
    {
        IReadOnlyList<OnTheWay>? chain = WhatItIsWaitingFor.PathTo(Rom(), 0x08000100, InsideTheCall);

        Assert.NotNull(chain);
        Assert.Single(chain);
    }

    /// <summary>
    /// And the two halves of the tool agree about it, which is the thing that failed twice.
    /// <c>Asks</c> has priced a conditional call as its called block alone since milestone 173;
    /// the walk now reaches the same remainder, so "can this script set that flag" and "what
    /// does this answer cost" are answers about the same script.
    /// </summary>
    [Fact]
    public void BothHalvesOfTheToolAgreeAboutTheRemainder()
    {
        Rom rom = Rom();

        WaitingOn waiting = WhatItIsWaitingFor.Asks(rom, 0x08000200);

        (IReadOnlyCollection<int> on, IReadOnlyCollection<int> _) = WhatItIsWaitingFor.ReallyTouches(
            rom, [new SetsAFlag("1.1", "trigger (0,0)", 0x08000200)], out int _);

        // Nothing this script asks about prices the remainder as its own: the flag after the
        // call belongs to neither arm.
        Assert.DoesNotContain(waiting.Flags, f => f.IfSet.Sets.Contains(AfterTheCall));
        Assert.DoesNotContain(waiting.Flags, f => f.IfClear.Sets.Contains(AfterTheCall));

        Assert.Contains(AfterTheCall, on);
    }

    /// <summary>
    /// And the other half of the same mistake: a run that stopped at a fight said so nowhere.
    /// <para>
    /// <b>A fallback that names a cause is worse than one that says nothing.</b> "It ran to the
    /// end, so the setflag is on an ordinary branch it had no reason to take" was printed
    /// because nothing else matched — and it is actionable, and it was wrong, and two sessions
    /// went looking for that branch. The run had stopped at GIOVANNI.
    /// </para>
    /// </summary>
    [Fact]
    public void AFightThatStoppedARunIsRememberedSoItCanBeSaidOutLoud()
    {
        var fought = new WhatRan().And(
            new PlayedScript([], [], [], [], null, 349));

        Assert.Contains(349, fought.Fought);

        // And a script that stopped at nothing at all still says nothing at all, which is the
        // case the fallback is actually for.
        var quiet = new WhatRan().And(new PlayedScript([], [], [], [], null, null));

        Assert.Empty(quiet.Fought);
    }

    /// <summary>
    /// And the run carries the commands it could not read all the way out, so the report can
    /// say so.
    /// <para>
    /// <b>The half of the error bar that was missing.</b> A run has always named the routines it
    /// could not answer and never the commands it could not step over. They are different
    /// boundaries: a routine is the game's own code, and a command with no width is a gap in a
    /// table in this repository. One of those is where the world ends and the other is a job,
    /// and reporting only the first made a small world look like the cartridge's fault.
    /// </para>
    /// </summary>
    [Fact]
    public void TheRunCarriesOutTheCommandsItCouldNotRead()
    {
        MapData start = new MapData("1.0", "1.0", 4, 4, new byte[16]) with
        {
            Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],
        };

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) => new PlayedScript([], [], [], [], null, null) { StoppedAt = [0xEE] });

        Assert.Equal(1, played.UnreadCommands.GetValueOrDefault((byte)0xEE));

        // And a run that read everything says nothing, so an empty list means empty rather
        // than unmeasured.
        Attempt clean = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) => new PlayedScript([], [], [], [], null, null));

        Assert.Empty(clean.UnreadCommands);
    }
}
