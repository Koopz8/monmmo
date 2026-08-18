using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// 255 split the middle bucket four ways and reported the two condition lists ADDED TOGETHER.
/// <para>
/// <b>250's rule, one level in.</b> 250 exists because <c>--arrivals</c> asked its "does anything
/// write this variable at all" question of one of its two lists, got nought, and quoted the
/// nought. Asked of the other list the same bucket holds forty-three. 255 is the same shape
/// again: a four-way split of a total that mixes two populations <b>cannot come back different
/// for them</b>, and this cartridge's two lists disagree completely — the one-hop copy correction
/// is worth 76 conditions on the arrival list and NOUGHT on the square list, and the counter is
/// worth nought on the arrival list and all three of what the square list gained.
/// </para>
/// <para>
/// <b>And the rules moved out of the printer.</b> 255 decided its four answers with lambdas
/// inside a function that needs a whole cartridge, so no fixture could reach them and no break
/// could be aimed at them — the fault this project fixed at 219, 221, 222 and 223. They are
/// <see cref="WhatAVariableCanHold.HowReached"/>, <see cref="WhatAVariableCanHold.CanItFire"/>
/// and <see cref="WhenAMapRunsSomething.ByList"/> now, and this is what asks them.
/// </para>
/// </summary>
public sealed class TheSameSplitAskedOfEachListTests
{
    private const int Ceiling = 99;

    /// <summary>A variable a setvar, or a copy of a literal, puts values in.</summary>
    private const int Written = 0x406F;

    /// <summary>A variable something sets and something else steps.</summary>
    private const int Counted = 0x4002;

    /// <summary>A variable something copies into from a source this cannot read.</summary>
    private const int Copied = 0x405F;

    /// <summary>A variable written with one value and nothing else.</summary>
    private const int Neither = 0x400F;

    private static IReadOnlyDictionary<int, WhatItCanHold> CanHold() =>
        new Dictionary<int, WhatItCanHold>
        {
            [Written] = new(Written, [0, 3, 6], [], false),
            [Counted] = new(Counted, [0], [1], false),
            [Copied] = new(Copied, [], [], true) { From = [0x4001] },
            [Neither] = new(Neither, [1], [], false),
        };

    /// <summary>
    /// A condition in the MIDDLE BUCKET: something writes the variable, and never this value.
    /// </summary>
    /// <remarks>
    /// The tally has to carry a write of something else. A variable nothing writes at all is the
    /// OTHER bucket — 250's — and a fixture built out of those would have every one of these
    /// tests asking about a population the four answers are not about.
    /// </remarks>
    private static WhenAMapRunsSomething.Arrival Asking(
        int variable, int wanted, string asks, uint address = 0x08160000) =>
        WhenAMapRunsSomething.For(
            "3.42", variable, wanted, address, new Dictionary<int, int> { [4242] = 3 })
            with { Asks = asks };

    /// <summary>The same, for a condition a plain setvar already satisfies.</summary>
    private static WhenAMapRunsSomething.Arrival Satisfied(
        int variable, int wanted, string asks, uint address = 0x08160000) =>
        WhenAMapRunsSomething.For(
            "3.42", variable, wanted, address, new Dictionary<int, int> { [wanted] = 4 })
            with { Asks = asks };

    // -------------------------------------------------------------- the four answers, by name

    /// <summary>
    /// ALL FOUR ANSWERS ARE PRODUCED, AND THE TEST NAMES THEM. The rule is a list, so the fixture
    /// carries one of everything and says which is which — 251's lesson, where a count of four
    /// was satisfied by whatever four the code happened to have.
    /// </summary>
    [Fact]
    public void EachOfTheFourAnswersIsProducedAndNamedHere()
    {
        IReadOnlyDictionary<int, WhatItCanHold> hold = CanHold();

        Assert.Equal(
            HowItIsReached.Written,
            WhatAVariableCanHold.HowReached(hold, Written, 3, Ceiling));
        Assert.Equal(
            HowItIsReached.Counted,
            WhatAVariableCanHold.HowReached(hold, Counted, 2, Ceiling));
        Assert.Equal(
            HowItIsReached.Copied,
            WhatAVariableCanHold.HowReached(hold, Copied, 5, Ceiling));
        Assert.Equal(
            HowItIsReached.Neither,
            WhatAVariableCanHold.HowReached(hold, Neither, 5, Ceiling));

        // And a variable the scan does not write at all is Neither and not a crash.
        Assert.Equal(
            HowItIsReached.Neither,
            WhatAVariableCanHold.HowReached(hold, 0x40FF, 1, Ceiling));
    }

