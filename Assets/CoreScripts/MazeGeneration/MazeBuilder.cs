using System.Collections.Generic;
using UnityEngine;

public class MazeBuilder
{
    private MazeData mazeData;
    private MazeGenerator generator;
    private MazeVisualizer visualizer;
    private System.Random random;

    public MazeBuilder(MazeData data, MazeGenerator mazeGenerator)
    {
        mazeData = data;
        generator = mazeGenerator;
        visualizer = new MazeVisualizer(data, mazeGenerator);
        random = new System.Random(mazeData.Seed); // Используем seed
    }

    public void Generate()
    {
        Debug.Log($"🚀 Starting maze generation with seed {mazeData.Seed}...");

        if (generator.createFinishArea)
        {
            Debug.Log("🎯 Creating finish area in middle...");
            CreateFinishAreaInMiddle();
        }
        else if (generator.createFinishAreaInCorner)
        {
            Debug.Log("🎯 Creating finish area in corner...");
            CreateFinishAreaInCorner();
        }

        Debug.Log("🔨 Generating maze structure...");
        GenerateMazeFromStartPoint();

        Debug.Log("🚪 Removing boundary walls between chunks...");
        RemoveBoundaryWallsBetweenChunks();

        Debug.Log("🎨 Creating maze visuals...");
        visualizer.CreateMazeVisuals();

        Debug.Log("✅ Maze generation completed!");
    }

    private void CreateFinishAreaInMiddle()
    {
        int centerChunkX = mazeData.StartGenerationChunk.x;
        int centerChunkZ = mazeData.StartGenerationChunk.y;
        int centerCellX = mazeData.StartGenerationCell.x;
        int centerCellY = mazeData.StartGenerationCell.y;

        Debug.Log($"🎯 Finish area in middle at Chunk({centerChunkX},{centerChunkZ}) Cell({centerCellX},{centerCellY})");

        mazeData.StartGenerationCells.Clear();
        var chunk = mazeData.Chunks[centerChunkX, centerChunkZ];

        if (chunk == null)
        {
            Debug.LogError($"❌ Chunk not found at ({centerChunkX},{centerChunkZ})");
            return;
        }

        for (int offsetX = 0; offsetX < 2; offsetX++)
        {
            for (int offsetY = 0; offsetY < 2; offsetY++)
            {
                int cellX = centerCellX - 1 + offsetX;
                int cellY = centerCellY - 1 + offsetY;

                if (cellX >= 0 && cellX < mazeData.ChunkSize && cellY >= 0 && cellY < mazeData.ChunkSize)
                {
                    mazeData.StartGenerationCells.Add(new Vector2Int(cellX, cellY));
                    Debug.Log($"   ✅ Added finish cell: ({cellX},{cellY})");

                    if (offsetX == 0 && offsetY == 0)
                    {
                        chunk.RemoveVerticalWall(cellX + 1, cellY);
                        chunk.RemoveHorizontalWall(cellX, cellY + 1);
                    }
                    else if (offsetX == 1 && offsetY == 0)
                    {
                        chunk.RemoveVerticalWall(cellX, cellY);
                        chunk.RemoveHorizontalWall(cellX, cellY + 1);
                    }
                    else if (offsetX == 0 && offsetY == 1)
                    {
                        chunk.RemoveVerticalWall(cellX + 1, cellY);
                        chunk.RemoveHorizontalWall(cellX, cellY);
                    }
                    else if (offsetX == 1 && offsetY == 1)
                    {
                        chunk.RemoveVerticalWall(cellX, cellY);
                        chunk.RemoveHorizontalWall(cellX, cellY);
                    }

                    chunk.Visited[cellX, cellY] = true;
                }
            }
        }
    }

    private void CreateFinishAreaInCorner()
    {
        // Финиш в правом верхнем углу (противоположном от машинки)
        // Машинка стартует в (0,0) chunk, (0,0) cell
        // Финиш будет в (maxX, maxZ) chunk, (chunkSize-2, chunkSize-2) to (chunkSize-1, chunkSize-1) cell
        mazeData.SetFinishAreaInCorner();
        
        int cornerChunkX = mazeData.StartGenerationChunk.x;
        int cornerChunkZ = mazeData.StartGenerationChunk.y;
        int cornerCellX = mazeData.StartGenerationCell.x;
        int cornerCellY = mazeData.StartGenerationCell.y;

        Debug.Log($"🎯 Finish area in corner at Chunk({cornerChunkX},{cornerChunkZ}) Cell({cornerCellX},{cornerCellY})");

        mazeData.StartGenerationCells.Clear();

        var chunk = mazeData.Chunks[cornerChunkX, cornerChunkZ];

        if (chunk == null)
        {
            Debug.LogError($"❌ Chunk not found at ({cornerChunkX},{cornerChunkZ})");
            return;
        }

        for (int offsetX = 0; offsetX < 2; offsetX++)
        {
            for (int offsetY = 0; offsetY < 2; offsetY++)
            {
                int cellX = cornerCellX + offsetX;
                int cellY = cornerCellY + offsetY;

                if (cellX >= 0 && cellX < mazeData.ChunkSize && cellY >= 0 && cellY < mazeData.ChunkSize)
                {
                    mazeData.StartGenerationCells.Add(new Vector2Int(cellX, cellY));
                    Debug.Log($"   ✅ Added finish cell: ({cellX},{cellY})");

                    if (offsetX == 0 && offsetY == 0)
                    {
                        chunk.RemoveVerticalWall(cellX + 1, cellY);
                        chunk.RemoveHorizontalWall(cellX, cellY + 1);
                    }
                    else if (offsetX == 1 && offsetY == 0)
                    {
                        chunk.RemoveVerticalWall(cellX, cellY);
                        chunk.RemoveHorizontalWall(cellX, cellY + 1);
                    }
                    else if (offsetX == 0 && offsetY == 1)
                    {
                        chunk.RemoveVerticalWall(cellX + 1, cellY);
                        chunk.RemoveHorizontalWall(cellX, cellY);
                    }
                    else if (offsetX == 1 && offsetY == 1)
                    {
                        chunk.RemoveVerticalWall(cellX, cellY);
                        chunk.RemoveHorizontalWall(cellX, cellY);
                    }

                    chunk.Visited[cellX, cellY] = true;
                }
            }
        }
    }

