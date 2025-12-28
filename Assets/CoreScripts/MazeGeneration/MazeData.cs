using System.Collections.Generic;
using UnityEngine;

public class MazeData
{
    public int ChunkSize { get; private set; }
    public Vector2Int MazeSizeInChunks { get; private set; }
    public MazeChunk[,] Chunks { get; private set; }
    public Vector2Int StartGenerationChunk { get; private set; }
    public Vector2Int StartGenerationCell { get; private set; }
    public List<Vector2Int> StartGenerationCells { get; private set; }
    public int Seed { get; private set; } // Добавлен Seed

    public int TotalCellsX => MazeSizeInChunks.x * ChunkSize;
    public int TotalCellsZ => MazeSizeInChunks.y * ChunkSize;

    // Обновленный конструктор с поддержкой seed
    public MazeData(int chunkSize, Vector2Int mazeSizeInChunks, int seed = 0)
    {
        ChunkSize = chunkSize;
        MazeSizeInChunks = mazeSizeInChunks;
        Seed = seed;
        StartGenerationCells = new List<Vector2Int>();
        Chunks = new MazeChunk[mazeSizeInChunks.x, mazeSizeInChunks.y];
    }

    public void Initialize()
    {
        CalculateStartGenerationPoint();
        InitializeChunks();
    }

    private void CalculateStartGenerationPoint()
    {
        StartGenerationChunk = new Vector2Int(MazeSizeInChunks.x / 2, MazeSizeInChunks.y / 2);
        StartGenerationCell = new Vector2Int(ChunkSize / 2, ChunkSize / 2);
    }

    public void SetFinishAreaInCorner()
    {
        // Устанавливаем финиш в правом верхнем углу (противоположном от машинки)
        StartGenerationChunk = new Vector2Int(MazeSizeInChunks.x - 1, MazeSizeInChunks.y - 1);
        StartGenerationCell = new Vector2Int(ChunkSize - 2, ChunkSize - 2);
    }

    private void InitializeChunks()
    {
        for (int chunkX = 0; chunkX < MazeSizeInChunks.x; chunkX++)
        {
            for (int chunkZ = 0; chunkZ < MazeSizeInChunks.y; chunkZ++)
            {
                Chunks[chunkX, chunkZ] = new MazeChunk(ChunkSize, new Vector2Int(chunkX, chunkZ));
            }
        }
    }

    public bool IsValidCell(int chunkX, int chunkZ, int x, int y)
    {
        if (!ChunkExists(chunkX, chunkZ))
            return false;
        return !Chunks[chunkX, chunkZ].Visited[x, y];
    }

    public bool ChunkExists(int chunkX, int chunkZ)
    {
        return chunkX >= 0 && chunkX < MazeSizeInChunks.x &&
               chunkZ >= 0 && chunkZ < MazeSizeInChunks.y;
    }

    public MazeChunk GetChunk(int chunkX, int chunkZ)
    {
        return ChunkExists(chunkX, chunkZ) ? Chunks[chunkX, chunkZ] : null;
    }

    public bool HasWallBetween(Vector2Int fromGlobal, Vector2Int toGlobal)
    {
        if (fromGlobal == toGlobal)
        {
            return false;
        }

        Vector2Int direction = toGlobal - fromGlobal;

        if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
        {
            return true;
        }

        bool wallFromAtoB = CheckWallInDirection(fromGlobal, direction);
        bool wallFromBtoA = CheckWallInDirection(toGlobal, -direction);

        bool hasWall = wallFromAtoB || wallFromBtoA;

        // Debug.Log убран для производительности - вызывается очень часто
        // Раскомментируйте для отладки:
        // Debug.Log($"🔍 Wall check: {fromGlobal} -> {toGlobal} Result: {(hasWall ? "BLOCKED" : "ALLOWED")}");

        return hasWall;
    }

    public bool CheckWallInDirection(Vector2Int globalPos, Vector2Int direction)
    {
        // Правильное вычисление для отрицательных координат
        // Используем Mathf.FloorToInt для правильного деления отрицательных чисел
        int chunkX = Mathf.FloorToInt((float)globalPos.x / ChunkSize);
        int chunkZ = Mathf.FloorToInt((float)globalPos.y / ChunkSize);
        Vector2Int chunkPos = new Vector2Int(chunkX, chunkZ);
        
        // Правильное вычисление cellPos для отрицательных координат
        // Используем формулу: (x % size + size) % size для корректной работы с отрицательными числами
        int cellX = ((globalPos.x % ChunkSize) + ChunkSize) % ChunkSize;
        int cellZ = ((globalPos.y % ChunkSize) + ChunkSize) % ChunkSize;
        Vector2Int cellPos = new Vector2Int(cellX, cellZ);

        if (!ChunkExists(chunkPos.x, chunkPos.y))
            return true;

        var chunk = GetChunk(chunkPos.x, chunkPos.y);
        if (chunk == null) return true;

        if (direction == Vector2Int.up)
        {
            return chunk.HorizontalWalls[cellPos.x, cellPos.y + 1];
        }
        else if (direction == Vector2Int.down)
        {
            return chunk.HorizontalWalls[cellPos.x, cellPos.y];
        }
        else if (direction == Vector2Int.right)
        {
            return chunk.VerticalWalls[cellPos.x + 1, cellPos.y];
        }
        else if (direction == Vector2Int.left)
        {
            return chunk.VerticalWalls[cellPos.x, cellPos.y];
        }

        return true;
    }

    public void DebugAllWallsAround(Vector2Int globalPos)
    {
        Debug.Log($"🔍 ALL WALLS AROUND {globalPos}:");

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        string[] directionNames = { "UP", "RIGHT", "DOWN", "LEFT" };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int target = globalPos + directions[i];
            bool wallThere = CheckWallInDirection(globalPos, directions[i]);
            bool wallBack = CheckWallInDirection(target, -directions[i]);

            Debug.Log($"   {directionNames[i]}: A->B={wallThere}, B->A={wallBack}, SYMMETRIC={wallThere == wallBack}");
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

    public void ClearCache()
    {
    }
}

public class MazeChunk
{
    public bool[,] HorizontalWalls { get; private set; }
    public bool[,] VerticalWalls { get; private set; }
    public bool[,] Visited { get; private set; }
    public Vector2Int ChunkPosition { get; private set; }
    public int Size { get; private set; }

    public MazeChunk(int size, Vector2Int position)
    {
        Size = size;
        ChunkPosition = position;
        HorizontalWalls = new bool[size, size + 1];
        VerticalWalls = new bool[size + 1, size];
        Visited = new bool[size, size];
        InitializeWalls();
    }

    private void InitializeWalls()
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y <= Size; y++)
            {
                HorizontalWalls[x, y] = true;
            }
        }

        for (int x = 0; x <= Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                VerticalWalls[x, y] = true;
            }
        }
    }

    public void RemoveHorizontalWall(int x, int y)
    {
        if (x >= 0 && x < Size && y >= 0 && y <= Size)
        {
            HorizontalWalls[x, y] = false;
            // Debug.Log убран для производительности - вызывается очень часто
        }
        else
        {
            Debug.LogError($"❌ Invalid H wall coordinates: [{x}, {y}] in chunk {ChunkPosition}");
        }
    }

    public void RemoveVerticalWall(int x, int y)
    {
        if (x >= 0 && x <= Size && y >= 0 && y < Size)
        {
            VerticalWalls[x, y] = false;
            // Debug.Log убран для производительности - вызывается очень часто
        }
        else
        {
            Debug.LogError($"❌ Invalid V wall coordinates: [{x}, {y}] in chunk {ChunkPosition}");
        }
    }
}