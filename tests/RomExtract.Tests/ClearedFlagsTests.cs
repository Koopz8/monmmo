using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A flag a script cleared, all the way down to the disk.
/// <para>
/// Played through the ROCKET HIDEOUT and it worked: the Rocket was beaten, talking to
/// him again ran the script the fight leads to, the server logged "1 cleared", the LIFT
/// KEY appeared on the floor and went into the bag. Signing out and back in put the
/// Rocket back and the flag with him.
/// </para>
/// <para>
/// Three things had to be true for that to be right and only two of them were checked,
/// so this checks all three: the server applies what the client reports, a snapshot
/// carries the absence of a flag rather than only the presence of one, and the store
/// writes the absence down. A flag is the only thing in this save that can go backwards,
/// and every layer has to agree about that or a door unlocks itself overnight.
/// </para>
/// </summary>
public class ClearedFlagsTests
{
    private const string Town = "1.0";

    /// <summary>The one hiding the LIFT KEY, which is where this came from.</summary>
    private const int Hidden = 0x0036;

    private static GameWorld World()
    {
        MapData map = new(Town, "PALLET TOWN", 8, 8, new byte[64]);

        return new GameWorld(new WorldData([map]) { FlagsAtStart = [Hidden] }, Town, TestRules.All);
    }

    [Fact]
    public void TheServerAppliesAFlagTheClientCleared()
    {
        GameWorld world = World();

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        Assert.True(player.Script.Has(Hidden));

        world.RunScript(player.Id, new ScriptRan([], [Hidden], []));

        Assert.False(player.Script.Has(Hidden));
    }

    /// <summary>
    /// And a snapshot carries the absence. A save is a list of what is set, so a flag
    /// that has gone is a flag that is simply not in it — which only works if the list
    /// is rebuilt rather than added to.
    /// </summary>
    [Fact]
    public void ASnapshotLeavesTheClearedFlagOut()
    {
        GameWorld world = World();

        (ServerPlayer player, _) = world.Join(1, "Mason", world.FreshCharacter());

        world.RunScript(player.Id, new ScriptRan([], [Hidden], []));

        SavedCharacter? saved = world.Snapshot(player.Id);

        Assert.NotNull(saved);
        Assert.DoesNotContain(Hidden, saved.Flags);
    }

    /// <summary>
    /// And the store writes the absence down. An insert-only table is one nothing can
    /// ever come back out of, and a door that locks behind you would open again on the
    /// next sign-in.
    /// </summary>
    [Fact]
    public async Task AClearedFlagStaysClearedAcrossASignOut()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        SavedCharacter before = SavedCharacter.Fresh(Town, 1, 1) with { Flags = [Hidden, 0x0035] };

        var registered = (AuthOutcome.Success)await store.RegisterAsync("Mason", "a-good-password", before);

        // What the server would write once a script had cleared one of them.
        await store.SaveAsync(registered.Account.Id, before with { Flags = [0x0035] });

        var back = (AuthOutcome.Success)await store.LoginAsync("Mason", "a-good-password");

        Assert.Contains(0x0035, back.Character.Flags);
        Assert.DoesNotContain(Hidden, back.Character.Flags);
    }
}
