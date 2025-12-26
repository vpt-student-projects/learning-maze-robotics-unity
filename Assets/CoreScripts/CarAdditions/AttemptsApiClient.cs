using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AttemptsApiClient : MonoBehaviour
{
    [Header("API base url (server)")]
    public string baseUrl = "http://localhost:5081";

    public int CurrentAttemptId { get; private set; } = -1;
    public bool IsAttemptReady => CurrentAttemptId > 0;

    [System.Serializable]
    private class CreateAttemptRequest
    {
        public int maze_seed;
        public int maze_width;
        public int maze_height;
    }

    [System.Serializable]
    private class CreateAttemptResponse
    {
        public int attempt_id;
    }

    [System.Serializable]
    private class ActionDto
    {
        public float time_sec;
        public string action;
        public int pos_x;
        public int pos_y;
    }

    [System.Serializable]
    private class ActionsWrapper
    {
        public ActionDto[] records;
    }

    public void CreateAttempt(int seed, int width, int height)
    {
        StartCoroutine(CreateAttemptCoroutine(seed, width, height));
    }

    public void UploadActions(MovementRecord[] records)
    {
        if (CurrentAttemptId <= 0)
        {
            Debug.LogError("AttemptsApiClient: attempt not created yet.");
            return;
        }

        StartCoroutine(UploadActionsCoroutine(CurrentAttemptId, records));
    }

    private IEnumerator CreateAttemptCoroutine(int seed, int width, int height)
    {
        var bodyObj = new CreateAttemptRequest
        {
            maze_seed = seed,
            maze_width = width,
            maze_height = height
        };

        string json = JsonUtility.ToJson(bodyObj);

        using var req = new UnityWebRequest($"{baseUrl}/attempts", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("CreateAttempt error: " + req.error);
            Debug.LogError(req.downloadHandler.text);
            yield break;
        }

        var resp = JsonUtility.FromJson<CreateAttemptResponse>(req.downloadHandler.text);
        CurrentAttemptId = resp.attempt_id;

        Debug.Log($"✅ Attempt created in DB. id={CurrentAttemptId}");
    }

    private IEnumerator UploadActionsCoroutine(int attemptId, MovementRecord[] records)
    {
        var wrapper = new ActionsWrapper { records = new ActionDto[records.Length] };

        for (int i = 0; i < records.Length; i++)
        {
            wrapper.records[i] = new ActionDto
            {
                time_sec = records[i].time_sec,
                action = records[i].action,
                pos_x = records[i].position.x,
                pos_y = records[i].position.y
            };
        }

        string json = JsonUtility.ToJson(wrapper);

        using var req = new UnityWebRequest($"{baseUrl}/attempts/{attemptId}/actions", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("UploadActions error: " + req.error);
            Debug.LogError(req.downloadHandler.text);
            yield break;
        }

        Debug.Log($"✅ Uploaded {records.Length} actions. Server: {req.downloadHandler.text}");
    }
}
