using System.Text;
using UnityEngine;
using TMPro;

public class LidarHUDReceiverTMP : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LidarController lidarController;
    [SerializeField] private CarAPIController carApiController;
    [SerializeField] private TMP_Text outputText;

    [Header("Output Settings")]
    [SerializeField] private bool autoFindLidarController = true;
    [SerializeField] private bool autoFindCarApiController = true;
    [SerializeField] private bool subscribeToScanEvent = true;
    [SerializeField] private float updateInterval = 0.2f;
    [SerializeField] private float autoFindRetryInterval = 0.5f;
    [SerializeField] private int maxShownPoints = 8;
    [SerializeField] private bool includeDisabledPoints = false;
    [SerializeField] private bool showGlobalMinDistance = true;
    [SerializeField] private bool showSingleLidar = true;
    [SerializeField] private bool show90LidarSummary = true;
    [SerializeField] private bool show360LidarSummary = true;

    private float lastUpdateTime = -999f;
    private float lastFindAttemptTime = -999f;
    private readonly StringBuilder sb = new StringBuilder(1024);

    private void Start()
    {
        CarController.CarSpawned += OnCarSpawned;
        TryResolveControllers(force: true);
        RefreshOutput();
    }

    private void Update()
    {
        TryResolveControllers(force: false);

        if (!subscribeToScanEvent && Time.time - lastUpdateTime >= updateInterval)
        {
            RefreshOutput();
        }
    }

    private void OnLidarScanCompleted(LidarController _)
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            RefreshOutput();
        }
    }

    private void RefreshOutput()
    {
        lastUpdateTime = Time.time;

        if (lidarController == null)
        {
            SetOutput("Lidar HUD: waiting for spawned car/lidar...");
            return;
        }

        sb.Clear();
        sb.AppendLine("<b>LIDAR LIVE</b>");
        sb.Append("Points: ").Append(lidarController.lidarPoints.Count).AppendLine();

        if (showGlobalMinDistance)
        {
            float min = lidarController.GetGlobalMinDistance();
            if (min >= 0f)
            {
                sb.Append("Global Min: ").Append(min.ToString("F2")).AppendLine(" m");
            }
            else
            {
                sb.AppendLine("Global Min: n/a");
            }
        }

        int shown = 0;
        for (int i = 0; i < lidarController.lidarPoints.Count; i++)
        {
            if (shown >= maxShownPoints) break;

            LidarPoint point = lidarController.lidarPoints[i];
            if (point == null) continue;
            if (!includeDisabledPoints && !point.enabled) continue;

            sb.Append("[").Append(i).Append("] ").Append(point.name).AppendLine();

            if (showSingleLidar && point.enableSingleLidar)
            {
                sb.Append("  Single ").Append(point.singleLidarDirection)
                  .Append(": ").Append(point.singleLidarResult.ToString("F2")).AppendLine(" m");
            }

            if (show90LidarSummary && point.enable90Lidar && point.lidar90Results != null && point.lidar90Results.Length > 0)
            {
                int left = 0;
                int center = point.lidar90Results.Length / 2;
                int right = point.lidar90Results.Length - 1;

                sb.Append("  90: L=").Append(point.lidar90Results[left].ToString("F2"))
                  .Append(" C=").Append(point.lidar90Results[center].ToString("F2"))
                  .Append(" R=").Append(point.lidar90Results[right].ToString("F2"))
                  .AppendLine(" m");
            }

            if (show360LidarSummary && point.enable360Lidar && point.lidar360Results != null && point.lidar360Results.Length > 0)
            {
                float min360 = float.MaxValue;
                for (int j = 0; j < point.lidar360Results.Length; j++)
                {
                    if (point.lidar360Results[j] < min360)
                    {
                        min360 = point.lidar360Results[j];
                    }
                }

                sb.Append("  360 min: ").Append(min360.ToString("F2")).AppendLine(" m");
            }

            shown++;
        }

        if (shown == 0)
        {
            sb.AppendLine("No lidar points to display");
        }

        SetOutput(sb.ToString());
    }

    private void SetOutput(string text)
    {
        if (outputText != null)
        {
            outputText.text = text;
        }
    }

    private void OnDestroy()
    {
        CarController.CarSpawned -= OnCarSpawned;
        UnsubscribeFromLidarEvents();
    }

    private void TryResolveControllers(bool force)
    {
        if (!force && Time.time - lastFindAttemptTime < autoFindRetryInterval)
        {
            return;
        }

        lastFindAttemptTime = Time.time;

        if (carApiController == null && autoFindCarApiController)
        {
            carApiController = FindObjectOfType<CarAPIController>();
        }

        LidarController resolved = lidarController;

        if (resolved == null && carApiController != null)
        {
            resolved = carApiController.lidarController;
        }

        if (resolved == null && autoFindLidarController)
        {
            resolved = FindObjectOfType<LidarController>();
        }

        if (resolved != lidarController)
        {
            UnsubscribeFromLidarEvents();
            lidarController = resolved;

            if (lidarController != null && subscribeToScanEvent)
            {
                lidarController.ScanCompleted += OnLidarScanCompleted;
            }
        }
    }

    private void UnsubscribeFromLidarEvents()
    {
        if (lidarController != null)
        {
            lidarController.ScanCompleted -= OnLidarScanCompleted;
        }
    }

    private void OnCarSpawned(CarController _, LidarController spawnedLidar)
    {
        if (spawnedLidar == null)
        {
            TryResolveControllers(force: true);
            return;
        }

        if (spawnedLidar != lidarController)
        {
            UnsubscribeFromLidarEvents();
            lidarController = spawnedLidar;

            if (subscribeToScanEvent)
            {
                lidarController.ScanCompleted += OnLidarScanCompleted;
            }
        }

        RefreshOutput();
    }
}
