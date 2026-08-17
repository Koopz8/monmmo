using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The boat, which is neither a square nor a script.
/// <para>
/// The archipelago is a second world with no warp, no map edge and no scripted door joining
/// it to the first. What crosses is a routine, and the scripts around it say everything the
/// routine will not: ten docks writing ten different numbers into one argument slot, and one
/// of them asking a question before it sails.
/// </para>
/// <para>
/// That question is <c>checkflag F</c> and, within a few commands, <c>checkitem I</c> — the
/// same command milestone 172 taught the runner to answer. Either opens the boat. The item
/// half of the cartridge's own "or" has been unanswerable for the whole life of this project
/// and stopped being so the moment there was a bag to ask.
/// </para>
/// <para>
/// <b>Riding at all is read. Where it goes is modelled.</b> Which places a ticket is worth is
/// inside the routine that draws the menu, so the walk joins every dock to every other — an
/// upper bound, which is why it is off unless somebody asks for it.
/// </para>
/// </summary>
public class TakingTheBoatTests
{
    private const int Ticket = 0x0160;
    private const int TicketFlag = 0x0820;
    private const int Drink = 0x001A;

    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// A mainland with a jetty, an island with a jetty, and nothing whatsoever joining them.
    /// </summary>
    private static WorldData Archipelago(params FerryPass[] passes) =>
        new(
        [
            Room("1.0") with
            {
                Ferry = new FerryDock(0, 1, 1, 1),
                Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],
            },
            Room("2.0") with
            {
                Ferry = new FerryDock(1, 1, 2, 2),
                Objects = [new MapObject(1, 1, 2, 1, Direction.Down, 0, false, Sells: [Drink])],
            },
        ])
        {
            FerryPasses = [.. passes],
        };

    private static Attempt Sail(WorldData world, bool boat, params (int ItemId, int Count)[] carrying)
    {
        var handed = false;

        return Autoplayer.Play(
            world,
            "1.0",
            TestRules.All,
            (_, _, _) =>
            {
                if (handed || carrying.Length == 0) return Nothing with { Asked = [(Drink, 1, false)] };

                handed = true;

                return Nothing with { Gets = carrying[0], Asked = [(Drink, 1, false)] };
            },
            null,
            boat);
    }

    // ---- the boat asks for nothing, and that is a correction ---------------------------

    /// <summary>
    /// The boat carries anybody who can reach a jetty, and this test replaces one that said
    /// the opposite.
    /// <para>
    /// The first version required a pass to sail at all, and the cartridge refuted it with
    /// evidence already in the repository: the two passes <c>Ferries</c> can read are
    /// MYSTICTICKET and AURORATICKET, and the dock numbering that same file derived says
    /// docks 9 and 10 are NAVEL ROCK and BIRTH ISLAND. Those are tickets for two particular
    /// destinations, and nothing on any map hands either of them over — so requiring one shut
    /// the whole archipelago behind an item that does not exist in ordinary play, and called
    /// the result a floor.
    /// </para>
    /// </summary>
    [Fact]
    public void APassNothingHandsOverDoesNotLockTheWholeArchipelago()
    {
        WorldData world = Archipelago(new FerryPass(TicketFlag, Ticket));

        Assert.Contains("2.0", Sail(world, boat: true).Reached);
    }

    /// <summary>
    /// And holding one changes nothing, which is the other half of the same correction: a
    /// pass is worth a destination this project cannot read, so it can neither open nor shut
    /// anything here.
    /// </summary>
    [Fact]
    public void AndHoldingOneChangesNothingEither()
    {
        WorldData world = Archipelago(new FerryPass(TicketFlag, Ticket));

        Assert.Equal(
            Sail(world, boat: true).Reached.Count,
            Sail(world, boat: true, (Ticket, 1)).Reached.Count);
    }

    /// <summary>A ferry that asks for nothing carries anybody, which it always did.</summary>
    [Fact]
    public void AFerryThatAsksForNothingCarriesAnybody()
    {
        Assert.Contains("2.0", Sail(Archipelago(), boat: true).Reached);
    }

    // ---- and the floor, which is the reason it is off ---------------------------------

    /// <summary>
    /// With the boat off, nothing sails. The walk is what it has always been: a floor.
    /// Switching it on joins every dock to every other and asks for nothing, which is an
    /// upper bound in both directions at once — so a run that took it silently would be
    /// neither floor nor ceiling and would mean nothing at all.
    /// </summary>
    [Fact]
    public void WithTheBoatOffNothingSails()
    {
        WorldData world = Archipelago(new FerryPass(TicketFlag, Ticket));

        Attempt played = Sail(world, boat: false, (Ticket, 1));

        Assert.DoesNotContain("2.0", played.Reached);
        Assert.False(played.RodeTheBoat);
    }

    /// <summary>And it does not claim a pass it has not got.</summary>
    [Fact]
    public void ARunWithNoTicketDoesNotClaimOne()
    {
        Attempt played = Sail(Archipelago(new FerryPass(TicketFlag, Ticket)), boat: false);

        Assert.False(played.HeldATicket);
    }

    // ---- and what a ticket is, which is the whole point of saying it has none -----------

    /// <summary>
    /// A run that has no ticket says what a ticket <em>is</em>, and where one comes from.
    /// <para>
    /// Otherwise the output is the shortest possible distance between an answer and being no
    /// further forward: it held no ticket, and nothing anywhere says what one looks like.
    /// The flag and the item are both read, and the item gets the same treatment every other
    /// refusal gets — everywhere in the world one could be got, and the way in to each.
    /// </para>
    /// </summary>
    [Fact]
    public void ARunWithNoTicketSaysWhatOneIsAndWhereItComesFrom()
    {
        var world = new WorldData(
        [
            .. Archipelago(new FerryPass(TicketFlag, Ticket)).Maps,

            // Somebody, somewhere it never got to, who hands one over.
            Room("5.0") with
            {
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false)
                        with { GivesItemId = Ticket, HiddenBy = 0x263 },
                ],
            },
        ])
        {
            FerryPasses = [new FerryPass(TicketFlag, Ticket)],
        };

        FerryTicket ticket = Assert.Single(Sail(world, boat: false).Tickets);

        Assert.Equal(TicketFlag, ticket.Flag);
        Assert.Equal(Ticket, ticket.ItemId);
        Assert.False(ticket.Opens);

        FoundAt from = Assert.Single(ticket.Sources);

        Assert.Equal("5.0", from.MapId);
        Assert.Equal("lying there", from.How);
        Assert.False(from.Reached);
    }

    /// <summary>
    /// And a ticket it is holding is not put on a shopping list. The decoy for the rule
    /// above: a list that named the ticket whatever the bag held would send somebody after
    /// something already in their pocket.
    /// </summary>
    [Fact]
    public void ATicketAlreadyHeldIsNotAskedFor()
    {
        FerryTicket ticket = Assert.Single(
            Sail(Archipelago(new FerryPass(TicketFlag, Ticket)), boat: false, (Ticket, 1)).Tickets);

        Assert.True(ticket.Opens);
        Assert.True(ticket.Carried);
        Assert.False(ticket.FlagSet);
    }

    // ---- what a dock is, and what it is not -------------------------------------------

    /// <summary>
    /// Somewhere with no jetty is not a port of call. Every dock joins every other dock and
    /// nothing else — a model that dropped passengers on any map at all would open the world
    /// and prove nothing.
    /// </summary>
    [Fact]
    public void SomewhereWithNoJettyIsNotAPortOfCall()
    {
        var world = new WorldData(
        [
            Room("1.0") with
            {
                Ferry = new FerryDock(0, 1, 1, 1),
                Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],
            },

            // No jetty, and joined to nothing.
            Room("9.9") with
            {
                Objects = [new MapObject(1, 1, 2, 1, Direction.Down, 0, false, Sells: [Drink])],
            },
        ]);

        Assert.DoesNotContain("9.9", Sail(world, boat: true).Reached);
    }

    /// <summary>
    /// And a jetty in a wall takes no passengers. The dock's own record says where somebody
    /// arriving by sea is put down; landing them on a square nothing can stand on would make
    /// a fault in the export read as a map that opened.
    /// </summary>
    [Fact]
    public void AJettyInAWallTakesNoPassengers()
    {
        // Solid everywhere. The arrival square the dock names cannot be stood on.
        var solid = new byte[16];
        Array.Fill(solid, (byte)1);

        var world = new WorldData(
        [
            Room("1.0") with
            {
                Ferry = new FerryDock(0, 1, 1, 1),
                Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],
            },
            new MapData("2.0", "2.0", 4, 4, solid) { Ferry = new FerryDock(1, 1, 2, 2) },
        ]);

        Assert.DoesNotContain("2.0", Sail(world, boat: true).Reached);
    }
}

