using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Joining without an account, for the tests that are about the world rather than
/// about who is logged in.
/// <para>
/// An extension rather than an overload on <see cref="GameWorld"/> itself: the server
/// always knows which account a player belongs to, and a convenience that lets it
/// forget is a convenience that will eventually be used in earnest.
/// </para>
/// </summary>
internal static class TestJoin
{
    private static long _nextAccountId;

    public static (ServerPlayer Player, List<Outgoing> Send) Join(this GameWorld world, string name) =>
        world.Join(Interlocked.Increment(ref _nextAccountId), name, TestRules.Equipped(world));
}
