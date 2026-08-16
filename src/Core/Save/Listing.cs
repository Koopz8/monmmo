using PokeMmo.Core.Battle;

namespace PokeMmo.Core.Save;

/// <summary>
/// One thing on the market, as everybody except its seller sees it.
/// <para>
/// A summary rather than a row reference, and that is a decision rather than convenience.
/// The thing anybody actually wants to search a market by is what a creature <em>is</em> —
/// its species, its level, what it was born with — and a listing that only pointed at a
/// row would have to fetch every candidate to answer the first question anybody asks.
/// </para>
/// <para>
/// It is also what makes a sold listing safe to keep. Once a creature is sold it belongs to
/// its buyer and its row is theirs to rewrite; a listing still pointing at that row would
/// be a listing whose money vanished the first time the buyer saved. So the summary is
/// copied at the moment of listing and outlives the row it was copied from.
/// </para>
/// </summary>
public sealed record Listing(
    long Id,
    string Seller,
    int Species,
    int Level,
    int Price)
{
    /// <summary>True once somebody has bought it and the price is owed to the seller.</summary>
    public bool Sold { get; init; }

    /// <summary>
    /// What it was born with, carried so a market can be searched by the only numbers
    /// anybody past the story cares about.
    /// </summary>
    public IReadOnlyList<int> Ivs { get; init; } = [];

    public Gender Sex { get; init; }

    /// <summary>
    /// Which item this is a pile of, or zero when it is a creature.
    /// <para>
    /// One table for both, because everything a market does around the edges — the board,
    /// the search, whose it is, what it sold for, the money waiting to be collected — is
    /// the same question for a pile of REVIVEs as for a DRAGONITE, and a second table
    /// would be a second copy of all of it that drifts out of step with the first.
    /// </para>
    /// <para>
    /// <see cref="Species"/> is nought on one of these, which is what makes a species
    /// search skip them without being told to. That is a happy accident of ids starting
    /// at one rather than a rule, and the search says so where it relies on it.
    /// </para>
    /// </summary>
    public int Item { get; init; }

    /// <summary>How many of it, when it is a pile.</summary>
    public int Count { get; init; }

    /// <summary>True when this is a pile of items rather than a creature.</summary>
    public bool IsItem => Item > 0;

    /// <summary>The same six numbers as something that knows what they mean.</summary>
    public Genes Born => Genes.Of(Ivs);

    /// <summary>
    /// What the six add up to, which is the one number anybody sorts a market by.
    /// <para>
    /// Out of a hundred and eighty-six. Kept as a property rather than a column because it
    /// is a sum of columns that are already there, and a stored total is a second copy of
    /// six facts — the copy that goes wrong when somebody edits one of them.
    /// </para>
    /// </summary>
    public int Total => Ivs.Sum();
}
