using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>Somewhere a script names one particular item, and what it does with it.</summary>
/// <param name="How">
/// Which of the ways an item can be named. This is the whole point of the instrument: the
/// world file only records what it can attribute to an object, so an item handed over inside
/// a routine, or offered by a menu, leaves no trace there at all while being the obvious
/// place a player gets one.
/// </param>
public sealed record ItemSite(
    string MapId, string What, uint Address, int Offset, string How, int ItemId, int Count);

/// <summary>
/// Every place in the image where a script names a given item.
/// <para>
/// Built because the world file said something that could not be the whole truth: that the
/// only source of a FRESH WATER in FireRed is one shop counter, on a map two hops and a boat
/// away from anywhere the story reaches. The world file knows about <c>pokemart</c> shops —
/// so a vending machine that offers a menu and hands the drink over inside a routine would be
/// invisible to it while being the place everybody actually buys one.
/// </para>
/// <para>
/// That is a question about bytes, and this prints them rather than arguing about them.
/// </para>
/// </summary>
public static class ItemMentions
{
    /// <summary>
    /// The argument slot a script writes an item into before calling a standard routine.
    /// <para>
    /// Not a guess and not new here — it is the same pair <see cref="ScriptRunner"/> already
    /// reads a handover out of: 0x8000 takes the item and 0x8001 the count, and the routine
    /// that does the giving is code this project cannot follow. Both numbers are written down
    /// in plain sight by the script about to make the call.
    /// </para>
    /// </summary>
    private const int ItemSlot = 0x8000;

    /// <summary>
    /// Every mention of any of these items, anywhere a map can reach.
    /// <para>
    /// One pass over the world rather than one per item, for the reason the special sweep
    /// records: opening a map decompresses and renders it, and doing that once per item turns
    /// a few seconds into an afternoon.
    /// </para>
    /// </summary>
    public static List<ItemSite> Of(Rom rom, MapLibrary library, IReadOnlyCollection<int> items)
    {
        var found = new List<ItemSite>();

        foreach ((string mapId, string what, uint address) in library.EveryScript())
        {
            {
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
                {
                    foreach ((string how, int itemId, int count) in Names(rom, command))
                    {
                        if (items.Contains(itemId))
                            found.Add(new ItemSite(mapId, what, address, command.Offset, how, itemId, count));
                    }
                }
            }
        }

        return found;
    }

    /// <summary>
    /// What one command names, if it names an item at all.
    /// <para>
    /// Every one of these opcodes was settled earlier and separately, off the bytes, and the
    /// derivations are in the width table beside them. Nothing new is claimed here — this
    /// only asks all of them the same question at once, which nothing had.
    /// </para>
    /// </summary>
    private static IEnumerable<(string How, int ItemId, int Count)> Names(Rom rom, ScriptCommand command)
    {
        switch (command.Code)
        {
            // The two that hand something over. Both carry a word and a word, and both are
            // followed within a few commands by their own first word being written into
            // 0x8000 for the "obtained" fanfare.
            case 0x44:
            case 0x46:
                if (command.Word() != 0)
                    yield return ("handed over", command.Word(), Math.Max(1, command.Word(2)));

                break;

            case 0x45:
                if (command.Word() != 0)
                    yield return ("taken away", command.Word(), Math.Max(1, command.Word(2)));

                break;

            case 0x47:
                if (command.Word() != 0)
                    yield return ("asked for", command.Word(), Math.Max(1, command.Word(2)));

                break;

            // Named into a gap in a sentence — "PROF. OAK entrusted me with the {FD}{03}".
            // Not a handover on its own, and worth seeing anyway: at all five sites it was
            // derived from, the script hands over that exact item a few commands later.
            case 0x80:
                if (command.Word(1) != 0) yield return ("named in a sentence", command.Word(1), 1);

                break;

            // Loaded into the argument slot a standard routine reads from. This is the one
            // the world file cannot see, and the reason this instrument exists: which routine
            // does the giving is a number this project cannot resolve, so an item that only
            // ever appears here is an item nothing can attribute to anybody.
            case 0x16:                                  // setvar
            case 0x1A:                                  // copyvarifnotzero
                // The item slot only. The count beside it holds a number rather than an id,
                // and reading that as an item is how "one of them" becomes item 1.
                if (command.Word() == ItemSlot && command.Word(2) != 0)
                    yield return ("loaded for a routine", command.Word(2), 1);

                break;

            // A shop counter, which the world file does already see. Kept in so that one
            // listing shows every source at once — a list that quietly omitted the sources
            // already known would read as "and nowhere else".
            case ScriptCommands.PokeMart:
                foreach (int itemId in Stock(rom, command.Pointer())) yield return ("sold", itemId, 1);

                break;
        }
    }

    /// <summary>
    /// What a shop's list holds. A run of two-byte ids ending in a zero, and no count
    /// anywhere — the same reading the runner already makes.
    /// </summary>
    private static IEnumerable<int> Stock(Rom rom, uint address, int mostItems = 64)
    {
        if (rom.ToOffsetOrNull(address) is not { } list) yield break;

        for (var i = 0; i < mostItems; i++)
        {
            int at = list + i * 2;
            if (at + 2 > rom.Length) yield break;

            int itemId = rom.ReadU16(at);
            if (itemId == 0) yield break;

            yield return itemId;
        }
    }
}
