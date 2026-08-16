namespace PokeMmo.Core.Scripts;

/// <summary>
/// How one script's condition came out: less, equal or greater.
/// <para>
/// Gen III scripts have no expressions. A command puts a comparison somewhere and the
/// jump that follows names which outcomes it wants — so the condition and the branch
/// are two separate instructions with a single register between them, and reading
/// either one alone tells you nothing.
/// </para>
/// </summary>
public enum Comparison
{
    Less,
    Equal,
    Greater,
}

/// <summary>
/// Everything a script has already done to a save: which flags are set and what the
/// variables hold.
/// <para>
/// This is in <c>Core</c> and not beside the reader on purpose. The bytes live on the
/// cartridge and only the client has one; the answers live in the save and only the
/// server has that. The two have to meet somewhere, and it is cheaper for the state to
/// be portable than for either side to grow the other's half.
/// </para>
/// <para>
/// A flag that was never set and a variable that was never written are both simply
/// absent. The games start every save with the whole space zeroed, so absent and zero
/// have to mean the same thing or a fresh character reads as having done everything.
/// </para>
/// </summary>
public sealed class ScriptState
{
    /// <summary>
    /// The answer a script gets when nobody in the party knows the move it asked about.
    /// <para>
    /// Six because a party has six slots, so the sixth index is the first one that
    /// cannot be a member. The cartridge's own scripts compare against exactly this and
    /// branch to "nobody here can do that" — which is how the number was read rather
    /// than chosen.
    /// </para>
    /// </summary>
    public const int NoSlot = 6;

    /// <summary>
    /// Which of the two sets of words this character reads.
    /// <para>
    /// Derived, not remembered. Command 0xA0 takes nothing and answers into the result
    /// variable, and the two arms of the fork after it are the cartridge's own words at
    /// every site — "Waiter" and "Waitress", "little brother" and "little sister", "All
    /// boys leave home someday" and "All girls dream of traveling", "dear boy" and "dear
    /// girl". Seven scripts on six maps, agreeing.
    /// </para>
    /// <para>
    /// Zero and one, in that order, because that is the order the branches take: the arm
    /// reached when the answer is zero is the one that says "boy" every time.
    /// </para>
    /// </summary>
    public bool IsGirl { get; init; }

    /// <summary>
    /// Who to put where the cartridge's dialogue leaves a gap.
    /// <para>
    /// 0xFD in a run of text is a marker meaning "something goes here" and the byte after
    /// it says what. The four this cartridge uses were derived by counting every site and
    /// reading the sentences: 0x01 appears 109 times and is always the person being
    /// spoken to or acted upon — "{FD}{01} obtained HM01 from the CAPTAIN!" — and 0x06
    /// appears 33 times and is always a speaker's label before a colon, in the mouth of
    /// the boy who follows you around the game.
    /// </para>
    /// <para>
    /// 0x02, 0x03 and 0x04 are not species. They are gaps, and what goes in one depends
    /// on which command filled it — that correction came from the professor, who says
    /// "{FD}{02} POKeMON seen and {FD}{03} POKeMON owned" once the parcel is delivered.
    /// Every one of the nineteen sites reachable from a fresh save fills them with a
    /// species, which is why they were called that; the sentence that proves otherwise
    /// is behind the story.
    /// </para>
    /// <para>
    /// Three commands fill them, and all three name the gap off by two: 0x7D a species,
    /// 0x83 a number — from a literal or a variable — and 0x80 an item. The rest are
    /// filled by the game's own code, which this project does not follow: the fishing
    /// guru's "{FD}{03} inches" is measured by a special routine, and no width would
    /// ever have produced it.
    /// </para>
    /// <para>
    /// The rival's name is a placeholder and is meant to look like one. The cartridge
    /// offers a menu of names for him during an intro this game does not run, and that
    /// menu has not been located — so writing a name here out of memory of the games
    /// would be exactly the kind of guess this project does not make.
    /// </para>
    /// </summary>
    public string PlayerName { get; set; } = "";

    public string RivalName { get; set; } = "RIVAL";

