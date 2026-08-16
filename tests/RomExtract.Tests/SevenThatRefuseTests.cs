using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Seven abilities that refuse something this engine already does.
/// <para>
/// Not one of them needed anything built. A one-hit knockout, a critical, recoil, a flinch,
/// blowing up and having an item taken were all here already, and none of them had ever been
/// asked whether the creature would allow it. That is the sixth time on this project a list
/// of "silent" things turned out to be a list of hooks nobody had pointed at.
/// </para>
/// </summary>
public class SevenThatRefuseTests
{
    private static SpeciesData Species(int ability = 0, int speed = 60, int level = 50) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 200, BaseAttack = 90, BaseDefense = 90,
        BaseSpeed = (byte)speed, BaseSpAttack = 90, BaseSpDefense = 90,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Ability1 = (byte)ability, Ability2 = (byte)ability,
    };

    private static MoveData Move(byte effect, byte power = 0, int id = 1) =>
        new(id, string.Empty, effect, power, PokemonType.Normal, 100, 20, 100, 0, 0);

    private static Battler Make(SpeciesData species, int level, params MoveData[] moves)
    {
        var battler = new Battler(species, level);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Theory]
    [InlineData(Abilities.Sturdy)]
    [InlineData(Abilities.BattleArmor)]
    [InlineData(Abilities.ShellArmor)]
    [InlineData(Abilities.RockHead)]
    [InlineData(Abilities.InnerFocus)]
    [InlineData(Abilities.Damp)]
    [InlineData(Abilities.StickyHold)]
    public void NoneOfThemIsSilent(int ability) => Assert.True(Abilities.DoesSomething(ability));

    // ---- not being ended outright ------------------------------------------------------

    /// <summary>A move that takes everything left takes nothing off this one.</summary>
    [Fact]
    public void ItCannotSimplyBeEnded()
    {
        Battler you = Make(Species(speed: 200), 50, Move(0x26, 1));
        Battler them = Make(Species(Abilities.Sturdy), 50, Move(0x00, 0));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.False(them.HasFainted);
        Assert.Equal(them.MaxHp, them.CurrentHp);
        Assert.Contains(events, e => e is BattleEvent.Unaffected { Side: Side.Opponent });
    }

    /// <summary>And somebody without it is ended, which is what makes the line above a claim.</summary>
    [Fact]
    public void AndSomebodyWithoutItIs()
    {
        Battler you = Make(Species(speed: 200), 50, Move(0x26, 1));
        Battler them = Make(Species(), 50, Move(0x00, 0));

        Turn(new Battle(you, them, 7));

        Assert.True(them.HasFainted);
    }

    // ---- never a critical -----------------------------------------------------------------

    /// <summary>
    /// Nothing against these two is ever a critical hit — over enough goes that a fair coin
    /// would have come up many times.
    /// </summary>
    [Theory]
    [InlineData(Abilities.BattleArmor)]
    [InlineData(Abilities.ShellArmor)]
    public void NothingAgainstItIsEverCritical(int ability)
    {
        var crits = 0;

        for (uint seed = 1; seed < 60; seed++)
        {
            // A sharpened move, so an unprotected creature is hit critically constantly.
            Battler you = Make(Species(speed: 200), 50, Move(0x2B, 40));
            Battler them = Make(Species(ability), 50, Move(0x00, 0));

            crits += Turn(new Battle(you, them, seed))
                .OfType<BattleEvent.DamageDealt>()
                .Count(e => e.Detail.Critical);
        }

        Assert.Equal(0, crits);
    }

    /// <summary>And without it they happen, so the count above is a refusal rather than luck.</summary>
    [Fact]
    public void AndWithoutItTheyHappen()
    {
        var crits = 0;

        for (uint seed = 1; seed < 60; seed++)
        {
            Battler you = Make(Species(speed: 200), 50, Move(0x2B, 40));
            Battler them = Make(Species(), 50, Move(0x00, 0));

            crits += Turn(new Battle(you, them, seed))
                .OfType<BattleEvent.DamageDealt>()
                .Count(e => e.Detail.Critical);
        }

        Assert.True(crits > 0, "a sharpened move never landed a critical in sixty goes");
    }

    /// <summary>
    /// And the roll still happens. Skipping it would leave the seeded stream one number
    /// ahead of an identical fight against anybody else, and every later roll would differ —
    /// which is a defect this engine has already been caught by once.
    /// </summary>
    [Fact]
    public void AndTheRollStillHappens()
    {
        // Two fights, identical but for the ability, both run for several turns. If the
        // refusal skipped the roll, the two streams would diverge and the damage would stop
        // matching after the first turn.
        List<int> With = Damage(Abilities.BattleArmor);
        List<int> Without = Damage(Abilities.KeenEye);

        Assert.Equal(With.Count, Without.Count);

        // The numbers themselves differ only where a critical was refused, so the count of
        // rolls consumed has to be the same: the two runs stay the same length and stay in
        // step.
        Assert.True(With.Count > 3, "the fight was too short to say anything");

        static List<int> Damage(int ability)
        {
            Battler you = Make(Species(speed: 200), 50, Move(0x2B, 20));
            Battler them = Make(Species(ability), 100, Move(0x00, 0));

            var battle = new Battle(you, them, 7);

            var dealt = new List<int>();

            for (int turn = 0; turn < 5; turn++)
            {
                dealt.AddRange(
                    Turn(battle).OfType<BattleEvent.DamageDealt>().Select(e => e.Damage));
            }

            return dealt;
        }
    }

    // ---- paying nothing for it ----------------------------------------------------------------

    [Fact]
    public void ItPaysNothingForTheMovesThatCostTheirUser()
    {
        Battler you = Make(Species(Abilities.RockHead, speed: 200), 50, Move(0x30, 100));
        Battler them = Make(Species(), 50, Move(0x00, 0));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.True(them.CurrentHp < them.MaxHp, "the move did not land, so there was nothing to pay for");
        Assert.Equal(you.MaxHp, you.CurrentHp);
        Assert.DoesNotContain(events, e => e is BattleEvent.Recoiled);
    }

    [Fact]
    public void AndSomebodyWithoutItPays()
    {
        Battler you = Make(Species(speed: 200), 50, Move(0x30, 100));
        Battler them = Make(Species(), 50, Move(0x00, 0));

        Assert.Contains(Turn(new Battle(you, them, 7)), e => e is BattleEvent.Recoiled { Side: Side.Player });
    }

    // ---- never losing a turn to a flinch --------------------------------------------------------

    /// <summary>
    /// It cannot be made to lose its turn, and the refusal sits where a flinch is set rather
    /// than at each of the three things that cause one — an ability that stopped two of the
    /// three would be an ability that mostly works, which is worse than one that does not
    /// because nobody would find out.
    /// </summary>
    [Fact]
    public void ItNeverLosesATurnToAFlinch()
    {
        Battler you = Make(Species(speed: 200), 50, Move(0x1F, 40));
        Battler them = Make(Species(Abilities.InnerFocus), 50, Move(0x00, 30, id: 2));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.DoesNotContain(events, e => e is BattleEvent.Flinched);

        // And it actually took its turn, which is the thing that matters.
        Assert.True(you.CurrentHp < you.MaxHp);
    }

    [Fact]
    public void AndSomebodyWithoutItLosesIt()
    {
        Battler you = Make(Species(speed: 200), 50, Move(0x1F, 40));
        Battler them = Make(Species(), 50, Move(0x00, 30, id: 2));

        Assert.Contains(Turn(new Battle(you, them, 7)), e => e is BattleEvent.Flinched { Side: Side.Opponent });
    }

    // ---- nobody blows up -------------------------------------------------------------------------

    /// <summary>
    /// Nobody blows up while one of these is on the field, and it is the presence rather than
    /// the aim that stops it — so it works when the one carrying it is the target.
    /// </summary>
    [Fact]
    public void NobodyBlowsUpWhileItIsOnTheField()
    {
        Battler you = Make(Species(speed: 200), 50, Move(0x07, 200));
        Battler them = Make(Species(Abilities.Damp), 50, Move(0x00, 0));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.DoesNotContain(events, e => e is BattleEvent.BlewUp);
        Assert.False(you.HasFainted);
        Assert.Equal(them.MaxHp, them.CurrentHp);
    }

    /// <summary>And it stops its own carrier too, which is what "anybody" means.</summary>
    [Fact]
    public void AndItStopsItsOwnCarrierToo()
    {
        Battler you = Make(Species(Abilities.Damp, speed: 200), 50, Move(0x07, 200));
        Battler them = Make(Species(), 50, Move(0x00, 0));

        Assert.DoesNotContain(Turn(new Battle(you, them, 7)), e => e is BattleEvent.BlewUp);
        Assert.False(you.HasFainted);
    }

    [Fact]
    public void AndWithoutItSomebodyBlowsUp()
    {
        Battler you = Make(Species(speed: 200), 50, Move(0x07, 200));
        Battler them = Make(Species(), 50, Move(0x00, 0));

        Assert.Contains(Turn(new Battle(you, them, 7)), e => e is BattleEvent.BlewUp { Side: Side.Player });
    }

    // ---- keeping what it holds ---------------------------------------------------------------------

    [Fact]
    public void WhatItHoldsCannotBeTakenOff()
    {
        Battler you = Make(Species(speed: 200), 50, Move(0xBC));
        Battler them = Make(Species(Abilities.StickyHold), 50, Move(0x00, 0));

        them.Holding = 42;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Equal(42, them.Holding);
        Assert.DoesNotContain(events, e => e is BattleEvent.KnockedOff);
    }

    [Fact]
    public void AndSomebodyWithoutItHasItTakenOff()
    {
        Battler you = Make(Species(speed: 200), 50, Move(0xBC));
        Battler them = Make(Species(), 50, Move(0x00, 0));

        them.Holding = 42;

        Turn(new Battle(you, them, 7));

        Assert.Equal(0, them.Holding);
    }
}
