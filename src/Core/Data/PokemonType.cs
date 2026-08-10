namespace PokeMmo.Core.Data;

/// <summary>
/// Generation III type identifiers, in the numeric order the games use.
/// Note TYPE_MYSTERY (9) sits between STEEL and FIRE — it is a real entry in
/// the Gen III table and shifts every subsequent value, so it must be kept.
/// </summary>
public enum PokemonType : byte
{
    Normal = 0,
    Fighting = 1,
    Flying = 2,
    Poison = 3,
    Ground = 4,
    Rock = 5,
    Bug = 6,
    Ghost = 7,
    Steel = 8,
    Mystery = 9,
    Fire = 10,
    Water = 11,
    Grass = 12,
    Electric = 13,
    Psychic = 14,
    Ice = 15,
    Dragon = 16,
    Dark = 17,
}

/// <summary>Experience curve identifiers used by the Gen III base-stat table.</summary>
public enum GrowthRate : byte
{
    MediumFast = 0,
    Erratic = 1,
    Fluctuating = 2,
    MediumSlow = 3,
    Fast = 4,
    Slow = 5,
}

/// <summary>Breeding-group identifiers used by the Gen III base-stat table.</summary>
public enum EggGroup : byte
{
    None = 0,
    Monster = 1,
    Water1 = 2,
    Bug = 3,
    Flying = 4,
    Field = 5,
    Fairy = 6,
    Grass = 7,
    HumanLike = 8,
    Water3 = 9,
    Mineral = 10,
    Amorphous = 11,
    Water2 = 12,
    Ditto = 13,
    Dragon = 14,
    Undiscovered = 15,
}
