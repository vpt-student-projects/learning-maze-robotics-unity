using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ReplaySender : MonoBehaviour
{
    [Header("Links")]
    public CarRecorderAPI recorder;
    public CarController car;

    [Header("API")]
    public string replayUrl = "http://localhost:8080/replay";

    [System.Serializable]
    private class Wrapper
    {
        public MovementRecord[] records;
    }

    // 🔴 КНОПКА REC
    public void StartRecording()
    {
        if (recorder == null || car == null)
        {
            Debug.LogError("ReplaySender: recorder or car not set");
            return;
        }

        recorder.ClearLog();      // очистили старое
        car.isRecording = true;   // ВКЛЮЧИЛИ запись
        Debug.Log("Recording ON");
    }

    // ⏹ (опционально) кнопка STOP
    public void StopRecording()
    {
        if (car == null) return;

        car.isRecording = false;
        Debug.Log("Recording OFF");
    }

    // ▶ КНОПКА REPLAY
    public void SendReplay()
    {
        if (recorder == null)
        {
            Debug.LogError("ReplaySender: recorder not set");
            return;
        }

        var log = recorder.GetMovementLog();
        if (log == null || log.Count == 0)
        {
            Debug.LogWarning("ReplaySender: movementLog is empty");
            return;
        }

        var wrapper = new Wrapper
        {
            records = log.ToArray()
        };

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
            }
            else
            {
                Debug.Log("ReplaySender: Replay sent OK");
            }
        }
    }
}
