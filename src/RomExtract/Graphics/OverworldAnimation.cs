using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Graphics;

/// <summary>
/// Which frame of a walking sprite to draw.
/// <para>
/// The layout is the cartridge's, and it is worth writing down exactly, because there
/// are two plausible ways to arrange nine frames and only one of them is right:
/// </para>
/// <code>
///   0  facing down, still        3  down, step one     4  down, step two
///   1  facing up, still          5  up, step one       6  up, step two
///   2  facing left, still        7  left, step one     8  left, step two
/// </code>
/// <para>
/// Grouped by direction, not by stride. Reading it the other way — three facings per
/// stride — produces frames that are all real and all in range, and a character who
/// alternates between facing toward you and away from you on every step. It looks
/// like spinning, and nothing about it looks like an out-of-bounds read.
/// </para>
/// <para>
/// Facing right is not stored at all: it is the left-facing frame drawn mirrored,
/// which halves the sprite data and is why every character in these games parts their
/// hair on whichever side you happen to be looking from.
/// </para>
/// </summary>
public static class OverworldAnimation
{
    /// <summary>Frames a full walking set has: three facings by three positions.</summary>
    public const int WalkingFrameCount = 9;

    /// <summary>
    /// The frame for a facing and stride, and whether to mirror it.
    /// <para>
    /// <paramref name="stride"/> alternates between the two walking frames so the
    /// character changes feet; passing the step count works.
    /// </para>
    /// </summary>
    public static (int Frame, bool Mirror) FrameFor(Direction facing, bool walking, int stride)
    {
        int row = facing switch
        {
            Direction.Down => 0,
            Direction.Up => 1,
            _ => 2,
        };

        bool mirror = facing == Direction.Right;

        if (!walking) return (row, mirror);

        // Two walking frames per direction, laid out together: 3 and 4 are down, 5 and
        // 6 are up, 7 and 8 are left.
        int foot = ((stride % 2) + 2) % 2;

        return (3 + row * 2 + foot, mirror);
    }

    /// <summary>True when a sprite has enough frames to be animated as a walker.</summary>
    public static bool CanWalk(int frameCount) => frameCount >= WalkingFrameCount;
}