/// <summary>
/// The other way a doorway opens: somebody steps aside rather than vanishing.
/// <para>
/// The eighth wall in the drinks chain, and the last one. With the drinks bought, the boat
/// ridden and every yes-or-no answered, SAFFRON's three doors still read "somebody is standing
/// in the way" — because the guard given his drink is not removed. He takes a step to one
/// side, and a walker that has only ever asked "is anybody on this square" sees him in the
/// doorway forever however the conversation went.
/// </para>
/// <para>
/// Where he ends up is <b>read</b>. <c>applymovement</c>'s steps are the cartridge's own bytes
/// and what they mean was derived by walking every list across every map and counting who
/// ended up inside a wall. A step this project does not model is stood still through.
/// </para>
/// </summary>
public class SteppingAsideTests
{
    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>A guard on the door, and somebody beside him to talk to.</summary>
    private static WorldData Gate() =>
        new(
        [
            Room("1.0") with
            {
                Warps = [new Warp(3, 1, 0, "1.1")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                    new MapObject(2, 1, 3, 1, Direction.Down, 0, false),
                ],
            },
            Room("1.1") with { Warps = [new Warp(1, 1, 0, "1.0")] },
        ]);

    private static Attempt Play(Func<uint, PlayedScript> script) =>
        Autoplayer.Play(Gate(), "1.0", TestRules.All, (address, _, _) => script(address));

