using System.Reflection;
using System.Text.Json;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a battle event is allowed to contain, and whether it survives the wire.
/// <para>
/// Both matter for the same reason: these are about to be produced by a server that
/// has no cartridge. It cannot put a name in one, and it has to be able to send one.
/// </para>
/// </summary>
public class BattleEventShapeTests
{
    private static IEnumerable<Type> EventTypes =>
        typeof(BattleEvent).GetNestedTypes(BindingFlags.Public)
            .Where(t => t.IsSubclassOf(typeof(BattleEvent)));

    [Fact]
    public void NoEventCarriesText()
    {
        // A structural guard rather than a behavioural one. Adding a name back to an
        // event is an easy, natural-looking change that would quietly put cartridge
        // text on a server, and nothing else in the suite would notice.
        var offenders = new List<string>();

        foreach (Type type in EventTypes)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.PropertyType == typeof(string))
                    offenders.Add($"{type.Name}.{property.Name}");
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryEventTypeIsOnTheWireContract()
    {
        // A new event without a discriminator serialises as its base type and arrives
        // as nothing at all, which is the sort of failure that shows up as a silent
        // missing message rather than an exception.
        var declared = typeof(BattleEvent)
            .GetCustomAttributes<System.Text.Json.Serialization.JsonDerivedTypeAttribute>()
            .Select(a => a.DerivedType)
            .ToHashSet();

        Assert.Equal(EventTypes.OrderBy(t => t.Name), EventTypes.Where(declared.Contains).OrderBy(t => t.Name));
    }

    [Fact]
    public void EveryActionTypeIsOnTheWireContract()
    {
        var actionTypes = typeof(BattleAction).GetNestedTypes(BindingFlags.Public)
            .Where(t => t.IsSubclassOf(typeof(BattleAction)))
            .ToList();

        var declared = typeof(BattleAction)
            .GetCustomAttributes<System.Text.Json.Serialization.JsonDerivedTypeAttribute>()
            .Select(a => a.DerivedType)
            .ToHashSet();

        Assert.All(actionTypes, t => Assert.Contains(t, declared));
    }

    [Fact]
    public void EveryEventTypeHasSomethingToSay()
    {
        // The narrator ends in a wildcard that returns an empty string, so an event with
        // no line of its own does not throw and does not fail anything — it arrives at
        // the client and nothing appears. That is the shape of failure this whole
        // milestone was about, and it is worth one check that fires on the absence.
        //
        // Built by reflection rather than by a hand-written list, because a hand-written
        // list is a thing somebody has to remember to add to, and the event they forget
        // is exactly the one that goes quiet.
        var names = new BattleNames("BULBASAUR", "the wild PIDGEY", id => $"move {id}");
        var silent = new List<string>();

        foreach (Type type in EventTypes)
        {
            ConstructorInfo constructor = type.GetConstructors().Single();

            object?[] arguments = [.. constructor.GetParameters().Select(p => Sample(p.ParameterType))];

            var made = (BattleEvent)constructor.Invoke(arguments);

            if (BattleNarrator.Describe(made, names).Length == 0) silent.Add(type.Name);
        }

        Assert.Empty(silent);
    }

    /// <summary>A value of the right type, chosen only so the event can be built.</summary>
    private static object? Sample(Type type)
    {
        Type bare = Nullable.GetUnderlyingType(type) ?? type;

        if (bare == typeof(Side)) return Side.Player;
        if (bare == typeof(Stat)) return Stat.Attack;
        if (bare == typeof(StatusCondition)) return StatusCondition.Poison;
        if (bare == typeof(Ailments)) return Ailments.Poison;
        if (bare == typeof(DamageResult)) return new DamageResult(12, true, 200, true);
        if (bare == typeof(int)) return 1;
        if (bare == typeof(bool)) return true;

        throw new InvalidOperationException(
            $"an event now carries a {bare.Name}, which this test has no value for");
    }

    [Theory]
    [MemberData(nameof(SampleEvents))]
    public void AnEventSurvivesTheWire(BattleEvent original)
    {
        string json = JsonSerializer.Serialize(original);
        BattleEvent? back = JsonSerializer.Deserialize<BattleEvent>(json);

        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> SampleEvents() =>
    [
        [new BattleEvent.MoveUsed(Side.Player, 33)],
        [new BattleEvent.MoveMissed(Side.Opponent, 98)],
        [new BattleEvent.NoEffect(Side.Opponent)],
        [new BattleEvent.Immobilised(Side.Player, StatusCondition.Sleep)],
        [new BattleEvent.WokeUp(Side.Player)],
        [new BattleEvent.DamageDealt(Side.Opponent, 12, 5, new DamageResult(12, true, 200, true))],
        [new BattleEvent.StatusHurt(Side.Player, StatusCondition.Burn, 3, 9)],
        [new BattleEvent.Fainted(Side.Opponent)],
        [new BattleEvent.BallThrown(Side.Opponent, 3, false)],
        [new BattleEvent.Ended(Side.Player)],
    ];

    [Theory]
    [MemberData(nameof(SampleActions))]
    public void AnActionSurvivesTheWire(BattleAction original)
    {
        string json = JsonSerializer.Serialize(original);

        Assert.Equal(original, JsonSerializer.Deserialize<BattleAction>(json));
    }

    public static IEnumerable<object[]> SampleActions() =>
    [
        [new BattleAction.UseMove(2)],
        [new BattleAction.Struggle()],
        [new BattleAction.ThrowBall(TestRules.BallItem) { Kind = BallKind.Ultra }],
    ];
}

public class NarrationWithoutACartridgeTests
{
    [Fact]
    public void EventsAloneCannotBeNarratedAndThatIsThePoint()
    {
        // The server produces this. Without names it reads as placeholders, which is
        // exactly right: the words are the client's job, using the player's own image.
        string line = BattleNarrator.Describe(new BattleEvent.MoveUsed(Side.Player, 33), BattleNames.Unknown);

        Assert.Equal("Your side used move 33!", line);
    }

    [Fact]
    public void TheSameEventsReadProperlyOnceNamesAreSupplied()
    {
        var names = new BattleNames("BULBASAUR", "the wild PIDGEY", id => id == 33 ? "TACKLE" : "?");

        var events = new List<BattleEvent>
        {
            new BattleEvent.MoveUsed(Side.Player, 33),
            new BattleEvent.DamageDealt(Side.Opponent, 7, 12, new DamageResult(7, false, 100, false)),
            new BattleEvent.Fainted(Side.Opponent),
        };

        List<string> lines = BattleNarrator.Describe(events, names).ToList();

        Assert.Equal("BULBASAUR used TACKLE!", lines[0]);
        // Capitalised, and that is the point of doing it in one place: half these lines
        // begin with a name, and the name a wild creature goes by begins with "the".
        Assert.Equal("The wild PIDGEY took 7 damage.", lines[1]);
        Assert.Equal("The wild PIDGEY fainted!", lines[2]);
    }
}
