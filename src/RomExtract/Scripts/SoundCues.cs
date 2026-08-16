using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// One place a script names a song, and which of the ways it names it.
/// </summary>
/// <param name="MapId">The map the script belongs to.</param>
/// <param name="What">Which person, trigger or sign it hangs off.</param>
/// <param name="Offset">Where the command is, so the bytes can be looked at.</param>
/// <param name="Code">The command byte, kept rather than translated to a name.</param>
/// <param name="Song">The song number it carries.</param>
public sealed record SoundCue(string MapId, string What, int Offset, byte Code, int Song);

/// <summary>
/// Which song numbers the scripts actually fire, read off the cartridge.
/// <para>
/// The sound work could perform any song in the table and had no idea which ones a game
/// asks for. A faint, a door, a healing machine, an item picked up, a badge — every one of
/// them is a song number, and until now the only song number this project had ever used was
/// the one on a map header.
/// </para>
/// <para>
/// <b>Nothing here is a list of song numbers.</b> There is no table of "these are the sound
/// effects" anywhere in this file or on the cartridge. What there is, is a family of script
/// commands whose widths this project already derived the hard way — see the notes on 0x30
/// and 0x31 in <see cref="ScriptReader"/>, both of which were settled by counting sites
/// across the whole image. This walks every script on every map and prints the numbers those
/// commands carry.
/// </para>
/// <para>
/// <b>How the family was identified, and how far that goes.</b> The widths were derived
/// first, from the bytes, without reference to what any command does. Two of them then fell
/// into a pair that is very hard to read any other way: 0x31 takes a word and is followed
/// immediately by 0x32, which takes nothing, at all three of its sites. A command that names
/// something and a command that waits for that same something to finish is the shape of a
/// fanfare and its wait. That reading is <b>modelled</b>. The song numbers themselves are
/// <b>read</b>, and they are read whether the reading of the family is right or wrong: if
/// 0x2F turns out to be something else, this prints the numbers that something else carries,
/// which is still a fact about the cartridge.
/// </para>
/// </summary>
public static class SoundCues
{
    /// <summary>
    /// Names a one-off song and carries on. Eighty sites, and the most common of the family.
    /// </summary>
    public const byte PlayEffect = 0x2F;

    /// <summary>Waits for whatever <see cref="PlayEffect"/> started. Takes nothing.</summary>
    public const byte WaitEffect = 0x30;

    /// <summary>
    /// Names a song and carries on, and is followed by <see cref="WaitFor"/> at every site
    /// this project has read.
    /// </summary>
    public const byte PlayOver = 0x31;

    /// <summary>Waits for whatever <see cref="PlayOver"/> started. Takes nothing.</summary>
    public const byte WaitFor = 0x32;

    /// <summary>
    /// Names a song and a byte after it. Three bytes wide, which is a word and one more.
    /// </summary>
    public const byte PlayMusic = 0x33;

    /// <summary>Names a song, two bytes wide, and nothing after it.</summary>
    public const byte KeepMusic = 0x34;

    /// <summary>
    /// The commands that carry a song number, and where in their arguments it sits.
    /// <para>
    /// All four put it first, which is not an assumption: it is the only two-byte reading of
    /// a three-byte argument list that leaves the odd byte at the end rather than in front of
    /// the number, and the two-byte commands have nowhere else to put it.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<byte> NameASong = [PlayEffect, PlayOver, PlayMusic, KeepMusic];

    /// <summary>The commands that wait for one, which carry nothing.</summary>
    public static readonly IReadOnlyList<byte> WaitForOne = [WaitEffect, WaitFor];

    /// <summary>Every song number every script on every map names.</summary>
    public static List<SoundCue> All(Rom rom, MapLibrary library)
    {
        var found = new List<SoundCue>();

        foreach ((string mapId, string what, uint address) in Scripts(library))
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (!NameASong.Contains(command.Code)) continue;

                // A command whose arguments came back short is a read that went wrong
                // upstream, and a song number taken from it would be a number this walk
                // invented. Counted by its absence rather than filled in.
                if (command.Arguments.Length < 2) continue;

                found.Add(new SoundCue(mapId, what, command.Offset, command.Code, command.Word()));
            }
        }

        return found;
    }

    /// <summary>
    /// How many times each command of the family appears, including the ones that carry no
    /// song.
    /// <para>
    /// The waits are counted because they are the corroboration. If 0x31 names something that
    /// finishes and 0x32 waits for it, the two counts should be close and 0x32 should almost
    /// never appear without a 0x31 before it. A count of waits far larger than a count of
    /// plays would mean the family is read wrong, and that is worth knowing from a number
    /// rather than from a sound.
    /// </para>
    /// </summary>
    public static Dictionary<byte, int> Sites(Rom rom, MapLibrary library)
    {
        var counted = new Dictionary<byte, int>();

        foreach ((_, _, uint address) in Scripts(library))
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (!NameASong.Contains(command.Code) && !WaitForOne.Contains(command.Code))
                    continue;

                counted[command.Code] = counted.GetValueOrDefault(command.Code) + 1;
            }
        }

        return counted;
    }

    /// <summary>
    /// How often a play is followed immediately by its wait.
    /// <para>
    /// The one check in here that could fail, and the reason the pairing is worth stating as
    /// a number. Two commands next to each other at nearly every site is the evidence that
    /// they belong together; a handful of sites where they are not is ordinary, because a
    /// script is free to do something else in between.
    /// </para>
    /// </summary>
    public static (int Plays, int FollowedByAWait) Pairing(Rom rom, MapLibrary library, byte play, byte wait)
    {
        var plays = 0;
        var paired = 0;

        foreach ((_, _, uint address) in Scripts(library))
        {
            List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].Code != play) continue;

                plays++;

                if (i + 1 < commands.Count && commands[i + 1].Code == wait) paired++;
            }
        }

        return (plays, paired);
    }

    /// <summary>Every script on every map, with where it came from.</summary>
    private static IEnumerable<(string MapId, string What, uint Address)> Scripts(MapLibrary library)
    {
        foreach (LoadedMap map in library.All())
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            foreach (MapObject person in map.Objects.Where(o => o.HasScript))
                yield return (mapId, $"person {person.LocalId}", person.ScriptAddress);

            foreach (MapTrigger trigger in map.Triggers.Where(t => t.HasScript))
                yield return (mapId, $"trigger ({trigger.X},{trigger.Y})", trigger.ScriptAddress);

            foreach (MapSign sign in map.Signs.Where(s => s.HasScript))
                yield return (mapId, $"sign ({sign.X},{sign.Y})", sign.ScriptAddress);
        }
    }
}