    /// <summary>
    /// AND THE ORDER IS THE ANSWER. A variable that is set, stepped AND copied into gets the
    /// strongest reading of the three, not whichever the code asks about first — four unordered
    /// booleans is how a condition lands in two buckets and the four counts stop adding up.
    /// </summary>
    [Fact]
    public void AValueSomethingSetsOutranksACounterAndACopy()
    {
        var everything = new Dictionary<int, WhatItCanHold>
        {
            [0x4001] = new(0x4001, [0, 3], [10], true) { From = [0x8004] },
        };

        // Three is set outright, thirteen is only reachable by counting, and five is neither —
        // a step of ten from nought or three lands on 0/3/10/13/20/23 and never on five.
        Assert.Equal(
            HowItIsReached.Written, WhatAVariableCanHold.HowReached(everything, 0x4001, 3, Ceiling));
        Assert.Equal(
            HowItIsReached.Counted, WhatAVariableCanHold.HowReached(everything, 0x4001, 13, Ceiling));
        Assert.Equal(
            HowItIsReached.Copied, WhatAVariableCanHold.HowReached(everything, 0x4001, 5, Ceiling));
    }

    // ------------------------------------------------------------- the verdict, and its error bar

    /// <summary>
    /// ALL FOUR VERDICTS ARE PRODUCED, AND THE TEST NAMES THEM. Three readings and an admission,
    /// and the admission is the error bar on the one before it.
    /// </summary>
    [Fact]
    public void EachOfTheFourVerdictsIsProducedAndNamedHere()
    {
        IReadOnlyDictionary<int, WhatItCanHold> hold = CanHold();

        Assert.Equal(
            WhetherItCanFire.SomethingWritesIt,
            WhatAVariableCanHold.CanItFire(hold, Written, 3, 0, Ceiling));
        Assert.Equal(
            WhetherItCanFire.SomethingWritesIt,
            WhatAVariableCanHold.CanItFire(hold, Counted, 2, 0, Ceiling));
        Assert.Equal(
            WhetherItCanFire.DoesNotKnow,
            WhatAVariableCanHold.CanItFire(hold, Copied, 5, 0, Ceiling));
        Assert.Equal(
            WhetherItCanFire.NothingCan,
            WhatAVariableCanHold.CanItFire(hold, Neither, 5, 0, Ceiling));
        Assert.Equal(
            WhetherItCanFire.ArmedFromTheStart,
            WhatAVariableCanHold.CanItFire(hold, Neither, 0, 0, Ceiling));
    }

    /// <summary>
    /// A PLAIN SETVAR IS ENOUGH ON ITS OWN. The verdict has to answer for conditions outside the
    /// middle bucket too, and those are decided by the tally <c>--arrivals</c> has always had
    /// rather than by the copy reading — without this the whole list would be re-decided by the
    /// half of it 255 built.
    /// </summary>
    [Fact]
    public void ASetvarThatWritesTheValueSettlesItWithoutTheCopyReading()
    {
        Assert.Equal(
            WhetherItCanFire.SomethingWritesIt,
            WhatAVariableCanHold.CanItFire(
                new Dictionary<int, WhatItCanHold>(), 0x40FF, 7, writtenWithThis: 4, Ceiling));
    }

    /// <summary>
    /// WANTING NOUGHT OUTRANKS AN ADMISSION OF IGNORANCE. Every variable holds nought before
    /// anything writes it, so a condition wanting nought is armed at the start whatever the
    /// copies do — and this is worth 72 of the square list's 228, so getting the order wrong
    /// turns most of a list from armed into unreadable.
    /// </summary>
    /// <remarks>
    /// The column is MODELLED and the instrument says so: nothing in this repository has read
    /// what the save's variable block holds before a script writes it. 250 asserted it in prose
    /// and did not mark it.
    /// </remarks>
    [Fact]
    public void WantingNoughtOutranksACopyItCannotRead()
    {
        Assert.Equal(
            WhetherItCanFire.ArmedFromTheStart,
            WhatAVariableCanHold.CanItFire(CanHold(), Copied, 0, 0, Ceiling));
    }

