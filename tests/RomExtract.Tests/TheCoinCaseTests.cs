using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The capacity of whatever the coin commands count is not written anywhere in the cartridge.
/// It is a bound plus a gift, at five sites that agree on neither.
/// <para>
/// So every fixture here has to have <b>two of whatever the key is made of</b> — two bounds,
/// two gifts, two lists, two doors — because a rule derived from a pair cannot be tested with
/// one of anything. That is fixtures-lie #7, and it is the whole design of this file.
/// </para>
/// </summary>
public class TheCoinCaseTests
{
    /// <summary>
    /// Something other than zero everywhere, and something that is not a command.
    /// <para>
    /// A zero-filled image is a NOP SLIDE — every <c>0x00</c> is a valid no-op, so a read that
    /// has drifted walks sixty bytes to whatever it was looking for and the fixture passes for
    /// the wrong reason. <c>0x77</c> has no width in this project, so a read that reaches filler
    /// stops there and the block fails the "reads as a script" filter, which is what filler
    /// should do.
    /// </para>
    /// </summary>
    private const byte Filler = 0x77;

    private const byte End = 0x02;
    private const byte Compare = 0x21;
    private const byte CompareVariables = 0x22;
    private const byte GotoIf = 0x06;
    private const byte Goto = 0x05;
    private const byte SetVar = 0x16;
    private const byte GiveItem = 0x46;
    private const byte GiveCreature = 0x79;
    private const byte CopyVar = 0x19;

    private const int Size = 0x20000;

    private static byte[] Blank()
    {
        var image = new byte[Size];

        Array.Fill(image, Filler);

        return image;
    }

    private static void Put(byte[] image, int at, params int[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++) image[at + i] = (byte)bytes[i];
    }

    private static void Word(byte[] image, int at, int value)
    {
        image[at] = (byte)(value & 0xFF);
        image[at + 1] = (byte)(value >> 8);
    }

