using PokeMmo.Core.World;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Tests;

public class GridPositionTests
{
    [Theory]
    [InlineData(Direction.Up, 5, 4)]
    [InlineData(Direction.Down, 5, 6)]
    [InlineData(Direction.Left, 4, 5)]
    [InlineData(Direction.Right, 6, 5)]
    public void StepsOneSquareInEachDirection(Direction direction, int expectedX, int expectedY)
    {
        GridPosition stepped = new GridPosition(5, 5).Step(direction);

        Assert.Equal(expectedX, stepped.X);
        Assert.Equal(expectedY, stepped.Y);
    }

    [Fact]
    public void UpIsNegativeYBecauseRowsRunDownwards()
    {
        // Worth pinning: the map's rows are stored top to bottom, so "up" decreases Y.
        // Getting this backwards produces a game that moves the wrong way.
        Assert.Equal(new GridPosition(0, -1), new GridPosition(0, 0).Step(Direction.Up));
    }
}

public class CollisionGridTests
{
    /// <summary>A 4x3 grid with a wall down the middle column.</summary>
    private static CollisionGrid Walled() => new(4, 3,
    [
        0, 0, 1, 0,
        0, 0, 1, 0,
        0, 0, 1, 0,
    ]);

    [Fact]
    public void ZeroIsWalkableAndAnythingElseIsNot()
    {
        CollisionGrid grid = Walled();

        Assert.True(grid.IsWalkable(new GridPosition(1, 1)));
        Assert.False(grid.IsWalkable(new GridPosition(2, 1)));
    }

    [Fact]
    public void TreatsEverythingOutsideTheMapAsBlocked()
    {
        CollisionGrid grid = Walled();

        Assert.False(grid.IsWalkable(new GridPosition(-1, 0)));
        Assert.False(grid.IsWalkable(new GridPosition(0, -1)));
        Assert.False(grid.IsWalkable(new GridPosition(4, 0)));
        Assert.False(grid.IsWalkable(new GridPosition(0, 3)));
    }

    [Fact]
    public void StepsIntoAnOpenSquare()
    {
        Assert.True(Walled().TryStep(new GridPosition(0, 0), Direction.Right, out GridPosition to));
        Assert.Equal(new GridPosition(1, 0), to);
    }

    [Fact]
    public void RefusesToStepIntoAWallAndStaysPut()
    {
        Assert.False(Walled().TryStep(new GridPosition(1, 1), Direction.Right, out GridPosition to));
        Assert.Equal(new GridPosition(1, 1), to);
    }

    [Fact]
    public void RefusesToStepOffTheEdge()
    {
        Assert.False(Walled().TryStep(new GridPosition(0, 0), Direction.Up, out GridPosition to));
        Assert.Equal(new GridPosition(0, 0), to);
    }

    [Fact]
    public void FindsSomewhereToStand()
    {
        CollisionGrid blockedTopLeft = new(2, 2, [1, 0, 0, 0]);
        Assert.Equal(new GridPosition(1, 0), blockedTopLeft.FirstWalkable());
    }

    [Fact]
    public void RejectsMismatchedDimensions()
    {
        Assert.Throws<ArgumentException>(() => new CollisionGrid(4, 4, new byte[4]));
        Assert.Throws<ArgumentException>(() => new CollisionGrid(0, 4, new byte[16]));
    }
}

public class MapCollisionExtractionTests
{
    private static readonly SyntheticRom Synthetic = new();

    [Fact]
    public void BuildsAGridMatchingTheMapDimensions()
    {
        Rom rom = Synthetic.ToRom();
        MapLayoutRecord layout = MapLocator.Locate(rom)!.Valid.First().Layout;

        CollisionGrid grid = layout.ReadCollision(rom);

        Assert.Equal(SyntheticRom.MapWidth, grid.Width);
        Assert.Equal(SyntheticRom.MapHeight, grid.Height);
    }

    [Fact]
    public void TakesWalkabilityFromEachBlocksCollisionBits()
    {
        Rom rom = Synthetic.ToRom();
        MapLayoutRecord layout = MapLocator.Locate(rom)!.Valid.First().Layout;

        CollisionGrid grid = layout.ReadCollision(rom);
        ushort[] blocks = layout.ReadBlocks(rom);

        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                int expected = new MapBlock(blocks[y * layout.Width + x]).Collision;
                Assert.Equal(expected, grid.CollisionAt(new GridPosition(x, y)));
            }
        }
    }
}
