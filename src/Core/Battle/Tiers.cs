using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>
/// How strong a species is, in bands, worked out from the cartridge rather than decided.
/// <para>
/// PokeMMO has tiers and they are curated: a committee decides what belongs where, the list
/// changes when they say so, and from outside there is no way to check it. That is a
/// reasonable way to run a competitive game and it is the opposite of what this project can
/// offer, so this does the other thing — every boundary below is computed from numbers that
/// are on the cartridge, and anybody with the same image gets the same answer.
/// </para>
/// <para>
/// <b>What is read:</b> every species' six base stats, and therefore their total. <b>What is
/// modelled:</b> that there are five bands, and nothing else. The boundaries are not written
/// anywhere in this file — they are the quintiles of the totals of the species this cartridge
/// actually fields, so a different image with different creatures produces different
/// boundaries without a line of this changing.
/// </para>
/// <para>
/// Five because it is the number of bands people can hold in their heads, and because
/// PokeMMO's own list is about that long. It is an opinion and it is the only one here.
/// </para>
/// </summary>
public static class Tiers
{
    /// <summary>How many bands. <b>Modelled</b>, and the only opinion in this file.</summary>
    public const int Bands = 5;

    /// <summary>
    /// The names, weakest first. Deliberately plain words rather than borrowed ones: this is
    /// not PokeMMO's list and calling a band "OU" would be claiming it was.
    /// </summary>
    public static readonly IReadOnlyList<string> Names =
        ["Fledgling", "Rising", "Seasoned", "Formidable", "Legendary"];

    /// <summary>
    /// The bands, as the highest total each one reaches.
    /// <para>
    /// Computed once from a rules file rather than held anywhere, because a boundary stored
    /// beside the species it was computed from is a second copy of the same fact — and it is
    /// the copy that is wrong the first time anybody re-exports.
    /// </para>
    /// <para>
    /// Species with no stats at all are left out. The cartridge's table is longer than its
    /// list of creatures and the slack is placeholders, which would otherwise drag every
    /// boundary down by being counted as very weak things.
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> Boundaries(IEnumerable<SpeciesData> species)
    {
        int[] totals =
        [
            .. species
                .Where(s => s.Index > 0 && s.BaseStatTotal > 0)
                .Select(s => s.BaseStatTotal)
                .Order(),
        ];

        if (totals.Length == 0) return [];

        // The quintiles, and the top band's ceiling is the strongest thing there is rather
        // than a number. Anything above the fourth boundary is in the last band by being
        // above it, which is what makes the list total.
        var cuts = new List<int>();

        for (int band = 1; band < Bands; band++)
        {
            int at = Math.Clamp(totals.Length * band / Bands, 0, totals.Length - 1);

            cuts.Add(totals[at]);
        }

        return cuts;
    }

    /// <summary>
    /// Which band a total falls in, nought to four.
    /// <para>
    /// Takes the boundaries rather than a rules file so it can be asked a hundred thousand
    /// times without recomputing them, and so a test can ask it about a boundary it made up.
    /// </para>
    /// </summary>
    public static int Of(int baseStatTotal, IReadOnlyList<int> boundaries)
    {
        for (int band = 0; band < boundaries.Count; band++)
        {
            if (baseStatTotal <= boundaries[band]) return band;
        }

        return boundaries.Count;
    }

    /// <summary>What that band is called.</summary>
    public static string NameOf(int band) => Names[Math.Clamp(band, 0, Names.Count - 1)];

    /// <summary>
    /// Which band a whole party sits in, which is the highest any of its members reaches.
    /// <para>
    /// The highest rather than the average, and that is the only sane answer: a party of
    /// five weak creatures and one that wins on its own is a party that wins on its own, and
    /// an average would put it in the middle where it would meet nothing that could stop it.
    /// </para>
    /// </summary>
    public static int OfParty(IEnumerable<int> totals, IReadOnlyList<int> boundaries)
    {
        int highest = 0;

        foreach (int total in totals) highest = Math.Max(highest, Of(total, boundaries));

        return highest;
    }
}
