namespace PokeMmo.RomExtract.Scripts;

/// <summary>One operand, and how often it names the move its own script is about (290).</summary>
/// <param name="Code">The command.</param>
/// <param name="At">Which byte offset into the command's arguments the word starts at.</param>
/// <param name="Places">Times it appears inside a script that asks who knows a move.</param>
/// <param name="Matches">Times its value IS the move that script asked about.</param>
public sealed record NamesItsOwnMove(byte Code, int At, int Places, int Matches)
{
    public override string ToString() =>
        $"0x{Code:X2} arg{At}  {Matches} of {Places}";
}

/// <summary>
/// Which operands name the move the script they sit in is about (290).
/// <para>
/// <b><c>0x82</c>'s word takes seven values on this cartridge and every one is a named move</b> —
/// ICE BEAM, IRON TAIL, THUNDERBOLT, SHADOW BALL, FLAMETHROWER in one run of five, and CUT and
/// ROCK SMASH in the two obstacle scripts. Seven values inside a 355-wide table is worth very
/// little on its own: most operands in this game name small numbers.
/// </para>
/// <para>
/// What is worth something is the two. A script that asks who knows CUT holds a <c>0x82</c> whose
/// word is CUT's own id, and a script that asks who knows ROCK SMASH holds one whose word is ROCK
/// SMASH's. That is a coincidence at one in 355 twice over — <b>if no other operand does it</b>,
/// which is what this counts. 238 wrote "two of two is not a column, do not build on it" without a
/// floor; this is the floor.
/// </para>
/// <para>
/// The scripts are the ones <c>0x7C</c> opens — the command that takes a move id and hands back a
/// party slot — so the move each script is about is READ off the script rather than decided here.
/// </para>
/// </summary>
public static class WhatElseNamesTheMove
{
    /// <summary>
    /// Every operand that appears inside a move-asking script, with how often its value is that
    /// script's own move. Most matches first.
    /// </summary>
    /// <param name="rom">The cartridge.</param>
    /// <param name="from">Script addresses to read — the map scan's own list.</param>
    public static IReadOnlyList<NamesItsOwnMove> In(Rom rom, IEnumerable<uint> from)
    {
        var places = new Dictionary<(byte, int), int>();
        var matches = new Dictionary<(byte, int), int>();

        foreach (uint start in from.Distinct())
        {
            List<ScriptCommand> read = ScriptReader.ReadAll(rom, start);

            // WHICH MOVE THIS SCRIPT IS ABOUT, read off its own 0x7C. A script that asks about
            // two moves is thrown away rather than credited to either: "its own move" would then
            // be a choice made here, and a reading that has to choose is not a reading.
            IReadOnlyList<int> asked =
                [.. read.Where(c => c.Code == ObstacleMoves.FindMove).Select(c => c.Word()).Distinct()];

            if (asked.Count != 1) continue;

            int move = asked[0];

            foreach (ScriptCommand command in read)
            {
                // EVERY byte offset, not every halfword-aligned one.
                //
                // The first version stepped by two, which is what the operand sweeps in this
                // project do (244) — and `0x82` is a BYTE THEN A WORD, so its word starts at
                // offset one and the aligned reading saw 0x0F01 where the cartridge has 15. It
                // reported nought matches, which is also what a wrong answer looks like. 238's
                // own warning, one milestone over: the same width is not the same reading.
                //
                // Every offset is a bigger denominator as well as a correct numerator: an operand
                // position that lands on a coincidence has more chances to, which can only make
                // the floor harder to beat.
                for (var at = 0; at + 1 < command.Arguments.Length; at++)
                {
                    var key = (command.Code, at);

                    places[key] = places.GetValueOrDefault(key) + 1;

                    int value = command.Arguments[at] | (command.Arguments[at + 1] << 8);

                    if (value == move) matches[key] = matches.GetValueOrDefault(key) + 1;
                }
            }
        }

        return
        [
            .. places.Select(p => new NamesItsOwnMove(
                    p.Key.Item1, p.Key.Item2, p.Value, matches.GetValueOrDefault(p.Key)))
                .OrderByDescending(o => o.Matches)
                .ThenByDescending(o => o.Places)
                .ThenBy(o => o.Code)
                .ThenBy(o => o.At),
        ];
    }
}
