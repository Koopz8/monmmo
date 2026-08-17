using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>What one script turned out to do, as far as playing the game goes.</summary>
/// <param name="FlagsSet">Flags it turned on.</param>
/// <param name="FlagsCleared">Flags it turned off.</param>
/// <param name="Teaches">Field moves it handed over, already translated from the item.</param>
/// <param name="Specials">Routines it asked for and did not get an answer from.</param>
/// <param name="Gives">
/// A creature it hands over and the level it names, if it hands one over. The level matters:
/// the first version took only the species and put everything in at five, so every gift in the
/// game arrived as a starter.
/// </param>
/// <param name="Fights">A trainer it picks a fight with, if it picks one.</param>
public sealed record PlayedScript(
    IReadOnlyList<int> FlagsSet,
    IReadOnlyList<int> FlagsCleared,
    IReadOnlyList<int> Teaches,
    IReadOnlyList<int> Specials,
    (int Species, int Level)? Gives,
    int? Fights)
{
    /// <summary>An item it handed over, and how many.</summary>
    public (int ItemId, int Count)? Gets { get; init; }

    /// <summary>An item it took away, and how many.</summary>
    public (int ItemId, int Count)? Takes { get; init; }

    /// <summary>
    /// People it took off the map, by their number on it.
    /// <para>
    /// Read and thrown away until now, by both this and the closure walk. It is how a
    /// guard stops being in a doorway: the script does not move him, it removes him —
    /// and a walker that never hears about it sees the same person standing there
    /// forever, however the conversation went.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Hides { get; init; } = [];

    /// <summary>
    /// Commands with no width that stopped this run's reading, by opcode.
    /// <para>
    /// <b>The half of the error bar that was missing.</b> A run reports the routines it could
    /// not answer, and it has never reported the commands it could not read. Those are not the
    /// same boundary: a routine is the game's own code and nothing here will ever follow it,
    /// while a command with no width is a gap in a table in this repository — the difference
    /// between "the world is this small" and "my reader stopped".
    /// </para>
    /// <para>
    /// One byte with no entry hid nineteen people on eleven maps, and every instrument that saw
    /// it reported a smaller world, cleanly, with no error anywhere.
    /// </para>
    /// </summary>
    public IReadOnlyList<byte> StoppedAt { get; init; } = [];

    /// <summary>What it asked the bag for, and what it was told.</summary>
    public IReadOnlyList<(int ItemId, int Count, bool Carried)> Asked { get; init; } = [];

    /// <summary>
    /// People this script walked, and where they ended up.
    /// <para>
    /// The other way somebody stops being in a doorway, and the one nothing here has ever
    /// modelled. A guard given his drink is not removed — he takes a step to one side, and to
    /// a walker that has only ever asked "is anybody on this square" he is in the doorway
    /// forever however the conversation went.
    /// </para>
    /// <para>
    /// Where they end up is <b>read</b>: the step bytes are the cartridge's own and what they
    /// mean was derived by walking every list across every map and counting who ended up
    /// inside a wall. A step this project does not model is stood still through, which is the
    /// same honest reading <c>DirectionOf</c> takes — being wrong visibly beats guessing.
    /// </para>
    /// </summary>
    /// <remarks>
    /// A displacement rather than a square. The script says who and how far; where they were
    /// standing to begin with is the map's record, or wherever an earlier scene left them, and
    /// only this side of the split knows that.
    /// </remarks>
    public IReadOnlyList<(int PersonId, int Dx, int Dy)> Walked { get; init; } = [];

    /// <summary>
    /// True when the script stopped at a yes-or-no and nobody answered it.
    /// <para>
    /// A run cannot answer one — everything else can be decided from a save and this needs a
    /// person — so the runner stops and hands back where to carry on from. Nothing has ever
    /// carried on. Neither this loop nor the closure walk has so much as looked at the field,
    /// so every offer in the game has been left hanging mid-sentence: not declined, which
    /// would at least be a branch, but simply not reached.
    /// </para>
    /// </summary>
    public bool StoppedAtAQuestion { get; init; }
}

/// <summary>
/// What running one script came to, as far as why it might not have finished.
/// <para>
/// A script that ran and did not do the thing it is named for stopped somewhere, and where is
/// the whole job. This game gives a run exactly three ways to stop short: a yes-or-no nobody
/// answered, a routine into code this cannot execute, or an ordinary branch it had no reason
/// to take. The first two have levers already — <c>--say-yes</c> and <c>--answer</c> — and
/// the third needs the bytes read.
/// </para>
/// </summary>
public sealed record WhatRan
{
    /// <summary>True when some pass of it stopped at a yes-or-no.</summary>
    public bool StoppedAtAQuestion { get; init; }

    /// <summary>Routines it asked and could not be answered, so it took the zero arm.</summary>
    public IReadOnlyList<int> Routines { get; init; } = [];

    /// <summary>
    /// Fights it stopped at and never got past.
    /// <para>
    /// <b>The third way a script stops, and it was not on the list.</b> A run that stopped at a
    /// fight it could not win was reported as "it ran to the end, so the setflag is on an
    /// ordinary branch it had no reason to take" — the fallback, printed because nothing else
    /// matched. Two sessions were spent hunting for that branch. There is no branch: SILPH
    /// CO.'s trigger sets the flag holding eight people on SAFFRON immediately after
    /// GIOVANNI, and the run loses to GIOVANNI.
    /// </para>
    /// <para>
    /// A fallback that names a cause is worse than one that says nothing, because it is
    /// actionable and wrong.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Fought { get; init; } = [];

    /// <summary>Flags it turned on, on the best pass it had.</summary>
    public IReadOnlyList<int> Set { get; init; } = [];

    /// <summary>
    /// This and one more pass of the same script, folded together.
    /// <para>
    /// The same script runs on every pass with a different bag and different flags behind it.
    /// Keeping only the last is keeping whichever pass happened to be last; keeping the union
    /// is "everything this script has ever managed", which is the honest ceiling.
    /// </para>
    /// </summary>
    public WhatRan And(PlayedScript did) => new()
    {
        StoppedAtAQuestion = StoppedAtAQuestion || did.StoppedAtAQuestion,
        Routines = [.. Routines.Union(did.Specials)],
        Set = [.. Set.Union(did.FlagsSet)],
        Fought = [.. did.Fights is { } trainer ? Fought.Union([trainer]) : Fought],
    };
}

