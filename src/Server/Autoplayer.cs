using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;

namespace PokeMmo.Server;


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
/// What a run has to say about one script on one map, when that script sets a flag the run
/// never set.
/// <para>
/// Four answers where <c>--flags</c> printed three. The missing one is
/// <see cref="ItRanTheSameBlockOnAnotherMap"/>, and it was missing because the run's record of
/// what it ran was keyed on the script's address with no map beside it — so a nurse's script
/// run in one town read as run in all nineteen, and the first case swallowed the third.
/// </para>
/// </summary>
public enum WhereItStands
{
    /// <summary>The setter is somewhere the walk never got to. That map is the job.</summary>
    OnAMapItNeverReached,

    /// <summary>
    /// It reached the map, stood on the square and ran this very script, and the flag is
    /// still unset. The only one of the four that licenses a reason.
    /// </summary>
    ItRanTheScriptHere,

    /// <summary>
    /// It reached the map and never ran the script THERE — but the same block hangs off
    /// another map and the run ran it on that one.
    /// <para>
    /// Not a weaker version of <see cref="ItRanTheScriptHere"/>: a different scene entirely,
    /// with its own square nobody stood on. The block having run somewhere is still a real
    /// fact and worth printing, which is why this is its own answer rather than folded into
    /// <see cref="ItNeverRanTheScript"/>.
    /// </para>
    /// </summary>
    ItRanTheSameBlockOnAnotherMap,

    /// <summary>It reached the map and never ran this block anywhere at all.</summary>
    ItNeverRanTheScript,
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
/// <summary>
/// One look at or change to a watched variable, with where in the run it happened.
/// <para>
/// <b>The instrument this project did not have.</b> <c>--who-writes</c> answers who writes a
/// variable <em>anywhere in the image</em>, statically, following every arm of every branch —
/// which is the right answer to "where in the file is this touched" and the wrong answer to
/// "what happened here". A run takes one arm per conditional, so a script that writes six on
/// one arm and eight on another writes neither when it takes a third; the static list names it
/// as a writer of both, and reading a cause off that list is reading it off the wrong
/// instrument.
/// </para>
/// </summary>
public sealed record Traced(int Pass, string MapId, int LocalId, uint Address, VariableTouch What)
{
    public override string ToString() =>
        $"pass {Pass}  {MapId}" + (LocalId == 0 ? "" : $" person {LocalId}") +
        $"  0x{Address:X8}  {What}";
}

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

/// <summary>
/// A shop counter on ground it reached that it never got beside, and how near it got.
/// </summary>
/// <param name="MapId">The map, which it did reach.</param>
/// <param name="LocalId">Who on that map.</param>
/// <param name="Square">Where they stand.</param>
/// <param name="NearestStood">
/// Manhattan distance to the nearest square the walk stood on, on this map, or <c>-1</c> when
/// it stood on no square of this map at all. Two means across a counter.
/// </param>
/// <param name="SquaresBesideThatAreWalkable">
/// How many of the four squares orthogonally beside them this map's own collision says can be
/// stood on. <b>Nought means no walk could ever get beside them</b>, so talking across the
/// counter is the only way the shop works and adjacency is the wrong rule, not a missing one.
/// </param>
public sealed record CounterOutOfReach(
    string MapId,
    int LocalId,
    GridPosition Square,
    int NearestStood,
    int SquaresBesideThatAreWalkable);

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

/// <summary>
/// One place that handed something over, and which passes it did so on.
/// <para>
/// <b>A run that takes the same gift twice is a ceiling, and nothing said so.</b> The party
/// has said it for a while — <em>a second copy of something already in it</em> — and the bag
/// never has, because an item picked up off the floor is kept from refilling by the flag on
/// the object's own record and an item handed over by a person is kept from refilling by a
/// guard inside the script. Only one of those two was ever read.
/// </para>
/// </summary>
/// <param name="MapId">Where.</param>
/// <param name="LocalId">Which person, or zero for an arrival script or a trigger.</param>
/// <param name="Address">Which script.</param>
/// <param name="What">What it hands over, said plainly.</param>
/// <param name="Passes">The passes it happened on.</param>
public sealed record HandedOver(
    string MapId, int LocalId, uint Address, string What, IReadOnlyList<int> Passes)
{
    public override string ToString() =>
        $"{MapId,-8} {(LocalId == 0 ? "on arrival" : $"person {LocalId}"),-12} 0x{Address:X8}"
        + $"  {What}  on pass(es) {string.Join(",", Passes)}";
}

/// <summary>
/// Somebody a scene walked to a square that is not on the map they are on.
/// <para>
/// <b>Reported and not repaired, on purpose.</b> A scene that walks somebody aside is applied
/// as a displacement from wherever they already are — so a scene the fixpoint plays six times
/// walks them six times, and the sixth time is off the edge. That much is arithmetic. What the
/// cartridge does instead is guarded by a flag this project has not read yet, and clamping the
/// number would turn a wrong position into a plausible one, which is the harder fault to find.
/// </para>
/// <para>
/// It matters beyond tidiness: <c>somebody is standing in the way</c> and <c>a person removed
/// is a person not in a doorway</c> are both computed against these positions.
/// </para>
/// </summary>
/// <param name="MapId">Which map.</param>
/// <param name="LocalId">Which person.</param>
/// <param name="To">Where the walk put them.</param>
/// <param name="Width">How wide the map is.</param>
/// <param name="Height">And how tall.</param>
public sealed record WalkedOffTheMap(string MapId, int LocalId, GridPosition To, int Width, int Height)
{
    public override string ToString() =>
        $"{MapId} person {LocalId} at ({To.X},{To.Y}) on a {Width}x{Height} map";
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

