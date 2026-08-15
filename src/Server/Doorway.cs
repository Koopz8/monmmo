using System.Diagnostics;

namespace PokeMmo.Server;

/// <summary>
/// The door, and how many people may be coming through it at once.
/// <para>
/// Checking a password is the most expensive thing this server does by three orders of
/// magnitude. A step is answered in about three milliseconds; a login costs ninety, and
/// nineteen megabytes while it runs, on purpose — a password hash that is cheap to check
/// is cheap to attack.
/// </para>
/// <para>
/// Left unbounded, that cost is not paid one login at a time. A hundred people arriving
/// together are a hundred simultaneous hashes: a hundred times the memory, all of it
/// live at once, and every one of them fighting the others for a core it needs
/// exclusively. Measured at a hundred, the median arrival took <b>24 seconds</b> and
/// seven of them never got in at all — while the world itself, for everybody already
/// inside, was answering steps in under three milliseconds. The wall was the door, not
/// the game.
/// </para>
/// <para>
/// So the door has a width. Permits are held while a password is hashed and released
/// the moment it is done, which bounds the memory to permits × the hash's own size and
/// lets each hash run at full speed instead of a crowd of them running at a fraction of
/// it. The queue behind it is honest: a door has a rate, and a server that pretends
/// otherwise simply fails at a larger number.
/// </para>
/// </summary>
public sealed class Doorway
{
    private readonly SemaphoreSlim _permits;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private long _admitted;
    private long _waitedMilliseconds;
    private double _longestWait;
    private int _waiting;

    /// <summary>
    /// One fewer than the cores, and at least one.
    /// <para>
    /// The spare core is the game. A door sized to every core it can see admits people
    /// slightly faster and stops everybody already inside from moving while it does,
    /// which is the wrong trade — the people in the world were there first.
    /// </para>
    /// </summary>
    public static int DefaultWidth => Math.Max(1, Environment.ProcessorCount - 1);

    public Doorway(int? width = null)
    {
        Width = Math.Max(1, width ?? DefaultWidth);
        _permits = new SemaphoreSlim(Width, Width);
    }

    /// <summary>How many passwords may be checked at once.</summary>
    public int Width { get; }

    /// <summary>How many have come through since the server started.</summary>
    public long Admitted => Interlocked.Read(ref _admitted);

    /// <summary>How many are queued at the door right now.</summary>
    public int Waiting => Volatile.Read(ref _waiting);

    /// <summary>The longest anybody has waited to be let through, in milliseconds.</summary>
    public double LongestWait => Volatile.Read(ref _longestWait);

    /// <summary>The average wait so far, in milliseconds.</summary>
    public double AverageWait => Admitted == 0 ? 0 : Interlocked.Read(ref _waitedMilliseconds) / (double)Admitted;

    /// <summary>
    /// Runs one password check, waiting for room at the door first.
    /// <para>
    /// Whatever the work returns is passed straight back, so the caller cannot tell this
    /// is here except by the queue — which is the point. The permit is released before
    /// anything else happens to the account, because everything else is cheap and there
    /// is no reason to hold the expensive resource through it.
    /// </para>
    /// </summary>
    public async Task<T> AdmitAsync<T>(Func<Task<T>> checking, CancellationToken cancellationToken = default)
    {
        double asked = _clock.Elapsed.TotalMilliseconds;

        Interlocked.Increment(ref _waiting);

        await _permits.WaitAsync(cancellationToken).ConfigureAwait(false);

        Interlocked.Decrement(ref _waiting);

        double waited = _clock.Elapsed.TotalMilliseconds - asked;

        Interlocked.Increment(ref _admitted);
        Interlocked.Add(ref _waitedMilliseconds, (long)waited);

        // Not exact under a race, and it does not need to be: this is a report, not a
        // rule, and a worst case that is occasionally the second worst is still the
        // right order of magnitude.
        if (waited > _longestWait) Volatile.Write(ref _longestWait, waited);

        try
        {
            return await checking().ConfigureAwait(false);
        }
        finally
        {
            _permits.Release();
        }
    }

    /// <summary>
    /// What this door can do, in the words the startup report uses.
    /// <para>
    /// Said out loud because it is the number that decides whether a launch works. A
    /// server that can hold ten thousand people and admit five a second takes half an
    /// hour to let them in, and nothing about that is visible from inside.
    /// </para>
    /// </summary>
    public string Rate(double millisecondsPerCheck) =>
        $"the door is {Width} wide: about {Width * 1000.0 / Math.Max(millisecondsPerCheck, 1):F0} people a second, " +
        $"{Width * PokeMmo.Server.Storage.PasswordHasher.MemoryKib / 1024} MiB while they are checked";
}
