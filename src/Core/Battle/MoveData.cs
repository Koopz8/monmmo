using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>
/// Whether a move draws on Attack or Special Attack.
/// <para>
/// In Generation III this is a property of the <em>type</em>, not of the move: every
/// Normal, Fighting, Flying, Poison, Ground, Rock, Bug, Ghost and Steel move is
/// physical, and everything from Fire onwards is special. The per-move split arrived
/// a generation later. Assuming the modern rule here would misprice a great many
/// moves, and always in the same direction.
/// </para>
/// </summary>
public enum DamageCategory
{
    Physical,
    Special,
    Status,
}

/// <summary>One move, as the cartridge stores it.</summary>
public sealed record MoveData(
    int Id,
    string Name,
    byte Effect,
    byte Power,
    PokemonType Type,
    byte Accuracy,
    byte Pp,
    byte SecondaryChance,
    byte Target,
    sbyte Priority)
{
    /// <summary>Size of a move record on the cartridge.</summary>
    public const int SizeBytes = 12;

    /// <summary>
    /// The target byte's values, counted off this cartridge rather than remembered.
    /// <para>
    /// Every move on the image falls into one of seven values, and the membership of each
    /// says what it means without anybody having to be told:
    /// </para>
    /// <code>
    ///   0x00  246 moves  POUND, KARATE CHOP, FIRE PUNCH        the one opposite
    ///   0x10   67 moves  SWORDS DANCE, RECOVER, REST, REFLECT   itself
    ///   0x08   22 moves  SURF, BLIZZARD, GROWL, LEER            everybody opposite
    ///   0x01    9 moves  COUNTER, METRONOME, MIRROR MOVE        whatever the move works out
    ///   0x20    5 moves  EARTHQUAKE, EXPLOSION, MAGNITUDE       everybody, own side included
    ///   0x04    4 moves  THRASH, PETAL DANCE, OUTRAGE           one of them, picked
    ///   0x40    1 move   SPIKES                                 the other side's ground
    /// </code>
    /// <para>
    /// The 67 are the check. Every move in that group is one whose whole effect is on the
    /// creature using it — the stat raisers, the healers, the screens — and no move
    /// outside it is. A byte whose members are exactly one idea is a byte that means that
    /// idea.
    /// </para>
    /// <para>
    /// It travelled in the world's rules file from the beginning and nothing ever read it,
    /// which is how a guard put up by one side came to stop the other side's SWORDS DANCE.
    /// </para>
    /// </summary>
    public const byte AimedAtSelf = 0x10;

    /// <summary>True when this move is used on the one using it.</summary>
    public bool AimsAtSelf => Target == AimedAtSelf;

    /// <summary>
    /// True when this move is aimed at somebody else — which is everything else.
    /// <para>
    /// Including the three that name more than one. This engine fights one against one,
    /// so "everybody opposite" and "the one opposite" are the same set, and EARTHQUAKE
    /// hitting everybody in a fight with one other creature in it is EARTHQUAKE hitting
    /// that creature.
    /// </para>
    /// </summary>
    public bool AimsAtSomebodyElse => !AimsAtSelf;

    /// <summary>The type at which the physical/special boundary sits in this generation.</summary>
    public const int FirstSpecialType = (int)PokemonType.Mystery;

    public DamageCategory Category => Power == 0
        ? DamageCategory.Status
        : (int)Type < FirstSpecialType ? DamageCategory.Physical : DamageCategory.Special;

    /// <summary>Accuracy of zero means the move cannot miss.</summary>
    public bool AlwaysHits => Accuracy == 0;

    public override string ToString() =>
        $"{Name} ({Type}, {(Power == 0 ? "status" : $"{Power} power")}, " +
        $"{(AlwaysHits ? "always hits" : $"{Accuracy}% accurate")})";

    /// <summary>Decodes a 12-byte move record.</summary>
    public static MoveData Parse(ReadOnlySpan<byte> src, int id, string name)
    {
        if (src.Length < SizeBytes)
            throw new ArgumentException($"A move record needs {SizeBytes} bytes, got {src.Length}.", nameof(src));

        return new MoveData(
            id,
            name,
            src[0],
            src[1],
            (PokemonType)src[2],
            src[3],
            src[4],
            src[5],
            src[6],
            unchecked((sbyte)src[7]));
    }

    /// <summary>True when a record's fields are all within their possible ranges.</summary>
    public static bool LooksPlausible(ReadOnlySpan<byte> src)
    {
        if (src.Length < SizeBytes) return false;

        return src[2] <= 17          // type
            && src[3] <= 100         // accuracy is a percentage
            && src[4] is > 0 and <= 40   // no move has zero or more than 40 PP
            && src[5] <= 100         // secondary effect chance
            && unchecked((sbyte)src[7]) is >= -7 and <= 5;   // priority
    }
}
