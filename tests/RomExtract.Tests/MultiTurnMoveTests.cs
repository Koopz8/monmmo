using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The moves that take more than one turn, which took none.
/// <para>
/// HYPER BEAM was a hundred and fifty power with no price. FLY was a worse hit that
/// wasted a turn getting there. THRASH was one swing. WRAP was fifteen power and a
/// shrug. A fight with any of them in it played out exactly like a fight without.
/// </para>
/// </summary>
public class MultiTurnMoveTests
{
    private const int Recharger = 1;
    private const int Vanisher = 2;
    private const int Locked = 3;
    private const int Holder = 4;
    private const int Plain = 5;

    /// <summary>The effect bytes, as the cartridge numbers them.</summary>
    private const byte RechargeEffect = 0x50;
    private const byte TwoTurnEffect = 0x9B;
    private const byte LockedInEffect = 0x1B;
    private const byte TrapEffect = 0x2A;

    private static MoveData Move(int id, byte effect, byte power, byte accuracy = 100) =>
        new(id, "", effect, power, PokemonType.Normal, accuracy, 20, 0, 0, 0);

    /// <summary>
    /// Two big, slow creatures so that neither knocks the other out before a three-turn
    /// move has finished being three turns.
    /// </summary>
    private static Battler Make(int speed, params MoveData[] moves)
    {
        var species = new SpeciesData
        {
            Index = 1,
            BaseHp = 250,
            BaseAttack = 30,
            BaseDefense = 200,
            BaseSpeed = (byte)speed,
            BaseSpAttack = 30,
            BaseSpDefense = 200,
            Type1 = PokemonType.Normal,
            Type2 = PokemonType.Normal,
            GrowthRate = GrowthRate.MediumFast,
        };

        var battler = new Battler(species, level: 50, nature: Nature.Hardy);

        battler.Moves.AddRange(moves);

        return battler;
    }

    /// <summary>A battle where the player always goes first, so the order never surprises.</summary>
    private static Battle Fight(Battler you, Battler them, uint seed = 7) => new(you, them, seed);

    private static List<BattleEvent> Turn(Battle battle, int yourSlot, int theirSlot = 0) =>
        battle.ResolveTurn(new BattleAction.UseMove(yourSlot), new BattleAction.UseMove(theirSlot));

    // ---- what the table reads -------------------------------------------------------

    [Fact]
    public void TheFourGroupsAreReadAsWhatTheyAre()
    {
        Assert.Equal(EffectKind.Recharge, MoveEffects.Of(RechargeEffect).Kind);
        Assert.Equal(EffectKind.TwoTurn, MoveEffects.Of(TwoTurnEffect).Kind);
        Assert.Equal(EffectKind.LockedIn, MoveEffects.Of(LockedInEffect).Kind);
        Assert.Equal(EffectKind.Trap, MoveEffects.Of(TrapEffect).Kind);
    }

    /// <summary>
    /// DOUBLE-EDGE and VOLT TACKLE are the same idea as TAKE DOWN with a steeper price,
    /// and were sitting one line away from working the whole time.
    /// </summary>
    [Fact]
    public void TheOtherRecoilGroupIsRecoilToo()
    {
        Assert.Equal(EffectKind.Recoil, MoveEffects.Of(0xC6).Kind);
    }

    // ---- HYPER BEAM -----------------------------------------------------------------

    [Fact]
    public void ARechargingMoveCostsTheTurnAfterIt()
    {
        Battler you = Make(200, Move(Recharger, RechargeEffect, 150));
        Battler them = Make(1, Move(Plain, 0, 10));

        Battle battle = Fight(you, them);

        List<BattleEvent> first = Turn(battle, 0);

        Assert.Contains(first, e => e is BattleEvent.DamageDealt { Side: Side.Opponent });

        List<BattleEvent> second = Turn(battle, 0);

        Assert.Contains(second, e => e is BattleEvent.Recharging { Side: Side.Player, MoveId: Recharger });

        // The turn is gone: nothing the player did landed on the opponent.
        Assert.DoesNotContain(second, e => e is BattleEvent.DamageDealt { Side: Side.Opponent });

        // And the debt is paid once, not for ever.
        Assert.DoesNotContain(Turn(battle, 0), e => e is BattleEvent.Recharging);
    }

    [Fact]
    public void AMissedRechargingMoveCostsNothing()
    {
        Battler you = Make(200, Move(Recharger, RechargeEffect, 150, accuracy: 1));
        Battler them = Make(1, Move(Plain, 0, 10));

        Battle battle = Fight(you, them, seed: 3);

        List<BattleEvent> first = Turn(battle, 0);

        Assert.Contains(first, e => e is BattleEvent.MoveMissed);
        Assert.DoesNotContain(Turn(battle, 0), e => e is BattleEvent.Recharging);
    }

    // ---- FLY ------------------------------------------------------------------------

