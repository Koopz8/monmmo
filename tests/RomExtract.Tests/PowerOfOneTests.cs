using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The moves whose record says one power.
/// <para>
/// One is not a power. Twenty-two moves in seventeen groups carry it, no group mixes it
/// with a real one, and every one of them is a move whose damage is written somewhere
/// other than in its own record. Four of those somewheres are inside the fight.
/// </para>
/// </summary>
public class PowerOfOneTests
{
    private const int Ender = 1;
    private const int Leveller = 2;
    private const int Halver = 3;
    private const int Equaliser = 4;

    private const byte KnockoutEffect = 0x26;
    private const byte LevelEffect = 0x57;
    private const byte HalfEffect = 0x28;
    private const byte EndeavourEffect = 0xBD;

    private static MoveData Move(int id, byte effect, PokemonType type = PokemonType.Normal) =>
        new(id, "", effect, 1, type, 100, 20, 0, 0, 0);

    private static Battler Make(int level, PokemonType type, params MoveData[] moves)
    {
        var species = new SpeciesData
        {
            Index = 1,
            BaseHp = 120,
            BaseAttack = 50,
            BaseDefense = 50,
            BaseSpeed = 50,
            BaseSpAttack = 50,
            BaseSpDefense = 50,
            Type1 = type,
            Type2 = type,
            GrowthRate = GrowthRate.MediumFast,
        };

        var battler = new Battler(species, level, Nature.Hardy);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Fact]
    public void TheFourAnswerableGroupsAreRead()
    {
        Assert.Equal(EffectKind.Knockout, MoveEffects.Of(KnockoutEffect).Kind);
        Assert.Equal(EffectKind.LevelDamage, MoveEffects.Of(LevelEffect).Kind);
        Assert.Equal(EffectKind.HalfTheirHealth, MoveEffects.Of(HalfEffect).Kind);
        Assert.Equal(EffectKind.DownToMine, MoveEffects.Of(EndeavourEffect).Kind);
    }

    /// <summary>
    /// And the ones whose number is in the game's code stay silent, which is the point
    /// rather than an omission. Writing forty here for DRAGON RAGE out of memory of
    /// another game is the mistake this project keeps a standing rule against.
    /// </summary>
    [Theory]
    [InlineData((byte)0x29)]  // DRAGON RAGE — always forty, and forty is nowhere in the data
    [InlineData((byte)0x82)]  // SONICBOOM — always twenty, likewise
    [InlineData((byte)0x58)]  // PSYWAVE — a roll around the level
    [InlineData((byte)0xC4)]  // LOW KICK — the target's weight, which is not in this table
    public void TheOnesWhoseNumberIsInTheCodeStaySilent(byte effect)
    {
        Assert.Equal(EffectKind.None, MoveEffects.Of(effect).Kind);
    }

    // ---- GUILLOTINE -----------------------------------------------------------------

    [Fact]
    public void AOneHitKnockoutEndsItWhateverWasLeft()
    {
        Battler you = Make(50, PokemonType.Normal, Move(Ender, KnockoutEffect));
        Battler them = Make(50, PokemonType.Normal, Move(9, 0));

        Battle battle = new(you, them, 7);

        List<BattleEvent> events = Turn(battle);

        Assert.Contains(events, e => e is BattleEvent.OneHitKnockout { Side: Side.Opponent });
        Assert.Contains(events, e => e is BattleEvent.Fainted { Side: Side.Opponent });
        Assert.Equal(0, them.CurrentHp);
    }

    /// <summary>
    /// Modelled, and worth a test precisely because it is: without it a level-two
    /// DIGLETT ends a level-seventy MEWTWO three times in ten.
    /// </summary>
    [Fact]
    public void ItCannotReachSomethingAboveIt()
    {
        Battler you = Make(5, PokemonType.Normal, Move(Ender, KnockoutEffect));
        Battler them = Make(50, PokemonType.Normal, Move(9, 0));

        Battle battle = new(you, them, 7);

        List<BattleEvent> events = Turn(battle);

        Assert.Contains(events, e => e is BattleEvent.Unaffected { Side: Side.Opponent });
        Assert.True(them.CurrentHp > 0);
    }