/// <summary>
/// A door out of somewhere it reached, into somewhere it never did.
/// <para>
/// The number that matters when a run stops. "246 maps it never got to" is a list nobody can
/// act on; most of those are behind each other, and only a handful sit one step from ground
/// the player is already standing on. Those few are the story's actual walls.
/// </para>
/// </summary>
/// <param name="FromMapId">A map it reached.</param>
/// <param name="Square">The door, so it can be looked at.</param>
/// <param name="ToMapId">Where the door leads, which it never got to.</param>
/// <param name="ToName">That map's name.</param>
/// <param name="CouldStandOnIt">
/// Whether it could actually stand on the door. False means the door was never the problem —
/// something on this side of it was, and the two fields below say which.
/// </param>
/// <param name="SquareIsWalkable">
/// Whether the square is walkable at all. <c>ToGrid</c> opens every warp square deliberately —
/// "a door that cannot be stood on is a map that cannot be entered" — so a door that is
/// <em>not</em> walkable is a fault in the export rather than a gate in the story.
/// </param>
/// <param name="SomebodyIsInTheWay">
/// Somebody rooted on or beside the door. This is the shape most of FireRed's story gates
/// take: a guard who wants a drink, a man who has not been given a reason to move.
/// </param>
/// <param name="StoodOnThisMap">
/// How many squares of the map the door leads out of were actually stood on.
/// </param>
/// <param name="WalkableOnThisMap">
/// How many squares of it are walkable at all. The two together settle the question a
/// walkable-but-unreached door raises: one out of many means the walk arrived somewhere it
/// could not step off, which is what an <em>island</em> is.
/// <para>
/// Islands are made deliberately. <c>ToGrid</c> opens every warp square — "a door that cannot
/// be stood on is a map that cannot be entered" — so a warp sitting in a wall becomes a square
/// nothing can reach from inside the map and nothing can leave. <c>WarpsOnSolidSquares</c>
/// already exists, which means somebody has met this before.
/// </para>
/// </param>
/// <param name="IsDynamic">
/// True when the target is the 127.127 sentinel — a warp a script fills in at the moment it is
/// used, rather than a door to a fixed place. Nothing is missing when one of these is unopened
/// and counting them as blockers would be counting a feature.
/// </param>
public sealed record ShutDoor(
    string FromMapId,
    GridPosition Square,
    string ToMapId,
    string ToName,
    bool CouldStandOnIt,
    bool SquareIsWalkable = true,
    bool SomebodyIsInTheWay = false,
    int StoodOnThisMap = 0,
    int WalkableOnThisMap = 0,
    bool IsDynamic = false)
{
    /// <summary>
    /// True when the walk reached almost none of the map this door leads out of — the
    /// signature of having arrived on a square it could not step off.
    /// </summary>
    public bool ArrivedOnAnIsland =>
        WalkableOnThisMap > 4 && StoodOnThisMap * 8 < WalkableOnThisMap;

    /// <summary>
    /// Whoever is standing in it, and what talking to them came to.
    /// <para>
    /// "Somebody is standing in the way" was true for eight measurements running and named
    /// nobody. Which person, and what happens when you talk to them, are the two things that
    /// make it actionable — and they lived in different halves of this output with nothing
    /// joining them up.
    /// </para>
    /// </summary>
    public IReadOnlyList<Blocker> Who { get; init; } = [];
}

/// <summary>Somebody in a doorway, and what talking to them did.</summary>
/// <param name="Talked">False when the run never got close enough to speak to them at all.</param>
public sealed record Blocker(
    int LocalId,
    GridPosition Square,
    int MovementType,
    bool Talked,
    IReadOnlyList<int> AskedFor,
    bool Walked,
    bool Hid,
    int FlagsSet)
{
    /// <summary>
    /// Routines its script asked the game for and did not get an answer from.
    /// <para>
    /// The difference between "talking to him does nothing" and "talking to him asks the game
    /// a question this project cannot ask, and takes the nothing arm". The first is a person
    /// with no part in the story; the second is a wall with a number on it, and the number is
    /// the thing to hand to <c>--answer</c>.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Routines { get; init; } = [];

    /// <summary>
    /// The flag on this one's <em>own record</em> that takes them off the map, or nought.
    /// <para>
    /// <b>Where the answer turned out to be.</b> The four people in the last four doorways
    /// have scripts with no conditional in them at all — nothing to wait on, nothing to ask
    /// for, no arm not taken. They are not moved by anything they do; they are moved by a
    /// flag written on the map's own object record, set by a script somewhere else entirely.
    /// </para>
    /// <para>
    /// Which is already the known shape of this cartridge: only 7 of the 575 objects carrying
    /// a hide flag have a script that sets it. Everything else is set by the game's own code,
    /// or by somebody two maps away. Reading the script for a gate that was never in the
    /// script is the ninth wall in a row that moved when something finally printed.
    /// </para>
    /// </summary>
    public int HiddenBy { get; init; }
}

/// <summary>
/// Something a script wanted that the playthrough was not carrying, and where.
/// <para>
/// The list this instrument never had. "It stopped at SAFFRON" says where; this says
/// what it would have needed to be holding, in the cartridge's own item numbers, at the
/// exact person who asked.
/// </para>
/// </summary>
public sealed record Wanted(int ItemId, int Count, string MapId, int Times)
{
    /// <summary>
    /// Everywhere in the world one of these could be got, and whether the run stood there.
    /// <para>
    /// The half that turns a shopping list into a job. "SAFFRON wants a FRESH WATER" is a
    /// fact about a door; "and the only FRESH WATER in the world is on a shelf on a map it
    /// reached and never bought from" is the thing to go and build.
    /// </para>
    /// <para>
    /// An empty list is the sharper answer of the two: it means nothing on any map in the
    /// game hands one over at all, so whatever produces it is behind a routine this project
    /// cannot run, and no amount of walking will ever find it.
    /// </para>
    /// </summary>
    public IReadOnlyList<FoundAt> Sources { get; init; } = [];
}

/// <summary>
/// What the boat asks for, and whether this run could answer it.
/// <para>
/// Kept apart from the shopping list rather than folded into it, and the reason is
/// attribution. Every other refusal is a script asking, on a map, and says which. This one is
/// asked by a walk rather than by a run — the sailor puts the flag first, so a save without it
/// never reaches the <c>checkitem</c> and the question is never recorded as being put. Filing
/// it under a map would be inventing the one thing this project never invents.
/// </para>
/// </summary>
/// <param name="FlagSet">Whether the save holds the flag half of the cartridge's own "or".</param>
/// <param name="Carried">Whether it holds the item half.</param>
public sealed record FerryTicket(
    int Flag, int ItemId, bool FlagSet, bool Carried, IReadOnlyList<FoundAt> Sources)
{
    public bool Opens => FlagSet || Carried;
}

/// <summary>Something the playthrough bought, and what it cost.</summary>
public sealed record Bought(int ItemId, int Count, int Price, string MapId);

/// <summary>
/// Something it was refused, could see on a shelf, and still did not buy — and why.
/// <para>
/// Because "it bought nothing" has four causes and they are not remotely alike: nobody
/// sells one, the counter is somewhere it cannot stand, it cannot afford one, or the bag
/// has no room. The last of those is what actually happened the first time this ran, and
/// without this line it read as the third.
/// </para>
/// </summary>
public sealed record NotBought(int ItemId, string MapId, string Why);

/// <summary>Somewhere one item could be got, and how.</summary>
/// <param name="How">
/// In the world file's own terms — lying on the floor, handed over by somebody, paid for,
/// won, or given on arriving. Which one it is decides what has to be built next, and they
/// are very different jobs.
/// </param>
public sealed record FoundAt(string MapId, int LocalId, string How, bool Reached)
{
    /// <summary>
    /// The shortest way in, from ground the run stood on to the map this is sitting in.
    /// <para>
    /// Empty when it was reached, and the interesting field when it was not. "The only
    /// FRESH WATER in the world is on a map it never got to" is a dead end to look at; the
    /// same sentence with <c>3.12 -> 0.0 -> 3.13</c> after it names the one door to go and
    /// open, and the first hop of it is that door.
    /// </para>
    /// <para>
    /// Empty <em>also</em> means no way in exists at all, which is a different finding and
    /// a much larger one — a map nothing on any other map leads to. The two are told apart
    /// by <see cref="Reached"/>, which is why this is not a nullable.
    /// </para>
    /// </summary>
    public IReadOnlyList<Hop> WayIn { get; init; } = [];

    /// <summary>
    /// When there is no way in: the maps upstream of this one that nothing anywhere leads
    /// into. The bottom of the hole rather than the top of it.
    /// <para>
    /// These are two very different findings and the first version of this printed the
    /// wrong one. A map with no door pointing at it is adrift; a map whose every door comes
    /// from maps that are themselves unreached is <em>fine</em>, and the thing to go and
    /// look at is wherever that chain bottoms out — which is somewhere else entirely, and
    /// is what this holds.
    /// </para>
    /// <para>
    /// Empty with an empty <see cref="WayIn"/> means the ways in lead only to each other:
    /// a closed ring of maps, all unreached, none of them adrift. A third answer again.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Behind { get; init; } = [];
}