    /// <summary>
    /// Water squares the walk turned back from, by map — the shore it stopped at.
    /// <para>
    /// The walker has always been able to swim and the playthrough has never asked it to, so
    /// every water square was dropped as solid — which reads exactly like there being nothing
    /// there. Seventeen of the doors this run calls shut are on maps it landed on and could not
    /// cross. Counted whether or not it is swimming, so the line means the same thing either
    /// way: how much of the world is on the far side of the water.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, int> Shore { get; init; } = new Dictionary<string, int>();

    /// <summary>What it bought, and what each one cost.</summary>
    public IReadOnlyList<Bought> Bought { get; init; } = [];

    /// <summary>What it had left.</summary>
    public int MoneyLeft { get; init; }

    /// <summary>What it stood in front of and did not buy, and why not.</summary>
    public IReadOnlyList<NotBought> CouldNotBuy { get; init; } = [];

    /// <summary>
    /// How many shop counters it stood in front of, by map and by who.
    /// <para>
    /// The denominator for <see cref="Bought"/> and <see cref="CouldNotBuy"/>, and the reason
    /// this exists at all: "it bought nothing" and "it never reached a shop" are different
    /// findings that read as one silence. The buying report was behind <c>money &gt; 0</c>, so
    /// a default run printed neither — and four entries on the shopping list turn out to be
    /// standing on ground where the thing is sold.
    /// </para>
    /// <para>
    /// Places rather than times: the same shopkeeper on the same map is one counter however
    /// many passes stand in front of it.
    /// </para>
    /// </summary>
    public int CountersStoodAt { get; init; }

    /// <summary>
    /// Shop counters on maps it reached, whether or not it got to one — the denominator above
    /// <see cref="CountersStoodAt"/>.
    /// <para>
    /// The shopping list says a thing is sold "on ground it reached", and ground it reached is
    /// the MAP. Standing beside the person selling it is a second thing, and the difference
    /// between these two numbers is exactly how often the two come apart. This project has
    /// known that a trigger fires only for somebody standing on it since milestone 184 and had
    /// never asked the same question of a counter.
    /// </para>
    /// </summary>
    public int CountersOnReachedGround { get; init; }

    /// <summary>Counters on reached ground whose own record hides them behind a flag.</summary>
    public int CountersHiddenByAFlag { get; init; }

    /// <summary>
    /// Counters on reached ground it was never beside — the map was walked, this square was
    /// not. The reason a thing sold on ground it reached can still be a walk finding rather
    /// than a money one.
    /// </summary>
    public int CountersNeverStoodBeside { get; init; }

    /// <summary>
    /// The counters it never stood beside, and how far off the nearest square it DID stand on
    /// was — sorted nearest first.
    /// <para>
    /// The number that tells the two causes apart. A distance of two is a clerk standing behind
    /// a counter the player talks across, which this walk cannot do and which is not a reach
    /// problem at all. A large distance, or <c>-1</c> for a map it stood nowhere on, is a room
    /// it never entered. Both print as "never stood beside" and only this separates them.
    /// </para>
    /// </summary>
    public IReadOnlyList<CounterOutOfReach> CountersOutOfReach { get; init; } = [];

    /// <summary>
    /// Scripts it reached that stopped at a yes-or-no, by map.
    /// <para>
    /// The size of a boundary nobody had measured. Everything past one of these is unreached
    /// in a way that looks exactly like a person having nothing more to say.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, int> Questions { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Every script this run actually ran, by the map it ran on and where it starts.
    /// <para>
    /// <b>The question that comes after a flag number.</b> "The flag that opens SAFFRON is set
    /// by a trigger on <c>1.57</c>" is three different jobs wearing one sentence: a map the run
    /// never reached, a square on a map it did reach that the walk never stood on, or a script
    /// it ran that stopped short of its own <c>setflag</c>. Reaching a map and running what is
    /// on it are not the same thing — a trigger fires only for somebody standing exactly on
    /// it — and nothing here could tell those apart.
    /// </para>
    /// <para>
    /// <b>The map is half the key.</b> This was keyed on the address alone, which is the fault
    /// 193 shipped in the walking and 194 fixed there — still live here. One nurse's script is
    /// attached to person 1 on nineteen Pokémon Centres, one shopkeeper's on nineteen marts,
    /// one gym guide's on eight. Running such a block once made it read as run on every map it
    /// hangs off — and <c>--flags</c> asks this dictionary whether a script ran before it
    /// prints WHY that script did not set its flag. A setter on a map the walk never stood on
    /// therefore got a confident diagnosis borrowed from a different town.
    /// </para>
    /// <para>
    /// A fallback that names a cause is worse than one that says nothing, and this is that
    /// shape a second time.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<(string MapId, uint Address), WhatRan> Ran { get; init; }
        = new Dictionary<(string MapId, uint Address), WhatRan>();

    /// <summary>
    /// The blocks this run ran <em>somewhere</em>, without the map beside them.
    /// <para>
    /// The denominator for <see cref="Ran"/>. "12 blocks are reached from more than one map"
    /// is a fact about the cartridge; how many of them this run ran on one map and not on
    /// another is a fact about the run, and the two read identically until both are printed. A
    /// number with no denominator cannot come back empty, which is the trap this project has
    /// now fallen into three times.
    /// </para>
    /// <para>
    /// <b>Derived rather than kept beside it, deliberately.</b> Two fields carrying the same
    /// fact can drift, and a guard on a field that cannot drift is a guard nothing can fail —
    /// which is already on this project's owed list once, under
    /// <c>SpecialContracts.ComparedAfter</c>. The only claim here is a projection, and a
    /// projection is not a claim about the world.
    /// </para>
    /// </summary>
    public IReadOnlySet<uint> RanAnywhere => Ran.Keys.Select(k => k.Address).ToHashSet();

