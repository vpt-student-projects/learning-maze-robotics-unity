using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class StandaloneLidar : MonoBehaviour
{
    [Header("=== НАСТРОЙКИ ЛИДАРА ===")]
    [SerializeField] private LayerMask obstacleLayerMask = 1;
    [SerializeField] private float maxScanDistance = 10f;
    [SerializeField] private int baudRate = 230400;
    
    [Header("Режим работы")]
    [SerializeField] private LidarMode currentMode = LidarMode.FourDirections;
    
    [Header("Визуализация")]
    [SerializeField] private bool autoVisualize = false;
    [SerializeField] private Color rayColor = Color.red;
    [SerializeField] private float visualizationDuration = 0.1f;

    public enum LidarMode
    {
        FourDirections,
        Cone45Degrees,
        Full360
    }

    // События для внешних систем
    public event Action<LidarRay[]> OnScanCompleted;
    public event Action<LidarMode> OnModeChanged;

    // Публичные свойства для чтения состояния
    public bool IsScanning { get; private set; }
    public LidarMode CurrentMode => currentMode;
    public float MaxScanDistance => maxScanDistance;

    private Coroutine scanCoroutine;
    private Coroutine visualizationCoroutine;

    void Update()
    {
        // Обработка клавиш для управления лидаром
        HandleInput();
    }

    private void HandleInput()
    {
        // Смена режимов
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetMode(LidarMode.FourDirections);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetMode(LidarMode.Cone45Degrees);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetMode(LidarMode.Full360);

        // Сканирование
        if (Input.GetKeyDown(KeyCode.S)) StartSingleScan();

        // Визуализация
        if (Input.GetKeyDown(KeyCode.V)) ToggleVisualization();
    }

    // === ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ ИНТЕГРАЦИИ ===

    public void SetMode(LidarMode newMode)
    {
        if (currentMode == newMode) return;
        
        currentMode = newMode;
        OnModeChanged?.Invoke(newMode);
        Debug.Log($"Режим лидара изменен на: {newMode}");
    }

    public void StartSingleScan()
    {
        if (IsScanning)
        {
            Debug.LogWarning("Лидар уже сканирует!");
            return;
        }

        scanCoroutine = StartCoroutine(PerformScanRoutine());
    }

    public void StartContinuousScan(float interval = 0.1f)
    {
        StopAllCoroutines();
        StartCoroutine(ContinuousScanRoutine(interval));
    }

    public void StopScan()
    {
        if (scanCoroutine != null)
        {
            StopCoroutine(scanCoroutine);
            scanCoroutine = null;
        }
        IsScanning = false;
    }

    public void ToggleVisualization()
    {
        autoVisualize = !autoVisualize;
        
        if (autoVisualize)
        {
            StartContinuousScan(0.05f);
        }
        else
        {
            StopScan();
        }
    }

    // === МЕТОДЫ ПОЛУЧЕНИЯ ДАННЫХ ===

    public float[] GetFourDirectionsScan()
    {
        var rays = ScanFourDirections();
        float[] distances = new float[4];
        for (int i = 0; i < 4; i++)
        {
            distances[i] = rays[i].distance;
        }
        return distances;
    }

    public LidarRay[] GetCone45Scan()
    {
        return ScanCone45();
    }

    public LidarRay[] GetFull360Scan()
    {
        return ScanFull360();
    }

    public Dictionary<string, float> GetScanAsDictionary()
    {
        var rays = PerformScan();
        var result = new Dictionary<string, float>();

        for (int i = 0; i < rays.Length; i++)
        {
            result[$"ray_{i}"] = rays[i].distance;
        }

        return result;
    }

    // === ВНУТРЕННЯЯ ЛОГИКА ===

    private IEnumerator PerformScanRoutine()
    {
        IsScanning = true;

        // Выполнение сканирования
        LidarRay[] scanData = PerformScan();

        // Имитация передачи данных
        yield return StartCoroutine(SimulateDataTransfer(scanData));

        // Вызов события
        OnScanCompleted?.Invoke(scanData);

        // Визуализация
        if (autoVisualize)
        {
            VisualizeRays(scanData);
        }

        IsScanning = false;
    }

    private IEnumerator ContinuousScanRoutine(float interval)
    {
        while (true)
        {
            yield return StartCoroutine(PerformScanRoutine());
            yield return new WaitForSeconds(interval);
        }
    }

    private LidarRay[] PerformScan()
    {
        return currentMode switch
        {
            LidarMode.FourDirections => ScanFourDirections(),
            LidarMode.Cone45Degrees => ScanCone45(),
            LidarMode.Full360 => ScanFull360(),
            _ => ScanFourDirections()
        };
    }

    private LidarRay[] ScanFourDirections()
    {
        LidarRay[] rays = new LidarRay[4];
        float[] angles = { 0f, 90f, 180f, 270f };

        for (int i = 0; i < 4; i++)
        {
            rays[i] = CastSingleRay(angles[i]);
        }
        return rays;
    }

    private LidarRay[] ScanCone45()
    {
        int rayCount = 180;
        LidarRay[] rays = new LidarRay[rayCount];
        float coneAngle = 45f;
        float angleStep = coneAngle / (rayCount - 1);
        float startAngle = -coneAngle / 2f;

        for (int i = 0; i < rayCount; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            rays[i] = CastSingleRay(currentAngle);
        }
        return rays;
    }

    private LidarRay[] ScanFull360()
    {
        int rayCount = 455;
        LidarRay[] rays = new LidarRay[rayCount];
        float angleStep = 360f / rayCount;

        for (int i = 0; i < rayCount; i++)
        {
            float currentAngle = i * angleStep;
            rays[i] = CastSingleRay(currentAngle);
        }
        return rays;
    }

    private LidarRay CastSingleRay(float angle)
    {
        Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
        RaycastHit hit;

        bool hasHit = Physics.Raycast(transform.position, direction, out hit, maxScanDistance, obstacleLayerMask);

        if (hasHit)
        {
            return new LidarRay(angle, hit.distance, hit.point, true);
        }
        else
        {
            return new LidarRay(angle, maxScanDistance, transform.position + direction * maxScanDistance, false);
        }
    }

    private IEnumerator SimulateDataTransfer(LidarRay[] data)
    {
        int bitsPerScan = data.Length * 32;
        float transferTime = bitsPerScan / (float)baudRate;
        yield return new WaitForSeconds(transferTime);
    }

    private void VisualizeRays(LidarRay[] rays)
    {
        foreach (var ray in rays)
        {
            Color color = ray.hitObstacle ? Color.red : Color.green;
            Debug.DrawLine(transform.position, ray.hitPoint, color, visualizationDuration);
        }
    }

    // === ДЕБАГ ИНФОРМАЦИЯ ===

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Отрисовка направления лидара в сцене
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}