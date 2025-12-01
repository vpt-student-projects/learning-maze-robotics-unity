using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LidarPoint
{
    public string name;
    public Transform pointTransform;
    public bool enabled = true;

    // 360° лидар
    public bool enable360Lidar = true;
    public float lidar360Range = 10f;
    public int lidar360Points = 360;

    // 90° лидар
    public bool enable90Lidar = true;
    public float lidar90Range = 10f;
    public int lidar90Points = 90;
    public float lidar90Angle = 90f;

    // Одиночные лидары
    public bool enableSingleLidars = true;
    public float singleLidarRange = 10f;

    // Результаты
    [HideInInspector] public float[] lidar360Results;
    [HideInInspector] public float[] lidar90Results;
    [HideInInspector] public float[] singleLidarResults;
}

public class LidarController : MonoBehaviour
{
    [Header("Точки лидаров")]
    public List<LidarPoint> lidarPoints = new List<LidarPoint>();

    [Header("Общие настройки")]
    public LayerMask wallLayerMask = 1 << 8; // Слой "Wall"

    [Header("Отладка")]
    public bool showDebugRays = true;
    public Color debugColor360 = Color.cyan;
    public Color debugColor90 = Color.yellow;
    public Color debugColorSingle = Color.green;

    void Start()
    {
        InitializeAllLidars();
    }

    void Update()
    {
        ScanAllLidarPoints();

        if (showDebugRays)
        {
            DrawAllDebugRays();
        }
    }

    private void InitializeAllLidars()
    {
        foreach (var point in lidarPoints)
        {
            if (point.enabled)
            {
                InitializeLidarPoint(point);
            }
        }
    }

    private void InitializeLidarPoint(LidarPoint point)
    {
        if (point.enable360Lidar)
        {
            point.lidar360Results = new float[point.lidar360Points];
        }

        if (point.enable90Lidar)
        {
            point.lidar90Results = new float[point.lidar90Points];
        }

        if (point.enableSingleLidars)
        {
            point.singleLidarResults = new float[4]; // 0: вперед, 1: вправо, 2: назад, 3: влево
        }
    }

    private void ScanAllLidarPoints()
    {
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
        Transform referenceTransform = point.pointTransform;

        if (point.enable360Lidar)
        {
            Scan360Lidar(point, origin);
        }

        if (point.enable90Lidar)
        {
            Scan90Lidar(point, origin, referenceTransform);
        }

        if (point.enableSingleLidars)
        {
            ScanSingleLidars(point, origin, referenceTransform);
        }
    }

    private void Scan360Lidar(LidarPoint point, Vector3 origin)
    {
        float angleStep = 360f / point.lidar360Points;

        for (int i = 0; i < point.lidar360Points; i++)
        {
            float angle = i * angleStep;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, point.lidar360Range, wallLayerMask))
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
        float halfAngle = point.lidar90Angle / 2f;
        float angleStep = point.lidar90Angle / (point.lidar90Points - 1);

