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
