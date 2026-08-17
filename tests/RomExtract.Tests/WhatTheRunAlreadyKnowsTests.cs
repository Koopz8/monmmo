using PokeMmo.Core.Save;
using PokeMmo.Core.Scripts;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Running a script with what the walk already knows — and the two things it was never told.
/// <para>
/// <b>This whole file was impossible to write until the reader moved out of <c>Program.cs</c>.</b>
/// Deciding what a scene does given the flags a run holds, the bag it carries and the trainers
/// it has beaten is not printing, and it lived in a file with no tests that no fixture can
/// reach. Two live faults were sitting in it, both found by measuring the cartridge rather than
/// by reading the code, and both broken on purpose afterwards to green.
/// </para>
/// </summary>
public class WhatTheRunAlreadyKnowsTests
{
    private const byte TrainerBattle = 0x5C;
    private const byte SetVar = 0x16;
    private const byte SetFlag = 0x29;
    private const byte GiveMon = 0x79;
    private const byte LoadPointer = 0x0F;
    private const byte CallStandard = 0x09;

    /// <summary>The standard call that puts a yes-or-no on the screen.</summary>
    private const byte YesOrNo = 0x05;

    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte End = 0x02;

    /// <summary>The variable a yes-or-no box answers into, which is what everything reads.</summary>
    private const int Answer = 0x800D;

    private const int Trainer = 0x0170;

    /// <summary>Turned on past the fight — the shape of every reward in this cartridge.</summary>
    private const int PastTheFight = 0x003E;

    /// <summary>Where the three balls in PALLET TOWN put the species they are.</summary>
    private const int SpeciesVariable = 0x4002;

    private const int Bulbasaur = 1;

    /// <summary>A counter the story keeps — above the scratch pads.</summary>
    private const int Counter = 0x4055;

    /// <summary>And one of the scratch pads, which it must not keep.</summary>
    private const int Scratch = 0x4001;

