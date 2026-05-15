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
        if (runCo != null)
            StopCoroutine(runCo);

        runCo = StartCoroutine(RunCo());
    }

    public void Stop()
    {
        if (runCo != null)
            StopCoroutine(runCo);

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

        // Даём лидарам обновиться после спавна/генерации
        yield return null;
        yield return null;

        // Перед запуском выбираем нормальный живой лидар
        lidar = FindBestLiveLidarController();

        if (lidar == null)
        {
            Debug.LogError("RUNNER: не найден рабочий LidarController!");
            yield break;
        }

        Debug.Log($"RUNNER: selected lidar = {lidar.name}");

        if (chain.workspaceRoot != null)
        {
            chain.RebuildFromWorkspace(chain.workspaceRoot);
            chain.RefreshIfElseBranches();

            Debug.Log($"RUNNER: chain rebuilt, blocks count = {chain.DebugCount}");
        }
        else
        {
            Debug.LogWarning("RUNNER: chain.workspaceRoot не назначен!");
        }

        BlockCommand start = chain.FindProgramStart();

        if (start == null)
        {
            Debug.LogWarning("RUNNER: Start программы не найден");
            runCo = null;
            yield break;
        }

        Debug.Log($"RUNNER: start block = {start.name}, type = {start.type}");

        if (start.type == BlockType.Start)
            start = start.next;

        if (start == null)
        {
            Debug.LogWarning("RUNNER: после Start нет команд");
            runCo = null;
            yield break;
        }

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
        if (cmd == null)
            yield break;

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
                bool result = CheckIfCondition(cmd);

                Debug.Log($"RUNNER IF: result = {result}");

                if (result)
                {
                    Debug.Log(
                        $"RUNNER IF TRUE BRANCH: " +
                        $"{(cmd.trueBranchStart != null ? cmd.trueBranchStart.name : "empty")}"
                    );

                    yield return ExecuteChain(cmd.trueBranchStart);
                }
                else
                {
                    Debug.Log(
                        $"RUNNER IF ELSE BRANCH: " +
                        $"{(cmd.falseBranchStart != null ? cmd.falseBranchStart.name : "empty")}"
                    );

                    yield return ExecuteChain(cmd.falseBranchStart);
                }

                break;

            default:
                Debug.LogWarning("RUNNER: неизвестный тип блока: " + cmd.type);
                break;
        }
    }

    private bool CheckIfCondition(BlockCommand cmd)
    {
        IfElseBlockUI ifUI = cmd.GetComponent<IfElseBlockUI>();

        if (ifUI != null)
            ifUI.ApplyConditionsToCommand();

        bool result = CheckSingleCondition(
            cmd.lidarSide,
            cmd.compare,
            cmd.distanceMeters
        );

        Debug.Log($"IF MAIN: {cmd.lidarSide} {cmd.compare} {cmd.distanceMeters} => {result}");

        if (cmd.conditions != null)
        {
            for (int i = 0; i < cmd.conditions.Count; i++)
            {
                BlockCommand.IfConditionData condition = cmd.conditions[i];

                bool current = CheckSingleCondition(
                    condition.side,
                    condition.compare,
                    condition.distanceMeters
                );

                Debug.Log(
                    $"IF EXTRA {i}: {condition.logic} {condition.side} " +
                    $"{condition.compare} {condition.distanceMeters} => {current}"
                );

                if (condition.logic == LogicOperator.And)
                    result = result && current;
                else
                    result = result || current;
            }
        }

        return result;
    }

    private bool CheckSingleCondition(LidarSide side, CompareOperator compare, float targetDistance)
    {
        float currentDistance = GetLidarDistance(side);

        Debug.Log($"IF CHECK VALUE: {side} distance={currentDistance:F2} target={targetDistance:F2}");

        switch (compare)
        {
            case CompareOperator.Less:
                return currentDistance < targetDistance;

            case CompareOperator.LessOrEqual:
                return currentDistance <= targetDistance;

            case CompareOperator.Greater:
                return currentDistance > targetDistance;

            case CompareOperator.GreaterOrEqual:
                return currentDistance >= targetDistance;

            case CompareOperator.Equal:
                return Mathf.Approximately(currentDistance, targetDistance);

            default:
                return false;
        }
    }

    private float GetLidarDistance(LidarSide side)
    {
        string pointName = GetLidarPointName(side);

        // Каждый раз проверяем, что выбранный лидар живой.
        // Если он показывает нули, выбираем другой.
        if (!IsLiveLidarController(lidar))
            lidar = FindBestLiveLidarController();

        if (lidar == null)
        {
            Debug.LogError($"RUNNER LIDAR ERROR: нет живого LidarController для {pointName}");
            return 999f;
        }

        LidarPoint point = FindPoint(lidar, pointName);

        if (point == null)
        {
            Debug.LogError($"RUNNER LIDAR ERROR: точка {pointName} не найдена в {lidar.name}");
            return 999f;
        }

        Debug.Log(
            $"LIDAR READ: controller={lidar.name}, side={side}, " +
            $"point={point.name}, value={point.singleLidarResult:F2}m"
        );

        return point.singleLidarResult;
    }

    private LidarController FindBestLiveLidarController()
    {
        LidarController[] controllers = FindObjectsByType<LidarController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        LidarController best = null;
        float bestScore = -1f;

        foreach (LidarController controller in controllers)
        {
            if (controller == null)
                continue;

            float score = GetLidarLiveScore(controller);

            Debug.Log($"LIDAR CANDIDATE: controller={controller.name}, score={score:F2}");

            if (score > bestScore)
            {
                bestScore = score;
                best = controller;
            }
        }

        if (best != null)
        {
            Debug.Log($"LIDAR SELECTED: controller={best.name}, score={bestScore:F2}");
        }

        return best;
    }

    private bool IsLiveLidarController(LidarController controller)
    {
        if (controller == null)
            return false;

        float score = GetLidarLiveScore(controller);

        // Если score почти ноль, это старый/нерабочий CarBody.
        return score > 0.01f;
    }

    private float GetLidarLiveScore(LidarController controller)
    {
        if (controller == null)
            return 0f;

        float score = 0f;

        score += GetPointValue(controller, "LidarF");
        score += GetPointValue(controller, "LidarR");
        score += GetPointValue(controller, "LidarL");
        score += GetPointValue(controller, "LidarB");

        return score;
    }

    private float GetPointValue(LidarController controller, string pointName)
    {
        LidarPoint point = FindPoint(controller, pointName);

        if (point == null)
            return 0f;

        // Нули игнорируем, потому что у тебя CarBody отдаёт 0.00 и ломает условие.
        if (point.singleLidarResult <= 0.001f)
            return 0f;

        return point.singleLidarResult;
    }

    private LidarPoint FindPoint(LidarController controller, string pointName)
    {
        if (controller == null || controller.lidarPoints == null)
            return null;

        for (int i = 0; i < controller.lidarPoints.Count; i++)
        {
            LidarPoint point = controller.lidarPoints[i];

            if (point == null)
                continue;

            if (point.name == pointName)
                return point;
        }

        return null;
    }

    private string GetLidarPointName(LidarSide side)
    {
        switch (side)
        {
            case LidarSide.Forward:
                return "LidarF";

            case LidarSide.Right:
                return "LidarR";

            case LidarSide.Left:
                return "LidarL";

            case LidarSide.Backward:
                return "LidarB";

            default:
                return "LidarF";
        }
    }
}