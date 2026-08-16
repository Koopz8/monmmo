using PokeMmo.Core.Battle;

namespace PokeMmo.Server;

/// <summary>
/// A fight between two people, and the two decisions a turn of one is waiting for.
/// <para>
/// The engine underneath is the same engine, unchanged. What is different is who the two
/// sides belong to: <see cref="Side.Player"/> is whoever was asked first and
/// <see cref="Side.Opponent"/> is whoever asked, and neither of those is "you" — each
/// client is told about the same turn in its own terms, by turning the events round.
/// </para>
/// <para>
/// A duel costs nothing and changes nothing. Both parties are copies made when it starts
/// and thrown away when it ends: no experience, no fainting that outlasts the fight, no
/// money, no black-out, no catching somebody else's creature. That is a decision rather
/// than a reading — the cartridge has no rule for this because the cartridge has no
/// second player — and it is the one that makes a duel something anybody would agree to
/// twice.
/// </para>
/// </summary>
public sealed class Duel
{
    private readonly List<Battler> _ones;
    private readonly List<Battler> _twos;

    private int _oneSlot;
    private int _twoSlot;

    private readonly MoveData? _struggle;

    private BattleAction? _oneChose;
    private BattleAction? _twoChose;

    public Duel(
        int one,
        int two,
        IReadOnlyList<Battler> ones,
        IReadOnlyList<Battler> twos,
        uint seed,
        MoveData? struggle = null)
    {
        if (ones.Count == 0 || twos.Count == 0)
            throw new ArgumentException("A duel needs somebody on both sides.");

        One = one;
        Two = two;
        _ones = [.. ones];
        _twos = [.. twos];

        _struggle = struggle;

        Current = Built(_ones[0], _twos[0], seed);
    }

    /// <summary>Whoever is <see cref="Side.Player"/> in the engine.</summary>
    public int One { get; }

    public int Two { get; }

    public Battle Current { get; private set; }

    public bool Has(int playerId) => playerId == One || playerId == Two;

    public int Other(int playerId) => playerId == One ? Two : One;

    /// <summary>Which side of the engine a player is on.</summary>
    public Side SideOf(int playerId) => playerId == One ? Side.Player : Side.Opponent;

    public int SlotOf(int playerId) => playerId == One ? _oneSlot : _twoSlot;

    public Battler ActiveFor(int playerId) => playerId == One ? Current.Player : Current.Opponent;

    public IReadOnlyList<Battler> TeamOf(int playerId) => playerId == One ? _ones : _twos;

    /// <summary>What this player has decided this turn, if they have decided.</summary>
    public BattleAction? ChoiceOf(int playerId) => playerId == One ? _oneChose : _twoChose;

    /// <summary>
    /// Writes down what somebody wants to do.
    /// <para>
    /// Kept rather than acted on. A turn where the faster player's move landed while the
    /// slower one was still reading the menu would not be a turn — it would be a race,
    /// and the reward for winning it would be going first every time.
    /// </para>
    /// </summary>
    public void Choose(int playerId, BattleAction action)
    {
        if (playerId == One) _oneChose = action;
        else _twoChose = action;
    }

    public bool BothHaveChosen => _oneChose is not null && _twoChose is not null;

    /// <summary>Runs the turn both sides have now decided, and forgets both decisions.</summary>
    public List<BattleEvent> Resolve()
    {
        BattleAction one = _oneChose ?? new BattleAction.UseMove(0);
        BattleAction two = _twoChose ?? new BattleAction.UseMove(0);

        _oneChose = null;
        _twoChose = null;

        return Current.ResolveTurn(one, two);
    }

    /// <summary>Everybody on a side who can still fight.</summary>
    public bool HasAnotherOne(int playerId) =>
        Team(playerId).Where((_, slot) => slot != SlotOf(playerId)).Any(b => !b.HasFainted);

    public bool IsBeaten(int playerId) => Team(playerId).All(b => b.HasFainted);

    /// <summary>
    /// Sends somebody out by choice rather than because somebody fainted.
    /// <para>
    /// Refused for a slot that is not in the team, the one already out, and anybody who
    /// cannot fight — all three are things a client could ask for and none of them is
    /// something a player could do, which is why they are decided here.
    /// </para>
    /// <para>
    /// It costs the turn. The engine is handed the switch and finds no move on it, which
    /// is what a switch is from the arithmetic's point of view: the one who comes out
    /// arrives to whatever the other side had already decided to do.
    /// </para>
    /// </summary>
    public Battler? SwitchTo(int playerId, int slot)
    {
        List<Battler> team = Team(playerId);

        if (slot < 0 || slot >= team.Count) return null;
        if (slot == SlotOf(playerId)) return null;
        if (team[slot].HasFainted) return null;

        return SendOut(playerId, slot);
    }

    /// <summary>
    /// Sends out the next one who can fight, because somebody fainted.
    /// <para>
    /// Chosen here rather than asked for. A duel that stopped to ask would need a third
    /// message and a screen that can wait on the other player twice in one turn, and the
    /// first version of a thing should be the version that can be played.
    /// </para>
    /// </summary>
    public Battler? SendNext(int playerId)
    {
        List<Battler> team = Team(playerId);

        for (int slot = 0; slot < team.Count; slot++)
        {
            if (team[slot].HasFainted) continue;

            return SendOut(playerId, slot);
        }

        return null;
    }

