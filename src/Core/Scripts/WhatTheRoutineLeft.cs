namespace PokeMmo.Core.Scripts;

/// <summary>
/// One routine this run could not answer, and what the compare after it actually read (308).
/// </summary>
/// <remarks>
/// <para>
/// <b>"The run answers nought" has been in this project's prompt since 214.</b> It was measured
/// on <c>special 0x0187</c> and it is right there — and it is a sentence about a variable nothing
/// had written. An unanswerable <c>special</c> or <c>specialvar</c> writes NOTHING into the
/// answer slot, so the compare after it reads whatever is still in there, and for 0x0187's slot
/// at the time that was nought.
/// </para>
/// <para>
/// 307 found the other half by accident: one block reached from nineteen maps sets or clears a
/// flag on <c>specialvar 0x800D, 0x0180 ; compare 0x800D, 0</c> and it alternated pass to pass,
/// because the input alternated. <c>--trace 0x800D</c> then said <b>968 of 3646 reads found a
/// value already in the slot, against nine writes in the whole run.</b>
/// </para>
/// <para>
/// That number has no denominator of the right kind (8). Most reads of a slot are ordinary reads
/// of something a script legitimately wrote; a leftover only masquerades as an answer at a
/// compare that follows an unanswered call with nothing in between. This record is one of those
/// places, and the four buckets it sorts into are what turns a share into a reading.
/// </para>
/// </remarks>
/// <param name="Routine">The routine number the run stepped over.</param>
/// <param name="Slot">The variable it would have answered into.</param>
/// <param name="At">Where the call is, so the place can be gone back to.</param>
/// <param name="Held">What was in the slot when the call was stepped over.</param>
/// <param name="Read">
/// Whether anything read the slot before something else wrote it or the script ended. False is
/// the bucket where a leftover costs nothing at all, and it is not a small one.
/// </param>
/// <param name="Against">What the compare compared it against, when one did.</param>
/// <param name="Differs">
/// Whether the comparison came out differently than it would have with nought in the slot —
/// <b>the blast radius, and the column that can come back empty</b> (9).
/// <para>
/// Read off the comparison RESULT and not off the branch that follows it, which makes it an
/// upper bound in the safe direction: if the result is the same, no conditional after it can
/// take a different arm; if it differs, some conditional could, and whether the one actually
/// there cares is a further question this does not claim to answer.
/// </para>
/// </param>
public sealed record WhatTheRoutineLeft(
    int Routine,
    int Slot,
    uint At,
    int Held,
    bool Read,
    int Against,
    bool Differs)
{
    /// <summary>Whether a conditional consumed the comparison at all.</summary>
    /// <remarks>
    /// A comparison nobody branches on cannot differ however far apart the two results are, and
    /// this cartridge has such places — a <c>compare</c> whose next command is not a conditional.
    /// </remarks>
    public bool Branched { get; init; }

    /// <summary>
    /// <b>Whether the conditional after the comparison actually took a different arm</b> for the
    /// leftover than it would have for nought.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the number, and <see cref="Differs"/> is the loose version of it kept alongside as
    /// the argument for it (25). <c>special 0x0187</c> is compared against 2 at every one of its
    /// sites and every conditional there tests EQUAL: a slot holding 129 gives Greater where
    /// nought gives Less — <see cref="Differs"/> says yes — and neither is equal, so the branch
    /// is the same and this says no.
    /// </para>
    /// </remarks>
    public bool TookADifferentArm { get; init; }

    /// <summary>The slot held nothing, so this call did answer nought and the sentence was true.</summary>
    public bool AnsweredNought => Held == 0;

    /// <summary>
    /// Somebody read a value an earlier script left, and the reading turned out the same as it
    /// would have at nought. A leftover that changes no answer costs nothing (9).
    /// </summary>
    public bool ReadAndHarmless => Read && Held != 0 && !Differs;

    /// <summary>Somebody read a leftover and the comparison came out differently for it.</summary>
    public bool ReadAndDiffers => Read && Held != 0 && Differs;

    /// <summary>Somebody read a leftover and a branch went the other way for it — the blast radius.</summary>
    public bool ReadAndTookADifferentArm => Read && Held != 0 && TookADifferentArm;

    /// <summary>
    /// What a leftover of <paramref name="held"/> does to a comparison against
    /// <paramref name="against"/> that <paramref name="condition"/> then branches on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The whole correction is in the gap between the two answers.</b> Both are returned and
    /// both are printed, because the loose one is the argument for the tight one (25).
    /// </para>
    /// </remarks>
    /// <param name="condition">
    /// The condition byte of the conditional that consumes the comparison, or null when nothing
    /// branches on it at all — in which case no leftover can change anything, however far apart
    /// the two results are.
    /// </param>
    public static (bool Differs, bool TookADifferentArm) Reading(int held, int against, byte? condition)
    {
        Comparison forTheLeftover = ScriptState.Compare(held, against);
        Comparison forNought = ScriptState.Compare(0, against);

        return (
            forTheLeftover != forNought,
            condition is { } code
            && ScriptState.Accepts(code, forTheLeftover) != ScriptState.Accepts(code, forNought));
    }

    public override string ToString() =>
        $"0x{Routine:X3} -> 0x{Slot:X4} at 0x{At:X8} held {Held}"
        + (Read ? $", compared against {Against}{(Differs ? " — DIFFERS AT NOUGHT" : "")}" : ", nobody read it");
}
