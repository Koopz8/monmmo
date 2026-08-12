using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Tests;

public class WalkingCharacterTests
{
    /// <summary>An open 5x5 field with a wall at (2, 2).</summary>
    private static CollisionGrid Field()
    {
        var collision = new byte[25];
        collision[2 * 5 + 2] = 1;
        return new CollisionGrid(5, 5, collision);
    }

    private static WalkingCharacter At(int x, int y)
    {
        var character = new WalkingCharacter();
        character.Place(Field(), new GridPosition(x, y));
        return character;
    }

    /// <summary>Runs enough frames at 60fps for any in-progress step to finish.</summary>
    private static void RunStepToCompletion(WalkingCharacter character, Direction? held)
    {
        for (int frame = 0; frame < 20 && (character.IsStepping || frame == 0); frame++)
            character.Update(1f / 60f, frame == 0 ? held : null);
    }

    [Fact]
    public void StartsWhereItIsPlaced()
    {
        WalkingCharacter character = At(1, 1);

        Assert.Equal(new GridPosition(1, 1), character.Square);
        Assert.False(character.IsStepping);
        Assert.Equal((16f, 16f), character.PixelPosition);
    }

    [Fact]
    public void TakesOneWholeSquarePerStep()
    {
        WalkingCharacter character = At(1, 1);
        RunStepToCompletion(character, Direction.Right);

        Assert.Equal(new GridPosition(2, 1), character.Square);
        Assert.Equal(1, character.StepsTaken);
        Assert.Equal((32f, 16f), character.PixelPosition);
    }

    [Fact]
    public void InterpolatesBetweenSquaresWhileStepping()
    {
        WalkingCharacter character = At(1, 1);

        // Half a step's worth of time should land halfway between the two squares.
        character.Update(WalkingCharacter.StepSeconds / 2f, Direction.Right);

        Assert.True(character.IsStepping);
        Assert.Equal(24f, character.PixelPosition.X, 1);
        Assert.Equal(16f, character.PixelPosition.Y, 1);
    }

    [Fact]
    public void AStepIsOnePixelAFrameAndNeverHalfOfOne()
    {
        // The whole of what "janky" was. A square is sixteen pixels and the screen is
        // drawn sixty times a second, so a step of sixteen frames is one pixel a frame,
        // exactly. At the old 0.16 seconds it was 1.67 pixels a frame, and a world drawn
        // at three times scale with point filtering has nowhere to put a third of a
        // pixel — so every tile on screen jumped between two and five screen pixels at
        // random, differently for each tile.
        WalkingCharacter character = At(1, 1);

        // The square it starts on, then one reading after every frame of the step. The
        // frame that starts a step also advances it, which is why the first reading is
        // already a pixel along.
        List<float> seen = [character.PixelPosition.X];

        character.Update(1f / 60f, Direction.Right);
        seen.Add(character.PixelPosition.X);

        while (character.IsStepping)
        {
            character.Update(1f / 60f, null);
            seen.Add(character.PixelPosition.X);
        }

        // Whole pixels, all the way across.
        Assert.All(seen, x => Assert.Equal(x, MathF.Round(x)));

        // And one at a time: sixteen pixels over sixteen frames, no frame skipped and
        // none doubled.
        Assert.Equal(WalkingCharacter.StepFrames + 1, seen.Count);
        Assert.Equal(16f, seen[0]);
        Assert.Equal(32f, seen[^1]);

        for (int i = 1; i < seen.Count; i++) Assert.Equal(1f, seen[i] - seen[i - 1]);
    }

    [Fact]
    public void AStepAnimationOutlastsTheServerSLimit()
    {
        // Why the client normally cannot break the rate limit without trying: its own
        // step takes longer than the shortest gap the server accepts, so walking
        // normally never comes close. This is the relationship the client's post-arrival
        // hold exists to preserve, and the moment it stops being true a player walking
        // in a straight line starts being dragged backwards.
        Assert.True(WalkingCharacter.MinimumStepSeconds < WalkingCharacter.StepSeconds);
    }

