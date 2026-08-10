namespace PokeMmo.Core.World;

/// <summary>
/// Matching a place by name.
/// <para>
/// Plain substring matching is wrong here in a way that is easy to miss: "route 1"
/// is contained in "ROUTE 17", so asking for the first route can quietly hand back
/// the seventeenth. Ranking exact matches above whole-word ones above bare substrings
/// fixes it, and keeping the rule in one place means the client, the server and the
/// tools cannot disagree about which map was meant.
/// </para>
/// </summary>
public static class MapNameMatch
{
    public const int NoMatch = 0;
    public const int Contains = 1;
    public const int WholeWord = 2;
    public const int Exact = 3;

    /// <summary>How well a name answers a query. Higher is better; zero does not match.</summary>
    public static int Score(string name, string query)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(name)) return NoMatch;

        string trimmedName = name.Trim();
        string trimmedQuery = query.Trim();

        if (trimmedName.Equals(trimmedQuery, StringComparison.OrdinalIgnoreCase)) return Exact;

        if (!trimmedName.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)) return NoMatch;

        // "route 1" should not count as a whole-word match inside "ROUTE 17": the
        // character after the match has to be a boundary, not more of the same token.
        int at = trimmedName.IndexOf(trimmedQuery, StringComparison.OrdinalIgnoreCase);

        while (at >= 0)
        {
            bool startsCleanly = at == 0 || !char.IsLetterOrDigit(trimmedName[at - 1]);
            int after = at + trimmedQuery.Length;
            bool endsCleanly = after >= trimmedName.Length || !char.IsLetterOrDigit(trimmedName[after]);

            if (startsCleanly && endsCleanly) return WholeWord;

            at = trimmedName.IndexOf(trimmedQuery, at + 1, StringComparison.OrdinalIgnoreCase);
        }

        return Contains;
    }

    public static bool Matches(string name, string query) => Score(name, query) > NoMatch;

    /// <summary>
    /// Everything that matches, best first. Ties are broken by <paramref name="sizeOf"/>,
    /// which puts the outdoor map ahead of the interiors that share its name.
    /// </summary>
    public static IEnumerable<T> Rank<T>(
        IEnumerable<T> items,
        Func<T, string> nameOf,
        string query,
        Func<T, int>? sizeOf = null)
    {
        IEnumerable<(T Item, int Score)> scored = items
            .Select(item => (Item: item, Score: Score(nameOf(item), query)))
            .Where(x => x.Score > NoMatch);

        IOrderedEnumerable<(T Item, int Score)> ordered = scored.OrderByDescending(x => x.Score);

        if (sizeOf is not null) ordered = ordered.ThenByDescending(x => sizeOf(x.Item));

        return ordered.Select(x => x.Item);
    }
}
