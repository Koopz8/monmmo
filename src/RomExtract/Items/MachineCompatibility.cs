using PokeMmo.Core.Battle;

namespace PokeMmo.RomExtract.Items;

/// <summary>
/// What each machine is allowed to teach, and to whom.
/// <para>
/// One eight-byte word per species, one bit per machine, in species order. Fifty-eight
/// machines, so the top six bits of every word are nothing at all — which is the shape
/// this is found by, and a weak shape: sixteen megabytes contain seven thousand runs of
/// four hundred and twelve words with a quiet top byte, and most of them are pointers.
/// </para>
/// <para>
/// The shape test has thousands of answers. The behaviour test has one, and it is a
/// cross-check between two tables located separately and for different reasons: a
/// machine teaches a move, and something that learns that move by growing up is
/// something the machine can teach. Score every candidate by how often its bits agree
/// with the level-up lists and the real table is not close — four hundred and fifty-four
/// agreements out of four hundred and fifty-seven, against sixty-six per cent for the
/// next best, which is this same table read eight bytes early.
/// </para>
/// <para>
/// The three that disagree are worth naming rather than smoothing over, because they are
/// all one peculiarity: a species that knows the move from birth and still cannot be
/// taught it. A table that agreed three hundred and fifty-seven times out of three
/// hundred and fifty-seven would be a table with no exceptions in it, and that would be
/// the more suspicious result.
/// </para>
/// </summary>
public sealed record MachineSets(int Address, IReadOnlyList<ulong> Masks, int Agreed, int Disagreed)
{
    public double Agreement => Agreed + Disagreed == 0 ? 0 : (double)Agreed / (Agreed + Disagreed);

    /// <summary>Whether this species may be taught by the machine at this position.</summary>
    public bool Allows(int species, int machine) =>
        species >= 0 && species < Masks.Count
        && machine >= 0 && machine < MachineMoves.Count
        && (Masks[species] & (1UL << machine)) != 0;
}

public static class MachineCompatibility
{
    private const int Entry = 8;

    /// <summary>
    /// How much of the level-up evidence the winner has to agree with.
    /// <para>
    /// Not one. A table that has to agree perfectly is a table this cartridge does not
    /// have — see the three above — and demanding it would throw away the right answer
    /// in favour of nothing.
    /// </para>
    /// </summary>
    private const double MustAgree = 0.9;

    /// <summary>
    /// And how far clear of the next best it has to be.
    /// <para>
    /// The runners-up are all the same bytes read at a shifted offset, and a shifted
    /// read of the right table still agrees by accident about half the time. A margin
    /// says "one candidate, not a field of them" without pretending the others scored
    /// nothing.
    /// </para>
    /// </summary>
    private const double Ahead = 0.2;

    /// <summary>
    /// A run of zeros has a quiet top byte for every word in it and says nothing about
    /// anything. Half the species have to be able to learn something.
    /// </summary>
    private const double Speaks = 0.5;

    public static MachineSets? Locate(
        Rom rom,
        int speciesCount,
        IReadOnlyList<int> machineMoves,
        IReadOnlyDictionary<int, Learnset> learnsets,
        Action<string>? log = null)
    {
        if (speciesCount <= 1 || machineMoves.Count != MachineMoves.Count || learnsets.Count == 0)
            return null;

        // Which machine teaches each move, so a level-up entry can be asked about.
        var machineOf = new Dictionary<int, int>();

        for (int i = 0; i < machineMoves.Count; i++) machineOf[machineMoves[i]] = i;

        ulong spare = ~((1UL << MachineMoves.Count) - 1);

        List<MachineSets> scored =
        [
            .. Runs(rom, speciesCount, spare)
                .Select(at => Score(rom, at, speciesCount, machineOf, learnsets))
                .OrderByDescending(s => s.Agreement)
                .Take(4)
        ];

        if (scored.Count == 0)
        {
            log?.Invoke("  machines: no compatibility table found — anything can learn anything");
            return null;
        }

        foreach (MachineSets candidate in scored)
        {
            log?.Invoke(
                $"  machines: table at 0x{Rom.BaseAddress + (uint)candidate.Address:X8} agrees with " +
                $"{candidate.Agreed} of {candidate.Agreed + candidate.Disagreed} level-up moves " +
                $"({candidate.Agreement:P1})");
        }

        MachineSets best = scored[0];

        if (best.Agreement < MustAgree)
        {
            log?.Invoke(
                $"  machines: best table only agrees {best.Agreement:P1} of the time — " +
                "not using it, since a wrong list refuses moves that should be allowed");

            return null;
        }

        if (scored.Count > 1 && best.Agreement - scored[1].Agreement < Ahead)
        {
            log?.Invoke(
                "  machines: two tables score alike — not using either, since there is no " +
                "way to tell which one the cartridge means");

            return null;
        }

        int silent = best.Masks.Count(m => m == 0);

        log?.Invoke(
            $"  machines: {best.Masks.Count - silent} species can be taught something, " +
            $"{silent} nothing at all");

        return best;
    }

    /// <summary>
    /// Every place a run of one word per species keeps its top six bits empty. Four
    /// -aligned, because a table of eight-byte words on this hardware is.
    /// </summary>
    private static IEnumerable<int> Runs(Rom rom, int speciesCount, ulong spare)
    {
        int span = speciesCount * Entry;
        int least = (int)(speciesCount * Speaks);

        for (int at = 0; at + span <= rom.Length; at += 4)
        {
            var ok = true;
            var speaking = 0;

            for (int i = 0; i < speciesCount; i++)
            {
                ulong word = Word(rom, at + i * Entry);

                if ((word & spare) != 0) { ok = false; break; }
                if (word != 0) speaking++;
            }

            if (ok && speaking >= least) yield return at;
        }
    }

    private static MachineSets Score(
        Rom rom,
        int at,
        int speciesCount,
        Dictionary<int, int> machineOf,
        IReadOnlyDictionary<int, Learnset> learnsets)
    {
        List<ulong> masks = [.. Enumerable.Range(0, speciesCount).Select(i => Word(rom, at + i * Entry))];

        int agreed = 0, disagreed = 0;

        foreach ((int species, Learnset learnset) in learnsets)
        {
            if (species <= 0 || species >= speciesCount) continue;

            ulong mask = masks[species];

            foreach (LevelUpMove entry in learnset.Moves)
            {
                if (!machineOf.TryGetValue(entry.MoveId, out int machine)) continue;

                if ((mask & (1UL << machine)) != 0) agreed++; else disagreed++;
            }
        }

        return new MachineSets(at, masks, agreed, disagreed);
    }

    private static ulong Word(Rom rom, int at) =>
        rom.ReadU32(at) | ((ulong)rom.ReadU32(at + 4) << 32);
}
