/// <summary>
/// Типы управления машинкой в зависимости от сложности
/// </summary>
public enum ControlMode
{
    /// <summary>
    /// Легкий - Управление через блоки
    /// </summary>
    Blocks,
    
    /// <summary>
    /// Средний - Управление через API машинкой по нодам
    /// </summary>
    API_Nodes,
    
    /// <summary>
    /// Сложный / Профи - Управление через API скоростью моторов колёс (WheelCollider)
    /// </summary>
    API_Motors
}

/// <summary>
/// Уровни сложности лабиринта
/// </summary>
public enum DifficultyLevel
{
    Easy,    // Легкий
    Medium,  // Средний
    Hard,    // Сложный
    Pro      // Профи (пока не реализован)
}

