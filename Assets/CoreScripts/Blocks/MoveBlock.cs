using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MoveBlock : CommandBlock
{
    public enum Direction { Forward, Backward }
    
    [Header("Настройки движения")]
    public Direction moveDirection = Direction.Forward;
    public float distance = 1.0f; // В "нодах" лабиринта
    
    [Header("UI элементы")]
    public Dropdown directionDropdown;
    public InputField distanceInput;
    
    [Header("Ссылки")]
    public CarController carController;
    
    void Start()
    {
        base.Start();
        blockName = "Движение";
        description = "Перемещает машинку";
        blockColor = new Color(0.2f, 0.8f, 0.2f); // Зеленый
        
        if (carController == null)
            carController = FindObjectOfType<CarController>();
        
        if (directionDropdown != null)
        {
            directionDropdown.onValueChanged.AddListener(OnDirectionChanged);
        }
        
        if (distanceInput != null)
        {
            distanceInput.onEndEdit.AddListener(OnDistanceChanged);
            distanceInput.text = distance.ToString();
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
        
        // Выполняем движение в зависимости от направления
        if (moveDirection == Direction.Forward)
        {
            // Выполняем несколько шагов вперед
            for (int i = 0; i < distance; i++)
            {
                if (carController.IsCarReady())
                {
                    carController.MoveForward();
                    
                    // Ждем завершения движения
                    yield return new WaitUntil(() => !IsCarMoving());
                    yield return new WaitForSeconds(0.1f); // Небольшая пауза
                }
                else
                {
                    break;
                }
            }
        }
        else // Backward
        {
            // Выполняем несколько шагов назад
            for (int i = 0; i < distance; i++)
            {
                if (carController.IsCarReady())
                {
                    carController.MoveBackward();
                    
                    // Ждем завершения движения
                    yield return new WaitUntil(() => !IsCarMoving());
                    yield return new WaitForSeconds(0.1f); // Небольшая пауза
                }
                else
                {
                    break;
                }
            }
        }
        
        Highlight(false);
    }
    
    // Проверяем, движется ли машинка
    private bool IsCarMoving()
    {
        // Здесь нужен доступ к приватному полю isMoving из CarController
        // Добавь в CarController публичное свойство:
        // public bool IsMoving => isMoving;
        
        // Временное решение через рефлексию (не рекомендуется для продакшена)
        var type = carController.GetType();
        var field = type.GetField("isMoving", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
            return (bool)field.GetValue(carController);
        
        return false;
    }
    
    void OnDirectionChanged(int index)
    {
        moveDirection = (Direction)index;
        
        // Обновляем описание
        description = moveDirection == Direction.Forward ? 
            $"Движение вперед на {distance} шагов" : 
            $"Движение назад на {distance} шагов";
            
        if (descriptionText != null)
            descriptionText.text = description;
    }
    
    void OnDistanceChanged(string value)
    {
        if (int.TryParse(value, out int result))
        {
            distance = Mathf.Max(1, result);
            
            // Обновляем описание
            description = moveDirection == Direction.Forward ? 
                $"Движение вперед на {distance} шагов" : 
                $"Движение назад на {distance} шагов";
                
            if (descriptionText != null)
                descriptionText.text = description;
        }
    }
}