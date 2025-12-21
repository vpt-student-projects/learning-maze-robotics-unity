using UnityEngine;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;

public class CarAPIController : MonoBehaviour
{
    [Header("API Settings")]
    public int port = 8080;
    public bool autoStartServer = true;
    public bool runInBackground = true;

    [Header("Lidar Integration")]
    public LidarController lidarController;
    public bool enableLidarAPI = true;

    private HttpListener httpListener;
    private CarController carController;
    private MazeGenerator mazeGenerator;
    private bool isServerRunning = false;
    private CancellationTokenSource cancellationTokenSource;
    private float serverStartTime;

    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    private bool isApplicationFocused = true;

    // Кэшированные значения для доступа из любого потока
    private float cachedTime = 0f;
    private Vector3 cachedCarPosition = Vector3.zero;
    private bool cacheInitialized = false;
    // время начала логирования действий
    private float logStartTime = -1f;


    [Serializable]
    public class MovementRecord
    {
        public string action;
        public string timestamp;

        // НОВОЕ: время в секундах от начала записи
        public float time_sec;

        public Vector2Int position; // глобальная позиция машины
    }


    private List<MovementRecord> movementLog = new List<MovementRecord>();

    public void SetCarController(CarController controller)
    {
        if (controller != null)
        {
            carController = controller;

            if (autoStartServer && !isServerRunning)
            {
                StartServer();
            }
        }
    }


    void Start()
    {
        ConfigureBackgroundSettings();
        isApplicationFocused = Application.isFocused;
        serverStartTime = Time.time;
        cacheInitialized = true;

        if (carController == null)
        {
            carController = FindAnyObjectByType<CarController>();
        }

        if (lidarController == null && enableLidarAPI)
        {
            lidarController = FindAnyObjectByType<LidarController>();
        }

        mazeGenerator = FindAnyObjectByType<MazeGenerator>();

        if (autoStartServer)
        {
            StartCoroutine(DelayedStartServer());
        }
    }

    void Update()
    {
        isApplicationFocused = Application.isFocused;

        cachedTime = Time.time;

        if (carController != null && carController.transform != null)
        {
            cachedCarPosition = carController.transform.position;
        }

        while (mainThreadActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }

        if (Time.frameCount % 300 == 0)
        {
            if (isServerRunning && carController == null)
            {
                carController = FindAnyObjectByType<CarController>();
            }
        }
    }

    private System.Collections.IEnumerator DelayedStartServer()
    {
        yield return new WaitForSeconds(1f);

        if (carController != null && !isServerRunning)
        {
            StartServer();
        }
    }

    void OnDestroy()
    {
        StopServer();
    }

    private void ConfigureBackgroundSettings()
    {
        Application.runInBackground = runInBackground;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    public void StartServer()
    {
        if (isServerRunning) return;

        StartCoroutine(StartServerCoroutine());
    }

    public void StopServer()
    {
        isServerRunning = false;

        // Сначала отменяем, потом освобождаем
        try
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                // Даем немного времени на обработку отмены
                Thread.Sleep(100);
            }
        }
        catch (ObjectDisposedException)
        {
            // Игнорируем, если уже освобожден
        }

        // Освобождаем ресурсы
        try
        {
            if (httpListener != null && httpListener.IsListening)
            {
                httpListener.Stop();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error stopping HTTP listener: {e.Message}");
        }

        try
        {
            httpListener?.Close();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error closing HTTP listener: {e.Message}");
        }

        try
        {
            cancellationTokenSource?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Уже освобожден - игнорируем
        }

        httpListener = null;
        cancellationTokenSource = null;
    }

    private System.Collections.IEnumerator StartServerCoroutine()
    {
        httpListener = new HttpListener();
        httpListener.Prefixes.Add($"http://*:{port}/");

        try
        {
            httpListener.Start();
            isServerRunning = true;
            cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => HandleRequestsAsync(cancellationTokenSource.Token));
            Debug.Log($"API Server started on port {port}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to start API server: {e.Message}");
        }

