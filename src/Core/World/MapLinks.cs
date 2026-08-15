namespace PokeMmo.Core.World;

/// <summary>Which edge of a map a neighbour is joined to.</summary>
public enum ConnectionSide
{
    Down,
    Up,
    Left,
    Right,
}

/// <summary>
/// A neighbouring map joined along an edge.
/// <para>
/// <see cref="Offset"/> slides the neighbour along that edge, in squares, and is
/// signed — a route wider than the town below it hangs off in one direction or the
/// other. It is what makes walking off the bottom of Pallet Town arrive at the right
/// column of Route 1 rather than at column zero.
/// </para>
/// </summary>
public sealed record MapConnection(ConnectionSide Side, int Offset, string MapId);

/// <summary>
/// A square that moves a player somewhere else: a door, a stairway, a cave mouth.
/// <para>
/// The destination is named as a warp on the target map rather than as coordinates.
/// That is the cartridge's own arrangement and it is the right one — a door leads to
/// "the other side of that door", so the two ends stay consistent even when one map
/// is edited.
/// </para>
/// </summary>
/// <summary>
/// A door a script makes, rather than one a square is.
/// <para>
/// Every warp above is a record on a map: stand here, arrive there. Those are the doors
/// this project has ever walked through, and they leave 179 of 425 maps with nothing
/// leading in — a whole archipelago, five department store floors, and everything behind
/// them. That was written down for a dozen milestones as a fact about geometry.
/// </para>
/// <para>
/// It never was. <c>warp</c> is also a script command — a bank and a map, a warp id and a
/// square — and a script can run one from anywhere, for any reason, with no square on any
/// map to say so. A boat is a door of this kind. So is a lift, and so is being thrown out
/// of somewhere.
/// </para>
/// <para>
/// Carried in the world file for the ordinary reason: reading it means reading scripts,
/// an operator's cartridge is the only thing that can, and a server with no cartridge
/// cannot invent one. The cartridge address it was read from stays on the cartridge.
/// </para>
/// </summary>
public sealed record ScriptedDoor(string What, string TargetMapId, int TargetWarpId, int X, int Y)
{
    public GridPosition Square => new(X, Y);

    public override string ToString() => $"{What} -> {TargetMapId} warp {TargetWarpId} at ({X},{Y})";
}

/// <summary>
/// A place a boat calls at, and the number the cartridge knows it by.
/// <para>
/// The one door in this game that is neither a square nor a script. The ferry's script
/// writes down where it is standing, hands the screen to the game's own code and ends —
/// and what that code does was, for a dozen milestones, the far side of the only boundary
/// this project cannot read across.
/// </para>
/// <para>
/// It did not have to be read. What a routine does is unreadable; what the scripts around
/// it say is not, and they say everything: ten places in this cartridge write a number
/// into the same argument slot and then hand over to the same routine as the last thing
/// they ever do, and no two of them write the same number. That is a table of destinations
/// written in plain sight by the scripts that use it.
/// </para>
/// </summary>
public sealed record FerryDock(int Number, int Attendant, int ArrivalX, int ArrivalY)
{
    /// <summary>Where somebody arriving by sea is put down.</summary>
    public GridPosition Arrival => new(ArrivalX, ArrivalY);

    public override string ToString() =>
        $"dock {Number}, attendant {Attendant}, arriving at ({ArrivalX},{ArrivalY})";
}

public sealed record Warp(int X, int Y, int TargetWarpId, string TargetMapId)
{
    /// <summary>
    /// A destination warp id the games use to mean "no matching warp" — the player
    /// arrives at the target warp's own square instead.
    /// </summary>
    public const int Unspecified = 0xFF;

    /// <summary>
    /// The bank and map number that mean "back the way you came".
    /// <para>
    /// Derived rather than remembered: nineteen warps on this cartridge name map 127.127,
    /// which no map bank has, and every one of them is the exit of a room with a single
    /// way in and many ways to it — the CABLE CLUB above every POKeMON CENTER, the lifts
    /// in SILPH CO. and the ROCKET HIDEOUT. A room reached from twelve places cannot
    /// write down which one it came from, so the games do not: they put a sentinel there
    /// and remember at runtime.
    /// </para>
    /// <para>
    /// It matters because a walker counting these as "a map this world file does not
    /// have" is a walker reporting a hole where the cartridge has a door.
    /// </para>
    /// </summary>
    public const int Dynamic = 127;