    // ---------------------------------------------------------------- the split, per list

    /// <summary>
    /// <b>THE THING.</b> The two lists get their own answers, and a fixture where they disagree
    /// completely says so — which is exactly this cartridge's shape: the copy correction is worth
    /// everything on one list and nothing on the other.
    /// </summary>
    [Fact]
    public void TheTwoListsAreSplitAndCanDisagreeCompletely()
    {
        WhenAMapRunsSomething.Arrival[] conditions =
        [
            Asking(Written, 3, WhenAMapRunsSomething.OnArrival),
            Asking(Written, 6, WhenAMapRunsSomething.OnArrival, 0x08160010),
            Asking(Counted, 2, WhenAMapRunsSomething.OnASquare, 0x08160020),
        ];

        IReadOnlyList<WhenAMapRunsSomething.Verdicts> byList =
            WhenAMapRunsSomething.ByList(conditions, CanHold(), Ceiling);

        WhenAMapRunsSomething.Verdicts arrival =
            byList.Single(v => v.Asks == WhenAMapRunsSomething.OnArrival);
        WhenAMapRunsSomething.Verdicts square =
            byList.Single(v => v.Asks == WhenAMapRunsSomething.OnASquare);

        // The copy correction is the arrival list's whole answer and none of the square list's.
        Assert.Equal(2, arrival.Middle[HowItIsReached.Written]);
        Assert.Equal(0, arrival.Middle[HowItIsReached.Counted]);

        // And the counter is the square list's whole answer and none of the arrival list's.
        Assert.Equal(0, square.Middle[HowItIsReached.Written]);
        Assert.Equal(1, square.Middle[HowItIsReached.Counted]);
    }

    /// <summary>
    /// AND EACH LIST'S DENOMINATOR IS ITS OWN. A share of the total rather than of the list is
    /// how 27.0% and 3.7% both read as 21.7% — the number that hid this for a milestone.
    /// </summary>
    [Fact]
    public void EachListIsCountedAgainstItsOwnSize()
    {
        WhenAMapRunsSomething.Arrival[] conditions =
        [
            Asking(Written, 3, WhenAMapRunsSomething.OnArrival),
            Asking(Written, 5, WhenAMapRunsSomething.OnArrival, 0x08160010),
            Asking(Counted, 2, WhenAMapRunsSomething.OnASquare, 0x08160020),
        ];

        IReadOnlyList<WhenAMapRunsSomething.Verdicts> byList =
            WhenAMapRunsSomething.ByList(conditions, CanHold(), Ceiling);

        Assert.Equal(2, byList.Single(v => v.Asks == WhenAMapRunsSomething.OnArrival).Conditions);
        Assert.Equal(1, byList.Single(v => v.Asks == WhenAMapRunsSomething.OnASquare).Conditions);
    }

    /// <summary>
    /// ONLY THE MIDDLE BUCKET IS SPLIT FOUR WAYS. A condition a setvar already satisfies is not
    /// one of the four answers, and counting it into them answers a different question with the
    /// same words — while the VERDICT counts every condition on the list.
    /// </summary>
    [Fact]
    public void TheFourAnswersAreAboutTheMiddleBucketAndTheVerdictIsAboutTheList()
    {
        WhenAMapRunsSomething.Arrival[] conditions =
        [
            Satisfied(Written, 3, WhenAMapRunsSomething.OnArrival),
            Asking(Written, 5, WhenAMapRunsSomething.OnArrival, 0x08160010),
        ];

        WhenAMapRunsSomething.Verdicts arrival =
            WhenAMapRunsSomething.ByList(conditions, CanHold(), Ceiling).Single();

        Assert.Equal(2, arrival.Conditions);
        Assert.Equal(2, arrival.Fire.Values.Sum());

        // One of the two is outside the middle bucket, so the four answers cover one condition.
        Assert.Equal(1, arrival.Middle.Values.Sum());
    }

