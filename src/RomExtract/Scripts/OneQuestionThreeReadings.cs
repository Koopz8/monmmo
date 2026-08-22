using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// How two readings of the same run of commands differ, over every call place in the game.
/// </summary>
/// <param name="Places">Call places both were asked of — the denominator.</param>
/// <param name="Agree">Where they say the same thing.</param>
/// <param name="CrudeSaysMore">Where the reading being replaced credits more.</param>
/// <param name="ReadSaysMore">Where the shared reading credits more.</param>
public sealed record HowTwoReadingsDiffer(
    string What,
    int Places,
    int Agree,
    int CrudeSaysMore,
    int ReadSaysMore)
{
    /// <summary>Places where the two do not say the same thing.</summary>
    public int Differ => Places - Agree;
}

/// <summary>
/// One question, three readings of it (298).
/// <para>
/// <b>"The run of commands before a call" is asked in three places in this repository, and every
/// one of them declared its own <c>Window = 4</c> and its own barriers.</b> The run AFTER a call
/// is asked in four more, on the same pattern. 295 replaced the backward distance in <see cref="SpecialCalls"/> with two rules
/// read off the script and 296 added the third; the other two copies were never touched — so
/// <c>--routines</c> printed <b>37</b> routines handed a value in one section and named <b>44</b>
/// in the column below it, in one output, and nothing compared them.
/// </para>
/// <para>
/// That is 224's rule and 220's together: five private copies of a list disagree with each other
/// and can be caught by comparing them, and a rule fixed in one arm and left standing in the other
/// contradicts itself out loud until somebody asks both. <b>Nobody asked both.</b> This asks all
/// three of the same 936 places, and the comparison stays in the output rather than in a commit
/// message.
/// </para>
/// </summary>
public static class OneQuestionThreeReadings
{
    private const byte SetVar = 0x16;

    /// <summary>A plain <c>call</c>, and the two conditional jumps.</summary>
    private static readonly byte[] Leaves = [0x04, 0x05, 0x06, 0x07];

    /// <summary>
    /// The two backward readings this repository had, asked of every call place.
    /// </summary>
    public static IReadOnlyList<HowTwoReadingsDiffer> Backwards(Rom rom, MapLibrary library)
    {
        int places = 0, contractsAgree = 0, contractsCrude = 0, contractsRead = 0;
        int waitedAgree = 0, waitedCrude = 0, waitedRead = 0;

        var seen = new HashSet<int>();

        foreach ((string mapId, string _, uint address) in library.EveryScript())
        {
            List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

            for (var i = 0; i < commands.Count; i++)
            {
                if (commands[i].Code is not (SpecialCalls.Special or SpecialCalls.SpecialVar))
                    continue;

                if (!seen.Add(commands[i].Offset)) continue;

                places++;

                List<(int Slot, int Value)> read = SpecialCalls.ArgumentsBefore(commands, i);

                int crude = SpecialContracts.TheCrudeReading(commands, i);

                if (crude == read.Count) contractsAgree++;
                else if (crude > read.Count) contractsCrude++;
                else contractsRead++;

                int selector = WhatIsWaitedFor.SelectorBefore(commands, i);
                int crudeSelector = WhatIsWaitedFor.TheCrudeReading(commands, i);

                if (crudeSelector == selector) waitedAgree++;
                else if (crudeSelector != WhatIsWaitedFor.NoSelector &&
                         selector == WhatIsWaitedFor.NoSelector) waitedCrude++;
                else waitedRead++;
            }
        }

        return
        [
            new HowTwoReadingsDiffer(
                "SpecialContracts.Arguments (how many slots)",
                places, contractsAgree, contractsCrude, contractsRead),
            new HowTwoReadingsDiffer(
                "WhatIsWaitedFor.SelectorBefore (what 0x8004 held)",
                places, waitedAgree, waitedCrude, waitedRead),
        ];
    }

