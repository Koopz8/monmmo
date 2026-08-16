using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Seven abilities that change a number, and seven that are finished rather than silent.
/// <para>
/// The second half is the interesting one. Seven abilities on this cartridge have nothing to
/// do in a fight of one against one: two act outside a battle entirely, two need a partner
/// this game mode does not have, one draws a move away from somebody who is not there, and
/// two do nothing anywhere at all — one of which no species even carries.
/// </para>
/// <para>
/// Counting those as unmodelled makes the number of things left to build wrong for ever,
/// because nothing will ever be built for them. It is the same distinction the move-effect
/// table draws between a move that is finished and a move nobody has written, and this
/// project got that wrong once already — twenty-three effect-0 moves spent a milestone
/// counted as silent when the answer was that they only hit.
/// </para>
/// </summary>
public class FinishedRatherThanSilentTests
{
    private static SpeciesData Species(int ability = 0, int speed = 60) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 250, BaseAttack = 90, BaseDefense = 90,
        BaseSpeed = (byte)speed, BaseSpAttack = 90, BaseSpDefense = 90,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Ability1 = (byte)ability, Ability2 = (byte)ability,
    };

    private static MoveData Move(
        byte effect, byte power = 0, byte accuracy = 100, byte chance = 100, int id = 1) =>
        new(id, string.Empty, effect, power, PokemonType.Normal, accuracy, 20, chance, 0, 0);

    private static Battler Make(SpeciesData species, params MoveData[] moves)
    {
        var battler = new Battler(species, 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Theory]
    [InlineData(Abilities.CompoundEyes)] [InlineData(Abilities.Hustle)]
    [InlineData(Abilities.MarvelScale)] [InlineData(Abilities.SereneGrace)]
    [InlineData(Abilities.LiquidOoze)] [InlineData(Abilities.EarlyBird)]
    [InlineData(Abilities.Truant)] [InlineData(Abilities.Stench)]
    [InlineData(Abilities.Illuminate)] [InlineData(Abilities.Pickup)]
    [InlineData(Abilities.Plus)] [InlineData(Abilities.Minus)]
    [InlineData(Abilities.LightningRod)] [InlineData(Abilities.Cacophony)]
    public void NoneOfThemIsSilent(int ability) => Assert.True(Abilities.DoesSomething(ability));

    // ---- finished rather than silent ---------------------------------------------------

    /// <summary>
    /// The seven that have nothing to do here say so, and they are the only ones that do.
    /// </summary>
    [Fact]
    public void TheSevenWithNothingToDoSaySo()
    {
        int[] finished =
        [
            Abilities.Stench, Abilities.Illuminate, Abilities.Pickup,
            Abilities.Plus, Abilities.Minus, Abilities.LightningRod, Abilities.Cacophony,
        ];

        Assert.All(finished, a => Assert.True(Abilities.NothingToDoHere(a)));

        // And nothing that does something claims to have nothing to do, which is the half
        // that would let this become a place to hide unfinished work.
        Assert.All(
            Abilities.Modelled.Where(a => !finished.Contains(a)),
            a => Assert.False(
                Abilities.NothingToDoHere(a),
                $"ability {a} is modelled and also claims to have nothing to do"));
    }

    /// <summary>And having nothing to do is not the same as not being accounted for.</summary>
    [Fact]
    public void AndHavingNothingToDoIsNotTheSameAsBeingUnaccountedFor()
    {
        Assert.True(Abilities.DoesSomething(Abilities.Stench));

        // While something genuinely unmodelled still says it is. 28 is SYNCHRONIZE, which
        // passes a condition back to whoever caused it and is not written yet — named by
        // number here precisely because it has no constant, which is what unmodelled means.
        Assert.False(Abilities.DoesSomething(28));
    }

    // ---- aiming -------------------------------------------------------------------------

    /// <summary>One is better at landing what it throws, and one is worse.</summary>
    [Fact]
    public void OneIsBetterAtLandingThingsAndOneIsWorse()
    {
        Assert.True(Abilities.Aiming(Abilities.CompoundEyes) > 100);
        Assert.True(Abilities.Aiming(Abilities.Hustle) < 100);
        Assert.Equal(100, Abilities.Aiming(Abilities.None));
    }

    /// <summary>And it reaches the roll rather than stopping at the table.</summary>
    [Fact]
    public void AndItReachesTheRoll()
    {
        MoveData shaky = Move(0x00, 40, accuracy: 50);

        Battler sharp = Make(Species(Abilities.CompoundEyes), shaky);
        Battler blunt = Make(Species(Abilities.Hustle), shaky);
        Battler them = Make(Species(), Move(0x00, 0));

        Assert.True(Landed(sharp) > Landed(blunt), "the two aims landed the same number of times");

        static int Landed(Battler attacker)
        {
            var rng = new BattleRng(7);
            var target = new Battler(Species(), 50);

            return Enumerable.Range(0, 400)
                .Count(_ => DamageCalculator.RollAccuracy(rng, attacker.MoveAt(0)!, attacker, target));
        }
    }

    // ---- guarding -----------------------------------------------------------------------

    /// <summary>
    /// One is tougher while it is suffering — the mirror of the one whose Attack rises for
    /// the same reason, and the second ability on this cartridge whose point is that being
    /// ill helps.
    /// </summary>
    [Fact]
    public void OneIsTougherWhileItIsSuffering()
    {
        Battler well = Make(Species(Abilities.MarvelScale), Move(0x00, 0));
        Battler ill = Make(Species(Abilities.MarvelScale), Move(0x00, 0));

        ill.Status = StatusCondition.Burn;

        Assert.Equal(100, Abilities.Guarding(Abilities.MarvelScale, well));
        Assert.True(Abilities.Guarding(Abilities.MarvelScale, ill) > 100);
    }

    /// <summary>And it reaches the damage, which is the part that could be right alone and wrong here.</summary>
    [Fact]
    public void AndBeingTougherReachesTheDamage()
    {
        Battler you = Make(Species(speed: 200), Move(0x00, 60));

        Battler well = Make(Species(Abilities.MarvelScale), Move(0x00, 0));
        Battler ill = Make(Species(Abilities.MarvelScale), Move(0x00, 0));

        ill.Status = StatusCondition.Burn;

        int onWell = DamageCalculator.Calculate(you, well, you.MoveAt(0)!, false, 100).Damage;
        int onIll = DamageCalculator.Calculate(you, ill, you.MoveAt(0)!, false, 100).Damage;

        Assert.True(onIll < onWell, $"{onIll} was not less than {onWell}");
    }

    // ---- sharper chances ------------------------------------------------------------------

    /// <summary>
    /// What rides on a move happens twice as often, and never more often than always — a
    /// chance past certainty is still certainty and would read as a bug the first time
    /// anybody printed it.
    /// </summary>
    [Fact]
    public void WhatRidesOnAMoveHappensTwiceAsOften()
    {
        Assert.True(Abilities.SharpensChances(Abilities.SereneGrace));
        Assert.False(Abilities.SharpensChances(Abilities.None));

        int Burns(int ability)
        {
            var burned = 0;

            for (uint seed = 1; seed < 60; seed++)
            {
                Battler you = Make(Species(ability, speed: 200), Move(0x04, 40, chance: 20));
                Battler them = Make(Species(), Move(0x00, 0));

                Turn(new Battle(you, them, seed));

                if (them.Status == StatusCondition.Burn) burned++;
            }

            return burned;
        }

        Assert.True(
            Burns(Abilities.SereneGrace) > Burns(Abilities.None),
            "the sharpened chance did not fire more often than the plain one");
    }

    // ---- draining what disagrees --------------------------------------------------------------

    /// <summary>
    /// Draining this one hurts instead. Asked of the creature being drained, which is what
    /// makes it a punishment rather than a defence.
    /// </summary>
    [Fact]
    public void DrainingItHurtsInstead()
    {
        Battler you = Make(Species(speed: 200), Move(0x03, 40));
        Battler them = Make(Species(Abilities.LiquidOoze), Move(0x00, 0));

        you.TakeDamage(you.MaxHp / 2);

        int before = you.CurrentHp;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.True(you.CurrentHp < before, "the drain healed, or did nothing");
        Assert.DoesNotContain(events, e => e is BattleEvent.Drained);
    }

    /// <summary>And draining anybody else still heals.</summary>
    [Fact]
    public void AndDrainingAnybodyElseStillHeals()
    {
        Battler you = Make(Species(speed: 200), Move(0x03, 40));
        Battler them = Make(Species(), Move(0x00, 0));

        you.TakeDamage(you.MaxHp / 2);

        int before = you.CurrentHp;

        Assert.Contains(Turn(new Battle(you, them, 7)), e => e is BattleEvent.Drained { Side: Side.Player });
        Assert.True(you.CurrentHp > before);
    }

    // ---- sleeping lightly -----------------------------------------------------------------------

    /// <summary>It is out for half as long, counted rather than refused.</summary>
    [Fact]
    public void ItSleepsHalfAsLong()
    {
        Assert.Equal(2, Abilities.WakesInTurns(Abilities.EarlyBird));
        Assert.Equal(1, Abilities.WakesInTurns(Abilities.None));

        int Turns(int ability)
        {
            Battler you = Make(Species(speed: 200), Move(0x00, 0));
            Battler them = Make(Species(ability), Move(0x00, 0));

            them.Status = StatusCondition.Sleep;
            them.SleepTurns = 6;

            var battle = new Battle(you, them, 7);

            var turns = 0;

            while (them.Status == StatusCondition.Sleep && turns < 12)
            {
                Turn(battle);
                turns++;
            }

            return turns;
        }

        Assert.True(Turns(Abilities.EarlyBird) < Turns(Abilities.None));
    }

    // ---- every other turn -------------------------------------------------------------------------

    /// <summary>
    /// It can only manage every other turn, and the turn it arrives is one it can act on —
    /// a creature that had to wait for its first go is one nobody could usefully switch in.
    /// </summary>
    [Fact]
    public void ItManagesEveryOtherTurn()
    {
        Battler you = Make(Species(speed: 200), Move(0x00, 0));
        Battler them = Make(Species(Abilities.Truant), Move(0x00, 30));

        var battle = new Battle(you, them, 7);

        var acted = new List<bool>();

        for (int turn = 0; turn < 4; turn++)
        {
            acted.Add(Turn(battle).OfType<BattleEvent.MoveUsed>().Any(e => e.Side == Side.Opponent));
        }

        Assert.Equal([true, false, true, false], acted);
    }

    /// <summary>And somebody without it manages every turn.</summary>
    [Fact]
    public void AndSomebodyWithoutItManagesEveryTurn()
    {
        Battler you = Make(Species(speed: 200), Move(0x00, 0));
        Battler them = Make(Species(), Move(0x00, 30));

        var battle = new Battle(you, them, 7);

        for (int turn = 0; turn < 4; turn++)
        {
            Assert.Contains(
                Turn(battle),
                e => e is BattleEvent.MoveUsed { Side: Side.Opponent });
        }
    }
}
