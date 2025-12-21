using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{

    public bool isRecording = false;
    public bool isReplaying = false;
    private float recordStartTime;

    [Header("Настройки машинки")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 270f;
    public float nodeProximityThreshold = 0.1f;

    [Header("Анимация")]
    public float rotationAnimationTime = 0.07f;
    public float moveAnimationTime = 0.09f;

    [Header("Ссылки")]
    public GameObject carPrefab;
    public MazeGenerator mazeGenerator;
    public MazeTimer mazeTimer;

    [Header("Отладка")]
    public bool showDebugLines = false;
    public bool logMovements = true;

    private GameObject carInstance;
    private NodeInfo currentNode;
    private NodeInfo targetNode;
    private Vector3 targetPosition;
    public bool isMoving = false;
    private bool isRotating = false;
    private bool isInitialized = false;
    public CarRecorderAPI recorderAPI;

    private int currentDirection = 0;
    private Vector2Int[] directionVectors = {
        Vector2Int.up,    // вперед (Z+)
        Vector2Int.right, // вправо (X+)
        Vector2Int.down,  // назад (Z-)
        Vector2Int.left   // влево (X-)
    };

    private MazeData mazeData;
    private Dictionary<Vector2Int, NodeInfo> nodeMap;
    private Coroutine currentMovementCoroutine;

    public void InitializeCar(bool forceReinitialize = false)
    {
        if (isInitialized && !forceReinitialize) return;

        if (forceReinitialize)
        {
            ResetInternalState();
        }

        StartCoroutine(InitializeCarCoroutine());
    }

    // Полный сброс состояния, чтобы можно было безопасно переинициализировать машинку после регенерации лабиринта
    public void ResetInternalState()
    {
        // Останавливаем любые движения/инициализации
        try
        {
            StopAllCoroutines();
        }
        catch { /* ignore */ }

        if (currentMovementCoroutine != null)
        {
            try { StopCoroutine(currentMovementCoroutine); } catch { /* ignore */ }
            currentMovementCoroutine = null;
        }

        // Удаляем визуальный инстанс машинки, но оставляем контейнер GameObject с компонентами
        if (carInstance != null)
        {
            if (Application.isPlaying)
                Destroy(carInstance);
            else
                DestroyImmediate(carInstance);
            carInstance = null;
        }

        currentNode = null;
        targetNode = null;
        nodeMap = null;
        mazeData = null;

        isMoving = false;
        isRotating = false;
        isInitialized = false;
        currentDirection = 0;
    }

    private IEnumerator InitializeCarCoroutine()
    {
        if (mazeGenerator == null)
        {
            mazeGenerator = FindObjectOfType<MazeGenerator>();
            if (mazeGenerator == null)
            {
                yield break;
            }
        }

        // Инициализация таймера если не назначен
        if (mazeTimer == null)
        {
            mazeTimer = FindObjectOfType<MazeTimer>();
            if (mazeTimer == null)
            {
                Debug.LogWarning("MazeTimer не найден, таймер не будет работать");
            }
        }

        // ✅ ДОБАВИЛИ: один раз ищем рекордер
        if (recorderAPI == null)
        {
            recorderAPI = FindObjectOfType<CarRecorderAPI>();
            if (recorderAPI == null)
            {
                Debug.LogWarning("CarRecorderAPI не найден — запись движений работать не будет");
            }
        }

        yield return new WaitUntil(() => mazeGenerator.GetMazeData() != null);

        mazeData = mazeGenerator.GetMazeData();

        yield return new WaitUntil(() => {
            NodeInfo[] nodes = FindObjectsOfType<NodeInfo>();
            bool nodesReady = nodes.Length >= mazeData.TotalCellsX * mazeData.TotalCellsZ * 0.8f;
            return nodesReady;
        });

        BuildNodeMap();
        SpawnCarAtStart();

        isInitialized = true;
    }


    void Update()
    {
        if (!isInitialized) return;

        HandleInput();

        if (showDebugLines)
        {
            DebugWallsAroundCar();
        }
    }

    public bool IsCarReady()
    {
        return isInitialized && carInstance != null && currentNode != null;
    }

    private void BuildNodeMap()
    {
        nodeMap = new Dictionary<Vector2Int, NodeInfo>();

        NodeInfo[] allNodes = FindObjectsOfType<NodeInfo>();

        foreach (NodeInfo node in allNodes)
        {
            Vector2Int detailedKey = new Vector2Int(
                node.chunkX * mazeData.ChunkSize + node.cellX,
                node.chunkZ * mazeData.ChunkSize + node.cellZ
            );

            if (!nodeMap.ContainsKey(detailedKey))
            {
                nodeMap[detailedKey] = node;
            }
        }
    }

    private void SpawnCarAtStart()
    {
        // ИСПРАВЛЕНО: Машинка всегда спавнится в правом нижнем углу (0,0)
        int startChunkX = 0;
        int startChunkZ = 0;
        int startCellX = 0;
        int startCellZ = 0;

        Debug.Log($"📍 Спавн машинки в правом нижнем углу: Chunk({startChunkX},{startChunkZ}) Cell({startCellX},{startCellZ})");

        Vector2Int startKey = new Vector2Int(
            startChunkX * mazeData.ChunkSize + startCellX,
            startChunkZ * mazeData.ChunkSize + startCellZ
        );

        if (nodeMap.ContainsKey(startKey))
        {
            currentNode = nodeMap[startKey];
            SpawnCarAtNode(currentNode);
        }
        else
        {
            Debug.LogWarning($"Не нашли нод для старта {startKey}, ищем альтернативу");
            FindAlternativeStartNode();
        }
    }

    private void FindAlternativeStartNode()
    {
        // Ищем правый нижний угол лабиринта (максимальные координаты)
        int maxChunkX = mazeData.MazeSizeInChunks.x - 1;
        int maxChunkZ = mazeData.MazeSizeInChunks.y - 1;
        int maxCellX = mazeData.ChunkSize - 1;
        int maxCellZ = mazeData.ChunkSize - 1;

        // Пробуем найти нод в правом нижнем углу
        for (int chunkX = maxChunkX; chunkX >= 0; chunkX--)
        {
            for (int chunkZ = maxChunkZ; chunkZ >= 0; chunkZ--)
            {
                for (int cellX = maxCellX; cellX >= 0; cellX--)
                {
                    for (int cellZ = maxCellZ; cellZ >= 0; cellZ--)
                    {
                        Vector2Int key = new Vector2Int(
                            chunkX * mazeData.ChunkSize + cellX,
                            chunkZ * mazeData.ChunkSize + cellZ
                        );

                        if (nodeMap.ContainsKey(key))
                        {
                            currentNode = nodeMap[key];
                            SpawnCarAtNode(currentNode);
                            Debug.Log($"📍 Альтернативный спавн: Chunk({chunkX},{chunkZ}) Cell({cellX},{cellZ})");
                            return;
                        }
                    }
                }
            }
        }

        // Если вообще ничего не нашли, берем первый попавшийся нод
        foreach (var pair in nodeMap)
        {
            currentNode = pair.Value;
            SpawnCarAtNode(currentNode);
            Debug.Log($"⚠️ Экстренный спавн на первом найденном ноде");
            return;
        }
    }

    private void SpawnCarAtNode(NodeInfo node)
    {
        if (carPrefab == null)
        {
            Debug.LogError("Car prefab не назначен!");
            return;
        }

        if (mazeGenerator == null)
        {
            Debug.LogError("MazeGenerator не назначен!");
            return;
        }

        Vector3 spawnPosition = mazeGenerator.GetCarWorldPosition(
            node.chunkX,
            node.chunkZ,
            node.cellX,
            node.cellZ
        );

        Debug.Log($"🚗 Spawning car at: Chunk({node.chunkX},{node.chunkZ}) Cell({node.cellX},{node.cellZ})");
        Debug.Log($"   Position: {spawnPosition} (height: {mazeGenerator.GetCarSpawnHeight()})");

        carInstance = Instantiate(carPrefab, spawnPosition, Quaternion.identity, transform);
        carInstance.name = "PlayerCar";

        currentDirection = 0;
        UpdateCarRotationImmediate();
    }

    public void SetCarPosition(int chunkX, int chunkZ, int cellX, int cellZ)
    {
        if (mazeGenerator != null && carInstance != null)
        {
            Vector3 position = mazeGenerator.GetCarWorldPosition(chunkX, chunkZ, cellX, cellZ);

            if (currentMovementCoroutine != null)
                StopCoroutine(currentMovementCoroutine);

            carInstance.transform.position = position;
            isMoving = false;
            isRotating = false;

            Vector2Int globalPos = new Vector2Int(
                chunkX * mazeData.ChunkSize + cellX,
                chunkZ * mazeData.ChunkSize + cellZ
            );

            if (nodeMap.ContainsKey(globalPos))
            {
                currentNode = nodeMap[globalPos];
                Debug.Log($"✅ Машинка установлена на позицию: Chunk({chunkX},{chunkZ}) Cell({cellX},{cellZ})");
            }
            else
            {
                Debug.LogWarning($"⚠️ Нод не найден для позиции: Chunk({chunkX},{chunkZ}) Cell({cellX},{cellZ})");

                // Пытаемся найти ближайший доступный нод
                NodeInfo nearestNode = FindNearestNode(globalPos);
                if (nearestNode != null)
                {
                    currentNode = nearestNode;
                    Vector3 nearestPos = mazeGenerator.GetCarWorldPosition(
                        nearestNode.chunkX, nearestNode.chunkZ,
                        nearestNode.cellX, nearestNode.cellZ
                    );
                    carInstance.transform.position = nearestPos;
                    Debug.Log($"🔄 Машинка перемещена на ближайший доступный нод: Chunk({nearestNode.chunkX},{nearestNode.chunkZ}) Cell({nearestNode.cellX},{nearestNode.cellZ})");
                }
            }
        }
    }
    private NodeInfo FindNearestNode(Vector2Int targetGlobalPos)
    {
        NodeInfo nearestNode = null;
        float minDistance = float.MaxValue;

        foreach (var pair in nodeMap)
        {
            float distance = Vector2Int.Distance(pair.Key, targetGlobalPos);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestNode = pair.Value;
            }
        }

        return nearestNode;
    }

    private void HandleInput()
    {
        if (carInstance == null || currentNode == null || isMoving || isRotating) return;

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            TurnLeft();
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            TurnRight();
        }
        else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveForward();
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveBackward();
        }
    }

    public void TurnLeft()
    {
        if (!IsCarReady() || isMoving || isRotating) return;

        if (isRecording && recorderAPI != null)
        {
            recorderAPI.LogMovement("turn_left", GetCurrentGlobalPosition());
        }

        if (mazeTimer != null)
        {
            mazeTimer.CarActionPerformed();
        }

        StartCoroutine(RotateCar(-1));
    }

    public void TurnRight()
    {
        if (!IsCarReady() || isMoving || isRotating) return;

        if (isRecording && recorderAPI != null)
        {
            recorderAPI.LogMovement("turn_right", GetCurrentGlobalPosition());
        }

        if (mazeTimer != null)
        {
            mazeTimer.CarActionPerformed();
        }

        StartCoroutine(RotateCar(1));
    }


    public void MoveForward()
    {
        if (!IsCarReady() || isMoving || isRotating) return;

        if (isRecording && recorderAPI != null)
        {
            recorderAPI.LogMovement("move_forward", GetCurrentGlobalPosition());
        }

        if (mazeTimer != null)
        {
            mazeTimer.CarActionPerformed();
        }

        TryMoveInDirection(currentDirection);
    }


    public void MoveBackward()
    {
        if (!IsCarReady() || isMoving || isRotating) return;

        if (isRecording && recorderAPI != null)
        {
            recorderAPI.LogMovement("move_backward", GetCurrentGlobalPosition());
        }

        if (mazeTimer != null)
        {
            mazeTimer.CarActionPerformed();
        }

        TryMoveInDirection((currentDirection + 2) % 4);
    }


    private IEnumerator RotateCar(int directionChange)
    {
        if (isRotating) yield break;

        isRotating = true;

        int targetDirection = (currentDirection + directionChange + 4) % 4;
        float startAngle = carInstance.transform.eulerAngles.y;
        float targetAngle = targetDirection * 90f;

        float currentAngle = startAngle;
        float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle);

        float elapsedTime = 0f;

        while (elapsedTime < rotationAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / rotationAnimationTime;
            float newAngle = Mathf.LerpAngle(currentAngle, currentAngle + angleDifference, t);

            carInstance.transform.rotation = Quaternion.Euler(0f, newAngle, 0f);
            yield return null;
        }

        carInstance.transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
        currentDirection = targetDirection;
        isRotating = false;
    }

    private void TryMoveInDirection(int direction)
    {
        Vector2Int moveDirection = directionVectors[direction];
        NodeInfo nextNode = GetNodeInDirection(moveDirection);

        if (nextNode != null && CanMoveToDirection(moveDirection))
        {
            if (currentMovementCoroutine != null)
                StopCoroutine(currentMovementCoroutine);

            currentMovementCoroutine = StartCoroutine(MoveToNodeCoroutine(nextNode));
        }
        else if (logMovements)
        {
            Debug.Log($"🚫 Cannot move {GetDirectionName(direction)}: wall or no node");
        }
    }

    private IEnumerator MoveToNodeCoroutine(NodeInfo targetNode)
    {
        isMoving = true;
        this.targetNode = targetNode;

        Vector3 targetPosition = mazeGenerator.GetCarWorldPosition(
            targetNode.chunkX,
            targetNode.chunkZ,
            targetNode.cellX,
            targetNode.cellZ
        );

        Vector3 startPosition = carInstance.transform.position;
        float distance = Vector3.Distance(startPosition, targetPosition);
        float duration = distance / moveSpeed;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            carInstance.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        carInstance.transform.position = targetPosition;
        currentNode = targetNode;
        isMoving = false;
    }

    private NodeInfo GetNodeInDirection(Vector2Int direction)
    {
        Vector2Int currentGlobal = GetCurrentGlobalPosition();
        Vector2Int targetGlobal = currentGlobal + direction;

        if (nodeMap.ContainsKey(targetGlobal))
        {
            return nodeMap[targetGlobal];
        }

        return null;
    }

    private Vector2Int GetCurrentGlobalPosition()
    {
        return new Vector2Int(
            currentNode.chunkX * mazeData.ChunkSize + currentNode.cellX,
            currentNode.chunkZ * mazeData.ChunkSize + currentNode.cellZ
        );
    }

    private bool CanMoveToDirection(Vector2Int direction)
    {
        Vector2Int currentGlobal = GetCurrentGlobalPosition();
        Vector2Int targetGlobal = currentGlobal + direction;

        if (!nodeMap.ContainsKey(targetGlobal))
        {
            return false;
        }

        bool hasWall = mazeData.HasWallBetween(currentGlobal, targetGlobal);

        if (hasWall)
        {
            if (showDebugLines)
            {
                DrawDebugLine(currentGlobal, targetGlobal, Color.red);
            }
            return false;
        }
        else
        {
            if (showDebugLines)
            {
                DrawDebugLine(currentGlobal, targetGlobal, Color.green);
            }
            return true;
        }
    }

    private void DebugWallsAroundCar()
    {
        if (currentNode == null) return;

        Vector2Int currentGlobal = GetCurrentGlobalPosition();

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int target = currentGlobal + directions[i];
            if (nodeMap.ContainsKey(target))
            {
                bool hasWall = mazeData.HasWallBetween(currentGlobal, target);
                Color color = hasWall ? Color.red : Color.green;

                Vector3 fromPos = GetWorldPosition(currentGlobal);
                Vector3 toPos = GetWorldPosition(target);
                Debug.DrawLine(fromPos, toPos, color, 0.1f);
            }
        }
    }

    private void DrawDebugLine(Vector2Int from, Vector2Int to, Color color)
    {
        Vector3 fromPos = GetWorldPosition(from);
        Vector3 toPos = GetWorldPosition(to);
        Debug.DrawLine(fromPos, toPos, color, 2f);
    }

    private Vector3 GetWorldPosition(Vector2Int globalPos)
    {
        if (nodeMap.ContainsKey(globalPos) && mazeGenerator != null)
        {
            NodeInfo node = nodeMap[globalPos];
            return mazeGenerator.GetCarWorldPosition(
                node.chunkX,
                node.chunkZ,
                node.cellX,
                node.cellZ
            );
        }
        return Vector3.zero;
    }

    private void UpdateCarRotationImmediate()
    {
        if (carInstance == null) return;

        float[] rotationAngles = { 0f, 90f, 180f, 270f };
        carInstance.transform.rotation = Quaternion.Euler(0f, rotationAngles[currentDirection], 0f);
    }

    private string GetDirectionName(int direction = -1)
    {
        if (direction == -1) direction = currentDirection;
        string[] directionNames = { "Вперед", "Вправо", "Назад", "Влево" };
        return directionNames[direction];
    }

    // Метод для сброса направления
    public void ResetDirection()
    {
        if (carInstance != null)
        {
            currentDirection = 0;
            UpdateCarRotationImmediate();
            Debug.Log("🔄 Направление машинки сброшено (смотрит вперед)");
        }
    }

    public void TeleportToNode(NodeInfo node)
    {
        if (carInstance != null && node != null && mazeGenerator != null)
        {
            if (currentMovementCoroutine != null)
                StopCoroutine(currentMovementCoroutine);

            currentNode = node;

            Vector3 newPosition = mazeGenerator.GetCarWorldPosition(
                node.chunkX,
                node.chunkZ,
                node.cellX,
                node.cellZ
            );

            carInstance.transform.position = newPosition;
            isMoving = false;
            isRotating = false;

            Debug.Log($"🚗 Car teleported to node: {node.name}");
            Debug.Log($"   Position: {newPosition} (height: {mazeGenerator.GetCarSpawnHeight()})");
        }
    }

    public Vector2Int GetCurrentChunkCoordinates()
    {
        if (currentNode != null)
        {
            return new Vector2Int(currentNode.chunkX, currentNode.chunkZ);
        }
        return Vector2Int.zero;
    }

    public Vector2Int GetCurrentCellCoordinates()
    {
        if (currentNode != null)
        {
            return new Vector2Int(currentNode.cellX, currentNode.cellZ);
        }
        return Vector2Int.zero;
    }

    public string GetCurrentDirectionName()
    {
        return GetDirectionName();
    }
    public bool CheckWallAhead()
    {
        // Используй существующую логику из CanMoveToDirection
        Vector2Int direction = directionVectors[currentDirection];
        return !CanMoveToDirection(direction);
    }

    public bool CheckWallLeft()
    {
        Vector2Int direction = directionVectors[(currentDirection + 3) % 4]; // Поворот налево
        return !CanMoveToDirection(direction);
    }

    public bool CheckWallRight()
    {
        Vector2Int direction = directionVectors[(currentDirection + 1) % 4]; // Поворот направо
        return !CanMoveToDirection(direction);
    }

    void OnDrawGizmos()
    {
        if (currentNode != null && carInstance != null && showDebugLines && mazeGenerator != null)
        {
            Vector3 nodePos = mazeGenerator.GetCarWorldPosition(
                currentNode.chunkX,
                currentNode.chunkZ,
                currentNode.cellX,
                currentNode.cellZ
            );

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(nodePos, Vector3.one * 0.3f);

            if (isMoving && targetNode != null)
            {
                Vector3 targetPos = mazeGenerator.GetCarWorldPosition(
                    targetNode.chunkX,
                    targetNode.chunkZ,
                    targetNode.cellX,
                    targetNode.cellZ
                );

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(carInstance.transform.position, targetPos);
            }

            Gizmos.color = Color.blue;
            Vector3 direction = carInstance.transform.forward;
            Gizmos.DrawRay(carInstance.transform.position, direction * 1f);
        }
    }
}