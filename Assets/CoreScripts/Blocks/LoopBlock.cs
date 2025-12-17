using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class LoopBlock : CommandBlock
{
    [Header("Настройки цикла")]
    public int iterations = 3;
    
    [Header("Контейнер для блоков внутри цикла")]
    public Transform loopContent;
    
    [Header("UI элементы")]
    public InputField iterationsInput;
    public Text counterText;
    public GameObject blockDropZone;
    
    private List<CommandBlock> loopBlocks = new List<CommandBlock>();
    private bool isDraggingBlock = false;
    
    void Start()
    {
        base.Start();
        blockName = "Цикл";
        description = $"Повторить {iterations} раз";
        blockColor = new Color(1f, 0.4f, 1f); // Фиолетовый
        
        if (iterationsInput != null)
        {
            iterationsInput.onEndEdit.AddListener(OnIterationsChanged);
            iterationsInput.text = iterations.ToString();
        }
        
        if (blockDropZone != null)
        {
            // Добавляем триггер для drop зоны
            var dropZone = blockDropZone.AddComponent<LoopDropZone>();
            dropZone.parentLoop = this;
        }
        
        UpdateCounterText();
    }
    
    public override IEnumerator Execute()
    {
        if (loopBlocks.Count == 0)
        {
            Debug.Log("Цикл пустой, пропускаем");
            yield break;
        }
        
        Highlight(true);
        
        for (int i = 0; i < iterations; i++)
        {
            UpdateCounterText(i + 1);
            
            // Выполняем все блоки внутри цикла
            foreach (var block in loopBlocks)
            {
                if (block != null)
                    yield return block.Execute();
                
                // Небольшая пауза между блоками
                yield return new WaitForSeconds(0.05f);
            }
            
            // Пауза между итерациями
            yield return new WaitForSeconds(0.1f);
        }
        
        UpdateCounterText();
        Highlight(false);
    }
    
    // Добавить блок в цикл
    public void AddBlockToLoop(CommandBlock block)
    {
        if (block != null && loopContent != null)
        {
            // Создаем копию блока
            GameObject blockCopy = Instantiate(block.gameObject, loopContent);
            blockCopy.transform.localPosition = Vector3.zero;
            blockCopy.transform.localScale = Vector3.one * 0.8f;
            
            // Получаем компонент CommandBlock
            CommandBlock blockComponent = blockCopy.GetComponent<CommandBlock>();
            if (blockComponent != null)
            {
                loopBlocks.Add(blockComponent);
                
                // Отключаем соединения для блоков внутри цикла
                if (blockComponent.inputPoint != null)
                    blockComponent.inputPoint.gameObject.SetActive(false);
                if (blockComponent.outputPoint != null)
                    blockComponent.outputPoint.gameObject.SetActive(false);
                
                // Обновляем позиционирование
                UpdateBlockPositions();
            }
        }
    }
    
    // Удалить блок из цикла
    public void RemoveBlockFromLoop(CommandBlock block)
    {
        if (block != null && loopBlocks.Contains(block))
        {
            loopBlocks.Remove(block);
            Destroy(block.gameObject);
            UpdateBlockPositions();
        }
    }
    
    // Обновить позиции блоков в цикле
    void UpdateBlockPositions()
    {
        float yOffset = 0f;
        float spacing = 60f;
        
        for (int i = 0; i < loopBlocks.Count; i++)
        {
            if (loopBlocks[i] != null)
            {
                RectTransform rt = loopBlocks[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(0, yOffset);
                    yOffset -= spacing;
                }
            }
        }
    }
    
    void OnIterationsChanged(string value)
    {
        if (int.TryParse(value, out int result))
        {
            iterations = Mathf.Max(1, result);
            description = $"Повторить {iterations} раз";
            
            if (descriptionText != null)
                descriptionText.text = description;
        }
    }
    
    void UpdateCounterText(int currentIteration = 0)
    {
        if (counterText != null)
        {
            if (currentIteration > 0)
                counterText.text = $"Итерация {currentIteration}/{iterations}";
            else
                counterText.text = $"Цикл ({iterations} раз)";
        }
    }
}

// Вспомогательный класс для drop зоны цикла
public class LoopDropZone : MonoBehaviour
{
    public LoopBlock parentLoop;
}