    private void GenerateMazeFromStartPoint()
    {
        if (generator.createFinishArea || generator.createFinishAreaInCorner)
        {
            foreach (var startCell in mazeData.StartGenerationCells)
            {
                GenerateMazeRecursive(mazeData.StartGenerationChunk.x, mazeData.StartGenerationChunk.y, startCell.x, startCell.y);
            }
        }
        else
        {
            GenerateMazeRecursive(mazeData.StartGenerationChunk.x, mazeData.StartGenerationChunk.y,
                                mazeData.StartGenerationCell.x, mazeData.StartGenerationCell.y);
        }

        EnsureAllCellsConnected();
    }

    private void GenerateMazeRecursive(int chunkX, int chunkZ, int x, int y)
    {
        var chunk = mazeData.GetChunk(chunkX, chunkZ);
        if (chunk == null) return;

        chunk.Visited[x, y] = true;

        Vector2Int[] directions = GetRandomDirections();

        foreach (var direction in directions)
        {
            var newPos = GetNewCellPosition(chunkX, chunkZ, x, y, direction);

            if (mazeData.IsValidCell(newPos.chunkX, newPos.chunkZ, newPos.cellX, newPos.cellY))
            {
                RemoveWall(chunkX, chunkZ, x, y, newPos.chunkX, newPos.chunkZ, newPos.cellX, newPos.cellY, direction);

                var newChunk = mazeData.GetChunk(newPos.chunkX, newPos.chunkZ);
                if (newChunk != null)
                {
                    newChunk.Visited[newPos.cellX, newPos.cellY] = true;
                    GenerateMazeRecursive(newPos.chunkX, newPos.chunkZ, newPos.cellX, newPos.cellY);
                }
            }
        }
    }

    private Vector2Int[] GetRandomDirections()
    {
        Vector2Int[] directions = new Vector2Int[] {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };
        ShuffleArray(directions);
        return directions;
    }

    private (int chunkX, int chunkZ, int cellX, int cellY) GetNewCellPosition(int chunkX, int chunkZ, int x, int y, Vector2Int direction)
    {
        int newChunkX = chunkX;
        int newChunkZ = chunkZ;
        int newX = x + direction.x;
        int newY = y + direction.y;

        if (newX < 0) { newChunkX--; newX = mazeData.ChunkSize - 1; }
        else if (newX >= mazeData.ChunkSize) { newChunkX++; newX = 0; }

        if (newY < 0) { newChunkZ--; newY = mazeData.ChunkSize - 1; }
        else if (newY >= mazeData.ChunkSize) { newChunkZ++; newY = 0; }

        return (newChunkX, newChunkZ, newX, newY);
    }

