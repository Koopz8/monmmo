namespace PokeMmo.Core.World;

/// <summary>What turning a flag on actually changes.</summary>
public enum FlagGate
{
    /// <summary>
    /// Nothing anywhere in the world file. A mark on the character and not a fact about the
    /// world — a badge, which starter was taken, whether somebody has been shown a screen.
    /// </summary>
    Nothing,

    /// <summary>
    /// Somebody stands there, or stops standing there. A person appearing is how this game
    /// opens most of its story: the guard moves, the rival turns up, the professor is in.
    /// </summary>
    APerson,

    /// <summary>The boat, which is the one gate that is a flag and an item together.</summary>
    TheBoat,
}

/// <summary>
/// Which flags are facts about the world and which are marks on a character.
/// <para>
/// <b>The question co-op could not answer without this.</b> Two people playing together want
/// a door one of them opened to be open for both — and do not want a badge one of them earned
/// to appear on the other. The cartridge does not distinguish them. There is no bit anywhere
/// that says "this flag is about the world", so the classification cannot be read.
/// </para>
/// <para>
/// It can be <b>derived</b>, which is this project's usual answer and a better one than a
/// hand-written list of flag numbers nobody could ever check. Ask what turning the flag on
/// changes, against the world file itself: does somebody appear or disappear? does the boat
/// sail? If the answer is no to all of it, the flag gates nothing and is a mark.
/// </para>
/// <para>
/// Derived from <see cref="WorldData"/> rather than from a cartridge, so the server can do it
/// with what it already has — the same file it loads to know what the ground looks like. The
/// dump tool prints the same classification against a real image so the split can be looked
/// at rather than trusted.
/// </para>
/// <para>
/// <b>Where this is wrong, it is wrong in a knowable direction.</b> A flag that gates
/// something this project has not extracted yet reads as <see cref="FlagGate.Nothing"/> and
/// stays personal — so a door somebody opened would not open for their friend. That is a
/// visible, reportable failure. The opposite error would hand somebody a badge, which is
/// not visible at all until much later.
/// </para>
/// </summary>
public sealed class FlagGates
{
    private readonly Dictionary<int, FlagGate> _gates = [];

    public FlagGates(WorldData world)
    {
        // Somebody who is hidden by a flag is somebody that flag puts on a map or takes off
        // one. This is the great majority of the story's gates: FireRed moves people about
        // far more often than it locks a door.
        foreach (MapData map in world.Maps)
        {
            foreach (MapObject person in map.Objects)
            {
                if (person.HiddenBy != 0) _gates[person.HiddenBy] = FlagGate.APerson;
            }
        }

        // And the boat, which is the one gate written as a flag and an item together. The
        // flag half is the world's; the item half is not, and is deliberately left alone —
        // see the note on carrying, below.
        foreach (FerryPass pass in world.FerryPasses)
        {
            if (pass.Flag != 0) _gates.TryAdd(pass.Flag, FlagGate.TheBoat);
        }
    }

    /// <summary>What this flag gates, which is nothing unless the world file says otherwise.</summary>
    public FlagGate Of(int flag) => _gates.GetValueOrDefault(flag, FlagGate.Nothing);

    /// <summary>
    /// True when this flag is a fact about the world rather than a mark on a character.
    /// <para>
    /// The one question anything outside this class asks. Kept as a method rather than left
    /// to callers comparing against the enum, because "not Nothing" is the rule and a caller
    /// writing it out is a caller who can write it out wrongly.
    /// </para>
    /// </summary>
    public bool IsAboutTheWorld(int flag) => Of(flag) != FlagGate.Nothing;

    /// <summary>How many flags gate each kind of thing, for anybody printing it.</summary>
    public IReadOnlyDictionary<FlagGate, int> Counted =>
        _gates.GroupBy(g => g.Value).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Every flag that gates something, in order.</summary>
    public IReadOnlyList<(int Flag, FlagGate Gate)> All =>
        [.. _gates.OrderBy(g => g.Key).Select(g => (g.Key, g.Value))];

    /// <summary>How many flags gate anything at all.</summary>
    public int Count => _gates.Count;
}
