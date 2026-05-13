using UnityEngine;

public class BlockCommand : MonoBehaviour
{
    public BlockType type = BlockType.MoveForward;
    public int repeat = 1;

    [HideInInspector] public BlockCommand prev;
    [HideInInspector] public BlockCommand next;

    [Header("IfElse")]
    public LidarSide lidarSide = LidarSide.Right;
    public CompareOperator compare = CompareOperator.LessOrEqual;
    public float distanceMeters = 0.04f;

    [HideInInspector] public BlockCommand trueBranchStart;
    [HideInInspector] public BlockCommand falseBranchStart;
}