    /// <summary>True when this door leads back wherever the player came from.</summary>
    public bool IsDynamic => TargetMapId == $"{Dynamic}.{Dynamic}";

    public GridPosition Square => new(X, Y);
}

/// <summary>
/// A square that runs a script when somebody walks onto it.
/// <para>
/// The third of the four lists in a map's events record, and the one most of a Pokémon
/// game's story is made of: the professor stopping you at the edge of town, the rival
/// waiting on a route, every cutscene in the game. Nothing here is talked to — it
/// happens because you stood somewhere.
/// </para>
/// <para>
/// <see cref="Variable"/> and <see cref="Value"/> are the condition. A trigger fires
/// only while that variable holds that value, which is how a beat that has already
/// happened stops happening: the script's last act is to write the variable to
/// something else, and the square goes quiet forever.
/// </para>
/// </summary>
public sealed record MapTrigger(
    int X,
    int Y,
    int Variable,
    int Value,
    uint ScriptAddress = 0,
    IReadOnlyList<int>? Fights = null)
{
    /// <summary>
    /// Every trainer this square can pick a fight with — usually none, sometimes one,
    /// and at the lab door three.
    /// <para>
    /// A list rather than a number because the rival is three trainers. His script
    /// compares which starter was taken and fields the boy holding the type yours loses
    /// to, so the fight behind that square is not a fact about the square: it is a fact
    /// about the save, and the save lives on the other side of the split.
    /// </para>
    /// <para>
    /// What this side keeps is the set, which is the part a server with no cartridge can
    /// honestly hold. The client runs the script and names one; the server checks the
    /// name came from this list. Neither half is enough alone, which is the usual shape.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Fights { get; init; } = Fights ?? [];

    public GridPosition Square => new(X, Y);

    /// <summary>True when there is anything at all to run here.</summary>
    public bool HasScript => ScriptAddress != 0;

    /// <summary>True when walking onto this one starts a fight.</summary>
    public bool CanBeFought => Fights.Count > 0;

    /// <summary>Whether a trainer a client has named is one this square could produce.</summary>
    public bool Fields(int trainerId) => Fights.Contains(trainerId);

    // A record's generated equality compares the list by reference, which would make two
    // triggers read from the same cartridge unequal and every world-file round-trip test
    // fail for a reason that has nothing to do with what changed.
    public bool Equals(MapTrigger? other) =>
        other is not null &&
        X == other.X && Y == other.Y &&
        Variable == other.Variable && Value == other.Value &&
        ScriptAddress == other.ScriptAddress &&
        Fights.SequenceEqual(other.Fights);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(X);
        hash.Add(Y);
        hash.Add(Variable);
        hash.Add(Value);
        hash.Add(ScriptAddress);

        foreach (int id in Fights) hash.Add(id);

        return hash.ToHashCode();
    }

    /// <summary>
    /// Whether this one is armed, given what a save holds.
    /// <para>
    /// Zero and absent are the same thing here, as everywhere else in this project: the
    /// games start every save with the variable space zeroed, so a trigger asking for
    /// zero is a trigger that is armed from the beginning.
    /// </para>
    /// </summary>
    public bool Armed(int held) => held == Value;
}

/// <summary>
/// Something written on a map that can be read from the square in front of it: a sign,
/// a notice board, a bookshelf, a television.
/// <para>
/// The third of the four lists in a map's events record, and one this project never
/// read. It is not a person — it occupies no square, moves for nobody, and has no
/// local id — which is why every sign in the game has until now been a solid block of
/// scenery with nothing behind it.
/// </para>
/// <para>
/// <see cref="Kind"/> is the cartridge's own tag and the reason it is kept: one value
/// of it means the record holds an item id where every other value holds a script
/// pointer. A hundred and eighty-three of the seven hundred are that kind, and reading
/// their item id as an address is how a reader ends up following a pointer to 0x0000.
/// </para>
/// </summary>
public sealed record MapSign(int X, int Y, int Kind, uint ScriptAddress)
{
    /// <summary>
    /// The tag that means "there is something buried here", not "here is a script".
    /// <para>
    /// Derived rather than known: of the seven hundred sign records on FireRed, the
    /// hundred and eighty-three whose last word is not a usable pointer are exactly the
    /// hundred and eighty-three carrying this tag, and no others.
    /// </para>
    /// </summary>
    public const int HiddenItem = 7;