    /// <summary>
    /// The type chart still decides whether it can be touched at all, even though it
    /// decides nothing about the number. A normal move does not reach a ghost.
    /// </summary>
    [Fact]
    public void ItStillCannotTouchWhatItsTypeCannotTouch()
    {
        Battler you = Make(50, PokemonType.Normal, Move(Ender, KnockoutEffect));
        Battler them = Make(20, PokemonType.Ghost, Move(9, 0));

        Battle battle = new(you, them, 7);

        List<BattleEvent> events = Turn(battle);

        Assert.Contains(events, e => e is BattleEvent.NoEffect);
        Assert.True(them.CurrentHp > 0);
    }

    // ---- SEISMIC TOSS ---------------------------------------------------------------

    [Fact]
    public void LevelDamageTakesExactlyTheUsersLevel()
    {
        Battler you = Make(37, PokemonType.Normal, Move(Leveller, LevelEffect));
        Battler them = Make(50, PokemonType.Normal, Move(9, 0));

        int before = them.CurrentHp;

        Turn(new Battle(you, them, 7));

        Assert.Equal(before - 37, them.CurrentHp);
    }

    /// <summary>
    /// And not a random amount around it. The whole point of these is that no roll
    /// happens, so the same fight twice does the same thing.
    /// </summary>
    [Fact]
    public void ItIsTheSameNumberEveryTime()
    {
        var dealt = new HashSet<int>();

        for (uint seed = 1; seed <= 6; seed++)
        {
            Battler you = Make(37, PokemonType.Normal, Move(Leveller, LevelEffect));
            Battler them = Make(50, PokemonType.Normal, Move(9, 0));

            int before = them.CurrentHp;

            Turn(new Battle(you, them, seed));

            dealt.Add(before - them.CurrentHp);
        }

        Assert.Equal([37], dealt);
    }

    // ---- SUPER FANG -----------------------------------------------------------------

    [Fact]
    public void HalfTheirHealthIsHalfOfWhatIsLeft()
    {
        Battler you = Make(20, PokemonType.Normal, Move(Halver, HalfEffect));
        Battler them = Make(50, PokemonType.Normal, Move(9, 0));

        int before = them.CurrentHp;

        Turn(new Battle(you, them, 7));

        Assert.Equal(before - before / 2, them.CurrentHp);
    }

    /// <summary>
    /// One health left is half of nothing. It connects and takes nothing, which is not
    /// missing and not having no effect — it is a third thing and says so.
    /// </summary>
    [Fact]
    public void HalfOfOneIsNothingAndSaysSo()
    {
        Battler you = Make(20, PokemonType.Normal, Move(Halver, HalfEffect));
        Battler them = Make(50, PokemonType.Normal, Move(9, 0));

        them.TakeDamage(them.CurrentHp - 1);

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.Unaffected);
        Assert.DoesNotContain(events, e => e is BattleEvent.MoveMissed);
        Assert.Equal(1, them.CurrentHp);
    }

    // ---- ENDEAVOR -------------------------------------------------------------------

    [Fact]
    public void EndeavourBringsThemDownToWhereTheUserIs()
    {
        Battler you = Make(20, PokemonType.Normal, Move(Equaliser, EndeavourEffect));
        Battler them = Make(50, PokemonType.Normal, Move(9, 0));

        you.TakeDamage(you.CurrentHp - 7);

        Turn(new Battle(you, them, 7));

        // Equal rather than seven: the two are the same speed, so the opponent may have
        // taken its own turn first and moved the number this one is measured against.
        Assert.Equal(you.CurrentHp, them.CurrentHp);
    }

    [Fact]
    public void EndeavourDoesNothingToSomethingAlreadyLower()
    {
        Battler you = Make(20, PokemonType.Normal, Move(Equaliser, EndeavourEffect));
        Battler them = Make(50, PokemonType.Normal, Move(9, 0));

        them.TakeDamage(them.CurrentHp - 3);

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.Unaffected);
        Assert.Equal(3, them.CurrentHp);
    }

    /// <summary>
    /// None of the four is a rider on a hit, so none of them may fall through to the
    /// handler that applies riders — which is where the last four landed, and how WRAP
    /// came to announce something about a stat.
    /// </summary>
    [Fact]
    public void NoneOfThemAlsoSaysSomethingAboutAStat()
    {
        foreach (byte effect in new byte[] { KnockoutEffect, LevelEffect, HalfEffect, EndeavourEffect })
        {
            Battler you = Make(20, PokemonType.Normal, Move(1, effect));
            Battler them = Make(50, PokemonType.Normal, Move(9, 0));

            Assert.DoesNotContain(Turn(new Battle(you, them, 7)), e => e is BattleEvent.StageChanged);
        }
    }
}
