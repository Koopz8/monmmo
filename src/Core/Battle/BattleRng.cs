namespace PokeMmo.Core.Battle;

/// <summary>
/// The hardware's linear congruential generator, reproduced exactly.
/// <para>
/// Determinism is the point. A battle is a pure function of its starting state, the
/// actions taken, and this seed — so the server can resolve a battle authoritatively
/// and send the client only the seed and the actions, and the client will reproduce
/// every damage roll and critical hit identically. Streaming each number instead would
/// mean the two sides could disagree, which is the situation the whole architecture
/// exists to avoid.
/// </para>
/// </summary>
public sealed class BattleRng(uint seed)
{
    private const uint Multiplier = 0x41C64E6D;
    private const uint Increment = 0x00006073;

    private uint _state = seed;

    /// <summary>The seed this generator started from, so a battle can be replayed.</summary>
    public uint Seed { get; } = seed;

    /// <summary>The current state, for saving and resuming mid-battle.</summary>
    public uint State => _state;

    /// <summary>
    /// The next 16-bit value. The hardware advances a 32-bit state and returns its
    /// high half — the low bits are famously poor, which is why they are discarded.
    /// </summary>
    public ushort Next()
    {
        _state = unchecked(_state * Multiplier + Increment);
        return (ushort)(_state >> 16);
    }

    /// <summary>A value in [0, bound).</summary>
    public int Next(int bound) => bound <= 0 ? 0 : Next() % bound;

    /// <summary>True with probability <paramref name="percent"/> out of 100.</summary>
    public bool Chance(int percent) => percent > 0 && Next(100) < percent;

    /// <summary>True with probability 1 in <paramref name="denominator"/>.</summary>
    public bool OneIn(int denominator) => denominator > 0 && Next(denominator) == 0;

    /// <summary>Resumes a generator mid-sequence.</summary>
    public static BattleRng Resume(uint seed, uint state) => new(seed) { _state = state };
}
