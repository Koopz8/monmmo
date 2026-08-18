using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// The two commands whose widths this project derived at 187 and whose meanings it never named.
/// </summary>
/// <remarks>
/// <para>
/// <c>0x63</c> takes three words and <c>0x65</c> takes a word and a byte, and 187 established
/// that the first word of each is <b>a person on that map</b> — from the shape of what surrounds
/// them, with the widths that made every site resume cleanly. What they do was left as a guess
/// and marked as one.
/// </para>
/// <para>
/// 225 found them again: the fan club's on-load chain calls <c>0x63 ; 0x65 ; return</c> once per
/// fan, and the run's silence at <c>0x0A3</c> runs both on all eight. So what they do is now on
/// the path of something the playthrough decides, which is reason enough to read them.
/// </para>
/// <para>
/// <b>Nothing here names them either.</b> What it does is measure the arguments against the map
/// they are on: does the person exist, do the other words land inside that map, how far from
/// where the cartridge put that person — each with a control that would answer the same way if
/// the reading were wrong.
/// </para>
/// </remarks>
public static class PersonCommands
{
    /// <summary>A person and two more words.</summary>
    public const byte Three = 0x63;

    /// <summary>A person and one byte.</summary>
    public const byte Two = 0x65;

    /// <param name="Person">The first word, which 187 read as a person's local id.</param>
    /// <param name="A">The second word, or the byte for <see cref="Two"/>.</param>
    /// <param name="B">The third word, or -1.</param>
    /// <param name="Square">Where the cartridge put that person, when there is such a person.</param>
    /// <param name="Movement">That person's movement type, when there is such a person.</param>
    public sealed record Site(
        string MapId,
        string What,
        int At,
        byte Code,
        int Person,
        int A,
        int B,
        (int X, int Y)? Square,
        int? Movement,
        int Width,
        int Height)
    {
        public bool ThereIsSuchAPerson => Square is not null;

        /// <summary>Whether the two words after the person land inside that map at all.</summary>
        public bool InsideTheMap => Code == Three && A >= 0 && A < Width && B >= 0 && B < Height;

        /// <summary>How far the words are from where the cartridge put that person.</summary>
        public int? Away =>
            Code == Three && Square is { } at ? Math.Abs(A - at.X) + Math.Abs(B - at.Y) : null;
    }

    /// <summary>
    /// Every site of both commands the map scan opens, with the person it names resolved against
    /// the map it is on.
    /// </summary>
    public static List<Site> In(Rom rom, MapLibrary library)
    {
        var found = new List<Site>();

        foreach (LoadedMap map in library.All())
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            foreach ((string _, string what, uint address) in MapLibrary.ScriptsOn(map))
            {
                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
                {
                    if (command.Code is not (Three or Two)) continue;
                    if (command.Arguments.Length < 3) continue;

                    int person = command.Word();

                    MapObject? who = map.Objects.FirstOrDefault(o => o.LocalId == person);

                    found.Add(new Site(
                        mapId,
                        what,
                        command.Offset,
                        command.Code,
                        person,
                        command.Code == Three ? command.Word(2) : command.Arguments[2],
                        command.Code == Three && command.Arguments.Length >= 6 ? command.Word(4) : -1,
                        who is null ? null : (who.X, who.Y),
                        who?.MovementType,
                        map.Collision.Width,
                        map.Collision.Height));
                }
            }
        }

        return found;
    }

    /// <summary>
    /// The same question asked of a word that is <b>not</b> the person id — the control.
    /// </summary>
    /// <remarks>
    /// "Almost every site names a person who is really on that map" means nothing on its own: a
    /// map with fifteen people accepts any small number, and these arguments are all small. The
    /// reading is worth something only if reading the WRONG word as the person id agrees far
    /// less often, and this is that number.
    /// </remarks>
    public static int NamesSomebody(IEnumerable<Site> sites) => sites.Count(s => s.ThereIsSuchAPerson);

    /// <summary>
    /// How many exact hits on the named person's own square would be expected <b>by chance</b>,
    /// if the two words were any square on that map.
    /// <para>
    /// The floor the count of exact hits needs. "Twenty-six of them are exactly where the
    /// cartridge put that person" says nothing without it: on a map of thirty by thirty a
    /// particular square comes up once in nine hundred, and twenty-six out of a hundred and
    /// twenty-six is either a coordinate pair or a coincidence, and only this number tells them
    /// apart.
    /// </para>
    /// </summary>
    public static double ExactlyThereByChance(IEnumerable<Site> sites) =>
        sites
            .Where(s => s.Code == Three && s.Square is not null && s.Width > 0 && s.Height > 0)
            .Sum(s => 1.0 / (s.Width * s.Height));

    /// <summary>
    /// How often the byte is the movement type of <b>somebody else</b> on that map — the floor
    /// for "it is that person's own".
    /// </summary>
    /// <remarks>
    /// A map whose people mostly stand still has the same movement type everywhere, and a byte
    /// that matches the named person would match anybody. This asks the same question of every
    /// OTHER person on the map and reports the rate, so "the person's own" is only worth
    /// something when it beats it.
    /// </remarks>
    public static double SomebodyElsesMovement(
        IEnumerable<Site> sites, IReadOnlyDictionary<string, IReadOnlyList<(int LocalId, int Movement)>> people)
    {
        var rate = 0.0;
        var counted = 0;

        foreach (Site site in sites.Where(s => s.Code == Two && s.Movement is not null))
        {
            if (!people.TryGetValue(site.MapId, out IReadOnlyList<(int LocalId, int Movement)>? on)) continue;

            List<(int LocalId, int Movement)> others = [.. on.Where(o => o.LocalId != site.Person)];

            if (others.Count == 0) continue;

            rate += (double)others.Count(o => o.Movement == site.A) / others.Count;
            counted++;
        }

        return counted == 0 ? 0 : rate;
    }

    /// <summary>How often the second word would name a person on that map instead.</summary>
    public static int TheOtherWordWould(
        IEnumerable<Site> sites, IReadOnlyDictionary<string, IReadOnlyList<(int LocalId, int Movement)>> people) =>
        sites.Count(s =>
            people.TryGetValue(s.MapId, out IReadOnlyList<(int LocalId, int Movement)>? on)
            && on.Any(o => o.LocalId == s.A));

    /// <summary>Who is on each map, as the two readings above want them.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<(int LocalId, int Movement)>> Everybody(
        MapLibrary library) =>
        library.All().ToDictionary(
            map => WorldExporter.MapId(map.Bank, map.Number),
            map => (IReadOnlyList<(int LocalId, int Movement)>)
                [.. map.Objects.Select(o => (o.LocalId, o.MovementType))]);
}
