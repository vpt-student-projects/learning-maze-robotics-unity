using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class AttemptsListMenu : MonoBehaviour
{
    [Header("API")]
    public string dbApiBaseUrl = "http://localhost:5081";
    public string replayUrl = "http://localhost:8080/replay";

    [Header("UI")]
    public Transform contentParent;   // ScrollView / Viewport / Content
    public Button buttonPrefab;       // Prefab кнопки попытки

    [Header("Options")]
    public int limit = 10;

    // ===== DTO =====

    [Serializable]
    public class AttemptsListWrapper
    {
        public AttemptItem[] items;
    }

    [Serializable]
    public class AttemptItem
    {
        public int attempt_id;
        public int maze_seed;
        public int maze_width;
        public int maze_height;
        public string created_at;
        public float duration_sec;
    }

    [Serializable]
    public class ActionsWrapper
    {
        public ActionItem[] records;
    }

    [Serializable]
    public class ActionItem
    {
        public float time_sec;
        public string action;
        public int pos_x;
        public int pos_y;
    }

    [Serializable]
    public class ReplayWrapper
    {
        public MovementRecord[] records;
    }

    // =================

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        StartCoroutine(LoadLatestAttempts());
    }

    private IEnumerator LoadLatestAttempts()
    {
        // Очистка старых кнопок
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }

        string url = $"{dbApiBaseUrl}/attempts/latest?limit={limit}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("AttemptsListMenu error: " + req.error);
            Debug.LogError(req.downloadHandler.text);
            yield break;
        }

        var data = JsonUtility.FromJson<AttemptsListWrapper>(req.downloadHandler.text);
        Debug.Log($"[AttemptsListMenu] items count = {data?.items?.Length}");
        if (data?.items == null) yield break;

        foreach (var a in data.items)
        {
            Debug.Log($"[Attempt] seed={a.maze_seed}, size={a.maze_width}x{a.maze_height}, time={a.duration_sec}");

            var btn = Instantiate(buttonPrefab, contentParent);

            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text =
                    $"Seed {a.maze_seed} | {a.maze_width}x{a.maze_height} | {a.duration_sec:0.0}s";
            }
            else
            {
                Debug.LogError("TextMeshProUGUI NOT FOUND in button prefab!");
            }

            int capturedId = a.attempt_id;
            btn.onClick.AddListener(() => StartCoroutine(PlayAttempt(capturedId)));
        }

    }

    private IEnumerator PlayAttempt(int attemptId)
    {
        // 1) Получаем действия попытки
        string url = $"{dbApiBaseUrl}/attempts/{attemptId}/actions";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Load actions error: " + req.error);
            Debug.LogError(req.downloadHandler.text);
            yield break;
        }

        var actions = JsonUtility.FromJson<ActionsWrapper>(req.downloadHandler.text);
        if (actions?.records == null || actions.records.Length == 0)
        {
            Debug.LogWarning($"Attempt {attemptId} has no actions");
            yield break;
        }

        // 2) Конвертация в MovementRecord[]
        MovementRecord[] records = new MovementRecord[actions.records.Length];
        for (int i = 0; i < actions.records.Length; i++)
        {
            var r = actions.records[i];
            records[i] = new MovementRecord
            {
                action = r.action,
                time_sec = r.time_sec,
                position = new Vector2Int(r.pos_x, r.pos_y)
            };
        }

        // 3) Отправка в replay-сервер Unity
        var wrapper = new ReplayWrapper { records = records };
        string json = JsonUtility.ToJson(wrapper);

        using var post = new UnityWebRequest(replayUrl, "POST");
        post.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        post.downloadHandler = new DownloadHandlerBuffer();
        post.SetRequestHeader("Content-Type", "application/json");

        yield return post.SendWebRequest();

        if (post.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Replay POST error: " + post.error);
            Debug.LogError(post.downloadHandler.text);
        }
    }
}
