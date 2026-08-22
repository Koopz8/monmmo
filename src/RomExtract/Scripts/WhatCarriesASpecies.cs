using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>One place a species number is named, and by what.</summary>
/// <param name="At">The byte position of the command that names it.</param>
/// <param name="Species">The number.</param>
/// <param name="Second">
/// The command's second field, where it has one — <c>0xB6</c>'s third byte, or the value in
/// <c>0x8005</c>. Nought when there is none.
/// </param>
public sealed record ASpeciesNamed(int At, string MapId, int Species, int Second);

/// <summary>
/// One block that names a species, and everything in it that names the same number.
/// </summary>
/// <param name="Slot">
/// The argument slot a <c>setvar</c> puts the species in, or nought where none does.
/// </param>
public sealed record WhereASpeciesIsNamed(
    string MapId,
    int Species,
    ASpeciesNamed? ByTheCommand,
    ASpeciesNamed? ByTheCry,
    int Slot,
    int InTheSlot,
    IReadOnlyList<int> Routines);

/// <summary>
/// The number three things carry (301).
/// <para>
/// <b><c>0xB6</c> is <c>species, a byte, 00 00</c></b> — ten byte positions, eight species, and its
/// third byte takes one value per species (30, 34, 50, 70). <b><c>0xA1</c>'s first word is the same
/// species</b>, and that is not read off the range: seventeen operand positions in the map scan
/// have every distinct value inside the species table's named span, and <c>0xA1 arg0</c> ranks
/// twenty-second among them. It is read off the AGREEMENT — <b>of the 63 operand positions that
/// occur in the ten blocks holding a <c>0xB6</c>, exactly two ever name the number that <c>0xB6</c>
/// names, and <c>0xA1 arg0</c> does it 10 of 10</b> (290's floor, one command over).
/// </para>
/// <para>
/// The other is a <c>setvar</c>'s value, and that is the finding: <b>six blocks put the species in
/// an argument SLOT, and the slot is <c>0x8004</c> six times out of six.</b> Four of the six also
/// hold a <c>0xB6</c>; the two that do not are the only two places in the game that call
/// <c>special 0x01BB</c>, and it is handed the species in <c>0x8004</c> and the same 30..70 byte in
/// <c>0x8005</c>. <b>What that byte IS, is not read</b> — the wild tables' own levels are printed
/// beside it as the band the cartridge already affords, and that is as far as the data goes.
/// </para>
/// </summary>
public static class WhatCarriesASpecies
{
    /// <summary>The command whose first word and third byte this reading is about.</summary>
    public const byte TheCommand = 0xB6;

    /// <summary>The one whose first word names the same number.</summary>
    public const byte TheCry = 0xA1;

    private const byte SetVar = 0x16;

    /// <summary>
    /// Whether a number can be a species at all — an index the located table has a NAME for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A span is not a table.</b> The first version of this took the COUNT of named entries and
    /// asked whether the value was below it. It is not a span: 386 of the 412 entries carry a name
    /// and twenty-six do not, and those twenty-six are in the MIDDLE — indices 252 to 276 are a
    /// single <c>?</c> apiece. So <c>value &lt;= 386</c> threw away index 410, which is named, and
    /// the reading lost one of the two places it exists to explain. 264's rule — a placeholder is
    /// not a name — asked of the index rather than of the count.
    /// </para>
    /// <para>
    /// <b>And this is not the evidence anyway.</b> Seventeen operand positions in the map scan have
    /// every distinct value inside the named set. It is the population, not the discrimination (25).
    /// </para>
    /// </remarks>
    public static bool CouldBeASpecies(int value, IReadOnlySet<int> named) => named.Contains(value);

    /// <summary>Every block that names a species, and what names it there.</summary>
    public static IReadOnlyList<WhereASpeciesIsNamed> In(Rom rom, MapLibrary library, IReadOnlySet<int> named)
    {
        var found = new List<WhereASpeciesIsNamed>();
        var seen = new HashSet<uint>();

        foreach ((string mapId, string _, uint address) in library.EveryScript())
        {
            if (!seen.Add(address)) continue;

            found.AddRange(InOneBlock(ScriptReader.ReadAll(rom, address), mapId, named));
        }

        return found;
    }

    /// <summary>
    /// The same, asked of one block's commands (301).
    /// </summary>
    /// <remarks>
    /// <b>Split out because two breaks came back green.</b> Both rules — the command's second field
    /// being a BYTE at offset two, and the pair's second half being the slot BESIDE the species —
    /// lived inside a sweep that needs a whole cartridge, so no fixture could reach either. That is
    /// this repository's most repeated structural fault (219, 221, 222, 223, and 298 in the other
    /// arm), and the fix is always the same: move the rule to where a test can ask it.
    /// </remarks>
    public static IReadOnlyList<WhereASpeciesIsNamed> InOneBlock(
        List<ScriptCommand> commands, string mapId, IReadOnlySet<int> named)
    {
        var found = new List<WhereASpeciesIsNamed>();

        {
            foreach (int species in commands
                         .Where(c => c.Code is TheCommand or TheCry)
                         .Select(c => c.Word())
                         .Where(v => CouldBeASpecies(v, named))
                         .Distinct())
            {
                ScriptCommand? byCommand =
                    commands.FirstOrDefault(c => c.Code == TheCommand && c.Word() == species);

                ScriptCommand? byCry =
                    commands.FirstOrDefault(c => c.Code == TheCry && c.Word() == species);

                ScriptCommand? put = commands.FirstOrDefault(
                    c => c.Code == SetVar && c.Word(2) == species &&
                         c.Word() is >= SpecialCalls.FirstArgument and <= SpecialCalls.LastArgument);

                found.Add(new WhereASpeciesIsNamed(
                    mapId,
                    species,
                    byCommand is null
                        ? null
                        : new ASpeciesNamed(
                            byCommand.Offset, mapId, species,
                            byCommand.Arguments.Length > 2 ? byCommand.Arguments[2] : 0),
                    byCry is null ? null : new ASpeciesNamed(byCry.Offset, mapId, species, byCry.Word(2)),
                    put?.Word() ?? 0,
                    put is null ? 0 : Beside(commands, put, species),
                    put is null ? [] : [.. Called(commands, put)]));
            }
        }

        return found;
    }

