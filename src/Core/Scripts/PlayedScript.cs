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
/// <summary>
/// One moment a script looked at, or changed, one of the story's own variables.
/// <para>
/// <b>A read is as much a fact as a write, and only writes have ever been recorded.</b>
/// <c>--who-writes</c> answers who puts a number in; nothing has ever answered who looks at
/// one, and the two questions have different answers at the only place it has mattered. The
/// three balls in the lab hand something over at <c>0x4055 == 2</c> and say "you already have
/// one" at three or more — so "the run ends holding five" and "the run was holding five when
/// the ball looked" are different claims, and this project has only ever been able to make the
/// first.
/// </para>
/// </summary>
/// <param name="Variable">Which of the story's variables.</param>
/// <param name="Wrote">True for a write, false for a comparison.</param>
/// <param name="Held">What was in it at that moment — before the write, for a write.</param>
/// <param name="Value">What went in, for a write; what it was compared against, for a read.</param>
public sealed record VariableTouch(int Variable, bool Wrote, int Held, int Value)
{
    public override string ToString() => Wrote
        ? $"0x{Variable:X4} <- {Value} (was {Held})"
        : $"0x{Variable:X4} ? {Value} (held {Held})";
}

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

    /// <summary>
    /// Money this script asked about or charged, which the run could answer neither way.
    /// <para>
    /// A ceiling, and the third one — <c>--say-yes</c> and <c>--boat</c> are the other two and
    /// both are named, printed and levered. This one had neither until it turned up carrying a
    /// party member: the run walks past the check with an empty purse and takes the arm where
    /// the thing is handed over. Counted here so that it cannot go on reading like a floor.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> MoneyWalkedPast { get; init; } = [];

    /// <summary>
    /// Where the fight this script stopped at leads, once it is won.
    /// <para>
    /// <b>Whoever resolves the fight has to run this, and nobody used to.</b> It is the
    /// battle's own continuation — the badge, the flags, the thing the victory was for — and
    /// it belongs to winning rather than to having won. Handed back rather than jumped to,
    /// because a script cannot know how a fight went and a run can.
    /// </para>
    /// </summary>
    public uint AfterTheFight { get; init; }

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
    /// <b>The steps rather than the sum, and that is the whole of milestone 192.</b> A
    /// displacement is applied in one go and lands wherever the arithmetic says: on a real
    /// cartridge 426 of these landed OFF THE MAP on the floor run and 41 on a square somebody
    /// could stand on. A person walking a cutscene walks one square at a time and stops at a
    /// wall, and the collision grid is already here to say where that is — so the steps travel
    /// and whoever holds the map does the walking.
    /// </remarks>
    public IReadOnlyList<(int PersonId, IReadOnlyList<Direction> Steps, uint At)> Walked { get; init; } = [];

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

    /// <summary>
    /// Every look at and change to a watched variable, in the order the script did them.
    /// <para>
    /// Empty unless somebody asked for a variable to be watched, because a run touches these
    /// tens of thousands of times and a diagnostic that costs the measurement is not one.
    /// </para>
    /// <para>
    /// Ordered, and that is the whole point: the question this exists for is not what a number
    /// ended up as but <b>what it was at the moment somebody read it</b>, and a dictionary of
    /// final values cannot answer that however many times it is printed.
    /// </para>
    /// </summary>
    public IReadOnlyList<VariableTouch> Touched { get; init; } = [];
}

