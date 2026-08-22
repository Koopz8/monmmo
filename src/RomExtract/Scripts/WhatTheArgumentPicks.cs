namespace PokeMmo.RomExtract.Scripts;

/// <summary>What one routine's places do with one argument value (291).</summary>
/// <param name="Routine">The routine.</param>
/// <param name="Argument">The value <c>0x8004</c> held when it was called, or nought for none.</param>
/// <param name="Calls">How many calls are made that way.</param>
/// <param name="Places">
/// How many distinct byte positions those calls sit at.
/// <para>
/// <b>Both, because they are not the same number and this project has been caught by that.</b>
/// <c>0x194</c> is 1066 calls at 34 places — the ROUTINE inflation runs 1x to 97x — so a report
/// that says "236 places" when it means 236 calls has made 224's mistake in a new list.
/// </para>
/// </param>
/// <param name="Compared">The distinct values the answer is compared against, in order.</param>
public sealed record OneArgument(
    int Routine, int? Argument, int Calls, int Places, IReadOnlyList<int> Compared)
{
    public override string ToString() =>
        $"0x{Routine:X4} with 0x8004 = {(Argument is { } v ? v.ToString() : "none")}"
        + $"  {Calls} call(s) at {Places} place(s), compared against"
        + (Compared.Count == 0 ? " nothing" : " " + string.Join("/", Compared));
}

/// <summary>What a routine's argument turns out to select (291).</summary>
/// <param name="Routine">The routine.</param>
/// <param name="Arguments">Its distinct argument values, each with what is compared.</param>
public sealed record ARoutinesArguments(int Routine, IReadOnlyList<OneArgument> Arguments)
{
    /// <summary>Arguments whose answer anything compares at all.</summary>
    public IReadOnlyList<OneArgument> Asked => [.. Arguments.Where(a => a.Compared.Count > 0)];

    /// <summary>
    /// True when two of its arguments have the answer compared against different sets.
    /// <para>
    /// <b>This is what separates a SELECTOR from an operation.</b> A routine that does one thing
    /// is asked one kind of question wherever it is called; a routine whose argument picks which
    /// question to ask is compared against different things depending on the argument, and the
    /// script says so in its own words.
    /// </para>
    /// </summary>
    public bool TheArgumentChangesTheQuestion =>
        Asked.Select(a => string.Join(",", a.Compared)).Distinct().Count() > 1;

    /// <summary>
    /// The same question asked of the calls that CARRY an argument, ignoring the ones that set
    /// nothing.
    /// </summary>
    /// <remarks>
    /// <b>The two are different claims and one of this cartridge's two hits is only the first.</b>
    /// <c>0x17C</c> is three calls: one with no argument compared against 1, and two with
    /// arguments 129 and 214 both compared against 0. That is a difference between being given an
    /// argument and not, which says nothing about what the argument SELECTS. <c>0x194</c> is a hit
    /// either way.
    /// </remarks>
    public bool TheValueChangesTheQuestion =>
        Asked.Where(a => a.Argument is not null)
            .Select(a => string.Join(",", a.Compared))
            .Distinct()
            .Count() > 1;
}

/// <summary>
/// Whether a routine's argument picks WHICH question is being asked (291).
/// <para>
/// <b>236 counted <c>0x194</c>'s places and called them nineteen doors.</b> They are not doors.
/// It is called at 34 places, 31 of which set <c>0x8004</c> first, to eighteen different values
/// spanning 0..20 with 13, 14 and 15 missing — and what the script does with the answer depends on
/// which value it handed over: at <c>0x8004 = 16</c> the answer is compared against 0 and a nought
/// says "This is a two-on-two battle"; at <c>= 18</c> a one runs a <c>warp</c>.
/// </para>
/// <para>
/// What the routine DOES is still ARM code and unreadable (67). What it TAKES is readable, and it
/// takes an index.
/// </para>
/// <para>
/// The floor is every other routine asked with an argument at more than one value: if most of them
/// change the question too, this says nothing about <c>0x194</c> and a great deal about how this
/// cartridge uses <c>0x8004</c>.
/// </para>
/// </summary>
public static class WhatTheArgumentPicks
{
    /// <summary>
    /// Every routine called with more than one value in <paramref name="slot"/>, most places
    /// first.
    /// </summary>
    /// <param name="calls">Every routine call the map scan reads.</param>
    /// <param name="slot">
    /// Which argument slot to read.
    /// <para>
    /// <b>Defaulted to <c>0x8004</c> and that is not the only one (292).</b> 236 measured that 25
    /// of the 178 routines take a value in <c>0x8004</c> and every sweep since has read that slot
    /// and no other — so <c>0x015B</c>, the routine called sixteen times on <c>9.6</c> and nowhere
    /// else in the game, reads as "called with one value or none". Its argument is in
    /// <c>0x8008</c>. A sweep that can only see one shape reports the shapes it cannot see as
    /// absent, which is 290's stride one list over.
    /// </para>
    /// </param>
    public static IReadOnlyList<ARoutinesArguments> In(
        IEnumerable<SpecialCall> calls, int slot = TheArgument)
    {
        var found = new List<ARoutinesArguments>();

        foreach (var routine in calls.GroupBy(c => c.Routine))
        {
            List<OneArgument> arguments =
            [
                .. routine
                    .GroupBy(c => ArgumentOf(c, slot))
                    .Select(g => new OneArgument(
                        routine.Key,
                        g.Key,
                        g.Count(),
                        g.Select(c => c.At).Distinct().Count(),
                        [.. g.SelectMany(c => c.Compared.Select(v => v.Value)).Distinct().Order()]))
                    .OrderByDescending(a => a.Places)
                    .ThenByDescending(a => a.Calls)
                    .ThenBy(a => a.Argument),
            ];

            if (arguments.Count > 1) found.Add(new ARoutinesArguments(routine.Key, arguments));
        }

        return [.. found.OrderByDescending(r => r.Arguments.Sum(a => a.Places)).ThenBy(r => r.Routine)];
    }

    /// <summary>
    /// What <c>0x8004</c> held when this call was made, or nought when nothing set it.
    /// </summary>
    /// <remarks>
    /// <c>0x8004</c> and nothing else: 236 measured that 25 of the 178 routines take one, and the
    /// pairing it built its "0 of 95" on is (routine, 0x8004). Reading a different slot here would
    /// be a different population wearing the same name.
    /// </remarks>
    public static int? ArgumentOf(SpecialCall call, int slot = TheArgument) =>
        call.Arguments.Where(a => a.Variable == slot)
            .Select(a => (int?)a.Value)
            .LastOrDefault();

    /// <summary>
    /// Which argument slots this routine is ever handed a value in, and how many distinct values
    /// each carries — the question that has to be asked before any of the above (292).
    /// </summary>
    public static IReadOnlyList<(int Slot, int Values, int Calls)> SlotsOf(
        IEnumerable<SpecialCall> calls) =>
    [
        .. calls.SelectMany(c => c.Arguments.Select(a => (a.Variable, a.Value)))
            .GroupBy(a => a.Variable)
            .Select(g => (
                Slot: g.Key,
                Values: g.Select(a => a.Value).Distinct().Count(),
                Calls: g.Count()))
            .OrderByDescending(s => s.Values)
            .ThenByDescending(s => s.Calls)
            .ThenBy(s => s.Slot),
    ];

    /// <summary>The slot this cartridge's scripts put a routine's argument in.</summary>
    public const int TheArgument = 0x8004;
}
