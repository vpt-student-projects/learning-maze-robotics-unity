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
    public float moveAnimationTime = 0.08f;

    [Header("Ссылки")]
    public GameObject carPrefab;
    public MazeGenerator mazeGenerator;

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
    void Start()
    {
        Invoke(nameof(InitializeCar), 0.5f);
    }

    void Update()
    {
        if (!isInitialized) return;
        HandleInput();
    }
    private IEnumerator InitializeCarCoroutine()
    {
        Debug.Log("🚗 Initializing car...");

        if (mazeGenerator == null)
        {
            mazeGenerator = FindObjectOfType<MazeGenerator>();
            if (mazeGenerator == null)
            {
                Debug.LogError("MazeGenerator not found!");
                yield break;
            }
        }

        // Ждем пока данные лабиринта будут готовы
        yield return new WaitUntil(() => mazeGenerator.GetMazeData() != null);

        mazeData = mazeGenerator.GetMazeData();

        // Ждем пока все ноды будут созданы
        yield return new WaitUntil(() => FindObjectsOfType<NodeInfo>().Length > 0);

        BuildNodeMap();
        SpawnCarAtStart();

        isInitialized = true;
        Debug.Log("✅ Car initialized successfully!");
    }
    public void InitializeCar()
    {
        if (isInitialized) return;

        StartCoroutine(InitializeCarCoroutine());
    }

    //private void InitializeCar()
    //{
    //    if (mazeGenerator == null)
    //    {
    //        mazeGenerator = FindObjectOfType<MazeGenerator>();
    //        if (mazeGenerator == null)
    //        {
    //            Debug.LogError("MazeGenerator не найден!");
    //            return;
    //        }
    //    }

    //    mazeData = mazeGenerator.GetMazeData();
    //    BuildNodeMap();
    //    SpawnCarAtStart();

    //    // Уведомляем API контроллер о создании машинки
    //    NotifyAPIController();
    //}
    //private void NotifyAPIController()
    //{
    //    // Ищем все API контроллеры в сцене и уведомляем их
    //    CarAPIController[] apiControllers = FindObjectsOfType<CarAPIController>();
    //    foreach (CarAPIController apiController in apiControllers)
    //    {
    //        if (apiController != null)
    //        {
    //            // Вызываем метод установки контроллера
    //            apiController.SetCarController(this);
    //        }
    //    }

    //    if (apiControllers.Length == 0)
    //    {
    //        Debug.LogWarning("CarAPIController не найден в сцене!");
    //    }
    //    else
    //    {
    //        Debug.Log($"✅ Уведомлено {apiControllers.Length} API контроллеров о создании машинки");
    //    }
    //}
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

    // Новый метод для получения расширенной информации
    public CarStatus GetCarStatus()
    {
        return new CarStatus
        {
            chunkCoordinates = GetCurrentChunkCoordinates(),
            cellCoordinates = GetCurrentCellCoordinates(),
            direction = GetCurrentDirectionName(),
            isMoving = isMoving,
            isRotating = isRotating
        };
    }

    [System.Serializable]
    public struct CarStatus
    {
        public Vector2Int chunkCoordinates;
        public Vector2Int cellCoordinates;
        public string direction;
        public bool isMoving;
        public bool isRotating;
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

        Debug.Log($"📍 Node map built: {nodeMap.Count} nodes");
    }

    private void SpawnCarAtStart()
    {
        Vector2Int startKey = new Vector2Int(0, 0);

        if (nodeMap.ContainsKey(startKey))
        {
            currentNode = nodeMap[startKey];
            SpawnCarAtNode(currentNode);
            Debug.Log($"🚗 Car spawned at start node: {startKey}");
        }
        else
        {
            Debug.LogWarning($"Start node {startKey} not found. Looking for alternative...");
            FindAlternativeStartNode();
        }
    }

    private void FindAlternativeStartNode()
    {
        foreach (var pair in nodeMap)
        {
            int chunkX = pair.Key.x / mazeData.ChunkSize;
            int chunkZ = pair.Key.y / mazeData.ChunkSize;

            if (chunkX == 0 && chunkZ == 0)
            {
                currentNode = pair.Value;
                SpawnCarAtNode(currentNode);
                Debug.Log($"🚗 Car spawned at alternative node: {pair.Key}");
                return;
            }
        }

        // Если не нашли в чанке (0,0), используем первый доступный нод
        foreach (var pair in nodeMap)
        {
            currentNode = pair.Value;
            SpawnCarAtNode(currentNode);
            Debug.Log($"🚗 Car spawned at random node: {pair.Key}");
            return;
        }

        Debug.LogError("❌ No nodes found for car spawn!");
    }

    private void SpawnCarAtNode(NodeInfo node)
    {
        if (carPrefab == null)
        {
            Debug.LogError("❌ Car prefab not assigned!");
            return;
        }

        Vector3 spawnPosition = node.transform.position + Vector3.up * 0.5f;
        carInstance = Instantiate(carPrefab, spawnPosition, Quaternion.identity, transform);
        carInstance.name = "PlayerCar";

        currentDirection = 0;
        UpdateCarRotationImmediate();

        Debug.Log($"🎯 Car positioned at: Chunk({node.chunkX},{node.chunkZ}) Cell({node.cellX},{node.cellZ})");
    }

    private void HandleInput()
    {
        if (carInstance == null || currentNode == null || isMoving || isRotating) return;

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            StartCoroutine(RotateCar(-1));
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            StartCoroutine(RotateCar(1));
        }
        else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            TryMoveInDirection(currentDirection);
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            TryMoveInDirection((currentDirection + 2) % 4);
        }
    }

    private IEnumerator RotateCar(int directionChange)
    {
        if (isRotating) yield break;

        isRotating = true;

        int targetDirection = (currentDirection + directionChange + 4) % 4;
        float startAngle = carInstance.transform.eulerAngles.y;
        float targetAngle = targetDirection * 90f;

        // Нормализуем углы для плавного вращения
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

        Debug.Log($"Поворот завершен. Текущее направление: {GetDirectionName()}");
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
        else
        {
            Debug.Log($"Движение {GetDirectionName(direction)} заблокировано стеной!");
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

        Debug.Log($"Машинка перемещена на нод: Чанк({currentNode.chunkX},{currentNode.chunkZ}) Ячейка({currentNode.cellX},{currentNode.cellZ})");
    }

    private NodeInfo GetNodeInDirection(Vector2Int direction)
    {
        int globalX = currentNode.chunkX * mazeData.ChunkSize + currentNode.cellX + direction.x;
        int globalZ = currentNode.chunkZ * mazeData.ChunkSize + currentNode.cellZ + direction.y;

        Vector2Int targetKey = new Vector2Int(globalX, globalZ);
        return nodeMap.ContainsKey(targetKey) ? nodeMap[targetKey] : null;
    }

    private bool CanMoveToDirection(Vector2Int direction)
    {
        Vector2Int currentGlobal = new Vector2Int(
            currentNode.chunkX * mazeData.ChunkSize + currentNode.cellX,
            currentNode.chunkZ * mazeData.ChunkSize + currentNode.cellZ
        );

        Vector2Int targetGlobal = currentGlobal + direction;

        return !mazeData.HasWallBetween(currentGlobal, targetGlobal);
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

            Debug.Log($"Машинка телепортирована на нод: Чанк({node.chunkX},{node.chunkZ}) Ячейка({node.cellX},{node.cellZ})");
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
        if (currentNode != null && carInstance != null)
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