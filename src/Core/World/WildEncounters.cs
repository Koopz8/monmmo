using PokeMmo.Core.Battle;

namespace PokeMmo.Core.World;

/// <summary>Where an encounter can happen.</summary>
public enum EncounterKind
{
    Land,
    Water,
    RockSmash,
    Fishing,
}

/// <summary>One slot of an encounter table: what appears, and between which levels.</summary>
public sealed record WildSlot(int Species, int MinLevel, int MaxLevel)
{
    /// <summary>Size of a slot record on the cartridge.</summary>
    public const int SizeBytes = 4;

    public int RollLevel(BattleRng rng) =>
        MaxLevel <= MinLevel ? MinLevel : MinLevel + rng.Next(MaxLevel - MinLevel + 1);

    public override string ToString() =>
        $"species {Species} L{MinLevel}{(MaxLevel > MinLevel ? $"-{MaxLevel}" : "")}";
}

/// <summary>One map's table for one encounter kind.</summary>
public sealed record EncounterTable(EncounterKind Kind, int Rate, IReadOnlyList<WildSlot> Slots)
{
    public bool IsUsable => Rate > 0 && Slots.Count > 0;
}

/// <summary>Everything that can be met on one map.</summary>
public sealed record MapEncounters(
    string MapId,
    EncounterTable? Land = null,
    EncounterTable? Water = null,
    EncounterTable? RockSmash = null,
    EncounterTable? Fishing = null)
{
    public EncounterTable? For(EncounterKind kind) => kind switch
    {
        EncounterKind.Land => Land,
        EncounterKind.Water => Water,
        EncounterKind.RockSmash => RockSmash,
        _ => Fishing,
    };

    public IEnumerable<EncounterTable> All =>
        new[] { Land, Water, RockSmash, Fishing }.Where(t => t is { IsUsable: true })!.Cast<EncounterTable>();
}

/// <summary>A rolled encounter, ready to become a battle.</summary>
public sealed record WildEncounter(int Species, int Level, EncounterKind Kind);

/// <summary>
/// Deciding whether a step meets something, and what.
/// <para>
/// Slot probabilities are fixed by the generation rather than stored per map — the
/// cartridge holds only which creature sits in each slot, and the odds of each slot
/// are the same everywhere. Getting those weights wrong would make rare encounters
/// common without anything looking broken.
/// </para>
/// </summary>
public static class WildEncounters
{
    /// <summary>Land tables have twelve slots, weighted 20/20/10/10/10/10/5/5/4/4/1/1 per cent.</summary>
    public static readonly int[] LandWeights = [20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1];

    /// <summary>Water and rock smash tables have five slots.</summary>
    public static readonly int[] WaterWeights = [60, 30, 5, 4, 1];

    /// <summary>
    /// Fishing is three rods in one table of ten: two slots for the old rod, three for
    /// the good, five for the super.
    /// </summary>
    public static readonly int[] FishingWeights = [70, 30, 60, 20, 20, 40, 40, 15, 4, 1];

    public static int SlotCount(EncounterKind kind) => WeightsFor(kind).Length;

    public static int[] WeightsFor(EncounterKind kind) => kind switch
    {
        EncounterKind.Land => LandWeights,
        EncounterKind.Water or EncounterKind.RockSmash => WaterWeights,
        _ => FishingWeights,
    };

    /// <summary>
    /// Whether a step on an encounter square meets something.
    /// <para>
    /// The hardware compares a roll out of 2880 against the map's rate times sixteen,
    /// which works out at roughly one step in ten on a typical route.
    /// </para>
    /// </summary>
    public static bool StepMeetsSomething(BattleRng rng, int encounterRate) =>
        encounterRate > 0 && rng.Next(2880) < encounterRate * 16;

    /// <summary>Picks a slot by the generation's fixed weights.</summary>
    public static int RollSlot(BattleRng rng, EncounterKind kind, int slotCount)
    {
        int[] weights = WeightsFor(kind);
        int usable = Math.Min(slotCount, weights.Length);

        if (usable <= 0) return 0;

        int total = 0;
        for (int i = 0; i < usable; i++) total += weights[i];

        int roll = rng.Next(total);

        for (int i = 0; i < usable; i++)
        {
            roll -= weights[i];
            if (roll < 0) return i;
        }

        return usable - 1;
    }

    /// <summary>
    /// Rolls a step. Returns null when nothing appears, which is the usual outcome.
    /// </summary>
    public static WildEncounter? RollStep(BattleRng rng, EncounterTable? table)
    {
        if (table is not { IsUsable: true }) return null;
        if (!StepMeetsSomething(rng, table.Rate)) return null;

        int slot = RollSlot(rng, table.Kind, table.Slots.Count);
        WildSlot chosen = table.Slots[Math.Clamp(slot, 0, table.Slots.Count - 1)];

        return new WildEncounter(chosen.Species, chosen.RollLevel(rng), table.Kind);
    }
}
