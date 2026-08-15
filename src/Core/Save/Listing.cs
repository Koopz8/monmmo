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

    /// <summary>The same six numbers as something that knows what they mean.</summary>
    public Genes Born => Genes.Of(Ivs);
}
