using PokeMmo.Core.Sound;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;
using PokeMmo.RomExtract.Scripts;

namespace PokeMmo.RomExtract.Sound;

/// <summary>
/// One fight a script sets up, and the song the script named before it.
/// </summary>
/// <param name="MapId">Where the script is.</param>
/// <param name="What">Which person, trigger or sign runs it.</param>
/// <param name="Offset">Where the song was named, so the bytes can be looked at.</param>
/// <param name="Song">The song number.</param>
/// <param name="CommandsBefore">
/// How many commands sat between naming the song and starting the fight. Nought is a script
/// doing the two things next to each other; a larger number is weaker evidence and is kept
/// rather than smoothed away.
/// </param>
public sealed record ScriptedBattleTheme(
    string MapId, string What, int Offset, int Song, int CommandsBefore);

/// <summary>
/// The only battle music on this cartridge that can be read rather than decided.
/// <para>
/// A fight that a script sets up runs through <c>trainerbattle</c>, and a script is data. So
/// a script that names a song and then starts a fight has said, in bytes, which song that
/// fight plays. That covers the fights the story stops for — which is not most fights, and
/// the gap is the point of the count rather than something to paper over.
/// </para>
/// <para>
/// An ordinary wild encounter has no script at all, and an ordinary trainer's script names no
/// song. For those the choice is made in the sound driver's caller, from constants in a
/// switch, and no amount of looking at data will produce them. See <see cref="BattleMusic"/>
/// for what happens to a slot with no answer: the map's music keeps playing and the slot is
/// counted, rather than a plausible integer being written in and the count reporting zero.
/// </para>
/// </summary>
public static class BattleMusicLocator
{
    /// <summary>How many commands may sit between naming a song and starting a fight.</summary>
    /// <remarks>
    /// <b>Modelled</b>, and the one number in here that is. Four is wide enough for the
    /// message and the movement a script usually does in between and narrow enough that a
    /// song named for the room rather than for the fight does not get counted. Every match
    /// carries how far apart the two actually were, so a bad choice here is visible in the
    /// output rather than baked into it.
    /// </remarks>
    public const int Window = 4;

    /// <summary>Every fight a script both names a song for and then starts.</summary>
    public static List<ScriptedBattleTheme> All(Rom rom, MapLibrary library)
    {
        var found = new List<ScriptedBattleTheme>();

        foreach ((string mapId, string what, uint address) in library.EveryScript())
        {
            List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].Code != ScriptCommands.TrainerBattle) continue;

                // Backwards from the fight rather than forwards from the song. A script names
                // songs for all sorts of reasons and starts a fight for one, so the fight is
                // the thing worth anchoring to.
                for (int back = 1; back <= Window && i - back >= 0; back++)
                {
                    ScriptCommand earlier = commands[i - back];

                    if (earlier.Code != SoundCues.PlayMusic && earlier.Code != SoundCues.KeepMusic)
                        continue;

                    if (earlier.Arguments.Length < 2) break;

                    found.Add(new ScriptedBattleTheme(
                        mapId, what, earlier.Offset, earlier.Word(), back - 1));

                    break;
                }
            }
        }

        return found;
    }

    /// <summary>
    /// What can be said about battle music from this cartridge, as slots.
    /// <para>
    /// One slot filled, at most, and it is filled only when the scripts agree with themselves:
    /// the song has to be the same one at more than one site. A single site naming a song
    /// before a fight is a coincidence away from being nothing, and this project has been
    /// caught by a run of one before — the song table walk used to end at the first song it
    /// could not confirm for exactly that reason.
    /// </para>
    /// </summary>
    public static BattleMusic Themes(Rom rom, MapLibrary library, Action<string>? log = null)
    {
        var music = new BattleMusic();

        List<ScriptedBattleTheme> scripted = All(rom, library);

        log?.Invoke($"  {scripted.Count} scripted fights name a song before starting");

        if (scripted.Count == 0) return music;

        var bySong = scripted
            .GroupBy(s => s.Song)
            .OrderByDescending(g => g.Count())
            .ToList();

        foreach (IGrouping<int, ScriptedBattleTheme> group in bySong)
        {
            log?.Invoke(
                $"    song {group.Key}: {group.Count()} site(s) — "
                + string.Join(", ", group.Take(3).Select(s => $"{s.MapId} {s.What} at 0x{s.Offset:X6}")));
        }

        IGrouping<int, ScriptedBattleTheme> best = bySong[0];

        if (best.Count() < 2)
        {
            log?.Invoke(
                "    no song is named at more than one site, so none of them is evidence of a "
                + "battle theme rather than of one script doing one thing");

            return music;
        }

        music.Set(new BattleTheme(
            BattleKind.Scripted,
            best.Key,
            Read: true,
            $"named before {best.Count()} scripted fights, first at "
            + $"{best.First().MapId} 0x{best.First().Offset:X6}"));

        return music;
    }

}
