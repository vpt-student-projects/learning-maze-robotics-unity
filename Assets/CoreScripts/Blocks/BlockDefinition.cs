using UnityEngine;

public enum BlockType
{
    Start,
    MoveForward,
    MoveBackward,
    TurnLeft,
    TurnRight
}


[CreateAssetMenu(menuName = "Blocks/Block Definition", fileName = "BlockDefinition")]
public class BlockDefinition : ScriptableObject
{
    public BlockType type;
    public string title = "Block";
    public Sprite icon;
}