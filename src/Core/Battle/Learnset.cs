namespace PokeMmo.Core.Battle;

/// <summary>A move learned at a level.</summary>
public readonly record struct LevelUpMove(int Level, int MoveId)
{
    /// <summary>
    /// A learnset entry is one 16-bit word: the move in the low nine bits, the level
    /// in the seven above it. Nine bits is exactly enough for this generation's move
    /// count, which is why the packing works out so tightly.
    /// </summary>
    public const ushort Terminator = 0xFFFF;

    public static LevelUpMove Decode(ushort entry) => new(entry >> 9, entry & 0x1FF);

    public ushort Encode() => (ushort)((Level << 9) | (MoveId & 0x1FF));

    public override string ToString() => $"L{Level}: move {MoveId}";
}

/// <summary>Everything one species learns by levelling.</summary>
public sealed record Learnset(int Species, IReadOnlyList<LevelUpMove> Moves)
{
    /// <summary>
    /// The moves a creature of this level would actually know: the last four learned
    /// at or below it, which is how the games fill in a wild encounter's moveset.
    /// </summary>
    public IEnumerable<int> MovesKnownAt(int level) =>
        Moves.Where(m => m.Level <= level)
             .TakeLast(4)
             .Select(m => m.MoveId);
}
