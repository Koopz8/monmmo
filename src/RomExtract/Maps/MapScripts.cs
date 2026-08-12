using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Maps;

/// <summary>One entry in a map's own script list: a kind, and where it points.</summary>
public sealed record MapScriptEntry(byte Kind, uint Pointer);

/// <summary>
/// The fifth list.
/// <para>
/// A map header has four pointers this project reads — the layout, the events, the
/// connections — and one it never has. Every map has it. Four hundred and twenty-five
/// headers, and until now the only thing known about that pointer was that it resolved.
/// </para>
/// <para>
/// It was found by running out of other explanations. The professor's lab has three
/// squares that run the next scene while variable 0x4055 holds 2, the opening sets that
/// variable to 1, and a sweep of every script attached to every person, sign and trigger
/// in the world finds nothing anywhere that ever sets it to 2. Twenty-one places wait on
/// it; two places write it; both write 1. The chain had to continue somewhere that was
/// not being read.
/// </para>
/// <para>
/// The shape came off the bytes. Entries are five wide — a kind and a pointer — and the
/// list ends at a zero kind. Every one of the 425 maps terminates within five entries and
/// every pointer in every entry resolves. Kinds 2 and 4 point at a table rather than at a
/// script, and the table is a variable, a value and a script, ending at a zero variable —
/// which reads as "on arriving here, if this holds that, run this". Everything else
/// points straight at a script.
/// </para>
/// </summary>
public static class MapScripts
{
    /// <summary>The largest list on this cartridge is five; anything longer is a misread.</summary>
    private const int LongestList = 8;

    private const int EntryBytes = 5;

    /// <summary>The two kinds whose pointer is a table of conditions rather than a script.</summary>
    public static bool IsConditional(byte kind) => kind is 2 or 4;

    /// <summary>The entries in one map's list, in the order the cartridge wrote them.</summary>
    public static List<MapScriptEntry> Read(Rom rom, MapHeaderRecord header)
    {
        var found = new List<MapScriptEntry>();

        if (rom.ToOffsetOrNull(header.ScriptsPointer) is not { } at) return found;

        for (int i = 0; i < LongestList; i++)
        {
            int start = at + i * EntryBytes;
            if (start + EntryBytes > rom.Length) break;

            byte kind = rom.ReadU8(start);
            if (kind == 0) break;

            found.Add(new MapScriptEntry(kind, rom.ReadU32(start + 1)));
        }

        return found;
    }

    /// <summary>
    /// What one map runs on arrival, as conditions anybody can check.
    /// <para>
    /// Only the two conditional kinds. The others point at scripts with no condition
    /// attached, and running one of those means knowing <em>when</em> the cartridge runs
    /// it — on load, on the first frame, once per visit — which is not written down
    /// anywhere in the data and is not going to be guessed at here. They are read and
    /// counted and left alone.
    /// </para>
    /// </summary>
    public static List<MapEntryScript> OnEntry(Rom rom, MapHeaderRecord header)
    {
        var found = new List<MapEntryScript>();

        foreach (MapScriptEntry entry in Read(rom, header).Where(e => IsConditional(e.Kind)))
        {
            if (rom.ToOffsetOrNull(entry.Pointer) is not { } at) continue;

            for (int i = 0; i < LongestTable; i++)
            {
                int start = at + i * ConditionBytes;
                if (start + ConditionBytes > rom.Length) break;

                int variable = rom.ReadU16(start);
                if (variable == 0) break;

                found.Add(new MapEntryScript(variable, rom.ReadU16(start + 2), rom.ReadU32(start + 4)));
            }
        }

        return found;
    }

    private const int LongestTable = 8;

    private const int ConditionBytes = 8;

    /// <summary>
    /// The evidence for all of the above, across every map on the cartridge.
    /// <para>
    /// Printed rather than asserted. Every claim here is a count that would come out
    /// differently if the shape were wrong: a list that did not terminate, a pointer that
    /// did not resolve, a condition naming a variable outside the range every other part
    /// of this project sees.
    /// </para>
    /// </summary>
    public sealed record Survey(
        int Maps,
        int WithNone,
        int LongestSeen,
        int Entries,
        int PointersThatResolve,
        Dictionary<byte, int> ByKind,
        int Conditions,
        int ConditionsInVariableRange,
        int DistinctVariables);

    /// <summary>Variables the rest of this project sees; anything outside is a misread.</summary>
    private static bool LooksLikeAVariable(int id) => id is >= 0x4000 and <= 0x40FF;

    public static Survey Check(Rom rom, MapBankTable banks)
    {
        int maps = 0, none = 0, longest = 0, entries = 0, resolve = 0;
        int conditions = 0, inRange = 0;

        var byKind = new Dictionary<byte, int>();
        var variables = new HashSet<int>();

        foreach ((int _, int _, MapHeaderRecord header) in banks.AllMaps)
        {
            maps++;

            List<MapScriptEntry> list = Read(rom, header);

            if (list.Count == 0) none++;

            longest = Math.Max(longest, list.Count);
            entries += list.Count;

            foreach (MapScriptEntry entry in list)
            {
                byKind[entry.Kind] = byKind.GetValueOrDefault(entry.Kind) + 1;

                if (rom.ToOffsetOrNull(entry.Pointer) is not null) resolve++;
            }

            foreach (MapEntryScript condition in OnEntry(rom, header))
            {
                conditions++;
                variables.Add(condition.Variable);

                if (LooksLikeAVariable(condition.Variable)) inRange++;
            }
        }

        return new Survey(maps, none, longest, entries, resolve, byKind, conditions, inRange, variables.Count);
    }
}
