using System.Collections.Generic;
using UnityEngine;

public class NodeGenerator
{
    private MazeGenerator generator;
    private List<GameObject> nodes;
    private GameObject nodesParent;

    public NodeGenerator(MazeGenerator mazeGenerator)
    {
        generator = mazeGenerator;
        nodes = new List<GameObject>();
    }

    public void CreateNodes()
    {
        if (generator.nodePrefab == null)
        {
            Debug.LogWarning("Node prefab не назначен! Ноды не будут созданы.");
            return;
        }

        Clear();
        nodesParent = new GameObject("MazeNodes");
        var mazeData = generator.GetMazeData();

        if (mazeData.Chunks == null)
        {
            Debug.LogError("MazeData не инициализирована! Сначала вызовите GenerateMaze()");
            return;
        }

        int totalNodes = 0;
        int createdNodes = 0;

        // Сначала подсчитаем общее количество нодов
        for (int chunkX = 0; chunkX < mazeData.MazeSizeInChunks.x; chunkX++)
        {
            for (int chunkZ = 0; chunkZ < mazeData.MazeSizeInChunks.y; chunkZ++)
            {
                totalNodes += mazeData.ChunkSize * mazeData.ChunkSize;
            }
        }

        Debug.Log($"🔄 Creating {totalNodes} nodes...");

        // Создаем ноды
        for (int chunkX = 0; chunkX < mazeData.MazeSizeInChunks.x; chunkX++)
        {
            for (int chunkZ = 0; chunkZ < mazeData.MazeSizeInChunks.y; chunkZ++)
            {
                // Проверяем существование чанка
                if (mazeData.Chunks[chunkX, chunkZ] == null)
                {
                    Debug.LogWarning($"Чанк [{chunkX}, {chunkZ}] не инициализирован");
                    continue;
                }

                for (int cellX = 0; cellX < mazeData.ChunkSize; cellX++)
                {
                    for (int cellZ = 0; cellZ < mazeData.ChunkSize; cellZ++)
                    {
                        if (CreateNode(chunkX, chunkZ, cellX, cellZ))
                        {
                            createdNodes++;
                        }
                        totalNodes++;
                    }
                }
            }
        }

        Debug.Log($"✅ Создано нодов: {createdNodes} (чанков: {mazeData.MazeSizeInChunks.x * mazeData.MazeSizeInChunks.y}, ячеек: {mazeData.ChunkSize * mazeData.ChunkSize})");

        // Валидация нодов
        ValidateNodes();
    }

    private bool CreateNode(int chunkX, int chunkZ, int cellX, int cellZ)
    {
        try
        {
            Vector3 nodePosition = generator.GetCellWorldPosition(chunkX, chunkZ, cellX, cellZ);

            // Небольшое смещение вверх чтобы ноды не были в полу
            nodePosition += Vector3.up * 0.1f;

            GameObject node = Object.Instantiate(generator.nodePrefab, nodePosition, Quaternion.identity, nodesParent.transform);
            node.name = $"Node_Chunk({chunkX},{chunkZ})_Cell({cellX},{cellZ})";

            NodeInfo nodeInfo = node.GetComponent<NodeInfo>() ?? node.AddComponent<NodeInfo>();
            nodeInfo.SetCoordinates(chunkX, chunkZ, cellX, cellZ);

            // Добавляем коллайдер для визуализации и отладки
            SphereCollider collider = node.GetComponent<SphereCollider>() ?? node.AddComponent<SphereCollider>();
            collider.radius = 0.2f;
            collider.isTrigger = true;

            nodes.Add(node);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка создания нода в [{chunkX},{chunkZ}] [{cellX},{cellZ}]: {e.Message}");
            return false;
        }
    }

    private void ValidateNodes()
    {
        // Проверяем что все ноды созданы правильно
        NodeInfo[] allNodes = Object.FindObjectsOfType<NodeInfo>();
        Dictionary<Vector2Int, NodeInfo> nodeMap = new Dictionary<Vector2Int, NodeInfo>();
        var mazeData = generator.GetMazeData();

        int duplicateNodes = 0;
        int missingNodes = 0;

        foreach (NodeInfo node in allNodes)
        {
            Vector2Int globalPos = new Vector2Int(
                node.chunkX * mazeData.ChunkSize + node.cellX,
                node.chunkZ * mazeData.ChunkSize + node.cellZ
            );

            if (nodeMap.ContainsKey(globalPos))
            {
                duplicateNodes++;
                Debug.LogWarning($"🔴 Дубликат нода: {globalPos} - {node.name} и {nodeMap[globalPos].name}");
            }
            else
            {
                nodeMap[globalPos] = node;
            }
        }

        // Проверяем отсутствующие ноды
        for (int globalX = 0; globalX < mazeData.TotalCellsX; globalX++)
        {
            for (int globalZ = 0; globalZ < mazeData.TotalCellsZ; globalZ++)
            {
                Vector2Int globalPos = new Vector2Int(globalX, globalZ);
                if (!nodeMap.ContainsKey(globalPos))
                {
                    missingNodes++;
                    Debug.LogWarning($"🔴 Отсутствует нод: {globalPos}");
                }
            }
        }

        if (duplicateNodes > 0 || missingNodes > 0)
        {
            Debug.LogError($"❌ Проблемы с нодами: Дубликаты: {duplicateNodes}, Отсутствуют: {missingNodes}");
        }
        else
        {
            Debug.Log("✅ Все ноды созданы корректно");
        }
    }

    public void Clear()
    {
        if (nodesParent != null)
        {
            if (Application.isPlaying)
                Object.Destroy(nodesParent);
            else
                Object.DestroyImmediate(nodesParent);
        }
        nodes.Clear();
    }
}