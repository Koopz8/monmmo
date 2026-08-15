using PokeMmo.Core.Data;
using PokeMmo.Core.Save;

namespace PokeMmo.Core.Battle;

/// <summary>
/// Two creatures, and what comes of leaving them together.
/// <para>
/// This is what genes are <em>for</em>. Six numbers nobody can change are a curiosity;
/// six numbers a player can work towards over generations are the reason to keep playing,
/// and the reason one creature is worth more than another, and therefore the reason a
/// market can exist at all. Every game like this one is really a game about this.
/// </para>
/// <para>
/// What is <b>read</b>, and could not be done without: the egg groups on every species
/// record, the gender ratio on every species record, and how many cycles each species'
/// eggs take — three fields extracted since the species table was first located and used
/// by nothing until now. What is <b>modelled</b> is the inheritance rule and the length of
/// a cycle, and each of those says so where it is written.
/// </para>
/// </summary>
public static class Breeding
{
    /// <summary>
    /// How many of the six a child takes from its parents rather than rolling fresh.
    /// <para>
    /// <b>Modelled.</b> Three of six is the games' own rule for this generation, and it
    /// is the number the whole practice of breeding is built on — two parents with three
    /// good stats each can produce a child with more than either, which is what makes a
    /// chain of them worth running.
    /// </para>
    /// </summary>
    public const int Inherited = 3;

    /// <summary>
    /// How many steps one egg cycle is. <b>Modelled</b>: the count is on the species
    /// record and what a count is worth is in the game's code.
    /// </summary>
    public const int StepsPerCycle = 255;

    /// <summary>
    /// Which sex one of these is, rolled from the ratio on its own record.
    /// <para>
    /// <b>Read.</b> The byte is the chance of being female out of 256, with 255 meaning
    /// neither and 254 meaning always female — this project has extracted and documented
    /// it since the species table was first parsed and has never once asked it a
    /// question, because until there was breeding there was nothing that needed the
    /// answer.
    /// </para>
    /// </summary>
    public static Gender SexOf(SpeciesData species, BattleRng rng)
    {
        if (species.IsGenderless) return Gender.None;
        if (species.GenderRatio == 0) return Gender.Male;
        if (species.GenderRatio == 254) return Gender.Female;

        return rng.Next(256) < species.GenderRatio ? Gender.Female : Gender.Male;
    }

    /// <summary>
    /// Whether these two can be left together to any purpose.
    /// <para>
    /// The rules, in the order they are asked: nothing in the Undiscovered group breeds
    /// at all; anything breeds with a DITTO, which is what the Ditto group means and the
    /// only reason that group has one member; two DITTOs do not; and otherwise they need
    /// opposite sexes and a shared egg group.
    /// </para>
    /// </summary>
    public static bool CanBreed(SpeciesData one, Gender first, SpeciesData two, Gender second)
    {
        if (Shuns(one) || Shuns(two)) return false;

        bool oneIsDitto = IsDitto(one);
        bool twoIsDitto = IsDitto(two);

        if (oneIsDitto && twoIsDitto) return false;
        if (oneIsDitto || twoIsDitto) return true;

        if (first == Gender.None || second == Gender.None) return false;
        if (first == second) return false;

        return Shares(one, two);
    }

    /// <summary>True when nothing at all can be bred from this species.</summary>
    public static bool Shuns(SpeciesData species) =>
        species.EggGroup1 == EggGroup.Undiscovered || species.EggGroup2 == EggGroup.Undiscovered;

    /// <summary>True when this species is the one that breeds with anything.</summary>
    public static bool IsDitto(SpeciesData species) =>
        species.EggGroup1 == EggGroup.Ditto || species.EggGroup2 == EggGroup.Ditto;

