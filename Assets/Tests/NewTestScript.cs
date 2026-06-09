using System;
using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

public class NewTestScript
{
    private const string BaseUrl = "http://localhost:5081";

    // =====================================================
    // UNIT-“≈—“€ ÀŒ√» » À¿¡»–»Õ“¿
    // =====================================================

    [Test]
    public void Vector2Int_Creation_StoresCorrectCoordinates()
    {
        Vector2Int position = new Vector2Int(3, 5);

        Assert.AreEqual(3, position.x);
        Assert.AreEqual(5, position.y);
    }

    [Test]
    public void MazeCellCoordinates_AddDirection_ReturnsExpectedPosition()
    {
        Vector2Int startCell = new Vector2Int(2, 2);
        Vector2Int direction = Vector2Int.right;

        Vector2Int result = startCell + direction;

        Assert.AreEqual(new Vector2Int(3, 2), result);
    }

    [Test]
    public void MazeSize_Calculation_ReturnsTotalCellCount()
    {
        int chunkSize = 4;
        Vector2Int mazeSizeInChunks = new Vector2Int(3, 3);

        int totalCellsX = chunkSize * mazeSizeInChunks.x;
        int totalCellsZ = chunkSize * mazeSizeInChunks.y;

        Assert.AreEqual(12, totalCellsX);
        Assert.AreEqual(12, totalCellsZ);
    }

    // =====================================================
    // DTO ƒÀﬂ API
    // =====================================================

    [Serializable]
    private class CreateAttemptDto
    {
        public int maze_seed;
        public int maze_width;
        public int maze_height;
        public bool create_finish_area;
        public bool create_finish_area_in_corner;
    }

    [Serializable]
    private class AttemptCreatedDto
    {
        public int attempt_id;
    }

    [Serializable]
    private class ActionDto
    {
        public float time_sec;
        public string action;
        public int pos_x;
        public int pos_y;
    }

    [Serializable]
    private class ActionsWrapperDto
    {
        public ActionDto[] records;
    }

    // =====================================================
    // »Õ“≈√–¿÷»ŒÕÕ€≈ “≈—“€ API » ¡ƒ
    // =====================================================

    [UnityTest]
    public IEnumerator Api_Health_ReturnsSuccessStatus()
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"{BaseUrl}/health"))
        {
            yield return WaitForRequest(request);

            Assert.AreEqual(UnityWebRequest.Result.Success, request.result, request.error);
            Assert.AreEqual(200, request.responseCode);
            Assert.IsTrue(request.downloadHandler.text.Contains("ok"));
        }
    }

    [UnityTest]
    public IEnumerator CarApi_CreateAttempt_ReturnsAttemptId()
    {
        CreateAttemptDto dto = new CreateAttemptDto
        {
            maze_seed = 123,
            maze_width = 3,
            maze_height = 3,
            create_finish_area = true,
            create_finish_area_in_corner = false
        };

        string json = JsonUtility.ToJson(dto);

        using (UnityWebRequest request = new UnityWebRequest($"{BaseUrl}/attempts", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return WaitForRequest(request);

            Assert.AreEqual(UnityWebRequest.Result.Success, request.result, request.error);
            Assert.AreEqual(200, request.responseCode);

            AttemptCreatedDto response =
                JsonUtility.FromJson<AttemptCreatedDto>(request.downloadHandler.text);

            Assert.Greater(response.attempt_id, 0);
        }
    }

    [UnityTest]
    public IEnumerator Database_SaveAndLoadCarActions_ReturnsSavedAction()
    {
        int attemptId = -1;

        yield return CreateAttempt(id => attemptId = id);

        Assert.Greater(attemptId, 0);

        ActionsWrapperDto actionsToSave = new


ActionsWrapperDto
        {
            records = new[]
            {
                new ActionDto
                {
                    time_sec = 1.5f,
                    action = "MOVE",
                    pos_x = 2,
                    pos_y = 3
                }
            }
        };

        string saveJson = JsonUtility.ToJson(actionsToSave);

        using (UnityWebRequest saveRequest = new UnityWebRequest($"{BaseUrl}/attempts/{attemptId}/actions", "POST"))
        {
            saveRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(saveJson));
            saveRequest.downloadHandler = new DownloadHandlerBuffer();
            saveRequest.SetRequestHeader("Content-Type", "application/json");

            yield return WaitForRequest(saveRequest);

            Assert.AreEqual(UnityWebRequest.Result.Success, saveRequest.result, saveRequest.error);
            Assert.AreEqual(200, saveRequest.responseCode);

            string saveResponseText = saveRequest.downloadHandler.text;

            Assert.IsFalse(string.IsNullOrEmpty(saveResponseText));
            Assert.IsTrue(saveResponseText.Contains("inserted") || saveResponseText.Contains("1"));
        }

        using (UnityWebRequest loadRequest = UnityWebRequest.Get($"{BaseUrl}/attempts/{attemptId}/actions"))
        {
            yield return WaitForRequest(loadRequest);

            Assert.AreEqual(UnityWebRequest.Result.Success, loadRequest.result, loadRequest.error);
            Assert.AreEqual(200, loadRequest.responseCode);

            string loadResponseText = loadRequest.downloadHandler.text;

            Assert.IsFalse(string.IsNullOrEmpty(loadResponseText));
            Assert.IsTrue(loadResponseText.Contains("MOVE"));
            Assert.IsTrue(loadResponseText.Contains("2"));
            Assert.IsTrue(loadResponseText.Contains("3"));
        }
    }

    // =====================================================
    // ¬—œŒÃŒ√¿“≈À‹Õ€≈ Ã≈“Œƒ€
    // =====================================================

    private IEnumerator CreateAttempt(Action<int> onCreated)
    {
        CreateAttemptDto dto = new CreateAttemptDto
        {
            maze_seed = 999,
            maze_width = 3,
            maze_height = 3,
            create_finish_area = true,
            create_finish_area_in_corner = false
        };

        string json = JsonUtility.ToJson(dto);

        using (UnityWebRequest request = new UnityWebRequest($"{BaseUrl}/attempts", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return WaitForRequest(request);

            Assert.AreEqual(UnityWebRequest.Result.Success, request.result, request.error);
            Assert.AreEqual(200, request.responseCode);

            AttemptCreatedDto response =
                JsonUtility.FromJson<AttemptCreatedDto>(request.downloadHandler.text);

            onCreated?.Invoke(response.attempt_id);
        }
    }

    private IEnumerator WaitForRequest(UnityWebRequest request)
    {
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();

        while (!operation.isDone)
        {
            yield return null;
        }
    }
}


