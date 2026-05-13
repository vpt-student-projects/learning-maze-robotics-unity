using System.Collections;
using UnityEngine;

public class WorkspaceProgramRunner : MonoBehaviour
{
    public CarController car;
    public BlockChainManager chain;
    public LidarController lidar;

    private Coroutine runCo;

    public void Run()
    {
        if (runCo != null) StopCoroutine(runCo);
        runCo = StartCoroutine(RunCo());
    }

    public void Stop()
    {
        if (runCo != null) StopCoroutine(runCo);
        runCo = null;
    }

    private IEnumerator RunCo()
    {
        if (car == null)
        {
            Debug.LogError("RUNNER: CarController не назначен!");
            yield break;
        }

        if (chain == null)
        {
            Debug.LogError("RUNNER: BlockChainManager не назначен!");
            yield break;
        }

        if (!car.IsCarReady())
            car.InitializeCar();

        yield return new WaitUntil(() => car.IsCarReady());

        if (chain.workspaceRoot != null)
        {
            chain.RebuildFromWorkspace(chain.workspaceRoot);
            chain.RefreshIfElseBranches();
            Debug.Log($"RUNNER: chain rebuilt, blocks count = {chain.DebugCount}");
        }

        BlockCommand start = chain.FindProgramStart();

        if (start == null)
        {
            Debug.LogWarning("RUNNER: Start программы не найден");
            yield break;
        }

        if (start.type == BlockType.Start)
            start = start.next;

        yield return ExecuteChain(start);

        Debug.Log("RUNNER: done ✅");
        runCo = null;
    }

    private IEnumerator ExecuteChain(BlockCommand start)
    {
        BlockCommand cur = start;
        int step = 0;

        while (cur != null)
        {
            step++;
            Debug.Log($"RUNNER STEP {step}: {cur.name} type={cur.type}");

            yield return Execute(cur);

            cur = cur.next;
        }
    }

    private IEnumerator Execute(BlockCommand cmd)
    {
        while (!car.IsCarReady() || car.isMoving)
            yield return null;

        switch (cmd.type)
        {
            case BlockType.MoveForward:
                car.MoveForward();
                while (car.isMoving) yield return null;
                break;

            case BlockType.MoveBackward:
                car.MoveBackward();
                while (car.isMoving) yield return null;
                break;

            case BlockType.TurnLeft:
                car.TurnLeft();
                yield return new WaitForSeconds(car.rotationAnimationTime + 0.02f);
                break;

            case BlockType.TurnRight:
                car.TurnRight();
                yield return new WaitForSeconds(car.rotationAnimationTime + 0.02f);
                break;

            case BlockType.IfElse:
                bool conditionResult = CheckIfCondition(cmd);

                Debug.Log($"RUNNER IF: result = {conditionResult}");

                if (conditionResult)
                    yield return ExecuteChain(cmd.trueBranchStart);
                else
                    yield return ExecuteChain(cmd.falseBranchStart);

                break;

            default:
                Debug.LogWarning("RUNNER: неизвестный тип блока: " + cmd.type);
                break;
        }
    }

    private bool CheckIfCondition(BlockCommand cmd)
    {
        if (lidar == null)
        {
            Debug.LogError("RUNNER: LidarController не назначен!");
            return false;
        }

        float currentDistance = GetLidarDistance(cmd.lidarSide);

        Debug.Log(
            $"IF CHECK: lidar={cmd.lidarSide}, distance={currentDistance:F3}m, " +
            $"operator={cmd.compare}, target={cmd.distanceMeters:F3}m"
        );

        switch (cmd.compare)
        {
            case CompareOperator.Less:
                return currentDistance < cmd.distanceMeters;

            case CompareOperator.LessOrEqual:
                return currentDistance <= cmd.distanceMeters;

            case CompareOperator.Greater:
                return currentDistance > cmd.distanceMeters;

            case CompareOperator.GreaterOrEqual:
                return currentDistance


>= cmd.distanceMeters;

            case CompareOperator.Equal:
                return Mathf.Approximately(currentDistance, cmd.distanceMeters);

            default:
                return false;
        }
    }

    private float GetLidarDistance(LidarSide side)
    {
        if (lidar == null)
            return -1f;

        for (int i = 0; i < lidar.lidarPoints.Count; i++)
        {
            LidarPoint point = lidar.GetLidarPoint(i);

            if (point == null || !point.enabled || !point.enableSingleLidar)
                continue;

            if (MatchesDirection(point.singleLidarDirection, side))
                return point.singleLidarResult;
        }

        Debug.LogWarning($"RUNNER: не найден SingleLidar для стороны {side}");
        return -1f;
    }

    private bool MatchesDirection(LidarPoint.SingleLidarDirection lidarDirection, LidarSide side)
    {
        switch (side)
        {
            case LidarSide.Forward:
                return lidarDirection == LidarPoint.SingleLidarDirection.Forward;

            case LidarSide.Right:
                return lidarDirection == LidarPoint.SingleLidarDirection.Right;

            case LidarSide.Backward:
                return lidarDirection == LidarPoint.SingleLidarDirection.Backward;

            case LidarSide.Left:
                return lidarDirection == LidarPoint.SingleLidarDirection.Left;

            default:
                return false;
        }
    }
}