    /// <summary>
    /// What to call a species, when there is anybody about who knows.
    /// <para>
    /// A delegate because the answer lives on a cartridge and this class is in the half
    /// of the project that has never seen one. The server fills nothing in: its rules
    /// file carries no names of any kind, deliberately, and it has no text to render.
    /// </para>
    /// </summary>
    public Func<int, string>? NameOfSpecies { get; set; }

    /// <summary>
    /// What to call an item, on the same terms.
    /// <para>
    /// The professor's aides are the ones who need it: "PROF. OAK entrusted me with the
    /// {FD}{03} for you" leaves the gap and the script names the item into it, three
    /// commands before the sentence and five different items across five maps.
    /// </para>
    /// </summary>
    public Func<int, string>? NameOfItem { get; set; }

    /// <summary>
    /// How many of an item the player is carrying, when anybody has said.
    /// <para>
    /// A delegate rather than a bag, for the reason the bag is not held here: what a
    /// player carries changes every time they pick something up, and each of the three
    /// callers keeps it somewhere different. The server has a <c>Bag</c> on the player,
    /// the client has whatever the last <c>BagUpdated</c> said, and the playthrough has
    /// one of its own. A copy taken here would be right until the next ball on the floor.
    /// </para>
    /// <para>
    /// The same arrangement <see cref="PartyMoves"/> has and for the same reason —
    /// attached at the point of asking rather than kept — and the same one
    /// <see cref="NameOfItem"/> has, for a different one.
    /// </para>
    /// </summary>
    public Func<int, int>? CountOfItem { get; set; }

    /// <summary>
    /// How many of an item this save holds. None when nobody has said.
    /// <para>
    /// Nothing rather than a refusal, because that is what the cartridge's own answer
    /// is worth here: a script asking whether you have something and being told nothing
    /// takes the arm it takes for a player who has not got one, which is the arm the
    /// whole game took before anybody was carrying anything.
    /// </para>
    /// </summary>
    public int Carried(int itemId) => itemId <= 0 ? 0 : CountOfItem?.Invoke(itemId) ?? 0;

    private readonly HashSet<int> _flags;
    private readonly Dictionary<int, int> _variables;
    private readonly HashSet<int> _beaten;
    private readonly List<IReadOnlyList<int>> _partyMoves;

    public ScriptState(
        IEnumerable<int>? flags = null,
        IEnumerable<KeyValuePair<int, int>>? variables = null,
        IEnumerable<int>? beaten = null,
        IEnumerable<IReadOnlyList<int>>? partyMoves = null)
    {
        _flags = flags is null ? [] : [.. flags];
        _variables = variables is null ? [] : new Dictionary<int, int>(variables);
        _beaten = beaten is null ? [] : [.. beaten];
        _partyMoves = partyMoves is null ? [] : [.. partyMoves];
    }

    /// <summary>
    /// What each party member knows, in party order.
    /// <para>
    /// Here rather than in the party itself because a script is the only thing that asks.
    /// Two hundred objects in this game — every cut tree, every boulder, every heap of
    /// rubble — open by naming a move and asking who has it, and the answer decides
    /// which of two completely different conversations happens.
    /// </para>
    /// </summary>
    public IReadOnlyList<IReadOnlyList<int>> PartyMoves => _partyMoves;

    /// <summary>
    /// Which party slot knows a move, or <see cref="NoSlot"/> for none.
    /// <para>
    /// The first one, because the games use the answer to decide who steps forward and
    /// there is only room for one to.
    /// </para>
    /// </summary>
    public int SlotKnowing(int moveId) => SlotKnowing(_partyMoves, moveId);

    /// <summary>
    /// The same question asked of a party directly.
    /// <para>
    /// The server has a party and no script state worth building one from; the client
    /// has script state and runs the script with it. Both need this answer and it is the
    /// same answer, so it lives here once rather than twice.
    /// </para>
    /// </summary>
    public static int SlotKnowing(IEnumerable<IReadOnlyList<int>> partyMoves, int moveId)
    {
        if (moveId == 0) return NoSlot;

        int slot = 0;

        foreach (IReadOnlyList<int> moves in partyMoves)
        {
            if (slot >= NoSlot) break;
            if (moves.Contains(moveId)) return slot;

            slot++;
        }

        return NoSlot;
    }

