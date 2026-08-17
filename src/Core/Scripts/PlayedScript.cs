using PokeMmo.Core.World;

namespace PokeMmo.Core.Scripts;

// MOVED OUT OF THE SERVER, AND OUT OF REACH OF A FIXTURE IS WHY.
//
// This record is the contract between whatever reads a script and whatever walks the world.
// It lived beside the walk, in an assembly the cartridge reader cannot see — so the reader
// itself had to live in Program.cs, which has no tests and which no fixture can hold. Two live
// fixes were sitting in there unguarded when this moved, and the same structural fault had been
// found five times in six milestones.
//
// A contract belongs where both sides can see it.
/// <summary>What one script turned out to do, as far as playing the game goes.</summary>
/// <param name="FlagsSet">Flags it turned on.</param>
/// <param name="FlagsCleared">Flags it turned off.</param>
/// <param name="Teaches">Field moves it handed over, already translated from the item.</param>
/// <param name="Specials">Routines it asked for and did not get an answer from.</param>
/// <param name="Gives">
/// A creature it hands over and the level it names, if it hands one over. The level matters:
/// the first version took only the species and put everything in at five, so every gift in the
/// game arrived as a starter.
/// </param>
/// <param name="Fights">A trainer it picks a fight with, if it picks one.</param>
public sealed record PlayedScript(
    IReadOnlyList<int> FlagsSet,
    IReadOnlyList<int> FlagsCleared,
    IReadOnlyList<int> Teaches,
    IReadOnlyList<int> Specials,
    (int Species, int Level)? Gives,
    int? Fights)
{
    /// <summary>An item it handed over, and how many.</summary>
    public (int ItemId, int Count)? Gets { get; init; }

    /// <summary>An item it took away, and how many.</summary>
    public (int ItemId, int Count)? Takes { get; init; }

    /// <summary>
    /// People it took off the map, by their number on it.
    /// <para>
    /// Read and thrown away until now, by both this and the closure walk. It is how a
    /// guard stops being in a doorway: the script does not move him, it removes him —
    /// and a walker that never hears about it sees the same person standing there
    /// forever, however the conversation went.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Hides { get; init; } = [];

    /// <summary>
    /// Commands with no width that stopped this run's reading, by opcode.
    /// <para>
    /// <b>The half of the error bar that was missing.</b> A run reports the routines it could
    /// not answer, and it has never reported the commands it could not read. Those are not the
    /// same boundary: a routine is the game's own code and nothing here will ever follow it,
    /// while a command with no width is a gap in a table in this repository — the difference
    /// between "the world is this small" and "my reader stopped".
    /// </para>
    /// <para>
    /// One byte with no entry hid nineteen people on eleven maps, and every instrument that saw
    /// it reported a smaller world, cleanly, with no error anywhere.
    /// </para>
    /// </summary>
    public IReadOnlyList<byte> StoppedAt { get; init; } = [];

    /// <summary>What it asked the bag for, and what it was told.</summary>
    public IReadOnlyList<(int ItemId, int Count, bool Carried)> Asked { get; init; } = [];

    /// <summary>
    /// People this script walked, and where they ended up.
    /// <para>
    /// The other way somebody stops being in a doorway, and the one nothing here has ever
    /// modelled. A guard given his drink is not removed — he takes a step to one side, and to
    /// a walker that has only ever asked "is anybody on this square" he is in the doorway
    /// forever however the conversation went.
    /// </para>
    /// <para>
    /// Where they end up is <b>read</b>: the step bytes are the cartridge's own and what they
    /// mean was derived by walking every list across every map and counting who ended up
    /// inside a wall. A step this project does not model is stood still through, which is the
    /// same honest reading <c>DirectionOf</c> takes — being wrong visibly beats guessing.
    /// </para>
    /// </summary>
    /// <remarks>
    /// A displacement rather than a square. The script says who and how far; where they were
    /// standing to begin with is the map's record, or wherever an earlier scene left them, and
    /// only this side of the split knows that.
    /// </remarks>
    public IReadOnlyList<(int PersonId, int Dx, int Dy)> Walked { get; init; } = [];

    /// <summary>
    /// True when the script stopped at a yes-or-no and nobody answered it.
    /// <para>
    /// A run cannot answer one — everything else can be decided from a save and this needs a
    /// person — so the runner stops and hands back where to carry on from. Nothing has ever
    /// carried on. Neither this loop nor the closure walk has so much as looked at the field,
    /// so every offer in the game has been left hanging mid-sentence: not declined, which
    /// would at least be a branch, but simply not reached.
    /// </para>
    /// </summary>
    public bool StoppedAtAQuestion { get; init; }
}

