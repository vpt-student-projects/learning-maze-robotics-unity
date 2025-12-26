using UnityEngine;

public class BlockCommand : MonoBehaviour
{
    public BlockType type = BlockType.MoveForward;
    public int repeat = 1;

    [HideInInspector] public BlockCommand prev;
    [HideInInspector] public BlockCommand next;
}
