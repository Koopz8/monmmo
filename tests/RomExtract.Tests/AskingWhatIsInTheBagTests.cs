using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The question the runner could never answer: is the player carrying this.
/// <para>
/// <c>grep -n "checkitem\|Bag\|HasItem" ScriptRunner.cs</c> returned nothing, and every one
/// of the sites that asks was therefore told no — because an unwritten answer variable holds
/// nought and the compare that follows this particular command is against <em>one</em>.
/// That is the shape of a guard who wants a drink and is never given one, which is what
/// roughly two hundred maps of this cartridge sit behind.
/// </para>
/// <para>
/// The command is 0x47, and it was not chosen from a table. Its width was adopted off the
/// bytes at milestone 100-odd on the shape it shares with giveitem — an id, a count — and
/// the one thing that separates the two is written down there: <c>21 0D 80 01 00</c>, the
/// answer compared against one, where giveitem's own compare is against zero. A command
/// that answers into the result variable and is asked "is it one" is a command that was
/// asked a question.
/// </para>
/// </summary>
public class AskingWhatIsInTheBagTests
{
    private const uint Start = Rom.BaseAddress;
    private const uint Elsewhere = Rom.BaseAddress + 0x100;
    private const uint SaysYes = Rom.BaseAddress + 0x200;
    private const uint SaysNo = Rom.BaseAddress + 0x220;

    private const int Tea = 0x0155;
    private const int Parcel = 0x015D;

    private static byte[] At(uint address) =>
        [(byte)address, (byte)(address >> 8), (byte)(address >> 16), (byte)(address >> 24)];

    private static byte[] Word(int value) => [(byte)value, (byte)(value >> 8)];

    private static byte[] Speech(char letter) =>
    [
        .. Enumerable.Repeat((byte)(0xBB + (letter - 'A')), 6),
        GameText.Terminator,
    ];

    private static Rom Image(params (uint Address, byte[] Bytes)[] chunks)
    {
        var image = new byte[0x400];

        foreach ((uint address, byte[] bytes) in chunks)
            bytes.CopyTo(image, (int)(address - Rom.BaseAddress));

        return new Rom(image);
    }

    private static byte[] Says(uint text) => [ScriptCommands.Message, .. At(text), ScriptCommands.End];

    /// <summary>
    /// The cartridge's own shape, from the bytes quoted in the width table: check, compare
    /// the answer against one, and branch to the arm that happens when you have it.
    /// </summary>
    private static Rom Guard(int itemId = Tea, int wanted = 1) => Image(
        (Start,
        [
            0x47, .. Word(itemId), .. Word(wanted),
            0x21, .. Word(0x800D), .. Word(1),
            ScriptCommands.GotoIf, 0x01, .. At(Elsewhere),
            .. Says(SaysNo),
        ]),
        (Elsewhere, Says(SaysYes)),
        (SaysYes, Speech('A')),
        (SaysNo, Speech('B')));

    private static ScriptState Carrying(params (int ItemId, int Count)[] items)
    {
        var bag = new Bag();

        foreach ((int itemId, int count) in items) bag.Add(itemId, count);

        return new ScriptState { CountOfItem = bag.CountOf };
    }

    // ---- the question itself ---------------------------------------------------------

    /// <summary>
    /// The two arms, and this is the whole milestone in one test: the same script, the same
    /// flags, a different bag, and a different conversation.
    /// </summary>
    [Fact]
    public void TheArmThatRunsDependsOnWhatIsCarried()
    {
        Rom rom = Guard();

        Assert.Equal("BBBBBB", Assert.Single(ScriptRunner.Run(rom, Start, Carrying()).Pages));

        Assert.Equal(
            "AAAAAA", Assert.Single(ScriptRunner.Run(rom, Start, Carrying((Tea, 1))).Pages));
    }

    /// <summary>
    /// And carrying something else is not carrying this. The failure worth guarding against
    /// is a bag that answers "yes" to any question at all once it holds anything.
    /// </summary>
    [Fact]
    public void SomethingElseIsNotTheThingThatWasAskedFor()
    {
        Assert.Equal(
            "BBBBBB",
            Assert.Single(ScriptRunner.Run(Guard(), Start, Carrying((Parcel, 1))).Pages));
    }