    public bool IsHiddenItem => Kind == HiddenItem;

    /// <summary>True when there is something here to read.</summary>
    public bool HasScript => ScriptAddress != 0 && !IsHiddenItem;

    public GridPosition Square => new(X, Y);
}

/// <summary>
/// Something a map runs on arrival, when one of its variables says so.
/// <para>
/// The fifth list — the one the map header has always pointed at and nothing has ever
/// read. It is what advances the story between the scenes attached to squares: the
/// professor's lab has three squares waiting for 0x4055 to hold 2, and nothing anywhere
/// in the world's people, signs or triggers ever sets it to 2. Walking through the door
/// does.
/// </para>
/// <para>
/// Same shape as a trigger's condition on purpose, because it is the same question asked
/// somewhere else: does this variable hold this value. What differs is only when it is
/// asked — on a square, or on a doorway.
/// </para>
/// </summary>
public sealed record MapEntryScript(int Variable, int Value, uint ScriptAddress)
{
    /// <summary>
    /// What arriving here hands over, if it hands anything over.
    /// <para>
    /// The shop in Viridian is the one that needed this. Walking in runs an arrival
    /// script, the clerk asks whether you know the professor, and the script hands over
    /// the parcel the rest of the story turns on — and nobody is being talked to, so
    /// none of the machinery that gives a person's gift applies.
    /// </para>
    /// <para>
    /// Read at export by running the script, the same way a person's gift is. The world
    /// file is the server's only knowledge of the cartridge, so anything it has to hand
    /// over has to be in there.
    /// </para>
    /// </summary>
    public int GivesItemId { get; init; }

    public int GivesCount { get; init; }

    public bool Gives => GivesItemId != 0;

    public bool HasScript => ScriptAddress != 0;

    public bool Armed(int held) => held == Value;

    /// <summary>
    /// Every script a doorway has armed, in the order the cartridge wrote them.
    /// <para>
    /// All of them, and this is the whole point of the method existing. A doorway can
    /// have more than one thing armed at once — the professor's lab has two on the same
    /// value of the same variable — and taking the first one meant taking the one whose
    /// read stops at its first command and does nothing at all. The scene that carries
    /// the story out of that room was second in the list.
    /// </para>
    /// </summary>
    public static List<uint> ArmedIn(IEnumerable<MapEntryScript> entries, Func<int, int> read) =>
    [
        .. entries
            .Where(e => e.HasScript && e.Armed(read(e.Variable)))
            .Select(e => e.ScriptAddress)
            .Distinct(),
    ];
}

/// <summary>
/// Somebody standing on a map: a person, a sign-poster, a rooted tree.
/// <para>
/// Called an object event on the cartridge, which covers anything that occupies a
/// square and is not scenery. Only what is needed to place one and draw it is kept —
/// the script that decides what it says is a separate problem, and a large one.
/// </para>
/// </summary>
/// <summary>A fight a script sets up: one creature, at one level, from nowhere.</summary>
public sealed record WildFight(int Species, int Level);