    /// <summary>
    /// One step to the left and the door is open. The same world, the same guard, and the
    /// difference is a movement list nothing used to read.
    /// </summary>
    [Fact]
    public void SomebodyWhoStepsAsideIsNoLongerInTheDoorway()
    {
        Assert.DoesNotContain("1.1", Play(_ => Nothing).Reached);

        Attempt aside = Play(address => address == 0x1000
            ? Nothing with { Walked = [(2, -1, 0)] }
            : Nothing);

        Assert.Contains("1.1", aside.Reached);
        Assert.Contains(("1.0", 2), aside.Moved);
    }

    /// <summary>
    /// And the square he steps onto is his now. A walker that opened the old square without
    /// shutting the new one would let two people through one gap.
    /// </summary>
    [Fact]
    public void AndTheSquareHeStepsOntoIsBlockedInstead()
    {
        // Sideways onto (2,1), which is between the door and everything else in this room.
        var world = new WorldData(
        [
            new MapData("1.0", "1.0", 4, 1, new byte[4])
            {
                Warps = [new Warp(3, 0, 0, "1.1")],
                Objects =
                [
                    new MapObject(1, 1, 0, 0, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                    new MapObject(2, 1, 3, 0, Direction.Down, 0, false),
                ],
            },
            new MapData("1.1", "1.1", 4, 1, new byte[4]) { Warps = [new Warp(1, 0, 0, "1.0")] },
        ]);

        // He steps off the door and onto the only square leading to it, which is no help.
        Attempt played = Autoplayer.Play(
            world,
            "1.0",
            TestRules.All,
            (address, _, _) => address == 0x1000
                ? Nothing with { Walked = [(2, -1, 0)] }
                : Nothing);

        Assert.Contains(("1.0", 2), played.Moved);
        Assert.DoesNotContain("1.1", played.Reached);
    }

    /// <summary>
    /// Somebody walked twice ends up where both walks put them, rather than where the second
    /// one alone would. A scene is a sequence and the file's record is only where it started.
    /// <para>
    /// Two doors, one behind the other, so the difference is visible: compounding leaves him
    /// clear of both, and starting from his original square each time parks him on the second.
    /// The first version of this test walked him twice in the same direction past a single
    /// door, which opens whether the walks compound or not and proved nothing.
    /// </para>
    /// </summary>
    [Fact]
    public void TwoWalksCompound()
    {
        var world = new WorldData(
        [
            Room("1.0") with
            {
                Warps = [new Warp(3, 1, 0, "1.1"), new Warp(3, 2, 0, "1.2")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                    new MapObject(2, 1, 3, 1, Direction.Down, 0, false),
                ],
            },
            Room("1.1") with { Warps = [new Warp(1, 1, 0, "1.0")] },
            Room("1.2") with { Warps = [new Warp(1, 1, 0, "1.0")] },
        ]);

        var steps = 0;

        Attempt played = Autoplayer.Play(
            world,
            "1.0",
            TestRules.All,
            (address, _, _) => address == 0x1000 && steps++ < 2
                ? Nothing with { Walked = [(2, 0, 1)] }
                : Nothing);

        Assert.True(steps >= 2, "the second walk has to happen for this to mean anything");

        // Down twice from (3,1) is (3,3). Down once from (3,1), twice over, is (3,2) — which
        // is the second door.
        Assert.Contains("1.1", played.Reached);
        Assert.Contains("1.2", played.Reached);
    }

    /// <summary>
    /// And a walk applied to somebody who is not on this map moves nobody. A person id is a
    /// number on one map's own list, so the same id means a different person elsewhere.
    /// </summary>
    [Fact]
    public void AWalkForSomebodyNotOnThisMapMovesNobody()
    {
        Attempt played = Play(address => address == 0x1000
            ? Nothing with { Walked = [(99, -1, 0)] }
            : Nothing);

        Assert.Empty(played.Moved);
        Assert.DoesNotContain("1.1", played.Reached);
    }
}

/// <summary>
/// Naming whoever is in the doorway, and what talking to them came to.
/// <para>
/// "Somebody is standing in the way" was true of SAFFRON's three doors for eight measurements
/// running and named nobody. Which person, and what happens when you talk to them, are the two
/// things that turn it from an observation into a job — and they lived in different halves of
/// the same output with nothing joining them up.
/// </para>
/// </summary>
public class WhoIsInTheDoorwayTests
{
    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    private const int Drink = 0x001A;

