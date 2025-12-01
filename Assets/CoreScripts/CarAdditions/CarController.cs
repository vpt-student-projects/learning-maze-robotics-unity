using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
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

    [Header("Отладка")]
    public bool showDebugLines = false;
    public bool logMovements = true;

    private GameObject carInstance;
    private NodeInfo currentNode;
    private NodeInfo targetNode;
    private Vector3 targetPosition;
    private bool isMoving = false;
    private bool isRotating = false;
    private bool isInitialized = false;

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

    public void InitializeCar()
    {
        if (isInitialized) return;

        StartCoroutine(InitializeCarCoroutine());
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

        // Ждем пока данные лабиринта будут готовы
        yield return new WaitUntil(() => mazeGenerator.GetMazeData() != null);

        mazeData = mazeGenerator.GetMazeData();

        // Ждем пока все ноды будут созданы
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
        Vector2Int startKey = new Vector2Int(0, 0);

        if (nodeMap.ContainsKey(startKey))
        {
            currentNode = nodeMap[startKey];
            SpawnCarAtNode(currentNode);
        }
        else
        {
            FindAlternativeStartNode();
        }
    }

    private void FindAlternativeStartNode()
    {
        // Ищем любой нод в чанке (0,0)
        foreach (var pair in nodeMap)
        {
            int chunkX = pair.Key.x / mazeData.ChunkSize;
            int chunkZ = pair.Key.y / mazeData.ChunkSize;

            if (chunkX == 0 && chunkZ == 0)
            {
                currentNode = pair.Value;
                SpawnCarAtNode(currentNode);
                return;
            }
        }

        // Если не нашли в чанке (0,0), используем первый доступный нод
        foreach (var pair in nodeMap)
        {
            currentNode = pair.Value;
            SpawnCarAtNode(currentNode);
            return;
        }
    }

    private void SpawnCarAtNode(NodeInfo node)
    {
        if (carPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = node.transform.position + Vector3.up * 0.5f;
        carInstance = Instantiate(carPrefab, spawnPosition, Quaternion.identity, transform);
        carInstance.name = "PlayerCar";

        currentDirection = 0;
        UpdateCarRotationImmediate();
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
        StartCoroutine(RotateCar(-1));
    }

    public void TurnRight()
    {
        if (!IsCarReady() || isMoving || isRotating) return;
        StartCoroutine(RotateCar(1));
    }

    public void MoveForward()
    {
        if (!IsCarReady() || isMoving || isRotating) return;
        TryMoveInDirection(currentDirection);
    }

    public void MoveBackward()
    {
        if (!IsCarReady() || isMoving || isRotating) return;
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
            // Только логируем, не останавливаем игру
        }
    }

    private IEnumerator MoveToNodeCoroutine(NodeInfo targetNode)
    {
        isMoving = true;
        this.targetNode = targetNode;
        targetPosition = targetNode.transform.position + Vector3.up * 0.5f;

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

        // Проверяем что целевая позиция существует
        if (!nodeMap.ContainsKey(targetGlobal))
        {
            return false;
        }

        // Проверяем стену без ошибок
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

        // Визуализация стен вокруг машинки
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int target = currentGlobal + directions[i];
            if (nodeMap.ContainsKey(target))
            {
                bool hasWall = mazeData.HasWallBetween(currentGlobal, target);
                Color color = hasWall ? Color.red : Color.green;
                Debug.DrawLine(GetWorldPosition(currentGlobal), GetWorldPosition(target), color, 0.1f);
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
        if (nodeMap.ContainsKey(globalPos))
        {
            return nodeMap[globalPos].transform.position + Vector3.up * 0.5f;
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

    public void TeleportToNode(NodeInfo node)
    {
        if (carInstance != null && node != null)
        {
            if (currentMovementCoroutine != null)
                StopCoroutine(currentMovementCoroutine);

            currentNode = node;
            Vector3 newPosition = node.transform.position + Vector3.up * 0.5f;
            carInstance.transform.position = newPosition;
            isMoving = false;
            isRotating = false;
        }
    }

    public Vector2Int GetCurrentChunkCoordinates()
    {
        return currentNode != null ? new Vector2Int(currentNode.chunkX, currentNode.chunkZ) : Vector2Int.zero;
    }

    public Vector2Int GetCurrentCellCoordinates()
    {
        return currentNode != null ? new Vector2Int(currentNode.cellX, currentNode.cellZ) : Vector2Int.zero;
    }

    public string GetCurrentDirectionName()
    {
        return GetDirectionName();
    }

    void OnDrawGizmos()
    {
        if (currentNode != null && carInstance != null && showDebugLines)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(currentNode.transform.position + Vector3.up * 0.5f, Vector3.one * 0.3f);

            if (isMoving && targetNode != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(carInstance.transform.position, targetNode.transform.position + Vector3.up * 0.5f);
            }

            Gizmos.color = Color.blue;
            Vector3 direction = carInstance.transform.forward;
            Gizmos.DrawRay(carInstance.transform.position, direction * 1f);
        }
    }
}