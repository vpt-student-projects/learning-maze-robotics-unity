using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AutoReplayFromDb : MonoBehaviour
{
    [Header("References")]
    public MazeGenerator mazeGenerator;

    [Header("API")]
    public string dbApiBaseUrl = "http://localhost:5081";
    public string replayUrl = "http://localhost:8080/replay";

    [Serializable] public class ActionsWrapper { public ActionItem[] records; }
    [Serializable]
    public class ActionItem
    {
        public float time_sec;
        public string action;
        public int pos_x;
        public int pos_y;
    }

    [Serializable] public class ReplayWrapper { public MovementRecord[] records; }

    private IEnumerator Start()
    {
        if (!SelectedAttempt.HasValue)
            yield break;

        if (mazeGenerator == null)
        {
            Debug.LogError("AutoReplayFromDb: MazeGenerator not assigned");
            yield break;
        }

        // 1) Подставляем ВСЕ параметры генерации из выбранной попытки
        mazeGenerator.useRandomSeed = false;
        mazeGenerator.mazeSeed = SelectedAttempt.Seed;
        mazeGenerator.mazeSizeInChunks = new Vector2Int(SelectedAttempt.Width, SelectedAttempt.Height);

        // ✅ КЛЮЧЕВОЕ: галочки из БД
        mazeGenerator.createFinishArea = SelectedAttempt.CreateFinishArea;
        mazeGenerator.useRightHandRule = SelectedAttempt.UseRightHandRule;

        Debug.Log($"[AutoReplay] attempt={SelectedAttempt.AttemptId} seed={SelectedAttempt.Seed} size={SelectedAttempt.Width}x{SelectedAttempt.Height} finishCenter={SelectedAttempt.CreateFinishArea} rightHand={SelectedAttempt.UseRightHandRule}");

        // 2) Генерируем лабиринт
        mazeGenerator.GenerateMazeWithCurrentSeed();
        while (mazeGenerator.IsGenerating())
            yield return null;

        // 3) Скачиваем действия
        int attemptId = SelectedAttempt.AttemptId;
        string url = $"{dbApiBaseUrl}/attempts/{attemptId}/actions";

        using var get = UnityWebRequest.Get(url);
        yield return get.SendWebRequest();

        if (get.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("GET actions error: " + get.error);
            Debug.LogError(get.downloadHandler.text);
            yield break;
        }

        var actions = JsonUtility.FromJson<ActionsWrapper>(get.downloadHandler.text);
        if (actions?.records == null || actions.records.Length == 0)
        {
            Debug.LogWarning($"Attempt {attemptId} has no actions");
            yield break;
        }

        // 4) Конвертируем и отправляем в replay-сервер (8080)
        var records = new MovementRecord[actions.records.Length];
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

        var wrapper = new ReplayWrapper { records = records };
        string json = JsonUtility.ToJson(wrapper);

        using var post = new UnityWebRequest(replayUrl, "POST");
        post.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        post.downloadHandler = new DownloadHandlerBuffer();
        post.SetRequestHeader("Content-Type", "application/json");

        yield return post.SendWebRequest();

        if (post.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("POST replay error: " + post.error);
            Debug.LogError(post.downloadHandler.text);
            yield break;
        }

        Debug.Log($"✅ Replay started for attempt {attemptId}");
        SelectedAttempt.Clear();
    }
}