    /// <summary>
    /// What the NEXT argument slot up holds where the species goes into one — the second field,
    /// asked of the pair rather than of the slot.
    /// </summary>
    private static int Beside(List<ScriptCommand> commands, ScriptCommand put, int species)
    {
        ScriptCommand? next = commands.FirstOrDefault(
            c => c.Code == SetVar && c.Word() == put.Word() + 1 && c.Offset > put.Offset);

        return next?.Word(2) ?? 0;
    }

    /// <summary>Which routines are called after the slot is filled, in the same block.</summary>
    private static IEnumerable<int> Called(List<ScriptCommand> commands, ScriptCommand put) =>
        commands
            .Where(c => c.Offset > put.Offset && c.Code is SpecialCalls.Special or SpecialCalls.SpecialVar)
            .Select(c => c.Code == SpecialCalls.Special ? c.Word() : c.Word(2))
            .Distinct();

    /// <summary>
    /// 290's floor, one command over: of every operand position occurring in the blocks that hold
    /// a <see cref="TheCommand"/>, which ever names the number it names?
    /// </summary>
    /// <remarks>
    /// <b>This is the reading and the range test is not.</b> A number in the species table's span
    /// is a fact about the span; a number that another command in the same block independently
    /// names is a fact about the two commands.
    /// </remarks>
    public static IReadOnlyList<(string Operand, int Agrees, int Occurs)> TheFloor(
        Rom rom, MapLibrary library)
    {
        var agrees = new Dictionary<string, int>();
        var occurs = new Dictionary<string, int>();
        var seen = new HashSet<uint>();

        foreach ((string _, string _, uint address) in library.EveryScript())
        {
            if (!seen.Add(address)) continue;

            List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

            List<int> species = [.. commands.Where(c => c.Code == TheCommand).Select(c => c.Word())];

            if (species.Count == 0) continue;

            var here = new HashSet<string>();
            var present = new HashSet<string>();

            foreach (ScriptCommand command in commands)
            {
                // Halfword positions at every byte offset — 290's stride.
                for (var at = 0; at + 1 < command.Arguments.Length; at++)
                {
                    if (command.Code == TheCommand && at == 0) continue;

                    string key = EveryOperand.NameOf(command.Code, at);

                    present.Add(key);

                    if (species.Contains(command.Arguments[at] | (command.Arguments[at + 1] << 8)))
                        here.Add(key);
                }
            }

            foreach (string key in present) occurs[key] = occurs.GetValueOrDefault(key) + 1;
            foreach (string key in here) agrees[key] = agrees.GetValueOrDefault(key) + 1;
        }

        return
        [
            .. occurs.Select(o => (o.Key, agrees.GetValueOrDefault(o.Key), o.Value))
                .OrderByDescending(o => o.Item2)
                .ThenBy(o => o.Key),
        ];
    }

    /// <summary>
    /// How many operand positions have EVERY distinct value inside the named span — the weak
    /// filter, printed so the reader can see which condition does the work (25).
    /// </summary>
    public static (int Inside, int Total, int Rank) TheWeakFilter(
        Rom rom, MapLibrary library, IReadOnlySet<int> named)
    {
        var byOperand = new Dictionary<string, HashSet<int>>();
        var seen = new HashSet<int>();

        foreach ((string _, string _, uint address) in library.EveryScript())
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (!seen.Add(command.Offset)) continue;

                for (var at = 0; at + 1 < command.Arguments.Length; at++)
                {
                    string key = EveryOperand.NameOf(command.Code, at);

                    if (!byOperand.TryGetValue(key, out HashSet<int>? values))
                        byOperand[key] = values = [];

                    values.Add(command.Arguments[at] | (command.Arguments[at + 1] << 8));
                }
            }
        }

        List<(string Key, double Share, int Count)> scored =
        [
            .. byOperand.Where(o => o.Value.Count >= 8)
                .Select(o => (o.Key,
                    (double)o.Value.Count(v => CouldBeASpecies(v, named)) / o.Value.Count,
                    o.Value.Count))
                .OrderByDescending(o => o.Item2)
                .ThenByDescending(o => o.Item3),
        ];

        return (scored.Count(s => s.Share == 1.0),
                scored.Count,
                scored.FindIndex(s => s.Key == EveryOperand.NameOf(TheCry, 0)) + 1);
    }
}