    /// <summary>
    /// The count is read as well as the id. Both words are in the command and reading only
    /// the first would answer yes to "have you got ten of these" for somebody holding one.
    /// </summary>
    [Fact]
    public void AskingForMoreThanIsCarriedIsRefused()
    {
        Rom rom = Guard(Tea, 5);

        Assert.Equal("BBBBBB", Assert.Single(ScriptRunner.Run(rom, Start, Carrying((Tea, 4))).Pages));
        Assert.Equal("AAAAAA", Assert.Single(ScriptRunner.Run(rom, Start, Carrying((Tea, 5))).Pages));
    }

    /// <summary>
    /// Nobody having handed a bag in reads as an empty one rather than as a refusal, which
    /// is what every caller in this project did before this milestone and what the ones that
    /// have no bag to give — the dump tools — still do.
    /// </summary>
    [Fact]
    public void AStateWithNoBagBehindItAnswersNoRatherThanThrowing()
    {
        Assert.Equal("BBBBBB", Assert.Single(ScriptRunner.Run(Guard(), Start).Pages));
        Assert.Equal(0, new ScriptState().Carried(Tea));
    }

    /// <summary>
    /// An item id that arrives in a variable, which is how several of these are written —
    /// the same reading <c>givemon</c>'s species and <c>hideobject</c>'s person already get.
    /// </summary>
    [Fact]
    public void AnItemIdHeldInAVariableIsResolvedLikeEveryOtherOne()
    {
        Rom rom = Image(
            (Start,
            [
                0x16, .. Word(0x8004), .. Word(Tea),            // setvar 0x8004 <- the item
                0x47, .. Word(0x8004), .. Word(1),
                0x21, .. Word(0x800D), .. Word(1),
                ScriptCommands.GotoIf, 0x01, .. At(Elsewhere),
                .. Says(SaysNo),
            ]),
            (Elsewhere, Says(SaysYes)),
            (SaysYes, Speech('A')),
            (SaysNo, Speech('B')));

        Assert.Equal("AAAAAA", Assert.Single(ScriptRunner.Run(rom, Start, Carrying((Tea, 1))).Pages));

        // And the variable is what is asked about, not the number 0x8004.
        Assert.Equal(
            "BBBBBB", Assert.Single(ScriptRunner.Run(rom, Start, Carrying((0x8004, 1))).Pages));
    }

    // ---- what the run says it asked --------------------------------------------------

    /// <summary>
    /// The question is recorded with its answer. A refusal is the only thing that says what
    /// a playthrough would have had to be holding, and it is invisible from the outside —
    /// the script simply says a different sentence.
    /// </summary>
    [Fact]
    public void EveryQuestionIsRecordedWithWhatItWasTold()
    {
        ItemAsked refused = Assert.Single(ScriptRunner.Run(Guard(Tea, 2), Start, Carrying()).ItemsAsked);

        Assert.Equal(Tea, refused.ItemId);
        Assert.Equal(2, refused.Count);
        Assert.False(refused.Carried);

        Assert.True(Assert.Single(
            ScriptRunner.Run(Guard(Tea, 2), Start, Carrying((Tea, 2))).ItemsAsked).Carried);
    }

    /// <summary>
    /// Item zero is not an item, for the reason giveitem says so: a script reaching this
    /// command with nothing loaded is doing something else with it, and reporting that as a
    /// question would put a shopping list in front of somebody who was never asked anything.
    /// </summary>
    [Fact]
    public void AskingForNothingIsNotAQuestion()
    {
        Assert.Empty(ScriptRunner.Run(Guard(0), Start, Carrying()).ItemsAsked);
    }

    // ---- the other half of a delivery ------------------------------------------------

    /// <summary>
    /// What a script takes away is reported rather than applied. A run is copied and not
    /// written through — the client runs one to decide whether to open a box at all — so a
    /// run that emptied the bag as it went would be right exactly once.
    /// </summary>
    [Fact]
    public void WhatIsTakenAwayIsReportedAndNotDoneHere()
    {
        var bag = new Bag();
        bag.Add(Parcel);

        Rom rom = Image((Start, [0x45, .. Word(Parcel), .. Word(1), ScriptCommands.End]));

        var state = new ScriptState { CountOfItem = bag.CountOf };

        ScriptRun run = ScriptRunner.Run(rom, Start, state);

        Assert.Equal(Parcel, run.TakesItem);
        Assert.Equal(1, run.TakesCount);

        // Still there, and the same answer a second time. Whoever holds the bag applies it.
        Assert.Equal(1, bag.CountOf(Parcel));
        Assert.Equal(Parcel, ScriptRunner.Run(rom, Start, state).TakesItem);
    }

