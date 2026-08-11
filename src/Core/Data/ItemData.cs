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
