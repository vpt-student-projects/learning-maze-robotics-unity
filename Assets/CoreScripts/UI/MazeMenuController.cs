using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class MazeMenuController : MonoBehaviour
{
    [Header("Главные ссылки (обязательно)")]
    public MazeGenerator mazeGenerator;

    // Эти поля будут найдены автоматически
    [HideInInspector] public Button generateButton;
    [HideInInspector] public TMP_InputField seedInputField;
    [HideInInspector] public Toggle useRandomSeedToggle;
    [HideInInspector] public TextMeshProUGUI currentSeedText;
    [HideInInspector] public Button randomSeedButton;

    void Start()
    {
        Debug.Log("🔄 Инициализация MazeMenuController...");

        // 1. Автоматически находим MazeGenerator если не привязан
        if (mazeGenerator == null)
        {
            mazeGenerator = FindObjectOfType<MazeGenerator>();
            if (mazeGenerator == null)
            {
                Debug.LogError("❌ MazeGenerator не найден в сцене!");
                enabled = false;
                return;
            }
            Debug.Log("✅ MazeGenerator найден автоматически");
        }

        // 2. Находим ВСЕ UI элементы автоматически
        FindAllUIElements();

        // 3. Инициализируем кнопки
        InitializeButtons();

        // 4. Устанавливаем начальные значения
        SetInitialValues();

        Debug.Log("✅ MazeMenuController успешно инициализирован!");
    }

    void FindAllUIElements()
    {
        // ВАЖНО: Ищем кнопку ButtonCreate (ваша кнопка!)
        generateButton = FindButtonByName("ButtonCreate", "GenerateButton", "GenerateBtn", "ButtonGenerate", "Generate");

        if (generateButton == null)
        {
            Debug.LogError("❌ Не удалось найти кнопку генерации!");
            Debug.Log("Искал кнопки: ButtonCreate, GenerateButton, GenerateBtn, ButtonGenerate, Generate");
            enabled = false;
            return;
        }

        // Находим поле ввода seed
        seedInputField = FindInputFieldByName("SeedInputField", "SeedInput", "SeedField");

        // Находим toggle случайного seed
        useRandomSeedToggle = FindToggleByName("UseRandomSeedToggle", "RandomSeedToggle", "RandomToggle");

        // Находим текст текущего seed
        currentSeedText = FindTextByName("CurrentSeedText", "SeedText", "CurrentSeed");

        // Находим кнопку случайного seed
        randomSeedButton = FindButtonByName("RandomSeedButton", "RandomSeedBtn", "RandomButton");

        Debug.Log("✅ Все UI элементы найдены");
    }

    void InitializeButtons()
    {
        // Подписываемся на клик основной кнопки
        generateButton.onClick.RemoveAllListeners();
        generateButton.onClick.AddListener(OnGenerateButtonClick);

        // Подписываемся на кнопку случайного seed если есть
        if (randomSeedButton != null)
        {
            randomSeedButton.onClick.RemoveAllListeners();
            randomSeedButton.onClick.AddListener(OnRandomSeedButtonClick);
        }

        // Подписываемся на изменения в поле seed если есть
        if (seedInputField != null)
        {
            seedInputField.onEndEdit.RemoveAllListeners();
            seedInputField.onEndEdit.AddListener(OnSeedInputChanged);
        }

        // Подписываемся на изменения toggle если есть
        if (useRandomSeedToggle != null)
        {
            useRandomSeedToggle.onValueChanged.RemoveAllListeners();
            useRandomSeedToggle.onValueChanged.AddListener(OnRandomSeedToggleChanged);
        }
    }

    void SetInitialValues()
    {
        // Устанавливаем начальные значения
        if (seedInputField != null)
        {
            seedInputField.text = mazeGenerator.mazeSeed.ToString();
        }

        if (useRandomSeedToggle != null)
        {
            useRandomSeedToggle.isOn = mazeGenerator.useRandomSeed;
        }

        UpdateCurrentSeedText();
    }

    // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ ПОИСКА ===

    Button FindButtonByName(params string[] names)
    {
        foreach (string name in names)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                Button btn = obj.GetComponent<Button>();
                if (btn != null)
                {
                    Debug.Log($"✅ Найдена кнопка: {name}");
                    return btn;
                }
            }
        }

        // Если не нашли по имени, ищем любую кнопку с текстом "Generate" или "Create"
        Button[] allButtons = FindObjectsOfType<Button>();
        foreach (Button btn in allButtons)
        {
            string buttonText = GetButtonText(btn);
            if (!string.IsNullOrEmpty(buttonText))
            {
                if (buttonText.Contains("Generate") ||
                    buttonText.Contains("Генерировать") ||
                    buttonText.Contains("Create") ||
                    buttonText.Contains("Создать"))
                {
                    Debug.Log($"✅ Найдена кнопка по тексту: {btn.name} (текст: {buttonText})");
                    return btn;
                }
            }
        }

        Debug.LogWarning($"⚠️ Не найдена кнопка с именами: {string.Join(", ", names)}");
        return null;
    }

    string GetButtonText(Button button)
    {
        // Пробуем получить текст из TextMeshPro
        TextMeshProUGUI tmpText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (tmpText != null) return tmpText.text;

        // Пробуем получить текст из обычного Text
        Text text = button.GetComponentInChildren<Text>();
        if (text != null) return text.text;

        return "";
    }

    TMP_InputField FindInputFieldByName(params string[] names)
    {
        foreach (string name in names)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                TMP_InputField input = obj.GetComponent<TMP_InputField>();
                if (input != null)
                {
                    Debug.Log($"✅ Найдено поле ввода: {name}");
                    return input;
                }
            }
        }
        Debug.LogWarning($"⚠️ Не найдено поле ввода с именами: {string.Join(", ", names)}");
        return null;
    }

    Toggle FindToggleByName(params string[] names)
    {
        foreach (string name in names)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                Toggle toggle = obj.GetComponent<Toggle>();
                if (toggle != null)
                {
                    Debug.Log($"✅ Найден toggle: {name}");
                    return toggle;
                }
            }
        }
        Debug.LogWarning($"⚠️ Не найден toggle с именами: {string.Join(", ", names)}");
        return null;
    }

    TextMeshProUGUI FindTextByName(params string[] names)
    {
        foreach (string name in names)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
                if (text != null)
                {
                    Debug.Log($"✅ Найден текст: {name}");
                    return text;
                }
            }
        }
        Debug.LogWarning($"⚠️ Не найден текст с именами: {string.Join(", ", names)}");
        return null;
    }

    // === ОСНОВНЫЕ МЕТОДЫ ===

    void OnGenerateButtonClick()
    {
        Debug.Log("🔄 Нажата кнопка генерации!");

        if (mazeGenerator == null)
        {
            Debug.LogError("❌ MazeGenerator не найден!");
            return;
        }

        if (mazeGenerator.IsGenerating())
        {
            Debug.Log("⏳ Генерация уже выполняется, подождите...");
            return;
        }

        // Применяем настройки
        ApplySettingsToMazeGenerator();

        // Выбираем метод генерации
        bool useRandom = (useRandomSeedToggle != null && useRandomSeedToggle.isOn);

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

    void OnRandomSeedButtonClick()
    {
        if (mazeGenerator != null)
        {
            // Генерируем случайный seed
            int randomSeed = new System.Random().Next(1000, 999999);

            if (seedInputField != null)
            {
                seedInputField.text = randomSeed.ToString();
            }

            if (useRandomSeedToggle != null)
            {
                useRandomSeedToggle.isOn = false;
            }

            mazeGenerator.SetSeed(randomSeed);
            UpdateCurrentSeedText();

            Debug.Log($"🎲 Сгенерирован новый seed: {randomSeed}");
        }
    }

    void OnSeedInputChanged(string value)
    {
        if (int.TryParse(value, out int seed))
        {
            if (useRandomSeedToggle != null)
            {
                useRandomSeedToggle.isOn = false;
            }

            if (mazeGenerator != null)
            {
                mazeGenerator.SetSeed(seed);
                UpdateCurrentSeedText();
            }
        }
    }

    void OnRandomSeedToggleChanged(bool isRandom)
    {
        UpdateCurrentSeedText();
    }

    void ApplySettingsToMazeGenerator()
    {
        // Здесь можно добавить другие настройки если нужно
        // Сейчас работаем только с seed

        if (seedInputField != null && useRandomSeedToggle != null)
        {
            if (!useRandomSeedToggle.isOn && int.TryParse(seedInputField.text, out int seed))
            {
                mazeGenerator.SetSeed(seed);
            }
            mazeGenerator.useRandomSeed = useRandomSeedToggle.isOn;
        }

        Debug.Log("⚙️ Настройки seed применены");
    }

    void UpdateCurrentSeedText()
    {
        if (mazeGenerator != null && currentSeedText != null)
        {
            if (mazeGenerator.useRandomSeed)
            {
                currentSeedText.text = "Seed: 🎲 Случайный";
            }
            else
            {
                currentSeedText.text = $"Seed: {mazeGenerator.mazeSeed}";
            }
        }
    }

    // Обновление для отладки
    void Update()
    {
        // Горячие клавиши для тестирования
        if (Input.GetKeyDown(KeyCode.F5))
        {
            Debug.Log("🔧 Принудительный запуск генерации по F5");
            OnGenerateButtonClick();
        }

        if (Input.GetKeyDown(KeyCode.F6))
        {
            Debug.Log("🔧 Проверка кнопок");
            FindAllUIElements();
        }
    }
}