/// <summary>
/// One step of a way in: a map, and how you get into it from the one before.
/// <para>
/// How, and not only where, because the three kinds are three different answers. A door
/// and a map edge are things a player walks; a door a script makes — the lifts, the boats,
/// being thrown out of somewhere — is on no square at all, and a map reachable only by one
/// of those is not shut, it is somewhere the walk has no way of expressing.
/// </para>
/// </summary>
public sealed record Hop(string MapId, string How)
{
    /// <summary>The first map of a chain, which is not entered from anywhere.</summary>
    public const string Start = "stood there";

    public override string ToString() => $"{MapId} ({How})";
}

/// <summary>Why the playthrough stopped.</summary>
public enum StoppedBecause
{
    /// <summary>Nothing new opened. This is the ordinary end, and the interesting one.</summary>
    NothingMoreOpened,

    /// <summary>The backstop, which means something never settles.</summary>
    ItNeverSettled,

    /// <summary>Every creature it had was beaten and there was nothing left to send out.</summary>
    NobodyLeftToFight,
}

/// <summary>What the run came to.</summary>
public sealed record Attempt(
    int Passes,
    StoppedBecause Stopped,
    IReadOnlyCollection<string> Reached,
    IReadOnlyList<string> Unreached,
    IReadOnlyCollection<int> Flags,
    IReadOnlyCollection<int> Moves,
    IReadOnlyList<SavedMon> Party,
    int FightsWon,
    int FightsLost,
    int FightsSkipped,
    int PartiesHealed,
    IReadOnlyDictionary<int, int> Specials,
    IReadOnlyList<ShutDoor> ShutDoors,
    IReadOnlyList<Frontier> Blocked)
{
    /// <summary>
    /// How many attempts at a fight failed, which is more than the number of trainers that beat
    /// it.
    /// <para>
    /// A run goes back to a trainer it lost to on every later pass, because the party is
    /// stronger each time. Counting the attempts as losses makes a party closing the gap look
    /// like one losing more and more fights.
    /// </para>
    /// </summary>
    public int FightAttemptsLost { get; init; }

    /// <summary>The highest level anything in the party reached, which is the shape of a run.</summary>
    public int HighestLevel => Party.Count == 0 ? 0 : Party.Max(m => m.Level);

    /// <summary>What it was carrying when it stopped.</summary>
    public IReadOnlyList<BagEntry> Carried { get; init; } = [];

    /// <summary>
    /// Everything a script asked it for and it did not have, commonest first.
    /// <para>
    /// The useful half of the bag. An empty bag makes every one of these a refusal and a
    /// full one makes them all silent, so the length of this list is the distance between
    /// where the story stopped and where it could go.
    /// </para>
    /// </summary>
    public IReadOnlyList<Wanted> Refused { get; init; } = [];

    /// <summary>
    /// How many scripts stopped at each command with no width, commonest first.
    /// <para>
    /// The second error bar, beside the routines. A door this walk calls shut may have been
    /// behind a command this project cannot step over, and that is a fault here rather than a
    /// fact about the cartridge.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<byte, int> UnreadCommands { get; init; } = new Dictionary<byte, int>();

    /// <summary>What it bought, and what each one cost.</summary>
    public IReadOnlyList<Bought> Bought { get; init; } = [];

    /// <summary>What it had left.</summary>
    public int MoneyLeft { get; init; }

    /// <summary>What it stood in front of and did not buy, and why not.</summary>
    public IReadOnlyList<NotBought> CouldNotBuy { get; init; } = [];

    /// <summary>
    /// Scripts it reached that stopped at a yes-or-no, by map.
    /// <para>
    /// The size of a boundary nobody had measured. Everything past one of these is unreached
    /// in a way that looks exactly like a person having nothing more to say.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, int> Questions { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Every script this run actually ran, by where it starts.
    /// <para>
    /// <b>The question that comes after a flag number.</b> "The flag that opens SAFFRON is set
    /// by a trigger on <c>1.57</c>" is three different jobs wearing one sentence: a map the run
    /// never reached, a square on a map it did reach that the walk never stood on, or a script
    /// it ran that stopped short of its own <c>setflag</c>. Reaching a map and running what is
    /// on it are not the same thing — a trigger fires only for somebody standing exactly on
    /// it — and nothing here could tell those apart.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<uint, WhatRan> Ran { get; init; } = new Dictionary<uint, WhatRan>();

    /// <summary>People a script took off a map, which is how a doorway stops being blocked.</summary>
    public IReadOnlyCollection<(string MapId, int LocalId)> Removed { get; init; } = [];

    /// <summary>People a script walked out of where they were standing, which is the other way.</summary>
    public IReadOnlyCollection<(string MapId, int LocalId)> Moved { get; init; } = [];

    /// <summary>
    /// Whether this run held any of the passes the scripts ask about.
    /// <para>
    /// Reported and <b>not</b> enforced. The two this cartridge asks about are MYSTICTICKET
    /// and AURORATICKET, which nothing on any map hands over — they are worth two particular
    /// destinations rather than the boat, and which destinations is inside the routine. A
    /// walk that required one in order to sail at all shut the archipelago behind an item
    /// that does not exist in ordinary play.
    /// </para>
    /// </summary>
    public bool HeldATicket { get; init; }

    /// <summary>Whether it was allowed to take the boat, which makes the reach an upper bound.</summary>
    public bool RodeTheBoat { get; init; }

    /// <summary>
    /// What the boat asks for, whether the run could answer it, and where one comes from.
    /// <para>
    /// The archipelago's shopping list, which the ordinary one cannot hold — see
    /// <see cref="FerryTicket"/> for why.
    /// </para>
    /// </summary>
    public IReadOnlyList<FerryTicket> Tickets { get; init; } = [];

    /// <summary>
    /// Maps that no door, map edge or scripted door anywhere in the world leads to.
    /// <para>
    /// A fact about the world file rather than about this run, and it belongs beside the run
    /// because that is where it turned up: the only place in the game that sells a drink is
    /// on one of these, so "the playthrough never got there" was never the problem.
    /// </para>
    /// <para>
    /// The mirror of a question this project has had open for a while — <em>19 warps lead to
    /// maps that are not here</em> — asked from the other end. A warp pointing at nothing may
    /// be an unused room; a room nothing points at cannot be entered by anybody, which is
    /// either a hole in the export or a doorway the cartridge makes some way that has never
    /// been read.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> NoWayIn { get; init; } = [];
}

/// <summary>
/// Plays the game from a fresh save, as far as it can get.
/// <para>
/// <b>Not a rig.</b> <c>tools/rig</c> drives the client — Xvfb, xdotool, screenshots, one
/// keypress at a time — and a whole story that way is tens of thousands of presses through a
/// GUI, every one of them a place for the harness to lose its footing. Its own README records
/// two scripts that each cost a milestone by pressing something helpful at the wrong moment.
/// </para>
/// <para>
/// This drives the server instead. It walks the world with what it has, talks to everybody it
/// can stand in front of, fights whoever picks a fight, and takes what it is given. Then it
/// walks again. When a pass opens nothing it stops and says where it got to.
/// </para>
/// <para>
/// What that buys over playing it by hand is not speed. It is that it gives the same answer
/// twice, and that everything it could not do is a number rather than a memory of an evening.
/// </para>
/// <para>
/// <b>It is a floor.</b> Every limit of <see cref="StoryClosure"/> applies — a routine this
/// project cannot execute answers zero and its callers take the zero arm — plus two of its
/// own: it fights with a flat policy rather than well, and it never buys anything. Where it
/// stops, a person might get further. Where it *gets through*, a person certainly can.
/// </para>
/// </summary>
public static class Autoplayer
{
    /// <summary>How many passes before it gives up. <b>Modelled</b>, same reasoning as the walk.</summary>
    public const int MostPasses = 24;

