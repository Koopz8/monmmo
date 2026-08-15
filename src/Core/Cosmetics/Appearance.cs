namespace PokeMmo.Core.Cosmetics;

/// <summary>
/// Where a cosmetic goes. One thing at a time in each.
/// <para>
/// Hair and eyes are slots like any other rather than a separate kind of thing, and that is
/// a deliberate simplification: a shop that sells hair colours and hats wants one answer to
/// "what do you own and what are you wearing", not two. A hair colour is a cosmetic whose
/// slot happens to be <see cref="Hair"/>.
/// </para>
/// </summary>
public enum CosmeticSlot
{
    Hair,
    Eyes,
    Hat,
    Glasses,
    Scarf,
    Shirt,
    Pants,
    Skirt,
    Dress,
    Shoes,
    Cape,
    Backpack,
}

/// <summary>One thing that can be worn, and where.</summary>
/// <param name="Id">Its number, which is what travels and what is owned.</param>
/// <param name="Slot">Where it goes.</param>
/// <param name="Name">What it is called, for a shop and a wardrobe to print.</param>
public sealed record Cosmetic(int Id, CosmeticSlot Slot, string Name);

/// <summary>
/// What one character looks like: at most one cosmetic in each slot.
/// <para>
/// <b>Nothing in this file is read off a cartridge.</b> Every constant here is this
/// project's own invention, and it is the first thing in the codebase of which that is true.
/// Everything else is either derived — located by a behaviour test and evidenced — or
/// modelled and marked as modelled with the argument written beside it. This is a third
/// category and it lives in its own namespace so the boundary is one grep away: a number in
/// <c>PokeMmo.Core.Cosmetics</c> is made up on purpose, and a number anywhere else is not
/// allowed to be.
/// </para>
/// <para>
/// The reason for the care is commercial as much as it is tidy. What can be sold is art this
/// project owns; what cannot is anything that came out of somebody's cartridge. Keeping the
/// invented things in one place is what keeps that line checkable rather than a matter of
/// remembering.
/// </para>
/// </summary>
public sealed record Appearance(IReadOnlyDictionary<CosmeticSlot, int> Worn)
{
    /// <summary>Wearing nothing at all, which is what a new character looks like.</summary>
    public static readonly Appearance Bare = new(new Dictionary<CosmeticSlot, int>());

    /// <summary>What is in that slot, or zero for nothing.</summary>
    public int In(CosmeticSlot slot) => Worn.GetValueOrDefault(slot);

    /// <summary>
    /// The slots a garment takes the place of, beyond its own.
    /// <para>
    /// A dress is not a shirt and a pair of trousers worn at once, it is instead of them,
    /// and a skirt and trousers are alternatives to each other. Modelled here rather than
    /// left to whoever writes the wardrobe screen, because a rule kept in an interface is a
    /// rule the next interface does not have.
    /// </para>
    /// </summary>
    public static IReadOnlyList<CosmeticSlot> InsteadOf(CosmeticSlot slot) => slot switch
    {
        CosmeticSlot.Dress => [CosmeticSlot.Shirt, CosmeticSlot.Pants, CosmeticSlot.Skirt],
        CosmeticSlot.Pants => [CosmeticSlot.Skirt, CosmeticSlot.Dress],
        CosmeticSlot.Skirt => [CosmeticSlot.Pants, CosmeticSlot.Dress],
        CosmeticSlot.Shirt => [CosmeticSlot.Dress],
        _ => [],
    };

    /// <summary>
    /// This appearance with one more thing on, and whatever it replaces taken off.
    /// </summary>
    public Appearance Wearing(Cosmetic what)
    {
        var worn = new Dictionary<CosmeticSlot, int>(Worn) { [what.Slot] = what.Id };

        foreach (CosmeticSlot gone in InsteadOf(what.Slot)) worn.Remove(gone);

        return new Appearance(worn);
    }

    /// <summary>This appearance with a slot emptied.</summary>
    public Appearance Without(CosmeticSlot slot)
    {
        var worn = new Dictionary<CosmeticSlot, int>(Worn);

        worn.Remove(slot);

        return new Appearance(worn);
    }

    /// <summary>
    /// Everything worn, in a fixed order, for anything that has to draw or store it.
    /// <para>
    /// The order is the drawing order: skin and hair first, then what is worn over them,
    /// then the things that hang off the outside. A renderer that iterated a dictionary
    /// would put a hat under a shirt one run in ten.
    /// </para>
    /// </summary>
    public IEnumerable<(CosmeticSlot Slot, int Id)> InDrawingOrder() =>
        Order.Where(Worn.ContainsKey).Select(slot => (slot, Worn[slot]));

    private static readonly CosmeticSlot[] Order =
    [
        CosmeticSlot.Eyes,
        CosmeticSlot.Hair,
        CosmeticSlot.Dress,
        CosmeticSlot.Shirt,
        CosmeticSlot.Skirt,
        CosmeticSlot.Pants,
        CosmeticSlot.Shoes,
        CosmeticSlot.Glasses,
        CosmeticSlot.Scarf,
        CosmeticSlot.Hat,
        CosmeticSlot.Backpack,
        CosmeticSlot.Cape,
    ];
}