    /// <summary>
    /// AND BOTH SPLITS ADD UP. Every condition gets exactly one verdict and every middle-bucket
    /// condition exactly one of the four answers — which is what makes them a split rather than
    /// four questions that happen to be asked together.
    /// </summary>
    [Fact]
    public void EveryConditionLandsInExactlyOneBucketOfEachSplit()
    {
        WhenAMapRunsSomething.Arrival[] conditions =
        [
            Satisfied(Written, 3, WhenAMapRunsSomething.OnArrival),
            Asking(Written, 5, WhenAMapRunsSomething.OnArrival, 0x08160010),
            Asking(Counted, 2, WhenAMapRunsSomething.OnASquare, 0x08160020),
            Asking(Copied, 5, WhenAMapRunsSomething.OnASquare, 0x08160030),
            Asking(Neither, 5, WhenAMapRunsSomething.OnASquare, 0x08160040),
            Asking(Neither, 0, WhenAMapRunsSomething.OnASquare, 0x08160050),
        ];

        IReadOnlyList<WhenAMapRunsSomething.Verdicts> byList =
            WhenAMapRunsSomething.ByList(conditions, CanHold(), Ceiling);

        Assert.Equal(6, byList.Sum(v => v.Conditions));
        Assert.Equal(6, byList.Sum(v => v.Fire.Values.Sum()));

        // Five of the six are in the middle bucket — the sixth is satisfied by a setvar.
        Assert.Equal(5, byList.Sum(v => v.Middle.Values.Sum()));
    }

    /// <summary>
    /// A LIST NOTHING ASKS IS NOT A ROW OF NOUGHTS. A cartridge whose triggers carry no condition
    /// should print one list, not two — a row of noughts reads as a measured empty bucket and
    /// this reading has already been burnt once by a bucket that was empty because nobody looked.
    /// </summary>
    [Fact]
    public void AListWithNoConditionsGetsNoRow()
    {
        IReadOnlyList<WhenAMapRunsSomething.Verdicts> byList = WhenAMapRunsSomething.ByList(
            [Asking(Written, 3, WhenAMapRunsSomething.OnArrival)], CanHold(), Ceiling);

        Assert.Equal(WhenAMapRunsSomething.OnArrival, Assert.Single(byList).Asks);
    }

    // ------------------------------------------------------------- where an unread copy came from

    /// <summary>
    /// A COPY'S SOURCE IS IN THE BYTES EVEN WHEN ITS VALUE IS NOT. <c>copyvar</c>'s second operand
    /// names a variable, and throwing that name away is how <c>0x405F</c> came to be reported as
    /// armed by nothing a script or the code could produce, while four copies on one map were
    /// filling it from <c>0x4001</c>.
    /// </summary>
    [Fact]
    public void AnUnreadCopyNamesTheVariableItCameFrom()
    {
        IReadOnlyDictionary<int, WhatItCanHold> held = WhatAVariableCanHold.From(
        [
            [Special(0x014B), CopyVar(0x405F, 0x4001)],
        ]);

        Assert.True(held[0x405F].Copied);
        Assert.Equal([0x4001], held[0x405F].From);
    }

    /// <summary>
    /// AND A COPY THIS READING RESOLVED NAMES NOTHING. The source list is the sources of the
    /// copies that made the answer "does not know" — a copy whose literal was read is not one of
    /// them, and folding the two together would put a source beside a value that is already
    /// known and invite a second hop nobody needs.
    /// </summary>
    [Fact]
    public void ACopyWhoseLiteralWasReadNamesNoSource()
    {
        IReadOnlyDictionary<int, WhatItCanHold> held = WhatAVariableCanHold.From(
        [
            [SetVar(0x8004, 3), CopyVar(0x406F, 0x8004)],
        ]);

        Assert.Equal([3], held[0x406F].Set);
        Assert.False(held[0x406F].Copied);
        Assert.Empty(held[0x406F].From);
    }

    private static ScriptCommand SetVar(int variable, int value) =>
        new(0, 0x16, [(byte)variable, (byte)(variable >> 8), (byte)value, (byte)(value >> 8)]);

    private static ScriptCommand CopyVar(int to, int from) =>
        new(0, 0x19, [(byte)to, (byte)(to >> 8), (byte)from, (byte)(from >> 8)]);

    private static ScriptCommand Special(int routine) =>
        new(0, 0x25, [(byte)routine, (byte)(routine >> 8)]);
}
