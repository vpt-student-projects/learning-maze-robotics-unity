using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MazeSeedUI : MonoBehaviour
{
    [Header("UI элементы")]
    public TMP_InputField seedInputField;
    public Toggle useRandomToggle;
    public Button generateButton;
    public Button randomSeedButton;
    public Button useCurrentSeedButton;
    public TextMeshProUGUI currentSeedText;

    [Header("Ссылки")]
    public MazeGenerator mazeGenerator;

    void Start()
    {
        if (mazeGenerator == null)
        {
            mazeGenerator = FindObjectOfType<MazeGenerator>();
        }

        InitializeUI();
    }

    private void InitializeUI()
    {
        // Обновляем UI значениями из MazeGenerator
        if (mazeGenerator != null)
        {
            seedInputField.text = mazeGenerator.mazeSeed.ToString();
            useRandomToggle.isOn = mazeGenerator.useRandomSeed;
            UpdateCurrentSeedText();
        }

        // Подписываемся на события
        seedInputField.onEndEdit.AddListener(OnSeedInputChanged);
        useRandomToggle.onValueChanged.AddListener(OnRandomToggleChanged);

        if (generateButton != null)
            generateButton.onClick.AddListener(OnGenerateClicked);

        if (randomSeedButton != null)
            randomSeedButton.onClick.AddListener(OnRandomSeedClicked);

        if (useCurrentSeedButton != null)
            useCurrentSeedButton.onClick.AddListener(OnUseCurrentSeedClicked);

        // Обновляем доступность поля ввода
        UpdateInputFieldState();
    }

    private void OnSeedInputChanged(string value)
    {
        if (int.TryParse(value, out int seed))
        {
            mazeGenerator.SetSeed(seed);
            useRandomToggle.isOn = false; // При ручном вводе отключаем случайный seed
            UpdateCurrentSeedText();
        }
    }

    private void OnRandomToggleChanged(bool isRandom)
    {
        mazeGenerator.useRandomSeed = isRandom;
        UpdateInputFieldState();
    }

    private void OnGenerateClicked()
    {
        if (mazeGenerator != null && !mazeGenerator.IsGenerating())
        {
            if (useRandomToggle.isOn)
            {
                mazeGenerator.GenerateMazeWithRandomSeed();
            }
            else
            {
                mazeGenerator.GenerateMazeWithCurrentSeed();
            }
        }
    }

    private void OnRandomSeedClicked()
    {
        mazeGenerator.GenerateMazeWithRandomSeed();
        // Обновляем UI после генерации
        seedInputField.text = mazeGenerator.mazeSeed.ToString();
        useRandomToggle.isOn = false;
        UpdateCurrentSeedText();
    }

    private void OnUseCurrentSeedClicked()
    {
        if (mazeGenerator != null)
        {
            seedInputField.text = mazeGenerator.mazeSeed.ToString();
            useRandomToggle.isOn = false;
            mazeGenerator.useRandomSeed = false;
            UpdateInputFieldState();
            UpdateCurrentSeedText();
        }
    }

    private void UpdateInputFieldState()
    {
        seedInputField.interactable = !useRandomToggle.isOn;
    }

    public void UpdateCurrentSeedText()
    {
        if (mazeGenerator != null && currentSeedText != null)
        {
            currentSeedText.text = $"Текущий Seed: {mazeGenerator.mazeSeed}";
        }
    }

    void Update()
    {
        // Периодически обновляем текст текущего seed
        if (Time.frameCount % 30 == 0 && mazeGenerator != null && currentSeedText != null)
        {
            currentSeedText.text = $"Текущий Seed: {mazeGenerator.mazeSeed}";
        }
    }
}