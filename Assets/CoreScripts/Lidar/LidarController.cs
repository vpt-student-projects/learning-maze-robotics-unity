using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LidarPoint
{
    public string name;
    public Transform pointTransform;
    public bool enabled = true;

    // 360° лидар
    public bool enable360Lidar = true;
    public float lidar360Range = 10f;
    public int lidar360Points = 36;
    public float[] lidar360Results;

    // 90° лидар
    public bool enable90Lidar = true;
    public float lidar90Range = 10f;
    public int lidar90Points = 18;
    public float lidar90Angle = 90f;
    public float[] lidar90Results;

    // Одиночные лидары
    public bool enableSingleLidar = true;
    public float singleLidarRange = 10f;
    public float singleLidarResult;

    public enum SingleLidarDirection
    {
        Forward,
        Right,
        Backward,
        Left
    }

    public SingleLidarDirection singleLidarDirection = SingleLidarDirection.Forward;

    // Кэшированные направления для оптимизации
    [NonSerialized] public Vector3[] cached360Directions;
    [NonSerialized] public float[] cached90Angles;
    [NonSerialized] public Vector3 cachedSingleDirection;
}

public class LidarController : MonoBehaviour
{
    [Header("Lidar Settings")]
    public List<LidarPoint> lidarPoints = new List<LidarPoint>();
    public LayerMask wallLayerMask = 1 << 8;
    public float scanInterval = 0.1f;

    [Header("Debug")]
    public bool showDebugRays = true;
    public Color debugColor360 = Color.cyan;
    public Color debugColor90 = Color.yellow;
    public Color debugColorSingle = Color.green;
    public bool logWarnings = true;

    [Header("Console Output")]
    public bool enableConsoleOutput = false;
    public float consoleOutputInterval = 1f;
    public int samplePoints360 = 4; // Сколько точек выводить из 360° лидара
    public bool sampleLeft90 = true; // Выводить левую точку 90° лидара
    public bool sampleCenter90 = true; // Выводить центральную точку 90° лидара
    public bool sampleRight90 = true; // Выводить правую точку 90° лидара

    private float lastScanTime;
    private float lastConsoleOutputTime;
    private bool isInitialized = false;

    void Start()
    {
        InitializeAllLidars();
        isInitialized = true;
    }

    void Update()
    {
        float currentTime = Time.time;

        // Сканируем с интервалом
        if (currentTime - lastScanTime >= scanInterval)
        {
            ScanAllLidarPoints();
            lastScanTime = currentTime;

            if (showDebugRays)
            {
                DrawAllDebugRays();
            }
        }

        // Выводим данные в консоль с интервалом
        if (enableConsoleOutput && currentTime - lastConsoleOutputTime >= consoleOutputInterval)
        {
            OutputLidarDataToConsole();
            lastConsoleOutputTime = currentTime;
        }
    }

    private void InitializeAllLidars()
    {
        foreach (var point in lidarPoints)
        {
            if (point.enabled && point.pointTransform != null)
            {
                InitializeLidarPoint(point);
            }
            else if (logWarnings && point.enabled)
            {
                Debug.LogWarning($"Lidar point '{point.name}' is enabled but has no transform assigned!");
            }
        }
    }

    private void InitializeLidarPoint(LidarPoint point)
    {
        // Инициализация 360° лидара
        if (point.enable360Lidar)
        {
            point.lidar360Results = new float[point.lidar360Points];
            point.cached360Directions = new Vector3[point.lidar360Points];

            float angleStep = 360f / point.lidar360Points;
            for (int i = 0; i < point.lidar360Points; i++)
            {
                float angle = i * angleStep;
                point.cached360Directions[i] = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            }
        }

        // Инициализация 90° лидара
        if (point.enable90Lidar)
        {
            point.lidar90Results = new float[point.lidar90Points];
            point.cached90Angles = new float[point.lidar90Points];

            float halfAngle = point.lidar90Angle / 2f;
            float angleStep = point.lidar90Angle / (point.lidar90Points - 1);
            for (int i = 0; i < point.lidar90Points; i++)
            {
                point.cached90Angles[i] = -halfAngle + (i * angleStep);
            }
        }

        // Инициализация одиночного лидара
        if (point.enableSingleLidar)
        {
            point.singleLidarResult = point.singleLidarRange;
            point.cachedSingleDirection = GetSingleLidarDirection(point, point.pointTransform);
        }
    }