    /// <summary>
    /// Rebuilds the fight around one side's new creature, carrying the dice and the other
    /// side's exactly as they are.
    /// <para>
    /// A fresh battle has no stat stages in it, so stages go with the one who left. That
    /// falls out of the arrangement rather than needing a rule of its own — and it is the
    /// same arrangement a trainer fight has used since the beginning.
    /// </para>
    /// </summary>
    private Battler SendOut(int playerId, int slot)
    {
        List<Battler> team = Team(playerId);

        if (playerId == One) _oneSlot = slot; else _twoSlot = slot;

        // Swapped in place rather than rebuilt around.
        //
        // This used to build a whole new battle and keep only where the dice had got to,
        // which meant every field belonging to the *room* had to be copied across by hand
        // — and the call that does that was never made here. A duel's weather stopped the
        // moment anybody swapped, and the moment anybody fainted, because replacing a
        // fainted creature comes through this same method.
        //
        // Not fixed by adding the missing call. Fixed by removing the rebuild, so there is
        // nothing left to remember: the room is never torn down, so it cannot be dropped.
        Arriving = Current.Bring(playerId == One ? Side.Player : Side.Opponent, team[slot]);

        return team[slot];
    }

    /// <summary>
    /// A battle around these two, knowing both benches.
    /// <para>
    /// One place rather than three, because the three had drifted: the two that rebuilt on a
    /// switch forgot the room, and all three forgot the parties — so the one move that
    /// reaches past the field reached an empty list in every duel ever fought. A constructor
    /// call repeated is a constructor call that will differ.
    /// </para>
    /// </summary>
    private Battle Built(Battler one, Battler two, uint seed) =>
        new(one, two, seed)
        {
            IsWild = false,
            Struggle = _struggle,
            PlayerParty = _ones,
            OpponentParty = _twos,
        };

    /// <summary>
    /// What the last send-out caused, which is the incoming one's ability having its say.
    /// Held rather than returned, because every caller already wanted the creature back.
    /// </summary>
    public List<BattleEvent> Arriving { get; private set; } = [];

    /// <summary>
    /// Catches up with whatever the engine did to the field during a turn, and says who
    /// changed.
    /// <para>
    /// The switch is the engine's now, so the two slot numbers this class keeps are no
    /// longer the only record of who is standing there — they are a cache of it, and a
    /// cache has to be reconciled rather than trusted. Asked once after every turn, and it
    /// answers nothing on the ordinary turn where nobody swapped.
    /// </para>
    /// </summary>
    public List<(int Who, Battler Sent)> CatchUp()
    {
        var changed = new List<(int, Battler)>();

        int one = _ones.FindIndex(b => ReferenceEquals(b, Current.Player));
        int two = _twos.FindIndex(b => ReferenceEquals(b, Current.Opponent));

        if (one >= 0 && one != _oneSlot)
        {
            _oneSlot = one;
            changed.Add((One, _ones[one]));
        }

        if (two >= 0 && two != _twoSlot)
        {
            _twoSlot = two;
            changed.Add((Two, _twos[two]));
        }

        return changed;
    }

    private List<Battler> Team(int playerId) => playerId == One ? _ones : _twos;
}

/// <summary>
/// Who is fighting whom, and who has been asked.
/// <para>
/// The same shape as <see cref="Trades"/>, deliberately: one at a time each, an invitation
/// that dies when either side walks away, and asking somebody who has already asked you is
/// how a fight begins. Two verbs that behave the same way are two verbs a player only has
/// to learn once.
/// </para>
/// </summary>
public sealed class Duels
{
    private readonly List<Duel> _live = [];
    private readonly Dictionary<int, int> _asked = [];

    public Duel? For(int playerId) => _live.FirstOrDefault(d => d.Has(playerId));

    public int? AskedBy(int playerId) => _asked.TryGetValue(playerId, out int who) ? who : null;

    /// <summary>
    /// One player asks another. Asking back is agreeing.
    /// <para>
    /// Whoever was asked first is <see cref="Duel.One"/>, which is the engine's
    /// <see cref="Side.Player"/>. It decides nothing about the fight — the engine has no
    /// preference between its two sides — but it has to be settled somewhere, and "the
    /// one who was asked" is a rule anybody can check.
    /// </para>
    /// </summary>
    public (int One, int Two)? Ask(int from, int to)
    {
        if (For(from) is not null || For(to) is not null) return null;

        if (_asked.TryGetValue(to, out int theirs) && theirs == from)
        {
            _asked.Remove(to);
            _asked.Remove(from);

            return (from, to);
        }

        _asked[from] = to;

        return null;
    }

    public void Begin(Duel duel) => _live.Add(duel);

    /// <summary>Everything this player was in the middle of, gone.</summary>
    public Duel? Drop(int playerId)
    {
        _asked.Remove(playerId);

        foreach (int asker in _asked.Where(a => a.Value == playerId).Select(a => a.Key).ToList())
            _asked.Remove(asker);

        if (For(playerId) is not { } duel) return null;

        _live.Remove(duel);

        return duel;
    }

    public void Finish(Duel duel) => _live.Remove(duel);

    public int Count => _live.Count;
}

/// <summary>
/// One finished duel, as something outside the world needs it.
/// <para>
/// Account ids rather than player ids, because a rating belongs to an account and a player
/// id is only the name of a connection. The band comes with it because it was decided by
/// what was standing on the field, which nothing outside the world can see afterwards.
/// </para>
/// </summary>
public sealed record DuelResult(long Winner, long Loser, int Band);