    /// <summary>
    /// Where a script named as setting a flag stands with respect to this run.
    /// <para>
    /// Here rather than in whoever prints it, for the reason <see cref="HandedOverTwice"/>
    /// gives: a rule about the world living in a printer is a rule no test can reach, and this
    /// project has moved the same kind of line out of the same file six times now. This one
    /// was a three-way conditional inside <c>--flags</c>, and the three ways were four.
    /// </para>
    /// <para>
    /// The fourth is <see cref="WhereItStands.ItRanTheSameBlockOnAnotherMap"/> and it did not exist
    /// while <c>Ran</c> was keyed on the address alone — it was silently the first case,
    /// complete with a reason it stopped that had been merged in from a different town.
    /// </para>
    /// </summary>
    /// <param name="mapId">The map the setter is on — half the key.</param>
    /// <param name="address">Where the script it names starts.</param>
    public WhereItStands HowItStands(string mapId, uint address) =>
        !Reached.Contains(mapId)
            ? WhereItStands.OnAMapItNeverReached
            : Ran.ContainsKey((mapId, address))
                ? WhereItStands.ItRanTheScriptHere
                : RanAnywhere.Contains(address)
                    ? WhereItStands.ItRanTheSameBlockOnAnotherMap
                    : WhereItStands.ItNeverRanTheScript;

    /// <summary>
    /// Every look at and change to the watched variable, in the order the run did them.
    /// <para>
    /// Empty unless a variable was watched. This is the ordered half of the story's memory:
    /// the run has always been able to say what a counter ended up holding and has never been
    /// able to say what it held <b>at the moment somebody read it</b>, which is the only
    /// question a counter ever raises.
    /// </para>
    /// </summary>
    public IReadOnlyList<Traced> Trace { get; init; } = [];

    /// <summary>
    /// How many touches were dropped because the trace filled up.
    /// <para>
    /// Printed rather than swallowed. A silent cap reads as "that is all that happened",
    /// which is the failure this project has spent a session finding in its own output.
    /// </para>
    /// </summary>
    public int TraceDropped { get; init; }

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
    /// Every place that handed something over, and the passes it did so on.
    /// <para>
    /// The whole list rather than only the repeats, so the denominator is visible: "none of
    /// them twice" and "nothing handed anything over" are different findings and they printed
    /// the same as each other before this.
    /// </para>
    /// </summary>
    public IReadOnlyList<HandedOver> Handovers { get; init; } = [];

    /// <summary>
    /// The move this cartridge crosses water with, and the pass the party first knew it.
    /// <para>
    /// Nought for a pass means never — the sea was a wall for the whole run, which is a
    /// different finding from a run that was never allowed to swim and printed the same
    /// number for years.
    /// </para>
    /// </summary>
    public int SurfMove { get; init; }

    public int LearnedToCrossOnPass { get; init; }

    /// <summary>Whether it swam because it was told to rather than because it knew how.</summary>
    public bool SwamAnyway { get; init; }

    /// <summary>People a scene walked to a square that is not on their map.</summary>
    public IReadOnlyList<WalkedOffTheMap> OffTheMap { get; init; } = [];

    /// <summary>
    /// How many distinct <c>applymovement</c> commands ON A MAP the run reached, and how many
    /// times it asked for one.
    /// <para>
    /// <b>The two are wildly different and the difference is what a fixpoint is.</b> A scene in
    /// this cartridge is commonly written as several tiny entry stubs — <c>lockall; setvar
    /// 0x4001, N; goto &lt;the scene&gt;</c>, one per square you can cross to start it, each
    /// announcing which door it came in by — and all of them run the same block. A player takes
    /// one door. A run that stands on every square takes all of them, and every one executes
    /// the same commands at the same addresses.
    /// </para>
    /// <para>
    /// So the same command is the same movement, and it applies once. That is identity rather
    /// than a decision, which is why it is not marked MODELLED.
    /// </para>
    /// <para>
    /// <b>Once per map, and the difference is nineteen Pokémon Centres.</b> One nurse's script
    /// is attached to person 1 on nineteen maps and one gym guide's to person 3 on eight, so a
    /// block reached from eight maps is eight scenes. Keyed on the address alone this dropped
    /// seven of every eight — found by asking how many walk sites the run reached from more
    /// than one map, which was three of eighty-three and had gone straight past every test.
    /// </para>
    /// </summary>
    public int WalkSites { get; init; }

    public int WalksAsked { get; init; }

    /// <summary>
    /// How many times the run asked, as opposed to how many places ask.
    /// <para>
    /// <b>The denominator these four numbers never had.</b> <c>N calls to M routines it could
    /// not answer</c> counted every run of every script on every pass, so a fixpoint that
    /// settles in six passes quoted an error bar six times too big: 5051 against 325 places on
    /// the floor run. Both are true and they answer different questions, and only one of them
    /// is about the cartridge.
    /// </para>
    /// </summary>
    public int AskedSpecials { get; init; }

    public int AskedUnread { get; init; }

    public int AskedQuestions { get; init; }

    public int AskedRefusals { get; init; }

