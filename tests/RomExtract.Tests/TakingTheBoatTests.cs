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

    // ---- the ticket, which is read ---------------------------------------------------

    /// <summary>
    /// The whole point, in one test: the same world, the same walk, and the difference is
    /// whether the bag holds a ticket.
    /// </summary>
    [Fact]
    public void TheItemHalfOfTheQuestionOpensTheBoat()
    {
        WorldData world = Archipelago(new FerryPass(TicketFlag, Ticket));

        Assert.DoesNotContain("2.0", Sail(world, boat: true).Reached);
        Assert.Contains("2.0", Sail(world, boat: true, (Ticket, 1)).Reached);
    }

    /// <summary>
    /// And the flag half opens it on its own, which is the cartridge's own "or". The two are
    /// asked one after the other on the same branch and either answer sails.
    /// </summary>
    [Fact]
    public void TheFlagHalfOpensItOnItsOwn()
    {
        WorldData world = Archipelago(new FerryPass(TicketFlag, Ticket));

        Attempt played = Autoplayer.Play(
            world,
            "1.0",
            TestRules.All,
            (_, _, _) => new PlayedScript([TicketFlag], [], [], [], null, null),
            null,
            true);

        Assert.Contains("2.0", played.Reached);
    }

    /// <summary>Some other item is not a ticket, which is the failure worth guarding.</summary>
    [Fact]
    public void SomethingElseInTheBagIsNotATicket()
    {
        WorldData world = Archipelago(new FerryPass(TicketFlag, Ticket));

        Assert.DoesNotContain("2.0", Sail(world, boat: true, (TestRules.PotionItem, 1)).Reached);
    }

    /// <summary>
    /// A ferry that asks for nothing is not a locked boat. An empty pass list is a fact about
    /// the world file — a cartridge whose sailor asks no questions — and refusing to sail on
    /// it would report an archipelago cut off by a ticket nobody sells.
    /// </summary>
    [Fact]
    public void AFerryThatAsksForNothingCarriesAnybody()
    {
        Assert.Contains("2.0", Sail(Archipelago(), boat: true).Reached);
    }

    // ---- and the floor, which is the reason it is off ---------------------------------

    /// <summary>
    /// With the boat off, the walk is what it has always been: a floor. Switching it on joins
    /// every dock to every other, which is an upper bound, so a run that took it silently
    /// would be neither floor nor ceiling and would mean nothing at all.
    /// </summary>
    [Fact]
    public void WithTheBoatOffNothingSailsHoweverManyTicketsAreHeld()
    {
        WorldData world = Archipelago(new FerryPass(TicketFlag, Ticket));

        Attempt played = Sail(world, boat: false, (Ticket, 1));

        Assert.DoesNotContain("2.0", played.Reached);

        // And it says so rather than looking like a run that simply never got there. Those
        // are the same output otherwise, and one of them is an afternoon's work.
        Assert.True(played.HeldATicket);
        Assert.False(played.RodeTheBoat);
    }

    /// <summary>
    /// And it does not claim a ticket it has not got. The nudge is only worth printing when
    /// switching the boat on would actually change something.
    /// </summary>
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
