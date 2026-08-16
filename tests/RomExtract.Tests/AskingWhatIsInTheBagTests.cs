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

    /// <summary>
    /// And it answers nothing, unlike the two commands beside it.
    /// <para>
    /// Both of those write the result variable on evidence — the compare that follows them
    /// and the bag-full line on the arm that reads zero. Nothing says what a script is told
    /// after taking something away, or whether anybody asks, so nothing is written. This is
    /// the guard for a number that was invented once to match the neighbours.
    /// </para>
    /// </summary>
    [Fact]
    public void AndTakingSomethingAwayAnswersNothingBecauseNothingSaysItDoes()
    {
        Rom rom = Image(
            (Start,
            [
                0x45, .. Word(Parcel), .. Word(1),
                0x21, .. Word(0x800D), .. Word(1),
                ScriptCommands.GotoIf, 0x01, .. At(Elsewhere),
                .. Says(SaysNo),
            ]),
            (Elsewhere, Says(SaysYes)),
            (SaysYes, Speech('A')),
            (SaysNo, Speech('B')));

        Assert.Equal("BBBBBB", Assert.Single(ScriptRunner.Run(rom, Start).Pages));
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

    // ---- and a real player, who is the point of the whole thing -----------------------

    /// <summary>
    /// A player's own script state answers from the bag they are holding now.
    /// <para>
    /// The instrument opening SAFFRON and a player opening SAFFRON are two different
    /// things, and only the second one matters. Asked through the player rather than
    /// handed the bag, because the answer has to change when something is picked up —
    /// a state built with a copy of the bag answers for the bag they signed in with.
    /// </para>
    /// </summary>
    [Fact]
    public void APlayersOwnScriptStateAnswersFromTheBagTheyAreHoldingNow()
    {
        const string town = "1.0";

        var world = new GameWorld(
            new WorldData([new MapData(town, "PALLET TOWN", 8, 8, new byte[64])]), town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(town, 3, 4));

        Assert.Equal(0, player.Script.Carried(TestRules.PotionItem));

        player.Bag.Add(TestRules.PotionItem, 2);

        Assert.Equal(2, player.Script.Carried(TestRules.PotionItem));

        player.Bag.Remove(TestRules.PotionItem, 2);

        Assert.Equal(0, player.Script.Carried(TestRules.PotionItem));
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
    /// And somebody who merely hands something over is not deleted for it.
    /// <para>
    /// The decoy for the rule above, and it was needed: setting the hide flag on anybody
    /// whose script gave something failed no test at all, because the fixture had only
    /// balls in it. Nearly every gift in this game comes from a person who is still there
    /// afterwards — the aides, the shop, the man on NUGGET BRIDGE — and most of them carry
    /// a hide flag of their own for reasons that have nothing to do with the gift.
    /// </para>
    /// <para>
    /// <c>CanBeTakenAway</c> is what separates the two, and it is the world file's own
    /// predicate: gives something <em>and</em> has a flag to vanish behind. Asked rather
    /// than re-decided, because the walker already walks through exactly that set.
    /// </para>
    /// </summary>
    [Fact]
    public void SomebodyWhoHandsSomethingOverIsStillThereAfterwards()
    {
        const int hides = 0x261;

        MapData start = Room("1.0") with
        {
            Objects =
            [
                // A hide flag, and nothing on the floor: this is a person, not a ball.
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }
                    with { HiddenBy = hides },
            ],
        };

        var spokenTo = 0;
        var passes = 0;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) =>
            {
                spokenTo++;

                return new PlayedScript([0x500 + passes++], [], [], [], null, null)
                {
                    Gets = (TestRules.PotionItem, 1),
                };
            });

        Assert.True(passes > 1, "there has to be a second pass for this to mean anything");
        Assert.True(spokenTo > 1, "he handed something over; he did not stop existing");
        Assert.DoesNotContain(hides, played.Flags);
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
    /// And the walk that <em>chooses whose script to run</em> is told too, which is a
    /// separate rule and was the one nothing could fail.
    /// <para>
    /// Breaking the removal out of the per-pass walk and leaving it in the last one failed
    /// no test at all: the reported reach came from the final walk, so the door read as open
    /// while nobody behind it had ever been spoken to. That is the whole game — a door that
    /// opens and opens nothing is not a door.
    /// </para>
    /// </summary>
    [Fact]
    public void AndTheDoorOpensInTimeForWhoeverIsBehindIt()
    {
        const int behind = 0x999;

        MapData near = Room("1.0") with
        {
            Warps = [new Warp(3, 1, 0, "1.1")],
            Objects =
            [
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                new MapObject(2, 1, 3, 1, Direction.Down, 0, false),
            ],
        };

        MapData far = Room("1.1") with
        {
            Warps = [new Warp(1, 2, 0, "1.0")],
            Objects = [new MapObject(3, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x3000 }],
        };

        Attempt played = Autoplayer.Play(
            new WorldData([near, far]),
            "1.0",
            TestRules.All,
            (address, _, _) => address switch
            {
                0x1000 => Nothing with { Hides = [2] },
                0x3000 => new PlayedScript([behind], [], [], [], null, null),
                _ => Nothing,
            });

        Assert.Contains(behind, played.Flags);
    }

    /// <summary>
    /// And nobody talks to him afterwards. Being hidden by a flag and being removed by a
    /// command are the same thing to a player and two different things in the file — only
    /// the first was ever asked about, so a person taken off the map went on holding a
    /// conversation from wherever he had gone.
    /// </summary>
    [Fact]
    public void SomebodyAScriptRemovesIsNotTalkedToAgain()
    {
        MapData start = Room("1.0") with
        {
            // The one who gets removed comes first, so he is talked to once before the
            // person beside him takes him off the map. Reading the other order proves
            // nothing: he would be skipped on the pass he was removed on and the test
            // would pass whether or not later passes remembered.
            Objects =
            [
                new MapObject(2, 1, 2, 1, Direction.Down, 0, false) { ScriptAddress = 0x2000 },
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
            ],
        };

        var spokenTo = 0;
        var passes = 0;

        Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, _) =>
            {
                if (address == 0x2000)
                {
                    spokenTo++;

                    return Nothing;
                }

                // Takes the other one off the map, and opens something new every pass so
                // the loop keeps running and would reach him again.
                return new PlayedScript([0x400 + passes++], [], [], [], null, null)
                {
                    Hides = [2],
                };
            });

        Assert.True(passes > 1, "there has to be a second pass for this to mean anything");
        Assert.Equal(1, spokenTo);
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

    /// <summary>
    /// And every refusal says where one could be got, in the world file's own terms.
    /// <para>
    /// The five ways a thing changes hands are listed apart rather than collapsed into
    /// "obtainable", because they are five different jobs. Something lying on the floor is
    /// walked onto and already works; something sold needs money and a shop; something
    /// behind a question needs an answer. A list that only said "yes, obtainable" would
    /// hide the one thing worth knowing.
    /// </para>
    /// </summary>
    [Fact]
    public void AndEachRefusalSaysWhereOneCouldHaveBeenGot()
    {
        MapData start = Room("1.0") with
        {
            Objects =
            [
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },

                // Sold by somebody standing right here.
                new MapObject(2, 1, 2, 1, Direction.Down, 0, false, Sells: [Tea]),
            ],
        };

        // And lying on the floor somewhere it never got to.
        MapData far = Room("1.1") with
        {
            Objects =
            [
                new MapObject(3, 1, 1, 1, Direction.Down, 0, false)
                    with { GivesItemId = Tea, HiddenBy = 0x262 },
            ],
        };

        Attempt played = Autoplayer.Play(
            new WorldData([start, far]),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing with { Asked = [(Tea, 1, false)] });

        Wanted want = Assert.Single(played.Refused);

        Assert.Equal(2, want.Sources.Count);

        FoundAt sold = Assert.Single(want.Sources, s => s.How == "sold");

        Assert.Equal("1.0", sold.MapId);
        Assert.True(sold.Reached, "the shelf is on the map it is standing on");

        FoundAt floor = Assert.Single(want.Sources, s => s.How == "lying there");

        Assert.Equal("1.1", floor.MapId);
        Assert.False(floor.Reached, "there is no way into 1.1 at all");
    }

    /// <summary>
    /// And nothing at all is the sharpest answer of the three.
    /// <para>
    /// The decoy for the rule above, and it is the case that matters most: an empty list
    /// means nothing on any map in the game hands one over, so whatever produces it is
    /// behind a routine this project cannot run and no amount of walking will ever find it.
    /// A source list that quietly reported something for an item nobody stocks would turn
    /// that into "go and look harder".
    /// </para>
    /// </summary>
    [Fact]
    public void SomethingNobodyInTheWorldHandsOverComesBackWithNowhere()
    {
        MapData start = Room("1.0") with
        {
            Objects =
            [
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                new MapObject(2, 1, 2, 1, Direction.Down, 0, false, Sells: [Parcel]),
            ],
        };

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing with { Asked = [(Tea, 1, false)] });

        Assert.Empty(Assert.Single(played.Refused).Sources);
    }

    /// <summary>
    /// And a source it never reached says how to get there, walked backwards.
    /// <para>
    /// Forwards from the player, everything past the first shut door is one undifferentiated
    /// cloud of two hundred-odd maps. Backwards from the thing you actually want, the first
    /// map in the chain that <em>was</em> reached is the door to go and open, and there is
    /// exactly one of it.
    /// </para>
    /// </summary>
    [Fact]
    public void ASourceItNeverReachedSaysHowToGetThere()
    {
        // Standing on 1.0, blocked out of 1.1 by somebody rooted on the door, and the shop
        // is one room further on again.
        MapData start = Room("1.0") with
        {
            Warps = [new Warp(3, 1, 0, "1.1")],
            Objects =
            [
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                new MapObject(2, 1, 3, 1, Direction.Down, 0, false),
            ],
        };

        MapData middle = Room("1.1") with
        {
            Warps = [new Warp(1, 1, 0, "1.0"), new Warp(2, 2, 0, "1.2")],
        };

        MapData shop = Room("1.2") with
        {
            Warps = [new Warp(1, 1, 1, "1.1")],
            Objects = [new MapObject(1, 1, 2, 1, Direction.Down, 0, false, Sells: [Tea])],
        };

        Attempt played = Autoplayer.Play(
            new WorldData([start, middle, shop]),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing with { Asked = [(Tea, 1, false)] });

        FoundAt sold = Assert.Single(Assert.Single(played.Refused).Sources);

        Assert.False(sold.Reached);
        Assert.Equal(["1.0", "1.1", "1.2"], sold.WayIn);
    }

    /// <summary>
    /// And the edge of a map is a way in, the same as a door is.
    /// <para>
    /// The decoy for the chain, and it was needed: half of this game's map graph is not
    /// doors at all, it is walking off the side of a route. Counting only warps left every
    /// outdoor map in the world looking like somewhere nothing leads to — and the run's
    /// three drinks are sold in a city, which is reached exactly that way.
    /// </para>
    /// </summary>
    [Fact]
    public void TheEdgeOfAMapIsAWayInTheSameAsADoorIs()
    {
        // Blocked out of 1.1 by somebody rooted on the door, exactly as before — so
        // everything past it is unreached and the chain is the only thing under test.
        MapData start = Room("1.0") with
        {
            Warps = [new Warp(3, 1, 0, "1.1")],
            Objects =
            [
                new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                new MapObject(2, 1, 3, 1, Direction.Down, 0, false),
            ],
        };

        // And the last step is an edge rather than a door. No warp joins these two at all.
        MapData middle = Room("1.1") with
        {
            Warps = [new Warp(1, 1, 0, "1.0")],
            Connections = [new MapConnection(ConnectionSide.Right, 0, "1.2")],
        };

        MapData shop = Room("1.2") with
        {
            Objects = [new MapObject(1, 1, 2, 1, Direction.Down, 0, false, Sells: [Tea])],
        };

        Attempt played = Autoplayer.Play(
            new WorldData([start, middle, shop]),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing with { Asked = [(Tea, 1, false)] });

        FoundAt sold = Assert.Single(Assert.Single(played.Refused).Sources);

        Assert.Equal(["1.0", "1.1", "1.2"], sold.WayIn);
    }

    /// <summary>
    /// And a map nothing leads to says so, rather than saying nothing.
    /// <para>
    /// The decoy, and the two answers could not be further apart: one shut door is an
    /// afternoon, and a map no door on any map in the game leads to is either a hole in the
    /// export or a room the cartridge reaches by some means this project has never read.
    /// Both come back as an empty chain, so <c>Reached</c> is what tells them apart — which
    /// is why the way in is not a nullable.
    /// </para>
    /// </summary>
    [Fact]
    public void AndAMapNothingLeadsToSaysThatInstead()
    {
        MapData start = Room("1.0") with
        {
            Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],
        };

        // No door anywhere points at it, and it has one pointing back — which is exactly
        // the shape that would read as "there is a way in" to anything walking forwards.
        MapData adrift = Room("1.9") with
        {
            Warps = [new Warp(1, 1, 0, "1.0")],
            Objects = [new MapObject(1, 1, 2, 1, Direction.Down, 0, false, Sells: [Tea])],
        };

        Attempt played = Autoplayer.Play(
            new WorldData([start, adrift]),
            "1.0",
            TestRules.All,
            (_, _, _) => Nothing with { Asked = [(Tea, 1, false)] });

        FoundAt sold = Assert.Single(Assert.Single(played.Refused).Sources);

        Assert.False(sold.Reached);
        Assert.Empty(sold.WayIn);
    }
}
