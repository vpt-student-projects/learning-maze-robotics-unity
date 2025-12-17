using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[System.Serializable]
public class BlockConnection
{
    public CommandBlock fromBlock;
    public CommandBlock toBlock;
    public LineRenderer lineRenderer;
}

public class ConnectionPoint : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public enum PointType { Input, Output }
    
    [Header("Настройки")]
    public PointType pointType = PointType.Output;
    public CommandBlock parentBlock;
    
    [Header("Соединения")]
    public ConnectionPoint connectedTo; // С кем соединена эта точка
    
    [Header("Визуальные")]
    public Image pointImage;
    public Color normalColor = Color.white;
    public Color hoverColor = Color.yellow;
    public Color connectedColor = Color.green;
    
    [Header("Линия соединения")]
    public LineRenderer lineRenderer;
    private bool isDragging = false;
    
    void Start()
    {
        if (pointImage != null)
        {
            pointImage.color = normalColor;
        }
        
        if (parentBlock == null)
        {
            parentBlock = GetComponentInParent<CommandBlock>();
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (pointType == PointType.Output)
        {
            StartDraggingConnection();
        }
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDragging)
        {
            EndDraggingConnection(eventData);
        }
    }
    
    void StartDraggingConnection()
    {
        isDragging = true;
        
        // Создаем новую линию
        CreateConnectionLine();
        
        if (pointImage != null)
        {
            pointImage.color = hoverColor;
        }
    }
    
    void EndDraggingConnection(PointerEventData eventData)
    {
        isDragging = false;
        
        // Проверяем, был ли дроп на другой точке соединения
        GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;
        
        if (dropTarget != null)
        {
            ConnectionPoint targetPoint = dropTarget.GetComponent<ConnectionPoint>();
            
            // Проверяем что это входная точка и она не та же самая
            if (targetPoint != null && 
                targetPoint.pointType == PointType.Input && 
                targetPoint != this)
            {
                // Соединяем точки
                CreateConnection(targetPoint);
            }
            else
            {
                // Удаляем линию, если соединение не удалось
                DestroyConnectionLine();
            }
        }
        else
        {
            DestroyConnectionLine();
        }
        
        if (pointImage != null)
        {
            pointImage.color = normalColor;
        }
    }
    
    void CreateConnectionLine()
    {
        if (lineRenderer != null)
        {
            Destroy(lineRenderer.gameObject);
        }
        
        GameObject lineObj = new GameObject("ConnectionLine");
        lineRenderer = lineObj.AddComponent<LineRenderer>();
        
        // Настройки линии
        lineRenderer.startWidth = 3f;
        lineRenderer.endWidth = 3f;
        lineRenderer.positionCount = 2;
        
        Material lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.material = lineMaterial;
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.red;
    }
    
    void CreateConnection(ConnectionPoint targetPoint)
    {
        // Разрываем старое соединение если есть
        if (connectedTo != null)
        {
            connectedTo.connectedTo = null;
        }
        
        // Устанавливаем новое соединение
        connectedTo = targetPoint;
        targetPoint.connectedTo = this;
        
        // Обновляем цвета
        if (pointImage != null) pointImage.color = connectedColor;
        if (targetPoint.pointImage != null) targetPoint.pointImage.color = connectedColor;
        
        // Устанавливаем родительский блок
        if (parentBlock != null && targetPoint.parentBlock != null)
        {
            parentBlock.nextBlock = targetPoint.parentBlock;
        }
    }
    
    void DestroyConnectionLine()
    {
        if (lineRenderer != null)
        {
            Destroy(lineRenderer.gameObject);
            lineRenderer = null;
        }
    }
    
    void Update()
    {
        if (isDragging && lineRenderer != null)
        {
            // Обновляем линию до курсора мыши
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = 10; // Отступ от камеры
            
            Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(mousePos);
            
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, worldMousePos);
        }
        else if (lineRenderer != null && connectedTo != null)
        {
            // Обновляем линию между соединенными точками
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, connectedTo.transform.position);
        }
    }
    
    // Разорвать соединение
    public void Disconnect()
    {
        if (connectedTo != null)
        {
            if (connectedTo.pointImage != null)
            {
                connectedTo.pointImage.color = connectedTo.normalColor;
            }
            
            connectedTo.connectedTo = null;
            connectedTo = null;
        }
        
        if (pointImage != null)
        {
            pointImage.color = normalColor;
        }
        
        DestroyConnectionLine();
    }
    
    // Проверить, есть ли соединение
    public bool IsConnected()
    {
        return connectedTo != null;
    }
}