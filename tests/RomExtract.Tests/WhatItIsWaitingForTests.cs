using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Reading the arm a playthrough did not take.
/// <para>
/// Four people in the last four doorways read as people with no part in the story: talked to,
/// set no flag, asked for nothing, walked nobody, called no routine at all. A run can only
/// ever report the arm it took, so "this script does nothing" and "this script does nothing
/// <em>yet</em>" arrive at the report looking identical — and the second one is a wall with a
/// number on it.
/// </para>
/// <para>
/// So: which flag, and what would the other answer have been worth. Both halves have to be
/// falsifiable — a script that asks about no flag has to come back saying so, or this is a
/// conclusion with a measurement painted on it.
/// </para>
/// </summary>
public class WhatItIsWaitingForTests
{
    private const byte CheckFlag = 0x2B;
    private const byte SetFlag = 0x29;
    private const byte GotoIf = 0x06;
    private const byte CallIf = 0x07;
    private const byte Goto = 0x05;
    private const byte Call = 0x04;
    private const byte ApplyMovement = 0x4F;
    private const byte Release = 0x6C;
    private const byte Return = 0x03;
    private const byte End = 0x02;
    private const byte Compare = 0x21;
    private const byte HideObject = 0x53;
    private const byte GiveItem = 0x46;
    private const byte Special = 0x25;
    private const byte SetVar = 0x16;
    private const byte AddVar = 0x17;
    private const byte CopyVar = 0x19;

    /// <summary>Jump when the comparison came out equal — a checkflag's "already done" arm.</summary>
    private const byte IfEqual = 1;

    /// <summary>And when it came out less, which is the "not yet" arm.</summary>
    private const byte IfLess = 0;

    private const int Waiting = 0x0825;
    private const int Opened = 0x0826;

    /// <summary>A flag set on the tail both arms run through, and therefore neither one's.</summary>
    private const int Shared = 0x0827;

