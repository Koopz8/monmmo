namespace PokeMmo.RomExtract.Scripts;

/// <summary>How a fight a script started came out, in the cartridge's own numbering.</summary>
/// <param name="Won">The value that means the creature was beaten.</param>
/// <param name="Ran">The value that means the player left without beating it.</param>
/// <param name="Caught">The value that means it is in the party now.</param>
public sealed record BattleOutcomes(int Won, int Ran, int Caught)
{
    /// <summary>Every value the cartridge's own scripts test against, in order.</summary>
    public IReadOnlyList<int> Tested { get; init; } = [];

    /// <summary>How many sites the reading was taken from.</summary>
    public int Sites { get; init; }
}

/// <summary>
/// Reads what the numbers behind a finished fight mean.
/// <para>
/// A script that starts a fight has to be told how it went, and the games tell it by
/// leaving a number in the result variable. Which number means what is not written
/// anywhere — it is code — so it is read off what the scripts do with each one.
/// </para>
/// <para>
/// Eighteen sites ask, and they fall into two shapes. Nine ask whether the answer is
/// one particular value and <em>return</em> when it is not: those nine are the nine
/// creatures there is only one of in the game, and the thing a script carries on to do
/// after one of those is hand it to you. So that value is <b>caught</b>.
/// </para>
/// <para>
/// The other nine test three values in a row. One of the three leads somewhere that
/// begins by setting a flag; the other two lead somewhere that does not. A flag is how
/// this cartridge writes down that something happened and will not happen again — the
/// bird on ONE ISLAND sets 0x2BC, MEWTWO sets 0x2F7 — so the value with the flag behind
/// it is <b>won</b>, and the ones without are the ways of walking away.
/// </para>
/// <para>
/// Nothing is remembered. Give this a cartridge whose numbering differs and it reads the
/// other numbering, which is the only test of a derivation that means anything.
/// </para>
/// </summary>
public static class BattleOutcomeLocator
{
    /// <summary>The command that asks a code routine for a number.</summary>
    private const byte SpecialVar = 0x26;

    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte SetFlag = 0x29;
    private const byte Return = 0x03;

    /// <summary>The variable every one of these answers into.</summary>
    private const int Result = 0x800D;

    /// <summary>
    /// What the numbers mean on this image, or nothing when the shape is not there.
    /// </summary>
    public static BattleOutcomes? Locate(Rom rom, Action<string>? log = null)
    {
        ReadOnlySpan<byte> data = rom.Span;

        if (Routine(rom) is not { } routine)
        {
            log?.Invoke("  battle outcomes: nothing on this image is asked and then compared three ways");
            return null;
        }

        var caught = new Dictionary<int, int>();
        var remembering = new Dictionary<int, int>();
        var walking = new Dictionary<int, int>();
        var tested = new List<int>();

        int sites = 0;

        for (int offset = 0; offset + 5 <= data.Length; offset++)
        {
            if (data[offset] != SpecialVar) continue;
            if ((data[offset + 1] | (data[offset + 2] << 8)) != Result) continue;
            if (data[offset + 3] != routine || data[offset + 4] != 0) continue;

            sites++;

            int at = offset + 5;
            var asked = new List<(int Value, uint Target)>();

            while (at + 11 <= data.Length &&
                   data[at] == Compare &&
                   (data[at + 1] | (data[at + 2] << 8)) == Result &&
                   data[at + 5] == GotoIf)
            {
                int value = data[at + 3] | (data[at + 4] << 8);
                byte condition = data[at + 6];
                uint target = (uint)(data[at + 7] | (data[at + 8] << 8) | (data[at + 9] << 16) | (data[at + 10] << 24));

                asked.Add((value, target));

                if (!tested.Contains(value)) tested.Add(value);

                // "If it is not this, stop" — the shape the one-of-a-kind creatures use,
                // and the only place a script's carrying on means the creature is yours.
                if (condition == 5 &&
                    rom.ToOffsetOrNull(target) is { } stops &&
                    data[stops] == Return)
                {
                    caught[value] = caught.GetValueOrDefault(value) + 1;
                }

                at += 11;
            }

            // A run of tests, each naming somewhere to go. Whether the first thing there
            // is a setflag is what tells the one that happened from the ones that did not.
            if (asked.Count < 2) continue;

            foreach ((int value, uint target) in asked)
            {
                if (rom.ToOffsetOrNull(target) is not { } lands) continue;

                if (data[lands] == SetFlag) remembering[value] = remembering.GetValueOrDefault(value) + 1;
                else walking[value] = walking.GetValueOrDefault(value) + 1;
            }
        }

        int? won = Best(remembering);
        int? was = Best(caught);
        int? ran = walking.Where(p => p.Key != won && p.Key != was)
            .OrderByDescending(p => p.Value)
            .Select(p => (int?)p.Key)
            .FirstOrDefault();

        if (won is null || was is null || ran is null)
        {
            log?.Invoke($"  battle outcomes: {sites} sites ask, and none of them says which number is which");
            return null;
        }

        log?.Invoke(
            $"  battle outcomes: routine 0x{routine:X2}, {sites} sites ask — " +
            $"won {won} (writes a flag at {remembering[won.Value]} sites), " +
            $"ran {ran}, caught {was} (carries on at {caught[was.Value]} sites); " +
            $"values tested: {string.Join(", ", tested.Order())}");

        return new BattleOutcomes(won.Value, ran.Value, was.Value)
        {
            Tested = tested,
            Sites = sites,
        };
    }

    /// <summary>
    /// Which code routine reports a fight, found by what is done with its answer.
    /// <para>
    /// Two hundred and thirty-four scripts ask this cartridge's routine 0x39 something
    /// and one asks 0x33, so the count of askers says nothing. What is distinctive is the
    /// <em>shape</em> of the asking: only a question with several separate answers is
    /// followed by a run of three or more compares against different numbers, one after
    /// another, each naming somewhere to go. A yes-or-no is compared once.
    /// </para>
    /// </summary>
    private static byte? Routine(Rom rom)
    {
        ReadOnlySpan<byte> data = rom.Span;
        var chains = new Dictionary<byte, int>();

        for (int offset = 0; offset + 5 <= data.Length; offset++)
        {
            if (data[offset] != SpecialVar) continue;
            if ((data[offset + 1] | (data[offset + 2] << 8)) != Result) continue;
            if (data[offset + 4] != 0) continue;

            int at = offset + 5;
            int chain = 0;

            while (at + 11 <= data.Length &&
                   data[at] == Compare &&
                   (data[at + 1] | (data[at + 2] << 8)) == Result &&
                   data[at + 5] == GotoIf)
            {
                chain++;
                at += 11;
            }

            if (chain >= 3) chains[data[offset + 3]] = chains.GetValueOrDefault(data[offset + 3]) + 1;
        }

        if (chains.Count == 0) return null;

        List<KeyValuePair<byte, int>> ranked = [.. chains.OrderByDescending(p => p.Value)];

        return ranked.Count == 1 || ranked[0].Value > ranked[1].Value ? ranked[0].Key : null;
    }

    /// <summary>The value the most sites agree on, when exactly one leads.</summary>
    private static int? Best(Dictionary<int, int> counts)
    {
        if (counts.Count == 0) return null;

        List<KeyValuePair<int, int>> ranked = [.. counts.OrderByDescending(p => p.Value)];

        return ranked.Count == 1 || ranked[0].Value > ranked[1].Value ? ranked[0].Key : null;
    }
}