    /// <summary>An address as the cartridge writes one: four bytes, little-endian, from 0x08000000.</summary>
    private static void Address(byte[] image, int at, int offset)
    {
        uint address = Rom.BaseAddress + (uint)offset;

        for (var i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>
    /// One guarded hand-over: read the count into a variable, compare it, branch, hand some over.
    /// </summary>
    private static void Ceiling(byte[] image, int at, int variable, int bound, int gift)
    {
        image[at] = TheCoinCase.HowMany;
        Word(image, at + 1, variable);

        image[at + 3] = Compare;
        Word(image, at + 4, variable);
        Word(image, at + 6, bound);

        image[at + 8] = GotoIf;
        image[at + 9] = 4;
        Address(image, at + 10, at);

        image[at + 14] = TheCoinCase.HandOver;
        Word(image, at + 15, gift);

        image[at + 17] = End;
    }

    /// <summary>
    /// TWO BOUNDS AND TWO GIFTS THAT AGREE ON NOTHING BUT THEIR SUM — AND A THIRD SITE THAT IS
    /// A COPY.
    /// <para>
    /// The fixture that matters. One chain says nothing at all — a bound and a gift always add
    /// up to something — and two chains with the same bound and the same gift are one fact
    /// written twice, which is why the count of distinct PAIRS is the number reported beside
    /// the sum.
    /// </para>
    /// <para>
    /// So there are three sites and two pairs here, deliberately. With three of each, "how many
    /// pairs" and "how many sites" are the same number and an instrument that confused them
    /// would pass — and this cartridge has exactly this shape, five sites and four pairs.
    /// </para>
    /// </summary>
    [Fact]
    public void TwoDifferentPairsSummingToTheSameNumberAreOneCapacity()
    {
        byte[] image = Blank();

        Ceiling(image, 0x1000, 0x4001, 90, 10);
        Ceiling(image, 0x2000, 0x4001, 70, 30);
        Ceiling(image, 0x3000, 0x4001, 70, 30);

        IReadOnlyList<TheCoinCase.Ceiling> found = TheCoinCase.Ceilings(new Rom(image));

        Assert.Equal(3, found.Count);

        (int sum, int sites, int pairs) = Assert.Single(TheCoinCase.Capacity(found));

        Assert.Equal(100, sum);
        Assert.Equal(3, sites);
        Assert.Equal(2, pairs);
    }

    /// <summary>
    /// And it has to be able to say no, or "they all agree" is a sentence with one outcome.
    /// </summary>
    [Fact]
    public void TwoPairsThatDoNotAgreeAreTwoGuardsAndNoCapacity()
    {
        byte[] image = Blank();

        Ceiling(image, 0x1000, 0x4001, 90, 10);
        Ceiling(image, 0x2000, 0x4001, 70, 40);

        IReadOnlyList<(int Sum, int Sites, int DistinctPairs)> capacity =
            TheCoinCase.Capacity(TheCoinCase.Ceilings(new Rom(image)));

        Assert.Equal(2, capacity.Count);
        Assert.Equal(new[] { 100, 110 }, capacity.Select(c => c.Sum).Order());
    }

    /// <summary>
    /// AND THE CONTROL CAN COME BACK EITHER WAY, which the one this instrument was first
    /// written with could not.
    /// <para>
    /// That one paired each bound with a gift it did not come with and reported that none of
    /// those sums was the answer. It is arithmetic: if every bound plus its own gift is
    /// <c>S</c> and no two sites share a pair, a bound crossed with somebody else's gift can
    /// never be <c>S</c>. The line was true before the cartridge was opened. <b>A control with
    /// one outcome is not a control</b>, and this project's own rule for a guard nothing can
    /// fail is decoy or deletion — it was deleted.
    /// </para>
    /// <para>
    /// The reversed image can say either thing, and this fixture makes it say the awkward one:
    /// bytes that read the same forwards and backwards give the floor the same chains as the
    /// image, so the floor agrees and the finding is worth nothing.
    /// </para>
    /// </summary>
    [Fact]
    public void TheFloorCanFindChainsOfItsOwnAndSayTheAgreementIsWorthNothing()
    {
        byte[] image = Blank();

        // Written so that reversing the image leaves two chains standing, by putting each one
        // in backwards as well as forwards.
        Ceiling(image, 0x1000, 0x4001, 90, 10);
        Ceiling(image, 0x2000, 0x4001, 70, 30);

        Backwards(image, Size - 0x1000 - 18, 0x4001, 90, 10);
        Backwards(image, Size - 0x2000 - 18, 0x4001, 70, 30);

        (int chains, int sums) = TheCoinCase.CeilingFloor(new Rom(image));

        Assert.Equal(2, chains);
        Assert.Equal(1, sums);
    }

    /// <summary>
    /// And the ordinary case: a file whose chains are only the right way round has an empty
    /// floor, which is the answer that makes the finding one.
    /// </summary>
    [Fact]
    public void AFileWhoseChainsAreOnlyOneWayRoundHasAnEmptyFloor()
    {
        byte[] image = Blank();

        Ceiling(image, 0x1000, 0x4001, 90, 10);
        Ceiling(image, 0x2000, 0x4001, 70, 30);

        Assert.Equal((0, 0), TheCoinCase.CeilingFloor(new Rom(image)));
    }

    /// <summary>
    /// A whole guarded hand-over written back to front, so that reversing the image turns it
    /// the right way round.
    /// </summary>
    private static void Backwards(byte[] image, int at, int variable, int bound, int gift)
    {
        var one = new byte[Size];

        Ceiling(one, 0, variable, bound, gift);

        for (var i = 0; i < 18; i++) image[at + i] = one[17 - i];
    }

    /// <summary>
    /// A compare about some other variable is a compare that happens to follow, which is three
    /// bytes of luck rather than a guard.
    /// </summary>
    [Fact]
    public void ACompareAboutADifferentVariableIsNotAGuardOnTheCount()
    {
        byte[] image = Blank();

        Ceiling(image, 0x1000, 0x4001, 90, 10);

        // The same chain, with the compare asking about a variable nobody read the count into.
        Ceiling(image, 0x2000, 0x4001, 70, 30);
        Word(image, 0x2000 + 4, 0x4002);

        TheCoinCase.Ceiling only = Assert.Single(TheCoinCase.Ceilings(new Rom(image)));

        Assert.Equal(0x1000, only.Offset);
    }

    /// <summary>
    /// A read with no compare after it is not a guard either — and the fixture has to put
    /// something ELSE four bytes wide there, carrying the same variable.
    /// <para>
    /// <c>B3 v; B4 g; end</c> looks like the test for this and is not one: the hand-over lands
    /// at index one, which the fall-through scan never reaches, so the instrument answers
    /// correctly for a reason that has nothing to do with the compare. A break that removed the
    /// compare check came back green against exactly that fixture. <c>copyvar</c> is the same
    /// four bytes with the same variable in the same place, and it discriminates.
    /// </para>
    /// </summary>
    [Fact]
    public void AReadFollowedBySomethingThatIsNotACompareIsNotAGuard()
    {
        byte[] image = Blank();

        Ceiling(image, 0x1000, 0x4001, 90, 10);

        // copyvar 0x4001 -> 0x8000, in the compare's place: four bytes, same variable first.
        image[0x1003] = CopyVar;
        Word(image, 0x1004, 0x4001);
        Word(image, 0x1006, 0x8000);

        Assert.Empty(TheCoinCase.Ceilings(new Rom(image)));
    }

    /// <summary>
    /// And a compare nobody branches on is not a guard: it is a comparison whose answer is
    /// thrown away, and the hand-over after it is unconditional.
    /// </summary>
    [Fact]
    public void ACompareNobodyBranchesOnIsNotAGuard()
    {
        byte[] image = Blank();

        Ceiling(image, 0x1000, 0x4001, 90, 10);

        // Something exactly as wide as the branch, in the branch's place, so the hand-over
        // still lands at index three and the ONLY thing that changed is that nothing acts on
        // the comparison. Filler here instead would stop the read dead and the fixture would
        // pass because the block was unreadable rather than because the branch was missing —
        // which is how the first version of this test let a break through.
        Put(image, 0x1008, TheCoinCase.AskAfterMoney, 0x01, 0x00, 0x00, 0x00, 0x00);

        Assert.Empty(TheCoinCase.Ceilings(new Rom(image)));
    }

    /// <summary>
    /// Money in, coins out, twice — and at two different rates, so "one price everywhere"
    /// cannot pass by there only being one.
    /// </summary>
    [Fact]
    public void TwoExchangesAtTwoRatesAreTwoRates()
    {
        byte[] image = Blank();

        Exchange(image, 0x1000, 1000, 50);
        Exchange(image, 0x2000, 900, 50);

        IReadOnlyList<TheCoinCase.Exchange> found =
            [.. TheCoinCase.Exchanges(new Rom(image)).OrderBy(e => e.Offset)];

        Assert.Equal(2, found.Count);

        Assert.Equal(1000, found[0].Asked);
        Assert.Equal(50, found[0].Given);
        Assert.Equal(1000, found[0].Paid);

        Assert.Equal(900, found[1].Asked);
    }

    /// <summary>Asking after money and never taking any is not a sale.</summary>
    [Fact]
    public void AskingAfterMoneyWithoutTakingAnyIsNotAnExchange()
    {
        byte[] image = Blank();

        Exchange(image, 0x1000, 1000, 50);

        // The question and the gift, and then an end instead of the command that takes the
        // money. Still a script, still hands something over, and not a sale.
        image[0x1000 + 20] = End;

        Assert.Empty(TheCoinCase.Exchanges(new Rom(image)));
    }

    private static void Exchange(byte[] image, int at, long asked, int given)
    {
        image[at] = TheCoinCase.AskAfterMoney;
        Money(image, at + 1, asked);

        image[at + 6] = Compare;
        Word(image, at + 7, 0x800D);
        Word(image, at + 9, 0);

        image[at + 11] = GotoIf;
        image[at + 12] = 1;
        Address(image, at + 13, at);

        image[at + 17] = TheCoinCase.HandOver;
        Word(image, at + 18, given);

        image[at + 20] = TheCoinCase.TakeMoney;
        Money(image, at + 21, asked);

        image[at + 26] = End;
    }

    private static void Money(byte[] image, int at, long value)
    {
        for (var i = 0; i < 4; i++) image[at + i] = (byte)(value >> (i * 8));

        image[at + 4] = 0;
    }

    /// <summary>
    /// TWO LISTS, TWO DOORS, AND THE DOOR IS WHAT SAYS WHICH TABLE A ROW IS READ AGAINST.
    /// <para>
    /// The fault this test exists for shipped in the first version of the instrument: every id
    /// in this cartridge's lists is inside the item table AND inside the species table, so a
    /// reading that tries one and falls back to the other answers with whichever came first and
    /// never says so. It named five creatures as berries and mail and looked entirely sane.
    /// </para>
    /// <para>
    /// Two lists in one fixture, because a fixture with one door cannot tell "it read the door"
    /// from "it always says the same thing".
    /// </para>
    /// </summary>
    [Fact]
    public void TheDoorSaysWhetherAListHoldsItemsOrCreatures()
    {
        byte[] image = Blank();

        // The thing that makes the second column the price: something takes it away.
        Put(image, 0x0800, TheCoinCase.TakeAway, 0x02, 0x40, End);

        Put(image, 0x0900, GiveItem, 0x01, 0x40, 0x01, 0x00, End);
        Put(image, 0x0A00, GiveCreature, 0x01, 0x40, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, End);

        Row(image, 0x1000, 0x4001, 11, 0x4002, 100, 0x0900);
        Row(image, 0x1010, 0x4001, 22, 0x4002, 200, 0x0900);

        Row(image, 0x2000, 0x4001, 33, 0x4002, 300, 0x0A00);
        Row(image, 0x2010, 0x4001, 44, 0x4002, 400, 0x0A00);

        IReadOnlyList<TheCoinCase.PriceList> lists =
            [.. TheCoinCase.PriceLists(new Rom(image)).OrderBy(l => l.Offset)];

        Assert.Equal(2, lists.Count);

        Assert.True(lists[0].HandsOverItems);
        Assert.False(lists[0].HandsOverCreatures);
        Assert.Equal(new[] { 100, 200 }, lists[0].Rows.Select(r => r.Price));

        Assert.False(lists[1].HandsOverItems);
        Assert.True(lists[1].HandsOverCreatures);
        Assert.Equal(new[] { 33, 44 }, lists[1].Rows.Select(r => r.Thing));
    }

    /// <summary>
    /// A pair of setvars nothing ever spends is not a price list, however much it looks like one.
    /// <para>
    /// The ordinary case in reverse: without this, every two-setvar-and-a-goto in the file is a
    /// price list and the instrument's own count means nothing.
    /// </para>
    /// </summary>
    [Fact]
    public void RowsWhoseSecondVariableNothingSpendsAreNotAPriceList()
    {
        byte[] image = Blank();

        Put(image, 0x0900, GiveItem, 0x01, 0x40, 0x01, 0x00, End);

        Row(image, 0x1000, 0x4001, 11, 0x4002, 100, 0x0900);
        Row(image, 0x1010, 0x4001, 22, 0x4002, 200, 0x0900);

        Assert.Empty(TheCoinCase.PriceLists(new Rom(image)));
    }

    /// <summary>One row is not a list — a row alone shares its door with nothing.</summary>
    [Fact]
    public void OneRowIsNotAList()
    {
        byte[] image = Blank();

        Put(image, 0x0800, TheCoinCase.TakeAway, 0x02, 0x40, End);
        Put(image, 0x0900, GiveItem, 0x01, 0x40, 0x01, 0x00, End);

        Row(image, 0x1000, 0x4001, 11, 0x4002, 100, 0x0900);

        Assert.Empty(TheCoinCase.PriceLists(new Rom(image)));
    }

    private static void Row(byte[] image, int at, int thingVar, int thing, int priceVar, int price, int exit)
    {
        image[at] = SetVar;
        Word(image, at + 1, thingVar);
        Word(image, at + 3, thing);

        image[at + 5] = SetVar;
        Word(image, at + 6, priceVar);
        Word(image, at + 8, price);

        image[at + 10] = Goto;
        Address(image, at + 11, exit);
    }

    /// <summary>
    /// The spending side: compare what is held against a price, branch, and take the price away.
    /// </summary>
    [Fact]
    public void ComparingWhatIsHeldAgainstAPriceAndThenTakingItIsASpend()
    {
        byte[] image = Blank();

        image[0x1000] = CompareVariables;
        Word(image, 0x1001, 0x800D);
        Word(image, 0x1003, 0x4002);

        image[0x1005] = GotoIf;
        image[0x1006] = 0;
        Address(image, 0x1007, 0x1000);

        Put(image, 0x100B, TheCoinCase.TakeAway, 0x02, 0x40, End);

        (int offset, int held, int price) = Assert.Single(TheCoinCase.Spends(new Rom(image)));

        Assert.Equal(0x1000, offset);
        Assert.Equal(0x800D, held);
        Assert.Equal(0x4002, price);
    }

    /// <summary>
    /// Taking away something OTHER than what was compared is not the same guard.
    /// </summary>
    [Fact]
    public void TakingAwayADifferentVariableIsNotThatSpend()
    {
        byte[] image = Blank();

        image[0x1000] = CompareVariables;
        Word(image, 0x1001, 0x800D);
        Word(image, 0x1003, 0x4002);

        image[0x1005] = GotoIf;
        image[0x1006] = 0;
        Address(image, 0x1007, 0x1000);

        Put(image, 0x100B, TheCoinCase.TakeAway, 0x03, 0x40, End);

        Assert.Empty(TheCoinCase.Spends(new Rom(image)));
    }

    /// <summary>
    /// The floor is counted in places, like the thing it is a floor for (206).
    /// <para>
    /// Written backwards, because the sweep reverses the image before it looks: <c>02 40 01
    /// B3</c> here is <c>B3 01 40 02</c> once reversed, which is a read of the count followed by
    /// an end.
    /// </para>
    /// </summary>
    [Fact]
    public void TheReversedImageFloorIsCountedInPlaces()
    {
        (int sites, int reads, int places) =
            TheCoinCase.NoiseFloor(new Rom(Backwards(0x8000, 0x8030, 0x8060, 0x8090, 0x80C0, 0x80F0)));

        Assert.Equal(6, sites);
        Assert.Equal(6, reads);

        Assert.True(
            places < reads,
            $"the floor has to be counted in places like the thing it is a floor for;"
            + $" got {places} place(s) from {reads} site(s)");
    }

    /// <summary>And the ordinary case, which is what stops "one place, always" passing.</summary>
    [Fact]
    public void AndTheFloorSaysAsManyPlacesAsSitesWhenTheyAreSpreadOut()
    {
        (int sites, int reads, int places) =
            TheCoinCase.NoiseFloor(new Rom(Backwards(0x2000, 0xA000, 0x18000)));

        Assert.Equal(3, sites);
        Assert.Equal(3, reads);
        Assert.Equal(3, places);
    }

    private static byte[] Backwards(params int[] at)
    {
        byte[] image = Blank();

        foreach (int o in at) Put(image, o, End, 0x40, 0x01, TheCoinCase.HowMany);

        return image;
    }
}