public sealed record MapObject(
    int LocalId,
    int GraphicsId,
    int X,
    int Y,
    Direction Facing,
    int MovementType,
    bool IsTrainer,
    int RangeX = 0,
    int RangeY = 0,
    uint ScriptAddress = 0,
    int TrainerId = 0,
    int SightRange = 0,
    IReadOnlyList<int>? Sells = null)
{
    /// <summary>What this one sells, which for almost everybody is nothing.</summary>
    public IReadOnlyList<int> Stock { get; init; } = Sells ?? [];

    /// <summary>True when talking to this one opens a shop.</summary>
    public bool IsShopkeeper => Stock.Count > 0;

    /// <summary>
    /// True when talking to this one puts the party back on its feet.
    /// <para>
    /// A fact about a person rather than about a map, and carried in the world file
    /// because the server cannot work it out: what heals a party is a routine in the
    /// game's own code, which is not data and cannot be read. What can be read is that
    /// every nurse in the game hands her work to one shared script, and that script is
    /// located at export by counting who calls it.
    /// </para>
    /// </summary>
    public bool Heals { get; init; }

    /// <summary>
    /// What talking to this one hands over, if it hands over anything.
    /// <para>
    /// A ball lying on the ground is a person like any other, with a script that writes
    /// an item id and a count into the two argument variables and calls a standard
    /// routine to do the giving. This project has never been able to follow one of those
    /// routines and did not need to: both numbers are written down in front of the call.
    /// </para>
    /// <para>
    /// A hundred and seventy-three of them across the world, and every one was a person
    /// whose script ran to a clean end and produced nothing at all.
    /// </para>
    /// </summary>
    /// <summary>
    /// What talking to this one takes away, if it takes anything.
    /// <para>
    /// Oak taking the parcel is the one that needed it, and it is the other half of a
    /// delivery: the shop hands it over and he receives it, and until he did the story
    /// had nowhere to go.
    /// </para>
    /// <para>
    /// Taken only from somebody who has it. The script reaches this on a branch the
    /// server cannot evaluate — it has no cartridge — but the condition that branch
    /// turns on is "you have the parcel", so removing it only when it is there
    /// reproduces the cartridge's own behaviour without taking a client's word for
    /// anything.
    /// </para>
    /// </summary>
    public int TakesItemId { get; init; }

    public int TakesCount { get; init; }

    public bool Takes => TakesItemId != 0;

    public int GivesItemId { get; init; }

    public int GivesCount { get; init; }

    /// <summary>True when there is something here to pick up.</summary>
    public bool GivesItem => GivesItemId != 0;

    /// <summary>
    /// What beating this one pays out, beyond the prize money.
    /// <para>
    /// A separate field from <see cref="GivesItemId"/> because the moment is different,
    /// and the moment is the whole of it: this is not handed over by talking. BROCK's
    /// TM39 is inside the script his <c>trainerbattle</c> runs when it is won, which is
    /// a place no conversation reaches — talk to him before the fight and he says he is
    /// PEWTER's gym leader; talk to him after and he muses about trainers everywhere.
    /// Neither branch mentions the TM.
    /// </para>
    /// <para>
    /// Kept on this side of the split for the ordinary reason: a client that could say
    /// "the script gave me a TM" could say it about anything.
    /// </para>
    /// </summary>
    public int WinsItemId { get; init; }

    public int WinsCount { get; init; }

    public bool WinsItem => WinsItemId != 0;

    /// <summary>
    /// Every item this one's script could hand over, whichever branch it takes.
    /// <para>
    /// Not the answer but the list of answers, which is the same arrangement a trigger's
    /// trainer ids already use: which fossil somebody ends up with depends on what they
    /// said to a yes/no, and that is a fact about a save rather than about a cartridge.
    /// The server cannot run the script and will not take a client's word for it, so it
    /// holds the set and checks against it.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> CanGive { get; init; } = [];

    /// <summary>
    /// The wild battles this one's script can start, as species and level.
    /// <para>
    /// The same arrangement as <see cref="CanGive"/> and for the same reason: which
    /// battle, if any, depends on what was said to a yes/no, so the client names it and
    /// the server checks the name against this. Ten scripts in the game set one up, and
    /// they are the ones worth being careful about — the two sleepers across the roads
    /// south and west, the three birds, and MEWTWO at level 70.
    /// </para>
    /// </summary>
    public IReadOnlyList<WildFight> CanFight { get; init; } = [];

    /// <summary>
    /// The monster this one hands over, or zero. May be a variable id rather than a
    /// species, which is how the three balls on the professor's table are one script.
    /// <para>
    /// Read from the script rather than run out of it, for the same reason an obstacle's
    /// move is: what a thing <em>is</em> does not depend on whether today's save can get
    /// at it yet.
    /// </para>
    /// </summary>
    public int GivesSpecies { get; init; }

    /// <summary>The level it is handed over at. Five for a starter, twenty-five for the rest.</summary>
    public int GivesLevel { get; init; }

    public bool GivesMon => GivesSpecies != 0;

    /// <summary>Where a variable id stops being a species and starts being a lookup.</summary>
    public const int FirstVariable = 0x4000;

    /// <summary>
    /// The flag that takes this one off the map, or zero.
    /// <para>
    /// Six hundred and five of the cartridge's sixteen hundred objects carry one, and it
    /// is how a Pokémon game has anybody appear and disappear: a rival who is only in the
    /// lab on the day you choose, a guard who moves once you have his tea, a professor
    /// who is standing in the street until the moment he takes you indoors.
    /// </para>
    /// <para>
    /// Set means hidden, and Pallet Town says so on its own. It has three people and
    /// exactly one of them carries a number — object 3, the professor, whose flag is
    /// 0x2C — and the opening script's last act, once he has walked you to his lab and
    /// gone in, is <c>setflag 0x2C</c>.
    /// </para>
    /// </summary>
    public int HiddenBy { get; init; }

    /// <summary>Whether this one is on the map, for somebody whose flags read like this.</summary>
    public bool IsHereFor(Func<int, bool> flagIsSet) => HiddenBy == 0 || !flagIsSet(HiddenBy);

    /// <summary>
    /// True when this one has something to say.
    /// <para>
    /// Carried because the server cannot know it. A ball on the ground and a person who
    /// hands you something while thanking you are the same record with the same item on
    /// it, and they need opposite treatment: one is picked up and that is the whole
    /// interaction, the other has to be held still while their lines are read.
    /// </para>
    /// <para>
    /// Fifteen people in FireRed are the second kind, and every one of them would have
    /// had their line replaced by "Found one POTION!" — the Silph president included.
    /// </para>
    /// </summary>
    public bool Talks { get; init; }

    /// <summary>
    /// The move that shifts this one out of the way, or zero for anything that is not
    /// in the way.
    /// <para>
    /// Two hundred objects across forty-seven maps: the cut trees, the strength boulders
    /// and the rock-smash rubble. Each one's script opens by naming a move and asking
    /// which party slot knows it, and the answer decides between two entirely different
    /// conversations. The move id is carried here because deciding needs the script, and
    /// the server has never seen one.
    /// </para>
    /// </summary>
    public int ShiftedBy { get; init; }

    /// <summary>True when this one is in the way rather than in the world.</summary>
    public bool IsObstacle => ShiftedBy != 0;

    /// <summary>
    /// Compares stock by its contents.
    /// <para>
    /// A record compares its members with <c>Equals</c>, and for a list that is
    /// reference equality. Third time this project has needed saying, and the world
    /// file's round-trip test is exactly the kind that would go quietly green without it.
    /// </para>
    /// </summary>
    public bool Equals(MapObject? other) =>
        other is not null &&
        LocalId == other.LocalId &&
        GraphicsId == other.GraphicsId &&
        X == other.X &&
        Y == other.Y &&
        Facing == other.Facing &&
        MovementType == other.MovementType &&
        IsTrainer == other.IsTrainer &&
        RangeX == other.RangeX &&
        RangeY == other.RangeY &&
        ScriptAddress == other.ScriptAddress &&
        TrainerId == other.TrainerId &&
        SightRange == other.SightRange &&
        Heals == other.Heals &&
        GivesItemId == other.GivesItemId &&
        GivesCount == other.GivesCount &&
        WinsItemId == other.WinsItemId &&
        WinsCount == other.WinsCount &&
        CanGive.SequenceEqual(other.CanGive) &&
        CanFight.SequenceEqual(other.CanFight) &&
        Talks == other.Talks &&
        ShiftedBy == other.ShiftedBy &&
        Stock.SequenceEqual(other.Stock);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(LocalId);
        hash.Add(X);
        hash.Add(Y);
        hash.Add(TrainerId);

        foreach (int itemId in Stock) hash.Add(itemId);

        return hash.ToHashCode();
    }

    /// <summary>True when talking to this one would do something.</summary>
    public bool HasScript => ScriptAddress != 0;
    public GridPosition Square => new(X, Y);

    /// <summary>True when this one has a party the server can actually field.</summary>
    /// <summary>
    /// Whether there is a fight here at all.
    /// <para>
    /// The trainer id alone, and not the record's mark. The mark says somebody watches
    /// the corridor and walks over; a gym leader does neither and still fights, which is
    /// what "talk to the leader" means. Requiring both left BROCK unfightable.
    /// </para>
    /// </summary>
    public bool CanBeFought => TrainerId != 0;

    /// <summary>
    /// Whether a player standing here is in this trainer's line of sight.
    /// <para>
    /// A straight line in the direction they are facing, out to their sight range, and
    /// nothing to either side. Written as "same column, in front, within range" rather
    /// than as a distance, because a distance would have them notice somebody standing
    /// diagonally — which they famously do not.
    /// </para>
    /// <para>
    /// Whether anything is <em>between</em> the two is not decided here. That needs the
    /// map, and this record has never seen one.
    /// </para>
    /// </summary>
    public bool CanSee(GridPosition square)
    {
        if (SightRange <= 0) return false;

        (int alongX, int alongY) = Facing switch
        {
            Direction.Up => (0, -1),
            Direction.Down => (0, 1),
            Direction.Left => (-1, 0),
            _ => (1, 0),
        };

        for (int step = 1; step <= SightRange; step++)
        {
            if (square == new GridPosition(X + alongX * step, Y + alongY * step)) return true;
        }

        return false;
    }

    /// <summary>The squares between this one and a player they can see, nearest first.</summary>
    public IEnumerable<GridPosition> ApproachTo(GridPosition square)
    {
        (int alongX, int alongY) = Facing switch
        {
            Direction.Up => (0, -1),
            Direction.Down => (0, 1),
            Direction.Left => (-1, 0),
            _ => (1, 0),
        };

        for (int step = 1; step <= SightRange; step++)
        {
            var next = new GridPosition(X + alongX * step, Y + alongY * step);

            // Stops one short: walking onto the player rather than up to them would put
            // two characters on one square.
            if (next == square) yield break;

            yield return next;
        }
    }

    /// <summary>True when this one paces about rather than standing still.</summary>
    public bool Wanders => MovementType is 2 or 3 or 4 or 5 or 6;

    /// <summary>
    /// True when this one will eventually get out of the way on its own.
    /// <para>
    /// Wandering is not enough. The range is a box around where they started and it is
    /// often nothing at all — a person whose movement type says "walks about" and whose
    /// range is zero in both axes turns on the spot forever, which to anybody trying to
    /// get past is the same as standing still.
    /// </para>
    /// <para>
    /// This is what tells a wall from a wait. It is asked by the walker rather than by
    /// the server: to the server everybody is solid, because at any instant they are.
    /// </para>
    /// </summary>
    public bool CanStepAside => Wanders && (RangeX > 0 || RangeY > 0);

    /// <summary>
    /// True when this is a thing lying on the ground rather than somebody standing on it.
    /// <para>
    /// It hands something over and it can be hidden, which together is the shape of a
    /// ball on the floor of a cave: you pick it up, a flag is set, and it is not there
    /// any more. Somebody who hands something over <em>while talking</em> has no hiding
    /// flag and stays exactly where they were.
    /// </para>
    /// <para>
    /// "Hands something over" has to include what a script hands over, not only what the
    /// object record says. The two fossils on the floor of MT. MOON give theirs from a
    /// script — the record's own item field is empty — and between them they were worth
    /// 137 maps: they sit side by side across the corridor to the east exit, so with
    /// both of them counted as walls the road to CERULEAN was shut and the game was 36
    /// maps large.
    /// </para>
    /// <para>
    /// Like <see cref="CanStepAside"/>, this is a question about whether a square is
    /// closed forever, and only a walker measuring the world has any use for it. Right
    /// now the ball is solid, and the server treats it so.
    /// </para>
    /// </summary>
    public bool CanBeTakenAway => (GivesItem || CanGive.Count > 0) && HiddenBy != 0;

    /// <summary>True when this one turns on the spot without going anywhere.</summary>
    public bool LooksAround => MovementType == 1;

    /// <summary>
    /// Whether a square is within this one's beat.
    /// <para>
    /// The range is a box around where they started, and it is per-axis: a shopkeeper
    /// pacing left and right has a range in x and none in y. Ignoring it would let
    /// everybody wander off across the map, which is both wrong and a good way to
    /// block a doorway nobody expected to be blocked.
    /// </para>
    /// </summary>
    public bool IsWithinRange(GridPosition square) =>
        Math.Abs(square.X - X) <= RangeX && Math.Abs(square.Y - Y) <= RangeY;

    /// <summary>
    /// Which way one of these starts out looking.
    /// <para>
    /// The movement type says both how it moves and where it faces to begin with.
    /// Wandering in a direction and standing still facing it are different numbers
    /// with the same starting look, which is why both map to the same facing here.
    /// </para>
    /// </summary>
    public static Direction FacingFor(int movementType) => movementType switch
    {
        3 or 7 => Direction.Up,
        4 or 8 => Direction.Down,
        5 or 9 => Direction.Left,
        6 or 10 => Direction.Right,
        _ => Direction.Down,
    };
}