    [Fact]
    public void IgnoresInputUntilTheCurrentStepFinishes()
    {
        // Grid-locked movement: a step that has started cannot be diverted, or a
        // player could stop between squares and desync from the server's view.
        WalkingCharacter character = At(1, 1);

        character.Update(0.01f, Direction.Right);
        character.Update(0.01f, Direction.Down);

        Assert.Equal(new GridPosition(2, 1), character.Square);
        Assert.Equal(Direction.Right, character.Facing);
    }

    [Fact]
    public void TurnsOnTheSpotWhenBlocked()
    {
        WalkingCharacter character = At(2, 1);
        character.Update(1f / 60f, Direction.Down);   // (2,2) is a wall

        Assert.Equal(new GridPosition(2, 1), character.Square);
        Assert.Equal(Direction.Down, character.Facing);
        Assert.False(character.IsStepping);
        Assert.Equal(0, character.StepsTaken);
    }

    [Fact]
    public void AStepIsReportedTheFrameItBegins()
    {
        WalkingCharacter character = At(1, 1);
        character.Update(1f / 60f, Direction.Right);

        Assert.Equal(Direction.Right, character.ToReport);
    }

    [Fact]
    public void ATurnOnTheSpotIsReportedToo()
    {
        // The whole point of this property. A turn changes what the other side should
        // answer about who is in front of whom, and it used to change it on one machine
        // only — so a player who walked up to somebody from the side and turned to face
        // them was, as far as the server knew, still looking the way they arrived.
        // Facing starts Down, so this both turns and is blocked.
        WalkingCharacter character = At(2, 3);
        character.Update(1f / 60f, Direction.Up);   // (2,2) is a wall

        Assert.False(character.IsStepping);
        Assert.Equal(Direction.Up, character.ToReport);
    }

    [Fact]
    public void HoldingADirectionAgainstAWallIsOneTurnAndThenNothing()
    {
        // Sixty frames a second of "still facing down" is not news, and the interval
        // that keeps a player from running would start refusing it.
        WalkingCharacter character = At(2, 3);

        character.Update(1f / 60f, Direction.Up);
        Assert.Equal(Direction.Up, character.ToReport);

        for (int frame = 0; frame < 30; frame++)
        {
            character.Update(1f / 60f, Direction.Up);
            Assert.Null(character.ToReport);
        }
    }

    [Fact]
    public void NothingHappeningIsNotReported()
    {
        WalkingCharacter character = At(1, 1);

        // Mid-step frames included: the step was reported when it began.
        character.Update(1f / 60f, Direction.Right);
        character.Update(1f / 60f, null);

        Assert.True(character.IsStepping);
        Assert.Null(character.ToReport);
    }

    [Fact]
    public void WillNotWalkOffTheMap()
    {
        WalkingCharacter character = At(0, 0);
        RunStepToCompletion(character, Direction.Left);

        Assert.Equal(new GridPosition(0, 0), character.Square);
        Assert.Equal(0, character.StepsTaken);
    }

    [Fact]
    public void StaysPutWithNoInput()
    {
        WalkingCharacter character = At(1, 1);

        for (int i = 0; i < 10; i++) character.Update(1f / 60f, null);

        Assert.Equal(new GridPosition(1, 1), character.Square);
        Assert.Equal(0, character.StepsTaken);
    }

    [Fact]
    public void WalksAPathOfSeveralSteps()
    {
        WalkingCharacter character = At(0, 0);

        foreach (Direction direction in new[] { Direction.Right, Direction.Right, Direction.Down })
            RunStepToCompletion(character, direction);

        Assert.Equal(new GridPosition(2, 1), character.Square);
        Assert.Equal(3, character.StepsTaken);
    }

    [Fact]
    public void ASlowFrameStillLandsExactlyOnTheSquare()
    {
        // A long frame must not overshoot into a fractional position.
        WalkingCharacter character = At(1, 1);
        character.Update(5f, Direction.Right);

        Assert.False(character.IsStepping);
        Assert.Equal((32f, 16f), character.PixelPosition);
    }
}
