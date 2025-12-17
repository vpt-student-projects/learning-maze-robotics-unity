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
    public MazeTimer mazeTimer;

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

    [Header("Управление Seed")]
    public TMP_InputField seedInputField;
    public Toggle useRandomSeedToggle;
    public Button randomSeedButton;
    public TextMeshProUGUI currentSeedText;

    [Header("Кнопки")]
    public Button generateButton;
    public Button closeButton;
    public Button resetSettingsButton;
    public Button restartButton;

    [Header("Настройки UI")]
    public float fadeDuration = 0.3f;
    public bool startVisible = true;
    public bool hideDuringGeneration = true;

    private Vector2 menuHiddenPosition;
    private Vector2 menuVisiblePosition;
    private bool isMenuVisible = true;
    private Coroutine fadeCoroutine;

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
        public bool useRandomSeed;
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
        menuVisiblePosition = menuPanel.anchoredPosition;
        menuHiddenPosition = menuVisiblePosition + new Vector2(-menuPanel.rect.width, 0);

        generateButton.onClick.AddListener(OnGenerateButtonClick);
        closeButton.onClick.AddListener(ToggleMenu);
        resetSettingsButton.onClick.AddListener(ResetToDefaults);

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartButtonClick);
        }

        // Seed элементы
        if (seedInputField != null)
        {
            seedInputField.onEndEdit.AddListener(OnSeedInputChanged);
        }

        if (useRandomSeedToggle != null)
        {
            useRandomSeedToggle.onValueChanged.AddListener(OnRandomSeedToggleChanged);
        }

        if (randomSeedButton != null)
        {
            randomSeedButton.onClick.AddListener(OnRandomSeedButtonClick);
        }

        chunkSizeInput.onEndEdit.AddListener(OnChunkSizeChanged);
        mazeWidthInput.onEndEdit.AddListener(OnMazeWidthChanged);
        mazeHeightInput.onEndEdit.AddListener(OnMazeHeightChanged);

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
            useRightHandRule = true,
            useRandomSeed = true
        };
    }

    void ApplyDefaultSettingsToUI()
    {
        chunkSizeInput.text = defaultSettings.chunkSize.ToString();
        mazeWidthInput.text = defaultSettings.mazeWidth.ToString();
        mazeHeightInput.text = defaultSettings.mazeHeight.ToString();
        createFinishToggle.isOn = defaultSettings.hasFinish;
        useRightHandRuleToggle.isOn = defaultSettings.useRightHandRule;

        // Установка значений seed по умолчанию
        if (seedInputField != null)
        {
            seedInputField.text = "0";
        }

        if (useRandomSeedToggle != null)
        {
            useRandomSeedToggle.isOn = defaultSettings.useRandomSeed;
        }

        UpdateCurrentSeedText();
    }

    void OnGenerateButtonClick()
    {
        if (mazeGenerator == null)
        {
            Debug.LogError("❌ MazeGenerator не назначен!");
            return;
        }
        ToggleMenu();
        ApplySettingsToMazeGenerator();
        StartCoroutine(GenerationSequence());
    }

    void OnRestartButtonClick()
    {
        if (mazeTimer != null)
        {
            mazeTimer.OnRestartButtonClick();
            Debug.Log("🔄 Рестарт выполнен через MazeMenuController");
        }
        else
        {
            mazeTimer = FindObjectOfType<MazeTimer>();
            if (mazeTimer != null)
            {
                mazeTimer.OnRestartButtonClick();
                Debug.Log("🔄 Рестарт выполнен (таймер найден автоматически)");
            }
            else
            {
                Debug.LogWarning("⚠️ MazeTimer не найден, пытаемся выполнить рестарт вручную");

                CarController car = FindObjectOfType<CarController>();
                if (car != null && mazeGenerator != null)
                {
                    if (mazeGenerator.createFinishArea)
                    {
                        var mazeData = mazeGenerator.GetMazeData();
                        if (mazeData != null)
                        {
                            int startChunkX = mazeData.StartGenerationChunk.x;
                            int startChunkZ = mazeData.StartGenerationChunk.y;
                            int startCellX = Mathf.Max(0, mazeData.StartGenerationCell.x - 2);
                            int startCellZ = Mathf.Max(0, mazeData.StartGenerationCell.y - 2);

                            car.SetCarPosition(startChunkX, startChunkZ, startCellX, startCellZ);
                            car.ResetDirection();
                            Debug.Log($"🔄 Машинка возвращена на старт вручную");
                        }
                    }
                }
            }
        }
    }

    IEnumerator GenerationSequence()
    {
        if (hideDuringGeneration)
        {
            ToggleMenu();
        }

        Debug.Log("🔄 Запуск генерации лабиринта...");
        generateButton.interactable = false;

        mazeGenerator.GenerateMaze();

        yield return new WaitUntil(() => !mazeGenerator.IsGenerating());

        generateButton.interactable = true;

        if (hideDuringGeneration)
        {
            ToggleMenu();
        }

        Debug.Log("✅ Генерация завершена");
    }

    // ОДИН метод ApplySettingsToMazeGenerator
    void ApplySettingsToMazeGenerator()
    {
        if (int.TryParse(chunkSizeInput.text, out int chunkSize))
            mazeGenerator.chunkSize = Mathf.Clamp(chunkSize, 2, 10);

        if (int.TryParse(mazeWidthInput.text, out int width))
            mazeGenerator.mazeSizeInChunks.x = Mathf.Clamp(width, 1, 10);

        if (int.TryParse(mazeHeightInput.text, out int height))
            mazeGenerator.mazeSizeInChunks.y = Mathf.Clamp(height, 1, 10);

        mazeGenerator.createFinishArea = createFinishToggle.isOn;
        mazeGenerator.useRightHandRule = useRightHandRuleToggle.isOn;

        // НОВОЕ: Применение seed настроек
        if (seedInputField != null && !useRandomSeedToggle.isOn)
        {
            if (int.TryParse(seedInputField.text, out int seed))
            {
                mazeGenerator.SetSeed(seed);
            }
        }
        mazeGenerator.useRandomSeed = useRandomSeedToggle != null ? useRandomSeedToggle.isOn : true;

        UpdateCurrentSeedText();

        Debug.Log("⚙️ Настройки применены к генератору");
    }

    // Методы для управления seed
    void OnSeedInputChanged(string value)
    {
        if (int.TryParse(value, out int seed))
        {
            if (useRandomSeedToggle != null)
            {
                useRandomSeedToggle.isOn = false; // При ручном вводе отключаем случайный seed
            }
            UpdateCurrentSeedText();
        }
    }

    void OnRandomSeedToggleChanged(bool isRandom)
    {
        if (seedInputField != null)
        {
            seedInputField.interactable = !isRandom;
        }
        UpdateCurrentSeedText();
    }

    void OnRandomSeedButtonClick()
    {
        if (mazeGenerator != null)
        {
            // Генерируем случайный seed
            int randomSeed = new System.Random().Next();

            if (seedInputField != null)
            {
                seedInputField.text = randomSeed.ToString();
            }

            if (useRandomSeedToggle != null)
            {
                useRandomSeedToggle.isOn = false;
            }

            UpdateCurrentSeedText();
        }
    }

    void UpdateCurrentSeedText()
    {
        if (mazeGenerator != null && currentSeedText != null)
        {
            if (mazeGenerator.useRandomSeed)
            {
                currentSeedText.text = "Seed: Случайный";
            }
            else
            {
                currentSeedText.text = $"Seed: {mazeGenerator.mazeSeed}";
            }
        }
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

            menuCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
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
        ApplySettingsToMazeGenerator(); // Только один вызов этого метода
    }

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

        if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftControl))
        {
            OnRestartButtonClick();
        }
    }

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