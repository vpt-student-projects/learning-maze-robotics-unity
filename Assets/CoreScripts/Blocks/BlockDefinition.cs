using UnityEngine;

public enum BlockType
{
    MoveForward,
    MoveBackward,
    TurnLeft,
    TurnRight,
    // Потом добавишь: Loop, IfWallAhead, etc
}

[CreateAssetMenu(menuName = "Blocks/Block Definition", fileName = "BlockDefinition")]
public class BlockDefinition : ScriptableObject
{
    public BlockType type;
    public string title = "Block";
    public Sprite icon;
}