    /// <summary>
    /// How many turns one fight may take before it is called a loss.
    /// <para>
    /// <b>Modelled.</b> Two parties that cannot hurt each other would otherwise run for ever —
    /// which is a real state, not a hypothetical: a creature with only status moves against
    /// one that resists them is a stalemate the games end by running out of PP, and PP is not
    /// what this loop is testing.
    /// </para>
    /// </summary>
    public const int MostTurns = 300;

    /// <param name="runScript">
    /// Runs one script with the flags and the bag it should see, and says what it did.
    /// <para>
    /// The bag is handed in rather than kept on the far side because a script asks about
    /// it: two hundred-odd sites in this cartridge check whether the player is carrying
    /// something, and a runner with no bag answers no at every one of them. Passed rather
    /// than copied because it changes inside the pass that reads it — the ball picked up
    /// at one end of a map is in the bag by the time the person at the other end asks.
    /// </para>
    /// </param>
    /// <param name="money">
    /// What it has to spend. <b>Modelled, and handed in from outside on purpose.</b>
    /// <para>
    /// Nothing in this game gives this run money. Winning a fight pays out in the cartridge
    /// and the payout is a trainer class's rate times a level, and that table has never been
    /// located here — so a run that awarded itself money would be quoting a number nobody
    /// read. Handed in instead, the same way <c>--answer</c> hands in a routine's answer: put
    /// some in, walk the story again, and see how much of the world opens.
    /// </para>
    /// <para>
    /// The prices themselves are <b>read</b>, off the item table on the cartridge.
    /// </para>
    /// </param>
    /// <param name="ridingTheBoat">
    /// Whether the walk may take the ferry. <b>Off by default, and that is not timidity.</b>
    /// <para>
    /// Where the boat goes is not readable: which places a ticket is worth lives inside the
    /// routine that draws the menu, so switching this on joins every dock to every other and
    /// asks for nothing, which is an upper bound in both directions at once.
    /// </para>
    /// <para>
    /// A run with it off is a floor, as this instrument has always been. A run with it on is
    /// an experiment, the same way <c>--answer</c> is, and the difference between the two is
    /// what the archipelago is worth.
    /// </para>
    /// </param>
    public static Attempt Play(
        WorldData world,
        string startMapId,
        GameRules rules,
        Func<uint, IReadOnlyCollection<int>, Bag, PlayedScript> runScript,
        Action<string>? log = null,
        bool ridingTheBoat = false,
        int money = 0,
        ISet<int>? beaten = null)
    {
        var battles = new BattleFactory(rules);
        var progress = new Progression(rules);

        // Whether anywhere in this world puts a party back on its feet. Every FireRed town
        // has a centre, so this is a fact about the export rather than about the game — and
        // if it is ever false, the run below is measuring a world with no centres in it and
        // should say so rather than quietly playing a much harder game.
        bool heals = world.Maps.Any(m => m.Objects.Any(o => o.Heals));

        var healed = 0;

        // A fresh save is not an empty save — see StoryClosure, and milestone 56. Forty-nine
        // flags are set before the first frame, and every one hides somebody not yet met.
        var flags = new HashSet<int>(world.FlagsAtStart);
        var moves = new HashSet<int>();
        var specials = new Dictionary<int, int>();
        var party = new List<SavedMon>();
        // WHO IT HAS BEATEN, SHARED WITH WHOEVER RUNS THE SCRIPTS.
        //
        // <b>The run knew and the reader did not.</b> A trainerbattle is its own conditional:
        // beaten, the fight does nothing and the script carries on into whatever the victory
        // was for. Nothing ever told the reader, so `HasBeaten` was false at every site on
        // every pass, and every script containing a fight stopped at the fight FOREVER —
        // however many the run won.
        //
        // That is the ROCKET HIDEOUT's LIFT KEY, and it is SILPH CO.'s `setflag 0x003E` sitting
        // eleven commands past GIOVANNI. Two sessions were spent on that flag.
        //
        // Passed in rather than handed back so the caller's reader can see a win the moment it
        // happens, which is what the cartridge does.
        ISet<int> fought = beaten ?? new HashSet<int>();
        var lostTo = new HashSet<int>();

        // And the ones it could not fight at all. Counted by trainer rather than by attempt for
        // the same reason as the losses: a run with nothing to send out comes back next pass
        // with a party, and one wall met seven times is one wall.
        var couldNot = new HashSet<int>();

        // What it is carrying. One bag for the whole run, written as it goes — the point
        // of it is that something picked up on ROUTE 2 is in hand at a door in SAFFRON.
        var bag = new Bag();

        // People a script has taken off a map. The walker has always been able to be told
        // this — `asIfGone` is its own parameter — and nothing has ever told it.
        var gone = new HashSet<(string MapId, int LocalId)>();

        // Every script that actually ran, by where it starts, and what running it came to.
        var ran = new Dictionary<uint, WhatRan>();

        // And people a script has walked somewhere else, which is the other half of the same
        // idea and had no parameter at all until now.
        var moved = new Dictionary<(string MapId, int LocalId), GridPosition>();

        // Everything asked for and not carried, by item and by where it was asked.
        var refused = new Dictionary<(int ItemId, int Count, string MapId), int>();

        var purse = money;
        var bought = new List<Bought>();
        var refusedAtTheCounter = new Dictionary<(int ItemId, string MapId), string>();
        var questions = new Dictionary<string, int>();
        var unread = new Dictionary<byte, int>();

        // What talking to each person came to, kept by who they are rather than by which
        // script address they share. Somebody standing in a doorway is only actionable
        // alongside what happens when you talk to them, and until now the two numbers lived
        // in different halves of the output with nothing joining them.
        var spokenTo = new Dictionary<(string MapId, int LocalId), PlayedScript>();

        var won = 0;
        var lost = 0;
        var skipped = 0;
        var passes = 0;

        StoppedBecause stopped = StoppedBecause.ItNeverSettled;

        for (int pass = 1; pass <= MostPasses; pass++)
        {
            passes = pass;

            Reach reach = WorldWalker.Walk(
                world, startMapId, moves, flagsSet: flags, asIfGone: gone,
                ridingTheBoat: ridingTheBoat, movedTo: moved);

            var stood = reach.Stood.ToHashSet();

            // What was known when this pass began. Compared at the end rather than watching
            // for additions as they happen — a script that clears a flag another one sets
            // reports something new every pass for ever, which is what put the first real run
            // into its backstop with nothing changing from pass four onwards.
            int flagsWere = flags.Count;
            int movesWere = moves.Count;
            int partyWas = party.Count;
            int carriedWas = bag.DistinctItems;
            int goneWere = gone.Count;
            int movedWere = moved.Count;

            foreach (MapData map in world.Maps.Where(m => reach.Maps.Contains(m.Id)))
            {
                foreach (Runnable what in Reachable(map, stood, flags, gone))
                {
                    PlayedScript did = runScript(what.Address, flags, bag);

                    // That it ran at all, which is a different fact from the map being
                    // reached. A trigger fires only for somebody standing exactly on it —
                    // and what running it came to, because a script that ran and did not do
                    // the thing it is named for stopped somewhere, and where is the job.
                    //
                    // Merged across passes rather than overwritten: the same script runs on
                    // every pass with a different bag and different flags, and the pass that
                    // got furthest is the one worth reporting.
                    ran[what.Address] = (ran.GetValueOrDefault(what.Address) ?? new WhatRan())
                        .And(did);

                    foreach (int routine in did.Specials)
                        specials[routine] = specials.GetValueOrDefault(routine) + 1;

                    foreach (int flag in did.FlagsSet) flags.Add(flag);

                    foreach (int flag in did.FlagsCleared) flags.Remove(flag);

                    foreach (int move in did.Teaches) moves.Add(move);

                    // Somebody this script removed. A guard who steps out of a doorway
                    // does it here and nowhere else, and until now the walk was never
                    // told: the same person stood in the same door on every pass, however
                    // the conversation had gone.
                    foreach (int who in did.Hides) gone.Add((map.Id, who));

                    // And whoever it walked. The script says who and how far; where they
                    // started is the map's own record, or wherever a previous scene left them.
                    foreach ((int who, int dx, int dy) in did.Walked)
                    {
                        if (map.Objects.FirstOrDefault(o => o.LocalId == who) is not { } walker) continue;

                        GridPosition from = moved.GetValueOrDefault((map.Id, who), walker.Square);

                        moved[(map.Id, who)] = new GridPosition(from.X + dx, from.Y + dy);
                    }

                    if (did.StoppedAtAQuestion) questions[map.Id] = questions.GetValueOrDefault(map.Id) + 1;

                    // And the commands it could not read at all, which is the other half of
                    // the same measurement and has never been carried out of here.
                    foreach (byte code in did.StoppedAt)
                        unread[code] = unread.GetValueOrDefault(code) + 1;

                    if (what.LocalId != 0) spokenTo[(map.Id, what.LocalId)] = did;

                    // What it handed over, and what it asked for and did not get. The
                    // refusals are the shopping list — the one thing that says what the
                    // story is actually waiting on rather than where it stopped.
                    foreach ((int itemId, int count, bool carried) in did.Asked)
                    {
                        if (carried) continue;

                        var key = (itemId, count, map.Id);
                        refused[key] = refused.GetValueOrDefault(key) + 1;
                    }

                    if (did.Takes is { } handedOver) bag.Remove(handedOver.ItemId, handedOver.Count);

                    if (did.Gets is { } got)
                    {
                        bag.Add(got.ItemId, got.Count, Most(rules, got.ItemId), Alongside(rules, got.ItemId));

                        // And a thing that is picked up is gone from the floor. The
                        // cartridge sets that flag inside the standard routine that does
                        // the handing over — code this project cannot follow, which is
                        // why only 7 of the 575 objects carrying a hide flag have a
                        // script that sets it. The object's own record says which flag,
                        // so the bookkeeping is readable even though the routine is not.
                        //
                        // Without it every ball in the world is picked up again on every
                        // pass, and a bag that refills itself is a bag whose counts mean
                        // nothing.
                        if (what.TakenAway != 0) flags.Add(what.TakenAway);
                    }

                    // Whatever it hands over. The first of these is the starter, and without
                    // it nothing after it can be fought at all.
                    // At the level the script says, which this used to throw away and replace
                    // with five — so every creature in the game arrived as a starter and the
                    // party could never be a match for anything.
                    if (did.Gives is { } gift && party.Count < 6
                        && battles.Wild(gift.Species, Math.Max(1, gift.Level)) is { } given)
                    {
                        party.Add(BattleFactory.Save(given));
                    }

                    // And whoever it picks a fight with. Once each: a trainer beaten stays
                    // beaten, which is what the flag they set means.
                    // A TRAINER BEATEN STAYS BEATEN. A TRAINER LOST TO DOES NOT.
                    //
                    // This marked one fought before the fight happened, so a loss was final:
                    // the run met GIOVANNI on its first pass with whatever it had, lost, and
                    // never went back — while every pass after that made the party stronger.
                    // A player who loses wakes up in a centre and walks in again; that is what
                    // the healing above is already modelling, one step too late.
                    //
                    // Forty-nine of these were sitting behind a party that had since doubled in
                    // level. Nothing said so: a fight lost once and a fight lost forever are
                    // the same line in the report.
                    if (did.Fights is not { } trainerId || fought.Contains(trainerId)) continue;

                    // Nothing to send out — but there may be next pass, so this is not final.
                    if (party.Count == 0)
                    {
                        couldNot.Add(trainerId);
                        skipped++;

                        continue;
                    }

                    // Healed first. A player walks back to a centre; this one cannot walk, so
                    // it is given the same thing for nothing.
                    //
                    // Not healing was the single worst decision in the first version, and the
                    // output said so in one number: the first loss left the whole party down,
                    // and every one of the 156 fights after it was lost before it began. A run
                    // that measures "did the first fight go badly" and reports it 157 times is
                    // not measuring the game.
                    if (heals)
                    {
                        for (var i = 0; i < party.Count; i++) party[i] = battles.Healed(party[i]);

                        healed++;
                    }

                    switch (Fight(battles, progress, party, trainerId))
                    {
                        case true:
                            fought.Add(trainerId);
                            won++;
                            break;

                        case false:
                            // Counted two ways on purpose, and reported apart. A trainer this
                            // run goes back to every pass is ONE wall; counting the attempts
                            // makes a party closing the gap look like one losing more and more
                            // fights, which is the opposite of what is happening.
                            lostTo.Add(trainerId);
                            lost++;
                            break;

                        // Nothing to send out, or a trainer this build could not assemble.
                        // Neither is a fight that happened, and neither is worth coming back
                        // to on the next pass with a bigger party — the first will be the same
                        // and the second is an export fault.
                        // A trainer whose party this build could not assemble. That is an
                        // export fault and it will be the same on every pass, so it is final.
                        default:
                            fought.Add(trainerId);
                            couldNot.Add(trainerId);
                            skipped++;
                            break;
                    }
                }
            }

            // And then the shopping, once everybody has been talked to and the list of things
            // it was refused is as long as this pass is going to make it.
            //
            // <b>It buys only what it has been refused.</b> Not a shopping policy — a policy
            // is a second thing to keep correct and this is measuring whether the story can be
            // finished, not whether a shopper is sensible. Something asked for by name at a
            // door, sold on a shelf it can stand in front of, and affordable, is bought.
            foreach (MapData map in world.Maps.Where(m => reach.Maps.Contains(m.Id)))
            {
                foreach (MapObject counter in map.Objects.Where(o => o.IsShopkeeper))
                {
                    if (!counter.IsHereFor(flags.Contains)) continue;
                    if (!Beside(map.Id, counter.Square).Any(stood.Contains)) continue;

                    foreach (int itemId in counter.Stock)
                    {
                        // Only what somebody asked for and it did not have, and only what the
                        // cartridge says is for sale at all: a price of nothing, or a key
                        // item on a shelf, is a listing rather than a purchase.
                        if (!refused.Keys.Any(r => r.ItemId == itemId)) continue;
                        if (bag.Has(itemId)) continue;

                        if (rules.ItemAt(itemId) is not { CanBeBought: true } sold)
                        {
                            refusedAtTheCounter[(itemId, map.Id)] = "the cartridge does not sell it";

                            continue;
                        }

                        if (purse < sold.Price)
                        {
                            refusedAtTheCounter[(itemId, map.Id)] =
                                $"cannot afford it — {sold.Price} against {purse} left";

                            continue;
                        }

                        // The pocket, not the bag. A cap named for one and counted across the
                        // other is why the first run of this stood in the shop with money in
                        // hand and bought nothing at all.
                        if (bag.Add(itemId, 1, Most(rules, itemId), Alongside(rules, itemId)) <= 0)
                        {
                            refusedAtTheCounter[(itemId, map.Id)] = "the bag had no room for it";

                            continue;
                        }

                        // No reason is recorded here, and there is nothing to clear either:
                        // a reason is written on the branch that failed and this one did not.
                        // The first version cleared a stale entry on the way past, which no
                        // test could ever fail — nothing writes one for a purchase that works.
                        purse -= sold.Price;
                        bought.Add(new Bought(itemId, 1, sold.Price, map.Id));
                    }
                }
            }

            log?.Invoke(
                $"  pass {pass,2}: {reach.Maps.Count,3} maps, {flags.Count,4} flags, "
                + $"{party.Count} in the party (highest level {(party.Count == 0 ? 0 : party.Max(m => m.Level))}), "
                + $"{bag.DistinctItems} things carried, {won} won / {lost} lost");

            // A pass that only picked something up has opened nothing yet and has still
            // changed the game — the door the thing unlocks is asked about by a script on
            // the next pass, not this one. Left out of this test, the loop stops one pass
            // before the bag is ever used and the whole of the above buys nothing.
            if (flags.Count == flagsWere && moves.Count == movesWere && party.Count == partyWas
                && bag.DistinctItems == carriedWas && gone.Count == goneWere
                && moved.Count == movedWere)
            {
                stopped = StoppedBecause.NothingMoreOpened;

                break;
            }
        }

        Reach last = WorldWalker.Walk(
            world, startMapId, moves, flagsSet: flags, asIfGone: gone,
            ridingTheBoat: ridingTheBoat, movedTo: moved);

        // Built once. Inside the query below it would be rebuilt for every map in the world,
        // which is the same mistake the walker's own comment records making with its grids.
        Dictionary<string, List<Hop>> anyWayIn = WaysIn(world);

        var reached = last.Maps.ToHashSet();
        var stoodAtTheEnd = last.Stood.ToHashSet();

        // How much of each map was actually walked, which is the number that tells a door
        // behind a story gate from a door on a square nothing can approach.
        Dictionary<string, int> stoodPerMap = last.Stood
            .GroupBy(s => s.MapId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Every door out of somewhere it got to, into somewhere it did not. This is the list
        // "246 maps unreached" should have been: most of those are behind each other, and only
        // these sit one step from ground already under the player's feet.
        List<ShutDoor> shut =
        [
            .. world.Maps
                .Where(m => reached.Contains(m.Id))
                .SelectMany(m => m.Warps
                    .Where(w => !reached.Contains(w.TargetMapId))
                    .Select(w => new ShutDoor(
                        m.Id,
                        w.Square,
                        w.TargetMapId,
                        world.Find(w.TargetMapId)?.Name ?? "(not exported)",
                        stoodAtTheEnd.Contains((m.Id, w.Square)),
                        m.ToGrid().IsWalkable(w.Square),
                        last.People.Any(p => p.MapId == m.Id && Near(p.Square, w.Square)),
                        stoodPerMap.GetValueOrDefault(m.Id),
                        Walkable(m),
                        w.IsDynamic)
                    {
                        Who =
                        [
                            .. last.People
                                .Where(p => p.MapId == m.Id && Near(p.Square, w.Square))
                                .Select(p => new Blocker(
                                    p.LocalId,
                                    p.Square,
                                    p.MovementType,
                                    spokenTo.ContainsKey((m.Id, p.LocalId)),
                                    [
                                        .. spokenTo.GetValueOrDefault((m.Id, p.LocalId))?.Asked
                                            .Select(a => a.ItemId) ?? [],
                                    ],
                                    spokenTo.GetValueOrDefault((m.Id, p.LocalId))?.Walked.Count > 0,
                                    spokenTo.GetValueOrDefault((m.Id, p.LocalId))?.Hides.Count > 0,
                                    spokenTo.GetValueOrDefault((m.Id, p.LocalId))?.FlagsSet.Count ?? 0)
                                {
                                    Routines =
                                    [
                                        .. spokenTo.GetValueOrDefault((m.Id, p.LocalId))?.Specials
                                            .Distinct() ?? [],
                                    ],

                                    // Off this one's own record, by their number on this map.
                                    // Not the first object with a hide flag on it and not any
                                    // flag on the map — the person in the doorway's own.
                                    HiddenBy = m.Objects
                                        .FirstOrDefault(o => o.LocalId == p.LocalId)?.HiddenBy ?? 0,
                                }),
                        ],
                    }))
                .DistinctBy(d => (d.FromMapId, d.ToMapId, d.Square)),
        ];

        return new Attempt(
            passes,
            stopped,
            last.Maps,
            [.. world.Maps.Select(m => m.Id).Where(id => !last.Maps.Contains(id)).Order()],
            flags,
            moves,
            party,
            won,
            lostTo.Count,
            couldNot.Count,            healed,
            specials,
            shut,
            last.Blocked)
        {
            FightAttemptsLost = lost,
            Carried = bag.Entries,
            Ran = ran,
            Removed = gone,
            Moved = [.. moved.Keys],
            RodeTheBoat = ridingTheBoat,
            Bought = bought,
            MoneyLeft = purse,
            Questions = questions,
            UnreadCommands = unread,
            CouldNotBuy = [.. refusedAtTheCounter.Select(r => new NotBought(r.Key.ItemId, r.Key.MapId, r.Value))],
            Tickets =
            [
                .. world.FerryPasses.Select(p => new FerryTicket(
                    p.Flag,
                    p.ItemId,
                    flags.Contains(p.Flag),
                    bag.Has(p.ItemId),
                    [.. Everywhere(world, p.ItemId, reached)])),
            ],
            HeldATicket = world.FerryPasses.Count > 0
                && world.FerryPasses.Any(p => flags.Contains(p.Flag) || bag.Has(p.ItemId)),
            // Except where the game starts, which is entered by waking up there rather than
            // through a door. It is the one map in the world that needs no way in, and
            // counting it would put a permanent false positive at the top of this list.
            NoWayIn =
            [
                .. world.Maps.Select(m => m.Id)
                    .Where(id => id != startMapId && !anyWayIn.ContainsKey(id))
                    .Order(),
            ],
            Refused =
            [
                // Only what it never got hold of. Being refused on pass one and buying one on
                // pass two is the story working, and leaving that on the list would make a
                // shopping list that never shortens however much of it is bought.
                .. refused
                    .Where(r => !bag.Has(r.Key.ItemId, r.Key.Count))
                    .Select(r => new Wanted(r.Key.ItemId, r.Key.Count, r.Key.MapId, r.Value)
                    {
                        Sources = [.. Everywhere(world, r.Key.ItemId, reached)],
                    })
                    .OrderByDescending(w => w.Times)
                    .ThenBy(w => w.ItemId)
                    .ThenBy(w => w.MapId),
            ],
        };
    }

    /// <summary>
    /// Everywhere in the world one item could be got.
    /// <para>
    /// Read out of the world file rather than off the cartridge, which is what makes it
    /// cheap enough to print for every refusal: everything that hands something over was
    /// already resolved at export, on all five of the ways a thing changes hands.
    /// </para>
    /// <para>
    /// All five are listed separately rather than collapsed into "obtainable", because they
    /// are entirely different jobs. Something lying on the floor is walked onto and is
    /// already handled; something sold needs money and a shop; something won needs a fight
    /// to be winnable. A list saying "yes, obtainable" would hide the only thing worth
    /// knowing.
    /// </para>
    /// </summary>
    private static IEnumerable<FoundAt> Everywhere(
        WorldData world, int itemId, HashSet<string> reached)
    {
        // Worked out once per map rather than once per source, because a map with a shop on
        // it usually has several things on the list and they all came the same way.
        var routes = new Dictionary<string, (IReadOnlyList<Hop> Chain, IReadOnlyList<string> Behind)>();

        (IReadOnlyList<Hop> Chain, IReadOnlyList<string> Behind) Through(string mapId) =>
            routes.TryGetValue(mapId, out (IReadOnlyList<Hop>, IReadOnlyList<string>) known)
                ? known
                : routes[mapId] = WayIn(world, mapId, reached);

        foreach (MapData map in world.Maps)
        {
            bool here = reached.Contains(map.Id);

            foreach (MapObject who in map.Objects)
            {
                string? how =
                    who.GivesItemId == itemId ? who.CanBeTakenAway ? "lying there" : "handed over"

                    // On one branch of a question this run cannot answer. Kept apart from a
                    // plain handover because that is exactly what stands in the way of it —
                    // not reaching the person, but replying to them.
                    : who.CanGive.Contains(itemId) ? "handed over on a branch"
                    : who.WinsItemId == itemId ? "for winning a fight"
                    : who.Stock.Contains(itemId) ? "sold"
                    : null;

                if (how is null) continue;

                yield return new FoundAt(map.Id, who.LocalId, how, here)
                {
                    WayIn = here ? [] : Through(map.Id).Chain,
                    Behind = here ? [] : Through(map.Id).Behind,
                };
            }

            foreach (MapEntryScript arriving in map.OnEntry)
            {
                if (arriving.GivesItemId != itemId) continue;

                yield return new FoundAt(map.Id, 0, "on arriving", here)
                {
                    WayIn = here ? [] : Through(map.Id).Chain,
                    Behind = here ? [] : Through(map.Id).Behind,
                };
            }
        }
    }

    /// <summary>
    /// The shortest way from ground the run stood on to a map it never got to.
    /// <para>
    /// Walked <em>backwards</em>, over the doors that lead <em>into</em> each map, and that is
    /// the whole trick. Forwards from where the player is, everything past the first shut door
    /// is one undifferentiated cloud of 246 maps; backwards from the thing you actually want,
    /// the first map in the chain that was reached is the door to go and open, and there is
    /// exactly one of it.
    /// </para>
    /// <para>
    /// Nothing at all comes back for a map with no way in — which is a much bigger finding
    /// than a shut door and must not read as one.
    /// </para>
    /// </summary>
    /// <returns>
    /// Map ids from the reached one to the target, inclusive of both, or nothing when no
    /// chain of doors joins them at all.
    /// </returns>
    /// <summary>
    /// Every way into every map, by where it leads.
    /// <para>
    /// All three kinds, because this game uses all three: a door on a square, walking off
    /// the side of a route, and a door a script makes on no square at all. The third was
    /// left out of the first version of this, and that is how a map reached by a lift came
    /// back as one nothing in the world leads to.
    /// </para>
    /// <para>
    /// The 127.127 sentinels are not filtered out, deliberately. A dynamic warp's target is
    /// the string "127.127", which is not a map any world file has — so it becomes a way in
    /// to a map nobody ever asks about, and skipping it is a rule that cannot be broken. It
    /// was written, it failed to fail, and it went.
    /// </para>
    /// </summary>
    private static Dictionary<string, List<Hop>> WaysIn(WorldData world)
    {
        var into = new Dictionary<string, List<Hop>>();

        void Joins(string from, string leadsTo, string how)
        {
            if (!into.TryGetValue(leadsTo, out List<Hop>? ways)) into[leadsTo] = ways = [];

            if (!ways.Any(w => w.MapId == from)) ways.Add(new Hop(from, how));
        }

        foreach (MapData map in world.Maps)
        {
            foreach (Warp door in map.Warps) Joins(map.Id, door.TargetMapId, "a door");

            foreach (MapConnection edge in map.Connections) Joins(map.Id, edge.MapId, "the map edge");

            foreach (ScriptedDoor made in map.Doors)
                Joins(map.Id, made.TargetMapId, $"a door a script makes ({made.What})");
        }

        // And the boat, which is neither a square nor a script. Every dock joins every other
        // dock, which is an upper bound and is said out loud rather than hidden: which places
        // a given ticket is worth is inside the routine that draws the menu and cannot be
        // read from here. That is the right bound for the question being asked — "is there
        // any way in at all" — and it is why the label says boat.
        List<string> docks = [.. world.Maps.Where(m => m.Ferry is not null).Select(m => m.Id)];

        foreach (string dock in docks)
        {
            foreach (string other in docks.Where(d => d != dock)) Joins(dock, other, "the boat");
        }

        return into;
    }

    private static (IReadOnlyList<Hop> Chain, IReadOnlyList<string> Behind) WayIn(
        WorldData world, string target, HashSet<string> reached)
    {
        // Every way into every map, by where it leads. All three kinds, because this game
        // uses all three: a door on a square, walking off the side of a route, and a door a
        // script makes on no square at all.
        //
        // The 127.127 sentinels are not filtered out, and deliberately. A dynamic warp's
        // target is the string "127.127", which is not a map any world file has — so it
        // becomes a way in to a map nobody ever asks about, and skipping it is a rule that
        // cannot be broken. It was written, it failed to fail, and it went.
        Dictionary<string, List<Hop>> into = WaysIn(world);

        var back = new Dictionary<string, Hop>();
        var seen = new HashSet<string>();
        var queue = new Queue<string>([target]);

        while (queue.Count > 0)
        {
            string here = queue.Dequeue();

            if (reached.Contains(here))
            {
                // Unwound forwards, so it reads the way somebody would walk it. How you got
                // into each map travels with the map you got into, so the labels come out
                // one step behind where they were stored.
                var chain = new List<Hop>();
                var how = Hop.Start;

                for (string at = here; ; )
                {
                    chain.Add(new Hop(at, how));

                    if (at == target) break;

                    Hop next = back[at];

                    how = next.How;
                    at = next.MapId;
                }

                return (chain, []);
            }

            seen.Add(here);

            foreach (Hop way in into.GetValueOrDefault(here, []))
            {
                if (back.TryAdd(way.MapId, new Hop(here, way.How))) queue.Enqueue(way.MapId);
            }
        }

        // Nowhere reached upstream of it. Which is not the same as "nothing leads here", and
        // saying so was this instrument's own worst line: the department store's floors are
        // what nothing leads into, and the roof above them reads as adrift because of it.
        return ([], [.. seen.Where(m => !into.ContainsKey(m)).Order()]);
    }

    /// <summary>
    /// The most of one item a bag may hold, which is one for anything the games call a key
    /// item and a full stack for everything else.
    /// <para>
    /// Read off the rules rather than decided here. It matters for exactly the items this
    /// run is about: a script that hands over the parcel or the tea is reached on every
    /// pass, and a bag that ends up holding ninety-nine of a thing there is only one of in
    /// the world is a bag nobody should trust about anything else either.
    /// </para>
    /// </summary>
    /// <summary>
    /// Which of the things already carried share a pocket with this one, so that the bag's
    /// capacity means what its name says.
    /// <para>
    /// An item whose record the rules have never heard of shares a pocket with nothing, which
    /// keeps an unknown id from filling a pocket it does not belong to.
    /// </para>
    /// </summary>
    private static Func<int, bool> Alongside(GameRules rules, int itemId)
    {
        Pocket mine = rules.ItemAt(itemId)?.Pocket ?? Pocket.None;

        return other => mine != Pocket.None && rules.ItemAt(other)?.Pocket == mine;
    }

    private static int Most(GameRules rules, int itemId) =>
        rules.ItemAt(itemId)?.IsKeyItem == true ? 1 : Bag.MaxStack;

    /// <summary>
    /// What a creature handed over by a script comes out at. <b>Modelled</b> — the level is in
    /// the script and this loop does not read it, so five is the starter's and everything else
    /// is levelled by fighting anyway.
    /// </summary>
    public const int StartingLevel = 5;

    /// <summary>
    /// One fight, front to back. True won, false lost, null could not be fought at all.
    /// <para>
    /// The policy is flat on purpose: send out whoever can still fight, use the move that
    /// would hurt most, never switch, never use an item. A cleverer player wins more fights,
    /// so a fight this loses is not proof a person would — but a fight this <em>wins</em> is
    /// proof the fight works, which is what is being measured.
    /// </para>
    /// </summary>
    private static bool? Fight(
        BattleFactory battles, Progression progress, List<SavedMon> party, int trainerId)
    {
        List<Battler> theirs = battles.TrainerParty(trainerId);

        if (theirs.Count == 0) return null;

        List<Battler> mine = [.. party.Select(battles.Restore).OfType<Battler>()];

        if (mine.Count == 0) return null;

        var mySlot = 0;
        var theirSlot = 0;

        var battle = new Battle(mine[0], theirs[0], 1) { IsWild = false, Struggle = battles.Struggle };

        for (var turn = 0; turn < MostTurns; turn++)
        {
            if (battle.Player.HasFainted)
            {
                int next = mine.FindIndex(m => !m.HasFainted);

                if (next < 0) break;

                mySlot = next;
                battle = Continue(battles, mine[mySlot], theirs[theirSlot], battle);

                continue;
            }

            if (battle.Opponent.HasFainted)
            {
                int next = theirs.FindIndex(t => !t.HasFainted);

                if (next < 0) break;

                theirSlot = next;
                battle = Continue(battles, mine[mySlot], theirs[theirSlot], battle);

                continue;
            }

            battle.ResolveTurn(Best(battle.Player, battle.Opponent), Best(battle.Opponent, battle.Player));
        }

        bool ours = mine.Any(m => !m.HasFainted);

        // Whatever survived, written back.
        // Written back WITH what the battle could not carry. A battler has no experience — the
        // note on Save says so and says every caller starting from a save puts it back — and
        // this caller did not. Every win reset the total to nothing, the next award started
        // from the bottom of the level it was already at, and nothing in this party ever
        // reached the next one.
        for (var i = 0; i < mine.Count && i < party.Count; i++)
            party[i] = BattleFactory.Save(mine[i], party[i]);

        // And what winning is worth. Without this the party stays at the level it was handed
        // over at for the whole game — which the first real run printed twenty-four times in a
        // row as "highest level 5" and which makes every fight after the first few unwinnable
        // by arithmetic rather than by anything the engine did.
        if (!ours) return false;

        foreach (Battler beaten in theirs)
        {
            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].Level >= 100) continue;

                (SavedMon grown, _) = progress.Award(party[i], beaten.Species.Index, beaten.Level);

                party[i] = grown;
            }
        }

        return true;
    }

    /// <summary>
    /// The next one-on-one of the same fight, carrying the dice and the room across.
    /// <para>
    /// <see cref="Battle.ContinueFrom"/> rather than a fresh battle, because the weather
    /// belongs to the room and not to either creature — the fault milestone 169 found in the
    /// one caller that had forgotten it.
    /// </para>
    /// </summary>
    private static Battle Continue(BattleFactory battles, Battler mine, Battler theirs, Battle before)
    {
        var next = new Battle(mine, theirs, before.State)
        {
            IsWild = false,
            Struggle = battles.Struggle,
        };

        next.ContinueFrom(before);

        return next;
    }

    /// <summary>
    /// The move that would hurt most, or the first one when none of them would.
    /// <para>
    /// Power alone, without type or stats. A better chooser would win more fights and would
    /// also be a second engine to keep correct, and what is being measured is whether the
    /// fight can be had at all.
    /// </para>
    /// </summary>
    private static BattleAction Best(Battler attacker, Battler defender)
    {
        var bestSlot = 0;
        var bestPower = -1;

        for (var slot = 0; slot < 4; slot++)
        {
            if (attacker.MoveAt(slot) is not { } move) continue;
            if (attacker.PpLeft(slot) <= 0) continue;

            if (move.Power > bestPower)
            {
                bestPower = move.Power;
                bestSlot = slot;
            }
        }

        return new BattleAction.UseMove(bestSlot);
    }

    /// <summary>
    /// Every script a player could actually stand in front of. Same rule as the closure walk,
    /// and for the same reason: a person behind a locked door is on a map you have been to and
    /// is not somebody you can talk to.
    /// </summary>
    private static IEnumerable<Runnable> Reachable(
        MapData map,
        HashSet<(string MapId, GridPosition Square)> stood,
        HashSet<int> flags,
        HashSet<(string MapId, int LocalId)> gone)
    {
        foreach (MapObject person in map.Objects)
        {
            if (!person.HasScript) continue;
            if (!person.IsHereFor(flags.Contains)) continue;

            // And not somebody a script has already taken off the map. Being hidden by a
            // flag and being removed by a command are the same thing to a player and two
            // different things in the file, and only the first was being asked about.
            if (gone.Contains((map.Id, person.LocalId))) continue;

            if (Beside(map.Id, person.Square).Any(stood.Contains))
            {
                yield return new Runnable(
                    person.ScriptAddress,
                    person.CanBeTakenAway ? person.HiddenBy : 0,
                    person.LocalId);
            }
        }

        foreach (MapTrigger trigger in map.Triggers)
        {
            if (trigger.HasScript && stood.Contains((map.Id, trigger.Square)))
                yield return new Runnable(trigger.ScriptAddress, 0);
        }

        foreach (MapEntryScript entry in map.OnEntry)
        {
            if (entry.ScriptAddress != 0) yield return new Runnable(entry.ScriptAddress, 0);
        }
    }

    /// <summary>
    /// A script a playthrough can actually run, and the flag that takes its owner off the
    /// map once whatever it is holding has been taken.
    /// <para>
    /// The second half is only ever set for a thing on the floor — <c>CanBeTakenAway</c> is
    /// the world file's own name for "gives something and has a flag to vanish behind".
    /// A person who hands you a parcel and stays put has a hide flag too, and setting it
    /// would delete them from the world for the rest of the run.
    /// </para>
    /// </summary>
    private sealed record Runnable(uint Address, int TakenAway, int LocalId = 0);

    /// <summary>How many squares of a map anybody could stand on at all.</summary>
    private static int Walkable(MapData map)
    {
        CollisionGrid grid = map.ToGrid();

        var count = 0;

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (grid.IsWalkable(new GridPosition(x, y))) count++;
            }
        }

        return count;
    }

    /// <summary>Whether two squares are the same one or touching, which is close enough to be
    /// in the way of a door.</summary>
    private static bool Near(GridPosition one, GridPosition two) =>
        Math.Abs(one.X - two.X) <= 1 && Math.Abs(one.Y - two.Y) <= 1;

    private static IEnumerable<(string, GridPosition)> Beside(string mapId, GridPosition at) =>
    [
        (mapId, at),
        (mapId, at with { Y = at.Y - 1 }),
        (mapId, at with { Y = at.Y + 1 }),
        (mapId, at with { X = at.X - 1 }),
        (mapId, at with { X = at.X + 1 }),
    ];
}
