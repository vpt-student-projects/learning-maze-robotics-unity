using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher instance;
    private static readonly Queue<Action> executionQueue = new Queue<Action>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (instance == null)
        {
            GameObject obj = new GameObject("MainThreadDispatcher");
            instance = obj.AddComponent<MainThreadDispatcher>();
            DontDestroyOnLoad(obj);
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                try
                {
                    Action action = executionQueue.Dequeue();
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in MainThreadDispatcher: {e.Message}");
                }
            }
        }
    }

    public static void ExecuteOnMainThread(Action action)
    {
        if (action == null)
        {
            Debug.LogWarning("Attempted to execute null action on main thread");
            return;
        }

        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    public static bool Exists()
    {
        return instance != null;
    }
}