    private void ScanAllLidarPoints()
    {
        if (!isInitialized) return;

        foreach (var point in lidarPoints)
        {
            if (point.enabled && point.pointTransform != null)
            {
                ScanLidarPoint(point);
            }
        }
    }

    private void ScanLidarPoint(LidarPoint point)
    {
        Vector3 origin = point.pointTransform.position;

        // Сканирование 360° лидара
        if (point.enable360Lidar && point.cached360Directions != null)
        {
            Scan360Lidar(point, origin);
        }

        // Сканирование 90° лидара
        if (point.enable90Lidar && point.cached90Angles != null)
        {
            Scan90Lidar(point, origin, point.pointTransform);
        }

        // Сканирование одиночного лидара
        if (point.enableSingleLidar)
        {
            ScanSingleLidar(point, origin, point.pointTransform);
        }
    }

    private void Scan360Lidar(LidarPoint point, Vector3 origin)
    {
        for (int i = 0; i < point.lidar360Points; i++)
        {
            if (Physics.Raycast(origin, point.cached360Directions[i], out RaycastHit hit, point.lidar360Range, wallLayerMask))
            {
                point.lidar360Results[i] = hit.distance;
            }
            else
            {
                point.lidar360Results[i] = point.lidar360Range;
            }
        }
    }