    /// <summary>Set by the second scene, if the first one is remembered.</summary>
    private const int SecondScene = 0x0321;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static void Pointer(byte[] image, int at, uint address)
    {
        for (int i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>
    /// A fight with the reward behind it, and separately a gift with a question in front of it.
    /// Both shapes are the cartridge's own.
    /// </summary>
    private static Rom Image()
    {
        var image = new byte[0x2000];

        // The fight. Kind 3 carries no script of its own, so a trainer already beaten falls
        // straight through to what the victory was for.
        Put(image, 0x100, TrainerBattle, 3, Trainer & 0xFF, Trainer >> 8, 0x00, 0x00);
        Pointer(image, 0x106, 0x08000400);
        Put(image, 0x10A, SetFlag, PastTheFight & 0xFF, PastTheFight >> 8, End);

        Put(image, 0x400, 0xFE, 0xFE, 0xFE, 0xFE);

        // The ball: it writes which species it is, asks, and hands the creature over on the
        // far side of the answer.
        Put(image, 0x200, SetVar, SpeciesVariable & 0xFF, SpeciesVariable >> 8, Bulbasaur, 0x00);
        Put(image, 0x205, LoadPointer, 0x00);
        Pointer(image, 0x207, 0x08000400);
        Put(image, 0x20B, CallStandard, YesOrNo);

        // And the answer is read, or "yes" would mean nothing and a run that answered no would
        // walk off with the creature anyway.
        Put(image, 0x20D, Compare, Answer & 0xFF, Answer >> 8, 1, 0x00);
        Put(image, 0x212, GotoIf, 0x01);
        Pointer(image, 0x214, 0x08000230);
        Put(image, 0x218, End);

        Put(image, 0x230, GiveMon, SpeciesVariable & 0xFF, SpeciesVariable >> 8, 5);
        Put(image, 0x23F, End);

        // Two scenes, the second reading what the first left behind — which is PALLET TOWN:
        // a trigger north of the town puts one in a counter, and the lab's arrival script
        // reads that one and acts on it.
        Put(image, 0x300, SetVar, Counter & 0xFF, Counter >> 8, 1, 0x00);
        Put(image, 0x305, SetVar, Scratch & 0xFF, Scratch >> 8, 1, 0x00);
        Put(image, 0x30A, End);

        Put(image, 0x320, Compare, Counter & 0xFF, Counter >> 8, 1, 0x00);
        Put(image, 0x325, GotoIf, 0x01);
        Pointer(image, 0x327, 0x08000340);
        Put(image, 0x32B, End);

        Put(image, 0x340, SetFlag, SecondScene & 0xFF, SecondScene >> 8, End);

        Put(image, 0x360, Compare, Scratch & 0xFF, Scratch >> 8, 1, 0x00);
        Put(image, 0x365, GotoIf, 0x01);
        Pointer(image, 0x367, 0x08000340);
        Put(image, 0x36B, End);

        return new Rom(image);
    }

    private static PlayedScript Read(HowAScriptRuns reader, uint address) =>
        reader.Read(address, [], new Bag());

    /// <summary>
    /// <b>A trainerbattle is its own conditional.</b> Beaten, the fight does nothing and the
    /// script carries on into whatever the victory was for. Told nothing, the reader stops at
    /// the fight on every pass forever, however many the run wins — which is SILPH CO.'s
    /// <c>setflag 0x003E</c>, eleven commands past GIOVANNI.
    /// </summary>
    [Fact]
    public void AFightAlreadyWonIsNotInTheWayAnyMore()
    {
        var told = new HowAScriptRuns(Image(), new Dictionary<int, int>(), beaten: [Trainer]);

        Assert.Contains(PastTheFight, Read(told, 0x08000100).FlagsSet);
    }

    /// <summary>
    /// And one not yet won still is. Without this half, everything a victory unlocks would open
    /// for nothing — the same error in the direction nobody notices.
    /// </summary>
    [Fact]
    public void AFightNotYetWonStillIs()
    {
        var untold = new HowAScriptRuns(Image(), new Dictionary<int, int>());

        PlayedScript played = Read(untold, 0x08000100);

        Assert.DoesNotContain(PastTheFight, played.FlagsSet);
        Assert.Equal(Trainer, played.Fights);
    }

    /// <summary>
    /// <b>The other half of a scene is its numbers.</b> The continuation past an unanswered
    /// question carried the flags across and left the variables behind, so PALLET TOWN's
    /// <c>givemon</c> read the species the ball had just written as nought — and
    /// <c>givemon</c> of nought hands over nothing. No run this project ever printed had a
    /// starter.
    /// </summary>
    [Fact]
    public void WhatTheFirstHalfOfASceneWroteIsStillThereInTheSecond()
    {
        var answering = new HowAScriptRuns(Image(), new Dictionary<int, int>(), sayYes: true);

        Assert.Equal((Bulbasaur, 5), Read(answering, 0x08000200).Gives);
    }

    /// <summary>
    /// And a run that answers nothing gets nothing, which is what makes the line above a
    /// measurement rather than a coincidence.
    /// <para>
    /// The answer is read by a <c>compare</c> in the fixture rather than assumed. Without it,
    /// yes and no reach the same <c>givemon</c> and none of this says anything about answering
    /// — which is how a break that wrote nought into the answer variable came back green.
    /// </para>
    /// </summary>
    [Fact]
    public void AndARunThatNeverAnswersStopsAtTheQuestion()
    {
        var silent = new HowAScriptRuns(Image(), new Dictionary<int, int>());

        PlayedScript played = Read(silent, 0x08000200);

        Assert.True(played.StoppedAtAQuestion);
        Assert.Null(played.Gives);
    }

    /// <summary>
    /// <b>Flags crossed from one script to the next and numbers did not.</b> Every variable was
    /// rebuilt from nothing at every script, so a counter one scene set was zero by the time the
    /// next scene read it — and PALLET TOWN, the whole opening of this game, is a counter.
    /// </summary>
    [Fact]
    public void WhatOneSceneLeavesInACounterIsThereForTheNext()
    {
        var reader = new HowAScriptRuns(Image(), new Dictionary<int, int>());

        Read(reader, 0x08000300);

        Assert.Contains(SecondScene, Read(reader, 0x08000320).FlagsSet);
    }

    /// <summary>
    /// And a scratch pad is not remembered. Three hundred scripts scribble on one of these; a
    /// run that carried them would have every comparison in the game answered by whatever the
    /// last person it spoke to happened to leave there.
    /// </summary>
    [Fact]
    public void AScratchPadIsNotRemembered()
    {
        var reader = new HowAScriptRuns(Image(), new Dictionary<int, int>());

        Read(reader, 0x08000300);

        Assert.DoesNotContain(SecondScene, Read(reader, 0x08000360).FlagsSet);
    }

    /// <summary>
    /// And nothing is remembered before anything has run, so an empty run is empty rather than
    /// carrying whatever the fixture happened to hold.
    /// </summary>
    [Fact]
    public void AFreshReaderRemembersNothing()
    {
        var reader = new HowAScriptRuns(Image(), new Dictionary<int, int>());

        Assert.DoesNotContain(SecondScene, Read(reader, 0x08000320).FlagsSet);
    }
}
