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

    private HttpListener httpListener;
    private CarController carController;
    private bool isServerRunning = false;
    private CancellationTokenSource cancellationTokenSource;

    public void SetCarController(CarController controller)
    {
        if (controller != null)
        {
            carController = controller;
            Debug.Log("✅ CarController assigned to API");

            if (autoStartServer && !isServerRunning)
            {
                StartServer();
            }
        }
    }

    void Start()
    {
        ConfigureBackgroundSettings();

        // Не запускаем сервер сразу - ждем пока MazeGenerator вызовет SetCarController
        if (autoStartServer)
        {
            Debug.Log("⏳ API waiting for car controller assignment...");
        }
    }

    void Update()
    {
        // Периодическая проверка состояния
        if (Time.frameCount % 300 == 0)
        {
            if (isServerRunning && carController == null)
            {
                Debug.LogWarning("⚠️ Car controller lost - searching...");
                carController = FindObjectOfType<CarController>();
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
        if (carController == null)
        {
            Debug.LogError("❌ Cannot start API - car controller not assigned!");
            return;
        }

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

        Debug.Log("🛑 Car API Server stopped");
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

            Debug.Log($"🚀 Car API Server started on port {port}");
            Debug.Log($"🎯 Car ready: {carController.IsCarReady()}");

            Task.Run(() => HandleRequestsAsync(cancellationTokenSource.Token));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to start server: {e.Message}");
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
            catch (System.Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Debug.LogWarning($"⚠️ Request handler error: {ex.Message}");
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
            // CORS headers
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
        catch (System.Exception e)
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
            return $"{{\"status\":\"healthy\",\"car_ready\":{(carController != null && carController.IsCarReady()).ToString().ToLower()}}}";
        }

        if (carController == null || !carController.IsCarReady())
        {
            response.StatusCode = 503;
            return "{\"status\":\"error\",\"message\":\"Car not ready yet\"}";
        }

        switch (path)
        {
            case "/car/turn/left" when method == "POST":
                MainThreadDispatcher.ExecuteOnMainThread(() => carController.TurnLeft());
                return "{\"status\":\"success\",\"action\":\"turn_left\"}";

            case "/car/turn/right" when method == "POST":
                MainThreadDispatcher.ExecuteOnMainThread(() => carController.TurnRight());
                return "{\"status\":\"success\",\"action\":\"turn_right\"}";

            case "/car/move/forward" when method == "POST":
                MainThreadDispatcher.ExecuteOnMainThread(() => carController.MoveForward());
                return "{\"status\":\"success\",\"action\":\"move_forward\"}";

            case "/car/move/backward" when method == "POST":
                MainThreadDispatcher.ExecuteOnMainThread(() => carController.MoveBackward());
                return "{\"status\":\"success\",\"action\":\"move_backward\"}";

            case "/car/status" when method == "GET":
                return GetCarStatus();

            default:
                response.StatusCode = 404;
                return "{\"status\":\"error\",\"message\":\"Endpoint not found\"}";
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

    private async Task SendResponse(HttpListenerResponse response, string responseText)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(responseText);
        response.ContentType = "application/json";
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