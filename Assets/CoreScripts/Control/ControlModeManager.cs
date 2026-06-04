using UnityEngine;

/// <summary>
/// Менеджер управления режимами управления машинкой
/// Управляет видимостью панелей блоков и API сервера в зависимости от выбранной сложности
/// Панель генерации всегда открыта
/// </summary>
public class ControlModeManager : MonoBehaviour
{
    [Header("UI Панели")]
    [SerializeField] private GameObject blocksPanel; // Панель с блоками для программирования (SelectionBlocksPanel)
    [SerializeField] private GameObject workspacePanel; // Рабочая панель для блоков (WorkPanel)
    [SerializeField] private GameObject generationPanel; // Панель генерации лабиринта (MazeGeneratorUI)

    [Header("API")]
    [SerializeField] private CarAPIController carAPIController;

    [Header("Текущий режим")]
    [SerializeField] private ControlMode currentControlMode = ControlMode.Blocks;

    private MazeGenerator mazeGenerator;

    void Start()
    {
        if (mazeGenerator == null)
            mazeGenerator = FindObjectOfType<MazeGenerator>();

        // Начальное состояние
        SetControlMode(ControlMode.Blocks, applyImmediately: false);

        // Автоматически находим панели, если они не назначены
        if (blocksPanel == null || workspacePanel == null || generationPanel == null)
        {
            AutoFindPanels();
        }

        // Убеждаемся, что панель генерации всегда активна
        EnsureGenerationPanelVisible();
    }

    /// <summary>
    /// Устанавливает режим управления на основе сложности
    /// </summary>
    public void SetControlModeFromDifficulty(DifficultyLevel difficulty)
    {
        ControlMode mode;

        switch (difficulty)
        {
            case DifficultyLevel.Easy:
                mode = ControlMode.Blocks;
                break;

            case DifficultyLevel.Medium:
                mode = ControlMode.API_Nodes;
                break;

            case DifficultyLevel.Hard:
                mode = ControlMode.API_Motors;
                break;

            case DifficultyLevel.Pro:
                mode = ControlMode.API_Motors;
                break;

            default:
                mode = ControlMode.Blocks;
                break;
        }

        SetControlMode(mode);
    }

    /// <summary>
    /// Устанавливает режим управления
    /// </summary>
    public void SetControlMode(ControlMode mode, bool applyImmediately = true)
    {
        currentControlMode = mode;

        if (applyImmediately)
        {
            ApplyControlMode();
        }
    }

    /// <summary>
    /// Применяет текущий режим управления
    /// </summary>
    private void ApplyControlMode()
    {
        Debug.Log($"🎮 Применение режима управления: {currentControlMode}");

        switch (currentControlMode)
        {
            case ControlMode.Blocks:
                // Легкий - показываем панель с блоками
                SetBlocksPanelVisible(true);
                Debug.Log("📦 Режим блоков: управление через панель программирования");
                break;

            case ControlMode.API_Nodes:
                // Средний и Сложный - скрываем панель блоков
                SetBlocksPanelVisible(false);
                EnsureAPIServerRunning();
                break;

            case ControlMode.API_Motors:
                SetBlocksPanelVisible(false);
                EnsureAPIServerRunning();
                Debug.Log("⚙️ Режим API_Motors: управление скоростью колёс");
                break;
        }

        EnsureGenerationPanelVisible();
        ApplyCarControlSettings();
    }

    private void ApplyCarControlSettings()
    {
        CarController car = FindObjectOfType<CarController>();
        if (car == null)
        {
            Debug.LogWarning("⚠️ CarController не найден для применения режима управления");
            return;
        }

        car.ApplyControlModeSettings(currentControlMode);
    }

