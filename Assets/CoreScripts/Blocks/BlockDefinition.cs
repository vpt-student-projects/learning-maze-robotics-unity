using UnityEngine;

public enum BlockType
{
    Start,
    MoveForward,
    MoveBackward,
    TurnLeft,
    TurnRight,
    IfElse
}
public enum LidarSide
{
    Forward,
    Right,
    Backward,
    Left
}

public enum CompareOperator
{
    LessOrEqual,
    GreaterOrEqual,
    Less,
    Greater,
    Equal
}

[CreateAssetMenu(menuName = "Blocks/Block Definition", fileName = "BlockDefinition")]
public class BlockDefinition : ScriptableObject
{
    public BlockType type;
    public string title = "Block";
    public Sprite icon;
}