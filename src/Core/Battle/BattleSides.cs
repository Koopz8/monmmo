using System.Reflection;

namespace PokeMmo.Core.Battle;

/// <summary>
/// The same turn, read from the other chair.
/// <para>
/// Every battle in this game until now had one player in it, so "player" and "opponent"
/// were absolute: the engine said <see cref="Side.Player"/> and it meant you. A fight
/// between two people has no such side. One turn happens, and each of the two has to be
/// told about it in their own terms — what the engine calls the player is you to one
/// client and them to the other.
/// </para>
/// <para>
/// Rather than a second engine or forty hand-written mirrors, this turns an event round
/// by rebuilding it with its side flipped. It can do that because every event in this
/// game names exactly one side, always as its first argument — which is not a coincidence
/// worth relying on quietly, so <c>BattleSideTests</c> asserts it of every event kind
/// there is and fails on the first one that breaks the pattern.
/// </para>
/// </summary>
public static class BattleSides
{
    public static Side Other(this Side side) => side == Side.Player ? Side.Opponent : Side.Player;

    private static readonly Dictionary<Type, ConstructorInfo?> Builders = [];

    /// <summary>
    /// The constructor of an event and whether its first argument is a side.
    /// <para>
    /// Cached, because a battle produces a dozen events a turn and reflection is slow
    /// enough to be worth not doing twice for the same shape.
    /// </para>
    /// </summary>
    public static ConstructorInfo? BuilderFor(Type kind)
    {
        lock (Builders)
        {
            if (Builders.TryGetValue(kind, out ConstructorInfo? cached)) return cached;

            ConstructorInfo? found = kind
                .GetConstructors()
                .FirstOrDefault(c => c.GetParameters() is [{ } first, ..]
                    && (first.ParameterType == typeof(Side) || first.ParameterType == typeof(Side?)));

            return Builders[kind] = found;
        }
    }

    /// <summary>
    /// One event as the other side saw it.
    /// <para>
    /// An event naming nobody — there is one, the end of the battle when nobody won — is
    /// returned as it stands, which is right: a draw looks the same from both chairs.
    /// </para>
    /// </summary>
    public static BattleEvent Swap(BattleEvent what)
    {
        if (BuilderFor(what.GetType()) is not { } builder) return what;

        ParameterInfo[] parameters = builder.GetParameters();
        var arguments = new object?[parameters.Length];

        for (int i = 1; i < parameters.Length; i++)
            arguments[i] = what.GetType().GetProperty(parameters[i].Name!)?.GetValue(what);

        object? side = what.GetType().GetProperty(parameters[0].Name!)?.GetValue(what);

        arguments[0] = side is Side named ? named.Other() : null;

        return (BattleEvent)builder.Invoke(arguments);
    }

    public static List<BattleEvent> Swap(IEnumerable<BattleEvent> events) => [.. events.Select(Swap)];
}
