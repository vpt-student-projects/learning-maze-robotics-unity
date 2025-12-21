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

    [Header("Replay API (как было раньше)")]
    public string replayUrl = "http://localhost:8080/replay";

    [Header("DB API (сохранение попыток)")]
    public AttemptsApiClient attemptsApi;

    [Header("Attempt data (пока вручную; потом привяжем к MazeGenerator)")]
    public int mazeSeed = 123;
    public int mazeWidth = 20;
    public int mazeHeight = 20;

    [Header("Upload settings")]
    public float waitAttemptSeconds = 3f;

    [System.Serializable]
    private class ReplayWrapper
    {
        public MovementRecord[] records;
    }

    // 🔴 КНОПКА REC
    public void StartRecording()
    {
        if (recorder == null || car == null)
        {
            Debug.LogError("ReplaySender: recorder or car not set in Inspector!");
            return;
        }

        // 1) Создаём attempt в БД (если клиент назначен)
        if (attemptsApi != null)
        {
            attemptsApi.CreateAttempt(mazeSeed, mazeWidth, mazeHeight);
        }

        // 2) Начинаем запись
        recorder.ClearLog();
        car.isRecording = true;

        Debug.Log("ReplaySender: Recording ON");
    }

    // ⏹ КНОПКА STOP
    public void StopRecording()
    {
        if (recorder == null || car == null)
        {
            Debug.LogError("ReplaySender: recorder or car not set in Inspector!");
            return;
        }

        car.isRecording = false;
        Debug.Log("ReplaySender: Recording OFF");

        // Сохраняем в БД (если клиент назначен)
        if (attemptsApi != null)
        {
            StartCoroutine(WaitAttemptAndUploadToDb());
        }
    }

    // ▶ КНОПКА REPLAY (как раньше)
    public void SendReplay()
    {
        if (recorder == null)
        {
            Debug.LogError("ReplaySender: recorder not set in Inspector!");
            return;
        }

        var log = recorder.GetMovementLog();
        if (log == null || log.Count == 0)
        {
            Debug.LogWarning("ReplaySender: movementLog is empty (nothing to replay).");
            return;
        }

        var wrapper = new ReplayWrapper { records = log.ToArray() };
        string json = JsonUtility.ToJson(wrapper);

        StartCoroutine(PostReplay(json));
    }

    private IEnumerator PostReplay(string json)
    {
        using (var req = new UnityWebRequest(replayUrl, "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("ReplaySender POST error: " + req.error);
                Debug.LogError(req.downloadHandler.text);
                Debug.LogError("Проверь: запущен ли твой replay-сервер на 8080 и правильный ли URL /replay");
            }
            else
            {
                Debug.Log("ReplaySender: Replay sent OK");
            }
        }
    }

    private IEnumerator WaitAttemptAndUploadToDb()
    {
        // Если записи нет — не отправляем
        var log = recorder.GetMovementLog();
        if (log == null || log.Count == 0)
        {
            Debug.LogWarning("ReplaySender: movementLog is empty (nothing to upload).");
            yield break;
        }

        // Ждём пока сервер БД вернёт attempt_id
        float t = 0f;
        while (!attemptsApi.IsAttemptReady && t < waitAttemptSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!attemptsApi.IsAttemptReady)
        {
            Debug.LogError("ReplaySender: attempt_id not ready. Actions NOT uploaded to DB. " +
                           "Проверь, запущен ли DB API (localhost:5081) и baseUrl в AttemptsApiClient.");
            yield break;
        }

        attemptsApi.UploadActions(log.ToArray());
        Debug.Log($"ReplaySender: Upload to DB requested. attempt_id={attemptsApi.CurrentAttemptId}, records={log.Count}");
    }
}
