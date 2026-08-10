namespace PokeMmo.Core.Data;

/// <summary>
/// One entry of the Generation III base-stat table.
/// <para>
/// This type lives in <c>Core</c> because both the client and the authoritative
/// server need it: the server resolves battles against these numbers, and the
/// client predicts the same turn locally using the identical code path.
/// </para>
/// <para>
/// The on-cartridge record is 28 bytes. <see cref="SizeBytes"/> is relied upon by
/// the extractor's table scanner, so it must not drift.
/// </para>
/// </summary>
public sealed class SpeciesData
{
    /// <summary>Size of a single base-stat record in the ROM, in bytes.</summary>
    public const int SizeBytes = 28;

    /// <summary>National-dex-adjacent internal species index this record was read from.</summary>
    public int Index { get; init; }

    /// <summary>Decoded display name. Empty when the name table was unavailable.</summary>
    public string Name { get; set; } = string.Empty;

    public byte BaseHp { get; init; }
    public byte BaseAttack { get; init; }
    public byte BaseDefense { get; init; }
    public byte BaseSpeed { get; init; }
    public byte BaseSpAttack { get; init; }
    public byte BaseSpDefense { get; init; }

    public PokemonType Type1 { get; init; }
    public PokemonType Type2 { get; init; }

    public byte CatchRate { get; init; }
    public byte ExpYield { get; init; }

    public byte EvHp { get; init; }
    public byte EvAttack { get; init; }
    public byte EvDefense { get; init; }
    public byte EvSpeed { get; init; }
    public byte EvSpAttack { get; init; }
    public byte EvSpDefense { get; init; }

    public ushort Item1 { get; init; }
    public ushort Item2 { get; init; }

    /// <summary>
    /// Raw gender ratio byte. 0 = always male, 254 = always female,
    /// 255 = genderless; otherwise it is the chance-of-female out of 256.
    /// </summary>
    public byte GenderRatio { get; init; }

    public byte EggCycles { get; init; }
    public byte BaseFriendship { get; init; }
    public GrowthRate GrowthRate { get; init; }
    public EggGroup EggGroup1 { get; init; }
    public EggGroup EggGroup2 { get; init; }
    public byte Ability1 { get; init; }
    public byte Ability2 { get; init; }
    public byte SafariZoneFleeRate { get; init; }
    public byte BodyColor { get; init; }
    public bool NoFlip { get; init; }

    /// <summary>True when this species is genderless.</summary>
    public bool IsGenderless => GenderRatio == 255;

    /// <summary>Sum of the six base stats — a convenient sanity signal when eyeballing a dump.</summary>
    public int BaseStatTotal =>
        BaseHp + BaseAttack + BaseDefense + BaseSpeed + BaseSpAttack + BaseSpDefense;

    /// <summary>
    /// Decodes a 28-byte base-stat record. <paramref name="src"/> must be at least
    /// <see cref="SizeBytes"/> long.
    /// </summary>
    public static SpeciesData Parse(ReadOnlySpan<byte> src, int index)
    {
        if (src.Length < SizeBytes)
            throw new ArgumentException($"Base-stat record needs {SizeBytes} bytes, got {src.Length}.", nameof(src));

        // Bytes 10-11 pack six 2-bit EV yields, lowest bits first.
        ushort ev = (ushort)(src[10] | (src[11] << 8));

        // Byte 26 packs body colour in bits 0-6 and the "do not mirror" flag in bit 7.
        byte colorAndFlip = src[26];

        return new SpeciesData
        {
            Index = index,
            BaseHp = src[0],
            BaseAttack = src[1],
            BaseDefense = src[2],
            BaseSpeed = src[3],
            BaseSpAttack = src[4],
            BaseSpDefense = src[5],
            Type1 = (PokemonType)src[6],
            Type2 = (PokemonType)src[7],
            CatchRate = src[8],
            ExpYield = src[9],
            EvHp = (byte)(ev & 0x3),
            EvAttack = (byte)((ev >> 2) & 0x3),
            EvDefense = (byte)((ev >> 4) & 0x3),
            EvSpeed = (byte)((ev >> 6) & 0x3),
            EvSpAttack = (byte)((ev >> 8) & 0x3),
            EvSpDefense = (byte)((ev >> 10) & 0x3),
            Item1 = (ushort)(src[12] | (src[13] << 8)),
            Item2 = (ushort)(src[14] | (src[15] << 8)),
            GenderRatio = src[16],
            EggCycles = src[17],
            BaseFriendship = src[18],
            GrowthRate = (GrowthRate)src[19],
            EggGroup1 = (EggGroup)src[20],
            EggGroup2 = (EggGroup)src[21],
            Ability1 = src[22],
            Ability2 = src[23],
            SafariZoneFleeRate = src[24],
            BodyColor = (byte)(colorAndFlip & 0x7F),
            NoFlip = (colorAndFlip & 0x80) != 0,
        };
    }

    public override string ToString() =>
        $"#{Index:D3} {Name} {BaseHp}/{BaseAttack}/{BaseDefense}/{BaseSpAttack}/{BaseSpDefense}/{BaseSpeed} ({Type1}{(Type2 == Type1 ? "" : "/" + Type2)})";
}
