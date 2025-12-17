using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CloneDragManager : MonoBehaviour
{
    public static CloneDragManager Instance { get; private set; }
    
    [Header("Зоны")]
    public RectTransform sourcePanel;    // Откуда берём (SelectionBlocksPanel)
    public RectTransform workPanel;      // Куда бросаем (WorkPanel)
    
    [Header("Настройки")]
    public float cloneOffset = 10f;      // Смещение клона от оригинала
    public Color validDropColor = new Color(0, 1, 0, 0.3f);
    public Color invalidDropColor = new Color(1, 0, 0, 0.3f);
    
    private List<GameObject> activeClones = new List<GameObject>();
    private Image workPanelHighlight;
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
        
        // Создаём подсветку для рабочей панели
        if (workPanel != null)
        {
            GameObject highlightObj = new GameObject("DropZoneHighlight");
            highlightObj.transform.SetParent(workPanel);
            highlightObj.transform.SetAsFirstSibling();
            
            workPanelHighlight = highlightObj.AddComponent<Image>();
            workPanelHighlight.color = Color.clear;
            
            RectTransform rt = highlightObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
    
    // Вызывается DraggableCloneUI при создании клона
    public void RegisterClone(GameObject clone)
    {
        if (!activeClones.Contains(clone))
        {
            activeClones.Add(clone);
            Debug.Log($"Зарегистрирован клон: {clone.name}. Всего: {activeClones.Count}");
        }
    }
    
    // Вызывается при успешном бросании клона
    public void OnCloneDroppedSuccessfully(GameObject clone)
    {
        // Настраиваем клон как постоянный элемент рабочей области
        if (workPanel != null)
        {
            clone.transform.SetParent(workPanel);
            
            // Делаем клон полностью функциональным
            DraggableCloneUI draggable = clone.GetComponent<DraggableCloneUI>();
            if (draggable != null)
            {
                draggable.cloneOnDrag = true; // Теперь этот клон тоже может создавать клоны
                draggable.targetDropZone = workPanel;
                draggable.cloneParent = workPanel;
            }
        }
        
        UpdateWorkPanelHighlight(false);
    }
    
    // Вызывается при уничтожении клона
    public void UnregisterClone(GameObject clone)
    {
        if (activeClones.Contains(clone))
        {
            activeClones.Remove(clone);
        }
    }
    
    // Подсветка рабочей панели при перетаскивании
    public void UpdateWorkPanelHighlight(bool isDragging, Vector2? dragPosition = null)
    {
        if (workPanelHighlight == null) return;
        
        if (isDragging && dragPosition.HasValue)
        {
            // Проверяем, находится ли позиция над рабочей панелью
            bool isOverWorkPanel = RectTransformUtility.RectangleContainsScreenPoint(
                workPanel, dragPosition.Value, null
            );
            
            workPanelHighlight.color = isOverWorkPanel ? validDropColor : invalidDropColor;
        }
        else
        {
            workPanelHighlight.color = Color.clear;
        }
    }
    
    // Очистка всех клонов
    public void ClearAllClones()
    {
        foreach (var clone in activeClones.ToArray())
        {
            if (clone != null)
                Destroy(clone);
        }
        
        activeClones.Clear();
        Debug.Log("Все клоны очищены");
    }
    
    // Получить все блоки в рабочей области
    public List<GameObject> GetWorkPanelBlocks()
    {
        List<GameObject> blocks = new List<GameObject>();
        
        if (workPanel != null)
        {
            foreach (Transform child in workPanel)
            {
                if (child.GetComponent<DraggableCloneUI>() != null)
                    blocks.Add(child.gameObject);
            }
        }
        
        return blocks;
    }
}