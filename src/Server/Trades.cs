using PokeMmo.Core.Save;

namespace PokeMmo.Server;

/// <summary>One trade in progress, between two players who have both agreed to be in it.</summary>
public sealed class Trade
{
    public Trade(int one, int two)
    {
        One = one;
        Two = two;
    }

    public int One { get; }

    public int Two { get; }

    /// <summary>Which party slot each has put up, or −1 for nothing yet.</summary>
    public int OfferedByOne { get; private set; } = -1;

    public int OfferedByTwo { get; private set; } = -1;

    public bool OneIsReady { get; private set; }

    public bool TwoIsReady { get; private set; }

    public bool Has(int playerId) => playerId == One || playerId == Two;

    public int Other(int playerId) => playerId == One ? Two : One;

    public int OfferedBy(int playerId) => playerId == One ? OfferedByOne : OfferedByTwo;

    public bool ReadyIs(int playerId) => playerId == One ? OneIsReady : TwoIsReady;

    /// <summary>
    /// Puts a slot up, and takes both agreements back down.
    /// <para>
    /// The second half is the whole rule. A trade where one side can change what is on the
    /// table after the other has agreed is a trade nobody should ever agree to, and it is
    /// the oldest confidence trick there is. Changing an offer un-agrees both of them, every
    /// time, including changing it to the same thing.
    /// </para>
    /// </summary>
    public void Offer(int playerId, int slot)
    {
        if (playerId == One) OfferedByOne = slot;
        else OfferedByTwo = slot;

        OneIsReady = false;
        TwoIsReady = false;
    }

    public void Ready(int playerId, bool ready)
    {
        if (playerId == One) OneIsReady = ready;
        else TwoIsReady = ready;
    }

    /// <summary>Both have put something up and both have said yes.</summary>
    public bool IsAgreed => OfferedByOne >= 0 && OfferedByTwo >= 0 && OneIsReady && TwoIsReady;
}

/// <summary>
/// Who is trading with whom, and who has been asked.
/// <para>
/// Kept apart from the world for the same reason the battles are: a table of live
/// negotiations is a thing with its own rules — one trade each, an invitation that expires
/// when either side walks away — and threading them through a map of players makes both
/// harder to read.
/// </para>
/// <para>
/// Nothing here touches a party. The swap is the world's to do, because the world is what
/// knows whether somebody is being asked to give away the last thing they can fight with.
/// </para>
/// </summary>
public sealed class Trades
{
    private readonly List<Trade> _live = [];
    private readonly Dictionary<int, int> _asked = [];

    /// <summary>The trade this player is in, if they are in one.</summary>
    public Trade? For(int playerId) => _live.FirstOrDefault(t => t.Has(playerId));

    /// <summary>Who this player has asked, if anybody.</summary>
    public int? AskedBy(int playerId) => _asked.TryGetValue(playerId, out int who) ? who : null;

    /// <summary>
    /// One player asks another. Asking somebody who has already asked you is agreeing, which
    /// is the whole handshake: two requests pointing at each other and nothing else.
    /// </summary>
    public Trade? Ask(int from, int to)
    {
        if (For(from) is not null || For(to) is not null) return null;

        if (_asked.TryGetValue(to, out int theirs) && theirs == from)
        {
            _asked.Remove(to);
            _asked.Remove(from);

            var started = new Trade(from, to);
            _live.Add(started);

            return started;
        }

        _asked[from] = to;

        return null;
    }

    /// <summary>
    /// Everything this player was in the middle of, gone: their trade if they had one and
    /// their invitation if they had made one. Called when they walk off the map or leave.
    /// </summary>
    public Trade? Drop(int playerId)
    {
        _asked.Remove(playerId);

        foreach (int asker in _asked.Where(a => a.Value == playerId).Select(a => a.Key).ToList())
            _asked.Remove(asker);

        if (For(playerId) is not { } trade) return null;

        _live.Remove(trade);

        return trade;
    }

    /// <summary>Takes a finished trade off the table.</summary>
    public void Finish(Trade trade) => _live.Remove(trade);

    public int Count => _live.Count;
}