    /// <summary>What a thing standing between a value and its call turns out to be (300).</summary>
    public enum WhatWasBetween
    {
        /// <summary>Nothing at all — the run from the value to the call is straight.</summary>
        Nothing,

        /// <summary>A <c>goto</c>: the call is not reached from the value at all.</summary>
        AGoto,

        /// <summary>A conditional jump: the call is reached on the arm it does not take.</summary>
        AConditionalJump,

        /// <summary>A call whose block writes no argument slot — the credit stands.</summary>
        ACallThatWritesNoSlot,

        /// <summary>A call whose block calls something — one level was not enough.</summary>
        ACallThatCallsSomething,

        /// <summary>A call whose block WRITES the very slot — the credit is wrong.</summary>
        ACallThatWritesTheSlot,
    }

    /// <summary>
    /// How many credited values have a <c>call</c> or a branch standing between them and the call
    /// they are credited to — 298's error bar, and 300's resolution of it.
    /// </summary>
    /// <remarks>
    /// <b>214 made a plain <c>call</c> a barrier in the ANSWER scan</b> because the block it jumps
    /// into can answer, and the ARGUMENT scan has no such barrier: a called block can write an
    /// argument slot exactly as it can write the answer variable. 298 printed the count of places
    /// where that could have happened; 300 follows the call one level and sorts them, and the
    /// finding is the NEGATIVE — <b>nought is a credit the block overwrote</b> (30).
    /// </remarks>

    /// <summary>
    /// 298's thirteen, followed one level and sorted (300).
    /// </summary>
    /// <remarks>
    /// <b>The worst verdict per place wins</b>, because a place with a call it cannot see past is
    /// unread whatever else is in the way. Trap 30: the interesting result here is the NEGATIVE —
    /// whether any credit turns out to be a value the block overwrote.
    /// </remarks>
    public static IReadOnlyDictionary<int, WhatWasBetween> Sorted(Rom rom, MapLibrary library)
    {
        var sorted = new Dictionary<int, WhatWasBetween>();
        var seen = new HashSet<int>();

        foreach ((string mapId, string _, uint address) in library.EveryScript())
        {
            List<ScriptCommand> commands = ScriptReader.ReadAll(rom, address);

            for (var i = 0; i < commands.Count; i++)
            {
                if (commands[i].Code is not (SpecialCalls.Special or SpecialCalls.SpecialVar))
                    continue;

                if (!seen.Add(commands[i].Offset)) continue;

                List<(int Slot, int Value)> handed = SpecialCalls.ArgumentsBefore(commands, i);

                if (handed.Count == 0) continue;

                sorted[commands[i].Offset] = WhatWasBetween.Nothing;

                List<ScriptCommand> run =
                [
                    .. SpecialCalls.Around(commands, i, SpecialCalls.Backwards).Select(c => c.Command),
                ];

                int furthest = run.FindLastIndex(
                    c => c.Code == SetVar &&
                         c.Word() is >= SpecialCalls.FirstArgument and <= SpecialCalls.LastArgument);

                foreach (ScriptCommand between in run.Take(furthest).Where(c => Leaves.Contains(c.Code)))
                {
                    WhatWasBetween verdict = between.Code switch
                    {
                        0x05 => WhatWasBetween.AGoto,
                        0x06 => WhatWasBetween.AConditionalJump,
                        _ => Follow(rom, between, handed),
                    };

                    if (verdict > sorted[commands[i].Offset]) sorted[commands[i].Offset] = verdict;
                }
            }
        }

        return sorted;
    }

    private static WhatWasBetween Follow(
        Rom rom, ScriptCommand call, List<(int Slot, int Value)> handed)
    {
        (IReadOnlySet<int> slots, bool nested) =
            SpecialCalls.WhichSlotsACallWrites(rom, call.Pointer(0));

        if (slots.Overlaps(handed.Select(a => a.Slot))) return WhatWasBetween.ACallThatWritesTheSlot;

        return nested
            ? WhatWasBetween.ACallThatCallsSomething
            : WhatWasBetween.ACallThatWritesNoSlot;
    }

}
