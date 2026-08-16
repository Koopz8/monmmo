namespace PokeMmo.Core.Save;

/// <summary>
/// What a listing hands over: one creature, or a number of one item.
/// <para>
/// The two are one type rather than two because of where the decision is made. Which kind
/// a listing is can only be known by reading the row, and reading it before buying it is
/// exactly the check that two people pressing buy at once both pass. So the buy is asked
/// for by id alone, the row decides what comes back, and this is the shape "what came
/// back" has to have.
/// </para>
/// <para>
/// The asymmetry inside it is real rather than an oversight. A creature is a thing with
/// an identity — a row, a history, six numbers it was born with — and a number of POTIONs
/// is a number. Making the item side into a fake creature so the two matched would be
/// modelling a lie for the sake of a tidier field list.
/// </para>
/// </summary>
public sealed record Parcel
{
    /// <summary>The one creature, when that is what this is.</summary>
    public SavedMon? Creature { get; init; }

    /// <summary>Which item, when that is what this is. Zero otherwise.</summary>
    public int Item { get; init; }

    /// <summary>How many of it.</summary>
    public int Count { get; init; }

    /// <summary>True when this is a pile of items rather than a creature.</summary>
    public bool IsItem => Item > 0;

    public static Parcel Of(SavedMon creature) => new() { Creature = creature };

    public static Parcel Of(int item, int count) => new() { Item = item, Count = count };
}
