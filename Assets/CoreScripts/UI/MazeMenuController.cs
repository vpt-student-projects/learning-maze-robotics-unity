using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MazeMenuController : MonoBehaviour
{
    [Header("Основные ссылки")]
    public MazeGenerator mazeGenerator;
    public CanvasGroup menuCanvasGroup;
    public RectTransform menuPanel;

    [Header("Режимы игры")]
    public ToggleGroup modeToggleGroup;
    public Toggle hardModeToggle;
    public Toggle easyModeToggle;
    public Toggle proModeToggle;

    [Header("Настройки лабиринта")]
    public TMP_InputField chunkSizeInput;
    public TMP_InputField mazeWidthInput;
    public TMP_InputField mazeHeightInput;
    public Toggle createFinishToggle;
    public Toggle useRightHandRuleToggle;

    //[Header("Параметры генерации")]
    //public Slider cellSizeSlider;
    //public TextMeshProUGUI cellSizeValueText;
    //public Slider wallHeightSlider;
    //public TextMeshProUGUI wallHeightValueText;

    [Header("Кнопки")]
    public Button generateButton;
    public Button closeButton;
    public Button resetSettingsButton;

    [Header("Настройки UI")]
    public float fadeDuration = 0.3f;
    public bool startVisible = true;
    public bool hideDuringGeneration = true;

    private Vector2 menuHiddenPosition;
    private Vector2 menuVisiblePosition;
    private bool isMenuVisible = true;
    private Coroutine fadeCoroutine;

    // Структура для хранения настроек по умолчанию
    [System.Serializable]
    private struct DefaultSettings
    {
        public int chunkSize;
        public int mazeWidth;
        public int mazeHeight;
        public float cellSize;
        public float wallHeight;
        public bool hasFinish;
        public bool useRightHandRule;
    }

    private DefaultSettings defaultSettings;

    void Start()
    {
        InitializeMenu();
        LoadDefaultSettings();
        ApplyDefaultSettingsToUI();

        if (!startVisible)
        {
            menuCanvasGroup.alpha = 0;
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
            isMenuVisible = false;
        }
    }

    void InitializeMenu()
    {
        // Сохраняем позиции для анимации
        menuVisiblePosition = menuPanel.anchoredPosition;
        menuHiddenPosition = menuVisiblePosition + new Vector2(-menuPanel.rect.width, 0);

        // Настраиваем кнопки
        generateButton.onClick.AddListener(OnGenerateButtonClick);
        closeButton.onClick.AddListener(ToggleMenu);
        resetSettingsButton.onClick.AddListener(ResetToDefaults);

        //// Настраиваем слайдеры
        //cellSizeSlider.onValueChanged.AddListener(OnCellSizeChanged);
        //wallHeightSlider.onValueChanged.AddListener(OnWallHeightChanged);

        // Настраиваем поля ввода
        chunkSizeInput.onEndEdit.AddListener(OnChunkSizeChanged);
        mazeWidthInput.onEndEdit.AddListener(OnMazeWidthChanged);
        mazeHeightInput.onEndEdit.AddListener(OnMazeHeightChanged);

        // Обновляем текстовые значения
        //UpdateSliderValueTexts();

        // Устанавливаем доступные режимы (пока только сложный)
        easyModeToggle.interactable = false;
        proModeToggle.interactable = false;
        hardModeToggle.isOn = true;

        Debug.Log("✅ Меню инициализировано");
    }

    void LoadDefaultSettings()
    {
        defaultSettings = new DefaultSettings
        {
            chunkSize = 4,
            mazeWidth = 3,
            mazeHeight = 3,
            cellSize = 2f,
            wallHeight = 3f,
            hasFinish = true,
            useRightHandRule = true
        };
    }

    void ApplyDefaultSettingsToUI()
    {
        chunkSizeInput.text = defaultSettings.chunkSize.ToString();
        mazeWidthInput.text = defaultSettings.mazeWidth.ToString();
        mazeHeightInput.text = defaultSettings.mazeHeight.ToString();
        //cellSizeSlider.value = defaultSettings.cellSize;
        //wallHeightSlider.value = defaultSettings.wallHeight;
        createFinishToggle.isOn = defaultSettings.hasFinish;
        useRightHandRuleToggle.isOn = defaultSettings.useRightHandRule;
    }

    void OnGenerateButtonClick()
    {
        if (mazeGenerator == null)
        {
            Debug.LogError("❌ MazeGenerator не назначен!");
            return;
        }

        // Сохраняем настройки из UI в MazeGenerator
        ApplySettingsToMazeGenerator();

        // Запускаем генерацию
        StartCoroutine(GenerationSequence());
    }

    IEnumerator GenerationSequence()
    {
        if (hideDuringGeneration)
        {
            ToggleMenu();
        }

        Debug.Log("🔄 Запуск генерации лабиринта...");
        generateButton.interactable = false;

        // Запускаем генерацию через публичный метод
        mazeGenerator.GenerateMaze();

        // Ждем завершения генерации
        yield return new WaitUntil(() => !mazeGenerator.IsGenerating());

        generateButton.interactable = true;

        if (hideDuringGeneration)
        {
            ToggleMenu();
        }

        Debug.Log("✅ Генерация завершена");
    }

    void ApplySettingsToMazeGenerator()
    {
        // Базовые настройки
        if (int.TryParse(chunkSizeInput.text, out int chunkSize))
            mazeGenerator.chunkSize = Mathf.Clamp(chunkSize, 2, 10);

        if (int.TryParse(mazeWidthInput.text, out int width))
            mazeGenerator.mazeSizeInChunks.x = Mathf.Clamp(width, 1, 10);

        if (int.TryParse(mazeHeightInput.text, out int height))
            mazeGenerator.mazeSizeInChunks.y = Mathf.Clamp(height, 1, 10);

        // Переключатели
        mazeGenerator.createFinishArea = createFinishToggle.isOn;
        mazeGenerator.useRightHandRule = useRightHandRuleToggle.isOn;

        // Параметры генерации
        //mazeGenerator.cellSize = cellSizeSlider.value;
        //mazeGenerator.wallHeight = wallHeightSlider.value;

        Debug.Log("⚙️ Настройки применены к генератору");
    }

    void ToggleMenu()
    {
        isMenuVisible = !isMenuVisible;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeMenu(isMenuVisible));
    }

    IEnumerator FadeMenu(bool show)
    {
        float targetAlpha = show ? 1 : 0;
        float startAlpha = menuCanvasGroup.alpha;
        float elapsedTime = 0;

        Vector2 startPos = menuPanel.anchoredPosition;
        Vector2 targetPos = show ? menuVisiblePosition : menuHiddenPosition;

        menuCanvasGroup.interactable = show;
        menuCanvasGroup.blocksRaycasts = show;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeDuration;

            // Плавное изменение прозрачности
            menuCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            // Плавное перемещение
            menuPanel.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);

            yield return null;
        }

        menuCanvasGroup.alpha = targetAlpha;
        menuPanel.anchoredPosition = targetPos;

        fadeCoroutine = null;
    }

    void ResetToDefaults()
    {
        Debug.Log("🔄 Сброс настроек к значениям по умолчанию");
        ApplyDefaultSettingsToUI();

        // Применяем настройки сразу
        ApplySettingsToMazeGenerator();
    }

    //void UpdateSliderValueTexts()
    //{
    //    cellSizeValueText.text = cellSizeSlider.value.ToString("F1");
    //    wallHeightValueText.text = wallHeightSlider.value.ToString("F1");
    //}

    //void OnCellSizeChanged(float value)
    //{
    //    cellSizeValueText.text = value.ToString("F1");
    //    mazeGenerator.cellSize = value;
    //}

    //void OnWallHeightChanged(float value)
    //{
    //    wallHeightValueText.text = value.ToString("F1");
    //    mazeGenerator.wallHeight = value;
    //}

    void OnChunkSizeChanged(string value)
    {
        if (int.TryParse(value, out int intValue))
        {
            intValue = Mathf.Clamp(intValue, 2, 10);
            chunkSizeInput.text = intValue.ToString();
        }
    }

    void OnMazeWidthChanged(string value)
    {
        if (int.TryParse(value, out int intValue))
        {
            intValue = Mathf.Clamp(intValue, 1, 10);
            mazeWidthInput.text = intValue.ToString();
        }
    }

    void OnMazeHeightChanged(string value)
    {
        if (int.TryParse(value, out int intValue))
        {
            intValue = Mathf.Clamp(intValue, 1, 10);
            mazeHeightInput.text = intValue.ToString();
        }
    }

    // Быстрые клавиши для удобства
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }

        if (Input.GetKeyDown(KeyCode.G) && Input.GetKey(KeyCode.LeftControl))
        {
            OnGenerateButtonClick();
        }
    }

    // Метод для внешнего вызова (например, из других скриптов)
    public void ShowMenu()
    {
        if (!isMenuVisible)
            ToggleMenu();
    }

    public void HideMenu()
    {
        if (isMenuVisible)
            ToggleMenu();
    }

    public bool IsMenuVisible
    {
        get { return isMenuVisible; }
    }
}