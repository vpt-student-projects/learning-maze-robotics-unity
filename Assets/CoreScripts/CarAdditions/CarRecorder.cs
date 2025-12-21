using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CarAction
{
    public string action;      // "move_forward", "turn_left" и т.д.
    public float timestamp;    // время с начала записи
}

public class CarRecorder : MonoBehaviour
{
    private List<CarAction> actions = new List<CarAction>();
    private float startTime;
    private bool recording = false;

    public void StartRecording()
    {
        actions.Clear();
        startTime = Time.time;
        recording = true;
    }

    public void StopRecording()
    {
        recording = false;
    }

    public void RecordAction(string action)
    {
        if (!recording) return;

        actions.Add(new CarAction
        {
            action = action,
            timestamp = Time.time - startTime
        });
    }

    public List<CarAction> GetRecordedActions()
    {
        return actions;
    }

    // Простейшее воспроизведение
    public void PlayRecording(CarController car)
    {
        StartCoroutine(PlaybackCoroutine(car));
    }

    private System.Collections.IEnumerator PlaybackCoroutine(CarController car)
    {
        foreach (var act in actions)
        {
            yield return new WaitForSeconds(act.timestamp);
            switch (act.action)
            {
                case "move_forward": car.MoveForward(); break;
                case "move_backward": car.MoveBackward(); break;
                case "turn_left": car.TurnLeft(); break;
                case "turn_right": car.TurnRight(); break;
            }
        }
    }
}
