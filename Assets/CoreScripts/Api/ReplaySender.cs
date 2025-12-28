using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ReplaySender : MonoBehaviour
{
    [Header("Links")]
    public CarRecorderAPI recorder;
    public CarController car;
    public MazeGenerator mazeGenerator;

    [Header("DB API")]
    public string dbApiBaseUrl = "http://localhost:5081";

    private int currentAttemptId = -1;

    [System.Serializable]
    private class CreateAttemptDto
    {
        public int maze_seed;
        public int maze_width;
        public int maze_height;
        public bool create_finish_area;
        public bool create_finish_area_in_corner;
    }

    [System.Serializable]
    private class AttemptCreatedDto
    {
        public int attempt_id;
    }

    [System.Serializable]
    private class ActionsWrapperDto
    {
        public MovementRecord[] records;
    }

    // 🔴 REC
    public void StartRecording()
    {
        if (recorder == null || car == null || mazeGenerator == null)
        {
            Debug.LogError("ReplaySender: recorder/car/mazeGenerator not set");
            return;
        }

        recorder.ClearLog();
        car.isRecording = false; // включим после создания attempt

        StartCoroutine(CreateAttemptAndStart());
    }

    private IEnumerator CreateAttemptAndStart()
    {
        // ВАЖНО: width/height берём в ЧАНКАХ (как у тебя в UI)
        int seed = mazeGenerator.mazeSeed;
        int w = mazeGenerator.mazeSizeInChunks.x;
        int h = mazeGenerator.mazeSizeInChunks.y;

        var dto = new CreateAttemptDto
        {
            maze_seed = seed,
            maze_width = w,
            maze_height = h,
            create_finish_area = mazeGenerator.createFinishArea,
            create_finish_area_in_corner = mazeGenerator.createFinishAreaInCorner
        };

        string json = JsonUtility.ToJson(dto);

        using var req = new UnityWebRequest($"{dbApiBaseUrl}/attempts", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Create attempt error: " + req.error);
            Debug.LogError(req.downloadHandler.text);
            yield break;
        }

        var created = JsonUtility.FromJson<AttemptCreatedDto>(req.downloadHandler.text);
        currentAttemptId = created.attempt_id;

        Debug.Log($"✅ Attempt created: {currentAttemptId} | seed={seed} size={w}x{h} finishCenter={dto.create_finish_area} finishCorner={dto.create_finish_area_in_corner}");

        car.isRecording = true;
        Debug.Log("Recording ON");
    }

    // ⏹ STOP
    public void StopRecording()
    {
        if (car == null || recorder == null) return;

        car.isRecording = false;
        Debug.Log("Recording OFF");

        if (currentAttemptId <= 0)
        {
            Debug.LogWarning("StopRecording: attempt_id not created yet");
            return;
        }

        var log = recorder.GetMovementLog();
        if (log == null || log.Count == 0)
        {
            Debug.LogWarning("StopRecording: movement log empty");
            return;
        }

        var wrapper = new ActionsWrapperDto { records = log.ToArray() };
        string json = JsonUtility.ToJson(wrapper);

        StartCoroutine(PostActions(currentAttemptId, json));
    }

    private IEnumerator PostActions(int attemptId, string json)
    {
        using var req = new UnityWebRequest($"{dbApiBaseUrl}/attempts/{attemptId}/actions", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Upload actions error: " + req.error);
            Debug.LogError(req.downloadHandler.text);
        }
        else
        {
            Debug.Log($"✅ Actions uploaded for attempt {attemptId}");
        }
    }
}
