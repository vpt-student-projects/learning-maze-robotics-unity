using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ReplaySender : MonoBehaviour
{
    [Header("Links")]
    public CarRecorderAPI recorder;
    public CarController car;

    [Header("Maze")]
    public MazeGenerator mazeGenerator;

    [Header("Replay API")]
    public string replayUrl = "http://localhost:8080/replay";

    [Header("DB API")]
    public AttemptsApiClient attemptsApi;

    [Header("Upload settings")]
    public float waitAttemptSeconds = 3f;

    [System.Serializable]
    private class ReplayWrapper
    {
        public MovementRecord[] records;
    }

    // 🔴 REC
    public void StartRecording()
    {
        if (recorder == null || car == null || mazeGenerator == null)
        {
            Debug.LogError("ReplaySender: recorder / car / mazeGenerator not set!");
            return;
        }

        // 🔑 БЕРЁМ РЕАЛЬНЫЕ ДАННЫЕ ЛАБИРИНТА
        int seed = mazeGenerator.GetCurrentSeed();
        int width = mazeGenerator.mazeSizeInChunks.x;
        int height = mazeGenerator.mazeSizeInChunks.y;


        Debug.Log($"🧩 Start attempt | seed={seed}, size={width}x{height}");

        if (attemptsApi != null)
        {
            attemptsApi.CreateAttempt(seed, width, height);
        }

        recorder.ClearLog();
        car.isRecording = true;
    }

    // ⏹ STOP
    public void StopRecording()
    {
        if (recorder == null || car == null)
            return;

        car.isRecording = false;

        if (attemptsApi != null)
        {
            StartCoroutine(WaitAttemptAndUpload());
        }
    }

    // ▶ REPLAY
    public void SendReplay()
    {
        if (recorder == null)
            return;

        var log = recorder.GetMovementLog();
        if (log == null || log.Count == 0)
            return;

        var wrapper = new ReplayWrapper { records = log.ToArray() };
        string json = JsonUtility.ToJson(wrapper);

        StartCoroutine(PostReplay(json));
    }

    private IEnumerator PostReplay(string json)
    {
        using var req = new UnityWebRequest(replayUrl, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("ReplaySender replay error: " + req.error);
        }
    }

    private IEnumerator WaitAttemptAndUpload()
    {
        float t = 0f;
        while (!attemptsApi.IsAttemptReady && t < waitAttemptSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!attemptsApi.IsAttemptReady)
        {
            Debug.LogError("ReplaySender: attempt_id not ready");
            yield break;
        }

        var log = recorder.GetMovementLog();
        if (log == null || log.Count == 0)
            yield break;

        attemptsApi.UploadActions(log.ToArray());
    }
}