    /// <summary>A guard rooted on a door, and somebody beside him.</summary>
    private static WorldData Gate() =>
        new(
        [
            Room("1.0") with
            {
                Warps = [new Warp(3, 1, 0, "1.1")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },
                    new MapObject(2, 1, 3, 1, Direction.Down, 7, false) { ScriptAddress = 0x2000 },
                ],
            },
            Room("1.1") with { Warps = [new Warp(1, 1, 0, "1.0")] },
        ]);

    /// <summary>
    /// The blocker is named, with his number, his square and how he moves. Without those a
    /// shut door is a fact nobody can act on.
    /// </summary>
    [Fact]
    public void TheBlockerIsNamed()
    {
        Attempt played = Autoplayer.Play(Gate(), "1.0", TestRules.All, (_, _, _) => Nothing);

        ShutDoor door = Assert.Single(played.ShutDoors, d => d.ToMapId == "1.1");

        Assert.True(door.SomebodyIsInTheWay);

        Blocker who = Assert.Single(door.Who);

        Assert.Equal(2, who.LocalId);
        Assert.Equal(new GridPosition(3, 1), who.Square);
        Assert.Equal(7, who.MovementType);
    }

    /// <summary>
    /// And what talking to him came to travels with him. A guard who asks for a drink and a
    /// man with nothing to say are the same line otherwise, and they are not the same job.
    /// </summary>
    [Fact]
    public void WhatTalkingToHimCameToTravelsWithHim()
    {
        Attempt played = Autoplayer.Play(
            Gate(),
            "1.0",
            TestRules.All,
            (address, _, _) => address == 0x2000
                ? Nothing with { Asked = [(Drink, 1, false)] }
                : Nothing);

        Blocker who = Assert.Single(Assert.Single(played.ShutDoors, d => d.ToMapId == "1.1").Who);

        Assert.True(who.Talked);
        Assert.Equal(Drink, Assert.Single(who.AskedFor));
    }