    /// <summary>
    /// Trainers this save has already beaten, by id.
    /// <para>
    /// Beside the flags rather than among them, because it is not one. A
    /// <c>trainerbattle</c> command names a trainer and then a word that is not a flag
    /// number — on a real FireRed image that word is zero for every trainer on Route 8,
    /// and this project spent a commit believing otherwise. Whatever the games use to
    /// remember a beaten trainer is not written in the script, so there is nothing to
    /// read and no number to guess at.
    /// </para>
    /// <para>
    /// The id is used instead. It is this project's own numbering, the server has
    /// persisted it since trainers existed, and it survives a re-export — which the
    /// cartridge's own numbering, whatever it is, would not need to.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<int> Beaten => _beaten;

    public bool HasBeaten(int trainerId) => _beaten.Contains(trainerId);

    public bool MarkBeaten(int trainerId) => _beaten.Add(trainerId);

    /// <summary>Throws the story away, which is what an operator's console is for.</summary>
    public void Forget()
    {
        _flags.Clear();
        _variables.Clear();
    }

    public IReadOnlyCollection<int> Flags => _flags;

    public IReadOnlyDictionary<int, int> Variables => _variables;

    public bool Has(int flag) => _flags.Contains(flag);

    /// <summary>Sets a flag, and says whether that changed anything.</summary>
    public bool Set(int flag) => _flags.Add(flag);

    public bool Clear(int flag) => _flags.Remove(flag);

    public int Read(int variable) => _variables.GetValueOrDefault(variable);

    public void Write(int variable, int value)
    {
        // Zero is the absence of a value rather than a value, so that a save which has
        // written a variable back down to nothing is identical to one that never wrote
        // it — which is what the games' zeroed save space means.
        if (value == 0) _variables.Remove(variable);
        else _variables[variable] = value;
    }

    public ScriptState Copy() =>
        new ScriptState(_flags, _variables, _beaten, _partyMoves) { IsGirl = IsGirl }.As(this);

    /// <summary>
    /// The same state with a party attached, for the one run that needs one.
    /// <para>
    /// Attached at the point of asking rather than kept, because what a party knows
    /// changes every time one of them learns a move — and a copy held anywhere goes
    /// stale between the level-up and the next tree.
    /// </para>
    /// </summary>
    public ScriptState WithParty(IEnumerable<IReadOnlyList<int>> partyMoves) =>
        new ScriptState(_flags, _variables, _beaten, partyMoves) { IsGirl = IsGirl }.As(this);

    /// <summary>
    /// Carries the naming across a derived state.
    /// <para>
    /// Every caller of <see cref="Copy"/> and <see cref="WithParty"/> wants it and not
    /// one of them would remember to pass it, and a state that has forgotten who the
    /// player is renders "{FD}{01}" in the middle of a sentence rather than failing.
    /// </para>
    /// </summary>
    private ScriptState As(ScriptState other)
    {
        PlayerName = other.PlayerName;
        RivalName = other.RivalName;
        NameOfSpecies = other.NameOfSpecies;
        NameOfItem = other.NameOfItem;
        CountOfItem = other.CountOfItem;

        return this;
    }

    /// <summary>How two numbers compare, in the only three answers a script has.</summary>
    public static Comparison Compare(int left, int right) =>
        left < right ? Comparison.Less : left > right ? Comparison.Greater : Comparison.Equal;

    /// <summary>
    /// Whether a jump's condition byte accepts a comparison.
    /// <para>
    /// Six conditions over three outcomes, which is every useful combination but "never"
    /// and "always": below, equal, above, and the three negations of those. Written as a
    /// membership test rather than as six cases so that the one thing worth getting right
    /// — which numbers mean which — is a single readable line.
    /// </para>
    /// </summary>
    public static bool Accepts(byte condition, Comparison result) => condition switch
    {
        0 => result == Comparison.Less,
        1 => result == Comparison.Equal,
        2 => result == Comparison.Greater,
        3 => result != Comparison.Greater,
        4 => result != Comparison.Less,
        5 => result != Comparison.Equal,
        _ => false,
    };
}
