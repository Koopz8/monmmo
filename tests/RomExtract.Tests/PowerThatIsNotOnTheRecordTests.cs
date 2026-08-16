using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The four groups whose record says a power and is wrong on purpose.
/// <para>
/// Each carries a placeholder — one, or the base of a range — and the real number is worked
/// out at the moment the move is used. The arithmetic is modelled, because it lives in the
/// game's code. What it is worked out <em>from</em> is read: a creature's health, its
/// condition, and the six numbers it was born with.
/// </para>
/// <para>
/// That last one is why HIDDEN POWER is here at all rather than on the list of things this
/// project refuses to guess at. Its formula is an invention; its inputs are a save.
/// </para>
/// </summary>
public class PowerThatIsNotOnTheRecordTests
{
    private static SpeciesData Species(PokemonType type = PokemonType.Normal) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 200, BaseAttack = 100, BaseDefense = 100,
        BaseSpeed = 60, BaseSpAttack = 100, BaseSpDefense = 100,
        Type1 = type, Type2 = type,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    private static MoveData Move(byte effect, byte power = 1) =>
        new(1, string.Empty, effect, power, PokemonType.Normal, 100, 20, 0, 0, 0);

    private static Battler Make(Genes? born = null) =>
        new(Species(), 50, genes: born);

    private static int Hits(Battler attacker, MoveData move) =>
        DamageCalculator.Calculate(attacker, Make(), move, false, 100).Damage;

    /// <summary>None of the four is silent, and all four are finished rather than unwritten.</summary>
    [Theory]
    [InlineData(MovePower.Cornered)]
    [InlineData(MovePower.Spending)]
    [InlineData(MovePower.Regardless)]
    [InlineData(MovePower.Hidden)]
    public void NoneOfThemIsSilent(int effect) =>
        Assert.Equal(EffectKind.Nothing, MoveEffects.Of((byte)effect).Kind);

    /// <summary>
    /// FLAIL hits harder the less there is left, in steps rather than along a curve — five
    /// points fitted to a curve would be a guess dressed up as arithmetic.
    /// </summary>
    [Fact]
    public void OneHitsHarderTheLessThereIsLeft()
    {
        MoveData flail = Move(MovePower.Cornered);

        Battler whole = Make();
        Battler hurt = Make();
        Battler nearly = Make();

        hurt.TakeDamage(hurt.MaxHp / 2);
        nearly.TakeDamage(nearly.MaxHp - 1);

        int atFull = Hits(whole, flail);
        int atHalf = Hits(hurt, flail);
        int atOne = Hits(nearly, flail);

        Assert.True(atHalf > atFull, $"{atHalf} should beat {atFull}");
        Assert.True(atOne > atHalf, $"{atOne} should beat {atHalf}");
    }

    /// <summary>And one does the opposite, which is what makes them a pair worth telling apart.</summary>
    [Fact]
    public void AndAnotherHitsSofterTheLessThereIsLeft()
    {
        MoveData erupting = Move(MovePower.Spending);

        Battler whole = Make();
        Battler hurt = Make();

        hurt.TakeDamage(hurt.MaxHp * 3 / 4);

        Assert.True(Hits(whole, erupting) > Hits(hurt, erupting));
    }

    /// <summary>
    /// FACADE is twice the number on its own record while its user is suffering, which is
    /// the one of the four whose placeholder is a real power.
    /// </summary>
    [Fact]
    public void OneIsTwiceItsOwnRecordWhileItsUserIsSuffering()
    {
        MoveData facade = Move(MovePower.Regardless, 70);

        Battler well = Make();
        Battler ill = Make();

        ill.Status = StatusCondition.Burn;

        // Burn halves physical damage, so this is measured against a special-side comparison
        // rather than against the well one directly: what is asserted is that the record's
        // own number is not what was used.
        Assert.Null(MovePower.Of(facade, well));
        Assert.Equal(140, MovePower.Of(facade, ill));
    }

    // ---- the one whose inputs are a save ------------------------------------------------

