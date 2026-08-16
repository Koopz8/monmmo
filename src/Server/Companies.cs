namespace PokeMmo.Server;

/// <summary>
/// A set of people travelling together.
///
/// <para>
/// Called a company rather than a party because this game already has parties and they are
/// the six creatures somebody is carrying. One word for two things is how a bug gets written
/// by somebody reading the right code and thinking of the wrong subject.
/// </para>
/// <para>
/// Not a guild. A guild is a named roster with a channel — a fact about who somebody knows.
/// This is a fact about where somebody is going, and it is small, temporary and unnamed.
/// Conflating the two would put every member of a guild in one copy of a place, which is
/// exactly the crowd copies exist to break up.
/// </para>
/// <para>
/// What it buys is the rule instancing has owed since milestone 117: <em>two people who want
/// to be together have to land in the same copy.</em> Doors already half-did that — walking
/// into a new place prefers the copy you were already in — but only by accident of you both
/// having come through the same one. It stops the moment one of you takes a different route,
/// warps, or is sent somewhere by a script. A party makes it deliberate and makes it survive
/// all three.
/// </para>
/// </summary>
public sealed class Company
{
    private readonly List<int> _members;

    public Company(int one, int two) => _members = [one, two];

    /// <summary>
    /// How many may travel together. <b>Modelled.</b>
    /// <para>
    /// Four, for two reasons that agree. A party arriving somewhere is stood beside whoever
    /// it is following, and the squares immediately around one tile number eight — so four
    /// always fit without anybody being placed somewhere they cannot see the others. And a
    /// party is meant to be far smaller than a copy: one that could approach forty would be
    /// a crowd, which is the thing copies exist to divide.
    /// </para>
    /// </summary>
    public const int MostMembers = 4;

    /// <summary>Everybody in it, in the order they joined.</summary>
    public IReadOnlyList<int> Members => _members;

    public int Count => _members.Count;

    public bool Has(int playerId) => _members.Contains(playerId);

    /// <summary>
    /// Whoever else is in it. Not "the other one" — a party is a set rather than a pair, and
    /// a method that returned one would be right until the third person joined.
    /// </summary>
    public IEnumerable<int> Besides(int playerId) => _members.Where(m => m != playerId);

    public bool IsFull => _members.Count >= MostMembers;

    public void Add(int playerId)
    {
        if (!_members.Contains(playerId)) _members.Add(playerId);
    }

    public void Remove(int playerId) => _members.Remove(playerId);

    /// <summary>
    /// A company of one is not a company.
    /// <para>
    /// Said here rather than left to whoever removes somebody, because the alternative is a
    /// player who is nominally in a company alone — following themselves into copies, holding
    /// an invitation nobody can accept, and reported as being with somebody. That state has
    /// no meaning and every caller would have to remember to avoid it.
    /// </para>
    /// </summary>
    public bool IsOver => _members.Count < 2;
}

/// <summary>
/// Who is travelling with whom, and who has been asked.
/// <para>
/// The same shape as <see cref="Trades"/> and <see cref="Duels"/>, deliberately: one at a
/// time each, an invitation that dies when either side walks away, and asking somebody who
/// has already asked you is how it begins. Three verbs that behave the same way are three
/// verbs a player only has to learn once — the argument milestone 100 made for duels, and it
/// holds a third time.
/// </para>
/// <para>
/// One difference, and it is the reason this is not a copy of <see cref="Trades"/>: a party
/// holds more than two. So asking somebody when you are already in one adds them to it rather
/// than starting a second, and the handshake has to check both sides for room.
/// </para>
/// </summary>
public sealed class Companies
{
    private readonly List<Company> _live = [];
    private readonly Dictionary<int, int> _asked = [];

    /// <summary>The party this player is in, if they are in one.</summary>
    public Company? For(int playerId) => _live.FirstOrDefault(p => p.Has(playerId));

    /// <summary>Who this player has asked, if anybody.</summary>
    public int? AskedBy(int playerId) => _asked.TryGetValue(playerId, out int who) ? who : null;

    public int Count => _live.Count;

    /// <summary>Why an invitation did not become a party, when it did not.</summary>
    public enum Trouble
    {
        /// <summary>It did, or it is waiting on the other one to ask back.</summary>
        None,

        /// <summary>Asking yourself, which is not a thing.</summary>
        Yourself,

        /// <summary>One of them is already travelling with somebody else.</summary>
        AlreadyWithSomebody,

        /// <summary>The party they would be joining is full.</summary>
        Full,
    }

    /// <summary>
    /// One player asks another. Asking somebody who has already asked you is agreeing.
    /// <para>
    /// Both sides are checked for room before either is moved, because a handshake that can
    /// half-succeed is worse than one that refuses: the asker would be told they had joined
    /// and the party would not have them.
    /// </para>
    /// </summary>
    public Company? Ask(int from, int to, out Trouble why)
    {
        why = Trouble.None;

        if (from == to)
        {
            why = Trouble.Yourself;

            return null;
        }

        Company? mine = For(from);
        Company? theirs = For(to);

        // Both already travelling, and not together. Neither can be moved without breaking
        // the other's party, so this is refused rather than resolved by picking one.
        if (mine is not null && theirs is not null)
        {
            why = mine == theirs ? Trouble.AlreadyWithSomebody : Trouble.AlreadyWithSomebody;

            return null;
        }

        if (mine is { IsFull: true } || theirs is { IsFull: true })
        {
            why = Trouble.Full;

            return null;
        }

        if (!_asked.TryGetValue(to, out int theirAsk) || theirAsk != from)
        {
            _asked[from] = to;

            return null;
        }

        _asked.Remove(to);
        _asked.Remove(from);

        // Whichever of them is already travelling keeps their party and the other joins it.
        // With neither in one, a new party of the two of them.
        if (mine is not null)
        {
            mine.Add(to);

            return mine;
        }

        if (theirs is not null)
        {
            theirs.Add(from);

            return theirs;
        }

        var started = new Company(from, to);

        _live.Add(started);

        return started;
    }

    /// <summary>
    /// Everything this player was in the middle of, gone: their party if they were in one and
    /// their invitation if they had made one. Called when they leave the world.
    /// </summary>
    /// <returns>The party they were in, whether or not it survived their leaving.</returns>
    public Company? Drop(int playerId)
    {
        _asked.Remove(playerId);

        foreach (int asker in _asked.Where(a => a.Value == playerId).Select(a => a.Key).ToList())
            _asked.Remove(asker);

        if (For(playerId) is not { } party) return null;

        party.Remove(playerId);

        if (party.IsOver) _live.Remove(party);

        return party;
    }
}
