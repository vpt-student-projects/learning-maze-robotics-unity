using System.Collections;
using UnityEngine;

public class WorkspaceProgramRunner : MonoBehaviour
{
    public CarController car;
    public BlockChainManager chain;

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
    {
        Debug.LogWarning("RUNNER: car.IsCarReady()=false -> InitializeCar()");
        car.InitializeCar();
    }

    yield return new WaitUntil(() => car.IsCarReady());
    Debug.Log("RUNNER: car ready ✅");

    // 🔥 ГЛАВНЫЙ ФИКС: перед запуском пересобираем blocks из воркспейса
    if (chain.workspaceRoot != null)
    {
        chain.RebuildFromWorkspace(chain.workspaceRoot);
        Debug.Log($"RUNNER: chain rebuilt, blocks count = {chain.DebugCount}");
    }
    else
    {
        Debug.LogWarning("RUNNER: chain.workspaceRoot не назначен (WorkspaceContent)!");
    }

    var cur = chain.FindProgramStart();
    if (cur == null)
    {
        Debug.LogWarning("RUNNER: Start программы не найден. (цепочка пустая или не связана)");
        yield break;
    }

    Debug.Log($"RUNNER: start block = {cur.name}, type = {cur.type}");

    if (cur.type == BlockType.Start)
        cur = cur.next;

    if (cur == null)
    {
        Debug.LogWarning("RUNNER: После Start нет команд (Start.next == null)");
        yield break;
    }

    int step = 0;
    while (cur != null)
    {
        step++;
        Debug.Log($"RUNNER STEP {step}: {cur.name} type={cur.type}");
        yield return Execute(cur);
        cur = cur.next;
    }

    Debug.Log("RUNNER: done ✅");
    runCo = null;
}


    private IEnumerator Execute(BlockCommand cmd)
    {
        // ждём, пока не двигается
        while (!car.IsCarReady() || car.isMoving) yield return null;

        switch (cmd.type)
        {
            case BlockType.MoveForward:
                car.MoveForward();                      // внутри есть проверки ready/moving/rotating【:contentReference[oaicite:2]{index=2}】
                while (car.isMoving) yield return null;  // ждём пока доедет【:contentReference[oaicite:3]{index=3}】
                break;

            case BlockType.MoveBackward:
                car.MoveBackward();
                while (car.isMoving) yield return null;
                break;

            case BlockType.TurnLeft:
                car.TurnLeft();
                yield return new WaitForSeconds(car.rotationAnimationTime + 0.02f); // isRotating приватный【:contentReference[oaicite:4]{index=4}】
                break;

            case BlockType.TurnRight:
                car.TurnRight();
                yield return new WaitForSeconds(car.rotationAnimationTime + 0.02f);
                break;

            default:
                Debug.LogWarning("RUNNER: неизвестный тип блока: " + cmd.type);
                break;
        }
    }
}
