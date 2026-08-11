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
