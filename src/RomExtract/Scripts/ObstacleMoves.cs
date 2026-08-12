using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// The moves that shift something out of the way, found by asking the map.
/// <para>
/// Two hundred objects across forty-seven maps open their script by naming a move and
/// asking which party slot knows it. Collecting those names gives a small set of move
/// ids that are certainly machine moves — which is exactly the foothold needed to find
/// the table saying what each teaching machine teaches, since nothing in an item's own
/// record says it.
/// </para>
/// <para>
/// Read rather than run. Every rock-smash rock in the game sits behind a badge check,
/// so a run with a fresh save takes the other arm and finds two of the three.
/// </para>
/// </summary>
public static class ObstacleMoves
{
    /// <summary>The command an obstacle opens with: a move id in, a party slot out.</summary>
    public const byte FindMove = 0x7C;

    public static List<int> Find(Rom rom, Action<string>? log = null)
    {
        var moves = new SortedSet<int>();

        foreach (LoadedMap map in MapLibrary.Open(rom).All())
        {
            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
            {
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, person.ScriptAddress))
                {
                    if (command.Code == FindMove) moves.Add(command.Word());
                }
            }
        }

        log?.Invoke(moves.Count == 0
            ? "  obstacles: nothing on any map asks who knows a move"
            : $"  obstacles: {moves.Count} moves shift something out of the way: {string.Join(", ", moves)}");

        return [.. moves];
    }
}
