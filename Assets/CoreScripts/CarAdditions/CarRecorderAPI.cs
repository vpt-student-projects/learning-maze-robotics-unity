using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class MovementRecord
{
    public string action;
    public string timestamp;
    public float time_sec;
    public Vector2Int position;
}

public class CarRecorderAPI : MonoBehaviour
{
    private float logStartTime = -1f;
    public List<MovementRecord> movementLog = new List<MovementRecord>();

    public void LogMovement(string action, Vector2Int position)
    {
        if (logStartTime < 0f)
            logStartTime = Time.time;

        movementLog.Add(new MovementRecord
        {
            action = action,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            time_sec = Time.time - logStartTime,
            position = position
        });
    }

    public List<MovementRecord> GetMovementLog()
    {
        return movementLog;
    }

    public void ClearLog()
    {
        movementLog.Clear();
        logStartTime = -1f;
    }
}
