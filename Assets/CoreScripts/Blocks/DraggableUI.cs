using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class DraggableCloneUI : MonoBehaviour, 
    IBeginDragHandler, 
    IDragHandler, 
    IEndDragHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Настройки клонирования")]
    public bool cloneOnDrag = true;          // Создавать клон при перетаскивании?
    public bool destroyCloneOnReturn = true; // Удалять клон при возврате?
    public bool returnOriginalOnRelease = false; // Возвращать оригинал на место?
    
    [Header("Настройки перетаскивания")]
    public bool isDraggable = true;
    public bool snapToGrid = false;
    public float gridSize = 50f;
    
    [Header("Ограничения")]
    public bool constrainToParent = false; // Для клона - обычно false
    public float dragAlpha = 0.7f;
    
    [Header("Ссылки")]
    public Transform targetDropZone;       // Куда можно бросать клоны (WorkPanel)
    public Transform cloneParent;          // Родитель для клонов
    
    [Header("События")]
    public UnityEngine.Events.UnityEvent<GameObject> onCloneCreated;
    public UnityEngine.Events.UnityEvent<GameObject> onCloneDropped;
    public UnityEngine.Events.UnityEvent<GameObject> onCloneDestroyed;
    
    // Приватные переменные
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas parentCanvas;
    private Vector2 originalPosition;
    private Transform originalParent;
    private bool isDragging = false;
    
    // Клон
    private GameObject currentClone;
    private DraggableCloneUI cloneDraggable;
    
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        parentCanvas = GetComponentInParent<Canvas>();
        
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        // Если не задан родитель для клонов, используем родителя канваса
        if (cloneParent == null)
            cloneParent = parentCanvas.transform;
    }
    
    // === СОЗДАНИЕ КЛОНА ===
    GameObject CreateClone()
    {
        // Создаём клон
        currentClone = Instantiate(gameObject, cloneParent);
        
        // Настраиваем позицию (рядом с оригиналом или под курсором)
        RectTransform cloneRT = currentClone.GetComponent<RectTransform>();
        cloneRT.anchoredPosition = rectTransform.anchoredPosition;
        cloneRT.localScale = Vector3.one;
        
        // Настраиваем DraggableCloneUI у клона
        cloneDraggable = currentClone.GetComponent<DraggableCloneUI>();
        if (cloneDraggable != null)
        {
            // У клона НЕ создаём ещё клоны (чтобы не было рекурсии)
            cloneDraggable.cloneOnDrag = false;
            cloneDraggable.returnOriginalOnRelease = false;
            cloneDraggable.constrainToParent = false;
            cloneDraggable.targetDropZone = targetDropZone;
        }
        
        // Убираем все ненужные компоненты у клона
        CleanupCloneComponents(currentClone);
        
        // Делаем клон полупрозрачным
        CanvasGroup cloneCG = currentClone.GetComponent<CanvasGroup>();
        if (cloneCG != null)
        {
            cloneCG.alpha = dragAlpha;
        }
        
        // Событие
        onCloneCreated?.Invoke(currentClone);
        
        Debug.Log($"Создан клон: {currentClone.name}");
        return currentClone;
    }
    
    void CleanupCloneComponents(GameObject clone)
    {
        // Удаляем DraggableCloneUI с оригинала если это клон оригинала
        var originalCloneComp = clone.GetComponent<DraggableCloneUI>();
        if (originalCloneComp != null && originalCloneComp != this)
        {
            // Сохраняем настройки
            bool wasDraggable = originalCloneComp.isDraggable;
            Destroy(originalCloneComp);
            
            // Добавляем свежий компонент
            var newComp = clone.AddComponent<DraggableCloneUI>();
            newComp.cloneOnDrag = false;
            newComp.isDraggable = wasDraggable;
        }
        
        // Можно удалить другие компоненты, которые не нужны у клона
        // Например, скрипты кнопок если они есть
        Button btn = clone.GetComponent<Button>();
        if (btn != null) Destroy(btn);
    }
    
    // === ОБРАБОТЧИКИ СОБЫТИЙ ===
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isDraggable) return;
        
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        
        // Поднимаем оригинал на верхний слой
        transform.SetAsLastSibling();
        
        // Визуальная обратная связь для оригинала
        canvasGroup.alpha = 0.5f;
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isDraggable) return;
        
        isDragging = true;
        
        // Создаём клон если нужно
        if (cloneOnDrag)
        {
            CreateClone();
            
            // Переносим drag на клон
            if (currentClone != null)
            {
                // Блокируем оригинал
                canvasGroup.blocksRaycasts = false;
                
                // Начинаем перетаскивать клон
                cloneDraggable?.StartDragFromOriginal(eventData.position);
                return;
            }
        }
        
        // Если клон не создан, перетаскиваем оригинал
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = dragAlpha;
    }
    
    // Метод для начала перетаскивания клона с позиции
    public void StartDragFromOriginal(Vector2 startPosition)
    {
        if (rectTransform == null) return;
        
        // Устанавливаем позицию клона под курсор
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            startPosition,
            parentCanvas.worldCamera,
            out Vector2 localPoint
        );
        
        rectTransform.anchoredPosition = localPoint;
        
        // Начинаем перетаскивание
        isDragging = true;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = dragAlpha;
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || !isDraggable) return;
        
        // Если есть клон - двигаем клон
        if (currentClone != null && cloneDraggable != null)
        {
            cloneDraggable.HandleDrag(eventData);
            return;
        }
        
        // Иначе двигаем оригинал
        HandleDrag(eventData);
    }
    
    void HandleDrag(PointerEventData eventData)
    {
        Vector2 newPosition = rectTransform.anchoredPosition + 
                             eventData.delta / (parentCanvas != null ? parentCanvas.scaleFactor : 1f);
        
        // Прилипание к сетке
        if (snapToGrid)
        {
            newPosition.x = Mathf.Round(newPosition.x / gridSize) * gridSize;
            newPosition.y = Mathf.Round(newPosition.y / gridSize) * gridSize;
        }
        
        rectTransform.anchoredPosition = newPosition;
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        
        isDragging = false;
        
        // Если перетаскивали клон
        if (currentClone != null && cloneDraggable != null)
        {
            cloneDraggable.HandleEndDrag(eventData);
            
            // Проверяем, куда бросили клон
            bool droppedInValidZone = IsInValidDropZone(currentClone.transform.position);
            
            if (droppedInValidZone)
            {
                // Клон остаётся в рабочей области
                OnCloneSuccessfullyDropped();
            }
            else
            {
                // Клон вне рабочей области - уничтожаем
                DestroyClone();
            }
        }
        else
        {
            // Перетаскивали оригинал
            HandleEndDrag(eventData);
        }
        
        // Восстанавливаем оригинал
        RestoreOriginal();
    }
    
    void HandleEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
    }
    
    void OnCloneSuccessfullyDropped()
    {
        // Делаем клон полностью видимым
        if (cloneDraggable != null)
        {
            cloneDraggable.canvasGroup.alpha = 1f;
            cloneDraggable.canvasGroup.blocksRaycasts = true;
            cloneDraggable.isDraggable = true; // Теперь клон можно перетаскивать
            
            // Событие
            onCloneDropped?.Invoke(currentClone);
        }
        
        Debug.Log($"Клон успешно помещён в рабочую область");
        
        // Отсоединяем ссылку на клон (теперь он самостоятельный)
        currentClone = null;
        cloneDraggable = null;
    }
    
    bool IsInValidDropZone(Vector3 position)
    {
        if (targetDropZone == null) return true; // Если зона не задана, разрешаем везде
        
        RectTransform dropRect = targetDropZone as RectTransform;
        if (dropRect == null) return true;
        
        // Преобразуем мировую позицию в локальную позицию относительно dropZone
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dropRect,
            Camera.main.WorldToScreenPoint(position),
            null,
            out localPoint
        );
        
        // Проверяем, находится ли точка внутри RectTransform
        return dropRect.rect.Contains(localPoint);
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDragging) return; // Если было перетаскивание, OnEndDrag уже обработал
        
        // Просто клик без перетаскивания
        RestoreOriginal();
    }
    
    void RestoreOriginal()
    {
        // Возвращаем оригинал на место если нужно
        if (returnOriginalOnRelease)
        {
            rectTransform.anchoredPosition = originalPosition;
            transform.SetParent(originalParent);
        }
        
        // Восстанавливаем видимость оригинала
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        
        // Если клон не был успешно помещён, уничтожаем его
        if (currentClone != null && cloneDraggable != null && 
            !cloneDraggable.isDragging && cloneDraggable.canvasGroup.alpha < 0.9f)
        {
            DestroyClone();
        }
    }
    
    void DestroyClone()
    {
        if (currentClone != null)
        {
            onCloneDestroyed?.Invoke(currentClone);
            Destroy(currentClone);
            Debug.Log($"Клон уничтожен");
        }
        
        currentClone = null;
        cloneDraggable = null;
    }
    
    // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===
    
    public void SetDraggable(bool draggable)
    {
        isDraggable = draggable;
        
        if (!draggable && isDragging)
        {
            OnEndDrag(new PointerEventData(EventSystem.current));
        }
    }
    
    public bool IsDragging()
    {
        return isDragging;
    }
    
    public GameObject GetCurrentClone()
    {
        return currentClone;
    }
    
    // Для внешнего управления
    public void StartDragManually()
    {
        if (!isDraggable) return;
        
        OnPointerDown(new PointerEventData(EventSystem.current));
        OnBeginDrag(new PointerEventData(EventSystem.current));
    }
    
    public void StopDragManually()
    {
        OnEndDrag(new PointerEventData(EventSystem.current));
    }
}