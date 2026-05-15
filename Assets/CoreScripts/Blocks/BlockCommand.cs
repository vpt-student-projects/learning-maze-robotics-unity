using System.Collections.Generic;
using UnityEngine;

public class BlockCommand : MonoBehaviour
{
    public BlockType type = BlockType.MoveForward;
    public int repeat = 1;

    [HideInInspector] public BlockCommand prev;
    [HideInInspector] public BlockCommand next;

    [Header("IfElse Main Condition")]
    public LidarSide lidarSide = LidarSide.Right;
    public CompareOperator compare = CompareOperator.LessOrEqual;
    public float distanceMeters = 0.04f;

    [Header("IfElse Branches")]
    [HideInInspector] public BlockCommand trueBranchStart;
    [HideInInspector] public BlockCommand falseBranchStart;

    [System.Serializable]
    public class IfConditionData
    {
        public LogicOperator logic = LogicOperator.And;
        public LidarSide side = LidarSide.Right;
        public CompareOperator compare = CompareOperator.LessOrEqual;
        public float distanceMeters = 0.04f;
    }

    [Header("IfElse Extra Conditions")]
    public List<IfConditionData> conditions = new List<IfConditionData>();
}