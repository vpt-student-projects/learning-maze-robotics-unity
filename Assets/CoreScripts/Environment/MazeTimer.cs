using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

public class MazeTimer : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI timerText;
    public Image timerBackground;
    public Button restartButton;

    [Header("Timer Settings")]
    public Color readyColor = Color.green;
    public Color runningColor = Color.yellow;
    public Color finishedColor = Color.cyan;

    [Header("References")]
    public CarController carController;
    public MazeGenerator mazeGenerator;

    private bool isTimerRunning = false;
    private bool hasStartedMoving = false;
    private bool hasReachedFinish = false;
    private float elapsedTime = 0f;

    // Для отслеживания финишной зоны
    private Vector2Int finishChunk;
    private Vector2Int finishCellStart;
    private bool hasFinishArea = false;

    void Start()
    {
        // Автопоиск ссылок, если не назначены в инспекторе
        if (mazeGenerator == null)
        {
            mazeGenerator = FindObjectOfType<MazeGenerator>();
        }
        if (carController == null)
        {
            carController = FindObjectOfType<CarController>();
        }

        InitializeTimer();

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartButtonClick);
        }

        StartCoroutine(InitializeFinishArea());
    }

    IEnumerator InitializeFinishArea()
    {
        yield return new WaitUntil(() => mazeGenerator != null);
        yield return new WaitUntil(() => mazeGenerator.GetMazeData() != null);

        RefreshFinishArea();
    }

    // Публично: можно вызывать из MazeGenerator после каждой генерации, чтобы таймер "знал" актуальный финиш
    public void RefreshFinishArea()
    {
        if (mazeGenerator == null || mazeGenerator.GetMazeData() == null)
        {
            hasFinishArea = false;
            return;
        }

        if (mazeGenerator.createFinishArea)
        {
            var mazeData = mazeGenerator.GetMazeData();
            finishChunk = mazeData.StartGenerationChunk;
            finishCellStart = new Vector2Int(
                mazeData.StartGenerationCell.x - 1,
                mazeData.StartGenerationCell.y - 1
            );
            hasFinishArea = true;

            Debug.Log($"🎯 Финишная зона обновлена: Chunk({finishChunk.x},{finishChunk.y}), Cells({finishCellStart.x},{finishCellStart.y}) to ({finishCellStart.x + 1},{finishCellStart.y + 1})");
        }
        else
        {
            hasFinishArea = false;
            Debug.Log("⚠️ Финишная зона отключена в настройках лабиринта");
        }
    }

    void InitializeTimer()
    {
        isTimerRunning = false;
        hasStartedMoving = false;
        hasReachedFinish = false;
        elapsedTime = 0f;

        UpdateTimerDisplay();
        SetTimerState(false, true);
    }

    void Update()
    {
        if (hasReachedFinish) return; // Если уже достигли финиша, ничего не делаем

        if (isTimerRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimerDisplay();

            // Проверяем достижение финиша только если есть финишная зона
            if (hasFinishArea && carController != null && carController.IsCarReady())
            {
                CheckFinishCondition();
            }
        }
    }

    // ВЫЗЫВАЕТСЯ ИЗ CarController ПРИ ЛЮБОМ ДВИЖЕНИИ ИЛИ ПОВОРОТЕ
    public void OnCarAction()
    {
        if (!hasStartedMoving)
        {
            Debug.Log("⏱️ Первое действие машинки - запуск таймера!");
            StartTimer();
        }
    }

    public void StartTimer()
    {
        if (!hasStartedMoving)
        {
            hasStartedMoving = true;
            isTimerRunning = true;
            SetTimerState(true, false);

            SaveStartPosition();

            Debug.Log("⏱️ Таймер запущен!");
        }
    }

    public void StopTimer()
    {
        if (isTimerRunning)
        {
            isTimerRunning = false;
            SetTimerState(false, true);
            Debug.Log($"⏱️ Таймер остановлен. Время: {FormatTime(elapsedTime)}");
        }
    }

    public void ResetTimer()
    {
        InitializeTimer();
        Debug.Log("🔄 Таймер сброшен");
    }

    private void CheckFinishCondition()
    {
        if (!hasFinishArea || hasReachedFinish) return;

        var currentChunk = carController.GetCurrentChunkCoordinates();
        var currentCell = carController.GetCurrentCellCoordinates();

        if (IsAtFinishArea(currentChunk, currentCell))
        {
            hasReachedFinish = true;
            StopTimer();
            timerBackground.color = finishedColor;
            UpdateTimerDisplay();
            Debug.Log($"🎉 ФИНИШ! Машинка достигла центра лабиринта! Время: {FormatTime(elapsedTime)}");
        }
    }

    private bool IsAtFinishArea(Vector2Int chunk, Vector2Int cell)
    {
        if (!hasFinishArea) return false;

        // Проверяем совпадение чанка
        if (chunk.x != finishChunk.x || chunk.y != finishChunk.y)
            return false;

        // Проверяем попадание в квадрат 2x2 финишной зоны
        bool inFinishArea = cell.x >= finishCellStart.x && cell.x <= finishCellStart.x + 1 &&
                           cell.y >= finishCellStart.y && cell.y <= finishCellStart.y + 1;

        if (inFinishArea)
        {
            Debug.Log($"📍 Машинка в финишной зоне: Chunk({chunk.x},{chunk.y}) Cell({cell.x},{cell.y})");
        }

        return inFinishArea;
    }

    private void SaveStartPosition()
    {
        if (carController != null && carController.IsCarReady())
        {
            var chunk = carController.GetCurrentChunkCoordinates();
            var cell = carController.GetCurrentCellCoordinates();
            Debug.Log($"📌 Сохранена стартовая позиция: Chunk({chunk.x},{chunk.y}) Cell({cell.x},{cell.y})");
        }
    }

    public void ResetCarPosition()
    {
        if (carController != null && carController.IsCarReady())
        {
            // ВСЕГДА возвращаем в правый нижний угол (0,0)
            int startChunkX = 0;
            int startChunkZ = 0;
            int startCellX = 0;
            int startCellZ = 0;

            // Телепортируем машинку
            carController.SetCarPosition(startChunkX, startChunkZ, startCellX, startCellZ);

            // Сбрасываем направление
            carController.ResetDirection();

            Debug.Log($"🚗 Машинка возвращена в правый нижний угол: Chunk({startChunkX},{startChunkZ}) Cell({startCellX},{startCellZ})");

            // Сохраняем эту позицию как стартовую
            SaveStartPosition();
        }
        else
        {
            Debug.LogWarning("⚠️ Не удалось вернуть машинку: контроллер не готов");

            // Пытаемся найти машинку
            if (carController == null)
            {
                carController = FindObjectOfType<CarController>();
                if (carController != null)
                {
                    StartCoroutine(DelayedReset());
                }
            }
        }
    }
    private IEnumerator DelayedReset()
    {
        yield return new WaitForSeconds(0.5f);
        ResetCarPosition();
    }

    public void OnRestartButtonClick()
    {
        Debug.Log("🔄 Нажата кнопка рестарта");

        // 1. Возвращаем машинку в правый нижний угол
        ResetCarPosition();

        // 2. Сбрасываем таймер
        ResetTimer();

        // 3. Сбрасываем флаги
        hasReachedFinish = false;
        hasStartedMoving = false;

        Debug.Log("✅ Рестарт завершен: машинка в правом нижнем углу, таймер сброшен");
    }

    private void UpdateTimerDisplay()
    {
        if (timerText != null)
        {
            if (!hasStartedMoving)
            {
                timerText.text = "Готов";
            }
            else if (hasReachedFinish)
            {
                timerText.text = $"Финиш: {FormatTime(elapsedTime)}";
            }
            else
            {
                timerText.text = FormatTime(elapsedTime);
            }
        }
    }

    private void SetTimerState(bool running, bool ready)
    {
        if (timerBackground != null)
        {
            if (hasReachedFinish)
            {
                timerBackground.color = finishedColor;
            }
            else if (running)
            {
                timerBackground.color = runningColor;
            }
            else if (ready)
            {
                timerBackground.color = readyColor;
            }
        }
    }

    private string FormatTime(float time)
    {
        TimeSpan timeSpan = TimeSpan.FromSeconds(time);

        if (timeSpan.TotalMinutes >= 1)
        {
            return string.Format("{0:00}:{1:00}.{2:00}",
                (int)timeSpan.TotalMinutes,
                timeSpan.Seconds,
                timeSpan.Milliseconds / 10);
        }
        else
        {
            return string.Format("{0:00}.{1:00}",
                timeSpan.Seconds,
                timeSpan.Milliseconds / 10);
        }
    }

    // Методы для внешнего доступа
    public bool IsRunning => isTimerRunning;
    public float CurrentTime => elapsedTime;
    public bool HasReachedFinish => hasReachedFinish;
    public bool HasStarted => hasStartedMoving;

    public void ForceStartTimer()
    {
        StartTimer();
    }

    public void ForceStopTimer()
    {
        StopTimer();
    }

    // Вызывается из CarController при любом действии
    public void CarActionPerformed()
    {
        OnCarAction();
    }

    void OnDestroy()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(OnRestartButtonClick);
        }
    }
}