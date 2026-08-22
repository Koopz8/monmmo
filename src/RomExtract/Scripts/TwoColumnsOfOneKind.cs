using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>One record of the command with two species columns.</summary>
/// <param name="Index">
/// The third halfword. It runs CONSECUTIVELY with the first species inside one scene, which is
/// what makes it an index rather than a value.
/// </param>
/// <param name="Kind">The fourth halfword, which takes nought or one.</param>
public sealed record TwoColumns(int At, string MapId, int First, int Second, int Index, int Kind);

/// <summary>How much two operands of one command draw from the same set.</summary>
/// <param name="Share">
/// The overlap as a share of the UNION, so that a big set does not beat a small one by being big.
/// </param>
public sealed record HowMuchTwoOperandsShare(
    string A, string B, int Overlap, int SizeA, int SizeB, double Share);

/// <summary>
/// Two columns of one kind (302).
/// <para>
/// <c>0xA2</c> is <b>four halfwords: a species, a species, an index and a nought-or-one</b>. Bytes
/// 1, 3 and 7 are nought in all 533 records, which is what the high half of a halfword field looks
/// like, and byte 5 takes 1, 2 and 3.
/// </para>
/// <para>
/// <b>That the two are the same KIND of field is read, not assumed.</b> 301 showed the range test
/// is worthless on its own — fifteen operand positions have every value inside the species table's
/// named set. This one is read off the pair: of the <b>134 pairs of operands of one command</b> in
/// the map scan where both have eight or more distinct values, <c>0xA2 arg0</c> and
/// <c>0xA2 arg2</c> share more of their union than any other — <b>rank 1 of 134</b>, 83.8% against
/// the runner-up's 68.6%.
/// </para>
/// <para>
/// <b>And what the index indexes is NOT FOUND.</b> 462 bases in the image put all 98 of its values
/// on a ROM pointer, against <b>nought in the reversed image</b> — so the structure is real — and
/// at the best of them ONE of the 98 targets reads as dialogue. 222's ending, with a floor.
/// </para>
/// </summary>
public static class TwoColumnsOfOneKind
{
    /// <summary>The command.</summary>
    public const byte TheCommand = 0xA2;

    /// <summary>Every record of it in the map scan, by byte position.</summary>
    public static IReadOnlyList<TwoColumns> In(Rom rom, MapLibrary library)
    {
        var found = new List<TwoColumns>();
        var seen = new HashSet<int>();

        foreach ((string mapId, string _, uint address) in library.EveryScript())
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (command.Code != TheCommand) continue;
                if (command.Arguments.Length < 8) continue;
                if (!seen.Add(command.Offset)) continue;

                found.Add(new TwoColumns(
                    command.Offset, mapId,
                    command.Word(), command.Word(2), command.Word(4), command.Word(6)));
            }
        }

        return found;
    }

    /// <summary>
    /// Every pair of operands of one command, ranked by how much of their union they share.
    /// </summary>
    /// <remarks>
    /// <b>The union, not the smaller set.</b> Scored against the smaller one a pair where one
    /// operand takes two values and the other takes two hundred wins by containing it, which is a
    /// fact about the sizes. The union is the version that cannot be gamed by either being big.
    /// </remarks>
    public static IReadOnlyList<HowMuchTwoOperandsShare> Ranked(
        Rom rom, MapLibrary library, int least = 8)
    {
        var byOperand = new Dictionary<(byte Code, int At), HashSet<int>>();
        var seen = new HashSet<int>();

        foreach ((string _, string _, uint address) in library.EveryScript())
        {
            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                if (!seen.Add(command.Offset)) continue;

                // Halfword positions at every byte offset — 290's stride.
                for (var at = 0; at + 1 < command.Arguments.Length; at++)
                {
                    if (!byOperand.TryGetValue((command.Code, at), out HashSet<int>? values))
                        byOperand[(command.Code, at)] = values = [];

                    values.Add(command.Arguments[at] | (command.Arguments[at + 1] << 8));
                }
            }
        }

        var pairs = new List<HowMuchTwoOperandsShare>();

        foreach (((byte code, int a), HashSet<int> first) in byOperand)
        {
            foreach (((byte other, int b), HashSet<int> second) in byOperand)
            {
                if (other != code || b <= a) continue;
                if (first.Count < least || second.Count < least) continue;

                pairs.Add(new HowMuchTwoOperandsShare(
                    EveryOperand.NameOf(code, a),
                    EveryOperand.NameOf(code, b),
                    first.Intersect(second).Count(),
                    first.Count,
                    second.Count,
                    (double)first.Intersect(second).Count() / first.Union(second).Count()));
            }
        }

        return [.. pairs.OrderByDescending(p => p.Share).ThenByDescending(p => p.Overlap)];
    }

    /// <summary>
    /// Where a table indexed by these numbers could be — every four-aligned base at which EVERY
    /// one of them lands on a ROM address, and how many of those targets read as dialogue.
    /// </summary>
    /// <remarks>
    /// <b>222's shape.</b> A hunt that finds candidates and cannot choose between them is a hunt
    /// that has not found the table, and saying so is the answer. The reversed image is the floor
    /// and it finds NOUGHT, so the candidates are not what these bytes do by accident — but of the
    /// ninety-eight targets at the best base, one reads as text.
    /// </remarks>
    public static IReadOnlyList<(int Base, int ReadsAsText, int Distinct)> WhereTheTableCouldBe(
        byte[] image, IReadOnlyList<int> ids)
    {
        var found = new List<(int, int, int)>();

        if (ids.Count == 0) return found;

        int top = ids.Max();

        for (var at = 0; at + 4 * (top + 1) <= image.Length; at += 4)
        {
            var targets = new List<int>(ids.Count);

            foreach (int id in ids)
            {
                int where = at + (4 * id);

                uint pointer = (uint)(image[where] | (image[where + 1] << 8) |
                                      (image[where + 2] << 16) | (image[where + 3] << 24));

                if (pointer < Rom.BaseAddress || pointer >= Rom.BaseAddress + (uint)image.Length)
                {
                    targets.Clear();

                    break;
                }

                targets.Add((int)(pointer - Rom.BaseAddress));
            }

            if (targets.Count != ids.Count) continue;

            found.Add((
                at,
                targets.Count(t => GameText.LooksLikeDialogue(
                    image.AsSpan(t, Math.Min(512, image.Length - t)))),
                targets.Distinct().Count()));
        }

        return found;
    }
}
