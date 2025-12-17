using UnityEngine;
using UnityEngine.SceneManagement;

public class TransitionBetweenScenesBut : MonoBehaviour
{
    [Tooltip("Название сцены для загрузки")]
    public string targetSceneName;

    [Tooltip("Задержка перед переходом (секунды)")]
    public float delay = 0f;

    [Tooltip("Использовать асинхронную загрузку (прогресс-бар)")]
    public bool useAsyncLoad = false;

    [Tooltip("Индекс сцены вместо названия (если > -1)")]
    public int targetSceneIndex = -1;

    /// <summary>
    /// Перейти на указанную сцену
    /// </summary>
    public void ChangeScene()
    {
        if (delay > 0)
        {
            Invoke(nameof(LoadScene), delay);
        }
        else
        {
            LoadScene();
        }
    }

    /// <summary>
    /// Перейти на сцену по её имени
    /// </summary>
    public void ChangeScene(string sceneName)
    {
        targetSceneName = sceneName;
        ChangeScene();
    }

    /// <summary>
    /// Перейти на сцену по индексу
    /// </summary>
    public void ChangeScene(int sceneIndex)
    {
        targetSceneIndex = sceneIndex;
        targetSceneName = ""; // Очищаем имя
        ChangeScene();
    }

    private void LoadScene()
    {
        if (useAsyncLoad)
        {
            LoadSceneAsync();
            return;
        }

        if (targetSceneIndex >= 0)
        {
            SceneManager.LoadScene(targetSceneIndex);
        }
        else if (!string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogError("Не указана сцена для перехода!");
        }
    }

    private void LoadSceneAsync()
    {
        StartCoroutine(LoadSceneAsyncCoroutine());
    }

    private System.Collections.IEnumerator LoadSceneAsyncCoroutine()
    {
        AsyncOperation asyncLoad;

        if (targetSceneIndex >= 0)
        {
            asyncLoad = SceneManager.LoadSceneAsync(targetSceneIndex);
        }
        else
        {
            asyncLoad = SceneManager.LoadSceneAsync(targetSceneName);
        }

        asyncLoad.allowSceneActivation = false;

        // Ждём загрузку
        while (!asyncLoad.isDone)
        {
            // Прогресс от 0 до 0.9
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            
            // Здесь можно обновлять UI прогресс-бара
            Debug.Log($"Загрузка: {progress * 100}%");

            // Когда загружено на 90%, активируем сцену
            if (asyncLoad.progress >= 0.9f)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Перезапустить текущую сцену
    /// </summary>
    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Выйти из игры
    /// </summary>
    public void QuitGame()
    {
        Application.Quit();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