        yield return null;
    }

    private async Task HandleRequestsAsync(CancellationToken cancellationToken)
    {
        while (isServerRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await httpListener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => ProcessRequestAsync(context), cancellationToken);
            }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            string responseText = await HandleRequest(request, response);
            await SendResponse(response, responseText);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error processing request: {ex.Message}");
            await SendErrorResponse(response, 500, $"Internal server error: {ex.Message}");
        }
    }
    private System.Collections.IEnumerator ReplayMovements(MovementRecord[] records)
    {
        float lastTime = 0f;

        foreach (var rec in records)
        {
            float wait = Mathf.Max(0f, rec.time_sec - lastTime);
            lastTime = rec.time_sec;

            yield return new WaitForSeconds(wait);

            switch (rec.action)
            {
                case "move_forward": carController.MoveForward(); break;
                case "move_backward": carController.MoveBackward(); break;
                case "turn_left": carController.TurnLeft(); break;
                case "turn_right": carController.TurnRight(); break;
            }
        }
    }


    private async Task<string> HandleReplayRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            string body;
            using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            var wrapper = JsonUtility.FromJson<MovementRecordListWrapper>(body);
            if (wrapper?.records == null || wrapper.records.Length == 0)
            {
                response.StatusCode = 400;
                return "{\"status\":\"error\",\"message\":\"No actions provided\"}";
            }

            mainThreadActions.Enqueue(() => StartCoroutine(ReplayMovements(wrapper.records)));

            return "{\"status\":\"success\",\"message\":\"Replay started\"}";
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    // Обёртка для массива движений
    [Serializable]
    private class MovementRecordListWrapper
    {
        public MovementRecord[] records;
    }


    private async Task<string> HandleRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        string path = request.Url.LocalPath;
        string method = request.HttpMethod;

        try
        {
            if (path.Equals("/replay", StringComparison.OrdinalIgnoreCase) && method == "POST")
            {
                return await HandleReplayRequest(request, response);
            }

            if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                return GetHealthStatus();
            }

            if (enableLidarAPI && path.StartsWith("/lidar/", StringComparison.OrdinalIgnoreCase))
            {
                return await HandleLidarRequest(path.ToLower(), method, response);
            }

            if (path.StartsWith("/car/", StringComparison.OrdinalIgnoreCase))
            {
                return await HandleCarRequest(path.ToLower(), method, response);
            }

            if (path.Equals("/status", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                return GetFullStatus();
            }

            if (path.Equals("/info", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                return GetSystemInfo();
            }

            // НОВЫЙ ENDPOINT: статус таймера
            if (path.Equals("/timer/status", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                return GetTimerStatus();
            }

            // НОВЫЙ ENDPOINT: рестарт
            if (path.Equals("/car/restart", StringComparison.OrdinalIgnoreCase) && method == "POST")
            {
                return await HandleRestartRequest(response);
            }

            response.StatusCode = 404;
            return "{\"status\":\"error\",\"message\":\"Endpoint not found\"}";
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in HandleRequest: {ex.Message}");
            response.StatusCode = 500;
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    // НОВЫЙ МЕТОД: статус таймера
    private string GetTimerStatus()
    {
        try
        {
            MazeTimer timer = FindObjectOfType<MazeTimer>();
            if (timer == null)
            {
                return "{\"status\":\"error\",\"message\":\"Timer not found\"}";
            }

            return $"{{\"status\":\"success\",\"timer\":{{\"running\":{timer.IsRunning.ToString().ToLower()},\"time\":{timer.CurrentTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"finished\":{timer.HasReachedFinish.ToString().ToLower()}}}}}";
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    // НОВЫЙ МЕТОД: обработка рестарта
    private async Task<string> HandleRestartRequest(HttpListenerResponse response)
    {
        try
        {
            mainThreadActions.Enqueue(() => {
                MazeTimer timer = FindObjectOfType<MazeTimer>();
                if (timer != null)
                {
                    timer.OnRestartButtonClick();
                }
                else
                {
                    Debug.LogWarning("MazeTimer not found for restart");
                }
            });

            return $"{{\"status\":\"success\",\"action\":\"restart\",\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"}}";
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in HandleRestartRequest: {ex.Message}");
            response.StatusCode = 500;
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private string GetHealthStatus()
    {
        try
        {
            bool carReady = carController != null && carController.IsCarReady();
            bool lidarReady = lidarController != null && lidarController.IsInitialized();
            bool apiReady = isServerRunning;
            float uptime = cacheInitialized ? cachedTime - serverStartTime : 0f;

            return $"{{\"status\":\"{(apiReady ? "healthy" : "unhealthy")}\",\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",\"services\":{{\"car\":{carReady.ToString().ToLower()},\"lidar\":{lidarReady.ToString().ToLower()},\"api\":{apiReady.ToString().ToLower()}}},\"uptime\":{uptime.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}}}";
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private string EscapeJsonString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        StringBuilder sb = new StringBuilder();
        foreach (char c in input)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\"': sb.Append("\\\""); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                    {
                        sb.AppendFormat("\\u{0:X4}", (int)c);
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    private async Task<string> HandleLidarRequest(string path, string method, HttpListenerResponse response)
    {
        try
        {
            if (lidarController == null)
            {
                response.StatusCode = 503;
                return "{\"status\":\"error\",\"message\":\"Lidar controller not available\"}";
            }

            if (!lidarController.IsInitialized())
            {
                response.StatusCode = 503;
                return "{\"status\":\"error\",\"message\":\"Lidar controller initializing\"}";
            }

            switch (path)
            {
                case "/lidar/status" when method == "GET":
                    return GetLidarStatus();

                case "/lidar/points" when method == "GET":
                    return GetLidarPointsList();

                case "/lidar/min" when method == "GET":
                    return GetLidarMinDistance();

                case "/lidar/simple" when method == "GET":
                    return GetSafeLidarData();

                default:
                    if (path.StartsWith("/lidar/point/"))
                    {
                        return HandleSafeLidarPointRequest(path, method, response);
                    }

                    response.StatusCode = 404;
                    return "{\"status\":\"error\",\"message\":\"Lidar endpoint not found\"}";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in HandleLidarRequest: {ex.Message}");
            response.StatusCode = 500;
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private string GetLidarStatus()
    {
        try
        {
            if (lidarController == null)
                return "{\"status\":\"error\",\"message\":\"Lidar controller not found\"}";

            int totalPoints = lidarController.lidarPoints?.Count ?? 0;
            int activePoints = 0;

            if (lidarController.lidarPoints != null)
            {
                foreach (var point in lidarController.lidarPoints)
                {
                    if (point.enabled) activePoints++;
                }
            }

            float minDistance = lidarController.GetGlobalMinDistance();

            return $"{{\"status\":\"success\",\"data\":{{\"initialized\":{lidarController.IsInitialized().ToString().ToLower()},\"points\":{{\"total\":{totalPoints},\"active\":{activePoints}}},\"minDistance\":{(minDistance >= 0 ? minDistance.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "null")},\"layerMask\":\"Wall\"}}}}";
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private string GetSafeLidarData()
    {
        try
        {
            if (lidarController == null)
                return "{\"status\":\"error\",\"message\":\"Lidar controller not available\"}";

            StringBuilder sb = new StringBuilder();
            sb.Append("{\"status\":\"success\",\"data\":[");

            bool firstPoint = true;
            for (int i = 0; i < lidarController.lidarPoints.Count; i++)
            {
                var point = lidarController.lidarPoints[i];
                if (!point.enabled) continue;

                if (!firstPoint) sb.Append(",");
                firstPoint = false;

                sb.Append("{");
                sb.Append($"\"index\":{i},");
                sb.Append($"\"name\":\"{EscapeJsonString(point.name)}\",");

                sb.Append($"\"position\":{{\"x\":0.00,\"y\":0.00,\"z\":0.00}},");

                if (point.enableSingleLidar)
                {
                    sb.Append($"\"single\":{{\"direction\":\"{point.singleLidarDirection.ToString().ToLower()}\",\"distance\":{point.singleLidarResult.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}}},");
                }

                if (point.enable360Lidar && point.lidar360Results != null && point.lidar360Results.Length > 0)
                {
                    sb.Append("\"lidar360\":[");
                    int pointsToShow = Mathf.Min(4, point.lidar360Points);
                    int step = Mathf.Max(1, point.lidar360Points / pointsToShow);

                    for (int j = 0; j < point.lidar360Points && j < pointsToShow * step; j += step)
                    {
                        if (j > 0) sb.Append(",");
                        float angle = (360f / point.lidar360Points) * j;
                        sb.Append($"{{\"angle\":{angle.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)},\"distance\":{point.lidar360Results[j].ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}}}");
                    }
                    sb.Append("],");
                }

                if (sb[sb.Length - 1] == ',')
                    sb.Remove(sb.Length - 1, 1);

                sb.Append("}");
            }

            sb.Append($"],\"count\":{lidarController.lidarPoints.Count}}}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private string GetLidarPointsList()
    {
        try
        {
            if (lidarController.lidarPoints == null || lidarController.lidarPoints.Count == 0)
            {
                return "{\"status\":\"success\",\"points\":[],\"count\":0}";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("{\"status\":\"success\",\"points\":[");

            for (int i = 0; i < lidarController.lidarPoints.Count; i++)
            {
                var point = lidarController.lidarPoints[i];
                sb.Append($"{{\"index\":{i},\"name\":\"{EscapeJsonString(point.name)}\",\"enabled\":{point.enabled.ToString().ToLower()}}}");
                if (i < lidarController.lidarPoints.Count - 1) sb.Append(",");
            }

            sb.Append($"],\"count\":{lidarController.lidarPoints.Count}}}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private string GetLidarMinDistance()
    {
        try
        {
            if (lidarController == null)
                return "{\"status\":\"error\",\"message\":\"Lidar controller not available\"}";

            float minDistance = lidarController.GetGlobalMinDistance();
            return $"{{\"status\":\"success\",\"minDistance\":{(minDistance >= 0 ? minDistance.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "null")}}}";
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private string HandleSafeLidarPointRequest(string path, string method, HttpListenerResponse response)
    {
        try
        {
            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3 || !int.TryParse(parts[2], out int pointIndex))
            {
                response.StatusCode = 400;
                return "{\"status\":\"error\",\"message\":\"Invalid point index\"}";
            }

            if (pointIndex < 0 || pointIndex >= lidarController.lidarPoints.Count)
            {
                response.StatusCode = 404;
                return $"{{\"status\":\"error\",\"message\":\"Point index {pointIndex} out of range\"}}";
            }

            var point = lidarController.lidarPoints[pointIndex];
            if (!point.enabled)
            {
                return "{\"status\":\"error\",\"message\":\"Point is disabled\"}";
            }

            if (parts.Length == 3)
            {
                return GetSafeLidarPointData(point, pointIndex);
            }
            else if (parts.Length == 4)
            {
                string dataType = parts[3].ToLower();

                switch (dataType)
                {
                    case "single":
                        if (point.enableSingleLidar)
                        {
                            return $"{{\"status\":\"success\",\"singleLidar\":{{\"direction\":\"{point.singleLidarDirection.ToString().ToLower()}\",\"distance\":{point.singleLidarResult.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}}}}}";
                        }
                        return "{\"status\":\"error\",\"message\":\"Single lidar not enabled\"}";

                    case "360":
                        if (point.enable360Lidar && point.lidar360Results != null)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.Append("{\"status\":\"success\",\"lidar360\":{\"distances\":[");

                            for (int i = 0; i < point.lidar360Results.Length; i++)
                            {
                                sb.Append(point.lidar360Results[i].ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                                if (i < point.lidar360Results.Length - 1) sb.Append(",");
                            }

                            sb.Append($"],\"range\":{point.lidar360Range.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"points\":{point.lidar360Points}}}");
                            return sb.ToString();
                        }
                        return "{\"status\":\"error\",\"message\":\"360° lidar not enabled or no data\"}";

                    default:
                        response.StatusCode = 400;
                        return "{\"status\":\"error\",\"message\":\"Invalid data type\"}";
                }
            }

            response.StatusCode = 404;
            return "{\"status\":\"error\",\"message\":\"Invalid request format\"}";
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in HandleSafeLidarPointRequest: {ex.Message}");
            response.StatusCode = 500;
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private string GetSafeLidarPointData(LidarPoint point, int index)
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"status\":\"success\",");
            sb.Append($"\"point\":{{");
            sb.Append($"\"index\":{index},");
            sb.Append($"\"name\":\"{EscapeJsonString(point.name)}\",");
            sb.Append($"\"position\":{{\"x\":0.00,\"y\":0.00,\"z\":0.00}},");
            sb.Append($"\"enabledLidars\":{{");
            sb.Append($"\"lidar360\":{point.enable360Lidar.ToString().ToLower()},");
            sb.Append($"\"lidar90\":{point.enable90Lidar.ToString().ToLower()},");
            sb.Append($"\"singleLidar\":{point.enableSingleLidar.ToString().ToLower()}");
            sb.Append($"}}");

            if (point.enableSingleLidar)
            {
                sb.Append($",\"singleLidar\":{{");
                sb.Append($"\"direction\":\"{point.singleLidarDirection.ToString().ToLower()}\",");
                sb.Append($"\"distance\":{point.singleLidarResult.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append($"}}");
            }

            sb.Append($"}}");
            sb.Append($"}}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private async Task<string> HandleCarRequest(string path, string method, HttpListenerResponse response)
    {
        try
        {
            if (carController == null || !carController.IsCarReady())
            {
                response.StatusCode = 503;
                return "{\"status\":\"error\",\"message\":\"Car not ready yet\"}";
            }

            switch (path)
            {
                case "/car/turn/left" when method == "POST":
                    mainThreadActions.Enqueue(() =>
                    {
                        carController.TurnLeft();
                        LogCarAction("turn_left");
                    });
                    return $"{{\"status\":\"success\",\"action\":\"turn_left\",\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"}}";


                case "/car/turn/right" when method == "POST":
                    mainThreadActions.Enqueue(() =>
                    {
                        carController.TurnRight();
                        LogCarAction("turn_right");
                    });
                    return $"{{\"status\":\"success\",\"action\":\"turn_right\",\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"}}";


                case "/car/move/forward" when method == "POST":
                    mainThreadActions.Enqueue(() =>
                    {
                        carController.MoveForward();
                        LogCarAction("move_forward");
                    });
                    return $"{{\"status\":\"success\",\"action\":\"move_forward\",\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"}}";


                case "/car/move/backward" when method == "POST":
                    mainThreadActions.Enqueue(() =>
                    {
                        carController.MoveBackward();
                        LogCarAction("move_backward");
                    });
                    return $"{{\"status\":\"success\",\"action\":\"move_backward\",\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"}}";

                case "/car/restart" when method == "POST": // Уже добавлено выше, но оставляем для совместимости
                    return await HandleRestartRequest(response);

                case "/car/status" when method == "GET":
                    return GetCarStatus();

                case "/car/position" when method == "GET":
                    return GetCarPosition();

                default:
                    response.StatusCode = 404;
                    return "{\"status\":\"error\",\"message\":\"Car endpoint not found\"}";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in HandleCarRequest: {ex.Message}");
            response.StatusCode = 500;
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private string GetCarStatus()
    {
        try
        {
            if (carController == null)
                return "{\"status\":\"car_not_found\"}";

            Vector2Int chunk = Vector2Int.zero;
            Vector2Int cell = Vector2Int.zero;
            string direction = "unknown";

            System.Threading.ManualResetEvent doneEvent = new System.Threading.ManualResetEvent(false);

            mainThreadActions.Enqueue(() => {
                try
                {
                    chunk = carController.GetCurrentChunkCoordinates();
                    cell = carController.GetCurrentCellCoordinates();
                    direction = carController.GetCurrentDirectionName();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error getting car status: {ex.Message}");
                }
                finally
                {
                    doneEvent.Set();
                }
            });

            doneEvent.WaitOne(100);

            return $"{{\"status\":\"operational\",\"position\":{{\"chunk\":{{\"x\":{chunk.x},\"y\":{chunk.y}}},\"cell\":{{\"x\":{cell.x},\"y\":{cell.y}}},\"direction\":\"{EscapeJsonString(direction)}\"}}}}";
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private void LogCarAction(string action)
    {
        if (carController == null) return;

        Vector2Int chunk = carController.GetCurrentChunkCoordinates();
        Vector2Int cell = carController.GetCurrentCellCoordinates();
        int chunkSize = GetMazeChunkSize();
        Vector2Int globalPos = new Vector2Int(chunk.x * chunkSize + cell.x, chunk.y * chunkSize + cell.y);

        if (logStartTime < 0f)
            logStartTime = Time.time;

        movementLog.Add(new MovementRecord
        {
            action = action,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            time_sec = Time.time - logStartTime,
            position = globalPos
        });
    }


    private string GetCarPosition()
    {
        try
        {
            if (carController == null)
                return "{\"status\":\"car_not_found\"}";

            Vector2Int chunk = Vector2Int.zero;
            Vector2Int cell = Vector2Int.zero;
            string direction = "unknown";

            System.Threading.ManualResetEvent doneEvent = new System.Threading.ManualResetEvent(false);

            mainThreadActions.Enqueue(() => {
                try
                {
                    chunk = carController.GetCurrentChunkCoordinates();
                    cell = carController.GetCurrentCellCoordinates();
                    direction = carController.GetCurrentDirectionName();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error getting car position: {ex.Message}");
                }
                finally
                {
                    doneEvent.Set();
                }
            });

            doneEvent.WaitOne(100);

            int chunkSize = GetMazeChunkSize();

            return $"{{\"status\":\"success\",\"position\":{{\"global\":{{\"x\":{chunk.x * chunkSize + cell.x},\"y\":{chunk.y * chunkSize + cell.y}}},\"chunk\":{{\"x\":{chunk.x},\"y\":{chunk.y}}},\"cell\":{{\"x\":{cell.x},\"y\":{cell.y}}},\"direction\":\"{EscapeJsonString(direction)}\"}}}}";
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private int GetMazeChunkSize()
    {
        try
        {
            return mazeGenerator?.chunkSize ?? 4;
        }
        catch
        {
            return 4;
        }
    }

    private string GetFullStatus()
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"status\":\"success\",\"systems\":{");

            bool carReady = false;
            Vector2Int chunk = Vector2Int.zero;
            Vector2Int cell = Vector2Int.zero;
            string direction = "unknown";

            if (carController != null)
            {
                System.Threading.ManualResetEvent doneEvent = new System.Threading.ManualResetEvent(false);

                mainThreadActions.Enqueue(() => {
                    try
                    {
                        carReady = carController.IsCarReady();
                        chunk = carController.GetCurrentChunkCoordinates();
                        cell = carController.GetCurrentCellCoordinates();
                        direction = carController.GetCurrentDirectionName();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error in full status: {ex.Message}");
                    }
                    finally
                    {
                        doneEvent.Set();
                    }
                });

                doneEvent.WaitOne(100);
            }

            if (carReady)
            {
                sb.Append($"\"car\":{{\"ready\":true,\"position\":{{\"chunk\":{{\"x\":{chunk.x},\"y\":{chunk.y}}},\"cell\":{{\"x\":{cell.x},\"y\":{cell.y}}},\"direction\":\"{EscapeJsonString(direction)}\"}}}},");
            }
            else
            {
                sb.Append("\"car\":{\"ready\":false},");
            }

            // ИНФОРМАЦИЯ О ТАЙМЕРЕ И ФИНИШЕ
            MazeTimer timer = FindObjectOfType<MazeTimer>();
            if (timer != null)
            {
                sb.Append($"\"timer\":{{\"running\":{timer.IsRunning.ToString().ToLower()},\"started\":{timer.HasStarted.ToString().ToLower()},\"time\":{timer.CurrentTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"finished\":{timer.HasReachedFinish.ToString().ToLower()}}},");
            }
            else
            {
                sb.Append("\"timer\":{\"available\":false},");
            }

            // ИНФОРМАЦИЯ О ФИНИШНОЙ ЗОНЕ
            if (mazeGenerator != null)
            {
                sb.Append($"\"maze\":{{\"hasFinishArea\":{mazeGenerator.createFinishArea.ToString().ToLower()}}},");
            }

            if (lidarController != null)
            {
                float minDistance = lidarController.GetGlobalMinDistance();
                int pointCount = lidarController.lidarPoints?.Count ?? 0;
                int activePoints = 0;

                if (lidarController.lidarPoints != null)
                {
                    foreach (var point in lidarController.lidarPoints)
                    {
                        if (point.enabled) activePoints++;
                    }
                }

                sb.Append($"\"lidar\":{{\"ready\":true,\"points\":{{\"total\":{pointCount},\"active\":{activePoints}}},\"minDistance\":{(minDistance >= 0 ? minDistance.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "null")}}},");
            }
            else
            {
                sb.Append("\"lidar\":{\"ready\":false},");
            }

            sb.Append($"\"system\":{{\"focus\":{isApplicationFocused.ToString().ToLower()},\"background\":{runInBackground.ToString().ToLower()},\"time\":\"{DateTime.Now:HH:mm:ss}\"}}");

            sb.Append("}}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private string GetSystemInfo()
    {
        try
        {
            return $"{{\"status\":\"success\",\"info\":{{\"name\":\"Maze Car Controller\",\"version\":\"1.1\",\"ports\":{{\"api\":{port}}},\"features\":[\"car_control\",\"lidar_sensors\",\"maze_navigation\",\"timer\",\"restart\"],\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"}}}}";
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(ex.Message)}\"}}";
        }
    }

    private async Task SendResponse(HttpListenerResponse response, string responseText)
    {
        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(responseText);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error sending response: {ex.Message}");
        }
    }

    private async Task SendErrorResponse(HttpListenerResponse response, int statusCode, string message)
    {
        try
        {
            string errorText = $"{{\"status\":\"error\",\"message\":\"{EscapeJsonString(message)}\"}}";
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            byte[] buffer = Encoding.UTF8.GetBytes(errorText);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error sending error response: {ex.Message}");
        }
    }

    public bool IsServerRunning() => isServerRunning;
}