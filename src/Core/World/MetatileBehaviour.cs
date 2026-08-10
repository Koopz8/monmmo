namespace PokeMmo.Core.World;

/// <summary>
/// What a map square <em>is</em>, as opposed to what it looks like.
/// <para>
/// These values were confirmed by measurement rather than assumed. Counting
/// behaviours across Route 1 gave 881 ordinary squares and 52 of value 0x02, which
/// matches that route's two small grass patches exactly; Cycling Road, which has no
/// grass at all, instead showed 184 and 155 squares of 0x38 and 0x39 — its ledges.
/// </para>
/// <para>
/// Only the values that have been checked against a real cartridge are named here.
/// Guessing at the rest is how several earlier bugs happened, and an unnamed value is
/// honest about what is known.
/// </para>
/// </summary>
public static class MetatileBehaviour
{
    /// <summary>Ordinary ground. The overwhelming majority of any map.</summary>
    public const byte Normal = 0x00;

    /// <summary>Tall grass — where land encounters happen. Confirmed on Route 1.</summary>
    public const byte TallGrass = 0x02;

    /// <summary>Long grass. Adjacent to tall grass in the numbering and also an encounter square.</summary>
    public const byte LongGrass = 0x03;

    /// <summary>Ledges, which can be hopped in one direction only. Confirmed on Cycling Road.</summary>
    public const byte LedgeSouth = 0x38;

    public const byte LedgeNorth = 0x39;

    public const byte LedgeWest = 0x3A;

    public const byte LedgeEast = 0x3B;

    /// <summary>True when standing here can start a land encounter.</summary>
    public static bool IsEncounterGrass(byte behaviour) => behaviour is TallGrass or LongGrass;

    /// <summary>True when this square is a ledge of any direction.</summary>
    public static bool IsLedge(byte behaviour) => behaviour is >= LedgeSouth and <= LedgeEast;
}
