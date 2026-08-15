using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace PokeMmo.Server.Storage;

/// <summary>
/// Argon2id password hashing.
/// <para>
/// Argon2id rather than PBKDF2 or bcrypt because it is deliberately memory-hard: an
/// attacker with a rack of GPUs gains far less on it than on a hash that only costs
/// arithmetic. The cost parameters are stored inside each hash rather than fixed in
/// code, so they can be raised later without every existing account being locked out
/// — an old hash still verifies with the parameters it was made under.
/// </para>
/// </summary>
public static class PasswordHasher
{
    /// <summary>
    /// 19 MiB and two passes, which is OWASP's published Argon2id baseline for p=1.
    /// <para>
    /// It was 64 MiB and three passes, chosen for being comfortably above any baseline
    /// — and "unnoticeable once per login" was written next to it without anybody having
    /// measured it. Measured, on the machine this was developed on, that cost <b>997 ms
    /// and 64 MiB per login</b>. A thousand people arriving is then 64 GiB of demand and
    /// a quarter of an hour of one core, which is not a slow door, it is a closed one.
    /// </para>
    /// <para>
    /// At the baseline the same measurement is 91 ms and 19 MiB: eleven times the
    /// throughput and a third of the memory, still memory-hard, and a number somebody
    /// else has argued for in public rather than one this project picked. The cost
    /// parameters travel inside each hash, so every account made under the old ones goes
    /// on verifying under the old ones — nobody is locked out by this, and nobody is
    /// silently downgraded either.
    /// </para>
    /// </summary>
    public const int MemoryKib = 19 * 1024;

    public const int Iterations = 2;

    public const int Parallelism = 1;

    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>Shortest password accepted. Length beats complexity rules.</summary>
    public const int MinimumPasswordLength = 8;

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Derive(password, salt, MemoryKib, Iterations, Parallelism);

        return string.Join('$',
            "",
            "argon2id",
            "v=19",
            $"m={MemoryKib},t={Iterations},p={Parallelism}",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Checks a password against a stored hash. Returns false rather than throwing on
    /// a malformed hash: a corrupted row should refuse a login, not take the server
    /// down.
    /// </summary>
    public static bool Verify(string password, string encoded)
    {
        // Argon2 refuses an empty password outright, and no account can have one — the
        // minimum length is eight. Answering "no" is the correct result and keeps a
        // client that sends an empty field from throwing inside the connection handler.
        if (string.IsNullOrEmpty(password)) return false;

        if (!TryParse(encoded, out byte[]? salt, out byte[]? expected, out int memory, out int iterations, out int parallelism))
            return false;

        byte[] actual = Derive(password, salt!, memory, iterations, parallelism);

        return CryptographicOperations.FixedTimeEquals(actual, expected!);
    }

    /// <summary>True when a stored hash was made with weaker parameters than current.</summary>
    public static bool NeedsRehash(string encoded)
    {
        if (!TryParse(encoded, out _, out _, out int memory, out int iterations, out int parallelism))
            return true;

        return memory < MemoryKib || iterations < Iterations || parallelism != Parallelism;
    }

    private static byte[] Derive(string password, byte[] salt, int memoryKib, int iterations, int parallelism)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };

        return argon.GetBytes(HashBytes);
    }

    private static bool TryParse(
        string encoded,
        out byte[]? salt,
        out byte[]? hash,
        out int memoryKib,
        out int iterations,
        out int parallelism)
    {
        salt = null;
        hash = null;
        memoryKib = 0;
        iterations = 0;
        parallelism = 0;

        string[] parts = encoded.Split('$');

        // "", "argon2id", "v=19", "m=..,t=..,p=..", salt, hash
        if (parts.Length != 6 || parts[1] != "argon2id") return false;

        foreach (string setting in parts[3].Split(','))
        {
            string[] pair = setting.Split('=');
            if (pair.Length != 2 || !int.TryParse(pair[1], out int value)) return false;

            switch (pair[0])
            {
                case "m": memoryKib = value; break;
                case "t": iterations = value; break;
                case "p": parallelism = value; break;
                default: return false;
            }
        }

        if (memoryKib <= 0 || iterations <= 0 || parallelism <= 0) return false;

        try
        {
            salt = Convert.FromBase64String(parts[4]);
            hash = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }
}