    [Fact]
    public void ATwoTurnMoveGoesAwayAndComesBack()
    {
        Battler you = Make(200, Move(Vanisher, TwoTurnEffect, 70));
        Battler them = Make(1, Move(Plain, 0, 10));

        Battle battle = Fight(you, them);

        List<BattleEvent> first = Turn(battle, 0);

        Assert.Contains(first, e => e is BattleEvent.WentAway { Side: Side.Player, MoveId: Vanisher });
        Assert.DoesNotContain(first, e => e is BattleEvent.DamageDealt { Side: Side.Opponent });

        // The second turn takes the move back whatever the player pressed — there is no
        // second move to press, and pressing it would not matter.
        List<BattleEvent> second = Turn(battle, 0);

        Assert.Contains(second, e => e is BattleEvent.DamageDealt { Side: Side.Opponent });
        Assert.DoesNotContain(second, e => e is BattleEvent.WentAway);
    }

    /// <summary>
    /// The half that makes it worth the turn. Checked against a move that cannot miss,
    /// because "it missed" and "it could not be reached" are different sentences and
    /// only one of them is true of SWIFT.
    /// </summary>
    [Fact]
    public void NothingReachesSomethingThatIsNotThere()
    {
        Battler you = Make(200, Move(Vanisher, TwoTurnEffect, 70));

        // Accuracy zero is how this cartridge writes "never misses".
        Battler them = Make(1, Move(Plain, 0x11, 60, accuracy: 0));

        Battle battle = Fight(you, them);

        List<BattleEvent> first = Turn(battle, 0);

        Assert.Contains(first, e => e is BattleEvent.WentAway);
        Assert.Contains(first, e => e is BattleEvent.MoveMissed { Side: Side.Opponent });
        Assert.DoesNotContain(first, e => e is BattleEvent.DamageDealt { Side: Side.Player });
    }

    // ---- THRASH ---------------------------------------------------------------------

    [Fact]
    public void ALockedInMoveRepeatsItselfAndEndsInConfusion()
    {
        Battler you = Make(200, Move(Locked, LockedInEffect, 20), Move(Plain, 0, 10));
        Battler them = Make(1, Move(Plain, 0, 5));

        Battle battle = Fight(you, them);

        Turn(battle, 0);

        // Slot one from here on, and slot one is never what happens.
        var used = new List<int>();

        for (int turn = 0; turn < 3; turn++)
        {
            used.AddRange(Turn(battle, 1)
                .OfType<BattleEvent.MoveUsed>()
                .Where(e => e.Side == Side.Player)
                .Select(e => e.MoveId));
        }

        Assert.Contains(Locked, used);

        // Two turns of it or three, and then the price.
        Assert.True(you.IsConfused, "the thrash ended without confusing anybody");
    }

    // ---- WRAP -----------------------------------------------------------------------

    [Fact]
    public void ATrappingMoveKeepsHurtingAfterTheTurnItLanded()
    {
        Battler you = Make(200, Move(Holder, TrapEffect, 15));
        Battler them = Make(1, Move(Plain, 0, 5));

        Battle battle = Fight(you, them);

        List<BattleEvent> first = Turn(battle, 0);

        Assert.Contains(first, e => e is BattleEvent.Trapped { Side: Side.Opponent, MoveId: Holder });
        Assert.Contains(first, e => e is BattleEvent.TrapHurt { Side: Side.Opponent });

        // And it goes on without the move being used again.
        List<BattleEvent> second = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(second, e => e is BattleEvent.TrapHurt { Side: Side.Opponent });
    }

    /// <summary>
    /// Caught by playing it: the four kinds are settled where the turn is taken, and
    /// falling through to the rider handler gave them its default — a stage change of
    /// nothing, to a stat that has no stages. WRAP landed and then announced "The wild
    /// PIDGEY's HP won't go any lower!"
    /// </summary>
    [Fact]
    public void NoneOfThemAlsoSaysSomethingAboutAStat()
    {
        Battler you = Make(200, Move(Holder, TrapEffect, 15));
        Battler them = Make(1, Move(Plain, 0, 5));

        Battle battle = Fight(you, them);

        Assert.DoesNotContain(Turn(battle, 0), e => e is BattleEvent.StageChanged);
    }

    [Fact]
    public void ATrapLetsGoEventually()
    {
        Battler you = Make(200, Move(Holder, TrapEffect, 1));
        Battler them = Make(1, Move(Plain, 0, 1));

        Battle battle = Fight(you, them);

        Turn(battle, 0);

        var freed = false;

        for (int turn = 0; turn < 8 && !freed; turn++)
            freed = Turn(battle, 0).Any(e => e is BattleEvent.BrokeFree { Side: Side.Opponent });

        Assert.True(freed, "nothing ever got free");
    }

    /// <summary>
    /// It does not stack. A second WRAP on somebody already wrapped would otherwise
    /// restart the count every turn and hold them until one of them fainted.
    /// </summary>
    [Fact]
    public void BeingTrappedTwiceIsBeingTrappedOnce()
    {
        Battler you = Make(200, Move(Holder, TrapEffect, 1));
        Battler them = Make(1, Move(Plain, 0, 1));

        Battle battle = Fight(you, them);

        Turn(battle, 0);

        int held = them.TrappedTurns;

        Assert.DoesNotContain(Turn(battle, 0), e => e is BattleEvent.Trapped);
        Assert.True(them.TrappedTurns < held, "the count went up again");
    }
}