    /// <summary>
    /// How many of the run's own counts were a scene arriving again by another door.
    /// <para>
    /// <b>Printed because the prediction was wrong.</b> 193 and 194 found that this cartridge
    /// writes one scene as several entry stubs and that a fixpoint takes every door, and it
    /// followed that everything counted per script — the routines it cannot answer, the
    /// commands it cannot read, the yes-or-nos, the refusals — would be inflated by however
    /// many doors a scene has. Measured, it is six calls in three hundred and twenty-five.
    /// </para>
    /// <para>
    /// The shape matters where the effect ACCUMULATES: a person walked once per door ends up
    /// four squares away, and that was worth nine maps. A counter that says how many times is
    /// not accumulating anything, and 38 duplicate runs out of thousands is noise. Kept anyway,
    /// because a number that is right is worth having, and printed so it cannot quietly become
    /// large.
    /// </para>
    /// </summary>
    public int FoldedByDoor { get; init; }

    /// <summary>
    /// The ones that did it more than once, which is the ceiling.
    /// <para>
    /// Here rather than in whoever prints, because a <c>Where</c> in a printer is a rule
    /// about the world in a file no test can reach — and this project has moved the same
    /// kind of line out of the same file five times for the same reason.
    /// </para>
    /// </summary>
    public IReadOnlyList<HandedOver> HandedOverTwice => [.. Handovers.Where(h => h.Passes.Count > 1)];

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

