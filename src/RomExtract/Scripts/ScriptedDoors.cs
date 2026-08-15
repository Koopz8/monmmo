using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// Reads the doors that are on no square.
/// <para>
/// This is what 179 unreachable maps were waiting for. Walking the world with every move,
/// every flag, on the water and with nobody standing anywhere reaches 246 of 425 maps —
/// and of the 179 it does not, 174 have no door leading in from anywhere at all. That
/// number has sat in this project's startup log for a dozen milestones as a flat statement
/// about geometry, and it was never about geometry: the doors exist, they are written in
/// script rather than in map records, and nothing had asked.
/// </para>
/// <para>
/// The opcode was derived long before this instrument, by the shape of its arguments — a
/// bank and a map either name a map this cartridge has or they do not, and a square is
/// either inside that map or it is not, and 0x39 gets both right at 19 of 19 sites. The
/// width has been sitting in the reader ever since with nobody reading what it framed.
/// </para>
/// <para>
/// Read rather than run. A script's branches are the point — a boat that only sails once
/// a flag is set is still a boat — and running one picks a single arm, which would report
/// the doors of whichever save happened to be handy.
/// </para>
/// </summary>
public static class ScriptedDoors
{
    /// <summary>The command that puts somebody on another map.</summary>
    public const byte Warp = 0x39;

    /// <summary>
    /// A bank and a map of 0xFF: the cartridge saying "wherever the last one was".
    /// <para>
    /// Used by the scripts that put a player back where a scene interrupted them. That is
    /// a door to nowhere in particular and cannot be followed, so it is left out rather
    /// than reported as a link to a map 255.255 that no bank has.
    /// </para>
    /// </summary>
    private const byte Wherever = 0xFF;

    /// <summary>Every door the scripts on one map can make.</summary>
    public static List<ScriptedDoor> On(
        Rom rom, MapHeaderRecord header, int width, int height, Action<string>? log = null)
    {
        var found = new List<ScriptedDoor>();
        var seen = new HashSet<(string, int, int, int)>();

        foreach ((string what, uint address) in ScriptsOn(rom, header, width, height, log))
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (Read(command, what) is not { } door) continue;

                if (seen.Add((door.TargetMapId, door.TargetWarpId, door.X, door.Y))) found.Add(door);
            }
        }

        return found;
    }

    /// <summary>One command's arguments, as a map, a warp id and a square.</summary>
    public static ScriptedDoor? Read(ScriptCommand command, string what)
    {
        if (command.Code != Warp || command.Arguments.Length < 7) return null;

        byte bank = command.Arguments[0];
        byte number = command.Arguments[1];

        if (bank == Wherever || number == Wherever) return null;

        return new ScriptedDoor(
            what,
            WorldExporter.MapId(bank, number),
            command.Arguments[2],
            command.Word(3),
            command.Word(5));
    }

    /// <summary>
    /// Every script this map hangs off, with a name for where it came from.
    /// <para>
    /// The same three lists <see cref="SpecialCalls"/> sweeps, read from the header rather
    /// than from a loaded map — building a picture of four hundred maps to find out where
    /// the boats are would be an afternoon's work for a list of nineteen.
    /// </para>
    /// </summary>
    private static IEnumerable<(string What, uint Address)> ScriptsOn(
        Rom rom, MapHeaderRecord header, int width, int height, Action<string>? log)
    {
        foreach (MapObject person in MapLinkExtractor.ReadObjects(rom, header, width, height, log)
                     .Where(o => o.HasScript))
        {
            yield return ($"person {person.LocalId}", person.ScriptAddress);
        }

        foreach (MapTrigger trigger in MapLinkExtractor.ReadTriggers(rom, header, width, height, log)
                     .Where(t => t.HasScript))
        {
            yield return ($"trigger ({trigger.X},{trigger.Y})", trigger.ScriptAddress);
        }

        foreach (MapSign sign in MapLinkExtractor.ReadSigns(rom, header, width, height, log)
                     .Where(s => s.HasScript))
        {
            yield return ($"sign ({sign.X},{sign.Y})", sign.ScriptAddress);
        }
    }
}
