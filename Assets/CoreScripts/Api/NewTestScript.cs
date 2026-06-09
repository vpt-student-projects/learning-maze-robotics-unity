using NUnit.Framework;
using UnityEditor.Overlays;
using UnityEngine;

public class NewTestScript
{
    [Test]
    public void MazeData_Initialize_CreatesChunksAndCalculatesSize()
    {
        int chunkSize = 4;
        Vector2Int mazeSize = new Vector2Int(3, 3);
        int seed = 123;

        MazeData mazeData = new MazeData(chunkSize, mazeSize, seed);

        mazeData.Initialize();

        Assert.AreEqual(chunkSize, mazeData.ChunkSize);
        Assert.AreEqual(mazeSize, mazeData.MazeSizeInChunks);
        Assert.AreEqual(seed, mazeData.Seed);
        Assert.AreEqual(12, mazeData.TotalCellsX);
        Assert.AreEqual(12, mazeData.TotalCellsZ);
        Assert.IsNotNull(mazeData.GetChunk(0, 0));
        Assert.IsNotNull(mazeData.GetChunk(2, 2));
    }

    [Test]
    public void MazeChunk_RemoveWalls_ChangesWallValuesToFalse()
    {
        MazeChunk chunk = new MazeChunk(4, Vector2Int.zero);

        chunk.RemoveHorizontalWall(1, 2);
        chunk.RemoveVerticalWall(2, 1);

        Assert.IsFalse(chunk.HorizontalWalls[1, 2]);
        Assert.IsFalse(chunk.VerticalWalls[2, 1]);
        Assert.IsTrue(chunk.HorizontalWalls[0, 0]);
        Assert.IsTrue(chunk.VerticalWalls[0, 0]);
    }

    [Test]
    public void MazeData_HasWallBetween_ReturnsFalseAfterWallRemoved()
    {
        MazeData mazeData = new MazeData(4, new Vector2Int(1, 1), 1);
        mazeData.Initialize();

        MazeChunk chunk = mazeData.GetChunk(0, 0);

        Vector2Int firstCell = new Vector2Int(0, 0);
        Vector2Int secondCell = new Vector2Int(1, 0);

        Assert.IsTrue(mazeData.HasWallBetween(firstCell, secondCell));

        chunk.RemoveVerticalWall(1, 0);

        Assert.IsFalse(mazeData.HasWallBetween(firstCell, secondCell));
    }
}