    private void RemoveWall(int chunkX, int chunkZ, int x, int y, int newChunkX, int newChunkZ, int newX, int newY, Vector2Int direction)
    {
        // Debug.Log убран для производительности - вызывается очень часто при генерации
        // Раскомментируйте для отладки:
        // Debug.Log($"🔨 Removing wall: Chunk({chunkX},{chunkZ})[({x},{y})] -> Chunk({newChunkX},{newChunkZ})[({newX},{newY})] Direction: {DirectionToString(direction)}");

        if (chunkX == newChunkX && chunkZ == newChunkZ)
        {
            var chunk = mazeData.GetChunk(chunkX, chunkZ);
            if (chunk != null)
            {
                if (direction == Vector2Int.right)
                {
                    chunk.RemoveVerticalWall(x + 1, y);
                }
                else if (direction == Vector2Int.left)
                {
                    chunk.RemoveVerticalWall(x, y);
                }
                else if (direction == Vector2Int.up)
                {
                    chunk.RemoveHorizontalWall(x, y + 1);
                }
                else if (direction == Vector2Int.down)
                {
                    chunk.RemoveHorizontalWall(x, y);
                }
            }
        }
        else
        {
            if (direction == Vector2Int.right)
            {
                var currentChunk = mazeData.GetChunk(chunkX, chunkZ);
                var rightChunk = mazeData.GetChunk(newChunkX, newChunkZ);
                if (currentChunk != null)
                {
                    currentChunk.RemoveVerticalWall(mazeData.ChunkSize, y);
                }
                if (rightChunk != null)
                {
                    rightChunk.RemoveVerticalWall(0, newY);
                }
            }
            else if (direction == Vector2Int.left)
            {
                var currentChunk = mazeData.GetChunk(chunkX, chunkZ);
                var leftChunk = mazeData.GetChunk(newChunkX, newChunkZ);
                if (currentChunk != null)
                {
                    currentChunk.RemoveVerticalWall(0, y);
                }
                if (leftChunk != null)
                {
                    leftChunk.RemoveVerticalWall(mazeData.ChunkSize, newY);
                }
            }
            else if (direction == Vector2Int.up)
            {
                var currentChunk = mazeData.GetChunk(chunkX, chunkZ);
                var topChunk = mazeData.GetChunk(newChunkX, newChunkZ);
                if (currentChunk != null)
                {
                    currentChunk.RemoveHorizontalWall(x, mazeData.ChunkSize);
                }
                if (topChunk != null)
                {
                    topChunk.RemoveHorizontalWall(newX, 0);
                }
            }
            else if (direction == Vector2Int.down)
            {
                var currentChunk = mazeData.GetChunk(chunkX, chunkZ);
                var bottomChunk = mazeData.GetChunk(newChunkX, newChunkZ);
                if (currentChunk != null)
                {
                    currentChunk.RemoveHorizontalWall(x, 0);
                }
                if (bottomChunk != null)
                {
                    bottomChunk.RemoveHorizontalWall(newX, mazeData.ChunkSize);
                }
            }
        }
    }

    private void RemoveBoundaryWallsBetweenChunks()
    {
        for (int chunkX = 0; chunkX < mazeData.MazeSizeInChunks.x; chunkX++)
        {
            for (int chunkZ = 0; chunkZ < mazeData.MazeSizeInChunks.y; chunkZ++)
            {
                var chunk = mazeData.GetChunk(chunkX, chunkZ);
                if (chunk == null) continue;

                if (chunkX < mazeData.MazeSizeInChunks.x - 1)
                {
                    for (int y = 0; y < mazeData.ChunkSize; y++)
                    {
                        chunk.RemoveVerticalWall(mazeData.ChunkSize, y);
                    }
                }

                if (chunkZ < mazeData.MazeSizeInChunks.y - 1)
                {
                    for (int x = 0; x < mazeData.ChunkSize; x++)
                    {
                        chunk.RemoveHorizontalWall(x, mazeData.ChunkSize);
                    }
                }
            }
        }
    }

    private void EnsureAllCellsConnected()
    {
        for (int chunkX = 0; chunkX < mazeData.MazeSizeInChunks.x; chunkX++)
        {
            for (int chunkZ = 0; chunkZ < mazeData.MazeSizeInChunks.y; chunkZ++)
            {
                var chunk = mazeData.GetChunk(chunkX, chunkZ);
                if (chunk == null) continue;

                for (int x = 0; x < mazeData.ChunkSize; x++)
                {
                    for (int y = 0; y < mazeData.ChunkSize; y++)
                    {
                        if (!chunk.Visited[x, y])
                        {
                            ConnectToNearestVisited(chunkX, chunkZ, x, y);
                        }
                    }
                }
            }
        }
    }

    private void ConnectToNearestVisited(int chunkX, int chunkZ, int x, int y)
    {
        var directions = new Vector2Int[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        foreach (var direction in directions)
        {
            var newPos = GetNewCellPosition(chunkX, chunkZ, x, y, direction);
            var neighborChunk = mazeData.GetChunk(newPos.chunkX, newPos.chunkZ);

            if (neighborChunk != null && neighborChunk.Visited[newPos.cellX, newPos.cellY])
            {
                RemoveWall(chunkX, chunkZ, x, y, newPos.chunkX, newPos.chunkZ, newPos.cellX, newPos.cellY, direction);

                var currentChunk = mazeData.GetChunk(chunkX, chunkZ);
                if (currentChunk != null)
                {
                    currentChunk.Visited[x, y] = true;
                }
                break;
            }
        }
    }

    private void ShuffleArray<T>(T[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int randomIndex = random.Next(0, i + 1);
            (array[i], array[randomIndex]) = (array[randomIndex], array[i]);
        }
    }

    private string DirectionToString(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return "UP";
        if (direction == Vector2Int.right) return "RIGHT";
        if (direction == Vector2Int.down) return "DOWN";
        if (direction == Vector2Int.left) return "LEFT";
        return "UNKNOWN";
    }

    public void Clear()
    {
        visualizer.Clear();
    }
}