    /// <summary>
    /// Two creatures of one species, born differently, get two different moves out of the
    /// same record. That is the whole of what makes this move worth having.
    /// </summary>
    [Fact]
    public void TwoCreaturesBornDifferentlyGetDifferentMoves()
    {
        MoveData hidden = Move(MovePower.Hidden, 1);

        Battler one = Make(Genes.Of([31, 31, 31, 31, 31, 31]));
        Battler another = Make(Genes.Of([30, 30, 30, 30, 30, 30]));

        Assert.NotEqual(MovePower.TypeOf(hidden, one), MovePower.TypeOf(hidden, another));
    }

    /// <summary>
    /// It is never Normal, and it is always one of the sixteen — a type outside the list
    /// would be a lookup off the end of it.
    /// </summary>
    [Fact]
    public void ItIsNeverNormalAndAlwaysOneOfTheSixteen()
    {
        MoveData hidden = Move(MovePower.Hidden, 1);

        var seen = new HashSet<PokemonType>();

        // Every combination of the lowest bit across the six, which is the whole input.
        for (int bits = 0; bits < 64; bits++)
        {
            int[] values =
            [
                .. Enumerable.Range(0, 6).Select(stat => 30 + ((bits >> stat) & 1)),
            ];

            PokemonType? type = MovePower.TypeOf(hidden, Make(Genes.Of(values)));

            Assert.NotNull(type);
            Assert.NotEqual(PokemonType.Normal, type);
            Assert.NotEqual(PokemonType.Mystery, type);

            seen.Add(type!.Value);
        }

        // And all sixteen are reachable, which is what says the scaling reaches the top of
        // its range rather than stopping one short.
        Assert.Equal(16, seen.Count);
    }

    /// <summary>
    /// Its power runs between thirty and seventy, both ends reachable.
    /// <para>
    /// Both ends, because dividing six bits by sixty-four instead of sixty-three is the
    /// commonest way to write this and it silently makes the top unreachable.
    /// </para>
    /// </summary>
    [Fact]
    public void ItsPowerRunsBetweenThirtyAndSeventyInclusive()
    {
        MoveData hidden = Move(MovePower.Hidden, 1);

        var powers = new HashSet<int>();

        for (int bits = 0; bits < 64; bits++)
        {
            int[] values =
            [
                .. Enumerable.Range(0, 6).Select(stat => 28 + (((bits >> stat) & 1) << 1)),
            ];

            powers.Add(MovePower.Of(hidden, Make(Genes.Of(values)))!.Value);
        }

        Assert.Equal(30, powers.Min());
        Assert.Equal(70, powers.Max());
    }

    /// <summary>
    /// And the type it comes out as is the one the damage is worked out with, not the one on
    /// its record.
    /// <para>
    /// Its own test because the calculator is where that could quietly stop being true — and
    /// it did: removing the lookup there left every other test in this file green, because
    /// the rest of them ask <see cref="MovePower"/> directly.
    /// </para>
    /// <para>
    /// Measured as "how many different numbers come out". Against a defender the type chart
    /// has opinions about, sixty-four different ways to be born produce a spread; a
    /// calculator using the record's own type produces one number sixty-four times, because
    /// the record says Normal for every one of them.
    /// </para>
    /// <para>
    /// The first version of this asserted a ghost always takes something, on the grounds that
    /// nothing Normal touches one. Ghosts are immune to Fighting as well, which is a type
    /// this move can come out as — so the test failed for a reason that was not the bug.
    /// </para>
    /// </summary>
    [Fact]
    public void TheTypeItComesOutAsIsTheOneTheDamageUses()
    {
        MoveData hidden = Move(MovePower.Hidden, 1);

        var rock = new Battler(Species(PokemonType.Rock), 50);

        var numbers = new HashSet<int>();

        for (int bits = 0; bits < 64; bits++)
        {
            int[] values =
            [
                .. Enumerable.Range(0, 6).Select(stat => 30 + ((bits >> stat) & 1)),
            ];

            numbers.Add(DamageCalculator.Calculate(Make(Genes.Of(values)), rock, hidden, false, 100).Damage);
        }

        Assert.True(numbers.Count > 1, "every way of being born hit for the same number");
    }

    /// <summary>And every other move is left alone, which is what the null is for.</summary>
    [Fact]
    public void EveryOtherMoveKeepsTheNumberOnItsRecord()
    {
        MoveData ordinary = Move(0x00, 80);

        Assert.Null(MovePower.Of(ordinary, Make()));
        Assert.Null(MovePower.TypeOf(ordinary, Make()));
    }
}
