using PokeMmo.Core.Battle;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A thousand people arriving.
/// <para>
/// The most expensive thing this server does is check a password, by three orders of
/// magnitude: a step is answered in about two milliseconds and a password costs ninety,
/// and nineteen megabytes for as long as it takes. That is deliberate — a hash that is
/// cheap to check is cheap to attack — and it was unbounded, so a hundred people
/// arriving together were a hundred hashes at once, each one holding memory and fighting
/// the others for a core it needed to itself.
/// </para>
/// <para>
/// Measured with the crowd tool, at a hundred: the median arrival took 24 seconds, the
/// worst took 44, and seven never got in at all — while the world, for everybody already
/// inside, answered steps in under three milliseconds. The wall was the door and not the
/// game, which is not what anybody would have guessed.
/// </para>
/// </summary>
public class TheDoorTests
{
    /// <summary>Never more people inside the door than it is wide.</summary>
    [Fact]
    public async Task NoMoreThanItsWidthAreLetThroughAtOnce()
    {
        var door = new Doorway(width: 3);

        int inside = 0, most = 0;
        object counter = new();

        async Task<int> Knock()
        {
            return await door.AdmitAsync(async () =>
            {
                lock (counter)
                {
                    inside++;
                    most = Math.Max(most, inside);
                }

                await Task.Delay(20);

                lock (counter) inside--;

                return 1;
            });
        }

        await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Knock()));

        Assert.True(most <= 3, $"{most} were inside a door three wide");
        Assert.Equal(0, inside);
    }

    /// <summary>And the width is never nothing, whatever it is asked for.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void AndADoorIsNeverShut(int asked)
    {
        Assert.True(new Doorway(asked).Width >= 1);
    }

    /// <summary>What the work returns is what the caller gets.</summary>
    [Fact]
    public async Task WhatIsInsideComesBackOut()
    {
        Assert.Equal("welcome", await new Doorway(1).AdmitAsync(() => Task.FromResult("welcome")));
    }

    /// <summary>
    /// And a check that throws still gives its permit back. Without this, one bad login
    /// narrows the door for ever and the server dies hours later of a cause nothing
    /// records.
    /// </summary>
    [Fact]
    public async Task AndAFailedCheckGivesItsPermitBack()
    {
        var door = new Doorway(width: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => door.AdmitAsync<int>(() => throw new InvalidOperationException("no")));

        Assert.Equal(2, await door.AdmitAsync(() => Task.FromResult(2)));
    }

    /// <summary>The door counts, because a queue nobody can see is a queue nobody fixes.</summary>
    [Fact]
    public async Task AndItCountsWhoCameThrough()
    {
        var door = new Doorway(width: 2);

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => door.AdmitAsync(() => Task.FromResult(0))));

        Assert.Equal(6, door.Admitted);
        Assert.Equal(0, door.Waiting);
    }

    /// <summary>
    /// The cost parameters are the published baseline rather than a number this project
    /// picked, and they are the thing a future decision to raise them has to beat.
    /// </summary>
    [Fact]
    public void ThePasswordCostIsTheBaselineItClaimsToBe()
    {
        Assert.Equal(19 * 1024, PasswordHasher.MemoryKib);
        Assert.Equal(2, PasswordHasher.Iterations);
        Assert.Equal(1, PasswordHasher.Parallelism);
    }

    /// <summary>
    /// And an account made under the old, dearer parameters still gets in. Lowering a
    /// cost must never lock anybody out, and it does not: every hash carries the
    /// parameters it was made under.
    /// </summary>
    [Fact]
    public void AndAnAccountMadeUnderTheOldCostStillOpens()
    {
        // What the hasher wrote when it was 64 MiB and three passes, made here by hand
        // rather than kept as a string, so this test says what it is testing.
        string dearer = Hashed("a-good-password", 64 * 1024, 3);

        Assert.True(PasswordHasher.Verify("a-good-password", dearer));
        Assert.False(PasswordHasher.Verify("the-wrong-one", dearer));

        // And it is not flagged for rehashing, because it is stronger and not weaker.
        Assert.False(PasswordHasher.NeedsRehash(dearer));
    }

    private static string Hashed(string password, int memoryKib, int iterations)
    {
        byte[] salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);

        using var argon = new Konscious.Security.Cryptography.Argon2id(
            System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = 1,
        };

        return string.Join('$',
            "",
            "argon2id",
            "v=19",
            $"m={memoryKib},t={iterations},p=1",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(argon.GetBytes(32)));
    }
}

/// <summary>
/// Writing everybody down.
/// <para>
/// A save is the one thing this server does that touches a disk. It happens on anything
/// a player does that is not walking, at most once a second each — and it rewrites the
/// whole character every time: the row, the party, every move, every item, every flag.
/// </para>
/// <para>
/// Measured at a hundred players doing something every two seconds: 21 ms a save on
/// average and 458 ms at worst. A thousand players at that rate is five hundred saves a
/// second, which is ten times more writing than those numbers allow. So the first
/// question is how many of them were needed at all.
/// </para>
/// </summary>
public class WritingEverybodyDownTests
{
    private static SavedCharacter Somebody() => new(
        "1.0", 4, 5, Direction.Down,
        [new SavedMon(1, 5, null, 20, StatusCondition.None, Nature.Hardy, [33])])
    {
        Money = 3000,
        Items = [new BagEntry(4, 1)],
        Flags = [7, 9],
    };

    /// <summary>
    /// Two snapshots of somebody who has not moved a muscle are the same snapshot.
    /// <para>
    /// They were not, and could not have been: a record compares its members with
    /// <c>Equals</c>, and for a list that is reference equality. <see cref="SavedMon"/>
    /// closed that trap on itself and said why; the type holding it did not, so the
    /// question "has anything changed since the last save?" could only ever be answered
    /// yes — and every non-movement message any player sent rewrote everything about
    /// them.
    /// </para>
    /// </summary>
    [Fact]
    public void TwoSnapshotsOfAnUnchangedCharacterAreEqual()
    {
        Assert.Equal(Somebody(), Somebody());
    }

    /// <summary>And one step apart is not.</summary>
    [Fact]
    public void AndOneStepApartIsNot()
    {
        Assert.NotEqual(Somebody(), Somebody() with { X = 5 });
    }

    /// <summary>Nor is one item, one flag, one coin or one creature apart.</summary>
    [Fact]
    public void NorIsAnythingElseThatChanged()
    {
        Assert.NotEqual(Somebody(), Somebody() with { Money = 2999 });
        Assert.NotEqual(Somebody(), Somebody() with { Flags = [7] });
        Assert.NotEqual(Somebody(), Somebody() with { Items = [] });

        Assert.NotEqual(
            Somebody(),
            Somebody() with
            {
                Party = [new SavedMon(1, 6, null, 20, StatusCondition.None, Nature.Hardy, [33])],
            });
    }

    /// <summary>
    /// And a party member whose health has changed by one point is a different save,
    /// which is the case that matters: the party is what a fight changes and the party is
    /// what would silently fail to be written.
    /// </summary>
    [Fact]
    public void AndOneHitPointIsADifferentSave()
    {
        SavedCharacter hurt = Somebody() with
        {
            Party = [new SavedMon(1, 5, null, 19, StatusCondition.None, Nature.Hardy, [33])],
        };

        Assert.NotEqual(Somebody(), hurt);
    }
}