    /// <summary>
    /// How many touches of a watched variable are kept.
    /// <para>
    /// <b>Modelled</b>, and the overflow is counted and printed rather than dropped quietly.
    /// A scratch pad is written a hundred and sixty-eight times by three hundred scripts on
    /// twenty-four passes, and a trace of one is a book nobody reads; the story's own counters
    /// are touched a few dozen times each and fit inside this many times over.
    /// </para>
    /// </summary>
    public const int MostTraced = 4096;

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
        ISet<int>? beaten = null,
        bool surfing = false,
        IReadOnlyDictionary<int, int>? remembered = null,
        bool inOrder = false,
        IReadOnlyDictionary<uint, uint>? doorsTo = null)
    {
        // WHICH SCENE A SCRIPT IS, WHICH IS NOT THE SAME AS WHICH SCRIPT IT IS.
        //
        // This cartridge writes one scene as several tiny stubs — lockall; setvar 0x4001, N;
        // goto the scene — one per square you can cross to start it. A player takes one door
        // and this walk takes all of them, so everything counted per script is counted once
        // per door: the routines it could not answer, the commands it could not read, the
        // yes-or-nos it stopped at, and the things it asked for and was refused. Those four
        // numbers are the error bars this project quotes.
        //
        // Handed in rather than worked out, because working it out means reading the
        // cartridge and nothing in this file has ever done that. See EntriesToAScene.
        uint Scene(uint address) => doorsTo?.GetValueOrDefault(address, address) ?? address;

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

        // Every script that actually ran, by the map it ran on and where it starts, and what
        // running it came to. Keyed on the map as well as the address for the same reason the
        // walk is: one nurse's script hangs off nineteen Pokémon Centres, and running it in
        // one town is not running it in the other eighteen.
        var ran = new Dictionary<(string MapId, uint Address), WhatRan>();


        // And people a script has walked somewhere else, which is the other half of the same
        // idea and had no parameter at all until now.
        var moved = new Dictionary<(string MapId, int LocalId), GridPosition>();

        // Everything asked for and not carried, by item and by where it was asked.
        var refused = new Dictionary<(int ItemId, int Count, string MapId), int>();

        var purse = money;
        var bought = new List<Bought>();

        // Every counter it actually stood in front of, by map and by who — and the two
        // denominators either side of it. All three are places, not times.
        var stoodAtACounter = new HashSet<(string MapId, int LocalId)>();
        var onReachedGround = new HashSet<(string MapId, int LocalId)>();
        var hiddenByAFlag = new HashSet<(string MapId, int LocalId)>();
        var neverStoodBeside = new HashSet<(string MapId, int LocalId)>();
        var outOfReach = new Dictionary<(string MapId, int LocalId), CounterOutOfReach>();
        var refusedAtTheCounter = new Dictionary<(int ItemId, string MapId), string>();
        var questions = new Dictionary<string, int>();

        // Every (map, scene) already counted, per counter, and the same thing keyed on the
        // SCRIPT rather than the scene beside it. The difference between the two is the door
        // count and nothing else, which is how the door claim gets measured on its own.
        var countedSpecials = new HashSet<(string, uint, int)>();
        var countedUnread = new HashSet<(string, uint, byte)>();
        var countedQuestions = new HashSet<(string, uint)>();
        var countedRefusals = new HashSet<(string, uint, int, int)>();
        var byScript = new HashSet<(string, uint, int, int)>();

        // Asked, as opposed to asked somewhere new. A fixpoint asks again on every pass, so
        // these are the raw counts this project has been quoting as error bars.
        var askedSpecials = 0;
        var askedUnread = 0;
        var askedQuestions = 0;
        var askedRefusals = 0;

        // And the part of the difference that is doors rather than passes.
        var foldedByDoor = 0;

        // True when this is somewhere the run has not counted before EXCEPT that the same
        // scene has already been counted by another door into it.
        bool ADoorAlreadyTaken(string mapId, uint address, int what, int which) =>
            byScript.Add((mapId, address, what, which));
        var unread = new Dictionary<byte, int>();

        // What talking to each person came to, kept by who they are rather than by which
        // script address they share. Somebody standing in a doorway is only actionable
        // alongside what happens when you talk to them, and until now the two numbers lived
        // in different halves of the output with nothing joining them.
        var spokenTo = new Dictionary<(string MapId, int LocalId), PlayedScript>();

        // The ordered record of one variable, when somebody asked for one. Bounded, and the
        // overflow is counted rather than dropped quietly: a trace that silently stops reads
        // exactly like a run that stopped touching the thing.
        var trace = new List<Traced>();
        var dropped = 0;

        var won = 0;
        var lost = 0;
        var skipped = 0;
        var passes = 0;

        StoppedBecause stopped = StoppedBecause.ItNeverSettled;

        // The pass the party first knew how to swim, or nought for never. Recorded rather than
        // recomputed at the end: "it can swim" and "it could swim in time for that to matter"
        // are different claims and only one of them is about a run.
        var learnedToCross = 0;

        // How many scene-walks were applied at all, so the count below has a denominator.
        var walksApplied = 0;
        var walksAsked = 0;
        // The same command on the same map. NOT the same command anywhere: nineteen Pokémon
        // Centres share one nurse's script and eight gym guides share one guide's, so a block
        // reached from eight maps is eight scenes and keying on the address alone silently
        // dropped seven of them. Found by asking how many walk sites the run reached from more
        // than one map — three of eighty-three, and every one of them cost seven walks.
        var walkedFrom = new HashSet<(string MapId, uint At)>();

        // Where something changed hands, by the script that did it. Kept by script rather
        // than by what it hands over: five shopkeepers selling the same potion is not one
        // place handing it over five times, and the question here is about the second.
        var handovers =
            new Dictionary<(string MapId, int LocalId, uint Address), (string What, List<int> Passes)>();

        for (int pass = 1; pass <= MostPasses; pass++)
        {
            passes = pass;

            Reach reach = WorldWalker.Walk(
                world, startMapId, moves, surfing: surfing || KnowsHowToCross(rules, moves), flagsSet: flags, asIfGone: gone,
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
                // A queue rather than a loop, for one reason: winning a fight runs a script,
                // and it has to run HERE — with the same bag, the same flags and the same
                // folding as everything else. Handing it to a second copy of this body is how
                // the two would drift apart, and this project has found that fault five times.
                var toRun = new Queue<Runnable>(Reachable(map, stood, flags, gone, remembered, inOrder));
                var alreadyRun = new HashSet<uint>();

                while (toRun.Count > 0)
                {
                    Runnable what = toRun.Dequeue();

                    PlayedScript did = runScript(what.Address, flags, bag);

                    // In the order it happened, which is the entire point of the thing.
                    foreach (VariableTouch touch in did.Touched)
                    {
                        if (trace.Count >= MostTraced) dropped++;
                        else trace.Add(new Traced(pass, map.Id, what.LocalId, what.Address, touch));
                    }

                    // That it ran at all, which is a different fact from the map being
                    // reached. A trigger fires only for somebody standing exactly on it —
                    // and what running it came to, because a script that ran and did not do
                    // the thing it is named for stopped somewhere, and where is the job.
                    //
                    // Merged across passes rather than overwritten: the same script runs on
                    // every pass with a different bag and different flags, and the pass that
                    // got furthest is the one worth reporting.
                    //
                    // Merged across passes ON THIS MAP. Merging across maps as well is what
                    // this was doing, and it is how a script that ran in CERULEAN reported
                    // the reason it stopped as the reason a run stopped in PEWTER.
                    ran[(map.Id, what.Address)] =
                        (ran.GetValueOrDefault((map.Id, what.Address)) ?? new WhatRan()).And(did);

                    foreach (int routine in did.Specials)
                    {
                        askedSpecials++;

                        bool fresh = ADoorAlreadyTaken(map.Id, what.Address, 1, routine);

                        if (!countedSpecials.Add((map.Id, Scene(what.Address), routine)))
                        {
                            if (fresh) foldedByDoor++;

                            continue;
                        }

                        specials[routine] = specials.GetValueOrDefault(routine) + 1;
                    }

                    foreach (int flag in did.FlagsSet) flags.Add(flag);

                    foreach (int flag in did.FlagsCleared) flags.Remove(flag);

                    foreach (int move in did.Teaches) moves.Add(move);

                    // Somebody this script removed. A guard who steps out of a doorway
                    // does it here and nowhere else, and until now the walk was never
                    // told: the same person stood in the same door on every pass, however
                    // the conversation had gone.
                    foreach (int who in did.Hides) gone.Add((map.Id, who));

                    // And whoever it walked. The script says who and which way; where they
                    // started is the map's own record, or wherever a previous scene left them.
                    //
                    // ONE SQUARE AT A TIME, AND THEY STOP AT A WALL. Summing the steps and
                    // applying the total in one jump put 364 of 426 walks off the edge of the
                    // map on the floor run — and a person at x = -29 on a map 48 wide is not
                    // in a doorway or out of one, which is what the walk goes on to ask about
                    // them. The grid is the same oracle the step bytes were derived against.
                    foreach ((int who, IReadOnlyList<Direction> going, uint at2) in did.Walked)
                    {
                        walksAsked++;

                        // The same command is the same movement. See Attempt.WalkSites.
                        if (!walkedFrom.Add((map.Id, at2))) continue;
                        if (map.Objects.FirstOrDefault(o => o.LocalId == who) is not { } walker) continue;

                        GridPosition at = moved.GetValueOrDefault((map.Id, who), walker.Square);

                        CollisionGrid grid = map.ToGrid();

                        foreach (Direction way in going)
                        {
                            GridPosition next = Step(at, way);

                            // Off the map, or into something. The cartridge stops them here
                            // and so does this — and the steps after it do not happen either,
                            // because a walker stopped at a wall does not carry on past it.
                            if (next.X < 0 || next.Y < 0 || next.X >= map.Width || next.Y >= map.Height) break;
                            if (!grid.IsWalkable(next)) break;

                            at = next;
                        }

                        if (at != walker.Square) moved[(map.Id, who)] = at;
                        else moved.Remove((map.Id, who));

                        walksApplied++;
                    }

                    if (did.StoppedAtAQuestion)
                    {
                        askedQuestions++;

                        bool fresh = ADoorAlreadyTaken(map.Id, what.Address, 3, 0);

                        if (countedQuestions.Add((map.Id, Scene(what.Address))))
                            questions[map.Id] = questions.GetValueOrDefault(map.Id) + 1;
                        else if (fresh) foldedByDoor++;
                    }

                    // And the commands it could not read at all, which is the other half of
                    // the same measurement and has never been carried out of here.
                    foreach (byte code in did.StoppedAt)
                    {
                        askedUnread++;

                        bool fresh = ADoorAlreadyTaken(map.Id, what.Address, 2, code);

                        if (!countedUnread.Add((map.Id, Scene(what.Address), code)))
                        {
                            if (fresh) foldedByDoor++;

                            continue;
                        }

                        unread[code] = unread.GetValueOrDefault(code) + 1;
                    }

                    if (what.LocalId != 0) spokenTo[(map.Id, what.LocalId)] = did;

                    // What it handed over, and what it asked for and did not get. The
                    // refusals are the shopping list — the one thing that says what the
                    // story is actually waiting on rather than where it stopped.
                    foreach ((int itemId, int count, bool carried) in did.Asked)
                    {
                        if (carried) continue;

                        askedRefusals++;

                        bool freshAsk = ADoorAlreadyTaken(map.Id, what.Address, 4, itemId);

                        if (!countedRefusals.Add((map.Id, Scene(what.Address), itemId, count)))
                        {
                            if (freshAsk) foldedByDoor++;

                            continue;
                        }

                        var key = (itemId, count, map.Id);
                        refused[key] = refused.GetValueOrDefault(key) + 1;
                    }

                    if (did.Takes is { } handedOver) bag.Remove(handedOver.ItemId, handedOver.Count);

                    if (did.Gets is not null || did.Gives is not null)
                    {
                        var where = (map.Id, what.LocalId, what.Address);

                        if (!handovers.TryGetValue(where, out (string What, List<int> Passes) already))
                        {
                            handovers[where] = already = (
                                did.Gives is { } creature
                                    ? $"#{creature.Species} at {creature.Level}"
                                    : $"item 0x{did.Gets!.Value.ItemId:X3} x{did.Gets!.Value.Count}",
                                []);
                        }

                        already.Passes.Add(pass);
                    }

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

                            // AND WHAT THE VICTORY WAS FOR, now, on the pass that won it.
                            // The badge, the flags, the LIFT KEY on the floor of the ROCKET
                            // HIDEOUT. It used to run on the pass AFTER the win, and on every
                            // pass after that as well, because "beaten" was read as "resume
                            // inside the fight's own script" — which handed the eight gym
                            // leaders' TMs over once per pass for ever.
                            if (did.AfterTheFight != 0 && alreadyRun.Add(did.AfterTheFight))
                            {
                                // Nobody's, on purpose: the continuation belongs to the
                                // battle rather than to the person, and filing it under them
                                // would overwrite what talking to them came to.
                                toRun.Enqueue(new Runnable(did.AfterTheFight, 0));
                            }

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
                    // Every counter on ground it reached, before either reason to skip one.
                    // This is the other half of the denominator: a shop on a map it walked
                    // through and a shop it never got to are the same absence downstream, and
                    // the shopping list says only "on ground it reached", which is the MAP.
                    // Reaching a map and standing beside somebody on it are not the same
                    // thing — this project has written that down about triggers and never
                    // asked it about a counter.
                    onReachedGround.Add((map.Id, counter.LocalId));

                    if (!counter.IsHereFor(flags.Contains))
                    {
                        hiddenByAFlag.Add((map.Id, counter.LocalId));

                        continue;
                    }

                    if (!SpokenToFrom(map, counter.Square).Any(stood.Contains))
                    {
                        neverStoodBeside.Add((map.Id, counter.LocalId));

                        // AND HOW FAR OFF IT WAS, which is the whole question.
                        //
                        // "It never stood beside them" has two causes that print alike: the
                        // walk never got into the room at all, or it got in and stopped one
                        // square short. In this game a shop clerk stands BEHIND a counter and
                        // the player talks across it, so a walk that requires orthogonal
                        // adjacency is one tile too strict by construction — and this would
                        // read as a reach problem for ever.
                        //
                        // The distance to the nearest square it did stand on tells them apart
                        // and nothing else does. Two is "across the counter". Large, or none
                        // at all, is a room it never entered.
                        int nearest = stood
                            .Where(p => p.MapId == map.Id)
                            .Select(p => Math.Abs(p.Square.X - counter.Square.X)
                                + Math.Abs(p.Square.Y - counter.Square.Y))
                            .DefaultIfEmpty(-1)
                            .Min();

                        // AND WHETHER ANY SQUARE BESIDE THEM IS STANDABLE AT ALL.
                        //
                        // This is what turns "probably a counter" into a reading. If every
                        // square orthogonally beside the clerk is impassable on this map's own
                        // collision, then no walk of any quality could ever stand next to
                        // them — so talking across the counter is not a convenience the
                        // cartridge offers, it is the ONLY way this shop can be used, and a
                        // walk that requires adjacency is wrong rather than merely incomplete.
                        //
                        // Nought here and a distance of two are the same fact read two ways,
                        // which is what this project asks of anything it is about to believe.
                        CollisionGrid grid = map.ToGrid();

                        int standable = Beside(map.Id, counter.Square)
                            .Skip(1)
                            .Count(b => grid.IsWalkable(b.Item2));

                        outOfReach[(map.Id, counter.LocalId)] =
                            new CounterOutOfReach(
                                map.Id, counter.LocalId, counter.Square, nearest, standable);

                        continue;
                    }

                    // The denominator for everything below it. "It bought nothing" and "it
                    // never got to a counter at all" are different findings and they print as
                    // the same silence — which this report has been printing since there has
                    // been a bag, because the whole section sat behind `money > 0`.
                    //
                    // Places, not times, per 195: the same shopkeeper on the same map is one
                    // counter however many passes stand in front of it.
                    stoodAtACounter.Add((map.Id, counter.LocalId));

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

            if (learnedToCross == 0 && KnowsHowToCross(rules, moves)) learnedToCross = pass;

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
            world, startMapId, moves, surfing: surfing || KnowsHowToCross(rules, moves), flagsSet: flags, asIfGone: gone,
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
            SurfMove = rules.SurfMove,
            LearnedToCrossOnPass = learnedToCross,
            SwamAnyway = surfing,
            WalkSites = walkedFrom.Count,
            WalksAsked = walksAsked,
            FoldedByDoor = foldedByDoor,
            AskedSpecials = askedSpecials,
            AskedUnread = askedUnread,
            AskedQuestions = askedQuestions,
            AskedRefusals = askedRefusals,
            OffTheMap =
            [
                // EVERYBODY, not only the ones a scene walked. Once the walk stops at a wall
                // nothing this loop does can put somebody off the map, and a check that
                // nothing can fail is not a check — so it asks the other half as well: does
                // every person the cartridge places stand on the map it places them on? That
                // one is about the export and it can come back with a number.
                .. world.Maps
                    .SelectMany(m => m.Objects.Select(o => (Map: m, Who: o.LocalId,
                        At: moved.GetValueOrDefault((m.Id, o.LocalId), o.Square))))
                    .Where(p => p.At.X < 0 || p.At.Y < 0
                                || p.At.X >= p.Map.Width || p.At.Y >= p.Map.Height)
                    .Select(p => new WalkedOffTheMap(p.Map.Id, p.Who, p.At, p.Map.Width, p.Map.Height))
                    .OrderBy(w => w.MapId, StringComparer.Ordinal)
                    .ThenBy(w => w.LocalId),
            ],
            Handovers =
            [
                .. handovers
                    .Select(h => new HandedOver(h.Key.MapId, h.Key.LocalId, h.Key.Address, h.Value.What, h.Value.Passes))
                    .OrderByDescending(h => h.Passes.Count)
                    .ThenBy(h => h.MapId, StringComparer.Ordinal),
            ],
            Trace = trace,
            TraceDropped = dropped,
            Ran = ran,
            Removed = gone,
            Moved = [.. moved.Keys],
            RodeTheBoat = ridingTheBoat,
            Bought = bought,
            MoneyLeft = purse,
            CountersStoodAt = stoodAtACounter.Count,
            CountersOnReachedGround = onReachedGround.Count,
            CountersHiddenByAFlag = hiddenByAFlag.Count,
            CountersNeverStoodBeside = neverStoodBeside.Count,
            CountersOutOfReach = [.. outOfReach.Values.OrderBy(c => c.NearestStood)],
            Questions = questions,
            Shore = last.Shore
                .GroupBy(w => w.MapId)
                .ToDictionary(g => g.Key, g => g.Count()),
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
        HashSet<(string MapId, int LocalId)> gone,
        IReadOnlyDictionary<int, int>? remembered = null,
        bool inOrder = false)
    {
        // WHETHER A SCRIPT'S OWN CONDITION IS HONOURED, WHICH IS WHAT THE FLOOR MEANS.
        //
        // A trigger and an arrival script each carry a variable and a value, and this walk has
        // always run them regardless — which makes the run a CEILING in that respect: it takes
        // arms of the story no single playthrough could take in one pass.
        //
        // PALLET TOWN is the case, and this comment described it WRONGLY for three milestones
        // because it was read off --who-writes, which answers the same question of the image
        // rather than of the run: statically, down every arm of every branch. It said the
        // counter ratcheted to nine before the three balls read it and the balls answered "you
        // already have one". Traced through an actual run, the balls read ONE, every pass, for
        // seven passes — the counter was too LOW, not too high, and the cause was the ordering
        // fixed below rather than this lever.
        //
        // What is true here, measured with the order corrected: the map has several arrival
        // scripts and running all of them takes the counter past two in one pass, so the ceiling
        // still does not hold a starter and the floor now does. That is a real cost of the
        // ceiling, and it is the one this paragraph was reaching for.
        //
        // Honoured, the same walk is a floor: it runs what a save in this state would run.
        // Both are worth having and neither is the truth on its own, so it is a lever.
        // No special case for an unconditional entry, and that is measured rather than assumed:
        // an entry with no condition is (0, 0), an unwritten variable holds nought, so it passes
        // this comparison already. The clause that said so was written, broken on purpose, and
        // came back green — then removed, because --play prints the same 215 maps and 193 flags
        // with and without it on the real image. A clause that cannot change an answer looks
        // like a rule and is not one.
        bool Fires(int variable, int value) =>
            !inOrder || (remembered?.GetValueOrDefault(variable) ?? 0) == value;

        // ARRIVING FIRST, WHICH IS THE ONLY ORDER THERE IS.
        //
        // This ran LAST — after every person on the map — and the ordering was never chosen,
        // it is the order the three loops happened to be written in.
        //
        // It is not a modelling choice and it has no defensible reading: an arrival script is
        // what runs when you arrive, and nobody has ever talked to somebody on a map they had
        // not yet arrived on. The other way round is not a stricter run or a looser one, it is
        // an order the cartridge cannot produce.
        //
        // PALLET TOWN is the case, and `--trace 0x4055` is how it was seen rather than argued
        // about. The trigger north of town writes ONE; the lab's arrival script reads that one
        // and writes TWO; TWO is the only number that makes the three balls hand anything over.
        // Running the people first, all three balls read ONE — "you are not ready" — and the
        // two arrives immediately after they have all been asked. On the next pass the map to
        // the north has moved it to five and they answer "you already have one".
        //
        // So the counter was right for one instant, between the lab's own script and the next
        // map, and nobody was looking. Every instrument this project has printed the five at
        // the end and none of them could say the balls never saw a two.
        foreach (MapEntryScript entry in map.OnEntry)
        {
            if (entry.ScriptAddress != 0 && Fires(entry.Variable, entry.Value))
                yield return new Runnable(entry.ScriptAddress, 0);
        }

        foreach (MapTrigger trigger in map.Triggers)
        {
            if (trigger.HasScript
                && stood.Contains((map.Id, trigger.Square))
                && Fires(trigger.Variable, trigger.Value))
            {
                yield return new Runnable(trigger.ScriptAddress, 0);
            }
        }

        foreach (MapObject person in map.Objects)
        {
            if (!person.HasScript) continue;
            if (!person.IsHereFor(flags.Contains)) continue;

            // And not somebody a script has already taken off the map. Being hidden by a
            // flag and being removed by a command are the same thing to a player and two
            // different things in the file, and only the first was being asked about.
            if (gone.Contains((map.Id, person.LocalId))) continue;

            if (SpokenToFrom(map, person.Square).Any(stood.Contains))
            {
                yield return new Runnable(
                    person.ScriptAddress,
                    person.CanBeTakenAway ? person.HiddenBy : 0,
                    person.LocalId);
            }
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

    /// <summary>One square that way.</summary>
    private static GridPosition Step(GridPosition from, Direction way) => way switch
    {
        Direction.Left => new GridPosition(from.X - 1, from.Y),
        Direction.Right => new GridPosition(from.X + 1, from.Y),
        Direction.Up => new GridPosition(from.X, from.Y - 1),
        _ => new GridPosition(from.X, from.Y + 1),
    };

    /// <summary>
    /// Whether anything in the party knows the move that crosses water.
    /// <para>
    /// <b>READ, and it is the cartridge's own condition.</b> The one block in this image that
    /// offers to cross water — <c>--who-knows</c> finds it at <c>0x081A6AD6</c>, jumped into,
    /// on no map, saying <em>the water is dyed a deep blue… would you like to SURF?</em> —
    /// opens by asking who knows the move and stops if the answer is nobody. So does this.
    /// </para>
    /// <para>
    /// Which move that is comes off the cartridge twice over: the move table's own name, and
    /// the move that block names. Zero when this cartridge has no such move, and zero means no
    /// swimming rather than a guess at which move it might have been.
    /// </para>
    /// </summary>
    private static bool KnowsHowToCross(GameRules rules, IReadOnlyCollection<int> moves) =>
        rules.SurfMove > 0 && moves.Contains(rules.SurfMove);

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

    /// <summary>
    /// Where somebody can be spoken to from: beside them, and across a counter.
    /// <para>
    /// <b>The rule that decides what this whole project can reach, and it was one square too
    /// strict.</b> Every shop clerk in this game stands behind a
    /// <see cref="MetatileBehaviour.Counter"/> square, so a walk requiring orthogonal adjacency
    /// stood in front of at most ONE counter in the entire cartridge — 11 of 11, 14 of 14 and
    /// 19 of 19 of the ones it missed were exactly two squares from the nearest floor it stood
    /// on, at every lever setting, with no exceptions and no tail.
    /// </para>
    /// <para>
    /// This is READ and not modelled. <c>0x80</c> was measured two ways before it was given a
    /// name — by what it stands beside (91.9% against an 8.9% control) and by its own shape
    /// (22.5% against 0.3%) — and the evidence is written out on the constant.
    /// </para>
    /// <para>
    /// One square of counter, not a line of them: the square between must itself be the
    /// counter, so this reaches exactly two away and only through that one value. A wall two
    /// away is still a wall, which is the discrimination the fixture has to make.
    /// </para>
    /// </summary>
    private static IEnumerable<(string, GridPosition)> SpokenToFrom(MapData map, GridPosition at)
    {
        foreach ((string, GridPosition) near in Beside(map.Id, at)) yield return near;

        foreach (GridPosition way in (GridPosition[])
        [
            at with { Y = at.Y - 1 },
            at with { Y = at.Y + 1 },
            at with { X = at.X - 1 },
            at with { X = at.X + 1 },
        ])
        {
            if (map.BehaviourAt(way) != MetatileBehaviour.Counter) continue;

            yield return (map.Id, new GridPosition(
                way.X + (way.X - at.X),
                way.Y + (way.Y - at.Y)));
        }
    }
}
