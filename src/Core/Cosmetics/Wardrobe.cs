namespace PokeMmo.Core.Cosmetics;

/// <summary>
/// Everything this game knows how to wear.
/// <para>
/// Invented, like everything else in this namespace. The list is deliberately small and
/// deliberately dull — one or two of each slot — because the point of the first version is
/// the machinery underneath, and a catalogue is the easiest thing in the world to add to
/// once nothing else has to change to do it.
/// </para>
/// <para>
/// Ids are grouped by slot in hundreds. That is not a rule anything depends on — the slot
/// comes off the record, not off the number — but it makes a wardrobe readable in a log and
/// leaves a hundred of room in each before anybody has to think about it.
/// </para>
/// </summary>
public static class Wardrobe
{
    public static readonly IReadOnlyList<Cosmetic> All =
    [
        new(101, CosmeticSlot.Hair, "BLACK HAIR"),
        new(102, CosmeticSlot.Hair, "BROWN HAIR"),
        new(103, CosmeticSlot.Hair, "RED HAIR"),
        new(104, CosmeticSlot.Hair, "FAIR HAIR"),

        new(201, CosmeticSlot.Eyes, "BROWN EYES"),
        new(202, CosmeticSlot.Eyes, "BLUE EYES"),
        new(203, CosmeticSlot.Eyes, "GREEN EYES"),

        new(301, CosmeticSlot.Hat, "RED CAP"),
        new(302, CosmeticSlot.Hat, "STRAW HAT"),

        new(401, CosmeticSlot.Glasses, "ROUND GLASSES"),
        new(402, CosmeticSlot.Glasses, "DARK GLASSES"),

        new(501, CosmeticSlot.Scarf, "LONG SCARF"),

        new(601, CosmeticSlot.Shirt, "PLAIN SHIRT"),
        new(602, CosmeticSlot.Shirt, "STRIPED SHIRT"),

        new(701, CosmeticSlot.Pants, "BLUE JEANS"),
        new(702, CosmeticSlot.Pants, "SHORTS"),

        new(801, CosmeticSlot.Skirt, "PLEATED SKIRT"),

        new(901, CosmeticSlot.Dress, "SUMMER DRESS"),

        new(1001, CosmeticSlot.Shoes, "TRAINERS"),
        new(1002, CosmeticSlot.Shoes, "BOOTS"),

        new(1101, CosmeticSlot.Cape, "TRAVELLING CAPE"),

        new(1201, CosmeticSlot.Backpack, "CANVAS BACKPACK"),
    ];

    private static readonly Dictionary<int, Cosmetic> ById = All.ToDictionary(c => c.Id);

    /// <summary>The cosmetic with that number, or nothing if this game has no such thing.</summary>
    public static Cosmetic? At(int id) => ById.GetValueOrDefault(id);

    /// <summary>Everything that goes in one slot, for a wardrobe screen to page through.</summary>
    public static IReadOnlyList<Cosmetic> For(CosmeticSlot slot) => [.. All.Where(c => c.Slot == slot)];

    /// <summary>
    /// What every account owns without buying anything.
    /// <para>
    /// A character with no hair and no clothes is not a character, so the plain end of each
    /// slot is free. What is sold is the rest — and keeping the free set named here rather
    /// than assumed is what stops a shop accidentally charging for the only shirt.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<int> FreeToEverybody =
    [
        101, 201, 601, 701, 1001,
    ];
}