    /// <summary>True when these two have an egg group in common.</summary>
    public static bool Shares(SpeciesData one, SpeciesData two)
    {
        foreach (EggGroup group in new[] { one.EggGroup1, one.EggGroup2 })
        {
            if (group == EggGroup.None) continue;
            if (group == two.EggGroup1 || group == two.EggGroup2) return true;
        }

        return false;
    }

    /// <summary>
    /// What the egg will be: the mother's species, wound back to what it was before it
    /// was anything else.
    /// <para>
    /// The winding back is <b>read</b> — the evolution table this project already
    /// extracts says what becomes what, so the bottom of a chain is found by asking which
    /// species has this one as its result and following it down. Nothing here knows the
    /// name of a baby form or has a list of them.
    /// </para>
    /// <para>
    /// A DITTO parent is never the mother, whichever way round the two were given.
    /// </para>
    /// </summary>
    public static int EggOf(GameRules rules, SpeciesData one, Gender first, SpeciesData two, int wound = 8)
    {
        SpeciesData mother = IsDitto(two) ? one : IsDitto(one) ? two : first == Gender.Female ? one : two;

        int species = mother.Index;

        for (int step = 0; step < wound; step++)
        {
            int from = rules.WhatBecomes(species);

            if (from == 0 || from == species) break;

            species = from;
        }

        return species;
    }

    /// <summary>
    /// How many steps this egg needs. <b>Read</b> off the species record, times the
    /// modelled length of a cycle.
    /// </summary>
    public static int StepsToHatch(SpeciesData species) => Math.Max(1, (int)species.EggCycles) * StepsPerCycle;

    /// <summary>
    /// What the child is born with.
    /// <para>
    /// Three of the six come from the parents — which three, and from which parent, is
    /// the dice. The other three are rolled as any wild creature's are. The rule is
    /// <b>modelled</b>; what it produces is the whole of why anybody breeds twice.
    /// </para>
    /// </summary>
    public static Genes Inherit(Genes mother, Genes father, BattleRng rng)
    {
        int[] child = [.. Genes.Roll(rng).Values];

        // Three distinct stats, chosen by shuffling the six and taking the front of it.
        List<int> stats = [0, 1, 2, 3, 4, 5];

        for (int i = stats.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);

            (stats[i], stats[j]) = (stats[j], stats[i]);
        }

        foreach (int stat in stats.Take(Inherited))
            child[stat] = rng.OneIn(2) ? mother.Values[stat] : father.Values[stat];

        return Genes.Of(child);
    }

    /// <summary>
    /// The egg two creatures make, as something that can be written down.
    /// <para>
    /// Level one, no experience, and the moves that species knows at level one — the same
    /// question every other new creature in this project asks. What is different is only
    /// where the six numbers came from.
    /// </para>
    /// </summary>
    public static SavedMon? Egg(
        GameRules rules, SavedMon one, Gender first, SavedMon two, Gender second, BattleRng rng)
    {
        if (rules.SpeciesAt(one.Species) is not { } left) return null;
        if (rules.SpeciesAt(two.Species) is not { } right) return null;
        if (!CanBreed(left, first, right, second)) return null;

        int species = EggOf(rules, left, first, right);

        SavedMon mother = IsDitto(right) || first == Gender.Female ? one : two;
        SavedMon father = mother == one ? two : one;

        return new SavedMon(
            species,
            1,
            null,
            0,
            StatusCondition.None,
            (Nature)rng.Next(25),
            [.. rules.MovesKnownAt(species, 1).Select(m => m.Id)])
        {
            Ivs = [.. Inherit(mother.Born, father.Born, rng).Values],
        };
    }
}

/// <summary>
/// Which sex a creature is.
/// <para>
/// Read off the gender ratio byte every species record carries, which this project has
/// extracted since the beginning and never once asked about — because until there was
/// breeding there was no question that needed the answer.
/// </para>
/// </summary>
public enum Gender
{
    /// <summary>Neither, which is what a MAGNEMITE or a DITTO is.</summary>
    None,

    Male,
    Female,
}