    /// <summary>
    /// And somebody with nothing to say at all says so, which is a different finding entirely.
    /// <para>
    /// A guard who asks for a drink is a gate with a price on it. Somebody rooted in a doorway
    /// with no script whatsoever cannot be moved by talking to them at all, and whatever opens
    /// that door is somewhere else on the map. The two read identically without this.
    /// </para>
    /// <para>
    /// Note what this is <em>not</em>: a person the walk could not reach is never reported as
    /// being in the way in the first place, because it never bumped into them. Every blocker
    /// has a square beside it that was stood on — that is what makes it a blocker — so a
    /// blocker with a script has always been talked to, and the only way this can be false is
    /// having no script.
    /// </para>
    /// </summary>
    [Fact]
    public void SomebodyWithNothingToSayAtAllSaysSo()
    {
        var world = new WorldData(
        [
            Room("1.0") with
            {
                Warps = [new Warp(3, 1, 0, "1.1")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },

                    // No script. Nothing anybody says to him will ever move him.
                    new MapObject(2, 1, 3, 1, Direction.Down, 7, false),
                ],
            },
            Room("1.1") with { Warps = [new Warp(1, 1, 0, "1.0")] },
        ]);

        Attempt played = Autoplayer.Play(world, "1.0", TestRules.All, (_, _, _) => Nothing);

        Blocker who = Assert.Single(Assert.Single(played.ShutDoors, d => d.ToMapId == "1.1").Who);

