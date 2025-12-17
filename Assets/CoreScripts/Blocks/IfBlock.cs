using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class IfBlock : CommandBlock
{
    public enum Condition { 
        WallAhead,      // Стена впереди
        NoWallAhead,    // Нет стены впереди
        WallLeft,       // Стена слева
        WallRight,      // Стена справа
        AtFinish        // На финише (можно добавить позже)
    }
    
    [Header("Условие")]
    public Condition condition = Condition.WallAhead;
    
    [Header("Ветки")]
    public Transform trueBranch;   // Блоки если условие ИСТИНА
    public Transform falseBranch;  // Блоки если условие ЛОЖЬ (опционально)
    
    [Header("UI элементы")]
    public Dropdown conditionDropdown;
    public GameObject trueDropZone;
    public GameObject falseDropZone;
    
    private List<CommandBlock> trueBlocks = new List<CommandBlock>();
    private List<CommandBlock> falseBlocks = new List<CommandBlock>();
    
    [Header("Ссылки")]
    public CarController carController;
    
    void Start()
    {
        base.Start();
        blockName = "Условие";
        description = GetConditionDescription();
        blockColor = new Color(0.4f, 1f, 0.4f); // Зеленый
        
        if (carController == null)
            carController = FindObjectOfType<CarController>();
        
        if (conditionDropdown != null)
        {
            conditionDropdown.onValueChanged.AddListener(OnConditionChanged);
            
            // Настраиваем опции dropdown
            conditionDropdown.ClearOptions();
            List<string> options = new List<string>
            {
                "Если стена впереди",
                "Если НЕТ стены впереди",
                "Если стена слева",
                "Если стена справа",
                "Если на финише"
            };
            conditionDropdown.AddOptions(options);
        }
        
        // Настраиваем drop зоны
        if (trueDropZone != null)
        {
            var dropZone = trueDropZone.AddComponent<IfDropZone>();
            dropZone.parentIf = this;
            dropZone.isTrueBranch = true;
        }
        
        if (falseDropZone != null)
        {
            var dropZone = falseDropZone.AddComponent<IfDropZone>();
            dropZone.parentIf = this;
            dropZone.isTrueBranch = false;
        }
    }
    
    public override IEnumerator Execute()
    {
        if (carController == null || !carController.IsCarReady())
        {
            Debug.LogError("CarController не готов!");
            yield break;
        }
        
        Highlight(true);
        
        // Проверяем условие
        bool conditionResult = CheckCondition();
        
        Debug.Log($"Условие: {GetConditionDescription()} = {conditionResult}");
        
        List<CommandBlock> blocksToExecute = conditionResult ? trueBlocks : falseBlocks;
        
        if (blocksToExecute.Count > 0)
        {
            // Выполняем блоки выбранной ветки
            foreach (var block in blocksToExecute)
            {
                if (block != null)
                    yield return block.Execute();
                
                // Небольшая пауза между блоками
                yield return new WaitForSeconds(0.05f);
            }
        }
        
        Highlight(false);
    }
    
    bool CheckCondition()
    {
        // Здесь нужен доступ к проверке стен из CarController
        // Пока используем заглушку
        
        switch (condition)
        {
            case Condition.WallAhead:
                // Нужен метод в CarController: public bool CheckWallAhead()
                // return carController.CheckWallAhead();
                return Random.value > 0.5f; // Заглушка
                
            case Condition.NoWallAhead:
                // return !carController.CheckWallAhead();
                return Random.value > 0.5f; // Заглушка
                
            case Condition.WallLeft:
                // Нужен метод: public bool CheckWallLeft()
                return Random.value > 0.5f; // Заглушка
                
            case Condition.WallRight:
                // Нужен метод: public bool CheckWallRight()
                return Random.value > 0.5f; // Заглушка
                
            case Condition.AtFinish:
                // Проверка достижения финиша
                return false; // Заглушка
                
            default:
                return false;
        }
    }
    
    string GetConditionDescription()
    {
        switch (condition)
        {
            case Condition.WallAhead: return "Если стена впереди";
            case Condition.NoWallAhead: return "Если НЕТ стены впереди";
            case Condition.WallLeft: return "Если стена слева";
            case Condition.WallRight: return "Если стена справа";
            case Condition.AtFinish: return "Если на финише";
            default: return "Условие";
        }
    }
    
    void OnConditionChanged(int index)
    {
        condition = (Condition)index;
        description = GetConditionDescription();
        
        if (descriptionText != null)
            descriptionText.text = description;
    }
    
    // Добавить блок в true ветку
    public void AddBlockToTrueBranch(CommandBlock block)
    {
        AddBlockToBranch(block, true);
    }
    
    // Добавить блок в false ветку
    public void AddBlockToFalseBranch(CommandBlock block)
    {
        AddBlockToBranch(block, false);
    }
    
    void AddBlockToBranch(CommandBlock block, bool isTrueBranch)
    {
        if (block == null) return;
        
        Transform branch = isTrueBranch ? trueBranch : falseBranch;
        List<CommandBlock> branchList = isTrueBranch ? trueBlocks : falseBlocks;
        
        if (branch != null)
        {
            // Создаем копию блока
            GameObject blockCopy = Instantiate(block.gameObject, branch);
            blockCopy.transform.localPosition = Vector3.zero;
            blockCopy.transform.localScale = Vector3.one * 0.7f;
            
            // Получаем компонент CommandBlock
            CommandBlock blockComponent = blockCopy.GetComponent<CommandBlock>();
            if (blockComponent != null)
            {
                branchList.Add(blockComponent);
                
                // Отключаем соединения для блоков внутри условия
                if (blockComponent.inputPoint != null)
                    blockComponent.inputPoint.gameObject.SetActive(false);
                if (blockComponent.outputPoint != null)
                    blockComponent.outputPoint.gameObject.SetActive(false);
                
                // Обновляем позиционирование
                UpdateBranchPositions(isTrueBranch);
            }
        }
    }
    
    void UpdateBranchPositions(bool isTrueBranch)
    {
        Transform branch = isTrueBranch ? trueBranch : falseBranch;
        List<CommandBlock> branchList = isTrueBranch ? trueBlocks : falseBlocks;
        
        float xOffset = isTrueBranch ? -100f : 100f;
        float yOffset = 0f;
        float spacing = 50f;
        
        for (int i = 0; i < branchList.Count; i++)
        {
            if (branchList[i] != null)
            {
                RectTransform rt = branchList[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(xOffset, yOffset);
                    yOffset -= spacing;
                }
            }
        }
    }
}

// Вспомогательный класс для drop зоны условия
public class IfDropZone : MonoBehaviour
{
    public IfBlock parentIf;
    public bool isTrueBranch;
}