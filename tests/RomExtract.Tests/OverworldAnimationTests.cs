using PokeMmo.Core.World;
using PokeMmo.RomExtract.Graphics;

namespace PokeMmo.RomExtract.Tests;

public class OverworldAnimationTests
{
    [Theory]
    [InlineData(Direction.Down, 0)]
    [InlineData(Direction.Up, 1)]
    [InlineData(Direction.Left, 2)]
    public void StandingStillUsesTheFirstThreeFrames(Direction facing, int expected)
    {
        (int frame, bool mirror) = OverworldAnimation.FrameFor(facing, walking: false, stride: 0);

        Assert.Equal(expected, frame);
        Assert.False(mirror);
    }

    [Fact]
    public void FacingRightIsTheLeftFrameMirrored()
    {
        // The cartridge stores no right-facing frames at all. It halves the sprite data
        // and is why every character in these games parts their hair on whichever side
        // you happen to be looking from.
        (int still, bool mirror) = OverworldAnimation.FrameFor(Direction.Right, walking: false, stride: 0);

        Assert.Equal(OverworldAnimation.FrameFor(Direction.Left, false, 0).Frame, still);
        Assert.True(mirror);
    }

    [Fact]
    public void WalkingAlternatesBetweenTwoStrides()
    {
        (int first, _) = OverworldAnimation.FrameFor(Direction.Down, walking: true, stride: 0);
        (int second, _) = OverworldAnimation.FrameFor(Direction.Down, walking: true, stride: 1);

        Assert.NotEqual(first, second);

        // And it comes back round, so a character changes feet rather than limping.
        Assert.Equal(first, OverworldAnimation.FrameFor(Direction.Down, true, 2).Frame);
    }

    [Fact]
    public void ANegativeStrideStillPicksAValidFrame()
    {
        // A step counter can start below zero if anything ever counts backwards, and a
        // negative modulo in C# would index off the front of the frame list.
        (int frame, _) = OverworldAnimation.FrameFor(Direction.Left, walking: true, stride: -1);

        Assert.InRange(frame, 0, OverworldAnimation.WalkingFrameCount - 1);
    }

    [Theory]
    [InlineData(Direction.Down)]
    [InlineData(Direction.Up)]
    [InlineData(Direction.Left)]
    [InlineData(Direction.Right)]
    public void EveryFrameItAsksForExists(Direction facing)
    {
        for (int stride = 0; stride < 4; stride++)
        {
            foreach (bool walking in new[] { true, false })
            {
                (int frame, _) = OverworldAnimation.FrameFor(facing, walking, stride);
                Assert.InRange(frame, 0, OverworldAnimation.WalkingFrameCount - 1);
            }
        }
    }

    [Fact]
    public void ASpriteWithTooFewFramesIsNotAWalker()
    {
        // Fourteen of the real cartridge's records have a single frame. Asking one of
        // those for frame eight would read somebody else's sprite.
        Assert.False(OverworldAnimation.CanWalk(1));
        Assert.True(OverworldAnimation.CanWalk(OverworldAnimation.WalkingFrameCount));
        Assert.True(OverworldAnimation.CanWalk(20));
    }
}

public class WalkProgressTests
{
    [Fact]
    public void ProgressRunsFromZeroToOneAcrossAStep()
    {
        var grid = new CollisionGrid(4, 4, new byte[16]);
        var player = new WalkingCharacter();

        player.Place(grid, new GridPosition(1, 1));

        Assert.Equal(1f, player.StepProgress);

        player.Update(WalkingCharacter.StepSeconds / 4f, Direction.Right);

        Assert.InRange(player.StepProgress, 0f, 1f);
        Assert.True(player.IsStepping);

        player.Update(WalkingCharacter.StepSeconds, null);

        Assert.Equal(1f, player.StepProgress);
        Assert.False(player.IsStepping);
    }
}
