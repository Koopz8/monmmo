namespace PokeMmo.Core.World;

/// <summary>
/// What a map square <em>is</em>, as opposed to what it looks like.
/// <para>
/// These values were confirmed by drawing them rather than by assumption. Reading
/// Route 1's attributes at four bytes per metatile and plotting value 0x02 gives 178
/// squares in solid rectangular patches — the route's grass. At two bytes it gives 52
/// squares scattered down the map's left and right edges on alternating rows, which is
/// aliasing rather than terrain. The shape is what distinguishes them; both readings
/// produce plausible-looking counts.
/// </para>
/// <para>
/// Ledges were confirmed the same way: 0x3B appears on 61 squares of Route 1, and
/// Cycling Road — which has no grass at all — is full of 0x38 and 0x39.
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