    /// <summary>A routine on the far arm — the code boundary, waiting behind the flag.</summary>
    private const int Routine = 0x01B5;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static void Pointer(byte[] image, int at, uint address)
    {
        for (int i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>
    /// A doorway's shape: ask whether it has been done, and if it has, step aside and say so.
    /// If it has not, fall straight through to the same ending the other arm reaches anyway.
    /// </summary>
    private static byte[] Image(byte condition = IfEqual)
    {
        var image = new byte[0x2000];

        Put(image, 0x100, CheckFlag, Waiting & 0xFF, Waiting >> 8);
        Put(image, 0x103, GotoIf, condition);
        Pointer(image, 0x105, 0x08000200);

        // The arm that falls through: nothing of its own at all, on to the shared ending.
        Put(image, 0x109, Goto);
        Pointer(image, 0x10A, 0x08000300);

        // The arm behind the flag: he steps aside, and the door is marked open.
        Put(image, 0x200, ApplyMovement, 0x01, 0x00);
        Pointer(image, 0x203, 0x08000500);
        Put(image, 0x207, SetFlag, Opened & 0xFF, Opened >> 8);
        Put(image, 0x20A, Goto);
        Pointer(image, 0x20B, 0x08000300);

        // The ending both of them reach. Whatever happens here is not the price of the answer.
        Put(image, 0x300, SetFlag, Shared & 0xFF, Shared >> 8, Release, End);

        // A movement list, so the step bytes are a list rather than a hole.
        Put(image, 0x500, 0x10, 0xFE);

        return image;
    }

    [Fact]
    public void ItNamesTheFlagTheScriptIsWaitingOn()
    {
        WaitingOn waiting = WhatItIsWaitingFor.Asks(new Rom(Image()), 0x08000100);

        Assert.Equal(Waiting, Assert.Single(waiting.Flags).Flag);
    }

    /// <summary>
    /// And prices the answer. "Waiting on 0x0825" is a fact; "and the other arm walks him out
    /// of the doorway" is what makes it worth going and finding.
    /// </summary>
    [Fact]
    public void TheArmBehindTheFlagIsPricedAtWhatItDoes()
    {
        FlagAsked asked = Assert.Single(WhatItIsWaitingFor.Asks(new Rom(Image()), 0x08000100).Flags);

        Assert.True(asked.IfSet.Walks);
        Assert.Contains(Opened, asked.IfSet.Sets);
    }

    /// <summary>
    /// The arm the run took is worth nothing, which is why the run reported a person who does
    /// nothing. Both halves of that sentence have to be visible or the finding is unreadable.
    /// </summary>
    [Fact]
    public void TheArmTheRunTookIsPricedAtNothing()
    {
        FlagAsked asked = Assert.Single(WhatItIsWaitingFor.Asks(new Rom(Image()), 0x08000100).Flags);

        Assert.True(asked.IfClear.Nothing);
        Assert.False(asked.NeitherAnswerChangesAnything);
    }

    /// <summary>
    /// What both arms run through is neither arm's doing.
    /// <para>
    /// Nearly every branch in this cartridge rejoins. An arm summarised on everything
    /// reachable from it credits each half with the other half's work, and two identical
    /// halves is exactly what "this flag changes nothing" looks like — so the instrument
    /// would report the flag it found as not worth having.
    /// </para>
    /// </summary>
    [Fact]
    public void TheEndingBothArmsReachIsCreditedToNeither()
    {
        FlagAsked asked = Assert.Single(WhatItIsWaitingFor.Asks(new Rom(Image()), 0x08000100).Flags);

        Assert.DoesNotContain(Shared, asked.IfSet.Sets);
        Assert.DoesNotContain(Shared, asked.IfClear.Sets);
    }

    /// <summary>
    /// Which arm is which is the operator byte's to say, not the branch's.
    /// <para>
    /// The same two blocks, the same order, one byte different: <c>goto if less</c> is the
    /// "not yet" arm, so now the flag being <em>clear</em> is what walks him. Assuming the
    /// branch is the set arm reads clean and is wrong about half the time, which is the worst
    /// way to be wrong.
    /// </para>
    /// </summary>
    [Fact]
    public void TheConditionByteDecidesWhichAnswerIsWhich()
    {
        FlagAsked asked = Assert.Single(
            WhatItIsWaitingFor.Asks(new Rom(Image(IfLess)), 0x08000100).Flags);

        Assert.True(asked.IfClear.Walks);
        Assert.True(asked.IfSet.Nothing);
    }

    /// <summary>
    /// Work behind a handoff inside the arm is still that arm's. Most people in this game do
    /// their work somewhere else, which is the fault <c>ReadAll</c> was written for.
    /// </summary>
    [Fact]
    public void WorkBehindACallInsideTheArmIsStillFound()
    {
        byte[] image = Image();

        // The arm no longer sets the flag itself; it hands off to something that does.
        Put(image, 0x207, Call);
        Pointer(image, 0x208, 0x08000600);
        Put(image, 0x20C, End);
        Put(image, 0x600, SetFlag, Opened & 0xFF, Opened >> 8, Return);

        FlagAsked asked = Assert.Single(WhatItIsWaitingFor.Asks(new Rom(image), 0x08000100).Flags);

        Assert.Contains(Opened, asked.IfSet.Sets);
    }

    /// <summary>
    /// A conditional <em>call</em> comes back, so what follows it is not the other arm — it is
    /// the shared remainder, run whichever way the answer went. Pricing it as the cost of the
    /// answer invents a reason to go and set a flag that changes nothing.
    /// </summary>
    [Fact]
    public void WhatFollowsAConditionalCallIsNotTheOtherArm()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, CheckFlag, Waiting & 0xFF, Waiting >> 8);
        Put(image, 0x103, CallIf, IfEqual);
        Pointer(image, 0x105, 0x08000200);

        // What runs either way, sitting where a goto's other arm would sit.
        Put(image, 0x109, SetFlag, Shared & 0xFF, Shared >> 8, Release, End);

        Put(image, 0x200, SetFlag, Opened & 0xFF, Opened >> 8, Return);

        FlagAsked asked = Assert.Single(WhatItIsWaitingFor.Asks(new Rom(image), 0x08000100).Flags);

        Assert.Contains(Opened, asked.IfSet.Sets);
        Assert.True(asked.IfClear.Nothing);
    }

