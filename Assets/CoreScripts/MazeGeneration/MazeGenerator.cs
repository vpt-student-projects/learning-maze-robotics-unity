using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class MazeGenerator : MonoBehaviour
{
    [Header("Настройки лабиринта")]
    public int chunkSize = 4;
    public Vector2Int mazeSizeInChunks = new Vector2Int(3, 3);
    public bool useRightHandRule = true;
    public bool createFinishArea = true;

    [Header("Настройки смещения")]
    public Vector3 chunkOffset = Vector3.zero;
    public Vector3 cellOffset = Vector3.zero;
    public Vector3 wallOffset = Vector3.zero;

    [Header("Префабы")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject finishPrefab;
    public GameObject nodePrefab;
    public GameObject carPrefab;

    [Header("Настройки генерации")]
    public float cellSize = 2f;
    public float wallHeight = 3f;
    public float wallThickness = 0.1f;

    [Header("Камера")]
    public MazeCameraController cameraController;

    [Header("События")]
    public UnityEvent OnMazeGenerated;
    public UnityEvent OnNodesCreated;
    public UnityEvent OnCarSpawned;
    public UnityEvent OnAllInitialized;

    private MazeData mazeData;
    private MazeBuilder mazeBuilder;
    private NodeGenerator nodeGenerator;
    private CarController carController;
    private bool isGenerating = false;

    public bool IsGenerating()
    {
        return isGenerating;
    }

    // Сделайте InitializeSequence публичным
    public IEnumerator InitializeSequence()
    {
        Debug.Log("🚀 Starting initialization sequence...");
        isGenerating = true;

        // 1. Генерация лабиринта
        yield return StartCoroutine(GenerateMazeCoroutine());

        // 2. Создание нодов
        yield return StartCoroutine(CreateNodesCoroutine());

        // 3. Создание машинки
        yield return StartCoroutine(SpawnCarCoroutine());

        // 4. Запуск API
        yield return StartCoroutine(StartAPICoroutine());

        Debug.Log("🎉 All systems initialized successfully!");
        OnAllInitialized?.Invoke();
        isGenerating = false;
    }

    [ContextMenu("Сгенерировать лабиринт")]
    public void GenerateMaze()
    {
        if (!isGenerating)
            StartCoroutine(InitializeSequence());
    }

    private IEnumerator GenerateMazeCoroutine()
    {
        Debug.Log("🔄 Step 1: Generating maze...");
        ClearExistingMaze();
        InitializeComponents();

        mazeData.Initialize();
        mazeBuilder.Generate();

        // Обновляем камеру после генерации лабиринта
        if (cameraController != null)
            cameraController.UpdateCameraForNewMaze();

        Debug.Log($"✅ Maze generated: {mazeSizeInChunks.x}x{mazeSizeInChunks.y} chunks, {chunkSize} cells per chunk");
        OnMazeGenerated?.Invoke();

        yield return null;
    }

    private IEnumerator CreateNodesCoroutine()
    {
        Debug.Log("🔄 Step 2: Creating nodes...");

        yield return null;

        nodeGenerator.CreateNodes();

        // Устанавливаем слой для всех нодов
        NodeInfo[] allNodes = FindObjectsOfType<NodeInfo>();
        foreach (NodeInfo node in allNodes)
        {
            node.gameObject.layer = LayerMask.NameToLayer("Floor");
            SetLayerRecursively(node.gameObject, LayerMask.NameToLayer("Floor"));
        }

        Debug.Log($"✅ Nodes created: {allNodes.Length} (слой: Floor)");
        OnNodesCreated?.Invoke();

        yield return null;
    }

    private IEnumerator SpawnCarCoroutine()
    {
        Debug.Log("🔄 Step 3: Spawning car...");

        yield return new WaitUntil(() => FindObjectsOfType<NodeInfo>().Length > 0);

        CarController existingCar = FindObjectOfType<CarController>();
        if (existingCar != null)
        {
            carController = existingCar;
            // Устанавливаем слой для машинки
            SetLayerRecursively(carController.gameObject, LayerMask.NameToLayer("Car"));
            Debug.Log("✅ Using existing car controller (слой: Car)");
        }
        else
        {
            GameObject carObject = new GameObject("Car");
            carController = carObject.AddComponent<CarController>();
            carController.carPrefab = carPrefab;
            carController.mazeGenerator = this;
            carController.InitializeCar();

            // Устанавливаем слой для машинки
            SetLayerRecursively(carObject, LayerMask.NameToLayer("Car"));
        }

        Debug.Log("✅ Car spawned successfully (слой: Car)");
        OnCarSpawned?.Invoke();

        yield return null;
    }

    // Добавьте вспомогательный метод
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

    private IEnumerator StartAPICoroutine()
    {
        Debug.Log("🔄 Step 4: Starting API...");

        // Ждем пока машинка будет готова
        yield return new WaitUntil(() => carController != null && carController.IsCarReady());

        CarAPIController apiController = FindObjectOfType<CarAPIController>();
        if (apiController == null)
        {
            // Создаем API контроллер если его нет
            GameObject apiObject = new GameObject("CarAPIController");
            apiController = apiObject.AddComponent<CarAPIController>();
        }

        // Настраиваем API контроллер
        apiController.SetCarController(carController);
        apiController.StartServer();

        Debug.Log("✅ API started successfully");

        yield return null;
    }

    private void InitializeComponents()
    {
        mazeData = new MazeData(chunkSize, mazeSizeInChunks);
        mazeBuilder = new MazeBuilder(mazeData, this);
        nodeGenerator = new NodeGenerator(this);

        if (cameraController == null)
            cameraController = FindObjectOfType<MazeCameraController>();
    }

    [ContextMenu("Очистить лабиринт")]
    private void ClearExistingMaze()
    {
        mazeBuilder?.Clear();
        nodeGenerator?.Clear();

        // Удаляем старую машинку
        CarController[] oldCars = FindObjectsOfType<CarController>();
        foreach (CarController car in oldCars)
        {
            if (Application.isPlaying)
                Destroy(car.gameObject);
            else
                DestroyImmediate(car.gameObject);
        }

        // Останавливаем API
        CarAPIController api = FindObjectOfType<CarAPIController>();
        api?.StopServer();
    }

    // Public getters для доступа из других классов
    public Vector3 GetCellWorldPosition(int chunkX, int chunkZ, int cellX, int cellY)
    {
        return new Vector3(
            chunkX * (chunkSize * cellSize + chunkOffset.x) + cellX * (cellSize + cellOffset.x) + wallOffset.x,
            wallOffset.y,
            chunkZ * (chunkSize * cellSize + chunkOffset.z) + cellY * (cellSize + cellOffset.z) + wallOffset.z
        );
    }

    public MazeData GetMazeData() => mazeData;
    public CarController GetCarController() => carController;

    public int GetTotalCellsX() => mazeSizeInChunks.x * chunkSize;
    public int GetTotalCellsZ() => mazeSizeInChunks.y * chunkSize;
    public float GetTotalWidth() => GetTotalCellsX() * cellSize + (mazeSizeInChunks.x - 1) * chunkOffset.x;
    public float GetTotalDepth() => GetTotalCellsZ() * cellSize + (mazeSizeInChunks.y - 1) * chunkOffset.z;
}