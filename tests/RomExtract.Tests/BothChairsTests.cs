using System.Reflection;
using PokeMmo.Core.Battle;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The same turn, read from the other chair.
/// <para>
/// Every battle in this project until now had one player in it, so <see cref="Side.Player"/>
/// meant you and nothing had to think about it. A fight between two people has no such
/// side: one turn happens and both have to be told about it in their own terms.
/// </para>
/// <para>
/// <see cref="BattleSides.Swap(BattleEvent)"/> does that by rebuilding an event with its
/// side flipped, which works because every event in this game names exactly one side, as
/// its first argument. That is a pattern rather than a rule the compiler knows, so it is
/// asserted here of every event kind there is — and the day somebody adds an event with two
/// sides in it, or with the side somewhere else, this is what says so rather than a duel
/// that quietly tells one of the two players the wrong story.
/// </para>
/// </summary>
public class BothChairsTests
{
    private static IEnumerable<Type> EveryKind =>
        typeof(BattleEvent).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(BattleEvent)) && !t.IsAbstract);

    [Fact]
    public void EveryEventNamesAtMostOneSide()
    {
        var wrong = new List<string>();

        foreach (Type kind in EveryKind)
        {
            ConstructorInfo constructor = kind.GetConstructors().First();

            int sides = constructor.GetParameters()
                .Count(p => p.ParameterType == typeof(Side) || p.ParameterType == typeof(Side?));

            if (sides > 1) wrong.Add($"{kind.Name} names {sides} sides");
        }

        Assert.Empty(wrong);
    }

    /// <summary>
    /// And names it first, which is what makes the rebuild possible without forty
    /// hand-written mirrors.
    /// </summary>
    [Fact]
    public void AndNamesItFirst()
    {
        var wrong = new List<string>();

        foreach (Type kind in EveryKind)
        {
            ConstructorInfo constructor = kind.GetConstructors().First();

            ParameterInfo[] parameters = constructor.GetParameters();

            bool anywhere = parameters.Any(p =>
                p.ParameterType == typeof(Side) || p.ParameterType == typeof(Side?));

            if (!anywhere) continue;

            if (BattleSides.BuilderFor(kind) is null) wrong.Add($"{kind.Name} names a side, but not first");
        }

        Assert.Empty(wrong);
    }

    /// <summary>Turned round and back again is the event you started with.</summary>
    [Theory]
    [InlineData(Side.Player)]
    [InlineData(Side.Opponent)]
    public void SwappingTwiceChangesNothing(Side side)
    {
        BattleEvent[] samples =
        [
            new BattleEvent.MoveUsed(side, 33),
            new BattleEvent.DamageDealt(side, 12, 7, new DamageResult(12, false, 100, true)),
            new BattleEvent.StatusInflicted(side, StatusCondition.Burn),
            new BattleEvent.StageChanged(side, Stat.Attack, -1, true),
            new BattleEvent.BallThrown(side, 3, true),
            new BattleEvent.Fainted(side),
            new BattleEvent.Ended(side),
        ];

        foreach (BattleEvent sample in samples)
        {
            Assert.NotEqual(sample, BattleSides.Swap(sample));
            Assert.Equal(sample, BattleSides.Swap(BattleSides.Swap(sample)));
        }
    }

    /// <summary>
    /// Nobody's turn round is still nobody's. A draw looks the same from both chairs, and
    /// the end of a battle nobody won is the one event in this game that names no side.
    /// </summary>
    [Fact]
    public void AnEventNamingNobodyIsUnchanged()
    {
        var over = new BattleEvent.Ended(null);

        Assert.Equal(over, BattleSides.Swap(over));
    }

    /// <summary>Everything else about the event survives the trip.</summary>
    [Fact]
    public void AndEverythingElseSurvivesIt()
    {
        var hurt = new BattleEvent.StatusHurt(Side.Player, StatusCondition.Poison, 9, 21);

        var swapped = (BattleEvent.StatusHurt)BattleSides.Swap(hurt);

        Assert.Equal(Side.Opponent, swapped.Side);
        Assert.Equal(hurt.Status, swapped.Status);
        Assert.Equal(hurt.Damage, swapped.Damage);
        Assert.Equal(hurt.RemainingHp, swapped.RemainingHp);
    }
}