        for (int i = 0; i < point.lidar90Points; i++)
        {
            float angle = -halfAngle + (i * angleStep);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * referenceTransform.forward;

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

    private void ScanSingleLidars(LidarPoint point, Vector3 origin, Transform referenceTransform)
    {
        // Вперед (0°)
        Vector3 forwardDir = referenceTransform.forward;
        point.singleLidarResults[0] = ScanSingleRay(origin, forwardDir, point.singleLidarRange);

        // Вправо (90°)
        Vector3 rightDir = referenceTransform.right;
        point.singleLidarResults[1] = ScanSingleRay(origin, rightDir, point.singleLidarRange);

        // Назад (180°)
        Vector3 backDir = -referenceTransform.forward;
        point.singleLidarResults[2] = ScanSingleRay(origin, backDir, point.singleLidarRange);

        // Влево (270°)
        Vector3 leftDir = -referenceTransform.right;
        point.singleLidarResults[3] = ScanSingleRay(origin, leftDir, point.singleLidarRange);
    }

    private float ScanSingleRay(Vector3 origin, Vector3 direction, float range)
    {
        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, wallLayerMask))
        {
            return hit.distance;
        }
        return range;
    }

    // Методы для получения данных
    public LidarPoint GetLidarPoint(int index)
    {
        if (index >= 0 && index < lidarPoints.Count)
        {
            return lidarPoints[index];
        }
        return null;
    }

    public LidarPoint GetLidarPointByName(string name)
    {
        foreach (var point in lidarPoints)
        {
            if (point.name == name)
            {
                return point;
            }
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

    public float[] GetSingleLidarData(int pointIndex)
    {
        var point = GetLidarPoint(pointIndex);
        return point?.singleLidarResults;
    }

    // Методы для получения конкретных направлений
    public float GetForwardDistance(int pointIndex)
    {
        var data = GetSingleLidarData(pointIndex);
        return data != null && data.Length > 0 ? data[0] : -1f;
    }

    public float GetRightDistance(int pointIndex)
    {
        var data = GetSingleLidarData(pointIndex);
        return data != null && data.Length > 1 ? data[1] : -1f;
    }

    public float GetBackwardDistance(int pointIndex)
    {
        var data = GetSingleLidarData(pointIndex);
        return data != null && data.Length > 2 ? data[2] : -1f;
    }

    public float GetLeftDistance(int pointIndex)
    {
        var data = GetSingleLidarData(pointIndex);
        return data != null && data.Length > 3 ? data[3] : -1f;
    }

    // Метод для получения минимального расстояния из всех точек
    public float GetGlobalMinDistance()
    {
        float minDistance = float.MaxValue;

        foreach (var point in lidarPoints)
        {
            if (!point.enabled) continue;

            float pointMin = GetPointMinDistance(point);
            if (pointMin < minDistance) minDistance = pointMin;
        }

        return minDistance < float.MaxValue ? minDistance : -1f;
    }

    private float GetPointMinDistance(LidarPoint point)
    {
        float minDistance = float.MaxValue;

        if (point.enable360Lidar && point.lidar360Results != null)
        {
            foreach (float dist in point.lidar360Results)
            {
                if (dist < minDistance) minDistance = dist;
            }
        }

        if (point.enable90Lidar && point.lidar90Results != null)
        {
            foreach (float dist in point.lidar90Results)
            {
                if (dist < minDistance) minDistance = dist;
            }
        }

        if (point.enableSingleLidars && point.singleLidarResults != null)
        {
            foreach (float dist in point.singleLidarResults)
            {
                if (dist < minDistance) minDistance = dist;
            }
        }

        return minDistance;
    }

    // Отладка - рисование лучей
    private void DrawAllDebugRays()
    {
        foreach (var point in lidarPoints)
        {
            if (point.enabled && point.pointTransform != null)
            {
                DrawPointDebugRays(point);
            }
        }
    }

    private void DrawPointDebugRays(LidarPoint point)
    {
        Vector3 origin = point.pointTransform.position;
        Transform refTransform = point.pointTransform;

        // 360° лидар
        if (point.enable360Lidar && point.lidar360Results != null)
        {
            float angleStep = 360f / point.lidar360Points;
            for (int i = 0; i < point.lidar360Points; i += 20)
            {
                float angle = i * angleStep;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                float distance = point.lidar360Results[i];

                if (distance < point.lidar360Range)
                {
                    Debug.DrawRay(origin, direction * distance, debugColor360);
                }
                else
                {
                    Debug.DrawRay(origin, direction * point.lidar360Range, debugColor360 * 0.3f);
                }
            }
        }

        // 90° лидар
        if (point.enable90Lidar && point.lidar90Results != null)
        {
            float halfAngle = point.lidar90Angle / 2f;
            float angleStep = point.lidar90Angle / (point.lidar90Points - 1);

            for (int i = 0; i < point.lidar90Points; i += 3)
            {
                float angle = -halfAngle + (i * angleStep);
                Vector3 direction = Quaternion.Euler(0, angle, 0) * refTransform.forward;
                float distance = point.lidar90Results[i];

                if (distance < point.lidar90Range)
                {
                    Debug.DrawRay(origin, direction * distance, debugColor90);
                }
                else
                {
                    Debug.DrawRay(origin, direction * point.lidar90Range, debugColor90 * 0.3f);
                }
            }
        }

        // Одиночные лидары
        if (point.enableSingleLidars && point.singleLidarResults != null)
        {
            Debug.DrawRay(origin, refTransform.forward * point.singleLidarResults[0], debugColorSingle);
            Debug.DrawRay(origin, refTransform.right * point.singleLidarResults[1], debugColorSingle);
            Debug.DrawRay(origin, -refTransform.forward * point.singleLidarResults[2], debugColorSingle);
            Debug.DrawRay(origin, -refTransform.right * point.singleLidarResults[3], debugColorSingle);
        }
    }

    // API методы
    public string GetAllLidarDataJSON()
    {
        List<LidarPointData> pointDataList = new List<LidarPointData>();

        foreach (var point in lidarPoints)
        {
            if (!point.enabled) continue;

            pointDataList.Add(new LidarPointData
            {
                name = point.name,
                position = point.pointTransform != null ?
                    new Vector3Data(point.pointTransform.position) : new Vector3Data(),
                rotation = point.pointTransform != null ?
                    new Vector3Data(point.pointTransform.eulerAngles) : new Vector3Data(),
                lidar360 = point.enable360Lidar ? point.lidar360Results : null,
                lidar90 = point.enable90Lidar ? point.lidar90Results : null,
                singleLidars = point.enableSingleLidars ? point.singleLidarResults : null,
                forward = GetForwardDistance(lidarPoints.IndexOf(point)),
                right = GetRightDistance(lidarPoints.IndexOf(point)),
                backward = GetBackwardDistance(lidarPoints.IndexOf(point)),
                left = GetLeftDistance(lidarPoints.IndexOf(point))
            });
        }

        AllLidarData data = new AllLidarData
        {
            points = pointDataList.ToArray(),
            globalMinDistance = GetGlobalMinDistance(),
            pointCount = pointDataList.Count
        };

        return JsonUtility.ToJson(data, true);
    }

    [System.Serializable]
    public class Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data() { }
        public Vector3Data(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }
    }

    [System.Serializable]
    public class LidarPointData
    {
        public string name;
        public Vector3Data position;
        public Vector3Data rotation;
        public float[] lidar360;
        public float[] lidar90;
        public float[] singleLidars;
        public float forward;
        public float right;
        public float backward;
        public float left;
    }

    [System.Serializable]
    public class AllLidarData
    {
        public LidarPointData[] points;
        public float globalMinDistance;
        public int pointCount;
    }

    [ContextMenu("Add New Lidar Point")]
    private void AddNewLidarPoint()
    {
        GameObject newPointObj = new GameObject($"LidarPoint_{lidarPoints.Count}");
        newPointObj.transform.SetParent(transform);
        newPointObj.transform.localPosition = Vector3.zero;

        LidarPoint newPoint = new LidarPoint
        {
            name = $"Point_{lidarPoints.Count}",
            pointTransform = newPointObj.transform
        };

        lidarPoints.Add(newPoint);
        InitializeLidarPoint(newPoint);
    }

    [ContextMenu("Print All Lidar Data")]
    private void PrintAllLidarData()
    {
        foreach (var point in lidarPoints)
        {
            if (!point.enabled || point.pointTransform == null) continue;

            if (point.enable360Lidar)
            {
                float min = GetMinFromArray(point.lidar360Results);
                float max = GetMaxFromArray(point.lidar360Results);
            }
        }
    }

    private float GetMinFromArray(float[] array)
    {
        if (array == null || array.Length == 0) return -1f;

        float min = float.MaxValue;
        foreach (float val in array)
        {
            if (val < min) min = val;
        }
        return min;
    }

    private float GetMaxFromArray(float[] array)
    {
        if (array == null || array.Length == 0) return -1f;

        float max = 0f;
        foreach (float val in array)
        {
            if (val > max) max = val;
        }
        return max;
    }

    [ContextMenu("Reset All Lidars")]
    public void ResetAllLidars()
    {
        InitializeAllLidars();
    }
}