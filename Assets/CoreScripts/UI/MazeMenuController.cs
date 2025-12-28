using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MazeMenuController : MonoBehaviour
{
    [Header("Главные ссылки")]
    public MazeGenerator mazeGenerator;

    [Header("UI элементы панели меню")]
    [SerializeField] private RectTransform menuPanel;
    [SerializeField] private Button toggleMenuButton;
    [SerializeField] private TextMeshProUGUI toggleButtonText;
    [SerializeField] private Button generateButton;
    [SerializeField] private TMP_InputField seedInputField;
    [SerializeField] private Toggle useRandomSeedToggle;
    [SerializeField] private TextMeshProUGUI currentSeedText;

    [Header("Настройки сложности (Toggle Group)")]
    [SerializeField] private Toggle easyToggle;
    [SerializeField] private Toggle mediumToggle;
    [SerializeField] private Toggle hardToggle;

    [Header("Настройки лабиринта")]
    [SerializeField] private TMP_InputField chunkSizeInputField;
    [SerializeField] private TMP_InputField mazeWidthInputField;
    [SerializeField] private TMP_InputField mazeHeightInputField;

    [Header("Дополнительные настройки")]
    [SerializeField] private Toggle finishInMiddleToggle;
    [SerializeField] private Toggle finishInCornerToggle;

    [Header("Управление камерой")]
    [SerializeField] private Button fullMazeViewButton;
    [SerializeField] private Button followCarButton;
    [SerializeField] private Button firstPersonButton;
    [SerializeField] private Slider followCarZoomSlider;
    [SerializeField] private TextMeshProUGUI followCarZoomText;
    [SerializeField] private MazeCameraController cameraController;

    [Header("Кнопки")]
    [SerializeField] private Button resetSettingsButton;

    [Header("Настройки анимации")]
    [SerializeField] private float animationSpeed = 5f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Состояние меню
    private bool isMenuOpen = true;
    private Coroutine animationCoroutine;
    private float menuPanelWidth;
    private Vector2 openPosition;
    private Vector2 closedPosition;

    void Start()
    {
        Debug.Log("🔄 Инициализация MazeMenuController...");

        // Проверяем обязательные ссылки
        if (mazeGenerator == null)
        {
            mazeGenerator = FindObjectOfType<MazeGenerator>();
            if (mazeGenerator == null)
            {
                Debug.LogError("❌ MazeGenerator не найден в сцене!");
                enabled = false;
                return;
            }
        }

        // Находим контроллер камеры
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<MazeCameraController>();
            if (cameraController == null)
            {
                Debug.LogWarning("⚠️ MazeCameraController не найден в сцене!");
            }
        }

        // Проверяем обязательные UI элементы
        if (generateButton == null)
        {
            Debug.LogError("❌ generateButton не привязан! Привяжите кнопку генерации в инспекторе.");
            enabled = false;
            return;
        }

        if (menuPanel == null)
        {
            Debug.LogError("❌ menuPanel не привязан! Это обязательное поле.");
            enabled = false;
            return;
        }

        if (toggleMenuButton == null)
        {
            Debug.LogError("❌ toggleMenuButton не привязан! Это обязательное поле.");
            enabled = false;
            return;
        }

        // Инициализируем позиции меню
        InitializeMenuPositions();

        // Инициализируем кнопки
        InitializeButtons();

        // Устанавливаем начальные значения СРАЗУ
        SetInitialValues();

        // Ждём один кадр и снова устанавливаем (на случай если другие скрипты перезаписывают)
        StartCoroutine(DelayedInitialization());

        // Начинаем с открытого меню
        isMenuOpen = true;
        UpdateMenuButtonText();

        Debug.Log("✅ MazeMenuController успешно инициализирован!");
    }

    IEnumerator DelayedInitialization()
    {
        yield return null; // Ждём один кадр

        // ГАРАНТИРОВАННОЕ включение финиша в середине
        if (finishInMiddleToggle != null)
        {
            finishInMiddleToggle.isOn = true;
            mazeGenerator.createFinishArea = true;
            Debug.Log("✅ Финиш в середине гарантированно включен (отложенная инициализация)");
        }
    }

    void InitializeMenuPositions()
    {
        // Сохраняем ширину панели для анимации
        menuPanelWidth = menuPanel.rect.width;

        // Позиция когда меню открыто (видно полностью)
        openPosition = menuPanel.anchoredPosition;

        // Позиция когда меню закрыто (скрыто за правым краем)
        closedPosition = new Vector2(
            openPosition.x + menuPanelWidth,
            openPosition.y
        );
    }

    void InitializeButtons()
    {
        // Кнопка переключения меню
        toggleMenuButton.onClick.RemoveAllListeners();
        toggleMenuButton.onClick.AddListener(ToggleMenu);

        // Основная кнопка генерации
        generateButton.onClick.RemoveAllListeners();
        generateButton.onClick.AddListener(OnGenerateButtonClick);

        // Кнопка сброса настроек
        if (resetSettingsButton != null)
        {
            resetSettingsButton.onClick.RemoveAllListeners();
            resetSettingsButton.onClick.AddListener(OnResetSettingsButtonClick);
        }

        // Поля ввода
        if (seedInputField != null)
        {
            seedInputField.onEndEdit.RemoveAllListeners();
            seedInputField.onEndEdit.AddListener(OnSeedInputChanged);
        }

        if (chunkSizeInputField != null)
        {
            chunkSizeInputField.onEndEdit.RemoveAllListeners();
            chunkSizeInputField.onEndEdit.AddListener(OnChunkSizeChanged);
        }

        if (mazeWidthInputField != null)
        {
            mazeWidthInputField.onEndEdit.RemoveAllListeners();
            mazeWidthInputField.onEndEdit.AddListener(OnMazeWidthChanged);
        }

        if (mazeHeightInputField != null)
        {
            mazeHeightInputField.onEndEdit.RemoveAllListeners();
            mazeHeightInputField.onEndEdit.AddListener(OnMazeHeightChanged);
        }

        // Toggles сложности
        if (easyToggle != null)
        {
            easyToggle.onValueChanged.RemoveAllListeners();
            easyToggle.onValueChanged.AddListener((isOn) => {
                if (isOn) OnDifficultyChanged(0);
            });
        }

        if (mediumToggle != null)
        {
            mediumToggle.onValueChanged.RemoveAllListeners();
            mediumToggle.onValueChanged.AddListener((isOn) => {
                if (isOn) OnDifficultyChanged(1);
            });
        }

        if (hardToggle != null)
        {
            hardToggle.onValueChanged.RemoveAllListeners();
            hardToggle.onValueChanged.AddListener((isOn) => {
                if (isOn) OnDifficultyChanged(2);
            });
        }

        // Другие toggles
        if (useRandomSeedToggle != null)
        {
            useRandomSeedToggle.onValueChanged.RemoveAllListeners();
            useRandomSeedToggle.onValueChanged.AddListener(OnRandomSeedToggleChanged);
        }

        if (finishInMiddleToggle != null)
        {
            finishInMiddleToggle.onValueChanged.RemoveAllListeners();
            finishInMiddleToggle.onValueChanged.AddListener(OnFinishInMiddleChanged);
        }

        if (finishInCornerToggle != null)
        {
            finishInCornerToggle.onValueChanged.RemoveAllListeners();
            finishInCornerToggle.onValueChanged.AddListener(OnFinishInCornerChanged);
        }

        // Кнопки управления камерой
        if (fullMazeViewButton != null)
        {
            fullMazeViewButton.onClick.RemoveAllListeners();
            fullMazeViewButton.onClick.AddListener(OnFullMazeViewButtonClick);
        }

        if (followCarButton != null)
        {
            followCarButton.onClick.RemoveAllListeners();
            followCarButton.onClick.AddListener(OnFollowCarButtonClick);
        }

        if (firstPersonButton != null)
        {
            firstPersonButton.onClick.RemoveAllListeners();
            firstPersonButton.onClick.AddListener(OnFirstPersonButtonClick);
        }

        // Ползунок зума для режима слежения
        if (followCarZoomSlider != null)
        {
            followCarZoomSlider.onValueChanged.RemoveAllListeners();
            followCarZoomSlider.onValueChanged.AddListener(OnFollowCarZoomChanged);
        }
    }

    void SetInitialValues()
    {
        Debug.Log("⚙️ Устанавливаем начальные значения...");

        // ВАЖНО: Галочка финиша в середине - по умолчанию true
        if (finishInMiddleToggle != null)
        {
            // Принудительно ВКЛЮЧАЕМ галочку
            finishInMiddleToggle.isOn = true;
            mazeGenerator.createFinishArea = true;
            Debug.Log("✅ Финиш в середине: ВКЛ (принудительно)");
        }

        // Устанавливаем остальные значения из MazeGenerator
        if (seedInputField != null)
        {
            seedInputField.text = mazeGenerator.mazeSeed.ToString();
            Debug.Log($"   Seed: {mazeGenerator.mazeSeed}");
        }

        if (useRandomSeedToggle != null)
        {
            useRandomSeedToggle.isOn = mazeGenerator.useRandomSeed;
            Debug.Log($"   Random Seed: {mazeGenerator.useRandomSeed}");
        }

        if (chunkSizeInputField != null)
        {
            chunkSizeInputField.text = mazeGenerator.chunkSize.ToString();
            Debug.Log($"   Chunk Size: {mazeGenerator.chunkSize}");
        }

        if (mazeWidthInputField != null)
        {
            mazeWidthInputField.text = mazeGenerator.mazeSizeInChunks.x.ToString();
            Debug.Log($"   Maze Width: {mazeGenerator.mazeSizeInChunks.x}");
        }

        if (mazeHeightInputField != null)
        {
            mazeHeightInputField.text = mazeGenerator.mazeSizeInChunks.y.ToString();
            Debug.Log($"   Maze Height: {mazeGenerator.mazeSizeInChunks.y}");
        }

        if (finishInCornerToggle != null)
        {
            finishInCornerToggle.isOn = mazeGenerator.createFinishAreaInCorner;
            Debug.Log($"   Финиш в углу: {mazeGenerator.createFinishAreaInCorner}");
        }

        // Устанавливаем сложность по умолчанию (Средняя)
        if (mediumToggle != null)
        {
            if (!mediumToggle.isOn)
            {
                mediumToggle.isOn = true;
                OnDifficultyChanged(1);
                Debug.Log("   Сложность: Средняя (установлена по умолчанию)");
            }
            else
            {
                Debug.Log("   Сложность: Средняя (уже была выбрана)");
            }
        }

        // Обновляем отображение сида
        UpdateCurrentSeedText();
    }

    // === АНИМАЦИЯ МЕНЮ ===

    void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        // Останавливаем предыдущую анимацию если есть
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        // Запускаем новую анимацию
        animationCoroutine = StartCoroutine(AnimateMenu(isMenuOpen));

        // Обновляем текст кнопки
        UpdateMenuButtonText();

        Debug.Log($"📱 Меню: {(isMenuOpen ? "Открыто" : "Закрыто")}");
    }

    IEnumerator AnimateMenu(bool open)
    {
        Vector2 startPosition = menuPanel.anchoredPosition;
        Vector2 targetPosition = open ? openPosition : closedPosition;

        float time = 0f;
        float duration = 1f / animationSpeed;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            float curveValue = animationCurve.Evaluate(t);

            menuPanel.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, curveValue);
            yield return null;
        }

        menuPanel.anchoredPosition = targetPosition;
        animationCoroutine = null;
    }

    void UpdateMenuButtonText()
    {
        if (toggleButtonText != null)
        {
            toggleButtonText.text = isMenuOpen ? "◀ Скрыть" : "▶ Настройки";
        }
    }

    // === ОБРАБОТЧИКИ UI ===

    void OnGenerateButtonClick()
    {
        Debug.Log("🔄 Нажата кнопка генерации!");

        if (mazeGenerator.IsGenerating())
        {
            Debug.Log("⏳ Генерация уже выполняется, подождите...");
            return;
        }

        // Применяем все настройки
        ApplySettingsToMazeGenerator();
        
        // Определяем и устанавливаем сложность перед генерацией
        DifficultyLevel difficulty = GetSelectedDifficulty();
        mazeGenerator.SetSelectedDifficulty(difficulty);
        Debug.Log($"📊 Сложность для генерации: {difficulty}");

        // Выбираем метод генерации
        bool useRandom = useRandomSeedToggle != null && useRandomSeedToggle.isOn;

        if (useRandom)
        {
            Debug.Log("🎲 Генерация со случайным seed");
            mazeGenerator.GenerateMazeWithRandomSeed();
        }
        else
        {
            Debug.Log($"🔢 Генерация с seed: {mazeGenerator.mazeSeed}");
            mazeGenerator.GenerateMazeWithCurrentSeed();
        }
    }
    
    /// <summary>
    /// Получает выбранную сложность из UI
    /// </summary>
    private DifficultyLevel GetSelectedDifficulty()
    {
        if (easyToggle != null && easyToggle.isOn)
        {
            return DifficultyLevel.Easy;
        }
        else if (mediumToggle != null && mediumToggle.isOn)
        {
            return DifficultyLevel.Medium;
        }
        else if (hardToggle != null && hardToggle.isOn)
        {
            return DifficultyLevel.Hard;
        }
        else
        {
            // По умолчанию средняя сложность
            return DifficultyLevel.Medium;
        }
    }

    void OnResetSettingsButtonClick()
    {
        Debug.Log("🔄 Сброс настроек к значениям по умолчанию...");

        // Значения по умолчанию
        mazeGenerator.chunkSize = 4;
        mazeGenerator.mazeSizeInChunks = new Vector2Int(3, 3);
        mazeGenerator.createFinishArea = true;  // ФИНИШ ВКЛЮЧЕН ПО УМОЛЧАНИЮ
        mazeGenerator.createFinishAreaInCorner = false;
        mazeGenerator.useRandomSeed = true;

        // Генерируем случайный seed
        mazeGenerator.mazeSeed = new System.Random().Next(1000, 999999);

        // Обновляем UI
        SetInitialValues();

        Debug.Log("✅ Настройки сброшены к значениям по умолчанию (финиш в середине ВКЛ)");
    }

    void OnSeedInputChanged(string value)
    {
        if (int.TryParse(value, out int seed))
        {
            if (useRandomSeedToggle != null)
                useRandomSeedToggle.isOn = false;

            mazeGenerator.SetSeed(seed);
            UpdateCurrentSeedText();

            Debug.Log($"🔢 Seed изменён: {seed}");
        }
    }

    void OnRandomSeedToggleChanged(bool isRandom)
    {
        // Если включили случайный seed, генерируем новый
        if (isRandom)
        {
            mazeGenerator.mazeSeed = new System.Random().Next(1000, 999999);
            if (seedInputField != null)
                seedInputField.text = mazeGenerator.mazeSeed.ToString();
        }

        UpdateCurrentSeedText();

        Debug.Log($"🎲 Случайный seed: {(isRandom ? "ВКЛ" : "ВЫКЛ")}");
    }

    void OnChunkSizeChanged(string value)
    {
        if (int.TryParse(value, out int chunkSize) && chunkSize > 0)
        {
            mazeGenerator.chunkSize = chunkSize;
            Debug.Log($"📏 Размер чанка изменён: {chunkSize}");
        }
    }

    void OnMazeWidthChanged(string value)
    {
        if (int.TryParse(value, out int width) && width > 0)
        {
            mazeGenerator.mazeSizeInChunks.x = width;
            Debug.Log($"📏 Ширина лабиринта изменена: {width}");
        }
    }

    void OnMazeHeightChanged(string value)
    {
        if (int.TryParse(value, out int height) && height > 0)
        {
            mazeGenerator.mazeSizeInChunks.y = height;
            Debug.Log($"📏 Высота лабиринта изменена: {height}");
        }
    }

    void OnFinishInMiddleChanged(bool value)
    {
        mazeGenerator.createFinishArea = value;
        // Если включаем финиш в середине, выключаем финиш в углу
        if (value && finishInCornerToggle != null)
        {
            finishInCornerToggle.isOn = false;
            mazeGenerator.createFinishAreaInCorner = false;
        }
        Debug.Log($"🎯 Финиш в середине: {(value ? "ВКЛ" : "ВЫКЛ")}");
    }

    void OnFinishInCornerChanged(bool value)
    {
        mazeGenerator.createFinishAreaInCorner = value;
        // Если включаем финиш в углу, выключаем финиш в середине
        if (value && finishInMiddleToggle != null)
        {
            finishInMiddleToggle.isOn = false;
            mazeGenerator.createFinishArea = false;
        }
        Debug.Log($"🎯 Финиш в углу: {(value ? "ВКЛ" : "ВЫКЛ")}");
    }

    void OnDifficultyChanged(int difficultyIndex)
    {
        // Сложность больше не влияет на размеры лабиринта
        // Размеры настраиваются отдельно через поля ввода
        switch (difficultyIndex)
        {
            case 0: // Лёгкая
                Debug.Log("🎮 Сложность: Лёгкая");
                break;

            case 1: // Средняя
                Debug.Log("🎮 Сложность: Средняя");
                break;

            case 2: // Сложная
                Debug.Log("🎮 Сложность: Сложная");
                break;
        }
    }

    void ApplySettingsToMazeGenerator()
    {
        Debug.Log("⚙️ Применение настроек к MazeGenerator...");

        // Применяем все настройки из UI

        // Размеры лабиринта всегда берутся из полей ввода, независимо от сложности
        if (chunkSizeInputField != null && int.TryParse(chunkSizeInputField.text, out int chunkSize))
            mazeGenerator.chunkSize = chunkSize;

        if (mazeWidthInputField != null && int.TryParse(mazeWidthInputField.text, out int width))
            mazeGenerator.mazeSizeInChunks.x = width;

        if (mazeHeightInputField != null && int.TryParse(mazeHeightInputField.text, out int height))
            mazeGenerator.mazeSizeInChunks.y = height;

        // Остальные настройки - финиш в середине или в углу
        if (finishInMiddleToggle != null)
        {
            mazeGenerator.createFinishArea = finishInMiddleToggle.isOn;
            Debug.Log($"   Финиш в середине: {(finishInMiddleToggle.isOn ? "ВКЛ" : "ВЫКЛ")}");
        }

        if (finishInCornerToggle != null)
        {
            mazeGenerator.createFinishAreaInCorner = finishInCornerToggle.isOn;
            Debug.Log($"   Финиш в углу: {(finishInCornerToggle.isOn ? "ВКЛ" : "ВЫКЛ")}");
        }

        // Настройки seed
        if (useRandomSeedToggle != null)
        {
            mazeGenerator.useRandomSeed = useRandomSeedToggle.isOn;

            // Если не случайный seed, то берём из поля ввода
            if (!useRandomSeedToggle.isOn && seedInputField != null && int.TryParse(seedInputField.text, out int seed))
                mazeGenerator.SetSeed(seed);
        }

        // Обновляем отображение сида
        UpdateCurrentSeedText();

        Debug.Log("✅ Все настройки применены");
    }

    void UpdateCurrentSeedText()
    {
        if (mazeGenerator != null && currentSeedText != null)
        {
            if (mazeGenerator.useRandomSeed)
                currentSeedText.text = "Seed: 🎲 Случайный";
            else
                currentSeedText.text = $"Seed: {mazeGenerator.mazeSeed}";
        }
    }

    // Обработчики управления камерой
    void OnFullMazeViewButtonClick()
    {
        if (cameraController != null)
        {
            cameraController.SetFullMazeViewMode();
            Debug.Log("📷 Режим камеры: Обзор на весь лабиринт");
        }
        else
        {
            Debug.LogWarning("⚠️ MazeCameraController не найден!");
        }
    }

    void OnFollowCarButtonClick()
    {
        if (cameraController != null)
        {
            cameraController.SetFollowCarMode();
            Debug.Log("📷 Режим камеры: Слежение за машинкой");
            
            // Показываем ползунок зума
            if (followCarZoomSlider != null)
            {
                followCarZoomSlider.gameObject.SetActive(true);
                followCarZoomSlider.minValue = cameraController.followCarMinZoom;
                followCarZoomSlider.maxValue = cameraController.followCarMaxZoom;
                followCarZoomSlider.value = cameraController.followCarZoom;
            }
            if (followCarZoomText != null)
            {
                followCarZoomText.gameObject.SetActive(true);
                UpdateFollowCarZoomText();
            }
        }
        else
        {
            Debug.LogWarning("⚠️ MazeCameraController не найден!");
        }
    }

    void OnFirstPersonButtonClick()
    {
        if (cameraController != null)
        {
            cameraController.SetFirstPersonMode();
            Debug.Log("📷 Режим камеры: Вид от первого лица");
            
            // Скрываем ползунок зума
            if (followCarZoomSlider != null)
                followCarZoomSlider.gameObject.SetActive(false);
            if (followCarZoomText != null)
                followCarZoomText.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("⚠️ MazeCameraController не найден!");
        }
    }

    void OnFollowCarZoomChanged(float value)
    {
        if (cameraController != null)
        {
            cameraController.SetFollowCarZoom(value);
            UpdateFollowCarZoomText();
        }
    }

    void UpdateFollowCarZoomText()
    {
        if (followCarZoomText != null && cameraController != null)
        {
            followCarZoomText.text = $"Зум: {cameraController.GetFollowCarZoom():F1}";
        }
    }

    // Обновление каждый кадр для отслеживания изменений сида
    void Update()
    {
        // Проверяем изменения сида напрямую
        if (mazeGenerator != null && currentSeedText != null)
        {
            string expectedText = mazeGenerator.useRandomSeed ?
                "Seed: 🎲 Случайный" :
                $"Seed: {mazeGenerator.mazeSeed}";

            if (currentSeedText.text != expectedText)
            {
                currentSeedText.text = expectedText;
            }
        }

        // Горячие клавиши для удобства
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleMenu();
        }
    }

    // === ВАЛИДАЦИЯ ===

    void OnValidate()
    {
        // В редакторе проверяем основные ссылки
        if (generateButton == null)
            Debug.LogWarning("⚠️ generateButton не привязан! Это обязательное поле.");

        if (menuPanel == null)
            Debug.LogWarning("⚠️ menuPanel не привязан! Это обязательное поле.");

        if (toggleMenuButton == null)
            Debug.LogWarning("⚠️ toggleMenuButton не привязан! Это обязательное поле.");

        if (finishInMiddleToggle == null)
            Debug.LogWarning("⚠️ finishInMiddleToggle не привязан!");
    }
}