    /// <summary>
    /// The honest negative, and the only reason to build this rather than write the answer
    /// down: a script that asks about no flag has to come back saying so.
    /// </summary>
    [Fact]
    public void AScriptThatAsksAboutNoFlagSaysSo()
    {
        WaitingOn waiting = WhatItIsWaitingFor.Asks(new Rom(Image()), 0x08000300);

        Assert.Empty(waiting.Flags);
        Assert.Equal(0, waiting.AskedWithoutABranch);
    }

    /// <summary>
    /// A checkflag this cannot pair with a branch is counted, not read. It is the size of the
    /// instrument's own blind spot, and a blind spot with no number on it is a claim of
    /// completeness nobody checked.
    /// </summary>
    [Fact]
    public void ACheckflagWithNothingToBranchOnIsCountedRatherThanGuessedAt()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, CheckFlag, Waiting & 0xFF, Waiting >> 8, Release, End);

        WaitingOn waiting = WhatItIsWaitingFor.Asks(new Rom(image), 0x08000100);

        Assert.Empty(waiting.Flags);
        Assert.Equal(1, waiting.AskedWithoutABranch);
    }

    /// <summary>
    /// Every way an arm can change the world, priced.
    /// <para>
    /// Walking somebody was the one that got tested, because it is what a guard in a doorway
    /// does — and the other three are the ones the report leans on hardest. <b>A routine on
    /// the arm behind the flag is the whole difference between "go and set this flag" and
    /// "setting this flag leads straight back to the code boundary"</b>, which is the finding
    /// that decides whether the door is worth walking to at all.
    /// </para>
    /// </summary>
    [Fact]
    public void AnArmIsPricedOnEveryWayItCanChangeTheWorld()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, CheckFlag, Waiting & 0xFF, Waiting >> 8);
        Put(image, 0x103, GotoIf, IfEqual);
        Pointer(image, 0x105, 0x08000200);
        Put(image, 0x109, Release, End);

        Put(image, 0x200, HideObject, 0x03, 0x00);
        Put(image, 0x203, GiveItem, 0x1A, 0x00, 0x01, 0x00);
        Put(image, 0x208, Special, Routine & 0xFF, Routine >> 8);
        Put(image, 0x20B, Release, End);

        FlagAsked asked = Assert.Single(WhatItIsWaitingFor.Asks(new Rom(image), 0x08000100).Flags);

        Assert.True(asked.IfSet.Hides);
        Assert.True(asked.IfSet.HandsSomethingOver);
        Assert.Contains(Routine, asked.IfSet.Routines);
    }

    /// <summary>
    /// A script gated on something that is not a flag has to say so, or this instrument
    /// reports the wrong shape of job as no job at all.
    /// <para>
    /// "Waiting on a flag" was a guess. A door gated on what is in the bag is a shopping
    /// list, one gated on a routine's answer is behind the code boundary, and a
    /// flag-shaped instrument with nothing counting the rest calls all three <em>asks about
    /// no flag at all</em> — which is what a person with no part in the story looks like.
    /// </para>
    /// </summary>
    [Fact]
    public void AScriptGatedOnSomethingOtherThanAFlagSaysWhatItAsked()
    {
        var image = new byte[0x2000];

        // The shape checkitem leaves behind: an answer in a variable, compared, then branched.
        Put(image, 0x100, Compare, 0x0D, 0x80, 0x01, 0x00);
        Put(image, 0x105, GotoIf, IfEqual);
        Pointer(image, 0x107, 0x08000300);
        Put(image, 0x10B, Release, End);
        Put(image, 0x300, SetFlag, Opened & 0xFF, Opened >> 8, Release, End);

        WaitingOn waiting = WhatItIsWaitingFor.Asks(new Rom(image), 0x08000100);

        Assert.Empty(waiting.Flags);
        Assert.Equal((Compare, 1), Assert.Single(waiting.OtherQuestions));
    }

    /// <summary>
    /// And a branch that <em>is</em> a flag's is not counted twice. A blind spot that includes
    /// what it can see is a number that means nothing.
    /// </summary>
    [Fact]
    public void AFlagsOwnBranchIsNotCountedAsSomethingElse()
    {
        WaitingOn waiting = WhatItIsWaitingFor.Asks(new Rom(Image()), 0x08000100);

        Assert.Empty(waiting.OtherQuestions);
    }

    /// <summary>
    /// A read that ran into its own limit says so.
    /// <para>
    /// The failure this project keeps meeting: a traversal that stops early comes back clean
    /// and quietly contains less, and every sentence built on it — <em>asks about no flag at
    /// all</em>, above all — reads as a finding rather than as a limit. A door reported as
    /// waiting on nothing, because the reader gave up two blocks short, is a person written
    /// out of the story by a number in a default parameter.
    /// </para>
    /// </summary>
    [Fact]
    public void AReadThatHitItsOwnLimitSaysSo()
    {
        var image = new byte[0x2000];

        // A chain longer than the reader is allowed to follow.
        for (var block = 0; block < 6; block++)
        {
            Put(image, 0x100 + (block * 0x10), Goto);
            Pointer(image, 0x101 + (block * 0x10), (uint)(0x08000110 + (block * 0x10)));
        }

        // And the gate at the far end of it, which is what a limit costs.
        Put(image, 0x160, CheckFlag, Waiting & 0xFF, Waiting >> 8);
        Put(image, 0x163, GotoIf, IfEqual);
        Pointer(image, 0x165, 0x08000200);
        Put(image, 0x169, Release, End);
        Put(image, 0x200, SetFlag, Opened & 0xFF, Opened >> 8, Release, End);

        WaitingOn stopped = WhatItIsWaitingFor.Asks(new Rom(image), 0x08000100, maxScripts: 3);

        Assert.True(stopped.Truncated);
        Assert.Empty(stopped.Flags);

        WaitingOn whole = WhatItIsWaitingFor.Asks(new Rom(image), 0x08000100);

        Assert.False(whole.Truncated);
        Assert.Equal(Waiting, Assert.Single(whole.Flags).Flag);
    }

    /// <summary>
    /// A setflag nothing stands in front of comes back with nothing standing in front of it.
    /// An empty chain and no chain are not the same answer and must not print the same.
    /// </summary>
    [Fact]
    public void AnUnconditionalSetflagIsGatedByNothing()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, SetFlag, Opened & 0xFF, Opened >> 8, Release, End);

        Assert.Empty(WhatItIsWaitingFor.PathTo(new Rom(image), 0x08000100, Opened)!);
    }

    /// <summary>And a flag nothing reachable sets comes back with no chain at all.</summary>
    [Fact]
    public void AFlagNothingSetsHasNoWayToIt()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, SetFlag, Opened & 0xFF, Opened >> 8, Release, End);

        Assert.Null(WhatItIsWaitingFor.PathTo(new Rom(image), 0x08000100, Waiting));
    }

    /// <summary>
    /// The answers that had to go a particular way, in order — and for the flag asked about
    /// rather than whichever setflag turns up first.
    /// <para>
    /// The decoy is the first line of the script: a setflag of something else, standing
    /// unconditionally where a reader looking for "the first setflag" would stop. Every real
    /// script of this shape starts with bookkeeping.
    /// </para>
    /// </summary>
    [Fact]
    public void TheChainIsToTheFlagAskedAboutAndNotTheFirstOneFound()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, SetFlag, Shared & 0xFF, Shared >> 8);
        Put(image, 0x103, CheckFlag, Waiting & 0xFF, Waiting >> 8);
        Put(image, 0x106, GotoIf, IfEqual);
        Pointer(image, 0x108, 0x08000200);
        Put(image, 0x10C, Release, End);
        Put(image, 0x200, SetFlag, Opened & 0xFF, Opened >> 8, Release, End);

        var rom = new Rom(image);

        Assert.Empty(WhatItIsWaitingFor.PathTo(rom, 0x08000100, Shared)!);
        Assert.Equal(
            [$"flag 0x{Waiting:X4} SET"],
            WhatItIsWaitingFor.PathTo(rom, 0x08000100, Opened)!.Select(w => w.ToString()));
    }

    /// <summary>
    /// Getting somewhere by <em>not</em> jumping is the condition inverted, and it is the
    /// commoner of the two — the "not yet" arm of every checkflag in this cartridge is the
    /// fall-through. Reporting it as though the branch had been taken names the opposite of
    /// what has to be true, which is a flag number pointing the wrong way.
    /// </summary>
    [Fact]
    public void FallingThroughABranchIsTheConditionInverted()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, CheckFlag, Waiting & 0xFF, Waiting >> 8);
        Put(image, 0x103, GotoIf, IfEqual);
        Pointer(image, 0x105, 0x08000300);
        Put(image, 0x109, SetFlag, Opened & 0xFF, Opened >> 8, Release, End);
        Put(image, 0x300, Release, End);

        Assert.Equal(
            [$"flag 0x{Waiting:X4} CLEAR"],
            WhatItIsWaitingFor.PathTo(new Rom(image), 0x08000100, Opened)!.Select(w => w.ToString()));
    }

    /// <summary>Two deep is two answers, in the order they have to go right.</summary>
    [Fact]
    public void EveryAnswerOnTheWayIsInTheChain()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, CheckFlag, Waiting & 0xFF, Waiting >> 8);
        Put(image, 0x103, GotoIf, IfEqual);
        Pointer(image, 0x105, 0x08000200);
        Put(image, 0x109, Release, End);

        Put(image, 0x200, Compare, 0x0D, 0x80, 0x01, 0x00);
        Put(image, 0x205, GotoIf, IfEqual);
        Pointer(image, 0x207, 0x08000300);
        Put(image, 0x20B, Release, End);

        Put(image, 0x300, SetFlag, Opened & 0xFF, Opened >> 8, Release, End);

        Assert.Equal(
            [$"flag 0x{Waiting:X4} SET", "0x800D == 1"],
            WhatItIsWaitingFor.PathTo(new Rom(image), 0x08000100, Opened)!.Select(w => w.ToString()));
    }

    /// <summary>
    /// And a chain through a handoff is still a chain. Most of the work in this game is at
    /// the other end of a <c>call</c>.
    /// </summary>
    [Fact]
    public void AChainThroughAHandoffIsStillAChain()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, CheckFlag, Waiting & 0xFF, Waiting >> 8);
        Put(image, 0x103, GotoIf, IfEqual);
        Pointer(image, 0x105, 0x08000200);
        Put(image, 0x109, Release, End);

        Put(image, 0x200, Call);
        Pointer(image, 0x201, 0x08000400);
        Put(image, 0x205, Release, End);

        Put(image, 0x400, SetFlag, Opened & 0xFF, Opened >> 8, Return);

        Assert.Equal(
            [$"flag 0x{Waiting:X4} SET"],
            WhatItIsWaitingFor.PathTo(new Rom(image), 0x08000100, Opened)!.Select(w => w.ToString()));
    }

    /// <summary>
    /// Who could put the right number in a variable, which is the mirror of who sets a flag.
    /// <para>
    /// What stands in front of SAFFRON is <c>0x4001 != 0 AND 0x4001 != 1</c> — a counter, not
    /// a flag — and an instrument that only knows about flags reports a counter as nothing at
    /// all. A variable nothing writes is behind the code boundary exactly as a flag nothing
    /// sets is.
    /// </para>
    /// <para>
    /// The decoy is the second script: one that <em>reads</em> the variable and never writes
    /// it. Every script gated on a counter is one of those, so a scan that counted mentions
    /// would hand back the gate itself as the way through it.
    /// </para>
    /// </summary>
    [Fact]
    public void WritesToNamesEveryScriptThatPutsANumberInAVariable()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, SetVar, 0x01, 0x40, 0x02, 0x00, End);
        Put(image, 0x200, AddVar, 0x01, 0x40, 0x01, 0x00, End);

        // Reads it and never writes it, and — the part that took a run to get right — does
        // not hand off to anything that writes it either. ReadAll follows calls and gotos, so
        // a decoy that jumps into the writer is credited with the writer's work, correctly.
        Put(image, 0x300, Compare, 0x01, 0x40, 0x02, 0x00);
        Put(image, 0x305, GotoIf, IfEqual);
        Pointer(image, 0x307, 0x08000400);
        Put(image, 0x30B, Release, End);
        Put(image, 0x400, Release, End);

        IReadOnlyDictionary<int, IReadOnlyList<WritesAVariable>> writes = WhatItIsWaitingFor.WritesTo(
            new Rom(image),
            [
                new SetsAFlag("1.57", "trigger (5,15)", 0x08000100),
                new SetsAFlag("1.57", "person 4", 0x08000200),
                new SetsAFlag("3.10", "person 1", 0x08000300),
            ]);

        Assert.Equal(
            ["1.57 trigger (5,15) setvar 2", "1.57 person 4 addvar 1"],
            writes[0x4001].Select(w => w.ToString()));
    }

    /// <summary>
    /// And a <c>copyvar</c> says it copied rather than naming a value it does not know. The
    /// second word there is another variable, and printing it as a number is a number nobody
    /// read.
    /// </summary>
    [Fact]
    public void ACopiedVariableIsNotReportedAsAValue()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, CopyVar, 0x01, 0x40, 0x08, 0x80, End);

        IReadOnlyList<WritesAVariable> writes = WhatItIsWaitingFor.WritesTo(
            new Rom(image), [new SetsAFlag("1.57", "person 4", 0x08000100)])[0x4001];

        Assert.Equal("1.57 person 4 copies 0x8008 into it", Assert.Single(writes).ToString());
    }

    /// <summary>
    /// And the other half of the job: who turns it on. A flag nothing sets is a door behind
    /// the code boundary; a flag somebody two maps away sets is a walk.
    /// </summary>
    [Fact]
    public void SetByNamesEveryScriptThatTurnsAFlagOn()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, SetFlag, Opened & 0xFF, Opened >> 8, End);
        Put(image, 0x200, SetFlag, Opened & 0xFF, Opened >> 8, End);
        Put(image, 0x300, SetFlag, Shared & 0xFF, Shared >> 8, End);

        // The decoy, and the whole difficulty: somebody who asks about the flag and never
        // turns it on. Every doorway this instrument is pointed at is one of these — a script
        // whose only mention of the flag is the question — so a scan that counts mentions
        // rather than settings would hand back the blockers themselves as the way through,
        // and the answer would be "go and talk to the man in the doorway".
        Put(image, 0x400, CheckFlag, Opened & 0xFF, Opened >> 8);
        Put(image, 0x403, GotoIf, IfEqual);
        Pointer(image, 0x405, 0x08000300);
        Put(image, 0x409, Release, End);

        IReadOnlyDictionary<int, IReadOnlyList<SetsAFlag>> setters = WhatItIsWaitingFor.SetBy(
            new Rom(image),
            [
                new SetsAFlag("3.10", "person 1", 0x08000100),
                new SetsAFlag("5.3", "trigger (4,7)", 0x08000200),
                new SetsAFlag("1.72", "sign", 0x08000300),
                new SetsAFlag("14.0", "person 1", 0x08000400),
            ]);

        Assert.Equal(["3.10 person 1", "5.3 trigger (4,7)"], setters[Opened].Select(s => s.ToString()));
        Assert.False(setters.ContainsKey(Waiting));

        // And the address travels, because "which script" is the next question after "where".
        Assert.Equal(0x08000100u, setters[Opened][0].Address);
    }
}
