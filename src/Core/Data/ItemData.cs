using PokeMmo.Core.Battle;

namespace PokeMmo.Core.Data;

/// <summary>Which part of the bag something goes in.</summary>
public enum Pocket
{
    None = 0,
    Items = 1,
    KeyItems = 2,
    Balls = 3,
    Machines = 4,
    Berries = 5,
}

/// <summary>
/// One item, as the server needs it.
/// <para>
/// No name and no description. Both are cartridge text, and the server has never held
/// any — it works in ids and prices, and the client turns an id into "SUPER POTION"
/// from the image on the player's own machine.
/// </para>
/// <para>
/// <see cref="Importance"/> is what the games use to mark something as a key item:
/// zero for ordinary things, non-zero for the ones that cannot be sold or thrown away.
/// Kept as the number rather than a flag, because there is more than one non-zero value
/// and this project has not yet earned an opinion about what they mean.
/// </para>
/// </summary>
public sealed record ItemData(
    int Id,
    int Price,
    Pocket Pocket,
    int HoldEffect,
    int HoldEffectParam,
    int Importance,
    int BattleUsage,
    int SecondaryId,
    BallKind? Ball = null)
{
    /// <summary>True when throwing this at something could catch it.</summary>
    public bool IsBall => Ball is not null;

    /// <summary>
    /// The move this teaches, or zero for anything that teaches nothing.
    /// <para>
    /// Not read from the item's own record, because it is not in there: all four data
    /// fields are zero for every one of the fifty-eight machines. It comes from a
    /// separate list of move ids in machine order, matched to the machines by position.
    /// </para>
    /// </summary>
    public int Teaches { get; init; }

    /// <summary>
    /// True when using this on a party member teaches them something.
    /// <para>
    /// The pocket as well as the move, so that an ordinary item which happened to be
    /// given a move id by a mismatched list cannot quietly become a teaching machine.
    /// </para>
    /// </summary>
    public bool CanTeach => Teaches != 0 && Pocket == Pocket.Machines;

    /// <summary>
    /// True when using this does not use it up.
    /// <para>
    /// The cartridge draws this line itself and it costs nothing to read: the fifty TMs
    /// have importance 0 and a price of 3000, and the eight HMs have importance 1 and no
    /// price at all. Importance is the key-item mark, so the reusable ones are exactly
    /// the ones already treated as too important to sell.
    /// </para>
    /// </summary>
    public bool IsReusableMachine => CanTeach && IsKeyItem;

    /// <summary>A restore amount meaning "all of it", as the cartridge writes it.</summary>
    public const int FullRestore = 255;

    /// <summary>
    /// How much health this restores, or null when it restores none.
    /// <para>
    /// It was already here. <see cref="HoldEffectParam"/> is 20 on a Potion, 50 on a
    /// Super Potion, 200 on a Hyper Potion and 255 on a Max Potion — the field does
    /// double duty, and this project spent a paragraph planning to go and read a second
    /// table with a variable-length format before looking at what it had already
    /// extracted.
    /// </para>
    /// <para>
    /// The status cures are <em>not</em> here: an Antidote and a Full Heal both carry
    /// zero, so which condition each one clears really does live somewhere else. That is
    /// still to do, and this is deliberately only the half that is knowable today.
    /// </para>
    /// </summary>
    public int? Restores =>
        BattleUsage != 0 && Pocket == Pocket.Items && HoldEffectParam > 0 ? HoldEffectParam : null;

    /// <summary>
    /// What this clears, or nothing.
    /// <para>
    /// Not in the item's own record — an Antidote and a Full Heal have zero in every
    /// field of theirs, and both run the same field routine besides. It comes from a
    /// second table of short arrays, one column of which is the one every named cure
    /// item claims a single distinct bit of.
    /// </para>
    /// <para>
    /// A set rather than a condition, because four items on this cartridge clear six
    /// things each.
    /// </para>
    /// </summary>
    public Ailments Cures { get; init; }

    /// <summary>True when using this could put a condition right.</summary>
    public bool IsCure => Cures != Ailments.None;

    /// <summary>How much this would actually put back on somebody, given their maximum.</summary>
    public int RestoreFor(int maxHp) =>
        Restores is not { } amount ? 0 : amount >= FullRestore ? maxHp : amount;

    /// <summary>True when a shop would sell this. A price of zero means it is not for sale.</summary>
    public bool CanBeBought => Price > 0 && Importance == 0;

    /// <summary>True when this is something the player is never allowed to lose.</summary>
    public bool IsKeyItem => Importance != 0 || Pocket == Pocket.KeyItems;

    /// <summary>
    /// What a shop pays for one of these.
    /// <para>
    /// Half the price, which is the games' rule and a simple one. Key items are not
    /// bought back at any price.
    /// </para>
    /// </summary>
    public int SellPrice => IsKeyItem ? 0 : Price / 2;
}