    private void Scan90Lidar(LidarPoint point, Vector3 origin, Transform referenceTransform)
    {
        for (int i = 0; i < point.lidar90Points; i++)
        {
            Vector3 direction = Quaternion.Euler(0, point.cached90Angles[i], 0) * referenceTransform.forward;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, point.lidar90Range, wallLayerMask))
            {
                point.lidar90Results[i] = hit.distance;
            }
            else
            {
                point.lidar90Results[i] = point.lidar90Range;
            }
        }
    }

    private void ScanSingleLidar(LidarPoint point, Vector3 origin, Transform referenceTransform)
    {
        // Обновляем кэшированное направление (на случай, если объект вращается)
        point.cachedSingleDirection = GetSingleLidarDirection(point, referenceTransform);

        if (Physics.Raycast(origin, point.cachedSingleDirection, out RaycastHit hit, point.singleLidarRange, wallLayerMask))
        {
            point.singleLidarResult = hit.distance;
        }
        else
        {
            point.singleLidarResult = point.singleLidarRange;
        }
    }

    private Vector3 GetSingleLidarDirection(LidarPoint point, Transform referenceTransform)
    {
        switch (point.singleLidarDirection)
        {
            case LidarPoint.SingleLidarDirection.Forward:
                return referenceTransform.forward;
            case LidarPoint.SingleLidarDirection.Right:
                return referenceTransform.right;
            case LidarPoint.SingleLidarDirection.Backward:
                return -referenceTransform.forward;
            case LidarPoint.SingleLidarDirection.Left:
                return -referenceTransform.right;
            default:
                return referenceTransform.forward;
        }
    }

    private void OutputLidarDataToConsole()
    {
        if (lidarPoints.Count == 0) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== LIDAR DATA ===");

        for (int i = 0; i < lidarPoints.Count; i++)
        {
            var point = lidarPoints[i];
            if (!point.enabled || point.pointTransform == null) continue;

            sb.AppendLine($"Point #{i}: {point.name}");

            // 360° лидар данные
            if (point.enable360Lidar && point.lidar360Results != null && point.lidar360Results.Length > 0)
            {
                sb.Append("  360°: ");
                int sampleStep = Mathf.Max(1, point.lidar360Points / samplePoints360);
                for (int j = 0; j < samplePoints360 && j < point.lidar360Points; j++)
                {
                    int index = (j * sampleStep) % point.lidar360Points;
                    float angle = (360f / point.lidar360Points) * index;
                    sb.Append($"{angle:F0}°={point.lidar360Results[index]:F2}m ");
                }
                sb.AppendLine();
            }

            // 90° лидар данные
            if (point.enable90Lidar && point.lidar90Results != null && point.lidar90Results.Length > 0)
            {
                sb.Append("  90°: ");
                List<string> samples = new List<string>();

                if (sampleLeft90)
                {
                    float angle = point.cached90Angles[0];
                    samples.Add($"Left({angle:F0}°)={point.lidar90Results[0]:F2}m");
                }

                if (sampleCenter90)
                {
                    int centerIndex = point.lidar90Points / 2;
                    float angle = point.cached90Angles[centerIndex];
                    samples.Add($"Center({angle:F0}°)={point.lidar90Results[centerIndex]:F2}m");
                }

                if (sampleRight90)
                {
                    int lastIndex = point.lidar90Points - 1;
                    float angle = point.cached90Angles[lastIndex];
                    samples.Add($"Right({angle:F0}°)={point.lidar90Results[lastIndex]:F2}m");
                }

                sb.AppendLine(string.Join(" | ", samples));
            }

            // Одиночный лидар данные
            if (point.enableSingleLidar)
            {
                sb.AppendLine($"  Single {point.singleLidarDirection}: {point.singleLidarResult:F2}m");
            }

            sb.AppendLine();
        }

        Debug.Log(sb.ToString());
    }

    // Оптимизированные методы получения данных
    public LidarPoint GetLidarPoint(int index)
    {
        if (index >= 0 && index < lidarPoints.Count)
        {
            return lidarPoints[index];
        }
        if (logWarnings)
        {
            Debug.LogWarning($"Lidar point index {index} is out of range!");
        }
        return null;
    }

    public float[] Get360LidarData(int pointIndex)
    {
        var point = GetLidarPoint(pointIndex);
        return point?.lidar360Results;
    }

    public float[] Get90LidarData(int pointIndex)
    {
        var point = GetLidarPoint(pointIndex);
        return point?.lidar90Results;
    }

    public float GetSingleLidarDistance(int pointIndex)
    {
        var point = GetLidarPoint(pointIndex);
        return point != null && point.enableSingleLidar ? point.singleLidarResult : -1f;
    }

    public string GetSingleLidarDirectionName(int pointIndex)
    {
        var point = GetLidarPoint(pointIndex);
        return point != null ? point.singleLidarDirection.ToString().ToLower() : "unknown";
    }

    public float GetGlobalMinDistance()
    {
        float minDistance = float.MaxValue;
        bool foundValidData = false;

        foreach (var point in lidarPoints)
        {
            if (!point.enabled) continue;

            // Проверяем 360° лидар
            if (point.enable360Lidar && point.lidar360Results != null)
            {
                foreach (float distance in point.lidar360Results)
                {
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        foundValidData = true;
                    }
                }
            }

            // Проверяем 90° лидар
            if (point.enable90Lidar && point.lidar90Results != null)
            {
                foreach (float distance in point.lidar90Results)
                {
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        foundValidData = true;
                    }
                }
            }

            // Проверяем одиночный лидар
            if (point.enableSingleLidar)
            {
                if (point.singleLidarResult < minDistance)
                {
                    minDistance = point.singleLidarResult;
                    foundValidData = true;
                }
            }
        }

        return foundValidData ? minDistance : -1f;
    }

    // Оптимизированные методы для API
    public string GetLidarDataJSON(int pointIndex)
    {
        try
        {
            var point = GetLidarPoint(pointIndex);
            if (point == null)
                return "{\"status\":\"error\",\"message\":\"Point not found\"}";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("{\"status\":\"success\",\"data\":{");
            sb.Append($"\"name\":\"{EscapeJson(point.name)}\",");
            sb.Append($"\"enabled\":{point.enabled.ToString().ToLower()},");

            if (point.pointTransform != null)
            {
                var pos = point.pointTransform.position;
                sb.Append($"\"position\":{{\"x\":{pos.x:F2},\"y\":{pos.y:F2},\"z\":{pos.z:F2}}},");
            }

            // 360° лидар данные
            if (point.enable360Lidar && point.lidar360Results != null)
            {
                sb.Append("\"lidar360\":{");
                sb.Append($"\"range\":{point.lidar360Range:F2},");
                sb.Append($"\"points\":{point.lidar360Points},");
                sb.Append("\"distances\":[");

                for (int i = 0; i < point.lidar360Results.Length; i++)
                {
                    sb.Append(point.lidar360Results[i].ToString("F2"));
                    if (i < point.lidar360Results.Length - 1) sb.Append(",");
                }
                sb.Append("]},");
            }

            // Одиночный лидар данные
            if (point.enableSingleLidar)
            {
                sb.Append("\"singleLidar\":{");
                sb.Append($"\"direction\":\"{point.singleLidarDirection.ToString().ToLower()}\",");
                sb.Append($"\"distance\":{point.singleLidarResult:F2}");
                sb.Append("},");
            }

            // Убираем последнюю запятую
            if (sb[sb.Length - 1] == ',')
                sb.Remove(sb.Length - 1, 1);

            sb.Append("}}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting lidar data: {ex.Message}");
            return "{\"status\":\"error\",\"message\":\"Internal error\"}";
        }
    }

    private string EscapeJson(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // Отладка
    private void DrawAllDebugRays()
    {
        foreach (var point in lidarPoints)
        {
            if (point.enabled && point.pointTransform != null)
            {
                DrawDebugRaysForPoint(point);
            }
        }
    }

    private void DrawDebugRaysForPoint(LidarPoint point)
    {
        Vector3 origin = point.pointTransform.position;

        // 360° лидар
        if (point.enable360Lidar && point.cached360Directions != null)
        {
            int step = Mathf.Max(1, point.lidar360Points / 12); // Рисуем 12 лучей для наглядности
            for (int i = 0; i < point.lidar360Points; i += step)
            {
                float distance = point.lidar360Results[i];
                Color color = distance < point.lidar360Range ? debugColor360 : debugColor360 * 0.3f;
                Debug.DrawRay(origin, point.cached360Directions[i] * distance, color);
            }
        }

        // Одиночный лидар
        if (point.enableSingleLidar)
        {
            Color color = point.singleLidarResult < point.singleLidarRange ? debugColorSingle : debugColorSingle * 0.3f;
            Debug.DrawRay(origin, point.cachedSingleDirection * point.singleLidarResult, color);
        }
    }

    [ContextMenu("Add Default Lidar Point")]
    private void AddDefaultLidarPoint()
    {
        GameObject pointObj = new GameObject($"LidarPoint_{lidarPoints.Count}");
        pointObj.transform.SetParent(transform);
        pointObj.transform.localPosition = Vector3.zero;

        LidarPoint newPoint = new LidarPoint
        {
            name = $"Point_{lidarPoints.Count}",
            pointTransform = pointObj.transform,
            enable360Lidar = true,
            enable90Lidar = false,
            enableSingleLidar = true,
            lidar360Points = 36,
            lidar360Range = 10f,
            singleLidarRange = 10f,
            singleLidarDirection = LidarPoint.SingleLidarDirection.Forward
        };

        lidarPoints.Add(newPoint);
        InitializeLidarPoint(newPoint);

        Debug.Log($"Added new lidar point at index {lidarPoints.Count - 1}");
    }

    [ContextMenu("Test Console Output")]
    public void TestConsoleOutput()
    {
        OutputLidarDataToConsole();
    }

    public bool IsInitialized() => isInitialized;
}