    /// <summary>
    /// Управляет видимостью панелей блоков
    /// </summary>
    private void SetBlocksPanelVisible(bool visible)
    {
        // Панель с выбором блоков
        if (blocksPanel != null)
        {
            blocksPanel.SetActive(visible);
            Debug.Log($"📦 Панель блоков (SelectionBlocksPanel): {(visible ? "Показана" : "Скрыта")}");
        }
        else
        {
            Debug.LogWarning("⚠️ blocksPanel не назначена в ControlModeManager");
        }

        // Рабочая панель для блоков
        if (workspacePanel != null)
        {
            workspacePanel.SetActive(visible);
            Debug.Log($"📦 Рабочая панель (WorkPanel): {(visible ? "Показана" : "Скрыта")}");
        }
        else
        {
            Debug.LogWarning("⚠️ workspacePanel не назначена в ControlModeManager");
        }
    }

    /// <summary>
    /// Убеждается, что панель генерации всегда видна
    /// </summary>
    private void EnsureGenerationPanelVisible()
    {
        if (generationPanel != null)
        {
            if (!generationPanel.activeSelf)
            {
                generationPanel.SetActive(true);
                Debug.Log("✅ Панель генерации (MazeGeneratorUI) активирована");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ generationPanel не назначена в ControlModeManager");
        }
    }

    /// <summary>
    /// Убеждается, что API сервер запущен
    /// </summary>
    private void EnsureAPIServerRunning()
    {
        if (carAPIController == null)
        {
            carAPIController = FindObjectOfType<CarAPIController>();
        }

        if (carAPIController != null)
        {
            if (!carAPIController.IsServerRunning())
            {
                carAPIController.StartServer();
                Debug.Log("🚀 API сервер запущен");
            }
            else
            {
                Debug.Log("✅ API сервер уже работает");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ CarAPIController не найден. API сервер не может быть запущен.");
        }
    }

    /// <summary>
    /// Получить текущий режим управления
    /// </summary>
    public ControlMode GetCurrentControlMode()
    {
        return currentControlMode;
    }

    /// <summary>
    /// Автоматический поиск панелей в сцене
    /// </summary>
    [ContextMenu("Автоматически найти панели")]
    public void AutoFindPanels()
    {
        // Поиск панели с блоками
        if (blocksPanel == null)
        {
            GameObject found = GameObject.Find("SelectionBlocksPanel");
            if (found != null)
            {
                blocksPanel = found;
                Debug.Log("✅ Найдена панель SelectionBlocksPanel");
            }
            else
            {
                Debug.LogWarning("⚠️ SelectionBlocksPanel не найдена на сцене");
            }
        }

        // Поиск рабочей панели
        if (workspacePanel == null)
        {
            GameObject found = GameObject.Find("WorkPanel");
            if (found != null)
            {
                workspacePanel = found;
                Debug.Log("✅ Найдена панель WorkPanel");
            }
            else
            {
                Debug.LogWarning("⚠️ WorkPanel не найдена на сцене");
            }
        }

        // Поиск панели генерации
        if (generationPanel == null)
        {
            GameObject found = GameObject.Find("MazeGeneratorUI");
            if (found != null)
            {
                generationPanel = found;
                Debug.Log("✅ Найдена панель MazeGeneratorUI");
            }
            else
            {
                Debug.LogWarning("⚠️ MazeGeneratorUI не найдена на сцене");
            }
        }

        // Поиск CarAPIController
        if (carAPIController == null)
        {
            carAPIController = FindObjectOfType<CarAPIController>();
            if (carAPIController != null)
            {
                Debug.Log("✅ Найден CarAPIController");
            }
            else
            {
                Debug.LogWarning("⚠️ CarAPIController не найден на сцене");
            }
        }
    }

    /// <summary>
    /// Метод для переключения видимости панели генерации (если нужно, например для дебага)
    /// </summary>
    [ContextMenu("Переключить панель генерации")]
    public void ToggleGenerationPanel()
    {
        if (generationPanel != null)
        {
            bool newState = !generationPanel.activeSelf;
            generationPanel.SetActive(newState);
            Debug.Log($"🔧 Панель генерации: {(newState ? "Включена" : "Отключена")}");
        }
    }

    /// <summary>
    /// Метод для принудительного включения панели генерации
    /// </summary>
    [ContextMenu("Включить панель генерации")]
    public void ForceEnableGenerationPanel()
    {
        if (generationPanel != null)
        {
            generationPanel.SetActive(true);
            Debug.Log("✅ Панель генерации принудительно включена");
        }
    }
}