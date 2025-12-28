using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class AttemptsListMenu : MonoBehaviour
{
    [Header("API (DB server)")]
    public string dbApiBaseUrl = "http://localhost:5081";

    [Header("UI")]
    public Transform contentParent;   // ScrollView/Viewport/Content
    public Button buttonPrefab;       // Prefab кнопки строки попытки
    public int limit = 10;

    [Header("Scene")]
    public string gameSceneName = "Scene"; // <-- поставь имя сцены с машинкой/лабиринтом

    [Serializable] public class AttemptsListWrapper { public AttemptItem[] items; }

    [Serializable]
    public class AttemptItem
    {
        public int attempt_id;
        public int maze_seed;
        public int maze_width;
        public int maze_height;
        public string created_at;
        public float duration_sec;

        // ✅ новые поля из БД
        public bool create_finish_area;
        public bool create_finish_area_in_corner;
    }

    void OnEnable() => Refresh();

    public void Refresh() => StartCoroutine(LoadLatestAttempts());

    private IEnumerator LoadLatestAttempts()
    {
        // очистка
        for (int i = contentParent.childCount - 1; i >= 0; i--)
            Destroy(contentParent.GetChild(i).gameObject);

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
        if (data?.items == null) yield break;

        foreach (var a in data.items)
        {
            var btn = Instantiate(buttonPrefab, contentParent);

            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text =
                    $"Seed {a.maze_seed} | {a.maze_width}x{a.maze_height} | {a.duration_sec:0.0}s";
            }

            int id = a.attempt_id;
            int seed = a.maze_seed;
            int w = a.maze_width;
            int h = a.maze_height;

            bool finishCenter = a.create_finish_area;
            bool finishCorner = a.create_finish_area_in_corner;

            btn.onClick.AddListener(() =>
            {
                // ✅ передаём галочки тоже
                SelectedAttempt.Set(id, seed, w, h, finishCenter, finishCorner);
                SceneManager.LoadScene(gameSceneName);
            });
        }
    }
}
