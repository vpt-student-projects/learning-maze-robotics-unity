using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class BlockEditor : MonoBehaviour
{
    [Header("Основные настройки")]
    public CarController carController;
    public Transform blockContainer;
    public CommandBlock startBlock;
    public Transform spawnPosition;
    
    [Header("Префабы блоков")]
    public GameObject moveBlockPrefab;
    public GameObject turnBlockPrefab;
    public GameObject ifBlockPrefab;
    public GameObject loopBlockPrefab;
    public GameObject waitBlockPrefab;
    
    [Header("UI - Создание блоков")]
    public GameObject blockCreationPanel;
    public Dropdown blockTypeDropdown;
    public Button createBlockButton;
    public Button confirmCreateButton;
    public Button cancelCreateButton;
    
    [Header("UI - Быстрое создание")]
    public Button quickCreateButton;
    public GameObject quickCreatePanel;
    public Button moveQuickButton;
    public Button turnQuickButton;
    public Button ifQuickButton;
    public Button loopQuickButton;
    public Button waitQuickButton;
    
    [Header("UI - Управление программой")]
    public Button runButton;
    public Button stopButton;
    public Button pauseButton;
    public Button stepButton;
    public Button clearButton;
    public Button resetButton;
    public Button saveButton;
    public Button loadButton;
    
    [Header("UI - Статус и информация")]
    public Text statusText;
    public Text carStatusText;
    public Text blockCountText;
    public Text programLengthText;
    public Slider speedSlider;
    public Text speedText;
    
    [Header("Настройки выполнения")]
    public float blockDelay = 0.1f;
    public float executionSpeed = 1.0f;
    
    // Списки и состояния
    private List<CommandBlock> allBlocks = new List<CommandBlock>();
    private bool isRunning = false;
    private bool isPaused = false;
    private Coroutine executionCoroutine;
    
    // Позиционирование
    private Vector2 nextBlockPosition = new Vector2(0, -120);
    private float verticalSpacing = 120f;
    private float horizontalSpacing = 200f;
    
    // Режим соединения
    private ConnectionPoint currentConnectionPoint;
    private bool isConnectionMode = false;
    
    // История действий для Undo/Redo
    private Stack<EditorAction> undoStack = new Stack<EditorAction>();
    private Stack<EditorAction> redoStack = new Stack<EditorAction>();
    
    void Start()
    {
        InitializeUI();
        InitializeBlocks();
        UpdateAllUI();
        
        // Автообновление статуса
        StartCoroutine(AutoUpdateCarStatus());
    }
    
    void InitializeUI()
    {
        // === СОЗДАНИЕ БЛОКОВ ===
        if (blockCreationPanel != null)
            blockCreationPanel.SetActive(false);
        
        if (quickCreatePanel != null)
            quickCreatePanel.SetActive(false);
        
        // Настройка Dropdown
        if (blockTypeDropdown != null)
        {
            blockTypeDropdown.ClearOptions();
            List<string> options = new List<string>
            {
                "Движение",
                "Поворот", 
                "Условие",
                "Цикл",
                "Ожидание"
            };
            blockTypeDropdown.AddOptions(options);
        }
        
        // Кнопки создания
        if (createBlockButton != null)
            createBlockButton.onClick.AddListener(OpenCreationPanel);
        
        if (confirmCreateButton != null)
            confirmCreateButton.onClick.AddListener(CreateSelectedBlock);
        
        if (cancelCreateButton != null)
            cancelCreateButton.onClick.AddListener(() => blockCreationPanel.SetActive(false));
        
        // Быстрые кнопки
        if (quickCreateButton != null)
            quickCreateButton.onClick.AddListener(ToggleQuickCreatePanel);
        
        if (moveQuickButton != null)
            moveQuickButton.onClick.AddListener(() => CreateBlock(BlockType.Move));
        
        if (turnQuickButton != null)
            turnQuickButton.onClick.AddListener(() => CreateBlock(BlockType.Turn));
        
        if (ifQuickButton != null)
            ifQuickButton.onClick.AddListener(() => CreateBlock(BlockType.If));
        
        if (loopQuickButton != null)
            loopQuickButton.onClick.AddListener(() => CreateBlock(BlockType.Loop));
        
        if (waitQuickButton != null)
            waitQuickButton.onClick.AddListener(() => CreateBlock(BlockType.Wait));
        
        // === УПРАВЛЕНИЕ ВЫПОЛНЕНИЕМ ===
        if (runButton != null)
            runButton.onClick.AddListener(StartExecution);
        
        if (stopButton != null)
            stopButton.onClick.AddListener(StopExecution);
        
        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);
        
        if (stepButton != null)
            stepButton.onClick.AddListener(ExecuteStep);
        
        if (clearButton != null)
            clearButton.onClick.AddListener(ClearWorkspace);
        
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetCar);
        
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveProgram);
        
        if (loadButton != null)
            loadButton.onClick.AddListener(LoadProgram);
        
        // === НАСТРОЙКИ ===
        if (speedSlider != null)
        {
            speedSlider.minValue = 0.1f;
            speedSlider.maxValue = 3.0f;
            speedSlider.value = executionSpeed;
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
        }
    }
    
    void InitializeBlocks()
    {
        UpdateBlockList();
        
        // Начальная позиция для новых блоков
        if (allBlocks.Count > 0)
        {
            float lowestY = 0;
            foreach (var block in allBlocks)
            {
                if (block != startBlock)
                {
                    RectTransform rt = block.GetComponent<RectTransform>();
                    if (rt != null && rt.anchoredPosition.y < lowestY)
                        lowestY = rt.anchoredPosition.y;
                }
            }
            nextBlockPosition.y = lowestY - verticalSpacing;
        }
    }
    
    // === СОЗДАНИЕ БЛОКОВ ===
    void OpenCreationPanel()
    {
        if (blockCreationPanel != null)
            blockCreationPanel.SetActive(true);
        
        if (quickCreatePanel != null)
            quickCreatePanel.SetActive(false);
        
        if (blockTypeDropdown != null)
            blockTypeDropdown.value = 0;
    }
    
    void ToggleQuickCreatePanel()
    {
        if (quickCreatePanel != null)
            quickCreatePanel.SetActive(!quickCreatePanel.activeSelf);
    }
    
    void CreateSelectedBlock()
    {
        if (blockTypeDropdown != null)
        {
            BlockType type = (BlockType)blockTypeDropdown.value;
            CreateBlock(type);
        }
        
        if (blockCreationPanel != null)
            blockCreationPanel.SetActive(false);
    }
    
    void CreateBlock(BlockType type)
    {
        GameObject prefab = GetBlockPrefab(type);
        
        if (prefab != null && blockContainer != null)
        {
            // Создаем блок
            GameObject newBlock = Instantiate(prefab, blockContainer);
            
            // Позиционируем
            RectTransform rt = newBlock.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = nextBlockPosition;
                nextBlockPosition.y -= verticalSpacing;
            }
            
            // Настраиваем компоненты
            CommandBlock blockComponent = newBlock.GetComponent<CommandBlock>();
            if (blockComponent != null)
            {
                allBlocks.Add(blockComponent);
                SetupBlockComponents(blockComponent);
                
                // Запись в историю
                RecordAction(EditorAction.ActionType.CreateBlock, blockComponent);
                
                // Авто-соединение с предыдущим блоком
                AutoConnectNewBlock(blockComponent);
            }
            
            UpdateAllUI();
            UpdateStatus($"Создан блок: {GetBlockTypeName(type)}");
        }
    }
    
    GameObject GetBlockPrefab(BlockType type)
    {
        switch (type)
        {
            case BlockType.Move: return moveBlockPrefab;
            case BlockType.Turn: return turnBlockPrefab;
            case BlockType.If: return ifBlockPrefab;
            case BlockType.Loop: return loopBlockPrefab;
            case BlockType.Wait: return waitBlockPrefab;
            default: return null;
        }
    }
    
    string GetBlockTypeName(BlockType type)
    {
        switch (type)
        {
            case BlockType.Move: return "Движение";
            case BlockType.Turn: return "Поворот";
            case BlockType.If: return "Условие";
            case BlockType.Loop: return "Цикл";
            case BlockType.Wait: return "Ожидание";
            default: return "Блок";
        }
    }
    
    void SetupBlockComponents(CommandBlock block)
    {
        // Настройка CarController для блоков, которым он нужен
        if (block is MoveBlock moveBlock)
        {
            moveBlock.carController = carController;
        }
        else if (block is TurnBlock turnBlock)
        {
            turnBlock.carController = carController;
        }
        else if (block is IfBlock ifBlock)
        {
            ifBlock.carController = carController;
        }
    }
    
    // === ВЫПОЛНЕНИЕ ПРОГРАММЫ ===
    void StartExecution()
    {
        if (isRunning || !IsCarReady()) return;
        
        isRunning = true;
        isPaused = false;
        executionCoroutine = StartCoroutine(ExecuteProgram());
        UpdateStatus("Выполнение...");
    }
    
    void StopExecution()
    {
        if (executionCoroutine != null)
            StopCoroutine(executionCoroutine);
        
        isRunning = false;
        isPaused = false;
        
        // Сбрасываем подсветку всех блоков
        foreach (var block in allBlocks)
        {
            if (block != null)
                block.ResetState();
        }
        
        UpdateStatus("Остановлено");
        UpdateAllUI();
    }
    
    void TogglePause()
    {
        if (!isRunning) return;
        
        isPaused = !isPaused;
        UpdateStatus(isPaused ? "Пауза" : "Выполнение...");
    }
    
    void ExecuteStep()
    {
        if (!isRunning)
        {
            // Если программа не запущена, выполняем один шаг
            StartCoroutine(ExecuteSingleStep());
        }
        else if (isPaused)
        {
            // Если на паузе, выполняем один шаг
            StartCoroutine(ExecuteSingleStep());
        }
    }
    
    IEnumerator ExecuteProgram()
    {
        CommandBlock currentBlock = startBlock;
        
        while (currentBlock != null && isRunning && IsCarReady())
        {
            if (isPaused)
            {
                yield return new WaitUntil(() => !isPaused || !isRunning);
                if (!isRunning) break;
            }
            
            yield return currentBlock.Execute();
            
            // Задержка между блоками с учетом скорости
            yield return new WaitForSeconds(blockDelay / executionSpeed);
            
            currentBlock = currentBlock.GetNextBlock();
        }
        
        isRunning = false;
        
        if (currentBlock == null)
            UpdateStatus("Программа завершена");
        else if (!IsCarReady())
            UpdateStatus("Прервано: машинка не готова");
        
        UpdateAllUI();
    }
    
    IEnumerator ExecuteSingleStep()
    {
        CommandBlock currentBlock = GetCurrentExecutingBlock();
        
        if (currentBlock != null && IsCarReady())
        {
            yield return currentBlock.Execute();
            
            // Обновляем UI после шага
            UpdateCarStatus();
            UpdateBlockCount();
        }
    }
    
    CommandBlock GetCurrentExecutingBlock()
    {
        // Находим текущий выполняемый блок
        foreach (var block in allBlocks)
        {
            if (block != null && block.isExecuting)
                return block;
        }
        
        // Если ничего не выполняется, возвращаем стартовый
        return startBlock.GetNextBlock() ?? startBlock;
    }
    
    // === УПРАВЛЕНИЕ БЛОКАМИ ===
    void ClearWorkspace()
    {
        if (isRunning) 
        {
            UpdateStatus("Нельзя очищать во время выполнения!");
            return;
        }
        
        List<CommandBlock> blocksToRemove = new List<CommandBlock>();
        
        foreach (var block in allBlocks)
        {
            if (block != null && block != startBlock)
                blocksToRemove.Add(block);
        }
        
        foreach (var block in blocksToRemove)
        {
            Destroy(block.gameObject);
            allBlocks.Remove(block);
        }
        
        UpdateBlockPositions();
        UpdateStatus("Рабочая область очищена");
        UpdateAllUI();
    }
    
    void UpdateBlockPositions()
    {
        float yPos = 0;
        
        // Стартовый блок всегда наверху
        if (startBlock != null)
        {
            RectTransform startRt = startBlock.GetComponent<RectTransform>();
            if (startRt != null)
                startRt.anchoredPosition = new Vector2(0, yPos);
            yPos -= verticalSpacing;
        }
        
        // Остальные блоки по порядку
        foreach (var block in allBlocks)
        {
            if (block != null && block != startBlock)
            {
                RectTransform rt = block.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(0, yPos);
                    yPos -= verticalSpacing;
                }
            }
        }
        
        nextBlockPosition = new Vector2(0, yPos);
    }
    
    void AutoConnectNewBlock(CommandBlock newBlock)
    {
        if (allBlocks.Count > 1 && newBlock.inputPoint != null)
        {
            // Найти последний блок с выходом
            for (int i = allBlocks.Count - 2; i >= 0; i--)
            {
                CommandBlock lastBlock = allBlocks[i];
                if (lastBlock != null && lastBlock != newBlock && 
                    lastBlock.outputPoint != null && !lastBlock.outputPoint.IsConnected())
                {
                    // Автоматическое логическое соединение:
                    // указываем, что выход последнего блока ведёт к новому блоку
                    lastBlock.nextBlock = newBlock;

                    // Визуально просто связываем ConnectionPoint'ы
                    lastBlock.outputPoint.connectedTo = newBlock.inputPoint;
                    newBlock.inputPoint.connectedTo = lastBlock.outputPoint;

                    UpdateStatus("Блоки автоматически соединены");
                    break;
                }
            }
        }
    }
    
    // === РЕЖИМ СОЕДИНЕНИЯ ===
    public void StartConnectionMode(ConnectionPoint outputPoint)
    {
        isConnectionMode = true;
        currentConnectionPoint = outputPoint;
        UpdateStatus("Выберите блок для соединения...");
        
        // Подсветить все доступные входы
        HighlightAvailableInputs(true);
    }
    
    public void EndConnectionMode(bool success = true)
    {
        isConnectionMode = false;
        
        // Убрать подсветку
        HighlightAvailableInputs(false);
        
        if (currentConnectionPoint != null)
        {
            if (!success)
                currentConnectionPoint.Disconnect();
            
            currentConnectionPoint = null;
        }
        
        UpdateStatus(success ? "Соединение установлено" : "Соединение отменено");
    }
    
    public bool IsInConnectionMode() => isConnectionMode;
    public ConnectionPoint GetCurrentConnectionPoint() => currentConnectionPoint;
    
    void HighlightAvailableInputs(bool highlight)
    {
        ConnectionPoint[] allPoints = FindObjectsOfType<ConnectionPoint>();
        foreach (var point in allPoints)
        {
            if (point.pointType == ConnectionPoint.PointType.Input && 
                !point.IsConnected() && point != currentConnectionPoint)
            {
                if (point.pointImage != null)
                {
                    point.pointImage.color = highlight ? Color.yellow : point.normalColor;
                }
            }
        }
    }
    
    // === УПРАВЛЕНИЕ МАШИНКОЙ ===
    void ResetCar()
    {
        if (carController != null)
        {
            // Ищем метод сброса в CarController
            var resetMethod = carController.GetType().GetMethod("ResetCar");
            if (resetMethod != null)
            {
                resetMethod.Invoke(carController, null);
                UpdateStatus("Машинка сброшена на старт");
            }
            else
            {
                // Альтернатива: просто сброс направления
                var directionMethod = carController.GetType().GetMethod("ResetDirection");
                if (directionMethod != null)
                    directionMethod.Invoke(carController, null);
                
                UpdateStatus("Направление сброшено");
            }
            
            UpdateCarStatus();
        }
    }
    
    bool IsCarReady()
    {
        return carController != null && carController.IsCarReady();
    }
    
    // === СОХРАНЕНИЕ/ЗАГРУЗКА ===
    void SaveProgram()
    {
        // Сохраняем программу в PlayerPrefs или файл
        string programData = SerializeProgram();
        PlayerPrefs.SetString("SavedProgram", programData);
        PlayerPrefs.Save();
        
        UpdateStatus("Программа сохранена");
    }
    
    void LoadProgram()
    {
        if (isRunning)
        {
            UpdateStatus("Нельзя загружать во время выполнения!");
            return;
        }
        
        if (PlayerPrefs.HasKey("SavedProgram"))
        {
            ClearWorkspace();
            string programData = PlayerPrefs.GetString("SavedProgram");
            DeserializeProgram(programData);
            UpdateStatus("Программа загружена");
        }
        else
        {
            UpdateStatus("Нет сохраненных программ");
        }
    }
    
    string SerializeProgram()
    {
        // Простая сериализация (можно расширить)
        List<string> blockData = new List<string>();
        
        foreach (var block in allBlocks)
        {
            if (block != null && block != startBlock)
            {
                string data = $"{block.GetType().Name}|{block.GetBlockName()}";
                blockData.Add(data);
            }
        }
        
        return string.Join(";", blockData);
    }
    
    void DeserializeProgram(string data)
    {
        // Десериализация (упрощенная)
        string[] blockData = data.Split(';');
        
        foreach (string blockInfo in blockData)
        {
            if (!string.IsNullOrEmpty(blockInfo))
            {
                string[] parts = blockInfo.Split('|');
                if (parts.Length >= 2)
                {
                    // Создаем блок по типу
                    // (нужно расширить для полного восстановления)
                }
            }
        }
    }
    
    // === ИСТОРИЯ ДЕЙСТВИЙ (Undo/Redo) ===
    void RecordAction(EditorAction.ActionType type, CommandBlock block = null)
    {
        var action = new EditorAction
        {
            type = type,
            block = block,
            blockPosition = block != null ? GetBlockPosition(block) : Vector2.zero
        };
        
        undoStack.Push(action);
        redoStack.Clear();
    }
    
    Vector2 GetBlockPosition(CommandBlock block)
    {
        RectTransform rt = block.GetComponent<RectTransform>();
        return rt != null ? rt.anchoredPosition : Vector2.zero;
    }
    
    public void Undo()
    {
        if (undoStack.Count > 0)
        {
            var action = undoStack.Pop();
            HandleUndoAction(action);
            redoStack.Push(action);
            UpdateAllUI();
        }
    }
    
    public void Redo()
    {
        if (redoStack.Count > 0)
        {
            var action = redoStack.Pop();
            HandleRedoAction(action);
            undoStack.Push(action);
            UpdateAllUI();
        }
    }
    
    void HandleUndoAction(EditorAction action)
    {
        switch (action.type)
        {
            case EditorAction.ActionType.CreateBlock:
                if (action.block != null)
                {
                    allBlocks.Remove(action.block);
                    Destroy(action.block.gameObject);
                }
                break;
            case EditorAction.ActionType.DeleteBlock:
                // Восстановление удаленного блока
                break;
            case EditorAction.ActionType.MoveBlock:
                // Возврат на старую позицию
                break;
        }
    }
    
    void HandleRedoAction(EditorAction action)
    {
        switch (action.type)
        {
            case EditorAction.ActionType.CreateBlock:
                // Повторное создание блока
                break;
        }
    }
    
    // === ОБНОВЛЕНИЕ UI ===
    void UpdateAllUI()
    {
        UpdateBlockList();
        UpdateCarStatus();
        UpdateBlockCount();
        UpdateProgramLength();
        UpdateButtonsState();
    }
    
    void UpdateBlockList()
    {
        allBlocks.Clear();
        
        if (blockContainer != null)
        {
            foreach (Transform child in blockContainer)
            {
                CommandBlock block = child.GetComponent<CommandBlock>();
                if (block != null)
                    allBlocks.Add(block);
            }
        }
    }
    
    IEnumerator AutoUpdateCarStatus()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            UpdateCarStatus();
        }
    }
    
    void UpdateCarStatus()
    {
        if (carStatusText != null && carController != null)
        {
            string status = carController.IsCarReady() ? "Готова" : "Не готова";
            Vector2Int chunk = carController.GetCurrentChunkCoordinates();
            Vector2Int cell = carController.GetCurrentCellCoordinates();
            string direction = carController.GetCurrentDirectionName();
            
            carStatusText.text = $"Машинка: {status}\n" +
                               $"Позиция: Чанк({chunk.x},{chunk.y}) Ячейка({cell.x},{cell.y})\n" +
                               $"Направление: {direction}";
        }
    }
    
    void UpdateBlockCount()
    {
        if (blockCountText != null)
        {
            int count = allBlocks.Count - (startBlock != null ? 1 : 0);
            blockCountText.text = $"Блоков: {count}";
        }
    }
    
    void UpdateProgramLength()
    {
        if (programLengthText != null)
        {
            // Подсчет длины программы (количество выполняемых блоков)
            int length = 0;
            CommandBlock current = startBlock;
            
            while (current != null)
            {
                length++;
                current = current.GetNextBlock();
            }
            
            programLengthText.text = $"Длина: {length} команд";
        }
    }
    
    void UpdateButtonsState()
    {
        // Активация/деактивация кнопок в зависимости от состояния
        bool carReady = IsCarReady();
        
        if (runButton != null)
            runButton.interactable = carReady && !isRunning;
        
        if (stopButton != null)
            stopButton.interactable = isRunning;
        
        if (pauseButton != null)
        {
            pauseButton.interactable = isRunning;
            pauseButton.GetComponentInChildren<Text>().text = isPaused ? "Продолжить" : "Пауза";
        }
        
        if (stepButton != null)
            stepButton.interactable = carReady;
        
        if (clearButton != null)
            clearButton.interactable = !isRunning;
        
        if (saveButton != null)
            saveButton.interactable = !isRunning;
        
        if (loadButton != null)
            loadButton.interactable = !isRunning;
    }
    
    void OnSpeedChanged(float value)
    {
        executionSpeed = value;
        if (speedText != null)
            speedText.text = $"Скорость: {value:F1}x";
    }
    
    void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = $"Статус: {message}";
    }
    
    // === ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ ===
    class EditorAction
    {
        public enum ActionType
        {
            CreateBlock,
            DeleteBlock,
            MoveBlock,
            ConnectBlocks,
            DisconnectBlocks
        }
        
        public ActionType type;
        public CommandBlock block;
        public Vector2 blockPosition;
        public ConnectionPoint connectionFrom;
        public ConnectionPoint connectionTo;
    }
    
    enum BlockType
    {
        Move = 0,
        Turn = 1,
        If = 2,
        Loop = 3,
        Wait = 4
    }
}