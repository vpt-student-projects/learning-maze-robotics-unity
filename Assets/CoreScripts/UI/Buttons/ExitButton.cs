using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class ExitButton : MonoBehaviour
{
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Настройки")]
    [SerializeField] private KeyCode exitKey = KeyCode.Escape;
    [SerializeField] private bool showConfirmation = true;
    [SerializeField] private bool pauseGameWhenPanelOpen = true;

    [Header("Автонастройка")]
    [SerializeField] private bool autoFindButtons = true;

    [Header("События")]
    public UnityEvent onExitRequested;
    public UnityEvent onExitConfirmed;
    public UnityEvent onExitCancelled;
    public UnityEvent onPanelOpened;
    public UnityEvent onPanelClosed;

    private bool isPanelOpen = false;

    void Start()
    {
        InitializePanel();
    }

    void InitializePanel()
    {
        // Автопоиск кнопок если включено
        if (autoFindButtons && confirmationPanel != null)
        {
            Button[] buttons = confirmationPanel.GetComponentsInChildren<Button>();
            foreach (var button in buttons)
            {
                if (button.name.ToLower().Contains("confirm") ||
                    button.name.ToLower().Contains("yes") ||
                    button.name.ToLower().Contains("accept"))
                {
                    confirmButton = button;
                }
                else if (button.name.ToLower().Contains("cancel") ||
                         button.name.ToLower().Contains("no") ||
                         button.name.ToLower().Contains("decline"))
                {
                    cancelButton = button;
                }
            }
        }

        // Инициализация панели
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);

            // Подписка на кнопки
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(ConfirmExit);
                confirmButton.onClick.AddListener(ConfirmExit);
            }
            else
            {
                Debug.LogWarning("Confirm button не найден!");
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(CancelExit);
                cancelButton.onClick.AddListener(CancelExit);
            }
            else
            {
                Debug.LogWarning("Cancel button не найден!");
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(exitKey))
        {
            ToggleExitPanel();
        }
    }

    // Переключатель панели (открыть/закрыть по ESC)
    public void ToggleExitPanel()
    {
        if (isPanelOpen)
        {
            CancelExit();
        }
        else
        {
            RequestExit();
        }
    }

    public void RequestExit()
    {
        if (isPanelOpen) return;

        onExitRequested?.Invoke();

        if (showConfirmation && confirmationPanel != null)
        {
            OpenPanel();
        }
        else
        {
            QuitApplication();
        }
    }

    private void OpenPanel()
    {
        confirmationPanel.SetActive(true);
        isPanelOpen = true;

        if (pauseGameWhenPanelOpen)
        {
            Time.timeScale = 0f;
        }

        onPanelOpened?.Invoke();

        // Даем фокус кнопке отмены для удобства
        if (cancelButton != null)
        {
            cancelButton.Select();
        }
    }

    private void ClosePanel()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        isPanelOpen = false;

        if (pauseGameWhenPanelOpen)
        {
            Time.timeScale = 1f;
        }

        onPanelClosed?.Invoke();
    }

    public void ConfirmExit()
    {
        onExitConfirmed?.Invoke();

        // Сохраняем данные перед выходом
        SaveBeforeExit();

        ClosePanel();
        QuitApplication();
    }

    public void CancelExit()
    {
        onExitCancelled?.Invoke();
        ClosePanel();
    }

    private void SaveBeforeExit()
    {
        // Пример сохранения данных
        PlayerPrefs.Save();

        // Здесь можно добавить вашу логику сохранения
        // GameManager.Instance.SaveProgress();
    }

    private void QuitApplication()
    {
        // Можно добавить задержку для анимации
        // StartCoroutine(QuitWithDelay(0.5f));

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        // Для WebGL показываем сообщение
        Debug.Log("Выход из WebGL приложения невозможен");
        // Можно показать сообщение игроку
        // ShowWebGLMessage();
#else
        Application.Quit();
#endif
    }

    // Корутина для выхода с задержкой (для анимаций)
    private System.Collections.IEnumerator QuitWithDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Для WebGL можно показать сообщение
    private void ShowWebGLMessage()
    {
        // Можно использовать UI Text или отдельную панель
        Debug.Log("Для выхода закройте вкладку браузера");
    }

    void OnDestroy()
    {
        // Отписываемся от событий при уничтожении объекта
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmExit);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(CancelExit);
    }
}