    /// <summary>Taking nothing away is not taking something away.</summary>
    [Fact]
    public void AScriptThatTakesNothingReportsNothing()
    {
        Rom rom = Image((Start, [0x45, .. Word(0), .. Word(1), ScriptCommands.End]));

        Assert.Null(ScriptRunner.Run(rom, Start).TakesItem);
    }

    // ---- the state carries the question across a copy ---------------------------------

    /// <summary>
    /// A derived state still knows the bag. Everything else on that list — the player's
    /// name, the rival's, the two naming delegates — is there because a caller forgot one
    /// once, and a copy that has forgotten the bag answers no to every door in the game
    /// while looking exactly like a copy that answered honestly.
    /// </summary>
    [Fact]
    public void ACopiedStateStillKnowsWhatIsCarried()
    {
        ScriptState state = Carrying((Tea, 3));

        Assert.Equal(3, state.Copy().Carried(Tea));
        Assert.Equal(3, state.WithParty([[1]]).Carried(Tea));

        // And the runner copies before it runs, which is where it would be lost.
        Assert.Equal(
            "AAAAAA", Assert.Single(ScriptRunner.Run(Guard(), Start, state.WithParty([[1]])).Pages));
    }

    // ---- and the playthrough, which is what all of it is for ---------------------------

    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// Something one person hands over is in hand at the next. The bag is one bag for the
    /// whole run and it is written as the pass goes rather than between passes — a ball on
    /// one side of a map is carried to the door on the other side of it.
    /// </summary>
    [Fact]
    public void WhatOnePersonHandsOverIsCarriedToTheNext()
    {
        MapData start = Room("1.0") with
        {
            Objects =
            [
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                new MapObject(2, 1, 2, 1, Direction.Down, 0, false) { ScriptAddress = 0x2000 },
            ],
        };

        var asked = new List<(int ItemId, bool Carried)>();

        Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, bag) =>
            {
                if (address == 0x1000) return Nothing with { Gets = (TestRules.PotionItem, 1) };

                asked.Add((TestRules.PotionItem, bag.Has(TestRules.PotionItem)));

                return Nothing;
            });

        Assert.NotEmpty(asked);
        Assert.Contains(asked, a => a.Carried);
    }

    /// <summary>
    /// And what a script takes away leaves the bag. Until this, a playthrough that had ever
    /// been handed the parcel was still holding it at the end of the game — a save whose own
    /// inventory cannot justify the flags beside it, which is the same fault co-op step four
    /// is open on.
    /// </summary>
    [Fact]
    public void WhatIsTakenAwayLeavesTheBag()
    {
        MapData start = Room("1.0") with
        {
            Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],
        };

        var handed = false;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) =>
            {
                if (handed) return Nothing with { Takes = (TestRules.PotionItem, 1) };

                handed = true;

                return Nothing with { Gets = (TestRules.PotionItem, 1) };
            });

        Assert.DoesNotContain(played.Carried, e => e.ItemId == TestRules.PotionItem);
    }

    /// <summary>
    /// A key item is held once however many times the script that hands it over is reached.
    /// Every reachable script is run on every pass, so without this the bag ends the game
    /// holding ninety-nine of a thing there is one of in the world.
    /// </summary>
    [Fact]
    public void AKeyItemIsHeldOnceHoweverManyTimesItIsHandedOver()
    {
        MapData start = Room("1.0") with
        {
            Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],
        };

        var passes = 0;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) =>
            {
                passes++;

                // Something new every pass, so the loop keeps going and the gift is
                // reached again and again.
                return new PlayedScript([passes], [], [], [], null, null)
                {
                    Gets = (TestRules.BicycleItem, 1),
                };
            });

        Assert.True(passes > 1, "the gift has to be reached more than once for this to mean anything");
        Assert.Equal(1, Assert.Single(played.Carried, e => e.ItemId == TestRules.BicycleItem).Count);

        // The control: something ordinary does stack, so the cap above is the rules
        // answering rather than the bag refusing everything after the first.
        Assert.True(Bag.MaxStack > 1);
    }

    /// <summary>
    /// A ball on the floor is picked up once. The cartridge sets the flag that takes it off
    /// the map inside the routine that hands the item over — code this project cannot follow
    /// — so it has to be done here from the object's own record, or every ball in the world
    /// is picked up again on every pass.
    /// </summary>
    [Fact]
    public void SomethingOnTheFloorIsPickedUpOnceAndIsThenGone()
    {
        const int hides = 0x260;

        MapData start = Room("1.0") with
        {
            Objects =
            [
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }
                    with { GivesItemId = TestRules.PotionItem, GivesCount = 1, HiddenBy = hides },
                new MapObject(2, 1, 2, 1, Direction.Down, 0, false) { ScriptAddress = 0x2000 },
            ],
        };

        var taken = 0;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, _) =>
            {
                if (address == 0x1000)
                {
                    taken++;

                    return Nothing with { Gets = (TestRules.PotionItem, 1) };
                }

                // Somebody who opens something new every pass, so the loop does not stop
                // before the ball would have been reached a second time.
                return new PlayedScript([0x300 + taken], [], [], [], null, null);
            });

        Assert.Equal(1, taken);
        Assert.Contains(hides, played.Flags);
        Assert.Equal(1, Assert.Single(played.Carried, e => e.ItemId == TestRules.PotionItem).Count);
    }

    /// <summary>
    /// A person a script takes off the map stops being in the way. <c>hideobject</c> has been
    /// read into <c>ScriptRun.Hides</c> for milestones and thrown away by everything that
    /// walks — so a guard who steps out of a doorway was, to the walker, standing in it
    /// forever, however the conversation had gone.
    /// </summary>
    [Fact]
    public void SomebodyAScriptRemovesIsNoLongerInTheDoorway()
    {
        MapData near = Room("1.0") with
        {
            Warps = [new Warp(3, 1, 0, "1.1")],

            // Rooted on the door itself, which is what the walker refuses to walk through.
            Objects =
            [
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                new MapObject(2, 1, 3, 1, Direction.Down, 0, false),
            ],
        };

        MapData far = Room("1.1") with { Warps = [new Warp(1, 1, 0, "1.0")] };

        var world = new WorldData([near, far]);

        // Standing there, the door is shut.
        Attempt stuck = Autoplayer.Play(world, "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.DoesNotContain("1.1", stuck.Reached);

        // Taken off the map by the person beside him, it is not.
        Attempt opened = Autoplayer.Play(
            world, "1.0", TestRules.All,
            (address, _, _) => address == 0x1000 ? Nothing with { Hides = [2] } : Nothing);

        Assert.Contains("1.1", opened.Reached);
        Assert.Contains(("1.0", 2), opened.Removed);
    }

    /// <summary>
    /// And the loop does not stop on the pass that only picked something up.
    /// <para>
    /// The settle test is "did this pass open anything", and picking something up opens
    /// nothing — the door it unlocks is asked about by a script on the <em>next</em> pass.
    /// Left out of that test, the loop stops one pass before the bag is ever used, and every
    /// line above buys precisely nothing.
    /// </para>
    /// </summary>
    [Fact]
    public void APassThatOnlyPickedSomethingUpIsNotTheEnd()
    {
        MapData start = Room("1.0") with
        {
            Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],
        };

        var seen = 0;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) =>
            {
                seen++;

                // One thing on the first pass and nothing after it. Nothing else changes,
                // so the only reason to run a second pass is the bag.
                return seen == 1 ? Nothing with { Gets = (TestRules.PotionItem, 1) } : Nothing;
            });

        Assert.True(played.Passes > 1, "the pass that filled the bag must not be the last one");
        Assert.Equal(StoppedBecause.NothingMoreOpened, played.Stopped);
    }

    /// <summary>
    /// Everything asked for and not carried comes back as a list. This is the number that
    /// makes a shut door actionable: not "it stopped at SAFFRON" but "SAFFRON wanted one of
    /// item 0x155 and it had none".
    /// </summary>
    [Fact]
    public void WhatItWasAskedForAndDidNotHaveComesBackAsAList()
    {
        MapData start = Room("1.0") with
        {
            Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],
        };

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing with { Asked = [(Tea, 2, false), (Parcel, 1, true)] });

        Wanted want = Assert.Single(played.Refused);

        Assert.Equal(Tea, want.ItemId);
        Assert.Equal(2, want.Count);
        Assert.Equal("1.0", want.MapId);

        // And the one it was carrying is not on the list, which is the whole distinction.
        Assert.DoesNotContain(played.Refused, w => w.ItemId == Parcel);
    }
}