        Assert.False(who.Talked);
        Assert.Empty(who.AskedFor);
    }

    /// <summary>
    /// And a blocker who asks the game something says which number.
    /// <para>
    /// "Talking to him does nothing" and "talking to him asks the game a question this project
    /// cannot ask, and takes the nothing arm" are a person with no part in the story and a
    /// wall with a number on it. The second is the number to hand to <c>--answer</c>, and
    /// without this line the two are the same sentence.
    /// </para>
    /// </summary>
    [Fact]
    public void ABlockerWhoAsksTheGameSomethingSaysWhichNumber()
    {
        Attempt played = Autoplayer.Play(
            Gate(),
            "1.0",
            TestRules.All,
            (address, _, _) => address == 0x2000
                ? new PlayedScript([], [], [], [0x187, 0x187, 0x159], null, null)
                : Nothing);

        Blocker who = Assert.Single(Assert.Single(played.ShutDoors, d => d.ToMapId == "1.1").Who);

        // Each routine once, however many times it was asked: this is a list of numbers to
        // try, not a tally.
        Assert.Equal([0x187, 0x159], who.Routines);
    }

    /// <summary>And somebody who asks nothing claims nothing.</summary>
    [Fact]
    public void AndSomebodyWhoAsksNothingClaimsNothing()
    {
        Attempt played = Autoplayer.Play(Gate(), "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.Empty(Assert.Single(Assert.Single(played.ShutDoors, d => d.ToMapId == "1.1").Who).Routines);
    }

    /// <summary>
    /// What takes the blocker off the map, which is on their own record and not in anything
    /// they do.
    /// <para>
    /// The four in the last four doorways have scripts with <b>no conditional in them at
    /// all</b> — nothing to wait on, no arm not taken, no routine asked. They are not moved by
    /// talking to them and they are not waiting on a question their script asks. They are
    /// moved by a flag written on the map's own object record and set somewhere else entirely,
    /// which is this cartridge's usual shape: only 7 of the 575 objects carrying a hide flag
    /// have a script that sets it.
    /// </para>
    /// </summary>
    [Fact]
    public void WhatTakesTheBlockerOffTheMapComesOffTheirOwnRecord()
    {
        var world = new WorldData(
        [
            Room("1.0") with
            {
                Warps = [new Warp(3, 1, 0, "1.1")],
                Objects =
                [
                    // The decoy, and the shape of every real map: somebody else on the map
                    // carrying a hide flag of their own. A blocker handed the first hide flag
                    // anybody on the map happens to carry is a flag number that reads clean
                    // and sends the next session to the wrong script.
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false)
                    {
                        ScriptAddress = 0x1000,
                        HiddenBy = 0x0035,
                    },
                    new MapObject(2, 1, 3, 1, Direction.Down, 7, false)
                    {
                        ScriptAddress = 0x2000,
                        HiddenBy = 0x0825,
                    },
                ],
            },
            Room("1.1") with { Warps = [new Warp(1, 1, 0, "1.0")] },
        ]);

        Attempt played = Autoplayer.Play(world, "1.0", TestRules.All, (_, _, _) => Nothing);

        Blocker who = Assert.Single(Assert.Single(played.ShutDoors, d => d.ToMapId == "1.1").Who);

        Assert.Equal(0x0825, who.HiddenBy);
    }

    /// <summary>
    /// And somebody nothing hides claims nothing. "No flag takes this person off the map" is
    /// a different and much worse finding than "this flag does" — it means the door opens some
    /// way nothing here has read — and a nought that means both is no answer at all.
    /// </summary>
    [Fact]
    public void AndSomebodyNoFlagHidesClaimsNoFlag()
    {
        Attempt played = Autoplayer.Play(Gate(), "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.Equal(0, Assert.Single(Assert.Single(played.ShutDoors, d => d.ToMapId == "1.1").Who).HiddenBy);
    }

    /// <summary>
    /// Which scripts actually ran, which is not the same question as which maps were reached.
    /// <para>
    /// <b>The question that comes after a flag number.</b> "The flag that opens SAFFRON is set
    /// by a trigger on <c>1.57</c>" is three different jobs wearing one sentence: a map never
    /// reached, a square on a reached map the walk never stood on, or a script that ran and
    /// stopped short of its own <c>setflag</c>. A trigger fires only for somebody standing
    /// exactly on it, so the middle case is real and common — and it is a walking problem,
    /// not a story one.
    /// </para>
    /// </summary>
    [Fact]
    public void OnlyTheScriptsItActuallyRanCountAsRun()
    {
        var world = new WorldData(
        [
            Room("1.0") with
            {
                Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],

                // The decoy: a trigger on this very map, on a square nothing can stand on.
                // Reached and never run are the two facts that must not collapse into one.
                Triggers = [new MapTrigger(9, 9, 0, 0, 0x3000)],
            },
        ]);

        Attempt played = Autoplayer.Play(world, "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.Contains("1.0", played.Reached);
        Assert.Contains(0x1000u, played.Ran);
        Assert.DoesNotContain(0x3000u, played.Ran);
    }

    /// <summary>
    /// Why a script stopped short survives the passes after the one it stopped on.
    /// <para>
    /// The same script runs on every pass, with a different bag and different flags behind it.
    /// Keeping only the last is keeping whichever pass happened to be last — and the reason a
    /// script did not finish is usually visible on one pass and gone by the next, because by
    /// then the run is walking past it with nothing left to say. Folded together instead:
    /// everything this script has ever managed, which is the honest ceiling.
    /// </para>
    /// </summary>
    [Fact]
    public void WhyAScriptStoppedShortSurvivesTheNextPass()
    {
        var times = 0;

        var world = new WorldData(
        [
            Room("1.0") with
            {
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 },

                    // Somebody who opens something on the first pass, so that there is a
                    // second pass at all. Without this the loop settles after one and the
                    // whole question of folding never arises — which is how this rule read
                    // as guarded while nothing could fail it.
                    new MapObject(2, 1, 3, 1, Direction.Down, 0, false) { ScriptAddress = 0x2000 },
                ],
            },
        ]);

        // Stops at a question the first time it is run, and has nothing to say ever after.
        Attempt played = Autoplayer.Play(
            world,
            "1.0",
            TestRules.All,
            (address, _, _) => address switch
            {
                0x2000 => Nothing with { FlagsSet = [0x0AAA] },
                _ when ++times == 1 => Nothing with { StoppedAtAQuestion = true, Specials = [0x0AB] },
                _ => Nothing,
            });

        Assert.True(times > 1, "the script has to run more than once for folding to mean anything");

        WhatRan did = played.Ran[0x1000];

        Assert.True(did.StoppedAtAQuestion);
        Assert.Equal([0x0AB], did.Routines);
    }

    /// <summary>
    /// And a door nobody is standing in names nobody. The list is only worth having if it is
    /// empty when it should be.
    /// </summary>
    [Fact]
    public void ADoorNobodyIsStandingInNamesNobody()
    {
        var world = new WorldData(
        [
            Room("1.0") with
            {
                // The door is fine; 1.1 is simply not joined back and has nothing in it.
                Warps = [new Warp(3, 1, 0, "1.9")],
                Objects = [new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = 0x1000 }],
            },
        ]);

        Attempt played = Autoplayer.Play(world, "1.0", TestRules.All, (_, _, _) => Nothing);

        Assert.All(played.ShutDoors, d => Assert.Empty(d.Who));
    }
}
