//using UnityEngine;
//using System.Collections.Generic;
//using System.Text;
//using Npgsql;
//using NpgsqlTypes;

//public class SavePathManager : MonoBehaviour
//{
//    [System.Serializable]
//    public class PathPoint
//    {
//        public int chunkX, chunkZ;
//        public int cellX, cellZ;
//        public int direction;
//        public string action;
//        public float time;
//    }

//    // === ПУБЛИЧНЫЕ ПОЛЯ ДЛЯ ИНСПЕКТОРА ===
//    [Header("Настройки БД")]
//    [SerializeField] private bool enableDatabase = true;
//    [SerializeField] private string dbHost = "localhost";
//    [SerializeField] private int dbPort = 5432;
//    [SerializeField] private string dbName = "maze_game";
//    [SerializeField] private string dbUser = "postgres";
//    [SerializeField] private string dbPassword = "123456";
//    [SerializeField] private string mazeId = "maze_01";

//    [Header("Настройки записи")]
//    [SerializeField] private bool autoStartRecording = true;
//    [SerializeField] private bool logToConsole = true;
//    [SerializeField] private bool autoSaveToDB = true;

//    // Приватные поля
//    private List<PathPoint> pathPoints = new List<PathPoint>();
//    private bool isRecording = false;
//    private string connectionString;

//    public static SavePathManager Instance { get; private set; }

//    void Awake()
//    {
//        if (Instance == null)
//        {
//            Instance = this;
//            DontDestroyOnLoad(gameObject);
//            InitializeDatabase();
//        }
//        else
//        {
//            Destroy(gameObject);
//        }
//    }

//    void InitializeDatabase()
//    {
//        if (!enableDatabase) return;

//        // Формируем строку подключения
//        connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";

//        if (logToConsole)
//        {
//            Debug.Log($"🛢️ Подключение к БД: {connectionString.Replace(dbPassword, "***")}");
//        }
//    }

//    void Start()
//    {
//        if (autoStartRecording)
//        {
//            StartRecording();
//        }

//        // Тест подключения при старте
//        if (enableDatabase)
//        {
//            TestDatabaseConnection();
//        }
//    }

//    public void StartRecording()
//    {
//        pathPoints.Clear();
//        isRecording = true;

//        if (logToConsole)
//            Debug.Log($"🚗 Запись пути начата: {mazeId}");
//    }

//    public void RecordPoint(int chunkX, int chunkZ, int cellX, int cellZ, int dir, string action)
//    {
//        if (!isRecording || !Application.isPlaying) return;

//        PathPoint point = new PathPoint
//        {
//            chunkX = chunkX,
//            chunkZ = chunkZ,
//            cellX = cellX,
//            cellZ = cellZ,
//            direction = dir,
//            action = action,
//            time = Time.time
//        };

//        pathPoints.Add(point);

//        if (logToConsole)
//            Debug.Log($"📝 {action} в ({chunkX},{chunkZ})[{cellX},{cellZ}] dir:{dir}");
//    }

//    public void StopRecording()
//    {
//        if (!isRecording) return;

//        isRecording = false;

//        if (logToConsole)
//            Debug.Log($"⏹️ Запись остановлена. Точек: {pathPoints.Count}");

//        // Сохраняем в файл (для надежности)
//        SaveToFile();

//        // Сохраняем в БД
//        if (enableDatabase && autoSaveToDB)
//        {
//            SaveToDatabase();
//        }
//    }

//    // СОХРАНЕНИЕ В БАЗУ ДАННЫХ
//    public bool SaveToDatabase()
//    {
//        if (!enableDatabase || pathPoints.Count == 0)
//        {
//            Debug.Log("🛢️ Сохранение в БД отключено или нет данных");
//            return false;
//        }

//        try
//        {
//            // Создаем JSON данные
//            string pathJson = CreateJsonData();

//            using (var conn = new NpgsqlConnection(connectionString))
//            {
//                conn.Open();

//                string sql = @"
//                    INSERT INTO car_paths (maze_id, path_data) 
//                    VALUES (@mazeId, @pathData::jsonb)
//                    RETURNING id;
//                ";

//                using (var cmd = new NpgsqlCommand(sql, conn))
//                {
//                    cmd.Parameters.AddWithValue("@mazeId", mazeId);
//                    cmd.Parameters.AddWithValue("@pathData", NpgsqlDbType.Jsonb, pathJson);

