using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract.Items;

/// <summary>
/// One item, as the cartridge records it.
/// <para>
/// Forty-four bytes: a fixed-width name, then the item's own id, a price, what it does
/// when held, a pointer to its description, which pocket it lives in, and two function
/// pointers for using it in the field and in a battle.
/// </para>
/// <para>
/// The name and description are read here and go no further than this process. Same
/// rule as species, moves and trainers — the rules file the server loads carries no
/// text at all.
/// </para>
/// </summary>
public sealed record ItemRecord(
    int Id,
    int Offset,
    string Name,
    int Price,
    Pocket Pocket,
    int HoldEffect,
    int HoldEffectParam,
    int Importance,
    int BattleUsage,
    int SecondaryId)
{
    public const int RecordSizeBytes = 44;

    public const int NameLength = 14;

    private const int IdOffset = 0x0E;
    private const int PriceOffset = 0x10;
    private const int HoldEffectOffset = 0x12;
    private const int HoldEffectParamOffset = 0x13;
    private const int DescriptionOffset = 0x14;
    private const int ImportanceOffset = 0x18;
    private const int PocketOffset = 0x1A;
    private const int FieldUseOffset = 0x1C;
    private const int BattleUsageOffset = 0x20;
    private const int BattleUseOffset = 0x24;
    private const int SecondaryIdOffset = 0x28;

    /// <summary>The highest pocket number the games use. Anything past it is not a record.</summary>
    private const int LastPocket = 5;

    /// <summary>
    /// Reads a record, or returns null when the bytes are not one at that index.
    /// <para>
    /// The discriminating check is that <b>an item states its own id</b>, and that id
    /// has to equal the position it was found at. Every other field here could be
    /// satisfied by chance; a table that counts along with itself, several hundred times
    /// in a row, could not.
    /// </para>
    /// <para>
    /// That also settles the question the trainer table needed a special case for. There
    /// is no ambiguity about where this table starts, because the record that says it is
    /// item zero is item zero.
    /// </para>
    /// </summary>
    public static ItemRecord? TryParse(Rom rom, int offset, int expectedId)
    {
        if (offset < 0 || offset + RecordSizeBytes > rom.Length) return null;

        if (rom.ReadU16(offset + IdOffset) != expectedId) return null;

        int pocket = rom.ReadU8(offset + PocketOffset);
        if (pocket > LastPocket) return null;

        if (!rom.IsRomAddress(rom.ReadU32(offset + DescriptionOffset))) return null;

        // Either a routine or nothing at all. Most items have one of the two and a few
        // have both, but a number that is neither means these are not function pointers
        // and this is not an item record.
        if (!IsRoutineOrNothing(rom, offset + FieldUseOffset)) return null;
        if (!IsRoutineOrNothing(rom, offset + BattleUseOffset)) return null;

        return new ItemRecord(
            expectedId,
            offset,
            GameText.Decode(rom.Slice(offset, NameLength)),
            rom.ReadU16(offset + PriceOffset),
            (Pocket)pocket,
            rom.ReadU8(offset + HoldEffectOffset),
            rom.ReadU8(offset + HoldEffectParamOffset),
            rom.ReadU8(offset + ImportanceOffset),
            rom.ReadU8(offset + BattleUsageOffset),
            rom.ReadU8(offset + SecondaryIdOffset));
    }

    private static bool IsRoutineOrNothing(Rom rom, int at)
    {
        uint pointer = rom.ReadU32(at);
        return pointer == 0 || rom.IsRomAddress(pointer);
    }

    /// <summary>Why the bytes at an offset are not the item with this id.</summary>
    public static string Explain(Rom rom, int offset, int expectedId)
    {
        if (offset < 0 || offset + RecordSizeBytes > rom.Length) return "outside the image";

        int stated = rom.ReadU16(offset + IdOffset);
        if (stated != expectedId) return $"says it is item {stated}, not {expectedId}";

        int pocket = rom.ReadU8(offset + PocketOffset);
        if (pocket > LastPocket) return $"pocket {pocket} is not a pocket";

        if (!rom.IsRomAddress(rom.ReadU32(offset + DescriptionOffset)))
            return "the description is not a pointer";

        if (!IsRoutineOrNothing(rom, offset + FieldUseOffset)) return "the field routine is not a pointer";
        if (!IsRoutineOrNothing(rom, offset + BattleUseOffset)) return "the battle routine is not a pointer";

        return "reads as a record";
    }

    public ItemData ToData() =>
        new(Id, Price, Pocket, HoldEffect, HoldEffectParam, Importance, BattleUsage, SecondaryId);
}

/// <summary>
/// Finds the item table and reads it.
/// <para>
/// The easiest table in this project to be sure about, and worth saying why: every
/// record contains its own index. Nothing else here has that. The species tables are
/// anchored on a known name, the sprite table on an arithmetic relationship between
/// three fields, the trainer table on pointers that have to lead to plausible
/// creatures — all of them indirect. This one simply counts, and a run of a hundred
/// consecutive self-consistent indices is not something a cartridge contains twice.
/// </para>
/// </summary>
public static class ItemTable
{
    /// <summary>
    /// Records a run must contain before it is believed. Real cartridges hold about
    /// four hundred; this is set well below that and well above coincidence.
    /// </summary>
    private const int MinimumRun = 48;

    /// <summary>Finds the table, whose first record is item zero.</summary>
    public static int? Locate(Rom rom, Action<string>? log = null)
    {
        int stride = ItemRecord.RecordSizeBytes;

        for (int offset = 0; offset + MinimumRun * stride <= rom.Length; offset += 4)
        {
            if (ItemRecord.TryParse(rom, offset, 0) is null) continue;

            int run = 1;

            while (offset + (run + 1) * stride <= rom.Length &&
                   ItemRecord.TryParse(rom, offset + run * stride, run) is not null)
            {
                run++;
            }

            if (run < MinimumRun) continue;

            log?.Invoke($"  items: {run} consecutive records at 0x{Rom.BaseAddress + (uint)offset:X8}");

            return offset;
        }

        return null;
    }

    /// <summary>
    /// Reads the table until a record stops agreeing about its own id.
    /// <para>
    /// No tolerance for holes, unlike the trainer table, because there is nothing to
    /// tolerate: an item that did not state its own index would mean the table had
    /// ended or that this was never the table.
    /// </para>
    /// </summary>
    public static List<ItemRecord> Read(Rom rom, int table, int maxItems = 1024)
    {
        var items = new List<ItemRecord>();

        for (int id = 0; id < maxItems; id++)
        {
            int at = table + id * ItemRecord.RecordSizeBytes;

            if (ItemRecord.TryParse(rom, at, id) is not { } record) break;

            items.Add(record);
        }

        return items;
    }
}
