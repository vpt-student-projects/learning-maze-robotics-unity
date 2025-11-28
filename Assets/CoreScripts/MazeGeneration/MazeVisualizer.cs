using UnityEngine;

public class MazeVisualizer
{
    private MazeData mazeData;
    private MazeGenerator generator;
    private GameObject mazeParent;

    public MazeVisualizer(MazeData data, MazeGenerator mazeGenerator)
    {
        mazeData = data;
        generator = mazeGenerator;
    }

    public void CreateMazeVisuals()
    {
        mazeParent = new GameObject("Maze");
        CreateFloor();
        CreateWalls();

        if (generator.createFinishArea && generator.finishPrefab != null)
        {
            CreateFinishVisual();
        }

        Debug.Log($"Визуализация создана: {mazeData.MazeSizeInChunks.x}x{mazeData.MazeSizeInChunks.y} чанков");
    }

    private void CreateFloor()
    {
        float totalWidth = generator.GetTotalWidth();
        float totalDepth = generator.GetTotalDepth();

        Vector3 floorPosition = new Vector3(
            totalWidth * 0.5f - generator.cellSize * 0.5f + generator.wallOffset.x,
            -0.5f + generator.wallOffset.y,
            totalDepth * 0.5f - generator.cellSize * 0.5f + generator.wallOffset.z
        );

        GameObject floor = Object.Instantiate(generator.floorPrefab, mazeParent.transform);
        floor.transform.localScale = new Vector3(totalWidth, 1f, totalDepth);
        floor.transform.position = floorPosition;
        floor.name = "MazeFloor";

        // Устанавливаем слой "Floor"
        floor.layer = LayerMask.NameToLayer("Floor");

        // Если у пола есть дочерние объекты, тоже устанавливаем им слой Floor
        SetLayerRecursively(floor, LayerMask.NameToLayer("Floor"));

        Debug.Log($"Пол создан: {totalWidth:F1}x{totalDepth:F1} (слой: {LayerMask.LayerToName(floor.layer)})");
    }

    private void CreateWalls()
    {
        int wallCount = 0;

        for (int chunkX = 0; chunkX < mazeData.MazeSizeInChunks.x; chunkX++)
        {
            for (int chunkZ = 0; chunkZ < mazeData.MazeSizeInChunks.y; chunkZ++)
            {
                var chunk = mazeData.GetChunk(chunkX, chunkZ);
                if (chunk == null)
                {
                    Debug.LogWarning($"Чанк [{chunkX},{chunkZ}] null при создании стен");
                    continue;
                }

                wallCount += CreateChunkWalls(chunkX, chunkZ);
            }
        }

        Debug.Log($"Создано стен: {wallCount} (слой: Wall)");
    }

    private int CreateChunkWalls(int chunkX, int chunkZ)
    {
        var chunk = mazeData.GetChunk(chunkX, chunkZ);
        Vector3 chunkBaseOffset = new Vector3(
            chunkX * (mazeData.ChunkSize * generator.cellSize + generator.chunkOffset.x) + generator.wallOffset.x,
            generator.wallOffset.y,
            chunkZ * (mazeData.ChunkSize * generator.cellSize + generator.chunkOffset.z) + generator.wallOffset.z
        );

        int wallCount = 0;

        // Создание горизонтальных стен
        for (int x = 0; x < mazeData.ChunkSize; x++)
        {
            for (int y = 0; y <= mazeData.ChunkSize; y++)
            {
                if (chunk.HorizontalWalls[x, y])
                {
                    CreateWall(chunkBaseOffset, x, y, true);
                    wallCount++;
                }
            }
        }

        // Создание вертикальных стен
        for (int x = 0; x <= mazeData.ChunkSize; x++)
        {
            for (int y = 0; y < mazeData.ChunkSize; y++)
            {
                if (chunk.VerticalWalls[x, y])
                {
                    CreateWall(chunkBaseOffset, x, y, false);
                    wallCount++;
                }
            }
        }

        return wallCount;
    }

    private void CreateWall(Vector3 chunkBaseOffset, int x, int y, bool isHorizontal)
    {
        Vector3 position = chunkBaseOffset;

        if (isHorizontal)
        {
            position += new Vector3(
                x * (generator.cellSize + generator.cellOffset.x),
                generator.wallHeight * 0.5f,
                y * (generator.cellSize + generator.cellOffset.z) - generator.cellSize * 0.5f
            );
        }
        else
        {
            position += new Vector3(
                x * (generator.cellSize + generator.cellOffset.x) - generator.cellSize * 0.5f,
                generator.wallHeight * 0.5f,
                y * (generator.cellSize + generator.cellOffset.z)
            );
        }

        GameObject wall = Object.Instantiate(generator.wallPrefab, position, Quaternion.identity, mazeParent.transform);

        if (isHorizontal)
            wall.transform.localScale = new Vector3(generator.cellSize + generator.cellOffset.x, generator.wallHeight, generator.wallThickness);
        else
            wall.transform.localScale = new Vector3(generator.wallThickness, generator.wallHeight, generator.cellSize + generator.cellOffset.z);

        // Устанавливаем слой "Wall"
        wall.layer = LayerMask.NameToLayer("Wall");

        // Если у стены есть дочерние объекты, тоже устанавливаем им слой Wall
        SetLayerRecursively(wall, LayerMask.NameToLayer("Wall"));

        // Переименовываем для удобства отладки
        string wallType = isHorizontal ? "Horizontal" : "Vertical";
        wall.name = $"Wall_{wallType}_Chunk({chunkBaseOffset.x / (mazeData.ChunkSize * generator.cellSize):F0},{chunkBaseOffset.z / (mazeData.ChunkSize * generator.cellSize):F0})_Pos({x},{y})";
    }

    private void CreateFinishVisual()
    {
        Vector3 finishPosition = generator.GetCellWorldPosition(
            mazeData.StartGenerationChunk.x,
            mazeData.StartGenerationChunk.y,
            mazeData.StartGenerationCell.x - 1,
            mazeData.StartGenerationCell.y - 1
        );

        finishPosition += new Vector3(generator.cellSize, 0, generator.cellSize);
        GameObject finish = Object.Instantiate(generator.finishPrefab, finishPosition, Quaternion.identity, mazeParent.transform);
        finish.name = "FinishArea";
        finish.transform.localScale = new Vector3(generator.cellSize * 2, 1, generator.cellSize * 2);

        // Устанавливаем слой "Floor" для финишной зоны
        finish.layer = LayerMask.NameToLayer("Floor");
        SetLayerRecursively(finish, LayerMask.NameToLayer("Floor"));
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;

        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }

    public void Clear()
    {
        if (mazeParent != null)
        {
            if (Application.isPlaying)
                Object.Destroy(mazeParent);
            else
                Object.DestroyImmediate(mazeParent);
        }
    }
}