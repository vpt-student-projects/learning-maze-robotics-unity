using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public abstract class CommandBlock : MonoBehaviour
{
    [Header("Базовые настройки")]
    public string blockName = "Блок";
    public string description = "Описание блока";
    public Color blockColor = Color.white;
    
    [Header("Соединения")]
    public ConnectionPoint inputPoint;
    public ConnectionPoint outputPoint;
    public CommandBlock nextBlock;
    
    [Header("UI элементы")]
    public Image background;
    public Text titleText;
    public Text descriptionText;
    
    public bool isExecuting = false;
    
    // Инициализация блока
    protected virtual void Start()
    {
        if (background != null)
            background.color = blockColor;
        
        if (titleText != null)
            titleText.text = blockName;
        
        if (descriptionText != null)
            descriptionText.text = description;
    }
    
    // Абстрактный метод выполнения
    public abstract IEnumerator Execute();
    
    // Визуальная подсветка
    public virtual void Highlight(bool active)
    {
        if (background != null)
        {
            background.color = active ? 
                Color.Lerp(blockColor, Color.yellow, 0.3f) : 
                blockColor;
        }
        
        isExecuting = active;
    }
    
    // Получить следующий блок
    public CommandBlock GetNextBlock()
    {
        // Проверяем соединение через outputPoint
        if (outputPoint != null && outputPoint.connectedTo != null)
        {
            // Получаем родительский блок подключенной точки
            return outputPoint.connectedTo.parentBlock;
        }
        
        // Альтернативно используем прямое поле nextBlock
        return nextBlock;
    }
    
    // Установить следующий блок
    public void SetNextBlock(CommandBlock block)
    {
        nextBlock = block;
    }
    
    // Сброс состояния
    public virtual void ResetState()
    {
        Highlight(false);
        isExecuting = false;
    }
    
    // Метод для клонирования блока
    public virtual CommandBlock Clone()
    {
        GameObject cloneObj = Instantiate(gameObject);
        CommandBlock clone = cloneObj.GetComponent<CommandBlock>();
        clone.ResetState();
        return clone;
    }
    
    // Получить цвет блока
    public Color GetBlockColor()
    {
        return blockColor;
    }
    
    // Получить название блока
    public string GetBlockName()
    {
        return blockName;
    }
}