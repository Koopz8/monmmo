using PokeMmo.Core.Battle;

namespace PokeMmo.Core.Data;

/// <summary>
/// The things that can be wrong with a creature, as a set.
/// <para>
/// A set rather than one, because an item clears several — the four that clear
/// everything clear six things each — and because "which of these does this cure" is
/// the question the bag asks. The battle's own <see cref="StatusCondition"/> stays a
/// single value, since a creature only ever has one of those at a time; confusion is
/// the sixth thing here and is not one of them, being something that runs alongside.
/// </para>
/// <para>
/// The numbers here are this project's own. The cartridge's bit for poison is not one
/// of these — it is read at export, matched to a meaning there, and what crosses into
/// the rules file is this.
/// </para>
/// </summary>
[Flags]
public enum Ailments
{
    None = 0,
    Poison = 1,
    Burn = 2,
    Paralysis = 4,
    Sleep = 8,
    Freeze = 16,
    Confusion = 32,

    /// <summary>Everything a creature can be suffering at once.</summary>
    Everything = Poison | Burn | Paralysis | Sleep | Freeze | Confusion,
}

public static class AilmentExtensions
{
    /// <summary>This condition as a one-member set, or nothing.</summary>
    public static Ailments AsAilment(this StatusCondition status) => status switch
    {
        StatusCondition.Poison => Ailments.Poison,
        StatusCondition.Burn => Ailments.Burn,
        StatusCondition.Paralysis => Ailments.Paralysis,
        StatusCondition.Sleep => Ailments.Sleep,
        StatusCondition.Freeze => Ailments.Freeze,
        _ => Ailments.None,
    };

    /// <summary>Whether this set covers that condition.</summary>
    public static bool Clears(this Ailments cures, StatusCondition status) =>
        status != StatusCondition.None && cures.HasFlag(status.AsAilment());
}
