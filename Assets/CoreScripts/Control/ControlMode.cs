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
    /// Сложный - Управление через API машинкой по нодам (По простому)
    /// </summary>
    API_Nodes,
    
    /// <summary>
    /// Профи - Управление через API машинкой моторчиками (Физически) - пока не реализован
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

