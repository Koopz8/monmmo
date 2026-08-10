using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Graphics;

/// <summary>
/// Which frame of a walking sprite to draw.
/// <para>
/// The layout is the cartridge's, and it is worth writing down because it is not the
/// obvious one: the first three frames are the three facings standing still, then two
/// more sets of three for the two halves of a stride. Facing right is not stored at
/// all — it is the left-facing frame drawn mirrored, which halves the sprite data and
/// is why every character in these games parts their hair on whichever side you are
/// looking from.
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

        // Frames 3-5 are one half of the stride and 6-8 the other.
        int half = ((stride % 2) + 2) % 2;

        return (3 + row + half * 3, mirror);
    }

    /// <summary>True when a sprite has enough frames to be animated as a walker.</summary>
    public static bool CanWalk(int frameCount) => frameCount >= WalkingFrameCount;
}
