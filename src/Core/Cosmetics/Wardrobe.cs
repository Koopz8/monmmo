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
        new(101, CosmeticSlot.Hair, "BLACK HAIR", 200),
        new(102, CosmeticSlot.Hair, "BROWN HAIR", 200),
        new(103, CosmeticSlot.Hair, "RED HAIR", 300),
        new(104, CosmeticSlot.Hair, "FAIR HAIR", 300),

        new(201, CosmeticSlot.Eyes, "BROWN EYES", 200),
        new(202, CosmeticSlot.Eyes, "BLUE EYES", 300),
        new(203, CosmeticSlot.Eyes, "GREEN EYES", 300),

        new(301, CosmeticSlot.Hat, "RED CAP", 800),
        new(302, CosmeticSlot.Hat, "STRAW HAT", 600),

        new(401, CosmeticSlot.Glasses, "ROUND GLASSES", 500),
        new(402, CosmeticSlot.Glasses, "DARK GLASSES", 700),

        new(501, CosmeticSlot.Scarf, "LONG SCARF", 600),

        new(601, CosmeticSlot.Shirt, "PLAIN SHIRT", 400),
        new(602, CosmeticSlot.Shirt, "STRIPED SHIRT", 700),

        new(701, CosmeticSlot.Pants, "BLUE JEANS", 500),
        new(702, CosmeticSlot.Pants, "SHORTS", 400),

        new(801, CosmeticSlot.Skirt, "PLEATED SKIRT", 600),

        new(901, CosmeticSlot.Dress, "SUMMER DRESS", 1200),

        new(1001, CosmeticSlot.Shoes, "TRAINERS", 500),
        new(1002, CosmeticSlot.Shoes, "BOOTS", 900),

        new(1101, CosmeticSlot.Cape, "TRAVELLING CAPE", 2000),

        new(1201, CosmeticSlot.Backpack, "CANVAS BACKPACK", 1500),
    ];

    /// <summary>
    /// The band prices are chosen inside, so that adding to the catalogue is a decision
    /// with a shape rather than a new number every time.
    /// <para>
    /// <b>Invented</b>, and the argument is about the game rather than about money: a new
    /// character is handed three thousand, and the cheapest thing here is a fifteenth of
    /// that. So somebody who buys nothing else can leave the first town looking different,
    /// and somebody who wants the cape is saving for it — which is the whole of what a
    /// cosmetic economy has to do before there is anything else in it.
    /// </para>
    /// </summary>
    public const int Cheapest = 200;

    public const int Dearest = 2000;

    /// <summary>What one thing costs, or nothing when this game has no such thing.</summary>
    public static int PriceOf(int id) => At(id)?.Price ?? 0;

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