//                    var newId = cmd.ExecuteScalar();

//                    if (logToConsole)
//                        Debug.Log($"✅ Путь сохранен в БД. ID записи: {newId}");

//                    return true;
//                }
//            }
//        }
//        catch (System.Exception ex)
//        {
//            Debug.LogError($"❌ Ошибка при сохранении в БД: {ex.Message}");
//            return false;
//        }
//    }

//    // Тест подключения к БД
//    public bool TestDatabaseConnection()
//    {
//        if (!enableDatabase)
//        {
//            Debug.Log("🛢️ БД отключена в настройках");
//            return false;
//        }

//        try
//        {
//            using (var conn = new NpgsqlConnection(connectionString))
//            {
//                conn.Open();

//                // Проверяем версию PostgreSQL
//                using (var cmd = new NpgsqlCommand("SELECT version();", conn))
//                {
//                    var version = cmd.ExecuteScalar();
//                    Debug.Log($"✅ Подключение к БД успешно");
//                    Debug.Log($"   Версия PostgreSQL: {version}");
//                }

//                return true;
//            }
//        }
//        catch (System.Exception ex)
//        {
//            Debug.LogError($"❌ Ошибка подключения к БД: {ex.Message}");
//            return false;
//        }
//    }

//    // СОЗДАНИЕ JSON ДЛЯ СОХРАНЕНИЯ
//    private string CreateJsonData()
//    {
//        // Простой JSON без сложных структур
//        StringBuilder jsonBuilder = new StringBuilder();
//        jsonBuilder.Append("{");
//        jsonBuilder.Append($"\"maze_id\":\"{mazeId}\",");
//        jsonBuilder.Append($"\"total_points\":{pathPoints.Count},");
//        jsonBuilder.Append($"\"record_time\":\"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
//        jsonBuilder.Append($"\"points\":[");

//        for (int i = 0; i < pathPoints.Count; i++)
//        {
//            var point = pathPoints[i];
//            jsonBuilder.Append("{");
//            jsonBuilder.Append($"\"chunkX\":{point.chunkX},");
//            jsonBuilder.Append($"\"chunkZ\":{point.chunkZ},");
//            jsonBuilder.Append($"\"cellX\":{point.cellX},");
//            jsonBuilder.Append($"\"cellZ\":{point.cellZ},");
//            jsonBuilder.Append($"\"direction\":{point.direction},");
//            jsonBuilder.Append($"\"action\":\"{point.action}\",");
//            jsonBuilder.Append($"\"time\":{point.time:F2}");
//            jsonBuilder.Append("}");

//            if (i < pathPoints.Count - 1)
//                jsonBuilder.Append(",");
//        }

//        jsonBuilder.Append("]}");

//        return jsonBuilder.ToString();
//    }

//    // СОХРАНЕНИЕ В ФАЙЛ (как было)
//    private void SaveToFile()
//    {
//        string fileName = $"car_path_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
//        string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);

//        StringBuilder sb = new StringBuilder();
//        sb.AppendLine("=== ПУТЬ МАШИНКИ ===");
//        sb.AppendLine($"Maze ID: {mazeId}");
//        sb.AppendLine($"Всего точек: {pathPoints.Count}");
//        sb.AppendLine($"Время записи: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
//        sb.AppendLine();

//        foreach (var point in pathPoints)
//        {
//            sb.AppendLine($"{point.time:F2}s: {point.action} at chunk({point.chunkX},{point.chunkZ}) cell({point.cellX},{point.cellZ}) dir:{point.direction}");
//        }

//        System.IO.File.WriteAllText(filePath, sb.ToString());

//        if (logToConsole)
//            Debug.Log($"💾 Сохранено в файл: {filePath}");
//    }

//    void OnApplicationQuit()
//    {
//        if (isRecording)
//        {
//            StopRecording();
//        }
//    }

//    // UI МЕТОДЫ (для кнопок)
//    public void ManualSaveToDB()
//    {
//        SaveToDatabase();
//    }

//    public void ManualTestConnection()
//    {
//        TestDatabaseConnection();
//    }

//    // ГЕТТЕРЫ для получения значений (опционально)
//    public bool GetEnableDatabase() => enableDatabase;
//    public string GetMazeId() => mazeId;
//    public bool GetAutoSaveToDB() => autoSaveToDB;
//}