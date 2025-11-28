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

    public int TotalCellsX => MazeSizeInChunks.x * ChunkSize;
    public int TotalCellsZ => MazeSizeInChunks.y * ChunkSize;

    public MazeData(int chunkSize, Vector2Int mazeSizeInChunks)
    {
        ChunkSize = chunkSize;
        MazeSizeInChunks = mazeSizeInChunks;
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

    // Новый метод для проверки стены между двумя клетками
    // Добавьте этот метод в класс MazeData:

    public bool HasWallBetween(Vector2Int fromGlobal, Vector2Int toGlobal)
    {
        // Получаем координаты чанков и ячеек
        Vector2Int fromChunk = new Vector2Int(fromGlobal.x / ChunkSize, fromGlobal.y / ChunkSize);
        Vector2Int fromCell = new Vector2Int(fromGlobal.x % ChunkSize, fromGlobal.y % ChunkSize);
        Vector2Int toChunk = new Vector2Int(toGlobal.x / ChunkSize, toGlobal.y / ChunkSize);
        Vector2Int toCell = new Vector2Int(toGlobal.x % ChunkSize, toGlobal.y % ChunkSize);

        // Проверяем что чанки существуют
        if (!ChunkExists(fromChunk.x, fromChunk.y) || !ChunkExists(toChunk.x, toChunk.y))
        {
            Debug.LogWarning($"🔴 Chunk doesn't exist: from {fromChunk} to {toChunk}");
            return true; // Стена есть если чанка не существует
        }

        MazeChunk chunk = GetChunk(fromChunk.x, fromChunk.y);
        if (chunk == null) return true;

        Vector2Int direction = toGlobal - fromGlobal;

        // Проверяем все возможные направления движения
        if (direction == Vector2Int.up) // Вперед (Z+)
        {
            return chunk.HorizontalWalls[fromCell.x, fromCell.y + 1];
        }
        else if (direction == Vector2Int.down) // Назад (Z-)
        {
            return chunk.HorizontalWalls[fromCell.x, fromCell.y];
        }
        else if (direction == Vector2Int.right) // Вправо (X+)
        {
            return chunk.VerticalWalls[fromCell.x + 1, fromCell.y];
        }
        else if (direction == Vector2Int.left) // Влево (X-)
        {
            return chunk.VerticalWalls[fromCell.x, fromCell.y];
        }

        Debug.LogError($"🎯 Invalid direction: {direction}");
        return true;
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
                HorizontalWalls[x, y] = false;
        }

        public void RemoveVerticalWall(int x, int y)
        {
            if (x >= 0 && x <= Size && y >= 0 && y < Size)
                VerticalWalls[x, y] = false;
        }
    }
}