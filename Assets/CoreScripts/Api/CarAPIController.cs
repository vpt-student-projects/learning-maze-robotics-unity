using UnityEngine;
using System.Collections;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

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
    private bool isServerRunning = false;
    private CancellationTokenSource cancellationTokenSource;

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

        if (carController == null)
        {
            carController = FindAnyObjectByType<CarController>();
        }

        if (lidarController == null && enableLidarAPI)
        {
            lidarController = FindAnyObjectByType<LidarController>();
        }

        if (autoStartServer)
        {
            StartCoroutine(DelayedStartServer());
        }
    }

    private IEnumerator DelayedStartServer()
    {
        yield return new WaitForSeconds(1f);

        if (carController != null && !isServerRunning)
        {
            StartServer();
        }
    }

    void Update()
    {
        if (Time.frameCount % 300 == 0)
        {
            if (isServerRunning && carController == null)
            {
                carController = FindAnyObjectByType<CarController>();
            }
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
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();

        if (httpListener != null)
        {
            if (httpListener.IsListening)
                httpListener.Stop();
            httpListener.Close();
        }
    }

    private IEnumerator StartServerCoroutine()
    {
        httpListener = new HttpListener();
        httpListener.Prefixes.Add($"http://localhost:{port}/");
        httpListener.Prefixes.Add($"http://127.0.0.1:{port}/");

        try
        {
            httpListener.Start();
            isServerRunning = true;
            cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => HandleRequestsAsync(cancellationTokenSource.Token));
        }
        catch (System.Exception e)
        {
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

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            string responseText = await HandleRequest(request, response);
            await SendResponse(response, responseText);
        }
        catch
        {
            await SendErrorResponse(response, 500, "Internal server error");
        }
    }

    private async Task<string> HandleRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        string path = request.Url.LocalPath.ToLower();
        string method = request.HttpMethod;

        if (path == "/health" && method == "GET")
        {
            return GetHealthStatus();
        }

        if (enableLidarAPI && path.StartsWith("/lidar/"))
        {
            return await HandleLidarRequest(path, method, response);
        }

        if (path.StartsWith("/car/"))
        {
            return await HandleCarRequest(path, method, response);
        }

        if (path == "/status" && method == "GET")
        {
            return GetFullStatus();
        }

        if (path == "/info" && method == "GET")
        {
            return GetSystemInfo();
        }

        response.StatusCode = 404;
        return "{\"status\":\"error\",\"message\":\"Endpoint not found\"}";
    }

    private string GetHealthStatus()
    {
        bool carReady = carController != null && carController.IsCarReady();
        bool lidarReady = lidarController != null;

        return $"{{\"status\":\"healthy\",\"services\":{{\"car\":{carReady.ToString().ToLower()},\"lidar\":{lidarReady.ToString().ToLower()}}},\"timestamp\":\"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\"}}";
    }

    private async Task<string> HandleLidarRequest(string path, string method, HttpListenerResponse response)
    {
        if (lidarController == null)
        {
            response.StatusCode = 503;
            return "{\"status\":\"error\",\"message\":\"Lidar controller not available\"}";
        }

        switch (path)
        {
            case "/lidar/all" when method == "GET":
                return lidarController.GetAllLidarDataJSON();

            case "/lidar/points" when method == "GET":
                return GetLidarPointsList();

            case "/lidar/global/min" when method == "GET":
                return GetLidarMinDistance();

            default:
                if (path.StartsWith("/lidar/point/"))
                {
                    return HandleLidarPointRequest(path, method, response);
                }

                response.StatusCode = 404;
                return "{\"status\":\"error\",\"message\":\"Lidar endpoint not found\"}";
        }
    }

    private string GetLidarPointsList()
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
            sb.Append($"{{\"index\":{i},\"name\":\"{point.name}\",\"enabled\":{point.enabled.ToString().ToLower()}}}");
            if (i < lidarController.lidarPoints.Count - 1) sb.Append(",");
        }

        sb.Append($"],\"count\":{lidarController.lidarPoints.Count}}}");
        return sb.ToString();
    }

    private string HandleLidarPointRequest(string path, string method, HttpListenerResponse response)
    {
        string[] parts = path.Split('/');
        if (parts.Length < 4)
        {
            response.StatusCode = 400;
            return "{\"status\":\"error\",\"message\":\"Invalid point request\"}";
        }

        if (int.TryParse(parts[3], out int pointIndex))
        {
            var point = lidarController.GetLidarPoint(pointIndex);
            if (point == null)
            {
                response.StatusCode = 404;
                return $"{{\"status\":\"error\",\"message\":\"Point {pointIndex} not found\"}}";
            }

            if (parts.Length == 4)
            {
                return GetLidarPointFullData(point, pointIndex);
            }
            else if (parts.Length == 5)
            {
                return GetLidarPointDataType(point, parts[4]);
            }
        }

        response.StatusCode = 400;
        return "{\"status\":\"error\",\"message\":\"Invalid point index\"}";
    }

    private string GetLidarPointFullData(LidarPoint point, int index)
    {
        float forward = lidarController.GetForwardDistance(index);
        float right = lidarController.GetRightDistance(index);
        float backward = lidarController.GetBackwardDistance(index);
        float left = lidarController.GetLeftDistance(index);

        return $"{{\"status\":\"success\",\"point\":{{\"index\":{index},\"name\":\"{point.name}\"," +
               $"\"position\":{{\"x\":{point.pointTransform.position.x:F2},\"y\":{point.pointTransform.position.y:F2},\"z\":{point.pointTransform.position.z:F2}}}," +
               $"\"singleLidars\":{{\"forward\":{forward:F2},\"right\":{right:F2},\"backward\":{backward:F2},\"left\":{left:F2}}}" +
               $"}}}}";
    }

    private string GetLidarPointDataType(LidarPoint point, string dataType)
    {
        switch (dataType.ToLower())
        {
            case "360":
                return GetLidar360Data(point);

            case "single":
                return GetLidarSingleData(point);

            default:
                return "{\"status\":\"error\",\"message\":\"Invalid data type\"}";
        }
    }

    private string GetLidar360Data(LidarPoint point)
    {
        if (!point.enable360Lidar || point.lidar360Results == null)
        {
            return "{\"status\":\"error\",\"message\":\"360° lidar not enabled\"}";
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("{\"status\":\"success\",\"lidar360\":[");

        for (int i = 0; i < point.lidar360Results.Length; i++)
        {
            sb.Append(point.lidar360Results[i].ToString("F2"));
            if (i < point.lidar360Results.Length - 1) sb.Append(",");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private string GetLidarSingleData(LidarPoint point)
    {
        if (!point.enableSingleLidars || point.singleLidarResults == null)
        {
            return "{\"status\":\"error\",\"message\":\"Single lidars not enabled\"}";
        }

        return $"{{\"status\":\"success\",\"singleLidars\":{{\"forward\":{point.singleLidarResults[0]:F2}," +
               $"\"right\":{point.singleLidarResults[1]:F2},\"backward\":{point.singleLidarResults[2]:F2}," +
               $"\"left\":{point.singleLidarResults[3]:F2}}}}}";
    }

    private string GetLidarMinDistance()
    {
        float minDistance = lidarController.GetGlobalMinDistance();
        return $"{{\"status\":\"success\",\"minDistance\":{minDistance:F2}}}";
    }

    private async Task<string> HandleCarRequest(string path, string method, HttpListenerResponse response)
    {
        if (carController == null || !carController.IsCarReady())
        {
            response.StatusCode = 503;
            return "{\"status\":\"error\",\"message\":\"Car not ready yet\"}";
        }

        switch (path)
        {
            case "/car/turn/left" when method == "POST":
                MainThreadDispatcher.ExecuteOnMainThread(() => carController.TurnLeft());
                return "{\"status\":\"success\",\"action\":\"turn_left\",\"timestamp\":\"" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";

            case "/car/turn/right" when method == "POST":
                MainThreadDispatcher.ExecuteOnMainThread(() => carController.TurnRight());
                return "{\"status\":\"success\",\"action\":\"turn_right\",\"timestamp\":\"" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";

            case "/car/move/forward" when method == "POST":
                MainThreadDispatcher.ExecuteOnMainThread(() => carController.MoveForward());
                return "{\"status\":\"success\",\"action\":\"move_forward\",\"timestamp\":\"" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";

            case "/car/move/backward" when method == "POST":
                MainThreadDispatcher.ExecuteOnMainThread(() => carController.MoveBackward());
                return "{\"status\":\"success\",\"action\":\"move_backward\",\"timestamp\":\"" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";

            case "/car/stop" when method == "POST":
                return "{\"status\":\"success\",\"action\":\"stop\",\"timestamp\":\"" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";

            case "/car/status" when method == "GET":
                return GetCarStatus();

            case "/car/position" when method == "GET":
                return GetCarPosition();

            default:
                response.StatusCode = 404;
                return "{\"status\":\"error\",\"message\":\"Car endpoint not found\"}";
        }
    }

    private string GetCarStatus()
    {
        if (carController == null)
            return "{\"status\":\"car_not_found\"}";

        var chunk = carController.GetCurrentChunkCoordinates();
        var cell = carController.GetCurrentCellCoordinates();
        string direction = carController.GetCurrentDirectionName();

        return $"{{\"status\":\"operational\",\"position\":{{\"chunk\":{{\"x\":{chunk.x},\"y\":{chunk.y}}},\"cell\":{{\"x\":{cell.x},\"y\":{cell.y}}},\"direction\":\"{direction}\"}}}}";
    }

    private string GetCarPosition()
    {
        if (carController == null)
            return "{\"status\":\"car_not_found\"}";

        var chunk = carController.GetCurrentChunkCoordinates();
        var cell = carController.GetCurrentCellCoordinates();
        string direction = carController.GetCurrentDirectionName();

        return $"{{\"status\":\"success\",\"position\":{{\"global\":{{\"x\":{chunk.x * GetMazeChunkSize() + cell.x},\"y\":{chunk.y * GetMazeChunkSize() + cell.y}}},\"chunk\":{{\"x\":{chunk.x},\"y\":{chunk.y}}},\"cell\":{{\"x\":{cell.x},\"y\":{cell.y}}},\"direction\":\"{direction}\"}}}}";
    }

    private int GetMazeChunkSize()
    {
        var mazeGenerator = FindAnyObjectByType<MazeGenerator>();
        return mazeGenerator != null ? mazeGenerator.chunkSize : 4;
    }

    private string GetFullStatus()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{\"status\":\"success\",\"systems\":{");

        if (carController != null && carController.IsCarReady())
        {
            var chunk = carController.GetCurrentChunkCoordinates();
            var cell = carController.GetCurrentCellCoordinates();
            string direction = carController.GetCurrentDirectionName();

            sb.Append($"\"car\":{{\"ready\":true,\"position\":{{\"chunk\":{{\"x\":{chunk.x},\"y\":{chunk.y}}},\"cell\":{{\"x\":{cell.x},\"y\":{cell.y}}},\"direction\":\"{direction}\"}}}},");
        }
        else
        {
            sb.Append("\"car\":{\"ready\":false},");
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

            sb.Append($"\"lidar\":{{\"ready\":true,\"points\":{{\"total\":{pointCount},\"active\":{activePoints}}},\"minDistance\":{minDistance:F2}}},");
        }
        else
        {
            sb.Append("\"lidar\":{\"ready\":false},");
        }

        sb.Append($"\"system\":{{\"focus\":{Application.isFocused.ToString().ToLower()},\"background\":{Application.runInBackground.ToString().ToLower()},\"time\":\"{System.DateTime.Now:HH:mm:ss}\"}}");

        sb.Append("}}");
        return sb.ToString();
    }

    private string GetSystemInfo()
    {
        return $"{{\"status\":\"success\",\"info\":{{\"name\":\"Maze Car Controller\",\"version\":\"1.0\",\"ports\":{{\"api\":{port}}},\"features\":[\"car_control\",\"lidar_sensors\",\"maze_navigation\"],\"timestamp\":\"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\"}}}}";
    }

    private async Task SendResponse(HttpListenerResponse response, string responseText)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(responseText);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    private async Task SendErrorResponse(HttpListenerResponse response, int statusCode, string message)
    {
        string errorText = $"{{\"status\":\"error\",\"message\":\"{message}\"}}";
        response.StatusCode = statusCode;
        byte[] buffer = Encoding.UTF8.GetBytes(errorText);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    public bool IsServerRunning() => isServerRunning;
}