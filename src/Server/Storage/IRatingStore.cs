namespace PokeMmo.Server.Storage;

/// <summary>
/// Where somebody stands in one band.
/// <para>
/// Named <c>Rung</c> rather than the obvious word because <c>Standing</c> is already taken,
/// in this same assembly, by where a person is standing on a map. Two types with one name in
/// one namespace is a compiler error somewhere far away from either of them.
/// </para>
/// </summary>
public sealed record Rung(string Name, int Band, int Rating, int Won, int Lost)
{
    public int Played => Won + Lost;
}

/// <summary>
/// Ratings, one per player per band.
/// <para>
/// Per band rather than one overall, and that is the decision this interface exists to make
/// visible. A player who is very good with a party of weak creatures and hopeless with
/// strong ones has two different abilities, and a single number would be the average of two
/// things that never meet. It is also what stops the obvious abuse: a strong party farming
/// the bottom of the ladder cannot, because it is not on that ladder.
/// </para>
/// <para>
/// Which band a fight counted in is decided by the world and handed in, because it is a fact
/// about what was standing on the field rather than about either account.
/// </para>
/// </summary>
public interface IRatingStore
{
    /// <summary>
    /// Writes down one result and returns both new ratings.
    /// <para>
    /// Both, in one transaction, because a rating is a comparison: half of a result written
    /// is two players whose numbers no longer add up to the game they played.
    /// </para>
    /// </summary>
    Task<(int Winner, int Loser)> RecordAsync(
        long winnerId, long loserId, int band, CancellationToken cancellationToken = default);

    /// <summary>Where one account stands in one band, whether or not it has ever played.</summary>
    Task<Rung> StandingAsync(
        long accountId, int band, CancellationToken cancellationToken = default);

    /// <summary>The top of one band, best first.</summary>
    Task<IReadOnlyList<Rung>> TopAsync(
        int band, int most = 20, CancellationToken cancellationToken = default);
}
