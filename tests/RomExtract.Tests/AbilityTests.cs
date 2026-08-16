using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Abilities: the first thing in this project that is code all the way down.
/// <para>
/// Every number modelled so far was somewhere in the image waiting to be found. Half of
/// this is too — seventy-eight names at thirteen bytes each anchored on STENCH, and two
/// bytes on every species record that have been extracted since the species table was
/// first located and read by nothing. The other half is not in the cartridge as data at
/// all: what an ability <em>does</em> is ARM code, the same boundary the special routines
/// sit behind.
/// </para>
/// <para>
/// So these tests are about two different kinds of claim, and they are kept apart on
/// purpose. What was read has to match the cartridge. What was modelled only has to be
/// what this project says it is — and the count of what is <em>not</em> modelled is
/// published rather than rounded away.
/// </para>
/// </summary>
public class AbilityTests
{
    private static SpeciesData With(int first, int second = 0, PokemonType type = PokemonType.Normal) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 60, BaseAttack = 60, BaseDefense = 60,
        BaseSpeed = 60, BaseSpAttack = 60, BaseSpDefense = 60,
        Type1 = type, Type2 = type,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Ability1 = (byte)first,
        Ability2 = (byte)second,
    };

    /// <summary>
    /// A move of a type. Its category is not a field on this cartridge — it comes off the
    /// type, which is the generation's own rule — so a physical one is asked for by picking
    /// a type on the physical side of that line.
    /// </summary>
    private static MoveData Move(PokemonType type, int power = 60) =>
        new(1, string.Empty, 0, (byte)power, type, 100, 20, 0, 0, 0);

    // ---- which one a creature has -----------------------------------------------------

    /// <summary>
    /// A species with one ability gives it to both slots. The alternative is a creature
    /// whose ability depends on a coin flip the cartridge never makes.
    /// </summary>
    [Fact]
    public void OneAbilityMeansOneAbilityWhicheverSlotYouAreBornInto()
    {
        SpeciesData only = With(Abilities.Levitate);

        Assert.Equal(Abilities.Levitate, Abilities.Of(only, 0));
        Assert.Equal(Abilities.Levitate, Abilities.Of(only, 1));
    }

    [Fact]
    public void AndTwoMeansTheSlotDecides()
    {
        SpeciesData both = With(Abilities.Guts, Abilities.ThickFat);

        Assert.Equal(Abilities.Guts, Abilities.Of(both, 0));
        Assert.Equal(Abilities.ThickFat, Abilities.Of(both, 1));
    }

    /// <summary>And a species with one is never rolled into the empty slot.</summary>
    [Fact]
    public void NobodyIsBornIntoASlotThatIsEmpty()
    {
        var rng = new BattleRng(7);
        SpeciesData only = With(Abilities.Levitate);

        for (int roll = 0; roll < 50; roll++) Assert.Equal(0, Abilities.SlotFor(only, rng));
    }

    /// <summary>And a species with two eventually produces both, or the dice are not dice.</summary>
    [Fact]
    public void AndBothSlotsComeUpWhenThereAreTwo()
    {
        var rng = new BattleRng(11);
        SpeciesData both = With(Abilities.Guts, Abilities.ThickFat);

        var seen = new HashSet<int>();

        for (int roll = 0; roll < 100; roll++) seen.Add(Abilities.SlotFor(both, rng));

        Assert.Equal([0, 1], seen.Order());
    }

    // ---- what they do ------------------------------------------------------------------

    /// <summary>LEVITATE, which is the ability most players would notice missing first.</summary>
    [Fact]
    public void LevitateIsNotHitByTheGround()
    {
        var floating = new Battler(With(Abilities.Levitate), 50) { AbilitySlot = 0 };
        var attacker = new Battler(With(0), 50);

        DamageResult ground = DamageCalculator.Calculate(attacker, floating, Move(PokemonType.Ground), false, 100);
        DamageResult normal = DamageCalculator.Calculate(attacker, floating, Move(PokemonType.Normal), false, 100);

        Assert.Equal(0, ground.Damage);
        Assert.True(ground.NoEffect);
        Assert.True(normal.Damage > 0);
    }

    [Theory]
    [InlineData(Abilities.VoltAbsorb, PokemonType.Electric)]
    [InlineData(Abilities.WaterAbsorb, PokemonType.Water)]
    [InlineData(Abilities.FlashFire, PokemonType.Fire)]
    public void TheAbsorbingOnesTakeNothingFromTheirOwnType(int ability, PokemonType type)
    {
        var defender = new Battler(With(ability), 50) { AbilitySlot = 0 };
        var attacker = new Battler(With(0), 50);

        Assert.Equal(0, DamageCalculator.Calculate(attacker, defender, Move(type), false, 100).Damage);
    }

    /// <summary>
    /// WONDER GUARD lets only what is already super effective through — and a status move
    /// is not damage, so it is not this ability's business.
    /// </summary>
    [Fact]
    public void WonderGuardOnlyLetsThroughWhatIsSuperEffective()
    {
        // A Rock type, so Water is super effective on it and Normal is not.
        var guarded = new Battler(With(Abilities.WonderGuard, type: PokemonType.Rock), 50) { AbilitySlot = 0 };
        var attacker = new Battler(With(0), 50);

        Assert.Equal(0, DamageCalculator.Calculate(attacker, guarded, Move(PokemonType.Normal), false, 100).Damage);
        Assert.True(DamageCalculator.Calculate(attacker, guarded, Move(PokemonType.Water), false, 100).Damage > 0);
    }

    /// <summary>THICK FAT, which is a reduction rather than a refusal.</summary>
    [Fact]
    public void ThickFatHalvesFireAndIce()
    {
        var padded = new Battler(With(Abilities.ThickFat), 50) { AbilitySlot = 0 };
        var bare = new Battler(With(0), 50);
        var attacker = new Battler(With(0), 50);

        int hot = DamageCalculator.Calculate(attacker, padded, Move(PokemonType.Fire), false, 100).Damage;
        int plain = DamageCalculator.Calculate(attacker, bare, Move(PokemonType.Fire), false, 100).Damage;

        Assert.True(hot < plain);
        Assert.InRange(hot, plain / 2 - 1, plain / 2 + 1);
    }

    [Fact]
    public void PurePowerDoublesWhatAPhysicalHitDoes()
    {
        var strong = new Battler(With(Abilities.PurePower), 50) { AbilitySlot = 0 };
        var plain = new Battler(With(0), 50);
        var target = new Battler(With(0), 50);

        int doubled = DamageCalculator.Calculate(strong, target, Move(PokemonType.Normal), false, 100).Damage;
        int ordinary = DamageCalculator.Calculate(plain, target, Move(PokemonType.Normal), false, 100).Damage;

        Assert.True(doubled > ordinary);
    }

    /// <summary>
    /// GUTS hits harder for being ill — and specifically is not also punished by the burn
    /// it is being rewarded for, which would leave it worse off than an unburned one.
    /// </summary>
    [Fact]
    public void GutsIsHelpedByABurnRatherThanHurtByIt()
    {
        var gutsy = new Battler(With(Abilities.Guts), 50) { AbilitySlot = 0 };
        var plain = new Battler(With(0), 50);
        var target = new Battler(With(0), 50);

        int healthy = DamageCalculator.Calculate(gutsy, target, Move(PokemonType.Normal), false, 100).Damage;

        gutsy.Status = StatusCondition.Burn;
        plain.Status = StatusCondition.Burn;

        int burned = DamageCalculator.Calculate(gutsy, target, Move(PokemonType.Normal), false, 100).Damage;
        int burnedPlain = DamageCalculator.Calculate(plain, target, Move(PokemonType.Normal), false, 100).Damage;

        Assert.True(burned > healthy);
        Assert.True(burned > burnedPlain * 2);
    }

    /// <summary>And the four type boosts only switch on once the fight has gone badly.</summary>
    [Fact]
    public void OvergrowOnlyHelpsWhenItIsCornered()
    {
        var grassy = new Battler(With(Abilities.Overgrow, type: PokemonType.Grass), 50) { AbilitySlot = 0 };
        var target = new Battler(With(0), 50);

        int healthy = DamageCalculator.Calculate(grassy, target, Move(PokemonType.Grass), false, 100).Damage;

        grassy.TakeDamage(grassy.MaxHp - grassy.MaxHp / 4);

        int cornered = DamageCalculator.Calculate(grassy, target, Move(PokemonType.Grass), false, 100).Damage;

        Assert.True(cornered > healthy);
    }

    // ---- the ones that refuse ----------------------------------------------------------

    [Theory]
    [InlineData(Abilities.Limber, StatusCondition.Paralysis)]
    [InlineData(Abilities.Insomnia, StatusCondition.Sleep)]
    [InlineData(Abilities.VitalSpirit, StatusCondition.Sleep)]
    [InlineData(Abilities.Immunity, StatusCondition.Poison)]
    [InlineData(Abilities.WaterVeil, StatusCondition.Burn)]
    [InlineData(Abilities.MagmaArmor, StatusCondition.Freeze)]
    public void SomeOfThemSimplyRefuse(int ability, StatusCondition condition)
    {
        var refusing = new Battler(With(ability), 50) { AbilitySlot = 0 };

        Assert.False(refusing.TryApplyStatus(condition));
        Assert.Equal(StatusCondition.None, refusing.Status);
    }

    /// <summary>And refuse only the one thing they refuse.</summary>
    [Fact]
    public void AndOnlyTheOneThingTheyRefuse()
    {
        var limber = new Battler(With(Abilities.Limber), 50) { AbilitySlot = 0 };

        Assert.True(limber.TryApplyStatus(StatusCondition.Poison));
        Assert.Equal(StatusCondition.Poison, limber.Status);
    }

    // ---- carrying it around ------------------------------------------------------------

    /// <summary>
    /// The slot survives a restart, which is what makes it a fact about a creature rather
    /// than about a session. A creature immune to Ground on Tuesday and not on Wednesday
    /// would be worse than one that was never immune at all.
    /// </summary>
    [Fact]
    public async Task TheSlotSurvivesARestart()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon born = new(1, 20, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
            {
                AbilitySlot = 1,
            };

            SavedCharacter character = new("1.0", 3, 4, Direction.Down, [born]);

            using (var writing = new SqlitePlayerStore(path))
                await writing.RegisterAsync("Mason", "a-good-password", character);

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                Assert.Equal(1, Assert.Single(login.Character.Party).AbilitySlot);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And a battler carries it in and hands it back out.</summary>
    [Fact]
    public void AndABattlerCarriesItBothWays()
    {
        var factory = new BattleFactory(TestRules.All);

        SavedMon saved = new(1, 5, null, 11, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
        {
            AbilitySlot = 1,
        };

        Assert.Equal(1, factory.Restore(saved)!.AbilitySlot);
        Assert.Equal(1, BattleFactory.Save(factory.Restore(saved)!).AbilitySlot);
    }

    /// <summary>
    /// And leaving the slot out of a save is the same as being born into the first one,
    /// which is what every creature written before this column existed effectively was.
    /// </summary>
    [Fact]
    public void AndASaveFromBeforeThisReadsAsTheFirstSlot()
    {
        SavedMon old = new(1, 5, null, 11, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

        Assert.Equal(0, old.AbilitySlot);
    }

    // ---- the honest count --------------------------------------------------------------

    /// <summary>
    /// What is modelled is modelled, and what is not is carried and does nothing. This test
    /// exists so the number cannot drift without somebody noticing: an ability added to the
    /// list without a rule behind it, or a rule written and never listed, both show up here.
    /// </summary>
    [Fact]
    public void EveryModelledAbilityActuallyDoesSomething()
    {
        var inert = new List<int>();

        foreach (int ability in Abilities.Modelled)
        {
            bool refuses = Enum.GetValues<StatusCondition>().Any(c => Abilities.Refuses(ability, c))
                || Abilities.RefusesConfusion(ability);

            bool changesDamage =
                Enum.GetValues<PokemonType>().Any(t => Abilities.Against(ability, Move(t), 100) is not null)
                || Enum.GetValues<PokemonType>().Any(t => Abilities.Defending(ability, Move(t)) != 100);

            bool changesAttack = Enum.GetValues<PokemonType>().Any(t =>
            {
                var ill = new Battler(With(ability, type: t), 50) { AbilitySlot = 0 };
                ill.Status = StatusCondition.Burn;
                ill.TakeDamage(ill.MaxHp - 1);

                return Abilities.Attacking(ability, ill, Move(t), true) != 100;
            });

            // And the ones that read the sky, which is a fourth way of doing something and
            // was a fourth way this test could not see until weather existed.
            bool readsTheSky = Enum.GetValues<Weather>().Any(w =>
                Abilities.Speed(ability, w) != 100
                || Abilities.ShrugsOffWeather(ability, w)
                || Abilities.DrinksFrom(ability, w))
                || Abilities.Ignores(ability);

            // And the ones that do something the moment their owner arrives, which is a
            // fifth way and was a fifth blind spot.
            bool arrives = Abilities.Brings(ability) != Weather.None || Abilities.Cows(ability) != 0;

            // And the ones that answer being touched, which is a sixth way. Rolled many
            // times rather than once, because what is being asked is whether the ability
            // has a rule at all and the cheapest of these fires one time in ten.
            var dice = new BattleRng(19);

            bool answersATouch = Abilities.Grazes(ability)
                || Enumerable.Range(0, 500).Any(_ => Abilities.Touched(ability, dice) is not null);

            // And the ones that refuse to be made worse at something, which is a seventh
            // way. Seven limbs now, one for each kind of ability this project has learned
            // about — which is the whole point of the test rather than a sign it is
            // getting unwieldy.
            bool refusesToBeWorsened =
                Enum.GetValues<Stat>().Any(st => Abilities.Protects(ability, st))
                || Abilities.ShrugsOffRiders(ability);

            // And the ones about leaving — an eighth way, and the only kind where an
            // ability decides something about somebody else's options rather than about
            // what happens to its owner.
            bool aboutLeaving = Abilities.HoldsGround(ability)
                || Enum.GetValues<PokemonType>().Any(t => Abilities.Traps(ability, t, t, 0));

            // And the ones that refuse something the engine already does — a ninth way, and
            // the cheapest kind there is: not one of these needed anything built. The test
            // failed the moment they were added, which is the whole reason it exists: a
            // meta-guard that quietly accepted an ability it had no way of seeing would be
            // no guard at all.
            bool refusesWhatIsDone = Abilities.CannotBeEndedOutright(ability)
                || Abilities.NeverCritical(ability)
                || Abilities.PaysNoRecoil(ability)
                || Abilities.NeverFlinches(ability)
                || Abilities.StopsAnybodyBlowingUp(ability)
                || Abilities.KeepsWhatItHolds(ability);

            if (!refuses && !changesDamage && !changesAttack && !readsTheSky && !arrives
                && !answersATouch && !refusesToBeWorsened && !aboutLeaving
                && !refusesWhatIsDone)
            {
                inert.Add(ability);
            }
        }

        Assert.Empty(inert);
    }

    /// <summary>And nothing claims to be modelled twice.</summary>
    [Fact]
    public void AndNothingIsListedTwice()
    {
        Assert.Equal(Abilities.Modelled.Count, Abilities.Modelled.Distinct().Count());
    }
}
